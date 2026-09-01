using System.Text.Json;
using WingsoftheValkyrie;

int failures = 0;

// A new network session, exactly as the game gives us one: a fresh router object. Registering
// against it is what makes FlightReport forget the previous session, so this exercises the real
// reset path rather than a hook that only exists for tests.
void NewSession()
{
    ZRoutedRpc.instance = new ZRoutedRpc();
    FlightReport.Register();
}

void Check(string label, bool ok, string detail = "")
{
    Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + label + (detail.Length > 0 ? "   [" + detail + "]" : ""));
    if (!ok) failures++;
}

// Also where the two sample exports land, for handing to the BarrkBOT reader.
string OUT = Path.Combine(Path.GetTempPath(), "wotv-flightlog-tests");
Directory.CreateDirectory(OUT);
// A leaderboard prints a name and nothing else, and the reader prints back what it is given.
// Two characters may carry the same name, so an undisambiguated board would show one person
// apparently disagreeing with themselves about their own distance.
try
{
    string TWINS = Path.Combine(Path.GetTempPath(), "wotv-flightlog-tests-twins");
    Directory.CreateDirectory(TWINS);
    foreach (var stale in Directory.GetFiles(TWINS)) File.Delete(stale);
    ModConfig.FlightStatsExportFolder.Value = TWINS;
    NewSession();

    // Enough same-named pilots to force a rollover, so the leaderboard is written at all.
    for (int i = 1; i <= 40; i++)
    {
        var twin = new Player { Name = i % 2 == 0 ? "Bronzebeard" : "Bronzebeard", PlayerId = 7000L + i };
        FlyingSkill.LevelValue = 10f + i;
        FlightLog.LoadFrom(twin);
        twin.transform.position = new UnityEngine.Vector3(0, 60, 0);
        FlightLog.Tick(twin, true, WingsItem.DragonName, 0.5f);
        for (int step = 1; step <= i; step++)
        {
            twin.transform.position = new UnityEngine.Vector3(step * 10f, 60f, 0);
            twin.Biome = Heightmap.Biome.Meadows;
            FlightLog.Tick(twin, true, WingsItem.DragonName, 0.5f);
        }
        FlightLog.Flush(twin, force: true);
    }
    FlightReport.WriteExport();

    var board = JsonDocument.Parse(File.ReadAllText(Path.Combine(TWINS, "barrkbot_flight.json")))
                    .RootElement.GetProperty("players_leaders").GetProperty("distance_flown_meters");
    var names = board.EnumerateArray().Select(e => e.GetProperty("name").GetString()).ToList();

    Check("pilots sharing a name are told apart on the leaderboard",
          names.Distinct().Count() == names.Count, string.Join(", ", names));
    Check("and the id is what tells them apart", names.All(n => n.Contains("(id ")));

    Directory.Delete(TWINS, true);
}
catch (Exception ex) { Check("duplicate names are disambiguated", false, ex.Message); }

ModConfig.FlightStatsExportFolder.Value = OUT;
foreach (var stale in Directory.GetFiles(OUT)) File.Delete(stale);

// ============================================================ 1. a flight, recorded
Console.WriteLine("--- tracking a flight ---");
var player = new Player { Name = "Ross", PlayerId = 1001L };
FlightLog.LoadFrom(player);
Check("fresh character starts with an empty logbook", !FlightLog.HasFlown);

FlyingSkill.LevelValue = 37.5f;
ZoneSystem.instance.GroundHeight = 10f;
void MoveTo(float x, float y, float z) => player.transform.position = new UnityEngine.Vector3(x, y, z);

MoveTo(0, 60, 0);
FlightLog.Tick(player, true, WingsItem.DragonName, 0.5f);
for (int i = 1; i <= 10; i++)
{
    MoveTo(i * 5f, 60f + i, 0);
    player.Velocity = new UnityEngine.Vector3(0, -8f, 0);
    EnvMan.Night = i > 5;
    player.Biome = i > 7 ? Heightmap.Biome.Mistlands : Heightmap.Biome.Meadows;
    FlightLog.Tick(player, true, WingsItem.DragonName, 0.5f);
}
FlightLog.NoteFlap(); FlightLog.NoteFlap(); FlightLog.NoteFlap();
FlightLog.NoteStaminaDenied();
FlightLog.NoteSkillDenied(); FlightLog.NoteSkillDenied();
player.Swimming = true;
FlightLog.Tick(player, false, WingsItem.DragonName, 0.5f);

