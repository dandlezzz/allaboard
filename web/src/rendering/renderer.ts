// The PixiJS rendering host: owns the application, the overhead "camera"
// (an orthographic mapping from world XZ to screen), and the world container
// hierarchy. Also manages purely-cosmetic effects (cannon tracers + powder
// smoke) and the selected ship's ordered-heading line — the web analogues of
// Unity's Projectile + CourseIndicator.

import { Application, Container, Graphics, Sprite, Text } from "pixi.js";
import * as Config from "../core/config";
import { headingToVector } from "../core/nav";
import { type Vec2, add, scale, sub, magnitude, normalize } from "../core/vec";
import { smokeTexture } from "./assets";

/** Clamps `v` into the inclusive range [lo, hi] (hi guarded ≥ lo). */
function clampRange(v: number, lo: number, hi: number): number {
  const top = Math.max(lo, hi);
  return v < lo ? lo : v > top ? top : v;
}

interface Tracer {
  pos: Vec2;
  target: Vec2;
  color: number;
  life: number;
}

interface Puff {
  pos: Vec2;
  vel: Vec2;
  age: number;
  life: number;
  size: number;
  /** Textured smoke sprite, when the smoke texture loaded (else null → drawn). */
  sprite: Sprite | null;
}

interface FloatingText {
  /** World position (drifts upward over the popup's life). */
  pos: Vec2;
  age: number;
  life: number;
  sprite: Text;
}

export class Renderer {
  readonly app = new Application();

  /** World-space container (scaled/positioned to act as the overhead camera). */
  world!: Container;
  /** Sea + arena frame (drawn beneath the fleet). */
  seaLayer!: Container;
  /** Per-ship containers live here. */
  shipsLayer!: Container;
  /** Cosmetic effects (tracers, smoke, heading line). */
  fxLayer!: Container;

  /** CSS pixels per world unit. */
  px = 1;
  cx = 0;
  cy = 0;

  private tracerGfx!: Graphics;
  private smokeGfx!: Graphics;
  private smokeLayer!: Container;
  private courseGfx!: Graphics;
  private previewGfx!: Graphics;
  /** Baton-of-Command markers on the sea + faint links to commanded ships. */
  private batonGfx!: Graphics;
  /** Per-side group command panels (sail + ammo buttons) drawn at each baton. */
  private cmdPanelGfx!: Graphics;
  /** Pre-game Setup placement pads (one per side), drawn in world space. */
  private setupGfx!: Graphics;
  /** Screen-space layer for floating combat-text popups (not world-scaled). */
  private textLayer!: Container;
  /** Screen-space layer for Setup pad labels (titles + prompts). */
  private setupTextLayer!: Container;
  private readonly padLabels: Text[] = [];

  private readonly tracers: Tracer[] = [];
  private readonly puffs: Puff[] = [];
  private readonly texts: FloatingText[] = [];

  private onResize: (() => void) | null = null;

  async init(canvas: HTMLCanvasElement): Promise<void> {
    await this.app.init({
      canvas,
      antialias: true,
      background: 0x14283b, // camera clear colour (deep blue), Unity (0.08,0.16,0.26)
      resizeTo: window,
      resolution: Math.max(1, window.devicePixelRatio || 1),
      autoDensity: true,
    });

    this.world = new Container();
    this.seaLayer = new Container();
    this.shipsLayer = new Container();
    this.fxLayer = new Container();
    this.world.addChild(this.seaLayer, this.shipsLayer, this.fxLayer);
    this.app.stage.addChild(this.world);

    // Floating text popups live in SCREEN space (added straight to the stage, not
    // the world container) so their font size stays constant regardless of the
    // world camera scale; they're positioned each frame via worldToScreen.
    this.textLayer = new Container();
    this.setupTextLayer = new Container();
    this.app.stage.addChild(this.textLayer, this.setupTextLayer);

    this.courseGfx = new Graphics();
    this.tracerGfx = new Graphics();
    this.smokeGfx = new Graphics();
    this.smokeLayer = new Container();
    this.previewGfx = new Graphics();
    this.batonGfx = new Graphics();
    this.cmdPanelGfx = new Graphics();
    this.setupGfx = new Graphics();
    this.fxLayer.addChild(
      this.setupGfx,
      this.batonGfx,
      this.cmdPanelGfx,
      this.courseGfx,
      this.tracerGfx,
      this.smokeGfx,
      this.smokeLayer,
      this.previewGfx,
    );

    this.recompute();
    this.onResize = () => this.recompute();
    this.app.renderer.on("resize", this.onResize);
  }

