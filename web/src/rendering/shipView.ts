// Builds and animates the visual representation of a Ship — a PixiJS view that
// renders a textured tall-ship sprite (per class) for the hull/deck/sails/guns,
// with procedural overlays on top: status rings, on-ship control buttons,
// health bars, selection ring and a faction-coloured gunwale stripe + stern
// pennant. If a ship texture fails to load it falls back to the fully
// procedural drawing (buildHull/Deck/Cannons/Sails/etc.) so it never blanks.

import { Container, Graphics, Sprite } from "pixi.js";
import * as Config from "../core/config";
import { Faction, accentColor } from "../core/faction";
import { headingToVector, signedDelta } from "../core/nav";
import { Deg2Rad, clamp, lerp, clamp01 } from "../core/mathf";
import { add, scale, distance, type Vec2 } from "../core/vec";
import { AmmoType } from "../ships/ammo";
import { SailSetting, throttleFactor } from "../ships/sail";
import { Ship, BroadsideSide, type ShipViewHooks } from "../ships/ship";
import { ShipClass, type ShipStats } from "../ships/shipClass";
import { hullOutline, hullEdgePoint, toLocalPoly } from "./geometry";
import { shipTextures } from "./assets";
import type { Renderer } from "./renderer";
import type { Wind } from "../combat/wind";

// Fraction of a ship texture's height occupied by the hull (bow-to-stern). The
// art is bow-UP with transparent margins; this maps world `length` onto the
// painted hull so on-screen length ≈ class length (selection/aim stay aligned).
const SPRITE_LENGTH_FILL = 0.86;

// Faction gunwale-stripe outline proportions (a thin coloured hull rim over the
// neutral-wood sprite), as fractions of the ship's world length.
const FACTION_OUTLINE_LEN = 0.86;
const FACTION_OUTLINE_BEAM = 0.2;

/** A tappable on-ring control around a selected ship — port of `ShipControl`. */
export enum ShipControl {
  None = 0,
  Port,
  Starboard,
  AmmoCycle,
}

// ---- Palette (Unity 0..1 colours packed to 0xRRGGBB) ----
const C_HULL = 0x573821;
const C_RAIL = 0x332112;
const C_DECK = 0xb88a52;
const C_PLANK_LINE = 0x8f6a3c;
const C_WOOD = 0x735133;
const C_IRON = 0x17171c;
const C_ROPE = 0x29211a;
const C_GOLD = 0xdbb351;
const C_STEP = 0xad8451;
const C_GRATING = 0x3f2c18;
const C_SAIL = 0xefe7d4;
const C_ICON = 0xf5f7ff;
const C_BAR_BG = 0x0d0d12;
const C_BAR_HULL = 0xd94033;
const C_BAR_RIG = 0x59cc66;

// Ammo colour key: grey = round shot, green = bar shot.
function ammoColor(ammo: AmmoType): number {
  switch (ammo) {
    case AmmoType.RoundShot:
      return 0xd1d1d9;
    case AmmoType.BarShot:
      return 0x52cc75;
    default:
      return 0x808080;
  }
}

interface ControlButton {
  type: ShipControl;
  angleDeg: number;
  disc: Graphics;
  baseColor: number;
  flash: number;
}

interface SailPart {
  canvas: Graphics;
}

export class ShipView implements ShipViewHooks {
  readonly container = new Container();

  private readonly ship: Ship;
  private readonly renderer: Renderer;

  private mastZ: number[] = [];

  /** Sprite hull when a class texture is available; otherwise procedural. */
  private useSprite = false;
  private hullSprite: Sprite | null = null;
  private factionOutline = new Graphics();

  private stripeGfx = new Graphics();
  private flashGfx = new Graphics();
  private flagGfx = new Graphics();

  private sailGroup = new Container();
  private sails: SailPart[] = [];
  private sailWidth: number[] = [];
  private sailDepth: number[] = [];

  // Command bubble: a distinct glowing ring + translucent disc drawn around the
  // ship the Baton of Command currently commands (replaces the old selection
  // ring). Toggled by setCommanded; pulses gently while active.
  private commandGfx = new Graphics();
  private commanded = false;
  private commandPulse = 0;

  private controlsContainer = new Container();
  private controlButtons: ControlButton[] = [];
  private ammoIconGfx: Graphics | null = null;
  private ammoIconBtnR = 0;
  private buttonHitRadius = 0;

  // Command controls (sail + ammo toggles): shown ONLY on the commanded ship as
  // part of its command bubble (toggled by setCommanded), not on every ship.
  private commandControls = new Container();

  // Shot-type toggle (icon), one of the command controls.
  private ammoBadge = new Container();
  private ammoBadgeGfx = new Graphics();
  private ammoBadgeR = 0;

  // Sail-SETTING toggle (the billow glyph), one of the command controls. Hit-
  // tested via sailBadgeHit when this ship is the commanded one. Glyph redrawn
  // only on change.
  private sailBadge = new Container();
  private sailIconGfx = new Graphics();
  private sailIconR = 0;
  private sailBadgeR = 0;
  private lastSail: SailSetting = -1 as SailSetting;

