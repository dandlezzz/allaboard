> Source: https://docs.dev.board.fun/guides/avatars — fetched 2026-06-04T18:38 (UTC-7)

# Avatars

Every player on Board has an avatar: a small PNG image chosen in the system profile, shown alongside the player's name in lobby screens, scoreboards, and pause overlays. Avatars are owned by the OS, not your app. When a player picks their avatar in Board's system settings, every game sees the same image, so you never store or upload avatar art yourself. You ask the SDK for the image that belongs to a player and render it.

This guide covers how each SDK turns a player into a displayable avatar image. The concept is identical across the three SDKs; the surface differs because each one returns its host engine's native image type and follows its native async convention.

---

## The avatar model

Avatars are addressed by an avatar id that travels with each player. A player object carries the id of the avatar its owner selected, and that id is what the SDK resolves into an image. The default avatar is id 0, available even before any player is added (use it as a placeholder, or for an empty seat).

Image loading is asynchronous because the bytes come from the OS over an IPC bridge, and every SDK caches the decoded result per id. Once an avatar has been fetched, repeated requests for the same id are cheap, so you can call into the avatar system freely from UI code without worrying about redundant work. You do not need to cache the images yourself.

| Concept | Meaning |
| --- | --- |
| avatar id | Identifies which system avatar a player selected. Carried on the player object. |
| default avatar | Avatar id 0, always available. Use it as a placeholder or for an empty seat. |
| caching | The SDK caches each decoded avatar by id; repeat requests are cheap. |

Avatar ids come from a player. To get the players in the current session, see Player Management. The same ids also appear on the players stored inside a saved game, so you can render avatars on a save-slot screen too: see Save Games.

### Per-SDK conventions

| Convention | Unity | Godot | Web |
| --- | --- | --- | --- |
| Image type returned | `Texture2D` | `ImageTexture` | PNG data URI (`string`) |
| Async type | `Task<Texture2D>` | `await` an `ImageTexture` | `Promise<string>` |
| How you resolve a player | Read `BoardPlayer.avatar` (lazy property) | `await_load_avatar(int(player.avatar_id))` | `avatar.forPlayer(player)` |
| `avatarId` field type | `string` | `String` (pass `int(...)`) | `string` (coerced for you by `forPlayer`) |

One structural difference: Unity exposes avatars through the player object: `BoardPlayer` lazy-loads its own `avatar` texture and raises an event when it is ready, so there is no public "load avatar id N" call. Godot and Web instead expose a dedicated avatar module you call with an id (Godot) or with a player (Web).

---

## Loading a player's avatar (Unity)

In Unity you read the player's lazy `avatar` property and subscribe to its `avatarLoaded` event, because the texture is null until the first asynchronous load finishes.

```csharp
using Board.Core;
using Board.Session;
using UnityEngine;
using UnityEngine.UI;

public class PlayerBadge : MonoBehaviour
{
    [SerializeField] private RawImage avatarImage;

    public void Show(BoardSessionPlayer player)
    {
        // BoardPlayer.avatar lazy-loads on first access; it's null until ready.
        if (player.avatar != null)
        {
            avatarImage.texture = player.avatar;
        }
        else
        {
            // Render when the asynchronous load completes.
            player.avatarLoaded += p => avatarImage.texture = p.avatar;
            // Touching the property kicks off the load.
            _ = player.avatar;
        }
    }
}
```

### Rendering every player

```csharp
using Board.Session;

foreach (var player in BoardSession.players)
{
    var badge = SpawnBadge(player.name);
    badge.GetComponent<PlayerBadge>().Show(player);
}
```

---

## The default avatar

Avatar id 0 is the default. Every SDK exposes a direct way to fetch it.

```csharp
using Board.Core;
using UnityEngine;

// Static helper on BoardPlayer; loads avatar id 0.
Texture2D defaultAvatar = await BoardPlayer.GetDefaultAvatar();
emptySeatImage.texture = defaultAvatar;
```

---

## Loading by avatar id

Unity has no public load-by-id call: avatars are reached through the player object's `avatar` property and the static `GetDefaultAvatar()` for id 0. If you need an avatar for a player you read from a saved game, render it through that player's lazy `avatar` property exactly as in the loading section above. (Godot's loader takes an `int`; Web's `loadPNG` takes a `number`.)

---

## Reacting to avatar changes

If a player switches their avatar while your game is running, you should re-render.

In Unity the player object raises an `avatarLoaded` event when its texture finishes loading, and `BoardSession.playersChanged` fires when the roster itself changes; subscribe to both and re-read the player's `avatar`.

```csharp
using Board.Core;
using Board.Session;

void OnEnable()
{
    BoardSession.playersChanged += RefreshAvatars;
}

void OnDisable()
{
    BoardSession.playersChanged -= RefreshAvatars;
}

void RefreshAvatars()
{
    foreach (var player in BoardSession.players)
    {
        player.avatarLoaded += p => UpdateBadge(p, p.avatar);
        if (player.avatar != null)
        {
            UpdateBadge(player, player.avatar);
        }
    }
}
```

(On Godot, `Board.session.players_changed` fires on any roster change; on Web there is no players-changed event so re-read after a player-selector call resolves. Godot/Web expose a `clear_cache()`/`clearCache()` to force a fresh fetch.)

---

## See Also

- Player Management — how players are added to and removed from a session, and where `avatarId` comes from
- Profile Switcher — invoking the system profile-switcher overlay
- Save Games — rendering avatars for the players stored in a saved game
- Per-SDK API references: Unity, Godot, Web
