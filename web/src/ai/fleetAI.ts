// A lightweight tactical AI that commands an entire fleet — a port of Unity
// `AI/FleetAI.cs`, extended with selectable PERSONAS:
//
//   • Standard   — the original behaviour: close, present a broadside, trim sail.
//   • Turtle     — furls every sail (Heave To) and never moves; guns still auto-
//                  fire via the combat system at anything that wanders into arc.
//   • Tactician  — a smarter AI: keeps loose line-ahead cohesion, manoeuvres to
//                  rake enemy STERNS (the ×4 bonus), avoids exposing its own
//                  stern, and uses the wind / points of sail to keep speed.

import * as Config from "../core/config";
import { enemyOf, Faction } from "../core/faction";
import { normalize360, angleDifference, signedDelta, vectorToHeading, headingToVector } from "../core/nav";
import { add, distance, scale, sub, angle, type Vec2 } from "../core/vec";
import { AmmoType } from "../ships/ammo";
import { SailSetting } from "../ships/sail";
import type { Ship } from "../ships/ship";
import type { Wind } from "../combat/wind";

/** Which opponent "brain" the AI fleet runs. */
export enum AIPersona {
  Standard = 0,
  Turtle = 1,
  Tactician = 2,
}

/** Degrees added to NoGoAngle to pick the close-hauled "best upwind" heading (a
 *  small margin so the ship sails just clear of the no-go zone, not on its edge). */
const TACK_CLOSE_HAULED_MARGIN = 6;
/** Minimum |target-bearing off the wind| (deg) before a tack switch is allowed —
 *  keeps a near-dead-upwind target from making the ship flip-flap tacks. */
const TACK_SWITCH_MARGIN = 8;
/** Minimum AI ticks committed to a tack before switching (hysteresis), so the
 *  ship holds each tack and zig-zags instead of tacking every frame. */
const MIN_TACK_TICKS = 90;

/** Per-ship tacking state: which tack we're on (+1/-1 = the wind-relative side of
 *  the close-hauled heading) and how many ticks we've held it. */
interface TackState {
  sign: number;
  ticks: number;
}

export class FleetAI {
  /** Mutable so a fresh game can be started against a different persona. */
  persona: AIPersona;

  // Per-ship tacking state (Standard persona), keyed by ship id. Lets each ship
  // commit to a tack and only come about at the layline (with hysteresis).
  private readonly tackState = new Map<number, TackState>();

  constructor(
    readonly faction: Faction,
    persona: AIPersona = AIPersona.Standard,
  ) {
    this.persona = persona;
  }

  tick(ships: ReadonlyArray<Ship>, wind: Wind): void {
    switch (this.persona) {
      case AIPersona.Turtle:
        this.tickTurtle(ships);
        return;
      case AIPersona.Tactician:
        this.tickTactician(ships, wind);
        return;
      default:
        this.tickStandard(ships, wind);
        return;
    }
  }

  // ---- Standard (closes, broadsides, trims — and now TACKS upwind) ------

  private tickStandard(ships: ReadonlyArray<Ship>, wind: Wind): void {
    for (const ship of ships) {
      if (!ship.isAlive || ship.faction !== this.faction) continue;
      this.commandShipStandard(ship, ships, wind);
    }
  }

  private commandShipStandard(ship: Ship, ships: ReadonlyArray<Ship>, wind: Wind): void {
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
      // Out of effective range: close under full sail. If the target is upwind
      // (its bearing is in the no-go zone), TACK toward it (zig-zag close-hauled)
      // instead of steering straight into irons; otherwise steer right at it.
      ship.setSail(SailSetting.FullSail);
      ship.setAmmo(chooseAmmo(ship, target));
      desiredHeading = this.headingTowardBearing(ship, bearing, wind);
    } else {
      // In the killing zone: present a broadside and pound away. A broadside is
      // ~90° off the bearing (a beam reach) so it's normally sailable; the
      // avoidNoGo clamp is a safety net and we drop any tack state.
      this.tackState.delete(ship.id);
      desiredHeading = avoidNoGo(broadsideHeading(ship, bearing), wind);
      ship.setSail(SailSetting.Reefed);
      ship.setAmmo(chooseAmmo(ship, target));
    }