  // Sailing-quality indicator (LEFT slot): a colour-coded point-of-sail gauge —
  // green when sailing well (Beam/Broad Reach, Running), amber when Close-Hauled,
  // red when In Irons (the no-go state, folded in here in place of the old
  // separate in-irons badge). Informational only (non-interactive). Redrawn only
  // when its state/fill bucket changes (no per-frame allocation).
  private qualityGfx = new Graphics();
  private qualityR = 0;
  private lastQualityBucket = -1;

  private statusBars = new Container();
  private statusLift = 0;
  private hullBar!: { fill: Graphics; width: number };
  private riggingBar!: { fill: Graphics; width: number };

  private lastAmmo: AmmoType = -1 as AmmoType;
  private hitFlash = 0;

  constructor(ship: Ship, renderer: Renderer) {
    this.ship = ship;
    this.renderer = renderer;

    this.computeMasts(ship.stats);

    const texture = shipTextures[ship.shipClass] ?? null;
    this.useSprite = texture !== null;

    // Z-order: flat-on-sea rings/controls beneath the hull, then the hull
    // (textured sprite or procedural layers), then faction stripe + stern
    // pennant, with status bars on top.
    this.container.addChild(this.commandGfx);
    this.container.addChild(this.controlsContainer);

    if (texture) {
      // Bow-UP art aligns with the container's local -Y bow, so the sprite needs
      // no rotation offset; the container's heading rotation steers it. Scale so
      // the painted hull length ≈ the class's world length.
      const sprite = new Sprite(texture);
      sprite.anchor.set(0.5);
      const s = ship.stats.length / (texture.height * SPRITE_LENGTH_FILL);
      sprite.scale.set(s);
      this.hullSprite = sprite;
      this.container.addChild(sprite);
      this.container.addChild(this.factionOutline);
    } else {
      // Procedural fallback (full wooden tall-ship drawing).
      const hullGfx = new Graphics();
      this.container.addChild(hullGfx);
      this.container.addChild(this.stripeGfx);
      const deckGfx = new Graphics();
      this.container.addChild(deckGfx);
      this.container.addChild(this.flashGfx);
      this.container.addChild(this.sailGroup);

      this.buildHull(hullGfx, ship.stats);
      this.buildStripe(ship.stats);
      this.buildDeck(deckGfx, ship.stats);
      this.buildFlash(ship.stats);
      this.buildDeckFeatures(deckGfx, ship.stats);
      this.buildBowsprit(deckGfx, ship.stats);
      this.buildCannons(deckGfx, ship.stats);
      this.buildRigging(deckGfx, ship.stats);
      this.buildSails(ship.stats);
    }

    // Common overlays (always on top of either hull style).
    this.container.addChild(this.flagGfx);
    this.container.addChild(this.statusBars);

    this.buildFlag(ship.stats);
    this.buildCommandBubble(ship.stats);
    this.buildControlButtons(ship.stats);
    this.buildStatusBars(ship.stats);
    this.buildSailQualityBadge(ship.stats);
    // Command controls (sail + ammo) live in their own container, shown only on
    // the commanded ship; the quality gauge + health bars are always visible.
    this.statusBars.addChild(this.commandControls);
    this.buildSailBadge(ship.stats);
    this.buildAmmoBadge(ship.stats);

    this.applyFactionColors();
    this.setCommanded(false, Faction.Neutral);

    this.syncTransform();
    renderer.shipsLayer.addChild(this.container);
    ship.view = this;
  }

  destroy(): void {
    this.container.destroy({ children: true });
  }

  private computeMasts(stats: ShipStats): void {
    const masts: number = stats.shipClass === ShipClass.Frigate ? 2 : 3;
    this.mastZ = [];
    for (let i = 0; i < masts; i++) {
      const t = masts === 1 ? 0.5 : i / (masts - 1);
      this.mastZ.push(lerp(stats.length * 0.3, -stats.length * 0.28, t));
    }
  }

  // ---- Hull / deck -------------------------------------------------------

  private buildHull(g: Graphics, stats: ShipStats): void {
    g.poly(toLocalPoly(hullOutline(stats.length, stats.beam))).fill({ color: C_HULL });
    g.poly(toLocalPoly(hullOutline(stats.length * 0.95, stats.beam * 0.9))).fill({ color: C_RAIL });
  }

  private buildStripe(stats: ShipStats): void {
    // Solid faction-accent silhouette; the (slightly smaller) deck drawn on top
    // leaves it showing as a coloured gun-stripe band around the rail. Tinted
    // white so `tint` can carry the faction accent.
    this.stripeGfx
      .poly(toLocalPoly(hullOutline(stats.length * 0.88, stats.beam * 0.8)))
      .fill({ color: 0xffffff });
  }

