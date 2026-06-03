# AGENTS.md

This repository is the **Board Web SDK** version of Trafalgar — Age of Sail.

## What to assume

- This is a WebSDK Android/WebView project. The web app (`web/`) is the build
  target; the Unity project under `unity/` is the original prototype and the
  gameplay source of truth being ported.
- The source Board Web SDK bundle lives at `$HOME/board/board-websdk`.
- The Board **Unity** SDK (`fun.board`) is vendored at `unity/package` and
  referenced by `unity/Packages/manifest.json` as `file:../package`.
- Use `Board.input.subscribe(...)` for live contact frames (see
  `web/src/board/input.ts`).
- Track physical piece instances by `contactId`, not `glyphId`.
- Treat `glyphId` as a piece type identifier only.
- Always guard SDK calls with `Board.isOnDevice` so browser preview works.

## Project identity

- Android package id: `com.defaultcompany.trafalgarweb`
- Board app id: `trafalgar-web`
- APK output: `Builds/Android/TrafalgarWeb.apk`
- Web app: `web/`
- Android wrapper: `android/`
- Unity prototype: `unity/`
- Shared SDK bundle: `$HOME/board/board-websdk`
- Piece model: `android/app/src/main/assets/model.tflite` (supplied from the SDK)

## Web source layout

`web/src/` mirrors the Unity `Assets/Scripts/` module split:

- `board/` ← Unity `Input/` — Board SDK input adapter + mouse fallback (done)
- `core/` ← Unity `Core/` — game loop, config, match state (stub)
- `ships/` ← Unity `Ships/` — ship model, classes, sail/ammo (stub)
- `combat/` ← Unity `Combat/` — wind, broadsides, boarding (stub)
- `ai/` ← Unity `AI/` — the solo fleet AI (stub)
- `rendering/` ← Unity `Rendering/` — procedural sea/ships as canvas drawing (stub)
- `ui/` ← Unity `UI/` — the code-built HUD (stub)

## Build and deploy loop

Prefer:

```bash
./scripts/build_android.sh --install
```

Use `--launch` to install and start the app:

```bash
./scripts/build_android.sh --launch
```

The script resolves `bdb` from `BDB_BIN`, `PATH`, `Tools/bdb`, `$HOME/Desktop/bdb`,
and `$HOME/Documents/bdb`.

## Browser loop

```bash
cd web
npm run dev
```

The browser preview uses pointer-event fallback input; do not fake
`Board.isOnDevice` in app code.
