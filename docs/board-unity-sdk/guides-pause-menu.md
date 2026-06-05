> Source: https://docs.dev.board.fun/guides/pause-menu — fetched 2026-06-04T18:38 (UTC-7)

# Pause Menu

Board ships a system pause menu that BoardOS renders on top of your game. The OS owns the menu button and every pixel of the menu UI; your game supplies the context the menu shows (its title, optional custom buttons, optional audio sliders) and reacts to whatever the player picks. This is consistent across all three SDKs; only the per-engine call style differs (Unity sets context on a static class and receives results as events, Godot pushes a Dictionary and listens on a signal, Web passes a typed object and subscribes a callback).

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

You do not manage the menu button's visibility. The OS shows the system menu button automatically on resume and hides it across the activity lifecycle.

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

Unity has no init call and no on-device gate around the pause API: `BoardSupport.enabled` exists for input but the pause calls are safe to make unconditionally. (Web throws off-device, Godot no-ops and requires a one-time `Board.initialize(app_id)`.)

---

## Basic setup

The minimum viable pause flow: register a context with your game name and the save option, then handle the result.

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

Register the context early, before the player can tap the system menu button. Without a context the button is live but the tap does nothing.

---

## The context

A pause context has the same logical fields in every SDK. Only the casing and the call shape change.

| Field | Unity | Godot | Web | Meaning |
| --- | --- | --- | --- | --- |
| App/game name | `applicationName` | `game_name` | `gameName` | Title in the menu header |
| Save on exit | `showSaveOptionUponExit` | `offer_save_option` | `offerSaveOption` | Show the Exit & Save action |
| Custom buttons | `customButtons` | `custom_buttons` | `customButtons` | Your extra action buttons |
| Audio sliders | `audioTracks` | `audio_tracks` | `audioTracks` | Volume sliders |

Godot and Web also have an app/game id field (`game_id`/`gameId`). Unity has no game-id field on the context.

Every field is optional. In Unity, unspecified named parameters fall back to defaults.

---

## Custom buttons

Add game-specific actions alongside the built-in ones. Each custom button carries a stable id (returned to you when tapped), display text, and an optional icon.

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

Field names per SDK: Unity's button text field is `text`; Godot and Web call it `title`. The id field is `id` everywhere. The id must be at most 64 characters and the label at most 128 characters. Custom buttons fill rows of two; an odd count promotes the trailing button to a full-width row.

### Available icons

| Suggested use | Unity (`BoardPauseButtonIcon`) | Godot (`Board.pause.*`) | Web (string) |
| --- | --- | --- | --- |
| No icon | `None` | `ICON_NONE` | `""` |
| Restart, retry, replay | `CircularArrow` | `ICON_CIRCULAR_ARROW` | `"circulararrow"` |
| Exit, leave, quit | `DoorWithArrow` | `ICON_DOOR_WITH_ARROW` | `"doorwitharrow"` |
| Back, previous | `LeftArrow` | `ICON_LEFT_ARROW` | `"leftarrow"` |
| Stop, generic action | `Square` | `ICON_SQUARE` | `"square"` |

### Handling a custom button

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

---

## Audio sliders

The pause menu can host volume sliders, one per audio track. The player adjusts them; the new values come back to you with the result, every time the menu is dismissed, regardless of which action was picked. Each track has a stable id, a display name, and an integer value from 0 to 100.

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

Fields: `id` (max 64 chars), `name` label (max 128 chars), and `value` (integer 0 to 100; out-of-range values are clamped).

### Applying the returned values

Apply the audio values first in your result handler, before branching on the action, since they come back no matter what the player picked.

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
- Godot action constants are not equal to their names; always compare against the `Board.pause.ACTION_*` constants.
- Web exposes no action constants: `result.action` is a plain string and the custom-button id is on `result.customButtonId`.

### The full dispatch

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

---

## Save and quit

When the player picks Exit & Save, your game must complete the save before terminating. Board never saves for you, and the quit call is fire-and-forget: once you call it, the app is going away. Save first, then quit. (See Save Games for the full save API.)

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

Reasonable practice: if the save fails, log it and quit anyway. Do not trap the player in your app because the save backing failed.

---

## Updating the menu

Unity and Web each offer a partial-update method plus a full replace; Godot only has a full-replace `set_context` (and an audio-only `update_audio_tracks`).

- Unity: `SetPauseScreenContext(...)` is a full replacement (any omitted named parameter resets to its default). `UpdatePauseScreenContext(...)` is a partial merge (omitted parameters keep their current value).

### Change a structural field

```csharp
// Partial merge: only the save toggle changes, everything else is preserved.
BoardApplication.UpdatePauseScreenContext(showSaveOptionUponExit: false);

// ... after the cinematic:
BoardApplication.UpdatePauseScreenContext(showSaveOptionUponExit: true);
```

### Swap custom buttons for a game state

```csharp
var combatButtons = new BoardPauseCustomButton[]
{
    new BoardPauseCustomButton("surrender", "Surrender", BoardPauseButtonIcon.DoorWithArrow)
};
BoardApplication.UpdatePauseScreenContext(customButtons: combatButtons);

// Remove them again by passing an empty array.
BoardApplication.UpdatePauseScreenContext(customButtons: new BoardPauseCustomButton[0]);
```

### Live audio updates

```csharp
// Partial update touching only the audio tracks.
BoardApplication.UpdatePauseScreenContext(audioTracks: currentAudioTracks);
```

Reserve a full `SetPauseScreenContext` for full reconfigurations: those force the OS to re-render the whole pause UI.

---

## Clearing the context

When you leave gameplay (for example, returning to a main menu), clear the context. After clearing, tapping the system menu button does nothing until you register a context again.

```csharp
BoardApplication.ClearPauseScreenContext();
```

---

## Pausing gameplay while the menu is open

The pause result fires when the player dismisses the overlay, not when they open it. None of the SDKs fire an "overlay opened" signal. To pause your game loop while the menu is up, use your engine's own application-lifecycle hooks.

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

Use these lifecycle hooks, not the pause result, to stop and restart your game loop.

---

## Best practices

1. Register the context early.
2. Always cover the standard actions.
3. Apply audio first in your result handler.
4. Use the partial update for live slider feedback.
5. Quit through the SDK, not the engine.
6. Pause your game loop with lifecycle hooks, not the pause result.
7. Use the named action enum or constants. Do not hardcode raw wire strings.

---

## See Also

- Save Games — saving when the player picks Exit & Save
- Player Management — the player-selector OS overlay
- Profile Switcher — the third OS overlay
- App Lifecycle — quitting cleanly and lifecycle events
- Architecture — how OS overlays sit above your game
- Per-SDK API references: Unity, Godot, Web
