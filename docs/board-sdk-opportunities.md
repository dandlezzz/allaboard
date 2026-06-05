# Board SDK Opportunities for *Trafalgar — Age of Sail*

> Read-only investigation of which Board Web SDK capabilities the web game
> (`web/`) currently uses versus what it could adopt. Documented facts are cited
> to their Board doc page; everything under **Suggestion** is our own proposal,
> not a Board recommendation.
>
> Scope note: capabilities are shared across the Board SDKs. The downloaded
> references under `docs/unity-sdk/` are the cross-SDK guides (each file header
> links its `docs.dev.board.fun` source); Unity API names below are mapped to
> their Web SDK equivalents per `web/AGENTS.md` and the Web overview at
> <https://docs.dev.board.fun/web/>.

---

## TL;DR — top unused features worth adopting

**#1 highest-value: Save Games (`Board.save.*`).** Today we already set
`offerSaveOption: false` and quietly map `save_and_quit` to a plain quit, so the
pause overlay's save path is wired but inert. Turning on real saves (save/resume
a battle, persist a campaign) is the single biggest player-facing win and it
slots directly into the pause flow we already own.

Runner-ups, in priority order:

1. **Save Games** — `Board.save.create/load/list/update` + cover image. Resume a
   half-fought battle; later, campaign persistence. **(High value / Med effort)**
2. **Players & sessions** — `Board.session.getPlayers/setAIPlayerTypes/presentAddPlayer`.
   Map our existing AI personas (Turtle / Tactician / Standard) to declared AI
   types and our 2-player toggle to a real OS roster. **(High value / Med effort)**
