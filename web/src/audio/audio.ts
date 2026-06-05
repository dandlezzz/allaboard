// Tiny Web Audio sound engine — see docs/sound-effects-plan.md.
//
// One shared AudioContext (created suspended), each clip decoded ONCE into a
// cached AudioBuffer, and cheap fire-and-forget one-shot AudioBufferSourceNodes
// played through a Master → SFX gain graph. A voice cap + per-sound throttle
// keep a 32-gun broadside from stacking dozens of sources, and slight pitch/gain
// jitter keeps repeats from sounding robotic.
//
// Everything is GUARDED: if Web Audio is unavailable or a clip fails to load,
// every method silently no-ops — the game just runs without sound, never throws.
// Not gated on Board.isOnDevice (Web Audio works in the browser and the Board
// WebView alike); the gesture `unlock()` handles the autoplay policy.

export type SoundName = "cannon" | "impact" | "splash";

interface SoundDef {
  /** Candidate URLs tried in order (OGG first, MP3 fallback). Document-relative
   *  so they resolve under the dev server, the packed bundle and a file:// view. */
  urls: string[];
  /** Base gain (0..1). */
  volume: number;
  /** Minimum ms between plays of THIS sound (coalesces a volley's many balls). */
  throttleMs: number;
}

const SOUNDS: Record<SoundName, SoundDef> = {
  cannon: { urls: ["audio/cannon.ogg", "audio/cannon.mp3"], volume: 0.9, throttleMs: 40 },
  impact: { urls: ["audio/impact.ogg", "audio/impact.mp3"], volume: 0.7, throttleMs: 55 },
  splash: { urls: ["audio/splash.ogg", "audio/splash.mp3"], volume: 0.4, throttleMs: 70 },
};

/** Hard cap on simultaneously-playing one-shots (drops extras past it). */
const MAX_VOICES = 16;

type AudioCtor = typeof AudioContext;

class AudioEngine {
  private ctx: AudioContext | null = null;
  private master: GainNode | null = null;
  private sfx: GainNode | null = null;
  private readonly buffers = new Map<SoundName, AudioBuffer>();
  private readonly lastPlayed = new Map<SoundName, number>();
  private activeVoices = 0;
  private masterVol = 1.0;
  private sfxVol = 0.9;

  /** Lazily creates the context + gain graph (Master → SFX → destination). The
   *  context starts suspended; decoding is allowed before the unlock gesture. */
  init(): void {
    if (this.ctx) return;
    try {
      const w = window as unknown as { webkitAudioContext?: AudioCtor };
      const Ctor: AudioCtor | undefined =
        typeof AudioContext !== "undefined" ? AudioContext : w.webkitAudioContext;
      if (!Ctor) return;
      const ctx = new Ctor();
      const master = ctx.createGain();
      master.gain.value = this.masterVol;
      const sfx = ctx.createGain();
      sfx.gain.value = this.sfxVol;
      sfx.connect(master);
      master.connect(ctx.destination);
      this.ctx = ctx;
      this.master = master;
      this.sfx = sfx;
    } catch {
      this.ctx = null; // no Web Audio → silent
    }
  }

  /** Fetches + decodes every clip into a cached buffer. Guarded per clip: a
   *  failed clip just stays absent (that sound is silent), never throws. */
  async preload(): Promise<void> {
    this.init();
    const ctx = this.ctx;
    if (!ctx) return;
    await Promise.all(
      (Object.keys(SOUNDS) as SoundName[]).map(async (name) => {
        for (const url of SOUNDS[name].urls) {
          try {
            const res = await fetch(url);
            if (!res.ok) continue;
            const bytes = await res.arrayBuffer();
            const buffer = await ctx.decodeAudioData(bytes);
            this.buffers.set(name, buffer);
            return;
          } catch {
            // try the next candidate URL (e.g. mp3 fallback)
          }
        }
        console.warn(`[audio] could not load sound: ${name}`);
      }),
    );
  }

  /**
   * Resumes the context from a user gesture (idempotent; safe to call often).
   * Fully guarded: resumes from ANY non-running, non-closed state (suspended, or
   * the "interrupted" state some engines enter when the OS audio focus / system
   * volume changes), and swallows both synchronous throws and promise rejections
   * so a system audio change can never bubble an error up to crash the app.
   */
  unlock(): void {
    this.init();
    const ctx = this.ctx;
    if (!ctx) return;
    try {
      if (ctx.state !== "running" && ctx.state !== "closed") {
        const p = ctx.resume();
        if (p && typeof p.catch === "function") {
          p.catch(() => {
            /* stays silent until a later gesture succeeds */
          });
        }
      }
    } catch {
      /* resume threw synchronously (closed/interrupted ctx) → ignore, stay silent */
    }
  }

  /**
   * Plays a one-shot. No-ops if the context isn't running yet (pre-unlock), the
   * buffer isn't loaded, the per-sound throttle hasn't elapsed, or the voice cap
   * is hit. Applies ±8% pitch and ±10% gain jitter so repeats vary.
   */
  play(name: SoundName, opts?: { volume?: number; throttleMs?: number }): void {
    const ctx = this.ctx;
    const sfx = this.sfx;
    if (!ctx || !sfx || ctx.state !== "running") return;
    const buffer = this.buffers.get(name);
    if (!buffer) return;

    const def = SOUNDS[name];
    const nowMs = ctx.currentTime * 1000;
    const throttle = opts?.throttleMs ?? def.throttleMs;
    if (nowMs - (this.lastPlayed.get(name) ?? -1e9) < throttle) return;
    if (this.activeVoices >= MAX_VOICES) return;
    this.lastPlayed.set(name, nowMs);

    try {
      const src = ctx.createBufferSource();
      src.buffer = buffer;
      src.playbackRate.value = 1 + (Math.random() - 0.5) * 0.16; // ±8% pitch
      const gain = ctx.createGain();
      gain.gain.value = (opts?.volume ?? def.volume) * (0.9 + Math.random() * 0.2); // ±10% gain
      src.connect(gain);
      gain.connect(sfx);
      this.activeVoices++;
      src.onended = () => {
        this.activeVoices = Math.max(0, this.activeVoices - 1);
        try {
          src.disconnect();
          gain.disconnect();
        } catch {
          /* already torn down */
        }
      };
      src.start();
    } catch {
      this.activeVoices = Math.max(0, this.activeVoices - 1);
    }
  }

  /** Master volume 0..1 (future "Master" pause slider). Guarded: setting a gain
   *  on a node of a closed/interrupted context must never throw out to the app. */
  setMasterVolume(v: number): void {
    this.masterVol = clamp01(v);
    try {
      if (this.master) this.master.gain.value = this.masterVol;
    } catch {
      /* ignore */
    }
  }

  /** SFX-bus volume 0..1 (future "Cannons / SFX" pause slider). Guarded as above. */
  setSfxVolume(v: number): void {
    this.sfxVol = clamp01(v);
    try {
      if (this.sfx) this.sfx.gain.value = this.sfxVol;
    } catch {
      /* ignore */
    }
  }
}

function clamp01(x: number): number {
  return x < 0 ? 0 : x > 1 ? 1 : x;
}

/** The shared sound engine singleton. */
export const audio = new AudioEngine();