  /** Recomputes the camera mapping so the whole 16:9 field stays framed. */
  recompute(): void {
    const halfH = Config.CameraOrthoSize;
    const halfW = halfH * (16 / 9);
    const w = this.app.screen.width;
    const h = this.app.screen.height;
    this.px = Math.min(h / 2 / halfH, w / 2 / halfW);
    this.cx = w / 2;
    this.cy = h / 2;
    this.world.position.set(this.cx, this.cy);
    this.world.scale.set(this.px);
  }

  /** Maps a world point to the world container's local space (Z → -Y). */
  worldLocal(pos: Vec2): { x: number; y: number } {
    return { x: pos.x, y: -pos.z };
  }

  /** Maps a screen position (CSS px, top-left origin) to a world sea point. */
  screenToWorld(sx: number, sy: number): Vec2 {
    return { x: (sx - this.cx) / this.px, z: -((sy - this.cy) / this.px) };
  }

  /** Maps a world sea point to a screen position (CSS px, top-left origin). */
  worldToScreen(pos: Vec2): { x: number; y: number } {
    return { x: this.cx + pos.x * this.px, y: this.cy - pos.z * this.px };
  }

  // ---- Effects -----------------------------------------------------------

  spawnProjectile(origin: Vec2, target: Vec2, color: number): void {
    const dist = magnitude(sub(target, origin));
    this.tracers.push({
      pos: { x: origin.x, z: origin.z },
      target: { x: target.x, z: target.z },
      color,
      life: Math.max(0.05, dist / Config.ProjectileSpeed),
    });
  }

  spawnSmoke(origin: Vec2, velocity: Vec2, count: number, size: number): void {
    for (let i = 0; i < count; i++) {
      const jitter = { x: (Math.random() - 0.5) * size, z: (Math.random() - 0.5) * size };
      let sprite: Sprite | null = null;
      if (smokeTexture) {
        sprite = new Sprite(smokeTexture);
        sprite.anchor.set(0.5);
        sprite.tint = 0xc8c8c8;
        sprite.rotation = Math.random() * Math.PI * 2;
        this.smokeLayer.addChild(sprite);
      }
      this.puffs.push({
        pos: add(origin, jitter),
        vel: scale(velocity, 0.7 + Math.random() * 0.6),
        age: 0,
        life: 1.4 + Math.random() * 0.6,
        size,
        sprite,
      });
    }
  }

  /** Spawns a bold floating text popup (e.g. "RAKE") at a world position; it
   *  drifts upward and fades out over `FloatingTextLife`, then is destroyed. */
  spawnText(pos: Vec2, text: string, color: number): void {
    const sprite = new Text({
      text,
      style: {
        fontFamily: "Georgia, 'Times New Roman', serif",
        fontSize: Config.FloatingTextFontSize,
        fontWeight: "bold",
        fill: color,
        stroke: { color: 0x1a1208, width: 4 },
        align: "center",
      },
    });
    sprite.anchor.set(0.5);
    const sp = this.worldToScreen(pos);
    sprite.position.set(sp.x, sp.y);
    this.textLayer.addChild(sprite);
    this.texts.push({
      pos: { x: pos.x, z: pos.z },
      age: 0,
      life: Config.FloatingTextLife,
      sprite,
    });
  }

