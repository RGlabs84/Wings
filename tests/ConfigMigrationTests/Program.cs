using BepInEx.Configuration;
using WingsoftheValkyrie;

// Fixtures are committed copies of real .cfg files, so this runs the same way on any machine
// and keeps running after a Gale profile is edited or deleted.
string FIXTURES = Path.Combine(AppContext.BaseDirectory, "fixtures");
string WORK = Path.Combine(Path.GetTempPath(), "wotv-migration-tests");
if (Directory.Exists(WORK)) Directory.Delete(WORK, recursive: true);
Directory.CreateDirectory(WORK);

int failures = 0;
void Check(string label, bool ok, string detail = "")
{
    Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + label + (detail.Length > 0 ? "   [" + detail + "]" : ""));
    if (!ok) failures++;
}

// The current defaults, exactly as ModConfig.cs binds them.
var NewDefaults = new (string Section, string Key, object Value)[]
{
    ("2. Crude Wings",  "FlightCeiling", 130f), ("2. Crude Wings",  "GlideSpeed", 8f),
    ("2. Crude Wings",  "FlapForce", 11f),      ("2. Crude Wings",  "FlapStaminaCost", 16f),
    ("3. Troll Wings",  "FlightCeiling", 150f), ("3. Troll Wings",  "GlideSpeed", 12f),
    ("3. Troll Wings",  "FlapForce", 13f),      ("3. Troll Wings",  "FlapStaminaCost", 13f),
    ("4. Lox Wings",    "FlightCeiling", 190f), ("4. Lox Wings",    "GlideSpeed", 16f),
    ("4. Lox Wings",    "FlapForce", 16f),      ("4. Lox Wings",    "FlapStaminaCost", 10f),
    ("5. Dragon Wings", "FlightCeiling", 1300f),("5. Dragon Wings", "GlideSpeed", 24f),
    ("5. Dragon Wings", "FlapForce", 20f),      ("5. Dragon Wings", "FlapStaminaCost", 7f),
    ("6. Valkyrie Flight Skill", "StaminaReductionAtMax", 0.55f),
    ("6. Valkyrie Flight Skill", "FlapPowerBonusAtMax", 0.5f),
    ("6. Valkyrie Flight Skill", "GlideSinkReductionAtMax", 0.6f),
    ("6. Valkyrie Flight Skill", "GlideSpeedBonusAtMax", 0.35f),
    // v1 rows, still declared because a v0 file passes through them on its way to the top.
    // The two workbench floors are 2 rather than 3 as of v3 -- see the migration's own note.
    ("2. Crude Wings",  "MinStationLevel", 2),
    ("2. Crude Wings",  "CraftingRequirements", "BronzeNails:10,DeerHide:10,LeatherScraps:20,TrollHide:5,Feathers:20"),
    ("3. Troll Wings",  "MinStationLevel", 2),
    ("3. Troll Wings",  "CraftingRequirements", "TrollHide:15,IronNails:10,Feathers:30"),
    ("4. Lox Wings",    "MinStationLevel", 3),
    ("4. Lox Wings",    "CraftingRequirements", "LoxPelt:10,Silver:20,LinenThread:10,Feathers:30"),
    ("5. Dragon Wings", "CraftingRequirements", "Feathers:40,Eitr:20,ScaleHide:10,DragonTear:2"),
    ("6. Valkyrie Flight Skill", "XpPerFlap", 0.4f),
    ("6. Valkyrie Flight Skill", "XpPerGlideSecond", 0.2f),
};

// Reads a raw .cfg the way BepInEx would, so "what was on disk" is the stored value.
Dictionary<string, string> ReadRaw(string path)
{
    var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    string section = "";
    foreach (string rawLine in File.ReadAllLines(path))
    {
        string line = rawLine.Trim();
        if (line.Length == 0 || line[0] == '#') continue;
        if (line[0] == '[' && line[^1] == ']') { section = line[1..^1].Trim(); continue; }
        int eq = line.IndexOf('=');
        if (eq <= 0) continue;
        map[section + "::" + line[..eq].Trim()] = line[(eq + 1)..].Trim();
    }
    return map;
}

object Parse(string raw, object defaultValue) => defaultValue switch
{
    float => float.TryParse(raw, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var f) ? f : defaultValue,
    int   => int.TryParse(raw, out var i) ? i : defaultValue,
    _     => (object)raw,
};

