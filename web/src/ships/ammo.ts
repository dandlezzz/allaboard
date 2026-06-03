// The shot loaded into a ship's guns — a port of Unity `Ships/AmmoType.cs`,
// trimmed to two shot types for the web build.

export enum AmmoType {
  /** Solid round shot: smashes the hull, sinking the enemy. */
  RoundShot = 0,
  /** Bar / chain shot: shreds rigging and sails. */
  BarShot = 1,
}

export interface AmmoProfile {
  hullDamage: number;
  riggingDamage: number;
  /** Tracer colour (0xRRGGBB) for the projectile / muzzle effect. */
  tracerColor: number;
  label: string;
  /** Multiplier on reload time (<1 = fires more often). */
  reloadFactor: number;
  /** Flat bonus added to per-ball hit chance (more accurate). */
  accuracyBonus: number;
}

const PROFILES: Record<AmmoType, AmmoProfile> = {
  [AmmoType.RoundShot]: {
    hullDamage: 7.5,
    riggingDamage: 1.0,
    tracerColor: 0x262626,
    label: "Round Shot",
    reloadFactor: 1.0,
    accuracyBonus: 0,
  },
  [AmmoType.BarShot]: {
    hullDamage: 1.0,
    riggingDamage: 20.0,
    tracerColor: 0x8c8073,
    label: "Bar Shot",
    reloadFactor: 0.6,
    accuracyBonus: 0.15,
  },
};

export function ammoProfile(type: AmmoType): AmmoProfile {
  return PROFILES[type];
}

/** Cycles to the next ammo type, toggling Round ↔ Bar. */
export function nextAmmo(type: AmmoType): AmmoType {
  return ((type + 1) % 2) as AmmoType;
}

export function ammoLabel(type: AmmoType): string {
  return PROFILES[type].label;
}
