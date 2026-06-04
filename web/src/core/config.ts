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

// ---- Starting formation ----

/** Gap (world units) left BETWEEN the hulls of adjacent ships in a starting
 *  line-ahead column (added to each ship's half-length so neighbours never
 *  overlap regardless of the class mix). */
export const ColumnGap = 2 * ShipScale;

/** Extra multiplier on hull size (length/beam) ONLY. Reduced to 0.75× of the
 *  previous 1.3125 (= 0.984375); selection radius and sprite scale track
 *  `length`, so everything stays coherent while arena/range/speed (ShipScale)
 *  are unchanged. */
export const HullSizeBoost = 1.3125 * 0.75;

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

/** Per-gun damage scalar applied on top of the ammo profile. Tuned DOWN now that
 *  broadsides fire their full gun count (First Rate = 32): a point-blank full
 *  broadside is devastating but a large ship still takes a few good broadsides to
 *  sink (not one). See the per-class totals in CombatSystem comments. */
export const PerGunDamageScale = 0.3;

/** Fraction of a normal gun's damage dealt by a fore/aft chase gun. Kept low so a
 *  2-gun chase volley is harassment, never a substitute for a broadside (a full
 *  broadside now massively outguns it on sheer gun count too). */
export const ChaseDamageFactor = 0.35;

/** Random spread (+/- fraction) applied to each ball's damage. */
export const DamageSpread = 0.2;

// ---- Stern rake ----
// A shot fired into a target's stern (from roughly behind it) rakes the full
// length of the gun deck and is devastating. We model this with a rear-arc cone
// measured from the target's astern direction (-forward): any shooter inside the
// cone deals SternRakeMultiplier× damage.

/** Half-angle (degrees) of a target's REAR arc, measured from dead astern. A
 *  shooter within this cone behind the target lands a stern rake. */
export const SternRakeArcHalfAngle = 50;

/** Damage multiplier applied to a shot landing in the target's rear/stern arc. */
export const SternRakeMultiplier = 4;

// ---- Friendly line-of-fire occlusion ----

/** A friendly hull is treated as a blocking circle of this radius (× its beam)
 *  on the shooter→target line of fire. If a friendly's circle straddles the
 *  line and sits between shooter and target, the shot is blocked. */
export const FriendlyBlockRadiusFactor = 1.0;

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

/** Minimum engagement range (world units) used by the gunnery model: the
 *  effective range is floored at this value before computing closeness, so two
 *  hulls grinding alongside each other (or briefly touching) can never reach the
 *  absolute point-blank peak and instakill. Point-blank fire is still strong,
 *  just not "on top of each other" lethal. */
export const MinEngagementRange = 12 * ShipScale;

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

// ---- Baton of Command ----

/** How close (world units) the Baton of Command must be placed to a friendly,
 *  human-controlled ship to take command of it. The baton commands the NEAREST
 *  such ship within this radius at the moment it is placed (≈ a couple of ship
 *  lengths, so clicking on or just beside a ship commands it). */
export const BatonCommandRadius = 22 * ShipScale;

// ---- Soft collision separation ----
// A long, thin hull can't be represented by a single centre circle: a circle big
// enough to cover the bow/stern tips (radius ≈ half the LENGTH) would hold ships
// absurdly far apart abeam, while a smaller circle lets bows/sterns visibly cross
// (the old bug). So each hull is a CAPSULE instead — its keel as a line segment
// of half-length `ShipCollisionHalfLengthFactor × length`, swollen by a radius of
// `ShipCollisionBeamFactor × beam`. The capsule tightly bounds the painted hull
// at every orientation, so two capsules not overlapping ⇒ two hulls not
// overlapping, AND hulls can still close until they actually touch (abeam or
// bow-to-stern) before stopping.

/** Capsule half-length as a fraction of hull length. 0.5 ⇒ the capsule spans the
 *  full painted hull (bow tip to stern tip), so collinear hulls stop exactly when
 *  their tips meet — no bow/stern crossing. */
export const ShipCollisionHalfLengthFactor = 0.5;

/** Capsule radius as a fraction of the hull BEAM. Slightly over 0.5 (half-beam)
 *  for a small visual margin so hull sides never appear to intersect. */
export const ShipCollisionBeamFactor = 0.55;

/** Relaxation iterations of the all-pairs separation per frame. With ≤16 ships
 *  this is trivially cheap (O(iterations × n²)); a few Gauss-Seidel passes drive
 *  residual penetration in packed clusters (spawn column, melee) to ~0 each
 *  frame, so overlap can't accumulate. */
export const ShipSeparationIterations = 4;

// ---- Initial wind ----

/** The INITIAL wind direction is constrained to within this many degrees of due
 *  north (0°) — i.e. the arc 315°→0°→45° — so battles never open with the wind
 *  in the [45, 315] band. Only the starting direction is constrained; the wind
 *  veers normally thereafter. */
export const InitialWindArcHalfAngle = 45;

// ---- Floating text popups (e.g. "RAKE") ----

/** Seconds a floating combat-text popup lives before it is destroyed. */
export const FloatingTextLife = 1.0;

/** World units a floating text popup drifts upward (toward +Z) over its life. */
export const FloatingTextRise = 6 * ShipScale;

/** On-screen font size (CSS px) of a floating combat-text popup. */
export const FloatingTextFontSize = 26;
