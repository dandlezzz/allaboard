// The central orchestrator — a port of Unity `Core/GameManager.cs`. Builds the
// fleets, owns the simulation systems (wind, gunnery, AI), routes pointer input
// into selection and orders, detects the win condition, and drives the HUD.
// Rendering is delegated to the PixiJS Renderer + ShipView.

import * as Config from "./config";
import { Faction, ControlMode, accentColor, displayName, enemyOf } from "./faction";
import { normalize360, headingToVector, vectorToHeading, angleDifference } from "./nav";
import { seed } from "./rng";
import { distance, add, scale, sub, magnitude, dot, type Vec2 } from "./vec";
import { Wind, pointOfSailColor } from "../combat/wind";
import { CombatSystem } from "../combat/combatSystem";
import { FleetAI, AIPersona } from "../ai/fleetAI";
import { SailSetting } from "../ships/sail";
import { nextAmmo } from "../ships/ammo";
import { Ship, ShipState } from "../ships/ship";
import { ShipClass, shipStats } from "../ships/shipClass";
import { ShipView } from "../rendering/shipView";
import type { Renderer } from "../rendering/renderer";
import { buildScene } from "../rendering/scene";
import { type Scenario, type FleetFormation, SCENARIOS, getScenario } from "./scenarios";
import type { Hud, Opponent } from "../ui/hud";
import type { PointerSample } from "../board/input";
import type { PauseMenu } from "../board/pauseMenu";

/**
 * The match's lifecycle. On load and on every Rematch/persona change the game
 * opens in `Setup` (players place their command pieces); once all required
 * players are ready it transitions to `Playing`; the win condition ends it in
 * `GameOver`.
 */
export enum GamePhase {
  Setup = 0,
  Playing = 1,
  GameOver = 2,
}

/** The two sides that can take the field (used to iterate pads / fleets). */
const PLAYABLE_FACTIONS: ReadonlyArray<Faction> = [Faction.British, Faction.FrancoSpanish];

/** Fallback setup-pad centre (used until the scenario fleets place one). */
function defaultPad(faction: Faction): Vec2 {
  const p = faction === Faction.British ? Config.SetupPadBritish : Config.SetupPadFrancoSpanish;
  return { x: p.x, z: p.z };
}

/** Clamps `v` into [lo, hi] (hi guarded ≥ lo). */
function clampInto(v: number, lo: number, hi: number): number {
  const top = Math.max(lo, hi);
  return v < lo ? lo : v > top ? top : v;
}

