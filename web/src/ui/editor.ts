// In-app SCENARIO EDITOR — authors/edits the same `Scenario` data the built-in
// battles use, so anything you make plays through the unchanged spawn/combat/AI
// pipeline. It is a plain-DOM overlay (works in the browser preview without any
// Board hardware) layered above the menu.
//
// Layout: a big LIVE PREVIEW on the left (the same world↔SVG chart the menu
// cards use, but interactive — drag fleet anchors and a heading handle, drag
// land vertices) and a scrollable control column on the right (scenario meta,
// wind, per-side fleet + placement, land). Edits mutate a working `draft`
// scenario and re-render the preview live; Save persists it via the custom
// scenario store.
//
// Coordinate convention is shared with `diagram.ts`: world +X → right, +Z → up
// (north up), arena [−W, W] × [−H, H].

import * as Config from "../core/config";
import { fleetSummary, formationPositions, type Scenario, type FleetFormation } from "../core/scenarios";
import {
  upsertCustomScenario,
  deleteCustomScenario,
  isCustomScenario,
  sanitizeScenario,
  newScenarioId,
  MAX_SHIPS_PER_SIDE,
} from "../core/scenarioStore";
import { ShipClass } from "../ships/shipClass";
import { Faction, accentCss } from "../core/faction";
import { headingToVector, vectorToHeading } from "../core/nav";
import { makeWorldMap, formationTicks, DIAGRAM_VB, DIAGRAM_MARGIN, type WorldMap } from "./diagram";

const W = Config.ArenaHalfX;
const H = Config.ArenaHalfZ;
const SVGNS = "http://www.w3.org/2000/svg";

/** World length of the on-preview heading handle arm. */
const HEADING_ARM = W * 0.16;
/** Pointer hit radius (SVG user units) for grabbing a handle/vertex. */
const HIT_RADIUS = 4.5;

type SideKey = "british" | "enemy";
const SIDES: SideKey[] = ["british", "enemy"];

const SHIP_TYPES: ReadonlyArray<{ value: ShipClass; label: string }> = [
  { value: ShipClass.FirstRate, label: "First Rate" },
  { value: ShipClass.ThirdRate, label: "Third Rate" },
  { value: ShipClass.Frigate, label: "Frigate" },
];

export interface EditorCallbacks {
  /** Called after a scenario is saved or deleted, so the menu can refresh. */
  onSaved: (scenarioId: string) => void;
}

/** What the preview pointer is currently dragging. */
type DragTarget =
  | { kind: "anchor"; side: SideKey }
  | { kind: "heading"; side: SideKey }
  | { kind: "vertex"; shape: number; vertex: number };

export class Editor {
  private readonly root: HTMLElement;
  private readonly preview: SVGSVGElement;
  private readonly metaHost: HTMLElement;
  private readonly windHost: HTMLElement;
  private readonly sidesHost: HTMLElement;
  private readonly landHost: HTMLElement;
  private readonly statusEl: HTMLElement;
  private readonly deleteBtn: HTMLButtonElement;
  private readonly map: WorldMap = makeWorldMap(DIAGRAM_VB.w, DIAGRAM_VB.h, DIAGRAM_MARGIN);

  private draft: Scenario = blankDraft();
  private isNew = true;
  /** "fleet" → drag anchors/headings; "land" → drag/add land vertices. */
  private mode: "fleet" | "land" = "fleet";
  private selectedLand: number | null = null;
  private drag: DragTarget | null = null;
  private addVertexCandidate: { x: number; z: number } | null = null;
  private dragMoved = false;

  /** Per-side numeric inputs that drag must keep in sync. */
  private sideInputs: Partial<Record<SideKey, { ax: HTMLInputElement; az: HTMLInputElement; heading: HTMLInputElement }>> =
    {};