  hideHeadingLine(): void {
    this.courseGfx.visible = false;
  }

  /** Draws an ordered-heading line from each commanded ship along its heading
   *  (each line carries its own accent colour, e.g. per faction in 2-player). */
  showHeadingLines(
    lines: ReadonlyArray<{ from: Vec2; headingDeg: number; length: number; color: number }>,
  ): void {
    const g = this.courseGfx.clear();
    if (lines.length === 0) {
      g.visible = false;
      return;
    }
    const width = 1.1 * Config.ShipScale;
    for (const ln of lines) {
      const a = this.worldLocal(ln.from);
      const b = this.worldLocal(add(ln.from, scale(headingToVector(ln.headingDeg), ln.length)));
      g.moveTo(a.x, a.y).lineTo(b.x, b.y).stroke({ width, color: ln.color, alpha: 0.9, cap: "round" });
    }
    g.visible = true;
  }

  /** Live drag-to-command preview: a line from EACH commanded ship to the drag
   *  point, with a single marker at the release point. */
  showCoursePreview(froms: ReadonlyArray<Vec2>, to: Vec2, color: number): void {
    const g = this.previewGfx.clear();
    if (froms.length === 0) {
      g.visible = false;
      return;
    }
    const b = this.worldLocal(to);
    for (const from of froms) {
      const a = this.worldLocal(from);
      g.moveTo(a.x, a.y)
        .lineTo(b.x, b.y)
        .stroke({ width: 0.8 * Config.ShipScale, color, alpha: 0.85, cap: "round" });
    }
    g.circle(b.x, b.y, 1.3 * Config.ShipScale).fill({ color, alpha: 0.5 });
    g.visible = true;
  }

  hideCoursePreview(): void {
    this.previewGfx.visible = false;
  }

  /**
   * Draws every side's Baton of Command — a glowing gold roundel with a compass
   * cross, surrounded by its translucent sphere-of-influence ring tinted with
   * the side's accent colour, so each squadron is unambiguous even with two
   * batons on the field. (No tether lines to commanded ships.)
   */
  showBatons(
    batons: ReadonlyArray<{
      pos: Vec2;
      color: number;
      /** True while the captain is steering (hand on the Piece / mouse drag) —
       *  drawn brighter, with the full sphere; a resting baton fades the sphere
       *  to a faint reminder so a stale disc doesn't clutter the field. */
      held: boolean;
    }>,
    influenceRadius: number,
  ): void {
    const g = this.batonGfx;
    g.clear();
    if (batons.length === 0) {
      g.visible = false;
      return;
    }
    const R = 2.4 * Config.ShipScale;
    for (const baton of batons) {
      const lp = this.worldLocal(baton.pos);
      const tint = baton.color;
      const held = baton.held;

      // Sphere of influence: prominent while held/steering, faded while resting.
      const fillAlpha = held ? 0.08 : 0.03;
      const ringAlpha = held ? 0.55 : 0.22;
      g.circle(lp.x, lp.y, influenceRadius).fill({ color: tint, alpha: fillAlpha });
      g.circle(lp.x, lp.y, influenceRadius).stroke({
        width: (held ? 0.35 : 0.2) * Config.ShipScale,
        color: tint,
        alpha: ringAlpha,
      });

      // Soft outer glow, bright ring, compass cross, and a solid core. The ring
      // brightens (and gains an accent halo) while held so it reads as "being
      // commanded right now".
      const glow = held ? 0.22 : 0.12;
      g.circle(lp.x, lp.y, R * 1.7).fill({ color: 0xffd24a, alpha: glow });
      if (held) {
        g.circle(lp.x, lp.y, R * 1.35).stroke({
          width: 0.3 * Config.ShipScale,
          color: tint,
          alpha: 0.8,
        });
      }
      g.circle(lp.x, lp.y, R).stroke({
        width: (held ? 0.6 : 0.5) * Config.ShipScale,
        color: 0xffe08a,
        alpha: held ? 1 : 0.9,
      });
      const tick = R * 1.35;
      g.moveTo(lp.x - tick, lp.y).lineTo(lp.x + tick, lp.y);
      g.moveTo(lp.x, lp.y - tick).lineTo(lp.x, lp.y + tick);
      g.stroke({ width: 0.28 * Config.ShipScale, color: 0xffe08a, alpha: 0.85 });
      g.circle(lp.x, lp.y, R * 0.42).fill({ color: 0xfff1c2, alpha: 0.95 });
    }
    g.visible = true;
  }

