> Source: https://docs.dev.board.fun/guides/save-games
>
> Cross-SDK guide. Unity code is the primary target; Godot/Web samples retained for reference.

# Save Games

How to persist and restore game state across launches. Board gives every game a slice of OS-managed storage where it can create, list, load, update, and remove saves: small binary payloads with metadata. The storage model and player-association rules are identical across the three SDKs; only the call shape (async/await vs request-id signals) and a few field names differ.

New to Board's player model? Read Player Management for how the session roster and active profile work, since saves are scoped to players.

---

## The save model

A save is a binary payload plus metadata, owned by the OS and scoped to players, not to the game alone. Three rules hold across every SDK:

- Saves auto-associate with the session's players at creation. You do not pass a player list. Whoever is in the session when you create the save is who the save belongs to.
- Loading a save is not read-only. Board activates the players associated with the save, replacing the session roster to match. If a profile no longer exists, it is replaced with a Guest. Watch your roster after a load.
- There is no direct delete. A game removes its own players from a save; the OS deletes the save once no players remain.

### Metadata fields

| Field | Meaning | Unity | Godot | Web |
| --- | --- | --- | --- | --- |
| id | Stable unique id, used for every later operation | `id` | `id` | `id` |
| description | User-visible label | `description` | `description` | `description` |
| game version | Version that wrote the save | `gameVersion` | `game_version` | `gameVersion` |
| created at | Creation time (epoch ms) | `createdAt` | `created_at` | `createdAt` |
| updated at | Last-update time (epoch ms) | `updatedAt` | `updated_at` | `updatedAt` |
| played time | Game-supplied play time | `playedTime` (seconds) | `played_time` (ms) | `playedTime` (ms) |
| payload size | Payload size in bytes | (use storage info) | `file_size` | `fileSize` |
| player count | Players associated with the save | `playerIds.Length` | `player_count` | `playerCount` |
| players | Resolved player objects | `players` | (not on metadata) | `players` |
| has cover image | Whether a cover exists | `hasCoverImage` | (not on metadata) | `hasCoverImage` |

Mind the played-time unit. Unity's `playedTime` is in seconds; Godot and Web express played time in milliseconds.

### Per-SDK call conventions

| Convention | Unity | Godot | Web |
| --- | --- | --- | --- |
| Async style | `Task` + `await` | request-id `int` + result signal, or `await_*` helper | `Promise` + `await` |
| Payload type | `byte[]` | `PackedByteArray` | `Uint8Array` |
| Create returns | `BoardSaveGameMetadata` | `BoardSaveMetadata` | `BoardSaveGameMetadata` |
| Load method name | `LoadSaveGame` | `load_data` (not `load`) | `load` |
| Field naming | PascalCase | snake_case | camelCase |

Godot names the load method `load_data` because `Board.save.load(...)` would shadow GDScript's `load` built-in. On every SDK, only one save operation should be in flight at a time.

---

## Creating a save

