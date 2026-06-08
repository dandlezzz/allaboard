// Custom-scenario registry + persistence. The five historical battles in
// `scenarios.ts` are read-only built-ins; this module layers USER-authored
// scenarios (from the in-app editor) on top, persisted to `localStorage` so they
// survive reloads and appear in the menu gallery beside the built-ins.
//
// A `Scenario` is already plain, JSON-safe data (strings, numbers, `Vec2`s, and
// the numeric `ShipClass` enum), so it serialises directly — no bespoke codec.
// Everything funnels through here so the menu and the game share ONE view of
// "all scenarios" and one notion of which ids are custom (and therefore
// editable/deletable).
//
// Board.save note: on hardware we could ALSO mirror these into `Board.save`
// (gated behind `Board.isOnDevice`, since its sync getters throw and async calls
// reject without a device). That is deferred — persistence is localStorage-only
// for now, which the Board WebView honours like any browser. See the editor's
// JSON export/import for moving scenarios between installs in the meantime.

import * as Config from "./config";
import { ShipClass } from "../ships/shipClass";
import { SCENARIOS, type Scenario, type ShipPlacement, type LandShape } from "./scenarios";

const STORAGE_KEY = "trafalgar.customScenarios.v3";
/** Superseded keys, purged on first load so stale customs never reappear. v2 and
 *  earlier used the old formation-based shape, incompatible with explicit ships. */
const LEGACY_KEYS = ["trafalgar.customScenarios.v1", "trafalgar.customScenarios.v2"];
const W = Config.ArenaHalfX;
const H = Config.ArenaHalfZ;

/** In-memory cache of the parsed custom list (lazy-loaded from storage). */
let cache: Scenario[] | null = null;

// ---------------------------------------------------------------------------
// Read / write
// ---------------------------------------------------------------------------

function readStorage(): Scenario[] {
  try {
    // Drop any superseded versions so previously-saved customs don't linger.
    for (const k of LEGACY_KEYS) localStorage.removeItem(k);
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) return [];
    const parsed: unknown = JSON.parse(raw);
    if (!Array.isArray(parsed)) return [];
    return parsed.map((s) => sanitizeScenario(s)).filter((s): s is Scenario => s !== null);
  } catch {
    return []; // corrupt/unavailable storage → behave as if there are no customs
  }
}

function writeStorage(list: Scenario[]): void {
  try {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(list));
  } catch {
    // Quota or privacy-mode failure: keep the in-memory cache so the session
    // still works; persistence just won't survive the reload.
  }
}

// ---------------------------------------------------------------------------
// Public queries
// ---------------------------------------------------------------------------

/** All user-authored scenarios (cached). */
export function customScenarios(): Scenario[] {
  if (!cache) cache = readStorage();
  return cache;
}

/** Built-ins first, then customs — the gallery's full, ordered list. */
export function listScenarios(): Scenario[] {
  return [...SCENARIOS, ...customScenarios()];
}

/** True if `id` belongs to a user scenario (editable/deletable). */
export function isCustomScenario(id: string): boolean {
  return customScenarios().some((s) => s.id === id);
}

/** Resolves any id (built-in or custom); `undefined` if no scenario matches. */
export function resolveScenario(id: string): Scenario | undefined {
  return listScenarios().find((s) => s.id === id);
}

// ---------------------------------------------------------------------------
// Mutations
// ---------------------------------------------------------------------------

/** Inserts or replaces a custom scenario by id, persisting the new list. */
export function upsertCustomScenario(scenario: Scenario): void {
  const list = customScenarios().slice();
  const i = list.findIndex((s) => s.id === scenario.id);
  if (i >= 0) list[i] = scenario;
  else list.push(scenario);
  cache = list;
  writeStorage(list);
}

/** Removes a custom scenario by id (no-op for built-ins). */
export function deleteCustomScenario(id: string): void {
  cache = customScenarios().filter((s) => s.id !== id);
  writeStorage(cache);
}

/** A collision-resistant id for a freshly-created custom scenario. */
export function newScenarioId(): string {
  return `custom-${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 6)}`;
}

// ---------------------------------------------------------------------------
// Sanitisation — defends against hand-edited/imported JSON
// ---------------------------------------------------------------------------

const num = (v: unknown, fallback: number): number =>
  typeof v === "number" && Number.isFinite(v) ? v : fallback;
const str = (v: unknown, fallback: string): string => (typeof v === "string" ? v : fallback);

const clamp = (v: number, lo: number, hi: number): number => (v < lo ? lo : v > hi ? hi : v);

