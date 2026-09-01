// Minimal stand-ins for the Unity / Valheim / BepInEx surface FlightLog.cs touches, so the
// real source file can be compiled and exercised off-game.
using System.Collections.Generic;

namespace UnityEngine
{
    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public float magnitude => (float)System.Math.Sqrt(x * x + y * y + z * z);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new Vector3(a.x - b.x, a.y - b.y, a.z - b.z);
    }
    public class Transform { public Vector3 position; }
    public static class Time { public static float realtimeSinceStartup = 0f; }
    public static class Mathf { public static float Max(float a, float b) => a > b ? a : b; }
}

namespace BepInEx { public static class Paths { public static string ConfigPath = "/tmp/wotv-test-config"; } }
namespace WingsoftheValkyrie
{
    public static class Log
    {
        public static void LogWarning(object m) => System.Console.WriteLine("[warn] " + m);
        public static void LogInfo(object m) => System.Console.WriteLine("[info] " + m);
        public static void LogError(object m) => System.Console.WriteLine("[error] " + m);
    }
}

public static class Heightmap
{
    [System.Flags]
    public enum Biome
    {
        None = 0, Meadows = 1, Swamp = 2, Mountain = 4, BlackForest = 8,
        Plains = 16, AshLands = 32, DeepNorth = 64, Ocean = 256, Mistlands = 512,
    }
}

public class Ship { }

public class Player
{
    public Dictionary<string, string> m_customData = new Dictionary<string, string>();
    public UnityEngine.Transform transform = new UnityEngine.Transform();
    public string Name = "Ross";
    public bool Swimming, InWaterFlag;
    public Ship StandingOn;
    public UnityEngine.Vector3 Velocity;
    public Heightmap.Biome Biome = Heightmap.Biome.Meadows;

    public long PlayerId = 1L;
    public long GetPlayerID() => PlayerId;
    public string GetPlayerName() => Name;
    public bool IsSwimming() => Swimming;
    public bool InWater() => InWaterFlag;
    public Ship GetStandingOnShip() => StandingOn;
    public UnityEngine.Vector3 GetVelocity() => Velocity;
    public Heightmap.Biome GetCurrentBiome() => Biome;
}

public class ZoneSystem
{
    public static ZoneSystem instance = new ZoneSystem();
    public float GroundHeight = 0f;
    public float GetGroundHeight(UnityEngine.Vector3 p) => GroundHeight;
}

public static class EnvMan { public static bool Night; public static bool IsNight() => Night; }

namespace WingsoftheValkyrie
{
    public class Entry<T> { public T Value; public Entry(T v) { Value = v; } }

    public static class ModConfig
    {
        public static Entry<bool> EnableFlightLog = new Entry<bool>(true);
        public static Entry<bool> PublishFlightStats = new Entry<bool>(true);
        public static Entry<string> FlightStatsExportFolder = new Entry<string>("");
        public static Entry<float> FlightStatsWriteInterval = new Entry<float>(60f);
        public static Entry<float> FlightStatsReportInterval = new Entry<float>(60f);
    }

    public static class WingsItem
    {
        public const string CrudeName = "WingsOf_Crude";
        public const string TrollName = "WingsOf_Troll";
        public const string LoxName = "WingsOf_Lox";
        public const string DragonName = "WingsOf_Dragon";
    }

    public static class FlyingSkill
    {
        public static float LevelValue = 0f;
        public static float Level(Player p) => LevelValue;
    }

    public static class WingsoftheValkyriePlugin
    {
        public const string PluginName = "Wings of the Valkyrie";
        public const string PluginVersion = "2.1.1";
    }
}

// ---- networking stand-ins, added for the FlightReport tests ---------------------------------

public class ZNetPeer { public long m_uid; }

public class ZNet
{
    public static ZNet instance = new ZNet();
    public bool Server = true;
    public ZNetPeer ServerPeer = new ZNetPeer { m_uid = 777L };
    public bool IsServer() => Server;
    public ZNetPeer GetServerPeer() => ServerPeer;
}

public class ZRoutedRpc
{
    public static ZRoutedRpc instance = new ZRoutedRpc();
    private readonly Dictionary<string, Delegate> _handlers = new Dictionary<string, Delegate>();
    public int Invocations;

    public void Register<T, U, V>(string name, Action<long, T, U, V> handler) => _handlers[name] = handler;

    public void InvokeRoutedRPC(long target, string method, params object[] parameters)
    {
        Invocations++;
        // Deliver it the way the real router would: straight to whoever registered the name.
        if (_handlers.TryGetValue(method, out Delegate handler))
            handler.DynamicInvoke(new object[] { 1L }.Concat(parameters).ToArray());
    }
}

namespace WingsoftheValkyrie
{
    public static partial class ModConfigExtra { }
}