  hideBatons(): void {
    this.batonGfx.clear();
    this.batonGfx.visible = false;
  }

  /**
   * World positions + radius of the two GROUP command buttons (sail, ammo) for a
   * baton at `batonPos`. The cluster is anchored as a ring a fixed distance from
   * the Piece, then CLAMPED into the arena safe area so a baton near an edge
   * still shows reachable controls (no fixed off-screen offset). Shared by the
   * renderer (to draw them) and the game (to hit-test taps), so the geometry
   * lives in one place.
   */
  commandPanelLayout(batonPos: Vec2): {
    sail: { x: number; z: number; halfW: number; halfH: number };
    ammo: Vec2;
    r: number;
  } {
    const offset = Config.BatonControlClusterRadius;
    const dx = Config.BatonControlButtonGap;
    const r = Config.BatonControlButtonRadius;
    // The sail control is a TALL vertical mast; the ammo control a disc.
    const thHalfW = r * 0.7;
    const thHalfH = r * 2.2;

    // Cluster centre sits BELOW the Piece (mirrored from the old above-anchor, so
    // the controls hang off the opposite side of the baton circle); clamp it so
    // the (taller) thermometer and the ammo disc both stay on-board.
    const safe = 1 - Config.ArenaSafeInset;
    const maxX = Config.ArenaHalfX * safe - (dx + Math.max(r, thHalfW));
    const maxZ = Config.ArenaHalfZ * safe - Math.max(r, thHalfH);
    const cx = clampRange(batonPos.x, -maxX, maxX);
    const cz = clampRange(batonPos.z - offset, -maxZ, maxZ);

    return {
      sail: { x: cx - dx, z: cz, halfW: thHalfW, halfH: thHalfH },
      ammo: { x: cx + dx, z: cz },
      r,
    };
  }

  /**
   * Draws a GROUP command panel by each baton: a vertical sail MAST whose canvas
   * furls/unfurls with the setting (bottom = Heave-To, top = Full Sail) and an
   * ammunition disc (round vs bar shot). Each controls one side's commanded
   * squadron at once.
   */
  showCommandPanels(
    panels: ReadonlyArray<{ pos: Vec2; sail: number; ammo: number }>,
  ): void {
    const g = this.cmdPanelGfx;
    g.clear();
    if (panels.length === 0) {
      g.visible = false;
      return;
    }
    for (const panel of panels) {
      const { sail, ammo, r } = this.commandPanelLayout(panel.pos);
      this.drawSailMast(g, sail, panel.sail);
      this.drawPanelDisc(g, ammo, r, 0x2a2433);
      this.drawAmmoButtonGlyph(g, ammo, r, panel.ammo);
    }
    g.visible = true;
  }

  hideCommandPanels(): void {
    this.cmdPanelGfx.clear();
    this.cmdPanelGfx.visible = false;
  }

  // ---- Pre-game Setup pads ----------------------------------------------

