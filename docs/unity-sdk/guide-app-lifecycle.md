> Source: https://docs.dev.board.fun/guides/app-lifecycle
>
> Cross-SDK guide. Unity code is the primary target; Godot/Web samples retained for reference.

# App Lifecycle

Board apps run in a managed environment. The OS decides when your app is foregrounded, backgrounded, or torn down, and Board's system overlays (the pause screen and the profile switcher) can suspend your input at any moment. This guide covers what happens at each lifecycle moment and how your game should react.

New to the platform? Read Architecture for how apps, the OS, and the system overlays fit together.

---

## What the SDK does and does not own

Board's SDKs deliberately do not add a new app-lifecycle event system on top of the host engine. There is no `onResume`/`onPause`/`onForeground` callback in any of the three SDKs. Foreground and background transitions reach your game through the host engine's native lifecycle hooks.

| Concern | Where it comes from |
| --- | --- |
| Startup / init | Engine entry point, plus a one-time SDK init where required (Godot) |
| Foreground / background | Host engine lifecycle hooks (not an SDK event) |
| Input cancellation on background | The touch stream itself: every active contact arrives with a `Canceled` phase |
| Pause overlay open / result | SDK pause channel (event, signal, or callback) |
| Quit | SDK quit call |

The single rule that ties these together: when your app leaves the foreground, every active contact is canceled. You receive a `Canceled` phase on each contact at the transition.

---

## Startup

Bring the SDK up as early as your engine allows, and gate every SDK call behind the on-device check.

Unity initializes the SDK automatically before the first scene loads, so there is no init call to make: you only read `BoardSupport.enabled`. Godot requires a one-time `Board.initialize(app_id)` and gates on `Board.is_on_device`. Web has no init call; it gates on `Board.isOnDevice`.

