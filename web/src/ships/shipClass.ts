// Rated classes of sailing warship and their immutable tuning stats — a port of
// Unity `Ships/ShipClassDef.cs`.

import * as Config from "../core/config";

export enum ShipClass {
  /** Fast, lightly-armed scout / raider. */
  Frigate = 0,
  /** Workhorse 74-gun ship-of-the-line. */
  ThirdRate = 1,
  /** Massive 100+ gun flagship: slow, ponderous, devastating. */
  FirstRate = 2,
}

export interface ShipStats {
  shipClass: ShipClass;
  displayName: string;
  /** Guns per broadside (one side), capped at 8. */
  gunsPerBroadside: number;
  /** Fore/aft chase guns (weak). */
  chaseGuns: number;
  maxHull: number;
  maxRigging: number;
  /** Top speed in world units / second at the optimal point of sail. */
  topSpeed: number;
  /** Best-case turn rate in degrees / second. */
  turnRate: number;
  acceleration: number;
  gunRange: number;
  reloadTime: number;
  length: number;
  beam: number;
}

/** Tuning stats for a given ship class — see Unity `ShipCatalog.Stats`. */
export function shipStats(shipClass: ShipClass): ShipStats {
  const s = Config.ShipScale;
  const speedScale = s * Config.BaseSpeedMultiplier;
  const rangeScale = s * Config.BaseRangeMultiplier;
  const hullScale = s * Config.HullSizeBoost;
  const beamScale = hullScale * 0.9;

  switch (shipClass) {
    case ShipClass.Frigate:
      return {
        shipClass: ShipClass.Frigate,
        displayName: "Frigate",
        gunsPerBroadside: 8,
        chaseGuns: 2,
        maxHull: 70,
        maxRigging: 60,
        topSpeed: 6.0 * speedScale,
        turnRate: 13.5,
        acceleration: 2.2 * speedScale,
        gunRange: 30 * rangeScale,
        reloadTime: 6,
        length: 6.25 * hullScale,
        beam: 1.6 * beamScale,
      };
    case ShipClass.ThirdRate:
      return {
        shipClass: ShipClass.ThirdRate,
        displayName: "Third Rate (74)",
        gunsPerBroadside: 20,
        chaseGuns: 2,
        maxHull: 130,
        maxRigging: 90,
        topSpeed: 5.0 * speedScale,
        turnRate: 8.7,
        acceleration: 1.3 * speedScale,
        gunRange: 36 * rangeScale,
        reloadTime: 8,
        length: 7.0 * hullScale,
        beam: 1.9 * beamScale,
      };
    case ShipClass.FirstRate:
      return {
        shipClass: ShipClass.FirstRate,
        displayName: "First Rate (100+)",
        gunsPerBroadside: 32,
        chaseGuns: 2,
        maxHull: 190,
        maxRigging: 120,
        topSpeed: 4.5 * speedScale,
        turnRate: 6,
        acceleration: 0.9 * speedScale,
        gunRange: 40 * rangeScale,
        reloadTime: 10,
        length: 8.5 * hullScale,
        beam: 2.3 * beamScale,
      };
    default:
      return shipStats(ShipClass.ThirdRate);
  }
}