/** Short label for an AI persona, shown on the opponent's setup pad. */
function personaName(persona: AIPersona): string {
  switch (persona) {
    case AIPersona.Turtle:
      return "Turtle";
    case AIPersona.Tactician:
      return "Giga-brain";
    default:
      return "Standard";
  }
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

/**
 * A live TOUCH-gated absolute-steer session for one side's baton. Created when a
 * hand first touches the Piece (`isTouched`): we capture the baton's orientation
 * at that instant (`startDeg`) and do NOT change course on the mere touch. Only
 * once the held baton is rotated past a small dead-band do we begin driving the
 * squadron — `steering` flips true and every commanded ship's ordered heading is
 * set to the baton's ABSOLUTE orientation each frame (so a scattered fleet
 * converges onto the one heading the held baton points at). Releasing the touch
 * deletes the session, latching the last heading; a resting baton never steers.
 */
interface SteerRef {
  startDeg: number;
  steering: boolean;
}

export class Game {
  private readonly renderer: Renderer;
  private readonly hud: Hud;

  private wind!: Wind;
  private readonly combat = new CombatSystem();

  private readonly ships: Ship[] = [];
  private readonly control = new Map<Faction, ControlMode>();
  private readonly ai = new Map<Faction, FleetAI>();

  // ---- Baton of Command (per faction) -----------------------------------
  // Each human side commands via its own "Baton of Command": a marker placed on
  // the sea (a mouse click in the browser; a Glyph contact on Board hardware).
  // On placement every alive friendly ship of THAT side within the baton's
  // sphere of influence (Config.BatonCommandRadius) comes under command and
  // stays commanded (steerable, trimmable) until that side's baton is placed
  // elsewhere. A single tap only ever moves one side's baton — the side whose
  // nearest ship is closest to the tap — so two players (or two physical
  // Glyphs) can hold independent command of their own fleets. The match seeds
  // each baton from where its command piece was placed during Setup.
  private readonly batonPos = new Map<Faction, Vec2>();
  private readonly commandedShips = new Map<Faction, Ship[]>();

  // The live Piece binding (device path): which physical contactId currently
  // commands each side's baton, so two Pieces (one per side, or per player) are
  // tracked and lifted independently. Keyed by contactId — NEVER by glyphId (a
  // Piece *type* id). Mouse-placed batons have no entry here.
  private readonly batonContact = new Map<Faction, number>();
  // TOUCH-gated absolute-steer session, per faction. Created when a hand touches
  // the Piece; only once the held baton is rotated past a dead-band does it drive
  // the squadron to the baton's ABSOLUTE orientation (all ships converge to that
  // heading). Absent ⇒ baton resting/released → heading is latched, course held.
  private readonly batonSteerRef = new Map<Faction, SteerRef>();
  // Whether each side's baton is currently HELD (hand on the Piece, or a mouse
  // steer-drag in progress) — drives the brighter "being commanded" highlight.
  private readonly batonHeld = new Map<Faction, boolean>();

  // True on real Board hardware: device contacts are Pieces (batons) + fingers
  // (trim only). False in the browser preview, where the mouse emulates the full
  // place / steer / trim / dismiss cycle.
  private onDevice = false;

  // Browser mouse-emulation gesture state (intent-by-target, no brittle global
  // tap/drag threshold). A press that starts on a baton roundel begins a
  // steer-drag (or, if it doesn't move, dismisses); a press on open sea places.
  private mouseGesture: "roundel" | "sea" | "control" | null = null;
  private mouseFaction: Faction | null = null;
  private mousePointer: number | null = null;
  private mouseDownScreen: { x: number; y: number } | null = null;
  private mouseMoved = false;

  // Sail-thermometer drag: the contact currently dragging a baton's vertical sail
  // control, and which side's squadron it trims. Tracked by contactId so finger
  // (device) and mouse (browser) drags both work; cleared on release.
  private sailDragContact: number | null = null;
  private sailDragFaction: Faction | null = null;

  // ---- Phase / setup -----------------------------------------------------
  private phase: GamePhase = GamePhase.Setup;
  /** Which human sides have placed their command piece this match. */
  private readonly placed = new Map<Faction, boolean>();
  /** Counting down to battle start once every required side is ready. */
  private countingDown = false;
  private setupCountdown = 0;

  private winner = Faction.Neutral;
  private gameOverTimer = 0;

  // Currently-selected opponent persona, persisted across restarts (Rematch
  // reuses it). The menu persona buttons set this and start a fresh game.
  private aiPersona: AIPersona = AIPersona.Standard;

  // ---- Scenario (the chosen historical battle) --------------------------
  // The scenario drives fleet composition, starting formations, fixed wind,
  // per-side display labels, and any cosmetic coastline. Gameplay is otherwise
  // identical across scenarios. Defaults to the first battle so the field is
  // populated behind the opening menu; the menu replaces it via configureMatch.
  private scenario: Scenario = SCENARIOS[0];
  /** Which faction the (first) human player commands; the other is the AI/2P side. */
  private playerFaction: Faction = Faction.British;
  /** Per-scenario setup-pad centres, computed from each fleet's spawned centroid. */
  private readonly padPos = new Map<Faction, Vec2>();

  // OS pause overlay (Board hardware menu button). Null in the browser preview;
  // on a Board it's set before `start()` so phase transitions can drive it.
  private pauseMenu: PauseMenu | null = null;

  constructor(renderer: Renderer, hud: Hud, onDevice = false) {
    this.renderer = renderer;
    this.hud = hud;
    this.onDevice = onDevice;
  }

  /** Attaches the OS pause-overlay controller (no-op driver in the browser). */
  setPauseMenu(pauseMenu: PauseMenu): void {
    this.pauseMenu = pauseMenu;
  }

  start(): void {
    seed(Math.floor(Math.random() * 0xffffffff));

    // Default match (Royal Navy vs Standard AI on the first battle) so the sea is
    // populated behind the opening scenario menu; confirming a battle in the menu
    // replaces this via configureMatch().
    this.scenario = SCENARIOS[0];
    this.playerFaction = Faction.British;
    this.control.set(Faction.British, ControlMode.Human);
    this.control.set(Faction.FrancoSpanish, ControlMode.AI);
    this.ai.set(Faction.FrancoSpanish, new FleetAI(Faction.FrancoSpanish, this.aiPersona));
    this.hud.setSideLabels(this.scenario.british.label, this.scenario.enemy.label);

    this.wind = new Wind(this.scenario.windFromDegrees);
    this.enterSetup();
  }

  /**
   * Configures and starts a fresh match from a menu selection: which battle
   * (scenario), which side the player commands, and the opponent (an AI persona
   * or 2-player). Sets the control/AI machinery accordingly and restarts into
   * the place-your-command-piece Setup phase. Reuses the existing per-faction
   * control map, so the player may command EITHER side; the other becomes the
   * AI (or the second human in 2-player).
   */
  configureMatch(scenarioId: string, playerFaction: Faction, opponent: Opponent): void {
    this.scenario = getScenario(scenarioId);
    this.playerFaction = playerFaction;
    const enemy = enemyOf(playerFaction);

    if (opponent === "human") {
      this.control.set(playerFaction, ControlMode.Human);
      this.control.set(enemy, ControlMode.Human);
      this.ai.clear();
    } else {
      this.aiPersona = opponent;
      this.control.set(playerFaction, ControlMode.Human);
      this.control.set(enemy, ControlMode.AI);
      this.ai.clear();
      this.ai.set(enemy, new FleetAI(enemy, opponent));
    }

    this.hud.setSideLabels(this.scenario.british.label, this.scenario.enemy.label);
    this.restart();
  }

  // ---- Frame loop --------------------------------------------------------

  update(dt: number): void {
    if (this.phase === GamePhase.Playing) {
      // The live battle: wind, AI, movement, collision, gunnery, win check.
      this.wind.tick(dt);
      this.tickAI();
      this.tickShips(dt);
      this.separateShips();
      this.combat.tick(this.ships, this.renderer);
      this.cullSunkShips();
      this.checkWinCondition();
    } else if (this.phase === GamePhase.GameOver) {
      this.gameOverTimer += dt;
    } else {
      // Setup: the simulation is paused (no movement/combat/AI) while players
      // place their command pieces; only the ready countdown advances.
      this.tickSetup(dt);
    }

    this.refreshCommandVisuals();
    this.updateCourseVisuals();
    this.refreshBatonVisuals();
    this.refreshSetupVisuals();
    this.renderer.updateEffects(dt);
    this.hud.refresh(this.wind, this.ships, this.phase === GamePhase.GameOver, this.winner);
  }

  // ---- Input -------------------------------------------------------------

  onPointerSamples(samples: ReadonlyArray<PointerSample>): void {
    for (const s of samples) {
      // On-device Piece map aid: log each recognised Piece the first frame it
      // appears, so the user can see which physical robot maps to which glyphId
      // (ids are only known empirically). Gated to the device + Pieces so the
      // browser console stays quiet. Fingers (glyphId 0) are never logged here.
      if (this.onDevice && s.isGlyph && s.glyphId > 0 && s.phase === "began") {
        console.log(
          `[baton] Piece placed: glyphId=${s.glyphId} contactId=${s.contactId} ` +
            `touched=${s.touched} phase=${this.phase} at (${Math.round(s.position.x)}, ${Math.round(s.position.y)})`,
        );
      }

      // Setup phase: a contact (mouse click or Glyph placement) on a side's pad
      // places that side's command piece. Both input paths share this handler.
      if (this.phase === GamePhase.Setup) {
        this.handleSetupContact(s);
        continue;
      }

      // Game over: a tap (after a short delay) starts a fresh match.
      if (this.phase === GamePhase.GameOver) {
        if (s.phase === "began" && this.gameOverTimer >= 2) this.restart();
        continue;
      }

      // Playing.
      // A physical Piece (Glyph) drives the Baton of Command lifecycle. Gate on
      // glyphId > 0 so a finger reported without a Piece type never trips piece
      // logic (per the Piece-interaction guide); such fingers fall through to
      // the finger-trim path below.
      if (s.isGlyph && s.glyphId > 0) {
        this.handleGlyph(s);
        continue;
      }

      // On hardware, every non-Piece contact is a FINGER: while a baton is on
      // the board fingers only ever operate the floating trim controls — they
      // never place, move, steer, or dismiss command (the Piece owns that).
      if (this.onDevice) {
        this.handleFinger(s);
        continue;
      }

      // Browser preview: the mouse emulates the whole Piece cycle.
      const world = this.renderer.screenToWorld(s.position.x, s.position.y);
      if (s.phase === "began") {
        this.handleMouseDown(world, s);
      } else if (s.phase === "moved") {
        this.handleMouseMove(world, s);
      } else if (s.phase === "ended") {
        this.handleMouseUp(world, s);
      }
    }
  }

  // ---- Setup phase -------------------------------------------------------

  /** (Re)enters the Setup phase: fresh fleets, no batons, no one placed yet. */
  private enterSetup(): void {
    this.phase = GamePhase.Setup;
    this.winner = Faction.Neutral;
    this.gameOverTimer = 0;
    this.countingDown = false;
    this.setupCountdown = 0;
    this.placed.clear();
    this.resetMouseGesture();
    this.sailDragContact = null;
    this.sailDragFaction = null;
    this.renderer.hideCoursePreview();
    this.spawnAllFleets();
    // No live match yet, but keep a pause context registered so the hardware
    // menu button (and its Quit) still works on the start screen — just without
    // the in-match Restart option.
    this.pauseMenu?.enterSetup();
  }

  /**
   * A Setup contact: if a present contact (a fresh tap OR a Piece/finger RESTING)
   * sits on a not-yet-ready human side's pad, that side places its command piece.
   *
   * We deliberately accept ANY non-lift phase (not only the one-frame "began"):
   * on the Board a Piece left on the table across a restart keeps the SAME
   * contactId, so it never re-fires "began" — gating on "began" alone meant such
   * a Piece could rest on the pad forever and never ready the fleet. Accepting
   * "moved"/stationary frames readies it as soon as it's on the pad.
   * `placeCommandPiece` is idempotent (once placed, `needsPlacement` is false, so
   * the loop skips). The hit radius is generous/touch-friendly. Drives the mouse
   * fallback (click/drag), device fingers, and recognised Glyph Pieces alike.
   */
  private handleSetupContact(s: PointerSample): void {
    if (s.phase === "ended") return; // a lift never places
    const world = this.renderer.screenToWorld(s.position.x, s.position.y);
    const hitRadius = Config.SetupPadRadius * 1.6; // generous, forgiving target
    for (const faction of PLAYABLE_FACTIONS) {
      if (!this.needsPlacement(faction)) continue;
      if (distance(world, this.padPosition(faction)) <= hitRadius) {
        this.placeCommandPiece(faction);
        return;
      }
    }
  }

  /** A human side that hasn't placed its command piece yet. */
  private needsPlacement(faction: Faction): boolean {
    return this.isHuman(faction) && !this.placed.get(faction);
  }

  /** True once a side is ready: AI sides are auto-ready; humans must place. */
  private isReady(faction: Faction): boolean {
    return this.isHuman(faction) ? this.placed.get(faction) === true : true;
  }

  /** Every required (human) side has placed → the battle may begin. */
  private allReady(): boolean {
    return PLAYABLE_FACTIONS.every((f) => this.isReady(f));
  }

  /**
   * Places a side's command piece on its pad: marks it ready, and SEEDS that
   * side's Baton of Command at the pad so the placed piece immediately takes
   * command of the fleet around it. When every required side is ready this kicks
   * off the short countdown into the battle.
   */
  private placeCommandPiece(faction: Faction): void {
    this.placed.set(faction, true);
    this.control.set(faction, ControlMode.Human);
    this.seedBaton(faction, this.padPosition(faction));
    if (this.allReady() && !this.countingDown) {
      this.countingDown = true;
      this.setupCountdown = Config.SetupCountdownSeconds;
    }
  }

  private tickSetup(dt: number): void {
    if (!this.countingDown) return;
    this.setupCountdown -= dt;
    if (this.setupCountdown <= 0) {
      this.countingDown = false;
      this.setupCountdown = 0;
      this.phase = GamePhase.Playing;
      // Battle is live: register the in-match pause context so the hardware menu
      // button offers Restart / Quit on a Board.
      this.pauseMenu?.enterPlaying();
    }
  }

  // ---- Baton lifecycle: Piece (device) path -----------------------------

  /**
   * A physical Piece (Glyph) IS the Baton of Command, modelled as a small state
   * machine keyed by its `contactId`:
   *   - Placed (Began, contact not bound yet): bind it to the nearest human
   *     side and capture that squadron ONCE (so the sphere ring means what it
   *     shows); the Piece position is the baton position.
   *   - Held/Resting (Moved/Stationary): the baton tracks the Piece position;
   *     while HELD (a hand on the Piece) rotating it steers the squadron, then
   *     the heading LATCHES (never re-clamped to a resting Piece).
   *   - Lifted (Ended/Canceled, synthesised by input.ts when the contactId
   *     leaves the per-frame snapshot, and on pause): dismiss the baton.
   */
  private handleGlyph(s: PointerSample): void {
    if (s.phase === "ended") {
      this.clearBatonByContact(s.contactId);
      return;
    }

    const world = this.renderer.screenToWorld(s.position.x, s.position.y);
    let faction = this.factionForContact(s.contactId);

    if (faction === null) {
      // Placement: bind this Piece to the nearest human side and capture once.
      faction = this.nearestHumanFaction(world);
      if (faction === null) return; // no friendly fleet in range → not a baton
      this.bindBaton(faction, s.contactId, world);
    } else {
      // Slide: keep the baton glued to the Piece, but do NOT re-capture the
      // commanded set (membership is frozen at placement).
      this.batonPos.set(faction, { x: world.x, z: world.z });
    }

    // Touch-gated ABSOLUTE steering. A resting (untouched) baton holds course; a
    // HELD baton, once rotated past a dead-band, drives the whole squadron onto
    // the baton's absolute orientation. Releasing latches the heading.
    this.batonHeld.set(faction, s.touched);
    if (s.touched) {
      this.steerFromTouch(faction, s.orientation);
    } else {
      this.batonSteerRef.delete(faction);
    }
  }

  /** The side whose baton is currently bound to `contactId`, or null. */
  private factionForContact(contactId: number): Faction | null {
    for (const [faction, id] of this.batonContact) {
      if (id === contactId) return faction;
    }
    return null;
  }

  /**
   * Binds a Piece (by `contactId`) to a side's baton, anchors it, and captures
   * that side's commanded squadron ONCE. Any baton that side already held (from
   * a different contact) is replaced by this fresh placement.
   */
  private bindBaton(faction: Faction, contactId: number, world: Vec2): void {
    this.batonContact.set(faction, contactId);
    this.seedBaton(faction, world);
    // End any prior steer session so placement starts from "hold current course"
    // — the next held frame re-baselines and a Δ of 0 changes nothing.
    this.batonSteerRef.delete(faction);
  }

  /** Dismisses the baton bound to `contactId` (Piece lifted / canceled). */
  private clearBatonByContact(contactId: number): void {
    const faction = this.factionForContact(contactId);
    if (faction !== null) this.clearBaton(faction);
  }

  /**
   * Tears down a side's baton: removes its bubble, sphere ring, and floating
   * controls, and unbinds the Piece. The commanded ships KEEP their last ordered
   * heading and sail on — lifting the baton dismisses command, it doesn't
   * capsize the fleet (docs/baton-touch-scheme.md §4.1).
   */
  private clearBaton(faction: Faction): void {
    this.batonPos.delete(faction);
    this.commandedShips.delete(faction);
    this.batonContact.delete(faction);
    this.batonSteerRef.delete(faction);
    this.batonHeld.delete(faction);
  }

  /**
   * RELATIVE rotate-to-steer. `orientationRad` is the Piece facing in radians.
   * The first held frame captures a baseline (the Piece angle + each commanded
   * ship's current ordered heading); thereafter each ship's heading = its
   * baseline + Δ, where Δ is how far the Piece has rotated since the baseline.
   * So merely placing/holding the Piece (Δ = 0) changes nothing — only an
   * intentional turn steers — and the squadron turns coherently (every ship by
   * the same Δ, preserving formation). The sign is POSITIVE (turning the Piece
   * clockwise turns the fleet the same way; matches the earlier hardware fix). A
   * small dead-band on Δ ignores Piece jitter.
   */
  private steerFromTouch(faction: Faction, orientationRad: number): void {
    const orientationDeg = normalize360(orientationRad * (180 / Math.PI));
    let ref = this.batonSteerRef.get(faction);
    if (!ref) {
      // Touch just began: remember where the baton was pointing; do NOT change
      // course on the mere touch (so placing/holding holds the current course).
      this.batonSteerRef.set(faction, { startDeg: orientationDeg, steering: false });
      return;
    }
    if (!ref.steering) {
      // Hold course until the held baton is rotated past the dead-band; only an
      // intentional turn begins steering (avoids Piece jitter creeping the course).
      if (angleDifference(orientationDeg, ref.startDeg) < Config.BatonSteerToleranceDeg) return;
      ref.steering = true;
    }
    // Touched + rotated → drive EVERY commanded ship onto the baton's ABSOLUTE
    // heading: aim the held baton where the fleet should sail and a scattered
    // squadron converges (each ship turns onto it at its own turn rate). The
    // positive sign matches the hardware-verified rotate direction.
    const heading = normalize360(orientationDeg);
    for (const cmd of this.aliveCommanded(faction)) cmd.setTargetHeading(heading);
  }

  /**
   * Anchors a specific side's baton at a point and commands that side's alive
   * ships within the sphere of influence (captured ONCE here). Used by live
   * placement, mouse placement, and to SEED a baton from a command piece during
   * Setup.
   */
  private seedBaton(faction: Faction, point: Vec2): void {
    this.batonPos.set(faction, { x: point.x, z: point.z });
    this.commandedShips.set(faction, this.pickCommandedShips(faction, point));
  }

  // ---- Baton lifecycle: finger trim (device) ----------------------------

  /**
   * A finger contact on hardware only ever operates the floating controls of a
   * baton — it never places, moves, steers, or dismisses command (the Piece owns
   * that). Routed through the shared control handler so a finger can DRAG the
   * sail thermometer (began/moved/ended) and tap the ammo disc.
   */
  private handleFinger(s: PointerSample): void {
    const world = this.renderer.screenToWorld(s.position.x, s.position.y);
    this.handleControlContact(world, s);
  }

  /**
   * Handles a contact against any baton's floating command controls:
   *   - Sail THERMOMETER: `began` inside it starts a drag and sets the squadron's
   *     sail from the touch height; `moved` keeps setting it live; `ended`
   *     releases. The height snaps to one of the four settings (bottom = Heave-To
   *     … top = Full Sail).
   *   - Ammo disc: a tap (`began`) cycles the squadron's shot type.
   * Returns true if the contact is "owned" by a control, so the caller doesn't
   * treat it as baton placement/steering. Shared by device fingers + browser mouse.
   */
  private handleControlContact(world: Vec2, s: PointerSample): boolean {
    if (s.phase === "moved") {
      if (s.contactId === this.sailDragContact && this.sailDragFaction !== null) {
        this.setSailFromThermometer(this.sailDragFaction, world);
        return true;
      }
      return false;
    }
    if (s.phase === "ended") {
      if (s.contactId === this.sailDragContact) {
        this.sailDragContact = null;
        this.sailDragFaction = null;
        return true;
      }
      return false;
    }

    // began: hit-test the thermometer (start a drag) then the ammo disc (tap).
    for (const [faction, pos] of this.batonPos) {
      if (this.aliveCommanded(faction).length === 0) continue;
      const panel = this.renderer.commandPanelLayout(pos);
      if (this.withinThermometer(world, panel.sail)) {
        this.sailDragContact = s.contactId;
        this.sailDragFaction = faction;
        this.setSailFromThermometer(faction, world);
        return true;
      }
      if (distance(world, panel.ammo) <= panel.r) {
        this.cycleGroupAmmo(faction);
        return true;
      }
    }
    return false;
  }

  /** Whether a world point is within a baton's sail-thermometer hit area (a
   *  generous, touch-friendly box around the track). */
  private withinThermometer(
    world: Vec2,
    sail: { x: number; z: number; halfW: number; halfH: number },
  ): boolean {
    const padX = Config.BatonControlButtonRadius * 0.7;
    const padZ = Config.BatonControlButtonRadius * 0.4;
    return (
      Math.abs(world.x - sail.x) <= sail.halfW + padX &&
      Math.abs(world.z - sail.z) <= sail.halfH + padZ
    );
  }

  /**
   * Maps a touch height on a baton's sail thermometer to one of the four sail
   * settings (bottom = Heave-To(0) … top = Full Sail(3)), snapping to the
   * nearest, and applies it to that side's whole commanded squadron.
   */
  private setSailFromThermometer(faction: Faction, world: Vec2): void {
    const alive = this.aliveCommanded(faction);
    const pos = this.batonPos.get(faction);
    if (alive.length === 0 || !pos) return;
    const sail = this.renderer.commandPanelLayout(pos).sail;
    // Screen-up = more sail: a higher touch (larger world.z) maps to a higher
    // setting. t=1 at the top of the control (Full Sail), t=0 at the bottom
    // (Heave-To). screenToWorld already makes world.z increase as the finger
    // rises, so this gives finger-up → Full, finger-down → Heave-To.
    const t = clamp01((world.z - (sail.z - sail.halfH)) / (2 * sail.halfH));
    const setting = Math.round(t * 3) as SailSetting; // 0..3, snap to nearest
    for (const cmd of alive) cmd.setSail(setting);
  }

  // ---- Baton lifecycle: mouse emulation (browser) -----------------------

  /**
   * Browser mouse-down, resolved by INTENT-BY-TARGET (no brittle global tap/drag
   * threshold): a press on a control trims; a press on a baton roundel arms a
   * steer-drag (or dismisses if it doesn't move); a press on open sea arms a
   * place (resolved on release).
   */
  private handleMouseDown(world: Vec2, s: PointerSample): void {
    this.resetMouseGesture();

    // 1) Floating command controls take priority (thermometer drag / ammo tap).
    if (this.handleControlContact(world, s)) {
      this.mouseGesture = "control";
      this.mousePointer = s.contactId;
      return;
    }

    // 2) On a baton roundel → steer-drag / dismiss.
    const onRoundel = this.batonAt(world);
    if (onRoundel !== null) {
      this.mouseGesture = "roundel";
      this.mouseFaction = onRoundel;
      this.mousePointer = s.contactId;
      this.mouseDownScreen = { x: s.position.x, y: s.position.y };
      this.mouseMoved = false;
      this.batonHeld.set(onRoundel, true);
      return;
    }

    // 3) Open sea → arm a placement.
    this.mouseGesture = "sea";
    this.mousePointer = s.contactId;
    this.mouseDownScreen = { x: s.position.x, y: s.position.y };
    this.mouseMoved = false;
  }

  private handleMouseMove(world: Vec2, s: PointerSample): void {
    // A live sail-thermometer drag takes priority (no down-screen anchor needed).
    if (this.mouseGesture === "control") {
      this.handleControlContact(world, s);
      return;
    }
    if (s.contactId !== this.mousePointer || !this.mouseDownScreen) return;

    if (!this.mouseMoved) {
      const dx = s.position.x - this.mouseDownScreen.x;
      const dy = s.position.y - this.mouseDownScreen.y;
      if (Math.hypot(dx, dy) > Config.BatonMouseDragThresholdPx) this.mouseMoved = true;
    }
    if (!this.mouseMoved) return;

    // Dragging a roundel steers: the commanded squadron heads along the bearing
    // from the baton to the cursor. Applied live so it latches on release.
    if (this.mouseGesture === "roundel" && this.mouseFaction !== null) {
      const faction = this.mouseFaction;
      const pos = this.batonPos.get(faction);
      const alive = this.aliveCommanded(faction);
      if (pos && alive.length > 0) {
        const dir = sub(world, pos);
        // Tint the preview by the point of sail of the heading being aimed, so
        // the course-setter sees the expected speed: green = reach/run, amber =
        // close-hauled, red = in irons (no-go). Falls back to the faction accent
        // for a zero-length drag (no meaningful heading yet).
        let color = accentColor(faction);
        if (magnitude(dir) > 0.001) {
          const heading = vectorToHeading(dir);
          color = pointOfSailColor(heading, this.wind);
          for (const cmd of alive) cmd.setTargetHeading(heading);
        }
        this.renderer.showCoursePreview(
          alive.map((c) => c.position),
          world,
          color,
        );
      }
    }
  }

  private handleMouseUp(world: Vec2, s: PointerSample): void {
    if (this.mouseGesture === "control") {
      this.handleControlContact(world, s); // release the thermometer drag
      this.resetMouseGesture();
      return;
    }
    if (s.contactId !== this.mousePointer) {
      this.resetMouseGesture();
      return;
    }

    if (this.mouseGesture === "roundel" && this.mouseFaction !== null) {
      if (this.mouseMoved) {
        this.renderer.hideCoursePreview(); // heading already latched
        this.batonHeld.set(this.mouseFaction, false);
      } else {
        // A tap on the roundel dismisses that side's baton (emulates a lift).
        this.clearBaton(this.mouseFaction);
      }
    } else if (this.mouseGesture === "sea" && !this.mouseMoved) {
      // A click on open sea places / re-places the baton for the nearest human
      // side and captures its squadron afresh.
      this.placeBaton(world);
    }

    this.resetMouseGesture();
  }

  private resetMouseGesture(): void {
    this.mouseGesture = null;
    this.mouseFaction = null;
    this.mousePointer = null;
    this.mouseDownScreen = null;
    this.mouseMoved = false;
  }

  /** The side whose baton roundel contains `world`, or null. */
  private batonAt(world: Vec2): Faction | null {
    for (const [faction, pos] of this.batonPos) {
      if (distance(world, pos) <= Config.BatonRoundelHitRadius) return faction;
    }
    return null;
  }

  /**
   * Places (or re-places) a Baton of Command at a sea point for the side whose
   * nearest ship is closest to it, capturing that side's alive ships within the
   * sphere of influence. Browser-only (mouse) entry point: device placement is
   * driven by the Piece in handleGlyph. A click with no friendly side in range
   * is ignored.
   */
  private placeBaton(world: Vec2): void {
    const faction = this.nearestHumanFaction(world);
    if (faction === null) return;
    this.seedBaton(faction, world);
    // A mouse-placed baton is not bound to a physical Piece; clear any stale
    // binding / held / steer-session state from a previous owner. Placement keeps
    // the squadron's current course (no heading change here).
    this.batonContact.delete(faction);
    this.batonSteerRef.delete(faction);
    this.batonHeld.set(faction, false);
  }

  /** The human side whose nearest ship is closest to a point, within the baton
   *  radius — or null if no human ship is in range. */
  private nearestHumanFaction(point: Vec2): Faction | null {
    let nearestFaction: Faction | null = null;
    let nearestDist = Config.BatonCommandRadius;
    for (const ship of this.ships) {
      if (!ship.isAlive || !this.isHuman(ship.faction)) continue;
      const d = distance(point, ship.position);
      if (d <= nearestDist) {
        nearestDist = d;
        nearestFaction = ship.faction;
      }
    }
    return nearestFaction;
  }

  /** A side's alive ships within the baton's sphere of influence at `point`. */
  private pickCommandedShips(faction: Faction, point: Vec2): Ship[] {
    return this.ships.filter(
      (ship) =>
        ship.isAlive &&
        ship.faction === faction &&
        distance(point, ship.position) <= Config.BatonCommandRadius,
    );
  }

  /** A side's currently-commanded ships that are still alive. */
  private aliveCommanded(faction: Faction): Ship[] {
    return (this.commandedShips.get(faction) ?? []).filter((c) => c.isAlive);
  }

  /** Every commanded ship across all sides (alive). */
  private allCommanded(): Ship[] {
    const out: Ship[] = [];
    for (const faction of this.commandedShips.keys()) out.push(...this.aliveCommanded(faction));
    return out;
  }

  /** Group ammunition order: cycles a side's commanded squadron to one shot type. */
  private cycleGroupAmmo(faction: Faction): void {
    const alive = this.aliveCommanded(faction);
    if (alive.length === 0) return;
    const next = nextAmmo(alive[0].ammo);
    for (const cmd of alive) cmd.setAmmo(next);
  }

  // ---- Setup -------------------------------------------------------------

  private spawnAllFleets(): void {
    for (const ship of this.ships) {
      (ship.view as ShipView | null)?.destroy();
    }
    this.ships.length = 0;
    // A fresh battle starts with no batons placed and nothing commanded.
    this.batonPos.clear();
    this.commandedShips.clear();
    this.batonContact.clear();
    this.batonSteerRef.clear();
    this.batonHeld.clear();

    // (Re)build the sea + this scenario's cosmetic coastline beneath the fleets.
    buildScene(this.renderer.seaLayer, this.scenario.land);

    // Place both fleets from the chosen scenario's formations, then derive each
    // side's setup pad from where its ships actually ended up.
    this.spawnFleet(Faction.British, this.scenario.british.formation);
    this.spawnFleet(Faction.FrancoSpanish, this.scenario.enemy.formation);
    this.computePads();
  }

  /**
   * Spawns a fleet from a scenario FleetFormation. Ships are placed bow-to-stern
   * from the REAR `anchor` marching FORWARD along `headingDeg`; with `columns > 1`
   * the ship list is split round-robin into that many parallel columns spaced
   * `columnGap` abeam (so a heavy ship leads each column). Spacing within a column
   * is cumulative from each ship's half-length plus `ColumnGap`, so neighbours
   * never overlap regardless of the class mix. This one primitive expresses a
   * single line-ahead, a long battle line, or Nelson's two attack columns alike.
   */
  private spawnFleet(faction: Faction, formation: FleetFormation): void {
    const cols = Math.max(1, formation.columns ?? 1);
    const columnGap = formation.columnGap ?? Config.ColumnGap + 8 * Config.ShipScale;
    const forward = headingToVector(formation.headingDeg); // bow direction / column axis
    const right = headingToVector(formation.headingDeg + 90); // abeam (to starboard)

    // Distribute ships round-robin across the columns so the flagship (index 0)
    // and the next-heaviest ship head columns 0 and 1.
    const columnLists: ShipClass[][] = Array.from({ length: cols }, () => []);
    formation.ships.forEach((c, i) => columnLists[i % cols].push(c));

    for (let ci = 0; ci < cols; ci++) {
      const list = columnLists[ci];
      if (list.length === 0) continue;
      const lateral = (ci - (cols - 1) / 2) * columnGap;
      const colAnchor = add(formation.anchor, scale(right, lateral));

      const lengths = list.map((c) => shipStats(c).length);
      const distFromRear = new Array<number>(list.length);
      distFromRear[list.length - 1] = 0;
      for (let i = list.length - 2; i >= 0; i--) {
        distFromRear[i] =
          distFromRear[i + 1] + lengths[i + 1] * 0.5 + Config.ColumnGap + lengths[i] * 0.5;
      }

      for (let i = 0; i < list.length; i++) {
        const pos = add(colAnchor, scale(forward, distFromRear[i]));
        const ship = new Ship(shipStats(list[i]), faction, pos, formation.headingDeg);
        new ShipView(ship, this.renderer);
        this.ships.push(ship);
      }
    }
  }

  /**
   * Derives each side's setup pad from the centroid of its spawned fleet (clamped
   * into the arena safe area), so the command piece is dropped amid that side's
   * ships and the baton's sphere of influence captures a squadron at its centre —
   * whatever formation/anchor the scenario used.
   */
  private computePads(): void {
    this.padPos.clear();
    const safe = 1 - Config.ArenaSafeInset;
    const maxX = Config.ArenaHalfX * safe - Config.SetupPadRadius;
    const maxZ = Config.ArenaHalfZ * safe - Config.SetupPadRadius;
    for (const faction of PLAYABLE_FACTIONS) {
      const fleet = this.ships.filter((s) => s.faction === faction);
      if (fleet.length === 0) {
        this.padPos.set(faction, defaultPad(faction));
        continue;
      }
      let cx = 0;
      let cz = 0;
      for (const s of fleet) {
        cx += s.position.x;
        cz += s.position.z;
      }
      cx /= fleet.length;
      cz /= fleet.length;
      this.padPos.set(faction, {
        x: clampInto(cx, -maxX, maxX),
        z: clampInto(cz, -maxZ, maxZ),
      });
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
        (ship.view as ShipView | null)?.destroy();
        this.ships.splice(i, 1);
      }
    }
    this.pruneCommanded((s) => s.state !== ShipState.Gone);
  }

  private checkWinCondition(): void {
    // A side wins once all of its enemy's ships have been sunk (the only way a
    // ship leaves play now that boarding/capture is removed).
    const britishAfloat = this.hasLivingShips(Faction.British);
    const francoAfloat = this.hasLivingShips(Faction.FrancoSpanish);
    if (britishAfloat && francoAfloat) return;

    this.phase = GamePhase.GameOver;
    this.gameOverTimer = 0;
    // Match is over: keep the menu live so the player can Restart (new battle)
    // or Quit straight from the overlay.
    this.pauseMenu?.enterGameOver();
    this.winner = britishAfloat
      ? Faction.British
      : francoAfloat
        ? Faction.FrancoSpanish
        : Faction.Neutral;
  }

  /** Rematch: a fresh match of the SAME scenario, back in Setup (re-place pieces),
   *  with the scenario's fixed wind reset. */
  restart(): void {
    this.wind = new Wind(this.scenario.windFromDegrees);
    this.enterSetup();
  }

  // ---- Selection / queries ----------------------------------------------

  /** Drops ships failing `keep` from every side's commanded set in place. */
  private pruneCommanded(keep: (s: Ship) => boolean): void {
    for (const [faction, list] of this.commandedShips) {
      this.commandedShips.set(
        faction,
        list.filter(keep),
      );
    }
  }

  private refreshCommandVisuals(): void {
    // Drop ships from the commanded sets that are no longer valid (sunk, or
    // switched to AI in 2-player).
    this.pruneCommanded((s) => s.isAlive && this.isHuman(s.faction));
    const commandedSet = new Set(this.allCommanded());
    for (const ship of this.ships) {
      const commanded = commandedSet.has(ship) && ship.isAlive;
      (ship.view as ShipView | null)?.setCommanded(commanded, ship.faction);
    }
  }

  private updateCourseVisuals(): void {
    const commanded = this.phase === GamePhase.GameOver ? [] : this.allCommanded();
    if (commanded.length === 0) {
      this.renderer.hideHeadingLine();
      return;
    }
    // Colour each ship's ordered-course vector by the point of sail of that
    // heading (green = reach/run, amber = close-hauled, red = in-irons), the same
    // ramp as the on-ship dot. On device these persistent vectors ARE the live
    // rotate-to-steer preview (they redraw every frame as the Piece turns), so
    // rotating the baton shows the expected-speed colour live.
    const lines = commanded.map((s) => ({
      from: s.position,
      headingDeg: s.targetHeadingDeg,
      length: s.stats.length * 2.5,
      color: pointOfSailColor(s.targetHeadingDeg, this.wind),
    }));
    this.renderer.showHeadingLines(lines);
  }

  private refreshBatonVisuals(): void {
    if (this.phase === GamePhase.GameOver || this.batonPos.size === 0) {
      this.renderer.hideBatons();
      this.renderer.hideCommandPanels();
      return;
    }
    const batons: { pos: Vec2; color: number; held: boolean }[] = [];
    const panels: { pos: Vec2; sail: number; ammo: number }[] = [];
    for (const [faction, pos] of this.batonPos) {
      const alive = this.aliveCommanded(faction);
      batons.push({
        pos,
        color: accentColor(faction),
        held: this.batonHeld.get(faction) === true,
      });
      // Per-side group command panel, reflecting that squadron's (uniform) sail +
      // ammo. Only while playing (no trimming during setup) and with ships in hand.
      if (this.phase === GamePhase.Playing && alive.length > 0) {
        panels.push({ pos, sail: alive[0].sail, ammo: alive[0].ammo });
      }
    }
    this.renderer.showBatons(batons, Config.BatonCommandRadius);
    this.renderer.showCommandPanels(panels);
  }

  /** Draws the Setup placement pads (one per side) while in Setup; hides them
   *  otherwise. Each pad shows its side, a prompt, and a ready/AI state. */
  private refreshSetupVisuals(): void {
    if (this.phase !== GamePhase.Setup) {
      this.renderer.hideSetupPads();
      this.hud.setSetupOverlay(false, "");
      return;
    }
    const pads = PLAYABLE_FACTIONS.map((faction) => {
      const human = this.isHuman(faction);
      const ready = this.isReady(faction);
      const subtitle = !human
        ? `AI · ${personaName(this.aiPersona)}`
        : ready
          ? "✓ In command"
          : "Place your command piece";
      return {
        pos: this.padPosition(faction),
        radius: Config.SetupPadRadius,
        color: accentColor(faction),
        title: this.sideLabel(faction),
        subtitle,
        ready,
      };
    });
    this.renderer.showSetupPads(pads);
    this.hud.setSetupOverlay(true, this.setupStatus());
  }

  private setupStatus(): string {
    if (this.countingDown) {
      return `All hands on deck — battle stations in ${Math.ceil(this.setupCountdown)}…`;
    }
    const waiting = PLAYABLE_FACTIONS.filter((f) => this.needsPlacement(f)).map((f) =>
      this.sideLabel(f),
    );
    if (waiting.length === 0) return "Standing by…";
    if (waiting.length === 1) {
      return `Place ${waiting[0]}'s command piece to begin`;
    }
    return `Waiting for ${waiting.join(" & ")} to place their command pieces`;
  }

  /** A faction's setup-pad centre for this scenario (fleet centroid, clamped). */
  private padPosition(faction: Faction): Vec2 {
    return this.padPos.get(faction) ?? defaultPad(faction);
  }

  /** Scenario display label for a side (e.g. "Royal Navy" / "Combined Fleet"). */
  private sideLabel(faction: Faction): string {
    if (faction === Faction.British) return this.scenario.british.label;
    if (faction === Faction.FrancoSpanish) return this.scenario.enemy.label;
    return displayName(faction);
  }

  private isHuman(faction: Faction): boolean {
    return this.control.get(faction) === ControlMode.Human;
  }

  private hasLivingShips(faction: Faction): boolean {
    return this.ships.some((s) => s.isAlive && s.faction === faction);
  }
}
