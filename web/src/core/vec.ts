// A tiny 2D vector shim standing in for the parts of UnityEngine.Vector3 the
// simulation uses. The game lives on the world XZ plane (Unity's Y/"up" is
// irrelevant top-down), so we model a world point as { x, z } — keeping the
// field names identical to the Unity source so the ports read 1:1.

import { Rad2Deg } from "./mathf";

export interface Vec2 {
  x: number;
  z: number;
}

export function v(x: number, z: number): Vec2 {
  return { x, z };
}

export function add(a: Vec2, b: Vec2): Vec2 {
  return { x: a.x + b.x, z: a.z + b.z };
}

export function sub(a: Vec2, b: Vec2): Vec2 {
  return { x: a.x - b.x, z: a.z - b.z };
}

export function scale(a: Vec2, s: number): Vec2 {
  return { x: a.x * s, z: a.z * s };
}

export function dot(a: Vec2, b: Vec2): number {
  return a.x * b.x + a.z * b.z;
}

export function magnitude(a: Vec2): number {
  return Math.sqrt(a.x * a.x + a.z * a.z);
}

export function sqrMagnitude(a: Vec2): number {
  return a.x * a.x + a.z * a.z;
}

export function distance(a: Vec2, b: Vec2): number {
  return magnitude(sub(a, b));
}

export function normalize(a: Vec2): Vec2 {
  const m = magnitude(a);
  return m > 1e-6 ? { x: a.x / m, z: a.z / m } : { x: 0, z: 0 };
}

/** Unsigned angle (degrees) between two vectors — Unity's Vector3.Angle. */
export function angle(a: Vec2, b: Vec2): number {
  const denom = magnitude(a) * magnitude(b);
  if (denom < 1e-9) return 0;
  const c = Math.max(-1, Math.min(1, dot(a, b) / denom));
  return Math.acos(c) * Rad2Deg;
}
