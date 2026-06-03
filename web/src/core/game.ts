// The central orchestrator — a port of Unity `Core/GameManager.cs`. Builds the
// fleets, owns the simulation systems (wind, gunnery, AI), routes pointer input
// into selection and orders, detects the win condition, and drives the HUD.
// Rendering is delegated to the PixiJS Renderer + ShipView.

import * as Config from "./config";
import { Faction, ControlMode, accentColor } from "./faction";
import { normalize360 } from "./nav";
import { clamp, floorToInt, ceilToInt } from "./mathf";
import { rangeFloat, seed } from "./rng";
import { distance, type Vec2 } from "./vec";
import { Wind } from "../combat/wind";
import { CombatSystem } from "../combat/combatSystem";
import { FleetAI } from "../ai/fleetAI";
import { SailSetting } from "../ships/sail";
import { Ship, ShipState } from "../ships/ship";
import { ShipClass, shipStats } from "../ships/shipClass";
import { ShipControl, ShipView } from "../rendering/shipView";
import type { Renderer } from "../rendering/renderer";
import { buildSea } from "../rendering/scene";
import type { Hud } from "../ui/hud";
import type { PointerSample } from "../board/input";

// A pointer that moves less than this (screen px) between down and up is treated
// as a tap (select / deselect); more than this is a drag (set course).
const K_DRAG_THRESHOLD_PX = 6;

export class Game {
  private readonly renderer: Renderer;
  private readonly hud: Hud;

  private wind!: Wind;
  private readonly combat = new CombatSystem();

  private readonly ships: Ship[] = [];
  private readonly control = new Map<Faction, ControlMode>();
  private readonly ai = new Map<Faction, FleetAI>();
  private readonly selected = new Map<Faction, Ship>();
  private activeFaction = Faction.British;

  // Drag-to-command state for the active pointer.
  private dragPointer: number | null = null;
  private dragTarget: Ship | null = null;
  private dragCandidate: Ship | null = null;
  private dragDownScreen: { x: number; y: number } | null = null;
  private dragMoved = false;

  private gameOver = false;
  private winner = Faction.Neutral;
  private gameOverTimer = 0;

  constructor(renderer: Renderer, hud: Hud) {
    this.renderer = renderer;
    this.hud = hud;
  }

  start(): void {
    seed(Math.floor(Math.random() * 0xffffffff));
    buildSea(this.renderer.seaLayer);

    this.wind = new Wind(rangeFloat(0, 360));

    this.control.set(Faction.British, ControlMode.Human);
    this.control.set(Faction.FrancoSpanish, ControlMode.AI);
    this.ai.set(Faction.FrancoSpanish, new FleetAI(Faction.FrancoSpanish));

    this.spawnAllFleets();
    this.hud.setSecondPlayerMode(false);
  }

  // ---- Frame loop --------------------------------------------------------

