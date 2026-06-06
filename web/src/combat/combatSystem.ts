// Resolves broadside gunnery each frame — a port of Unity
// `Combat/CombatSystem.cs`. Guns are mounted along the hull sides, so a ship can
// only engage enemies roughly abeam (port / starboard arcs). Each broadside
// reloads independently and automatically fires at the best target in its arc.

import * as Config from "../core/config";
import { enemyOf } from "../core/faction";
import { lerp, clamp01 } from "../core/mathf";
import { type Vec2, add, scale, sub, magnitude, angle, dot } from "../core/vec";
import { rangeFloat, value } from "../core/rng";
import { ammoProfile } from "../ships/ammo";
import { Ship, BroadsideSide } from "../ships/ship";
import type { Effects } from "./effects";

/** Gold colour for the "RAKE" floating popup on a stern-raking broadside. */
const RAKE_TEXT_COLOR = 0xffcf40;

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
    // Fire EVERY gun in the broadside (no 8-gun cap) — a First Rate looses all
    // 32. Per-gun work is O(1) and target selection is resolved once per volley
    // (not per gun), so this stays linear in gun count.
    const guns = Math.max(1, shooter.stats.gunsPerBroadside);
    // ×4 if we're firing into the target's stern arc (geometry per shot is the
    // same, so resolve the rake once for the whole volley).
    const rake = sternRakeMultiplier(shooter, target);
    const raked = rake > 1;

    shooter.notifyFired(side);

    const forward = shooter.forward;
    const beamOffset = shooter.stats.beam * 0.6;
    const zBack = -shooter.stats.length * 0.28;
    const zFront = shooter.stats.length * 0.3;
    let anyHit = false;
    for (let g = 0; g < guns; g++) {
      const hit = value() < hitChance;
      if (hit) {
        anyHit = true;
        const spread = 1 + rangeFloat(-Config.DamageSpread, Config.DamageSpread);
        target.applyDamage(profile, Config.PerGunDamageScale * damageFalloff * spread * rake);
      }

      const t = guns === 1 ? 0.5 : g / (guns - 1);
      const origin = add(
        add(shooter.position, scale(normal, beamOffset)),
        scale(forward, lerp(zBack, zFront, t)),
      );
      effects.spawnProjectile(origin, impactPoint(target, hit, raked), profile.tracerColor);
    }

    // A stern-raking broadside that lands at least one ball gets a "RAKE" popup
    // on the target. Fires once per volley, broadsides only (never chase guns or
    // a normal abeam broadside, where `rake` is 1).
    if (rake > 1 && anyHit) {
      effects.spawnText(target.position, "RAKE", RAKE_TEXT_COLOR);
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
    const rake = sternRakeMultiplier(shooter, target);

    shooter.notifyChaseFired(bow);

    const right = shooter.right;
    const reach = shooter.stats.length * 0.45;
    for (let g = 0; g < guns; g++) {
      const hit = value() < hitChance;
      if (hit) {
        const spread = 1 + rangeFloat(-Config.DamageSpread, Config.DamageSpread);
        target.applyDamage(
          profile,
          Config.PerGunDamageScale * Config.ChaseDamageFactor * damageFalloff * spread * rake,
        );
      }

      const lateral = (guns === 1 ? 0 : g / (guns - 1) - 0.5) * shooter.stats.beam * 0.5;
      const origin = add(
        add(shooter.position, scale(normal, reach)),
        scale(right, lateral),
      );
      effects.spawnProjectile(origin, impactPoint(target, hit, rake > 1), profile.tracerColor);
    }
  }
}

// ---- Range-dependent gunnery model ----

/**
 * "Closeness" in [0,1]: 1 at point-blank, 0 at (or beyond) max range. The range
 * is floored at `MinEngagementRange` so two hulls touching/grinding alongside
 * can't reach the absolute point-blank peak and instantly melt each other (the
 * collision-stop pass keeps them apart, and this caps the multiplier as a
 * second guard).
 */
