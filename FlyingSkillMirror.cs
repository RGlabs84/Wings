using System;
using System.Collections.Generic;
using System.Globalization;
using HarmonyLib;

namespace WingsoftheValkyrie
{
    /// <summary>
    /// A second copy of the Valkyrie Flight level, kept somewhere Valheim will not throw away.
    ///
    /// The 2.1.0 bug was not that the level was stored badly -- it was that vanilla's skill
    /// loader silently discards anything it does not recognise, and nothing noticed for three
    /// releases. <c>Player.m_customData</c> has no such filter: it is why the flight logbook
    /// came through that bug untouched while the skill did not. So the level is mirrored there
    /// too, and if the skill ever comes back from a load missing entirely while the mirror still
    /// remembers it, it is put back.
    ///
    /// This is insurance, not the mechanism -- <see cref="FlyingSkill"/>'s IsSkillValid patch is
    /// what makes the skill persist normally. It exists so that a Valheim update, a Harmony
    /// conflict or another mod patching the same method cannot silently delete anyone's progress
    /// again. It deliberately does NOT depend on the logbook being switched on.
    /// </summary>
    [HarmonyPatch(typeof(Player))]
    public static class FlyingSkillMirror
    {
        private const string CustomDataKey = "wubarrk.wotv.flyingskill";

        /// <summary>Whether the mirror can be relied on to carry the level by itself. The custom
        /// data dictionary is a plain public vanilla field, so this is only false if a future
        /// Valheim removes it -- but <see cref="FlyingSkill"/> asks before deciding whether XP
        /// earned right now is safe to award, so it is worth answering honestly.</summary>
        public static bool CanMirror { get; private set; } = true;

        /// <summary>
        /// Writes the level out beside the character. A prefix, because Player.Save serialises
        /// m_customData as part of the same call -- by the postfix it is already on disk.
        /// </summary>
        [HarmonyPatch("Save")]
        [HarmonyPrefix]
        public static void SavePrefix(Player __instance)
        {
            if (__instance == null || !FlyingSkill.IsAvailable) return;

            try
            {
                // FindEntry, never GetEntry: reading must not be what gives a character the
                // skill. No entry means this Viking has never flown, and the key stays absent.
                Skills.Skill entry = FlyingSkill.FindEntry(__instance);
                if (entry == null) return;

                Dictionary<string, string> data = __instance.m_customData;
                if (data == null) { CanMirror = false; return; }

                data[CustomDataKey] =
                    entry.m_level.ToString("R", CultureInfo.InvariantCulture) + ";" +
                    entry.m_accumulator.ToString("R", CultureInfo.InvariantCulture);

                CanMirror = true;
            }
            catch (Exception ex)
            {
                CanMirror = false;
                Log.LogWarning($"Could not mirror the Valkyrie Flight level onto the character: {ex.Message}");
            }
        }

        /// <summary>
        /// Puts the level back if the loader dropped it. Player.Load reads the skills first and
        /// the custom data immediately after, so by this postfix both are in place and the two
        /// can be compared.
        ///
        /// The test is "the skill is missing entirely", never "the skill is lower than the
        /// mirror". Those are not the same thing: dying lowers the level by a quarter and leaves
        /// the entry in place, and restoring over that would quietly cancel the death penalty.
        /// </summary>
        [HarmonyPatch("Load")]
        [HarmonyPostfix]
        public static void LoadPostfix(Player __instance)
        {
            if (__instance == null || !FlyingSkill.IsAvailable) return;
            if (FlyingSkill.HasEntry(__instance)) return;   // the skill loaded fine

            try
            {
                Dictionary<string, string> data = __instance.m_customData;
                if (data == null || !data.TryGetValue(CustomDataKey, out string stored) || string.IsNullOrEmpty(stored)) return;

                if (!TryParse(stored, out float level, out float accumulator)) return;
                if (level <= 0f) return;   // nothing worth restoring

                if (!FlyingSkill.SetLevel(__instance, level, accumulator)) return;

                Log.LogWarning(
                    $"Valheim discarded the Valkyrie Flight skill on load; restored it to level {(int)level} " +
                    "from the copy kept on the character. Flight itself is unaffected, but this should not " +
                    "happen -- please report it.");
            }
            catch (Exception ex)
            {
                Log.LogWarning($"Could not read the mirrored Valkyrie Flight level: {ex.Message}");
            }
        }

        private static bool TryParse(string stored, out float level, out float accumulator)
        {
            level = 0f;
            accumulator = 0f;

            string[] parts = stored.Split(';');
            if (parts.Length < 1) return false;

            if (!float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out level)) return false;

            // An older mirror carried the level alone; a missing accumulator is not a failure.
            if (parts.Length > 1)
            {
                float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out accumulator);
            }

            return true;
        }
    }
}
