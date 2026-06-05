> Source: https://docs.dev.board.fun/guides/pause-menu
>
> Cross-SDK guide. Unity code is the primary target; Godot/Web samples retained for reference.

# Pause Menu

Board ships a system pause menu that BoardOS renders on top of your game. The OS owns the menu button and every pixel of the menu UI; your game supplies the context the menu shows (its title, optional custom buttons, optional audio sliders) and reacts to whatever the player picks. This is consistent across all three SDKs: the configuration and result shapes are the same logical model, only the per-engine call style differs (Unity sets context on a static class and receives results as events, Godot pushes a Dictionary and listens on a signal, Web passes a typed object and subscribes a callback).

New to Board's overlays? The pause menu is one of three OS-owned overlays, alongside the Player Management selector and the Profile Switcher. They all follow the same "the OS renders it, your game describes it" pattern.

---

## How the pause menu works

The pause menu is a pass-through system. BoardOS renders the UI and captures the player's taps; your game owns all the logic. The menu always has these built-in actions:

| Action | When shown | Your responsibility |
| --- | --- | --- |
| Resume | Always | Unpause gameplay (restore your timescale, resume audio) |
| Exit to Library | Always | Do any cleanup, then quit the app |
| Exit & Save | Only when you opt in | Save the game, then quit the app |

You cannot remove or restyle the built-in actions. You can add your own custom buttons (Restart, How to Play, anything your game needs) and audio sliders alongside them.

The game is responsible for everything behind the menu:

1. Register a context before the player can pause. If no context is set, the system menu button has nothing to open and the tap looks like a no-op.
2. Pause gameplay while the overlay is open. BoardOS does not pause your game loop for you.
3. Resume gameplay when the player taps Resume.
4. Save the game when the player taps Exit & Save (Board never saves on your behalf).
5. Quit the app when the player exits.
6. Apply the returned audio values to your audio system.

You do not manage the menu button's visibility. The OS shows the system menu button automatically on resume and hides it across the activity lifecycle. Your only job is to register a context so the button has something to open.

### Per-SDK shape of the API

| | Unity | Godot | Web |
| --- | --- | --- | --- |
| Set context | `SetPauseScreenContext(...)` (named params) | `set_context(Dictionary)` | `setContext(BoardPauseContext)` |
| Partial update | `UpdatePauseScreenContext(...)` | `update_audio_tracks(Array)` (audio only) | `updateContext(Partial<...>)` |
| Clear | `ClearPauseScreenContext()` | `clear_context()` | `clearContext()` |
| Result delivery | C# events | `pause_result_received` signal | `onResult(cb)` callback |
| Action type | `BoardPauseAction` enum | `ACTION_*` string constants | plain string |
| Icon type | `BoardPauseButtonIcon` enum | `ICON_*` string constants | plain string |
| Quit the app | `BoardApplication.Exit()` | `Board.application.quit()` | `Board.application.quit()` |

One difference worth calling out before any code: the Godot and Web SDKs require a device-gating check before touching the API (Web throws off-device, Godot no-ops), and Godot requires a one-time `Board.initialize(app_id)`. Unity has no init call and no on-device gate around the pause API: `BoardSupport.enabled` exists for input but the pause calls are safe to make unconditionally.

---

## Basic setup

The minimum viable pause flow: register a context with your game name and the save option, then handle the result.

