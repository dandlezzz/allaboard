# Trafalgar — Age of Sail

A real-time-strategy simulation of 18th-century naval combat (think *Battle of Trafalgar*),
built as a game for the **[board.fun](https://board.fun)** physical tabletop multi-touch platform.
Two fleets of sailing warships — **British** vs **Franco-Spanish** — manoeuvre with the wind and
trade thundering broadsides until one side is sunk or captured.

The entire game is **procedurally generated at runtime** (camera, sea, ships, sails, smoke, HUD).
There is **no scene or prefab to author by hand**: open the project and press **Play**.

---

## What it is

- **Overhead, top-down RTS.** Orthographic camera straight above the sea. **No fog of war** — the
  whole arena is visible to every player around the table at all times.
- **Two opposing fleets** of multiple ships. Local multiplayer on the same table (the board.fun
  model), each side commanding its own ships, **plus a built-in AI** so it's playable solo.
- **Per-ship command** of course/heading, sail (speed) setting, and ammunition type.
- **Wind-driven sailing**: ships cannot sail into the wind. Course choice is the core tactic.
- **Broadside gunnery** with three shot types, hull/rigging/crew damage, sinking, and **boarding &
  capture** of weakened enemies.

---

## How to open it in Unity

1. **Unity version:** `2021.3` (developed against `2021.3.45f1`; any `2021.3.x` LTS is fine). This is
   the version the Board SDK targets.
2. Open this repository folder (`/.../boarders`) as a Unity project. The project lives at the repo
   root: `Assets/`, `Packages/`, `ProjectSettings/`.
3. The Board SDK is referenced as a **local package** from `Packages/manifest.json`:
   ```json
   "fun.board": "file:../package"
   ```
   (`com.unity.inputsystem` 1.7.0 and `com.unity.ugui` are also pulled in.) Unity resolves the SDK
   that ships in this repo at `./package`.
4. The project is pre-configured for the **new Input System** (`activeInputHandler: 1` in
   `ProjectSettings/ProjectSettings.asset`) and **landscape** orientation, as the Board SDK requires.
5. Press **Play**. `GameBootstrap` (a `[RuntimeInitializeOnLoadMethod]`) builds the whole game in code,
   so any empty scene works.

> For a device build, run **Board → Configure Unity Project…** (provided by the SDK) to set Android /
> IL2CPP / ARM64 / API 33, then build for Android.

---

## How to play (controls)

Designed for a touch table first, with a full **mouse fallback in the editor** — both go through one
identical input path (`InputRouter`).

| Action | Touch / Mouse |
| --- | --- |
| **Select** one of your ships | Tap / click the ship |
| **Set a course** | Tap the sea where you want the ship to head |
| **Steer continuously** | Drag from your ship (or on the sea) — the ship steers toward your finger |
| **Change sail** (Furled → Battle Sail → Full Sail) | Tap the **Sail** button on the ship's pop-up panel |
| **Change shot** (Round → Bar → Grape) | Tap the **Shot** button on the panel |
| **Toggle 2nd player** (AI ⇄ human) | Tap **"Franco-Spanish: AI/Human"** (top-left) |
| **Rematch** (after a result) | Tap anywhere |

- The **control panel appears next to the selected ship** (not in a screen corner) so players seated
  anywhere around the table can use it. Hit targets are touch-sized.
- **Firing is automatic**: a ship looses a broadside whenever an enemy drifts into a port/starboard
  arc and that side has finished reloading. Your job is to manoeuvre to bring guns to bear.
- Both sides can be operated at once (unlimited simultaneous touches on real hardware); each side has
  its own independent selection.

---

## Rules & mechanics (how each system works)

### Wind & point of sail (`Combat/Wind.cs`)
A single global wind direction slowly veers over the battle. A ship's achievable speed depends on its
angle to the wind (its *point of sail*):

- **In irons** (within ~42° of dead upwind): almost no headway — avoid this.
- **Close-hauled** (~42–75°): slow.
- **Beam reach** (~75–115°): fastest.
- **Broad reach / running** (downwind): fast but slightly less than a reach.

A wind indicator at the top of the HUD shows which way the wind blows and the bearing it comes from.

### Ships & classes (`Ships/ShipClassDef.cs`, `Ships/Ship.cs`)
Three classes with distinct stats (guns, hull/rigging/crew, top speed, turn rate, range, reload):

| Class | Guns/broadside | Hull | Top speed | Turn rate | Feel |
| --- | --- | --- | --- | --- | --- |
| **Frigate** | 18 | 70 | fast | nimble | scout / raider |
| **Third Rate (74)** | 37 | 130 | medium | medium | line workhorse |
| **First Rate (100+)** | 52 | 190 | slow | ponderous | flagship |

Movement is frame-rate independent (`Time.deltaTime`). Heading eases toward the ordered course at the
ship's turn rate; speed eases toward `topSpeed × sail × pointOfSail × rigging`. **Rigging damage drags
down both speed and turn rate.**

### Combat & ammunition (`Combat/CombatSystem.cs`, `Ships/AmmoType.cs`)
Guns are mounted along the hull sides, so a ship fires **broadsides** only at targets roughly **abeam**
(a ±45° arc on port or starboard) — never ahead or astern. Each broadside reloads independently. Damage
scales with **gun count**, **range falloff**, and a little spread.

- **Round shot** → smashes the **hull** (sinks ships).
- **Bar shot** → tears up **rigging/sails** (cripples speed & turning, little hull damage).
- **Grape shot** → cuts down the **crew** (softens a ship for boarding).

A sunk ship (hull → 0) visibly goes under and is removed.

### Boarding & capture (`Combat/BoardingSystem.cs`)
When a ship lies alongside an enemy that is sufficiently weakened (**low hull or low crew**) for a few
seconds, the enemy is **captured** rather than sunk: it switches to the attacker's side (patched up just
enough to keep fighting). Use grape shot to thin a crew, then close to board and take the prize.

