// A lightweight tactical AI that commands an entire fleet — a port of Unity
// `AI/FleetAI.cs`. For each ship it picks the nearest enemy, manoeuvres to bring
// a broadside to bear, trims sail to the situation, and selects ammunition to
// match its intent.

import * as Config from "../core/config";
import { enemyOf, Faction } from "../core/faction";
import { normalize360, angleDifference, vectorToHeading } from "../core/nav";
import { distance, scale, sub } from "../core/vec";
import { AmmoType } from "../ships/ammo";
import { SailSetting } from "../ships/sail";
import type { Ship } from "../ships/ship";
import type { Wind } from "../combat/wind";

export class FleetAI {
  constructor(readonly faction: Faction) {}

  tick(ships: ReadonlyArray<Ship>, wind: Wind): void {
    for (const ship of ships) {
      if (!ship.isAlive || ship.faction !== this.faction) continue;
      this.commandShip(ship, ships, wind);
    }
  }

  private commandShip(ship: Ship, ships: ReadonlyArray<Ship>, wind: Wind): void {
    const found = this.nearestEnemy(ship, ships);
    const target = found.ship;

    if (!target) {
      // No enemies in sight: hold a steady reach across the wind.
      ship.setTargetHeading(normalize360(wind.fromDegrees + 90));
      ship.setSail(SailSetting.Reefed);
      return;
    }

    const bearing = vectorToHeading(sub(target.position, ship.position));
    let desiredHeading: number;

    if (found.dist > ship.stats.gunRange * 0.8) {
      // Out of effective range: close the distance under full sail.
      desiredHeading = bearing;
      ship.setSail(SailSetting.FullSail);
      ship.setAmmo(chooseAmmo(ship, target));
    } else {
      // In the killing zone: present a broadside and pound away.
      desiredHeading = broadsideHeading(ship, bearing);
      ship.setSail(SailSetting.Reefed);
      ship.setAmmo(chooseAmmo(ship, target));
    }

    desiredHeading = avoidNoGo(desiredHeading, wind);
    desiredHeading = avoidEdges(ship, desiredHeading);
    ship.setTargetHeading(desiredHeading);
  }

  private nearestEnemy(ship: Ship, ships: ReadonlyArray<Ship>): { ship: Ship | null; dist: number } {
    let dist = Number.MAX_VALUE;
    let best: Ship | null = null;
    const enemy = enemyOf(this.faction);

    for (const candidate of ships) {
      if (!candidate.isAlive || candidate.faction !== enemy) continue;
      const d = distance(ship.position, candidate.position);
      if (d < dist) {
        dist = d;
        best = candidate;
      }
    }

    return { ship: best, dist };
  }
}

function chooseAmmo(ship: Ship, target: Ship): AmmoType {
  // Cripple a fast, healthy runner so it cannot escape; otherwise pound the hull.
  if (target.riggingFraction > 0.6 && target.stats.topSpeed > ship.stats.topSpeed) {
    return AmmoType.BarShot;
  }
  return AmmoType.RoundShot;
}

function broadsideHeading(ship: Ship, bearingToTarget: number): number {
  const portOption = normalize360(bearingToTarget + 90);
  const starboardOption = normalize360(bearingToTarget - 90);
  const toPort = angleDifference(ship.headingDeg, portOption);
  const toStarboard = angleDifference(ship.headingDeg, starboardOption);
  return toPort <= toStarboard ? portOption : starboardOption;
}

function avoidNoGo(desiredHeading: number, wind: Wind): number {
  const off = angleDifference(desiredHeading, wind.fromDegrees);
  const limit = Config.NoGoAngle + 6;
  if (off >= limit) return desiredHeading;

  const tackA = normalize360(wind.fromDegrees + limit);
  const tackB = normalize360(wind.fromDegrees - limit);
  return angleDifference(desiredHeading, tackA) <= angleDifference(desiredHeading, tackB)
    ? tackA
    : tackB;
}

function avoidEdges(ship: Ship, desiredHeading: number): number {
  const edgeX = Config.ArenaHalfX * Config.EdgeTurnThreshold;
  const edgeZ = Config.ArenaHalfZ * Config.EdgeTurnThreshold;
  const p = ship.position;
  if (Math.abs(p.x) < edgeX && Math.abs(p.z) < edgeZ) return desiredHeading;
  return vectorToHeading(scale(p, -1));
}
