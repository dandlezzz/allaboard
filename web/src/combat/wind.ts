// The global wind — a port of Unity `Combat/Wind.cs`. Age-of-sail ships cannot
// sail directly into the wind, so wind direction is the central tactical
// constraint. The wind slowly veers over the course of a battle.

import * as Config from "../core/config";
import { headingToVector, normalize360, angleDifference, moveTowardsAngle } from "../core/nav";
import { inverseLerp, lerp } from "../core/mathf";
import { rangeFloat } from "../core/rng";
import type { Vec2 } from "../core/vec";

export interface PointOfSailResult {
  factor: number;
  pointOfSail: string;
}

export class Wind {
  /** Direction the wind blows *from*, in compass degrees (the "weather gauge"). */
  fromDegrees: number;

  private shiftTimer: number;
  private targetFromDeg: number;

  constructor(initialFromDeg: number) {
    this.fromDegrees = normalize360(initialFromDeg);
    this.targetFromDeg = this.fromDegrees;
    this.shiftTimer = Config.WindShiftInterval;
  }

  /** Unit vector pointing the way the wind is blowing *toward* (downwind). */
  get blowingToward(): Vec2 {
    return headingToVector(normalize360(this.fromDegrees + 180));
  }

  /** Unit vector pointing toward the source of the wind (upwind). */
  get source(): Vec2 {
    return headingToVector(this.fromDegrees);
  }

  tick(dt: number): void {
    this.shiftTimer -= dt;
    if (this.shiftTimer <= 0) {
      this.shiftTimer = Config.WindShiftInterval;
      const shift = rangeFloat(-Config.WindShiftMagnitude, Config.WindShiftMagnitude);
      this.targetFromDeg = normalize360(this.targetFromDeg + shift);
    }

    this.fromDegrees = moveTowardsAngle(this.fromDegrees, this.targetFromDeg, 3 * dt);
  }

  /** Speed multiplier for a ship on a given heading (its "point of sail"). */
  pointOfSailFactorFor(headingDeg: number): number {
    const offWind = angleDifference(headingDeg, this.fromDegrees);
    return pointOfSailFactor(offWind).factor;
  }
}

// Point-of-sail status colours — shared by the on-ship point-of-sail dot AND the
// course-vector lines so the two always read the same. A SIX-step slow→fast ramp,
// one colour per point of sail (see `pointOfSailColorIndex` / `pointOfSailFactor`):
//   0 In Irons      → red       (no-go, slowest)
//   1 Close-Hauled  → orange
//   2 Close Reach   → amber/yellow
//   3 Beam Reach    → yellow-green
//   4 Broad Reach   → green      (fastest)
//   5 Running       → teal/cyan  (fast, slightly off the broad-reach peak)
export const POINT_OF_SAIL_COLORS = [
  0xff4d4d, 0xff8c33, 0xffd24a, 0x9ed94d, 0x4dd06a, 0x33c4c4,
] as const;

/** Maps a point-of-sail label to its colour-ramp index (0..5). Every label
 *  `pointOfSailFactor` can return is covered; unknown labels fall back to a
 *  mid/fast green so there's never an out-of-range lookup. */
export function pointOfSailColorIndex(label: string): number {
  switch (label) {
    case "In Irons":
      return 0;
    case "Close-Hauled":
      return 1;
    case "Close Reach":
      return 2;
    case "Beam Reach":
      return 3;
    case "Broad Reach":
      return 4;
    case "Running":
      return 5;
    default:
      return 4;
  }
}

/** The point-of-sail status colour for sailing `headingDeg` in the given wind —
 *  used to tint the course preview by the expected speed of that heading. */
export function pointOfSailColor(headingDeg: number, wind: Wind): number {
  const off = angleDifference(headingDeg, wind.fromDegrees);
  return POINT_OF_SAIL_COLORS[pointOfSailColorIndex(pointOfSailFactor(off).pointOfSail)];
}

/**
 * Computes the speed multiplier and classifies the point of sail across SIX
 * classic bands (angle off the true wind, 0° = dead into wind → 180° = dead
 * downwind). Speeds rise from the no-go zone to a broad-reach peak, then ease
 * back a touch dead downwind (square-rig blanketing). Factors interpolate
 * smoothly and are continuous at every band boundary:
 *
 *   In Irons     0°–NoGoAngle(42°)   InIronsFactor → 0.30   (slowest, no-go)
 *   Close-Hauled 42°–60°             0.30 → 0.50
 *   Close Reach  60°–80°             0.50 → 0.70
 *   Beam Reach   80°–100°            0.70 → 0.88 → 0.90    (bulged at 90°)
 *   Broad Reach  100°–150°           0.90 → 1.00            (fastest)
 *   Running      150°–180°           1.00 → 0.85
 */
export function pointOfSailFactor(offWindAngle: number): PointOfSailResult {
  offWindAngle = Math.abs(offWindAngle);

  if (offWindAngle < Config.NoGoAngle) {
    return {
      pointOfSail: "In Irons",
      factor: lerp(Config.InIronsFactor, 0.3, inverseLerp(0, Config.NoGoAngle, offWindAngle)),
    };
  }

  if (offWindAngle < 60) {
    return {
      pointOfSail: "Close-Hauled",
      factor: lerp(0.3, 0.5, inverseLerp(Config.NoGoAngle, 60, offWindAngle)),
    };
  }

  if (offWindAngle < 80) {
    return {
      pointOfSail: "Close Reach",
      factor: lerp(0.5, 0.7, inverseLerp(60, 80, offWindAngle)),
    };
  }

  if (offWindAngle < 100) {
    // Bulged at the true beam (90°): ships hold more speed perpendicular to the
    // wind. Endpoints (0.70 at 80°, 0.90 at 100°) match the neighbouring bands,
    // so the curve stays continuous.
    const factor =
      offWindAngle < 90
        ? lerp(0.7, 0.88, inverseLerp(80, 90, offWindAngle))
        : lerp(0.88, 0.9, inverseLerp(90, 100, offWindAngle));
    return { pointOfSail: "Beam Reach", factor };
  }

  if (offWindAngle < 150) {
    return {
      pointOfSail: "Broad Reach",
      factor: lerp(0.9, 1.0, inverseLerp(100, 150, offWindAngle)),
    };
  }

  return {
    pointOfSail: "Running",
    factor: lerp(1.0, 0.85, inverseLerp(150, 180, offWindAngle)),
  };
}