Unity (C#):

```csharp
using Board.Core;
using Board.Input;
using UnityEngine;

public class GameBootstrap : MonoBehaviour
{
    void Awake()
    {
        // The SDK self-initializes before the first scene; no init call.
        // Gate on BoardSupport.enabled (true on device and in the Editor).
        if (!BoardSupport.enabled)
        {
            return;
        }

        // Configure OS-owned surfaces while the game is coming up.
        BoardApplication.SetPauseScreenContext(applicationName: "My Game");
    }
}
```

Web (JS):

```js
import { Board } from "@board.fun/web-sdk";

if (Board.isOnDevice) {
  Board.pause.setContext({ gameName: "My Game", offerSaveOption: true });
}
```

Godot (GDScript):

```gdscript
extends Node

const APP_ID := "00000000-0000-0000-0000-000000000000"  # your app ID (a UUID)

func _ready() -> void:
    if not Board.is_on_device:
        return
    # initialize() must run once before any session/save/avatar/pause call.
    Board.initialize(APP_ID)

    Board.pause.set_context({ "game_name": "My Game", "offer_save_option": true })
```

Off-device behavior differs by SDK. On Unity and Godot, SDK calls no-op or return defaults when not on device. On Web, most service calls throw when the `window.BoardSDK` bridge is absent (the one exception is `Board.input.getContacts()`, which returns an empty array).

---

## Foreground and background

Pause game logic when you lose the foreground and resume when you return. The SDK does not deliver these transitions; you receive them through the host engine. The shared, cross-SDK signal that the OS has taken over input is the contact stream: at the moment you lose the foreground, every active contact is reported with a `Canceled` phase.

In Unity, use `MonoBehaviour.OnApplicationPause` and `OnApplicationFocus`. In Godot, handle `NOTIFICATION_APPLICATION_PAUSED`/`NOTIFICATION_APPLICATION_RESUMED`. On Web, use the page Visibility API.

Unity (C#):

```csharp
using UnityEngine;

public class LifecycleHandler : MonoBehaviour
{
    void OnApplicationPause(bool paused)
    {
        if (paused)
        {
            PauseGameplay();   // lost the foreground
        }
        else
        {
            ResumeGameplay();  // back in the foreground
        }
    }

    void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
        {
            PauseGameplay();
        }
    }
}
```

Web (JS):

```js
document.addEventListener("visibilitychange", () => {
  if (document.hidden) {
    pauseGameplay();   // lost the foreground
  } else {
    resumeGameplay();  // back in the foreground
  }
});
```

Godot (GDScript):

```gdscript
func _notification(what: int) -> void:
    match what:
        NOTIFICATION_APPLICATION_PAUSED:
            pause_gameplay()    # lost the foreground
        NOTIFICATION_APPLICATION_RESUMED:
            resume_gameplay()   # back in the foreground
```

Also handle the `Canceled` contacts you receive at the transition:

Unity (C#):

```csharp
foreach (var contact in BoardInput.GetActiveContacts())
{
    if (contact.phase == BoardContactPhase.Canceled)
    {
        ReleaseContactState(contact);  // OS took over input; drop held state
    }
}
```

Web (JS):

```js
import { Board, BoardContactPhase } from "@board.fun/web-sdk";

Board.input.subscribe((contacts) => {
  for (const c of contacts) {
    if (c.phase === BoardContactPhase.Canceled) {
      releaseContactState(c);
    }
  }
});
```

Godot (GDScript):

```gdscript
func _on_contacts(contacts: Array) -> void:
    for c in contacts:
        if c.phase_id == Board.input.PHASE_CANCELED:
            release_contact_state(c)
```

---

## The pause overlay

Board's pause overlay is fully OS-owned. Do not draw your own pause UI. You register a pause context, and the OS renders the screen. Register the context early, during startup.

- Unity delivers results through two C# events: `pauseScreenActionReceived` for system actions and `customPauseScreenButtonPressed` for custom buttons.
- Godot emits a single signal, `pause_result_received`.
- Web uses a callback: `pause.onResult(cb)`.

Unity (C#):

```csharp
using Board.Core;
using UnityEngine;

public class PauseHandler : MonoBehaviour
{
    void OnEnable()
    {
        BoardApplication.pauseScreenActionReceived += OnPauseAction;
        BoardApplication.customPauseScreenButtonPressed += OnCustomButton;
    }

    void OnDisable()
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
            case BoardPauseAction.ExitGameSaved:
                SaveThenExit();
                break;
            case BoardPauseAction.ExitGameUnsaved:
                BoardApplication.Exit();
                break;
        }
    }

    void OnCustomButton(string customButtonId, BoardPauseAudioTrack[] audioTracks)
    {
        if (customButtonId == "restart")
        {
            RestartGame();
        }
    }
}
```

Web (JS):

```js
import { Board, type BoardPauseResult } from "@board.fun/web-sdk";

const unsubscribe = Board.pause.onResult((result: BoardPauseResult) => {
  switch (result.action) {
    case "resume":
      resumeGameplay();
      break;
    case "save_and_quit":
      saveThenQuit();
      break;
    case "quit":
      Board.application.quit();
      break;
    case "custom_button":
      if (result.customButtonId === "restart") {
        restartGame();
      }
      break;
  }
});
```

Godot (GDScript):

```gdscript
func _ready() -> void:
    if not Board.is_on_device:
        return
    Board.pause.pause_result_received.connect(_on_pause_result)

func _on_pause_result(result: BoardPauseResult) -> void:
    match result.action:
        Board.pause.ACTION_RESUME:
            resume_gameplay()
        Board.pause.ACTION_SAVE_AND_QUIT:
            save_then_quit()
        Board.pause.ACTION_QUIT:
            Board.application.quit()
        Board.pause.ACTION_CUSTOM_BUTTON:
            if result.custom_button_id == "restart":
                restart_game()
```

The pause action values are not interchangeable across SDKs. See Pause Menu for the full context schema.

---

## Quitting

Always quit through the SDK so the OS returns the user cleanly to the launcher. Do not call the engine's raw quit on device.

Unity's quit is `BoardApplication.Exit()`. Godot's is `Board.application.quit()` (not `get_tree().quit()`). Web's is `Board.application.quit()`. All three are fire-and-forget.

Unity (C#):

```csharp
using Board.Core;

// Clean quit: removes the task and returns to the launcher.
BoardApplication.Exit();
```

Web (JS):

```js
import { Board } from "@board.fun/web-sdk";

Board.application.quit();
```

Godot (GDScript):

```gdscript
# Use this on device, NOT get_tree().quit().
Board.application.quit()
```

Board is wall-powered and does not perform a graceful shutdown on power loss, so the OS may also tear your app down without warning. Persist state at meaningful checkpoints during play rather than relying on a quit hook.

---

## The system menu button and the profile switcher

The system menu button (which opens the pause overlay) is shown and hidden by the OS automatically. There is no SDK call to toggle it. Your only responsibility is to register a pause context.

The profile switcher is an OS overlay you can show or hide. The call lives on `BoardApplication` in Unity, on `Board.session` in Godot, and on `Board.application` in Web.

Unity (C#):

```csharp
using Board.Core;

BoardApplication.ShowProfileSwitcher();
// ...
BoardApplication.HideProfileSwitcher();
```

Web (JS):

```js
import { Board } from "@board.fun/web-sdk";

Board.application.showProfileSwitcher();
// ...
Board.application.hideProfileSwitcher();
```

Godot (GDScript):

```gdscript
Board.session.show_profile_switcher()
# ...
Board.session.hide_profile_switcher()
```

See Profile Switcher for the full flow and Player Management for re-reading the roster after a switch.

---

## See Also

- Architecture — how apps, the OS, and the system overlays fit together
- Touch — the contact and phase model, including the `Canceled` phase on background
- Pause Menu — configuring and reacting to the system pause overlay
- Profile Switcher — showing the OS profile switcher
- Player Management — re-reading the roster after a profile switch
- Save Games — persisting state across sessions and at checkpoints
- Per-SDK API references: Unity, Godot, Web
