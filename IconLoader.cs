using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace WingsoftheValkyrie
{
    /// <summary>
    /// Loads icon sprites from PNGs embedded in this assembly, so the mod keeps shipping as a
    /// single DLL. Lookup is by file-name suffix rather than the full manifest name, which keeps
    /// it independent of the root-namespace prefix MSBuild bakes into resource names. Every
    /// failure path returns null so the caller keeps whatever icon it already had.
    /// </summary>
    internal static class IconLoader
    {
        // Null results are cached too: a missing resource warns once, not once per lookup.
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        // ImageConversion.LoadImage bound by exact signature via reflection (same pattern as
        // BarrkUI): the module also declares ReadOnlySpan overloads, and merely compiling a
        // direct call makes the compiler resolve those too -- which fails on net48, where
        // ReadOnlySpan does not exist (CS0518).
        private static readonly MethodInfo LoadImageMethod =
            typeof(ImageConversion).GetMethod("LoadImage", new[] { typeof(Texture2D), typeof(byte[]), typeof(bool) })
            ?? typeof(ImageConversion).GetMethod("LoadImage", new[] { typeof(Texture2D), typeof(byte[]) });

        private static bool LoadPng(Texture2D tex, byte[] bytes)
        {
            if (LoadImageMethod == null) return false;

            object[] args = LoadImageMethod.GetParameters().Length == 3
                ? new object[] { tex, bytes, false }   // markNonReadable: false, Sprite.Create may read it
                : new object[] { tex, bytes };
            return (bool)LoadImageMethod.Invoke(null, args);
        }

        public static Sprite Load(string fileName)
        {
            if (Cache.TryGetValue(fileName, out Sprite cached)) return cached;

            Sprite sprite = null;
            try
            {
                Assembly asm = Assembly.GetExecutingAssembly();
                string resourceName = asm.GetManifestResourceNames()
                    .FirstOrDefault(n => n.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));

                if (resourceName == null)
                {
                    Jotunn.Logger.LogWarning($"[Wings of the Valkyrie] Embedded icon '{fileName}' not found; keeping the fallback icon.");
                }
                else
                {
                    byte[] bytes;
                    using (Stream stream = asm.GetManifestResourceStream(resourceName))
                    using (var buffer = new MemoryStream())
                    {
                        stream.CopyTo(buffer);
                        bytes = buffer.ToArray();
                    }

                    // Size and format here are placeholders; LoadImage replaces both from the PNG.
                    var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false)
                    {
                        name = "wotv_icon_" + fileName
                    };

                    if (LoadPng(tex, bytes))
                    {
                        sprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                        sprite.name = tex.name;
                    }
                    else
                    {
                        Jotunn.Logger.LogWarning($"[Wings of the Valkyrie] Embedded icon '{fileName}' is not a decodable PNG.");
                    }
                }
            }
            catch (Exception ex)
            {
                Jotunn.Logger.LogWarning($"[Wings of the Valkyrie] Failed loading embedded icon '{fileName}': {ex.Message}");
            }

            Cache[fileName] = sprite;
            return sprite;
        }
    }
}
