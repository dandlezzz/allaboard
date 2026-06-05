> Source: https://docs.dev.board.fun/guides/player-management
>
> Cross-SDK guide. Unity code is the primary target; Godot/Web samples retained for reference.

# Player Management

How to read who is playing, react when the roster changes, and ask the OS to add, replace, or reset players. The session model is the same across all three SDKs: the roster is owned by the OS, not the game. Your game reads the roster and asks the OS to change it; it never silently adds or removes players.

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

The OS owns the roster: players are added, replaced, or reset only through the OS selector overlay or a reset call.

Per-SDK difference in how AI surfaces. Unity and Web expose a distinct `AI` player type, so an AI player reports an AI type and carries the index of the registered AI type it was created from. Godot's player type enum is only Profile and Guest: AI players come back tagged as Profile or Guest, and you match them by the name you registered.

### Player id vs session id

- player id is durable. Use it for save game associations, long-term per-player state, and cross-session identity. Guests get a fresh, random player id every session, so never persist data keyed by a Guest's player id.
- session id is ephemeral. Use it for in-game state during the current match and to target a specific player when asking the OS to replace one. Do not persist it.

When loading a save game whose original profile no longer exists, the OS replaces it with a Guest. The Guest inherits the original session id but receives a new player id.

---

## Reading the roster

Every SDK exposes the current players and a count.

