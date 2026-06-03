// <copyright file="GameManager.cs" company="board.fun game">
//     Trafalgar - Age of Sail. Procedural board.fun RTS.
// </copyright>

namespace Trafalgar.Core
{
    using System.Collections.Generic;
    using Board.Core;
    using Board.Session;
    using Trafalgar.AI;
    using Trafalgar.Combat;
    using Trafalgar.InputLayer;
    using Trafalgar.Rendering;
    using Trafalgar.Ships;
    using Trafalgar.UI;
    using UnityEngine;

    /// <summary>
    /// The central orchestrator. Builds the scene and fleets, owns the simulation systems
    /// (wind, gunnery, boarding, AI), routes touch / mouse input into selection and orders,
    /// detects the win condition, and drives the HUD. Created at runtime by
    /// <see cref="GameBootstrap"/>, so the project needs no authored scene.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        private const int kNoPointer = int.MinValue;

        // Steering by on-ring buttons: each tap of a turn button nudges the ordered heading by this
        // many degrees; holding the button keeps turning at the hold rate (deg/second).
        private const float kTurnStepDeg = 12f;
        private const float kTurnHoldRateDeg = 50f;

        // Radius (as a multiple of hull length) of the "command dial" around a selected ship: a tap
        // inside it that isn't a button or another ship sets the course to that bearing; beyond it
        // is open water and deselects.
        private const float kDialZoneFactor = 1.05f;

        private Camera m_Camera;
        private Wind m_Wind;
        private InputRouter m_Input;
        private CombatSystem m_Combat;
        private BoardingSystem m_Boarding;
        private HudController m_Hud;
        private CourseIndicator m_Course;
        private Transform m_FleetRoot;

        private readonly List<Ship> m_Ships = new List<Ship>();
        private readonly Dictionary<Faction, ControlMode> m_Control = new Dictionary<Faction, ControlMode>();
        private readonly Dictionary<Faction, FleetAI> m_AI = new Dictionary<Faction, FleetAI>();
        private readonly Dictionary<Faction, Ship> m_Selected = new Dictionary<Faction, Ship>();

        private Faction m_ActiveFaction = Faction.British;

        // A held turn button (Port/Starboard) keeps turning the selected ship while pressed.
        private ShipControl m_HeldControl = ShipControl.None;
        private int m_HeldPointer = kNoPointer;

        private bool m_GameOver;
        private Faction m_Winner = Faction.Neutral;
        private float m_GameOverTimer;

        private void Start()
        {
            m_Camera = SceneBuilder.BuildCamera(transform);
            SceneBuilder.BuildLighting(transform);
            SceneBuilder.BuildSea(transform);

            var courseGo = new GameObject("CourseIndicator");
            courseGo.transform.SetParent(transform, false);
            m_Course = courseGo.AddComponent<CourseIndicator>();
            m_Course.Build();

            m_Wind = new Wind(Random.Range(0f, 360f));
            m_Input = new InputRouter();
            m_Combat = new CombatSystem();
            m_Boarding = new BoardingSystem();
            m_Boarding.ShipCaptured += OnShipCaptured;

            // Player 1 (British) is human; Player 2 (Franco-Spanish) defaults to AI so the game
            // is immediately playable solo. The HUD toggle flips P2 to a second human (hot-seat /
            // table multiplayer).
            m_Control[Faction.British] = ControlMode.Human;
            m_Control[Faction.FrancoSpanish] = ControlMode.AI;
            m_AI[Faction.FrancoSpanish] = new FleetAI(Faction.FrancoSpanish);

            // Build the playable scene BEFORE the HUD so a UI/font hiccup can never blank it.
            ConfigureBoardPlatform();
            SpawnAllFleets();

            // The HUD is built last and defensively: if anything in HUD construction throws, the
            // game keeps running without a HUD (m_Hud stays null and is null-guarded everywhere)
            // rather than aborting Start() and leaving the scene uninitialised.
            try
            {
                var hudGo = new GameObject("HUD");
                hudGo.transform.SetParent(transform, false);
                var hud = hudGo.AddComponent<HudController>();
                hud.Build(m_Camera, GetSelectedForHud, ToggleSecondPlayer);
                hud.SetSecondPlayerMode(false);
                m_Hud = hud; // only published once fully built
            }
            catch (System.Exception e)
            {
                m_Hud = null;
                Debug.LogWarning("[Trafalgar] HUD build failed; continuing without HUD. " + e);
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            m_Input.Poll();

            if (!m_GameOver)
            {
                m_Wind.Tick(dt);
                HandleInput();
                TickAI();
                TickShips(dt);
                m_Combat.Tick(m_Ships);
                m_Boarding.Tick(m_Ships, dt);
                CullSunkShips();
                CheckWinCondition();
            }
            else
            {
                HandleGameOverInput(dt);
            }

            RefreshSelectionVisuals();
            UpdateCourseVisuals();
            if (m_Hud != null)
            {
                m_Hud.Refresh(m_Wind, m_Ships, m_GameOver, m_Winner);
            }
        }

        // ---- Setup -------------------------------------------------------------------------

        private void ConfigureBoardPlatform()
        {
            // These calls are safe no-ops in the editor / on non-Board hardware.
            try
            {
                BoardSession.SetAIPlayerTypes(new[]
                {
                    new BoardAIPlayerType { name = "Commodore", description = "A balanced AI admiral." },
                });
                BoardApplication.SetPauseScreenContext("Trafalgar - Age of Sail", false, null, null);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Trafalgar] Board platform configuration skipped: " + e.Message);
            }
        }

