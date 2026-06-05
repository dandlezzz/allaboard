// On-screen error overlay — diagnostics for the Board, where we have no console
// or log-streaming access. The global error boundary and the loop try/catch
// (see main.ts) funnel caught errors here so they're VISIBLE on the hardware:
// the last few errors render as fixed red monospace text at the bottom of the
// screen. The overlay is best-effort and must NEVER throw itself.

const MAX_LINES = 3;
let host: HTMLDivElement | null = null;
const lines: string[] = [];

function ensureHost(): HTMLDivElement | null {
  if (typeof document === "undefined") return null;
  if (host && host.isConnected) return host;
  try {
    const el = document.createElement("div");
    el.id = "debug-overlay";
    el.style.cssText = [
      "position:fixed",
      "left:8px",
      "right:8px",
      "bottom:8px",
      "z-index:99999",
      "font:12px/1.35 ui-monospace,Menlo,Consolas,monospace",
      "color:#ff6b6b",
      "background:rgba(10,6,6,0.82)",
      "border:1px solid rgba(255,80,80,0.55)",
      "border-radius:6px",
      "padding:6px 9px",
      "max-height:40vh",
      "overflow:auto",
      "white-space:pre-wrap",
      "pointer-events:none", // never intercept taps meant for the game
      "text-shadow:0 1px 2px #000",
    ].join(";");
    (document.body ?? document.documentElement).appendChild(el);
    host = el;
    return el;
  } catch {
    return null;
  }
}

/**
 * Records a caught error and shows it (message + first stack line) on the
 * bottom-of-screen overlay, keeping the last few. Fully guarded — a failure to
 * render diagnostics must never itself crash anything.
 */
export function showError(label: string, err: unknown): void {
  try {
    const e = err as { message?: string; stack?: string } | undefined;
    const msg = (e && (e.message ?? String(e))) || String(err);
    const stackHead = e?.stack
      ? String(e.stack).split("\n").slice(0, 2).join(" ⏎ ").slice(0, 280)
      : "";
    let time = "";
    try {
      time = new Date().toLocaleTimeString();
    } catch {
      /* ignore */
    }
    lines.push(`[${time}] ${label}: ${msg}${stackHead ? "\n  " + stackHead : ""}`);
    while (lines.length > MAX_LINES) lines.shift();
    const el = ensureHost();
    if (el) el.textContent = lines.join("\n");
    try {
      console.error(`[overlay] ${label}:`, err);
    } catch {
      /* ignore */
    }
  } catch {
    /* the diagnostics overlay must never throw */
  }
}
