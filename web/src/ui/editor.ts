// In-app SCENARIO EDITOR — authors/edits the same `Scenario` data the game plays,
// so anything you make runs through the unchanged spawn/combat/AI pipeline. It is
// a plain-DOM overlay (works in the browser preview without any Board hardware)
// layered above the menu.
//
// Placement is CHESSBOARD-style: each ship is an individually placed token with
// its own position, facing, and class — there is no formation/rows abstraction.
// Layout: a big LIVE PREVIEW on the left (the same world↔SVG chart the menu cards
// use, but interactive — drag ships to move, drag a handle to rotate, drag land
// vertices) and a scrollable control column on the right (meta, wind, per-side
// ship palettes, land). A selected ship gets an inspector under the preview
// (type / heading / delete). Edits mutate a working `draft` and re-render live;
// Save persists it via the custom scenario store.
//
// Coordinate convention is shared with `diagram.ts`: world +X → right, +Z → up
// (north up), arena [−W, W] × [−H, H].

import * as Config from "../core/config";
import { fleetSummary, type Scenario, type ShipPlacement } from "../core/scenarios";
import {
  upsertCustomScenario,
  deleteCustomScenario,
  isCustomScenario,
  sanitizeScenario,
  newScenarioId,
  MAX_SHIPS_PER_SIDE,
} from "../core/scenarioStore";
import { ShipClass, shipStats } from "../ships/shipClass";
import { Faction, accentCss } from "../core/faction";
import { headingToVector, vectorToHeading } from "../core/nav";
import {
  makeWorldMap,
  shipMarkerPoints,
  MARKER_STROKE,
  DIAGRAM_VB,
  DIAGRAM_MARGIN,
  type WorldMap,
} from "./diagram";

const W = Config.ArenaHalfX;
const H = Config.ArenaHalfZ;
const SVGNS = "http://www.w3.org/2000/svg";

/** World distance the rotate handle sits beyond a ship's bow tip. */
const ROT_OFFSET = W * 0.05;
/** Pointer hit radius (SVG user units) for grabbing a handle/vertex. */
const HIT_RADIUS = 4.5;

type SideKey = "british" | "enemy";
const SIDES: SideKey[] = ["british", "enemy"];

const SHIP_TYPES: ReadonlyArray<{ value: ShipClass; label: string; short: string }> = [
  { value: ShipClass.FirstRate, label: "First Rate", short: "1st" },
  { value: ShipClass.ThirdRate, label: "Third Rate", short: "3rd" },
  { value: ShipClass.Frigate, label: "Frigate", short: "Frig" },
];

export interface EditorCallbacks {
  /** Called after a scenario is saved or deleted, so the menu can refresh. */
  onSaved: (scenarioId: string) => void;
}

/** A reference to one placed ship. */
interface ShipRef {
  side: SideKey;
  index: number;
}

/** What the preview pointer is currently dragging. */
type DragTarget =
  | { kind: "ship"; side: SideKey; index: number }
  | { kind: "rotate"; side: SideKey; index: number }
  | { kind: "vertex"; shape: number; vertex: number };

export class Editor {
  private readonly root: HTMLElement;
  private readonly preview: SVGSVGElement;
  private readonly metaHost: HTMLElement;
  private readonly windHost: HTMLElement;
  private readonly sidesHost: HTMLElement;
  private readonly landHost: HTMLElement;
  private readonly statusEl: HTMLElement;
  private readonly inspectorHost: HTMLElement;
  private readonly deleteBtn: HTMLButtonElement;
  private readonly map: WorldMap = makeWorldMap(DIAGRAM_VB.w, DIAGRAM_VB.h, DIAGRAM_MARGIN);

  private draft: Scenario = blankDraft();
  private isNew = true;
  /** "ships" → place/move/rotate ships; "land" → drag/add/remove land vertices. */
  private mode: "ships" | "land" = "ships";
  private selectedShip: ShipRef | null = null;
  private selectedLand: number | null = null;
  private drag: DragTarget | null = null;
  private addVertexCandidate: { x: number; z: number } | null = null;
  private dragMoved = false;

  /** The selected-ship inspector's heading slider (synced during rotate drag). */
  private inspectorHeading: HTMLInputElement | null = null;