  update(dt: number): void {
    if (!this.gameOver) {
      this.wind.tick(dt);
      this.tickAI();
      this.tickShips(dt);
      this.combat.tick(this.ships, this.renderer);
      this.cullSunkShips();
      this.checkWinCondition();
    } else {
      this.gameOverTimer += dt;
    }

    this.refreshSelectionVisuals();
    this.updateCourseVisuals();
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
   * Pointer down. On-ring buttons (sail / ammo) of the selected ship take
   * priority on a tap; otherwise we arm a tap-or-drag gesture. The drag steers
   * the ship under the finger (or, if started on open water, the selected ship).
   */
  private handleDown(world: Vec2, s: PointerSample): void {
    const sel = this.selectedOf(this.activeFaction);

    // 1) On-ring control button of the selected ship (sail +/-, ammo cycle).
    if (sel && sel.isAlive) {
      const view = sel.view as ShipView | null;
      if (view) {
        const ctrl = view.tryHitControl(world);
        if (ctrl !== ShipControl.None) {
          this.applyControl(sel, ctrl);
          view.flashControl(ctrl);
          return; // consumed: not a tap/drag
        }
      }
    }

    // 1b) Always-present shot toggle on ANY human ship (no need to select first).
    for (const ship of this.ships) {
      if (!ship.isAlive || !this.isHuman(ship.faction)) continue;
      const v = ship.view as ShipView | null;
      if (v && v.ammoBadgeHit(world)) {
        ship.cycleAmmo();
        return; // consumed: not a tap/drag
      }
    }

    // 2) Arm a tap-or-drag gesture.
    const hit = this.findShipAt(world);
    const friendly = hit && this.isHuman(hit.faction) ? hit : null;
    this.dragCandidate = friendly;
    // Drag steers the ship under the finger, or the already-selected ship if the
    // gesture started on open water.
    this.dragTarget = friendly ?? sel;
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

    if (this.dragMoved && this.dragTarget && this.dragTarget.isAlive) {
      this.renderer.showCoursePreview(
        this.dragTarget.position,
        world,
        accentColor(this.dragTarget.faction),
      );
    }
  }

  private handleUp(world: Vec2, s: PointerSample): void {
    if (s.contactId !== this.dragPointer) return;

    if (this.dragMoved) {
      // Drag → commit a course toward the release point and select the ship.
      if (this.dragTarget && this.dragTarget.isAlive) {
        this.dragTarget.setCourseToPoint(world);
        this.selected.set(this.dragTarget.faction, this.dragTarget);
        this.activeFaction = this.dragTarget.faction;
      }
      this.renderer.hideCoursePreview();
    } else {
      // Tap → select / switch / deselect.
      const sel = this.selectedOf(this.activeFaction);
      if (this.dragCandidate && this.dragCandidate !== sel) {
        this.selected.set(this.dragCandidate.faction, this.dragCandidate);
        this.activeFaction = this.dragCandidate.faction;
      } else if (!this.dragCandidate) {
        this.selected.delete(this.activeFaction);
      }
      // Tapping the already-selected ship leaves it selected.
    }

    this.resetDrag();
  }

  private resetDrag(): void {
    this.dragPointer = null;
    this.dragTarget = null;
    this.dragCandidate = null;
    this.dragDownScreen = null;
    this.dragMoved = false;
  }

  private applyControl(ship: Ship, control: ShipControl): void {
    switch (control) {
      case ShipControl.SailUp:
        ship.setSail(clamp(ship.sail + 1, 0, 3) as SailSetting);
        break;
      case ShipControl.SailDown:
        ship.setSail(clamp(ship.sail - 1, 0, 3) as SailSetting);
        break;
      case ShipControl.AmmoCycle:
        ship.cycleAmmo();
        break;
      default:
        break;
    }
  }

  private handleGlyph(s: PointerSample): void {
    const world = this.renderer.screenToWorld(s.position.x, s.position.y);
    const hit = this.findShipAt(world);
    if (!hit || !this.isHuman(hit.faction)) return;
    this.selected.set(hit.faction, hit);
    this.activeFaction = hit.faction;
    hit.setTargetHeading(normalize360(-s.orientation * (180 / Math.PI)));
  }

  // ---- Setup -------------------------------------------------------------

  private spawnAllFleets(): void {
    for (const ship of this.ships) {
      (ship.view as ShipView | null)?.destroy();
    }
    this.ships.length = 0;
    this.selected.clear();

    // British behind the left short edge steering east (90°); Franco-Spanish
    // behind the right short edge steering west (270°).
    this.spawnFleet(Faction.British, -1, 90);
    this.spawnFleet(Faction.FrancoSpanish, 1, 270);
  }

  private spawnFleet(faction: Faction, side: number, heading: number): void {
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

    const xMargin = 10 * Config.ShipScale;
    const zMargin = 7 * Config.ShipScale;
    const frontX = side * (Config.ArenaHalfX - xMargin);
    const usableHalfZ = Config.ArenaHalfZ - zMargin;

    const minSpacing = 2.4 * shipStats(ShipClass.FirstRate).beam;
    const maxPerRank = Math.max(1, floorToInt((2 * usableHalfZ) / minSpacing) + 1);
    const ranks = Math.max(1, ceilToInt(line.length / maxPerRank));
    const perRank = ceilToInt(line.length / ranks);
    const rankGap = 4 * Config.ShipScale;

    for (let i = 0; i < line.length; i++) {
      const rank = Math.floor(i / perRank);
      const indexInRank = i % perRank;
      const countInRank = Math.min(perRank, line.length - rank * perRank);

      const z =
        countInRank > 1
          ? lerpf(-usableHalfZ, usableHalfZ, indexInRank / (countInRank - 1))
          : 0;
      const x = frontX + side * rank * rankGap;

      const ship = new Ship(shipStats(line[i]), faction, { x, z }, heading);
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

  private cullSunkShips(): void {
    for (let i = this.ships.length - 1; i >= 0; i--) {
      const ship = this.ships[i];
      if (ship.state === ShipState.Gone) {
        this.clearSelectionOf(ship);
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
    this.wind = new Wind(rangeFloat(0, 360));
    this.spawnAllFleets();
  }

  // ---- Selection / queries ----------------------------------------------

  private refreshSelectionVisuals(): void {
    for (const ship of this.ships) {
      let selected = false;
      let selector = Faction.Neutral;
      if (this.selected.get(Faction.British) === ship) {
        selected = true;
        selector = Faction.British;
      } else if (this.selected.get(Faction.FrancoSpanish) === ship) {
        selected = true;
        selector = Faction.FrancoSpanish;
      }
      (ship.view as ShipView | null)?.setSelected(selected && ship.isAlive, selector);
    }
  }

  private updateCourseVisuals(): void {
    const sel = this.selectedOf(this.activeFaction);
    if (this.gameOver || !sel) {
      this.renderer.hideHeadingLine();
      return;
    }
    this.renderer.showHeadingLine(
      sel.position,
      sel.targetHeadingDeg,
      sel.stats.length * 2.5,
      accentColor(this.activeFaction),
    );
  }

  toggleSecondPlayer(): void {
    const nowHuman = this.control.get(Faction.FrancoSpanish) !== ControlMode.Human;
    this.control.set(Faction.FrancoSpanish, nowHuman ? ControlMode.Human : ControlMode.AI);
    if (!nowHuman) this.selected.delete(Faction.FrancoSpanish);
    this.hud.setSecondPlayerMode(nowHuman);
  }

  private selectedOf(faction: Faction): Ship | null {
    const ship = this.selected.get(faction);
    if (ship && ship.isAlive && ship.faction === faction) return ship;
    return null;
  }

  private clearSelectionOf(ship: Ship): void {
    if (this.selected.get(Faction.British) === ship) this.selected.delete(Faction.British);
    if (this.selected.get(Faction.FrancoSpanish) === ship) this.selected.delete(Faction.FrancoSpanish);
  }

  private isHuman(faction: Faction): boolean {
    return this.control.get(faction) === ControlMode.Human;
  }

  private hasLivingShips(faction: Faction): boolean {
    return this.ships.some((s) => s.isAlive && s.faction === faction);
  }

  private findShipAt(world: Vec2): Ship | null {
    let best: Ship | null = null;
    let bestDist = Number.MAX_VALUE;
    for (const ship of this.ships) {
      if (!ship.isAlive) continue;
      const radius = Math.max(Config.ShipSelectRadius, ship.stats.length * 0.6);
      const d = distance(world, ship.position);
      if (d <= radius && d < bestDist) {
        bestDist = d;
        best = ship;
      }
    }
    return best;
  }
}

function lerpf(a: number, b: number, t: number): number {
  return a + (b - a) * t;
}
