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
// Board.save persistence: on the PHYSICAL BOARD the WebView's `localStorage` is
// NOT namespaced by the stable Board `appId`, so it can be wiped on app
// reinstall/relaunch — which is exactly why authored battles "didn't persist" on
// device. The durable fix is to MIRROR custom scenarios into `Board.save` (the
// SDK's app-scoped save store, keyed by the stable `appId`). We keep the whole
// custom list as ONE save slot (identified by {@link BOARD_SAVE_DESCRIPTION}):
//   * startup  → async {@link hydrateCustomScenarios} reads the slot, merges it
//                over the localStorage cache (Board.save wins), and notifies
//                listeners so the menu re-renders with the durable list;
//   * upsert/delete → write localStorage synchronously (browser fallback +
//                secondary cache) AND schedule a Board.save write.
// In the browser none of this is on — `loadBoard()` resolves null, so we stay on
// the localStorage-only path and preview keeps working.

import * as Config from "./config";
import { ShipClass } from "../ships/shipClass";
import { loadBoard, type BoardSaveApi } from "../board/sdk";
import { SCENARIOS, type Scenario, type ShipPlacement, type LandShape } from "./scenarios";

const STORAGE_KEY = "trafalgar.customScenarios.v3";
/** Older storage keys, listed OLDEST→NEWEST. On load we forward-MIGRATE the most
 *  recent of these into the current key (rather than blindly deleting it) so a
 *  storage-key/format bump never silently drops the user's authored battles — the
 *  scenario survives even if an old fleet shape sanitises to empty and has to be
 *  re-populated in the editor. Once migrated, the legacy keys are removed. */
const LEGACY_KEYS = ["trafalgar.customScenarios.v1", "trafalgar.customScenarios.v2"];
const W = Config.ArenaHalfX;
const H = Config.ArenaHalfZ;

/** The single Board.save slot that holds the whole custom-scenario list. The OS
 *  scopes saves by the stable `appId`, so this survives reinstall/relaunch. */
const BOARD_SAVE_DESCRIPTION = "trafalgar:customScenarios";
/** Game-version tag stamped on the Board.save record (informational). */
const BOARD_SAVE_GAME_VERSION = "0.1.0";

/** In-memory cache of the parsed custom list (lazy-loaded from storage). */
let cache: Scenario[] | null = null;

// ---------------------------------------------------------------------------
// Read / write
// ---------------------------------------------------------------------------

/** Parses one storage slot into a sanitised scenario list (empty if absent or
 *  unreadable). Pure read — never mutates storage. */
function parseSlot(raw: string | null): Scenario[] {
  if (!raw) return [];
  try {
    const parsed: unknown = JSON.parse(raw);
    if (!Array.isArray(parsed)) return [];
    return parsed.map((s) => sanitizeScenario(s)).filter((s): s is Scenario => s !== null);
  } catch {
    return [];
  }
}

function purgeLegacyKeys(): void {
  for (const k of LEGACY_KEYS) localStorage.removeItem(k);
}

function readStorage(): Scenario[] {
  try {
    const current = parseSlot(localStorage.getItem(STORAGE_KEY));
    if (current.length > 0) {
      // Current data wins; drop any stale legacy copies left behind.
      purgeLegacyKeys();
      return current;
    }
    // No current data: forward-migrate the NEWEST legacy key that still holds
    // recoverable scenarios, persist it under the current key, then clean up. This
    // is what makes customs survive a storage-key bump instead of vanishing.
    for (let i = LEGACY_KEYS.length - 1; i >= 0; i--) {
      const migrated = parseSlot(localStorage.getItem(LEGACY_KEYS[i]));
      if (migrated.length > 0) {
        writeStorage(migrated);
        purgeLegacyKeys();
        return migrated;
      }
    }
    purgeLegacyKeys();
    return current; // genuinely empty
  } catch {
    return []; // corrupt/unavailable storage → behave as if there are no customs
  }
}

/** Persists the list, returning `false` if storage rejected the write (quota,
 *  privacy mode, or a host WebView with DOM storage disabled). Callers MUST NOT
 *  treat a save as durable without checking this — a swallowed failure here is
 *  exactly how "my scenarios aren't persisting" looks to the player. */
function writeStorage(list: Scenario[]): boolean {
  try {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(list));
    return true;
  } catch (err) {
    // Keep the in-memory cache so the session still works, but SIGNAL the failure
    // so the editor can warn the user instead of silently closing as if saved.
    console.warn("[scenarioStore] failed to persist custom scenarios:", err);
    return false;
  }
}

// ---------------------------------------------------------------------------
// Change notification — lets the menu rebuild its gallery once the async
// Board.save hydration replaces the initial localStorage-only cache.
// ---------------------------------------------------------------------------

type ChangeListener = () => void;
const changeListeners = new Set<ChangeListener>();

/** Subscribe to custom-scenario list changes (e.g. async Board.save hydration).
 *  Returns an unsubscribe fn. */
export function subscribeScenarios(cb: ChangeListener): () => void {
  changeListeners.add(cb);
  return () => {
    changeListeners.delete(cb);
  };
}

