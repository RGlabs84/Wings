using System.Collections.Generic;
using Jotunn.Entities;

namespace WingsoftheValkyrie
{
    /// <summary>
    /// The <c>wov</c> console command: reads out the flight saga in game and writes the export
    /// file on demand. Not a cheat and not networked -- it only ever reports the local
    /// character's own numbers.
    /// </summary>
    public class FlightLogCommand : ConsoleCommand
    {
        public override string Name => "wov";

        public override string Help =>
            "Wings of the Valkyrie. 'wov log' for your flight saga, 'wov oddities' for the strange corners of it, " +
            "'wov export' to write the logbook out as JSON, 'wov where' for that file's path.";

        public override bool IsCheat => false;

        public override List<string> CommandOptionList()
        {
            return new List<string> { "log", "oddities", "export", "where" };
        }

        public override void Run(string[] args)
        {
            Player player = Player.m_localPlayer;
            if (player == null)
            {
                Console.instance.Print("No Viking to speak of - load a character first.");
                return;
            }

            FlightLog.EnsureLoaded(player);

            string subcommand = args != null && args.Length > 0 ? args[0].ToLowerInvariant() : "log";

            switch (subcommand)
            {
                case "log":
                case "oddities":
                    foreach (string line in FlightLog.Report(player, includeOddities: subcommand == "oddities"))
                    {
                        Console.instance.Print(line);
                    }
                    break;

                case "export":
                {
                    // Flushing always reports upward; on a server or a solo world that lands in
                    // the registry immediately, so the file can be written in the same breath.
                    FlightLog.Flush(player, force: true);

                    if (!ModConfig.PublishFlightStats.Value)
                    {
                        Console.instance.Print("Flight statistics are switched off here - set PublishFlightStats to true in the config.");
                        break;
                    }

                    if (ZNet.instance != null && !ZNet.instance.IsServer())
                    {
                        Console.instance.Print("Sent your flight saga to the server. The server owns the statistics file - ask its admin, or run this on the server itself.");
                        break;
                    }

                    string path = FlightReport.WriteExport();
                    Console.instance.Print(path != null
                        ? "Flight statistics written to " + path
                        : "Could not write the flight statistics - see the log for why.");
                    break;
                }

                case "where":
                    Console.instance.Print("Flight statistics file: " + FlightReport.ExportPath());
                    Console.instance.Print("Your own saga lives on your character, not in that file.");
                    break;

                default:
                    Console.instance.Print("Unknown option '" + subcommand + "'. Try: log, oddities, export, where.");
                    break;
            }
        }
    }
}