var report = FlightLog.Report(player, includeOddities: true);
Check("time flown is ~5.5s", report.Exists(l => l.Contains("Time flown") && l.Contains("5s")));
Check("distance flown is 50m", report.Exists(l => l.Contains("Distance flown") && l.Contains("50")));
Check("highest above ground is 60m", report.Exists(l => l.Contains("Highest above ground") && l.Contains("60.0")));
Check("water landing counted", report.Exists(l => l.Contains("Landed in the drink") && l.TrimEnd().EndsWith("1")));
Check("two biomes crossed", report.Exists(l => l.Contains("Biomes crossed") && l.Contains("2/9")));

// ============================================================ 2. persistence round-trip
Console.WriteLine();
Console.WriteLine("--- persistence on the character ---");
FlightLog.Flush(player, force: true);
string stored = player.m_customData["wubarrk.wotv.flightlog"];
Check("saga stored under the custom-data key", !string.IsNullOrEmpty(stored));

var reborn = new Player { Name = "Ross", PlayerId = 1001L, m_customData = new Dictionary<string, string>(player.m_customData) };
FlightLog.LoadFrom(reborn);
Check("round-trip preserves every line", string.Join("\n", report) == string.Join("\n", FlightLog.Report(reborn, true)));

var future = new Player { Name = "Ross", PlayerId = 1001L, m_customData = new Dictionary<string, string> {
    ["wubarrk.wotv.flightlog"] = stored + ";somethingNew=42;anotherThing=1.5" } };
FlightLog.LoadFrom(future);
Check("unknown keys from a newer build are skipped", string.Join("\n", FlightLog.Report(future, true)) == string.Join("\n", FlightLog.Report(reborn, true)));

var corrupt = new Player { Name = "Broken", m_customData = new Dictionary<string, string> { ["wubarrk.wotv.flightlog"] = "=;;;junk=notanumber;time=" } };
FlightLog.LoadFrom(corrupt);
Check("a corrupt saga loads as empty rather than throwing", !FlightLog.HasFlown);

FlightLog.LoadFrom(reborn);
var stranger = new Player { Name = "SomeoneElse" };
FlightLog.Flush(stranger, force: true);
Check("flush refuses to write one character's saga onto another", !stranger.m_customData.ContainsKey("wubarrk.wotv.flightlog"));

// ============================================================ 3. teleport guard
Console.WriteLine();
Console.WriteLine("--- guards ---");
var tp = new Player { Name = "Porter" };
FlightLog.LoadFrom(tp);
tp.transform.position = new UnityEngine.Vector3(0, 50, 0);
FlightLog.Tick(tp, true, WingsItem.CrudeName, 0.5f);
tp.transform.position = new UnityEngine.Vector3(9000, 50, 0);
FlightLog.Tick(tp, true, WingsItem.CrudeName, 0.5f);
var tpReport = FlightLog.Report(tp, false);
Check("a portal jump is not logged as distance flown", tpReport.Exists(l => l.Contains("Distance flown") && l.Contains(": 0 m")));
Check("a portal jump is not logged as a speed record", tpReport.Exists(l => l.Contains("Fastest on the wing") && l.Contains(": 0.0")));

ModConfig.EnableFlightLog.Value = false;
var off = new Player { Name = "Quiet" };
FlightLog.LoadFrom(off);
FlightLog.Tick(off, true, WingsItem.CrudeName, 1f);
FlightLog.Flush(off, force: true);
Check("with the logbook off nothing is written to the character", !off.m_customData.ContainsKey("wubarrk.wotv.flightlog"));
ModConfig.EnableFlightLog.Value = true;

