// A single sailing warship: the heart of the simulation. A port of Unity
// `Ships/Ship.cs`. Owns its movement (course, speed, point of sail), damage
// state (hull / rigging), and gun reloads. Rendering is delegated to an
// attached view via the ShipViewHooks interface.

import * as Config from "../core/config";
import { Faction } from "../core/faction";
import {
  headingToVector,
  normalize360,
  angleDifference,
  moveTowardsAngle,
  vectorToHeading,
} from "../core/nav";
import { clamp01, lerp, moveTowards, sign } from "../core/mathf";
import { type Vec2, sub, scale, add, sqrMagnitude, dot } from "../core/vec";
import { pointOfSailFactor, type Wind } from "../combat/wind";
import { type AmmoProfile } from "./ammo";
import { nextAmmo, AmmoType, ammoProfile } from "./ammo";
import { SailSetting, throttleFactor, nextSail } from "./sail";
import { type ShipStats, type ShipClass } from "./shipClass";

export enum BroadsideSide {
  Port = 0,
  Starboard = 1,
}

export enum ShipState {
  Sailing = 0,
  Sinking = 1,
  Gone = 2,
}

/** Hooks the rendering layer implements so the ship can drive its view. */
export interface ShipViewHooks {
  flashHit(): void;
  playBroadsideSmoke(side: BroadsideSide): void;
  updateVisuals(ship: Ship, wind: Wind, dt: number): void;
  updateSinking(t: number): void;
}

let s_NextId = 1;

export class Ship {
  readonly id: number;
  readonly stats: ShipStats;

  faction: Faction;
  hull: number;
  rigging: number;
  state: ShipState = ShipState.Sailing;
  headingDeg: number;
  targetHeadingDeg: number;
  sail: SailSetting = SailSetting.Reefed;
  ammo: AmmoType = AmmoType.RoundShot;
  speed = 0;
  pointOfSail = "-";
  position: Vec2;

  /** Sink progress in [0,1], for the renderer. */
  sinkProgress = 0;

  /** Seconds since this ship last took damage (gates automatic repair). */
  secondsSinceHit = Config.RepairSafeDelay;

  view: ShipViewHooks | null = null;

  private portReload = 0;
  private starboardReload = 0;
  private bowReload = 0;
  private sternReload = 0;
  private sinkTimer = 0;
  private edgeReversing = false;

  constructor(stats: ShipStats, faction: Faction, position: Vec2, headingDeg: number) {
    this.id = s_NextId++;
    this.stats = stats;
    this.faction = faction;
    this.hull = stats.maxHull;
    this.rigging = stats.maxRigging;
    this.headingDeg = normalize360(headingDeg);
    this.targetHeadingDeg = this.headingDeg;
    this.position = { x: position.x, z: position.z };
  }

  get shipClass(): ShipClass {
    return this.stats.shipClass;
  }

  get isAlive(): boolean {
    return this.state === ShipState.Sailing;
  }

  get hullFraction(): number {
    return clamp01(this.hull / this.stats.maxHull);
  }

  get riggingFraction(): number {
    return clamp01(this.rigging / this.stats.maxRigging);
  }

  /** Unit forward (bow) direction on the XZ plane. */
  get forward(): Vec2 {
    return headingToVector(this.headingDeg);
  }

  /** Unit starboard (+X local) direction on the XZ plane. */
  get right(): Vec2 {
    const r = this.headingDeg * (Math.PI / 180);
    return { x: Math.cos(r), z: -Math.sin(r) };
  }

  // ---- Orders ------------------------------------------------------------

  setCourseToPoint(worldPoint: Vec2): void {
    const dir = sub(worldPoint, this.position);
    if (sqrMagnitude(dir) > 0.001) {
      this.targetHeadingDeg = vectorToHeading(dir);
    }
  }

  setTargetHeading(headingDeg: number): void {
    this.targetHeadingDeg = normalize360(headingDeg);
  }

  cycleSail(): void {
    this.sail = nextSail(this.sail);
  }

  setSail(setting: SailSetting): void {
    this.sail = setting;
  }

  cycleAmmo(): void {
    this.ammo = nextAmmo(this.ammo);
  }

  setAmmo(type: AmmoType): void {
    this.ammo = type;
  }

  // ---- Gunnery state -----------------------------------------------------

  isBroadsideReady(side: BroadsideSide): boolean {
    return (side === BroadsideSide.Port ? this.portReload : this.starboardReload) <= 0;
  }

  reloadProgress(side: BroadsideSide): number {
    const t = side === BroadsideSide.Port ? this.portReload : this.starboardReload;
    return 1 - clamp01(t / this.stats.reloadTime);
  }

  notifyFired(side: BroadsideSide): void {
    const reload = this.stats.reloadTime * ammoProfile(this.ammo).reloadFactor;
    if (side === BroadsideSide.Port) {
      this.portReload = reload;
    } else {
      this.starboardReload = reload;
    }
    this.view?.playBroadsideSmoke(side);
  }

  isChaseReady(bow: boolean): boolean {
    return (bow ? this.bowReload : this.sternReload) <= 0;
  }

  notifyChaseFired(bow: boolean): void {
    const reload = this.stats.reloadTime * ammoProfile(this.ammo).reloadFactor;
    if (bow) {
      this.bowReload = reload;
    } else {
      this.sternReload = reload;
    }
  }

  /** World-space outward direction the chase guns point. */
  chaseNormal(bow: boolean): Vec2 {
    return bow ? this.forward : scale(this.forward, -1);
  }