// Runs the REAL Begin -> (simulated Bind) -> Finish cycle against a copy of a real file.
(ConfigFile Config, ConfigEntry<int> Version) Migrate(string sourcePath, string workName)
{
    string work = Path.Combine(WORK, workName);
    Directory.CreateDirectory(Path.GetDirectoryName(work));
    foreach (string stale in Directory.GetFiles(Path.GetDirectoryName(work), workName + "*")) File.Delete(stale);
    File.Copy(sourcePath, work, overwrite: true);

    var config = new ConfigFile { ConfigFilePath = work };
    WingsoftheValkyrie.Log.Lines.Clear();

    ConfigMigration.Begin(config);   // real code: snapshots the file and plans the rebases

    // Simulate BepInEx binding: each entry holds the stored value, and knows its new default.
    var onDisk = ReadRaw(work);
    foreach (var (section, key, def) in NewDefaults)
    {
        object stored = onDisk.TryGetValue(section + "::" + key, out string raw) ? Parse(raw, def) : def;
        var definition = new ConfigDefinition(section, key);
        config.Entries[definition] = def switch
        {
            float f => new ConfigEntry<float>((float)stored, f),
            int i => new ConfigEntry<int>((int)stored, i),
            _ => (ConfigEntryBase)new ConfigEntry<string>((string)stored, (string)def),
        };
    }
    var versionEntry = new ConfigEntry<int>(
        onDisk.TryGetValue("0. Meta::ConfigVersion", out string v) ? int.Parse(v) : 0,
        ConfigMigration.CurrentConfigVersion);

    ConfigMigration.Finish(config, versionEntry);   // real code: applies the rebases
    return (config, versionEntry);
}

object ValueOf(ConfigFile c, string section, string key) => c[new ConfigDefinition(section, key)].BoxedValue;

string TESTER = Path.Combine(FIXTURES, "v1-stock-2.0.0.cfg");

Console.WriteLine("--- a stock 2.0.0 file (ConfigVersion 1) upgrading to the current layout ---");
var (stock, stockVersion) = Migrate(TESTER, "stock.cfg");
Check("config version is stamped 3", stockVersion.Value == 3, stockVersion.Value.ToString());
Check("Crude ceiling  120 -> 130", (float)ValueOf(stock, "2. Crude Wings", "FlightCeiling") == 130f);
Check("Crude stamina   10 -> 16", (float)ValueOf(stock, "2. Crude Wings", "FlapStaminaCost") == 16f);
Check("Crude flap      15 -> 11", (float)ValueOf(stock, "2. Crude Wings", "FlapForce") == 11f);
Check("Troll glide     15 -> 12", (float)ValueOf(stock, "3. Troll Wings", "GlideSpeed") == 12f);
Check("Lox ceiling    160 -> 190", (float)ValueOf(stock, "4. Lox Wings", "FlightCeiling") == 190f);
Check("Dragon ceiling 1100 -> 1300", (float)ValueOf(stock, "5. Dragon Wings", "FlightCeiling") == 1300f);
Check("Dragon stamina   4 -> 7", (float)ValueOf(stock, "5. Dragon Wings", "FlapStaminaCost") == 7f);
Check("StaminaReduction 0.5  -> 0.55", (float)ValueOf(stock, "6. Valkyrie Flight Skill", "StaminaReductionAtMax") == 0.55f);
Check("FlapPowerBonus   0.3  -> 0.5",  (float)ValueOf(stock, "6. Valkyrie Flight Skill", "FlapPowerBonusAtMax") == 0.5f);
Check("GlideSpeedBonus  0.15 -> 0.35", (float)ValueOf(stock, "6. Valkyrie Flight Skill", "GlideSpeedBonusAtMax") == 0.35f);
Check("XP rates are left exactly where they were", (float)ValueOf(stock, "6. Valkyrie Flight Skill", "XpPerFlap") == 0.4f);
Check("recipes untouched by the 2.0.1 step",
      (string)ValueOf(stock, "2. Crude Wings", "CraftingRequirements") == "BronzeNails:10,DeerHide:10,LeatherScraps:20,TrollHide:5,Feathers:20");
Check("a backup was written before touching anything",
      File.Exists(Path.Combine(WORK, "stock.cfg.v1.bak")));
Check("the file was saved once", stock.SaveCount == 1);
// v3. A workbench stops at level 5 and upgrading to quality 4 needs MinStationLevel + 3, so a
// floor of 3 would have made the top upgrade uncraftable on both workbench tiers.
Check("Crude station level 3 -> 2 so quality 4 is reachable", (int)ValueOf(stock, "2. Crude Wings", "MinStationLevel") == 2);
Check("Troll station level 3 -> 2 as well", (int)ValueOf(stock, "3. Troll Wings", "MinStationLevel") == 2);
Check("Lox stays at 3 - a forge reaches level 7", (int)ValueOf(stock, "4. Lox Wings", "MinStationLevel") == 3);

