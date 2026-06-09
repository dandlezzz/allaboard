// Global, designer-tunable constants for the whole simulation — a port of
// Unity `Core/GameConfig.cs`. Kept in one place so the feel of the game can be
// adjusted without hunting through systems. See the Unity source for the full
// rationale behind each knob.

/** Master linear scale for the whole battle. */
export const ShipScale = 10;

/** Global tuning multiplier on the base sailing speed of every ship. Lowered so
 *  the whole fleet sails more slowly (sailboat feel), on top of the per-setting
 *  sail throttle fractions. Reduced 0.18 → 0.12 (~33% slower) for a more
 *  deliberate, stately pace; nudged back up 0.12 → 0.15 (midway to the earlier
 *  0.18) so the fleet isn't sluggish, while the per-class topSpeed compression
 *  keeps the small-vs-large spread tight. */
export const BaseSpeedMultiplier = 0.15;

/** Global tuning multiplier on the base broadside gun range of every ship.
 *  Reduced 2 → 1.5 (25% shorter range): fleets must close more to engage. Range
 *  still dwarfs a hull length and fits the arena (First Rate gunRange = 40 × 10 ×
 *  1.5 = 600 < arena width 1800). Further × 0.8 cuts every ship's effective fire
 *  range by 1/5th (fleets must close even more to engage); damage is untouched. */
export const BaseRangeMultiplier = 1.5 * 0.8;

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

/** Extra multiplier on hull size (length/beam) ONLY. Base 1.3125 × 0.75 × 0.75
 *  (≈ 0.738), then × 1.25 to render every ship 25% larger (≈ 0.923). Selection
 *  radius, sprite scale, the collision capsule, the command bubble and spawn
 *  spacing all track `length`, so everything scales coherently while
 *  arena/range/speed (ShipScale) are unchanged; the 8-ship bottom-corner columns
 *  still fit on-screen and don't overlap at this size. Final × 0.75 shrinks every
 *  ship to 0.75 of its former footprint (visual + collision capsule together). */
export const HullSizeBoost = 1.3125 * 0.75 * 0.75 * 1.25 * 0.75;

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

/** Per-gun damage scalar applied on top of the ammo profile. Tuned DOWN (0.3 →
 *  0.24, ~20% less base damage) so engagements last longer; combined with the
 *  steeper range falloff below, close-range broadsides still bite while
 *  long-range fire is weak. */
export const PerGunDamageScale = 0.24;

/** Fraction of a normal gun's damage dealt by a fore/aft chase gun. Kept low so a
 *  2-gun chase volley is harassment, never a substitute for a broadside (a full
 *  broadside now massively outguns it on sheer gun count too). */
export const ChaseDamageFactor = 0.35;

/** Random spread (+/- fraction) applied to each ball's damage. */
export const DamageSpread = 0.2;

// ---- Stern rake ----
// A true rake is a broadside fired INTO a target's stern such that the line of
// fire runs down the full fore-aft length of the hull — the balls sweep the
// length of the gun deck. The decisive geometry is the TARGET's orientation
// relative to the shooter→target line: the target must be presenting its stern,
// i.e. its keel axis is nearly aligned with the line of fire. We model this with
// a TIGHT cone measured from the target's astern direction (-forward): a shooter
// inside that cone is dead behind the target, so the shot rakes and deals
// SternRakeMultiplier× damage. A ship hit abeam (broadside-to-broadside) is NOT
// raked.

/** Half-angle (degrees) of a target's stern cone, measured from dead astern. The
 *  shooter must sit within this cone of the target's keel line for the line of
 *  fire to run the length of the hull (a true rake). Deliberately tight: hulls
 *  are ~4:1 length:beam, so the stern-to-opposite-bow diagonal is only
 *  atan(beam/length) ≈ 14° off the keel; 20° keeps the rake to shots that
 *  genuinely sweep the deck while allowing a little slop for the target yawing.
 *  (Was 50° — a 100°-wide rear cone that raked almost any shot from behind.) */
export const SternRakeArcHalfAngle = 20;

/** Damage multiplier applied to a shot landing in the target's rear/stern arc.
 *  Bumped 4 → 7 now that the rake cone is tight (20°): true rakes are rare and
 *  hard-earned, so a clean stern rake should be devastating. */
export const SternRakeMultiplier = 7;

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

/** Per-ball damage multiplier at maximum range (the curve's floor). Lowered
 *  0.2 → 0.07 so max-range hits do far less. */
