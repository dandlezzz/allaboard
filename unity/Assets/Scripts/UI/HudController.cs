// <copyright file="HudController.cs" company="board.fun game">
//     Trafalgar - Age of Sail. Procedural board.fun RTS.
// </copyright>

namespace Trafalgar.UI
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Trafalgar.Combat;
    using Trafalgar.Core;
    using Trafalgar.Ships;
    using UnityEngine;
    using UnityEngine.UI;

    /// <summary>
    /// Builds and drives the heads-up display: a central wind indicator, per-side fleet status,
    /// a context control panel that follows each side's selected ship (sail / ammo / course read-out),
    /// and the win banner.
    /// </summary>
    /// <remarks>
    /// The HUD does its own screen-space hit testing rather than relying on Unity's EventSystem, so
    /// that Board finger contacts and editor mouse clicks travel through one identical code path.
    /// Control panels live next to the selected ship (not in a screen corner) because players sit
    /// all the way around the table.
    /// </remarks>
    public class HudController : MonoBehaviour
    {
        private Camera m_Camera;
        private Func<Faction, Ship> m_GetSelected;

        private RectTransform m_WindArrow;
        private Text m_WindLabel;
        private Text m_Banner;
        private Text m_Hint;
        private HudButton m_TogglePlayer2;

        private readonly Dictionary<Faction, Text> m_FleetStatus = new Dictionary<Faction, Text>();
        private readonly Dictionary<Faction, ControlPanel> m_Panels = new Dictionary<Faction, ControlPanel>();
        private readonly List<ButtonBinding> m_Buttons = new List<ButtonBinding>();

        private struct ButtonBinding
        {
            public HudButton button;
            public Action action;
        }

        private class ControlPanel
        {
            public RectTransform root;
            public Text title;
            public Text info;
        }

        /// <summary>
        /// Builds the entire HUD hierarchy.
        /// </summary>
        /// <param name="camera">The world camera (for world→screen placement).</param>
        /// <param name="getSelected">Callback returning the selected ship for a faction (or null).</param>
        /// <param name="onToggleSecondPlayer">Callback invoked when the player toggles P2 control (AI/human).</param>
        public void Build(Camera camera, Func<Faction, Ship> getSelected, Action onToggleSecondPlayer)
        {
            m_Camera = camera;
            m_GetSelected = getSelected;

            Canvas canvas = UIFactory.CreateCanvas("HUD Canvas");
            canvas.transform.SetParent(transform, false);

            BuildWindIndicator(canvas.transform);
            BuildFleetStatus(canvas.transform, Faction.British, anchorLeft: true);
            BuildFleetStatus(canvas.transform, Faction.FrancoSpanish, anchorLeft: false);

            // Control panels are built for both sides; an AI side simply never has a selection,
            // so its panel stays hidden until a human takes the helm.
            BuildControlPanel(canvas.transform, Faction.British);
            BuildControlPanel(canvas.transform, Faction.FrancoSpanish);

            BuildPlayer2Toggle(canvas.transform, onToggleSecondPlayer);

            m_Banner = UIFactory.CreateText(canvas.transform, "Banner", string.Empty, 56, Color.white, TextAnchor.MiddleCenter);
            var brt = m_Banner.rectTransform;
            brt.anchorMin = new Vector2(0.5f, 0.5f);
            brt.anchorMax = new Vector2(0.5f, 0.5f);
            brt.anchoredPosition = Vector2.zero;
            brt.sizeDelta = new Vector2(1200f, 200f);

            m_Hint = UIFactory.CreateText(canvas.transform, "Hint",
                "Tap a ship to select  •  use the ring buttons: ◄ ► turn, + − sail, ● shot  •  tap the ring to steer to a bearing  •  tap open water to deselect",
                18, new Color(1f, 1f, 1f, 0.7f), TextAnchor.LowerCenter);
            var hrt = m_Hint.rectTransform;
            hrt.anchorMin = new Vector2(0.5f, 0f);
            hrt.anchorMax = new Vector2(0.5f, 0f);
            hrt.pivot = new Vector2(0.5f, 0f);
            hrt.anchoredPosition = new Vector2(0f, 16f);
            hrt.sizeDelta = new Vector2(1200f, 40f);
        }

        private void BuildWindIndicator(Transform parent)
        {
            var container = UIFactory.CreatePanel(parent, "WindIndicator", new Vector2(150f, 150f), new Color(0.05f, 0.08f, 0.12f, 0.6f));
            container.anchorMin = new Vector2(0.5f, 1f);
            container.anchorMax = new Vector2(0.5f, 1f);
            container.pivot = new Vector2(0.5f, 1f);
            container.anchoredPosition = new Vector2(0f, -16f);

            // Arrow pivot rotates to show which way the wind blows (downwind). Built as a shaft with
            // a real triangular head so the direction reads at a glance on the top-down view.
            Color windColor = new Color(0.6f, 0.85f, 1f);
            m_WindArrow = UIFactory.CreatePanel(container, "Arrow", new Vector2(0f, 0f), new Color(0, 0, 0, 0));
            m_WindArrow.anchorMin = new Vector2(0.5f, 0.5f);
            m_WindArrow.anchorMax = new Vector2(0.5f, 0.5f);
            m_WindArrow.anchoredPosition = new Vector2(0f, 6f);

            var shaft = UIFactory.CreatePanel(m_WindArrow, "Shaft", new Vector2(11f, 48f), windColor);
            shaft.anchoredPosition = new Vector2(0f, -6f);

            var head = UIFactory.CreatePanel(m_WindArrow, "Head", new Vector2(38f, 30f), windColor);
            head.anchoredPosition = new Vector2(0f, 30f);
            Image headImg = head.GetComponent<Image>();
            headImg.sprite = UIFactory.TriangleSprite();
            headImg.type = Image.Type.Simple;
            headImg.preserveAspect = false;

            m_WindLabel = UIFactory.CreateText(container, "WindLabel", "WIND", 16, Color.white, TextAnchor.LowerCenter);
            var lrt = m_WindLabel.rectTransform;
            lrt.anchorMin = new Vector2(0.5f, 0f);
            lrt.anchorMax = new Vector2(0.5f, 0f);
            lrt.pivot = new Vector2(0.5f, 0f);
            lrt.anchoredPosition = new Vector2(0f, 6f);
            lrt.sizeDelta = new Vector2(150f, 40f);
        }

        private void BuildFleetStatus(Transform parent, Faction faction, bool anchorLeft)
        {
            var panel = UIFactory.CreatePanel(parent, faction + "Status", new Vector2(220f, 70f), new Color(0.05f, 0.08f, 0.12f, 0.55f));
            panel.anchorMin = new Vector2(anchorLeft ? 0f : 1f, 0.5f);
            panel.anchorMax = panel.anchorMin;
            panel.pivot = new Vector2(anchorLeft ? 0f : 1f, 0.5f);
            panel.anchoredPosition = new Vector2(anchorLeft ? 16f : -16f, 0f);

            var text = UIFactory.CreateText(panel, "Text", faction.DisplayName(), 16, faction.AccentColor(),
                anchorLeft ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight);
            var rt = text.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(12f, 6f);
            rt.offsetMax = new Vector2(-12f, -6f);
            m_FleetStatus[faction] = text;
        }

        private void BuildPlayer2Toggle(Transform parent, Action onToggle)
        {
            m_TogglePlayer2 = UIFactory.CreateButton(parent, "P2Toggle", new Vector2(180f, 40f),
                new Color(0.1f, 0.13f, 0.2f, 0.85f), "Franco-Spanish: AI", 14);
            m_TogglePlayer2.rect.anchorMin = new Vector2(0f, 1f);
            m_TogglePlayer2.rect.anchorMax = new Vector2(0f, 1f);
            m_TogglePlayer2.rect.pivot = new Vector2(0f, 1f);
            m_TogglePlayer2.rect.anchoredPosition = new Vector2(16f, -16f);

            m_Buttons.Add(new ButtonBinding
            {
                button = m_TogglePlayer2,
                action = () => onToggle?.Invoke(),
            });
        }

        /// <summary>Updates the P2 toggle label to reflect the current control mode.</summary>
        /// <param name="secondPlayerIsHuman">Whether the Franco-Spanish fleet is human-controlled.</param>
        public void SetSecondPlayerMode(bool secondPlayerIsHuman)
        {
            if (m_TogglePlayer2 != null)
            {
                m_TogglePlayer2.label.text = secondPlayerIsHuman ? "Franco-Spanish: Human" : "Franco-Spanish: AI";
            }
        }

        private void BuildControlPanel(Transform parent, Faction faction)
        {
            // Read-only status readout below the selected ship. (Controls moved to the on-ring
            // buttons, so this no longer holds any tappable controls — it just reports state.)
            var panel = new ControlPanel();
            panel.root = UIFactory.CreatePanel(parent, faction + "Status", new Vector2(240f, 96f), new Color(0.04f, 0.06f, 0.1f, 0.82f));
            panel.root.pivot = new Vector2(0.5f, 0f);

            panel.title = UIFactory.CreateText(panel.root, "Title", "-", 16, faction.AccentColor(), TextAnchor.UpperCenter);
            var trt = panel.title.rectTransform;
            trt.anchorMin = new Vector2(0f, 1f);
            trt.anchorMax = new Vector2(1f, 1f);
            trt.pivot = new Vector2(0.5f, 1f);
            trt.sizeDelta = new Vector2(0f, 24f);
            trt.anchoredPosition = new Vector2(0f, -6f);

            panel.info = UIFactory.CreateText(panel.root, "Info", "-", 13, new Color(0.85f, 0.9f, 1f), TextAnchor.UpperCenter);
            var irt = panel.info.rectTransform;
            irt.anchorMin = new Vector2(0f, 0f);
            irt.anchorMax = new Vector2(1f, 1f);
            irt.offsetMin = new Vector2(8f, 8f);
            irt.offsetMax = new Vector2(-8f, -30f);

            panel.root.gameObject.SetActive(false);
            m_Panels[faction] = panel;
        }

        /// <summary>
        /// Tests a tap against the active HUD buttons, invoking the first one hit.
        /// </summary>
        /// <param name="screenPosition">Tap position in screen pixels (bottom-left origin).</param>
        /// <returns><c>true</c> if a button consumed the tap (world input should be ignored).</returns>
        public bool HandleTap(Vector2 screenPosition)
        {
            for (int i = 0; i < m_Buttons.Count; i++)
            {
                ButtonBinding b = m_Buttons[i];
                if (b.button.rect == null || !b.button.rect.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (RectTransformUtility.RectangleContainsScreenPoint(b.button.rect, screenPosition, null))
                {
                    b.action?.Invoke();
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Refreshes all dynamic HUD elements.
        /// </summary>
        /// <param name="wind">The global wind.</param>
        /// <param name="ships">All ships in play.</param>
        /// <param name="gameOver">Whether the game has ended.</param>
        /// <param name="winner">The winning faction (if any).</param>
        public void Refresh(Wind wind, IReadOnlyList<Ship> ships, bool gameOver, Faction winner)
        {
            UpdateWind(wind);
            UpdateFleetStatus(Faction.British, ships);
            UpdateFleetStatus(Faction.FrancoSpanish, ships);

            foreach (var kvp in m_Panels)
            {
                UpdatePanel(kvp.Key, kvp.Value, wind);
            }

            if (gameOver)
            {
                m_Banner.text = winner == Faction.Neutral
                    ? "STALEMATE"
                    : winner.DisplayName() + " Fleet Victorious!";
                m_Banner.color = winner.AccentColor();
            }
            else
            {
                m_Banner.text = string.Empty;
            }
        }

        private void UpdateWind(Wind wind)
        {
            // The arrow points the way the wind blows (downwind). Screen-up == world +Z, and Unity
            // UI rotation is counter-clockwise, hence the negation of the compass angle.
            float downwind = Nav.Normalize360(wind.FromDegrees + 180f);
            m_WindArrow.localEulerAngles = new Vector3(0f, 0f, -downwind);
            m_WindLabel.text = $"WIND\nfrom {Mathf.RoundToInt(wind.FromDegrees)}°";
        }

        private void UpdateFleetStatus(Faction faction, IReadOnlyList<Ship> ships)
        {
            if (!m_FleetStatus.TryGetValue(faction, out Text text))
            {
                return;
            }

            int afloat = 0;
            float totalHull = 0f;
            for (int i = 0; i < ships.Count; i++)
            {
                if (ships[i].IsAlive && ships[i].Faction == faction)
                {
                    afloat++;
                    totalHull += ships[i].HullFraction;
                }
            }

            float avgHull = afloat > 0 ? (totalHull / afloat) * 100f : 0f;
            var sb = new StringBuilder();
            sb.AppendLine($"<b>{faction.DisplayName()}</b>");
            sb.Append($"Ships: {afloat}   Avg hull: {Mathf.RoundToInt(avgHull)}%");
            text.text = sb.ToString();
        }

        private void UpdatePanel(Faction faction, ControlPanel panel, Wind wind)
        {
            Ship ship = m_GetSelected != null ? m_GetSelected(faction) : null;
            if (ship == null || !ship.IsAlive || ship.Faction != faction)
            {
                if (panel.root.gameObject.activeSelf)
                {
                    panel.root.gameObject.SetActive(false);
                }

                return;
            }

            panel.root.gameObject.SetActive(true);

            // Place the panel directly *below* the hull in screen space so a finger on the panel
            // doesn't obscure the ship, tracking it as it moves. WorldToScreenPoint gives bottom-left
            // origin pixels, matching the overlay canvas (scaleFactor 1).
            Vector3 screen = m_Camera.WorldToScreenPoint(ship.Position);
            if (screen.z < 0f)
            {
                panel.root.gameObject.SetActive(false);
                return;
            }

            // Offset below the hull by the ship's on-screen half-length (orthographic px-per-unit),
            // then clamp the whole panel to the screen so it stays visible near the edges. The panel
            // pivot is bottom-centre, so it occupies [y, y + height].
            float pxPerUnit = Screen.height / (2f * Mathf.Max(0.01f, m_Camera.orthographicSize));
            float shipHalfPx = ship.Stats.length * 0.5f * pxPerUnit;
            Vector2 size = panel.root.sizeDelta;
            float halfW = size.x * 0.5f;

            float x = Mathf.Clamp(screen.x, halfW + 8f, Mathf.Max(halfW + 8f, Screen.width - halfW - 8f));
            float y = screen.y - shipHalfPx - 14f - size.y;
            y = Mathf.Clamp(y, 8f, Mathf.Max(8f, Screen.height - size.y - 8f));
            panel.root.position = new Vector3(x, y, 0f);

            panel.title.text = $"{ship.Stats.displayName}";
            panel.info.text =
                $"Course {Mathf.RoundToInt(ship.HeadingDeg)}° → {Mathf.RoundToInt(ship.TargetHeadingDeg)}°\n" +
                $"{ship.PointOfSail}   {Mathf.RoundToInt(ship.Speed * 10f) / 10f} kn\n" +
                $"Sail: {ship.Sail.Label()}   Shot: {Ammo.Label(ship.Ammo)}";
        }
    }
}
