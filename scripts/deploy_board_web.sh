#!/usr/bin/env bash
set -euo pipefail

# Deploy the Trafalgar web game to Board (board.fun) hardware over the network.
#
# This is the HARDWARE deploy path and is additive: it does NOT touch the Vercel
# browser CD (see DEPLOYMENT.md).
#
# Pipeline:
#   1. npm run build        -> web/dist            (Vite static build)
#   2. @board.fun/web-pack  -> web/dist/<name>.webapp.zip
#   3. board-connect install <zip> [--launch]      (LAN HTTP, port 8843)
#
# Prerequisites (see BOARD_HARDWARE.md for how to obtain each):
#   - board-connect CLI:  curl -fsSL https://dev.board.fun/connect/install | sh
#                         (installs to ~/.local/bin/board-connect)
#   - @board.fun/web-pack: a dev dependency of web/ (auth-gated; from the dev portal)
#   - A paired Board:     board-connect pair   (tap Approve on the device, once)
#
# Environment overrides:
#   BOARD_CONNECT_BIN   path to the board-connect binary (else PATH / ~/.local/bin)
#   WEB_PACK_CMD        the web-pack invocation (default: "npx @board.fun/web-pack")
#   WEBAPP_ZIP          explicit output bundle path (default: autodetected in dist/)
#   BOARD_HOST          target Board ip / ip:port (else board-connect's saved default)

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
WEB_DIR="$PROJECT_ROOT/web"
DIST_DIR="$WEB_DIR/dist"

install_after_build=false
launch_after_install=false

usage() {
    cat <<'EOF'
Usage: scripts/deploy_board_web.sh [--install] [--launch]

Builds web/, packages it into a .webapp.zip with @board.fun/web-pack, and
(optionally) installs it on a paired Board with board-connect.

Options:
  --install  Install the packaged .webapp.zip on the Board with board-connect.
  --launch   Install and then bring the app to the foreground. Implies --install.
  --help     Show this help text.

Without --install this only builds and packages the bundle (useful in CI or to
inspect the .webapp.zip before deploying).
EOF
}

log()  { printf '[board-web] %s\n' "$1"; }
fail() { printf '[board-web] %s\n' "$1" >&2; exit 1; }

resolve_board_connect() {
    if [[ -n "${BOARD_CONNECT_BIN:-}" ]]; then
        [[ -x "$BOARD_CONNECT_BIN" ]] && printf '%s\n' "$BOARD_CONNECT_BIN" && return 0
        return 1
    fi
    local found
    found="$(command -v board-connect 2>/dev/null || true)"
    if [[ -n "$found" && -x "$found" ]]; then
        printf '%s\n' "$found"
        return 0
    fi
    local candidate="$HOME/.local/bin/board-connect"
    if [[ -x "$candidate" ]]; then
        printf '%s\n' "$candidate"
        return 0
    fi
    return 1
}

while [[ $# -gt 0 ]]; do
    case "$1" in
        --install) install_after_build=true ;;
        --launch)  install_after_build=true; launch_after_install=true ;;
        --help)    usage; exit 0 ;;
        *)         usage >&2; fail "Unknown argument: $1" ;;
    esac
    shift
done

# 1. Build the Vite static app.
log "Building web app."
(
    cd "$WEB_DIR"
    if [[ ! -d node_modules ]]; then
        log "Installing web dependencies."
        if [[ -f package-lock.json ]]; then npm ci; else npm install; fi
    fi
    npm run build
)
[[ -d "$DIST_DIR" ]] || fail "Build output not found at $DIST_DIR"

# 2. Package dist/ into a .webapp.zip with @board.fun/web-pack.
#    web-pack turns a built web-app dir into an installable .webapp.zip and runs
#    the same gate the device enforces (it requires a Board SDK marker in the
#    bundle, a reverse-domain packageId, a UUID appId, and a semver sdkVersion).
#    The output zip must NOT live inside <dist-dir>, so we emit to Builds/Board.
APP_PACKAGE_ID="${APP_PACKAGE_ID:-com.defaultcompany.trafalgarweb}"
APP_NAME="${APP_NAME:-Trafalgar — Age of Sail}"
APP_ID="${APP_ID:-40d89417-f8f1-47c4-9899-4254a976ef7b}"
SDK_VERSION="${SDK_VERSION:-1.0.0-beta.2}"
# Piece Set model, bundle-relative to dist/ (Vite copies web/public/ → dist/).
MODEL_FILE="${MODEL_FILE:-model.tflite}"
OUT_DIR="$PROJECT_ROOT/Builds/Board"
ZIP_PATH="${WEBAPP_ZIP:-$OUT_DIR/trafalgar.webapp.zip}"
WEB_PACK_CMD="${WEB_PACK_CMD:-npx --yes @board.fun/web-pack@latest}"
mkdir -p "$OUT_DIR"
[[ -f "$DIST_DIR/$MODEL_FILE" ]] || fail "Piece Set model not found at $DIST_DIR/$MODEL_FILE (expected web/public/$MODEL_FILE to be copied into dist by Vite)."
log "Packaging dist/ into $ZIP_PATH ($WEB_PACK_CMD)."
(
    cd "$WEB_DIR"
    # If a real @board.fun/web-sdk is installed, --sdk-version is auto-detected and
    # can be dropped; we pass it so packaging works without the SDK installed.
    # The Piece Set model (web/public/model.tflite → dist/model.tflite via Vite)
    # is bundled with --model so the device recognises physical Pieces (Glyphs);
    # web-pack reads it at pack time and the device uses it for on-device Piece
    # detection (it is NOT downloaded at runtime). The path is bundle-relative.
    # shellcheck disable=SC2086
    $WEB_PACK_CMD dist \
        --package-id "$APP_PACKAGE_ID" \
        --app-id "$APP_ID" \
        --name "$APP_NAME" \
        --model "$MODEL_FILE" \
        --sdk-version "$SDK_VERSION" \
        -o "$ZIP_PATH"
)
[[ -f "$ZIP_PATH" ]] || fail "No .webapp.zip produced at $ZIP_PATH."
log "Bundle ready: $ZIP_PATH"

# 3. Install (and optionally launch) on the Board over the network.
#    board-connect resolves the target Board from: --board flag (BOARD_HOST here),
#    BOARD_HOST env, the saved default (from `pair`/`use`), or LAN discovery.
if [[ "$install_after_build" == true ]]; then
    bc_bin="$(resolve_board_connect)" || fail "board-connect not found. Install it (see BOARD_HARDWARE.md) or set BOARD_CONNECT_BIN."

    board_flag=()
    [[ -n "${BOARD_HOST:-}" ]] && board_flag=(--board "$BOARD_HOST")

    log "Checking Board status."
    "$bc_bin" "${board_flag[@]}" status || fail "board-connect could not reach a Board. Pair first: 'board-connect pair <ip>' (tap Approve on the device), or set BOARD_HOST=<ip>."

    install_args=(install "$ZIP_PATH" "${board_flag[@]}")
    [[ "$launch_after_install" == true ]] && install_args+=(--launch)
    log "Installing on Board: ${install_args[*]}"
    "$bc_bin" "${install_args[@]}"
    log "Done."
else
    log "Skipping install (no --install). Deploy manually with:"
    log "  board-connect install \"$ZIP_PATH\" --launch        # add --board <ip> if not discovered"
fi