export const RangeFalloffFloor = 0.07;

/** Exponent shaping the damage curve vs closeness (1 = linear, >1 rewards
 *  closing). Raised 1.8 → 2.6 so damage drops off harder as the range opens. */
export const DamageFalloffExponent = 2.6;

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

/** Sphere-of-influence radius (world units) around the Baton of Command. Every
 *  alive, human-controlled friendly ship of ONE side within this radius at
 *  placement time comes under command, so a single baton moves a whole squadron
 *  together. Deliberately large (a full fleet column is ≈ 500 units, and ships
 *  are now smaller) so the sphere gathers several ships at once. Grown to 20×
 *  ShipScale so the pushed-out command-control cluster (sail mast + ammo disc,
 *  anchored BatonControlClusterRadius below the baton) sits inside the command
 *  circle; still a clear sub-region of the field. */
export const BatonCommandRadius = 20 * ShipScale;

// ---- Baton lifecycle: rotate-to-steer, controls, mouse emulation ----
// The Baton of Command is a Piece-centric controller: set the Piece down to take
// command (capturing its squadron once), rotate it to steer, tap the floating
// controls with a finger to trim, and lift it to dismiss. These knobs tune that
// scheme; see docs/baton-touch-scheme.md and docs/piece-interaction-design.md.

/** Minimum change (degrees) in a Piece's orientation before the commanded
 *  squadron's heading is re-applied. Doubles as the rotate dead-band (sub-this
 *  jitter on a resting Piece is ignored) and the latch threshold (we only write
 *  a new heading when the Piece has genuinely turned, so a held heading is never
 *  re-clamped to the Piece every frame). The Board reports ~1° precision, so 5°
 *  is comfortably above noise and matches the forgiving rotate tolerance the
 *  Piece-interaction guide recommends. */
export const BatonSteerToleranceDeg = 5;

/** World-unit radius around a baton roundel that counts as "on the roundel" for
 *  the browser mouse path (press here to steer-drag; tap here to dismiss). A
 *  little larger than the drawn roundel (2.4 × ShipScale) for an easy target. */
export const BatonRoundelHitRadius = 3.4 * ShipScale;

/** How far (world units) the floating finger-trim control cluster sits from the
 *  resting baton (anchored as a ring around it, then clamped on-screen). Pushed
 *  out so the sail mast + ammo disc don't crowd the baton roundel; the
 *  command/sphere radius (BatonCommandRadius) was grown to keep them inside the
 *  command circle. */
export const BatonControlClusterRadius = 12 * ShipScale;

/** Half the gap (world units) between the two trim buttons in the cluster. */
export const BatonControlButtonGap = 3.8 * ShipScale;

/** World-unit radius of each floating trim button (sail / ammo) + its hit-test. */
export const BatonControlButtonRadius = 2.6 * ShipScale;

/** Screen-px a mouse press on a baton roundel must travel before it is treated
 *  as a steer-drag rather than a (dismiss) tap. Only used on the roundel, so it
 *  no longer governs place-vs-course the way the old global threshold did. */
export const BatonMouseDragThresholdPx = 6;

// ---- Pre-game setup / command-piece placement ----
// Before the battle starts the game sits in a SETUP phase: each human side must
// place its command piece (a mouse click in the browser, a Glyph contact on
// Board hardware) on a designated glowing PAD near that fleet's start corner.
// The placed position seeds that side's Baton of Command, assigns the fleet to
// human control, and marks the player ready. The battle begins once every
// required (human) side is ready — AI sides are auto-ready via their persona.

/** Radius (world units) of a setup placement pad / its accepting hit-test. */
export const SetupPadRadius = 6.5 * ShipScale;

/** Centre of the British command pad (bottom-left, inboard of that fleet's
 *  start column so it sits in open water with room below for its label). */
export const SetupPadBritish: { x: number; z: number } = {
  x: -ArenaHalfX * 0.66,
  z: -ArenaHalfZ * 0.58,
};

/** Centre of the Franco-Spanish command pad (bottom-right, mirrored). */
export const SetupPadFrancoSpanish: { x: number; z: number } = {
  x: ArenaHalfX * 0.66,
  z: -ArenaHalfZ * 0.58,
};

/** Seconds the "all hands ready" countdown runs before the battle begins. */
export const SetupCountdownSeconds = 1.6;

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
