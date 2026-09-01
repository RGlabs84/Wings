using BepInEx.Configuration;
using ServerSync;
using UnityEngine;

namespace WingsoftheValkyrie
{
    /// <summary>Per-tier flight stats bundled so FlightController does one lookup per frame
    /// instead of a per-stat if/else chain over the tier names.</summary>
    public readonly struct WingStats
    {
        /// <summary>Ceiling at skill 100. Novices get <see cref="ModConfig.CeilingAtNovice"/> of it.</summary>
        public readonly float FlightCeiling;
        public readonly float GlideSpeed;
        public readonly float FlapForce;
        public readonly float FlapStaminaCost;

        /// <summary>Valkyrie Flight level needed before these wings will beat. Below it they
        /// still unfurl and glide -- you simply cannot climb under your own power.</summary>
        public readonly float MinSkillToFlap;

        public WingStats(float flightCeiling, float glideSpeed, float flapForce, float flapStaminaCost, float minSkillToFlap)
        {
            FlightCeiling = flightCeiling;
            GlideSpeed = glideSpeed;
            FlapForce = flapForce;
            FlapStaminaCost = flapStaminaCost;
            MinSkillToFlap = minSkillToFlap;
        }
    }

    public static class ModConfig
    {
        // Where the migration stamp lives. Named here so ConfigMigration can find it in a raw
        // file that predates the entry existing. Deliberately NOT admin-synced: it describes
        // the local file's layout, not a gameplay rule the server should push to clients.
        public const string MetaSection = "0. Meta";
        public const string ConfigVersionKey = "ConfigVersion";

        // Replaces Jotunn's config sync and its [NetworkCompatibility] handshake in one piece.
        // MinimumRequiredVersion is deliberately NOT the plugin version: pinning the two
        // together disconnects every player on every release. Bump it only when a synced entry
        // is renamed or retyped, or an RPC signature changes.
        private static readonly ConfigSync Sync = new ConfigSync(WingsoftheValkyriePlugin.PluginGUID)
        {
            DisplayName = WingsoftheValkyriePlugin.PluginName,
            CurrentVersion = WingsoftheValkyriePlugin.PluginVersion,
            MinimumRequiredVersion = WingsoftheValkyriePlugin.SyncFloor,
            ModRequired = true
        };

        public static ConfigEntry<int> ConfigVersion { get; private set; }

        public static SyncedConfigEntry<bool> LockConfiguration { get; private set; }
        public static SyncedConfigEntry<bool> EnableMod { get; private set; }
        public static SyncedConfigEntry<float> GlobalWingSpan { get; private set; }
        public static SyncedConfigEntry<float> BaseGlideSinkRate { get; private set; }
        public static SyncedConfigEntry<float> MaxDiveSpeed { get; private set; }

        // Valkyrie Flight skill
        public static SyncedConfigEntry<float> SkillXpPerFlap { get; private set; }
        public static SyncedConfigEntry<float> SkillXpPerGlideSecond { get; private set; }
        public static SyncedConfigEntry<float> SkillStaminaReduction { get; private set; }
        public static SyncedConfigEntry<float> SkillFlapPowerBonus { get; private set; }
        public static SyncedConfigEntry<float> SkillGlideSinkReduction { get; private set; }
        public static SyncedConfigEntry<float> SkillGlideSpeedBonus { get; private set; }
        public static SyncedConfigEntry<float> CeilingAtNovice { get; private set; }

        // Flight logbook
        public static SyncedConfigEntry<bool> EnableFlightLog { get; private set; }
        public static SyncedConfigEntry<bool> PublishFlightStats { get; private set; }
        public static SyncedConfigEntry<string> FlightStatsExportFolder { get; private set; }
        public static SyncedConfigEntry<float> FlightStatsWriteInterval { get; private set; }
        public static SyncedConfigEntry<float> FlightStatsReportInterval { get; private set; }

