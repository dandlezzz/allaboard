> Source: https://docs.dev.board.fun/guides/save-games — fetched 2026-06-04T18:38 (UTC-7)

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
| id | Stable unique id | `id` | `id` | `id` |
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
| Async style | `Task<T>` + `await` | request-id `int` + result signal, or `await_*` helper | `Promise<T>` + `await` |
| Payload type | `byte[]` | `PackedByteArray` | `Uint8Array` |
| Create returns | `BoardSaveGameMetadata` | `BoardSaveMetadata` | `BoardSaveGameMetadata` |
| Load method name | `LoadSaveGame` | `load_data` (not `load`) | `load` |
| Field naming | PascalCase | snake_case | camelCase |

On every SDK, only one save operation should be in flight at a time: await the current one before starting the next.

---

## Creating a save

Serialize your game state to bytes, then create the save with a description, the payload, the played time, and your game version. The save auto-associates with the players currently in the session.

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

Cover images on create. Only Unity accepts a cover image at create/update time, via `BoardSaveGameMetadataChange.coverImage` (a `Texture2D`, standardized to 432×243 PNG). Godot and Web can read a save's cover back but do not currently expose a write path.

### Serializing game state

The payload is just bytes; how you produce them is up to you. Keep a schema version inside the payload so future builds can migrate old saves.

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

---

## Loading a save

Load by id to get the payload back, then deserialize and restore. Remember that loading also activates the save's players: your session roster changes to match the save.

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

Loading changes the roster. After a load, the session players reflect the save's associated players. A missing profile is replaced with a Guest that keeps the original session slot but gets a fresh player id. In Unity, subscribe to `BoardSession.playersChanged`.

---

## Listing saves

Get all saves for the current app. Each SDK returns metadata only (cheap); the payload is fetched separately with a load. Saves come back sorted by last-updated time, most recent first.

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

(Godot's underlying model returns a request-id `int` immediately and delivers the result on a typed signal carrying that id; the `await_*` helpers wrap this. Every Godot save operation emits `save_failed(rid, error)` on failure.)

---

## Updating a save

Overwrite an existing save by id with a new payload and metadata. Track the current save id and update it rather than creating duplicates.

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

Unity's `UpdateSaveGame` resolves the updated metadata; Web's `update` resolves `void`; Godot's `await_update` resolves a `bool`.

---

## Removing players and deleting saves

There is no direct delete on any SDK. To get rid of a save, remove its player associations; the OS deletes the save once none remain.

```csharp
// Remove all of this session's players from the save.
// If none remain, the save is deleted automatically.
bool removed = await BoardSaveGameManager.RemovePlayersFromSaveGame(saveId);

// Or remove only the active profile, preserving other players.
await BoardSaveGameManager.RemoveActiveProfileFromSaveGame(saveId);
```

Deletion is silent and final. When the last player is removed, the save is deleted with no confirmation and no recovery. Check the player count (Unity `playerIds.Length`) before removing.

---

## Cover images

A save can carry a cover image (432×243, 16:9). All three SDKs can read a save's cover back. Only Unity can write one.

### Reading a cover

```csharp
public async void LoadCoverImage(BoardSaveGameMetadata save)
{
    if (!save.hasCoverImage)
    {
        coverImage.texture = defaultCover;
        return;
    }

    // Returns a Texture2D, or null if the save has no cover.
    Texture2D texture =
        await BoardSaveGameManager.LoadSaveGameCoverImage(save.id);
    coverImage.texture = texture ?? defaultCover;
}
```

### Writing a cover (Unity only)

Capture a clean game frame before any pause overlay or dimming renders, then hand the texture to the metadata-change object on create or update. The SDK scales and converts it to a 432×243 PNG.

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

Board enforces a per-app storage budget and per-save limits. The authoritative values come from the OS at runtime through sync getters: do not hardcode them. The defaults today are 16 MB per save, 64 MB total per app, and a 100-character description.

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

Check your payload against the limit before saving:

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

The storage-info `usagePercentage` is a fraction from 0.0 to 1.0, not a 0–100 percentage. Multiply by 100 before showing a percent.

---

## Player associations

Saves are linked to the players that were in the session when they were created. This lets you filter saves by the active profile and build "who has played" views.

### Finding saves for the active profile

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

### Getting unique players across saves

Unity and Web ship a helper that dedupes profile players across a set of saves (guests and AI excluded).

```csharp
var saves = await BoardSaveGameManager.GetSaveGamesMetadata();
var uniquePlayers = saves.GetUniquePlayers(); // extension, profiles only

foreach (var player in uniquePlayers)
    Debug.Log($"Player in saves: {player.name}");
```

---

## Save and Quit integration

When the player picks Save and Quit from the pause screen, you get one window to persist before the app terminates. Save the current state, then quit. Create on the first save, update afterward.

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
3. Handle the roster change after loading (Unity: subscribe to `playersChanged`).
4. Warn before removing the last player. No-player saves are deleted silently with no recovery.
5. Update, do not duplicate. Track the current save id.
6. Stamp the game version on every write.

## See Also

- Player Management — the session roster and active profile that saves are scoped to
- Pause Menu — the Save and Quit flow
- Profile Switcher — reloading saves when the active profile changes
- Per-SDK API references: Unity, Godot, Web