  constructor(private readonly callbacks: EditorCallbacks) {
    this.root = document.createElement("div");
    this.root.id = "editor";
    this.root.className = "editor-overlay";
    this.root.hidden = true;
    // NOTE: the action buttons live in a BOTTOM bar, never the top-right corner —
    // on the Board the OS hardware menu button overlays the top-right and would
    // swallow taps meant for Save/Cancel.
    this.root.innerHTML = `
      <div class="editor-panel">
        <header class="editor-bar">
          <span class="editor-title">Battle Editor</span>
        </header>
        <div class="editor-body">
          <div class="editor-stage">
            <div class="editor-modes">
              <button type="button" class="mode-btn active" data-mode="ships">Place Ships</button>
              <button type="button" class="mode-btn" data-mode="land">Edit Land</button>
              <span class="editor-hint" data-hint></span>
            </div>
            <svg class="editor-preview" viewBox="0 0 ${DIAGRAM_VB.w} ${DIAGRAM_VB.h}"
                 preserveAspectRatio="xMidYMid meet" aria-label="Live scenario preview"></svg>
            <div class="editor-status" data-status></div>
            <div class="editor-inspector" data-inspector hidden></div>
          </div>
          <div class="editor-controls">
            <section class="editor-section" data-meta></section>
            <section class="editor-section" data-wind></section>
            <section class="editor-section" data-sides></section>
            <section class="editor-section" data-land></section>
          </div>
        </div>
        <footer class="editor-foot">
          <input id="editor-import" type="file" accept="application/json" hidden />
          <button type="button" class="chart-link danger" data-act="delete">Delete</button>
          <span class="editor-foot-spacer"></span>
          <button type="button" class="chart-link" data-act="import">Import</button>
          <button type="button" class="chart-link" data-act="export">Export</button>
          <button type="button" class="chart-link" data-act="cancel">Cancel</button>
          <button type="button" class="chart-btn small" data-act="save">Save Battle</button>
        </footer>
      </div>`;
    document.body.appendChild(this.root);

    this.preview = this.root.querySelector("svg.editor-preview") as SVGSVGElement;
    this.metaHost = this.q("[data-meta]");
    this.windHost = this.q("[data-wind]");
    this.sidesHost = this.q("[data-sides]");
    this.landHost = this.q("[data-land]");
    this.statusEl = this.q("[data-status]");
    this.inspectorHost = this.q("[data-inspector]");
    this.deleteBtn = this.root.querySelector('[data-act="delete"]') as HTMLButtonElement;

    this.wireBar();
    this.wirePreviewPointer();
  }

  // ---- Open / close ------------------------------------------------------

  /** Opens the editor on a working copy of `scenario`. */
  open(scenario: Scenario, opts: { isNew: boolean }): void {
    this.draft = JSON.parse(JSON.stringify(scenario)) as Scenario;
    this.isNew = opts.isNew;
    this.selectedShip = null;
    this.selectedLand = null;
    this.deleteBtn.hidden = opts.isNew || !isCustomScenario(scenario.id);
    this.setMode("ships");
    this.renderAll();
    this.root.hidden = false;
    document.body.classList.add("editor-open");
  }

  private close(): void {
    this.root.hidden = true;
    document.body.classList.remove("editor-open");
  }

  // ---- Top bar -----------------------------------------------------------

  private wireBar(): void {
    this.root.querySelector('[data-act="cancel"]')!.addEventListener("click", () => this.close());
    this.root.querySelector('[data-act="save"]')!.addEventListener("click", () => this.save());
    this.root.querySelector('[data-act="export"]')!.addEventListener("click", () => this.exportJson());
    this.deleteBtn.addEventListener("click", () => {
      deleteCustomScenario(this.draft.id);
      this.callbacks.onSaved(this.draft.id);
      this.close();
    });
    const importInput = this.root.querySelector("#editor-import") as HTMLInputElement;
    this.root.querySelector('[data-act="import"]')!.addEventListener("click", () => importInput.click());
    importInput.addEventListener("change", () => this.importJson(importInput));

    for (const btn of Array.from(this.root.querySelectorAll<HTMLButtonElement>(".mode-btn"))) {
      btn.addEventListener("click", () => this.setMode(btn.dataset.mode as "ships" | "land"));
    }
  }

  private setMode(mode: "ships" | "land"): void {
    this.mode = mode;
    for (const btn of Array.from(this.root.querySelectorAll<HTMLButtonElement>(".mode-btn"))) {
      btn.classList.toggle("active", btn.dataset.mode === mode);
    }
    this.q("[data-hint]").textContent =
      mode === "ships"
        ? "Add ships from a side's palette; drag to move, drag the ○ handle to rotate, shift-click to delete."
        : "Select a land shape, drag its points; click open water to add a point, shift-click a point to remove it.";
    this.renderInspector();
    this.refreshPreview();
  }

