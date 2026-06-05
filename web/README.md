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

> **Heads up:** the Board Web SDK (`@board.fun/web-sdk`) is **optional** here.
> `npm install` and `npm run build` work with the public dependencies alone —
> [`src/board/sdk.ts`](src/board/sdk.ts) shims the SDK with local interfaces and a
> guarded dynamic import, so nothing fails to resolve when the SDK is absent. See
> [Linking the SDK](#linking-the-sdk) to add the real package for on-device builds.

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

The Board Web SDK is published as **`@board.fun/web-sdk`**. It is **not** a hard
dependency of this project: [`src/board/sdk.ts`](src/board/sdk.ts) describes the
slice of the SDK we use with local interfaces and loads the real module at
runtime via a guarded dynamic import, so the app installs, builds, and runs in
the browser (mouse fallback) with only public deps.

To get the real typed APIs / on-device behaviour, install it:

```bash
npm install @board.fun/web-sdk
```

The dynamic import in `sdk.ts` then resolves at runtime, and `Board.isOnDevice`
gates the on-device path. The documented static-import form is
`import { Board, BoardContactType } from "@board.fun/web-sdk";`.
