using BepInEx.Configuration;
using System;
using System.Collections.Generic;
using System.IO;

namespace WingsoftheValkyrie
{
    // Config migration.
    //
    // v2 rebalanced every crafting recipe and retuned the skill XP rates, but BepInEx writes
    // every bound value into the .cfg -- so a 1.1.x file carries the old defaults as literal
    // lines, and on upgrade those stale lines silently win over the new balance. Unlike
    // TortalPortal's migration (which carries renamed keys to new homes), no key here ever
    // moved; what changed is the DEFAULTS. So the rule is: a stored value still equal to its
    // old default belongs to the mod and is rebased onto the new default; a stored value an
    // admin changed is real work and is preserved untouched.
    //
    // Same machinery shape as TortalPortal and Fatty: stamp a layout version on the file,
    // back it up before touching it, and never let a failed migration stop the mod loading.
    //
    // To add a migration:
    //   1. bump CurrentConfigVersion
    //   2. add the entries whose defaults changed to Rebases, keyed by the NEW version, each
    //      listing every old default it might find on disk
    // Begin/Finish do the rest.
    public static class ConfigMigration
    {
        //   0 = unstamped. Any file from before 1.9.1, which had no migration system. Covers
        //       both live 1.1.x files (old recipe defaults) and 1.9.0 test files (the skill
        //       XP rates as first shipped, before the -20% learning-rate tune).
        //   1 = the v2 layout: Bronze Age recipes, raised station levels, tuned XP rates.
        //   2 = the 2.0.1 "earn the sky" balance: per-tier flight stats rebased so the listed
        //       numbers are what MASTERY buys, with the skill bonus curves widened to match.
        public const int CurrentConfigVersion = 2;

        public sealed class Rebase
        {
            public string Section;
            public string Key;

            // Every default this slot shipped with before this version. A stored value equal
            // to ANY of them is the mod's own old default, not admin work.
            public string[] OldDefaults;
        }

