using HarmonyLib;

namespace WingsoftheValkyrie
{
    /// <summary>
    /// Ties the flight logbook to the character's own load/save cycle, so the saga is read at
    /// the moment the character's custom data exists and written at the moment the game is
    /// about to serialise it. Every hook is a no-op when the logbook is switched off.
    /// </summary>
    [HarmonyPatch(typeof(Player))]
    public static class FlightLogPatches
    {
        // Player.Load is only ever called for the character the profile belongs to -- remote
        // players arrive over the network, never through here -- so this needs no local check.
        [HarmonyPatch("Load")]
        [HarmonyPostfix]
        public static void LoadPostfix(Player __instance)
        {
            if (!ModConfig.EnableFlightLog.Value) return;
            FlightLog.LoadFrom(__instance);
        }

        // A brand new character is never Load()ed, so this is what binds the saga for a Viking
        // taking their very first breath. For everyone else Load already ran and this is a no-op.
        [HarmonyPatch("OnSpawned")]
        [HarmonyPostfix]
        public static void OnSpawnedPostfix(Player __instance)
        {
            if (!ModConfig.EnableFlightLog.Value) return;
            if (__instance != Player.m_localPlayer) return;
            FlightLog.EnsureLoaded(__instance);
        }

        // Prefix, not postfix: the saga has to be inside m_customData before Save writes it out.
        [HarmonyPatch("Save")]
        [HarmonyPrefix]
        public static void SavePrefix(Player __instance)
        {
            if (!ModConfig.EnableFlightLog.Value) return;
            FlightLog.Flush(__instance, force: true);
        }
    }
}