  private save(): void {
    if (!this.draft.name.trim()) this.draft.name = "Untitled Battle";
    const clean = sanitizeScenario(this.draft);
    if (!clean) {
      this.flash("Couldn't save — the battle data is invalid.");
      return;
    }
    // A side with no ships is allowed (saved as-is) but flagged so the user
    // knows; we never silently no-op on Save. `upsertCustomScenario` reports
    // whether the write actually reached durable storage — if not, keep the
    // editor open and warn rather than closing as if the battle were saved.
    if (!upsertCustomScenario(clean)) {
      this.flash("Couldn't save — storage is unavailable.");
      return;
    }
    this.callbacks.onSaved(clean.id);
    this.close();
  }

  private exportJson(): void {
    const blob = new Blob([JSON.stringify(this.draft, null, 2)], { type: "application/json" });
    const url = URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url;
    a.download = `${this.draft.name.replace(/[^\w-]+/g, "_") || "battle"}.json`;
    a.click();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
  }

  private importJson(input: HTMLInputElement): void {
    const file = input.files?.[0];
    input.value = "";
    if (!file) return;
    file
      .text()
      .then((text) => {
        const parsed = sanitizeScenario(JSON.parse(text));
        if (!parsed) throw new Error("not a scenario");
        parsed.id = newScenarioId(); // imported → a new local custom on save
        this.draft = parsed;
        this.isNew = true;
        this.selectedShip = null;
        this.deleteBtn.hidden = true;
        this.renderAll();
      })
      .catch(() => this.flash("Could not import that file."));
  }

  // ---- Rendering (full) --------------------------------------------------

  private renderAll(): void {
    this.renderMeta();
    this.renderWind();
    this.renderSides();
    this.renderLand();
    this.renderInspector();
    this.update();
  }

  /** Live refresh after a value edit: preview + validation status. */
  private update(): void {
    this.refreshPreview();
    this.validate();
  }

  private renderMeta(): void {
    this.metaHost.replaceChildren();
    this.metaHost.appendChild(sectionTitle("Battle"));
    this.metaHost.appendChild(
      textRow("Name", this.draft.name, (v) => {
        this.draft.name = v;
      }),
    );
    this.metaHost.appendChild(
      numberRow("Year", this.draft.year, { min: 1500, max: 1900, step: 1 }, (v) => {
        this.draft.year = v;
      }),
    );
    this.metaHost.appendChild(
      textAreaRow("Blurb", this.draft.blurb, (v) => {
        this.draft.blurb = v;
      }),
    );
  }

  private renderWind(): void {
    this.windHost.replaceChildren();
    this.windHost.appendChild(sectionTitle("Wind"));
    const wrap = el("div", "wind-control");
    const dial = document.createElementNS(SVGNS, "svg");
    dial.setAttribute("viewBox", "0 0 60 60");
    dial.classList.add("wind-dial");
    const arrow = (): void => {
      dial.replaceChildren();
      dial.appendChild(svg("circle", { cx: 30, cy: 30, r: 26, fill: "#efe2c4", stroke: "#8a7546", "stroke-width": 1.2 }));
      // Wind blows FROM windFromDegrees; mark that bearing on the dial.
      const fromDir = headingToVector(this.draft.windFromDegrees);
      const tx = 30 + fromDir.x * 18;
      const ty = 30 - fromDir.z * 18; // +Z up
      const hx = 30 - fromDir.x * 18;
      const hy = 30 + fromDir.z * 18;
      dial.appendChild(svg("line", { x1: hx, y1: hy, x2: tx, y2: ty, stroke: "#5a4327", "stroke-width": 2 }));
      dial.appendChild(svg("circle", { cx: tx, cy: ty, r: 3, fill: "#5a4327" }));
      dial.appendChild(svg("text", { x: 30, y: 9, "text-anchor": "middle", "font-size": 7, fill: "#6b5536" }, "N"));
    };
    arrow();
    const slider = document.createElement("input");
    slider.type = "range";
    slider.min = "0";
    slider.max = "359";
    slider.step = "1";
    slider.value = String(Math.round(normDeg(this.draft.windFromDegrees)));
    const readout = el("span", "row-readout", `${slider.value}° (from)`);
    slider.addEventListener("input", () => {
      this.draft.windFromDegrees = Number(slider.value);
      readout.textContent = `${slider.value}° (from)`;
      arrow();
    });
    wrap.appendChild(dial);
    const col = el("div", "wind-col");
    col.appendChild(slider);
    col.appendChild(readout);
    wrap.appendChild(col);
    this.windHost.appendChild(wrap);
  }

