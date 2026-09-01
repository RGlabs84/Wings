using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace WingsoftheValkyrie
{
    /// <summary>
    /// Builds and registers the four wing items without Jotunn.
    ///
    /// The shape of this is dictated by three vanilla facts (see
    /// libs-Tools/VALHEIM-API-REFERENCE/01-ITEM-CLONING-AND-REGISTRATION.md):
    ///
    ///  1. There is no "add item" API. You append to <c>ObjectDB.m_items</c> and re-run the
    ///     private <c>UpdateRegisters()</c>, and you write straight into the private
    ///     <c>ZNetScene.m_namedPrefabs</c> -- <c>m_prefabs</c> is only read inside its Awake.
    ///  2. A runtime <c>Instantiate</c> of a prefab produces a LIVE object: ZNetView.Awake
    ///     creates a real ZDO at world origin and replicates it. The only way to keep a template
    ///     inert is to parent it under a GameObject that was deactivated before the parenting.
    ///  3. <c>ObjectDB.Awake</c> and <c>ZNetScene.Awake</c> have no guaranteed order, and
    ///     <c>ObjectDB.CopyOtherDB</c> replaces both m_items and m_recipes BY REFERENCE. So every
    ///     step below is idempotent and is attempted from all three patch points.
    ///
    /// The clones are still cut from vanilla capes, because a cape prefab is the only source of
    /// a correct item chassis (ZNetView, Rigidbody, ZSyncTransform, ItemDrop, dropped-item model)
    /// without shipping an asset bundle. Nothing about the donor's *stats* survives: every field
    /// the wings care about is written from config in <see cref="ApplyStats"/>. That is what
    /// removed the fire weakness the dragon wings inherited from CapeFeather.
    /// </summary>
    [HarmonyPatch]
    public static class WingsFactory
    {
        private const string HolderName = "WingsoftheValkyrie_PrefabContainer";

        private static GameObject _holder;

        private static readonly Dictionary<string, GameObject> Prefabs = new Dictionary<string, GameObject>();
        private static readonly Dictionary<string, Recipe> Recipes = new Dictionary<string, Recipe>();

        // Last logged station binding per tier, so the recipe pass can be re-run as often as it
        // likes without repeating itself. See LogStation.
        private static readonly Dictionary<string, string> StationState = new Dictionary<string, string>();

        // ObjectDB.UpdateRegisters is private and has no public equivalent; it is what rebuilds
        // m_itemByHash, without which ItemDrop.Awake cannot resolve m_dropPrefab and every one of
        // our items saves as an empty string and is destroyed on the next load.
        private static readonly MethodInfo UpdateRegistersMethod =
            AccessTools.Method(typeof(ObjectDB), "UpdateRegisters");

        // private readonly Dictionary<int, GameObject>. Only the dictionary reference is ever
        // read -- the dictionary itself is then mutated, which readonly does not prevent -- so a
        // plain FieldInfo read is enough and sidesteps taking a ref to an initonly field.
        private static readonly FieldInfo NamedPrefabsField =
            AccessTools.Field(typeof(ZNetScene), "m_namedPrefabs");

        // ---- lifecycle ---------------------------------------------------------------------

        public static void Init()
        {
            EnsureHolder();

            // The server can push new values at any time once ServerSync connects, and an admin
            // can edit the file live. Both have to reach items that were built minutes earlier,
            // or a synced armour value would be a number nobody's wings actually have.
            //
            // Subscribed at the file rather than per entry: BepInEx only raises SettingChanged on
            // the typed ConfigEntry<T>, and this file belongs to this plugin alone, so there is
            // nothing to filter out.
            if (ModConfig.File != null) ModConfig.File.SettingChanged += OnConfigChanged;
        }

        private static void OnConfigChanged(object sender, EventArgs args)
        {
            if (Prefabs.Count == 0) return;

            try
            {
                foreach (WingsTier tier in WingsItem.Tiers)
                {
                    if (Prefabs.TryGetValue(tier.PrefabName, out GameObject prefab)) ApplyStats(tier, prefab);
                }
                EnsureRecipes();
            }
            catch (Exception ex)
            {
                Log.LogWarning($"Could not re-apply the wing stats after a config change; the previous values stay in force. Reason: {ex.Message}");
            }
        }

        /// <summary>
        /// The inert parent. It is deactivated BEFORE anything is parented to it, which is the
        /// whole trick: a clone that never becomes active never runs ZNetView.Awake, so it never
        /// creates the world-origin ZDO that a plain runtime Instantiate would.
        /// </summary>
        private static void EnsureHolder()
        {
            if (_holder != null) return;

            _holder = new GameObject(HolderName);
            _holder.SetActive(false);
            UnityEngine.Object.DontDestroyOnLoad(_holder);
        }

        // ---- registration steps ------------------------------------------------------------

        /// <summary>Everything that needs ObjectDB and nothing else. Safe to call repeatedly.</summary>
        internal static void EnsureItems()
        {
            ObjectDB db = ObjectDB.instance;

            // FejdStartup's ObjectDB.Awake fires with an empty list, before CopyOtherDB fills it.
            // Building against that DB would find no donors and register into a list the real one
            // is about to replace.
            if (db == null || db.m_items == null || db.m_items.Count == 0 || db.GetItemPrefab("Wood") == null) return;

            EnsureHolder();

            bool added = false;

            foreach (WingsTier tier in WingsItem.Tiers)
            {
                try
                {
                    GameObject prefab = BuildOrGet(db, tier);
                    if (prefab == null) continue;

                    // Re-applied on every pass, not just at creation: CopyOtherDB can hand us a
                    // different m_items list, and the config may have moved underneath us.
                    ApplyStats(tier, prefab);

                    if (!db.m_items.Contains(prefab))
                    {
                        db.m_items.Add(prefab);
                        added = true;
                    }
                }
                catch (Exception ex)
                {
                    Log.LogError($"Could not build {tier.PrefabName}; that tier will not exist this session. Reason: {ex}");
                }
            }

            if (added) RebuildRegisters(db);
        }

        private static GameObject BuildOrGet(ObjectDB db, WingsTier tier)
        {
            if (Prefabs.TryGetValue(tier.PrefabName, out GameObject existing) && existing != null) return existing;

            GameObject donor = db.GetItemPrefab(tier.DonorPrefab);
            if (donor == null)
            {
                Log.LogError($"Donor prefab '{tier.DonorPrefab}' is missing from the ObjectDB, so {tier.PrefabName} cannot be built.");
                return null;
            }

            // Never overwrite somebody else's hash. ZNetScene.Awake and ObjectDB.UpdateRegisters
            // both use Dictionary.Add, so a collision there is an ArgumentException that aborts
            // Awake before ZDOMan is hooked up -- i.e. a bricked game, not a broken mod.
            if (db.GetItemPrefab(tier.Hash) != null)
            {
                Log.LogError($"Something else already owns the item name '{tier.PrefabName}'. Refusing to replace it.");
                return null;
            }

            GameObject clone = UnityEngine.Object.Instantiate(donor, _holder.transform, worldPositionStays: false);

            // Strips "(Clone)". ItemDrop truncates a prefab name at the first '(' or ' ' while
            // ZNetScene and ObjectDB hash it in full, so the two would disagree and m_dropPrefab
            // would silently end up null.
            clone.name = tier.PrefabName;

            Prefabs[tier.PrefabName] = clone;
            Log.LogInfo($"Forged {tier.PrefabName} from {tier.DonorPrefab}.");
            return clone;
        }

        private static void RebuildRegisters(ObjectDB db)
        {
            if (UpdateRegistersMethod == null)
            {
                Log.LogError("ObjectDB.UpdateRegisters could not be found, so the wings cannot be indexed. They will not be craftable this session.");
                return;
            }

            UpdateRegistersMethod.Invoke(db, null);
        }

        /// <summary>
        /// Puts the prefabs into the dictionary ZNetScene actually reads at runtime. Without this
        /// a dropped pair of wings is an unknown prefab to the server, which deletes its ZDO
        /// permanently -- the item is simply gone.
        /// </summary>
        internal static void EnsurePrefabs()
        {
            ZNetScene scene = ZNetScene.instance;
            if (scene == null || Prefabs.Count == 0) return;

            if (NamedPrefabsField == null)
            {
                Log.LogError("ZNetScene.m_namedPrefabs could not be found. Dropped wings would be destroyed by the server, so they are not being registered for spawning.");
                return;
            }

            var named = NamedPrefabsField.GetValue(scene) as Dictionary<int, GameObject>;
            if (named == null) return;

            foreach (WingsTier tier in WingsItem.Tiers)
            {
                if (!Prefabs.TryGetValue(tier.PrefabName, out GameObject prefab) || prefab == null) continue;

                // m_prefabs is only ever read inside ZNetScene.Awake, so adding to it registers
                // nothing. It is kept in step anyway because other mods enumerate it.
                if (!scene.m_prefabs.Contains(prefab)) scene.m_prefabs.Add(prefab);

                if (!named.ContainsKey(tier.Hash)) named[tier.Hash] = prefab;
            }
        }

        /// <summary>
        /// Recipes need ObjectDB (for the ingredients) and ZNetScene (for the station), so this
        /// runs last. m_recipes has no index to rebuild -- every consumer walks it linearly.
        /// </summary>
        internal static void EnsureRecipes()
        {
            ObjectDB db = ObjectDB.instance;
            if (db == null || Prefabs.Count == 0) return;

            foreach (WingsTier tier in WingsItem.Tiers)
            {
                try
                {
                    if (!Prefabs.TryGetValue(tier.PrefabName, out GameObject prefab) || prefab == null) continue;

                    ItemDrop itemDrop = prefab.GetComponent<ItemDrop>();
                    if (itemDrop == null) continue;

                    if (!Recipes.TryGetValue(tier.PrefabName, out Recipe recipe) || recipe == null)
                    {
                        recipe = ScriptableObject.CreateInstance<Recipe>();
                        recipe.name = "Recipe_" + tier.PrefabName;

                        // Valheim runs Resources.UnloadUnusedAssets across scene changes. This
                        // object is held in a static dictionary so it would survive anyway, but
                        // an asset nobody owns is exactly what that sweep is for.
                        recipe.hideFlags = HideFlags.HideAndDontSave;
                        Recipes[tier.PrefabName] = recipe;
                    }

                    ModConfig.TierConfig cfg = tier.Config;
                    string stationName = cfg.CraftingStation.Value;
                    CraftingStation station = null;

                    if (!string.IsNullOrEmpty(stationName))
                    {
                        // A recipe whose m_craftingStation is null is craftable from the inventory
                        // with no bench at all, so the station has to resolve BEFORE the recipe is
                        // published. On a dedicated server ObjectDB.Awake fires before
                        // ZNetScene.Awake, and the station lives in the scene -- so on that first
                        // pass there is simply nothing to look it up in. Skip the tier and let the
                        // ZNetScene.Awake postfix publish it; every pass here is idempotent.
                        if (ZNetScene.instance == null) continue;

                        station = FindStation(stationName);
                        if (station == null)
                        {
                            // The scene is up and the name still will not resolve: a typo in the
                            // config, or a station belonging to a mod that is not installed.
                            // Leaving the recipe in would hand out free wings, so it comes back
                            // out until the name is fixed -- which a config edit does live.
                            LogStation(tier, "missing:" + stationName,
                                () => Log.LogError($"Crafting station '{stationName}' does not exist, so {tier.DisplayName} cannot be crafted. Fix CraftingStation in the config (or clear it to make them craftable with no station)."));

                            recipe.m_enabled = false;
                            db.m_recipes.Remove(recipe);
                            continue;
                        }
                    }

                    // Must be the CLONE's ItemDrop: DoCrafting crafts by
                    // m_craftRecipe.m_item.gameObject.name, so the donor here would craft a cape.
                    recipe.m_item = itemDrop;
                    recipe.m_amount = 1;
                    recipe.m_enabled = true;
                    recipe.m_minStationLevel = Mathf.Max(1, cfg.MinStationLevel.Value);
                    recipe.m_craftingStation = station;

                    // The station that may perform upgrades and repairs. Same station: an upgrade
                    // path that needed a different bench would be a surprise nobody asked for.
                    recipe.m_repairStation = station;

                    recipe.m_resources = BuildRequirements(db, tier, cfg);

                    if (!db.m_recipes.Contains(recipe)) db.m_recipes.Add(recipe);

                    LogStation(tier, $"ok:{stationName}:{recipe.m_minStationLevel}",
                        () => Log.LogInfo(string.IsNullOrEmpty(stationName)
                            ? $"{tier.PrefabName} is craftable with no station."
                            : $"{tier.PrefabName} bound to station '{stationName}' at level {recipe.m_minStationLevel}."));
                }
                catch (Exception ex)
                {
                    Log.LogError($"Could not build the recipe for {tier.PrefabName}. Reason: {ex}");
                }
            }
        }

        /// <summary>
        /// Returns null both when the name does not resolve and when it resolves to something
        /// that is not a bench. Says nothing either way: the caller knows whether the scene is
        /// up yet, and that is the difference between "too early" and "wrong".
        /// </summary>
        private static CraftingStation FindStation(string stationPrefabName)
        {
            if (string.IsNullOrEmpty(stationPrefabName)) return null;
            if (ZNetScene.instance == null) return null;

            GameObject prefab = ZNetScene.instance.GetPrefab(stationPrefabName);
            return prefab != null ? prefab.GetComponent<CraftingStation>() : null;
        }

        /// <summary>
        /// EnsureRecipes runs from three patch points and again on every config change, so a line
        /// logged unconditionally would repeat for as long as the game is open. This logs only
        /// when a tier's station binding actually changes -- which is exactly when it is news.
        /// </summary>
        private static void LogStation(WingsTier tier, string state, Action write)
        {
            if (StationState.TryGetValue(tier.PrefabName, out string previous) && previous == state) return;

            StationState[tier.PrefabName] = state;
            write();
        }

        // ---- stats -------------------------------------------------------------------------

        /// <summary>
        /// Writes every stat the wings have, rather than inheriting the donor cape's. The clone
        /// owns its SharedData outright -- Unity's Instantiate deep-copies the [Serializable]
        /// ItemData and SharedData -- so nothing written here can reach the vanilla cape.
        /// </summary>
        private static void ApplyStats(WingsTier tier, GameObject prefab)
        {
            ItemDrop itemDrop = prefab.GetComponent<ItemDrop>();
            if (itemDrop == null || itemDrop.m_itemData == null || itemDrop.m_itemData.m_shared == null) return;

            ItemDrop.ItemData.SharedData shared = itemDrop.m_itemData.m_shared;
            ModConfig.TierConfig cfg = tier.Config;

            shared.m_name = tier.DisplayName;
            shared.m_description = tier.Description;
            shared.m_itemType = ItemDrop.ItemData.ItemType.Shoulder;
            shared.m_maxStackSize = 1;

            shared.m_maxQuality = Mathf.Clamp(ModConfig.MaxQuality.Value, 1, 4);
            shared.m_weight = cfg.Weight.Value;
            shared.m_teleportable = true;

            shared.m_armor = cfg.Armor.Value;
            shared.m_armorPerLevel = cfg.ArmorPerLevel.Value;
            shared.m_damageModifiers = ParseDamageModifiers(cfg.DamageModifiers.Value, tier.PrefabName);

            shared.m_useDurability = true;
            shared.m_destroyBroken = false;
            shared.m_canBeReparied = true;   // vanilla's spelling
            shared.m_maxDurability = cfg.MaxDurability.Value;
            shared.m_durabilityPerLevel = 50f;

            shared.m_jumpStaminaModifier = cfg.JumpStaminaModifier.Value;
            shared.m_runStaminaModifier = cfg.RunStaminaModifier.Value;
            shared.m_sneakStaminaModifier = cfg.SneakStaminaModifier.Value;
            shared.m_eitrRegenModifier = cfg.EitrRegenModifier.Value;

            // Inherited leftovers of the donor cape. The set membership is the notable one: the
            // troll wings were cloned from CapeTrollHide, whose m_setName is "troll", so up to
            // 2.0.4 wearing them counted towards the vanilla troll armour set bonus.
            shared.m_setName = "";
            shared.m_setSize = 0;
            shared.m_setStatusEffect = null;

            // CapeFeather's SlowFall. The mod's own flight replaces it, and having both fight
            // over the fall speed is how the dragon tier used to feel mushy.
            shared.m_equipStatusEffect = null;

            ApplyIcon(tier, shared);
            DisableCloth(prefab);
        }

        /// <summary>
        /// Swaps the icon inherited from the donor cape for the mod's own embedded art. On any
        /// load failure the donor icon is kept, because ItemData.GetIcon() indexes m_icons
        /// unguarded and must never see an empty array.
        /// </summary>
        private static void ApplyIcon(WingsTier tier, ItemDrop.ItemData.SharedData shared)
        {
            Sprite sprite = IconLoader.Load(tier.IconFile);
            if (sprite == null) return;

            if (shared.m_icons != null && shared.m_icons.Length == 1 && shared.m_icons[0] == sprite) return;

            shared.m_icons = new[] { sprite };
        }

        /// <summary>
        /// Cloth lives in UnityEngine.ClothModule, which this project does not reference;
        /// Behaviour.enabled is serialized, so disabling it here does carry over to the
        /// dropped-item model. The equipped cape mesh itself is suppressed separately, in
        /// <see cref="CapeVisualPatch"/>.
        /// </summary>
        private static void DisableCloth(GameObject prefab)
        {
            foreach (Component component in prefab.GetComponentsInChildren<Component>(true))
            {
                if (component == null || component.GetType().Name != "Cloth") continue;

                PropertyInfo enabled = component.GetType().GetProperty("enabled");
                if (enabled != null) enabled.SetValue(component, false, null);
            }
        }

        // ---- config parsing ----------------------------------------------------------------

        /// <summary>
        /// Builds the recipe's resource table from the craft and upgrade lists together. Vanilla
        /// keeps both in one array: Piece.Requirement.GetAmount returns m_amount at quality 1 and
        /// (quality - 1) * m_amountPerLevel above it. An item that appears in only one of the two
        /// lists therefore gets an explicit 0 in the other -- leaving m_amountPerLevel at its
        /// default of 1 would quietly charge for materials the admin never listed.
        /// </summary>
        private static Piece.Requirement[] BuildRequirements(ObjectDB db, WingsTier tier, ModConfig.TierConfig cfg)
        {
            Dictionary<string, int> craft = ParseAmounts(cfg.CraftingRequirements.Value, tier.PrefabName, "CraftingRequirements");
            Dictionary<string, int> upgrade = ParseAmounts(cfg.UpgradeRequirements.Value, tier.PrefabName, "UpgradeRequirements");

            // Craft items first so the crafting panel lists what you need to make the wings
            // before what you need to improve them.
            var order = new List<string>(craft.Keys);
            foreach (string item in upgrade.Keys)
            {
                if (!craft.ContainsKey(item)) order.Add(item);
            }

            var requirements = new List<Piece.Requirement>(order.Count);

            foreach (string itemName in order)
            {
                GameObject prefab = db.GetItemPrefab(itemName);
                ItemDrop resource = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
                if (resource == null)
                {
                    Log.LogWarning($"{tier.PrefabName}: ingredient '{itemName}' is not a known item, so it is being left out of the recipe.");
                    continue;
                }

                requirements.Add(new Piece.Requirement
                {
                    m_resItem = resource,
                    m_amount = craft.TryGetValue(itemName, out int amount) ? amount : 0,
                    m_amountPerLevel = upgrade.TryGetValue(itemName, out int perLevel) ? perLevel : 0,
                    m_recover = true
                });
            }

            return requirements.ToArray();
        }

        /// <summary>Parses "ItemName:Amount,ItemName:Amount". Later duplicates win.</summary>
        private static Dictionary<string, int> ParseAmounts(string value, string tierName, string key)
        {
            var parsed = new Dictionary<string, int>();
            if (string.IsNullOrEmpty(value)) return parsed;

            foreach (string entry in value.Split(','))
            {
                if (string.IsNullOrEmpty(entry.Trim())) continue;

                string[] parts = entry.Split(':');
                if (parts.Length != 2 || !int.TryParse(parts[1].Trim(), out int amount) || amount < 0)
                {
                    Log.LogWarning($"{tierName}: could not read '{entry.Trim()}' in {key}. Expected ItemName:Amount.");
                    continue;
                }

                parsed[parts[0].Trim()] = amount;
            }

            return parsed;
        }

        /// <summary>
        /// Parses "Fire:VeryResistant,Frost:Resistant" into the list SharedData carries. The two
        /// composite masks in HitData.DamageType -- Physical and Elemental -- are rejected: they
        /// are not damage types, and Enum.TryParse will happily hand them over.
        /// </summary>
        private static List<HitData.DamageModPair> ParseDamageModifiers(string value, string tierName)
        {
            var mods = new List<HitData.DamageModPair>();
            if (string.IsNullOrEmpty(value)) return mods;

            foreach (string entry in value.Split(','))
            {
                string trimmed = entry.Trim();
                if (trimmed.Length == 0) continue;

                string[] parts = trimmed.Split(':');
                if (parts.Length != 2)
                {
                    Log.LogWarning($"{tierName}: could not read '{trimmed}' in DamageModifiers. Expected Type:Modifier.");
                    continue;
                }

                string typeName = parts[0].Trim();
                string modifierName = parts[1].Trim();

                if (!TryParseEnum(typeName, out HitData.DamageType type) ||
                    type == HitData.DamageType.Physical || type == HitData.DamageType.Elemental)
                {
                    Log.LogWarning($"{tierName}: '{typeName}' is not a damage type that can carry a resistance.");
                    continue;
                }

                if (!TryParseEnum(modifierName, out HitData.DamageModifier modifier))
                {
                    Log.LogWarning($"{tierName}: '{modifierName}' is not a damage modifier.");
                    continue;
                }

                mods.Add(new HitData.DamageModPair { m_type = type, m_modifier = modifier });
            }

            return mods;
        }

        private static bool TryParseEnum<T>(string name, out T parsed) where T : struct
        {
            foreach (string candidate in Enum.GetNames(typeof(T)))
            {
                if (!string.Equals(candidate, name, StringComparison.OrdinalIgnoreCase)) continue;

                parsed = (T)Enum.Parse(typeof(T), candidate);
                return true;
            }

            parsed = default(T);
            return false;
        }

        // ---- patch points ------------------------------------------------------------------
        //
        // Postfixes throughout: the vanilla registers have to be built before we touch them, or
        // our Add lands in the middle of a foreach vanilla is still walking.

        [HarmonyPatch(typeof(ObjectDB), "Awake")]
        [HarmonyPostfix]
        private static void ObjectDBAwakePostfix()
        {
            EnsureItems();
            EnsurePrefabs();
            EnsureRecipes();
        }

        /// <summary>
        /// CopyOtherDB assigns m_items and m_recipes by reference from the other database, so
        /// everything registered before it ran is no longer in the list ObjectDB is now using.
        /// </summary>
        [HarmonyPatch(typeof(ObjectDB), "CopyOtherDB")]
        [HarmonyPostfix]
        private static void ObjectDBCopyOtherDBPostfix()
        {
            EnsureItems();
            EnsurePrefabs();
            EnsureRecipes();
        }

        [HarmonyPatch(typeof(ZNetScene), "Awake")]
        [HarmonyPostfix]
        private static void ZNetSceneAwakePostfix()
        {
            // ZNetScene may awake before ObjectDB, in which case there is nothing to register
            // yet and the ObjectDB postfix will do all three steps itself.
            EnsureItems();
            EnsurePrefabs();
            EnsureRecipes();
        }
    }
}
