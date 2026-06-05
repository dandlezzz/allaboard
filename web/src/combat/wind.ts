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
// course-preview line so the two always read the same: red = in irons (no-go),
// amber = close-hauled, green = reaching / running (fast points of sail).
export const POINT_OF_SAIL_COLORS = [0xff4d4d, 0xffb020, 0x4dd06a] as const;

/** Maps a point-of-sail label to a colour-ramp index (0 = red In Irons, 1 =
 *  amber Close-Hauled, 2 = green reach/run or unknown). */
export function pointOfSailColorIndex(label: string): number {
  if (label === "In Irons") return 0;
  if (label === "Close-Hauled") return 1;
  return 2;
}

/** The point-of-sail status colour for sailing `headingDeg` in the given wind —
 *  used to tint the course preview by the expected speed of that heading. */
export function pointOfSailColor(headingDeg: number, wind: Wind): number {
  const off = angleDifference(headingDeg, wind.fromDegrees);
  return POINT_OF_SAIL_COLORS[pointOfSailColorIndex(pointOfSailFactor(off).pointOfSail)];
}

/** Computes the speed multiplier and classifies the point of sail. */
export function pointOfSailFactor(offWindAngle: number): PointOfSailResult {
  offWindAngle = Math.abs(offWindAngle);

  if (offWindAngle < Config.NoGoAngle) {
    const t = inverseLerp(0, Config.NoGoAngle, offWindAngle);
    return { pointOfSail: "In Irons", factor: lerp(Config.InIronsFactor, 0.45, t) };
  }

  if (offWindAngle < 75) {
    return {
      pointOfSail: "Close-Hauled",
      factor: lerp(0.45, 0.85, inverseLerp(Config.NoGoAngle, 75, offWindAngle)),
    };
  }

  if (offWindAngle < 115) {
    return {
      pointOfSail: "Beam Reach",
      factor: lerp(0.85, 1.0, inverseLerp(75, 100, offWindAngle)),
    };
  }

  if (offWindAngle < 150) {
    return {
      pointOfSail: "Broad Reach",
      factor: lerp(1.0, 0.9, inverseLerp(115, 150, offWindAngle)),
    };
  }

  return { pointOfSail: "Running", factor: lerp(0.9, 0.78, inverseLerp(150, 180, offWindAngle)) };
}
