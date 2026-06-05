> Source: https://docs.dev.board.fun/guides/player-management — fetched 2026-06-04T18:38 (UTC-7)

# Player Management

How to read who is playing, react when the roster changes, and ask the OS to add, replace, or reset players. The session model is the same across all three SDKs: the roster is owned by the OS, not the game. Your game reads the roster and asks the OS to change it; it never silently adds or removes players. This guide covers the shared model and shows the per-engine API side by side.

New to Board's player model? Read Players & Sessions for how profiles, guests, and AI players fit together, and Profile Switcher for the OS overlay that swaps the active profile.

---

## The session model

A Board session is the set of players currently in your game. Every SDK exposes the same logical pieces:

| Concept | Meaning |
| --- | --- |
| session roster | The list of players in the game right now (one or more) |
| active profile | The system-wide Board profile that owns the device, distinct from the roster |
| player | One participant: a Profile, a Guest, or an AI player |
| player id | Persistent, app-specific identifier for a Profile, stable across sessions and reboots |
| session id | Identifier valid only for the current session, used to target a specific player |

At launch the roster contains the active profile. The session always requires at least one Profile player. This constraint is enforced by the OS and cannot be bypassed: the OS selector hides the "remove" and "guest" options when only one Profile remains.

### Player types

| Type | Meaning |
| --- | --- |
| Profile | A persistent Board identity (name, avatar, durable id) |
| Guest | A temporary player that exists only for the current session |
| AI | A game-controlled player chosen from the AI types your game registers |

The OS owns the roster: players are added, replaced, or reset only through the OS selector overlay or a reset call. A game cannot mutate who is playing on its own.

Per-SDK difference in how AI surfaces. Unity and Web expose a distinct `AI` player type, so an AI player reports an AI type and carries the index of the registered AI type it was created from. Godot's player type enum is only Profile and Guest: AI players come back tagged as Profile or Guest, and you match them by the name you registered.

### Player id vs session id

- player id is durable. Use it for save game associations, long-term per-player state (high scores, unlocks, preferences), and cross-session identity. Guests get a fresh, random player id every session, so never persist data keyed by a Guest's player id.
- session id is ephemeral. Use it for in-game state during the current match and to target a specific player when asking the OS to replace one. Do not persist it: it has no meaning across launches.

When loading a save game whose original profile no longer exists, the OS replaces it with a Guest. The Guest inherits the original session id but receives a new player id, preserving in-game state keyed by session id while breaking persistent associations.

---

## Reading the roster

Every SDK exposes the current players and a count. The shapes differ per engine, but the fields carry the same meaning: a player id, a session id, a display name, a type, and an avatar id.

### Unity (C#)

```csharp
using Board.Session;

// All players in the current session.
BoardSessionPlayer[] players = BoardSession.players;

foreach (var player in players)
{
    Debug.Log($"{player.name}: playerId={player.playerId} sessionId={player.sessionId}");
}
```

### Player fields

The field names follow each engine's naming convention, but they map one to one:

| Meaning | Unity | Godot | Web |
| --- | --- | --- | --- |
| display name | `name` | `display_name` | `name` |
| persistent player id | `playerId` | `player_id` | `playerId` |
| session id | `sessionId` | `session_id` | `sessionId` |
| type | `type` (`BoardPlayerType`) | `type` (`BoardPlayer.Type`) | `type` (`BoardPlayerType`) |
| avatar id | `avatarId` (`string`) | `avatar_id` (`String`) | `avatarId` (`string`) |
| AI type index | `aiTypeIndex` (`-1` if not AI) | not on `BoardPlayer` | `aiTypeIndex` (present only when AI) |

### Branching on player type

```csharp
using Board.Core;
using Board.Session;

foreach (var player in BoardSession.players)
{
    switch (player.type)
    {
        case BoardPlayerType.Profile:
            // Persistent identity stored on Board
            break;
        case BoardPlayerType.Guest:
            // Temporary player for this session only
            break;
        case BoardPlayerType.AI:
            // Game-controlled AI player
            break;
    }
}
```

---

## The active profile

Distinct from the roster is the system-wide active profile: the Board identity that owns the device right now. The active profile is one specific player; the roster is the full list of who is playing your game. Use the active profile to surface "your saves" by default, greet the player on the title screen, or decide which progression state to load. The active profile may or may not appear in the roster.

