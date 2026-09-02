using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace WingsoftheValkyrie
{
    /// <summary>
    /// Makes vanilla's <c>raiseskill</c> and <c>resetskill</c> console commands aware of Valkyrie
    /// Flight, and offers it in their tab completion.
    ///
    /// Vanilla cannot find a custom skill by name on its own: both cheats walk
    /// <c>Enum.GetValues(typeof(Skills.SkillType))</c> and compare <c>ToString()</c>, and a hashed
    /// skill type is not in that enum, so the only answer they ever have is "Skill not found".
    /// Jotunn's SkillManager patched all three of these; dropping Jotunn in 2.1.0 dropped them
    /// with it, which left no way to hand a level back to a player who had lost one.
    /// </summary>
    [HarmonyPatch]
    public static class FlyingSkillCommands
    {
        /// <summary>
        /// The one-word name the commands answer to. It has to be one word: Terminal.TryRunCommand
        /// splits the typed line on plain spaces with no quote handling and passes args[1] on, so
        /// "raiseskill Valkyrie Flight 50" reaches the cheat as the name "Valkyrie" and the amount
        /// "Flight". Jotunn matched on the display name, which is precisely why raising this skill
        /// from the console did not work under Jotunn either.
        /// </summary>
        public const string ConsoleName = "ValkyrieFlight";

        private static readonly string[] AcceptedNames =
        {
            ConsoleName,
            "valkyrie_flight",
            "wubarrk.wingsofthevalkyrie.flying"   // the skill identifier, for scripts
        };

        // Terminal.commands is protected static; ConsoleCommand.m_tabOptionsFetcher is private.
        private static readonly FieldInfo CommandsField = AccessTools.Field(typeof(Terminal), "commands");
        private static readonly FieldInfo TabOptionsFetcherField =
            AccessTools.Field(typeof(Terminal.ConsoleCommand), "m_tabOptionsFetcher");

        private static bool _tabOptionsAdded;

        private static bool IsOurs(string name)
        {
            if (string.IsNullOrEmpty(name) || !FlyingSkill.IsAvailable) return false;

            foreach (string accepted in AcceptedNames)
            {
                if (string.Equals(name, accepted, StringComparison.OrdinalIgnoreCase)) return true;
            }

            return false;
        }

        [HarmonyPatch(typeof(Skills), "CheatRaiseSkill")]
        [HarmonyPrefix]
        private static bool CheatRaiseSkillPrefix(Skills __instance, string name, float value, bool showMessage)
        {
            if (!IsOurs(name)) return true;   // not ours; let vanilla answer

            Skills.Skill skill = FlyingSkill.GetEntry(__instance);
            if (skill == null)
            {
                Print("Could not reach the Valkyrie Flight skill - see the log.");
                return false;
            }

            // Vanilla's own arithmetic, minus RebalanceSkills: that is private, it only does
            // anything when a server has turned on the total-skill cap, and Jotunn skipped it too.
            skill.m_level = Mathf.Clamp(skill.m_level + value, 0f, 100f);

            Player player = __instance.GetComponent<Player>();
            if (showMessage && player != null)
            {
                player.Message(MessageHud.MessageType.TopLeft,
                    $"Skill increased Valkyrie Flight: {(int)skill.m_level}", 0,
                    skill.m_info != null ? skill.m_info.m_icon : null);
            }

            Print($"Skill Valkyrie Flight = {skill.m_level}");
            return false;
        }

        [HarmonyPatch(typeof(Skills), "CheatResetSkill")]
        [HarmonyPrefix]
        private static bool CheatResetSkillPrefix(Skills __instance, string name)
        {
            if (!IsOurs(name)) return true;

            __instance.ResetSkill(FlyingSkill.SkillType);
            Print("Skill Valkyrie Flight reset");
            return false;
        }

        /// <summary>
        /// Appends the skill to both cheats' tab completion. InitTerminal is where vanilla builds
        /// the command table, so every command certainly exists by the time this runs -- but a
        /// postfix fires even on the call that returns early because the terminal is already
        /// initialised, hence the guard against wrapping the same fetcher twice.
        /// </summary>
        [HarmonyPatch(typeof(Terminal), "InitTerminal")]
        [HarmonyPostfix]
        private static void InitTerminalPostfix()
        {
            if (_tabOptionsAdded || !FlyingSkill.IsAvailable) return;
            if (CommandsField == null || TabOptionsFetcherField == null) return;

            var commands = CommandsField.GetValue(null) as Dictionary<string, Terminal.ConsoleCommand>;
            if (commands == null) return;

            _tabOptionsAdded = true;
            AddTabOption(commands, "raiseskill");
            AddTabOption(commands, "resetskill");
        }

        private static void AddTabOption(Dictionary<string, Terminal.ConsoleCommand> commands, string commandName)
        {
            if (!commands.TryGetValue(commandName, out Terminal.ConsoleCommand command) || command == null)
            {
                Log.LogWarning($"No '{commandName}' console command to add Valkyrie Flight to; its tab completion will not list the skill.");
                return;
            }

            try
            {
                var inner = TabOptionsFetcherField.GetValue(command) as Terminal.ConsoleOptionsFetcher;

                TabOptionsFetcherField.SetValue(command, (Terminal.ConsoleOptionsFetcher)(() =>
                {
                    List<string> options = inner != null ? inner() : null;
                    if (options == null) options = new List<string>();
                    if (!options.Contains(ConsoleName)) options.Add(ConsoleName);
                    return options;
                }));
            }
            catch (Exception ex)
            {
                Log.LogWarning($"Could not extend '{commandName}' tab completion ({ex.Message}). The command still works; the skill just will not be offered.");
            }
        }

        // Valheim's Console, not System.Console.
        private static void Print(string line)
        {
            if (global::Console.instance != null) global::Console.instance.Print(line);
        }
    }
}