  /**
   * Draws the pre-game placement pads (one per side): a glowing accent-tinted
   * roundel a player drops their command piece onto to take command. A ready pad
   * is filled green with a check; a waiting pad pulses. Each pad carries a
   * screen-space label (side name + prompt) beneath it.
   */
  showSetupPads(
    pads: ReadonlyArray<{
      pos: Vec2;
      radius: number;
      color: number;
      title: string;
      subtitle: string;
      ready: boolean;
    }>,
  ): void {
    const g = this.setupGfx;
    g.clear();
    g.visible = true;

    // Gentle pulse so unfilled pads read as "drop here".
    const pulse = 0.5 + 0.5 * Math.sin(performance.now() / 420);

    let labelIdx = 0;
    for (const pad of pads) {
      const lp = this.worldLocal(pad.pos);
      const r = pad.radius;
      const ringColor = pad.ready ? 0x49d17a : pad.color;

      // Outer glow + soft fill.
      const glow = pad.ready ? 0.18 : 0.1 + 0.14 * pulse;
      g.circle(lp.x, lp.y, r * 1.7).fill({ color: ringColor, alpha: glow });
      g.circle(lp.x, lp.y, r).fill({ color: ringColor, alpha: pad.ready ? 0.22 : 0.12 });

      // Dashed-feel double ring (crisp inner + faint outer).
      g.circle(lp.x, lp.y, r).stroke({
        width: 0.55 * Config.ShipScale,
        color: ringColor,
        alpha: pad.ready ? 0.95 : 0.55 + 0.35 * pulse,
      });
      g.circle(lp.x, lp.y, r * (pad.ready ? 0.62 : 0.55 + 0.06 * pulse)).stroke({
        width: 0.3 * Config.ShipScale,
        color: ringColor,
        alpha: 0.5,
      });

      if (pad.ready) {
        // A bold check mark for a claimed pad.
        const s = r * 0.45;
        g.moveTo(lp.x - s, lp.y + s * 0.1)
          .lineTo(lp.x - s * 0.2, lp.y + s * 0.7)
          .lineTo(lp.x + s, lp.y - s * 0.6)
          .stroke({ width: 0.7 * Config.ShipScale, color: 0xeafff1, alpha: 0.97, cap: "round", join: "round" });
      } else {
        // A central target dot to aim the command piece at.
        g.circle(lp.x, lp.y, r * 0.16).fill({ color: ringColor, alpha: 0.6 + 0.3 * pulse });
      }

      // Labels (screen space) — title bold, prompt beneath.
      const sp = this.worldToScreen(pad.pos);
      const yBelow = sp.y + r * this.px + 10;
      const title = this.padLabel(labelIdx++);
      title.text = pad.title;
      title.style.fill = pad.ready ? 0xbff0cf : pad.color;
      title.position.set(sp.x, yBelow);
      title.visible = true;

      const sub = this.padLabel(labelIdx++);
      sub.text = pad.subtitle;
      sub.style.fill = 0xdfe9f2;
      sub.style.fontSize = 15;
      sub.style.fontWeight = "600";
      sub.position.set(sp.x, yBelow + 22);
      sub.visible = true;
    }

    // Hide any leftover labels from a previous (larger) pad set.
    for (let i = labelIdx; i < this.padLabels.length; i++) this.padLabels[i].visible = false;
  }

  hideSetupPads(): void {
    this.setupGfx.clear();
    this.setupGfx.visible = false;
    for (const label of this.padLabels) label.visible = false;
  }

  /** Lazily grows the pool of reusable pad-label Text objects. */
  private padLabel(index: number): Text {
    let label = this.padLabels[index];
    if (!label) {
      label = new Text({
        text: "",
        style: {
          fontFamily: "Inter, system-ui, sans-serif",
          fontSize: 19,
          fontWeight: "800",
          fill: 0xffffff,
          align: "center",
          stroke: { color: 0x06121c, width: 4 },
        },
      });
      label.anchor.set(0.5, 0);
      this.setupTextLayer.addChild(label);
      this.padLabels[index] = label;
    }
    return label;
  }

  private drawPanelDisc(g: Graphics, pos: Vec2, r: number, fill: number): void {
    const p = this.worldLocal(pos);
    g.circle(p.x, p.y, r).fill({ color: fill, alpha: 0.92 });
    g.circle(p.x, p.y, r).stroke({ width: 0.3 * Config.ShipScale, color: 0xffe08a, alpha: 0.9 });
  }