  private buildDeck(g: Graphics, stats: ShipStats): void {
    const deckLen = stats.length * 0.72;
    const deckBeam = stats.beam * 0.56;
    const deckZ = -stats.length * 0.03;
    const deckOutline = hullOutline(deckLen, deckBeam).map((p) => ({ x: p.x, z: p.z + deckZ }));
    g.poly(toLocalPoly(deckOutline)).fill({ color: C_DECK });

    const planks = 9;
    for (let i = 1; i < planks; i++) {
      const z = lerp(-deckLen * 0.5, deckLen * 0.5, i / planks) + deckZ;
      const halfW = deckBeam * 0.5 * 0.85;
      g.moveTo(-halfW, -z).lineTo(halfW, -z).stroke({ width: 0.5, color: C_PLANK_LINE, alpha: 0.5 });
    }
  }

  private buildFlash(stats: ShipStats): void {
    this.flashGfx.poly(toLocalPoly(hullOutline(stats.length, stats.beam))).fill({ color: 0xffffff });
    this.flashGfx.alpha = 0;
  }

  private buildDeckFeatures(g: Graphics, stats: ShipStats): void {
    const mastR = stats.beam * 0.085;
    for (const mz of this.mastZ) {
      const p = lp(0, mz);
      g.circle(p.x, p.y, mastR).fill({ color: C_WOOD });
      g.circle(p.x, p.y, mastR * 0.5).fill({ color: C_IRON });
    }

    const capR = stats.beam * 0.13;
    const cap = lp(0, -stats.length * 0.04);
    g.circle(cap.x, cap.y, capR).fill({ color: C_WOOD });
    g.circle(cap.x, cap.y, capR * 0.5).fill({ color: C_IRON });

    const hatches: number = stats.shipClass === ShipClass.FirstRate ? 3 : 2;
    const hatch = stats.beam * 0.24;
    for (let i = 0; i < hatches; i++) {
      const z = lerp(stats.length * 0.2, -stats.length * 0.2, hatches === 1 ? 0.5 : i / (hatches - 1));
      this.drawGrating(g, 0, z, hatch);
    }

    const stepW = stats.beam * 0.22;
    const stepGap = stats.length * 0.022;
    for (let s = 0; s < 4; s++) {
      rectAt(g, 0, stats.length * 0.3 + s * stepGap, stepW, stepGap * 0.6, C_STEP);
    }
  }

  private drawGrating(g: Graphics, x: number, z: number, size: number): void {
    rectAt(g, x, z, size, size, C_GRATING, 0.92);
    const p = lp(x, z);
    const h = size / 2;
    for (let i = 1; i < 4; i++) {
      const o = -h + (size * i) / 4;
      g.moveTo(p.x - h, p.y + o).lineTo(p.x + h, p.y + o).stroke({ width: 0.5, color: 0x20160c });
      g.moveTo(p.x + o, p.y - h).lineTo(p.x + o, p.y + h).stroke({ width: 0.5, color: 0x20160c });
    }
  }

  private buildBowsprit(g: Graphics, stats: ShipStats): void {
    const bowZ = stats.length * 0.5;
    const spritLen = stats.length * 0.18;
    rectAt(g, 0, bowZ + spritLen * 0.5, stats.beam * 0.07, spritLen, C_WOOD);
    const fig = lp(0, bowZ + spritLen * 0.05);
    g.circle(fig.x, fig.y, stats.beam * 0.12 * 0.5).fill({ color: C_GOLD });
  }

  private buildCannons(g: Graphics, stats: ShipStats): void {
    const barrelLen = stats.beam * 0.3;
    const barrelWid = stats.beam * 0.085;
    const portSize = stats.beam * 0.13;

    const n = clamp(stats.gunsPerBroadside, 0, 8);
    for (let i = 0; i < n; i++) {
      const u = lerp(0.3, 0.74, n === 1 ? 0.5 : i / (n - 1));
      for (let s = 0; s < 2; s++) {
        const side = s === 0 ? 1 : -1;
        const edge = hullEdgePoint(stats.length, stats.beam, u, side);
        rectAt(g, edge.x * 0.96, edge.z, portSize, portSize, C_IRON);
        rectAt(g, edge.x + side * barrelLen * 0.35, edge.z, barrelLen, barrelWid, C_IRON);
      }
    }

    const chaseLen = stats.beam * 0.34;
    const chaseWid = stats.beam * 0.1;
    if (stats.chaseGuns > 0) {
      rectAt(g, 0, stats.length * 0.44, chaseWid, chaseLen, C_IRON);
    }
    if (stats.chaseGuns > 1) {
      rectAt(g, 0, -stats.length * 0.46, chaseWid, chaseLen, C_IRON);
    }
  }

  private buildRigging(g: Graphics, stats: ShipStats): void {
    const bowZ = stats.length * 0.52;
    const sternZ = -stats.length * 0.5;
    const rope = Math.max(0.4, stats.beam * 0.025);

    for (let i = 0; i < this.mastZ.length; i++) {
      const base: Vec2 = { x: 0, z: this.mastZ[i] };
      if (i === 0) line(g, base, { x: 0, z: bowZ }, rope);
      if (i === this.mastZ.length - 1) line(g, base, { x: 0, z: sternZ }, rope);

      const u = clamp01(
        (this.mastZ[i] + stats.length * 0.5) / (stats.length) - 0.02,
      );
      const sb = hullEdgePoint(stats.length, stats.beam, u, 1);
      const pt = hullEdgePoint(stats.length, stats.beam, u, -1);
      line(g, base, sb, rope);
      line(g, base, pt, rope);
    }
  }

