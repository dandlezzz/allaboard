// A sink for purely-cosmetic combat effects, implemented by the rendering layer.
// Keeping it an interface lets the engine-agnostic combat code stay free of any
// PixiJS dependency (mirroring how Unity's CombatSystem spawned Projectiles).

import type { Vec2 } from "../core/vec";

export interface Effects {
  /** Spawns a cannon tracer travelling from `origin` to `target`. */
  spawnProjectile(origin: Vec2, target: Vec2, color: number): void;
}

/** A no-op sink, useful for headless logic / tests. */
export const NullEffects: Effects = {
  spawnProjectile() {
    /* no-op */
  },
};