  /**
   * Draws the sail control as a vertical MAST with a sail that is HOISTED up the
   * mast to show the trim, so it tracks the finger: dragging up hoists more sail
   * (the canvas rises toward the top), dragging down lowers/furls it. The boom
   * sits at the BOTTOM; the sail rises from it to a head whose height is
   * proportional to the setting:
   *   FullSail(3)    → hoisted nearly the whole mast (tall billow, 3 reef bands),
   *   Reefed(2)      → ~2/3 up (2 bands),
   *   CloseReefed(1) → ~1/3 up (1 band),
   *   HeaveTo(0)     → fully furled: a small bundle on the boom, bare mast above.
   * `level` is the SailSetting ordinal 0..3, matching the touch-height mapping in
   * game.ts (top = Full, bottom = Heave-To).
   */
  private drawSailMast(
    g: Graphics,
    rect: { x: number; z: number; halfW: number; halfH: number },
    level: number,
  ): void {
    const cx = rect.x;
    // worldLocal maps +Z (top in world) → smaller local y, so the control top
    // (Full Sail) is the smaller y; the bottom (Heave-To) the larger y.
    const yTop = -(rect.z + rect.halfH);
    const yBot = -(rect.z - rect.halfH);
    const hw = rect.halfW;
    const H = yBot - yTop;

    // Faint backdrop so the control reads against the sea + marks the hit area.
    g.roundRect(cx - hw * 1.25, yTop - hw * 0.5, hw * 2.5, H + hw, hw * 0.7).fill({
      color: 0x12283b,
      alpha: 0.5,
    });
    g.roundRect(cx - hw * 1.25, yTop - hw * 0.5, hw * 2.5, H + hw, hw * 0.7).stroke({
      width: 0.22 * Config.ShipScale,
      color: 0xffe08a,
      alpha: 0.8,
    });

    // Mast (vertical) + boom (a horizontal spar at the bottom).
    const mastW = Math.max(0.12 * Config.ShipScale, hw * 0.22);
    g.rect(cx - mastW / 2, yTop, mastW, H).fill({ color: 0x6b4e2e });
    const boomHalf = hw * 0.98;
    g.rect(cx - boomHalf, yBot - mastW * 0.6, boomHalf * 2, mastW * 1.2).fill({ color: 0x6b4e2e });

    const frac = clampRange(level, 0, 3) / 3;
    const footY = yBot - mastW * 0.8; // just above the boom

    if (frac <= 0.001) {
      // Heave-To: a furled bundle stowed on the boom; bare mast above.
      g.roundRect(cx - hw * 0.85, footY - hw * 0.7, hw * 1.7, hw * 0.7, hw * 0.35).fill({
        color: 0xcfc4ab,
        alpha: 0.95,
      });
      return;
    }

    // Hoisted sail: a billowed canvas rising from the boom, taller with more sail.
    const hoist = (H - mastW) * (0.2 + 0.8 * frac);
    const headY = footY - hoist;
    const bulge = hw * 0.95;
    g.moveTo(cx - hw * 0.62, footY)
      .quadraticCurveTo(cx - bulge, (headY + footY) / 2, cx - hw * 0.85, headY)
      .lineTo(cx + hw * 0.85, headY)
      .quadraticCurveTo(cx + bulge, (headY + footY) / 2, cx + hw * 0.62, footY)
      .closePath()
      .fill({ color: 0xefe7d4, alpha: 0.94 });

    // Reef bands (horizontal seams) — more bands as more sail is hoisted.
    const bands = frac >= 0.99 ? 3 : frac >= 0.6 ? 2 : 1;
    for (let i = 1; i <= bands; i++) {
      const y = footY - (hoist * i) / (bands + 1);
      g.moveTo(cx - hw * 0.66, y)
        .lineTo(cx + hw * 0.66, y)
        .stroke({ width: 0.08 * Config.ShipScale, color: 0xbcae8e, alpha: 0.7 });
    }

    // A gaff/spar at the head so the hoist level reads clearly.
    g.rect(cx - hw * 0.95, headY - mastW * 0.45, hw * 1.9, mastW * 0.9).fill({ color: 0x6b4e2e });
  }

