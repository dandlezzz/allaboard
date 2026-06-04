// The central orchestrator — a port of Unity `Core/GameManager.cs`. Builds the
// fleets, owns the simulation systems (wind, gunnery, AI), routes pointer input
// into selection and orders, detects the win condition, and drives the HUD.
// Rendering is delegated to the PixiJS Renderer + ShipView.

import * as Config from "./config";
import { Faction, ControlMode, accentColor } from "./faction";
import { normalize360, headingToVector } from "./nav";
import { rangeFloat, seed } from "./rng";
import { distance, add, scale, sub, magnitude, dot, type Vec2 } from "./vec";
import { Wind } from "../combat/wind";
import { CombatSystem } from "../combat/combatSystem";
import { FleetAI, AIPersona } from "../ai/fleetAI";
import { nextSail } from "../ships/sail";
import { Ship, ShipState } from "../ships/ship";
import { ShipClass, shipStats } from "../ships/shipClass";
import { ShipView } from "../rendering/shipView";
import type { Renderer } from "../rendering/renderer";
import { buildSea } from "../rendering/scene";
import type { Hud } from "../ui/hud";
import type { PointerSample } from "../board/input";

// A pointer that moves less than this (screen px) between down and up is treated
// as a tap (select / deselect); more than this is a drag (set course).
const K_DRAG_THRESHOLD_PX = 6;

/**
 * Picks an INITIAL wind direction in the arc (45°, 315°) — i.e. always over 45°
 * and under 315°, the band that sweeps through due south (180°). The wind may
 * still veer freely afterward (see Wind.tick).
 */
function initialWindFromDegrees(): number {
  return normalize360(rangeFloat(45, 315));
}

function clamp01(x: number): number {
  return x < 0 ? 0 : x > 1 ? 1 : x;
}

/**
 * Closest points between two line segments [p1,q1] and [p2,q2] (Ericson,
 * Real-Time Collision Detection). Returns the pair of nearest points; their
 * distance is the segment-to-segment distance used for capsule collision.
 */
function closestSegmentSegment(
  p1: Vec2,
  q1: Vec2,
  p2: Vec2,
  q2: Vec2,
): { c1: Vec2; c2: Vec2 } {
  const d1 = sub(q1, p1); // direction of segment 1
  const d2 = sub(q2, p2); // direction of segment 2
  const r = sub(p1, p2);
  const a = dot(d1, d1); // squared length of segment 1
  const e = dot(d2, d2); // squared length of segment 2
  const f = dot(d2, r);
  const EPS = 1e-9;

  let s: number;
  let t: number;
  if (a <= EPS && e <= EPS) {
    s = 0;
    t = 0;
  } else if (a <= EPS) {
    s = 0;
    t = clamp01(f / e);
  } else {
    const c = dot(d1, r);
    if (e <= EPS) {
      t = 0;
      s = clamp01(-c / a);
    } else {
      const b = dot(d1, d2);
      const denom = a * e - b * b;
      s = denom > EPS ? clamp01((b * f - c * e) / denom) : 0;
      t = (b * s + f) / e;
      if (t < 0) {
        t = 0;
        s = clamp01(-c / a);
      } else if (t > 1) {
        t = 1;
        s = clamp01((b - c) / a);
      }
    }
  }

  return { c1: add(p1, scale(d1, s)), c2: add(p2, scale(d2, t)) };
}

export class Game {
  private readonly renderer: Renderer;
  private readonly hud: Hud;

  private wind!: Wind;
  private readonly combat = new CombatSystem();

  private readonly ships: Ship[] = [];
  private readonly control = new Map<Faction, ControlMode>();
  private readonly ai = new Map<Faction, FleetAI>();

  // ---- Baton of Command -------------------------------------------------
  // The player commands via a single "Baton of Command": a marker placed on the
  // sea (a mouse click in the browser; a Glyph contact on Board hardware). On
  // placement it takes command of the NEAREST human-controlled ship within
  // Config.BatonCommandRadius; that ship stays commanded (so it can be steered
  // and trimmed) until the baton is placed again elsewhere. In 2-player either
  // human side can be commanded — whichever human ship is nearest the baton.
  private batonPos: Vec2 | null = null;
  private commandedShip: Ship | null = null;

  // Pointer gesture state: a TAP (re)places the baton; a DRAG sets the commanded
  // ship's course toward the release point.
  private dragPointer: number | null = null;
  private dragDownScreen: { x: number; y: number } | null = null;
  private dragMoved = false;