        // Wing upgrades (shared across all four tiers)
        public static SyncedConfigEntry<int> MaxQuality { get; private set; }
        public static SyncedConfigEntry<float> CeilingBonusPerLevel { get; private set; }
        public static SyncedConfigEntry<float> GlideSpeedBonusPerLevel { get; private set; }
        public static SyncedConfigEntry<float> FlapForceBonusPerLevel { get; private set; }
        public static SyncedConfigEntry<float> FlapStaminaReductionPerLevel { get; private set; }

        /// <summary>Everything one tier's config section holds. Bundling it keeps the four
        /// sections from drifting apart: a stat added here has to be given to every tier.</summary>
        public sealed class TierConfig
        {
            public SyncedConfigEntry<float> FlightCeiling;
            public SyncedConfigEntry<float> GlideSpeed;
            public SyncedConfigEntry<float> FlapForce;
            public SyncedConfigEntry<float> FlapStaminaCost;
            public SyncedConfigEntry<float> MinSkillToFlap;

            public SyncedConfigEntry<string> CraftingStation;
            public SyncedConfigEntry<int> MinStationLevel;
            public SyncedConfigEntry<string> CraftingRequirements;
            public SyncedConfigEntry<string> UpgradeRequirements;

            public SyncedConfigEntry<float> Armor;
            public SyncedConfigEntry<float> ArmorPerLevel;
            public SyncedConfigEntry<float> Weight;
            public SyncedConfigEntry<float> MaxDurability;
            public SyncedConfigEntry<string> DamageModifiers;

            public SyncedConfigEntry<float> JumpStaminaModifier;
            public SyncedConfigEntry<float> RunStaminaModifier;
            public SyncedConfigEntry<float> SneakStaminaModifier;
            public SyncedConfigEntry<float> EitrRegenModifier;
        }

        public static TierConfig Crude { get; private set; }
        public static TierConfig Troll { get; private set; }
        public static TierConfig Lox { get; private set; }
        public static TierConfig Dragon { get; private set; }

        // ---- binding -----------------------------------------------------------------------

        private static ConfigFile _file;

        /// <summary>The bound file, so anything that has to react to a value changing -- an admin
        /// editing it live, or the server pushing its own copy down -- can hang off its events.</summary>
        public static ConfigFile File => _file;

        private static SyncedConfigEntry<T> Bind<T>(string section, string key, T value, ConfigDescription description, bool synced = true)
        {
            ConfigEntry<T> entry = _file.Bind(section, key, value, description);
            SyncedConfigEntry<T> synchronised = Sync.AddConfigEntry(entry);
            synchronised.SynchronizedConfig = synced;
            return synchronised;
        }

        private static SyncedConfigEntry<T> Bind<T>(string section, string key, T value, string description, bool synced = true) =>
            Bind(section, key, value, new ConfigDescription(description), synced);

