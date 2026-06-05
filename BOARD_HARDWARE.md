# Deploying Trafalgar to Board (board.fun) hardware

This is the **hardware** deploy path for the web game in [`web/`](web/): build →
package into a `.webapp.zip` → install on a physical Board over your LAN with the
`board-connect` CLI.

It is **additive** and does not disturb:

- the **Vercel** browser CD (see [`DEPLOYMENT.md`](DEPLOYMENT.md)), or
- the legacy Android WebView wrapper in [`android/`](android/).

Sources: Board Connect tool docs (<https://docs.dev.board.fun/tools/board-connect>),
the Touch/Contact guide (<https://docs.dev.board.fun/guides/touch-input>), and the
`@board.fun/web-pack` package README.

---

## TL;DR

```bash
# one-time: install the CLI, then pair with your Board (tap Approve on the device)
curl -fsSL https://dev.board.fun/connect/install | sh     # -> ~/.local/bin/board-connect
board-connect pair <board-ip>                              # then APPROVE on the Board

# build + package + install + launch
cd web && npm ci && npm run build
cd /Users/danielmiller/projects/boarders
scripts/deploy_board_web.sh --launch                       # uses BOARD_HOST or the saved default
```

Or do the packaging step alone with `cd web && npm run pack:board`.

---

## What's in this repo for hardware deploy

| File | Purpose |
|---|---|
| [`scripts/deploy_board_web.sh`](scripts/deploy_board_web.sh) | Build `web/` → package `.webapp.zip` → (optionally) `board-connect install --launch`. `--install` / `--launch` flags; honors `BOARD_HOST`. |
| `web/package.json` → `pack:board` | Build + package only, emitting `Builds/Board/trafalgar.webapp.zip`. |
| `web/package.json` → `deploy:board` | Thin wrapper for `scripts/deploy_board_web.sh --launch`. |
| `Builds/Board/trafalgar.webapp.zip` | The packaged bundle (gitignored; produced by the steps above). |

App identity stamped into the bundle (`harness-config.json`):

- **packageId**: `com.defaultcompany.trafalgarweb` (matches the Android package id)
- **appId**: `40d89417-f8f1-47c4-9899-4254a976ef7b` (UUID; the device namespaces
  on-device saves by this — keep it stable so saves survive reinstalls)
- **name**: `Trafalgar — Age of Sail`
- **model**: `null` (no Piece Set model bundled yet — see
  [Piece input on device](#piece-glyph-input-on-device))

---

## Prerequisites

1. **`board-connect` CLI** (cross-platform, talks to the Board over HTTP on port
   `8843`; no USB/ADB):

   ```bash
   curl -fsSL https://dev.board.fun/connect/install | sh   # macOS/Linux
   # Windows: irm https://dev.board.fun/connect/install.ps1 | iex
   board-connect --version
   ```

2. **`@board.fun/web-pack`** — the packager. **Public on npm**, so no auth needed;
   the scripts call it via `npx --yes @board.fun/web-pack@latest`. (Optionally add
   it as a dev dependency to pin a version.)

3. **A Board on the same LAN as your computer.** Board Connect discovers Boards
   via mDNS; your machine and the Board must be on the same subnet (Wi-Fi client
   isolation / a separate VLAN will hide it — fall back to the IP, below).

4. **Auth-gated downloads from the dev portal** (<https://dev.board.fun/>) — only
   needed for the *full* SDK experience, **not** for this finger-playable deploy:
   - `@board.fun/web-sdk` (now **public** on npm too) for typed APIs / save games.
   - **Piece Set `model.tflite`** for physical-piece (Glyph) recognition. The
     model is obtained out of band from the portal; `web-pack` never fetches it.

---

## Step 1 — Find / pair your Board

```bash
board-connect ls                 # discover Boards on the LAN
board-connect status             # readiness of the resolved Board
board-connect pair <board-ip>    # authorize this machine; then tap APPROVE on the Board
board-connect use  <board-ip>    # (optional) pin a default so later cmds need no --board
```

- `pair` stores a bearer token at `~/.config/board-connect/tokens.json` and saves
  that Board as the default. You only pair once.
- **If `board-connect ls` shows "No Boards found"** (mDNS blocked / different
  subnet), target the Board explicitly by IP everywhere with `-b/--board <ip>` or
  the `BOARD_HOST` env var. Find the IP from the Board's on-screen
  Settings/Developer panel or your router's DHCP client list.

## Step 2 — Build

```bash
cd web
npm ci
npm run build        # -> web/dist  (tsc --noEmit && vite build)
```

## Step 3 — Package into a `.webapp.zip`

```bash
cd web
npm run pack:board   # -> ../Builds/Board/trafalgar.webapp.zip
```

This runs:

```bash
npx --yes @board.fun/web-pack@latest dist \
  --package-id com.defaultcompany.trafalgarweb \
  --app-id 40d89417-f8f1-47c4-9899-4254a976ef7b \
  --name "Trafalgar — Age of Sail" \
  --no-model \
  --sdk-version 1.0.0-beta.2 \
  -o ../Builds/Board/trafalgar.webapp.zip
```

Notes on the flags (these mirror the device's install gate, which `web-pack`
runs locally so a bad bundle fails on your machine):

- **`--sdk-version`** is required unless `@board.fun/web-sdk` is installed in
  `web/` (then it's auto-detected — drop the flag). We stamp `1.0.0-beta.2` to
  match the current toolchain.
- **`--no-model`** records `model: null`. Swap for `--model <file>` (relative to
  `dist/`) once you bundle a Piece Set model — see below.
- **SDK marker requirement.** The device rejects bundles that don't reference a
  Board SDK bridge global. The app's SDK loader
  ([`web/src/board/sdk.ts`](web/src/board/sdk.ts)) detects `window.BoardSDK` /
  `window.boardTouch` / `window.__board` / `window.Harness`, which lands the
  required marker in `dist`. If you ever strip that code, packaging will fail with
  *"no Board SDK reference found"*.
- The output zip must live **outside** `dist/` (else it would pack itself) — we
  emit to `Builds/Board/`.

## Step 4 — Install + launch

```bash
# with a paired/default Board:
board-connect install /ABS/PATH/Builds/Board/trafalgar.webapp.zip --launch

# or target a specific Board by IP (no pairing/discovery needed for a quick test):
board-connect install /ABS/PATH/Builds/Board/trafalgar.webapp.zip --launch --board <board-ip>

# observe the running app:
board-connect logs <appId|--board ...>      # appId: 40d89417-f8f1-47c4-9899-4254a976ef7b
board-connect screenshot --out shot.png
```

Or do build+package+install+launch in one shot:

```bash
BOARD_HOST=<board-ip> scripts/deploy_board_web.sh --launch
```

Useful `board-connect` commands: `apps` (list installed dev apps), `stop <id>`,
`remove <id>`, `cleanup`, `open` (Board web UI in your browser).

---

## Piece / Glyph input on device

This bundle ships with **`model: null`**, so on the Board it runs with **finger
touch only** (the canvas pointer-event fallback in
[`web/src/board/input.ts`](web/src/board/input.ts) handles touches). The game is
playable, but physical **Pieces are not recognized** without a Piece Set model.

To enable physical-piece (Glyph) input:

1. Download the chosen Piece Set's **`model.tflite`** from the dev portal
   (<https://dev.board.fun/>).
2. Place it in `web/public/` (Vite copies `public/` to `dist/`) so it lands at
   `dist/model.tflite`.
3. Package with the model instead of `--no-model`:

   ```bash
   npx --yes @board.fun/web-pack@latest dist \
     --package-id com.defaultcompany.trafalgarweb \
     --app-id 40d89417-f8f1-47c4-9899-4254a976ef7b \
     --name "Trafalgar — Age of Sail" \
     --model model.tflite \
     --sdk-version 1.0.0-beta.2 \
     -o ../Builds/Board/trafalgar.webapp.zip
   ```

4. To actually *use* Glyph contacts (position + orientation + `glyphId`) in
   gameplay, wire the Web SDK input through the Baton-of-Command adapter — see
   [`docs/baton-of-command-integration.md`](docs/baton-of-command-integration.md).

Which Piece Set to download (the "robots from the space game"): see the
integration doc — the lead candidate is **Board Arcade** (the Retro Arcade
Collection's spaceship + robot Pieces).

---

## Contact-model quick reference (Web SDK)

From <https://docs.dev.board.fun/guides/touch-input>. The Board panel is
**1920×1080**; the Web SDK delivers contacts via a per-frame callback:

```js
import { Board, BoardContactType } from "@board.fun/web-sdk";
if (Board.isOnDevice) {
  Board.input.subscribe((contacts) => {
    for (const c of contacts) {
      // c.contactId, c.type (Finger|Glyph), c.x, c.y, c.orientation, c.glyphId, c.phase, c.isTouched
    }
  });
}
```

| Field | Web convention |
|---|---|
| `x` / `y` | display pixels, **origin top-left, Y down** (matches canvas/DOM) |
| `orientation` | **degrees** (Pieces only) — note: differs from Unity's radians |
| `glyphId` | which Piece in the set; **`0` = finger, `1+` = Piece** |
| `type` | `BoardContactType.Finger` / `BoardContactType.Glyph` |
| `phase` | `Began` / `Moved` / `Stationary` / `Ended` / `Canceled` |
| `isTouched` | whether a hand is on the Piece (Pieces only) |

Always guard SDK calls with `Board.isOnDevice` so the browser/mouse fallback
remains the path off-device.
