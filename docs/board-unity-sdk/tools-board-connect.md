> Source: https://docs.dev.board.fun/tools/board-connect — fetched 2026-06-04T18:38 (UTC-7)

# Board Connect

Board Connect is the device-side developer and agent control plane for Board. It is a small HTTP API served on port `8843` by the Board Developer Bridge service running on the device, and it replaces the USB-serial workflow of `bdb`. Because everything is plain HTTP over your LAN (no USB, no ADB), Board Connect works the same way from macOS, Linux, and Windows. You discover a Board on your network, pair once, and then install, launch, and inspect apps over the wire.

The primary way to drive Board Connect is the `board-connect` CLI. This page covers installing the CLI, pointing it at a Board, pairing, and deploying an app in one command. For the full HTTP surface, see the Board Connect API reference.

Board Connect vs `bdb`: `bdb` (the Board Developer Bridge command-line tool) talks to a Board over USB serial. Board Connect does the same jobs (install, launch, logs, app management) over HTTP, so it works without a cable and on production devices on your network. New tooling targets Board Connect; `bdb` remains documented in the bdb reference.

## Install the CLI

macOS and Linux:

```bash
curl -fsSL https://dev.board.fun/connect/install | sh
```

Windows (PowerShell):

```powershell
irm https://dev.board.fun/connect/install.ps1 | iex
```

The installer drops a single binary at `~/.local/bin/board-connect` (on Windows, `%USERPROFILE%\.local\bin\board-connect.exe`). Add that directory to your `PATH` if it isn't already.

macOS note: because you install via `curl`, Gatekeeper does not quarantine the binary. If you ever obtain the binary another way, clear the quarantine attribute first:

```bash
xattr -d com.apple.quarantine ~/.local/bin/board-connect
```

Verify the install (`--version` prints the CLI's own version):

```bash
board-connect --version
```

## Target a Board

Most commands do not take a Board address. The CLI resolves which Board to talk to, in order, from:

1. the `-b, --board <addr>` flag,
2. the `BOARD_HOST` environment variable,
3. the saved default Board (set by `pair` or `use`), or
4. discovery (a single Board on your network is used automatically; multiple Boards prompt you when running interactively).

So the usual pattern is to pin a default once:

```bash
board-connect use 10.0.0.42   # set the default Board (accepts ip, ip:port, or a name/serial)
board-connect status          # no --board needed from here on
```

`<addr>` is an IP or `ip:port` (the default port is `8843`). Run `board-connect ls` to discover Boards on your network first if you do not know the address.

## Pair with a Board

Pairing authorizes your machine to manage a Board and issues a bearer token that every later command reuses. You only pair a given Board once.

```bash
board-connect pair               # resolve a Board, then tap Approve on the device
board-connect pair 10.0.0.42     # pair a specific Board by address
board-connect pair --code 123456 # pair using the code shown on the device
```

When you run `pair`, the Board shows an approval prompt on screen and the command waits. Tap Approve on the device and the command completes, storing the token locally and saving that Board as your default.

`pair` is idempotent: if the saved token still authenticates, it is a no-op that just refreshes the default. Pass `--force` to mint a fresh token.

The token is persisted under `~/.config/board-connect/tokens.json`, and the default Board alongside it in `config.json`. You can override that directory with `XDG_CONFIG_HOME`, or supply a token out of band with the `BOARD_TOKEN` environment variable.

## Deploy in one command

Once you have paired (or set a default), install an app and launch it in a single step, with no address on the command:

```bash
board-connect install ./dist/MyGame.webapp.zip --launch
```

This works for both native Android apps and web apps. Pass an `.apk` or a `.webapp.zip` bundle and the CLI detects the bundle type from its contents.

```
$ board-connect install ./dist/MyGame.webapp.zip --launch
Installed MyGame (appId 7f3c1e90-2b4a-4d11-9c0e-8a51d2f6b4aa)
Launched
```

## Common commands

App commands take a single `<app>`, which is either an APK package name or a web-app `appId`; the CLI resolves which it is. None of these take a Board address.

| Command | Description |
| --- | --- |
| `board-connect ls` | Discover Boards on your network (name / model / serial / ipv4 / transport) |
| `board-connect use <addr>` | Set the default Board (address, or name/serial via discovery) |
| `board-connect open [board]` | Open a Board's web UI in your browser (no argument discovers) |
| `board-connect pair` | Pair with a Board (tap Approve on the device); `--code`, `--force` |
| `board-connect status` | Check device readiness |
| `board-connect version` | Show the Board OS version (distinct from the CLI's `--version`) |
| `board-connect capabilities` | Show protocol version and feature tags (alias: `caps`) |
| `board-connect apps` | List installed dev apps (APKs and web apps) |
| `board-connect install <file>` | Install an `.apk` or `.webapp.zip` (type auto-detected); add `--launch` |
| `board-connect launch <app>` | Launch an installed app by id |
| `board-connect stop <app>` | Stop a running app |
| `board-connect remove <app>` | Uninstall an app by id (confirms unless `--yes`) |
| `board-connect cleanup` | Uninstall all dev-managed apps (`--yes` to skip the confirm) |
| `board-connect logs <app>` | Dump recent logs (`--tag`, `--level`); add `--follow` to stream live until Ctrl-C |
| `board-connect screenshot` | Capture the screen (`--out file.png`) |

Read commands accept `--json` for machine-readable output. The global flags `-y, --yes` skip destructive-action confirmations and `-q, --quiet` suppresses informational notices. `--follow` streams logs live for APKs (web apps fall back to a one-shot dump).

## Which surface when

- board-connect CLI (primary): the supported path for terminals and coding agents. Discovers Boards, pairs, and drives install, launch, logs, and app management from the command line. Start here.
- Board Connect web UI: the human-facing graphical interface served by the Board itself at `http://<board>:8843/`. Open it in a browser to install and manage apps without a terminal.
- web-pack (`@board.fun/web-pack`): a packaging helper that turns a built web-app directory into an installable `.webapp.zip`. Run it before `install` when you are deploying a web app. It packages bundles; it does not talk to the device.

For terminals and agents, the `board-connect` CLI is the supported primary path.

## API reference

Every command above is a thin wrapper over the device HTTP API. To see the full surface (discovery, pairing flows, app lifecycle, screenshots, logs, and paired-client management), browse the Board Connect API reference.