Console.WriteLine();
Console.WriteLine("--- an admin who tuned their own values keeps them ---");
string admin = Path.Combine(WORK, "adminsource.cfg");
Directory.CreateDirectory(Path.GetDirectoryName(admin));
File.WriteAllLines(admin, File.ReadAllLines(TESTER)
    .Select(l => l.StartsWith("FlightCeiling = 1100") ? "FlightCeiling = 4000" : l)
    .Select(l => l.StartsWith("FlapStaminaCost = 10") ? "FlapStaminaCost = 2" : l)
    .Select(l => l.StartsWith("GlideSpeedBonusAtMax = 0.15") ? "GlideSpeedBonusAtMax = 0.9" : l));
var (tuned, _) = Migrate(admin, "admin.cfg");
Check("admin's Dragon ceiling 4000 is preserved", (float)ValueOf(tuned, "5. Dragon Wings", "FlightCeiling") == 4000f);
Check("admin's Crude stamina 2 is preserved", (float)ValueOf(tuned, "2. Crude Wings", "FlapStaminaCost") == 2f);
Check("admin's GlideSpeedBonus 0.9 is preserved", (float)ValueOf(tuned, "6. Valkyrie Flight Skill", "GlideSpeedBonusAtMax") == 0.9f);
Check("untouched slots beside them still rebase", (float)ValueOf(tuned, "2. Crude Wings", "FlightCeiling") == 130f);
Check("the log says which values were kept",
      WingsoftheValkyrie.Log.Lines.Any(l => l.Contains("was customised") && l.Contains("4000")));

Console.WriteLine();
Console.WriteLine("--- a 1.1.x file (unstamped, version 0) jumping straight to the current layout ---");
string v0 = Path.Combine(FIXTURES, "v0-unstamped-1.1.x.cfg");
var (old, oldVersion) = Migrate(v0, "v0.cfg");
Check("config version is stamped 3", oldVersion.Value == 3, oldVersion.Value.ToString());
Check("the v1 recipe rebase still applies",
      (string)ValueOf(old, "2. Crude Wings", "CraftingRequirements") == "BronzeNails:10,DeerHide:10,LeatherScraps:20,TrollHide:5,Feathers:20");
Check("station level lands on the v3 value, not the v1 one it passed through",
      (int)ValueOf(old, "2. Crude Wings", "MinStationLevel") == 2);
Check("and the v2 stat rebase applies in the same pass", (float)ValueOf(old, "2. Crude Wings", "FlapStaminaCost") == 16f);
Check("Dragon ceiling reaches the 2.0.1 value", (float)ValueOf(old, "5. Dragon Wings", "FlightCeiling") == 1300f);
Check("backup names the version it came from", File.Exists(Path.Combine(WORK, "v0.cfg.v0.bak")));

Console.WriteLine();
Console.WriteLine("--- running it twice must change nothing the second time ---");
File.Copy(Path.Combine(WORK, "stock.cfg"), Path.Combine(WORK, "again-source.cfg"), true);
// Re-stamp the file as the current version the way Finish + BepInEx would have written it back.
var lines = File.ReadAllLines(Path.Combine(WORK, "again-source.cfg"))
    .Select(l => l.StartsWith("ConfigVersion = ") ? "ConfigVersion = 3" : l).ToList();
File.WriteAllLines(Path.Combine(WORK, "again-source.cfg"), lines);
WingsoftheValkyrie.Log.Lines.Clear();
var (again, againVersion) = Migrate(Path.Combine(WORK, "again-source.cfg"), "again.cfg");
Check("an already-migrated file is left alone", !WingsoftheValkyrie.Log.Lines.Any(l => l.Contains("Migrating")));
Check("no second backup is written", !File.Exists(Path.Combine(WORK, "again.cfg.v3.bak")));
Check("version stays 3", againVersion.Value == 3);

Console.WriteLine();
Console.WriteLine("--- a missing config file (fresh install) ---");
var fresh = new ConfigFile { ConfigFilePath = Path.Combine(WORK, "does-not-exist.cfg") };
WingsoftheValkyrie.Log.Lines.Clear();
ConfigMigration.Begin(fresh);
var freshVersion = new ConfigEntry<int>(0, ConfigMigration.CurrentConfigVersion);
ConfigMigration.Finish(fresh, freshVersion);
Check("a fresh install stamps the current version and logs no migration",
      freshVersion.Value == 3 && !WingsoftheValkyrie.Log.Lines.Any(l => l.Contains("Migrating")));

Console.WriteLine();
Console.WriteLine(failures == 0 ? "ALL CHECKS PASSED" : failures + " CHECK(S) FAILED");
return failures == 0 ? 0 : 1;
