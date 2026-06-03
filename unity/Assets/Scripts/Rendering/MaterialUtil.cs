// <copyright file="MaterialUtil.cs" company="board.fun game">
//     Trafalgar - Age of Sail. Procedural board.fun RTS.
// </copyright>

namespace Trafalgar.Rendering
{
    using UnityEngine;

    /// <summary>
    /// Helpers for creating runtime materials with built-in render pipeline shaders, so the
    /// project needs no shader/material assets on disk.
    /// </summary>
    public static class MaterialUtil
    {
        private static Shader s_UnlitColor;
        private static Shader s_Sprite;
        private static Shader s_UnlitTransparent;
        private static Shader s_UnlitTexture;

        /// <summary>Gets the always-included sprite shader (used for lines and flat sprites).</summary>
        public static Shader SpriteShader
        {
            get
            {
                if (s_Sprite == null)
                {
                    s_Sprite = Shader.Find("Sprites/Default");
                }

                return s_Sprite;
            }
        }

        /// <summary>
        /// Creates an unlit, flat-coloured material. Ideal for a top-down RTS where consistent
        /// readability matters more than realistic shading.
        /// </summary>
        /// <param name="color">The colour to apply.</param>
        /// <returns>A new <see cref="Material"/>.</returns>
        public static Material Unlit(Color color)
        {
            if (s_UnlitColor == null)
            {
                s_UnlitColor = Shader.Find("Unlit/Color");
            }

            if (s_UnlitColor != null)
            {
                var mat = new Material(s_UnlitColor);
                mat.color = color;
                return mat;
            }

            // Fall back to the sprite shader, which supports per-material colour tinting.
            var fallback = new Material(SpriteShader);
            fallback.color = color;
            return fallback;
        }

        /// <summary>
        /// Creates a flat, opaque coloured material that shows its EXACT colour regardless of scene
        /// lighting. The art is top-down and read at a glance, so surfaces are unlit; this avoids the
        /// Standard (lit) shader washing light-toned surfaces (sea, decks, sails, stripes) to white
        /// under the scene's ambient + directional light. The <paramref name="smoothness"/> argument
        /// is retained only for call-site compatibility and is unused.
        /// </summary>
        /// <param name="color">The flat colour to display exactly.</param>
        /// <param name="smoothness">Ignored (kept for compatibility).</param>
        /// <returns>A new unlit <see cref="Material"/>.</returns>
        public static Material Lit(Color color, float smoothness = 0.1f)
        {
            return Unlit(color);
        }

        /// <summary>
        /// Creates a flat, opaque textured material (planked deck) shown UNLIT, so the texture's own
        /// colours read exactly without lighting wash-out. <paramref name="tint"/> is unused (callers
        /// pass white); the unlit texture shader has no tint slot.
        /// </summary>
        /// <param name="texture">The albedo texture.</param>
        /// <param name="tint">Ignored (kept for compatibility; callers pass white).</param>
        /// <param name="smoothness">Ignored (kept for compatibility).</param>
        /// <returns>A new unlit textured <see cref="Material"/>.</returns>
        public static Material LitTextured(Texture texture, Color tint, float smoothness = 0.08f)
        {
            if (s_UnlitTexture == null)
            {
                s_UnlitTexture = Shader.Find("Unlit/Texture");
            }

            Material mat = s_UnlitTexture != null ? new Material(s_UnlitTexture) : new Material(SpriteShader);
            mat.mainTexture = texture;
            return mat;
        }

        /// <summary>
        /// Creates an unlit, alpha-blended textured material (used for translucent sails and the
        /// see-through deck gratings). Falls back to the sprite shader if Unlit/Transparent is
        /// unavailable.
        /// </summary>
        /// <param name="texture">A texture whose alpha drives transparency.</param>
        /// <returns>A new transparent <see cref="Material"/>.</returns>
        public static Material UnlitTransparent(Texture texture)
        {
            if (s_UnlitTransparent == null)
            {
                s_UnlitTransparent = Shader.Find("Unlit/Transparent");
            }

            Material mat = s_UnlitTransparent != null
                ? new Material(s_UnlitTransparent)
                : new Material(SpriteShader);
            mat.mainTexture = texture;
            return mat;
        }
    }
}