  // ---- Sails -------------------------------------------------------------

  private buildSails(stats: ShipStats): void {
    const count = this.mastZ.length;
    for (let i = 0; i < count; i++) {
      const holder = new Container();
      const hp = lp(0, this.mastZ[i]);
      holder.position.set(hp.x, hp.y);

      const w = stats.beam * 2.0;
      const d = stats.length * 0.16;
      this.sailWidth.push(w);
      this.sailDepth.push(d);

      const yard = new Graphics();
      rectAt(yard, 0, d * 0.5, w, stats.beam * 0.06, C_WOOD);
      holder.addChild(yard);

      const canvas = new Graphics();
      canvas.rect(-w / 2, -d / 2, w, d).fill({ color: C_SAIL, alpha: 0.72 });
      holder.addChild(canvas);

      this.sails.push({ canvas });
      this.sailGroup.addChild(holder);
    }
  }

  private buildFlag(stats: ShipStats): void {
    this.drawFlag(accentColor(this.ship.faction), stats);
  }

  /** Faction-coloured triangular stern pennant streaming aft (clear side cue). */
  private drawFlag(color: number, stats: ShipStats = this.ship.stats): void {
    const len = stats.length;
    const w = Math.max(len * 0.085, stats.beam * 0.8);
    const baseZ = -len * 0.46;
    const tipZ = -len * 0.62;
    const a = lp(-w * 0.5, baseZ);
    const b = lp(w * 0.5, baseZ);
    const tip = lp(0, tipZ);
    this.flagGfx.clear();
    this.flagGfx.poly([a.x, a.y, b.x, b.y, tip.x, tip.y]).fill({ color });
  }

  /**
   * Faction "gunwale stripe": a thin coloured rim traced just inside the hull
   * silhouette, drawn over the neutral-wood sprite so British (blue) and
   * Franco-Spanish (orange) read at a glance without tinting the whole hull.
   */
  private drawFactionOutline(color: number): void {
    const len = this.ship.stats.length;
    this.factionOutline.clear();
    this.factionOutline
      .poly(toLocalPoly(hullOutline(len * FACTION_OUTLINE_LEN, len * FACTION_OUTLINE_BEAM)))
      .stroke({ width: Math.max(1.5, len * 0.02), color, alpha: 0.95 });
  }

  private buildCommandBubble(stats: ShipStats): void {
    const r = Math.max(stats.length, stats.beam) * 0.78;
    const band = 0.7 * Config.ShipScale;
    // A translucent glow disc + a bright ring (tinted to the faction in
    // setCommanded). Drawn beneath the hull so the hull reads clearly on top.
    this.commandGfx.circle(0, 0, r).fill({ color: 0xffffff, alpha: 0.12 });
    this.commandGfx.circle(0, 0, r + band / 2).stroke({ width: band, color: 0xffffff });
    this.commandGfx.visible = false;
  }

  // ---- Control buttons ---------------------------------------------------

  private buildControlButtons(_stats: ShipStats): void {
    // No selected-only ring buttons remain: course is set by click-hold-drag, and
    // both sail and shot are always-present, directly-tappable badges
    // (buildSailBadge / buildAmmoBadge) that work without selecting the ship. The
    // controls container + hit-test machinery are kept (harmless, no buttons) so
    // the rest of the selection plumbing stays untouched.
    this.controlsContainer.visible = false;
  }

  /**
   * Draws the sail-SETTING glyph (mast + billowed canvas whose size shrinks with
   * less sail; Heave To shows a furled mast with a red X) into the always-visible
   * badge. Redrawn only when the setting changes (no per-frame allocation).
   */
  private drawSailIcon(setting: SailSetting): void {
    drawSailGlyph(this.sailIconGfx, this.sailIconR, setting);
  }

  private addControl(type: ShipControl, angleDeg: number, r: number, btnR: number, baseColor: number): void {
    const group = new Container();
    const dir = headingToVector(angleDeg);
    const p = lp(dir.x * r, dir.z * r);
    group.position.set(p.x, p.y);

    const disc = new Graphics();
    disc.circle(0, 0, btnR).fill({ color: baseColor });
    group.addChild(disc);
    this.addControlIcon(type, group, btnR);

    this.controlsContainer.addChild(group);
    this.controlButtons.push({ type, angleDeg, disc, baseColor, flash: 0 });
  }