  private gameOver = false;
  private winner = Faction.Neutral;
  private gameOverTimer = 0;

  // Currently-selected opponent persona, persisted across restarts (Rematch
  // reuses it). The HUD persona buttons set this and start a fresh game.
  private aiPersona: AIPersona = AIPersona.Standard;

  constructor(renderer: Renderer, hud: Hud) {
    this.renderer = renderer;
    this.hud = hud;
  }

  start(): void {
    seed(Math.floor(Math.random() * 0xffffffff));
    buildSea(this.renderer.seaLayer);

    this.wind = new Wind(initialWindFromDegrees());

    this.control.set(Faction.British, ControlMode.Human);
    this.control.set(Faction.FrancoSpanish, ControlMode.AI);
    this.ai.set(Faction.FrancoSpanish, new FleetAI(Faction.FrancoSpanish, this.aiPersona));

    this.spawnAllFleets();
    this.hud.setSecondPlayerMode(false);
    this.hud.setActivePersona(this.aiPersona);
  }

  // ---- Frame loop --------------------------------------------------------

  update(dt: number): void {
    if (!this.gameOver) {
      this.wind.tick(dt);
      this.tickAI();
      this.tickShips(dt);
      this.separateShips();
      this.combat.tick(this.ships, this.renderer);
      this.cullSunkShips();
      this.checkWinCondition();
    } else {
      this.gameOverTimer += dt;
    }

    this.refreshCommandVisuals();
    this.updateCourseVisuals();
    this.refreshBatonVisuals();
    this.renderer.updateEffects(dt);
    this.hud.refresh(this.wind, this.ships, this.gameOver, this.winner);
  }

  // ---- Input -------------------------------------------------------------

  onPointerSamples(samples: ReadonlyArray<PointerSample>): void {
    for (const s of samples) {
      if (s.isGlyph) {
        this.handleGlyph(s);
        continue;
      }
      const world = this.renderer.screenToWorld(s.position.x, s.position.y);
      if (s.phase === "began") {
        if (this.gameOver) {
          if (this.gameOverTimer >= 2) this.restart();
          continue;
        }
        this.handleDown(world, s);
      } else if (s.phase === "moved") {
        this.handleMove(world, s);
      } else if (s.phase === "ended") {
        this.handleUp(world, s);
      }
    }
  }

  /**
   * Pointer down. Command-control buttons (sail / ammo) of the CURRENTLY
   * commanded ship take priority on a tap; otherwise we arm a tap-or-drag
   * gesture (tap = move the baton, drag = set the commanded ship's course).
   */
  private handleDown(world: Vec2, s: PointerSample): void {
    // 1) Command controls (sail / ammo) of the commanded ship — these are the
    // baton's command surface, shown in the command bubble.
    const cmd = this.commandedShip;
    if (cmd && cmd.isAlive) {
      const v = cmd.view as ShipView | null;
      if (v) {
        if (v.ammoBadgeHit(world)) {
          cmd.cycleAmmo();
          return; // consumed: not a tap/drag
        }
        if (v.sailBadgeHit(world)) {
          cmd.setSail(nextSail(cmd.sail));
          return; // consumed: not a tap/drag
        }
      }
    }

    // 2) Arm a tap-or-drag gesture (resolved on pointer up).
    this.dragPointer = s.contactId;
    this.dragDownScreen = { x: s.position.x, y: s.position.y };
    this.dragMoved = false;
  }

  private handleMove(world: Vec2, s: PointerSample): void {
    if (s.contactId !== this.dragPointer || !this.dragDownScreen) return;

    if (!this.dragMoved) {
      const dx = s.position.x - this.dragDownScreen.x;
      const dy = s.position.y - this.dragDownScreen.y;
      if (Math.hypot(dx, dy) > K_DRAG_THRESHOLD_PX) this.dragMoved = true;
    }

    // A drag previews the commanded ship's new course.
    const cmd = this.commandedShip;
    if (this.dragMoved && cmd && cmd.isAlive) {
      this.renderer.showCoursePreview(cmd.position, world, accentColor(cmd.faction));
    }
  }

  private handleUp(world: Vec2, s: PointerSample): void {
    if (s.contactId !== this.dragPointer) return;

    if (this.dragMoved) {
      // Drag → set the commanded ship's course toward the release point.
      const cmd = this.commandedShip;
      if (cmd && cmd.isAlive) cmd.setCourseToPoint(world);
      this.renderer.hideCoursePreview();
    } else {
      // Tap → (re)place the Baton of Command on the sea and take command of the
      // nearest human ship within range.
      this.placeBaton(world);
    }

    this.resetDrag();
  }

