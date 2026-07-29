using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace WingsoftheValkyrie
{
    /// <summary>
    /// The wing items are clones of vanilla capes, so Valheim will happily attach the original
    /// cape mesh to the shoulders on top of our procedural runic wings.
    ///
    /// Hiding renderers on the item prefab cannot fix this: VisEquipment.AttachArmor instantiates
    /// the prefab's "attach_skin" child and rebinds it to the player skeleton, and
    /// Renderer.forceRenderingOff is a runtime-only flag that Unity does not copy across
    /// Instantiate (this is why the cape reappeared in 1.1.2). Renderer.enabled would survive,
    /// but it also hides the item lying on the ground and any mod that walks the player's
    /// renderers can switch it back on.
    ///
    /// So we stop the attachment from ever being built. Nothing exists to re-enable, the cloth
    /// simulation never runs, and the dropped-item model is left untouched.
    /// </summary>
    [HarmonyPatch]
    public static class CapeVisualPatch
    {
        private static readonly AccessTools.FieldRef<VisEquipment, List<GameObject>> ShoulderInstancesRef =
            ReflectionUtil.TryFieldRef<VisEquipment, List<GameObject>>("m_shoulderItemInstances");

        [HarmonyPatch(typeof(VisEquipment), "AttachArmor")]
        [HarmonyPrefix]
        public static bool AttachArmorPrefix(int itemHash, ref List<GameObject> __result)
        {
            if (!WingsItem.IsWingsHash(itemHash)) return true;

            // Callers store the result straight into m_*ItemInstances and later iterate it to
            // destroy the attachments, so hand back an empty list rather than null.
            __result = new List<GameObject>();
            return false;
        }

        /// <summary>
        /// Safety net for the case where another mod's prefix skips ours and builds the
        /// attachment anyway. Runs last so it sees whatever everyone else ended up with.
        /// </summary>
        [HarmonyPatch(typeof(VisEquipment), "SetShoulderEquipped")]
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        public static void SetShoulderEquippedPostfix(VisEquipment __instance, int hash)
        {
            if (ShoulderInstancesRef == null || !WingsItem.IsWingsHash(hash)) return;

            var instances = ShoulderInstancesRef(__instance);
            if (instances == null) return;

            for (int i = 0; i < instances.Count; i++)
            {
                if (instances[i] != null) instances[i].SetActive(false);
            }
        }
    }
}
