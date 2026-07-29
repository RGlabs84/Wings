using BepInEx.Configuration;
using Jotunn.Configs;

namespace WingsoftheValkyrie
{
    public static class ModConfig
    {
        public static ConfigEntry<bool> EnableMod { get; private set; }
        public static ConfigEntry<float> GlobalWingSpan { get; private set; }

        // Tier 1 - Crude
        public static ConfigEntry<float> CrudeFlightCeiling { get; private set; }
        public static ConfigEntry<float> CrudeGlideSpeed { get; private set; }
        public static ConfigEntry<float> CrudeFlapForce { get; private set; }
        public static ConfigEntry<float> CrudeFlapStaminaCost { get; private set; }
        public static ConfigEntry<string> CrudeCraftingStation { get; private set; }
        public static ConfigEntry<int> CrudeMinStationLevel { get; private set; }
        public static ConfigEntry<string> CrudeCraftingRequirements { get; private set; }

        // Tier 2 - Troll
        public static ConfigEntry<float> TrollFlightCeiling { get; private set; }
        public static ConfigEntry<float> TrollGlideSpeed { get; private set; }
        public static ConfigEntry<float> TrollFlapForce { get; private set; }
        public static ConfigEntry<float> TrollFlapStaminaCost { get; private set; }
        public static ConfigEntry<string> TrollCraftingStation { get; private set; }
        public static ConfigEntry<int> TrollMinStationLevel { get; private set; }
        public static ConfigEntry<string> TrollCraftingRequirements { get; private set; }

        // Tier 3 - Lox
        public static ConfigEntry<float> LoxFlightCeiling { get; private set; }
        public static ConfigEntry<float> LoxGlideSpeed { get; private set; }
        public static ConfigEntry<float> LoxFlapForce { get; private set; }
        public static ConfigEntry<float> LoxFlapStaminaCost { get; private set; }
        public static ConfigEntry<string> LoxCraftingStation { get; private set; }
        public static ConfigEntry<int> LoxMinStationLevel { get; private set; }
        public static ConfigEntry<string> LoxCraftingRequirements { get; private set; }

        // Tier 4 - Dragon
        public static ConfigEntry<float> DragonFlightCeiling { get; private set; }
        public static ConfigEntry<float> DragonGlideSpeed { get; private set; }
        public static ConfigEntry<float> DragonFlapForce { get; private set; }
        public static ConfigEntry<float> DragonFlapStaminaCost { get; private set; }
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

            EnableMod = config.Bind("1. General", "EnableMod", true, new ConfigDescription("Enable or disable the Valkyrie Wings mod.", null, adminOnly));
            GlobalWingSpan = config.Bind("1. General", "GlobalWingSpan", 1.0f, new ConfigDescription("Multiplier for the overall size of the wings.", new AcceptableValueRange<float>(0.1f, 5.0f), adminOnly));

            // Crude
            CrudeFlightCeiling = config.Bind("2. Crude Wings", "FlightCeiling", 120f, new ConfigDescription("Flight ceiling limit above ground.", rangeCeiling, adminOnly));
            CrudeGlideSpeed = config.Bind("2. Crude Wings", "GlideSpeed", 10f, new ConfigDescription("Horizontal glide speed.", rangeSpeed, adminOnly));
            CrudeFlapForce = config.Bind("2. Crude Wings", "FlapForce", 15f, new ConfigDescription("Upward lift force when flapping.", rangeForce, adminOnly));
            CrudeFlapStaminaCost = config.Bind("2. Crude Wings", "FlapStaminaCost", 10f, new ConfigDescription("Stamina consumed per flap.", rangeStamina, adminOnly));
            CrudeCraftingStation = config.Bind("2. Crude Wings", "CraftingStation", "piece_workbench", new ConfigDescription("Prefab name of the crafting station.", null, adminOnly));
            CrudeMinStationLevel = config.Bind("2. Crude Wings", "MinStationLevel", 1, new ConfigDescription("Minimum station level required.", rangeLevel, adminOnly));
            CrudeCraftingRequirements = config.Bind("2. Crude Wings", "CraftingRequirements", "Feathers:10,LeatherScraps:10", new ConfigDescription("Required items. Format: ItemName:Amount,ItemName:Amount", null, adminOnly));