function closenessFrom(range: number, gunRange: number): number {
  const effective = Math.max(range, Config.MinEngagementRange);
  return clamp01(1 - clamp01(effective / gunRange));
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
 *
 * For a RAKED volley the balls land strung out ALONG the target's fore-aft (keel)
 * axis with only a little lateral scatter, so the volley visibly travels down the
 * length of the deck — reinforcing what a rake is — instead of clustering around
 * the centre.
 */
function impactPoint(target: Ship, hit: boolean, raked: boolean): Vec2 {
  if (raked) return rakeImpactPoint(target, hit);
  const radius = hit
    ? target.stats.beam * Config.HitScatterFactor
    : target.stats.length * Config.MissScatterFactor;
  return scatterAround(target.position, radius);
}

/**
 * Impact point for a raked ball: spread along the target's keel (fore→aft) over
 * the hull length, with a small beam-wise jitter, so raking tracers sweep the
 * deck end to end. A hit stays within the beam; a miss can splash just outside.
 */
function rakeImpactPoint(target: Ship, hit: boolean): Vec2 {
  const fwd = target.forward; // unit bow direction
  const along = rangeFloat(-0.5, 0.5) * target.stats.length; // anywhere down the hull
  const lateralRadius = target.stats.beam * (hit ? Config.HitScatterFactor : 0.9);
  const lateral = rangeFloat(-lateralRadius, lateralRadius);
  return {
    x: target.position.x + fwd.x * along - fwd.z * lateral,
    z: target.position.z + fwd.z * along + fwd.x * lateral,
  };
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

    // Can't fire through our own ships: skip any enemy whose line of fire is
    // blocked by a friendly hull between us and them. Checked last as it's the
    // most expensive test.
    if (lineOfFireBlocked(shooter, candidate, ships)) continue;

    if (dist < bestRange) {
      bestRange = dist;
      best = candidate;
    }
  }

  return { target: best, range: bestRange };
}

/**
 * Stern-rake multiplier: SternRakeMultiplier when the line of fire runs down the
 * length of the target's hull (a true rake into its stern), else 1.
 *
 * A rake is decided by the TARGET's orientation relative to the shooter→target
 * line, NOT by the shooter's heading: the target must be presenting its stern so
 * the balls sweep the full fore-aft length of the deck. `target.forward` is the
 * target's bow (unit) direction, so `astern = -forward` points dead astern.
 * `toShooter = shooter - target` points from the target back toward whoever is
 * firing; when it is within the tight `SternRakeArcHalfAngle` of `astern`, the
 * shooter is dead behind the target and the line of fire runs along the keel —
 * the classic stern rake. A normal broadside (shooter abeam) puts `toShooter`
 * ~90° off astern, far outside the cone, so it is never boosted. (Bow-on shots —
 * `toShooter` aligned with `forward` — are intentionally NOT raked; only the
 * stern counts.)
 */
function sternRakeMultiplier(shooter: Ship, target: Ship): number {
  const toShooter = sub(shooter.position, target.position);
  const astern = scale(target.forward, -1);
  return angle(astern, toShooter) <= Config.SternRakeArcHalfAngle
    ? Config.SternRakeMultiplier
    : 1;
}

/**
 * True if a friendly ship (same faction as `shooter`, excluding the shooter)
 * sits on the line of fire between `shooter` and `candidate`, occluding the shot.
 *
 * Each friendly hull is treated as a circle of radius ≈ its beam. We project the
 * friendly onto the shooter→candidate segment: `t` is its distance along the line
 * of fire. Only friendlies whose projection falls strictly BETWEEN shooter and
 * target (0 < t < segLen) can block — one off to the side, behind the shooter, or
 * beyond the target cannot. If such a friendly's perpendicular distance to the
 * line is within its radius, the line of fire is blocked.
 */
function lineOfFireBlocked(
  shooter: Ship,
  candidate: Ship,
  ships: ReadonlyArray<Ship>,
): boolean {
  const seg = sub(candidate.position, shooter.position);
  const segLen = magnitude(seg);
  if (segLen < 1e-6) return false;
  const dir = scale(seg, 1 / segLen);

  for (const friendly of ships) {
    if (friendly === shooter || !friendly.isAlive || friendly.faction !== shooter.faction) {
      continue;
    }
    const toFriendly = sub(friendly.position, shooter.position);
    const t = dot(toFriendly, dir); // distance along the line of fire
    if (t <= 0 || t >= segLen) continue; // beside/behind shooter, or beyond target
    const perp = magnitude(sub(toFriendly, scale(dir, t)));
    const radius = friendly.stats.beam * Config.FriendlyBlockRadiusFactor;
    if (perp <= radius) return true;
  }
  return false;
}
