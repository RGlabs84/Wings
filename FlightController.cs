using HarmonyLib;
using UnityEngine;

namespace WingsoftheValkyrie
{
    [HarmonyPatch(typeof(Player))]
    public static class FlightController
    {
        // Glide state lives only on the owning client, so the owner publishes it on its ZDO for
        // everyone else to read. The flap counter is a monotonic tick: remote clients replay one
        // flap animation each time it changes.
        private static readonly int ZdoGliding = "wotv_gliding".GetStableHashCode();
        private static readonly int ZdoFlapCount = "wotv_flapcount".GetStableHashCode();

        [HarmonyPatch("Update")]
        [HarmonyPostfix]
        public static void UpdatePostfix(Player __instance)
        {
            if (!ModConfig.EnableMod.Value) return;

            try
            {
                var vfx = __instance.GetComponent<WingsoftheValkyrie.VFX.RuneWingVFX>();
                if (vfx == null) vfx = __instance.gameObject.AddComponent<WingsoftheValkyrie.VFX.RuneWingVFX>();

                bool isLocal = __instance == Player.m_localPlayer;
                string wingsName = WingsItem.GetEquippedWingsName(__instance);

                if (wingsName == null)
                {
                    vfx.IsGlidingLocal = false;
                }
                else
                {
                    vfx.SetTierColor(GetTierColor(wingsName));

                    if (isLocal)
                    {
                        UpdateLocalGlide(__instance, vfx, wingsName);
                        FlyingSkill.AccumulateGlideXP(__instance, vfx.IsGlidingLocal, Time.deltaTime);
                    }
                    else ReadRemoteState(__instance, vfx);
                }

                // Published unconditionally so unequipping mid-air clears the flag for everyone.
                if (isLocal) PublishState(vfx);

                vfx.SetGlidingState(vfx.IsGlidingLocal);
            }
            catch (System.Exception ex)
            {
                Jotunn.Logger.LogWarning($"[Wings of the Valkyrie] Error in UpdatePostfix for {__instance?.GetPlayerName()}: {ex.Message}");
            }
        }

        private static Color GetTierColor(string wingsName)
        {
            switch (wingsName)
            {
                case WingsItem.TrollName: return Color.cyan;
                case WingsItem.LoxName: return new Color(0.8f, 0.8f, 1f);
                case WingsItem.DragonName: return new Color(0.8f, 0.1f, 1f);
                default: return Color.green;
            }
        }

        private static void UpdateLocalGlide(Player player, WingsoftheValkyrie.VFX.RuneWingVFX vfx, string wingsName)
        {
            // A player standing on a moving ship is not IsOnGround() (the deck is a rigidbody,
            // not terrain), so wave pitch/bob can spike vertical velocity past the fall-speed
            // threshold below and wrongly deploy the wings. GetStandingOnShip() is true for
            // anyone on deck, steering or not, and is a plain physical-state query so it is safe
            // to check for local and remote players alike.
            if (player.IsOnGround() || player.IsSwimming() || player.InWater() || player.GetStandingOnShip() != null)
            {
                vfx.IsGlidingLocal = false;
                return;
            }

            if (ValkyrieInput.JumpPressed(player))
            {
                float staminaCost = ModConfig.GetStats(wingsName).FlapStaminaCost
                                    * (1f - ModConfig.SkillStaminaReduction.Value * FlyingSkill.Factor(player));

                if (player.HaveStamina(staminaCost))
                {
                    player.UseStamina(staminaCost);
                    FlyingSkill.AddFlapXP(player);
                    vfx.TriggerFlap();
                    vfx.WantsToFlap = true;
                    vfx.FlapCount++;
                }
                else
                {
                    player.Message(MessageHud.MessageType.Center, "Not enough stamina to flap!");
                }
            }

            if (!vfx.IsGlidingLocal)
            {
                // Only auto-deploy if falling fast, or if the user actively flaps
                if (vfx.WantsToFlap || player.GetVelocity().y < -5f)
                {
                    vfx.IsGlidingLocal = true;
                }
            }

            if (vfx.IsGlidingLocal)
            {
                Traverse.Create(player).Field("m_maxAirAltitude").SetValue(player.transform.position.y);
            }
        }

        private static void PublishState(WingsoftheValkyrie.VFX.RuneWingVFX vfx)
        {
            var nview = vfx.NView;
            if (nview == null || !nview.IsValid() || !nview.IsOwner()) return;

            ZDO zdo = nview.GetZDO();
            if (zdo == null) return;

            if (zdo.GetBool(ZdoGliding, false) != vfx.IsGlidingLocal)
                zdo.Set(ZdoGliding, vfx.IsGlidingLocal);

            if (zdo.GetInt(ZdoFlapCount, 0) != vfx.FlapCount)
                zdo.Set(ZdoFlapCount, vfx.FlapCount);
        }