  private addControlIcon(type: ShipControl, group: Container, btnR: number): void {
    const icon = new Graphics();
    switch (type) {
      case ShipControl.Starboard:
        // arrow pointing to the ship's right (+X)
        icon.poly([btnR * 0.5, 0, -btnR * 0.4, -btnR * 0.5, -btnR * 0.4, btnR * 0.5]).fill({ color: C_ICON });
        break;
      case ShipControl.Port:
        icon.poly([-btnR * 0.5, 0, btnR * 0.4, -btnR * 0.5, btnR * 0.4, btnR * 0.5]).fill({ color: C_ICON });
        break;
      case ShipControl.AmmoCycle:
        // The icon itself shows the loaded shot (cannonball vs bar shot) and is
        // redrawn when the shot is cycled.
        this.ammoIconGfx = icon;
        this.ammoIconBtnR = btnR;
        this.drawAmmoIcon(this.ship.ammo);
        break;
    }
    group.addChild(icon);
  }

  /**
   * Draws the loaded-shot icon procedurally into the ammo button:
   *   Round shot → a solid cannonball (filled circle).
   *   Bar shot   → two balls joined by a bar (the classic bar/chain silhouette).
   * Filled in the ammo colour (grey = round, green = bar) for an obvious read.
   */
  private drawAmmoIcon(ammo: AmmoType): void {
    const g = this.ammoIconGfx;
    if (!g) return;
    const r = this.ammoIconBtnR;
    const col = ammoColor(ammo);
    g.clear();

    if (ammo === AmmoType.BarShot) {
      g.rect(-r * 0.5, -r * 0.1, r, r * 0.2).fill({ color: col });
      g.circle(-r * 0.5, 0, r * 0.24).fill({ color: col });
      g.circle(r * 0.5, 0, r * 0.24).fill({ color: col });
    } else {
      g.circle(0, 0, r * 0.45).fill({ color: col });
    }
  }

  /** Tests a world-sea point against active control buttons (port of TryHitControl). */
  tryHitControl(worldPoint: Vec2): ShipControl {
    if (!this.controlsContainer.visible) return ShipControl.None;

    const len = this.ship.stats.length;
    const r = len * 0.92;
    let best = this.buttonHitRadius;
    let hit = ShipControl.None;
    for (const b of this.controlButtons) {
      const dir = headingToVector(b.angleDeg + this.ship.headingDeg);
      const world = add(this.ship.position, scale(dir, r));
      const d = distance(worldPoint, world);
      if (d <= best) {
        best = d;
        hit = b.type;
      }
    }
    return hit;
  }

  flashControl(control: ShipControl): void {
    for (const b of this.controlButtons) {
      if (b.type === control) {
        b.flash = 0.18;
        b.disc.tint = 0xffffff;
        break;
      }
    }
  }

  private updateControlButtons(dt: number): void {
    if (!this.controlsContainer.visible) return;
    for (const b of this.controlButtons) {
      if (b.flash > 0) {
        b.flash -= dt;
        const k = clamp01(b.flash / 0.18);
        b.disc.tint = lerpColor(b.baseColor, 0xffffff, k);
      }
    }
  }

  // ---- Status bars -------------------------------------------------------

  private buildStatusBars(stats: ShipStats): void {
    // The bars float just "above" the hull (screen-up) regardless of heading;
    // their world offset is applied each frame in updateVisuals so they stay
    // north-up and readable from any seat.
    this.statusLift = Math.max(stats.length, stats.beam) * 0.8;

    // Two stacked bars (Hull, Rigging) centred on the anchor: rigging sits one
    // half-gap above, hull one half-gap below, so the pair reads as a tidy unit.
    const width = stats.length * 1.5;
    const thick = stats.length * 0.07;
    const gap = stats.length * 0.11;
    this.riggingBar = this.makeBar(C_BAR_RIG, width, thick, gap * 0.5);
    this.hullBar = this.makeBar(C_BAR_HULL, width, thick, -gap * 0.5);
  }

  /**
   * Always-visible sail-setting badge, parented to the (north-up) status-bar
   * group so it stays upright above the hull without per-frame counter-rotation.
   * Redrawn only when the setting changes.
   */
  private buildSailBadge(stats: ShipStats): void {
    const r = stats.length * 0.12;
    this.sailIconR = r;
    this.sailBadgeR = r;
    // Centre of the single status row: quality gauge (left) · sail toggle
    // (centre) · ammo toggle (right). The ammo badge sits 0.5·len to the right,
    // wider than the summed ~0.38·len tap radii, so the two clickable circles
    // never overlap.
    this.sailBadge.position.set(0, -stats.length * 0.32);

    const disc = new Graphics();
    disc.circle(0, 0, r).fill({ color: 0x24435e, alpha: 0.85 });
    this.sailBadge.addChild(disc);
    this.sailBadge.addChild(this.sailIconGfx);
    this.commandControls.addChild(this.sailBadge);

    this.drawSailIcon(this.ship.sail);
    this.lastSail = this.ship.sail;
  }

  /**
   * Tests a world-sea point against this ship's always-present sail badge —
   * identical approach to {@link ammoBadgeHit}: recover the badge's world centre
   * via toGlobal → screenToWorld (DPR-safe) and compare against its radius. Lets
   * the sail plan be cycled by tapping the badge on any human ship, no selection
   * required.
   */
  sailBadgeHit(worldPoint: Vec2): boolean {
    if (!this.ship.isAlive || !this.commanded) return false;
    const g = this.sailBadge.toGlobal({ x: 0, y: 0 });
    const world = this.renderer.screenToWorld(g.x, g.y);
    return distance(worldPoint, world) <= this.sailBadgeR * 1.6;
  }