        private void SpawnAllFleets()
        {
            if (m_FleetRoot != null)
            {
                Destroy(m_FleetRoot.gameObject);
            }

            m_FleetRoot = new GameObject("Fleets").transform;
            m_FleetRoot.SetParent(transform, false);
            m_Ships.Clear();
            m_Selected.Clear();

            // British behind the left short edge steering east (90° = +X); Franco-Spanish behind the
            // right short edge steering west (270° = -X). They close across the long X span.
            SpawnFleet(Faction.British, -1f, 90f);
            SpawnFleet(Faction.FrancoSpanish, 1f, 270f);
        }

        private void SpawnFleet(Faction faction, float side, float heading)
        {
            ShipClass[] line = { ShipClass.FirstRate, ShipClass.ThirdRate, ShipClass.ThirdRate, ShipClass.Frigate };

            // Each fleet lines up behind an opposite SHORT (left/right) edge — near ±ArenaHalfX —
            // and sails toward the other across the long X span. The line itself runs across the
            // short Z axis, spread between the top/bottom edges with a margin. Ships face inward
            // (±X) so the bow length stays clear of the edge and the spawn never triggers the
            // edge turn-around. Hulls are oriented along X here, so their beam runs along Z.
            float xMargin = 10f * GameConfig.ShipScale;        // > half the longest hull length
            float zMargin = 7f * GameConfig.ShipScale;
            float frontX = side * (GameConfig.ArenaHalfX - xMargin);
            float usableHalfZ = GameConfig.ArenaHalfZ - zMargin;

            // Keep ships from overlapping (beam runs along Z). If a single rank would pack tighter
            // than this, fall back to a couple of ranks staggered back toward the fleet's own edge
            // rather than overflowing the short edges.
            float minSpacing = 2.4f * ShipCatalog.Stats(ShipClass.FirstRate).beam;
            int maxPerRank = Mathf.Max(1, Mathf.FloorToInt((2f * usableHalfZ) / minSpacing) + 1);
            int ranks = Mathf.Max(1, Mathf.CeilToInt(line.Length / (float)maxPerRank));
            int perRank = Mathf.CeilToInt(line.Length / (float)ranks);
            float rankGap = 4f * GameConfig.ShipScale;

            for (int i = 0; i < line.Length; i++)
            {
                int rank = i / perRank;
                int indexInRank = i % perRank;
                int countInRank = Mathf.Min(perRank, line.Length - (rank * perRank));

                float z = countInRank > 1
                    ? Mathf.Lerp(-usableHalfZ, usableHalfZ, indexInRank / (float)(countInRank - 1))
                    : 0f;

                // Trailing ranks sit a little further back toward the fleet's own edge.
                float x = frontX + (side * rank * rankGap);

                var pos = new Vector3(x, 0f, z);
                Ship ship = ShipFactory.Create(line[i], faction, pos, heading, m_FleetRoot);
                m_Ships.Add(ship);
            }
        }