  constructor(private readonly callbacks: EditorCallbacks) {
    this.root = document.createElement("div");
    this.root.id = "editor";
    this.root.className = "editor-overlay";
    this.root.hidden = true;
    this.root.innerHTML = `
      <div class="editor-panel">
        <header class="editor-bar">
          <span class="editor-title">Battle Editor</span>
          <div class="editor-bar-actions">
            <input id="editor-import" type="file" accept="application/json" hidden />
            <button type="button" class="chart-link" data-act="import">Import</button>
            <button type="button" class="chart-link" data-act="export">Export</button>
            <button type="button" class="chart-link danger" data-act="delete">Delete</button>
            <button type="button" class="chart-link" data-act="cancel">Cancel</button>
            <button type="button" class="chart-btn small" data-act="save">Save Battle</button>
          </div>
        </header>
        <div class="editor-body">
          <div class="editor-stage">
            <div class="editor-modes">
              <button type="button" class="mode-btn active" data-mode="fleet">Move Fleets</button>
              <button type="button" class="mode-btn" data-mode="land">Edit Land</button>
              <span class="editor-hint" data-hint></span>
            </div>
            <svg class="editor-preview" viewBox="0 0 ${DIAGRAM_VB.w} ${DIAGRAM_VB.h}"
                 preserveAspectRatio="xMidYMid meet" aria-label="Live scenario preview"></svg>
            <div class="editor-status" data-status></div>
          </div>
          <div class="editor-controls">
            <section class="editor-section" data-meta></section>
            <section class="editor-section" data-wind></section>
            <section class="editor-section" data-sides></section>
            <section class="editor-section" data-land></section>
          </div>
        </div>
      </div>`;
    document.body.appendChild(this.root);

    this.preview = this.root.querySelector("svg.editor-preview") as SVGSVGElement;
    this.metaHost = this.q("[data-meta]");
    this.windHost = this.q("[data-wind]");
    this.sidesHost = this.q("[data-sides]");
    this.landHost = this.q("[data-land]");
    this.statusEl = this.q("[data-status]");
    this.deleteBtn = this.root.querySelector('[data-act="delete"]') as HTMLButtonElement;

    this.wireBar();
    this.wirePreviewPointer();
  }

  // ---- Open / close ------------------------------------------------------