  private resetDrag(): void {
    this.dragPointer = null;
    this.dragDownScreen = null;
    this.dragMoved = false;
  }

  /**
   * Places the Baton of Command at a sea point and commands the nearest human-
   * controlled ship within Config.BatonCommandRadius (or none, if the sea is
   * empty there — the baton marker still shows). Shared by mouse taps and, on
   * Board hardware, Glyph contacts.
   */
  private placeBaton(world: Vec2): void {
    this.batonPos = { x: world.x, z: world.z };
    this.commandedShip = this.pickCommandedShip(world);
  }

  private handleGlyph(s: PointerSample): void {
    // A physical Glyph piece acts as the baton: its position commands the nearest
    // human ship and its orientation sets that ship's course.
    const world = this.renderer.screenToWorld(s.position.x, s.position.y);
    this.placeBaton(world);
    const cmd = this.commandedShip;
    if (cmd && cmd.isAlive) {
      cmd.setTargetHeading(normalize360(-s.orientation * (180 / Math.PI)));
    }
  }

  /** Nearest alive, human-controlled ship within the baton's command radius. */
  private pickCommandedShip(point: Vec2): Ship | null {
    let best: Ship | null = null;
    let bestDist = Config.BatonCommandRadius;
    for (const ship of this.ships) {
      if (!ship.isAlive || !this.isHuman(ship.faction)) continue;
      const d = distance(point, ship.position);
      if (d <= bestDist) {
        bestDist = d;
        best = ship;
      }
    }
    return best;
  }

  // ---- Setup -------------------------------------------------------------

  private spawnAllFleets(): void {
    for (const ship of this.ships) {
      (ship.view as ShipView | null)?.destroy();
    }
    this.ships.length = 0;
    // A fresh battle starts with no baton placed and nothing commanded.
    this.batonPos = null;
    this.commandedShip = null;

    // Both fleets start tucked into a BOTTOM corner of the arena (negative Z is
    // the bottom of the screen — see Renderer.worldToScreen), drawn up in a
    // line-ahead (bow-to-stern) column. Both share one straight heading — due
    // "up" the board (0° = +Z, toward the top of the screen) — so the two
    // columns are axis-aligned and parallel: British near the bottom-left,
    // Franco-Spanish near the bottom-right. Players/AI steer them to engage.
    const columnHeading = 0; // straight up (+Z), parallel for both fleets
    this.spawnFleet(Faction.British, -1, columnHeading);
    this.spawnFleet(Faction.FrancoSpanish, 1, columnHeading);
  }

  /**
   * Spawns a fleet as a single line-ahead column anchored in a bottom corner.
   *
   * `cornerSignX` picks the corner: -1 = bottom-left, +1 = bottom-right. The
   * anchor (inset from the true corner so the rear-most hull stays on-screen) is
   * the REAR of the column; ships march FORWARD from it along `headingDeg`, so
   * the flagship (index 0) ends up leading and every ship shares one heading,
   * reading bow-to-stern. Spacing is cumulative from each ship's half-length plus
   * `ColumnGap`, so neighbours never overlap regardless of the class mix.
   */
  private spawnFleet(faction: Faction, cornerSignX: number, headingDeg: number): void {
    // Eight ships per fleet: a flagship, four 74s, and three frigates.
    const line: ShipClass[] = [
      ShipClass.FirstRate,
      ShipClass.ThirdRate,
      ShipClass.ThirdRate,
      ShipClass.ThirdRate,
      ShipClass.ThirdRate,
      ShipClass.Frigate,
      ShipClass.Frigate,
      ShipClass.Frigate,
    ];

    // Rear anchor in the bottom corner, kept a margin inside the arena bounds so
    // even the rear ship's stern stays on-screen.
    const marginX = 18 * Config.ShipScale;
    const marginZ = 10 * Config.ShipScale;
    const anchor: Vec2 = {
      x: cornerSignX * (Config.ArenaHalfX - marginX),
      z: -(Config.ArenaHalfZ - marginZ),
    };

    const forward = headingToVector(headingDeg); // column axis (bow direction)
    const lengths = line.map((c) => shipStats(c).length);

    // Distance of each ship FORWARD from the rear anchor. The rear-most ship
    // (last index) sits at the anchor; each step forward adds the two half-lengths
    // plus the gap so hulls clear each other.
    const distFromRear = new Array<number>(line.length);
    distFromRear[line.length - 1] = 0;
    for (let i = line.length - 2; i >= 0; i--) {
      distFromRear[i] =
        distFromRear[i + 1] + lengths[i + 1] * 0.5 + Config.ColumnGap + lengths[i] * 0.5;
    }

    for (let i = 0; i < line.length; i++) {
      const pos = add(anchor, scale(forward, distFromRear[i]));
      const ship = new Ship(shipStats(line[i]), faction, pos, headingDeg);
      new ShipView(ship, this.renderer);
      this.ships.push(ship);
    }
  }