  private renderSides(): void {
    this.sidesHost.replaceChildren();
    for (const side of SIDES) this.sidesHost.appendChild(this.renderSide(side));
  }

  private renderSide(side: SideKey): HTMLElement {
    const spec = this.draft[side];
    const accent = accentCss(side === "british" ? Faction.British : Faction.FrancoSpanish);
    const wrap = el("div", "side-editor");

    const head = el("div", "side-editor-head");
    head.appendChild(svgSwatch(accent));
    const labelInput = document.createElement("input");
    labelInput.type = "text";
    labelInput.className = "side-label-input";
    labelInput.value = spec.label;
    labelInput.addEventListener("input", () => {
      spec.label = labelInput.value;
    });
    head.appendChild(labelInput);
    wrap.appendChild(head);

    wrap.appendChild(el("div", "fleet-summary", fleetSummary(spec.ships) || "No ships placed"));

    const palette = el("div", "ship-palette");
    palette.appendChild(el("span", "ship-count", `${spec.ships.length}/${MAX_SHIPS_PER_SIDE}`));
    for (const t of SHIP_TYPES) {
      const btn = el("button", "palette-btn") as HTMLButtonElement;
      btn.type = "button";
      btn.textContent = `＋ ${t.short}`;
      btn.title = `Add a ${t.label}`;
      btn.addEventListener("click", () => this.addShip(side, t.value));
      palette.appendChild(btn);
    }
    wrap.appendChild(palette);
    return wrap;
  }

  private renderInspector(): void {
    this.inspectorHost.replaceChildren();
    this.inspectorHeading = null;
    const ref = this.selectedShip;
    if (this.mode !== "ships" || !ref) {
      this.inspectorHost.hidden = true;
      return;
    }
    const ship = this.draft[ref.side].ships[ref.index];
    if (!ship) {
      this.inspectorHost.hidden = true;
      return;
    }
    this.inspectorHost.hidden = false;
    const accent = accentCss(ref.side === "british" ? Faction.British : Faction.FrancoSpanish);

    const head = el("div", "inspector-head");
    head.appendChild(svgSwatch(accent));
    head.appendChild(el("span", "inspector-title", "Selected ship"));
    this.inspectorHost.appendChild(head);

    const fields = el("div", "inspector-fields");

    const type = document.createElement("select");
    for (const t of SHIP_TYPES) {
      const opt = document.createElement("option");
      opt.value = String(t.value);
      opt.textContent = t.label;
      if (t.value === ship.shipClass) opt.selected = true;
      type.appendChild(opt);
    }
    type.addEventListener("change", () => {
      ship.shipClass = Number(type.value) as ShipClass;
      this.renderSides();
      this.update();
    });
    fields.appendChild(labeled("Type", type));

    const heading = sliderInput(Math.round(normDeg(ship.headingDeg)), { min: 0, max: 359, step: 1 }, (v) => {
      ship.headingDeg = v;
      this.refreshPreview();
    });
    this.inspectorHeading = heading.input;
    fields.appendChild(labeled("Heading °", heading.wrap));

    const del = el("button", "del-ship") as HTMLButtonElement;
    del.type = "button";
    del.textContent = "✕ Delete ship";
    del.addEventListener("click", () => this.deleteSelectedShip());
    fields.appendChild(del);

    this.inspectorHost.appendChild(fields);
  }