Unity (C#):

```csharp
using Board.Save;

public async void SaveGame()
{
    // Serialize your game state to bytes.
    byte[] payload = SerializeGameState();

    var metadataChange = new BoardSaveGameMetadataChange
    {
        description = "Chapter 3 - The Forest",
        gameVersion = Application.version,
        playedTime  = (ulong)totalPlayTimeSeconds, // seconds
        coverImage  = CaptureScreenshot()          // optional Texture2D
    };

    try
    {
        BoardSaveGameMetadata saved =
            await BoardSaveGameManager.CreateSaveGame(payload, metadataChange);
        Debug.Log($"Save created: {saved.id}");
    }
    catch (System.Exception e)
    {
        Debug.LogError($"Save failed: {e.Message}");
    }
}
```

Web (JS):

```js
import { Board } from "@board.fun/web-sdk";

async function saveGame() {
  const payload: Uint8Array = serializeGameState();
  const playedTimeMs = totalPlayTimeMs; // milliseconds

  try {
    const saved = await Board.save.create(
      "Chapter 3 - The Forest",
      payload,
      playedTimeMs,
      GAME_VERSION
    );
    console.log(`Save created: ${saved.id}`);
  } catch (e) {
    console.error("Save failed", e);
  }
}
```

Godot (GDScript):

```gdscript
func _on_save_pressed() -> void:
    if not Board.is_on_device:
        return
    var data: PackedByteArray = _serialize_game_state()
    var played_ms: int = _total_played_ms() # milliseconds

    var meta: BoardSaveMetadata = await Board.save.await_create(
        "Chapter 3 - The Forest", data, played_ms, GAME_VERSION)
    if meta == null:
        push_warning("[save] create failed")
        return
    _current_save_id = meta.id
    print("[save] created %s (%d bytes)" % [meta.id, meta.file_size])
```

Cover images on create. Only Unity accepts a cover image at create/update time, via `BoardSaveGameMetadataChange.coverImage` (a `Texture2D`, standardized to 432×243 PNG). Godot and Web can read a save's cover back but do not currently expose a write path.

### Serializing game state

Keep a schema version inside the payload so future builds can migrate old saves.

Unity (C#):

```csharp
// Example: JSON via Unity's JsonUtility, then UTF-8 bytes.
byte[] SerializeGameState()
{
    string json = JsonUtility.ToJson(currentState);
    return System.Text.Encoding.UTF8.GetBytes(json);
}

GameState DeserializeGameState(byte[] payload)
{
    string json = System.Text.Encoding.UTF8.GetString(payload);
    return JsonUtility.FromJson<GameState>(json);
}
```

Web (JS):

```js
function serializeGameState(): Uint8Array {
  const state = { level, score, version: 1 /* schema version */ };
  return new TextEncoder().encode(JSON.stringify(state));
}

function deserializeGameState(payload: Uint8Array): GameState {
  return JSON.parse(new TextDecoder().decode(payload));
}
```

Godot (GDScript):

```gdscript
func _serialize_game_state() -> PackedByteArray:
    var state := {
        "level": _level,
        "score": _score,
        "version": 1,  # schema version
    }
    return var_to_bytes(state)

func _deserialize_game_state(bytes: PackedByteArray) -> Dictionary:
    if bytes.is_empty():
        return {}
    return bytes_to_var(bytes)
```

Godot note: `var_to_bytes()` is engine-version-bound. For long-term durability, hand-roll a versioned binary format instead.

---

## Loading a save

Load by id to get the payload back, then deserialize and restore. Remember that loading also activates the save's players.

Unity (C#):

```csharp
public async void LoadGame(string saveId)
{
    try
    {
        byte[] payload = await BoardSaveGameManager.LoadSaveGame(saveId);
        DeserializeGameState(payload);
        Debug.Log("Game loaded");
    }
    catch (System.Exception e)
    {
        Debug.LogError($"Load failed: {e.Message}");
    }
}
```

Web (JS):

```js
async function loadGame(saveId: string) {
  try {
    const payload: Uint8Array = await Board.save.load(saveId);
    deserializeGameState(payload);
    console.log("Game loaded");
  } catch (e) {
    console.error("Load failed", e);
  }
}
```

Godot (GDScript):

```gdscript
func _on_load_pressed(save_id: String) -> void:
    if not Board.is_on_device:
        return
    # Note: load_data, NOT load (load is a GDScript built-in).
    var bytes: PackedByteArray = await Board.save.await_load(save_id)
    if bytes.is_empty():
        push_warning("[save] load failed or empty")
        return
    var state := bytes_to_var(bytes) as Dictionary
    _apply_game_state(state)
    _current_save_id = save_id
```

Loading changes the roster. After a load, the session players reflect the save's associated players. In Unity, subscribe to `BoardSession.playersChanged`; in Godot, listen for `players_changed`. The Web SDK has no players-changed event, so re-read `Board.session.getPlayers()` after the load resolves.

---

## Listing saves

Saves come back sorted by last-updated time, most recent first.

Unity (C#):

```csharp
public async void DisplaySaveSlots()
{
    BoardSaveGameMetadata[] saves =
        await BoardSaveGameManager.GetSaveGamesMetadata();

    foreach (var save in saves)
    {
        Debug.Log($"{save.description}  " +
                  $"updated={save.updatedAt}  " +
                  $"played={save.playedTime}s  " +
                  $"players={save.playerIds.Length}");
    }
}
```

Web (JS):

```js
async function displaySaveSlots() {
  const saves = await Board.save.list();

  for (const save of saves) {
    console.log(
      `${save.description}  updated=${save.updatedAt}  ` +
      `played=${save.playedTime}ms  players=${save.playerCount}`
    );
  }
}
```

Godot (GDScript):

```gdscript
func _refresh_saves_ui() -> void:
    if not Board.is_on_device:
        return
    var saves: Array = await Board.save.await_list()
    _clear_saves_ui()
    for s in saves:
        print("%s  updated=%d  played=%dms  players=%d" % [
            s.description, s.updated_at, s.played_time, s.player_count])
        _add_save_row(s)
```

### The Godot request-id pattern

```gdscript
func _ready() -> void:
    if not Board.is_on_device:
        return
    Board.save.save_listed.connect(_on_listed)
    Board.save.save_failed.connect(_on_failed)
    var rid := Board.save.list()

func _on_listed(rid: int, saves: Array) -> void:
    print("[save] listed %d saves" % saves.size())

func _on_failed(rid: int, error: String) -> void:
    push_error("[save] request %d failed: %s" % [rid, error])
```

---

## Updating a save

Track the current save id and update it rather than creating duplicates.

Unity (C#):

```csharp
public async void UpdateSave(string saveId)
{
    byte[] payload = SerializeGameState();

    var metadataChange = new BoardSaveGameMetadataChange
    {
        description = currentLevelName,
        gameVersion = Application.version,
        playedTime  = (ulong)totalPlayTimeSeconds,
        coverImage  = CaptureScreenshot()
    };

    BoardSaveGameMetadata updated =
        await BoardSaveGameManager.UpdateSaveGame(saveId, payload, metadataChange);
    Debug.Log($"Save updated: {updated.updatedAt}");
}
```

Web (JS):

```js
async function updateSave(saveId: string) {
  const payload = serializeGameState();
  await Board.save.update(
    saveId,
    currentLevelName,
    payload,
    totalPlayTimeMs,
    GAME_VERSION
  );
  console.log("Save updated");
}
```

Godot (GDScript):

```gdscript
func _on_overwrite_save(save_id: String) -> void:
    if not Board.is_on_device:
        return
    var data := _serialize_game_state()
    var played_ms := _total_played_ms()
    var ok: bool = await Board.save.await_update(
        save_id, "Updated %s" % Time.get_time_string_from_system(),
        data, played_ms, GAME_VERSION)
    if not ok:
        push_warning("[save] update failed")
```

Unity's `UpdateSaveGame` resolves the updated metadata; Web's `update` resolves `void`; Godot's `await_update` resolves a `bool`.

---

## Removing players and deleting saves

There is no direct delete on any SDK. To get rid of a save, remove its player associations; the OS deletes the save once none remain.

Unity (C#):

```csharp
// Remove all of this session's players from the save.
// If none remain, the save is deleted automatically.
bool removed = await BoardSaveGameManager.RemovePlayersFromSaveGame(saveId);

// Or remove only the active profile, preserving other players.
await BoardSaveGameManager.RemoveActiveProfileFromSaveGame(saveId);
```

Web (JS):

```js
await Board.save.removePlayersFromSave(saveId);
await Board.save.removeActiveProfileFromSave(saveId);
```

Godot (GDScript):

```gdscript
var removed: bool = await Board.save.await_remove_players(save_id)
await Board.save.await_remove_active_profile(save_id)
```

Deletion is silent and final. Warn the user first: check the player count (Unity `playerIds.Length`, Godot `player_count`, Web `playerCount`) before removing.

---

## Cover images

A save can carry a cover image (432×243, 16:9). All three SDKs can read a save's cover back. Only Unity can write one.

### Reading a cover

Unity (C#):

```csharp
public async void LoadCoverImage(BoardSaveGameMetadata save)
{
    if (!save.hasCoverImage)
    {
        coverImage.texture = defaultCover;
        return;
    }

    Texture2D texture =
        await BoardSaveGameManager.LoadSaveGameCoverImage(save.id);
    coverImage.texture = texture ?? defaultCover;
}
```

Web (JS):

```js
async function loadCover(save: BoardSaveGameMetadata): Promise<string | null> {
  if (!save.hasCoverImage) return null;
  const dataUri = await Board.save.loadCoverImage(save.id);
  imgElement.src = dataUri;
  return dataUri;
}
```

Godot (GDScript):

```gdscript
func _load_cover_for(save_id: String) -> Texture2D:
    var png_bytes: PackedByteArray =
        await Board.save.await_load_cover_image(save_id)
    if png_bytes.is_empty():
        return null
    var img := Image.new()
    if img.load_png_from_buffer(png_bytes) != OK:
        return null
    return ImageTexture.create_from_image(img)
```

### Writing a cover (Unity only)

```csharp
Texture2D CaptureScreenshot()
{
    var rt = new RenderTexture(Screen.width, Screen.height, 24);
    Camera.main.targetTexture = rt;
    Camera.main.Render();

    RenderTexture.active = rt;
    var screenshot = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
    screenshot.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
    screenshot.Apply();

    Camera.main.targetTexture = null;
    RenderTexture.active = null;
    Destroy(rt);
    return screenshot;
}
```

---

## Storage limits

The authoritative values come from the OS at runtime through sync getters: do not hardcode them. The defaults today are 16 MB per save, 64 MB total per app, and a 100-character description.

Unity (C#):

```csharp
public async void CheckStorage()
{
    BoardAppStorageInfo info =
        await BoardSaveGameManager.GetAppStorageInfo();

    Debug.Log($"Used {info.usedStorage} / {info.totalStorage} bytes " +
              $"({info.usagePercentage:P0})"); // usagePercentage is 0.0..1.0

    long maxPayload  = BoardSaveGameManager.GetMaxPayloadSize();
    long maxStorage  = BoardSaveGameManager.GetMaxAppStorage();
    int  maxDescLen  = BoardSaveGameManager.GetMaxSaveDescriptionLength();
}
```

Web (JS):

```js
function checkStorage() {
  const info = Board.save.getAppStorageInfo();
  console.log(
    `Used ${info.usedStorage} / ${info.totalStorage} bytes ` +
    `(${Math.round(info.usagePercentage * 100)}%)`
  );

  const maxData = Board.save.getMaxDataSize();
  const maxApp  = Board.save.getMaxAppStorageSize();
  const maxDesc = Board.save.getMaxDescriptionLength();
}
```

Godot (GDScript):

```gdscript
func _check_storage() -> void:
    var info: Dictionary = Board.save.get_app_storage_info()
    print("Used %d / %d bytes (%.0f%%)" % [
        info.used_storage, info.total_storage,
        info.usage_percentage * 100.0])

    var max_data := Board.save.get_max_data_size()
    var max_app  := Board.save.get_max_app_storage_size()
    var max_desc := Board.save.get_max_description_length()
```

The storage-info `usagePercentage` (Web/Unity) and `usage_percentage` (Godot) is a fraction from 0.0 to 1.0, not a 0–100 percentage.

Check your payload against the limit before saving:

Unity (C#):

```csharp
public async void OnSavePressed()
{
    byte[] payload = SerializeGameState();
    if (payload.Length > BoardSaveGameManager.GetMaxPayloadSize())
    {
        ShowStorageFullDialog();
        return;
    }
    await BoardSaveGameManager.CreateSaveGame(payload, BuildMetadataChange());
}
```

---

## Player associations

Saves are linked to the players that were in the session when they were created.

### Finding saves for the active profile

Unity (C#):

```csharp
using System.Linq;

public async Task<BoardSaveGameMetadata[]> GetSavesForActiveProfile()
{
    var allSaves  = await BoardSaveGameManager.GetSaveGamesMetadata();
    var activeId  = BoardSession.activeProfile?.playerId;

    if (string.IsNullOrEmpty(activeId))
        return allSaves;

    return allSaves
        .Where(s => s.playerIds.Contains(activeId))
        .ToArray();
}
```

Web (JS):

```js
async function getSavesForActiveProfile() {
  const allSaves = await Board.save.list();
  const active   = Board.session.getActiveProfile();
  if (!active) return allSaves;

  return allSaves.filter(s =>
    s.players.some(p => p.playerId === active.playerId)
  );
}
```

Godot (GDScript):

```gdscript
# Godot metadata exposes player_count, not individual ids.
func _saves_with_players() -> Array:
    var profile: BoardPlayer = Board.session.get_active_profile()
    if profile == null:
        return []
    var all: Array = await Board.save.await_list()
    return all.filter(func(s): return s.player_count > 0)
```

### Getting unique players across saves

Unity and Web ship a helper that dedupes profile players across a set of saves (guests and AI excluded).

Unity (C#):

```csharp
var saves = await BoardSaveGameManager.GetSaveGamesMetadata();
var uniquePlayers = saves.GetUniquePlayers(); // extension, profiles only

foreach (var player in uniquePlayers)
    Debug.Log($"Player in saves: {player.name}");
```

Web (JS):

```js
const saves = await Board.save.list();
const uniquePlayers = Board.save.getUniquePlayers(saves); // profiles only

for (const player of uniquePlayers) {
  console.log(`Player in saves: ${player.name}`);
}
```

---

## Save and Quit integration

When the player picks Save and Quit from the pause screen, save the current state, then quit. Create on the first save, update afterward.

Unity (C#):

```csharp
void OnEnable()
{
    BoardApplication.pauseScreenActionReceived += OnPauseAction;
}

async void OnPauseAction(BoardPauseAction action, BoardPauseAudioTrack[] tracks)
{
    if (action == BoardPauseAction.ExitGameSaved)
    {
        await SaveCurrentGame();
        BoardApplication.Exit();
    }
}

async Task SaveCurrentGame()
{
    byte[] payload = SerializeGameState();
    var change = BuildMetadataChange();
    if (string.IsNullOrEmpty(_currentSaveId))
    {
        var meta = await BoardSaveGameManager.CreateSaveGame(payload, change);
        _currentSaveId = meta.id;
    }
    else
    {
        await BoardSaveGameManager.UpdateSaveGame(_currentSaveId, payload, change);
    }
}
```

See Pause Menu for the full pause-flow context and the action constants.

---

## Best practices

1. Await each operation before starting the next.
2. Check storage before saving.
3. Handle the roster change after loading.
4. Warn before removing the last player.
5. Update, do not duplicate.
6. Stamp the game version on every write.

## See Also

- Player Management — the session roster and active profile that saves are scoped to
- Pause Menu — the Save and Quit flow
- Profile Switcher — reloading saves when the active profile changes
- Per-SDK API references: Unity, Godot, Web