Unity (C#):

```csharp
using Board.Session;

// All players in the current session.
BoardSessionPlayer[] players = BoardSession.players;

foreach (var player in players)
{
    Debug.Log($"{player.name}: playerId={player.playerId} sessionId={player.sessionId}");
}
```

Web (JS):

```js
import { Board } from "@board.fun/web-sdk";

if (Board.isOnDevice) {
  const players = Board.session.getPlayers();
  const count = Board.session.getPlayerCount();
  for (const p of players) {
    console.log(`${p.name}: playerId=${p.playerId} sessionId=${p.sessionId}`);
  }
}
```

Godot (GDScript):

```gdscript
if Board.is_on_device:
    var players: Array[BoardPlayer] = Board.session.get_players()
    var count: int = Board.session.get_player_count()
    for p in players:
        print("%s: player_id=%s session_id=%d" % [
            p.display_name, p.player_id, p.session_id])
```

### Player fields

| Meaning | Unity | Godot | Web |
| --- | --- | --- | --- |
| display name | `name` | `display_name` | `name` |
| persistent player id | `playerId` | `player_id` | `playerId` |
| session id | `sessionId` | `session_id` | `sessionId` |
| type | `type` (`BoardPlayerType`) | `type` (`BoardPlayer.Type`) | `type` (`BoardPlayerType`) |
| avatar id | `avatarId` (`string`) | `avatar_id` (`String`) | `avatarId` (`string`) |
| AI type index | `aiTypeIndex` (`-1` if not AI) | not on `BoardPlayer` | `aiTypeIndex` (present only when AI) |

### Branching on player type

Unity (C#):

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

Web (JS):

```js
import { Board, BoardPlayerType } from "@board.fun/web-sdk";

for (const p of Board.session.getPlayers()) {
  switch (p.type) {
    case BoardPlayerType.Profile:
      break;
    case BoardPlayerType.Guest:
      break;
    case BoardPlayerType.AI:
      break;
  }
}
```

Godot (GDScript):

```gdscript
# Godot's BoardPlayer.Type is only PROFILE or GUEST. Use the helpers.
for p in Board.session.get_players():
    if p.is_profile():
        pass
    elif p.is_guest():
        pass
```

---

## The active profile

Distinct from the roster is the system-wide active profile: the Board identity that owns the device right now. The active profile may or may not appear in the roster.

Unity (C#):

```csharp
using Board.Core;
using Board.Session;

BoardPlayer activeProfile = BoardSession.activeProfile; // null if none

// React when the active profile changes.
BoardSession.activeProfileChanged += OnActiveProfileChanged;
```

Web (JS):

```js
const profile = Board.session.getActiveProfile(); // null if none
if (profile) {
  console.log("Active:", profile.name);
}
```

Godot (GDScript):

```gdscript
var active: BoardPlayer = Board.session.get_active_profile() # null if none
print("active profile: %s" % (active.display_name if active != null else "(none)"))

# In Godot the active profile change is folded into players_changed;
# re-read get_active_profile() when that fires.
```

---

## Reacting to roster changes

This is the biggest paradigm difference between the SDKs. Unity raises a C# event. Godot emits a signal. Web has no roster event at all: you re-read the roster after a selector call resolves.

Unity (C#):

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

Web (JS):

```js
// The Web SDK has NO players-changed event. Re-read the roster after a
// selector call resolves (see "Adding players" below).
async function refreshRoster() {
  const players = Board.session.getPlayers();
  renderPlayerList(players);
}
```

Godot (GDScript):

```gdscript
func _ready() -> void:
    if not Board.is_on_device:
        return
    Board.session.players_changed.connect(_on_players_changed)

func _on_players_changed() -> void:
    # The signal carries no payload; re-query get_players().
    var players: Array[BoardPlayer] = Board.session.get_players()
    _refresh_ui(players)
    _validate_game_state(players)
```

---

## Session readiness

On Godot and Web the session manager binds to OS services asynchronously, so the roster can read empty for a moment after startup even though a profile is active. Wait for both readiness checks before treating an empty roster as authoritative.

Unity has no separate readiness call. Its roster is populated by an internal poller and surfaced through `playersChanged`.

Web (JS):

```js
// areServicesReady() is the right runtime gate (not the SDK version).
function rosterIfReady() {
  if (Board.session.isReady() && Board.session.areServicesReady()) {
    return Board.session.getPlayers();
  }
  return [];
}
```

Godot (GDScript):

```gdscript
func _ready() -> void:
    if not Board.is_on_device:
        return
    Board.initialize("00000000-0000-0000-0000-000000000000")
    await _wait_for_session()
    _render_player_list()

func _wait_for_session() -> void:
    while not (Board.session.is_session_ready() and Board.session.are_services_ready()):
        await get_tree().process_frame
```

---

## Loading avatars

Each player carries an avatar id. Unity surfaces the avatar texture directly on the player object and loads it lazily; Godot and Web load it through the avatar module.

Unity (C#):

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

Web (JS):

```js
// forPlayer coerces the string avatarId to a number internally.
// All avatar loads resolve a PNG data URI.
async function loadAvatar(player) {
  const dataUri = await Board.avatar.forPlayer(player);
  imgElement.src = dataUri;
}

async function showDefault() {
  imgElement.src = await Board.avatar.getDefault(); // default = id 0
}
```

Godot (GDScript):

```gdscript
# avatar_id is a String on BoardPlayer; the loader takes an int.
func _load_avatar_for(player: BoardPlayer) -> ImageTexture:
    var tex := await Board.avatar.await_load_avatar(int(player.avatar_id))
    if tex == null:
        tex = await Board.avatar.await_default_avatar() # default = id 0
    return tex
```

Avatars are cached after the first load, and concurrent loads of the same id coalesce into a single fetch. Godot and Web expose a `clear_cache()`/`clearCache()` to drop the decoded textures.

---

## Adding players

The roster is OS-owned, so a game brings in a new player only by opening the OS selector overlay. Unity awaits a `Task`, Web awaits a `Promise`, and Godot returns a request id and emits a completion signal.

Unity (C#):

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

Web (JS):

```js
async function onAddPlayer() {
  const added = await Board.session.presentAddPlayer();
  if (added) {
    refreshRoster(); // no event; re-read after the promise resolves
  }
}
```

Godot (GDScript):

```gdscript
func _on_add_player_pressed() -> void:
    if not Board.is_on_device:
        return
    var rid: int = Board.session.present_add_player()
    if rid < 0:
        return  # selector already open, or off-device
    await Board.session.player_selector_finished
    _refresh_player_list()
```

Only one selector at a time. Opening a second selector while one is in flight fails. Unity throws an `InvalidOperationException`; Godot returns `-1` and emits `player_selector_failed`.

### Godot completion signals

```gdscript
func _on_add_player_pressed() -> void:
    if not Board.is_on_device:
        return
    var rid: int = Board.session.present_add_player()
    if rid < 0:
        return

    # Split success / failure with one-shot connections.
    Board.session.player_selector_completed.connect(_on_added_ok, CONNECT_ONE_SHOT)
    Board.session.player_selector_failed.connect(_on_added_failed, CONNECT_ONE_SHOT)

func _on_added_ok(rid: int) -> void:
    _refresh_player_list()

func _on_added_failed(rid: int, reason: String) -> void:
    push_warning("[session] player selector failed: %s" % reason) # reason == "dismissed" on cancel
```

### Adding a guest

A Guest is an anonymous, session-only player. On Unity and Godot there is no direct "add guest" call: the user picks "Guest" in the OS selector. The Web SDK additionally exposes a direct `addGuest(sessionId)`.

Web (JS):

```js
// Web only: add a Guest directly with a caller-supplied session id.
Board.session.addGuest(nextSessionId);
refreshRoster();
```

---

## Replacing a player

Replace targets one player by session id, opening the OS selector pre-configured to swap that slot.

Unity (C#):

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

Web (JS):

```js
async function onReplace(sessionId: number) {
  const replaced = await Board.session.presentReplacePlayer(sessionId);
  if (replaced) {
    refreshRoster();
  }
}
```

Godot (GDScript):

```gdscript
func _on_replace_pressed(session_id: int) -> void:
    if not Board.is_on_device:
        return
    var rid: int = Board.session.present_replace_player(session_id)
    if rid < 0:
        return
    await Board.session.player_selector_finished
    _refresh_player_list()
```

The replace selector adapts to the roster automatically. When replacing the only Profile in the session, the "Remove player" button and the Guest option are hidden. This is how the OS guarantees the session always keeps at least one Profile.

### Removing a player

On Unity and Godot a game cannot remove a player directly: removal is OS-owned, surfaced through the replace selector or cleared wholesale with a reset. The Web SDK additionally exposes a direct `removePlayer(sessionId)`.

Web (JS):

```js
// Web only: remove a player directly by session id.
Board.session.removePlayer(sessionId);
refreshRoster();
```

---

## AI players

The SDK provides the UI for users to add AI players and tells your game which AI type was chosen. It provides no AI logic. Register the AI difficulty levels or play styles your game supports (maximum of eight); if you register none, no AI option appears.

Unity (C#):

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

Web (JS):

```js
Board.session.setAIPlayerTypes([
  { name: "Easy",   description: "Plays conservatively" },
  { name: "Hard",   description: "Aggressive strategy" },
  { name: "Master", description: "Expert-level play" },
]);
```

Godot (GDScript):

```gdscript
func _ready() -> void:
    if not Board.is_on_device:
        return
    Board.initialize("00000000-0000-0000-0000-000000000000")
    Board.session.set_ai_player_types([
        { "name": "Easy",   "description": "Plays conservatively" },
        { "name": "Hard",   "description": "Aggressive strategy" },
        { "name": "Master", "description": "Expert-level play" },
    ])
```

### Identifying an AI player in the roster

On Unity and Web an AI player reports the `AI` type and carries the index of the registered AI type. On Godot the player type enum has no AI member: match them by the name you registered.

Unity (C#):

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

Web (JS):

```js
import { Board, BoardPlayerType } from "@board.fun/web-sdk";

for (const p of Board.session.getPlayers()) {
  if (p.type === BoardPlayerType.AI) {
    const typeIndex = p.aiTypeIndex; // index into your registered AI types
    configureAiBehaviour(typeIndex);
  }
}
```

Godot (GDScript):

```gdscript
# Godot does not tag AI players as a distinct type; match by the name you
# registered with set_ai_player_types().
for p in Board.session.get_players():
    if p.display_name in ai_difficulty_names:
        _apply_ai_difficulty(p.display_name)
```

### Filtering AI types in the selector

Both the add and replace selectors accept an optional list of AI type indices to restrict the "Add AI" tab to a subset of your registered types.

Unity (C#):

```csharp
// Show only the Easy and Hard options (indices 0 and 1).
await BoardSession.PresentAddPlayerSelector(new int[] { 0, 1 });

// Show all registered AI types (default).
await BoardSession.PresentAddPlayerSelector();
```

Web (JS):

```js
// Show only indices 0 and 1.
const added = await Board.session.presentAddPlayer([0, 1]);

// Show all registered AI types (default).
const addedAll = await Board.session.presentAddPlayer();
```

Godot (GDScript):

```gdscript
# Offer only the first and third registered AI types.
var rid: int = Board.session.present_add_player(PackedInt32Array([0, 2]))

# All registered AI types (default).
var all_rid: int = Board.session.present_add_player()
```

### AI players in save games

AI players are stored in save game metadata like any other player. On Unity, when you load a save the resolved `BoardSaveGamePlayer.aiTypeIndex` preserves which AI type was active. On Godot, loading a save brings the AI player back into the roster tagged as Profile or Guest. See Save Games for the full save/load flow.

---

## Resetting the session

Reset clears every Guest and AI player, leaving only the active Profile. It returns a boolean indicating success.

Unity (C#):

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

Web (JS):

```js
function onReset() {
  const ok = Board.session.resetPlayers();
  if (ok) {
    refreshRoster();
  }
}
```

Godot (GDScript):

```gdscript
func _on_reset_pressed() -> void:
    if not Board.is_on_device:
        return
    var ok: bool = Board.session.reset_players()
    print("[session] reset returned %s" % ok)
```

On Godot, `reset_players()` may not emit `players_changed` if the session is already at the reset state.

---

## Best practices

1. Use the persistent player id for durable data, the session id for in-game state.
2. React through the right channel for your SDK.
3. Treat Guests as ephemeral.
4. Register AI types early.
5. Handle the empty roster gracefully.

---

## See Also

- Players & Sessions — how profiles, guests, and AI players fit together
- Profile Switcher — the OS overlay for swapping the active profile
- Save Games — per-player save game associations
- Pause Menu — system pause screen integration
- Per-SDK API references: Unity, Godot, Web
