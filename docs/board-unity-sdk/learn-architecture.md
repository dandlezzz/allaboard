> Source: https://docs.dev.board.fun/learn/architecture — fetched 2026-06-04T18:38 (UTC-7)

# Architecture

How Board turns a physical Piece on the glass into data your game can use. This page is SDK-neutral: the pipeline is the same whether you build with Unity, the Web SDK, or Godot. For the classes and entry points a specific SDK exposes, see that SDK's own architecture page.

---

## The touch pipeline

```
Physical Piece / finger
        │
        ▼
Capacitive touch sensor  ──►  Touch controller (raw capacitance frames)
        │
        ▼
On-device ML (dedicated NPU)  ──►  Glyph recognition + contact tracking
        │
        ▼
Board OS touch service  ──►  noise rejection, smoothing, persistence
        │
        ▼
SDK (Unity / Web / Godot)  ──►  your game
```

Two things make this different from ordinary multi-touch:

- The ML runs on dedicated hardware. Glyph recognition and contact tracking happen on a separate NPU, so they don't compete with your game for CPU or GPU. Touch stays responsive no matter how busy your render loop is.
- The OS owns the touch pipeline, not your app. Inference, noise rejection, and tracking live in a Board OS service. Every SDK is a thin client over the same service, which is why all three report the same contact model.

See Touch for how recognition, noise rejection, and unlimited contacts work, and Pieces for how Glyphs encode Piece identity.

---

## What every SDK exposes

Each SDK presents the same capabilities, named idiomatically for its engine:

| Capability | What it covers |
| --- | --- |
| Touch input | Active contacts (fingers and Pieces), Glyph recognition, phases, orientation, touch state |
| Session & players | Active profile, player list, guests, profile switcher |
| Save games | Persistent per-app storage with player associations and quotas |
| Pause screen | Integration with Board's system pause overlay and custom buttons |

The data they hand you is the same; the way you receive it differs by engine (Unity polls each frame, the Web SDK delivers a callback, Godot emits a signal). The shared shape of a contact is documented once in Contact Model, including the per-SDK coordinate and orientation conventions.

---

## Per-SDK architecture

- Unity SDK Architecture — `BoardInput`, `BoardSession`, `BoardApplication`, `BoardSaveGameManager`, and the `Board.*` namespaces.
- Web SDK Architecture — the `Board` object, the platform bridge, and the six domain modules.
- Godot SDK Architecture — the `Board` autoload and its module singletons.

---

## See Also

- Contact Model - The shared contact shape and per-SDK conventions
- Concepts - Terminology and definitions
- Touch - How Board's touch technology works
- Pieces - How Glyphs identify Pieces
