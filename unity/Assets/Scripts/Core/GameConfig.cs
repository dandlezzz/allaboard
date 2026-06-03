// <copyright file="GameConfig.cs" company="board.fun game">
//     Trafalgar - Age of Sail. Procedural board.fun RTS.
// </copyright>

namespace Trafalgar.Core
{
    /// <summary>
    /// Global, designer-tunable constants for the whole simulation. Kept in one place so
    /// the feel of the game can be adjusted without hunting through systems.
    /// </summary>
    public static class GameConfig
    {
        /// <summary>
        /// Master linear scale for the whole battle. The ships' hull dimensions and every
        /// world-space quantity that must stay proportional to hull size (arena, fleet spacing,
        /// gun range, boarding reach, selection radius, speeds, tracer speed) are multiplied by
        /// this. Keeping it a single knob means the simulation stays internally coherent: at any
        /// scale the game plays identically, just larger or smaller in world units. Set to 1 for
        /// the original tabletop scale; 10 makes the ships ten times bigger.
        /// </summary>
        public const float ShipScale = 10f;

        /// <summary>
        /// Global tuning multiplier on the *base* sailing speed of every ship, applied on top of
        /// <see cref="ShipScale"/> to both top speed and acceleration (so handling stays matched —
        /// ships still reach top speed over a comparable distance). This is independent of the
        /// scale: 1 = the original feel, 0.25 = a quarter of the original base sailing speed across
        /// the fleet. Distance- and time-based combat/handling stats are unaffected.
        /// </summary>
        public const float BaseSpeedMultiplier = 0.25f;

        /// <summary>
        /// Global tuning multiplier on the *base* broadside gun range of every ship, applied on top
        /// of <see cref="ShipScale"/>. This is independent of the scale: 1 = the original reach,
        /// 2 = double range across the fleet. The broadside firing arc half-angles are angular and
        /// are unaffected.
        /// </summary>
        public const float BaseRangeMultiplier = 2f;

        /// <summary>
        /// Half-extent of the playable sea along the LONG axis (world X = the board's 1920 / wide
        /// dimension, screen-horizontal). Fleets line up behind the short left/right edges and sail
        /// toward each other across this long span. Kept at the old square half so the gun-range vs
        /// arena balance is unchanged (the longest range, 800, still fits inside this 900).
        /// </summary>
        public const float ArenaHalfX = 90f * ShipScale;

        /// <summary>
        /// Half-extent along the SHORT axis (world Z = the board's 1080 / narrow dimension,
        /// screen-vertical). Derived from <see cref="ArenaHalfX"/> at a 16:9 ratio so the whole
        /// field matches the 1920×1080 landscape board.
        /// </summary>
        public const float ArenaHalfZ = ArenaHalfX * (9f / 16f);

        /// <summary>Fraction of the view kept as a safe-area inset on each edge (the board has a bezel).</summary>
        public const float ArenaSafeInset = 0.05f;

        /// <summary>
        /// Orthographic camera vertical half-height. Driven by the SHORT (Z) extent plus the safe
        /// inset; the long (X) extent then fits via the 16:9 aspect (both axes share the same 16:9
        /// ratio, so a single uniform inset frames the whole field on a 16:9 board).
        /// </summary>
        public const float CameraOrthoSize = ArenaHalfZ / (1f - ArenaSafeInset);

        /// <summary>
        /// Extra multiplier on hull size (length/beam) ONLY — deliberately NOT applied to the arena
        /// or camera. Because the arena and camera are both tied to <see cref="ShipScale"/>, bumping
        /// ShipScale changes nothing on screen (the view zooms out with the ships). To actually make
        /// the ships fill more of the view we grow the hull relative to the fixed arena/camera, so
        /// the hull-to-view ratio rises directly with this value (≈ the on-screen size multiplier).
        /// Hull-coupled gameplay reach (boarding distance) carries this factor too so the feel stays
        /// coherent; selection radius already tracks hull length automatically.
        /// </summary>
        public const float HullSizeBoost = 1.75f;

        // ---- Wind ----

