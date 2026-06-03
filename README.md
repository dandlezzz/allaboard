# Trafalgar — Age of Sail

Trafalgar — Age of Sail is a Board Web SDK port of the Unity **Trafalgar** naval
combat prototype. It runs as a Vite + TypeScript canvas game inside the Board
WebView Android wrapper. Two fleets of 18th-century sailing warships — **British**
vs **Franco-Spanish** — manoeuvre with the wind and trade broadsides until one
side is sunk or captured.

> **Status:** this repo currently holds the **web scaffold**. The directory
> layout, build tooling, and the Board input adapter are in place; the gameplay
> systems are stubbed and being ported from the Unity project under `unity/`.

## Layout

- `web/`: Vite + TypeScript game source.
- `android/`: Android WebView wrapper with Board touch bridge integration.
- `scripts/`: build/deploy helpers (`build_android.sh`).
- `unity/`: the original Unity prototype (the gameplay source of truth being ported).
- `_refs/`: art/reference material (e.g. top-down ship reference).
- `Builds/Android/`: copied APK output from the project build helper.
- Shared SDK bundle: `$HOME/board/board-websdk/`.

## Identity

- Android package/application id: `com.defaultcompany.trafalgarweb`
- Android display label: `Trafalgar — Age of Sail`
- Board app id: `trafalgar-web`
- APK output: `Builds/Android/TrafalgarWeb.apk`

## Build And Install

```bash
./scripts/build_android.sh
./scripts/build_android.sh --install
./scripts/build_android.sh --launch
```

The wrapper builds `web/dist`, packages it into Android assets, copies the debug
APK to `Builds/Android/TrafalgarWeb.apk`, and can install or launch with `bdb`.
The web dependency and Android AAR resolve from the shared SDK bundle at
`$HOME/board/board-websdk/`. Override this with `BOARD_WEBSDK_DIR=/path/to/sdk`
or `-PboardWebSdkDir=/path/to/sdk`.

For browser-only iteration:

```bash
cd web
npm install
npm run dev
```

The browser preview uses a mouse/pointer fallback. Board hardware uses
`Board.input.subscribe(...)` for glyph and finger contacts (see
`web/src/board/input.ts`).

## The Unity prototype (`unity/`)

The original game — an entirely procedural Unity 2021.3 project — lives in
`unity/` and remains fully intact (`unity/Assets`, `unity/Packages`,
`unity/ProjectSettings`, and the Board **Unity** SDK at `unity/package`). Open
`unity/` as a Unity project and press Play. See `unity/README.md` for the full
gameplay design, controls, and mechanics. The web port mirrors its module split
under `web/src/`.
