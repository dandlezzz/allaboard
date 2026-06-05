# Board Web SDK — AI Assistant Quick Reference

> Source: https://docs.dev.board.fun/web/ai-assistant — pasted here as a project
> rules file so any AI assistant (Cursor/Claude/Copilot) writes correct, idiomatic
> Board Web SDK code for this `web/` app. Follow these conventions.

You are writing TypeScript against the Board Web SDK (`@board.fun/web-sdk`). Board is a
23.8" 1080p landscape touch console; its sensor detects fingers and physical Pieces
(tokens with conductive Glyph patterns). The SDK is ESM-only and runs the web app inside
Board's built-in browser.

## Basics
- Import the single frozen `Board` object: `import { Board, BoardContactType } from "@board.fun/web-sdk";`.
  Features hang off six domains: `Board.input`, `Board.session`, `Board.save`, `Board.avatar`,
  `Board.pause`, `Board.application`.
- ALWAYS gate device calls on `Board.isOnDevice`. In a desktop browser it is `false` and
  service-backed calls do NOT no-op: sync calls (`Board.session.*`, `Board.pause.*`,
  `Board.application.*`, the `save` getters) THROW, and async calls (`Board.save.*`,
  `Board.avatar.*`, `Board.session.present*`) REJECT (native bridge absent). The ONE
  exception is `Board.input.getContacts()`, which safely returns `[]`. So: gate sync calls
  behind `if (Board.isOnDevice)` and wrap async calls in `try/catch`, so the same build
  runs with or without hardware.
- Do NOT gate on `Board.sdkVersion` (informational only). The runtime capability gate is
  `Board.session.areServicesReady()`.

## Touch input
- Subscribe to `Board.input`; each callback gets a full per-frame snapshot of contacts.
  Filter Pieces by `glyphId`, NOT by contact type.
- Contact fields: `contactId`, `type` (`BoardContactType.Finger`/`Glyph`/`Blob`), `glyphId`,
  `x`, `y` (DEVICE PIXELS, origin TOP-LEFT, Y DOWN — no flip), `orientation`, `phase`.
- There are NO discrete down/up events — keep a previous-frame map keyed by `contactId` and
  diff it for edges. Finger touch needs no model; Piece detection uses a Piece Set Model
  recorded at pack time.

```ts
function onContacts(contacts: BoardContact[]) {
  for (const c of contacts) {
    if (c.type === BoardContactType.Glyph) handlePiece(c.glyphId, c.x, c.y, c.orientation);
  }
}
if (Board.isOnDevice) Board.input.subscribe(onContacts);
```

## Players / sessions
- Roster is OS-owned; never silently add/remove players. Read: `getPlayers()`,
  `getPlayerCount()`, `getActiveProfile()`. Change via OS selector
  `presentAddPlayer(aiTypeIndices?)` / `presentReplacePlayer(sessionId, aiTypeIndices?)`
  (resolve `true`=added/replaced, `false`=dismissed) or `resetPlayers()`. Declare AI types
  with `setAIPlayerTypes([{ name, description }])`.

## Save games
- Promise-based `create`, `load`, `list`, `update`. No direct delete — remove your own
  involvement via `removePlayersFromSave` or `removeActiveProfileFromSave`; the OS deletes
  the save once no players remain. Also `loadCoverImage`, `getAppStorageInfo()`,
  `getUniquePlayers(saves)`. Payloads are `Uint8Array`.

## Avatars
- `Board.avatar.loadPNG(avatarId)` → cached PNG data URI; `getDefault()` = avatar 0;
  `forPlayer(player)` shortcut. Assign the data URI to an `<img>` `src`.

## Pause overlay
- OS owns the menu button/UI; the game supplies context and reads results:
  `setContext(context)`, `updateContext(partial)`, `clearContext()`. Subscribe with
  `onResult(callback)` (preferred; legacy `pollResult()`).

```ts
Board.pause.setContext({ offerSaveOption: true, customButtons: [{ id: "restart", title: "Restart", icon: "circulararrow" }] });
Board.pause.onResult((r) => {
  if (r.action === "quit" || r.action === "save_and_quit") Board.application.quit();
  if (r.action === "custom_button" && r.customButtonId === "restart") restartGame();
});
```

## Application lifecycle
- `Board.application.quit()` returns to the launcher. `showProfileSwitcher()` /
  `hideProfileSwitcher()` drive the OS profile switcher.

## Build & deploy
- Build a STATIC app with RELATIVE paths (`base: "./"` in Vite).
- Pack: `web-pack dist --package-id fun.board.<name> --name "<Name>"` → writes a flat
  `.webapp.zip` and mints a random-UUID `appId` persisted to `board.config.json`.
  COMMIT `board.config.json` so the appId (and therefore saved games) survives rebuilds.
- For Piece games, download the Piece Set Model out of band and pass `--model <path>` to
  web-pack; the tooling never bundles/downloads it at runtime.
- Pair once: `board-connect pair <addr>` (tap Approve on the device) — saves it as default.
  Deploy: `board-connect install <app>.webapp.zip --launch`. Logs:
  `board-connect logs --follow`. Verify: `board-connect capabilities` (MP.1.9.x or newer).
  If the first install reports host unavailable, foreground the Board Browser once and retry.

## Key gotchas
- Gate on `Board.isOnDevice`, not `Board.sdkVersion`; runtime gate is `Board.session.areServicesReady()`.
- Y-down coordinates, no flip; contacts are device pixels, origin top-left.
- Identify Pieces by `glyphId`, not contact type.
- No direct save delete; remove your own players and the OS cleans up.
- Commit `board.config.json` so `appId`/saves survive rebuilds.
