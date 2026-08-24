using BepInEx.Configuration;
using Jotunn.Configs;

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

        public static ConfigEntry<int> ConfigVersion { get; private set; }

        public static ConfigEntry<bool> EnableMod { get; private set; }
        public static ConfigEntry<float> GlobalWingSpan { get; private set; }
        public static ConfigEntry<float> BaseGlideSinkRate { get; private set; }
        public static ConfigEntry<float> MaxDiveSpeed { get; private set; }

        // Valkyrie Flight skill
        public static ConfigEntry<float> SkillXpPerFlap { get; private set; }
        public static ConfigEntry<float> SkillXpPerGlideSecond { get; private set; }
        public static ConfigEntry<float> SkillStaminaReduction { get; private set; }
        public static ConfigEntry<float> SkillFlapPowerBonus { get; private set; }
        public static ConfigEntry<float> SkillGlideSinkReduction { get; private set; }
        public static ConfigEntry<float> SkillGlideSpeedBonus { get; private set; }
        public static ConfigEntry<float> CeilingAtNovice { get; private set; }

        // Flight logbook
        public static ConfigEntry<bool> EnableFlightLog { get; private set; }
        public static ConfigEntry<bool> PublishFlightStats { get; private set; }
        public static ConfigEntry<string> FlightStatsExportFolder { get; private set; }
        public static ConfigEntry<float> FlightStatsWriteInterval { get; private set; }
        public static ConfigEntry<float> FlightStatsReportInterval { get; private set; }

        // Tier 1 - Crude
        public static ConfigEntry<float> CrudeFlightCeiling { get; private set; }
        public static ConfigEntry<float> CrudeGlideSpeed { get; private set; }
        public static ConfigEntry<float> CrudeFlapForce { get; private set; }
        public static ConfigEntry<float> CrudeFlapStaminaCost { get; private set; }
        public static ConfigEntry<float> CrudeMinSkillToFlap { get; private set; }
        public static ConfigEntry<string> CrudeCraftingStation { get; private set; }
        public static ConfigEntry<int> CrudeMinStationLevel { get; private set; }
        public static ConfigEntry<string> CrudeCraftingRequirements { get; private set; }

        // Tier 2 - Troll
        public static ConfigEntry<float> TrollFlightCeiling { get; private set; }
        public static ConfigEntry<float> TrollGlideSpeed { get; private set; }
        public static ConfigEntry<float> TrollFlapForce { get; private set; }
        public static ConfigEntry<float> TrollFlapStaminaCost { get; private set; }
        public static ConfigEntry<float> TrollMinSkillToFlap { get; private set; }
        public static ConfigEntry<string> TrollCraftingStation { get; private set; }
        public static ConfigEntry<int> TrollMinStationLevel { get; private set; }
        public static ConfigEntry<string> TrollCraftingRequirements { get; private set; }

        // Tier 3 - Lox
        public static ConfigEntry<float> LoxFlightCeiling { get; private set; }
        public static ConfigEntry<float> LoxGlideSpeed { get; private set; }
        public static ConfigEntry<float> LoxFlapForce { get; private set; }
        public static ConfigEntry<float> LoxFlapStaminaCost { get; private set; }
        public static ConfigEntry<float> LoxMinSkillToFlap { get; private set; }
        public static ConfigEntry<string> LoxCraftingStation { get; private set; }
        public static ConfigEntry<int> LoxMinStationLevel { get; private set; }
        public static ConfigEntry<string> LoxCraftingRequirements { get; private set; }

        // Tier 4 - Dragon
        public static ConfigEntry<float> DragonFlightCeiling { get; private set; }
        public static ConfigEntry<float> DragonGlideSpeed { get; private set; }
        public static ConfigEntry<float> DragonFlapForce { get; private set; }
        public static ConfigEntry<float> DragonFlapStaminaCost { get; private set; }
        public static ConfigEntry<float> DragonMinSkillToFlap { get; private set; }
        public static ConfigEntry<string> DragonCraftingStation { get; private set; }
        public static ConfigEntry<int> DragonMinStationLevel { get; private set; }
        public static ConfigEntry<string> DragonCraftingRequirements { get; private set; }

        public static void Init(ConfigFile config)
        {
            var adminOnly = new ConfigurationManagerAttributes { IsAdminOnly = true };
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

            EnableMod = config.Bind("1. General", "EnableMod", true, new ConfigDescription("Enable or disable the Valkyrie Wings mod.", null, adminOnly));
            GlobalWingSpan = config.Bind("1. General", "GlobalWingSpan", 1.0f, new ConfigDescription("Multiplier for the overall size of the wings.", new AcceptableValueRange<float>(0.1f, 5.0f), adminOnly));
            BaseGlideSinkRate = config.Bind("1. General", "BaseGlideSinkRate", 2.5f, new ConfigDescription("How fast a level 0 flier sinks while gliding level, in metres per second. Skill flattens this (see GlideSinkReductionAtMax).", new AcceptableValueRange<float>(0f, 20f), adminOnly));
            MaxDiveSpeed = config.Bind("1. General", "MaxDiveSpeed", 20f, new ConfigDescription("Descent speed at a full straight-down dive, in metres per second. Never scaled by skill - diving is intent, not practice.", new AcceptableValueRange<float>(1f, 100f), adminOnly));

            // Crude
            CrudeFlightCeiling = config.Bind("2. Crude Wings", "FlightCeiling", 130f, new ConfigDescription("Flight ceiling above ground at Valkyrie Flight 100. Lower levels reach only a fraction of it (see CeilingAtNovice).", rangeCeiling, adminOnly));
            CrudeGlideSpeed = config.Bind("2. Crude Wings", "GlideSpeed", 8f, new ConfigDescription("Horizontal glide speed before skill bonuses.", rangeSpeed, adminOnly));
            CrudeFlapForce = config.Bind("2. Crude Wings", "FlapForce", 11f, new ConfigDescription("Upward lift force when flapping, before skill bonuses.", rangeForce, adminOnly));
            CrudeFlapStaminaCost = config.Bind("2. Crude Wings", "FlapStaminaCost", 16f, new ConfigDescription("Stamina consumed per flap before skill reductions.", rangeStamina, adminOnly));
            CrudeMinSkillToFlap = config.Bind("2. Crude Wings", "MinSkillToFlap", 0f, new ConfigDescription("Valkyrie Flight level needed to flap these wings. Below it they still unfurl and glide, so the skill can always be earned. 0 = no requirement.", rangeSkillLevel, adminOnly));
            CrudeCraftingStation = config.Bind("2. Crude Wings", "CraftingStation", "piece_workbench", new ConfigDescription("Prefab name of the crafting station.", null, adminOnly));
            CrudeMinStationLevel = config.Bind("2. Crude Wings", "MinStationLevel", 3, new ConfigDescription("Minimum station level required.", rangeLevel, adminOnly));
            CrudeCraftingRequirements = config.Bind("2. Crude Wings", "CraftingRequirements", "BronzeNails:10,DeerHide:10,LeatherScraps:20,TrollHide:5,Feathers:20", new ConfigDescription("Required items. Format: ItemName:Amount,ItemName:Amount", null, adminOnly));

            // Troll
            TrollFlightCeiling = config.Bind("3. Troll Wings", "FlightCeiling", 150f, new ConfigDescription("Flight ceiling above ground at Valkyrie Flight 100.", rangeCeiling, adminOnly));
            TrollGlideSpeed = config.Bind("3. Troll Wings", "GlideSpeed", 12f, new ConfigDescription("Horizontal glide speed before skill bonuses.", rangeSpeed, adminOnly));
            TrollFlapForce = config.Bind("3. Troll Wings", "FlapForce", 13f, new ConfigDescription("Upward lift force when flapping, before skill bonuses.", rangeForce, adminOnly));
            TrollFlapStaminaCost = config.Bind("3. Troll Wings", "FlapStaminaCost", 13f, new ConfigDescription("Stamina consumed per flap before skill reductions.", rangeStamina, adminOnly));
            TrollMinSkillToFlap = config.Bind("3. Troll Wings", "MinSkillToFlap", 15f, new ConfigDescription("Valkyrie Flight level needed to flap these wings. Below it they still unfurl and glide.", rangeSkillLevel, adminOnly));
            TrollCraftingStation = config.Bind("3. Troll Wings", "CraftingStation", "piece_workbench", new ConfigDescription("Prefab name of the crafting station.", null, adminOnly));
            TrollMinStationLevel = config.Bind("3. Troll Wings", "MinStationLevel", 3, new ConfigDescription("Minimum station level required.", rangeLevel, adminOnly));
            TrollCraftingRequirements = config.Bind("3. Troll Wings", "CraftingRequirements", "TrollHide:15,IronNails:10,Feathers:30", new ConfigDescription("Required items.", null, adminOnly));

            // Lox
            LoxFlightCeiling = config.Bind("4. Lox Wings", "FlightCeiling", 190f, new ConfigDescription("Flight ceiling above ground at Valkyrie Flight 100.", rangeCeiling, adminOnly));
            LoxGlideSpeed = config.Bind("4. Lox Wings", "GlideSpeed", 16f, new ConfigDescription("Horizontal glide speed before skill bonuses.", rangeSpeed, adminOnly));
            LoxFlapForce = config.Bind("4. Lox Wings", "FlapForce", 16f, new ConfigDescription("Upward lift force when flapping, before skill bonuses.", rangeForce, adminOnly));
            LoxFlapStaminaCost = config.Bind("4. Lox Wings", "FlapStaminaCost", 10f, new ConfigDescription("Stamina consumed per flap before skill reductions.", rangeStamina, adminOnly));
            LoxMinSkillToFlap = config.Bind("4. Lox Wings", "MinSkillToFlap", 30f, new ConfigDescription("Valkyrie Flight level needed to flap these wings. Below it they still unfurl and glide.", rangeSkillLevel, adminOnly));
            LoxCraftingStation = config.Bind("4. Lox Wings", "CraftingStation", "forge", new ConfigDescription("Prefab name of the crafting station.", null, adminOnly));
            LoxMinStationLevel = config.Bind("4. Lox Wings", "MinStationLevel", 3, new ConfigDescription("Minimum station level required.", rangeLevel, adminOnly));
            LoxCraftingRequirements = config.Bind("4. Lox Wings", "CraftingRequirements", "LoxPelt:10,Silver:20,LinenThread:10,Feathers:30", new ConfigDescription("Required items.", null, adminOnly));

            // Dragon
            DragonFlightCeiling = config.Bind("5. Dragon Wings", "FlightCeiling", 1300f, new ConfigDescription("Flight ceiling above ground at Valkyrie Flight 100.", rangeCeiling, adminOnly));
            DragonGlideSpeed = config.Bind("5. Dragon Wings", "GlideSpeed", 24f, new ConfigDescription("Horizontal glide speed before skill bonuses.", rangeSpeed, adminOnly));
            DragonFlapForce = config.Bind("5. Dragon Wings", "FlapForce", 20f, new ConfigDescription("Upward lift force when flapping, before skill bonuses.", rangeForce, adminOnly));
            DragonFlapStaminaCost = config.Bind("5. Dragon Wings", "FlapStaminaCost", 7f, new ConfigDescription("Stamina consumed per flap before skill reductions.", rangeStamina, adminOnly));
            DragonMinSkillToFlap = config.Bind("5. Dragon Wings", "MinSkillToFlap", 50f, new ConfigDescription("Valkyrie Flight level needed to flap these wings. Below it they still unfurl and glide.", rangeSkillLevel, adminOnly));
            DragonCraftingStation = config.Bind("5. Dragon Wings", "CraftingStation", "piece_magetable", new ConfigDescription("Prefab name of the crafting station.", null, adminOnly));
            DragonMinStationLevel = config.Bind("5. Dragon Wings", "MinStationLevel", 1, new ConfigDescription("Minimum station level required.", rangeLevel, adminOnly));
            DragonCraftingRequirements = config.Bind("5. Dragon Wings", "CraftingRequirements", "Feathers:40,Eitr:20,ScaleHide:10,DragonTear:2", new ConfigDescription("Required items.", null, adminOnly));

            // Valkyrie Flight skill
            SkillXpPerFlap = config.Bind("6. Valkyrie Flight Skill", "XpPerFlap", 0.4f, new ConfigDescription("Skill XP awarded per wing flap.", rangeXp, adminOnly));
            SkillXpPerGlideSecond = config.Bind("6. Valkyrie Flight Skill", "XpPerGlideSecond", 0.2f, new ConfigDescription("Skill XP awarded per second spent gliding. This is the only way a flier below a tier's MinSkillToFlap can earn levels, so keep it above 0.", rangeXp, adminOnly));
            SkillStaminaReduction = config.Bind("6. Valkyrie Flight Skill", "StaminaReductionAtMax", 0.55f, new ConfigDescription("Fraction of the flap stamina cost removed at skill level 100.", rangeFraction, adminOnly));
            SkillFlapPowerBonus = config.Bind("6. Valkyrie Flight Skill", "FlapPowerBonusAtMax", 0.5f, new ConfigDescription("Extra flap lift at skill level 100, as a fraction of the tier's base force.", rangeFraction, adminOnly));
            SkillGlideSinkReduction = config.Bind("6. Valkyrie Flight Skill", "GlideSinkReductionAtMax", 0.6f, new ConfigDescription("How much slower you sink while gliding at skill level 100 (longer glides).", rangeFraction, adminOnly));
            SkillGlideSpeedBonus = config.Bind("6. Valkyrie Flight Skill", "GlideSpeedBonusAtMax", 0.35f, new ConfigDescription("Extra horizontal glide speed at skill level 100, as a fraction.", rangeFraction, adminOnly));
            CeilingAtNovice = config.Bind("6. Valkyrie Flight Skill", "CeilingAtNovice", 0.35f, new ConfigDescription("Fraction of a tier's flight ceiling available at skill level 0, rising to the full ceiling at 100. 1 = altitude is not gated by skill at all.", rangeFraction, adminOnly));

            // Flight logbook. Bookkeeping rather than balance, so deliberately NOT admin-synced:
            // whether a player's own flights are written down is theirs to decide, and where a
            // server puts its export file is a property of that machine, not of the game rules.
            EnableFlightLog = config.Bind("7. Flight Logbook", "EnableFlightLog", true,
                "Track your flight saga - time flown, distance, records and oddities - and store it on your character. Read it in-game with the 'wov' console command.");
            PublishFlightStats = config.Bind("7. Flight Logbook", "PublishFlightStats", true,
                "Take part in the server's flight statistics. On a client this sends your totals to the server you are playing on; on a server it publishes everyone's totals to a JSON file that tools such as BarrkBOT read. Set it to false on a client to keep your own logbook but stay out of the server's.");
            FlightStatsExportFolder = config.Bind("7. Flight Logbook", "FlightStatsExportFolder", "",
                "Server only. Folder to write the flight statistics into. Leave empty for 'WingsOfTheValkyrie' beside this config file, which is where the export sweep looks - change it only if you know the reader is looking somewhere else.");
            FlightStatsWriteInterval = config.Bind("7. Flight Logbook", "FlightStatsWriteIntervalSeconds", 60f, new ConfigDescription(
                "Server only. How often the flight statistics file is rewritten, in seconds.", new AcceptableValueRange<float>(5f, 3600f)));
            FlightStatsReportInterval = config.Bind("7. Flight Logbook", "FlightStatsReportIntervalSeconds", 60f, new ConfigDescription(
                "Client only. How often your totals are sent to the server while you play, in seconds.", new AcceptableValueRange<float>(5f, 3600f)));

            ConfigMigration.Finish(config, ConfigVersion);
        }

        /// <summary>Per-tier flight stats; unknown or null names fall back to Crude values.</summary>
        public static WingStats GetStats(string wingsName)
        {
            switch (wingsName)
            {
                case WingsItem.TrollName:
                    return new WingStats(TrollFlightCeiling.Value, TrollGlideSpeed.Value, TrollFlapForce.Value, TrollFlapStaminaCost.Value, TrollMinSkillToFlap.Value);
                case WingsItem.LoxName:
                    return new WingStats(LoxFlightCeiling.Value, LoxGlideSpeed.Value, LoxFlapForce.Value, LoxFlapStaminaCost.Value, LoxMinSkillToFlap.Value);
                case WingsItem.DragonName:
                    return new WingStats(DragonFlightCeiling.Value, DragonGlideSpeed.Value, DragonFlapForce.Value, DragonFlapStaminaCost.Value, DragonMinSkillToFlap.Value);
                default:
                    return new WingStats(CrudeFlightCeiling.Value, CrudeGlideSpeed.Value, CrudeFlapForce.Value, CrudeFlapStaminaCost.Value, CrudeMinSkillToFlap.Value);
            }
        }
    }
}