  /** Opens the editor on a working copy of `scenario`. */
  open(scenario: Scenario, opts: { isNew: boolean }): void {
    this.draft = JSON.parse(JSON.stringify(scenario)) as Scenario;
    this.isNew = opts.isNew;
    this.mode = "fleet";
    this.selectedLand = null;
    this.deleteBtn.hidden = opts.isNew || !isCustomScenario(scenario.id);
    this.setMode("fleet");
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
      btn.addEventListener("click", () => this.setMode(btn.dataset.mode as "fleet" | "land"));
    }
  }

  private setMode(mode: "fleet" | "land"): void {
    this.mode = mode;
    for (const btn of Array.from(this.root.querySelectorAll<HTMLButtonElement>(".mode-btn"))) {
      btn.classList.toggle("active", btn.dataset.mode === mode);
    }
    const hint = this.q("[data-hint]");
    hint.textContent =
      mode === "fleet"
        ? "Drag each fleet's ● anchor to move it; drag the ○ handle to aim the heading."
        : "Select a land shape, drag its points; click open water to add a point, shift-click a point to remove it.";
    this.refreshPreview();
  }

  private save(): void {
    if (!this.draft.name.trim()) this.draft.name = "Untitled Battle";
    const clean = sanitizeScenario(this.draft);
    if (!clean) return;
    upsertCustomScenario(clean);
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
        // Imported scenarios become a new local custom (fresh id) on save.
        parsed.id = newScenarioId();
        this.draft = parsed;
        this.isNew = true;
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
    const arrow = () => {
      dial.replaceChildren();
      const ring = svg("circle", { cx: 30, cy: 30, r: 26, fill: "#efe2c4", stroke: "#8a7546", "stroke-width": 1.2 });
      dial.appendChild(ring);
      // Wind blows FROM windFromDegrees → the arrow points the way it blows (TO).
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
    this.sideInputs = {};
    for (const side of SIDES) this.sidesHost.appendChild(this.renderSide(side));
  }

  private renderSide(side: SideKey): HTMLElement {
    const spec = this.draft[side];
    const f = spec.formation;
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

    // --- Fleet list ---
    const summary = el("div", "fleet-summary", fleetSummary(f.ships));
    wrap.appendChild(summary);
    const list = el("div", "ship-list");
    const rebuildList = (): void => {
      list.replaceChildren();
      f.ships.forEach((cls, i) => {
        const row = el("div", "ship-row");
        row.appendChild(el("span", "ship-index", i === 0 ? "★" : String(i + 1)));
        const sel = document.createElement("select");
        for (const t of SHIP_TYPES) {
          const opt = document.createElement("option");
          opt.value = String(t.value);
          opt.textContent = t.label;
          if (t.value === cls) opt.selected = true;
          sel.appendChild(opt);
        }
        sel.addEventListener("change", () => {
          f.ships[i] = Number(sel.value) as ShipClass;
          summary.textContent = fleetSummary(f.ships);
          this.update();
        });
        row.appendChild(sel);
        row.appendChild(
          iconBtn("↑", "Move up", i === 0, () => {
            [f.ships[i - 1], f.ships[i]] = [f.ships[i], f.ships[i - 1]];
            rebuildList();
            this.update();
          }),
        );
        row.appendChild(
          iconBtn("↓", "Move down", i === f.ships.length - 1, () => {
            [f.ships[i + 1], f.ships[i]] = [f.ships[i], f.ships[i + 1]];
            rebuildList();
            this.update();
          }),
        );
        row.appendChild(
          iconBtn("✕", "Remove", f.ships.length <= 1, () => {
            f.ships.splice(i, 1);
            if (f.columns && f.columns > f.ships.length) f.columns = Math.max(1, f.ships.length);
            rebuildList();
            summary.textContent = fleetSummary(f.ships);
            this.update();
          }),
        );
        list.appendChild(row);
      });
    };
    rebuildList();
    wrap.appendChild(list);

    const addBtn = el("button", "add-ship") as HTMLButtonElement;
    addBtn.type = "button";
    addBtn.textContent = "＋ Add ship";
    addBtn.addEventListener("click", () => {
      if (f.ships.length >= MAX_SHIPS_PER_SIDE) {
        this.flash(`Max ${MAX_SHIPS_PER_SIDE} ships per side.`);
        return;
      }
      f.ships.push(ShipClass.ThirdRate);
      rebuildList();
      summary.textContent = fleetSummary(f.ships);
      this.update();
    });
    wrap.appendChild(addBtn);

    // --- Placement ---
    const place = el("div", "placement");
    const ax = numInput(Math.round(f.anchor.x), { min: -Math.round(W), max: Math.round(W), step: 5 }, (v) => {
      f.anchor.x = v;
      this.update();
    });
    const az = numInput(Math.round(f.anchor.z), { min: -Math.round(H), max: Math.round(H), step: 5 }, (v) => {
      f.anchor.z = v;
      this.update();
    });
    place.appendChild(labeled("Anchor X", ax));
    place.appendChild(labeled("Anchor Z", az));

    const heading = sliderInput(Math.round(normDeg(f.headingDeg)), { min: 0, max: 359, step: 1 }, (v) => {
      f.headingDeg = v;
      this.update();
    });
    place.appendChild(labeled("Heading °", heading.wrap));

    const cols = numInput(f.columns ?? 1, { min: 1, max: MAX_SHIPS_PER_SIDE, step: 1 }, (v) => {
      if (v <= 1) delete f.columns;
      else f.columns = Math.min(v, f.ships.length);
      this.update();
    });
    place.appendChild(labeled("Columns", cols));

    const gap = sliderInput(Math.round(f.columnGap ?? Config.ColumnGap + 8 * Config.ShipScale), { min: 0, max: 400, step: 5 }, (v) => {
      f.columnGap = v;
      this.update();
    });
    place.appendChild(labeled("Column gap", gap.wrap));

    const arc = sliderInput(Math.round(f.arcDeg ?? 0), { min: -90, max: 90, step: 1 }, (v) => {
      if (v === 0) delete f.arcDeg;
      else f.arcDeg = v;
      this.update();
    });
    place.appendChild(labeled("Curve (arc°)", arc.wrap));

    wrap.appendChild(place);
    this.sideInputs[side] = { ax, az, heading: heading.input };
    return wrap;
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
      // A small default coast patch in the lower-left, ready to drag into shape.
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

  // ---- Preview (interactive) --------------------------------------------

  private refreshPreview(): void {
    const svgRoot = this.preview;
    svgRoot.replaceChildren();

    // Parchment arena.
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
    // Inner arena border (the actual [-W,W]×[-H,H] play area).
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

    // Fleet ticks.
    for (const side of SIDES) {
      const accent = accentCss(side === "british" ? Faction.British : Faction.FrancoSpanish);
      const g = svg("g", { stroke: accent, "stroke-width": 2, "stroke-linecap": "round" });
      for (const t of formationTicks(this.draft[side].formation, this.map)) {
        g.appendChild(svg("line", { x1: t.x1, y1: t.y1, x2: t.x2, y2: t.y2 }));
      }
      svgRoot.appendChild(g);
    }

    // Anchor + heading handles (fleet mode only).
    if (this.mode === "fleet") {
      for (const side of SIDES) {
        const f = this.draft[side].formation;
        const accent = accentCss(side === "british" ? Faction.British : Faction.FrancoSpanish);
        const ax = this.map.toSvgX(f.anchor.x);
        const ay = this.map.toSvgY(f.anchor.z);
        const dir = headingToVector(f.headingDeg);
        const hx = this.map.toSvgX(f.anchor.x + dir.x * HEADING_ARM);
        const hy = this.map.toSvgY(f.anchor.z + dir.z * HEADING_ARM);
        svgRoot.appendChild(svg("line", { x1: ax, y1: ay, x2: hx, y2: hy, stroke: accent, "stroke-width": 0.8 }));
        svgRoot.appendChild(svg("circle", { cx: hx, cy: hy, r: 2, fill: "#fff4d6", stroke: accent, "stroke-width": 1 }));
        svgRoot.appendChild(svg("circle", { cx: ax, cy: ay, r: 3, fill: accent, stroke: "#fff4d6", "stroke-width": 1 }));
      }
    }
  }

  private wirePreviewPointer(): void {
    const svgRoot = this.preview;
    svgRoot.addEventListener("pointerdown", (e) => {
      const p = this.toUser(e);
      this.dragMoved = false;
      this.addVertexCandidate = null;

      if (this.mode === "land" && this.selectedLand !== null) {
        const shape = this.draft.land?.[this.selectedLand];
        if (shape) {
          const vi = this.hitVertex(shape.polygon, p);
          if (vi >= 0) {
            if (e.shiftKey && shape.polygon.length > 3) {
              shape.polygon.splice(vi, 1);
              this.update();
              this.renderLand();
              return;
            }
            this.drag = { kind: "vertex", shape: this.selectedLand, vertex: vi };
          } else {
            // Empty water: remember to add a vertex on click-release.
            this.addVertexCandidate = { x: this.map.toWorldX(p.x), z: this.map.toWorldZ(p.y) };
          }
        }
      } else if (this.mode === "fleet") {
        this.drag = this.hitFleetHandle(p);
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
      if (this.drag.kind === "anchor") {
        const f = this.draft[this.drag.side].formation;
        f.anchor.x = clamp(wx, -W, W);
        f.anchor.z = clamp(wz, -H, H);
        this.syncSide(this.drag.side);
      } else if (this.drag.kind === "heading") {
        const f = this.draft[this.drag.side].formation;
        f.headingDeg = Math.round(normDeg(vectorToHeading({ x: wx - f.anchor.x, z: wz - f.anchor.z })));
        this.syncSide(this.drag.side);
      } else if (this.drag.kind === "vertex") {
        const shape = this.draft.land?.[this.drag.shape];
        if (shape) {
          shape.polygon[this.drag.vertex] = { x: clamp(wx, -W * 1.6, W * 1.6), z: clamp(wz, -H * 1.6, H * 1.6) };
        }
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

  private hitFleetHandle(p: { x: number; y: number }): DragTarget | null {
    // Heading handles take priority (they sit further out than the anchor dot).
    for (const side of SIDES) {
      const f = this.draft[side].formation;
      const dir = headingToVector(f.headingDeg);
      const hx = this.map.toSvgX(f.anchor.x + dir.x * HEADING_ARM);
      const hy = this.map.toSvgY(f.anchor.z + dir.z * HEADING_ARM);
      if (dist2(p.x, p.y, hx, hy) <= HIT_RADIUS * HIT_RADIUS) return { kind: "heading", side };
    }
    for (const side of SIDES) {
      const f = this.draft[side].formation;
      const ax = this.map.toSvgX(f.anchor.x);
      const ay = this.map.toSvgY(f.anchor.z);
      if (dist2(p.x, p.y, ax, ay) <= HIT_RADIUS * HIT_RADIUS) return { kind: "anchor", side };
    }
    return null;
  }

  private hitVertex(poly: ReadonlyArray<{ x: number; z: number }>, p: { x: number; y: number }): number {
    for (let i = 0; i < poly.length; i++) {
      const vx = this.map.toSvgX(poly[i].x);
      const vy = this.map.toSvgY(poly[i].z);
      if (dist2(p.x, p.y, vx, vy) <= HIT_RADIUS * HIT_RADIUS) return i;
    }
    return -1;
  }

  /** Pushes draft values back into a side's numeric inputs during a drag. */
  private syncSide(side: SideKey): void {
    const refs = this.sideInputs[side];
    if (!refs) return;
    const f = this.draft[side].formation;
    refs.ax.value = String(Math.round(f.anchor.x));
    refs.az.value = String(Math.round(f.anchor.z));
    refs.heading.value = String(Math.round(normDeg(f.headingDeg)));
    refs.heading.dispatchEvent(new Event("sync"));
  }

  // ---- Validation --------------------------------------------------------

  private validate(): void {
    const warnings: string[] = [];
    for (const side of SIDES) {
      const f = this.draft[side].formation;
      const label = this.draft[side].label || side;
      if (f.ships.length < 1) warnings.push(`${label}: needs at least one ship.`);
      if (offField(f)) warnings.push(`${label}: some ships start off-field — drag the anchor inboard.`);
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
    british: { label: "Royal Navy", formation: { ships: [ShipClass.ThirdRate], anchor: { x: -W * 0.5, z: 0 }, headingDeg: 90 } },
    enemy: { label: "Enemy Fleet", formation: { ships: [ShipClass.ThirdRate], anchor: { x: W * 0.5, z: 0 }, headingDeg: 270 } },
  };
}

/** True if any spawned ship centre lands outside the arena. */
function offField(f: FleetFormation): boolean {
  for (const p of formationPositions(f)) {
    if (p.pos.x < -W || p.pos.x > W || p.pos.z < -H || p.pos.z > H) return true;
  }
  return false;
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
  return labeled(label, numInput(value, opts, onInput));
}

function numInput(
  value: number,
  opts: { min: number; max: number; step: number },
  onInput: (v: number) => void,
): HTMLInputElement {
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
  return input;
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
