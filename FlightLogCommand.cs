using System.Collections.Generic;

namespace WingsoftheValkyrie
{
    /// <summary>
    /// The <c>wov</c> console command: reads out the flight saga in game and writes the export
    /// file on demand. Not a cheat and not networked -- it only ever reports the local
    /// character's own numbers.
    ///
    /// Registered straight onto vanilla's Terminal rather than through Jotunn's CommandManager.
    /// Terminal.ConsoleCommand's constructor inserts itself into the static command table, so
    /// building one is the registration; InitTerminal only ever adds to that table, never
    /// rebuilds it, so registering before the terminal is first opened is safe.
    /// </summary>
    public static class FlightLogCommand
    {
        private const string Name = "wov";

        private const string Help =
            "Wings of the Valkyrie. 'wov log' for your flight saga, 'wov oddities' for the strange corners of it, " +
            "'wov export' to write the logbook out as JSON, 'wov where' for that file's path.";

        private static readonly List<string> Options = new List<string> { "log", "oddities", "export", "where" };

        public static void Register()
        {
            new Terminal.ConsoleCommand(Name, Help, Run, isCheat: false, isNetwork: false, onlyServer: false,
                isSecret: false, allowInDevBuild: false, optionsFetcher: () => Options);
        }

        private static void Run(Terminal.ConsoleEventArgs args)
        {
            Player player = Player.m_localPlayer;
            if (player == null)
            {
                Print(args, "No Viking to speak of - load a character first.");
                return;
            }

            FlightLog.EnsureLoaded(player);

            // Args[0] is the command itself; vanilla does not strip it the way Jotunn did.
            string subcommand = args != null && args.Length > 1 ? args[1].ToLowerInvariant() : "log";

            switch (subcommand)
            {
                case "log":
                case "oddities":
                    foreach (string line in FlightLog.Report(player, includeOddities: subcommand == "oddities"))
                    {
                        Print(args, line);
                    }
                    break;

                case "export":
                {
                    // Flushing always reports upward; on a server or a solo world that lands in
                    // the registry immediately, so the file can be written in the same breath.
                    FlightLog.Flush(player, force: true);

                    if (!ModConfig.PublishFlightStats.Value)
                    {
                        Print(args, "Flight statistics are switched off here - set PublishFlightStats to true in the config.");
                        break;
                    }

                    if (ZNet.instance != null && !ZNet.instance.IsServer())
                    {
                        Print(args, "Sent your flight saga to the server. The server owns the statistics file - ask its admin, or run this on the server itself.");
                        break;
                    }

                    string path = FlightReport.WriteExport();
                    Print(args, path != null
                        ? "Flight statistics written to " + path
                        : "Could not write the flight statistics - see the log for why.");
                    break;
                }

                case "where":
                    Print(args, "Flight statistics file: " + FlightReport.ExportPath());
                    Print(args, "Your own saga lives on your character, not in that file.");
                    break;

                default:
                    Print(args, "Unknown option '" + subcommand + "'. Try: log, oddities, export, where.");
                    break;
            }
        }

        /// <summary>Answers into whichever terminal asked. Falls back to the console so a call
        /// from a context without a terminal still reaches the player.</summary>
        private static void Print(Terminal.ConsoleEventArgs args, string line)
        {
            Terminal terminal = args != null ? args.Context : null;

            if (terminal != null) terminal.AddString(line);
            else if (Console.instance != null) Console.instance.AddString(line);
        }
    }
}
