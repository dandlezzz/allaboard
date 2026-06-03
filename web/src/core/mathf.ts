// Unity `Mathf` helpers, ported so the gameplay logic translates 1:1 from C#.
// Only the helpers the simulation actually uses are provided.

export const Deg2Rad = Math.PI / 180;
export const Rad2Deg = 180 / Math.PI;

export function clamp(value: number, min: number, max: number): number {
  if (value < min) return min;
  if (value > max) return max;
  return value;
}

export function clamp01(value: number): number {
  return clamp(value, 0, 1);
}

export function lerp(a: number, b: number, t: number): number {
  return a + (b - a) * clamp01(t);
}

/** Unclamped linear interpolation. */
export function lerpUnclamped(a: number, b: number, t: number): number {
  return a + (b - a) * t;
}

export function inverseLerp(a: number, b: number, value: number): number {
  if (a === b) return 0;
  return clamp01((value - a) / (b - a));
}

/** Moves `current` toward `target` by at most `maxDelta`. */
export function moveTowards(current: number, target: number, maxDelta: number): number {
  if (Math.abs(target - current) <= maxDelta) return target;
  return current + Math.sign(target - current) * maxDelta;
}

export function smoothStep(from: number, to: number, t: number): number {
  t = clamp01((t - from) / (to - from || 1));
  return t * t * (3 - 2 * t);
}

export function sign(value: number): number {
  // Unity's Mathf.Sign returns 1 for 0, matching the edge-turn logic.
  return value >= 0 ? 1 : -1;
}

export function roundToInt(value: number): number {
  return Math.round(value);
}

export function floorToInt(value: number): number {
  return Math.floor(value);
}

export function ceilToInt(value: number): number {
  return Math.ceil(value);
}
