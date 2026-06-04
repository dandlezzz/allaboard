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
  /** Baton-of-Command marker on the sea + a faint link to its commanded ship. */
  private batonGfx!: Graphics;
  /** Screen-space layer for floating combat-text popups (not world-scaled). */
  private textLayer!: Container;

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
    this.app.stage.addChild(this.textLayer);

    this.courseGfx = new Graphics();
    this.tracerGfx = new Graphics();
    this.smokeGfx = new Graphics();
    this.smokeLayer = new Container();
    this.previewGfx = new Graphics();
    this.batonGfx = new Graphics();
    this.fxLayer.addChild(
      this.batonGfx,
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

  showHeadingLine(from: Vec2, headingDeg: number, length: number, color: number): void {
    const dir = headingToVector(headingDeg);
    const a = this.worldLocal(from);
    const bEnd = add(from, scale(dir, length));
    const b = this.worldLocal(bEnd);
    const width = 1.1 * Config.ShipScale;
    this.courseGfx
      .clear()
      .moveTo(a.x, a.y)
      .lineTo(b.x, b.y)
      .stroke({ width, color, alpha: 0.9, cap: "round" });
    this.courseGfx.visible = true;
  }

  hideHeadingLine(): void {
    this.courseGfx.visible = false;
  }

  /** Live drag-to-command preview: a line + marker from the ship to the drag point. */
  showCoursePreview(from: Vec2, to: Vec2, color: number): void {
    const a = this.worldLocal(from);
    const b = this.worldLocal(to);
    this.previewGfx
      .clear()
      .moveTo(a.x, a.y)
      .lineTo(b.x, b.y)
      .stroke({ width: 0.8 * Config.ShipScale, color, alpha: 0.85, cap: "round" });
    this.previewGfx.circle(b.x, b.y, 1.3 * Config.ShipScale).fill({ color, alpha: 0.5 });
    this.previewGfx.visible = true;
  }

  hideCoursePreview(): void {
    this.previewGfx.visible = false;
  }

  /**
   * Draws the Baton of Command marker at a sea point — a glowing gold roundel
   * with a compass cross — and, when it currently commands a ship, a faint gold
   * link from the baton to that ship so the pairing is obvious.
   */
  showBaton(pos: Vec2, commandedShipPos: Vec2 | null): void {
    const lp = this.worldLocal(pos);
    const R = 2.4 * Config.ShipScale;
    const g = this.batonGfx;
    g.clear();

    if (commandedShipPos) {
      const c = this.worldLocal(commandedShipPos);
      g.moveTo(lp.x, lp.y)
        .lineTo(c.x, c.y)
        .stroke({ width: 0.35 * Config.ShipScale, color: 0xffd24a, alpha: 0.4 });
    }

    // Soft outer glow, bright ring, compass cross, and a solid core.
    g.circle(lp.x, lp.y, R * 1.7).fill({ color: 0xffd24a, alpha: 0.12 });
    g.circle(lp.x, lp.y, R).stroke({ width: 0.5 * Config.ShipScale, color: 0xffe08a, alpha: 0.95 });
    const tick = R * 1.35;
    g.moveTo(lp.x - tick, lp.y).lineTo(lp.x + tick, lp.y);
    g.moveTo(lp.x, lp.y - tick).lineTo(lp.x, lp.y + tick);
    g.stroke({ width: 0.28 * Config.ShipScale, color: 0xffe08a, alpha: 0.85 });
    g.circle(lp.x, lp.y, R * 0.42).fill({ color: 0xfff1c2, alpha: 0.95 });

    g.visible = true;
  }

  hideBaton(): void {
    this.batonGfx.clear();
    this.batonGfx.visible = false;
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
