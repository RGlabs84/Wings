using HarmonyLib;

namespace WingsoftheValkyrie
{
    internal static class ReflectionUtil
    {
        /// <summary>
        /// Binds a fast field accessor, returning null instead of throwing if the field is gone.
        /// These bindings live in static initializers, so an unguarded failure after a Valheim
        /// update would surface as a TypeInitializationException and disable the whole mod
        /// rather than just the feature that depends on the field.
        /// </summary>
        public static AccessTools.FieldRef<T, F> TryFieldRef<T, F>(string fieldName)
        {
            try
            {
                return AccessTools.FieldRefAccess<T, F>(fieldName);
            }
            catch (System.Exception ex)
            {
                Jotunn.Logger.LogWarning(
                    $"[Wings of the Valkyrie] Could not bind {typeof(T).Name}.{fieldName} ({ex.Message}). " +
                    "The feature depending on it will be skipped.");
                return null;
            }
        }
    }
}
