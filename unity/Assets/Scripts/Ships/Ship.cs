// <copyright file="Ship.cs" company="board.fun game">
//     Trafalgar - Age of Sail. Procedural board.fun RTS.
// </copyright>

namespace Trafalgar.Ships
{
    using Trafalgar.Combat;
    using Trafalgar.Core;
    using UnityEngine;

    /// <summary>
    /// Which side of the ship a broadside is fired from.
    /// </summary>
    public enum BroadsideSide
    {
        /// <summary>The left-hand side when facing the bow.</summary>
        Port = 0,

        /// <summary>The right-hand side when facing the bow.</summary>
        Starboard = 1,
    }

    /// <summary>
    /// The high-level lifecycle state of a ship.
    /// </summary>
    public enum ShipState
    {
        /// <summary>Afloat and under command.</summary>
        Sailing = 0,

        /// <summary>Hull breached; visibly going down and about to be removed.</summary>
        Sinking = 1,

        /// <summary>Removed from play.</summary>
        Gone = 2,
    }

    /// <summary>
    /// A single sailing warship: the heart of the simulation. Owns its movement (course, speed,
    /// point of sail), damage state (hull / rigging / crew), gun reloads, and capture handling.
    /// Rendering is delegated to a sibling <see cref="ShipView"/>.
    /// </summary>
    public class Ship : MonoBehaviour
    {
        private ShipView m_View;
        private float m_PortReload;
        private float m_StarboardReload;
        private float m_BowReload;
        private float m_SternReload;
        private float m_SinkTimer;
        private bool m_EdgeReversing;

        /// <summary>Gets the static stats for this ship's class.</summary>
        public ShipStats Stats { get; private set; }

        /// <summary>Gets the ship's class.</summary>
        public ShipClass ShipClass => Stats.shipClass;

        /// <summary>Gets the faction currently commanding the ship (changes on capture).</summary>
        public Faction Faction { get; private set; }

        /// <summary>Gets the current hull integrity.</summary>
        public float Hull { get; private set; }

        /// <summary>Gets the current rigging integrity (affects speed and turn rate).</summary>
        public float Rigging { get; private set; }

        /// <summary>Gets the current crew complement (gates boarding/capture).</summary>
        public float Crew { get; private set; }

        /// <summary>Gets the lifecycle state.</summary>
        public ShipState State { get; private set; } = ShipState.Sailing;

        /// <summary>Gets the current compass heading in degrees.</summary>
        public float HeadingDeg { get; private set; }

        /// <summary>Gets the ordered course (target heading) in degrees.</summary>
        public float TargetHeadingDeg { get; private set; }

        /// <summary>Gets the current sail plan.</summary>
        public SailSetting Sail { get; private set; } = SailSetting.BattleSail;

        /// <summary>Gets the currently loaded ammunition type.</summary>
        public AmmoType Ammo { get; private set; } = AmmoType.RoundShot;

        /// <summary>Gets the current forward speed in world units / second.</summary>
        public float Speed { get; private set; }

        /// <summary>Gets a friendly description of the current point of sail (e.g. "Close-Hauled").</summary>
        public string PointOfSail { get; private set; } = "-";

        /// <summary>Gets a value indicating whether the ship is afloat and controllable.</summary>
        public bool IsAlive => State == ShipState.Sailing;

        /// <summary>Gets the hull integrity as a fraction in [0, 1].</summary>
        public float HullFraction => Mathf.Clamp01(Hull / Stats.maxHull);

        /// <summary>Gets the rigging integrity as a fraction in [0, 1].</summary>
        public float RiggingFraction => Mathf.Clamp01(Rigging / Stats.maxRigging);

        /// <summary>Gets the crew complement as a fraction in [0, 1].</summary>
        public float CrewFraction => Mathf.Clamp01(Crew / Stats.maxCrew);

        /// <summary>Gets a value indicating whether the ship is weak enough to be boarded and captured.</summary>
        public bool IsBoardable =>
            IsAlive && (HullFraction <= GameConfig.BoardingHullThreshold ||
                        CrewFraction <= GameConfig.BoardingCrewThreshold);

