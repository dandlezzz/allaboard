// A sink for purely-cosmetic combat effects, implemented by the rendering layer.
// Keeping it an interface lets the engine-agnostic combat code stay free of any
// PixiJS dependency (mirroring how Unity's CombatSystem spawned Projectiles).

import type { Vec2 } from "../core/vec";

/** Outcome a tracer carries so the renderer can play the right impact sound when
 *  the ball LANDS: a hull hit (thud) vs a near-miss water splash. */
export type ImpactKind = "hit" | "splash";

export interface Effects {
  /**
   * Spawns a cannon tracer travelling from `origin` to `target`. `impact` (the
   * gunnery hit/miss outcome) lets the renderer play a hull-thud vs a splash
   * when the tracer arrives; omit it for non-combat tracers.
   */
  spawnProjectile(origin: Vec2, target: Vec2, color: number, impact?: ImpactKind): void;

  /** Spawns a floating text popup (e.g. "RAKE") at `pos` that rises and fades. */
  spawnText(pos: Vec2, text: string, color: number): void;

  /** Plays a one-shot sound effect by name (e.g. "cannon"). Engine-agnostic:
   *  the renderer routes it to the Web Audio engine; headless sinks no-op. */
  playSound(name: string): void;
}

/** A no-op sink, useful for headless logic / tests. */
export const NullEffects: Effects = {
  spawnProjectile() {
    /* no-op */
  },
  spawnText() {
    /* no-op */
  },
  playSound() {
    /* no-op */
  },
};