function notifyChanged(): void {
  for (const cb of changeListeners) {
    try {
      cb();
    } catch (err) {
      console.warn("[scenarioStore] scenario-change listener threw:", err);
    }
  }
}

// ---------------------------------------------------------------------------
// Board.save mirror (durable, app-scoped persistence on device)
// ---------------------------------------------------------------------------

/** Resolves the on-device Board.save API once; null in the browser (or if the
 *  host doesn't expose save), so every caller degrades to localStorage-only. */
let boardSavePromise: Promise<BoardSaveApi | null> | undefined;
function boardSave(): Promise<BoardSaveApi | null> {
  if (!boardSavePromise) {
    boardSavePromise = loadBoard()
      .then((b) => (b?.isOnDevice && b.save ? b.save : null))
      .catch(() => null);
  }
  return boardSavePromise;
}

/** Reads the custom-scenario list out of the Board.save slot. Returns `null` on
 *  any failure (so the caller keeps the localStorage cache), `[]` if absent. */
async function readBoardSave(api: BoardSaveApi): Promise<Scenario[] | null> {
  try {
    const saves = await api.list();
    const slot = saves.find((s) => s.description === BOARD_SAVE_DESCRIPTION);
    if (!slot) return [];
    const bytes = await api.load(slot.id);
    return parseSlot(new TextDecoder().decode(bytes));
  } catch (err) {
    console.warn("[scenarioStore] Board.save read failed:", err);
    return null;
  }
}

/** Serialises Board.save writes so rapid upserts/deletes can't race; each write
 *  persists the LATEST cache, so coalescing is safe. */
let boardWriteChain: Promise<void> = Promise.resolve();
function scheduleBoardWrite(): void {
  boardWriteChain = boardWriteChain.then(writeBoardSaveNow).catch((err) => {
    console.warn("[scenarioStore] Board.save write failed:", err);
  });
}

async function writeBoardSaveNow(): Promise<void> {
  const api = await boardSave();
  if (!api) return; // browser — localStorage-only
  const list = cache ?? [];
  const saves = await api.list();
  const slot = saves.find((s) => s.description === BOARD_SAVE_DESCRIPTION);
  // Don't mint an empty slot just to hold "[]" — only persist once there's
  // something to keep (or a slot already exists that we must keep in sync).
  if (!slot && list.length === 0) return;
  const data = new TextEncoder().encode(JSON.stringify(list));
  if (slot) {
    await api.update(slot.id, BOARD_SAVE_DESCRIPTION, data, 0, BOARD_SAVE_GAME_VERSION);
  } else {
    await api.create(BOARD_SAVE_DESCRIPTION, data, 0, BOARD_SAVE_GAME_VERSION);
  }
}

function mergeById(local: Scenario[], board: Scenario[]): Scenario[] {
  const byId = new Map<string, Scenario>();
  for (const s of local) byId.set(s.id, s);
  for (const s of board) byId.set(s.id, s); // Board.save wins on id conflicts
  return Array.from(byId.values());
}

let hydrated = false;
/**
 * Asynchronously hydrates the custom-scenario cache from Board.save (on device).
 * Merges the durable Board.save list OVER the localStorage cache, persists the
 * merged set to BOTH stores (so any local-only customs created before this fix
 * become durable), and notifies listeners so the menu refreshes. Idempotent and
 * a no-op in the browser. Call once at startup; safe to await or fire-and-forget.
 */
export async function hydrateCustomScenarios(): Promise<void> {
  if (hydrated) return;
  hydrated = true;
  const api = await boardSave();
  if (!api) return; // browser — nothing to hydrate
  const boardList = await readBoardSave(api);
  if (boardList === null) return; // read failed — keep the localStorage cache
  const merged = mergeById(customScenarios(), boardList);
  cache = merged;
  writeStorage(merged); // refresh the localStorage secondary cache
  scheduleBoardWrite(); // push the full merged set up so everything is durable
  notifyChanged();
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

/** Inserts or replaces a custom scenario by id. Returns `true` if the new list
 *  was durably persisted; `false` means it lives only in this session's cache. */
export function upsertCustomScenario(scenario: Scenario): boolean {
  const list = customScenarios().slice();
  const i = list.findIndex((s) => s.id === scenario.id);
  if (i >= 0) list[i] = scenario;
  else list.push(scenario);
  cache = list;
  const ok = writeStorage(list);
  scheduleBoardWrite(); // durable mirror on device (async; no-op in browser)
  return ok;
}

/** Removes a custom scenario by id (no-op for built-ins). Returns `true` if the
 *  pruned list was durably persisted. */
export function deleteCustomScenario(id: string): boolean {
  cache = customScenarios().filter((s) => s.id !== id);
  const ok = writeStorage(cache);
  scheduleBoardWrite(); // mirror the deletion to Board.save on device
  return ok;
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
    const ship: ShipPlacement = {
      pos: { x: clamp(num(pos.x, 0), -W, W), z: clamp(num(pos.z, 0), -H, H) },
      headingDeg: num(s.headingDeg, 90),
      shipClass: sanitizeShipClass(s.shipClass),
    };
    const name = str(s.name, "").trim();
    if (name) ship.name = name.slice(0, 40);
    ships.push(ship);
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
  if (o.randomWind === true) scenario.randomWind = true;
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