        /// <summary>Smallest angle off the true wind a ship can make headway: inside this is the "no-go" zone.</summary>
        public const float NoGoAngle = 42f;

        /// <summary>Speed multiplier floor while in irons (head to wind).</summary>
        public const float InIronsFactor = 0.05f;

        /// <summary>Seconds between gradual wind-direction shifts.</summary>
        public const float WindShiftInterval = 22f;

        /// <summary>Maximum degrees the wind veers/backs at each shift.</summary>
        public const float WindShiftMagnitude = 18f;

        // ---- Combat ----

        /// <summary>Half-angle (degrees) of the firing arc abeam each broadside.</summary>
        public const float BroadsideArcHalfAngle = 45f;

        /// <summary>Half-angle (degrees) of the fore/aft arc in which the (weak) chase guns may fire.</summary>
        public const float ChaseArcHalfAngle = 28f;

        /// <summary>
        /// Per-gun damage scalar applied on top of the ammo profile. Each gun now fires its own
        /// cannonball doing this much, and a broadside spawns one ball per gun, so total volley
        /// damage is roughly (gunsPerBroadside × this). Tuned up from the old single-shot value
        /// because gun counts dropped to a realistic handful (≤8) per side.
        /// </summary>
        public const float PerGunDamageScale = 0.3f;

        /// <summary>Fraction of a normal gun's damage dealt by a fore/aft chase gun (chase fire is weak).</summary>
        public const float ChaseDamageFactor = 0.5f;

        /// <summary>Fraction of damage still delivered at maximum range (linear falloff to this floor).</summary>
        public const float RangeFalloffFloor = 0.35f;

        /// <summary>Random spread (+/- fraction) applied to each broadside's damage.</summary>
        public const float DamageSpread = 0.2f;

        /// <summary>Visual travel speed of a cannonball in world units / second (scales with the battle).</summary>
        public const float ProjectileSpeed = 70f * ShipScale;

        // ---- Boarding ----

        /// <summary>
        /// Maximum centre-to-centre distance (world units) at which boarding can be attempted.
        /// Scaled with <see cref="ShipScale"/> and <see cref="HullSizeBoost"/> so "lying alongside"
        /// stays proportional to the (now larger) hulls.
        /// </summary>
        public const float BoardingRange = 5.0f * ShipScale * HullSizeBoost;

        /// <summary>Hull fraction at or below which a ship becomes vulnerable to capture.</summary>
        public const float BoardingHullThreshold = 0.33f;

        /// <summary>Crew fraction at or below which a ship becomes vulnerable to capture.</summary>
        public const float BoardingCrewThreshold = 0.33f;

        /// <summary>Seconds a boarding action takes to resolve once grapples are thrown.</summary>
        public const float BoardingDuration = 3.0f;

        // ---- Edges ----

        /// <summary>
        /// Fraction of the per-axis half-extent (<see cref="ArenaHalfX"/> / <see cref="ArenaHalfZ"/>)
        /// at which a ship still heading outward commits to a 180° turn back toward open water. Set
        /// conservatively so even the widest turning circle (the slow first-rate) completes inside
        /// the bounds — important on the short Z axis where there's less room — without a hard clamp.
        /// </summary>
        public const float EdgeTurnThreshold = 0.70f;

        /// <summary>
        /// Fraction of the per-axis half-extent a turning ship must fall back inside before it may
        /// trigger another edge turn. The gap from <see cref="EdgeTurnThreshold"/> is the hysteresis
        /// band that stops it oscillating at the boundary.
        /// </summary>
        public const float EdgeTurnClear = 0.55f;

        // ---- Misc ----

        /// <summary>Seconds a sinking ship lingers (visibly going down) before removal.</summary>
        public const float SinkDuration = 4.0f;

        /// <summary>
        /// World-unit floor for the tap-to-select radius. The effective radius is the larger of
        /// this and a fraction of the ship's hull length, so on big ships the hull itself drives
        /// selection; this just guarantees a sane minimum. Scaled with <see cref="ShipScale"/>.
        /// </summary>
        public const float ShipSelectRadius = 3.5f * ShipScale;
    }
}
