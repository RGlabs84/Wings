using BepInEx;
using HarmonyLib;

namespace WingsoftheValkyrie
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    public class WingsoftheValkyriePlugin : BaseUnityPlugin
    {
        public const string PluginGUID = "wubarrk.wingsofthevalkyrie";
        public const string PluginName = "Wings of the Valkyrie";
        public const string PluginVersion = "2.1.5";

        // ServerSync's minimum-version pin, deliberately NOT PluginVersion. Pinning the two
        // together means every build disconnects every player until they update, which is right
        // for a release and ruinous for a test bed. Bump it only when a synced config entry is
        // renamed or retyped, or an RPC signature changes -- i.e. when an older client would
        // actually get it wrong. 2.1.0 is that kind of release: it drops Jotunn, so a 2.0.x
        // client's items are registered by a different mechanism entirely.
        //
        // 2.1.4 is the other kind: an older client does not merely get it wrong, it DESTROYS
        // data. On 2.1.0, 2.1.1 and 2.1.2 the game discards the player's Valkyrie Flight level
        // on every load and overwrites it at zero on the next save, and because the character
        // file lives on the player's own machine there is nothing a server can do to prevent it
        // except refuse the connection until they have updated. Turning someone away at the door
        // is the smaller harm.
        public const string SyncFloor = "2.1.4";

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

            // Only meaningful after PatchAll: it asks Harmony what actually attached, rather
            // than trusting that it did. See FlyingSkill.VerifyPersistencePatch.
            FlyingSkill.VerifyPersistencePatch();

            Log.LogInfo("Wings of the Valkyrie has loaded!");
        }

        // The flight export is the server's job and a dedicated server has no local player to
        // hang an update on, so the plugin itself carries the heartbeat. FlightReport.Tick does
        // nothing at all on a machine that is not acting as the server.
        private void Update()
        {
            if (!ModConfig.EnableMod.Value) return;
            FlightReport.Tick(UnityEngine.Time.deltaTime);

            // Registers the RPC (a dedicated server never spawns a Player, so nothing else
            // would) and drives the client's one-time restore request until it is answered.
            FlyingSkillRestore.Tick(UnityEngine.Time.deltaTime);
        }
    }
}
