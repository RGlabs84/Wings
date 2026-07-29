using UnityEngine;

namespace WingsoftheValkyrie.VFX
{
    public static class ValkyrieRuneTextureGenerator
    {
        private const int Size = 128;

        // Entry point used by ValkyrieRuneVFX today
        public static Texture2D GenerateRune(Color color)
        {
            int seed = Mathf.Abs(color.GetHashCode());
            int runeIndex = seed % 16; // 16 runes
            return GenerateRune(color, runeIndex);
        }

        // Generate a 4x4 atlas of all 16 runes
        public static Texture2D GenerateRuneAtlas(Color color)
        {
            int atlasSize = 512;
            int cellSize = 128;
            Texture2D tex = new Texture2D(atlasSize, atlasSize, TextureFormat.RGBA32, true);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            Color clear = new Color(0, 0, 0, 0);
            Color[] blanks = new Color[atlasSize * atlasSize];
            for (int i = 0; i < blanks.Length; i++) blanks[i] = clear;
            tex.SetPixels(blanks);

            for (int i = 0; i < 16; i++)
            {
                int col = i % 4;
                int row = 3 - (i / 4); // Top to bottom
                DrawRuneOntoAtlas(tex, color, i, col * cellSize, row * cellSize, cellSize);
            }

            tex.Apply(true);
            return tex;
        }

        // New: generate a soft circular glow texture for particle-based wakes
        public static Texture2D GenerateSoftTrail(Color color)
        {
            Texture2D tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;

            Vector2 center = new Vector2(Size / 2f, Size / 2f);
            float maxDist = Size / 2f;

            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                    float dist = Vector2.Distance(p, center) / maxDist; // 0..1
                    // Smooth radial falloff
                    float a = Mathf.Clamp01(1f - dist);
                    // Make a softer falloff
                    a = Mathf.SmoothStep(0f, 1f, a);
                    a = Mathf.Pow(a, 1.6f);

                    Color c = new Color(color.r, color.g, color.b, color.a * a);
                    tex.SetPixel(x, y, c);
                }
            }