  /**
   * Always-visible shot-type badge, parented to the (north-up) status group so
   * it shows on every ship and stays upright. Also serves as the ever-present
   * shot toggle (hit-tested in Game input via {@link ammoBadgeHit}).
   */
  private buildAmmoBadge(stats: ShipStats): void {
    const r = stats.length * 0.12;
    this.ammoBadgeR = r;
    // Right end of the single status row (sail toggle is at centre, 0.5·len away
    // — wider than the summed ~0.38·len tap radii — so the two tap circles can't
    // overlap).
    this.ammoBadge.position.set(stats.length * 0.5, -stats.length * 0.32);

    const disc = new Graphics();
    disc.circle(0, 0, r).fill({ color: 0x2a2433, alpha: 0.85 });
    this.ammoBadge.addChild(disc);
    this.ammoBadge.addChild(this.ammoBadgeGfx);
    this.commandControls.addChild(this.ammoBadge);

    this.drawAmmoBadge(this.ship.ammo);
  }

  /** Draws the loaded-shot glyph into the always-visible badge (round vs bar). */
  private drawAmmoBadge(ammo: AmmoType): void {
    const g = this.ammoBadgeGfx;
    const r = this.ammoBadgeR;
    const col = ammoColor(ammo);
    g.clear();
    if (ammo === AmmoType.BarShot) {
      g.rect(-r * 0.5, -r * 0.1, r, r * 0.2).fill({ color: col });
      g.circle(-r * 0.5, 0, r * 0.24).fill({ color: col });
      g.circle(r * 0.5, 0, r * 0.24).fill({ color: col });
    } else {
      g.circle(0, 0, r * 0.45).fill({ color: col });
    }
  }

  /**
   * Tests a world-sea point against this ship's always-present shot badge.
   * The badge lives in the north-up status group, so its world position is
   * recovered via toGlobal → screenToWorld (DPR-safe, same path as the pointer).
   */
  ammoBadgeHit(worldPoint: Vec2): boolean {
    if (!this.ship.isAlive || !this.commanded) return false;
    const g = this.ammoBadge.toGlobal({ x: 0, y: 0 });
    const world = this.renderer.screenToWorld(g.x, g.y);
    return distance(worldPoint, world) <= this.ammoBadgeR * 1.6;
  }

  /**
   * Sailing-quality gauge (LEFT slot): a colour-coded point-of-sail dial on a
   * dark roundel that shows how well the ship is sailing its current heading
   * relative to the wind. The arc fills further and shifts colour with quality —
   * green (Beam/Broad Reach, Running), amber (Close-Hauled), red (In Irons, the
   * old separate in-irons warning, now folded in). Informational only; parented
   * to the north-up status group so it stays upright. Redrawn only on state
   * change (see updateSailQuality), so there's no per-frame allocation.
   */
  private buildSailQualityBadge(stats: ShipStats): void {
    const r = stats.length * 0.12;
    this.qualityR = r;

    const group = new Container();
    group.position.set(-stats.length * 0.5, -stats.length * 0.32);

    const disc = new Graphics();
    disc.circle(0, 0, r).fill({ color: 0x161b22, alpha: 0.85 });
    group.addChild(disc);
    group.addChild(this.qualityGfx);
    this.statusBars.addChild(group);

    this.drawSailQuality(2, 1);
    this.lastQualityBucket = -1;
  }

  /**
   * Draws the point-of-sail gauge: a 270° dial whose coloured arc fills by
   * `factor` (point-of-sail speed multiplier, 0..1) and is tinted by `state`
   * (0 = red/In Irons, 1 = amber/Close-Hauled, 2 = green/good). A centre dot in
   * the same colour gives an at-a-glance read top-down.
   */
  private drawSailQuality(state: number, factor: number): void {
    const g = this.qualityGfx;
    const r = this.qualityR;
    g.clear();

    const col = SAIL_QUALITY_COLORS[state] ?? SAIL_QUALITY_COLORS[2];
    const a0 = Math.PI * 0.75; // start lower-left
    const a1 = Math.PI * 2.25; // sweep 270° round to lower-right
    const aFill = a0 + (a1 - a0) * clamp01(factor);

    // Track (unlit) then the lit portion.
    g.arc(0, 0, r * 0.6, a0, a1).stroke({ width: r * 0.26, color: 0x0c1014, alpha: 0.9 });
    g.arc(0, 0, r * 0.6, a0, aFill).stroke({ width: r * 0.26, color: col });
    g.circle(0, 0, r * 0.2).fill({ color: col });
  }