// The stub carries its own copy of the plugin version, because pulling the real plugin class in
// would drag BepInEx and Jotunn into a harness that deliberately runs without them. A copy drifts
// silently -- and the version it stamps is published in `source` on every export, which is how the
// bot reports which build wrote a file -- so the copy is checked against the real declaration
// rather than trusted.
try
{
    string dir = Directory.GetCurrentDirectory();
    string found = null;
    for (int up = 0; up < 6 && dir != null; up++)
    {
        string candidate = Path.Combine(dir, "WingsoftheValkyriePlugin.cs");
        if (File.Exists(candidate)) { found = candidate; break; }
        dir = Path.GetDirectoryName(dir);
    }

    var declared = System.Text.RegularExpressions.Regex.Match(
        found == null ? "" : File.ReadAllText(found), "PluginVersion\\s*=\\s*\"([^\"]+)\"");

    Check("the stub's plugin version still matches the real one",
          declared.Success && declared.Groups[1].Value == WingsoftheValkyriePlugin.PluginVersion,
          found == null ? "WingsoftheValkyriePlugin.cs not found"
                        : declared.Groups[1].Value + " vs stub " + WingsoftheValkyriePlugin.PluginVersion);
}
catch (Exception ex) { Check("the stub's plugin version still matches the real one", false, ex.Message); }

// ============================================================ 4. the empty server export
Console.WriteLine();
Console.WriteLine("--- SAMPLE 1: a server where nothing has been recorded ---");
NewSession();
foreach (var stale in Directory.GetFiles(OUT)) File.Delete(stale);
string emptyPath = FlightReport.WriteExport();
string emptyJson = File.ReadAllText(emptyPath);
File.WriteAllText(Path.Combine(OUT, "sample-empty.json"), emptyJson);
try
{
    var doc = JsonDocument.Parse(emptyJson);
    Check("empty export is valid JSON", true);
    Check("empty export has an EMPTY players map, not a missing one",
          doc.RootElement.TryGetProperty("players", out var pl) && pl.EnumerateObject().Count() == 0);
    Check("empty export still carries generated_at / source / schema_version",
          doc.RootElement.TryGetProperty("generated_at", out _) &&
          doc.RootElement.GetProperty("source").GetString() == "Wings of the Valkyrie 2.1.1" &&
          doc.RootElement.GetProperty("schema_version").GetInt32() == 4);
    Check("empty export declares its write interval",
          doc.RootElement.GetProperty("intervals").GetProperty("write_seconds").GetInt32() == 60);
    Check("empty export carries the players_notes guard",
          doc.RootElement.GetProperty("players_notes").GetString().Contains("never means nobody"));
}
catch (Exception ex) { Check("empty export is valid JSON", false, ex.Message); }

// ============================================================ 5. client -> server over the RPC
Console.WriteLine();
Console.WriteLine("--- client reporting up to the server ---");
NewSession();
ZNet.instance.Server = false;                 // we are a CLIENT now
ZRoutedRpc.instance.Invocations = 0;
UnityEngine.Time.realtimeSinceStartup = 1000f;

FlightLog.LoadFrom(reborn);
FlightLog.Flush(reborn, force: true);
Check("a client sends its saga over the RPC", ZRoutedRpc.instance.Invocations == 1);

UnityEngine.Time.realtimeSinceStartup = 1005f;   // 5s later, inside the 60s throttle
FlightLog.Flush(reborn, force: false);
Check("the report throttle holds back a second send", ZRoutedRpc.instance.Invocations == 1);

UnityEngine.Time.realtimeSinceStartup = 1100f;   // 100s later, past it
FlightLog.Flush(reborn, force: false);
Check("and lets one through once the interval has passed", ZRoutedRpc.instance.Invocations == 2);

ModConfig.PublishFlightStats.Value = false;
UnityEngine.Time.realtimeSinceStartup = 2000f;
FlightLog.Flush(reborn, force: true);
Check("a player who opted out sends nothing", ZRoutedRpc.instance.Invocations == 2);
ModConfig.PublishFlightStats.Value = true;

// A character that has never flown must not create a row.
var neverFlew = new Player { Name = "Grounded", PlayerId = 3003L };
FlightLog.LoadFrom(neverFlew);
UnityEngine.Time.realtimeSinceStartup = 3000f;
FlightLog.Flush(neverFlew, force: true);
Check("a character who has never flown is never pre-seeded into the export", ZRoutedRpc.instance.Invocations == 2);

ZNet.instance.Server = true;

// ============================================================ 6. the populated export
Console.WriteLine();
Console.WriteLine("--- SAMPLE 2: a populated server ---");