Unity (C#):

```csharp
using Board.Core;

void Start()
{
    // Configure the menu. All parameters are optional.
    BoardApplication.SetPauseScreenContext(
        applicationName: "My Game",
        showSaveOptionUponExit: true
    );

    // Results arrive as events.
    BoardApplication.pauseScreenActionReceived += OnPauseAction;
    BoardApplication.customPauseScreenButtonPressed += OnCustomButton;
}

void OnDestroy()
{
    BoardApplication.pauseScreenActionReceived -= OnPauseAction;
    BoardApplication.customPauseScreenButtonPressed -= OnCustomButton;
}

void OnPauseAction(BoardPauseAction action, BoardPauseAudioTrack[] audioTracks)
{
    switch (action)
    {
        case BoardPauseAction.Resume:
            ResumeGameplay();
            break;
        case BoardPauseAction.ExitGameUnsaved:
            BoardApplication.Exit();
            break;
        case BoardPauseAction.ExitGameSaved:
            // Save first (see "Save and quit" below), then exit.
            break;
    }
}

void OnCustomButton(string customButtonId, BoardPauseAudioTrack[] audioTracks)
{
    // Dispatch your own buttons here (see "Custom buttons").
}
```

Web (JS):

```js
import { Board, type BoardPauseResult } from "@board.fun/web-sdk";

if (Board.isOnDevice) {
  Board.pause.setContext({
    gameName: "My Game",
    offerSaveOption: true,
  });

  const unsubscribe = Board.pause.onResult((result: BoardPauseResult) => {
    switch (result.action) {
      case "resume":
        resumeGameplay();
        break;
      case "quit":
        Board.application.quit();
        break;
      case "save_and_quit":
        break;
      case "custom_button":
        break;
    }
  });
}
```

Godot (GDScript):

```gdscript
func _ready() -> void:
    if not Board.is_on_device:
        return
    Board.initialize("00000000-0000-0000-0000-000000000000")

    Board.pause.set_context({
        "game_name": "My Game",
        "offer_save_option": true,
    })

    Board.pause.pause_result_received.connect(_on_pause_result)

func _on_pause_result(result: BoardPauseResult) -> void:
    match result.action:
        Board.pause.ACTION_RESUME:
            pass
        Board.pause.ACTION_QUIT:
            Board.application.quit()
        Board.pause.ACTION_SAVE_AND_QUIT:
            pass
        Board.pause.ACTION_CUSTOM_BUTTON:
            pass
```

Register the context early. Set it during your scene's initialization, before the player can tap the system menu button. Without a context the button is live but the tap does nothing because the OS has nothing to display.

---

## The context

A pause context has the same logical fields in every SDK. Only the casing and the call shape change.

| Field | Unity | Godot | Web | Meaning |
| --- | --- | --- | --- | --- |
| App/game name | `applicationName` | `game_name` | `gameName` | Title in the menu header |
| Save on exit | `showSaveOptionUponExit` | `offer_save_option` | `offerSaveOption` | Show the Exit & Save action |
| Custom buttons | `customButtons` | `custom_buttons` | `customButtons` | Your extra action buttons |
| Audio sliders | `audioTracks` | `audio_tracks` | `audioTracks` | Volume sliders |

Godot and Web also have an app/game id field (`game_id`/`gameId`). It is normally omitted: Godot derives it from `Board.initialize()`, and on Web it is optional. Unity has no game-id field on the context.

Every field is optional. In Unity, unspecified named parameters fall back to defaults; in Web, keys you omit keep their previous value (for `updateContext`) or reset to defaults (for a fresh `setContext`). In Godot, `set_context` always replaces and sends only the keys you pass, so restate the full context.

---

## Custom buttons

Add game-specific actions alongside the built-in ones. Each custom button carries a stable id (returned to you when tapped), display text, and an optional icon.

Unity (C#):

```csharp
using Board.Core;

var customButtons = new BoardPauseCustomButton[]
{
    new BoardPauseCustomButton("restart", "Restart Level", BoardPauseButtonIcon.CircularArrow),
    new BoardPauseCustomButton("help", "How to Play", BoardPauseButtonIcon.Square),
    new BoardPauseCustomButton("info", "About") // no icon (defaults to None)
};

BoardApplication.SetPauseScreenContext(
    applicationName: "My Game",
    showSaveOptionUponExit: true,
    customButtons: customButtons
);
```

Web (JS):

```js
Board.pause.setContext({
  gameName: "My Game",
  offerSaveOption: true,
  customButtons: [
    { id: "restart", title: "Restart Level", icon: "circulararrow" },
    { id: "help",    title: "How to Play",   icon: "square" },
    { id: "info",    title: "About",         icon: "" }, // no icon
  ],
});
```

Godot (GDScript):

```gdscript
Board.pause.set_context({
    "game_name": "My Game",
    "offer_save_option": true,
    "custom_buttons": [
        { "id": "restart", "title": "Restart Level", "icon": Board.pause.ICON_CIRCULAR_ARROW },
        { "id": "help",    "title": "How to Play",   "icon": Board.pause.ICON_SQUARE },
        { "id": "info",    "title": "About",         "icon": Board.pause.ICON_NONE },
    ],
})
```

Field names per SDK. Unity's button text field is `text`; Godot and Web call it `title`. The id field is `id` everywhere.

Length limits. Across all SDKs, the id must be at most 64 characters and the label at most 128 characters.

Layout. Custom buttons fill rows of two. An even count gives all half-width buttons; an odd count promotes the trailing button to a full-width row.

### Available icons

Every SDK exposes the same five icon options.

| Suggested use | Unity (`BoardPauseButtonIcon`) | Godot (`Board.pause.*`) | Web (string) |
| --- | --- | --- | --- |
| No icon | `None` | `ICON_NONE` | `""` |
| Restart, retry, replay | `CircularArrow` | `ICON_CIRCULAR_ARROW` | `"circulararrow"` |
| Exit, leave, quit | `DoorWithArrow` | `ICON_DOOR_WITH_ARROW` | `"doorwitharrow"` |
| Back, previous | `LeftArrow` | `ICON_LEFT_ARROW` | `"leftarrow"` |
| Stop, generic action | `Square` | `ICON_SQUARE` | `"square"` |

### Handling a custom button

When the player taps a custom button, you get its id back. Unity delivers it on a dedicated event; Godot and Web deliver it through the single result handler.

Unity (C#):

```csharp
void OnCustomButton(string customButtonId, BoardPauseAudioTrack[] audioTracks)
{
    switch (customButtonId)
    {
        case "restart":
            RestartLevel();
            break;
        case "help":
            ShowHelpScreen();
            break;
    }

    // Audio values come back here too; apply them (see "Audio sliders").
    ApplyAudioSettings(audioTracks);
}
```

Web (JS):

```js
Board.pause.onResult((result) => {
  if (result.action === "custom_button") {
    switch (result.customButtonId) {
      case "restart":
        restartLevel();
        break;
      case "help":
        showHelpScreen();
        break;
    }
  }
});
```

Godot (GDScript):

```gdscript
func _on_pause_result(result: BoardPauseResult) -> void:
    if result.action == Board.pause.ACTION_CUSTOM_BUTTON:
        match result.custom_button_id:
            "restart":
                _restart_level()
            "help":
                _show_help_screen()
```

---

## Audio sliders

The pause menu can host volume sliders, one per audio track. The player adjusts them; the new values come back to you with the result, every time the menu is dismissed, regardless of which action was picked. Each track has a stable id, a display name, and an integer value from 0 to 100.

Unity (C#):

```csharp
using Board.Core;

var audioTracks = new BoardPauseAudioTrack[]
{
    new BoardPauseAudioTrack { id = "music", name = "Music", value = 80 },
    new BoardPauseAudioTrack { id = "sfx", name = "Sound Effects", value = 90 },
    new BoardPauseAudioTrack { id = "voice", name = "Voice", value = 100 }
};

BoardApplication.SetPauseScreenContext(
    applicationName: "My Game",
    audioTracks: audioTracks
);
```

Web (JS):

```js
Board.pause.setContext({
  gameName: "My Game",
  audioTracks: [
    { id: "music", name: "Music",         value: 80 },
    { id: "sfx",   name: "Sound Effects", value: 90 },
    { id: "voice", name: "Voice",         value: 100 },
  ],
});
```

Godot (GDScript):

```gdscript
Board.pause.set_context({
    "game_name": "My Game",
    "audio_tracks": [
        { "id": "music", "name": "Music",         "value": 80 },
        { "id": "sfx",   "name": "Sound effects", "value": 90 },
        { "id": "voice", "name": "Voice",         "value": 100 },
    ],
})
```

Fields. `id` (max 64 chars), `name` label (max 128 chars), and `value` (integer 0 to 100; out-of-range values are clamped by the native side).

### Applying the returned values

Apply the audio values first in your result handler, before branching on the action, since they come back no matter what the player picked.

Unity (C#):

```csharp
void ApplyAudioSettings(BoardPauseAudioTrack[] audioTracks)
{
    foreach (var track in audioTracks)
    {
        // Map track.id to your mixer group and set the volume from track.value (0..100).
        SetMixerVolume(track.id, track.value);
    }
}
```

Web (JS):

```js
Board.pause.onResult((result) => {
  for (const track of result.audioTracks ?? []) {
    setMixerVolume(track.id, track.value); // value is 0..100
  }
  // ... then branch on result.action
});
```

Godot (GDScript):

```gdscript
func _on_pause_result(result: BoardPauseResult) -> void:
    for track in result.audio_tracks:
        _apply_volume(track.id, int(track.value))
    # ... then branch on result.action

func _apply_volume(track_id: String, value: int) -> void:
    var bus_idx := AudioServer.get_bus_index(track_id)
    if bus_idx < 0:
        return
    AudioServer.set_bus_volume_db(bus_idx, linear_to_db(value / 100.0))
```

The result's audio entries carry an id and a value only (not the display name).

---

## Actions

The result tells you which action the player chose.

| Meaning | Unity (`BoardPauseAction`) | Godot constant (wire value) | Web string |
| --- | --- | --- | --- |
| Resume gameplay | `Resume` | `ACTION_RESUME` (`"RESUME"`) | `"resume"` |
| Exit without saving | `ExitGameUnsaved` | `ACTION_QUIT` (`"EXIT_GAME_UNSAVED"`) | `"quit"` |
| Exit after saving | `ExitGameSaved` | `ACTION_SAVE_AND_QUIT` (`"EXIT_GAME_SAVED"`) | `"save_and_quit"` |
| A custom button | `CustomButton` (via the dedicated Unity event) | `ACTION_CUSTOM_BUTTON` (`"CUSTOM_ACTION"`) | `"custom_button"` |

- Unity splits delivery: standard actions arrive on `pauseScreenActionReceived` and custom-button taps arrive on the separate `customPauseScreenButtonPressed` event.
- Godot action constants are not equal to their names; always compare against `Board.pause.ACTION_*` constants.
- Web exposes no action constants: `result.action` is a plain string and the custom-button id is on `result.customButtonId`.

### The full dispatch

Unity (C#):

```csharp
async void OnPauseAction(BoardPauseAction action, BoardPauseAudioTrack[] audioTracks)
{
    ApplyAudioSettings(audioTracks); // comes back regardless of action

    switch (action)
    {
        case BoardPauseAction.Resume:
            ResumeGameplay();
            break;
        case BoardPauseAction.ExitGameSaved:
            await SaveGame();        // you must save; Board does not
            BoardApplication.Exit();
            break;
        case BoardPauseAction.ExitGameUnsaved:
            BoardApplication.Exit();
            break;
    }
}

void OnCustomButton(string customButtonId, BoardPauseAudioTrack[] audioTracks)
{
    ApplyAudioSettings(audioTracks);
    switch (customButtonId)
    {
        case "restart": RestartLevel(); break;
        case "help":    ShowHelpScreen(); break;
    }
}
```

Web (JS):

```js
Board.pause.onResult(async (result) => {
  for (const track of result.audioTracks ?? []) {
    setMixerVolume(track.id, track.value);
  }

  switch (result.action) {
    case "resume":
      break;
    case "quit":
      Board.application.quit();
      break;
    case "save_and_quit":
      await saveGame();            // you must save; Board does not
      Board.application.quit();
      break;
    case "custom_button":
      switch (result.customButtonId) {
        case "restart": restartLevel(); break;
        case "help":    showHelpScreen(); break;
      }
      break;
  }
});
```

Godot (GDScript):

```gdscript
func _on_pause_result(result: BoardPauseResult) -> void:
    for track in result.audio_tracks:
        _apply_volume(track.id, int(track.value))

    match result.action:
        Board.pause.ACTION_RESUME:
            pass
        Board.pause.ACTION_QUIT:
            Board.application.quit()
        Board.pause.ACTION_SAVE_AND_QUIT:
            await _save_current_game()   # you must save; Board does not
            Board.application.quit()
        Board.pause.ACTION_CUSTOM_BUTTON:
            match result.custom_button_id:
                "restart":
                    get_tree().reload_current_scene.call_deferred()
                _:
                    push_warning("[pause] unknown custom button: %s" % result.custom_button_id)
        _:
            push_warning("[pause] unknown action: %s" % result.action)
```

---

## Save and quit

When the player picks Exit & Save, your game must complete the save before terminating. Board never saves for you, and the quit call is fire-and-forget. Save first, then quit. (See Save Games for the full save API.)

Unity (C#):

```csharp
async void OnPauseAction(BoardPauseAction action, BoardPauseAudioTrack[] audioTracks)
{
    if (action == BoardPauseAction.ExitGameSaved)
    {
        try
        {
            byte[] payload = SerializeGameState();
            var change = new BoardSaveGameMetadataChange
            {
                description = SaveDescription(),
                playedTime = PlayedSeconds(),
                gameVersion = Application.version
            };

            if (string.IsNullOrEmpty(currentSaveId))
                await BoardSaveGameManager.CreateSaveGame(payload, change);
            else
                await BoardSaveGameManager.UpdateSaveGame(currentSaveId, payload, change);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[pause] save-and-quit failed; exiting anyway: {e}");
        }

        BoardApplication.Exit();
    }
}
```

Web (JS):

```js
Board.pause.onResult(async (result) => {
  if (result.action === "save_and_quit") {
    try {
      const data: Uint8Array = serializeGameState();
      const playedMs = playedTime();

      if (!currentSaveId) {
        const meta = await Board.save.create(
          saveDescription(), data, playedMs, GAME_VERSION);
        currentSaveId = meta.id;
      } else {
        await Board.save.update(
          currentSaveId, saveDescription(), data, playedMs, GAME_VERSION);
      }
    } catch (e) {
      console.error("[pause] save-and-quit failed; quitting anyway", e);
    }
    Board.application.quit();
  }
});
```

Godot (GDScript):

```gdscript
func _on_pause_result(result: BoardPauseResult) -> void:
    if result.action == Board.pause.ACTION_SAVE_AND_QUIT:
        await _save_current_game()
        Board.application.quit()

func _save_current_game() -> void:
    var data := _serialize_game_state()
    var played_ms := _played_time_ms()

    if _current_save_id == "":
        var meta: BoardSaveMetadata = await Board.save.await_create(
            _save_description(), data, played_ms, GAME_VERSION)
        if meta == null:
            push_error("[pause] save-and-quit: create failed; quitting anyway")
            return
        _current_save_id = meta.id
    else:
        await Board.save.await_update(
            _current_save_id, _save_description(), data, played_ms, GAME_VERSION)
```

Reasonable practice: if the save fails, log it and quit anyway.

---

## Updating the menu

Your pause context changes as game state changes. Unity and Web each offer a partial-update method plus a full replace; Godot only has a full-replace `set_context` (and an audio-only `update_audio_tracks`).

The merge semantics differ:

- Unity has two explicit methods. `SetPauseScreenContext(...)` is a full replacement; `UpdatePauseScreenContext(...)` is a partial merge.
- Web mirrors Unity: `setContext(...)` replaces, `updateContext(partial)` merges.
- Godot has no general partial-merge. Every `set_context(...)` call REPLACES the context with exactly the keys you pass, so restate the FULL context (including `game_name`) on every call. The only partial path is `update_audio_tracks(...)`.

### Change a structural field

Unity (C#):

```csharp
// Partial merge: only the save toggle changes, everything else is preserved.
BoardApplication.UpdatePauseScreenContext(showSaveOptionUponExit: false);

// ... after the cinematic:
BoardApplication.UpdatePauseScreenContext(showSaveOptionUponExit: true);
```

Web (JS):

```js
Board.pause.updateContext({ offerSaveOption: false });
Board.pause.updateContext({ offerSaveOption: true });
```

Godot (GDScript):

```gdscript
Board.pause.set_context({ "game_name": "My Game", "offer_save_option": false })
Board.pause.set_context({ "game_name": "My Game", "offer_save_option": true })
```

### Swap custom buttons for a game state

Unity (C#):

```csharp
var combatButtons = new BoardPauseCustomButton[]
{
    new BoardPauseCustomButton("surrender", "Surrender", BoardPauseButtonIcon.DoorWithArrow)
};
BoardApplication.UpdatePauseScreenContext(customButtons: combatButtons);

// Remove them again by passing an empty array.
BoardApplication.UpdatePauseScreenContext(customButtons: new BoardPauseCustomButton[0]);
```

Web (JS):

```js
Board.pause.updateContext({
  customButtons: [
    { id: "surrender", title: "Surrender", icon: "doorwitharrow" },
  ],
});

Board.pause.updateContext({ customButtons: [] });
```

Godot (GDScript):

```gdscript
Board.pause.set_context({
    "game_name": "My Game",
    "custom_buttons": [
        { "id": "surrender", "title": "Surrender", "icon": Board.pause.ICON_DOOR_WITH_ARROW },
    ],
})

Board.pause.set_context({ "game_name": "My Game", "custom_buttons": [] })
```

### Live audio updates

Unity (C#):

```csharp
BoardApplication.UpdatePauseScreenContext(audioTracks: currentAudioTracks);
```

Web (JS):

```js
Board.pause.updateContext({ audioTracks: currentAudioTracks });
```

Godot (GDScript):

```gdscript
Board.pause.update_audio_tracks(_audio_tracks)
```

Use the audio-only / partial update for per-keystroke slider changes. Reserve a full `SetPauseScreenContext`/`setContext` for full reconfigurations.

---

## Clearing the context

When you leave gameplay, clear the context. After clearing, tapping the system menu button does nothing until you register a context again.

Unity (C#):

```csharp
BoardApplication.ClearPauseScreenContext();
```

Web (JS):

```js
Board.pause.clearContext();
```

Godot (GDScript):

```gdscript
Board.pause.clear_context()
```

---

## Pausing gameplay while the menu is open

The pause result fires when the player dismisses the overlay, not when they open it. None of the SDKs fire an "overlay opened" signal. To pause your game loop while the menu is up, use your engine's own application-lifecycle hooks.

Unity (C#):

```csharp
// Unity routes the activity-pause through OnApplicationPause.
void OnApplicationPause(bool paused)
{
    if (paused)
        PauseGameplay();   // overlay opened (or app backgrounded)
    else
        ResumeGameplay();  // overlay dismissed (or app foregrounded)
}
```

Web (JS):

```js
document.addEventListener("visibilitychange", () => {
  if (document.hidden) {
    pauseGameplay();   // overlay opened (or app backgrounded)
  } else {
    resumeGameplay();  // overlay dismissed (or app foregrounded)
  }
});
```

Godot (GDScript):

```gdscript
func _notification(what: int) -> void:
    if what == NOTIFICATION_APPLICATION_PAUSED:
        _pause_game()    # overlay opened (or app backgrounded)
    elif what == NOTIFICATION_APPLICATION_RESUMED:
        _resume_game()   # overlay dismissed (or app foregrounded)
```

---

## Best practices

1. Register the context early.
2. Always cover the standard actions.
3. Apply audio first in your result handler.
4. Use the partial update for live slider feedback.
5. Quit through the SDK, not the engine.
6. Pause your game loop with lifecycle hooks, not the pause result.
7. Use the named action enum or constants.

---

## See Also

- Save Games - saving when the player picks Exit & Save
- Player Management - the player-selector OS overlay
- Profile Switcher - the third OS overlay
- App Lifecycle - quitting cleanly and lifecycle events
- Architecture - how OS overlays sit above your game
- Per-SDK API references: Unity, Godot, Web