        public static void Init(ConfigFile config)
        {
            _file = config;

            var rangeCeiling = new AcceptableValueRange<float>(10f, 5000f);
            var rangeSpeed = new AcceptableValueRange<float>(1f, 100f);
            var rangeForce = new AcceptableValueRange<float>(1f, 100f);
            var rangeStamina = new AcceptableValueRange<float>(0f, 100f);
            var rangeLevel = new AcceptableValueRange<int>(1, 10);
            var rangeSkillLevel = new AcceptableValueRange<float>(0f, 100f);
            var rangeXp = new AcceptableValueRange<float>(0f, 5f);
            var rangeFraction = new AcceptableValueRange<float>(0f, 1f);

            // Must run before the first Bind so it can snapshot the raw file as the previous
            // version left it.
            ConfigMigration.Begin(config);

            ConfigVersion = config.Bind(MetaSection, ConfigVersionKey, ConfigMigration.CurrentConfigVersion,
                "Internal bookkeeping - the layout version of this file, used to carry your settings across mod updates. Do not edit.");

            LockConfiguration = Bind("1. General", "LockConfiguration", true,
                "If on, this file is served by the server and only its admins can change the synced settings. Replaces the admin-only lock Jotunn used to provide.");
            Sync.AddLockingConfigEntry(LockConfiguration.SourceConfig);

            EnableMod = Bind("1. General", "EnableMod", true, "Enable or disable the Valkyrie Wings mod.");
            GlobalWingSpan = Bind("1. General", "GlobalWingSpan", 1.0f, new ConfigDescription("Multiplier for the overall size of the wings.", new AcceptableValueRange<float>(0.1f, 5.0f)));
            BaseGlideSinkRate = Bind("1. General", "BaseGlideSinkRate", 2.5f, new ConfigDescription("How fast a level 0 flier sinks while gliding level, in metres per second. Skill flattens this (see GlideSinkReductionAtMax).", new AcceptableValueRange<float>(0f, 20f)));
            MaxDiveSpeed = Bind("1. General", "MaxDiveSpeed", 20f, new ConfigDescription("Descent speed at a full straight-down dive, in metres per second. Never scaled by skill - diving is intent, not practice.", new AcceptableValueRange<float>(1f, 100f)));

            // ---- tiers ----------------------------------------------------------------------
            //
            // MinStationLevel is load-bearing beyond the craft itself: upgrading to quality q
            // needs station level MinStationLevel + (q - 1). A workbench tops out at level 5 and
            // a Galdr table at 4, so a tier whose floor is too high has upgrades nobody can ever
            // reach. Crude and Troll sit at 2 for exactly that reason (2 + 3 = 5).
            Crude = BindTier("2. Crude Wings",
                ceiling: 130f, glide: 8f, flap: 11f, stamina: 16f, minSkill: 0f,
                station: "piece_workbench", stationLevel: 2,
                craft: "BronzeNails:10,DeerHide:10,LeatherScraps:20,TrollHide:5,Feathers:20",
                upgrade: "Feathers:25,LeatherScraps:15,BronzeNails:8,TrollHide:6",
                armor: 2f, armorPerLevel: 1f, weight: 4f, durability: 400f,
                damageModifiers: "",
                jump: -0.10f, run: 0f, sneak: 0f, eitr: 0f,
                ranges: (rangeCeiling, rangeSpeed, rangeForce, rangeStamina, rangeSkillLevel, rangeLevel));

            Troll = BindTier("3. Troll Wings",
                ceiling: 150f, glide: 12f, flap: 13f, stamina: 13f, minSkill: 15f,
                station: "piece_workbench", stationLevel: 2,
                craft: "TrollHide:15,IronNails:10,Feathers:30",
                upgrade: "Feathers:35,TrollHide:12,IronNails:10,Silver:5",
                armor: 4f, armorPerLevel: 2f, weight: 4f, durability: 500f,
                damageModifiers: "Blunt:SlightlyResistant",
                jump: -0.15f, run: -0.05f, sneak: -0.15f, eitr: 0f,
                ranges: (rangeCeiling, rangeSpeed, rangeForce, rangeStamina, rangeSkillLevel, rangeLevel));

            Lox = BindTier("4. Lox Wings",
                ceiling: 190f, glide: 16f, flap: 16f, stamina: 10f, minSkill: 30f,
                station: "forge", stationLevel: 3,
                craft: "LoxPelt:10,Silver:20,LinenThread:10,Feathers:30",
                upgrade: "Feathers:40,LoxPelt:8,Silver:25,LinenThread:12",
                armor: 7f, armorPerLevel: 3f, weight: 5f, durability: 1200f,
                damageModifiers: "Frost:Resistant",
                jump: -0.20f, run: -0.10f, sneak: 0f, eitr: 0f,
                ranges: (rangeCeiling, rangeSpeed, rangeForce, rangeStamina, rangeSkillLevel, rangeLevel));

            // Fire:VeryResistant is the whole point of the tier's rewrite. The wings were cloned
            // from CapeFeather, which ships {Fire, VeryWeak} -- so up to 2.0.4 the dragon wings
            // burned faster than bare skin. Nothing about them was ever meant to.
            Dragon = BindTier("5. Dragon Wings",
                ceiling: 1300f, glide: 24f, flap: 20f, stamina: 7f, minSkill: 50f,
                station: "piece_magetable", stationLevel: 1,
                craft: "Feathers:40,Eitr:20,ScaleHide:10,DragonTear:2",
                upgrade: "Feathers:50,Eitr:25,ScaleHide:12,DragonTear:1",
                armor: 10f, armorPerLevel: 4f, weight: 3f, durability: 1200f,
                damageModifiers: "Fire:VeryResistant,Frost:Resistant",
                jump: -0.25f, run: -0.15f, sneak: -0.10f, eitr: 0.10f,
                ranges: (rangeCeiling, rangeSpeed, rangeForce, rangeStamina, rangeSkillLevel, rangeLevel));

            // Valkyrie Flight skill
            SkillXpPerFlap = Bind("6. Valkyrie Flight Skill", "XpPerFlap", 0.4f, new ConfigDescription("Skill XP awarded per wing flap.", rangeXp));
            SkillXpPerGlideSecond = Bind("6. Valkyrie Flight Skill", "XpPerGlideSecond", 0.2f, new ConfigDescription("Skill XP awarded per second spent gliding. This is the only way a flier below a tier's MinSkillToFlap can earn levels, so keep it above 0.", rangeXp));
            SkillStaminaReduction = Bind("6. Valkyrie Flight Skill", "StaminaReductionAtMax", 0.55f, new ConfigDescription("Fraction of the flap stamina cost removed at skill level 100.", rangeFraction));
            SkillFlapPowerBonus = Bind("6. Valkyrie Flight Skill", "FlapPowerBonusAtMax", 0.5f, new ConfigDescription("Extra flap lift at skill level 100, as a fraction of the tier's base force.", rangeFraction));
            SkillGlideSinkReduction = Bind("6. Valkyrie Flight Skill", "GlideSinkReductionAtMax", 0.6f, new ConfigDescription("How much slower you sink while gliding at skill level 100 (longer glides).", rangeFraction));
            SkillGlideSpeedBonus = Bind("6. Valkyrie Flight Skill", "GlideSpeedBonusAtMax", 0.35f, new ConfigDescription("Extra horizontal glide speed at skill level 100, as a fraction.", rangeFraction));
            CeilingAtNovice = Bind("6. Valkyrie Flight Skill", "CeilingAtNovice", 0.35f, new ConfigDescription("Fraction of a tier's flight ceiling available at skill level 0, rising to the full ceiling at 100. 1 = altitude is not gated by skill at all.", rangeFraction));

            // Flight logbook. Bookkeeping rather than balance, so deliberately NOT synced:
            // whether a player's own flights are written down is theirs to decide, and where a
            // server puts its export file is a property of that machine, not of the game rules.
            EnableFlightLog = Bind("7. Flight Logbook", "EnableFlightLog", true,
                "Track your flight saga - time flown, distance, records and oddities - and store it on your character. Read it in-game with the 'wov' console command.", synced: false);
            PublishFlightStats = Bind("7. Flight Logbook", "PublishFlightStats", true,
                "Take part in the server's flight statistics. On a client this sends your totals to the server you are playing on; on a server it publishes everyone's totals to a JSON file that tools such as BarrkBOT read. Set it to false on a client to keep your own logbook but stay out of the server's.", synced: false);
            FlightStatsExportFolder = Bind("7. Flight Logbook", "FlightStatsExportFolder", "",
                "Server only. Folder to write the flight statistics into. Leave empty for 'WingsOfTheValkyrie' beside this config file, which is where the export sweep looks - change it only if you know the reader is looking somewhere else.", synced: false);
            FlightStatsWriteInterval = Bind("7. Flight Logbook", "FlightStatsWriteIntervalSeconds", 60f, new ConfigDescription(
                "Server only. How often the flight statistics file is rewritten, in seconds.", new AcceptableValueRange<float>(5f, 3600f)), synced: false);
            FlightStatsReportInterval = Bind("7. Flight Logbook", "FlightStatsReportIntervalSeconds", 60f, new ConfigDescription(
                "Client only. How often your totals are sent to the server while you play, in seconds.", new AcceptableValueRange<float>(5f, 3600f)), synced: false);

            // Wing upgrades. The per-level bonuses are deliberately small: an upgrade buys
            // armour first and flight second, so a fully upgraded lower tier never turns into
            // the tier above it.
            MaxQuality = Bind("8. Wing Upgrades", "MaxQuality", 4, new ConfigDescription(
                "How many quality levels each pair of wings can be upgraded to. 1 disables upgrading entirely. Lowering this below a pair of wings someone already upgraded does not take their levels away, it only stops further upgrades.",
                new AcceptableValueRange<int>(1, 4)));
            CeilingBonusPerLevel = Bind("8. Wing Upgrades", "CeilingBonusPerLevel", 0.08f, new ConfigDescription(
                "Extra flight ceiling per quality level above 1, as a fraction of the tier's ceiling.", rangeFraction));
            GlideSpeedBonusPerLevel = Bind("8. Wing Upgrades", "GlideSpeedBonusPerLevel", 0.05f, new ConfigDescription(
                "Extra glide speed per quality level above 1, as a fraction of the tier's glide speed.", rangeFraction));
            FlapForceBonusPerLevel = Bind("8. Wing Upgrades", "FlapForceBonusPerLevel", 0.05f, new ConfigDescription(
                "Extra flap lift per quality level above 1, as a fraction of the tier's flap force.", rangeFraction));
            FlapStaminaReductionPerLevel = Bind("8. Wing Upgrades", "FlapStaminaReductionPerLevel", 0.06f, new ConfigDescription(
                "Flap stamina removed per quality level above 1, as a fraction of the tier's cost.", rangeFraction));

            ConfigMigration.Finish(config, ConfigVersion);
        }