// A second, much bigger flier, built by driving the real tracker.
var veteran = new Player { Name = "Bronzebeard", PlayerId = 2002L, Biome = Heightmap.Biome.Plains };
FlightLog.LoadFrom(veteran);
FlyingSkill.LevelValue = 91.4f;
ZoneSystem.instance.GroundHeight = 5f;
var rng = new Random(7);
for (int flight = 0; flight < 40; flight++)
{
    float x = 0f, y = 200f + flight * 3f;
    veteran.transform.position = new UnityEngine.Vector3(x, y, 0);
    veteran.Biome = (flight % 4) switch
    {
        0 => Heightmap.Biome.Meadows, 1 => Heightmap.Biome.Mountain,
        2 => Heightmap.Biome.Plains, _ => Heightmap.Biome.Ocean,
    };
    EnvMan.Night = flight % 3 == 0;
    FlightLog.Tick(veteran, true, WingsItem.DragonName, 0.5f);
    for (int step = 0; step < 60; step++)
    {
        x += 12f + (float)rng.NextDouble() * 4f;
        y += (float)(rng.NextDouble() - 0.4) * 3f;
        veteran.transform.position = new UnityEngine.Vector3(x, y, 0);
        veteran.Velocity = new UnityEngine.Vector3(0, -(4f + (float)rng.NextDouble() * 14f), 0);
        FlightLog.Tick(veteran, true, WingsItem.DragonName, 0.5f);
        if (step % 5 == 0) FlightLog.NoteFlap();
    }
    if (flight % 7 == 0) FlightLog.NoteStaminaDenied();
    if (flight % 11 == 0) FlightLog.NoteSkillDenied();
    veteran.Swimming = flight % 9 == 0;
    veteran.StandingOn = flight % 13 == 0 ? new Ship() : null;
    FlightLog.Tick(veteran, false, WingsItem.DragonName, 0.5f);
    veteran.Swimming = false; veteran.StandingOn = null;
}
FlightLog.Flush(veteran, force: true);

// And the first flier, on lower tiers.
FlyingSkill.LevelValue = 37.5f;
FlightLog.LoadFrom(reborn);
FlightLog.Flush(reborn, force: true);

string fullPath = FlightReport.WriteExport();
string fullJson = File.ReadAllText(fullPath);
File.WriteAllText(Path.Combine(OUT, "sample-populated.json"), fullJson);

// The export is four files split by subject, each holding every pilot. Read them by name:
// what a question can be answered from now depends on which file it lands in.
JsonElement Subject(string file) =>
    JsonDocument.Parse(File.ReadAllText(Path.Combine(OUT, file))).RootElement;
JsonElement RowFor(JsonElement root, string name) =>
    root.GetProperty("players").EnumerateObject().First(p => p.Value.GetProperty("name").GetString() == name).Value;

try
{
    var doc = JsonDocument.Parse(fullJson);
    var players = doc.RootElement.GetProperty("players");
    Check("populated export is valid JSON", true);
    Check("both fliers are present", players.EnumerateObject().Count() == 2, players.EnumerateObject().Count().ToString());

    var rows = players.EnumerateObject().ToDictionary(p => p.Value.GetProperty("name").GetString(), p => p.Value);
    Check("rows are keyed by player id with the name inside",
          players.EnumerateObject().All(p => long.TryParse(p.Name, out _)) && rows.ContainsKey("Bronzebeard"));

    var vet = rows["Bronzebeard"];
    Check("veteran has real flight time", vet.GetProperty("flight_time_seconds").GetDouble() > 1000);
    Check("veteran has real distance", vet.GetProperty("distance_flown_meters").GetDouble() > 20000);
    Check("veteran's skill level came across", Math.Abs(vet.GetProperty("valkyrie_flight_level").GetDouble() - 91.4) < 0.05);
    Check("timestamps are ISO-8601 with a Z",
          vet.GetProperty("last_flight_at").GetString().EndsWith("Z") &&
          doc.RootElement.GetProperty("generated_at").GetString().EndsWith("Z"));
    Check("the never-summed counter guard is published",
          doc.RootElement.GetProperty("counters_notes").GetString().Contains("never be added together"));
    Check("the distance-base guard is published",
          doc.RootElement.GetProperty("distance_flown_meters_notes").GetString().Contains("never sum, rank or compare"));
    Check("the not-playtime guard is published",
          doc.RootElement.GetProperty("flight_time_seconds_notes").GetString().Contains("not playtime"));
}
catch (Exception ex) { Check("populated export is valid JSON", false, ex.Message); }