        /// <summary>Gets the world position of the ship on the sea plane.</summary>
        public Vector3 Position => transform.position;

        /// <summary>
        /// Configures a freshly-spawned ship. Must be called once before the ship ticks.
        /// </summary>
        /// <param name="stats">The class stats.</param>
        /// <param name="faction">The owning faction.</param>
        /// <param name="position">Initial world position (Y will be flattened to the sea plane).</param>
        /// <param name="headingDeg">Initial compass heading.</param>
        public void Initialize(ShipStats stats, Faction faction, Vector3 position, float headingDeg)
        {
            Stats = stats;
            Faction = faction;
            Hull = stats.maxHull;
            Rigging = stats.maxRigging;
            Crew = stats.maxCrew;
            HeadingDeg = Nav.Normalize360(headingDeg);
            TargetHeadingDeg = HeadingDeg;

            transform.position = new Vector3(position.x, 0f, position.z);
            transform.rotation = Quaternion.Euler(0f, HeadingDeg, 0f);

            m_View = GetComponent<ShipView>();
            if (m_View == null)
            {
                m_View = gameObject.AddComponent<ShipView>();
            }

            m_View.Build(this);
        }

        /// <summary>Orders the ship to steer toward a world destination.</summary>
        /// <param name="worldPoint">A point on the sea plane.</param>
        public void SetCourseToPoint(Vector3 worldPoint)
        {
            Vector3 dir = worldPoint - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.001f)
            {
                TargetHeadingDeg = Nav.VectorToHeading(dir);
            }
        }

        /// <summary>Sets the ordered course directly.</summary>
        /// <param name="headingDeg">The desired compass heading.</param>
        public void SetTargetHeading(float headingDeg)
        {
            TargetHeadingDeg = Nav.Normalize360(headingDeg);
        }

        /// <summary>Cycles to the next sail setting (furled → battle → full → furled).</summary>
        public void CycleSail()
        {
            Sail = Sail.Next();
        }

        /// <summary>Sets the sail plan directly.</summary>
        /// <param name="setting">The desired sail setting.</param>
        public void SetSail(SailSetting setting)
        {
            Sail = setting;
        }

        /// <summary>Cycles to the next ammunition type (round → bar → grape → round).</summary>
        public void CycleAmmo()
        {
            Ammo = Trafalgar.Ships.Ammo.Next(Ammo);
        }

        /// <summary>Sets the loaded ammunition directly.</summary>
        /// <param name="type">The desired ammo type.</param>
        public void SetAmmo(AmmoType type)
        {
            Ammo = type;
        }

        /// <summary>Gets a value indicating whether the given broadside is reloaded and ready.</summary>
        /// <param name="side">Which broadside.</param>
        /// <returns><c>true</c> if ready to fire.</returns>
        public bool IsBroadsideReady(BroadsideSide side)
        {
            return (side == BroadsideSide.Port ? m_PortReload : m_StarboardReload) <= 0f;
        }

        /// <summary>Gets reload progress in [0, 1] for a broadside (1 = ready).</summary>
        /// <param name="side">Which broadside.</param>
        /// <returns>Reload progress.</returns>
        public float ReloadProgress(BroadsideSide side)
        {
            float t = side == BroadsideSide.Port ? m_PortReload : m_StarboardReload;
            return 1f - Mathf.Clamp01(t / Stats.reloadTime);
        }

        /// <summary>Marks a broadside as fired, starting its reload timer.</summary>
        /// <param name="side">Which broadside.</param>
        public void NotifyFired(BroadsideSide side)
        {
            if (side == BroadsideSide.Port)
            {
                m_PortReload = Stats.reloadTime;
            }
            else
            {
                m_StarboardReload = Stats.reloadTime;
            }

            if (m_View != null)
            {
                m_View.PlayBroadsideSmoke(side);
            }
        }

        /// <summary>Gets a value indicating whether a fore/aft chase battery is reloaded and ready.</summary>
        /// <param name="bow"><c>true</c> for the bow chase guns, <c>false</c> for the stern.</param>
        /// <returns><c>true</c> if ready to fire.</returns>
        public bool IsChaseReady(bool bow)
        {
            return (bow ? m_BowReload : m_SternReload) <= 0f;
        }

