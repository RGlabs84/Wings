using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace WingsoftheValkyrie
{
    /// <summary>
    /// The "Valkyrie Flight" custom skill. Level scales flap stamina cost, flap lift, glide
    /// sink rate and glide speed (applied in <see cref="FlightController"/>); XP comes from
    /// flapping and from time spent gliding. Skill progress lives in the player save like any
    /// vanilla skill, so the death penalty and skill-loss protection come for free.
    ///
    /// Registration no longer goes through Jotunn's SkillManager, but the SkillType value is
    /// computed exactly as Jotunn computed it -- Math.Abs of the identifier's stable hash. That
    /// is not a stylistic choice: Skills.Save writes the skill under its integer SkillType, so
    /// a different number would orphan every level every player has earned since 1.9.0.
    /// </summary>
    [HarmonyPatch]
    public static class FlyingSkill
    {
        // Must never change. It is the identity of the skill in every player's save file.
        private const string Identifier = "wubarrk.wingsofthevalkyrie.flying";

        private const string SkillName = "Valkyrie Flight";
        private const string SkillDescription = "Mastery of the Valkyrie's wings. Cheaper, stronger wingbeats and longer glides.";

        public static Skills.SkillType SkillType { get; private set; } = Skills.SkillType.None;

        /// <summary>The localisation key vanilla builds for a skill's display name:
        /// SkillsDialog does Localize("$skill_" + m_skill.ToString().ToLower()), and for a value
        /// outside the enum ToString() is just the number.</summary>
        private static string LocalizationKey;

        private static Skills.SkillDef _definition;

        // Localization.AddWord is private, so there is no supported way to add a translation.
        private static readonly MethodInfo AddWordMethod =
            AccessTools.Method(typeof(Localization), "AddWord", new[] { typeof(string), typeof(string) });

        // Read instead of Localization.instance, which BUILDS the singleton on first touch --
        // its constructor loads the language resources and runs SetupLanguage. Doing that from a
        // BepInEx Awake would drag the whole localisation system up before the game asks for it.
        private static readonly FieldInfo LocalizationInstanceField =
            AccessTools.Field(typeof(Localization), "m_instance");

        // Glide XP is awarded in whole-second ticks; the fractional remainder carries across
        // glides so short hops still add up instead of being rounded away every landing.
        private static float _glideSeconds;

        public static void Register()
        {
            try
            {
                // Jotunn refused any identifier whose hash landed inside the vanilla enum's
                // range, and so do we: a collision would silently share a save slot with a
                // real skill.
                int hash = Math.Abs(Identifier.GetStableHashCode());
                if (hash <= (int)Skills.SkillType.All)
                {
                    Log.LogError($"The Valkyrie Flight skill identifier hashes to {hash}, which collides with vanilla's skill range. The skill will not be registered.");
                    return;
                }

                SkillType = (Skills.SkillType)hash;
                LocalizationKey = "skill_" + hash.ToString();

                _definition = new Skills.SkillDef
                {
                    m_skill = SkillType,
                    m_description = SkillDescription,
                    m_increseStep = 1f,   // vanilla's spelling
                    m_icon = IconLoader.Load("skill_valkyrie_flight.png")
                };

                AddLocalizationWord();
                Log.LogInfo($"Valkyrie Flight registered as skill {hash}.");
            }
            catch (Exception ex)
            {
                SkillType = Skills.SkillType.None;
                Log.LogError($"Could not register the Valkyrie Flight skill; flight will work but will not level. Reason: {ex}");
            }
        }

        /// <summary>
        /// Teaches Localization the skill's name. Missing translations come back as "[skill_123]"
        /// and are cached, so this has to be in place before anything asks -- and again after
        /// every language switch, because SetupLanguage clears the whole table. It is therefore
        /// called from three places, and does nothing if the table is not built yet.
        /// </summary>
        private static void AddLocalizationWord()
        {
            if (AddWordMethod == null || LocalizationKey == null || LocalizationInstanceField == null) return;

            var localization = LocalizationInstanceField.GetValue(null) as Localization;
            if (localization == null) return;

            AddWordMethod.Invoke(localization, new object[] { LocalizationKey, SkillName });
        }

        [HarmonyPatch(typeof(Localization), "SetupLanguage")]
        [HarmonyPostfix]
        private static void SetupLanguagePostfix()
        {
            AddLocalizationWord();
        }

        /// <summary>
        /// Vanilla's Skills.Load reads every stored skill and then throws away any whose
        /// SkillType fails IsSkillValid -- and vanilla's IsSkillValid is nothing more than
        /// Enum.IsDefined against Skills.SkillType. A custom skill's hash is by definition not
        /// in that enum, so without this patch every level a player has earned is read off the
        /// save and silently discarded on load, and written back out at zero the next time the
        /// character is saved. Jotunn carried this patch; dropping Jotunn in 2.1.0 dropped it
        /// too, which is what reset players' Valkyrie Flight. Adding the SkillDef to
        /// Skills.m_skills does NOT cover this -- Load never consults that list.
        /// </summary>
        [HarmonyPatch(typeof(Skills), "IsSkillValid")]
        [HarmonyPostfix]
        private static void IsSkillValidPostfix(Skills.SkillType type, ref bool __result)
        {
            if (__result || SkillType == Skills.SkillType.None) return;
            if (type == SkillType) __result = true;
        }

        /// <summary>
        /// Skills.m_skills is the per-character list SkillDefs are looked up in, and it is
        /// deep-copied onto every Player instance, so the definition has to be added per
        /// character rather than once globally.
        /// </summary>
        [HarmonyPatch(typeof(Skills), "Awake")]
        [HarmonyPostfix]
        private static void SkillsAwakePostfix(Skills __instance)
        {
            if (_definition == null || __instance == null || __instance.m_skills == null) return;

            // A character exists, so localisation certainly does. This is the pass that matters
            // if the language was set up before our patches were applied.
            AddLocalizationWord();

            foreach (Skills.SkillDef existing in __instance.m_skills)
            {
                if (existing != null && existing.m_skill == SkillType) return;
            }

            __instance.m_skills.Add(_definition);
        }

        /// <summary>Whether the skill actually registered. When it did not, nothing can raise it,
        /// so every gate that depends on it has to stand aside rather than lock a player out of
        /// flight they can no longer earn their way into.</summary>
        public static bool IsAvailable => SkillType != Skills.SkillType.None;

        /// <summary>Skill factor 0..1 (level/100) used to scale flight stats. 0 when unavailable.</summary>
        public static float Factor(Player player)
        {
            if (player == null || SkillType == Skills.SkillType.None) return 0f;

            Skills skills = player.GetSkills();
            return skills != null ? skills.GetSkillFactor(SkillType) : 0f;
        }

        /// <summary>Skill level 0..100, as shown in the skills panel. 0 when unavailable, which
        /// is the safe answer: an unregistered skill must not hand out flight it did not earn.</summary>
        public static float Level(Player player)
        {
            if (player == null || SkillType == Skills.SkillType.None) return 0f;

            Skills skills = player.GetSkills();
            return skills != null ? skills.GetSkillLevel(SkillType) : 0f;
        }

        /// <summary>
        /// Whether the patch that keeps this skill through a save/load round trip is actually
        /// attached. It is checked rather than assumed because when it silently was not -- which
        /// is what 2.1.0 shipped -- nothing anywhere said so, and players lost levels for three
        /// releases before a bug report arrived.
        /// </summary>
        public static bool PersistenceGuaranteed { get; private set; }

        // Skills.GetSkill(SkillType) is private and is the only thing that CREATES the skill
        // entry for a character that has none -- which is the case both for a first-time flier
        // and for one whose entry the loader threw away.
        private static readonly MethodInfo GetSkillMethod =
            AccessTools.Method(typeof(Skills), "GetSkill", new[] { typeof(Skills.SkillType) });

        /// <summary>
        /// Confirms after PatchAll that our IsSkillValid postfix really did attach to vanilla's
        /// method. A Valheim update that renames or inlines it, or another mod that replaces it,
        /// would otherwise put the mod straight back into silently discarding levels.
        /// </summary>
        public static void VerifyPersistencePatch()
        {
            PersistenceGuaranteed = false;

            MethodInfo target = AccessTools.Method(typeof(Skills), "IsSkillValid");
            if (target != null)
            {
                HarmonyLib.Patches info = Harmony.GetPatchInfo(target);
                if (info != null && info.Postfixes != null)
                {
                    foreach (Patch patch in info.Postfixes)
                    {
                        if (patch.owner == WingsoftheValkyriePlugin.PluginGUID) { PersistenceGuaranteed = true; break; }
                    }
                }
            }

            if (PersistenceGuaranteed) return;

            Log.LogError(
                "Skills.IsSkillValid is NOT patched, so Valheim will discard the Valkyrie Flight level " +
                "every time a character loads. This is the 2.1.0-2.1.2 bug returning, most likely because " +
                "a Valheim update changed that method or another mod replaced it. Please report this.");
        }

        /// <summary>
        /// The character's skill entry if they already have one, WITHOUT creating it. Callers
        /// that only want to read must use this: <see cref="GetEntry"/> goes through vanilla's
        /// private GetSkill, which adds an entry as a side effect, and that would quietly give
        /// the skill to every character that has never once put on a pair of wings.
        /// </summary>
        internal static Skills.Skill FindEntry(Player player)
        {
            if (player == null || SkillType == Skills.SkillType.None) return null;

            Skills skills = player.GetSkills();
            if (skills == null) return null;

            List<Skills.Skill> list = skills.GetSkillList();
            if (list == null) return null;

            foreach (Skills.Skill skill in list)
            {
                if (skill != null && skill.m_info != null && skill.m_info.m_skill == SkillType) return skill;
            }

            return null;
        }

        /// <summary>The character's skill entry, created if they do not have one yet.</summary>
        internal static Skills.Skill GetEntry(Skills skills)
        {
            if (GetSkillMethod == null || skills == null || SkillType == Skills.SkillType.None) return null;

            try
            {
                return GetSkillMethod.Invoke(skills, new object[] { SkillType }) as Skills.Skill;
            }
            catch (Exception ex)
            {
                Log.LogWarning($"Could not reach the Valkyrie Flight skill entry: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Whether the character carries this skill at all. Deliberately asks about the ENTRY
        /// rather than the level: a level of zero is ambiguous -- a beginner has one too -- while
        /// a missing entry after a load is the unmistakable signature of the loader dropping it.
        /// </summary>
        internal static bool HasEntry(Player player) => FindEntry(player) != null;

        internal static bool SetLevel(Player player, float level, float accumulator)
        {
            if (player == null) return false;

            Skills.Skill entry = GetEntry(player.GetSkills());
            if (entry == null) return false;

            entry.m_level = Mathf.Clamp(level, 0f, 100f);
            entry.m_accumulator = Mathf.Max(accumulator, 0f);
            return true;
        }

        /// <summary>
        /// Whether XP earned now would still be there tomorrow. If the vanilla patch is missing
        /// AND the logbook mirror cannot stand in for it, levelling is stopped rather than left
        /// to quietly build progress that the next load will delete.
        /// </summary>
        private static bool XpWillStick => PersistenceGuaranteed || FlyingSkillMirror.CanMirror;

        public static void AddFlapXP(Player player)
        {
            if (player == null || SkillType == Skills.SkillType.None || !XpWillStick) return;

            float xp = ModConfig.SkillXpPerFlap.Value;
            if (xp > 0f) player.RaiseSkill(SkillType, xp);
        }

        public static void AccumulateGlideXP(Player player, bool gliding, float deltaTime)
        {
            if (!gliding || player == null || SkillType == Skills.SkillType.None || !XpWillStick) return;

            _glideSeconds += deltaTime;
            if (_glideSeconds < 1f) return;
            _glideSeconds -= 1f;

            float xp = ModConfig.SkillXpPerGlideSecond.Value;
            if (xp > 0f) player.RaiseSkill(SkillType, xp);
        }
    }
}