// ============================================================ 6b. the subject split
Console.WriteLine();
Console.WriteLine("--- SAMPLE 3: split by subject, every pilot in every file ---");

string[] subjectFiles = { "barrkbot_flight.json", "barrkbot_flight_records.json",
                          "barrkbot_flight_counters.json", "barrkbot_flight_tiers.json" };
try
{
    Check("all four subject files are written",
          subjectFiles.All(f => File.Exists(Path.Combine(OUT, f))));

    // The whole point of splitting by subject rather than by player: a superlative drawn from
    // any one file is computed over the entire roster, so it is never partially right.
    Check("every file holds EVERY pilot, so each ranks over the whole roster",
          subjectFiles.All(f => Subject(f).GetProperty("players").EnumerateObject().Count() == 2));

    Check("every file identifies its rows on its own (name + both timestamps)",
          subjectFiles.All(f => Subject(f).GetProperty("players").EnumerateObject()
              .All(p => p.Value.TryGetProperty("name", out _) &&
                        p.Value.TryGetProperty("first_flight_at", out _) &&
                        p.Value.TryGetProperty("last_flight_at", out _))));

    Check("every file carries the full set of _notes guards",
          subjectFiles.All(f => Subject(f).GetProperty("players_notes").GetString().Contains("never means nobody")));

    Check("every file declares schema_version 4",
          subjectFiles.All(f => Subject(f).GetProperty("schema_version").GetInt32() == 4));

    Check("every file says what it holds, so the index can be chosen from",
          subjectFiles.All(f => Subject(f).GetProperty("holds").GetString().Length > 40));

    // Each subject owns its fields and does not pay for the others' width.
    var totals   = RowFor(Subject("barrkbot_flight.json"), "Bronzebeard");
    var records  = RowFor(Subject("barrkbot_flight_records.json"), "Bronzebeard");
    var counters = RowFor(Subject("barrkbot_flight_counters.json"), "Bronzebeard");
    var tiers    = RowFor(Subject("barrkbot_flight_tiers.json"), "Bronzebeard");

    Check("totals holds the career figures",
          new[]{"valkyrie_flight_level","flight_time_seconds","distance_flown_meters","flights","night_flight_seconds"}
              .All(k => totals.TryGetProperty(k, out _)));
    Check("records holds the personal bests",
          new[]{"longest_flight_seconds","longest_flight_meters","max_altitude_meters",
                "top_speed_meters_per_second","steepest_dive_meters_per_second"}
              .All(k => records.TryGetProperty(k, out _)));
    Check("counters holds the event tallies",
          new[]{"wingbeats","landings_ground","landings_water","landings_ship","stamina_denials","skill_denials"}
              .All(k => counters.TryGetProperty(k, out _)));
    Check("landings are still split three ways",
          counters.GetProperty("landings_ground").GetInt32() > 0 &&
          counters.GetProperty("landings_water").GetInt32() > 0 &&
          counters.GetProperty("landings_ship").GetInt32() > 0);

    // A subject's fields must NOT leak into the others, or the width saving is undone.
    Check("subjects do not duplicate each other's fields",
          !totals.TryGetProperty("max_altitude_meters", out _) &&
          !records.TryGetProperty("wingbeats", out _) &&
          !counters.TryGetProperty("distance_flown_meters", out _));

    // Flattened from one nested object, which the reader dropped from every multi-row listing
    // no matter which file it sat in. Four scalars are listable AND rankable.
    Check("tier time is four flat scalars, not a nested object",
          new[]{"tier_time_crude_seconds","tier_time_troll_seconds","tier_time_lox_seconds","tier_time_dragon_seconds"}
              .All(k => tiers.TryGetProperty(k, out _) && tiers.GetProperty(k).ValueKind == JsonValueKind.Number));
    Check("the old nested tier_time_seconds is gone", !tiers.TryGetProperty("tier_time_seconds", out _));
    Check("the tier split totals the overall flight time",
          Math.Abs((tiers.GetProperty("tier_time_crude_seconds").GetDouble() +
                    tiers.GetProperty("tier_time_troll_seconds").GetDouble() +
                    tiers.GetProperty("tier_time_lox_seconds").GetDouble() +
                    tiers.GetProperty("tier_time_dragon_seconds").GetDouble())
                   - totals.GetProperty("flight_time_seconds").GetDouble()) < 0.5);

    // An array can never be ranked, so the count travels beside it.
    Check("veteran crossed four biomes", tiers.GetProperty("biomes_flown_over").GetArrayLength() == 4);
    Check("distinct_biomes_flown is a rankable number matching the array",
          tiers.GetProperty("distinct_biomes_flown").GetInt32() == tiers.GetProperty("biomes_flown_over").GetArrayLength());

    Check("every field carrying a unit says so in its name",
          new[]{"flight_time_seconds","distance_flown_meters","night_flight_seconds"}.All(k => totals.TryGetProperty(k, out _)) &&
          new[]{"max_altitude_meters","longest_flight_seconds","longest_flight_meters",
                "top_speed_meters_per_second","steepest_dive_meters_per_second"}.All(k => records.TryGetProperty(k, out _)));
}
catch (Exception ex) { Check("the subject split is well formed", false, ex.Message); }