function sanitizeShipClass(v: unknown): ShipClass {
  const n = Number(v);
  return n === ShipClass.FirstRate || n === ShipClass.ThirdRate || n === ShipClass.Frigate
    ? (n as ShipClass)
    : ShipClass.ThirdRate;
}

/** Coerces an arbitrary array into a clamped, capped list of ship placements.
 *  Old (formation-shaped) data has no `ships` array of objects → yields []. */
function sanitizeShips(v: unknown): ShipPlacement[] {
  if (!Array.isArray(v)) return [];
  const ships: ShipPlacement[] = [];
  for (const raw of v.slice(0, MAX_SHIPS_PER_SIDE)) {
    if (!raw || typeof raw !== "object") continue;
    const s = raw as Record<string, unknown>;
    const pos = (s.pos ?? {}) as Record<string, unknown>;
    ships.push({
      pos: { x: clamp(num(pos.x, 0), -W, W), z: clamp(num(pos.z, 0), -H, H) },
      headingDeg: num(s.headingDeg, 90),
      shipClass: sanitizeShipClass(s.shipClass),
    });
  }
  return ships;
}

function sanitizeLand(v: unknown): LandShape[] | undefined {
  if (!Array.isArray(v)) return undefined;
  const shapes: LandShape[] = [];
  for (const raw of v) {
    const s = (raw ?? {}) as Record<string, unknown>;
    if (!Array.isArray(s.polygon)) continue;
    const polygon = s.polygon
      .map((p) => {
        const pt = (p ?? {}) as Record<string, unknown>;
        return { x: num(pt.x, 0), z: num(pt.z, 0) };
      })
      .filter((_, i) => i < 64);
    if (polygon.length < 3) continue;
    const shape: LandShape = { polygon };
    if (typeof s.fill === "number") shape.fill = s.fill;
    shapes.push(shape);
  }
  return shapes.length > 0 ? shapes : undefined;
}

/** Coerces arbitrary parsed JSON into a valid Scenario, or null if hopeless. */
export function sanitizeScenario(v: unknown): Scenario | null {
  if (!v || typeof v !== "object") return null;
  const o = v as Record<string, unknown>;
  const id = str(o.id, "");
  if (!id) return null;
  const british = (o.british ?? {}) as Record<string, unknown>;
  const enemy = (o.enemy ?? {}) as Record<string, unknown>;
  const scenario: Scenario = {
    id,
    name: str(o.name, "Untitled Battle"),
    year: Math.round(num(o.year, 1805)),
    blurb: str(o.blurb, ""),
    windFromDegrees: num(o.windFromDegrees, 0),
    british: {
      label: str(british.label, "Royal Navy"),
      ships: sanitizeShips(british.ships),
    },
    enemy: {
      label: str(enemy.label, "Enemy Fleet"),
      ships: sanitizeShips(enemy.ships),
    },
  };
  const land = sanitizeLand(o.land);
  if (land) scenario.land = land;
  return scenario;
}

// ---------------------------------------------------------------------------
// Authoring helpers shared with the editor
// ---------------------------------------------------------------------------

/** Hard cap on ships per side (mirrors the scaling rule in scenarios.ts). */
export const MAX_SHIPS_PER_SIDE = 12;

/** A blank starting scenario for "Create Battle": a few seed ships per side,
 *  ready to be dragged around the arena (or deleted) in the editor. */
export function blankScenario(): Scenario {
  const british = (z: number, cls: ShipClass): ShipPlacement => ({
    pos: { x: -W * 0.5, z },
    headingDeg: 90,
    shipClass: cls,
  });
  const enemy = (z: number, cls: ShipClass): ShipPlacement => ({
    pos: { x: W * 0.5, z },
    headingDeg: 270,
    shipClass: cls,
  });
  return {
    id: newScenarioId(),
    name: "New Battle",
    year: 1805,
    blurb: "",
    windFromDegrees: 0,
    british: {
      label: "Royal Navy",
      ships: [british(-H * 0.3, ShipClass.ThirdRate), british(0, ShipClass.FirstRate), british(H * 0.3, ShipClass.ThirdRate)],
    },
    enemy: {
      label: "Enemy Fleet",
      ships: [enemy(-H * 0.3, ShipClass.ThirdRate), enemy(0, ShipClass.ThirdRate), enemy(H * 0.3, ShipClass.ThirdRate)],
    },
  };
}

/** Deep-clones a scenario as a NEW custom scenario (for duplicate / edit-copy). */
export function duplicateScenario(source: Scenario, name?: string): Scenario {
  const copy = JSON.parse(JSON.stringify(source)) as Scenario;
  copy.id = newScenarioId();
  copy.name = name ?? `${source.name} (copy)`;
  return copy;
}