        private static TierConfig BindTier(
            string section,
            float ceiling, float glide, float flap, float stamina, float minSkill,
            string station, int stationLevel, string craft, string upgrade,
            float armor, float armorPerLevel, float weight, float durability, string damageModifiers,
            float jump, float run, float sneak, float eitr,
            (AcceptableValueRange<float> ceiling, AcceptableValueRange<float> speed, AcceptableValueRange<float> force,
             AcceptableValueRange<float> stamina, AcceptableValueRange<float> skill, AcceptableValueRange<int> level) ranges)
        {
            var rangeModifier = new AcceptableValueRange<float>(-1f, 1f);

            return new TierConfig
            {
                FlightCeiling = Bind(section, "FlightCeiling", ceiling, new ConfigDescription("Flight ceiling above ground at Valkyrie Flight 100. Lower levels reach only a fraction of it (see CeilingAtNovice).", ranges.ceiling)),
                GlideSpeed = Bind(section, "GlideSpeed", glide, new ConfigDescription("Horizontal glide speed before skill and quality bonuses.", ranges.speed)),
                FlapForce = Bind(section, "FlapForce", flap, new ConfigDescription("Upward lift force when flapping, before skill and quality bonuses.", ranges.force)),
                FlapStaminaCost = Bind(section, "FlapStaminaCost", stamina, new ConfigDescription("Stamina consumed per flap before skill and quality reductions.", ranges.stamina)),
                MinSkillToFlap = Bind(section, "MinSkillToFlap", minSkill, new ConfigDescription("Valkyrie Flight level needed to flap these wings. Below it they still unfurl and glide, so the skill can always be earned. 0 = no requirement.", ranges.skill)),

                CraftingStation = Bind(section, "CraftingStation", station, "Prefab name of the crafting station."),
                MinStationLevel = Bind(section, "MinStationLevel", stationLevel, new ConfigDescription("Minimum station level required to craft. Upgrading to quality q needs this + (q - 1), so raising it can put the top quality levels out of reach: a workbench stops at level 5, a forge at 7 and a Galdr table at 4.", ranges.level)),
                CraftingRequirements = Bind(section, "CraftingRequirements", craft, "Items consumed to craft these wings at quality 1. Format: ItemName:Amount,ItemName:Amount"),
                UpgradeRequirements = Bind(section, "UpgradeRequirements", upgrade, "Items consumed PER QUALITY LEVEL when upgrading. Valheim charges (quality - 1) times these amounts, so 'Feathers:25' costs 25 feathers for quality 2, 50 for quality 3 and 75 for quality 4. Format: ItemName:AmountPerLevel"),

                Armor = Bind(section, "Armor", armor, new ConfigDescription("Armour these wings give at quality 1.", new AcceptableValueRange<float>(0f, 100f))),
                ArmorPerLevel = Bind(section, "ArmorPerLevel", armorPerLevel, new ConfigDescription("Extra armour per quality level above 1.", new AcceptableValueRange<float>(0f, 50f))),
                Weight = Bind(section, "Weight", weight, new ConfigDescription("Carry weight of the wings.", new AcceptableValueRange<float>(0f, 100f))),
                MaxDurability = Bind(section, "MaxDurability", durability, new ConfigDescription("Durability at quality 1. Each quality level adds 50 on top, as vanilla armour does.", new AcceptableValueRange<float>(1f, 10000f))),
                DamageModifiers = Bind(section, "DamageModifiers", damageModifiers,
                    "Damage resistances the wings grant. Format: Type:Modifier,Type:Modifier. Types: Blunt, Slash, Pierce, Chop, Pickaxe, Fire, Frost, Lightning, Poison, Spirit. Modifiers: Normal, Resistant, VeryResistant, SlightlyResistant, Weak, VeryWeak, SlightlyWeak, Immune, Ignore. Empty for none."),

                JumpStaminaModifier = Bind(section, "JumpStaminaModifier", jump, new ConfigDescription("Change to jump stamina cost while worn. Negative is cheaper - wings should make leaving the ground easy.", rangeModifier)),
                RunStaminaModifier = Bind(section, "RunStaminaModifier", run, new ConfigDescription("Change to run stamina cost while worn. Negative is cheaper.", rangeModifier)),
                SneakStaminaModifier = Bind(section, "SneakStaminaModifier", sneak, new ConfigDescription("Change to sneak stamina cost while worn. Negative is cheaper.", rangeModifier)),
                EitrRegenModifier = Bind(section, "EitrRegenModifier", eitr, new ConfigDescription("Change to eitr regeneration while worn. Positive is faster.", rangeModifier)),
            };
        }

