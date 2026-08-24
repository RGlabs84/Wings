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
        public const string PluginVersion = "2.0.4";
        
        private readonly Harmony harmony = new Harmony(PluginGUID);

        private void Awake()
        {
            Jotunn.Logger.LogInfo("Wings of the Valkyrie initializing...");
            
            ModConfig.Init(Config);
            FlyingSkill.Register();
            WingsItem.Init();
            Jotunn.Managers.CommandManager.Instance.AddConsoleCommand(new FlightLogCommand());
            
            harmony.PatchAll();
            
            Jotunn.Logger.LogInfo("Wings of the Valkyrie has loaded!");
        }

        // The flight export is the server's job and a dedicated server has no local player to
        // hang an update on, so the plugin itself carries the heartbeat. FlightReport.Tick does
        // nothing at all on a machine that is not acting as the server.
        private void Update()
        {
            if (!ModConfig.EnableMod.Value) return;
            FlightReport.Tick(UnityEngine.Time.deltaTime);
        }
    }
}