        // ---- Input -------------------------------------------------------------------------

        private void HandleInput()
        {
            IReadOnlyList<PointerSample> pointers = m_Input.Pointers;
            for (int i = 0; i < pointers.Count; i++)
            {
                PointerSample p = pointers[i];
                switch (p.phase)
                {
                    case PointerPhase.Began:
                        HandlePointerBegan(p);
                        break;
                    case PointerPhase.Moved:
                    case PointerPhase.Stationary:
                        HandlePointerHeld(p);
                        break;
                    case PointerPhase.Ended:
                        HandlePointerEnded(p);
                        break;
                }
            }

            HandleGlyphs();
        }

        private void HandlePointerBegan(PointerSample p)
        {
            // Screen-space HUD buttons first (e.g. the P2 toggle) so they never issue a world order.
            if (m_Hud != null && m_Hud.HandleTap(p.screenPosition))
            {
                return;
            }

            if (!ScreenToSea(p.screenPosition, out Vector3 world))
            {
                return;
            }

            Ship sel = SelectedOf(m_ActiveFaction);

            // 1) On-ring control button of the selected ship — checked BEFORE select/deselect so a
            //    button tap performs its action and never deselects.
            if (sel != null && sel.IsAlive)
            {
                ShipView view = sel.GetComponent<ShipView>();
                if (view != null && view.TryHitControl(world, out ShipControl ctrl))
                {
                    ApplyControl(sel, ctrl);
                    view.FlashControl(ctrl);
                    if (ctrl == ShipControl.Port || ctrl == ShipControl.Starboard)
                    {
                        m_HeldControl = ctrl; // keep turning while held
                        m_HeldPointer = p.id;
                    }

                    return;
                }
            }

            // 2) Tapping a DIFFERENT friendly ship selects / switches to it.
            Ship hit = FindShipAt(world);
            if (hit != null && IsHuman(hit.Faction) && hit != sel)
            {
                m_Selected[hit.Faction] = hit;
                m_ActiveFaction = hit.Faction;
                return;
            }

            // 3) With a ship selected: a tap on its surrounding ring/dial sets the course toward that
            //    bearing; a tap beyond the dial (open water) clears the selection.
            if (sel != null && sel.IsAlive)
            {
                if (Vector3.Distance(world, sel.Position) <= sel.Stats.length * kDialZoneFactor)
                {
                    sel.SetCourseToPoint(world);
                }
                else
                {
                    m_Selected.Remove(m_ActiveFaction);
                }
            }
        }

        private void HandlePointerHeld(PointerSample p)
        {
            // Continuous turn while a Port/Starboard button is held down.
            if (m_HeldControl == ShipControl.None || p.id != m_HeldPointer)
            {
                return;
            }

            Ship sel = SelectedOf(m_ActiveFaction);
            if (sel == null || !sel.IsAlive)
            {
                m_HeldControl = ShipControl.None;
                m_HeldPointer = kNoPointer;
                return;
            }

            float dir = m_HeldControl == ShipControl.Starboard ? 1f : -1f;
            sel.SetTargetHeading(Nav.Normalize360(sel.TargetHeadingDeg + (dir * kTurnHoldRateDeg * Time.deltaTime)));
        }

        private void HandlePointerEnded(PointerSample p)
        {
            if (p.id == m_HeldPointer)
            {
                m_HeldControl = ShipControl.None;
                m_HeldPointer = kNoPointer;
            }
        }