            tex.Apply(true);
            return tex;
        }

        // Generate an elongated streak texture (vertical smear) for directional wake
        public static Texture2D GenerateStreak(Color color)
        {
            Texture2D tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;

            Vector2 center = new Vector2(Size / 2f, Size / 2f);
            float maxDist = Size / 2f;

            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    // Compute distance along streak axis (y) and perpendicular (x)
                    float along = Mathf.Abs((y + 0.5f) - center.y) / maxDist; // 0..1
                    float across = Mathf.Abs((x + 0.5f) - center.x) / (maxDist * 0.6f); // tighter across

                    float a = Mathf.Clamp01(1f - across);
                    // stretch falloff along length
                    float lengthFade = Mathf.SmoothStep(1f, 0f, along);
                    a *= Mathf.Pow(lengthFade, 0.9f);

                    Color c = new Color(color.r, color.g, color.b, color.a * a);
                    tex.SetPixel(x, y, c);
                }
            }

            tex.Apply(true);
            return tex;
        }

        // Blend soft trail with faint rune detail for magical look
        public static Texture2D GenerateBlend(Color color)
        {
            Texture2D soft = GenerateSoftTrail(color);
            Texture2D rune = GenerateRune(color);

            Texture2D outTex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            outTex.filterMode = FilterMode.Bilinear;

            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    Color s = soft.GetPixel(x, y);
                    Color r = rune.GetPixel(x, y);
                    // blend: soft base plus faint rune overlay
                    float a = Mathf.Clamp01(s.a + r.a * 0.65f);
                    Color c = new Color(color.r, color.g, color.b, color.a * a);
                    outTex.SetPixel(x, y, c);
                }
            }

            outTex.Apply(true);
            return outTex;
        }

        private static void DrawRuneOntoAtlas(Texture2D tex, Color color, int runeIndex, int offsetX, int offsetY, int size)
        {
            Color glow = new Color(color.r * 0.6f, color.g * 0.6f, color.b * 0.6f, 0.6f);
            Color stroke = new Color(color.r, color.g, color.b, 1f);

            // Scale coordinates (designed for 128x128)
            float scale = size / 128f;

            void DrawLineLocal(int x0, int y0, int x1, int y1, Color c, int thickness)
            {
                DrawLineAtlas(tex, offsetX + (int)(x0 * scale), offsetY + (int)(y0 * scale), 
                                   offsetX + (int)(x1 * scale), offsetY + (int)(y1 * scale), c, thickness, 512);
            }

            // Pick a rune pattern
            switch (runeIndex)
            {
                case 0:
                    DrawLineLocal(20, 20, 108, 108, glow, 6);
                    DrawLineLocal(108, 20, 20, 108, glow, 6);
                    DrawLineLocal(20, 20, 108, 108, stroke, 3);
                    DrawLineLocal(108, 20, 20, 108, stroke, 3);
                    break;
                case 1:
                    DrawLineLocal(64, 16, 64, 112, glow, 6);
                    DrawLineLocal(64, 64, 32, 96, glow, 5);
                    DrawLineLocal(64, 64, 96, 96, glow, 5);
                    DrawLineLocal(64, 16, 64, 112, stroke, 3);
                    DrawLineLocal(64, 64, 32, 96, stroke, 2);
                    DrawLineLocal(64, 64, 96, 96, stroke, 2);
                    break;
                case 2:
                    DrawLineLocal(40, 20, 40, 108, glow, 6);
                    DrawLineLocal(40, 96, 96, 112, glow, 5);
                    DrawLineLocal(40, 72, 96, 88, glow, 5);
                    DrawLineLocal(40, 20, 40, 108, stroke, 3);
                    DrawLineLocal(40, 96, 96, 112, stroke, 2);
                    DrawLineLocal(40, 72, 96, 88, stroke, 2);
                    break;
                case 3:
                    DrawLineLocal(40, 20, 40, 112, glow, 6);
                    DrawLineLocal(40, 96, 88, 112, glow, 5);
                    DrawLineLocal(88, 112, 88, 80, glow, 5);
                    DrawLineLocal(88, 80, 40, 96, glow, 5);
                    DrawLineLocal(40, 20, 40, 112, stroke, 3);
                    DrawLineLocal(40, 96, 88, 112, stroke, 2);
                    DrawLineLocal(88, 112, 88, 80, stroke, 2);
                    DrawLineLocal(88, 80, 40, 96, stroke, 2);
                    break;
                case 4:
                    DrawLineLocal(64, 16, 64, 112, glow, 8);
                    DrawLineLocal(64, 16, 64, 112, stroke, 4);
                    break;
                case 5:
                    DrawLineLocal(64, 16, 64, 112, glow, 6);
                    DrawLineLocal(64, 40, 32, 72, glow, 5);
                    DrawLineLocal(64, 40, 96, 72, glow, 5);
                    DrawLineLocal(64, 16, 64, 112, stroke, 3);
                    DrawLineLocal(64, 40, 32, 72, stroke, 2);
                    DrawLineLocal(64, 40, 96, 72, stroke, 2);
                    break;
                case 6:
                    DrawLineLocal(36, 16, 36, 112, glow, 6);
                    DrawLineLocal(92, 16, 92, 112, glow, 6);
                    DrawLineLocal(36, 36, 92, 92, glow, 5);
                    DrawLineLocal(36, 16, 36, 112, stroke, 3);
                    DrawLineLocal(92, 16, 92, 112, stroke, 3);
                    DrawLineLocal(36, 36, 92, 92, stroke, 2);
                    break;
                case 7:
                    DrawLineLocal(80, 20, 40, 20, glow, 5);
                    DrawLineLocal(80, 20, 40, 64, glow, 5);
                    DrawLineLocal(40, 64, 80, 108, glow, 5);
                    DrawLineLocal(80, 108, 40, 108, glow, 5);
                    DrawLineLocal(80, 20, 40, 20, stroke, 2);
                    DrawLineLocal(80, 20, 40, 64, stroke, 2);
                    DrawLineLocal(40, 64, 80, 108, stroke, 2);
                    DrawLineLocal(80, 108, 40, 108, stroke, 2);
                    break;
                case 8:
                    DrawLineLocal(64, 16, 64, 112, glow, 6);
                    DrawLineLocal(96, 96, 32, 32, glow, 5);
                    DrawLineLocal(64, 16, 64, 112, stroke, 3);
                    DrawLineLocal(96, 96, 32, 32, stroke, 2);
                    break;
                case 9:
                    DrawLineLocal(32, 48, 32, 112, glow, 6);
                    DrawLineLocal(96, 48, 96, 112, glow, 6);
                    DrawLineLocal(32, 48, 64, 16, glow, 5);
                    DrawLineLocal(64, 16, 96, 48, glow, 5);
                    DrawLineLocal(32, 48, 32, 112, stroke, 3);
                    DrawLineLocal(96, 48, 96, 112, stroke, 3);
                    DrawLineLocal(32, 48, 64, 16, stroke, 2);
                    DrawLineLocal(64, 16, 96, 48, stroke, 2);
                    break;
                case 10:
                    DrawLineLocal(48, 16, 48, 112, glow, 6);
                    DrawLineLocal(48, 80, 96, 48, glow, 5);
                    DrawLineLocal(48, 16, 48, 112, stroke, 3);
                    DrawLineLocal(48, 80, 96, 48, stroke, 2);
                    break;
                case 11:
                    DrawLineLocal(64, 112, 96, 72, glow, 5);
                    DrawLineLocal(96, 72, 64, 32, glow, 5);
                    DrawLineLocal(64, 32, 32, 72, glow, 5);
                    DrawLineLocal(32, 72, 64, 112, glow, 5);
                    DrawLineLocal(32, 72, 16, 16, glow, 5);
                    DrawLineLocal(96, 72, 112, 16, glow, 5);
                    DrawLineLocal(64, 112, 96, 72, stroke, 2);
                    DrawLineLocal(96, 72, 64, 32, stroke, 2);
                    DrawLineLocal(64, 32, 32, 72, stroke, 2);
                    DrawLineLocal(32, 72, 64, 112, stroke, 2);
                    DrawLineLocal(32, 72, 16, 16, stroke, 2);
                    DrawLineLocal(96, 72, 112, 16, stroke, 2);
                    break;
                case 12:
                    DrawLineLocal(20, 20, 64, 64, glow, 5);
                    DrawLineLocal(20, 108, 64, 64, glow, 5);
                    DrawLineLocal(108, 20, 64, 64, glow, 5);
                    DrawLineLocal(108, 108, 64, 64, glow, 5);
                    DrawLineLocal(20, 20, 64, 64, stroke, 2);
                    DrawLineLocal(20, 108, 64, 64, stroke, 2);
                    DrawLineLocal(108, 20, 64, 64, stroke, 2);
                    DrawLineLocal(108, 108, 64, 64, stroke, 2);
                    break;
                case 13:
                    DrawLineLocal(36, 16, 36, 112, glow, 6);
                    DrawLineLocal(36, 96, 80, 80, glow, 5);
                    DrawLineLocal(80, 80, 36, 64, glow, 5);
                    DrawLineLocal(36, 64, 80, 48, glow, 5);
                    DrawLineLocal(80, 48, 36, 32, glow, 5);
                    DrawLineLocal(36, 16, 36, 112, stroke, 3);
                    DrawLineLocal(36, 96, 80, 80, stroke, 2);
                    DrawLineLocal(80, 80, 36, 64, stroke, 2);
                    DrawLineLocal(36, 64, 80, 48, stroke, 2);
                    DrawLineLocal(80, 48, 36, 32, stroke, 2);
                    break;
                case 14:
                    DrawLineLocal(32, 16, 32, 112, glow, 6);
                    DrawLineLocal(96, 16, 96, 112, glow, 6);
                    DrawLineLocal(32, 112, 96, 64, glow, 5);
                    DrawLineLocal(96, 112, 32, 64, glow, 5);
                    DrawLineLocal(32, 16, 32, 112, stroke, 3);
                    DrawLineLocal(96, 16, 96, 112, stroke, 3);
                    DrawLineLocal(32, 112, 96, 64, stroke, 2);
                    DrawLineLocal(96, 112, 32, 64, stroke, 2);
                    break;
                case 15:
                    DrawLineLocal(64, 16, 108, 64, glow, 6);
                    DrawLineLocal(108, 64, 64, 112, glow, 6);
                    DrawLineLocal(64, 112, 20, 64, glow, 6);
                    DrawLineLocal(20, 64, 64, 16, glow, 6);
                    DrawLineLocal(64, 16, 108, 64, stroke, 3);
                    DrawLineLocal(108, 64, 64, 112, stroke, 3);
                    DrawLineLocal(64, 112, 20, 64, stroke, 3);
                    DrawLineLocal(20, 64, 64, 16, stroke, 3);
                    break;
            }
        }

        // Internal: generate a specific rune by index
        public static Texture2D GenerateRune(Color color, int runeIndex)
        {
            // Create with mipmaps to reduce aliasing and ensure proper sampling on particles
            Texture2D tex = new Texture2D(Size, Size, TextureFormat.RGBA32, true);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            Color clear = new Color(0, 0, 0, 0);
            Color glow  = new Color(color.r * 0.6f, color.g * 0.6f, color.b * 0.6f, 0.6f);
            Color stroke = new Color(color.r, color.g, color.b, 1f);

            // Clear
            Color[] blanks = new Color[Size * Size];
            for (int i = 0; i < blanks.Length; i++) blanks[i] = clear;
            tex.SetPixels(blanks);

            // Pick a rune pattern
            switch (runeIndex)
            {
                // 0: Crossed rune (your original X, but with halo first)
                case 0:
                    DrawLine(tex, 20, 20, 108, 108, glow, 6);
                    DrawLine(tex, 108, 20, 20, 108, glow, 6);
                    DrawLine(tex, 20, 20, 108, 108, stroke, 3);
                    DrawLine(tex, 108, 20, 20, 108, stroke, 3);
                    break;

                // 1: Algiz‑like (ᛉ)
                case 1:
                    // Vertical spine
                    DrawLine(tex, 64, 16, 64, 112, glow, 6);
                    // Arms
                    DrawLine(tex, 64, 64, 32, 96, glow, 5);
                    DrawLine(tex, 64, 64, 96, 96, glow, 5);
                    DrawLine(tex, 64, 16, 64, 112, stroke, 3);
                    DrawLine(tex, 64, 64, 32, 96, stroke, 2);
                    DrawLine(tex, 64, 64, 96, 96, stroke, 2);
                    break;

                // 2: Fehu‑like (ᚠ)
                case 2:
                    // Vertical spine
                    DrawLine(tex, 40, 20, 40, 108, glow, 6);
                    // Upper branch
                    DrawLine(tex, 40, 96, 96, 112, glow, 5);
                    // Lower branch
                    DrawLine(tex, 40, 72, 96, 88, glow, 5);
                    DrawLine(tex, 40, 20, 40, 108, stroke, 3);
                    DrawLine(tex, 40, 96, 96, 112, stroke, 2);
                    DrawLine(tex, 40, 72, 96, 88, stroke, 2);
                    break;

                // 3: Raidho‑like (ᚱ)
                case 3:
                    // Vertical spine
                    DrawLine(tex, 40, 20, 40, 112, glow, 6);
                    // Upper loop
                    DrawLine(tex, 40, 96, 88, 112, glow, 5);
                    DrawLine(tex, 88, 112, 88, 80, glow, 5);
                    DrawLine(tex, 88, 80, 40, 96, glow, 5);
                    DrawLine(tex, 40, 20, 40, 112, stroke, 3);
                    DrawLine(tex, 40, 96, 88, 112, stroke, 2);
                    DrawLine(tex, 88, 112, 88, 80, stroke, 2);
                    DrawLine(tex, 88, 80, 40, 96, stroke, 2);
                    break;

                // 4: Isa‑like (ᛁ)
                case 4:
                    DrawLine(tex, 64, 16, 64, 112, glow, 8);
                    DrawLine(tex, 64, 16, 64, 112, stroke, 4);
                    break;

                // 5: Tiwaz‑like (ᛏ) — upward arrow / spear
                case 5:
                    // Vertical spine
                    DrawLine(tex, 64, 16, 64, 112, glow, 6);
                    // Left arm pointing up-left
                    DrawLine(tex, 64, 40, 32, 72, glow, 5);
                    // Right arm pointing up-right
                    DrawLine(tex, 64, 40, 96, 72, glow, 5);
                    DrawLine(tex, 64, 16, 64, 112, stroke, 3);
                    DrawLine(tex, 64, 40, 32, 72, stroke, 2);
                    DrawLine(tex, 64, 40, 96, 72, stroke, 2);
                    break;

                // 6: Hagalaz‑like (ᚺ) — two verticals with diagonal bar
                case 6:
                    // Left spine
                    DrawLine(tex, 36, 16, 36, 112, glow, 6);
                    // Right spine
                    DrawLine(tex, 92, 16, 92, 112, glow, 6);
                    // Diagonal connector
                    DrawLine(tex, 36, 36, 92, 92, glow, 5);
                    DrawLine(tex, 36, 16, 36, 112, stroke, 3);
                    DrawLine(tex, 92, 16, 92, 112, stroke, 3);
                    DrawLine(tex, 36, 36, 92, 92, stroke, 2);
                    break;

                // 7: Sowilo‑like (ᛊ) — lightning / S zigzag
                case 7:
                default:
                    // Top horizontal
                    DrawLine(tex, 80, 20, 40, 20, glow, 5);
                    // Upper diagonal down-left
                    DrawLine(tex, 80, 20, 40, 64, glow, 5);
                    // Lower diagonal down-right
                    DrawLine(tex, 40, 64, 80, 108, glow, 5);
                    // Bottom horizontal
                    DrawLine(tex, 80, 108, 40, 108, glow, 5);
                    DrawLine(tex, 80, 20, 40, 20, stroke, 2);
                    DrawLine(tex, 80, 20, 40, 64, stroke, 2);
                    DrawLine(tex, 40, 64, 80, 108, stroke, 2);
                    DrawLine(tex, 80, 108, 40, 108, stroke, 2);
                    break;

                // 8: Nauthiz (ᚾ) — Need/constraint: vertical spine + diagonal cross-slash
                case 8:
                    DrawLine(tex, 64, 16, 64, 112, glow, 6);
                    DrawLine(tex, 96, 96, 32, 32, glow, 5);
                    DrawLine(tex, 64, 16, 64, 112, stroke, 3);
                    DrawLine(tex, 96, 96, 32, 32, stroke, 2);
                    break;

                // 9: Uruz (ᚢ) — Strength: inverted arch (two posts + curved top)
                case 9:
                    DrawLine(tex, 32, 48, 32, 112, glow, 6);
                    DrawLine(tex, 96, 48, 96, 112, glow, 6);
                    DrawLine(tex, 32, 48, 64, 16, glow, 5);
                    DrawLine(tex, 64, 16, 96, 48, glow, 5);
                    DrawLine(tex, 32, 48, 32, 112, stroke, 3);
                    DrawLine(tex, 96, 48, 96, 112, stroke, 3);
                    DrawLine(tex, 32, 48, 64, 16, stroke, 2);
                    DrawLine(tex, 64, 16, 96, 48, stroke, 2);
                    break;

                // 10: Kenaz (ᚲ) — Torch: vertical spine + single arm upper-right
                case 10:
                    DrawLine(tex, 48, 16, 48, 112, glow, 6);
                    DrawLine(tex, 48, 80, 96, 48, glow, 5);
                    DrawLine(tex, 48, 16, 48, 112, stroke, 3);
                    DrawLine(tex, 48, 80, 96, 48, stroke, 2);
                    break;

                // 11: Othala (ᛟ) — Heritage: diamond with two descending legs
                case 11:
                    DrawLine(tex, 64, 112, 96, 72, glow, 5);
                    DrawLine(tex, 96, 72, 64, 32, glow, 5);
                    DrawLine(tex, 64, 32, 32, 72, glow, 5);
                    DrawLine(tex, 32, 72, 64, 112, glow, 5);
                    DrawLine(tex, 32, 72, 16, 16, glow, 5);
                    DrawLine(tex, 96, 72, 112, 16, glow, 5);
                    DrawLine(tex, 64, 112, 96, 72, stroke, 2);
                    DrawLine(tex, 96, 72, 64, 32, stroke, 2);
                    DrawLine(tex, 64, 32, 32, 72, stroke, 2);
                    DrawLine(tex, 32, 72, 64, 112, stroke, 2);
                    DrawLine(tex, 32, 72, 16, 16, stroke, 2);
                    DrawLine(tex, 96, 72, 112, 16, stroke, 2);
                    break;

                // 12: Dagaz (ᛞ) — Day/breakthrough: butterfly hourglass
                case 12:
                    DrawLine(tex, 20, 20, 64, 64, glow, 5);
                    DrawLine(tex, 20, 108, 64, 64, glow, 5);
                    DrawLine(tex, 108, 20, 64, 64, glow, 5);
                    DrawLine(tex, 108, 108, 64, 64, glow, 5);
                    DrawLine(tex, 20, 20, 64, 64, stroke, 2);
                    DrawLine(tex, 20, 108, 64, 64, stroke, 2);
                    DrawLine(tex, 108, 20, 64, 64, stroke, 2);
                    DrawLine(tex, 108, 108, 64, 64, stroke, 2);
                    break;

                // 13: Berkanan (ᛒ) — Growth: spine + two right-side bumps
                case 13:
                    DrawLine(tex, 36, 16, 36, 112, glow, 6);
                    DrawLine(tex, 36, 96, 80, 80, glow, 5);
                    DrawLine(tex, 80, 80, 36, 64, glow, 5);
                    DrawLine(tex, 36, 64, 80, 48, glow, 5);
                    DrawLine(tex, 80, 48, 36, 32, glow, 5);
                    DrawLine(tex, 36, 16, 36, 112, stroke, 3);
                    DrawLine(tex, 36, 96, 80, 80, stroke, 2);
                    DrawLine(tex, 80, 80, 36, 64, stroke, 2);
                    DrawLine(tex, 36, 64, 80, 48, stroke, 2);
                    DrawLine(tex, 80, 48, 36, 32, stroke, 2);
                    break;

                // 14: Mannaz (ᛗ) — Mankind: two spines + upper X cross
                case 14:
                    DrawLine(tex, 32, 16, 32, 112, glow, 6);
                    DrawLine(tex, 96, 16, 96, 112, glow, 6);
                    DrawLine(tex, 32, 112, 96, 64, glow, 5);
                    DrawLine(tex, 96, 112, 32, 64, glow, 5);
                    DrawLine(tex, 32, 16, 32, 112, stroke, 3);
                    DrawLine(tex, 96, 16, 96, 112, stroke, 3);
                    DrawLine(tex, 32, 112, 96, 64, stroke, 2);
                    DrawLine(tex, 96, 112, 32, 64, stroke, 2);
                    break;

                // 15: Ingwaz (ᛜ) — Fertility: pure diamond / rhombus
                case 15:
                    DrawLine(tex, 64, 16, 108, 64, glow, 6);
                    DrawLine(tex, 108, 64, 64, 112, glow, 6);
                    DrawLine(tex, 64, 112, 20, 64, glow, 6);
                    DrawLine(tex, 20, 64, 64, 16, glow, 6);
                    DrawLine(tex, 64, 16, 108, 64, stroke, 3);
                    DrawLine(tex, 108, 64, 64, 112, stroke, 3);
                    DrawLine(tex, 64, 112, 20, 64, stroke, 3);
                    DrawLine(tex, 20, 64, 64, 16, stroke, 3);
                    break;
            }

            // Generate mipmaps
            tex.Apply(true);
            return tex;
        }

        private static void DrawLineAtlas(Texture2D tex, int x0, int y0, int x1, int y1, Color c, int thickness, int atlasSize)
        {
            int dx = Mathf.Abs(x1 - x0);
            int dy = Mathf.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1;
            int sy = y0 < y1 ? 1 : -1;
            int err = dx - dy;

            while (true)
            {
                DrawCircleAtlas(tex, x0, y0, thickness, c, atlasSize);

                if (x0 == x1 && y0 == y1)
                    break;

                int e2 = 2 * err;
                if (e2 > -dy) { err -= dy; x0 += sx; }
                if (e2 < dx)  { err += dx; y0 += sy; }
            }
        }

        private static void DrawCircleAtlas(Texture2D tex, int cx, int cy, int r, Color c, int atlasSize)
        {
            for (int y = -r; y <= r; y++)
            {
                for (int x = -r; x <= r; x++)
                {
                    if (x * x + y * y <= r * r)
                    {
                        int px = cx + x;
                        int py = cy + y;
                        if (px >= 0 && px < atlasSize && py >= 0 && py < atlasSize)
                            tex.SetPixel(px, py, c);
                    }
                }
            }
        }
        private static void DrawLine(Texture2D tex, int x0, int y0, int x1, int y1, Color c, int thickness)
        {
            int dx = Mathf.Abs(x1 - x0);
            int dy = Mathf.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1;
            int sy = y0 < y1 ? 1 : -1;
            int err = dx - dy;

            while (true)
            {
                DrawCircle(tex, x0, y0, thickness, c);

                if (x0 == x1 && y0 == y1)
                    break;

                int e2 = 2 * err;
                if (e2 > -dy) { err -= dy; x0 += sx; }
                if (e2 < dx)  { err += dx; y0 += sy; }
            }
        }

        private static void DrawCircle(Texture2D tex, int cx, int cy, int r, Color c)
        {
            for (int y = -r; y <= r; y++)
            {
                for (int x = -r; x <= r; x++)
                {
                    if (x * x + y * y <= r * r)
                    {
                        int px = cx + x;
                        int py = cy + y;
                        if (px >= 0 && px < Size && py >= 0 && py < Size)
                            tex.SetPixel(px, py, c);
                    }
                }
            }
        }
    }
}