### Win condition (`Core/GameManager.cs`)
A side wins when **every enemy ship is sunk or captured**. The result banner appears and a tap starts a
fresh engagement (new random wind).

### AI (`AI/FleetAI.cs`)
Each AI ship picks the nearest enemy, manoeuvres to present a broadside (or closes to board a crippled
prize), trims sail to the situation, avoids the no-go zone and the arena edges, and chooses ammo to match
its intent (bar shot to cripple a runner, grape to set up a capture, round shot otherwise).

---

## Mapping to the Board platform

- **Touch / glyph input** — `InputLayer/InputRouter.cs` reads `BoardInput.GetActiveContacts(
  BoardContactType.Finger)` and `…(Glyph)` on real hardware and converts each `BoardContact`
  (`contactId`, `screenPosition`, `orientation`, `phase`) into a unified `PointerSample`. Screen
  coordinates are bottom-left origin, matching both the SDK and Unity, so no Y-flip is needed for
  world ray-casting.
- **Editor / desktop fallback** — `BoardInput` returns empty arrays unless running on the Board
  (`BoardSupport.enabled`), so the same router transparently reads the **new Input System**
  `Mouse`/`Touchscreen` in the editor. The full finger/mouse control path is self-sufficient.
- **Glyph pieces (optional)** — if physical glyph pieces are present, placing one on a friendly ship
  selects it and steers it to the piece's orientation. This is a bonus on hardware and never blocks the
  finger/mouse path (glyphs are always empty in the editor).
- **Session & AI players** — registers a `BoardAIPlayerType` ("Commodore") via
  `BoardSession.SetAIPlayerTypes`.
- **Pause screen** — sets the app name via `BoardApplication.SetPauseScreenContext`. (These SDK calls
  are safe no-ops in the editor.)

---

## Project layout

```
Assets/Scripts/
  Core/      GameBootstrap, GameManager, SceneBuilder, GameConfig, Faction, Nav
  Ships/     Ship, ShipView, ShipFactory, ShipClassDef, SailSetting, AmmoType
  Combat/    Wind, CombatSystem, BoardingSystem, Projectile
  Input/     InputRouter, PointerSample        (namespace Trafalgar.InputLayer)
  AI/        FleetAI
  UI/        HudController, UIFactory
  Rendering/ MeshUtil, MaterialUtil
Packages/manifest.json        # references fun.board (local), input system, uGUI
ProjectSettings/              # Unity 2021.3, new Input System, landscape
package/                      # the Board SDK (fun.board 3.3.0) — provided
```

### Key scripts at a glance
- **`GameBootstrap`** — `[RuntimeInitializeOnLoadMethod]` entry point; spawns the `GameManager`.
- **`GameManager`** — builds scene + fleets, owns all systems, routes input → selection/orders,
  detects the win, drives the HUD, handles rematch and the AI/human toggle.
- **`SceneBuilder`** — overhead camera, sun, and the sea (disables the default scene camera).
- **`Ship` / `ShipView`** — simulation (movement, damage, reloads, capture) and its procedural visuals
  (hull silhouette, sails that shrink with rigging damage, north-up status bars, powder smoke).
- **`Wind`** — global wind + point-of-sail speed curve.
- **`CombatSystem` / `BoardingSystem` / `Projectile`** — broadside gunnery, capture, tracer effects.
- **`FleetAI`** — the solo opponent.
- **`InputRouter` / `PointerSample`** — the Board ↔ mouse input abstraction.
- **`HudController` / `UIFactory`** — the entirely code-built HUD with custom (EventSystem-free) hit
  testing.

---

## Notes, assumptions & what couldn't be verified

- **Could not run Unity** in this environment, so the C# was static-checked by hand against the actual
  SDK source in `package/` (input, session, core, application APIs). API usage was verified to exist;
  it has not been compiled by the Unity toolchain.
- **Shaders** are resolved at runtime via `Shader.Find` (`Standard`, `Unlit/Color`, `Sprites/Default`).
  These resolve in the editor by default. For a stripped device build you may need to add them to
  **Project Settings → Graphics → Always Included Shaders** (Standard is included by default).
- **`ProjectSettings.asset`** is provided with the critical fields set (new Input System, landscape,
  Android/IL2CPP defaults). Unity will fill any remaining defaults on first open.
- **Tuning** lives in `Core/GameConfig.cs` and `Ships/ShipClassDef.cs` so balance is easy to adjust.
- **Glyph→ship** mapping is intentionally light-touch (select + orient nearest friendly ship); it is a
  hardware-only nicety and does not gate any gameplay.
