using BepInEx;
using HarmonyLib;
using Jotunn.Utils;

namespace WingsoftheValkyrie
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    public class WingsoftheValkyriePlugin : BaseUnityPlugin
    {
        public const string PluginGUID = "wubarrk.wingsofthevalkyrie";
        public const string PluginName = "Wings of the Valkyrie";
        public const string PluginVersion = "1.1.5";
        
        private readonly Harmony harmony = new Harmony(PluginGUID);

        private void Awake()
        {
            Jotunn.Logger.LogInfo("Wings of the Valkyrie initializing...");
            
            ModConfig.Init(Config);
            WingsItem.Init();
            
            harmony.PatchAll();
            
            Jotunn.Logger.LogInfo("Wings of the Valkyrie has loaded!");
        }
    }
}