        /// <summary>Marks a chase battery as fired, starting its reload timer.</summary>
        /// <param name="bow"><c>true</c> for the bow chase guns, <c>false</c> for the stern.</param>
        public void NotifyChaseFired(bool bow)
        {
            if (bow)
            {
                m_BowReload = Stats.reloadTime;
            }
            else
            {
                m_SternReload = Stats.reloadTime;
            }
        }

        /// <summary>
        /// Gets the world-space outward direction the chase guns point (bow = ahead, stern = astern).
        /// </summary>
        /// <param name="bow"><c>true</c> for the bow chase guns, <c>false</c> for the stern.</param>
        /// <returns>A unit direction on the XZ plane.</returns>
        public Vector3 ChaseNormal(bool bow)
        {
            return bow ? transform.forward : -transform.forward;
        }

        /// <summary>
        /// Gets the world-space outward normal of a broadside (perpendicular to the hull).
        /// </summary>
        /// <param name="side">Which broadside.</param>
        /// <returns>A unit direction on the XZ plane.</returns>
        public Vector3 BroadsideNormal(BroadsideSide side)
        {
            return side == BroadsideSide.Starboard ? transform.right : -transform.right;
        }

        /// <summary>
        /// Applies a resolved broadside's worth of damage from a given ammo profile.
        /// </summary>
        /// <param name="profile">The attacking ammo profile.</param>
        /// <param name="multiplier">A combined gun-count / range / spread multiplier.</param>
        public void ApplyDamage(AmmoProfile profile, float multiplier)
        {
            if (!IsAlive)
            {
                return;
            }

            Hull = Mathf.Max(0f, Hull - (profile.hullDamage * multiplier));
            Rigging = Mathf.Max(0f, Rigging - (profile.riggingDamage * multiplier));
            Crew = Mathf.Max(0f, Crew - (profile.crewDamage * multiplier));

            if (m_View != null)
            {
                m_View.FlashHit();
            }

            if (Hull <= 0f)
            {
                BeginSinking();
            }
        }

        /// <summary>
        /// Captures the ship for a boarding faction: it switches allegiance and is patched up
        /// just enough to keep fighting under its new colours.
        /// </summary>
        /// <param name="newFaction">The faction taking the prize.</param>
        public void Capture(Faction newFaction)
        {
            if (!IsAlive)
            {
                return;
            }

            Faction = newFaction;
            Hull = Mathf.Max(Hull, Stats.maxHull * 0.25f);
            Crew = Mathf.Max(Crew, Stats.maxCrew * 0.4f);
            Sail = SailSetting.BattleSail;
            TargetHeadingDeg = HeadingDeg;
            Speed = 0f;

            if (m_View != null)
            {
                m_View.OnFactionChanged();
            }
        }

        /// <summary>Begins the visible sinking sequence; the ship stops responding to orders.</summary>
        public void BeginSinking()
        {
            if (State != ShipState.Sailing)
            {
                return;
            }

            State = ShipState.Sinking;
            Faction = Faction.Neutral;
            Hull = 0f;
            m_SinkTimer = GameConfig.SinkDuration;
        }