        private static void ReadRemoteState(Player player, WingsoftheValkyrie.VFX.RuneWingVFX vfx)
        {
            var nview = vfx.NView;
            ZDO zdo = (nview != null && nview.IsValid()) ? nview.GetZDO() : null;

            if (zdo == null || !zdo.GetBool(ZdoGliding, out bool gliding))
            {
                // Owner is on a build that does not publish state (VersionStrictness is Minor,
                // so a 1.1.x mismatch is possible). Fall back to guessing from vertical motion.
                gliding = !player.IsOnGround() && !player.IsSwimming() && !player.InWater()
                          && player.GetStandingOnShip() == null
                          && Mathf.Abs(player.GetVelocity().y) > 2f;
                vfx.IsGlidingLocal = gliding;
                return;
            }

            vfx.IsGlidingLocal = gliding;

            int flaps = zdo.GetInt(ZdoFlapCount, 0);
            if (vfx.LastSeenFlapCount == int.MinValue)
            {
                // First sighting: adopt the count so we do not replay their whole flight history.
                vfx.LastSeenFlapCount = flaps;
            }
            else if (flaps != vfx.LastSeenFlapCount)
            {
                vfx.LastSeenFlapCount = flaps;
                vfx.TriggerFlap();
            }
        }

        [HarmonyPatch("FixedUpdate")]
        [HarmonyPostfix]
        public static void FixedUpdatePostfix(Player __instance)
        {
            if (__instance != Player.m_localPlayer) return;
            if (!ModConfig.EnableMod.Value) return;

            try
            {
                var vfx = __instance.GetComponent<WingsoftheValkyrie.VFX.RuneWingVFX>();
                if (vfx == null) return;

                if (vfx.IsGlidingLocal && !__instance.IsOnGround() && !__instance.IsSwimming() && !__instance.InWater() && __instance.GetStandingOnShip() == null)
                {
                    Rigidbody rb = __instance.GetComponent<Rigidbody>();
                    if (rb != null)
                    {
                        WingStats stats = ModConfig.GetStats(WingsItem.GetEquippedWingsName(__instance));
                        float skillFactor = FlyingSkill.Factor(__instance);
                        float glideSpeed = stats.GlideSpeed * (1f + ModConfig.SkillGlideSpeedBonus.Value * skillFactor);
                        float ceilingLimit = stats.FlightCeiling;
                        float flapForce = stats.FlapForce * (1f + ModConfig.SkillFlapPowerBonus.Value * skillFactor);

                        if (vfx.WantsToFlap)
                        {
                            rb.linearVelocity = new Vector3(rb.linearVelocity.x, flapForce, rb.linearVelocity.z); // Upward lift burst
                            vfx.WantsToFlap = false;
                        }
                        else
                        {
                            // Automatic descent based on look direction. Skill flattens the slow
                            // glide (longer flights) but leaves the full -20f dive available --
                            // diving is player intent, not something practice should weaken.
                            Vector3 lookDir = __instance.GetLookDir();
                            float baseSink = -2f * (1f - ModConfig.SkillGlideSinkReduction.Value * skillFactor);
                            float targetDescent = baseSink;
                            if (lookDir.y < 0)
                            {
                                // If looking down, increase descent speed based on how sharply they are looking down (up to -20f)
                                targetDescent = Mathf.Lerp(baseSink, -20f, -lookDir.y);
                            }

                            if (rb.linearVelocity.y < targetDescent)
                            {
                                rb.linearVelocity = new Vector3(rb.linearVelocity.x, targetDescent, rb.linearVelocity.z);
                            }
                        }

                        Vector3 moveDir = Traverse.Create(__instance).Field("m_moveDir").GetValue<Vector3>();
                        if (moveDir.magnitude > 0.1f)
                        {
                            Vector3 targetVelocity = moveDir * glideSpeed;
                            targetVelocity.y = rb.linearVelocity.y;

                            rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, targetVelocity, 3f * Time.fixedDeltaTime);
                        }

                        if (ZoneSystem.instance != null)
                        {
                            float groundHeight = ZoneSystem.instance.GetGroundHeight(__instance.transform.position);

                            if (__instance.transform.position.y - groundHeight > ceilingLimit)
                            {
                                if (rb.linearVelocity.y > 0)
                                {
                                    rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
                                }
                            }
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                Jotunn.Logger.LogWarning($"[Wings of the Valkyrie] Error in FixedUpdatePostfix for {__instance?.GetPlayerName()}: {ex.Message}");
            }
        }
    }
}
