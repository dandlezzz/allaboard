// A small seeded RNG replacing UnityEngine.Random. A single shared instance is
// used across the simulation so a battle is reproducible from its seed.

let state = 0x9e3779b9 >>> 0;

/** Reseeds the global RNG. */
export function seed(value: number): void {
  state = value >>> 0;
  if (state === 0) state = 0x9e3779b9;
}

/** Mulberry32 — fast, decent-quality 32-bit PRNG. Returns [0, 1). */
function next(): number {
  state = (state + 0x6d2b79f5) >>> 0;
  let t = state;
  t = Math.imul(t ^ (t >>> 15), t | 1);
  t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
  return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
}

/** Unity's `Random.Range(min, max)` for floats — inclusive min, exclusive max. */
export function rangeFloat(min: number, max: number): number {
  return min + next() * (max - min);
}

/** Random value in [0, 1). */
export function value(): number {
  return next();
}