        // Keyed by the version the rebases produce: Rebases[n] takes a file at n-1 up to n.
        private static readonly Dictionary<int, Rebase[]> Rebases = new Dictionary<int, Rebase[]>
        {
            { 1, new[]
                {
                    new Rebase { Section = "2. Crude Wings", Key = "MinStationLevel", OldDefaults = new[] { "1" } },
                    new Rebase { Section = "2. Crude Wings", Key = "CraftingRequirements", OldDefaults = new[] { "Feathers:10,LeatherScraps:10" } },
                    new Rebase { Section = "3. Troll Wings", Key = "MinStationLevel", OldDefaults = new[] { "2" } },
                    new Rebase { Section = "3. Troll Wings", Key = "CraftingRequirements", OldDefaults = new[] { "TrollHide:5,Feathers:15" } },
                    new Rebase { Section = "4. Lox Wings", Key = "MinStationLevel", OldDefaults = new[] { "1" } },
                    new Rebase { Section = "4. Lox Wings", Key = "CraftingRequirements", OldDefaults = new[] { "LoxPelt:5,Silver:5" } },
                    new Rebase { Section = "5. Dragon Wings", Key = "CraftingRequirements", OldDefaults = new[] { "Feathers:20,Eitr:5" } },
                    // 1.9.0 test builds shipped the skill XP 20% hotter than it ended up.
                    new Rebase { Section = "6. Valkyrie Flight Skill", Key = "XpPerFlap", OldDefaults = new[] { "0.5" } },
                    new Rebase { Section = "6. Valkyrie Flight Skill", Key = "XpPerGlideSecond", OldDefaults = new[] { "0.25" } },
                }
            },
            // 2.0.1 flipped every tier from "the listed stat is what you get" to "the listed
            // stat is what mastery buys". Bases came down, the skill bonus curves went up, and
            // the ceiling became skill-scaled. Every flight stat below carries the SAME default
            // through 1.1.x and 2.0.0 -- neither release retuned them -- so one old default per
            // slot covers a file arriving from either version. XP rates are deliberately absent:
            // 2.0.1 left the learning rate alone and lets the new gates do the work.
            { 2, new[]
                {
                    new Rebase { Section = "2. Crude Wings", Key = "FlightCeiling", OldDefaults = new[] { "120" } },
                    new Rebase { Section = "2. Crude Wings", Key = "GlideSpeed", OldDefaults = new[] { "10" } },
                    new Rebase { Section = "2. Crude Wings", Key = "FlapForce", OldDefaults = new[] { "15" } },
                    new Rebase { Section = "2. Crude Wings", Key = "FlapStaminaCost", OldDefaults = new[] { "10" } },

                    new Rebase { Section = "3. Troll Wings", Key = "FlightCeiling", OldDefaults = new[] { "135" } },
                    new Rebase { Section = "3. Troll Wings", Key = "GlideSpeed", OldDefaults = new[] { "15" } },
                    new Rebase { Section = "3. Troll Wings", Key = "FlapForce", OldDefaults = new[] { "18" } },
                    new Rebase { Section = "3. Troll Wings", Key = "FlapStaminaCost", OldDefaults = new[] { "8" } },

                    new Rebase { Section = "4. Lox Wings", Key = "FlightCeiling", OldDefaults = new[] { "160" } },
                    new Rebase { Section = "4. Lox Wings", Key = "GlideSpeed", OldDefaults = new[] { "20" } },
                    new Rebase { Section = "4. Lox Wings", Key = "FlapForce", OldDefaults = new[] { "22" } },
                    new Rebase { Section = "4. Lox Wings", Key = "FlapStaminaCost", OldDefaults = new[] { "6" } },

                    new Rebase { Section = "5. Dragon Wings", Key = "FlightCeiling", OldDefaults = new[] { "1100" } },
                    new Rebase { Section = "5. Dragon Wings", Key = "GlideSpeed", OldDefaults = new[] { "30" } },
                    new Rebase { Section = "5. Dragon Wings", Key = "FlapForce", OldDefaults = new[] { "28" } },
                    new Rebase { Section = "5. Dragon Wings", Key = "FlapStaminaCost", OldDefaults = new[] { "4" } },

                    new Rebase { Section = "6. Valkyrie Flight Skill", Key = "StaminaReductionAtMax", OldDefaults = new[] { "0.5" } },
                    new Rebase { Section = "6. Valkyrie Flight Skill", Key = "FlapPowerBonusAtMax", OldDefaults = new[] { "0.3" } },
                    new Rebase { Section = "6. Valkyrie Flight Skill", Key = "GlideSinkReductionAtMax", OldDefaults = new[] { "0.5" } },
                    new Rebase { Section = "6. Valkyrie Flight Skill", Key = "GlideSpeedBonusAtMax", OldDefaults = new[] { "0.15" } },
                }
            }
        };

        // Raw "Section::Key" -> serialized value, read off disk before anything was bound.
        private static readonly Dictionary<string, string> Snapshot = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Slots whose stored value matched an old default, waiting to be reset onto the new
        // default once binding has happened.
        private static readonly List<ConfigDefinition> Pending = new List<ConfigDefinition>();

        private static int _fileVersion;

        private static string Slot(string section, string key) => section + "::" + key;

        // ---- before any Bind ------------------------------------------------------------------

        public static void Begin(ConfigFile config)
        {
            Snapshot.Clear();
            Pending.Clear();
            _fileVersion = CurrentConfigVersion;

            try
            {
                string path = config.ConfigFilePath;
                if (!File.Exists(path)) return; // fresh install: new defaults bind on their own

                ReadRaw(path, Snapshot);

                _fileVersion = ReadStampedVersion(Snapshot);
                if (_fileVersion >= CurrentConfigVersion) return;

                Backup(path, _fileVersion);
                Jotunn.Logger.LogWarning($"[Wings of the Valkyrie] Migrating {Path.GetFileName(path)} from config version {_fileVersion} to {CurrentConfigVersion}. Values you changed are kept; values still at their old defaults move to the new ones. The previous file is backed up beside it.");

                PlanRebases();
            }
            catch (Exception ex)
            {
                // A failed migration must never stop the mod loading - worst case the config
                // binds exactly as it always did, which is the old behaviour.
                Jotunn.Logger.LogError($"[Wings of the Valkyrie] Config migration could not start, settings will be read as-is. Reason: {ex}");
            }
        }

