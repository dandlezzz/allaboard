# SFX Sourcing — *Trafalgar — Age of Sail*

> Read-only research on where to get high-quality, **commercially licensable,
> redistributable, tiny** sound effects for the web game — focused on **cannon
> fire** and **cannonball impact** (hit thud + near-miss water splash), plus
> naval/age-of-sail ambience (creaking ships, wind, sinking, UI clicks).
>
> Companion to `docs/sound-effects-plan.md` (which covers the *engineering* —
> Web Audio graph, hook points, bundle budget). This doc only covers *sourcing
> and licensing*. **No code is touched.**
>
> Researched & link-verified 2026-06-05. Where a fact is quoted from a source it
> is marked **[confirmed]** with a link; everything else is **[guidance]**.

---

## TL;DR — recommendations for our case

Our constraints (from `sound-effects-plan.md` + `web/AGENTS.md`): the app ships
as a flat board.fun `.webapp.zip`, so every clip must be **commercially usable,
redistributable *inside* the bundle, short, mono, ≤ ~40 KB each (OGG/MP3)**, and
**ideally CC0** so there's zero attribution bookkeeping.

**Top 3 recommended sources (in order):**

1. **Freesound.org, filtered to CC0** — biggest selection of exactly our sounds
   (cannon, black-powder, wood impact, water splash), public-domain, no
   attribution, redistribution-safe. The single best place to hand-pick 3–4
   perfect short clips. [confirmed → license/filter below]
2. **OpenGameArt.org (CC0 entries)** — has a ready-made **CC0 naval-battle set**
   (`Tiny Naval Battle Sounds Set` by qubodup) with cannon fire + water splashes
   already game-ready as `.ogg`. Purpose-built for games. [confirmed]
3. **Sonniss "GDC Game Audio Bundle"** — free, professional-grade, royalty-free,
   **no attribution, redistribution-inside-your-game allowed** — best *quality*
   if you want pro source material and don't mind digging through GBs. [confirmed]

**Best licensing-safe option (zero hassle):** **CC0 clips from Freesound +
OpenGameArt.** CC0 = public domain, no attribution, free to redistribute in a
commercial app. This sidesteps the one real footgun (CC-BY attribution) entirely.

**AI option worth trying:** **ElevenLabs Sound Effects** (text-to-SFX). On any
paid plan (from **$5/mo Starter**) generated audio is **royalty-free for
commercial use**; great for getting an *exact* "32-gun first-rate broadside" or
a specific "cannonball splash" without crate-digging. Caveat: you don't get
exclusive ownership and can't use it to build a competing product. **[confirmed]**

**Easiest path to "good enough now":** grab `cannon_fire.ogg` + a splash + a
wood-thud from the **OpenGameArt CC0 naval set** (or 3 CC0 Freesound clips),
trim/normalize/downsample to mono in Audacity, export ~22–32 kbps mono OGG.
Done in ~15 min, license-clean.
**Path to "best quality":** pull raw cannon/foley from the **Sonniss GDC bundle**
(or ElevenLabs-generate to taste), then design + compress in a DAW.

---

## 1. Free / royalty-free libraries

### Freesound.org — **best pick for hand-picking exact clips** [confirmed]
- **What:** huge community library; every sound carries an explicit Creative
  Commons license. Filterable by license, format, duration.