  // ---- Simulation --------------------------------------------------------

  private tickAI(): void {
    for (const [faction, ai] of this.ai) {
      if (this.control.get(faction) === ControlMode.AI) {
        ai.tick(this.ships, this.wind);
      }
    }
  }

  private tickShips(dt: number): void {
    for (const ship of this.ships) ship.tick(dt, this.wind);
  }

  /**
   * Soft-collision resolution: a per-frame, purely-kinematic pass over every pair
   * of alive ships that guarantees no two HULLS overlap. It NEVER deals damage.
   *
   * Each hull is a CAPSULE — its keel as a line segment swollen by a radius (see
   * config). Two capsules overlap iff the closest distance between their keel
   * segments is less than the sum of their radii; because the capsule tightly
   * bounds the painted hull at any orientation, "capsules don't overlap" ⇒
   * "hulls don't overlap", whether bow-to-stern (the spawn column / head-on) or
   * abeam (a broadside duel).
   *
   *  1. Stop-on-contact (once per frame). On first detecting an overlap, each
   *     ship's forward speed is damped toward zero in proportion to how head-on
   *     the contact is (straight-in → halts, glancing → keeps speed and grinds
   *     alongside). Speed is only ever reduced, never reversed — a gentle stop,
   *     not a bounce.
   *  2. No overlap, ever. For each overlapping pair the FULL penetration is
   *     resolved by pushing the two ships apart along the contact normal, split
   *     evenly (each moves half). This runs as several Gauss-Seidel relaxation
   *     iterations (Config.ShipSeparationIterations): resolving one pair can
   *     nudge a third back into a neighbour, so repeating the all-pairs sweep
   *     drives residual penetration in packed clusters to ~0 within the frame.
   *
   * Headings are fixed across the iterations (we only translate positions), so
   * each ship's keel direction/length is precomputed once. Sunk / Gone ships are
   * skipped. Runs AFTER movement, so it only nudges positions and doesn't fight
   * the steering or edge-turn logic.
   */
  private separateShips(): void {
    const alive = this.ships.filter((s) => s.isAlive);
    const n = alive.length;
    if (n < 2) return;

    // Per-ship capsule axis (keel direction + segment half-length) and radius.
    const fwd: Vec2[] = new Array(n);
    const segHalf: number[] = new Array(n);
    const radius: number[] = new Array(n);
    for (let i = 0; i < n; i++) {
      const s = alive[i];
      const r = s.stats.beam * Config.ShipCollisionBeamFactor;
      const half = s.stats.length * Config.ShipCollisionHalfLengthFactor;
      fwd[i] = s.forward;
      radius[i] = r;
      segHalf[i] = Math.max(0, half - r); // capsule keel = hull minus rounded caps
    }

    for (let iter = 0; iter < Config.ShipSeparationIterations; iter++) {
      const damp = iter === 0; // apply stop-on-contact only once per frame
      for (let i = 0; i < n; i++) {
        const a = alive[i];
        for (let j = i + 1; j < n; j++) {
          const b = alive[j];
          // Fresh keel segments from current positions (either may have moved).
          const a1 = sub(a.position, scale(fwd[i], segHalf[i]));
          const a2 = add(a.position, scale(fwd[i], segHalf[i]));
          const b1 = sub(b.position, scale(fwd[j], segHalf[j]));
          const b2 = add(b.position, scale(fwd[j], segHalf[j]));

          const cp = closestSegmentSegment(a1, a2, b1, b2);
          let delta = sub(cp.c2, cp.c1); // from A's hull toward B's hull
          let dist = magnitude(delta);
          const minDist = radius[i] + radius[j];
          if (dist >= minDist) continue;

          // Degenerate (keels touch/cross): push apart along A's beam axis.
          let normal: Vec2;
          if (dist < 1e-6) {
            normal = { x: fwd[i].z, z: -fwd[i].x };
            dist = 0;
          } else {
            normal = scale(delta, 1 / dist);
          }

          if (damp) {
            const inwardA = Math.max(0, dot(fwd[i], normal));
            const inwardB = Math.max(0, dot(fwd[j], scale(normal, -1)));
            a.applyContactStop(1 - inwardA);
            b.applyContactStop(1 - inwardB);
          }

          const penetration = minDist - dist;
          const pushHalf = scale(normal, penetration * 0.5);
          a.nudgePosition(scale(pushHalf, -1));
          b.nudgePosition(pushHalf);
        }
      }
    }
  }