  /**
   * Recolours/refills the sailing-quality gauge from the ship's point of sail.
   * Colour comes from `ship.pointOfSail` (the wind model's classification) and
   * the arc fill from the point-of-sail speed factor for the current heading.
   * Quantized to a small bucket so the gauge is only redrawn when it changes.
   */
  private updateSailQuality(ship: Ship, wind: Wind): void {
    const factor = clamp01(wind.pointOfSailFactorFor(ship.headingDeg));
    const state = sailQualityState(ship.pointOfSail);
    const bucket = state * 16 + Math.round(factor * 8);
    if (bucket === this.lastQualityBucket) return;
    this.lastQualityBucket = bucket;
    this.drawSailQuality(state, factor);
  }

  private makeBar(color: number, width: number, thickness: number, zOffset: number): { fill: Graphics; width: number } {
    // Higher z (further forward / "up" on screen) → more negative local y.
    const y = -zOffset;
    const bg = new Graphics();
    bg.rect(-width / 2, y - thickness / 2, width, thickness).fill({ color: C_BAR_BG });
    this.statusBars.addChild(bg);

    const fill = new Graphics();
    fill.rect(-width / 2, y - (thickness * 0.88) / 2, width, thickness * 0.88).fill({ color });
    this.statusBars.addChild(fill);
    return { fill, width };
  }

  // ---- Live updates -----------------------------------------------------

  /** Applies the faction accent to the gunwale stripe/outline and pennant. */
  private applyFactionColors(): void {
    const col = accentColor(this.ship.faction);
    if (this.useSprite) {
      this.drawFactionOutline(col);
    } else {
      this.stripeGfx.tint = col;
    }
    this.drawFlag(col);
  }

  /** Marks this ship as the one under the Baton of Command: shows the glowing
   *  command bubble (tinted to its faction) and its sail/ammo command controls. */
  setCommanded(commanded: boolean, faction: Faction): void {
    this.commanded = commanded;
    this.commandGfx.visible = commanded;
    if (commanded) this.commandGfx.tint = accentColor(faction);
    this.commandControls.visible = commanded;
    this.controlsContainer.visible = commanded;
    if (!commanded) {
      this.commandPulse = 0;
      this.commandGfx.alpha = 1;
    }
  }

  flashHit(): void {
    this.hitFlash = 0.15;
  }

  playBroadsideSmoke(side: BroadsideSide): void {
    const normal = this.ship.broadsideNormal(side);
    const origin = add(this.ship.position, scale(normal, this.ship.stats.beam * 0.6));
    this.renderer.spawnSmoke(origin, scale(normal, 2.5 * Config.ShipScale), 8, 0.9 * Config.ShipScale);
  }

  updateVisuals(ship: Ship, wind: Wind, dt: number): void {
    this.syncTransform();
    this.updateSails(ship, wind);
    this.updateStatusOverlays(ship);
    this.updateSailQuality(ship, wind);
    this.updateControlButtons(dt);

    // Status bars: anchor directly above the ship (screen-up) and stay
    // world-aligned (north-up) by cancelling the hull heading. Both the offset
    // and the rotation counter the container's heading rotation.
    const h = ship.headingDeg * Deg2Rad;
    this.statusBars.position.set(-this.statusLift * Math.sin(h), -this.statusLift * Math.cos(h));
    this.statusBars.rotation = -h;
    this.updateBar(this.hullBar, ship.hullFraction);
    this.updateBar(this.riggingBar, ship.riggingFraction);

    // Gentle pulse on the command bubble so the commanded ship stands out.
    if (this.commanded) {
      this.commandPulse += dt;
      this.commandGfx.alpha = 0.75 + 0.25 * Math.sin(this.commandPulse * 4);
    }

    // Damage smoke once the hull is hurt.
    const dmg = 1 - ship.hullFraction;
    if (dmg > 0.4 && Math.random() < dmg * dt * 6) {
      this.renderer.spawnSmoke(ship.position, { x: 0, z: 0 }, 1, 0.6 * Config.ShipScale);
    }

    if (this.hitFlash > 0) {
      this.hitFlash -= dt;
      const k = clamp01(this.hitFlash / 0.15);
      if (this.useSprite && this.hullSprite) {
        // Brief red tint on the sprite (tint multiplies, so it reads as "hit").
        this.hullSprite.tint = lerpColor(0xffffff, 0xff4030, k);
      } else {
        this.flashGfx.alpha = k;
      }
    }
  }

  private syncTransform(): void {
    const lpos = this.renderer.worldLocal(this.ship.position);
    this.container.position.set(lpos.x, lpos.y);
    this.container.rotation = this.ship.headingDeg * Deg2Rad;
  }

  private updateSails(ship: Ship, wind: Wind): void {
    // Sprite art has fixed (painted) sails; only the procedural rig animates.
    if (this.useSprite) return;
    const throttle = throttleFactor(ship.sail);
    const speedFrac = ship.stats.topSpeed > 0.01 ? clamp01(ship.speed / ship.stats.topSpeed) : 0;
    const rig = ship.riggingFraction;
    const set = clamp(0.2 + 0.8 * throttle * (0.35 + 0.65 * speedFrac) * rig, 0.14, 1);

    for (const sail of this.sails) {
      sail.canvas.scale.set(lerp(0.5, 1, set), set);
    }

    const blowing = wind.fromDegrees + 180;
    const swing = clamp(signedDelta(ship.headingDeg, blowing) * 0.18, -28, 28);
    this.sailGroup.rotation = swing * Deg2Rad;
  }