```csharp
using Board.Core;
using Board.Session;

BoardPlayer activeProfile = BoardSession.activeProfile; // null if none

// React when the active profile changes.
BoardSession.activeProfileChanged += OnActiveProfileChanged;
```

---

## Reacting to roster changes

This is the biggest paradigm difference between the SDKs. Unity raises a C# event. Godot emits a signal. Web has no roster event at all: you re-read the roster after a selector call resolves.

```csharp
using Board.Session;

void Start()
{
    BoardSession.playersChanged += OnPlayersChanged;
}

void OnDestroy()
{
    BoardSession.playersChanged -= OnPlayersChanged;
}

void OnPlayersChanged()
{
    // Re-read BoardSession.players and refresh your UI / game state.
    RefreshPlayerDisplay();
    ValidateGameState();
}
```

The Web SDK does not expose a roster-changed callback. Because the only ways the roster mutates are the OS selector and reset (all driven by your own calls), re-reading `getPlayers()` immediately after those calls resolve is sufficient.

---

## Session readiness

On Godot and Web the session manager binds to OS services asynchronously, so the roster can read empty for a moment after startup even though a profile is active. Wait for both readiness checks before treating an empty roster as authoritative.

Unity has no separate readiness call. Its roster is populated by an internal poller and surfaced through `playersChanged`, so you subscribe to that event rather than gating on a readiness flag.

---

## Loading avatars

Each player carries an avatar id. How you turn that into a displayable image differs per engine: Unity surfaces the avatar texture directly on the player object and loads it lazily; Godot and Web load it through the avatar module, passing the avatar id (coerced to a number).

```csharp
using Board.Core;
using Board.Session;

void DisplayPlayer(BoardSessionPlayer player)
{
    // avatar lazy-loads on first access; may be null until ready.
    playerImage.texture = player.avatar;
    player.avatarLoaded += OnAvatarLoaded;
}

void OnAvatarLoaded(BoardPlayer player)
{
    playerImage.texture = player.avatar;
}

// For players not in the session (e.g. save-game UI), use the default avatar.
async void ShowDefault()
{
    Texture2D defaultAvatar = await BoardPlayer.GetDefaultAvatar();
    unknownPlayerImage.texture = defaultAvatar;
}
```

Avatars are cached after the first load, and concurrent loads of the same id coalesce into a single fetch, so you can call these freely from your render path. Godot and Web expose a `clear_cache()`/`clearCache()` to drop the decoded textures if you need the memory back.

---

## Adding players

The roster is OS-owned, so a game brings in a new player only by opening the OS selector overlay. The user picks a Profile, a Guest, or an AI type from the picker, and the selection lands back in your game. How the result is delivered is the per-engine difference: Unity awaits a `Task<bool>`, Web awaits a `Promise<boolean>`, and Godot returns a request id and emits a completion signal.

```csharp
using System;
using Board.Session;

public async void OnAddPlayerButtonPressed()
{
    try
    {
        bool added = await BoardSession.PresentAddPlayerSelector();
        if (added)
        {
            // playersChanged also fires with the updated list.
            Debug.Log("New player added to session");
        }
        else
        {
            Debug.Log("Player selector dismissed");
        }
    }
    catch (InvalidOperationException e)
    {
        Debug.LogError($"Failed to present player selector: {e.Message}");
    }
}
```

Only one selector at a time. Opening a second selector while one is in flight fails. Unity throws an `InvalidOperationException`; Godot returns `-1` and emits `player_selector_failed`. Gate the buttons that open the selector while one is in flight.

### Adding a guest

A Guest is an anonymous, session-only player. On Unity and Godot there is no direct "add guest" call: the user picks "Guest" in the OS selector opened by the add-player flow above, and the new Guest appears in the roster with the OS allocating its session id. The Web SDK additionally exposes a direct `addGuest(sessionId)` that adds a Guest with the session id you supply, bypassing the selector.

---

## Replacing a player

Replace targets one player by session id, opening the OS selector pre-configured to swap that slot. The user picks a different Profile or Guest, or dismisses to keep the current player.

```csharp
using System;
using Board.Session;

public async void OnReplacePlayerPressed(BoardSessionPlayer player)
{
    try
    {
        bool replaced = await BoardSession.PresentReplacePlayerSelector(player);
        if (replaced)
        {
            Debug.Log("Player replaced or removed");
        }
    }
    catch (InvalidOperationException e)
    {
        Debug.LogError($"Failed to replace player: {e.Message}");
    }
}
```