// A file this version no longer writes must be removed, not left for the sweep to rank as
// though it were current: stale data reads as fresh data with an old timestamp.
try
{
    string orphan = Path.Combine(OUT, "barrkbot_flight_legacy.json");
    string keepMe = Path.Combine(OUT, "notes_for_rohan.json");
    File.WriteAllText(orphan, "{\"players\":{}}");
    File.WriteAllText(keepMe, "{}");
    FlightReport.WriteExport();
    Check("an export this version no longer writes is swept away", !File.Exists(orphan));
    Check("a file that is not one of ours is left alone", File.Exists(keepMe));
    Check("the four current files survive the sweep",
          subjectFiles.All(f => File.Exists(Path.Combine(OUT, f))));
    File.Delete(keepMe);
}
catch (Exception ex) { Check("stale exports are swept", false, ex.Message); }

// ============================================================ 6c. safe on the DEPLOYED reader
Console.WriteLine();
Console.WriteLine("--- SAMPLE 4: nothing a pre-rollover reader would misread ---");

// The reader running in production has no concept of parts. Fed a `part`/`part_of` it shrugs,
// but fed a `players_leaders` it invents a second collection out of it -- and fed a file holding
// SOME of the pilots it ranks them with full confidence and names the wrong leader. So on a
// roster that fits, none of those keys may appear at all. This is the check that stands between
// a useful release and a confidently wrong one.
try
{
    Check("a roster that fits declares no part",
          subjectFiles.All(f => !Subject(f).TryGetProperty("part", out _) &&
                                !Subject(f).TryGetProperty("part_of", out _)));
    Check("a roster that fits ships no players_leaders for the old reader to mistake for data",
          subjectFiles.All(f => !Subject(f).TryGetProperty("players_leaders", out _)));
    Check("and no continuation files exist to be read as a second export",
          !Directory.GetFiles(OUT, "barrkbot_flight*_2.json").Any() &&
          !Directory.GetFiles(OUT, "barrkbot_flight*_3.json").Any());
}
catch (Exception ex) { Check("the export is safe on the deployed reader", false, ex.Message); }

// ============================================================ 6d. rollover, forced
Console.WriteLine();
Console.WriteLine("--- SAMPLE 5: a roster big enough to roll over ---");

// Its own folder, and its own registry with it: the later restart check reads the two fliers
// back off the registry in OUT, and forty pilots landing on top of them would be testing this
// section's leftovers rather than the restart.
string ROLL = Path.Combine(Path.GetTempPath(), "wotv-flightlog-tests-rollover");
Directory.CreateDirectory(ROLL);
foreach (var stale in Directory.GetFiles(ROLL)) File.Delete(stale);
ModConfig.FlightStatsExportFolder.Value = ROLL;
JsonElement Part(string file) =>
    JsonDocument.Parse(File.ReadAllText(Path.Combine(ROLL, file))).RootElement;
NewSession();