  private updateStatusOverlays(ship: Ship): void {
    // Ammo button icon + always-visible sail badge: redraw only on change.
    if (ship.ammo !== this.lastAmmo) {
      this.lastAmmo = ship.ammo;
      this.drawAmmoIcon(ship.ammo);
      this.drawAmmoBadge(ship.ammo);
    }
    if (ship.sail !== this.lastSail) {
      this.lastSail = ship.sail;
      this.drawSailIcon(ship.sail);
    }
  }

  private updateBar(bar: { fill: Graphics; width: number }, fraction: number): void {
    fraction = clamp01(fraction);
    bar.fill.scale.x = fraction;
    bar.fill.position.x = -(bar.width * (1 - fraction)) * 0.5;
  }

  updateSinking(t: number): void {
    this.setCommanded(false, Faction.Neutral);
    this.statusBars.visible = false;
    this.controlsContainer.visible = false;
    this.syncTransform();
    this.container.alpha = lerp(1, 0.15, t);
    this.container.scale.set(lerp(1, 0.3, t));
  }
}

// ---- Local drawing helpers (Unity local XZ → Pixi local, Z → -Y) ----

function lp(x: number, z: number): { x: number; y: number } {
  return { x, y: -z };
}

/** Draws an axis-aligned rect centred at Unity-local (x, z) with X/Z extents. */
function rectAt(g: Graphics, x: number, z: number, xExtent: number, zExtent: number, color: number, alpha = 1): void {
  const p = lp(x, z);
  g.rect(p.x - xExtent / 2, p.y - zExtent / 2, xExtent, zExtent).fill({ color, alpha });
}

function line(g: Graphics, from: Vec2, to: Vec2, width: number): void {
  const a = lp(from.x, from.z);
  const b = lp(to.x, to.z);
  g.moveTo(a.x, a.y).lineTo(b.x, b.y).stroke({ width, color: C_ROPE, alpha: 0.85 });
}

// Sailing-quality colours, indexed by state: 0 = In Irons (red), 1 = Close-
// Hauled (amber), 2 = good point of sail (green).
const SAIL_QUALITY_COLORS = [0xff4d4d, 0xffb020, 0x4dd06a];

/**
 * Maps a wind-model point-of-sail label to a sailing-quality state:
 *   "In Irons"     → 0 (red, stalled in the no-go zone),
 *   "Close-Hauled" → 1 (amber, slow but moving),
 *   everything else (Beam/Broad Reach, Running, or the initial "-") → 2 (green).
 */
function sailQualityState(pointOfSail: string): number {
  if (pointOfSail === "In Irons") return 0;
  if (pointOfSail === "Close-Hauled") return 1;
  return 2;
}

/**
 * Draws a sail-plan glyph into `g` (radius `r`): a mast plus a billowed canvas
 * whose bulge/size shrinks as sail is reduced; Heave To shows a furled mast with
 * a red X. Shared by the always-visible setting badge and the on-ship sail-cycle
 * button so both read identically. Caller clears via this function's `g.clear()`.
 */
function drawSailGlyph(g: Graphics, r: number, setting: SailSetting): void {
  g.clear();

  // Mast (vertical; the glyph is kept north-up by its parent group).
  g.moveTo(0, -r * 0.6).lineTo(0, r * 0.6).stroke({ width: r * 0.12, color: 0xcaa46a });

  if (setting === SailSetting.HeaveTo) {
    // Furled + stopped: a red X over the mast.
    const x = r * 0.42;
    g.moveTo(-x, -x).lineTo(x, x).moveTo(x, -x).lineTo(-x, x).stroke({
      width: r * 0.14,
      color: 0xff5555,
    });
    return;
  }

  // Billowed canvas bulging to the right; size encodes how much sail is set.
  let bulge: number;
  let h: number;
  if (setting === SailSetting.FullSail) {
    bulge = r * 0.72;
    h = r * 0.55;
  } else if (setting === SailSetting.Reefed) {
    bulge = r * 0.45;
    h = r * 0.45;
  } else {
    // Close-Reefed: minimal canvas.
    bulge = r * 0.22;
    h = r * 0.32;
  }
  g.moveTo(0, -h).quadraticCurveTo(bulge, 0, 0, h).fill({ color: C_SAIL });
}

function lerpColor(a: number, b: number, t: number): number {
  const ar = (a >> 16) & 0xff;
  const ag = (a >> 8) & 0xff;
  const ab = a & 0xff;
  const br = (b >> 16) & 0xff;
  const bg = (b >> 8) & 0xff;
  const bb = b & 0xff;
  const r = Math.round(ar + (br - ar) * t);
  const gg = Math.round(ag + (bg - ag) * t);
  const bl = Math.round(ab + (bb - ab) * t);
  return (r << 16) | (gg << 8) | bl;
}
