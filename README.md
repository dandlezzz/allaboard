# Broadsides

**Broadsides** is an overhead, age-of-sail naval real-time strategy game for the
[board.fun](https://board.fun) tabletop console — and it's fully playable in a
desktop browser too. Two fleets of 18th-century sailing warships — **British**
vs **Franco-Spanish** — beat against the wind and trade thundering broadsides
until one side is sunk or struck.

It's built to be played around the table on a shared horizontal display: command
your squadron with a physical **Baton of Command** piece on the Board, or with
the mouse in the browser.

## Gameplay

- **Command a squadron with the Baton of Command.** Place your command piece (a
  physical Glyph on the Board, a mouse click in the browser) next to a ship to
  take command of that squadron.
- **Hold and rotate to steer.** While the baton is held down, rotating it sets
  your fleet's course; lift it to lock in the heading and let them sail it.
- **Trim sail and ammunition on the ship.** Each commanded ship shows an on-hull
  **sail-mast** control (reef down to slow/turn tight, hoist full for speed) and
  an **ammo** selector for the next broadside.
- **Mind the points of sail.** The wind is everything. There are six points of
  sail, each with its own speed factor and colour cue — run before the wind for
  speed, but you can't sail straight into it, so you'll have to **tack**.
- **Win** by sinking or capturing the enemy fleet.

### Opponents

Pick your foe on the **start screen**:

- **Standard** — a balanced AI that manoeuvres and tacks upwind.
- **Turtle** — holds station and keeps its guns blazing.
- **Giga-brain** — the tactician; manoeuvres to cross your bow/stern and rake.
- **2 Players** — local hot-seat: both fleets are human-controlled, and the
  battle starts once *both* captains have placed a command piece.

## Tech

- **Vite + TypeScript + PixiJS** — the whole game is a procedurally-rendered
  PixiJS canvas app (sea, ships, HUD all drawn in code).
- **Board Web SDK** — touch/glyph input, the OS pause menu, and app lifecycle on
  Board hardware. SDK access is shimmed (`web/src/board/sdk.ts`) and every call
  is guarded by `Board.isOnDevice`, so the same build runs in the browser with a
  pointer-event fallback.

## Run in the browser

```bash
cd web
npm install
npm run dev
```

Open the printed local URL. The browser uses a mouse/pointer fallback for the
Baton of Command — click to place it, drag to steer, click the on-ship controls
to trim.

## Deploy to a Board

Build `web/`, package it into a `.webapp.zip` with `@board.fun/web-pack`, and
install it on a paired Board over the LAN with `board-connect`:

```bash
./scripts/deploy_board_web.sh            # build + package only
./scripts/deploy_board_web.sh --install  # install on the Board
./scripts/deploy_board_web.sh --launch   # install and launch
```

The script resolves the target Board from `BOARD_HOST` (e.g.
`BOARD_HOST=192.168.4.85 ./scripts/deploy_board_web.sh --launch`) and finds
`board-connect` via `BOARD_CONNECT_BIN`, `PATH`, or `~/.local/bin`. See
[`BOARD_HARDWARE.md`](BOARD_HARDWARE.md) for prerequisites and device pairing.
Continuous browser deployment to Vercel is described in
[`DEPLOYMENT.md`](DEPLOYMENT.md).

## Project layout

```
web/                 the game — Vite + TypeScript + PixiJS (the build target)
  src/
    board/           Board SDK input adapter + mouse/pointer fallback
    core/            game loop, config, match state, setup/start flow
    ships/           ship model, classes, sail/ammo
    combat/          wind + six points of sail, broadsides, boarding
    ai/              the solo fleet AI (Standard / Turtle / Giga-brain)
    rendering/       procedural sea + ships drawn to the canvas
    ui/              the code-built HUD and "Broadsides" start screen
scripts/             build/deploy helpers (deploy_board_web.sh)
docs/                design notes and Board SDK reference material
android/             legacy Android WebView wrapper (not the primary path)
```

> **Note:** the underlying Board app identity is still `trafalgar-web`
> (package id `com.defaultcompany.trafalgarweb`) from the project's working
> title; the game is **Broadsides**.

## Scripts (`web/`)

| Command            | What it does                              |
| ------------------ | ----------------------------------------- |
| `npm run dev`      | Vite dev server (browser preview)         |
| `npm run build`    | Type-check (`tsc --noEmit`) + Vite build  |
| `npm run preview`  | Preview the production build              |
| `npm run pack:board` | Build and package a `.webapp.zip`       |
