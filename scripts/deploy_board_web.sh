#!/usr/bin/env bash
set -euo pipefail

# Deploy the Trafalgar web game to Board (board.fun) hardware over the network.
#
# This is the HARDWARE deploy path and is additive: it does NOT touch the Vercel
# browser CD (see DEPLOYMENT.md) or the Android/bdb path (scripts/build_android.sh).
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
#    web-pack turns a built web-app directory into an installable .webapp.zip.
#    The exact flag surface is auth-gated; confirm against the package README and
#    override WEB_PACK_CMD if your version differs.
WEB_PACK_CMD="${WEB_PACK_CMD:-npx @board.fun/web-pack}"
log "Packaging dist/ into a .webapp.zip ($WEB_PACK_CMD)."
(
    cd "$WEB_DIR"
    # Common form: web-pack <input-dir>; produces a .webapp.zip alongside dist.
    # shellcheck disable=SC2086
    $WEB_PACK_CMD "$DIST_DIR"
)

# Locate the produced bundle.
ZIP_PATH="${WEBAPP_ZIP:-}"
if [[ -z "$ZIP_PATH" ]]; then
    ZIP_PATH="$(ls -t "$DIST_DIR"/*.webapp.zip "$WEB_DIR"/*.webapp.zip 2>/dev/null | head -n1 || true)"
fi
[[ -n "$ZIP_PATH" && -f "$ZIP_PATH" ]] || fail "No .webapp.zip produced. Set WEBAPP_ZIP=/path/to/bundle.webapp.zip or check WEB_PACK_CMD."
log "Bundle ready: $ZIP_PATH"

# 3. Install (and optionally launch) on the Board over the network.
if [[ "$install_after_build" == true ]]; then
    bc_bin="$(resolve_board_connect)" || fail "board-connect not found. Install it (see BOARD_HARDWARE.md) or set BOARD_CONNECT_BIN."
    log "Checking Board status."
    "$bc_bin" status || fail "board-connect could not reach a Board. Run 'board-connect pair' first, or set BOARD_HOST."

    install_args=(install "$ZIP_PATH")
    [[ "$launch_after_install" == true ]] && install_args+=(--launch)
    log "Installing on Board: ${install_args[*]}"
    "$bc_bin" "${install_args[@]}"
    log "Done."
else
    log "Skipping install (no --install). Deploy manually with:"
    log "  board-connect install \"$ZIP_PATH\" --launch"
fi
