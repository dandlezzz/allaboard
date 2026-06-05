---
name: deploy-board
description: Deploy the Trafalgar — Age of Sail web game to Daniel's physical Board (board.fun) hardware over the LAN. Use whenever the user says "deploy to the board", "push to the board", "ship it to my board", "install on the board", "send it to the board", "run it on the board", "board deploy", or any phrasing that means "build the web app and install it on the Board device". Distinct from the Vercel browser CD and the Android/bdb path — this skill is only for the Board hardware deploy.
---

# Deploy to Board hardware

Builds `web/`, packages it into a `.webapp.zip` with `@board.fun/web-pack`, and installs + launches it on a paired Board over the LAN via `board-connect`.

This is the **hardware** deploy path. It does **not** touch the Vercel browser
CD (see `DEPLOYMENT.md`).

If the user asks to "deploy" without qualification in this repo, prefer this skill unless they explicitly say "Vercel", "web", "browser", "Android", or "apk".

## The one command

From repo root:

```bash
scripts/deploy_board_web.sh --launch
```

It does the whole pipeline:
1. `npm ci` + `npm run build` in `web/` → `web/dist`
2. `npx @board.fun/web-pack@latest` → `Builds/Board/trafalgar.webapp.zip`
3. `board-connect status` → `board-connect install <zip> --launch`

It honors:
- `BOARD_HOST=<ip>` — target a specific Board (skips discovery / saved default)
- `BOARD_CONNECT_BIN=<path>` — override the CLI binary

## Known facts about Daniel's setup

- **Board IP:** `192.168.4.85` (saved default; `board-connect status` resolves it). mDNS discovery (`board-connect ls`) is often blocked on his network, so prefer `BOARD_HOST=192.168.4.85` if anything looks flaky.
- **`board-connect`** is installed at `~/.local/bin/board-connect`.
- **Pairing token** lives at `~/.config/board-connect/tokens.json` — already paired in normal operation. If pairing was revoked, `board-connect pair 192.168.4.85` needs Daniel to **tap Approve on the device** (takes up to ~90s).
- The app's stable `appId` is `40d89417-f8f1-47c4-9899-4254a976ef7b` — use it for `logs`, `screenshot`, `stop`, `remove`.

## Workflow

1. Run the deploy script with `BOARD_HOST` set to the known IP, in the foreground, with a generous timeout (build + package + install can take ~60s):

   ```bash
   BOARD_HOST=192.168.4.85 scripts/deploy_board_web.sh --launch
   ```

2. **If it fails at `board-connect status` with "not paired"** — pair first, then re-run:

   ```bash
   board-connect pair 192.168.4.85    # tell Daniel to tap APPROVE on the Board
   BOARD_HOST=192.168.4.85 scripts/deploy_board_web.sh --launch
   ```

3. **If it fails with "no Boards found"** and there's no saved default — `board-connect status` will still report the saved IP if one exists. Tell Daniel "I don't have an IP — what's your Board's IP?" and use whatever he gives via `BOARD_HOST=<ip>`. Don't guess.

4. **If the build fails** — that's a real bug in `web/`; surface the error, don't paper over it.

5. **If install succeeds but the game isn't visible on the Board** — the most likely cause is the user is still on the home screen because something denied the launch. Try:
   ```bash
   board-connect logs 40d89417-f8f1-47c4-9899-4254a976ef7b --board 192.168.4.85
   board-connect screenshot --out /tmp/board.png --board 192.168.4.85
   ```

## After deploying — ALWAYS commit (required)

**Immediately after any successful Board deploy, commit everything.** This is mandatory — a deploy is not "done" until the repo is committed, so the on-Board build always corresponds to a commit. Run:

```bash
git add -A && git commit -m "Deploy to Board: <short description of what changed>"
```

(The bundled `Builds/Board/*.webapp.zip` is gitignored, so it won't be staged — that's fine.) Do this yourself; don't ask first. Then end with one short sentence confirming the deploy + commit and giving Daniel the handful of commands he might want next (logs / screenshot / redeploy). Don't dump verbose script output — the user can scroll if they want it.

```bash
# Tail app logs
board-connect logs 40d89417-f8f1-47c4-9899-4254a976ef7b --board 192.168.4.85

# Grab a screenshot from the Board
board-connect screenshot --out /tmp/board.png --board 192.168.4.85

# Redeploy after edits
BOARD_HOST=192.168.4.85 scripts/deploy_board_web.sh --launch
```

## Gotchas

- The script's `--install` flag deploys without bringing the app to the foreground; `--launch` implies `--install` AND foregrounds it. Daniel almost always wants `--launch`.
- The bundled `.webapp.zip` lands in `Builds/Board/trafalgar.webapp.zip` (gitignored). Don't try to commit it.
- Currently packed with `--no-model`, so physical Pieces (Glyphs) are NOT recognized — finger touch only on-device. To enable Pieces, drop a Piece Set `model.tflite` (from the auth-gated dev portal at https://dev.board.fun/) into `web/public/` and switch `web/package.json`'s `pack:board` from `--no-model` to `--model model.tflite`.
- Don't try `board-connect ls` to "verify" the Board afterward — mDNS discovery typically returns "No Boards found" on this network even when the device is fully reachable by IP. `board-connect status` (which uses the saved default / `BOARD_HOST`) is the right check.
- The `appId` (`40d89417-f8f1-47c4-9899-4254a976ef7b`) is stable across reinstalls because the device namespaces saves by it. Don't regenerate it.

## Reference

- `scripts/deploy_board_web.sh` — the script itself; read it for the full pipeline
- `BOARD_HARDWARE.md` — the long-form guide (prerequisites, troubleshooting, piece model setup)
- `docs/baton-of-command-integration.md` — how a physical Piece drives the Baton of Command in-game
- Upstream docs: https://docs.dev.board.fun/tools/board-connect
