using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using System.Linq;
using HarmonyLib;

namespace WingsoftheValkyrie
{
    public static class WingsItem
    {
        public const string CrudeName = "WingsOf_Crude";
        public const string TrollName = "WingsOf_Troll";
        public const string LoxName = "WingsOf_Lox";
        public const string DragonName = "WingsOf_Dragon";

        // VisEquipment only ever exposes the *hash* of the equipped shoulder item, so all
        // remote-player lookups have to match on hashes rather than prefab names.
        private static readonly int CrudeHash = CrudeName.GetStableHashCode();
        private static readonly int TrollHash = TrollName.GetStableHashCode();
        private static readonly int LoxHash = LoxName.GetStableHashCode();
        private static readonly int DragonHash = DragonName.GetStableHashCode();

        // Cached field refs: these run once per player per frame, so Traverse is too slow.
        private static readonly AccessTools.FieldRef<Humanoid, ItemDrop.ItemData> ShoulderItemRef =
            ReflectionUtil.TryFieldRef<Humanoid, ItemDrop.ItemData>("m_shoulderItem");
        private static readonly AccessTools.FieldRef<VisEquipment, int> CurrentShoulderHashRef =
            ReflectionUtil.TryFieldRef<VisEquipment, int>("m_currentShoulderItemHash");

        public static void Init()
        {
            PrefabManager.OnVanillaPrefabsAvailable += CreateCustomWings;
        }

        private static void CreateCustomWings()
        {
            CustomItem crudeWings = new CustomItem(CrudeName, "CapeDeerHide", new ItemConfig
            {
                Name = "Wings of the Valkyrie (Crude)",
                Description = "Basic crafted wings. Low flight ceiling.",
                CraftingStation = ModConfig.CrudeCraftingStation.Value,
                MinStationLevel = ModConfig.CrudeMinStationLevel.Value,
                Requirements = ParseRequirements(ModConfig.CrudeCraftingRequirements.Value)
            });
            StripCapeVisuals(crudeWings.ItemPrefab);
            ApplyCustomIcon(crudeWings, "wings_crude.png");
            ItemManager.Instance.AddItem(crudeWings);

            CustomItem trollWings = new CustomItem(TrollName, "CapeTrollHide", new ItemConfig
            {
                Name = "Wings of the Valkyrie (Troll)",
                Description = "Improved glide and lift.",
                CraftingStation = ModConfig.TrollCraftingStation.Value,
                MinStationLevel = ModConfig.TrollMinStationLevel.Value,
                Requirements = ParseRequirements(ModConfig.TrollCraftingRequirements.Value)
            });
            StripCapeVisuals(trollWings.ItemPrefab);
            ApplyCustomIcon(trollWings, "wings_troll.png");
            ItemManager.Instance.AddItem(trollWings);

            CustomItem loxWings = new CustomItem(LoxName, "CapeLox", new ItemConfig
            {
                Name = "Wings of the Valkyrie (Lox)",
                Description = "High glide speed, strong lift.",
                CraftingStation = ModConfig.LoxCraftingStation.Value,
                MinStationLevel = ModConfig.LoxMinStationLevel.Value,
                Requirements = ParseRequirements(ModConfig.LoxCraftingRequirements.Value)
            });
            StripCapeVisuals(loxWings.ItemPrefab);
            ApplyCustomIcon(loxWings, "wings_lox.png");
            ItemManager.Instance.AddItem(loxWings);

            CustomItem dragonWings = new CustomItem(DragonName, "CapeFeather", new ItemConfig
            {
                Name = "Wings of the Valkyrie (Dragon)",
                Description = "Unlimited sky ceiling and excellent speed.",
                CraftingStation = ModConfig.DragonCraftingStation.Value,
                MinStationLevel = ModConfig.DragonMinStationLevel.Value,
                Requirements = ParseRequirements(ModConfig.DragonCraftingRequirements.Value)
            });
            StripCapeVisuals(dragonWings.ItemPrefab);
            ApplyCustomIcon(dragonWings, "wings_dragon.png");
            ItemManager.Instance.AddItem(dragonWings);

            PrefabManager.OnVanillaPrefabsAvailable -= CreateCustomWings;
        }