  private renderLand(): void {
    this.landHost.replaceChildren();
    const title = el("div", "section-head");
    title.appendChild(el("span", "section-title-text", "Land"));
    const addShape = el("button", "add-ship") as HTMLButtonElement;
    addShape.type = "button";
    addShape.textContent = "＋ Add land";
    addShape.addEventListener("click", () => {
      if (!this.draft.land) this.draft.land = [];
      this.draft.land.push({
        polygon: [
          { x: -W * 0.5, z: -H * 0.7 },
          { x: -W * 0.2, z: -H * 0.7 },
          { x: -W * 0.2, z: -H * 0.95 },
          { x: -W * 0.5, z: -H * 0.95 },
        ],
      });
      this.selectedLand = this.draft.land.length - 1;
      this.setMode("land");
      this.renderLand();
      this.update();
    });
    title.appendChild(addShape);
    this.landHost.appendChild(title);

    const shapes = this.draft.land ?? [];
    if (shapes.length === 0) {
      this.landHost.appendChild(el("p", "muted-note", "No land. Add a coast or island; it is purely cosmetic."));
      return;
    }
    shapes.forEach((shape, i) => {
      const row = el("div", "land-row" + (i === this.selectedLand ? " selected" : ""));
      const selectBtn = el("button", "land-select") as HTMLButtonElement;
      selectBtn.type = "button";
      selectBtn.textContent = `Shape ${i + 1} · ${shape.polygon.length} pts`;
      selectBtn.addEventListener("click", () => {
        this.selectedLand = i;
        this.setMode("land");
        this.renderLand();
      });
      row.appendChild(selectBtn);

      const color = document.createElement("input");
      color.type = "color";
      color.value = "#" + (shape.fill ?? 0xcdba8a).toString(16).padStart(6, "0");
      color.title = "Fill colour";
      color.addEventListener("input", () => {
        shape.fill = parseInt(color.value.slice(1), 16);
        this.update();
      });
      row.appendChild(color);

      row.appendChild(
        iconBtn("✕", "Remove shape", false, () => {
          shapes.splice(i, 1);
          if (shapes.length === 0) delete this.draft.land;
          if (this.selectedLand === i) this.selectedLand = null;
          this.renderLand();
          this.update();
        }),
      );
      this.landHost.appendChild(row);
    });
  }

  // ---- Ship operations ---------------------------------------------------

  private addShip(side: SideKey, shipClass: ShipClass): void {
    const spec = this.draft[side];
    if (spec.ships.length >= MAX_SHIPS_PER_SIDE) {
      this.flash(`Max ${MAX_SHIPS_PER_SIDE} ships per side.`);
      return;
    }
    // Spread successive adds across a small grid near that side's half so they
    // don't stack (the user then drags them into place).
    const n = spec.ships.length;
    const baseX = side === "british" ? -W * 0.5 : W * 0.5;
    const col = n % 4;
    const row = Math.floor(n / 4) % 3;
    const pos = {
      x: clamp(baseX + (col - 1.5) * (W * 0.08), -W, W),
      z: clamp((row - 1) * (H * 0.28), -H, H),
    };
    spec.ships.push({ pos, headingDeg: side === "british" ? 90 : 270, shipClass });
    this.selectedShip = { side, index: spec.ships.length - 1 };
    this.setMode("ships");
    this.renderSides();
    this.renderInspector();
    this.update();
  }

  private deleteSelectedShip(): void {
    const ref = this.selectedShip;
    if (!ref) return;
    this.draft[ref.side].ships.splice(ref.index, 1);
    this.selectedShip = null;
    this.renderSides();
    this.renderInspector();
    this.update();
  }

  // ---- Preview (interactive) --------------------------------------------

