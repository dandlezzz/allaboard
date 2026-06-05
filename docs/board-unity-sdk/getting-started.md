> Source: https://docs.dev.board.fun/getting-started — fetched 2026-06-04T18:38 (UTC-7)

# Getting Started

Board has three SDKs. They all expose the same capabilities (touch and Piece input, players and profiles, save games, the system pause overlay, avatars) and you can build a full game with any of them. Pick the one that matches the engine or stack you already work in. You can also build the same game in more than one if you ever want to.

## Pick your SDK

### Unity (v3.3.0)
Best fit if you build with Unity or use C# as your primary game language. The most mature SDK and the broadest API surface.
→ Unity Quick Start

### Godot (v1.0.0-beta.1)
Best fit if you build with Godot 4.6+ in GDScript. A drop-in addon that exposes Board through a `Board` autoload singleton with signals and GDScript idioms throughout.
→ Godot Quick Start

### Web (v1.0.0-beta.1)
Best fit if you build with web technology (TypeScript, JavaScript, any web framework). A small typed wrapper over the platform bridge that runs your app inside Board's built-in browser.
→ Web Quick Start

## Next steps

Whichever SDK you pick, the following are worth knowing about before you go deep:

- Learn — how Board's touch tech works, what Pieces and Glyphs are, hardware specs, and the platform architecture.
- Guides — implementation guides that apply to all SDKs (touch input, players, save games, the pause overlay, and more) with code samples in all three languages.
- Tools — Board Connect (the recommended developer workflow tool) and `bdb` (legacy CLI).