        // ---- lookups -----------------------------------------------------------------------

        /// <summary>The config section for a tier; unknown or null names fall back to Crude.</summary>
        public static TierConfig GetTier(string wingsName)
        {
            switch (wingsName)
            {
                case WingsItem.TrollName: return Troll;
                case WingsItem.LoxName: return Lox;
                case WingsItem.DragonName: return Dragon;
                default: return Crude;
            }
        }

        /// <summary>Per-tier flight stats at quality 1.</summary>
        public static WingStats GetStats(string wingsName) => GetStats(wingsName, 1);

        /// <summary>
        /// Per-tier flight stats with the wings' upgrade level folded in. Quality buys a little
        /// more of everything -- deliberately much less than the next tier up, because an upgrade
        /// is meant to be worth the cost without making the tier above it pointless.
        /// </summary>
        public static WingStats GetStats(string wingsName, int quality)
        {
            TierConfig tier = GetTier(wingsName);

            // Quality is read off an item, so it can be anything a save file or another mod put
            // there. Clamp before it multiplies anything.
            int levels = Mathf.Clamp(quality, 1, Mathf.Max(1, MaxQuality.Value)) - 1;

            float ceilingBonus = 1f + CeilingBonusPerLevel.Value * levels;
            float glideBonus = 1f + GlideSpeedBonusPerLevel.Value * levels;
            float flapBonus = 1f + FlapForceBonusPerLevel.Value * levels;
            float staminaCut = Mathf.Max(0f, 1f - FlapStaminaReductionPerLevel.Value * levels);

            return new WingStats(
                tier.FlightCeiling.Value * ceilingBonus,
                tier.GlideSpeed.Value * glideBonus,
                tier.FlapForce.Value * flapBonus,
                tier.FlapStaminaCost.Value * staminaCut,
                tier.MinSkillToFlap.Value);
        }
    }
}