        private static void PlanRebases()
        {
            for (int version = _fileVersion + 1; version <= CurrentConfigVersion; version++)
            {
                if (!Rebases.TryGetValue(version, out Rebase[] steps)) continue;

                foreach (Rebase rebase in steps)
                {
                    if (!Snapshot.TryGetValue(Slot(rebase.Section, rebase.Key), out string stored)) continue;

                    bool wasOldDefault = false;
                    foreach (string oldDefault in rebase.OldDefaults)
                    {
                        if (string.Equals(stored.Trim(), oldDefault, StringComparison.Ordinal)) { wasOldDefault = true; break; }
                    }

                    if (wasOldDefault)
                    {
                        Pending.Add(new ConfigDefinition(rebase.Section, rebase.Key));
                        Jotunn.Logger.LogInfo($"[Wings of the Valkyrie] [{rebase.Section}] {rebase.Key} was still the old default; moving it to the new default.");
                    }
                    else
                    {
                        Jotunn.Logger.LogInfo($"[Wings of the Valkyrie] [{rebase.Section}] {rebase.Key} was customised ('{stored}'); keeping your value.");
                    }
                }
            }
        }

        // ---- after every Bind -----------------------------------------------------------------

        public static void Finish(ConfigFile config, ConfigEntry<int> versionEntry)
        {
            try
            {
                foreach (ConfigDefinition definition in Pending)
                {
                    if (!config.ContainsKey(definition)) continue;

                    // The bound entry currently holds the stale on-disk value; its DefaultValue
                    // is the new v2 default the Bind call declared. No string round-trip, so
                    // type conversion and range clamping stay BepInEx's problem, not ours.
                    ConfigEntryBase entry = config[definition];
                    entry.BoxedValue = entry.DefaultValue;
                }

                if (versionEntry != null) versionEntry.Value = CurrentConfigVersion;
                config.Save();
            }
            catch (Exception ex)
            {
                Jotunn.Logger.LogError($"[Wings of the Valkyrie] Config migration could not finish - check the backup beside your config file. Reason: {ex}");
            }
            finally
            {
                Snapshot.Clear();
                Pending.Clear();
            }
        }

        // ---- file handling --------------------------------------------------------------------

        private static void Backup(string path, int fromVersion)
        {
            try
            {
                File.Copy(path, path + $".v{fromVersion}.bak", overwrite: true);
            }
            catch (Exception ex)
            {
                Jotunn.Logger.LogError($"[Wings of the Valkyrie] Could not back up the config before migrating ({ex.Message}). Migrating anyway.");
            }
        }

        private static int ReadStampedVersion(Dictionary<string, string> snapshot)
        {
            if (snapshot.TryGetValue(Slot(ModConfig.MetaSection, ModConfig.ConfigVersionKey), out string raw) &&
                int.TryParse(raw, out int version))
            {
                return version;
            }

            // No stamp at all: a pre-1.9.1 file, which is version 0 by definition.
            return 0;
        }

        // BepInEx config files are plain INI: [Section] headers, '#' comments, blank lines, and
        // "Key = value" where the value may itself contain '=' or ','.
        private static void ReadRaw(string path, Dictionary<string, string> into)
        {
            string section = "";

            foreach (string rawLine in File.ReadAllLines(path))
            {
                string line = rawLine.Trim();
                if (line.Length == 0 || line[0] == '#') continue;

                if (line[0] == '[' && line[line.Length - 1] == ']')
                {
                    section = line.Substring(1, line.Length - 2).Trim();
                    continue;
                }

                int eq = line.IndexOf('=');
                if (eq <= 0) continue;

                string key = line.Substring(0, eq).Trim();
                string value = line.Substring(eq + 1).Trim();
                if (key.Length == 0) continue;

                into[Slot(section, key)] = value;
            }
        }
    }
}