// Enough pilots that the widest subject cannot hold them in one file. Distances ascend with the
// index, so the true distance leader is the LAST one added -- whichever part they land in, the
// leaderboard has to name them.
for (int i = 1; i <= 40; i++)
{
    var pilot = new Player { Name = "Pilot" + i.ToString("00"), PlayerId = 5000L + i };
    FlyingSkill.LevelValue = 10f + i;
    FlightLog.LoadFrom(pilot);
    ZoneSystem.instance.GroundHeight = 10f;
    pilot.transform.position = new UnityEngine.Vector3(0, 60, 0);
    FlightLog.Tick(pilot, true, WingsItem.DragonName, 0.5f);
    for (int step = 1; step <= i; step++)
    {
        pilot.transform.position = new UnityEngine.Vector3(step * 10f, 60f, 0);
        pilot.Biome = Heightmap.Biome.Meadows;
        FlightLog.Tick(pilot, true, WingsItem.DragonName, 0.5f);
    }
    FlightLog.NoteFlap();
    FlightLog.Flush(pilot, force: true);
}
FlightReport.WriteExport();

try
{
    var partOne = Part("barrkbot_flight.json");
    Check("the roster no longer fits one file", partOne.GetProperty("part_of").GetInt32() > 1);

    int partOf = partOne.GetProperty("part_of").GetInt32();
    var partFiles = new List<string> { "barrkbot_flight.json" };
    for (int i = 2; i <= partOf; i++) partFiles.Add("barrkbot_flight_" + i + ".json");

    Check("every declared part exists on disk", partFiles.All(f => File.Exists(Path.Combine(ROLL, f))));
    Check("continuations are numbered from the SECOND file", !File.Exists(Path.Combine(ROLL, "barrkbot_flight_1.json")));
    Check("every part declares part and part_of, 1-indexed",
          partFiles.Select((f, i) => Part(f).GetProperty("part").GetInt32() == i + 1 &&
                                     Part(f).GetProperty("part_of").GetInt32() == partOf).All(x => x));

    // No pilot may be lost or counted twice on the way through the split.
    var idsAcrossParts = partFiles.SelectMany(f => Part(f).GetProperty("players").EnumerateObject().Select(p => p.Name)).ToList();
    Check("the parts together hold every pilot exactly once",
          idsAcrossParts.Count == 40 && idsAcrossParts.Distinct().Count() == 40, idsAcrossParts.Count.ToString());

    // The budget is on row width, and the reader injects a key of its own, so we aim under it.
    Check("no part exceeds the reader's row budget",
          partFiles.All(f => Part(f).GetProperty("players").EnumerateObject()
              .Sum(p => JsonSerializer.Serialize(p.Value).Length) <= 2400));

    // The whole point: a part that holds a fraction of the roster still answers correctly.
    string trueLeader = "Pilot40";
    Check("EVERY part carries a world leaderboard",
          partFiles.All(f => Part(f).TryGetProperty("players_leaders", out _)));
    Check("every part names the TRUE distance leader, not its own local best",
          partFiles.All(f => Part(f).GetProperty("players_leaders")
                                       .GetProperty("distance_flown_meters")[0]
                                       .GetProperty("name").GetString() == trueLeader));
    Check("the part that does NOT contain the leader still names them",
          partFiles.Any(f => !Part(f).GetProperty("players").EnumerateObject()
                                 .Any(p => p.Value.GetProperty("name").GetString() == trueLeader) &&
                             Part(f).GetProperty("players_leaders").GetProperty("distance_flown_meters")[0]
                                 .GetProperty("name").GetString() == trueLeader));
    Check("a leaderboard is at most three deep and sorted highest first",
          partFiles.All(f => Part(f).GetProperty("players_leaders").EnumerateObject().All(field =>
              field.Value.GetArrayLength() <= 3 &&
              field.Value.EnumerateArray().Select(e => e.GetProperty("value").GetDouble())
                   .SequenceEqual(field.Value.EnumerateArray().Select(e => e.GetProperty("value").GetDouble()).OrderByDescending(v => v)))));
    Check("a field nobody has scored on is dropped rather than led by a zero",
          !Part("barrkbot_flight_counters.json").GetProperty("players_leaders").TryGetProperty("skill_denials", out _));
    Check("parts explain that the ranking, not the rows, answers a superlative",
          partFiles.All(f => Part(f).GetProperty("part_notes").GetString().Contains("never from these rows")));

    // Kept for the reader to be run against: the shrink check below deliberately sweeps the
    // parts away, and a sample nobody can look at afterwards proves nothing to anyone.
    string PARTS = Path.Combine(OUT, "sample-rolled-over");
    Directory.CreateDirectory(PARTS);
    foreach (var f in Directory.GetFiles(PARTS)) File.Delete(f);
    foreach (var f in Directory.GetFiles(ROLL, "barrkbot_flight*.json"))
        File.Copy(f, Path.Combine(PARTS, Path.GetFileName(f)), true);

    // A roster that shrinks back must not leave a part behind for the reader to keep ranking.
    var survivors = Part("barrkbot_flight.json").GetProperty("players").EnumerateObject().Count();
    Check("rollover actually split the roster rather than writing one fat file", survivors < 40);
}
catch (Exception ex) { Check("the rollover is well formed", false, ex.Message); }

