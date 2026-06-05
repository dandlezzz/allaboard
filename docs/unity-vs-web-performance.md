# Unity SDK vs Web SDK on Board: Performance Comparison

> Research doc for the Trafalgar — Age of Sail port. Compares building a Board game
> with the **Unity SDK** (native Android app) versus the **Web SDK** (web app running
> in Board's built-in browser). Read-only research; cites the official Board docs.
>
> Captured 2026-06-04 against the locally mirrored Unity SDK docs (`docs/unity-sdk/`)
> plus live Web SDK pages. SDK versions: Unity **v3.3.0**, Web SDK packaged as
> `1.0.0-beta.2` in this repo (`web/package.json`).

## How to read this doc

Every claim is tagged:

- **[DOC]** — stated directly in a Board doc page (path + source URL given).
- **[INF]** — reasoned inference from general engine/platform knowledge. The docs do
  **not** directly state it, so treat it as engineering judgment, not Board's word.

The single most important documented fact up front: **Board's docs are explicit that
the touch/Piece recognition pipeline is identical across all three SDKs and runs on a
dedicated NPU, so the headline "ML keeps your CPU/GPU free" benefit applies *equally* to
Unity and Web.** The performance gap between the two is therefore almost entirely about
the *rendering/runtime* layer, not the input layer.

---

## Bottom line / recommendation

| Situation | Recommended SDK | Why |
| --- | --- | --- |
| 3D scenes, heavy shaders, large particle counts, big asset sets, sustained high draw calls | **Unity** | Native IL2CPP + full Unity render pipeline + direct GPU access ([INF], grounded in [DOC] platform requirements) |
| 2D board games, sprite/Canvas/WebGL rendering, modest scene complexity (our Trafalgar PixiJS build) | **Web is sufficient** | Mali-G57 + WebGL handles 2D comfortably; no documented web perf ceiling hit for this class of game ([INF]) |
| Input responsiveness / many simultaneous Pieces | **Tie (no advantage either way)** | The NPU touch pipeline and contact model are identical across SDKs [DOC] |
| Fastest iteration / no APK build / smallest deploy artifact | **Web** | Ship a static web app, not an APK; `board-connect` installs it [DOC] |

**Verdict:** For a graphically heavy or 3D title, Unity wins on the rendering/runtime
ceiling — and that is the *only* axis where it has a documented structural advantage.
For a 2D game like Trafalgar, the Web SDK is performance-sufficient, and you gain faster
iteration and a smaller deploy loop. **Board's docs do not publish any head-to-head
performance numbers, a Web frame-rate target, draw-call budgets, or memory ceilings**, so
the rendering-side comparison below is necessarily inference anchored to the documented
hardware and platform constraints.

---

## Shared hardware ceiling (the same for both SDKs)

From **Hardware** — `docs/unity-sdk/learn-hardware.md` (source: <https://docs.dev.board.fun/learn/hardware>): **[DOC]**

| Component | Spec |
| --- | --- |
| Processor | MediaTek Genio 700 Octa-Core |
| CPU | 8 cores: 2× Cortex-A78 + 6× Cortex-A55 |
| GPU | Mali-G57 |
| RAM | 4 GB |
| Storage | 64 GB internal + SD slot |
| Display | 23.8", 1920×1080, **60 Hz**, capacitive touch |

This is a mid-range ARM Android tablet-class SoC. **[INF]** Both SDKs run on the same
silicon, so neither can exceed this ceiling; the question is purely how efficiently each
runtime uses it. 4 GB RAM shared across the OS, the touch service, and your app is the
tightest documented constraint.

---

## 1. Runtime & rendering

### Unity (native)
- **[DOC]** Unity apps build as native Android: **IL2CPP scripting backend, ARM64,
  Android 13 (API 33)**, Unity 2021.3+/2022.3 LTS (Unity 6 supported).
  Source: `docs/unity-sdk/getting-started-setup-reference.md`
  (<https://docs.dev.board.fun/unity/getting-started/setup-reference>).
- **[DOC]** Both the **Built-in Render Pipeline and URP are supported; HDRP is *not*
  supported on Android** and is incompatible with Board. (same page)
- **[INF]** IL2CPP compiles C# to native ARM machine code (AOT), and Unity talks to the
  Mali-G57 through its own Android GPU path. This gives the highest available CPU ceiling
  and the most direct GPU access of the two options. The docs confirm the *toolchain*
  (IL2CPP/ARM64/render pipelines) but do **not** quantify the rendering advantage.

### Web (browser/WebView)
- **[DOC]** A Web SDK app **runs inside Board's built-in browser ("Board Browser"), a
  system-managed WebView. You don't ship an APK; you ship a built web app** and
  Board Connect installs/launches it. Source:
  <https://docs.dev.board.fun/web/getting-started/setup-reference> ("Where the app runs")
  and <https://docs.dev.board.fun/web/architecture>.
- **[DOC]** The SDK is a **thin, typed TypeScript layer → platform bridge → device
  services**; it never talks to device services directly. Source:
  <https://docs.dev.board.fun/web/architecture> ("The layers").
- **[INF]** Rendering goes through the browser: HTML5 Canvas 2D or WebGL (PixiJS uses
  WebGL) on top of the Mali-G57. This adds a WebView/JS layer (JIT-compiled JS, a
  single-threaded main loop, browser compositor) between your code and the GPU, so the
  CPU and draw-call ceiling is lower than native Unity. **No Board doc quantifies this**;
  it is standard web-vs-native reasoning.

**Net:** Unity has the documented native toolchain and render-pipeline access; the Web
runtime sits behind a browser layer. The *magnitude* of the rendering gap is inferred,
not documented.

---

## 2. Frame rate

This is the one place the docs give explicit, SDK-specific performance guidance — and it
is **Unity-only**.

- **[DOC]** **Unity defaults to 30 fps on Android. Board's display is 60 Hz, so without
  changing this your game renders at half the refresh rate**, causing visible lag for
  fast-moving Pieces. Fix by setting `Application.targetFrameRate = 60` early (e.g. in a
  `GameManager.Awake()`). Source: `docs/unity-sdk/performance.md`
  (<https://docs.dev.board.fun/unity/performance>) — the page the user specifically
  flagged.
- **[DOC]** 60 fps matches the display refresh and is the recommended target; `-1`
  removes the cap but 60 is recommended on Board hardware. (same page)
- **[DOC / absent]** There is **no `/web/performance` page** — the URL 404s — and no
  documented frame-rate target or `requestAnimationFrame` guidance for the Web SDK.
  **[INF]** A WebView typically drives `requestAnimationFrame` at the display's 60 Hz by
  default, so a web app is more likely to hit 60 fps "for free" than a freshly-created
  Unity project, which ships capped at 30 until you change one line. In other words, the
  documented Unity 30→60 footgun does not have a known web equivalent.

**Net:** Unity *can* render at 60 fps but only after an explicit one-line fix the docs
call out as a common mistake. The Web side has no documented frame-rate guidance at all.

---

## 3. Touch / Piece input (the big equalizer — documented)

This is where the Board docs are strongest, and the conclusion is that **input
performance is essentially identical across SDKs.**

- **[DOC]** **The ML runs on dedicated hardware (a separate NPU). Glyph recognition and
  contact tracking happen off the main CPU/GPU, so they don't compete with your game for
  resources. Touch stays responsive no matter how busy your render loop is.** And: **"The
  OS owns the touch pipeline, not your app… Every SDK is a thin client over the same
  service, which is why all three report the same contact model."** Source:
  `docs/unity-sdk/learn-architecture.md` (<https://docs.dev.board.fun/learn/architecture>).
- **[DOC]** The Touch page reinforces it: recognition runs on a dedicated NPU enabling
  **consistent frame rates** (touch doesn't steal render cycles), **low latency**
  ("dedicated hardware responds faster than software polling"), and complex recognition
  (palm/wrist rejection, overlapping contacts). Source:
  `docs/unity-sdk/learn-touch-system.md` (<https://docs.dev.board.fun/learn/touch-system>).
- **[DOC]** **No artificial contact limit** — "Track as many fingers and Pieces as
  physically fit… you can place ~170 Board Arcade Pieces on the screen simultaneously."
  This is a platform property, not an SDK property. (same page)
- **[DOC]** Noise rejection (palm, wrist, overlap) happens **at the hardware level**, so
  every SDK receives pre-cleaned contacts and implements no rejection logic. (same page)

### How each SDK *receives* the (identical) data
- **[DOC]** **Unity polls** every frame: `BoardInput.GetActiveContacts()` in `Update()`.
  Source: `docs/unity-sdk/architecture.md` and `docs/unity-sdk/guide-touch-input.md`.
- **[DOC]** **Web subscribes** a callback that fires **once per inference frame** with
  the full current contact set: `Board.input.subscribe(...)`. Source:
  `docs/unity-sdk/guide-touch-input.md` (Web example) and `web/AGENTS.md`.
- **[DOC]** Both deliver a **full per-frame snapshot with no discrete down/up events**;
  you diff against the previous frame keyed by `contactId`. Source:
  `docs/unity-sdk/guide-piece-interaction-design.md` ("Touch and Release") and `web/AGENTS.md`.

**Net (input):** **[DOC]** The NPU offload, the contact model, the noise rejection, the
unlimited-contacts property, and the per-frame snapshot semantics are all OS-owned and
identical across SDKs. **[INF]** Polling (Unity) vs callback (Web) is a delivery-style
difference, not a throughput/latency difference — both get one snapshot per inference
frame. There is **no documented input-performance advantage for Unity over Web.**

### One nuance: input *smoothing/persistence* knobs
- **[DOC]** Unity exposes tunable input settings: **Translation Smoothing, Rotation
  Smoothing (0–1, higher = smoother but more lag), Persistence (frames to keep a contact
  without confirmation), Piece Set Model**. These are readonly at runtime; swap settings
  assets to change them. Source: `docs/unity-sdk/ai-assistant.md` and
  `docs/unity-sdk/getting-started-setup-reference.md`; `docs/unity-sdk/api-class-BoardInputSettings.md`.
- **[INF]** The smoothing/persistence trade-off (responsiveness vs. lag) is documented for
  Unity; the Web docs reviewed don't expose equivalent named knobs. This is a *tuning
  surface* difference, not a raw-performance difference.

---

## 4. CPU / GPU / memory headroom, startup, bundle size, thermal

**The docs publish none of these as comparative numbers.** What's documented:

- **[DOC]** Shared budget: 8-core CPU, Mali-G57, **4 GB RAM** (`learn-hardware.md`).
- **[DOC]** Touch ML is **off-loaded to the NPU** and does not consume game CPU/GPU on
  either SDK (`learn-architecture.md`, `learn-touch-system.md`).
- **[DOC]** Unity deploy artifact is an **APK** (`Builds/Android/...apk` per repo
  `AGENTS.md`; build via `board-connect install game.apk`). Web deploy is a **static web
  app packed to a `.webapp.zip`** with `web-pack`, no APK. Sources:
  `docs/unity-sdk/ai-assistant.md` (Build & Deploy) and `web/AGENTS.md` (Build & deploy).
- **[INF]** Startup time, peak memory, bundle/APK size, and battery/thermal behavior are
  **undocumented**. General expectation: a Unity APK carries the IL2CPP runtime + engine
  (larger artifact, heavier baseline memory), while a web app is a smaller bundle but runs
  inside a WebView with its own memory overhead. On 4 GB shared RAM, a graphically heavy
  Unity scene is the more likely memory-pressure candidate, but neither is documented.

---

## 5. Capability / feature parity & limits

- **[DOC]** Capability parity is explicit: **"Each SDK presents the same capabilities…
  The data they hand you is the same; the way you receive it differs by engine (Unity
  polls each frame, the Web SDK delivers a callback, Godot emits a signal)."** Touch,
  session/players, save games, and pause screen are all present in both. Source:
  `docs/unity-sdk/learn-architecture.md`.
- **[DOC]** Per-SDK conventions differ but are cosmetic, not performance-relevant: Unity
  uses bottom-left origin / Y-up / **radians**; Web uses top-left origin / Y-down /
  **degrees**. Source: `docs/unity-sdk/guide-touch-input.md` (per-SDK table),
  `docs/unity-sdk/learn-touch-system.md`.
- **[DOC]** Unity has UI plumbing the Web doesn't need: **`BoardUIInputModule`** is
  required because Board blocks system touch events from Unity's standard input module.
  Source: `docs/unity-sdk/getting-started-setup-reference.md`. **[INF]** This is a
  correctness/setup requirement, not a perf limit; the Web app uses normal DOM/Canvas
  events and has no analog.
- **[DOC]** The Web SDK has the **same model-at-pack-time** requirement: Piece games pass
  `--model <path>` to `web-pack`; the tooling never bundles/downloads the model at
  runtime. Unity stores the `.tflite` in `StreamingAssets/`. Sources: `web/AGENTS.md`,
  `docs/unity-sdk/getting-started-setup-reference.md`.
- **No documented Unity-only feature that affects performance, and no documented Web cap
  on contacts** — the architecture page states all SDKs report the same contact model.

---

## 6. Practical: our two builds

- **[FACT, repo]** The Web build is **PixiJS 8 (WebGL) 2D** (`web/package.json`:
  `"pixi.js": "^8.6.6"`, Vite, TypeScript), packed with `@board.fun/web-pack`. The Unity
  build is the original native port now running on the Board.
- **[INF]** For Trafalgar's 2D procedural sea + sprite ships + HUD, PixiJS/WebGL on a
  Mali-G57 is well within budget; there is no documented web performance ceiling this
  class of game would hit. The native Unity build's structural rendering advantage
  (section 1) is most relevant if the game moves to 3D, heavy shaders, or very high
  sprite/particle counts.
- **[DOC]** Worth noting for testing methodology: the Unity **Simulator explicitly warns
  "Desktop performance doesn't match device performance" and "Always test on real
  hardware before release."** It also does **not run the ML model**. Source:
  `docs/unity-sdk/simulator.md` ("Limitations"). The Web SDK's equivalent caveat: in a
  desktop browser `Board.isOnDevice` is `false` and there is no touch input, so perf must
  also be validated on-device (`web/AGENTS.md`, web setup-reference).

---

## 7. Where the docs are silent (be honest)

The following were **not** found in any reviewed Board doc and are therefore inference
only:

- Any head-to-head Unity-vs-Web benchmark, fps comparison, or "use Unity if…" perf rule.
- A `/web/performance` page (404) or any documented Web frame-rate target / draw-call
  budget.
- Memory footprint, startup time, APK vs `.webapp.zip` size, or battery/thermal figures
  for either SDK.
- Any statement that one SDK gets lower input latency than another — the docs frame the
  NPU/contact pipeline as SDK-neutral.

---

## Source index

Local mirror (Unity SDK docs), all under `docs/unity-sdk/`:

| File | Source URL | Used for |
| --- | --- | --- |
| `performance.md` | https://docs.dev.board.fun/unity/performance | 30→60 fps target (Unity-only) |
| `learn-hardware.md` | https://docs.dev.board.fun/learn/hardware | SoC/GPU/RAM/display specs |
| `learn-architecture.md` | https://docs.dev.board.fun/learn/architecture | NPU offload, SDK-neutral pipeline, capability parity |
| `learn-touch-system.md` | https://docs.dev.board.fun/learn/touch-system | NPU, low latency, unlimited contacts, noise rejection |
| `learn-pieces.md` / `learn-concepts.md` | .../learn/pieces, .../learn/concepts | Contact/Piece model, no contact limit |
| `architecture.md` | https://docs.dev.board.fun/unity/architecture | Unity polls `GetActiveContacts()` in `Update()` |
| `guide-touch-input.md` | https://docs.dev.board.fun/guides/touch-input | Poll (Unity) vs subscribe (Web), per-SDK conventions |
| `guide-piece-interaction-design.md` | https://docs.dev.board.fun/guides/piece-interaction-design | Per-frame snapshot, no discrete events |
| `getting-started-setup-reference.md` | https://docs.dev.board.fun/unity/getting-started/setup-reference | IL2CPP/ARM64, render pipelines, HDRP unsupported, input module |
| `ai-assistant.md` | https://docs.dev.board.fun/unity/ai-assistant | Input settings (smoothing/persistence), APK deploy |
| `api-class-BoardInputSettings.md` | https://docs.dev.board.fun/unity/api/BoardInputSettings.html | Smoothing/persistence/model properties |
| `simulator.md` | https://docs.dev.board.fun/unity/simulator | "Desktop performance doesn't match device" |
| `changelog.md` | https://docs.dev.board.fun/unity/changelog | SDK version 3.3.0; no perf-specific entries |

Live Web SDK pages (no local mirror; fetched 2026-06-04):

| Page | Source URL | Used for |
| --- | --- | --- |
| Web Architecture | https://docs.dev.board.fun/web/architecture | Thin TS layer → bridge → services; runs in Board Browser |
| Web Setup Reference | https://docs.dev.board.fun/web/getting-started/setup-reference | ESM/Vite, "runs inside Board's built-in browser (WebView)", ship web app not APK |
| Web Sample | https://docs.dev.board.fun/web/getting-started/sample | `Board.input.subscribe` per-frame Canvas draw |
| Web AI Assistant | https://docs.dev.board.fun/web/ai-assistant | mirrored in `web/AGENTS.md` (input subscribe, build/deploy) |
| **Web Performance** | https://docs.dev.board.fun/web/performance | **404 — does not exist** (no documented web perf guidance) |
