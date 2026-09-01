using HarmonyLib;

namespace WingsoftheValkyrie
{
    /// <summary>
    /// Everything that identifies one tier of wings. The four instances in
    /// <see cref="WingsItem.Tiers"/> are the single place a tier is described, so adding one
    /// means adding a row here and a config section, not editing five switch statements.
    /// </summary>
    public sealed class WingsTier
    {
        /// <summary>The prefab name. This is the item's identity everywhere that matters -- the
        /// ZDO prefab hash, the ObjectDB key and the string written into every inventory save --
        /// so it must never change once shipped.</summary>
        public readonly string PrefabName;

        /// <summary>The vanilla cape whose prefab supplies the chassis: ZNetView, Rigidbody,
        /// ZSyncTransform, ItemDrop and a dropped-item model. Its *stats* are not inherited;
        /// every field the wings care about is written explicitly in <see cref="WingsFactory"/>.</summary>
        public readonly string DonorPrefab;

        /// <summary>SharedData.m_name. Byte-identical to what the Jotunn build produced, so a
        /// character upgrading from 2.0.4 keeps their known-recipe entry.</summary>
        public readonly string DisplayName;

        public readonly string Description;
        public readonly string IconFile;

        public readonly int Hash;

        public WingsTier(string prefabName, string donorPrefab, string displayName, string description, string iconFile)
        {
            PrefabName = prefabName;
            DonorPrefab = donorPrefab;
            DisplayName = displayName;
            Description = description;
            IconFile = iconFile;
            Hash = prefabName.GetStableHashCode();
        }

        public ModConfig.TierConfig Config => ModConfig.GetTier(PrefabName);
    }

    public static class WingsItem
    {
        public const string CrudeName = "WingsOf_Crude";
        public const string TrollName = "WingsOf_Troll";
        public const string LoxName = "WingsOf_Lox";
        public const string DragonName = "WingsOf_Dragon";

        public static readonly WingsTier[] Tiers =
        {
            new WingsTier(CrudeName, "CapeDeerHide", "Wings of the Valkyrie (Crude)",
                "Basic crafted wings. Low flight ceiling.", "wings_crude.png"),

            new WingsTier(TrollName, "CapeTrollHide", "Wings of the Valkyrie (Troll)",
                "Improved glide and lift.", "wings_troll.png"),

            new WingsTier(LoxName, "CapeLox", "Wings of the Valkyrie (Lox)",
                "High glide speed, strong lift.", "wings_lox.png"),

            new WingsTier(DragonName, "CapeFeather", "Wings of the Valkyrie (Dragon)",
                "Unlimited sky ceiling and excellent speed.", "wings_dragon.png"),
        };

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
            WingsFactory.Init();
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

        /// <summary>Inverse of <see cref="GetWingsNameFromHash"/>. 0 for null or any other name.</summary>
        public static int GetHashFromWingsName(string wingsName)
        {
            switch (wingsName)
            {
                case CrudeName: return CrudeHash;
                case TrollName: return TrollHash;
                case LoxName: return LoxHash;
                case DragonName: return DragonHash;
                default: return 0;
            }
        }

        /// <summary>
        /// The wings a player is genuinely wearing, or null. Authoritative, and so only meaningful
        /// for the local player: Humanoid.m_shoulderItem is owner-only and null for everyone else.
        ///
        /// It deliberately does NOT fall back to the model's shoulder hash. **Appearance is not
        /// evidence.** AzuExtendedPlayerInventory's vanity slots prefix VisEquipment.SetShoulderEquipped
        /// and overwrite the hash with whatever the player chose to *look* like, and that hash is
        /// what lands in m_currentShoulderItemHash -- so up to 2.0.2 a Deer Cape transformed into
        /// wings granted real flight, wings the wearer did not have to own, craft or even carry.
        ///
        /// Reading only the equipped item settles both directions at once: a costume never flies,
        /// and real wings worn under a costume always do.
        /// </summary>
        public static ItemDrop.ItemData GetEquippedWings(Player player)
        {
            if (player == null || ShoulderItemRef == null) return null;

            var shoulderItem = ShoulderItemRef(player);
            if (shoulderItem == null || shoulderItem.m_dropPrefab == null) return null;

            return IsWingsHash(shoulderItem.m_dropPrefab.name.GetStableHashCode()) ? shoulderItem : null;
        }

        public static string GetEquippedWingsName(Player player)
        {
            if (player == null) return null;

            // A missing field ref means the game moved m_shoulderItem out from under us. Fall back
            // to the model rather than grounding every player at once: a stale reflection handle
            // must not cost people flight they crafted, and of the two ways to be wrong, letting a
            // costume fly is much the smaller.
            if (ShoulderItemRef == null) return GetVisualWingsName(player);

            var wings = GetEquippedWings(player);
            return wings != null ? GetWingsNameFromHash(wings.m_dropPrefab.name.GetStableHashCode()) : null;
        }

        /// <summary>
        /// The upgrade level of the wings a player is wearing, or 1. Only the local player's
        /// quality is ever needed: it feeds the flight physics, which run on the owning client.
        /// It is deliberately not published on the ZDO -- remote clients draw the wings, they do
        /// not fly them.
        /// </summary>
        public static int GetEquippedWingsQuality(Player player)
        {
            var wings = GetEquippedWings(player);
            return wings != null ? wings.m_quality : 1;
        }

        /// <summary>
        /// What a player's model is wearing on its back, or null. **Cosmetic only.** This is the
        /// post-vanity hash, so it can name wings nobody equipped and stay silent about wings
        /// somebody did -- never gate flight on it. It is the last resort for drawing a remote
        /// player whose owner is on a build that does not publish its tier.
        /// </summary>
        public static string GetVisualWingsName(Player player)
        {
            if (player == null || CurrentShoulderHashRef == null) return null;

            var visEq = player.GetComponent<VisEquipment>();
            return visEq != null ? GetWingsNameFromHash(CurrentShoulderHashRef(visEq)) : null;
        }

        public static bool IsWingsEquipped(Player player)
        {
            return GetEquippedWingsName(player) != null;
        }
    }
}
