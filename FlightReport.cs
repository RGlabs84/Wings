using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace WingsoftheValkyrie
{
    /// <summary>
    /// Gets flight numbers off the clients that measure them and onto the server that can
    /// publish them.
    ///
    /// Everything the logbook records -- glide time, altitude, speed, wingbeats -- is client-side
    /// physics. On a dedicated server the flying player's own client owns the simulation, so the
    /// server never sees any of it, and a mod that only wrote a file locally would write it on
    /// the wrong machine: BarrkBOT can read the server box and nothing else. So clients report
    /// their totals up over a routed RPC, the server keeps the latest per player, and the server
    /// writes the export.
    ///
    /// The reported numbers are whatever the client says they are. That is fine for what this
    /// is -- a logbook of who flew where, on a server whose players are known to each other --
    /// but it is not evidence and nothing should be built on it that needs to be.
    /// </summary>
    public static class FlightReport
    {
        private const string RpcName = "WOTV_FlightSaga";

        /// <summary>Folder and filename are the BarrkBOT export convention: a `barrkbot_*.json`
        /// no more than two levels under BepInEx/config. Renaming either means the sweep stops
        /// finding it, silently -- there is no error anywhere when a name simply matches nothing.</summary>
        private const string ExportFolder = "WingsOfTheValkyrie";

        /// <summary>The file the console command names and the one a person looks for first.
        /// The other three sit beside it; see Subjects().</summary>
        private const string ExportFileName = "barrkbot_flight.json";

        /// <summary>The server's own copy of who has flown, so a restart does not blank the
        /// export until everybody happens to log in and fly again. Deliberately not a
        /// `barrkbot_*.json`, so the sweep never sees two files claiming the same facts.</summary>
        private const string RegistryFileName = "flight_registry.dat";

        private sealed class Row
        {
            public long PlayerId;
            public string Name;
            public FlightSaga Saga;
        }

        // Server side: latest saga per character id.
        private static readonly Dictionary<long, Row> Rows = new Dictionary<long, Row>();

        // The ZRoutedRpc object is rebuilt for every network session. Holding the instance we
        // registered against (rather than a bool) means joining a second world re-registers by
        // itself, with no patch on session teardown to forget.
        private static ZRoutedRpc _registeredOn;
        private static bool _registryLoaded;
        private static float _writeTimer;
        private static float _lastReportTime = float.NegativeInfinity;

        // ---- registration ------------------------------------------------------------------------

        /// <summary>Registers the RPC for the current network session, and clears anything the
        /// previous one left behind. Cheap enough to call every frame: it does nothing at all
        /// once the session it registered for is the session still running.</summary>
        public static void Register()
        {
            ZRoutedRpc rpc = ZRoutedRpc.instance;
            if (rpc == null || ReferenceEquals(_registeredOn, rpc)) return;

            try
            {
                rpc.Register<long, string, string>(RpcName, OnSagaReceived);
                _registeredOn = rpc;

                // A new session: whatever is in memory belongs to the last world. The server's
                // rows come back off the registry file, not out of the previous session.
                Rows.Clear();
                _registryLoaded = false;
                _writeTimer = 0f;
                _lastReportTime = float.NegativeInfinity;
            }
            catch (Exception ex)
            {
                Jotunn.Logger.LogWarning($"[Wings of the Valkyrie] Could not register the flight report RPC; flight stats will not reach the server. Reason: {ex.Message}");
            }
        }

        // ---- client side -------------------------------------------------------------------------

        /// <summary>Offers the local character's saga to the server, no more often than the
        /// configured interval unless <paramref name="force"/> says otherwise.</summary>
        public static void ReportToServer(Player player, FlightSaga saga, bool force)
        {
            if (player == null || saga == null) return;
            if (!ModConfig.PublishFlightStats.Value) return;

            // Nothing to say yet. Rows are created on first activity, never pre-seeded, so an
            // empty export means "no flight recorded" rather than "nobody flies".
            if (!saga.HasFlown) return;

            // Before the throttle, and before anything is accepted. Register() is what notices a
            // new session and wipes the last one's state, so running it afterwards would either
            // reset the throttle it just consumed or -- on a solo world, where the line below
            // stores the row directly -- discard the row it had only just been given.
            Register();

            float interval = Mathf.Max(1f, ModConfig.FlightStatsReportInterval.Value);
            if (!force && Time.realtimeSinceStartup - _lastReportTime < interval) return;
            _lastReportTime = Time.realtimeSinceStartup;

            try
            {
                long playerId = player.GetPlayerID();
                string name = player.GetPlayerName();
                string payload = saga.Serialize();

                // Solo play and a listen server are both "the server is right here": routing an
                // RPC to ourselves is a needless round trip and, on a world with no peers yet,
                // may have nowhere to route to at all.
                if (ZNet.instance == null || ZNet.instance.IsServer())
                {
                    Accept(playerId, name, payload);
                    return;
                }

                ZRoutedRpc rpc = ZRoutedRpc.instance;
                if (rpc == null) return;

                // ZRoutedRpc.GetServerPeerID is private; the peer it would return is not, and
                // its uid is the same number. Reaching through the public door avoids a
                // reflection dependency on a method name that could quietly move.
                ZNetPeer server = ZNet.instance.GetServerPeer();
                if (server == null) return;

                rpc.InvokeRoutedRPC(server.m_uid, RpcName, playerId, name, payload);
            }
            catch (Exception ex)
            {
                Jotunn.Logger.LogWarning($"[Wings of the Valkyrie] Could not send your flight stats to the server. Reason: {ex.Message}");
            }
        }

        // ---- server side -------------------------------------------------------------------------

        private static void OnSagaReceived(long sender, long playerId, string name, string payload)
        {
            // A client that receives this has nothing to do with it; only the server publishes.
            if (ZNet.instance != null && !ZNet.instance.IsServer()) return;

            Accept(playerId, name, payload);
        }

        private static void Accept(long playerId, string name, string payload)
        {
            try
            {
                if (playerId == 0L || string.IsNullOrEmpty(payload)) return;

                FlightSaga saga = FlightSaga.Deserialize(payload);
                if (!saga.HasFlown) return;

                EnsureRegistryLoaded();

                Rows[playerId] = new Row
                {
                    PlayerId = playerId,
                    Name = string.IsNullOrEmpty(name) ? "Unknown" : name,
                    Saga = saga,
                };
            }
            catch (Exception ex)
            {
                Jotunn.Logger.LogWarning($"[Wings of the Valkyrie] Could not read a flight report. Reason: {ex.Message}");
            }
        }

        /// <summary>Drives the export cadence. Called once per frame by the plugin; does nothing
        /// anywhere except a machine that is actually acting as the server.</summary>
        public static void Tick(float deltaTime)
        {
            if (!ModConfig.PublishFlightStats.Value) return;
            if (ZNet.instance == null || !ZNet.instance.IsServer()) return;

            Register();
            EnsureRegistryLoaded();

            _writeTimer += deltaTime;
            if (_writeTimer < Mathf.Max(5f, ModConfig.FlightStatsWriteInterval.Value)) return;
            _writeTimer = 0f;

            WriteExport();
        }

        // ---- files -------------------------------------------------------------------------------

        public static string ExportDirectory()
        {
            string configured = ModConfig.FlightStatsExportFolder.Value;
            if (!string.IsNullOrEmpty(configured)) return configured.Trim();

            return Path.Combine(BepInEx.Paths.ConfigPath, ExportFolder);
        }

        public static string ExportPath() => Path.Combine(ExportDirectory(), ExportFileName);

        private static string RegistryPath() => Path.Combine(ExportDirectory(), RegistryFileName);

        private static void EnsureRegistryLoaded()
        {
            if (_registryLoaded) return;
            _registryLoaded = true;

            try
            {
                string path = RegistryPath();
                if (!File.Exists(path)) return;

                // "<id>|<name>|<saga>" per line. Names can contain anything, so the saga is
                // found from the LAST separator rather than the second.
                foreach (string line in File.ReadAllLines(path))
                {
                    if (string.IsNullOrEmpty(line)) continue;

                    int first = line.IndexOf('|');
                    int last = line.LastIndexOf('|');
                    if (first <= 0 || last <= first) continue;

                    if (!long.TryParse(line.Substring(0, first), NumberStyles.Integer, CultureInfo.InvariantCulture, out long id)) continue;

                    Rows[id] = new Row
                    {
                        PlayerId = id,
                        Name = line.Substring(first + 1, last - first - 1),
                        Saga = FlightSaga.Deserialize(line.Substring(last + 1)),
                    };
                }
            }
            catch (Exception ex)
            {
                Jotunn.Logger.LogWarning($"[Wings of the Valkyrie] Could not read the flight registry; it will rebuild as players fly. Reason: {ex.Message}");
            }
        }

        private static void SaveRegistry()
        {
            var sb = new StringBuilder(Rows.Count * 320);
            foreach (Row row in Rows.Values)
            {
                sb.Append(row.PlayerId.ToString(CultureInfo.InvariantCulture)).Append('|')
                  .Append((row.Name ?? "").Replace("\n", " ").Replace("\r", " ")).Append('|')
                  .Append(row.Saga.Serialize()).Append('\n');
            }

            WriteAtomic(RegistryPath(), sb.ToString());
        }

        /// <summary>Writes the export where BarrkBOT sweeps for it. Public so the console
        /// command can force one; returns the path written, or null on failure.</summary>
        public static string WriteExport()
        {
            string directory = ExportDirectory();

            try
            {
                EnsureRegistryLoaded();

                var allRows = new List<Row>();
                foreach (Row row in Rows.Values)
                {
                    if (row?.Saga != null) allRows.Add(row);
                }

                var written = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (Subject subject in Subjects())
                {
                    List<List<Row>> parts = Partition(subject, allRows);

                    for (int i = 0; i < parts.Count; i++)
                    {
                        string name = PartFileName(subject.FileName, i + 1);
                        written.Add(name);
                        WriteAtomic(Path.Combine(directory, name), BuildJson(subject, parts[i], allRows, i + 1, parts.Count));
                    }
                }

                SweepStaleExports(directory, written);
                SaveRegistry();
                return ExportPath();
            }
            catch (Exception ex)
            {
                Jotunn.Logger.LogWarning($"[Wings of the Valkyrie] Could not write the flight export to '{directory}'. Reason: {ex.Message}");
                return null;
            }
        }

        /// <summary>Part one keeps the subject's plain name; continuations take `_2`, `_3` and so
        /// on. The reader groups a family by the digits at the end, so `_records` is read as part
        /// of the name and `_records_2` as its second part.</summary>
        private static string PartFileName(string fileName, int part)
        {
            if (part <= 1) return fileName;

            return fileName.Substring(0, fileName.Length - ".json".Length)
                 + "_" + part.ToString(CultureInfo.InvariantCulture) + ".json";
        }

        /// <summary>
        /// Removes any `barrkbot_*.json` in our own export folder that this version no longer
        /// writes.
        ///
        /// The sweep reads whatever it finds, and a file nobody updates any more does not look
        /// stale to it -- it looks like current data with an old timestamp. An export left behind
        /// by a previous version of this mod would go on being read and ranked for as long as it
        /// sat there, which is a wrong answer served confidently rather than a missing one.
        ///
        /// Scoped to files this mod is the author of: the folder is ours, but a config folder is
        /// somewhere a person may reasonably have put something, so anything not named like one
        /// of our exports is left exactly where it is.
        /// </summary>
        private static void SweepStaleExports(string directory, HashSet<string> written)
        {
            try
            {
                if (!Directory.Exists(directory)) return;

                foreach (string found in Directory.GetFiles(directory, "barrkbot_flight*.json"))
                {
                    string name = Path.GetFileName(found);
                    if (written.Contains(name)) continue;

                    File.Delete(found);
                    Jotunn.Logger.LogInfo($"[Wings of the Valkyrie] Removed a flight export this version no longer writes, so the reader stops ranking it: {name}");
                }
            }
            catch (Exception ex)
            {
                // A file we could not delete is a stale ranking, not a broken export: say so and
                // carry on writing the ones that did work.
                Jotunn.Logger.LogWarning($"[Wings of the Valkyrie] Could not tidy old flight exports; an out-of-date file may still be read. Reason: {ex.Message}");
            }
        }

        // Temp-then-replace, because the reader sweeps on its own schedule and half a file is
        // worse than a slightly old one: a truncated read is a parse error at best and a wrong
        // number at worst.
        private static void WriteAtomic(string path, string contents)
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory)) Directory.CreateDirectory(directory);

            string temp = path + ".tmp";
            File.WriteAllText(temp, contents, new UTF8Encoding(false));

            if (File.Exists(path)) File.Replace(temp, path, null);
            else File.Move(temp, path);
        }

        // ---- the export ---------------------------------------------------------------------------

        /// <summary>The reader's budget for one collection's rows is 2,600 characters of compact
        /// JSON, measured per row and summed. We aim under it: the reader injects a `key` into
        /// every row before it measures, and that injection is not ours to size.</summary>
        private const int RowBudgetChars = 2400;

        /// <summary>One numeric column. Ranking and writing read the same list, so a field can
        /// never be published without being rankable or ranked without being published.</summary>
        private sealed class Field
        {
            public string Name;
            public Func<FlightSaga, double> Value;
            public bool Integral;
        }

        /// <summary>
        /// One published file. The export is split by subject rather than by player, because the
        /// reader's per-row budget is spent on row *width*: every field a row carries is paid for
        /// by every player in the file. One file of 21 fields showed 2 pilots of 5; four narrow
        /// files show all of them, and each one still ranks over the complete roster.
        /// </summary>
        private sealed class Subject
        {
            public string FileName;
            public string Holds;
            public Field[] Fields;
            public Action<StringBuilder, FlightSaga> AppendExtra;
        }

        private static Subject[] Subjects()
        {
            return new[]
            {
                new Subject
                {
                    FileName = "barrkbot_flight.json",
                    Holds = "Career totals per pilot: Valkyrie Flight skill level, total time aloft, total distance, number of flights, and time flown at night.",
                    Fields = new[]
                    {
                        new Field { Name = "valkyrie_flight_level",  Value = s => Math.Round(s.SkillLevel, 1) },
                        new Field { Name = "flight_time_seconds",    Value = s => Math.Round(s.TimeFlownSeconds, 1) },
                        new Field { Name = "distance_flown_meters",  Value = s => Math.Round(s.DistanceMeters, 1) },
                        new Field { Name = "flights",                Value = s => s.Flights, Integral = true },
                        new Field { Name = "night_flight_seconds",   Value = s => Math.Round(s.NightSeconds, 1) },
                    },
                },
                new Subject
                {
                    FileName = "barrkbot_flight_records.json",
                    Holds = "Personal bests per pilot: longest single flight by time and by distance, highest altitude, top speed and steepest dive.",
                    Fields = new[]
                    {
                        new Field { Name = "longest_flight_seconds",          Value = s => Math.Round(s.BestFlightSeconds, 1) },
                        new Field { Name = "longest_flight_meters",           Value = s => Math.Round(s.BestFlightMeters, 1) },
                        new Field { Name = "max_altitude_meters",             Value = s => Math.Round(s.BestAltitude, 1) },
                        new Field { Name = "top_speed_meters_per_second",     Value = s => Math.Round(s.BestSpeed, 1) },
                        new Field { Name = "steepest_dive_meters_per_second", Value = s => Math.Round(s.BestDiveSpeed, 1) },
                    },
                },
                new Subject
                {
                    FileName = "barrkbot_flight_counters.json",
                    Holds = "Event tallies per pilot: wingbeats, landings on ground, water and ship, and flights refused for want of stamina or skill.",
                    Fields = new[]
                    {
                        new Field { Name = "wingbeats",       Value = s => s.Flaps,          Integral = true },
                        new Field { Name = "landings_ground", Value = s => s.LandingsGround, Integral = true },
                        new Field { Name = "landings_water",  Value = s => s.LandingsWater,  Integral = true },
                        new Field { Name = "landings_ship",   Value = s => s.LandingsShip,   Integral = true },
                        new Field { Name = "stamina_denials", Value = s => s.StaminaDenials, Integral = true },
                        new Field { Name = "skill_denials",   Value = s => s.SkillDenials,   Integral = true },
                    },
                },
                // tier_time_seconds used to be one nested object. The reader drops nested objects
                // with more than two keys from any multi-row listing, in every file, so no amount
                // of moving it made it visible -- flattening it did, and turned four buried
                // numbers into four rankable ones. biomes_flown_over stays the array it is, but an
                // array can never be ranked, so the count travels beside it for the question
                // people actually ask of it.
                new Subject
                {
                    FileName = "barrkbot_flight_tiers.json",
                    Holds = "Time flown on each grade of wings per pilot -- crude, troll, lox and dragon -- and which biomes they have crossed.",
                    Fields = new[]
                    {
                        new Field { Name = "tier_time_crude_seconds",  Value = s => Math.Round(s.CrudeSeconds, 1) },
                        new Field { Name = "tier_time_troll_seconds",  Value = s => Math.Round(s.TrollSeconds, 1) },
                        new Field { Name = "tier_time_lox_seconds",    Value = s => Math.Round(s.LoxSeconds, 1) },
                        new Field { Name = "tier_time_dragon_seconds", Value = s => Math.Round(s.DragonSeconds, 1) },
                        new Field { Name = "distinct_biomes_flown",    Value = s => s.BiomesFlownOver().Count, Integral = true },
                    },
                    AppendExtra = AppendBiomes,
                },
            };
        }

        private static void AppendBiomes(StringBuilder sb, FlightSaga saga)
        {
            sb.Append("      \"biomes_flown_over\": [");
            List<string> biomes = saga.BiomesFlownOver();
            for (int i = 0; i < biomes.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append('"').Append(Escape(biomes[i])).Append('"');
            }
            sb.Append("],\n");
        }

        // ---- rollover ------------------------------------------------------------------------------

        /// <summary>
        /// Divides a subject's pilots between as many files as their rows need.
        ///
        /// The cap is on row width, never on file bytes: the reader's budget is spent on the rows
        /// it renders, and the raw-to-rendered ratio is not a usable proxy for it. A row wider
        /// than the whole budget still gets a file of its own -- publishing a pilot in a file
        /// slightly over budget loses the tail of one row, whereas dropping them loses the pilot.
        /// </summary>
        private static List<List<Row>> Partition(Subject subject, List<Row> rows)
        {
            var parts = new List<List<Row>>();
            var current = new List<Row>();
            int running = 0;

            foreach (Row row in rows)
            {
                int width = CompactRowWidth(subject, row);

                if (current.Count > 0 && running + width > RowBudgetChars)
                {
                    parts.Add(current);
                    current = new List<Row>();
                    running = 0;
                }

                current.Add(row);
                running += width;
            }

            parts.Add(current);
            return parts;
        }

        /// <summary>What one row costs against the reader's budget: the compact length of the row
        /// object, which is what the reader measures after it re-serialises what we wrote. Every
        /// field we publish is a scalar or a flat array, so our rendered row and our written row
        /// are the same row -- nothing here is dropped on the way through.</summary>
        private static int CompactRowWidth(Subject subject, Row row)
        {
            var sb = new StringBuilder(320);
            AppendRowObject(sb, row, subject);
            return CompactLength(sb.ToString());
        }

        /// <summary>Length of the same JSON with the whitespace between tokens removed, which is
        /// what JSON.stringify of the parsed value comes to. Whitespace inside a string literal
        /// is content and is counted.</summary>
        private static int CompactLength(string json)
        {
            int count = 0;
            bool inString = false;
            bool escaped = false;

            foreach (char c in json)
            {
                if (inString)
                {
                    count++;
                    if (escaped) escaped = false;
                    else if (c == '\\') escaped = true;
                    else if (c == '"') inString = false;
                    continue;
                }

                if (c == '"') { inString = true; count++; continue; }
                if (c == ' ' || c == '\t' || c == '\n' || c == '\r') continue;
                count++;
            }

            return count;
        }

        /// <summary>
        /// The leaderboard a part carries so that a part can be answered from on its own.
        ///
        /// A file holding some of the pilots can still rank all of them, because the writer has
        /// every pilot in hand at the moment it decides to split. Without this block a part ranks
        /// its own rows and hands back a confident superlative computed from a fraction of the
        /// server; with it, either part answers "who has flown furthest" correctly.
        ///
        /// Only written when there is more than one part. On a single file the rows ARE the whole
        /// roster, and the deployed reader -- which predates parts entirely -- reads an unexpected
        /// `players_leaders` as a second collection of its own rather than as guidance.
        /// </summary>
        private sealed class Leader
        {
            public string Name;
            public double Value;
        }

        private static void AppendLeaders(StringBuilder sb, Subject subject, List<Row> allRows)
        {
            // Two characters may carry the same name. The rows themselves are keyed by player id
            // so they stay distinct, but a leaderboard prints the name alone -- and the reader
            // prints what it is given without disambiguating. Two identical names against
            // different numbers reads as one person contradicting themselves, so the id joins any
            // name that is not unique.
            var seen = new Dictionary<string, int>();
            foreach (Row row in allRows)
            {
                string name = row.Name ?? "Unknown";
                seen.TryGetValue(name, out int n);
                seen[name] = n + 1;
            }

            sb.Append("  \"players_leaders\": {\n");

            bool firstField = true;
            foreach (Field field in subject.Fields)
            {
                var ranked = new List<Leader>();
                foreach (Row row in allRows)
                {
                    if (row?.Saga == null) continue;

                    double value = field.Value(row.Saga);

                    // A pilot with nothing to show is left out rather than padded with a zero: a
                    // field where the best figure is zero is dropped by the reader entirely, which
                    // is better than a leaderboard headed by somebody who has never done the thing.
                    if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0d) continue;

                    string name = row.Name ?? "Unknown";
                    if (seen[name] > 1)
                        name = name + " (id " + row.PlayerId.ToString(CultureInfo.InvariantCulture) + ")";

                    ranked.Add(new Leader { Name = name, Value = value });
                }

                if (ranked.Count == 0) continue;

                // The reader takes our order as given and does not re-sort, so the order here is
                // the order that gets published as the ranking.
                ranked.Sort((a, b) => b.Value.CompareTo(a.Value));

                if (!firstField) sb.Append(",\n");
                firstField = false;

                Indent(sb, 2).Append('"').Append(field.Name).Append("\": [");
                int take = Math.Min(3, ranked.Count);
                for (int i = 0; i < take; i++)
                {
                    if (i > 0) sb.Append(", ");
                    sb.Append("{\"name\": \"").Append(Escape(ranked[i].Name)).Append("\", \"value\": ")
                      .Append(Number(ranked[i].Value, field.Integral)).Append('}');
                }
                sb.Append(']');
            }

            sb.Append(firstField ? "},\n" : "\n  },\n");
        }

        // ---- building one file ---------------------------------------------------------------------

        private static string BuildJson(Subject subject, List<Row> partRows, List<Row> allRows, int part, int partOf)
        {
            var sb = new StringBuilder(1024 + partRows.Count * 320);
            sb.Append("{\n");

            Str(sb, 1, "generated_at", FlightSaga.IsoNow()).Append(",\n");
            Str(sb, 1, "source", WingsoftheValkyriePlugin.PluginName + " " + WingsoftheValkyriePlugin.PluginVersion).Append(",\n");
            Raw(sb, 1, "schema_version", FlightSaga.ExportSchemaVersion.ToString(CultureInfo.InvariantCulture)).Append(",\n");
            Str(sb, 1, "holds", subject.Holds).Append(",\n");

            // Declared only once a rollover has actually happened. A reader that has never heard
            // of parts treats these as ordinary data, so on one file they are noise at best -- and
            // the leaderboard beside them is read as a collection of its own.
            if (partOf > 1)
            {
                Raw(sb, 1, "part", part.ToString(CultureInfo.InvariantCulture)).Append(",\n");
                Raw(sb, 1, "part_of", partOf.ToString(CultureInfo.InvariantCulture)).Append(",\n");
                AppendLeaders(sb, subject, allRows);
            }

            sb.Append("  \"intervals\": {\n");
            Raw(sb, 2, "write_seconds", ((int)ModConfig.FlightStatsWriteInterval.Value).ToString(CultureInfo.InvariantCulture)).Append(",\n");
            Raw(sb, 2, "client_report_seconds", ((int)ModConfig.FlightStatsReportInterval.Value).ToString(CultureInfo.InvariantCulture)).Append("\n");
            sb.Append("  },\n");

            // Keys ending in _notes are lifted out of the data by the reader and treated as
            // guidance about it, so a caveat can be published without becoming a quotable fact.
            // Every file carries the whole set: each is read on its own, so a caveat left out of
            // one is a caveat that file's reader never sees.
            AppendNotes(sb, partOf);

            sb.Append("  \"players\": {");

            bool first = true;
            foreach (Row row in partRows)
            {
                if (row?.Saga == null) continue;

                sb.Append(first ? "\n" : ",\n");
                first = false;

                sb.Append("    \"").Append(Escape(row.PlayerId.ToString(CultureInfo.InvariantCulture))).Append("\": ");
                AppendRowObject(sb, row, subject);
            }

            sb.Append(first ? "}\n" : "\n  }\n");
            sb.Append("}\n");
            return sb.ToString();
        }

        /// <summary>One pilot's row. `name` and both timestamps repeat in every subject so that a
        /// file can be read on its own without a second file to say who a row belongs to.</summary>
        private static void AppendRowObject(StringBuilder sb, Row row, Subject subject)
        {
            FlightSaga saga = row.Saga;

            sb.Append("{\n");
            Str(sb, 3, "name", row.Name).Append(",\n");

            foreach (Field field in subject.Fields)
                Num(sb, 3, field.Name, field.Value(saga)).Append(",\n");

            if (subject.AppendExtra != null) subject.AppendExtra(sb, saga);

            Str(sb, 3, "first_flight_at", FlightSaga.Iso(saga.FirstFlightUnix)).Append(",\n");
            Str(sb, 3, "last_flight_at", FlightSaga.Iso(saga.LastFlightUnix)).Append("\n");

            sb.Append("    }");
        }

        private static void AppendNotes(StringBuilder sb, int partOf)
        {
            Str(sb, 1, "flight_time_seconds_notes",
                "Time with wings deployed and gliding. This is not playtime and not session length - a player with hundreds of hours may have very little flight time. Never present it as time played.").Append(",\n");
            Str(sb, 1, "distance_flown_meters_notes",
                "Horizontal distance covered under this mod's wings only. It shares no base with any sailing, riding or walking distance counter from another mod on this server: never sum, rank or compare them together.").Append(",\n");
            Str(sb, 1, "counters_notes",
                "flights, wingbeats, stamina_denials and skill_denials all count different events inside the same flight and must never be added together into one total. A single flight raises several of them at once.").Append(",\n");
            Str(sb, 1, "players_notes",
                "Rows are created the first time a character actually flies, never pre-seeded. An empty players map means no flight has been recorded yet - it never means nobody flies, and it is not a zero. A missing player is a player who has not flown since this server last saw them, not a player who has flown zero metres.").Append(",\n");
            Str(sb, 1, "max_altitude_meters_notes",
                "Height above the ground directly below, not above sea level, and capped in play by the flight ceiling of the wings worn and the pilot's Valkyrie Flight level.").Append(",\n");
            Str(sb, 1, "tier_time_notes",
                "tier_time_crude_seconds, tier_time_troll_seconds, tier_time_lox_seconds and tier_time_dragon_seconds divide the same flight time between the grades of wings worn, so together they total flight_time_seconds and must never be added to it.").Append(",\n");
            Str(sb, 1, "distinct_biomes_flown_notes",
                "How many of the nine biomes this pilot has crossed under wing, not how many they have visited on foot. Ranking it ranks exploration by air only.").Append(",\n");
            Str(sb, 1, "subject_split_notes",
                "This mod publishes four files that each hold every pilot, divided by subject rather than by player: barrkbot_flight.json (totals), _records (personal bests), _counters (event tallies), _tiers (wing grades and biomes). Each ranks over the complete roster, so a superlative from any one of them is correct and they never need combining to be trusted.").Append(",\n");

            if (partOf > 1)
                Str(sb, 1, "part_notes",
                    "This subject has outgrown one file and continues into numbered parts. The rows below are this part's only, but players_leaders above is ranked over every pilot on the server, so any superlative should be answered from it and never from these rows.").Append(",\n");
        }

        // ---- json primitives ----------------------------------------------------------------------

        private static StringBuilder Indent(StringBuilder sb, int depth) => sb.Append(' ', depth * 2);

        private static StringBuilder Str(StringBuilder sb, int depth, string key, string value)
        {
            return Indent(sb, depth).Append('"').Append(key).Append("\": \"").Append(Escape(value)).Append('"');
        }

        private static StringBuilder Raw(StringBuilder sb, int depth, string key, string value)
        {
            return Indent(sb, depth).Append('"').Append(key).Append("\": ").Append(value);
        }

        /// <summary>A number as the export writes it: no exponent, no trailing zeros, and the
        /// invariant decimal point regardless of the server's locale.</summary>
        private static string Number(double value, bool integral)
        {
            return integral
                ? ((long)Math.Round(value)).ToString(CultureInfo.InvariantCulture)
                : value.ToString("0.####", CultureInfo.InvariantCulture);
        }

        private static StringBuilder Num(StringBuilder sb, int depth, string key, double value)
        {
            return Indent(sb, depth).Append('"').Append(key).Append("\": ")
                     .Append(value.ToString("0.####", CultureInfo.InvariantCulture));
        }

        private static StringBuilder Num(StringBuilder sb, int depth, string key, long value)
        {
            return Indent(sb, depth).Append('"').Append(key).Append("\": ")
                     .Append(value.ToString(CultureInfo.InvariantCulture));
        }

        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";

            var sb = new StringBuilder(value.Length + 8);
            foreach (char c in value)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < ' ') sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }
    }
}