  /** World-space outward normal of a broadside (perpendicular to the hull). */
  broadsideNormal(side: BroadsideSide): Vec2 {
    return side === BroadsideSide.Starboard ? this.right : scale(this.right, -1);
  }

  // ---- Damage ------------------------------------------------------------

  applyDamage(profile: AmmoProfile, multiplier: number): void {
    if (!this.isAlive) return;

    this.hull = Math.max(0, this.hull - profile.hullDamage * multiplier);
    this.rigging = Math.max(0, this.rigging - profile.riggingDamage * multiplier);

    // Taking fire resets the "safe" timer, pausing any in-progress repair (it
    // auto-resumes once clear for RepairSafeDelay again — see tick()).
    this.secondsSinceHit = 0;

    this.view?.flashHit();

    if (this.hull <= 0) {
      this.beginSinking();
    }
  }

  beginSinking(): void {
    if (this.state !== ShipState.Sailing) return;
    this.state = ShipState.Sinking;
    this.faction = Faction.Neutral;
    this.hull = 0;
    this.sinkTimer = Config.SinkDuration;
  }

  // ---- Simulation step ---------------------------------------------------

  tick(dt: number, wind: Wind): void {
    if (this.state === ShipState.Sinking) {
      this.tickSinking(dt);
      return;
    }

    if (this.state !== ShipState.Sailing) return;

    this.portReload = Math.max(0, this.portReload - dt);
    this.starboardReload = Math.max(0, this.starboardReload - dt);
    this.bowReload = Math.max(0, this.bowReload - dt);
    this.sternReload = Math.max(0, this.sternReload - dt);

    this.tickRepair(dt);

    this.handleArenaEdge();

    // Speed: throttle (sail plan) × point of sail (wind) × rigging health. The
    // more the sails are shot away, the slower the ship can go.
    const offWind = angleDifference(this.headingDeg, wind.fromDegrees);
    const pos = pointOfSailFactor(offWind);
    this.pointOfSail = pos.pointOfSail;

    const riggingSpeedFactor = lerp(Config.RiggingSpeedFloor, 1, this.riggingFraction);
    const targetSpeed = this.stats.topSpeed * throttleFactor(this.sail) * pos.factor * riggingSpeedFactor;
    this.speed = moveTowards(this.speed, targetSpeed, this.stats.acceleration * dt);

    // Turning (sailboat feel): a ship only turns as well as it is moving — the
    // effective turn rate scales with current speed, so a near-stationary ship
    // barely turns and a hove-to (zero-speed) ship cannot turn at all. Because
    // speed already reflects the point of sail, turning naturally reflects the
    // wind too. Forward motion continues through the turn (no pivot in place).
    const speedFrac = this.stats.topSpeed > 0.01 ? clamp01(this.speed / this.stats.topSpeed) : 0;
    const effectiveTurn = this.stats.turnRate * speedFrac;
    this.headingDeg = moveTowardsAngle(this.headingDeg, this.targetHeadingDeg, effectiveTurn * dt);

    this.position = add(this.position, scale(headingToVector(this.headingDeg), this.speed * dt));

    this.view?.updateVisuals(this, wind, dt);
  }

  /**
   * Automatic rigging repair. Once the ship has been clear of fire for
   * RepairSafeDelay seconds, its rigging slowly regenerates on its own (no
   * player action). Taking a hit resets `secondsSinceHit` (see applyDamage),
   * which immediately pauses regen until it has been safe that long again.
   * Regen stops at full rigging.
   */
  private tickRepair(dt: number): void {
    this.secondsSinceHit += dt;

    if (this.secondsSinceHit >= Config.RepairSafeDelay && this.rigging < this.stats.maxRigging) {
      const rate = this.stats.maxRigging * Config.RepairFractionPerSecond;
      this.rigging = Math.min(this.stats.maxRigging, this.rigging + rate * dt);
    }
  }

  private tickSinking(dt: number): void {
    this.sinkTimer -= dt;
    const t = 1 - clamp01(this.sinkTimer / Config.SinkDuration);
    this.sinkProgress = t;

    this.view?.updateSinking(t);

    if (this.sinkTimer <= 0) {
      this.state = ShipState.Gone;
    }
  }

  /** Edge turnaround with hysteresis — see Unity `Ship.HandleArenaEdge`. */
  private handleArenaEdge(): void {
    const edgeX = Config.ArenaHalfX * Config.EdgeTurnThreshold;
    const edgeZ = Config.ArenaHalfZ * Config.EdgeTurnThreshold;
    const clearX = Config.ArenaHalfX * Config.EdgeTurnClear;
    const clearZ = Config.ArenaHalfZ * Config.EdgeTurnClear;
    const p = this.position;

    if (this.edgeReversing) {
      if (Math.abs(p.x) < clearX && Math.abs(p.z) < clearZ) {
        this.edgeReversing = false;
      }
      return;
    }

    const beyond = Math.abs(p.x) > edgeX || Math.abs(p.z) > edgeZ;
    if (!beyond) return;

    const outward: Vec2 = {
      x: Math.abs(p.x) > edgeX ? sign(p.x) : 0,
      z: Math.abs(p.z) > edgeZ ? sign(p.z) : 0,
    };
    if (dot(headingToVector(this.headingDeg), outward) <= 0) return;

    this.targetHeadingDeg = normalize360(this.headingDeg + 180);
    this.edgeReversing = true;
  }
}