        /// <summary>
        /// Advances the ship one simulation step.
        /// </summary>
        /// <param name="dt">Delta time in seconds.</param>
        /// <param name="wind">The global wind.</param>
        public void Tick(float dt, Wind wind)
        {
            if (State == ShipState.Sinking)
            {
                TickSinking(dt);
                return;
            }

            if (State != ShipState.Sailing)
            {
                return;
            }

            // Reloads tick regardless of motion.
            m_PortReload = Mathf.Max(0f, m_PortReload - dt);
            m_StarboardReload = Mathf.Max(0f, m_StarboardReload - dt);
            m_BowReload = Mathf.Max(0f, m_BowReload - dt);
            m_SternReload = Mathf.Max(0f, m_SternReload - dt);

            // Steer away from the world edge before turning, so the reversal is applied this frame.
            HandleArenaEdge();

            // Rigging damage degrades both handling and achievable speed.
            float riggingFactor = Mathf.Lerp(0.35f, 1f, RiggingFraction);

            // Turn toward the ordered course.
            float effectiveTurn = Stats.turnRate * riggingFactor;
            HeadingDeg = Nav.MoveTowardsAngle(HeadingDeg, TargetHeadingDeg, effectiveTurn * dt);

            // Point of sail sets how much of the rig's drive we actually get.
            float offWind = Nav.AngleDifference(HeadingDeg, wind.FromDegrees);
            float sailFactor = Combat.Wind.PointOfSailFactor(offWind, out string pos);
            PointOfSail = pos;

            float targetSpeed = Stats.topSpeed * Sail.ThrottleFactor() * sailFactor * riggingFactor;
            Speed = Mathf.MoveTowards(Speed, targetSpeed, Stats.acceleration * dt);

            // Advance.
            transform.rotation = Quaternion.Euler(0f, HeadingDeg, 0f);
            transform.position += Nav.HeadingToVector(HeadingDeg) * (Speed * dt);

            if (m_View != null)
            {
                m_View.UpdateVisuals(this, wind, dt);
            }
        }

        private void TickSinking(float dt)
        {
            m_SinkTimer -= dt;
            float t = 1f - Mathf.Clamp01(m_SinkTimer / GameConfig.SinkDuration);

            // Slide gently under the waves and shrink.
            transform.position += Vector3.down * (0.6f * dt);
            transform.localScale = Vector3.one * Mathf.Lerp(1f, 0.3f, t);

            if (m_View != null)
            {
                m_View.UpdateSinking(t);
            }

            if (m_SinkTimer <= 0f)
            {
                State = ShipState.Gone;
            }
        }

        /// <summary>
        /// Keeps ships in play without clamping or bouncing: when a ship crosses the edge threshold
        /// while still heading outward, it commits (once) to a 180° reversal of its current heading
        /// and the normal steering turns it around at the normal turn rate and speed. A hysteresis
        /// band (it must fall back inside <see cref="GameConfig.EdgeTurnClear"/> before re-arming)
        /// stops it oscillating, and a fresh player/AI course issued afterwards overrides the turn.
        /// </summary>
        private void HandleArenaEdge()
        {
            // Per-axis bounds: the long X axis and the short Z axis have different extents on the
            // 16:9 field, so thresholds are computed independently for each.
            float edgeX = GameConfig.ArenaHalfX * GameConfig.EdgeTurnThreshold;
            float edgeZ = GameConfig.ArenaHalfZ * GameConfig.EdgeTurnThreshold;
            float clearX = GameConfig.ArenaHalfX * GameConfig.EdgeTurnClear;
            float clearZ = GameConfig.ArenaHalfZ * GameConfig.EdgeTurnClear;
            Vector3 p = transform.position;

            if (m_EdgeReversing)
            {
                // Committed to the turn: just wait until we're well back inside, then re-arm. We do
                // not touch the target here, so any new course the player/AI sets meanwhile wins.
                if (Mathf.Abs(p.x) < clearX && Mathf.Abs(p.z) < clearZ)
                {
                    m_EdgeReversing = false;
                }

                return;
            }

            bool beyond = Mathf.Abs(p.x) > edgeX || Mathf.Abs(p.z) > edgeZ;
            if (!beyond)
            {
                return;
            }

            // Only intervene if we are actually sailing further out; a ship already heading inward
            // (e.g. the AI steering for the centre, or a fleet spawned facing inward) is left alone.
            var outward = new Vector3(
                Mathf.Abs(p.x) > edgeX ? Mathf.Sign(p.x) : 0f,
                0f,
                Mathf.Abs(p.z) > edgeZ ? Mathf.Sign(p.z) : 0f);
            if (Vector3.Dot(Nav.HeadingToVector(HeadingDeg), outward) <= 0f)
            {
                return;
            }

            TargetHeadingDeg = Nav.Normalize360(HeadingDeg + 180f);
            m_EdgeReversing = true;
        }
    }
}
