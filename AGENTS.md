# AGENTS.md

This repository is the **Board Web SDK** game Trafalgar — Age of Sail.

## What to assume

- The web app (`web/`) is the product and the build target: a Vite + TypeScript
  + PixiJS canvas game that runs on Board hardware and in the browser.
- Deploy to Board hardware with `scripts/deploy_board_web.sh` (build →
  `@board.fun/web-pack` → `board-connect install`). See `BOARD_HARDWARE.md`.
- The source Board Web SDK bundle lives at `$HOME/board/board-websdk`.
- Use `Board.input.subscribe(...)` for live contact frames (see
  `web/src/board/input.ts`).
- Track physical piece instances by `contactId`, not `glyphId`.
- Treat `glyphId` as a piece type identifier only.
- Always guard SDK calls with `Board.isOnDevice` so browser preview works.
- `web/AGENTS.md` holds the detailed Board Web SDK rules for `web/`.

## Project identity

- Board app id: `trafalgar-web`
- Board package id: `com.defaultcompany.trafalgarweb`
- Web app: `web/`
- Legacy Android WebView wrapper: `android/` (not the primary deploy path)
- Shared SDK bundle: `$HOME/board/board-websdk`

## Web source layout

`web/src/` is split into gameplay modules:

- `board/` — Board SDK input adapter + mouse fallback
- `core/` — game loop, config, match state
- `ships/` — ship model, classes, sail/ammo
- `combat/` — wind, broadsides, boarding
- `ai/` — the solo fleet AI
- `rendering/` — procedural sea/ships drawn to the canvas
- `ui/` — the code-built HUD

## Deploy loop

Prefer:

```bash
./scripts/deploy_board_web.sh --install
```

Use `--launch` to install and start the app:

```bash
./scripts/deploy_board_web.sh --launch
```

The script resolves `board-connect` from `BOARD_CONNECT_BIN`, `PATH`, and
`$HOME/.local/bin/board-connect`.

**ALWAYS commit after every Board deploy.** Immediately after any successful
deploy (`scripts/deploy_board_web.sh` / `board-connect install`), commit the
whole working tree so the on-Board build always maps to a commit:

```bash
git add -A && git commit -m "Deploy to Board: <what changed>"
```

Do this without being asked. (The `Builds/` deploy artifact is gitignored.)
See the `deploy-board` skill for the full pipeline.

## Browser loop

```bash
cd web
npm run dev
```

The browser preview uses pointer-event fallback input; do not fake
`Board.isOnDevice` in app code.