  /** Round shot (one ball) vs bar shot (two balls + bar); 0 = round, 1 = bar. */
  private drawAmmoButtonGlyph(g: Graphics, pos: Vec2, r: number, ammo: number): void {
    const c = this.worldLocal(pos);
    const col = ammo === 1 ? 0x52cc75 : 0xd1d1d9;
    if (ammo === 1) {
      g.rect(c.x - r * 0.5, c.y - r * 0.1, r, r * 0.2).fill({ color: col });
      g.circle(c.x - r * 0.5, c.y, r * 0.24).fill({ color: col });
      g.circle(c.x + r * 0.5, c.y, r * 0.24).fill({ color: col });
    } else {
      g.circle(c.x, c.y, r * 0.45).fill({ color: col });
    }
  }

  /** Advances + redraws tracers and smoke. Call once per frame. */
  updateEffects(dt: number): void {
    // Tracers.
    this.tracerGfx.clear();
    const tracerSize = 0.45 * Config.ShipScale;
    for (let i = this.tracers.length - 1; i >= 0; i--) {
      const t = this.tracers[i];
      const toTarget = sub(t.target, t.pos);
      const d = magnitude(toTarget);
      const step = Config.ProjectileSpeed * dt;
      if (d <= step) {
        t.pos = { x: t.target.x, z: t.target.z };
      } else {
        t.pos = add(t.pos, scale(normalize(toTarget), step));
      }
      t.life -= dt;
      if (t.life <= 0 || d < 0.5) {
        this.tracers.splice(i, 1);
        continue;
      }
      const lp = this.worldLocal(t.pos);
      this.tracerGfx
        .rect(lp.x - tracerSize / 2, lp.y - tracerSize / 2, tracerSize, tracerSize)
        .fill({ color: t.color, alpha: 0.95 });
    }

    // Smoke. Textured puffs (smoke.png) drive sprites; otherwise we draw soft
    // grey circles into smokeGfx as a fallback.
    this.smokeGfx.clear();
    for (let i = this.puffs.length - 1; i >= 0; i--) {
      const p = this.puffs[i];
      p.age += dt;
      if (p.age >= p.life) {
        if (p.sprite) {
          p.sprite.destroy();
          p.sprite = null;
        }
        this.puffs.splice(i, 1);
        continue;
      }
      p.pos = add(p.pos, scale(p.vel, dt));
      const k = p.age / p.life;
      const r = p.size * (0.6 + k * 1.6);
      const alpha = 0.5 * (1 - k);
      const lp = this.worldLocal(p.pos);

      if (p.sprite) {
        const tex = p.sprite.texture;
        p.sprite.position.set(lp.x, lp.y);
        p.sprite.scale.set((r * 2) / Math.max(1, tex.width));
        p.sprite.alpha = alpha;
      } else {
        this.smokeGfx.circle(lp.x, lp.y, r).fill({ color: 0xc8c8c8, alpha });
      }
    }

    // Floating text popups: drift upward (+Z) and fade out, then destroy (no
    // leak). Positioned in screen space each frame via worldToScreen.
    const riseRate = Config.FloatingTextRise / Config.FloatingTextLife;
    for (let i = this.texts.length - 1; i >= 0; i--) {
      const ft = this.texts[i];
      ft.age += dt;
      if (ft.age >= ft.life) {
        ft.sprite.destroy();
        this.texts.splice(i, 1);
        continue;
      }
      ft.pos = { x: ft.pos.x, z: ft.pos.z + riseRate * dt };
      const sp = this.worldToScreen(ft.pos);
      ft.sprite.position.set(sp.x, sp.y);
      ft.sprite.alpha = 1 - ft.age / ft.life;
    }
  }
}
