// <copyright file="UIFactory.cs" company="board.fun game">
//     Trafalgar - Age of Sail. Procedural board.fun RTS.
// </copyright>

namespace Trafalgar.UI
{
    using UnityEngine;
    using UnityEngine.UI;

    /// <summary>
    /// Helpers for building the entire HUD in code (no prefabs / scene assets). Everything is
    /// created on a Screen-Space-Overlay canvas with a 1:1 pixel mapping so screen coordinates
    /// (bottom-left origin, matching Board contacts and the new Input System) line up directly.
    /// </summary>
    public static class UIFactory
    {
        private static Font s_Font;
        private static Sprite s_Triangle;

        /// <summary>
        /// Gets a cached, procedurally-generated solid white triangle sprite that points "up"
        /// (apex at the top, base at the bottom). Used for crisp HUD arrowheads that can be tinted
        /// and rotated. No art asset required.
        /// </summary>
        /// <returns>An upward-pointing triangle <see cref="Sprite"/>.</returns>
        public static Sprite TriangleSprite()
        {
            if (s_Triangle != null)
            {
                return s_Triangle;
            }

            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[size * size];
            var clear = new Color(1f, 1f, 1f, 0f);
            float cx = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                float ny = y / (float)(size - 1);           // 0 = bottom, 1 = top
                float halfWidth = (1f - ny) * cx;            // wide at the base, a point at the apex
                for (int x = 0; x < size; x++)
                {
                    pixels[(y * size) + x] = Mathf.Abs(x - cx) <= halfWidth ? Color.white : clear;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            s_Triangle = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
            return s_Triangle;
        }

        /// <summary>Gets a usable built-in dynamic font, robust across Unity versions.</summary>
        /// <returns>A <see cref="Font"/> instance.</returns>
        public static Font GetFont()
        {
            if (s_Font != null)
            {
                return s_Font;
            }

            // PRIMARY path: an OS font. Resources.GetBuiltinResource<Font> is deliberately NOT used
            // first because on this editor version it logs a Unity error ("…could not be loaded")
            // and returns null *without throwing* (so a try/catch can't suppress it). OS fonts have
            // no such issue and always exist on a desktop/Board host. Cached on first success.
            s_Font = TryOsFont(new[] { "Arial", "Helvetica", "Helvetica Neue", "Liberation Sans", "Arial Unicode MS", "Sans" });

            if (s_Font == null)
            {
                s_Font = TryOsFont(new[] { "Arial" });
            }

            // Last-resort fallbacks (only reached if no OS font was available at all). These may log
            // a benign error on some versions, but the game will still have a usable font object.
            if (s_Font == null)
            {
                s_Font = TryBuiltin("LegacyRuntime.ttf") ?? TryBuiltin("Arial.ttf");
            }

            // Absolute guarantee of a non-null Font so no caller ever dereferences null.
            if (s_Font == null)
            {
                s_Font = new Font();
            }

            return s_Font;
        }

        private static Font TryOsFont(string[] names)
        {
            try
            {
                return Font.CreateDynamicFontFromOSFont(names, 16);
            }
            catch
            {
                return null;
            }
        }

        private static Font TryBuiltin(string name)
        {
            try
            {
                return Resources.GetBuiltinResource<Font>(name);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>Creates the root overlay canvas the whole HUD lives on.</summary>
        /// <param name="name">GameObject name.</param>
        /// <returns>The created <see cref="Canvas"/>.</returns>
        public static Canvas CreateCanvas(string name)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.GetComponent<CanvasScaler>();

            // Constant pixel size (scaleFactor 1) keeps anchoredPosition == screen pixels.
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;
            return canvas;
        }

        /// <summary>Creates a panel (a coloured, optionally-rounded background image).</summary>
        /// <param name="parent">Parent transform.</param>
        /// <param name="name">GameObject name.</param>
        /// <param name="size">Panel size in pixels.</param>
        /// <param name="color">Background colour.</param>
        /// <returns>The created <see cref="RectTransform"/>.</returns>
        public static RectTransform CreatePanel(Transform parent, string name, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            var img = go.GetComponent<Image>();
            img.color = color;
            return rt;
        }

        /// <summary>Creates a text label.</summary>
        /// <param name="parent">Parent transform.</param>
        /// <param name="name">GameObject name.</param>
        /// <param name="content">Initial text.</param>
        /// <param name="fontSize">Font size.</param>
        /// <param name="color">Text colour.</param>
        /// <param name="anchor">Text anchor.</param>
        /// <returns>The created <see cref="Text"/>.</returns>
        public static Text CreateText(Transform parent, string name, string content, int fontSize, Color color, TextAnchor anchor)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.font = GetFont();
            text.text = content;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = anchor;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        /// <summary>Creates a tappable button rendered as a coloured box with a centred label.</summary>
        /// <param name="parent">Parent transform.</param>
        /// <param name="name">GameObject name.</param>
        /// <param name="size">Button size in pixels.</param>
        /// <param name="bg">Background colour.</param>
        /// <param name="label">Button label.</param>
        /// <param name="fontSize">Label font size.</param>
        /// <returns>A <see cref="HudButton"/> bundling the rect, image and label.</returns>
        public static HudButton CreateButton(Transform parent, string name, Vector2 size, Color bg, string label, int fontSize)
        {
            var rt = CreatePanel(parent, name, size, bg);
            var text = CreateText(rt, name + "Label", label, fontSize, Color.white, TextAnchor.MiddleCenter);
            var trt = text.rectTransform;
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;
            return new HudButton { rect = rt, background = rt.GetComponent<Image>(), label = text };
        }
    }

    /// <summary>
    /// A simple HUD button bundle used with manual hit-testing (the game does not use Unity's
    /// EventSystem, so touch and mouse share one code path).
    /// </summary>
    public class HudButton
    {
        /// <summary>The button's rect transform.</summary>
        public RectTransform rect;

        /// <summary>The background image.</summary>
        public UnityEngine.UI.Image background;

        /// <summary>The label text.</summary>
        public UnityEngine.UI.Text label;
    }
}