  private refreshPreview(): void {
    const svgRoot = this.preview;
    svgRoot.replaceChildren();

    // Parchment arena + inner play-area border.
    svgRoot.appendChild(
      svg("rect", {
        x: 1.5,
        y: 1.5,
        width: DIAGRAM_VB.w - 3,
        height: DIAGRAM_VB.h - 3,
        rx: 2,
        fill: "#e7d4ac",
        stroke: "#8a7546",
        "stroke-width": 0.8,
      }),
    );
    svgRoot.appendChild(
      svg("rect", {
        x: this.map.toSvgX(-W),
        y: this.map.toSvgY(H),
        width: this.map.toSvgX(W) - this.map.toSvgX(-W),
        height: this.map.toSvgY(-H) - this.map.toSvgY(H),
        fill: "none",
        stroke: "#b9a468",
        "stroke-width": 0.5,
        "stroke-dasharray": "2 2",
      }),
    );

    // Land.
    (this.draft.land ?? []).forEach((shape, i) => {
      const pts = shape.polygon.map((p) => `${this.map.toSvgX(p.x).toFixed(2)},${this.map.toSvgY(p.z).toFixed(2)}`).join(" ");
      const fill = "#" + (shape.fill ?? 0xcdba8a).toString(16).padStart(6, "0");
      const selected = this.mode === "land" && i === this.selectedLand;
      svgRoot.appendChild(
        svg("polygon", {
          points: pts,
          fill,
          "fill-opacity": 0.7,
          stroke: selected ? "#3a2c1a" : "#8a7546",
          "stroke-width": selected ? 0.8 : 0.4,
        }),
      );
      if (selected) {
        shape.polygon.forEach((p) => {
          svgRoot.appendChild(
            svg("circle", {
              cx: this.map.toSvgX(p.x),
              cy: this.map.toSvgY(p.z),
              r: 1.7,
              fill: "#fff4d6",
              stroke: "#3a2c1a",
              "stroke-width": 0.5,
            }),
          );
        });
      }
    });

    // Ships — one oriented hull silhouette each, with the selected one
    // highlighted by a dashed ring and given a rotate handle off the bow.
    for (const side of SIDES) {
      const accent = accentCss(side === "british" ? Faction.British : Faction.FrancoSpanish);
      this.draft[side].ships.forEach((ship, index) => {
        const g = this.shipGeom(ship);
        const selected =
          this.mode === "ships" &&
          this.selectedShip?.side === side &&
          this.selectedShip?.index === index;
        if (selected) {
          const r = Math.hypot(g.cx - g.bx, g.cy - g.by) * 1.35 + 2;
          svgRoot.appendChild(
            svg("circle", { cx: g.cx, cy: g.cy, r, fill: "none", stroke: "#3a2c1a", "stroke-width": 0.6, "stroke-dasharray": "1.5 1.5" }),
          );
          svgRoot.appendChild(svg("line", { x1: g.bx, y1: g.by, x2: g.rx, y2: g.ry, stroke: "#3a2c1a", "stroke-width": 0.6 }));
          svgRoot.appendChild(svg("circle", { cx: g.rx, cy: g.ry, r: 1.8, fill: "#fff4d6", stroke: "#3a2c1a", "stroke-width": 0.6 }));
        }
        svgRoot.appendChild(
          svg("polygon", {
            points: shipMarkerPoints(ship, this.map),
            fill: accent,
            stroke: MARKER_STROKE,
            "stroke-width": selected ? 0.7 : 0.4,
            "stroke-linejoin": "round",
          }),
        );
      });
    }
  }

  /** SVG-space geometry for a placed ship (centre, bow, stern, rotate handle). */
  private shipGeom(p: ShipPlacement): {
    cx: number; cy: number; bx: number; by: number; sx: number; sy: number; rx: number; ry: number;
  } {
    const half = shipStats(p.shipClass).length * 0.5;
    const dir = headingToVector(p.headingDeg);
    return {
      cx: this.map.toSvgX(p.pos.x),
      cy: this.map.toSvgY(p.pos.z),
      bx: this.map.toSvgX(p.pos.x + dir.x * half),
      by: this.map.toSvgY(p.pos.z + dir.z * half),
      sx: this.map.toSvgX(p.pos.x - dir.x * half),
      sy: this.map.toSvgY(p.pos.z - dir.z * half),
      rx: this.map.toSvgX(p.pos.x + dir.x * (half + ROT_OFFSET)),
      ry: this.map.toSvgY(p.pos.z + dir.z * (half + ROT_OFFSET)),
    };
  }