        private void ApplyControl(Ship ship, ShipControl control)
        {
            switch (control)
            {
                case ShipControl.Port:
                    ship.SetTargetHeading(Nav.Normalize360(ship.TargetHeadingDeg - kTurnStepDeg));
                    break;
                case ShipControl.Starboard:
                    ship.SetTargetHeading(Nav.Normalize360(ship.TargetHeadingDeg + kTurnStepDeg));
                    break;
                case ShipControl.SailUp:
                    ship.SetSail((SailSetting)Mathf.Clamp((int)ship.Sail + 1, 0, 2));
                    break;
                case ShipControl.SailDown:
                    ship.SetSail((SailSetting)Mathf.Clamp((int)ship.Sail - 1, 0, 2));
                    break;
                case ShipControl.AmmoCycle:
                    ship.CycleAmmo();
                    break;
            }
        }

        private void HandleGlyphs()
        {
            // Optional hardware feature: a physical piece on the table selects and orients the
            // nearest friendly ship by its rotation. Glyphs are always empty in the editor.
            IReadOnlyList<PointerSample> glyphs = m_Input.Glyphs;
            for (int i = 0; i < glyphs.Count; i++)
            {
                PointerSample g = glyphs[i];
                if (!ScreenToSea(g.screenPosition, out Vector3 world))
                {
                    continue;
                }

                Ship hit = FindShipAt(world);
                if (hit == null || !IsHuman(hit.Faction))
                {
                    continue;
                }

                m_Selected[hit.Faction] = hit;
                m_ActiveFaction = hit.Faction;

                // Orientation is radians CCW from vertical; vertical maps to world +Z (heading 0),
                // and compass headings run clockwise, hence the negation.
                float headingDeg = Nav.Normalize360(-g.orientation * Mathf.Rad2Deg);
                hit.SetTargetHeading(headingDeg);
            }
        }

        private void HandleGameOverInput(float dt)
        {
            m_GameOverTimer += dt;
            if (m_GameOverTimer < 2f)
            {
                return;
            }

            IReadOnlyList<PointerSample> pointers = m_Input.Pointers;
            for (int i = 0; i < pointers.Count; i++)
            {
                if (pointers[i].IsBegan)
                {
                    Restart();
                    return;
                }
            }
        }

        // ---- Simulation --------------------------------------------------------------------

        private void TickAI()
        {
            foreach (var kvp in m_AI)
            {
                if (m_Control[kvp.Key] == ControlMode.AI)
                {
                    kvp.Value.Tick(m_Ships, m_Wind);
                }
            }
        }

        private void TickShips(float dt)
        {
            for (int i = 0; i < m_Ships.Count; i++)
            {
                m_Ships[i].Tick(dt, m_Wind);
            }
        }

        private void CullSunkShips()
        {
            for (int i = m_Ships.Count - 1; i >= 0; i--)
            {
                Ship ship = m_Ships[i];
                if (ship.State == ShipState.Gone)
                {
                    ClearSelectionOf(ship);
                    m_Ships.RemoveAt(i);
                    Destroy(ship.gameObject);
                }
            }
        }

        private void CheckWinCondition()
        {
            bool britishAfloat = HasLivingShips(Faction.British);
            bool francoAfloat = HasLivingShips(Faction.FrancoSpanish);

            if (britishAfloat && francoAfloat)
            {
                return;
            }

            m_GameOver = true;
            m_GameOverTimer = 0f;
            if (britishAfloat)
            {
                m_Winner = Faction.British;
            }
            else if (francoAfloat)
            {
                m_Winner = Faction.FrancoSpanish;
            }
            else
            {
                m_Winner = Faction.Neutral;
            }
        }

        private void Restart()
        {
            m_GameOver = false;
            m_Winner = Faction.Neutral;
            m_GameOverTimer = 0f;
            m_HeldControl = ShipControl.None;
            m_HeldPointer = kNoPointer;
            m_Wind = new Wind(Random.Range(0f, 360f));
            SpawnAllFleets();
        }

        // ---- Selection / queries -----------------------------------------------------------

        private void OnShipCaptured(Ship ship, Faction newOwner)
        {
            // If a captured ship was selected by its former (now enemy) side, drop that selection.
            foreach (var faction in new[] { Faction.British, Faction.FrancoSpanish })
            {
                if (m_Selected.TryGetValue(faction, out Ship sel) && sel == ship && faction != newOwner)
                {
                    m_Selected.Remove(faction);
                }
            }
        }