  private cullSunkShips(): void {
    for (let i = this.ships.length - 1; i >= 0; i--) {
      const ship = this.ships[i];
      if (ship.state === ShipState.Gone) {
        if (this.commandedShip === ship) this.commandedShip = null;
        (ship.view as ShipView | null)?.destroy();
        this.ships.splice(i, 1);
      }
    }
  }

  private checkWinCondition(): void {
    // A side wins once all of its enemy's ships have been sunk (the only way a
    // ship leaves play now that boarding/capture is removed).
    const britishAfloat = this.hasLivingShips(Faction.British);
    const francoAfloat = this.hasLivingShips(Faction.FrancoSpanish);
    if (britishAfloat && francoAfloat) return;

    this.gameOver = true;
    this.gameOverTimer = 0;
    this.winner = britishAfloat
      ? Faction.British
      : francoAfloat
        ? Faction.FrancoSpanish
        : Faction.Neutral;
  }

  restart(): void {
    this.gameOver = false;
    this.winner = Faction.Neutral;
    this.gameOverTimer = 0;
    this.resetDrag();
    this.renderer.hideCoursePreview();
    this.wind = new Wind(initialWindFromDegrees());
    this.spawnAllFleets();
  }

  // ---- Selection / queries ----------------------------------------------

  private refreshCommandVisuals(): void {
    // Drop command if the commanded ship is no longer a valid, human-controlled,
    // living ship (sunk, or switched to AI in 2-player).
    const cmd = this.commandedShip;
    if (cmd && (!cmd.isAlive || !this.isHuman(cmd.faction))) this.commandedShip = null;

    for (const ship of this.ships) {
      const commanded = ship === this.commandedShip && ship.isAlive;
      (ship.view as ShipView | null)?.setCommanded(commanded, ship.faction);
    }
  }

  private updateCourseVisuals(): void {
    const cmd = this.commandedShip;
    if (this.gameOver || !cmd || !cmd.isAlive) {
      this.renderer.hideHeadingLine();
      return;
    }
    this.renderer.showHeadingLine(
      cmd.position,
      cmd.targetHeadingDeg,
      cmd.stats.length * 2.5,
      accentColor(cmd.faction),
    );
  }

  private refreshBatonVisuals(): void {
    if (this.gameOver || !this.batonPos) {
      this.renderer.hideBaton();
      return;
    }
    const cmd = this.commandedShip;
    this.renderer.showBaton(this.batonPos, cmd && cmd.isAlive ? cmd.position : null);
  }

  toggleSecondPlayer(): void {
    const nowHuman = this.control.get(Faction.FrancoSpanish) !== ControlMode.Human;
    this.control.set(Faction.FrancoSpanish, nowHuman ? ControlMode.Human : ControlMode.AI);
    // If the commanded ship just reverted to AI control, drop the baton's command.
    if (!nowHuman && this.commandedShip?.faction === Faction.FrancoSpanish) {
      this.commandedShip = null;
    }
    this.hud.setSecondPlayerMode(nowHuman);
  }

  /**
   * Starts a fresh game with the Franco-Spanish fleet under AI control using the
   * given persona. The persona is persisted so the Rematch button reuses it.
   */
  selectPersona(persona: AIPersona): void {
    this.aiPersona = persona;
    this.control.set(Faction.FrancoSpanish, ControlMode.AI);
    this.ai.set(Faction.FrancoSpanish, new FleetAI(Faction.FrancoSpanish, persona));
    this.hud.setSecondPlayerMode(false);
    this.hud.setActivePersona(persona);
    this.restart();
  }

  private isHuman(faction: Faction): boolean {
    return this.control.get(faction) === ControlMode.Human;
  }

  private hasLivingShips(faction: Faction): boolean {
    return this.ships.some((s) => s.isAlive && s.faction === faction);
  }
}
