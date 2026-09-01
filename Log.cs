using BepInEx.Logging;

namespace WingsoftheValkyrie
{
    /// <summary>
    /// The mod's log sink. Replaces Jotunn.Logger, which is what every call site used while
    /// Jotunn was a dependency.
    ///
    /// Every method tolerates never having been initialised: the off-game test harnesses compile
    /// these same source files against a stub, and a logging call must never be the thing that
    /// throws inside a catch block that was trying to report a different failure.
    /// </summary>
    internal static class Log
    {
        private static ManualLogSource _source;

        public static void Init(ManualLogSource source)
        {
            _source = source;
        }

        public static void LogInfo(object message)
        {
            if (_source != null) _source.LogInfo(message);
            else UnityEngine.Debug.Log(message);
        }

        public static void LogWarning(object message)
        {
            if (_source != null) _source.LogWarning(message);
            else UnityEngine.Debug.LogWarning(message);
        }

        public static void LogError(object message)
        {
            if (_source != null) _source.LogError(message);
            else UnityEngine.Debug.LogError(message);
        }
    }
}
