using BepInEx;
using HarmonyLib;

namespace WingsoftheValkyrie
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    public class WingsoftheValkyriePlugin : BaseUnityPlugin
    {
        public const string PluginGUID = "wubarrk.wingsofthevalkyrie";
        public const string PluginName = "Wings of the Valkyrie";
        public const string PluginVersion = "2.1.0";

        // ServerSync's minimum-version pin, deliberately NOT PluginVersion. Pinning the two
        // together means every build disconnects every player until they update, which is right
        // for a release and ruinous for a test bed. Bump it only when a synced config entry is
        // renamed or retyped, or an RPC signature changes -- i.e. when an older client would
        // actually get it wrong. 2.1.0 is that kind of release: it drops Jotunn, so a 2.0.x
        // client's items are registered by a different mechanism entirely.
        public const string SyncFloor = "2.1.0";

        private readonly Harmony harmony = new Harmony(PluginGUID);

        private void Awake()
        {
            Log.Init(Logger);
            Log.LogInfo("Wings of the Valkyrie initializing...");

            ModConfig.Init(Config);
            FlyingSkill.Register();
            WingsItem.Init();
            FlightLogCommand.Register();

            harmony.PatchAll();

            Log.LogInfo("Wings of the Valkyrie has loaded!");
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
