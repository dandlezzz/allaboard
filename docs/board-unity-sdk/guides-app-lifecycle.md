> Source: https://docs.dev.board.fun/guides/app-lifecycle — fetched 2026-06-04T18:38 (UTC-7)

# App Lifecycle

Board apps run in a managed environment. The OS decides when your app is foregrounded, backgrounded, or torn down, and Board's system overlays (the pause screen and the profile switcher) can suspend your input at any moment. This guide covers what happens at each lifecycle moment and how your game should react: starting up, losing and regaining the foreground, reacting to the system pause overlay, and quitting cleanly. The concepts are the same across the three SDKs; the way each engine surfaces them differs, so this guide shows the idiomatic setup for each.

New to the platform? Read Architecture for how apps, the OS, and the system overlays fit together.

---

## What the SDK does and does not own

Board's SDKs deliberately do not add a new app-lifecycle event system on top of the host engine. There is no `onResume`/`onPause`/`onForeground` callback in any of the three SDKs. Foreground and background transitions reach your game through the host engine's native lifecycle hooks, and you use the SDK only for the things the OS genuinely owns: the pause overlay, the profile switcher, and a clean quit.

| Concern | Where it comes from |
| --- | --- |
| Startup / init | Engine entry point, plus a one-time SDK init where required (Godot) |
| Foreground / background | Host engine lifecycle hooks (not an SDK event) |
| Input cancellation on background | The touch stream itself: every active contact arrives with a `Canceled` phase |
| Pause overlay open / result | SDK pause channel (event, signal, or callback) |
| Quit | SDK quit call |

The single rule that ties these together: when your app leaves the foreground, every active contact is canceled. You receive a `Canceled` phase on each contact at the transition, regardless of SDK. Treat that as your signal to drop per-contact state. See Touch for the contact and phase model.

---

## Startup

Bring the SDK up as early as your engine allows, and gate every SDK call behind the on-device check so the same code runs unmodified in the editor or a desktop build. The device-support flag has a different name in each SDK.

Unity initializes the SDK automatically before the first scene loads, so there is no init call to make: you only read `BoardSupport.enabled` to decide whether to drive Board features. Godot requires a one-time `Board.initialize(app_id)` before any session, save, avatar, or pause call, and gates on the `Board.is_on_device` property. Web has no init call either; it gates on `Board.isOnDevice`, which is true only inside a Board WebView.

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

Off-device behavior differs by SDK. On Unity and Godot, SDK calls no-op or return defaults when not on device. On Web, most service calls throw when the `window.BoardSDK` bridge is absent, so always branch on `Board.isOnDevice` before calling them. (The one exception is `Board.input.getContacts()`, which returns an empty array rather than throwing.)

---

## Foreground and background

Your app is in the foreground when it is the active game on the device. It moves to the background when the system pause overlay or profile switcher is up, or when the OS is mid-transition to or from another app.

Pause game logic when you lose the foreground and resume when you return. The SDK does not deliver these transitions; you receive them through the host engine. The shared, cross-SDK signal that the OS has taken over input is the contact stream: at the moment you lose the foreground, every active contact is reported with a `Canceled` phase.

In Unity, use `MonoBehaviour.OnApplicationPause` and `OnApplicationFocus`.

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

Whichever path you use, also handle the `Canceled` contacts you receive at the transition so no Piece or finger is left in a "held" state.

```csharp
// In your per-frame contact loop (see the Touch guide).
foreach (var contact in BoardInput.GetActiveContacts())
{
    if (contact.phase == BoardContactPhase.Canceled)
    {
        ReleaseContactState(contact);  // OS took over input; drop held state
    }
}
```

(In Godot, handle the engine's `NOTIFICATION_APPLICATION_PAUSED`/`NOTIFICATION_APPLICATION_RESUMED` notifications. On Web, use the page Visibility API and `pagehide`/`pageshow` events.)

---

## The pause overlay

Board's pause overlay is its own lifecycle moment, and it is fully OS-owned. Do not draw your own pause UI. You register a pause context (the app name, whether to offer Save and Quit, custom buttons, audio sliders), and the OS renders the screen and shows or hides the system menu button across the activity lifecycle. Register the context early, during startup.

When the user makes a choice, the result reaches you through the SDK's pause channel:

- Unity delivers results through two C# events surfaced by an internal per-frame poller: `pauseScreenActionReceived` for system actions (resume, exit saved, exit unsaved) and `customPauseScreenButtonPressed` for custom buttons.
- Godot emits a single signal, `pause_result_received`, carrying a `BoardPauseResult`.
- Web uses a callback: `pause.onResult(cb)`, which returns an unsubscribe function.

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

The pause action values are not interchangeable across SDKs. Unity uses the `BoardPauseAction` enum (`Resume`, `ExitGameSaved`, `ExitGameUnsaved`, `CustomButton`). See Pause Menu for the full context schema, custom buttons, and audio tracks.

---

## Quitting

Always quit through the SDK so the OS returns the user cleanly to the launcher. Do not call the engine's raw quit on device. Unity's quit is `BoardApplication.Exit()`.

```csharp
using Board.Core;

// Clean quit: removes the task and returns to the launcher.
BoardApplication.Exit();
```

Board is wall-powered and does not perform a graceful shutdown on power loss, so the OS may also tear your app down without warning. Treat a clean quit as the happy path, not a guarantee: persist state at meaningful checkpoints during play rather than relying on a quit hook to flush it. See Save Games for the save model.

---

## The system menu button and the profile switcher

The system menu button (the affordance that opens the pause overlay) is shown and hidden by the OS automatically across the activity lifecycle. There is no SDK call to toggle it. Your only responsibility is to register a pause context so the button has something to open.

The profile switcher is an OS overlay you can show or hide. After a switch, re-read the active profile rather than caching it. The call lives on `BoardApplication` in Unity.

```csharp
using Board.Core;

BoardApplication.ShowProfileSwitcher();
// ...
BoardApplication.HideProfileSwitcher();
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
