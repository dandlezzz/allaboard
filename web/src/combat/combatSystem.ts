// Resolves broadside gunnery each frame — a port of Unity
// `Combat/CombatSystem.cs`. Guns are mounted along the hull sides, so a ship can
// only engage enemies roughly abeam (port / starboard arcs). Each broadside
// reloads independently and automatically fires at the best target in its arc.

import * as Config from "../core/config";
import { enemyOf } from "../core/faction";
import { lerp, clamp01 } from "../core/mathf";
import { type Vec2, add, scale, sub, magnitude, angle } from "../core/vec";
import { rangeFloat, value } from "../core/rng";
import { ammoProfile } from "../ships/ammo";
import { Ship, BroadsideSide } from "../ships/ship";
import type { Effects } from "./effects";

export class CombatSystem {
  tick(ships: ReadonlyArray<Ship>, effects: Effects): void {
    for (const shooter of ships) {
      if (!shooter.isAlive) continue;
      this.tryFireBroadside(shooter, BroadsideSide.Port, ships, effects);
      this.tryFireBroadside(shooter, BroadsideSide.Starboard, ships, effects);
      this.tryFireChase(shooter, true, ships, effects);
      this.tryFireChase(shooter, false, ships, effects);
    }
  }

  private tryFireBroadside(
    shooter: Ship,
    side: BroadsideSide,
    ships: ReadonlyArray<Ship>,
    effects: Effects,
  ): void {
    if (!shooter.isBroadsideReady(side)) return;

    const normal = shooter.broadsideNormal(side);
    const result = findBestTarget(shooter, normal, Config.BroadsideArcHalfAngle, ships);
    if (!result.target) return;

    const target = result.target;
    const profile = ammoProfile(shooter.ammo);
    const closeness = closenessFrom(result.range, shooter.stats.gunRange);
    const damageFalloff = damageFalloffAt(closeness);
    const hitChance = clamp01(hitChanceAt(closeness) + profile.accuracyBonus);
    const guns = Math.min(8, Math.max(1, shooter.stats.gunsPerBroadside));

    shooter.notifyFired(side);

    const forward = shooter.forward;
    const beamOffset = shooter.stats.beam * 0.6;
    const zBack = -shooter.stats.length * 0.28;
    const zFront = shooter.stats.length * 0.3;
    for (let g = 0; g < guns; g++) {
      const hit = value() < hitChance;
      if (hit) {
        const spread = 1 + rangeFloat(-Config.DamageSpread, Config.DamageSpread);
        target.applyDamage(profile, Config.PerGunDamageScale * damageFalloff * spread);
      }

      const t = guns === 1 ? 0.5 : g / (guns - 1);
      const origin = add(
        add(shooter.position, scale(normal, beamOffset)),
        scale(forward, lerp(zBack, zFront, t)),
      );
      effects.spawnProjectile(origin, impactPoint(target, hit), profile.tracerColor);
    }
  }

  private tryFireChase(
    shooter: Ship,
    bow: boolean,
    ships: ReadonlyArray<Ship>,
    effects: Effects,
  ): void {
    if (shooter.stats.chaseGuns <= 0 || !shooter.isChaseReady(bow)) return;

    const normal = shooter.chaseNormal(bow);
    const result = findBestTarget(shooter, normal, Config.ChaseArcHalfAngle, ships);
    if (!result.target) return;

    const target = result.target;
    const profile = ammoProfile(shooter.ammo);
    const closeness = closenessFrom(result.range, shooter.stats.gunRange);
    const damageFalloff = damageFalloffAt(closeness);
    const hitChance = clamp01(hitChanceAt(closeness) + profile.accuracyBonus);
    const guns = Math.max(1, shooter.stats.chaseGuns);

    shooter.notifyChaseFired(bow);

    const right = shooter.right;
    const reach = shooter.stats.length * 0.45;
    for (let g = 0; g < guns; g++) {
      const hit = value() < hitChance;
      if (hit) {
        const spread = 1 + rangeFloat(-Config.DamageSpread, Config.DamageSpread);
        target.applyDamage(
          profile,
          Config.PerGunDamageScale * Config.ChaseDamageFactor * damageFalloff * spread,
        );
      }

      const lateral = (guns === 1 ? 0 : g / (guns - 1) - 0.5) * shooter.stats.beam * 0.5;
      const origin = add(
        add(shooter.position, scale(normal, reach)),
        scale(right, lateral),
      );
      effects.spawnProjectile(origin, impactPoint(target, hit), profile.tracerColor);
    }
  }
}

// ---- Range-dependent gunnery model ----

/** "Closeness" in [0,1]: 1 at point-blank, 0 at (or beyond) max range. */
function closenessFrom(range: number, gunRange: number): number {
  return clamp01(1 - clamp01(range / gunRange));
}

/** Per-ball damage multiplier: peaks at point-blank, falls to the floor at range. */
function damageFalloffAt(closeness: number): number {
  return (
    Config.RangeFalloffFloor +
    (Config.PointBlankDamageMultiplier - Config.RangeFalloffFloor) *
      Math.pow(closeness, Config.DamageFalloffExponent)
  );
}

/** Per-ball hit chance: high at point-blank, low at max range. */
function hitChanceAt(closeness: number): number {
  return (
    Config.HitChanceMaxRange +
    (Config.HitChancePointBlank - Config.HitChanceMaxRange) *
      Math.pow(closeness, Config.HitChanceExponent)
  );
}

/**
 * Where a ball's tracer ends. A hit lands tight on the hull; a miss lands in a
 * wider scatter around the ship so it reads as a near-miss splash (no damage).
 */
function impactPoint(target: Ship, hit: boolean): Vec2 {
  const radius = hit
    ? target.stats.beam * Config.HitScatterFactor
    : target.stats.length * Config.MissScatterFactor;
  return scatterAround(target.position, radius);
}

function scatterAround(center: Vec2, radius: number): Vec2 {
  return { x: center.x + rangeFloat(-radius, radius), z: center.z + rangeFloat(-radius, radius) };
}

interface TargetResult {
  target: Ship | null;
  range: number;
}

function findBestTarget(
  shooter: Ship,
  normal: Vec2,
  arcHalfAngle: number,
  ships: ReadonlyArray<Ship>,
): TargetResult {
  let bestRange = Number.MAX_VALUE;
  let best: Ship | null = null;
  const enemy = enemyOf(shooter.faction);

  for (const candidate of ships) {
    if (candidate === shooter || !candidate.isAlive || candidate.faction !== enemy) continue;

    const to = sub(candidate.position, shooter.position);
    const dist = magnitude(to);
    if (dist > shooter.stats.gunRange || dist < 0.001) continue;

    if (angle(normal, to) > arcHalfAngle) continue;

    if (dist < bestRange) {
      bestRange = dist;
      best = candidate;
    }
  }

  return { target: best, range: bestRange };
}
