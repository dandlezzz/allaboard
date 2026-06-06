// The shot loaded into a ship's guns — a port of Unity `Ships/AmmoType.cs`,
// trimmed to two shot types for the web build.

export enum AmmoType {
  /** Solid round shot: primarily smashes the hull to sink the enemy, with some
   *  secondary rigging damage. */
  RoundShot = 0,
  /** Bar / chain shot: primarily shreds rigging and sails, with some secondary
   *  hull damage. */
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

// Each shot type is a BLEND, not all-or-nothing: it does full damage to its
// specialty and a clearly-secondary amount to the other. The specialist values
// below are the "100%" for that resource — round shot owns the hull, bar shot
// owns the rigging — and the off-specialty hit is SECONDARY_FRACTION of the
// OTHER type's specialist value, so cross-damage is measured on the right scale
// (hull damage vs rigging damage are not the same magnitude). Keeping the
// specialist values unchanged preserves the existing balance; only the small
// secondary chip is new.
const ROUND_HULL = 7.5; // round shot's full hull-smashing power
const BAR_RIGGING = 20.0; // bar shot's full rigging-shredding power
const SECONDARY_FRACTION = 0.2; // ~20%: meaningful, but clearly secondary

const PROFILES: Record<AmmoType, AmmoProfile> = {
  [AmmoType.RoundShot]: {
    // Primarily the hull; also chips sails/rigging at ~20% of bar shot's rate.
    hullDamage: ROUND_HULL,
    riggingDamage: BAR_RIGGING * SECONDARY_FRACTION,
    tracerColor: 0x262626,
    label: "Round Shot",
    reloadFactor: 1.0,
    accuracyBonus: 0,
  },
  [AmmoType.BarShot]: {
    // Primarily the rigging; also splinters the hull at ~20% of round shot's rate.
    hullDamage: ROUND_HULL * SECONDARY_FRACTION,
    riggingDamage: BAR_RIGGING,
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
