// Stand-ins for the BepInEx config surface ConfigMigration.cs uses, so the real migration can
// be run against real .cfg files off-game.
using System.Collections.Generic;

namespace BepInEx.Configuration
{
    public class ConfigDefinition
    {
        public string Section, Key;
        public ConfigDefinition(string section, string key) { Section = section; Key = key; }
        public override bool Equals(object o) =>
            o is ConfigDefinition d && d.Section == Section && d.Key == Key;
        public override int GetHashCode() => (Section + "::" + Key).GetHashCode();
        public override string ToString() => Section + "::" + Key;
    }

    public abstract class ConfigEntryBase
    {
        public object BoxedValue { get; set; }
        public object DefaultValue { get; protected set; }
    }

    public class ConfigEntry<T> : ConfigEntryBase
    {
        public ConfigEntry(T stored, T defaultValue) { BoxedValue = stored; DefaultValue = defaultValue; }
        public T Value { get => (T)BoxedValue; set => BoxedValue = value; }
    }

    public class ConfigFile
    {
        public string ConfigFilePath;
        public Dictionary<ConfigDefinition, ConfigEntryBase> Entries = new Dictionary<ConfigDefinition, ConfigEntryBase>();
        public int SaveCount;
        public bool ContainsKey(ConfigDefinition d) => Entries.ContainsKey(d);
        public ConfigEntryBase this[ConfigDefinition d] => Entries[d];
        public void Save() { SaveCount++; }
    }
}

namespace WingsoftheValkyrie
{
    public static class Log
    {
        public static List<string> Lines = new List<string>();
        public static void LogInfo(object m) { Lines.Add("INFO " + m); }
        public static void LogWarning(object m) { Lines.Add("WARN " + m); }
        public static void LogError(object m) { Lines.Add("ERROR " + m); }
    }

    public static class ModConfig
    {
        public const string MetaSection = "0. Meta";
        public const string ConfigVersionKey = "ConfigVersion";
    }
}
