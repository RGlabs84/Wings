using System.Collections.Generic;
using UnityEngine;


namespace WingsoftheValkyrie.VFX
{
    public static class ValkyrieRuneVFX_Helper
    {
        // Cache key: shader instance-ID + texture instance-ID + color hash
        private static readonly Dictionary<long, Material> _materialCache = new();
        private static bool _initialized = false;

        private static void Init()
        {
            if (_initialized)
                return;

            _initialized = true;
            Log.LogInfo("🜁 VFX‑Forge: Seeking glowing, additive, unlit shaders worthy of Njord.");
        }

        // Compute a cheap 64-bit composite cache key from shader, texture and color.
        private static long MakeCacheKey(Shader shader, Texture2D tex, Color color)
        {
            int sid  = shader != null ? shader.GetInstanceID() : 0;
            int tid  = tex    != null ? tex.GetInstanceID()    : 0;
            int cid  = color.GetHashCode();
            // pack: shader (24 bits) ^ tex (20 bits) ^ color (20 bits) — good enough for our scale
            return ((long)sid << 32) ^ ((long)(uint)tid << 16) ^ (uint)cid;
        }

        /// <summary>
        /// Find the best glowing/additive/unlit particle shader available at runtime,
        /// create a material from it, configure alpha-blended rune rendering, and cache it.
        /// Returns null when no suitable shader is found.
        /// </summary>
        public static Material GetOrCreateRuneMaterial(Texture2D runeTex, Color color)
        {
            Init();

            // ── 1. Find the best particle shader ────────────────────────────
            // Prefer an explicit known-working shader; fall back to runtime scoring.
            Shader best = Shader.Find("Particles/Standard Unlit")
                       ?? Shader.Find("Legacy Shaders/Particles/Alpha Blended")
                       ?? Shader.Find("Particles/Alpha Blended")
                       ?? Shader.Find("Particles/Additive");

            if (best == null)
            {
                // Runtime scoring: scan existing materials for a high-quality candidate
                float bestScore = float.MinValue;

                foreach (var mat in Resources.FindObjectsOfTypeAll<Material>())
                {
                    if (mat == null || mat.shader == null)
                        continue;

                    Shader s = mat.shader;
                    string name = s.name.ToLower();
                    float score = 0f;

                    if (name.Contains("particle"))  score += 3f;
                    if (name.Contains("add"))        score += 3f;
                    if (name.Contains("unlit"))      score += 2f;
                    if (name.Contains("transparent")) score += 1.5f;

                    // Require property for color
                    if (!mat.HasProperty("_Color") && !mat.HasProperty("_TintColor")) continue;

                    if (score > bestScore)
                    {
                        bestScore = score;
                        best = s;
                    }
                }
            }

            if (best == null)
            {
                Log.LogError("🜁 VFX‑Forge: No glowing shader found. The runes refuse to shine.");
                return null;
            }

            // ── 2. Check cache ────────────────────────────────────────────────
            long key = MakeCacheKey(best, runeTex, color);
            if (_materialCache.TryGetValue(key, out Material cached) && cached != null)
                return cached;

            // ── 3. Build material ─────────────────────────────────────────────
            Material m = new Material(best);

            // Texture
            if (runeTex != null)
            {
                if (m.HasProperty("_MainTex"))  m.SetTexture("_MainTex", runeTex);
                if (m.HasProperty("_BaseMap"))  m.SetTexture("_BaseMap", runeTex);
                try { m.mainTexture = runeTex; } catch { }
            }

            // Color / emission tint
            if (m.HasProperty("_Color"))
                m.SetColor("_Color", color);
            if (m.HasProperty("_TintColor"))
                m.SetColor("_TintColor", color);
            if (m.HasProperty("_EmissionColor"))
                m.SetColor("_EmissionColor", color * 3f);

            // ── 4. Additive blending ──────────────────────────────────────────
            // Additive (One, One) gives the classic glowing "ink-on-water" look and
            // avoids opaque squares around each rune on dark backgrounds.
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
            m.SetInt("_ZWrite",   0);
            m.DisableKeyword("_ALPHATEST_ON");
            m.EnableKeyword("_ALPHABLEND_ON");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.renderQueue = 3000;

            // ── 5. Cache and return ───────────────────────────────────────────
            _materialCache[key] = m;

            Log.LogInfo($"🜁 VFX‑Forge: Forged glowing additive rune material from '{best.name}'.");
            return m;
        }

        /// <summary>Clear the material cache (e.g. on config reload or preset change).</summary>
        public static void ClearCache()
        {
            _materialCache.Clear();
            _initialized = false;
        }
    }
}
