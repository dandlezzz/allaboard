// <copyright file="TextureUtil.cs" company="board.fun game">
//     Trafalgar - Age of Sail. Procedural board.fun RTS.
// </copyright>

namespace Trafalgar.Rendering
{
    using UnityEngine;

    /// <summary>
    /// Procedural Texture2D generators (planked decks, choppy water, deck gratings, sail canvas).
    /// Every texture is generated once and cached, then shared by every renderer that needs it, so
    /// whole fleets reuse a handful of textures rather than allocating per ship.
    /// </summary>
    public static class TextureUtil
    {
        private static Texture2D s_Plank;
        private static Texture2D s_Grating;
        private static Texture2D s_Canvas;

        /// <summary>
        /// Tan deck planking: long boards (seams running vertically in UV → fore-aft on the deck),
        /// per-plank tone variation, lengthwise wood grain, and occasional caulked butt-joints.
        /// </summary>
        /// <returns>A cached repeating plank <see cref="Texture2D"/>.</returns>
        public static Texture2D PlankTexture()
        {
            if (s_Plank != null)
            {
                return s_Plank;
            }

            const int w = 128;
            const int h = 128;
            const int planks = 6;
            var tex = NewTex(w, h, "PlankTex");
            var px = new Color[w * h];
            Color baseWood = new Color(0.60f, 0.44f, 0.27f);

            for (int y = 0; y < h; y++)
            {
                float fv = y / (float)h;
                for (int x = 0; x < w; x++)
                {
                    float fu = x / (float)w;
                    float pf = fu * planks;
                    int pi = (int)pf;
                    float frac = pf - pi;

                    float toneSeed = Frac(Mathf.Sin(pi * 12.9898f) * 43758.5453f);
                    float tone = (toneSeed - 0.5f) * 0.16f;
                    float grain = (Mathf.PerlinNoise(pi * 5.3f, fv * 7f) - 0.5f) * 0.10f;

                    Color c = baseWood + new Color(tone + grain, (tone + grain) * 0.85f, (tone + grain) * 0.6f);

                    // Caulked seams between planks (darker lines), plus occasional cross butt-joints.
                    bool seam = frac < 0.05f || frac > 0.95f;
                    bool butt = Frac((fv * 2.3f) + (pi * 0.37f)) < 0.02f;
                    if (seam || butt)
                    {
                        c *= 0.55f;
                    }

                    c.a = 1f;
                    px[(y * w) + x] = c;
                }
            }

            tex.SetPixels(px);
            tex.Apply();
            s_Plank = tex;
            return tex;
        }

        /// <summary>
        /// A deck grating / hatch cover: dark cross-hatched bars with see-through holes (alpha 0 in
        /// the gaps) so the deck reads through it. Use on a transparent material.
        /// </summary>
        /// <returns>A cached grating <see cref="Texture2D"/> with an alpha mask.</returns>
        public static Texture2D GratingTexture()
        {
            if (s_Grating != null)
            {
                return s_Grating;
            }

            const int size = 32;
            const int bars = 5;
            var tex = NewTex(size, size, "GratingTex");
            var px = new Color[size * size];
            Color bar = new Color(0.18f, 0.12f, 0.07f, 1f);
            Color frame = new Color(0.10f, 0.07f, 0.04f, 1f);
            var hole = new Color(0f, 0f, 0f, 0f);

            for (int y = 0; y < size; y++)
            {
                float fv = y / (float)size;
                for (int x = 0; x < size; x++)
                {
                    float fu = x / (float)size;
                    bool border = fu < 0.10f || fu > 0.90f || fv < 0.10f || fv > 0.90f;
                    float gu = Frac(fu * bars);
                    float gv = Frac(fv * bars);
                    bool barPx = gu < 0.34f || gv < 0.34f;
                    px[(y * size) + x] = border ? frame : (barPx ? bar : hole);
                }
            }

            tex.SetPixels(px);
            tex.Apply();
            s_Grating = tex;
            return tex;
        }

        /// <summary>
        /// Semi-transparent off-white sail canvas with faint panel seams and weave, so spread sails
        /// read as translucent cloth. Use on a transparent material.
        /// </summary>
        /// <returns>A cached canvas <see cref="Texture2D"/> with partial alpha.</returns>
        public static Texture2D CanvasTexture()
        {
            if (s_Canvas != null)
            {
                return s_Canvas;
            }

            const int size = 64;
            var tex = NewTex(size, size, "CanvasTex");
            var px = new Color[size * size];
            Color cloth = new Color(0.94f, 0.92f, 0.85f);

            for (int y = 0; y < size; y++)
            {
                float fv = y / (float)size;
                for (int x = 0; x < size; x++)
                {
                    float fu = x / (float)size;
                    float weave = (Mathf.PerlinNoise(fu * 16f, fv * 16f) - 0.5f) * 0.06f;
                    Color c = cloth + new Color(weave, weave, weave);

                    // Horizontal panel seams across the sail (cloth sewn from strips).
                    bool seam = Frac(fv * 5f) < 0.04f;
                    c *= seam ? 0.9f : 1f;

                    // Translucent, a touch denser toward the centre for body.
                    c.a = 0.5f + (0.12f * Mathf.Sin(fu * Mathf.PI));
                    px[(y * size) + x] = c;
                }
            }

            tex.SetPixels(px);
            tex.Apply();
            s_Canvas = tex;
            return tex;
        }

        private static Texture2D NewTex(int w, int h, string name)
        {
            return new Texture2D(w, h, TextureFormat.RGBA32, true)
            {
                name = name,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 2,
            };
        }

        private static float Frac(float v)
        {
            return v - Mathf.Floor(v);
        }
    }
}
