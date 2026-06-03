// Global, designer-tunable constants for the whole simulation — a port of
// Unity `Core/GameConfig.cs`. Kept in one place so the feel of the game can be
// adjusted without hunting through systems. See the Unity source for the full
// rationale behind each knob.

/** Master linear scale for the whole battle. */
export const ShipScale = 10;

/** Global tuning multiplier on the base sailing speed of every ship. Lowered so
 *  the whole fleet sails more slowly (sailboat feel), on top of the per-setting
 *  sail throttle fractions. */
export const BaseSpeedMultiplier = 0.18;

/** Global tuning multiplier on the base broadside gun range of every ship. */
export const BaseRangeMultiplier = 2;

/** Half-extent of the playable sea along the LONG axis (world X). */
export const ArenaHalfX = 90 * ShipScale;

/** Half-extent along the SHORT axis (world Z), at a 16:9 ratio. */
export const ArenaHalfZ = ArenaHalfX * (9 / 16);

/** Fraction of the view kept as a safe-area inset on each edge. */
export const ArenaSafeInset = 0.05;

/** Orthographic camera vertical half-height. */
export const CameraOrthoSize = ArenaHalfZ / (1 - ArenaSafeInset);

/** Extra multiplier on hull size (length/beam) ONLY. Ships render at 0.75× of
 *  the previous 1.75 here; selection radius and sprite scale track `length`, so
 *  everything stays coherent while arena/range/speed (ShipScale) are unchanged. */
export const HullSizeBoost = 1.3125;

// ---- Wind ----

/** Smallest angle off the true wind a ship can make headway (the "no-go" zone). */
export const NoGoAngle = 42;

/** Speed multiplier floor while in irons (head to wind). */
export const InIronsFactor = 0.05;

/** Seconds between gradual wind-direction shifts. */
export const WindShiftInterval = 22;

/** Maximum degrees the wind veers/backs at each shift. */
export const WindShiftMagnitude = 18;

// ---- Combat ----

/** Half-angle (degrees) of the firing arc abeam each broadside. */
export const BroadsideArcHalfAngle = 45;

/** Half-angle (degrees) of the fore/aft arc in which the chase guns may fire. */
export const ChaseArcHalfAngle = 28;

/** Per-gun damage scalar applied on top of the ammo profile. Raised so close
 *  engagements are decisively lethal (a few good broadsides tell). */
export const PerGunDamageScale = 0.5;

/** Fraction of a normal gun's damage dealt by a fore/aft chase gun. */
export const ChaseDamageFactor = 0.5;

/** Random spread (+/- fraction) applied to each ball's damage. */
export const DamageSpread = 0.2;

/** Visual travel speed of a cannonball in world units / second. */
export const ProjectileSpeed = 70 * ShipScale;

// ---- Range-dependent gunnery -------------------------------------------------
// Both DAMAGE and HIT CHANCE scale with "closeness" = 1 - (range / gunRange),
// so 1 at point-blank and 0 at max range. Each is shaped by an exponent so
// closing the range is strongly rewarded (exponent > 1 → the bonus falls off
// quickly as the gap opens). The result: point-blank volleys are deadly and
// land most of their balls; long-range volleys are weak and mostly miss.

/** Per-ball damage multiplier at point-blank (the curve's peak, range 0). */
export const PointBlankDamageMultiplier = 1.35;

/** Per-ball damage multiplier at maximum range (the curve's floor). */
export const RangeFalloffFloor = 0.2;

/** Exponent shaping the damage curve vs closeness (1 = linear, >1 rewards closing). */
export const DamageFalloffExponent = 1.8;

/** Per-ball hit chance at point-blank range. */
export const HitChancePointBlank = 0.95;

/** Per-ball hit chance at maximum range. */
export const HitChanceMaxRange = 0.2;

/** Exponent shaping the hit-chance curve vs closeness (>1 rewards closing). */
export const HitChanceExponent = 1.5;

/** Scatter radius (× target beam) for a HIT — lands tight on the hull. */
export const HitScatterFactor = 0.5;

/** Scatter radius (× target length) for a MISS — a visible near-miss splash. */
export const MissScatterFactor = 0.9;

// ---- Edges ----

/** Fraction of the per-axis half-extent at which a ship commits to a 180° turn. */
export const EdgeTurnThreshold = 0.7;

/** Fraction a turning ship must fall back inside before it may turn again. */
export const EdgeTurnClear = 0.55;

// ---- Rigging / sail damage ----

/** Effective-speed multiplier at zero rigging (full rigging = 1). Damaged sails
 *  slow the ship: effective top speed = lerp(RiggingSpeedFloor, 1, riggingFrac). */
export const RiggingSpeedFloor = 0.3;

// ---- Repair (fully automatic; no player action / button) ----

/** Seconds a ship must go WITHOUT being hit before auto-repair kicks in. Getting
 *  hit resets this timer, pausing regen until it's been safe this long again. */
export const RepairSafeDelay = 5.0;

/** Rigging auto-restored per second once safe, as a fraction of max rigging
 *  (≈ a full repair from zero in ~1/this seconds, i.e. ~25s at 0.04). */
export const RepairFractionPerSecond = 0.01;

// ---- Misc ----

/** Seconds a sinking ship lingers before removal. */
export const SinkDuration = 4.0;

/** World-unit floor for the tap-to-select radius. */
export const ShipSelectRadius = 3.5 * ShipScale;