3. **Avatars** — `Board.avatar.forPlayer` to badge each admiral's avatar on the
   HUD / their fleet. **(Med value / Low effort, depends on #2)**
4. **Pause polish** — set `gameName`, add `audioTracks` sliders, and actually
   honor `offerSaveOption` (ties to #1). **(Med value / Low effort)**
5. **Profile Switcher** — `Board.application.showProfileSwitcher()` on the Setup /
   game-over screens. **(Low value / Low effort)**
6. **Background-safe lifecycle** — pause the sim on `visibilitychange` (overlay
   open / app backgrounded). **(Med value / Low effort)**
7. **Richer Piece input** — Blob contacts, multi-finger gestures (shake / twist /
   trace), and unlimited simultaneous contacts for true multi-admiral play.
   **(High value / High effort)**

---

## What we already use (USED)

From `web/src/board/`:

- **`Board.input.subscribe`** — live per-frame contact snapshots, diffed by
  `contactId` into began/moved/ended edges (`input.ts`).
- **Contact fields** `contactId`, `x`, `y`, `orientation`, `glyphId`, `type`,
  `phase`, and **`isTouched`** (threaded into `PointerSample.touched` to gate
  rotate-to-steer on "held"). `Canceled` phase is treated as a lift (`sdk.ts`
  `isEndedPhase`, per App Lifecycle's "background cancels every contact" rule).
- **`Board.pause.setContext`** — registers an in-match context with a single
  custom **Restart** button (`pauseMenu.ts`).
- **`Board.pause.onResult`** — handles `quit`, `save_and_quit` (→ quit), and the
  `custom_button` Restart.
- **`Board.pause.clearContext`** — on game over / return to Setup.
- **`Board.application.quit`** — on quit / save-and-quit.
- **`Board.isOnDevice` gating** — device calls gated; browser uses pointer
  fallback (`input.ts`, `pauseMenu.ts`).
- **A glyph→Baton adapter exists** (`batonGlyphAdapter.ts`) but is intentionally
  **not wired** yet (place/slide/rotate of a single Piece as the Baton).

Notably **not** used today: the entire `Board.save`, `Board.session`, and
`Board.avatar` domains; `Board.application.showProfileSwitcher/hideProfileSwitcher`;
pause `gameName` / `audioTracks` / a live `offerSaveOption`; `updateContext`;
Blob contacts; multi-finger gestures; and visibility-based pause.

---

## Capability matrix (the deliverable)

Prioritized highest-value first. **Status** is a documented fact about our code;
**Suggestion** is our proposal.

### 1. Save Games — `Board.save.*`  *(HIGH value / MED effort)*

> Docs: [Save Games guide](https://docs.dev.board.fun/guides/save-games) ·
> [Board.Save API](https://docs.dev.board.fun/web/) · `docs/unity-sdk/guide-save-games.md`

| Capability | Used? | Suggestion for Trafalgar | Effort / Value |
| --- | --- | --- | --- |
| `Board.save.create(desc, payload: Uint8Array, playedTimeMs, gameVersion)` | **NOT USED** | On `save_and_quit` (and an optional pause "Save Battle" button), serialize the match — ship positions/headings/hull-state, wind, sail/ammo settings, score, active AI persona, schema `version` — to a `Uint8Array` and create a save. | Med / High |
| `Board.save.update(id, …)` | **NOT USED** | Track `currentSaveId`; update instead of duplicating on subsequent saves (autosave at turn/round checkpoints — Board is wall-powered and can be torn down without warning, per App Lifecycle). | Low / High |
| `Board.save.load(id) → Uint8Array` | **NOT USED** | A "Resume battle" entry on the Setup screen rebuilds the fleet from the payload. Note: **loading mutates the roster** to the save's players — re-read `getPlayers()` afterward (Web has no players-changed event). | Med / High |
| `Board.save.list()` | **NOT USED** | Render a save-slot list on Setup (sorted newest-first), showing `description`, `updatedAt`, `playedTime`, `playerCount`. | Med / High |
| `Board.save.loadCoverImage(id) → data URI` | **NOT USED** | Show a battle thumbnail per save slot. (Web can *read* covers; only Unity can *write* them, so we can't author a screenshot cover from Web today — use a generated/static cover image instead.) | Low / Med |
| `removePlayersFromSave(id)` / `removeActiveProfileFromSave(id)` | **NOT USED** | "Delete save" = remove our players; OS deletes once none remain (there is **no direct delete**). Warn before removing the last player. | Low / Med |
| `getAppStorageInfo()`, `getMaxDataSize()`, `getMaxAppStorageSize()`, `getMaxDescriptionLength()` | **NOT USED** | Check payload vs limit before saving (defaults today: 16 MB/save, 64 MB/app, 100-char description — read at runtime, don't hardcode). | Low / Low |
| `getUniquePlayers(saves)` | **NOT USED** | Dedupe admirals across saves for a "continue as…" UI (profiles only; guests/AI excluded). | Low / Low |

### 2. Players & Sessions — `Board.session.*`  *(HIGH value / MED effort)*

> Docs: [Player Management guide](https://docs.dev.board.fun/guides/player-management) ·
> [Players & Sessions concepts](https://docs.dev.board.fun/learn/concepts) ·
> `docs/unity-sdk/guide-player-management.md`

| Capability | Used? | Suggestion for Trafalgar | Effort / Value |
| --- | --- | --- | --- |
| `setAIPlayerTypes([{name, description}])` | **NOT USED** | Register our three personas as declared AI types — `Turtle` ("Defensive, holds the line"), `Tactician` ("Maneuvers for raking fire"), `Standard` ("Balanced fleet command"). Then the OS "Add AI" UI offers exactly our personas (max 8). | Low / High |
| `getPlayers()` / `getPlayerCount()` | **NOT USED** | Drive a real per-seat roster instead of the ad-hoc `toggleSecondPlayer()` boolean: the British seat = one admiral, the Franco-Spanish seat = a Profile/Guest or an AI player. | Med / High |
| `presentAddPlayer(aiTypeIndices?)` | **NOT USED** | Replace the HUD persona buttons: tap "Add opponent" → OS selector; pass the persona's AI type index to filter the "Add AI" tab. Resolves `true` (added) / `false` (dismissed); re-read the roster after. | Med / High |
| `presentReplacePlayer(sessionId, aiTypeIndices?)` | **NOT USED** | Swap a human admiral for an AI (or change the AI persona) mid-campaign by targeting that seat's `sessionId`. | Med / Med |
| `aiTypeIndex` on AI players | **NOT USED** | When the roster has an AI player, read `aiTypeIndex` to instantiate the matching `FleetAI` persona — the persona becomes data on the roster, not a HUD toggle. | Low / High |
| `getActiveProfile()` | **NOT USED** | Title the match ("Admiral <name>'s fleet") and scope saves/preferences to the active profile's durable `playerId`. | Low / Med |
| `resetPlayers()` | **NOT USED** | "New game / clear opponents" → back to just the active profile. | Low / Low |
| `addGuest(sessionId)` / `removePlayer(sessionId)` *(Web-only direct calls)* | **NOT USED** | Quick local hot-seat: add a Guest second admiral without the full selector. | Low / Med |
| `isReady()` + `areServicesReady()` | **NOT USED** | Gate first roster/save reads on readiness (the roster can momentarily read empty at startup). This is the documented runtime capability gate (not `sdkVersion`). | Low / Med |

### 3. Avatars — `Board.avatar.*`  *(MED value / LOW effort, needs §2)*

> Docs: [Avatars guide](https://docs.dev.board.fun/guides/avatars) ·
> `docs/unity-sdk/guide-avatars.md`

| Capability | Used? | Suggestion for Trafalgar | Effort / Value |
| --- | --- | --- | --- |
| `Board.avatar.forPlayer(player) → PNG data URI` | **NOT USED** | Badge each side's commander avatar in the HUD (and optionally floating over their flagship), so the table can see whose fleet is whose. Assign the data URI to an `<img>.src`. | Low / Med |
| `Board.avatar.getDefault()` (avatar id 0) | **NOT USED** | Placeholder for an empty/AI seat before a player is added. | Low / Low |
| `Board.avatar.loadPNG(avatarId)` | **NOT USED** | Render avatars in a save-slot list for players who aren't in the current session. | Low / Low |
| `Board.avatar.clearCache()` | **NOT USED** | Drop cached avatars after a profile switch / roster change so a changed avatar re-fetches. | Low / Low |

### 4. Pause overlay polish — `Board.pause.*`  *(MED value / LOW effort)*

> Docs: [Pause Menu guide](https://docs.dev.board.fun/guides/pause-menu) ·
> `docs/unity-sdk/guide-pause-menu.md`

| Capability | Used? | Suggestion for Trafalgar | Effort / Value |
| --- | --- | --- | --- |
| `setContext` — Restart custom button | **USED** | (Keep.) | — |
| `setContext` — `gameName` | **NOT USED** | Set `gameName: "Trafalgar — Age of Sail"` so the overlay header is branded (we currently pass none). | Trivial / Low |
| `offerSaveOption: true` | **NOT USED** (hardcoded `false`) | Flip to `true` once §1 lands so the OS shows **Exit & Save**; we already route `save_and_quit` (but currently only quits). | Low / High |
| `audioTracks: [{id,name,value}]` | **NOT USED** | Add **Music** / **Cannons (SFX)** sliders (0–100); apply `result.audioTracks` first in `onResult`, every dismissal, before branching on action. | Low / Med |
| Additional custom buttons (max 8) | **partially** (1 button) | Add **How to Play** (`icon: "square"`) and a contextual **Surrender** (`icon: "doorwitharrow"`) during a battle. | Low / Med |
| `updateContext(partial)` | **NOT USED** (shim supports it) | Swap buttons / toggle `offerSaveOption` by phase (e.g. hide Save in Setup, show Surrender only mid-battle) without restating the whole context. | Low / Med |
| `result.audioTracks` handling | **NOT USED** | We ignore returned audio values; wire them to the audio system. | Low / Med |
| Result actions we don't handle: `resume` | **NOT USED** | We don't explicitly resume; combined with §6 (pause sim while overlay open) we should resume the loop on `resume`. | Low / Med |

### 5. Application lifecycle — `Board.application.*`  *(LOW–MED value / LOW effort)*

> Docs: [App Lifecycle guide](https://docs.dev.board.fun/guides/app-lifecycle) ·
> [Profile Switcher guide](https://docs.dev.board.fun/guides/profile-switcher) ·
> `docs/unity-sdk/guide-app-lifecycle.md`, `guide-profile-switcher.md`

| Capability | Used? | Suggestion for Trafalgar | Effort / Value |
| --- | --- | --- | --- |
| `Board.application.quit()` | **USED** | (Keep.) | — |
| `showProfileSwitcher()` / `hideProfileSwitcher()` | **NOT USED** (bridge methods declared in `sdk.ts` but not exposed on `BoardLike.application` or called) | Show the OS profile switcher on Setup / game-over / between rounds; hide it during active battle so an accidental tap doesn't disrupt play. | Low / Low |
| Background pause via host lifecycle (`visibilitychange`) | **NOT USED** | Pause the sim when `document.hidden` (overlay opened or app backgrounded) and resume when visible. We already clamp `dt`, but an explicit pause is cleaner and matches the documented model. | Low / Med |
| `Canceled`-phase release on background | **USED** | (`isEndedPhase` already tears down batons on `Canceled`.) | — |
| Reload player-scoped state after a profile switch | **NOT USED** | After showing the switcher / on focus regain, re-read `getActiveProfile()` + `getPlayers()` and reload that profile's saves (no players-changed event on Web). | Low / Med |

### 6. Input — under-used Piece & touch capabilities  *(HIGH value / HIGH effort)*

> Docs: [Touch system](https://docs.dev.board.fun/learn/touch-system) ·
> [Touch Input guide](https://docs.dev.board.fun/guides/touch-input) ·
> [Piece Interaction Design](https://docs.dev.board.fun/guides/piece-interaction-design) ·
> [Pieces](https://docs.dev.board.fun/learn/pieces) ·
> `docs/unity-sdk/learn-touch-system.md`, `guide-piece-interaction-design.md`, `api-class-BoardContactType.md`

| Capability | Used? | Suggestion for Trafalgar | Effort / Value |
| --- | --- | --- | --- |
| **Blob contacts** (`BoardContactType.Blob`, value 2) | **NOT USED** (we classify only Finger/Glyph; a Blob would fall through as a non-glyph finger) | Treat large/undefined Blob contacts (a flat hand or laid object) as a transient "fog/squall" or a wall the table can make — or just explicitly ignore them so a resting object near the baton doesn't read as a command. At minimum, recognize the type. | Med / Med |
| **Place & Lift** primitive | **partially** (baton adapter, unwired) | Wire `batonGlyphAdapter.ts`: placing the Baton Piece = issue a command at the nearest friendly ship; lifting = clear. This is the core native-to-Board loop. | Med / High |
| **Rotate** (continuous orientation) | **partially** (orientation read; baton adapter unwired) | Baton facing = commanded course, with a deadband + ~5° overshoot tolerance per the design guide. | Low / High |
| **Touch & Release / hold** (`isTouched`) beyond the baton | **partially** (parsed, used to gate steering) | Use the hold edge as a discrete "commit/fire" — pick up the Baton to arm a broadside, set it down to fire; or hold-to-lock a course. Diff `isTouched` per `contactId` frame-to-frame. | Med / Med |
| **Shake** gesture | **NOT USED** | Shake a Piece to "all hands" / rally a damaged ship or signal retreat; detect from velocity reversals, reset on lift. | Med / Med |
| **Twist** gesture (quick discrete rotation) | **NOT USED** | Twist the Baton past a threshold to cycle sail setting (full/battle/reefed) or ammo type — read rate-of-change, not resting angle. | Med / Med |
| **Trace** gesture | **NOT USED** | Trace a path to plot a multi-waypoint course / line-of-battle formation; accumulate the `Moved` path, match leniently (~70%). | High / Med |
| **Unlimited simultaneous contacts** (no 10-point cap, ~170 Pieces) | **partially** (snapshot handles N, but game logic assumes ~1–2 batons) | True multi-admiral play: one Baton Piece per player around the table, plus finger taps from many hands at once — the SDK already tracks them all; the gameplay/AI seat model (§2) is the limiter. | High / High |
| **Multiple Glyph ids / Piece categories** | **partially** (adapter can filter by `glyphId`) | Use distinct Pieces: a **Pawn** baton (command), an **Action** piece (tap to fire/board), a **Reaction** piece (drop a buoy/objective marker). Branch on `glyphId` (Web: `1+` = Piece, `0` = finger). | Med / Med |

---

## Documented facts vs. our suggestions

- **Facts (cited above):** the API surface of each domain, call shapes, Web vs
  Unity naming, the no-direct-delete save rule, roster-mutation-on-load, the
  five pause icons, the Blob contact type, unlimited contacts, and the
  off-device throw/reject behavior all come from the linked Board docs and
  `web/AGENTS.md`.
- **Suggestions (ours):** every "Suggestion for Trafalgar" cell — mapping
  personas to AI types, baton gestures for sail/ammo, blob-as-squall, avatar
  badges, audio sliders, etc. — is a design proposal, not something the Board
  docs prescribe.

## Caveats / things to verify on hardware

- **Cover image authoring** is Unity-only today; Web can read but not write a
  cover (Save Games guide). A Web save cover would need a pre-generated image.
- **No players-changed event on Web** — every roster/profile/save-load change
  requires an explicit re-read of `getPlayers()` / `getActiveProfile()`.
- **`save_and_quit` currently just quits** (`pauseMenu.ts`) — adopting §1 means
  doing the actual save *before* `Board.application.quit()`.
- Our `sdk.ts` is a hand-rolled bridge shim, not the published `@board.fun/web-sdk`
  package; it currently exposes only `input` / `pause` / `application.quit`. Adding
  `session`, `save`, `avatar`, and `application.showProfileSwitcher` means
  extending the shim (or bundling the real SDK) — factor that into effort.
