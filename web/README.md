# Trafalgar — Age of Sail (Web)

Vite + TypeScript port of the Unity **Trafalgar — Age of Sail** prototype, built
on the Board Web SDK. This currently contains the **structural scaffold**: the
canvas, the Board/pointer input adapter, and an empty render loop. Gameplay
systems are stubbed and live under `src/` folders that mirror the Unity module
split (see below).

## Run it

```bash
npm install
npm run dev
```

Opens at <http://localhost:5173>. Off-device, `Board.isOnDevice` is `false`, so
the Board SDK calls are skipped and the mouse/pointer fallback is used. This mode
is for styling, wiring UI, and syntax-checking SDK calls.

> **Heads up:** `npm install` needs the private Board Web SDK tarball
> (`@harrishill/board-sdk`) — see [Linking the SDK](#linking-the-sdk). Until that
> tarball is present, install and `npm run build` will fail to resolve the SDK,
> exactly as in the upstream Board-binho project.

## Run it against a real bridge

The SDK bridge (`window.BoardSDK` / `window.boardTouch`) only exists inside a
Board WebView. Two ways to get one on your dev machine:

1. **Android wrapper (in this repo).** Build this web app (`npm run build`), then
   build the wrapper from `../android` (or use `../scripts/build_android.sh`). The
   Gradle task copies `dist/` into `android/app/src/main/assets/web/`. The native
   bridge is arm64-only, so install the APK on an arm64 Android device/emulator.
2. **A real Board device.** Serve the built `dist/` from anywhere and point the
   device's WebView host at the URL.

## Build

```bash
npm run build
```

Outputs to `dist/`. `vite.config.ts` uses `base: "./"` so the built HTML works
whether it's loaded via `file://` from Android assets or served from any subpath.

## `src/` layout

The source mirrors the Unity project's `Assets/Scripts/` module split so ported
systems land in an obvious place:

| Folder | Web port of Unity… | Holds |
|---|---|---|
| `src/board/` | `Input/` (`InputRouter`, `PointerSample`) | Board SDK input adapter + mouse fallback (**done**) |
| `src/core/` | `Core/` | game loop, config, match state, win detection (stub) |
| `src/ships/` | `Ships/` | ship model, classes, sail/ammo, factory, view (stub) |
| `src/combat/` | `Combat/` | wind, broadsides, boarding, projectiles (stub) |
| `src/ai/` | `AI/` | the solo Franco-Spanish fleet AI (stub) |
| `src/rendering/` | `Rendering/` | procedural sea / ships / smoke as canvas drawing (stub) |
| `src/ui/` | `UI/` | the code-built HUD (stub) |
| `src/main.ts` | `Core/GameBootstrap` | entry point: canvas + input + pause context |

## Linking the SDK

`package.json` references the SDK via
`file:../../board-websdk/harrishill-board-sdk-0.1.0.tgz`, mirroring Board-binho's
shared-bundle convention. `@harrishill/board-sdk` is **not** on public npm —
fetch it from the Board developer portal (<https://dev.board.fun/>) and place the
tarball at that path (or update the relative `file:` path to wherever you keep the
shared `board-websdk/` bundle).