- **License (the important part):** each sound is **CC0**, **CC-BY 4.0**, or
  **CC-BY-NC 4.0** (NC = *non-commercial, do not use*). Use the **license filter**
  to show only **CC0** (public domain, no attribution) or "Free Cultural Works"
  (CC0 + CC-BY). Source: [Freesound FAQ](https://freesound.org/help/faq/).
- **CC0 = ideal for us:** "CC0 sounds can be used without restriction" — no
  attribution, redistributable in a commercial app. [confirmed,
  [FAQ](https://freesound.org/help/faq/)]
- **CC-BY = usable but adds bookkeeping:** you must credit Title-Author-Source-
  License (TASL). A bundled `CREDITS.txt` or an in-game credits screen satisfies
  this; you do *not* need on-screen credit during play.
  [[CC attribution practices](https://wiki.creativecommons.org/wiki/Recommended_practices_for_attribution)]
- **Footgun to know:** some CC0 uploaders write "please link back" / "See profile
  for CC-BY attribution requirements" in the description. Per Freesound's own
  legal forum, **the license badge governs — if it says CC0, attribution is
  legally optional** regardless of the description text. Screenshot the sound's
  license at download time for your records.
  [[Freesound legal forum](https://freesound.org/forum/legal-help-and-attribution-questions/42164/)]
- **Quality:** variable (community uploads) but the best clips are excellent;
  plenty of pro-grade gun/cannon/foley.
- **Cannon / impact / splash availability:** strong. Verified examples:
  - `Cannon Shot` by qubodup — **CC0** (US-gov public-domain source), 12 s stereo FLAC.
    [link](https://freesound.org/people/qubodup/sounds/187767/)
  - `MK 45 Gun Fired` by qubodup — **CC0** "big cannon on a navy ship," 15 s.
    [link](https://freesound.org/people/qubodup/sounds/184728/)
  - `Cannon explosion sound` by SamsterBirdies — **CC0**, layered boom, ~6.7 s.
    [link](https://freesound.org/people/SamsterBirdies/sounds/621000/)
  - `S20-06 Big cannon with reverb` by craigsmith — vintage Hollywood cannon, **mono** 4.7 s.
    [link](https://freesound.org/people/craigsmith/sounds/675608/)
  - `Wooden Thud (Mono)` by Breviceps — **CC0**, 0.5 s, **15.7 KB** (perfect impact size!).
    [link](https://freesound.org/people/Breviceps/sounds/449955/)
  - `Big Splash` by darcydunes — literal cannonball-into-water, ~4.3 s.
    [link](https://freesound.org/people/darcydunes/sounds/273834/)
  - `Big Water Splash` by qubodup — CC-BY, 2.2 s, good near-miss plume.
    [link](https://freesound.org/people/qubodup/sounds/442773/)
  - Search-helper site that surfaces Freesound by license/duration:
    [SoundSpool /tags/cannon](https://soundspool.com/tags/cannon),
    [/sounds/cannonball](https://soundspool.com/sounds/cannonball).
- **Search terms:** `cannon`, `cannon shot`, `cannon broadside`, `black powder`,
  `musket`, `cannonball impact wood`, `wood impact`, `splash`, `water splash`,
  `ship creak`, `rigging`, `wind`. Always set License = **CC0** first.

### OpenGameArt.org — **best ready-made naval pack** [confirmed]
- **What:** game-asset community; sounds are CC0 / CC-BY / CC-BY-SA / OGA-BY / GPL.
  CC0 entries are perfect for us.
- **★ `Tiny Naval Battle Sounds Set`** by qubodup (Iwan Gabovitch) — explicitly
  **CC0** ("optional" attribution), built from US-military and public-domain
  recordings, **includes cannon fire + water splash variants**, single `.7z`
  (~4.5 MB) of game-ready clips. This is the closest single "drop-in" match to
  our needs. [link](https://lpc.opengameart.org/content/tiny-naval-battle-sounds-set)
- **`Battle at sea`** (OGA Winter Jam 2022) — purpose-built age-of-sail set:
  `cannon_fire.ogg` (180 KB), `cannon_hit.ogg`, `cannon_miss.ogg` (81 KB, the
  near-miss splash), `cannon_hit_ship`, `cannon_hit_wall`, `ship_destroyed`,
  `ship_ram_ship` (wood creak/break). Files are small `.ogg` already.
  ⚠️ **Verify the license badge on the page before use** — the listing didn't
  surface a machine-readable license in research; confirm it's CC0/CC-BY and
  follow it. [link](https://opengameart.org/content/battle-at-sea)
- **License caution:** OGA mixes licenses *per file*; some are **OGA-BY**/CC-BY
  (attribution) or GPL (copyleft — avoid for a closed bundle). Check each file's
  license; prefer CC0. CC0 packs are redistribution-safe in a commercial app.
- **Quality:** game-tuned (short, punchy, pre-trimmed) — often *more* convenient
  than Freesound even if less "cinematic."

### Pixabay Sound Effects — **easy, no-attribution, commercial** [confirmed]
- **License:** Pixabay Content License — **free for commercial use, no
  attribution required**, may modify/adapt. [confirmed,
  [license summary](https://pixabay.com/service/license-summary/) ·
  [FAQ](https://pixabay.com/service/faq/)]
- **Restrictions:** can't resell/redistribute content **standalone** (i.e. as a
  sound pack) — but **using it inside your game is fine**. Watch trademarks and
  identifiable people (n/a for SFX). No indemnification — Pixabay assumes zero
  legal risk for you, so do light due-diligence.
  [[risk profile](https://picdefense.io/resources/source-intel/pixabay/)]
- **Content-ID note:** some audio is fingerprinted for YouTube Content-ID
  (irrelevant for an app bundle, only matters for video).
- **Quality / cannon coverage:** decent, curated; has cannon, explosion, splash.
  Browse [pixabay.com/sound-effects](https://pixabay.com/sound-effects/).

### Mixkit — **free, commercial, no attribution, no account** [confirmed]
- **License:** Mixkit Free SFX License — free for commercial & personal use,
  **no attribution, no sign-up**. Cannot redistribute the files standalone.
  [[overview](https://uppbeat.io/blog/sound-effects/free-sound-effects-websites)]
- **Quality:** professionally curated, smaller catalog. Good for quick grabs.

### Zapsplat — **big library, but free tier needs attribution** [confirmed]
- **License:** worldwide/perpetual/commercial. **Free (Basic) account ⇒
  attribution to "ZapSplat" required**, MP3 only, 4 downloads/hour. **Premium
  (Gold) ⇒ no attribution, WAV access, unlimited.** Never redistribute as
  standalone files. [confirmed,
  [standard license](https://www.zapsplat.com/license-type/standard-license/) ·
  [pricing](https://www.zapsplat.com/pricing/)]
- **Our take:** the free-tier attribution requirement makes it less "zero-hassle"
  than CC0 sources; fine if you go Gold or are OK crediting ZapSplat.

### Sonniss "GDC Game Audio Bundle" — **free pro packs, best quality** [confirmed]
- **What:** every year Sonniss gives away GBs of pro sound-library samples
  (e.g. GDC 2024 = 27.5 GB+) to celebrate GDC; 10+ years archived.
  [[archive](https://sonniss.com/gameaudiogdc/)]
- **License (excellent for us):** **royalty-free, commercially usable, no
  attribution, unlimited projects for life.** You **may** modify and ship them
  *inside* a finished game/app. You **may not** resell them as standalone files
  or in a competing SFX library. **AI/ML training is explicitly prohibited.**
  [confirmed, [license + FAQ](https://sonniss.com/gameaudiogdc/) ·
  [license summary](https://www.scribd.com/document/374748229/License)]
- **Quality:** professional — same files Sonniss sells. The free samples are real
  library content, untampered.
- **Cannon coverage:** the bundles include weapons/explosion/foley/water libraries
  across vendors; expect usable cannon/black-powder/impacts/splashes, but you must
  download multi-GB archives and dig (no per-clip search). Best when you want a
  *high-quality* starting point to design from.

### BBC Sound Effects — **great archive, but NOT free for commercial** [confirmed]
- **What:** ~33,000 effects online. [confirmed]
- **License:** **RemArc license = personal / educational / research ONLY.**
  **Commercial use requires a paid license** (handled by Pro Sound Effects via the
  "Buy sound" button). Text/data-mining & AI training also need a license.
  [confirmed, [BBC licensing](https://sound-effects.bbcrewind.co.uk/licensing) ·
  [RemArc explainer](https://www.avosound.com/en/licensing/remarc-license)]
- **Our take:** **do not ship BBC RemArc clips in our commercial `.webapp.zip`.**
  Only relevant if you buy a commercial license per clip.

### Kenney — **CC0, but no cannon-specific pack** [confirmed]
- **License:** **CC0** across all packs — free, no attribution, redistributable.
  [confirmed, [Kenney audio](https://www.kenney.nl/assets/category:Audio)]
- **Useful packs:** `Impact Sounds` (130 files, foley impacts — usable for the
  *thud*), `Interface Sounds` / `UI Audio` (**perfect for our UI clicks**),
  `Digital Audio`, `RPG Audio`.
  [[Impact Sounds](https://www.kenney.nl/assets/impact-sounds)]
- **Cannon:** no dedicated cannon/black-powder pack — use Kenney for **UI clicks
  and generic impacts**, get cannon/splash from Freesound/OGA.

---

## 2. Paid libraries / marketplaces

Worth it when you need **breadth, consistency, and indemnification** — overkill
for *two or three* clips, but handy if SFX scope grows (wind beds, sinking, UI).

| Source | Cost model (2026) | When it's worth it |
| --- | --- | --- |
| **Soundsnap** | Subscription: **~$269/yr** (unlimited SFX) or **~$169/6 mo** (75 SFX/mo); 465k+ sounds. [confirmed, [pricing](https://omr.com/en/reviews/product/soundsnap/pricing)] | You need many curated SFX fast with a clean license. |
| **Epidemic Sound** | **$9.99/mo** (Creator, personal) → **$16.99** (Pro, commercial) → **$30** (Business), annual; 30-day free trial; 250k+ SFX. Direct license, "stays licensed after you publish." [confirmed, [pricing](https://www.epidemicsound.com/blog/artlist-vs-epidemic-sound/)] | Music **and** SFX from one sub; content published while subscribed stays licensed. |
| **Artlist** | **$9.99/mo** (Social) → Max **$39.99/mo** (adds footage/templates), annual; ~50k SFX. [confirmed, [comparison](https://www.cchound.com/epidemic-sound/artlist-vs-epidemic-sound/)] | Similar to Epidemic; pick by catalog taste. |
| **BOOM Library** | **Buyout from $999** (own forever) or **subscription from ~$230/yr** (access while subscribed; published projects stay licensed). [confirmed, [Signature Series](https://www.boomlibrary.com/sound-effects/signature-series-bundle/)] | Top-tier cinematic quality; pro sound design. Overkill for placeholders. |
| **Pro Sound Effects** | Per-clip / subscription; also the **commercial licensor of BBC SFX**. [[BBC licensing](https://sound-effects.bbcrewind.co.uk/licensing)] | You specifically want BBC clips with a commercial license. |
| **Unity Asset Store / Unreal Marketplace (Fab)** | One-time per pack ($0–$50 typical). | If you're already in that ecosystem; many "naval/cannon SFX" packs with engine-redistribution-friendly EULAs (check each). |

**Bottom line:** for *our* two-to-four clips, **paid is not necessary** — free
CC0 + Sonniss covers it. Revisit a subscription only if SFX scope expands into a
full soundscape (Phase 3 in `sound-effects-plan.md`).

---

## 3. AI sound-effect generators (2026)

Great for getting an **exact** sound on demand ("naval 32-gun broadside with a
long rolling tail," "single cannonball splash, close, mono") instead of crate-
digging. Quality on **impulsive sounds (cannon booms, impacts)** is good and
improving, though a real recording often still has more low-end "punch."

### ElevenLabs Sound Effects — **most accessible, clear commercial rights** [confirmed]
- **What:** text-to-SFX generator. [link](https://elevenlabs.io/sound-effects)
- **License/commercial rights:** on **any paid plan**, output is **royalty-free
  for commercial use** (games, ads, YouTube). **Free plan = non-commercial +
  attribution only — do not ship free-tier output.** You may not resell the tool
  or use output to build a competing product. [confirmed,
  [SFX page](https://elevenlabs.io/sound-effects) ·
  [pricing 2026](https://bigvu.tv/blog/elevenlabs-pricing-2026-plans-credits-commercial-rights-api-costs/)]
- **Cost:** **Starter $5/mo** (commercial rights unlock here), Creator $22, Pro $99;
  credit-based (SFX gen consumes monthly credits). [confirmed]
- **Caveat:** per ToS you grant ElevenLabs a broad license over your inputs and
  don't get *exclusive* ownership of outputs.
  [[ToS](https://elevenlabs.io/terms-of-use)]
- **Our take:** best first AI tool to try — cheap, fast, exact, and clearly
  commercial-safe on the $5 tier.

### Stable Audio (Stability AI) — **SFX-capable, license tiers matter** [confirmed]
- **What:** generative audio incl. **sound effects** + audio inpainting; API
  available. [link](https://stability.ai/stable-audio)
- **Commercial rights:** **Free/Pro = Personal license (not for commercial)**;
  **Studio ($29.99/mo) / Max ($89.99/mo) = Creator license for individual
  commercial projects**; **organizations/apps/games should use the Enterprise
  license** (which adds legal indemnification). Trained on **fully licensed data**
  (cleaner provenance story). [confirmed,
  [review/pricing](https://aipedia.wiki/tools/stable-audio/) ·
  [models](https://stability.ai/stable-audio)]
- **Note:** there's also **Stable Audio Open** (open weights, MIT-ish) but lower
  quality and you must re-check the current license before shipping.
- **Our take:** strong for SFX, but **read the license tier carefully** — a
  shipped commercial game likely points to Enterprise, so it's heavier than
  ElevenLabs for our scale.

### Others (2026) [guidance]
- **Adobe Firefly** (audio prompting, inside Adobe's commercially-oriented
  ecosystem), **Noiz AI** (voices + SFX + foley + music in one workflow,
  [link](https://noiz.ai/use-cases/en/ai-sound-creator)), **Meta MusicGen / Audio
  models** (open weights, MIT — self-host, lower SFX quality). Verify commercial
  terms per tool before shipping.
- **General licensing caution for *all* AI audio:** confirm (a) your plan grants
  commercial rights, and (b) the vendor's stance on output ownership/indemnity.
  The copyright status of purely AI-generated assets is still unsettled, so
  ElevenLabs/Stable-Audio's explicit "royalty-free on paid plan" language is the
  safest footing today.

---

## 4. DIY — record or synthesize a passable cannon fast

### Synthesize a cannon boom in Audacity (free) — fastest placeholder [guidance]
A convincing cannon = **sharp transient + low-end body + noise burst + reverb
tail**. In **Audacity** ([free, audacityteam.org](https://www.audacityteam.org/)):

1. **Generate → Noise** (Brownian/"brown" noise, ~0.6–1.0 s) for the body —
   brown noise is bassier than white, better for a boom.
2. **Effect → Filter Curve / Low-Pass** around **150–400 Hz** to keep the
   low-end thump; roll off the hiss.
3. **Add the transient:** layer a very short (~20–40 ms) burst of louder/whiter
   noise or a clipped sine "thump" at the very start for the "crack."
4. **Envelope:** use the Envelope tool for a near-instant attack and a fast-ish
   exponential decay (fade-out) so it punches then falls away.
5. **Effect → Reverb** (large/"cathedral"-ish, moderate wet) for the rolling
   tail across open water; keep total length ≤ ~1.2 s.
6. **Optional:** slight **distortion/limiter** for grit; **pitch down** ~10–20%
   for a bigger "first-rate" feel.
7. **Make the impact:** for the **hull thud**, a short brown-noise burst + a
   low sine "knock" + tiny reverb (~0.3–0.5 s). For the **near-miss splash**,
   layer filtered white noise with a quick bandpass sweep (bright→dull) and a
   little reverb (~0.4 s), kept quieter than the hit.
8. **Export → OGG/MP3**, **mono**, downsample to **22.05 kHz**, low bitrate, to
   hit the **≤ ~40 KB** budget.

(Layering 2–3 free CC0 booms and re-mixing them is often faster and better than
pure synthesis — and still license-clean.)

`jsfxr`/`sfxr` ([sfxr.me](https://sfxr.me)) is great for **UI clicks/blips** but
too "8-bit" for a believable cannon.

### Recording / foley basics [guidance]
- Real black-powder/cannon recordings are hard to get safely; **foley substitutes**
  work: a deep **drum hit / kick** + slammed heavy door for the body, books
  dropped on wood for impacts, a bucket of water / paddle slap for splashes, rope
  creak for rigging. Record close, in a quiet room, then add reverb.
- Capture at 44.1 kHz, leave headroom (peak ~ -6 dB), then trim → normalize →
  mono → compress to budget. Keep your **own recordings** = you own the rights,
  no attribution ever.

---

## 5. Practical recommendation for OUR bundle

**Hard requirements (recap):** commercially licensable · redistributable inside
the `.webapp.zip` · short mono OGG/MP3 ≤ ~40 KB each · **prefer CC0** (no
attribution). These rule out: BBC RemArc (non-commercial), Zapsplat free-tier
(attribution), any CC-BY-NC, and GPL OGA files (copyleft).

### Shortlist — exact sources & search terms

**Cannon fire:**
- OpenGameArt **`Tiny Naval Battle Sounds Set`** (CC0) → cannon fire clip.
  [link](https://lpc.opengameart.org/content/tiny-naval-battle-sounds-set)
- OpenGameArt **`Battle at sea`** → `cannon_fire.ogg` (verify license badge).
  [link](https://opengameart.org/content/battle-at-sea)
- Freesound (License = **CC0**), terms: `cannon shot`, `cannon broadside`,
  `black powder`. Verified CC0: `Cannon explosion sound`
  ([link](https://freesound.org/people/SamsterBirdies/sounds/621000/)),
  `Cannon Shot` ([link](https://freesound.org/people/qubodup/sounds/187767/)),
  `MK 45 Gun Fired` (navy cannon, [link](https://freesound.org/people/qubodup/sounds/184728/)).

**Impact (hull thud):**
- Freesound `Wooden Thud (Mono)` by Breviceps — **CC0, 0.5 s, 15.7 KB**, already
  within budget. [link](https://freesound.org/people/Breviceps/sounds/449955/)
- OGA `Battle at sea` → `cannon_hit.ogg` / `cannon_hit_ship_short.ogg`.
- Kenney `Impact Sounds` (CC0) for generic thuds.
  [link](https://www.kenney.nl/assets/impact-sounds)
- Freesound terms: `wood impact`, `cannonball impact wood`, `wood hit`.

**Near-miss splash:**
- OGA `Battle at sea` → `cannon_miss.ogg` (81 KB) or `Tiny Naval Battle Sounds Set`.
- Freesound `Big Splash` ([link](https://freesound.org/people/darcydunes/sounds/273834/)),
  `Big Water Splash` ([link](https://freesound.org/people/qubodup/sounds/442773/) — CC-BY),
  terms: `splash`, `water splash`, `body of water splash`. Filter **CC0**.

**UI clicks / ambience (Phase 3):**
- Kenney `Interface Sounds` / `UI Audio` (CC0) for clicks.
- Freesound CC0: `ship creak`, `rope creak`, `rigging`, `wind`, `ocean waves`,
  `ship sinking`, `wood breaking` for creaks/wind/sinking.

### The two paths

- **Easiest "good enough now" (≈15 min, license-clean):** download
  `cannon_fire` + a splash + `Wooden Thud (Mono)` from the **CC0 OGA naval set +
  Freesound**, trim/normalize/mono/downsample in Audacity, export ~32 kbps mono
  OGG (with MP3 fallback per `sound-effects-plan.md`). All CC0 → no attribution,
  redistribution-safe.
- **Best quality (more effort):** pull raw cannon + water + foley from the
  **Sonniss GDC bundle**, and/or **ElevenLabs-generate** an exact "naval broadside"
  / "cannonball splash" on the $5 paid tier, then design + compress in a DAW.

### Housekeeping
- Even though CC0 needs no attribution, keep a **`web/public/audio/CREDITS.txt`**
  (or a doc note) listing each clip's source URL + license + download date. It's
  cheap insurance and *required* the moment you mix in any CC-BY clip.
- Screenshot/record the license badge at download time (Freesound CC0 descriptions
  sometimes "ask" for credit that the license doesn't require).
- Re-encode everything to **mono ≤ ~40 KB** to respect the `.webapp.zip` budget;
  confirm **OGG/Vorbis decode on the Board WebView** (open question already noted
  in `sound-effects-plan.md` §5) or ship MP3.

---

## Sources (confirmed)

- Freesound FAQ (license types, CC0 = no restriction): https://freesound.org/help/faq/
- Freesound legal forum (CC0 badge overrides "please credit" text): https://freesound.org/forum/legal-help-and-attribution-questions/42164/
- CC attribution best practices (TASL): https://wiki.creativecommons.org/wiki/Recommended_practices_for_attribution
- OpenGameArt — Tiny Naval Battle Sounds Set (CC0): https://lpc.opengameart.org/content/tiny-naval-battle-sounds-set
- OpenGameArt — Battle at sea (verify license): https://opengameart.org/content/battle-at-sea
- Pixabay Content License summary: https://pixabay.com/service/license-summary/ · FAQ: https://pixabay.com/service/faq/
- Mixkit / Zapsplat overview: https://uppbeat.io/blog/sound-effects/free-sound-effects-websites
- Zapsplat standard license: https://www.zapsplat.com/license-type/standard-license/ · pricing: https://www.zapsplat.com/pricing/
- Sonniss GDC Game Audio Bundle (license/FAQ): https://sonniss.com/gameaudiogdc/
- BBC Sound Effects licensing (RemArc, non-commercial): https://sound-effects.bbcrewind.co.uk/licensing
- Kenney audio (CC0): https://www.kenney.nl/assets/category:Audio · Impact Sounds: https://www.kenney.nl/assets/impact-sounds
- ElevenLabs Sound Effects: https://elevenlabs.io/sound-effects · pricing 2026: https://bigvu.tv/blog/elevenlabs-pricing-2026-plans-credits-commercial-rights-api-costs/
- Stable Audio (license tiers/pricing): https://aipedia.wiki/tools/stable-audio/ · https://stability.ai/stable-audio
- Soundsnap pricing: https://omr.com/en/reviews/product/soundsnap/pricing
- Epidemic Sound / Artlist pricing: https://www.epidemicsound.com/blog/artlist-vs-epidemic-sound/ · https://www.cchound.com/epidemic-sound/artlist-vs-epidemic-sound/
- BOOM Library Signature Series: https://www.boomlibrary.com/sound-effects/signature-series-bundle/
- Audacity (free DAW): https://www.audacityteam.org/