The replace selector adapts to the roster automatically. When replacing the only Profile in the session, the "Remove player" button and the Guest option are hidden: that player can only be replaced with another Profile. When there are multiple Profiles, "Remove player" appears and the Guest option is available alongside other Profiles. This is how the OS guarantees the session always keeps at least one Profile.

### Removing a player

On Unity and Godot a game cannot remove a player directly: removal is OS-owned, surfaced through the replace selector (when more than one Profile is present) or cleared wholesale with a reset. The Web SDK additionally exposes a direct `removePlayer(sessionId)`.

---

## AI players

The SDK provides the UI for users to add AI players and tells your game which AI type was chosen. It provides no AI logic: all decision-making lives in your game code. Register the AI difficulty levels or play styles your game supports, and the OS surfaces them in the selector's "Add AI" tab alongside Profiles and Guests. Each AI type is a name plus an optional one-line description. Register a maximum of eight; if you register none, no AI option appears.

```csharp
using Board.Session;

void Start()
{
    BoardSession.SetAIPlayerTypes(new BoardAIPlayerType[]
    {
        new BoardAIPlayerType { name = "Easy",   description = "Plays conservatively" },
        new BoardAIPlayerType { name = "Hard",   description = "Aggressive strategy" },
        new BoardAIPlayerType { name = "Master", description = "Expert-level play" },
    });
}
```

### Identifying an AI player in the roster

This is where the type model diverges. On Unity and Web an AI player reports the `AI` type and carries the index of the registered AI type it was created from, so you read that index to pick the matching behavior. On Godot the player type enum has no AI member: AI players come back as Profile or Guest, so you match them by the name you registered.

```csharp
using Board.Core;
using Board.Session;

foreach (var player in BoardSession.players)
{
    if (player.type == BoardPlayerType.AI)
    {
        int typeIndex = player.aiTypeIndex; // 0 = Easy, 1 = Hard, 2 = Master
        ConfigureAiBehaviour(typeIndex);
    }
}
// For non-AI players, aiTypeIndex is -1.
```

### Filtering AI types in the selector

Both the add and replace selectors accept an optional list of AI type indices to restrict the "Add AI" tab to a subset of your registered types. An empty or omitted list offers all of them. This is useful when certain AI types should only be available in specific game modes. The indices correspond to the order you passed when registering.

```csharp
// Show only the Easy and Hard options (indices 0 and 1).
await BoardSession.PresentAddPlayerSelector(new int[] { 0, 1 });

// Show all registered AI types (default).
await BoardSession.PresentAddPlayerSelector();
```

### AI players in save games

AI players are stored in save game metadata like any other player. On Unity, when you load a save the resolved `BoardSaveGamePlayer.aiTypeIndex` preserves which AI type was active, so restore the matching behavior for that slot. On Godot, loading a save brings the AI player back into the roster tagged as Profile or Guest, so match it by the name you registered. See Save Games for the full save/load flow.

---

## Resetting the session

Reset clears every Guest and AI player, leaving only the active Profile. It returns a boolean indicating success.

```csharp
public void OnResetPlayersPressed()
{
    bool success = BoardSession.ResetPlayers();
    if (success)
    {
        Debug.Log("Players reset to initial state");
    }
}
```

---

## Best practices

1. Use the persistent player id for durable data, the session id for in-game state. Mixing them up breaks when a player rejoins later under a new session id.
2. React through the right channel for your SDK. Subscribe to `playersChanged` (Unity) or `players_changed` (Godot); on Web, re-read the roster after each selector or reset call resolves.
3. Treat Guests as ephemeral. Never key persistent data on a Guest's player id: it is regenerated every session.
4. Register AI types early, before presenting the selector, so the AI options are available from the start.
5. Handle the empty roster gracefully. On Godot and Web the roster can read empty until services are ready, and every SDK reads empty off-device. Render an off-device or loading state rather than crashing.

---

## See Also

- Players & Sessions — how profiles, guests, and AI players fit together
- Profile Switcher — the OS overlay for swapping the active profile
- Save Games — per-player save game associations
- Pause Menu — system pause screen integration
- Per-SDK API references: Unity, Godot, Web