        private void RefreshSelectionVisuals()
        {
            for (int i = 0; i < m_Ships.Count; i++)
            {
                Ship ship = m_Ships[i];
                Faction selector = Faction.Neutral;
                bool selected = false;

                if (m_Selected.TryGetValue(Faction.British, out Ship b) && b == ship)
                {
                    selected = true;
                    selector = Faction.British;
                }
                else if (m_Selected.TryGetValue(Faction.FrancoSpanish, out Ship f) && f == ship)
                {
                    selected = true;
                    selector = Faction.FrancoSpanish;
                }

                ShipView view = ship.GetComponent<ShipView>();
                if (view != null)
                {
                    view.SetSelected(selected && ship.IsAlive, selector);
                }
            }
        }

        private void UpdateCourseVisuals()
        {
            if (m_Course == null)
            {
                return;
            }

            // Display-only: a line from the selected ship along its ordered heading (the on-ring
            // gold needle shows the same heading; steering is now via the ring control buttons).
            Ship sel = SelectedOf(m_ActiveFaction);
            if (m_GameOver || sel == null)
            {
                m_Course.HideAll();
                return;
            }

            m_Course.ShowHeading(sel.Position, sel.TargetHeadingDeg, sel.Stats.length * 2.5f, m_ActiveFaction.AccentColor());
        }

        private void ToggleSecondPlayer()
        {
            bool nowHuman = m_Control[Faction.FrancoSpanish] != ControlMode.Human;
            m_Control[Faction.FrancoSpanish] = nowHuman ? ControlMode.Human : ControlMode.AI;
            if (!nowHuman)
            {
                m_Selected.Remove(Faction.FrancoSpanish);
            }

            if (m_Hud != null)
            {
                m_Hud.SetSecondPlayerMode(nowHuman);
            }
        }

        private Ship GetSelectedForHud(Faction faction)
        {
            return IsHuman(faction) ? SelectedOf(faction) : null;
        }

        private Ship SelectedOf(Faction faction)
        {
            if (m_Selected.TryGetValue(faction, out Ship ship) && ship != null && ship.IsAlive && ship.Faction == faction)
            {
                return ship;
            }

            return null;
        }

        private void ClearSelectionOf(Ship ship)
        {
            if (m_Selected.TryGetValue(Faction.British, out Ship b) && b == ship)
            {
                m_Selected.Remove(Faction.British);
            }

            if (m_Selected.TryGetValue(Faction.FrancoSpanish, out Ship f) && f == ship)
            {
                m_Selected.Remove(Faction.FrancoSpanish);
            }
        }

        private bool IsHuman(Faction faction)
        {
            return m_Control.TryGetValue(faction, out ControlMode mode) && mode == ControlMode.Human;
        }

        private bool HasLivingShips(Faction faction)
        {
            for (int i = 0; i < m_Ships.Count; i++)
            {
                if (m_Ships[i].IsAlive && m_Ships[i].Faction == faction)
                {
                    return true;
                }
            }

            return false;
        }

        private Ship FindShipAt(Vector3 world)
        {
            Ship best = null;
            float bestDist = float.MaxValue;
            for (int i = 0; i < m_Ships.Count; i++)
            {
                Ship ship = m_Ships[i];
                if (!ship.IsAlive)
                {
                    continue;
                }

                float radius = Mathf.Max(GameConfig.ShipSelectRadius, ship.Stats.length * 0.6f);
                float dist = Vector3.Distance(world, ship.Position);
                if (dist <= radius && dist < bestDist)
                {
                    bestDist = dist;
                    best = ship;
                }
            }

            return best;
        }

        private bool ScreenToSea(Vector2 screenPosition, out Vector3 world)
        {
            Ray ray = m_Camera.ScreenPointToRay(new Vector3(screenPosition.x, screenPosition.y, 0f));
            var sea = new Plane(Vector3.up, Vector3.zero);
            if (sea.Raycast(ray, out float enter))
            {
                world = ray.GetPoint(enter);
                return true;
            }

            world = Vector3.zero;
            return false;
        }
    }
}