  private wirePreviewPointer(): void {
    const svgRoot = this.preview;
    svgRoot.addEventListener("pointerdown", (e) => {
      const p = this.toUser(e);
      this.dragMoved = false;
      this.addVertexCandidate = null;

      if (this.mode === "ships") {
        // A selected ship's rotate handle wins (it sits beyond the bow).
        if (this.selectedShip) {
          const s = this.draft[this.selectedShip.side].ships[this.selectedShip.index];
          if (s) {
            const g = this.shipGeom(s);
            if (dist2(p.x, p.y, g.rx, g.ry) <= HIT_RADIUS * HIT_RADIUS) {
              this.drag = { kind: "rotate", side: this.selectedShip.side, index: this.selectedShip.index };
            }
          }
        }
        if (!this.drag) {
          const hit = this.hitShip(p);
          if (hit) {
            if (e.shiftKey) {
              this.draft[hit.side].ships.splice(hit.index, 1);
              this.selectedShip = null;
              this.renderSides();
              this.renderInspector();
              this.update();
            } else {
              this.selectedShip = hit;
              this.drag = { kind: "ship", side: hit.side, index: hit.index };
              this.renderInspector();
              this.refreshPreview();
            }
          } else if (this.selectedShip) {
            this.selectedShip = null;
            this.renderInspector();
            this.refreshPreview();
          }
        }
      } else if (this.mode === "land" && this.selectedLand !== null) {
        const shape = this.draft.land?.[this.selectedLand];
        if (shape) {
          const vi = this.hitVertex(shape.polygon, p);
          if (vi >= 0) {
            if (e.shiftKey && shape.polygon.length > 3) {
              shape.polygon.splice(vi, 1);
              this.renderLand();
              this.update();
              return;
            }
            this.drag = { kind: "vertex", shape: this.selectedLand, vertex: vi };
          } else {
            this.addVertexCandidate = { x: this.map.toWorldX(p.x), z: this.map.toWorldZ(p.y) };
          }
        }
      }

      if (this.drag || this.addVertexCandidate) {
        svgRoot.setPointerCapture(e.pointerId);
        e.preventDefault();
      }
    });

    svgRoot.addEventListener("pointermove", (e) => {
      if (!this.drag) return;
      this.dragMoved = true;
      const p = this.toUser(e);
      const wx = this.map.toWorldX(p.x);
      const wz = this.map.toWorldZ(p.y);
      if (this.drag.kind === "ship") {
        const s = this.draft[this.drag.side].ships[this.drag.index];
        if (s) s.pos = { x: clamp(wx, -W, W), z: clamp(wz, -H, H) };
      } else if (this.drag.kind === "rotate") {
        const s = this.draft[this.drag.side].ships[this.drag.index];
        if (s) {
          s.headingDeg = Math.round(normDeg(vectorToHeading({ x: wx - s.pos.x, z: wz - s.pos.z })));
          if (this.inspectorHeading) {
            this.inspectorHeading.value = String(s.headingDeg);
            this.inspectorHeading.dispatchEvent(new Event("sync"));
          }
        }
      } else if (this.drag.kind === "vertex") {
        const shape = this.draft.land?.[this.drag.shape];
        if (shape) shape.polygon[this.drag.vertex] = { x: clamp(wx, -W * 1.6, W * 1.6), z: clamp(wz, -H * 1.6, H * 1.6) };
      }
      this.refreshPreview();
      this.validate();
    });

    const end = (e: PointerEvent): void => {
      if (this.addVertexCandidate && !this.dragMoved && this.selectedLand !== null) {
        const shape = this.draft.land?.[this.selectedLand];
        if (shape) {
          shape.polygon.push(this.addVertexCandidate);
          this.renderLand();
          this.update();
        }
      }
      this.addVertexCandidate = null;
      if (this.drag) {
        this.drag = null;
        try {
          svgRoot.releasePointerCapture(e.pointerId);
        } catch {
          /* capture may already be gone */
        }
      }
    };
    svgRoot.addEventListener("pointerup", end);
    svgRoot.addEventListener("pointercancel", end);
  }

  /** Converts a pointer event to SVG user-unit coordinates. */
  private toUser(e: PointerEvent): { x: number; y: number } {
    const rect = this.preview.getBoundingClientRect();
    return {
      x: ((e.clientX - rect.left) / rect.width) * DIAGRAM_VB.w,
      y: ((e.clientY - rect.top) / rect.height) * DIAGRAM_VB.h,
    };
  }

  /** Nearest ship (either side) whose hull tick is within grab range, or null. */
  private hitShip(p: { x: number; y: number }): ShipRef | null {
    let best: ShipRef | null = null;
    let bestD = Infinity;
    for (const side of SIDES) {
      this.draft[side].ships.forEach((ship, index) => {
        const g = this.shipGeom(ship);
        // Match the drawn marker (scaled ~1.25× the true half-length) plus slack.
        const reach = Math.max(HIT_RADIUS, Math.hypot(g.cx - g.bx, g.cy - g.by) * 1.25 + 1.5);
        const d = dist2(p.x, p.y, g.cx, g.cy);
        if (d <= reach * reach && d < bestD) {
          bestD = d;
          best = { side, index };
        }
      });
    }
    return best;
  }

  private hitVertex(poly: ReadonlyArray<{ x: number; z: number }>, p: { x: number; y: number }): number {
    for (let i = 0; i < poly.length; i++) {
      const vx = this.map.toSvgX(poly[i].x);
      const vy = this.map.toSvgY(poly[i].z);
      if (dist2(p.x, p.y, vx, vy) <= HIT_RADIUS * HIT_RADIUS) return i;
    }
    return -1;
  }

  // ---- Validation --------------------------------------------------------