    desiredHeading = avoidEdges(ship, desiredHeading);
    ship.setTargetHeading(desiredHeading);
  }

  /**
   * Heading for a ship trying to reach `bearing`. If that bearing is SAILABLE
   * (outside the no-go zone) steer straight at it (and clear any tack state). If
   * it's upwind (within `NoGoAngle` of the wind's "from" direction), the ship
   * can't sail there directly, so tack toward it instead (see {@link tackUpwind}).
   */
  private headingTowardBearing(ship: Ship, bearing: number, wind: Wind): number {
    if (angleDifference(bearing, wind.fromDegrees) >= Config.NoGoAngle) {
      this.tackState.delete(ship.id);
      return bearing;
    }
    return this.tackUpwind(ship, bearing, wind);
  }

  /**
   * Stateful tacking toward an UPWIND target. The two tacks are the close-hauled
   * headings `wind ± (NoGoAngle + margin)`; `sign` (+1/-1) selects the side. We
   * favour the tack on the target's side of the wind (the one that points more
   * toward it), and only come about when we've OVERSTOOD — the target's bearing
   * has crossed to the OTHER side of the wind axis (the layline) — gated by a
   * minimum hold (`MIN_TACK_TICKS`) and a small angle margin (`TACK_SWITCH_MARGIN`)
   * so the ship commits to each tack and zig-zags upwind instead of flip-flapping.
   */
  private tackUpwind(ship: Ship, bearing: number, wind: Wind): number {
    const limit = Config.NoGoAngle + TACK_CLOSE_HAULED_MARGIN;
    // Signed offset of the target from dead-upwind: >0 one side, <0 the other.
    const bearingOffWind = signedDelta(wind.fromDegrees, bearing);
    const favored = bearingOffWind >= 0 ? 1 : -1; // tack that points toward the target

    let st = this.tackState.get(ship.id);
    if (!st) {
      st = { sign: favored, ticks: 0 };
      this.tackState.set(ship.id, st);
    }
    st.ticks++;

    // Come about only once we've clearly overstood (target now on the other side)
    // AND we've held this tack long enough — prevents rapid flip-flapping that
    // would kill speed, especially for a near-dead-upwind target.
    if (
      st.sign !== favored &&
      Math.abs(bearingOffWind) > TACK_SWITCH_MARGIN &&
      st.ticks >= MIN_TACK_TICKS
    ) {
      st.sign = favored;
      st.ticks = 0;
    }

    return normalize360(wind.fromDegrees + st.sign * limit);
  }

  // ---- Turtle -----------------------------------------------------------

  /** Furl all canvas and sit still the whole game. No course/sail-up orders. */
  private tickTurtle(ships: ReadonlyArray<Ship>): void {
    for (const ship of ships) {
      if (!ship.isAlive || ship.faction !== this.faction) continue;
      ship.setSail(SailSetting.HeaveTo); // throttle 0 ⇒ ship makes no way
    }
  }

  // ---- Tactician --------------------------------------------------------

  private tickTactician(ships: ReadonlyArray<Ship>, wind: Wind): void {
    const own = ships
      .filter((s) => s.isAlive && s.faction === this.faction)
      .sort((a, b) => a.id - b.id); // stable order ⇒ index 0 is the flagship/leader
    if (own.length === 0) return;

    const enemy = enemyOf(this.faction);
    const foes = ships.filter((s) => s.isAlive && s.faction === enemy);

    for (let i = 0; i < own.length; i++) {
      this.commandShipTactician(own[i], i, own, foes, wind);
    }
  }

  /**
   * Tactician per-ship logic. Priority each tick:
   *   0. No enemies → hold a tidy reach in formation.
   *   1. Someone is on OUR stern within range → wheel beam-on to them (stop being
   *      raked AND bring guns to bear).
   *   2. We're already in the target's stern arc → present a broadside and RAKE.
   *   3. Closing → steer for the target's stern quarter (biased to windward for
   *      the weather gauge), with line-ahead cohesion pulling followers into the
   *      column so the squadron advances together instead of scattering.
   * Sail is kept full while manoeuvring (turn rate scales with speed) and reefed
   * only once settled into a raking broadside. Headings are clamped out of the
   * no-go zone and away from the arena edge.
   */
  private commandShipTactician(
    ship: Ship,
    index: number,
    own: ReadonlyArray<Ship>,
    foes: ReadonlyArray<Ship>,
    wind: Wind,
  ): void {
    // Pick a focus enemy: prefer one we can already rake (we're in its stern arc
    // and within range), else the nearest.
    let nearest: Ship | null = null;
    let nearestDist = Number.MAX_VALUE;
    let rakeable: Ship | null = null;
    let rakeableDist = Number.MAX_VALUE;
    let sternThreat: Ship | null = null; // foe sitting in OUR stern arc
    let sternThreatDist = Number.MAX_VALUE;
    const range = ship.stats.gunRange;

    for (const foe of foes) {
      const d = distance(ship.position, foe.position);
      if (d < nearestDist) {
        nearestDist = d;
        nearest = foe;
      }
      if (d < range * 1.05 && isAstern(foe, ship.position) && d < rakeableDist) {
        rakeableDist = d;
        rakeable = foe;
      }
      if (d < range * 1.1 && isAstern(ship, foe.position) && d < sternThreatDist) {
        sternThreatDist = d;
        sternThreat = foe;
      }
    }

    const focus = rakeable ?? nearest;

    let desiredHeading: number;
    let sail: SailSetting;

    if (!focus) {
      // No enemies: hold a steady reach, followers fall in behind the leader.
      desiredHeading = index === 0 ? normalize360(wind.fromDegrees + 90) : own[index - 1].headingDeg;
      sail = SailSetting.Reefed;
    } else if (sternThreat) {
      // Being raked from astern — turn beam-on to the threat (defend + fire),
      // keeping full sail so the turn actually carries through.
      const bearing = vectorToHeading(sub(sternThreat.position, ship.position));
      desiredHeading = broadsideHeading(ship, bearing);
      sail = SailSetting.FullSail;
      ship.setAmmo(chooseAmmo(ship, sternThreat));
    } else {
      const dist = distance(ship.position, focus.position);
      const engaging = dist < range * 1.15;
      const bearing = vectorToHeading(sub(focus.position, ship.position));

      if (engaging && isAstern(focus, ship.position)) {
        // We're behind them: settle into a broadside to rake the stern.
        desiredHeading = broadsideHeading(ship, bearing);
        sail = SailSetting.Reefed;
      } else {
        // Close on the target's stern quarter (biased upwind for the weather
        // gauge); followers blend toward their line-ahead station for cohesion.
        const windSource = headingToVector(wind.fromDegrees); // points upwind
        const sternQuarter = add(
          sub(focus.position, scale(focus.forward, focus.stats.length * 1.1)),
          scale(windSource, focus.stats.length * 0.8),
        );
        let aim = sternQuarter;
        if (index > 0) {
          const ahead = own[index - 1];
          const station = sub(
            ahead.position,
            scale(ahead.forward, ahead.stats.length * 1.6 + Config.ColumnGap),
          );
          // Far from station (out of formation) → rejoin before pressing the attack.
          if (distance(ship.position, station) > ship.stats.length * 2 && !engaging) {
            aim = station;
          }
        }
        desiredHeading = vectorToHeading(sub(aim, ship.position));
        sail = SailSetting.FullSail;
      }

      ship.setAmmo(chooseAmmo(ship, focus));
    }

    // If nearly stalled, carry full canvas to rebuild speed (a hove-to ship can't
    // turn at all — turn rate scales with speed).
    if (ship.speed < ship.stats.topSpeed * 0.15) sail = SailSetting.FullSail;

    desiredHeading = avoidNoGo(desiredHeading, wind);
    desiredHeading = avoidEdges(ship, desiredHeading);
    ship.setTargetHeading(desiredHeading);
    ship.setSail(sail);
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

/**
 * True if `point` lies within `ship`'s tight stern cone — i.e. dead behind it,
 * along the keel line. Shares `SternRakeArcHalfAngle` with the combat system so
 * the Tactician's notion of "rakeable / being raked" stays exactly in sync with
 * where a rake actually lands: the AI only treats a foe as rakeable (or itself as
 * threatened) when the geometry is a genuine down-the-hull stern rake, not a
 * mere shot from somewhere behind.
 */
function isAstern(ship: Ship, point: Vec2): boolean {
  const toPoint = sub(point, ship.position);
  const astern = scale(ship.forward, -1);
  return angle(astern, toPoint) <= Config.SternRakeArcHalfAngle;
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