        /// <summary>
        /// Strips the leftovers of the vanilla cape these items are cloned from.
        /// The equipped cape mesh itself is suppressed in <see cref="CapeVisualPatch"/> --
        /// touching renderers on the prefab cannot work, because VisEquipment instantiates
        /// a fresh copy of the "attach_skin" child onto the player's skeleton and Unity does
        /// not carry runtime-only renderer flags across Instantiate.
        /// </summary>
        private static void StripCapeVisuals(UnityEngine.GameObject prefab)
        {
            if (prefab == null) return;

            var itemDrop = prefab.GetComponent<ItemDrop>();
            if (itemDrop != null && itemDrop.m_itemData != null && itemDrop.m_itemData.m_shared != null)
            {
                itemDrop.m_itemData.m_shared.m_equipStatusEffect = null;
            }

            // Cloth lives in UnityEngine.ClothModule which we do not reference; Behaviour.enabled
            // is serialized, so disabling it here does carry over to the dropped-item model.
            var components = prefab.GetComponentsInChildren<UnityEngine.Component>(true);
            foreach (var comp in components)
            {
                if (comp != null && comp.GetType().Name == "Cloth")
                {
                    var prop = comp.GetType().GetProperty("enabled");
                    if (prop != null)
                    {
                        prop.SetValue(comp, false, null);
                    }
                }
            }
        }

        /// <summary>
        /// Swaps the icon inherited from the donor cape for the mod's own embedded art. The
        /// clone's SharedData is its own instance (Instantiate deep-copies it), so this cannot
        /// touch the vanilla cape's icon. On any load failure the donor icon is kept, because
        /// ItemData.GetIcon() indexes m_icons unguarded and must never see an empty array.
        /// </summary>
        private static void ApplyCustomIcon(CustomItem item, string iconFileName)
        {
            var sprite = IconLoader.Load(iconFileName);
            if (sprite == null) return;

            var itemDrop = item.ItemPrefab != null ? item.ItemPrefab.GetComponent<ItemDrop>() : null;
            if (itemDrop == null || itemDrop.m_itemData == null || itemDrop.m_itemData.m_shared == null) return;

            itemDrop.m_itemData.m_shared.m_icons = new[] { sprite };
        }

        private static RequirementConfig[] ParseRequirements(string requirementsString)
        {
            if (string.IsNullOrWhiteSpace(requirementsString))
                return new RequirementConfig[0];

            return requirementsString.Split(',')
                .Select(req =>
                {
                    var parts = req.Split(':');
                    if (parts.Length == 2 && int.TryParse(parts[1], out int amount))
                    {
                        return new RequirementConfig { Item = parts[0].Trim(), Amount = amount };
                    }
                    return null;
                })
                .Where(r => r != null)
                .ToArray();
        }

        /// <summary>Maps a shoulder-item hash back to one of our wing prefab names, or null.</summary>
        public static string GetWingsNameFromHash(int hash)
        {
            if (hash == CrudeHash) return CrudeName;
            if (hash == TrollHash) return TrollName;
            if (hash == LoxHash) return LoxName;
            if (hash == DragonHash) return DragonName;
            return null;
        }

        public static bool IsWingsHash(int hash)
        {
            return GetWingsNameFromHash(hash) != null;
        }

        public static string GetEquippedWingsName(Player player)
        {
            if (player == null) return null;

            // Local player: the real ItemData is authoritative and available immediately.
            if (ShoulderItemRef != null)
            {
                var shoulderItem = ShoulderItemRef(player);
                if (shoulderItem != null && shoulderItem.m_dropPrefab != null)
                {
                    string name = GetWingsNameFromHash(shoulderItem.m_dropPrefab.name.GetStableHashCode());
                    if (name != null) return name;
                }
            }

            // Remote players: Humanoid.m_shoulderItem and VisEquipment.m_shoulderItem are both
            // owner-only. The networked value is resolved into m_currentShoulderItemHash.
            if (CurrentShoulderHashRef != null)
            {
                var visEq = player.GetComponent<VisEquipment>();
                if (visEq != null)
                {
                    return GetWingsNameFromHash(CurrentShoulderHashRef(visEq));
                }
            }

            return null;
        }

        public static bool IsWingsEquipped(Player player)
        {
            return GetEquippedWingsName(player) != null;
        }
    }
}