// A part family that shrinks: the orphaned continuation must go, or the reader keeps ranking it.
try
{
    NewSession();
    var lone = new Player { Name = "LastOneFlying", PlayerId = 9999L };
    FlyingSkill.LevelValue = 20f;
    FlightLog.LoadFrom(lone);
    lone.transform.position = new UnityEngine.Vector3(0, 60, 0);
    FlightLog.Tick(lone, true, WingsItem.DragonName, 0.5f);
    lone.transform.position = new UnityEngine.Vector3(50, 60, 0);
    FlightLog.Tick(lone, true, WingsItem.DragonName, 0.5f);
    FlightLog.Flush(lone, force: true);

    File.Delete(Path.Combine(ROLL, "flight_registry.dat"));
    NewSession();
    FlightLog.LoadFrom(lone);
    FlightLog.Flush(lone, force: true);
    FlightReport.WriteExport();

    Check("a roster that shrinks back to one file sweeps its old continuations away",
          !Directory.GetFiles(ROLL, "barrkbot_flight*_*.json")
              .Any(f => System.Text.RegularExpressions.Regex.IsMatch(Path.GetFileName(f), @"_[0-9]+\.json$")));
    Check("and stops declaring parts once it fits again",
          !Part("barrkbot_flight.json").TryGetProperty("part_of", out _));
    Check("and drops the leaderboard the old reader would misread",
          !Part("barrkbot_flight.json").TryGetProperty("players_leaders", out _));
}
catch (Exception ex) { Check("a shrinking roster tidies up after itself", false, ex.Message); }

ModConfig.FlightStatsExportFolder.Value = OUT;

// ============================================================ 7. restart survives
Console.WriteLine();
Console.WriteLine("--- a server restart ---");
NewSession();
string afterRestart = File.ReadAllText(FlightReport.WriteExport());
try
{
    var doc = JsonDocument.Parse(afterRestart);
    Check("both fliers come back from the registry after a restart",
          doc.RootElement.GetProperty("players").EnumerateObject().Count() == 2);
    Check("their numbers are unchanged",
          doc.RootElement.GetProperty("players").EnumerateObject()
             .Any(p => p.Value.GetProperty("name").GetString() == "Bronzebeard" &&
                       p.Value.GetProperty("flight_time_seconds").GetDouble() > 1000));
}
catch (Exception ex) { Check("registry survives a restart", false, ex.Message); }

Check("no .tmp file is left behind", !Directory.GetFiles(OUT).Any(f => f.EndsWith(".tmp")));
Check("the export is named the way the sweep expects",
      Path.GetFileName(FlightReport.ExportPath()) == "barrkbot_flight.json");

// A name full of JSON metacharacters must not break the file.
var awkward = new Player { Name = "Ro\"ss\\the\nBold", PlayerId = 1001L, m_customData = new Dictionary<string, string>(reborn.m_customData) };
FlightLog.LoadFrom(awkward);
FlightLog.Flush(awkward, force: true);
try
{
    var doc = JsonDocument.Parse(File.ReadAllText(FlightReport.WriteExport()));
    Check("a player name with quotes/backslashes/newlines still yields valid JSON",
          doc.RootElement.GetProperty("players").EnumerateObject()
             .Any(p => p.Value.GetProperty("name").GetString() == "Ro\"ss\\the\nBold"));
}
catch (Exception ex) { Check("awkward player name yields valid JSON", false, ex.Message); }

Console.WriteLine();
Console.WriteLine("samples written to " + OUT);
Console.WriteLine(failures == 0 ? "ALL CHECKS PASSED" : failures + " CHECK(S) FAILED");
return failures == 0 ? 0 : 1;
