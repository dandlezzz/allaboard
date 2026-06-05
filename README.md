# Trafalgar — Age of Sail

Trafalgar — Age of Sail is a Board Web SDK naval-combat game. It runs as a
Vite + TypeScript + PixiJS canvas app on Board (board.fun) hardware and in the
browser. Two fleets of 18th-century sailing warships — **British** vs
**Franco-Spanish** — manoeuvre with the wind and trade broadsides until one side
is sunk or captured.

> **Status:** active development. The web app under `web/` is the product and
> the build target; the directory layout, build/deploy tooling, and the Board
> input adapter are in place while the gameplay systems are filled in.

## Layout

- `web/`: the game — Vite + TypeScript + PixiJS source (the build target).
- `android/`: legacy Android WebView wrapper (kept for reference; not the
  primary deploy path).
- `scripts/`: build/deploy helpers (`deploy_board_web.sh`).
- `docs/`: design notes and downloaded Board SDK reference material.
- `_refs/`: art/reference material (e.g. top-down ship reference).
- Shared SDK bundle: `$HOME/board/board-websdk/`.

## Identity

- Board app id: `trafalgar-web`
- Board package id: `com.defaultcompany.trafalgarweb`
- Display name: `Trafalgar — Age of Sail`

## Develop

For browser iteration:

```bash
cd web
npm install
npm run dev
```

The browser preview uses a mouse/pointer fallback. Board hardware uses
`Board.input.subscribe(...)` for glyph and finger contacts (see
`web/src/board/input.ts`). See `web/AGENTS.md` for the Board Web SDK rules.

## Deploy to Board hardware

Build `web/`, package it into a `.webapp.zip` with `@board.fun/web-pack`, and
install it on a paired Board over the LAN with `board-connect`:

```bash
./scripts/deploy_board_web.sh            # build + package only
./scripts/deploy_board_web.sh --install  # install on the Board
./scripts/deploy_board_web.sh --launch   # install and launch
```

See [`BOARD_HARDWARE.md`](BOARD_HARDWARE.md) for prerequisites (the
`board-connect` CLI, `@board.fun/web-pack`, and pairing).

## Deploy to the browser (Vercel)

Continuous deployment of `web/` to Vercel is described in
[`DEPLOYMENT.md`](DEPLOYMENT.md).