            // Troll
            TrollFlightCeiling = config.Bind("3. Troll Wings", "FlightCeiling", 135f, new ConfigDescription("Flight ceiling limit above ground.", rangeCeiling, adminOnly));
            TrollGlideSpeed = config.Bind("3. Troll Wings", "GlideSpeed", 15f, new ConfigDescription("Horizontal glide speed.", rangeSpeed, adminOnly));
            TrollFlapForce = config.Bind("3. Troll Wings", "FlapForce", 18f, new ConfigDescription("Upward lift force when flapping.", rangeForce, adminOnly));
            TrollFlapStaminaCost = config.Bind("3. Troll Wings", "FlapStaminaCost", 8f, new ConfigDescription("Stamina consumed per flap.", rangeStamina, adminOnly));
            TrollCraftingStation = config.Bind("3. Troll Wings", "CraftingStation", "piece_workbench", new ConfigDescription("Prefab name of the crafting station.", null, adminOnly));
            TrollMinStationLevel = config.Bind("3. Troll Wings", "MinStationLevel", 2, new ConfigDescription("Minimum station level required.", rangeLevel, adminOnly));
            TrollCraftingRequirements = config.Bind("3. Troll Wings", "CraftingRequirements", "TrollHide:5,Feathers:15", new ConfigDescription("Required items.", null, adminOnly));

            // Lox
            LoxFlightCeiling = config.Bind("4. Lox Wings", "FlightCeiling", 160f, new ConfigDescription("Flight ceiling limit above ground.", rangeCeiling, adminOnly));
            LoxGlideSpeed = config.Bind("4. Lox Wings", "GlideSpeed", 20f, new ConfigDescription("Horizontal glide speed.", rangeSpeed, adminOnly));
            LoxFlapForce = config.Bind("4. Lox Wings", "FlapForce", 22f, new ConfigDescription("Upward lift force when flapping.", rangeForce, adminOnly));
            LoxFlapStaminaCost = config.Bind("4. Lox Wings", "FlapStaminaCost", 6f, new ConfigDescription("Stamina consumed per flap.", rangeStamina, adminOnly));
            LoxCraftingStation = config.Bind("4. Lox Wings", "CraftingStation", "forge", new ConfigDescription("Prefab name of the crafting station.", null, adminOnly));
            LoxMinStationLevel = config.Bind("4. Lox Wings", "MinStationLevel", 1, new ConfigDescription("Minimum station level required.", rangeLevel, adminOnly));
            LoxCraftingRequirements = config.Bind("4. Lox Wings", "CraftingRequirements", "LoxPelt:5,Silver:5", new ConfigDescription("Required items.", null, adminOnly));

            // Dragon
            DragonFlightCeiling = config.Bind("5. Dragon Wings", "FlightCeiling", 1100f, new ConfigDescription("Flight ceiling limit above ground.", rangeCeiling, adminOnly));
            DragonGlideSpeed = config.Bind("5. Dragon Wings", "GlideSpeed", 30f, new ConfigDescription("Horizontal glide speed.", rangeSpeed, adminOnly));
            DragonFlapForce = config.Bind("5. Dragon Wings", "FlapForce", 28f, new ConfigDescription("Upward lift force when flapping.", rangeForce, adminOnly));
            DragonFlapStaminaCost = config.Bind("5. Dragon Wings", "FlapStaminaCost", 4f, new ConfigDescription("Stamina consumed per flap.", rangeStamina, adminOnly));
            DragonCraftingStation = config.Bind("5. Dragon Wings", "CraftingStation", "piece_magetable", new ConfigDescription("Prefab name of the crafting station.", null, adminOnly));
            DragonMinStationLevel = config.Bind("5. Dragon Wings", "MinStationLevel", 1, new ConfigDescription("Minimum station level required.", rangeLevel, adminOnly));
            DragonCraftingRequirements = config.Bind("5. Dragon Wings", "CraftingRequirements", "Feathers:20,Eitr:5", new ConfigDescription("Required items.", null, adminOnly));
        }
    }
}
