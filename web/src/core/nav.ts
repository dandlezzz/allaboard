// Navigation / heading maths shared across the simulation — a direct port of
// Unity `Core/Nav.cs`.
//
// Headings are compass-style degrees measured clockwise from +Z ("north"):
// 0 = +Z, 90 = +X, 180 = -Z, 270 = -X.

import { clamp, Deg2Rad, Rad2Deg } from "./mathf";
import type { Vec2 } from "./vec";

/** Converts a compass heading in degrees to a unit direction on the XZ plane. */
export function headingToVector(headingDeg: number): Vec2 {
  const r = headingDeg * Deg2Rad;
  return { x: Math.sin(r), z: Math.cos(r) };
}

/** Converts an XZ direction to a compass heading in degrees in [0, 360). */
export function vectorToHeading(dir: Vec2): number {
  const deg = Math.atan2(dir.x, dir.z) * Rad2Deg;
  return normalize360(deg);
}

/** Wraps an angle to the range [0, 360). */
export function normalize360(deg: number): number {
  deg %= 360;
  if (deg < 0) deg += 360;
  return deg;
}

/** Wraps an angle to the range (-180, 180]. */
export function normalize180(deg: number): number {
  deg = normalize360(deg);
  if (deg > 180) deg -= 360;
  return deg;
}

/** Absolute smallest angle between two headings, in [0, 180]. */
export function angleDifference(a: number, b: number): number {
  return Math.abs(normalize180(a - b));
}

/** Signed shortest delta to steer from `from` toward `to`, in (-180, 180]. */
export function signedDelta(from: number, to: number): number {
  return normalize180(to - from);
}

/** Rotates `current` toward `target` by at most `maxDeg`. */
export function moveTowardsAngle(current: number, target: number, maxDeg: number): number {
  const delta = signedDelta(current, target);
  const step = clamp(delta, -maxDeg, maxDeg);
  return normalize360(current + step);
}