  private validate(): void {
    const warnings: string[] = [];
    for (const side of SIDES) {
      const spec = this.draft[side];
      if (spec.ships.length < 1) warnings.push(`${spec.label || side}: no ships placed.`);
    }
    this.statusEl.classList.toggle("has-warning", warnings.length > 0);
    this.statusEl.textContent = warnings.length ? `⚠ ${warnings.join("  ")}` : "Looks shipshape.";
  }

  private flash(msg: string): void {
    this.statusEl.classList.add("has-warning");
    this.statusEl.textContent = `⚠ ${msg}`;
  }

  private q(sel: string): HTMLElement {
    return this.root.querySelector(sel) as HTMLElement;
  }
}

// ---------------------------------------------------------------------------
// Small DOM/SVG/maths helpers
// ---------------------------------------------------------------------------

function blankDraft(): Scenario {
  return {
    id: "",
    name: "",
    year: 1805,
    blurb: "",
    windFromDegrees: 0,
    british: { label: "Royal Navy", ships: [] },
    enemy: { label: "Enemy Fleet", ships: [] },
  };
}

function normDeg(d: number): number {
  return ((d % 360) + 360) % 360;
}
function clamp(v: number, lo: number, hi: number): number {
  return v < lo ? lo : v > hi ? hi : v;
}
function dist2(ax: number, ay: number, bx: number, by: number): number {
  const dx = ax - bx;
  const dy = ay - by;
  return dx * dx + dy * dy;
}

function el(tag: string, className?: string, text?: string): HTMLElement {
  const e = document.createElement(tag);
  if (className) e.className = className;
  if (text !== undefined) e.textContent = text;
  return e;
}

function svg(tag: string, attrs: Record<string, string | number>, text?: string): SVGElement {
  const e = document.createElementNS(SVGNS, tag);
  for (const [k, v] of Object.entries(attrs)) e.setAttribute(k, String(v));
  if (text !== undefined) e.textContent = text;
  return e;
}

function sectionTitle(text: string): HTMLElement {
  return el("div", "section-title-text", text);
}

function svgSwatch(color: string): HTMLElement {
  const i = el("i", "swatch");
  i.style.background = color;
  return i;
}

function textRow(label: string, value: string, onInput: (v: string) => void): HTMLElement {
  const input = document.createElement("input");
  input.type = "text";
  input.value = value;
  input.addEventListener("input", () => onInput(input.value));
  return labeled(label, input);
}

function textAreaRow(label: string, value: string, onInput: (v: string) => void): HTMLElement {
  const input = document.createElement("textarea");
  input.rows = 2;
  input.value = value;
  input.addEventListener("input", () => onInput(input.value));
  return labeled(label, input);
}

function numberRow(
  label: string,
  value: number,
  opts: { min: number; max: number; step: number },
  onInput: (v: number) => void,
): HTMLElement {
  const input = document.createElement("input");
  input.type = "number";
  input.min = String(opts.min);
  input.max = String(opts.max);
  input.step = String(opts.step);
  input.value = String(value);
  input.addEventListener("input", () => {
    if (input.value === "" || input.value === "-") return;
    onInput(clamp(Number(input.value), opts.min, opts.max));
  });
  return labeled(label, input);
}

/** A range slider plus a live readout; returns the wrapper and the input. */
function sliderInput(
  value: number,
  opts: { min: number; max: number; step: number },
  onInput: (v: number) => void,
): { wrap: HTMLElement; input: HTMLInputElement } {
  const wrap = el("div", "slider-wrap");
  const input = document.createElement("input");
  input.type = "range";
  input.min = String(opts.min);
  input.max = String(opts.max);
  input.step = String(opts.step);
  input.value = String(value);
  const readout = el("span", "row-readout", String(value));
  const sync = (): void => {
    readout.textContent = input.value;
  };
  input.addEventListener("input", () => {
    sync();
    onInput(Number(input.value));
  });
  // Custom "sync" event lets a drag update the slider without re-firing onInput.
  input.addEventListener("sync", sync);
  wrap.appendChild(input);
  wrap.appendChild(readout);
  return { wrap, input };
}

function labeled(label: string, control: HTMLElement): HTMLElement {
  const row = el("label", "field-row");
  row.appendChild(el("span", "field-label", label));
  row.appendChild(control);
  return row;
}

function iconBtn(glyph: string, title: string, disabled: boolean, onClick: () => void): HTMLButtonElement {
  const b = document.createElement("button");
  b.type = "button";
  b.className = "icon-btn";
  b.textContent = glyph;
  b.title = title;
  b.disabled = disabled;
  b.addEventListener("click", onClick);
  return b;
}
