> Source: https://docs.dev.board.fun/guides/avatars
>
> Cross-SDK guide. Unity code is the primary target; Godot/Web samples retained for reference.

# Avatars

Every player on Board has an avatar: a small PNG image chosen in the system profile, shown alongside the player's name in lobby screens, scoreboards, and pause overlays. Avatars are owned by the OS, not your app. When a player picks their avatar in Board's system settings, every game sees the same image, so you never store or upload avatar art yourself. You ask the SDK for the image that belongs to a player and render it.

---

## The avatar model

Avatars are addressed by an avatar id that travels with each player. The default avatar is id 0, available even before any player is added.

Image loading is asynchronous because the bytes come from the OS over an IPC bridge, and every SDK caches the decoded result per id.

| Concept | Meaning |
| --- | --- |
| avatar id | Identifies which system avatar a player selected. Carried on the player object. |
| default avatar | Avatar id 0, always available. Use it as a placeholder or for an empty seat. |
| caching | The SDK caches each decoded avatar by id; repeat requests are cheap. |

### Per-SDK conventions

| Convention | Unity | Godot | Web |
| --- | --- | --- | --- |
| Image type returned | `Texture2D` | `ImageTexture` | PNG data URI (`string`) |
| Async type | `Task` | `await` an `ImageTexture` | `Promise` |
| How you resolve a player | Read `BoardPlayer.avatar` (lazy property) | `await_load_avatar(int(player.avatar_id))` | `avatar.forPlayer(player)` |
| `avatarId` field type | `string` | `String` (pass `int(...)`) | `string` (coerced for you by `forPlayer`) |

One structural difference: Unity exposes avatars through the player object: `BoardPlayer` lazy-loads its own `avatar` texture and raises an event when it is ready, so there is no public "load avatar id N" call. Godot and Web instead expose a dedicated avatar module.

---

## Loading a player's avatar

Unity (C#):

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

Web (JS):

```js
import { Board, type BoardPlayer } from "@board.fun/web-sdk";

async function showPlayer(player: BoardPlayer) {
  // forPlayer coerces the player's string avatarId to a number for you.
  const dataUri = await Board.avatar.forPlayer(player);
  const img = document.querySelector<HTMLImageElement>("#avatar")!;
  img.src = dataUri; // a data:image/png;base64,... URI
}
```

Godot (GDScript):

```gdscript
@onready var avatar_rect: TextureRect = $TextureRect

func show_player(player: BoardPlayer) -> void:
    # avatar_id is a String on BoardPlayer; the loader takes an int.
    var tex := await Board.avatar.await_load_avatar(int(player.avatar_id))
    if tex != null:
        avatar_rect.texture = tex
```

### Rendering every player

Unity (C#):

```csharp
using Board.Session;

foreach (var player in BoardSession.players)
{
    var badge = SpawnBadge(player.name);
    badge.GetComponent<PlayerBadge>().Show(player);
}
```

Web (JS):

```js
import { Board } from "@board.fun/web-sdk";

for (const player of Board.session.getPlayers()) {
  const dataUri = await Board.avatar.forPlayer(player);
  addBadge(player.name, dataUri);
}
```

Godot (GDScript):

```gdscript
for player in Board.session.get_players():  # Array[BoardPlayer]
    var tex := await Board.avatar.await_load_avatar(int(player.avatar_id))
    add_badge(player.display_name, tex)
```

---

## The default avatar

Avatar id 0 is the default.

Unity (C#):

```csharp
using Board.Core;
using UnityEngine;

// Static helper on BoardPlayer; loads avatar id 0.
Texture2D defaultAvatar = await BoardPlayer.GetDefaultAvatar();
emptySeatImage.texture = defaultAvatar;
```

Web (JS):

```js
import { Board } from "@board.fun/web-sdk";

const dataUri = await Board.avatar.getDefault();
emptySeatImg.src = dataUri;
```

Godot (GDScript):

```gdscript
var tex := await Board.avatar.await_default_avatar()
if tex != null:
    empty_seat_rect.texture = tex
```

---

## Loading by avatar id

Godot and Web let you load by raw id. Unity has no public load-by-id call: avatars are reached through the player object's `avatar` property and the static `GetDefaultAvatar()` for id 0.

Web (JS):

```js
import { Board } from "@board.fun/web-sdk";

const dataUri = await Board.avatar.loadPNG(avatarId);
iconImg.src = dataUri;
```

Godot (GDScript):

```gdscript
var tex := await Board.avatar.await_load_avatar(avatar_id)
if tex != null:
    icon_rect.texture = tex
```

---

## Reacting to avatar changes

In Unity the player object raises an `avatarLoaded` event, and `BoardSession.playersChanged` fires when the roster changes. In Godot, `Board.session.players_changed` fires on any roster change. The Web SDK has no players-changed event: re-read `Board.session.getPlayers()` after a player-selector call resolves.

Unity (C#):

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

Web (JS):

```js
import { Board } from "@board.fun/web-sdk";

async function refreshAfterSelector() {
  const added = await Board.session.presentAddPlayer();
  if (!added) return;

  // Drop cached avatars so a switched avatar re-fetches.
  Board.avatar.clearCache();
  for (const player of Board.session.getPlayers()) {
    const dataUri = await Board.avatar.forPlayer(player);
    updateBadge(player, dataUri);
  }
}
```

Godot (GDScript):

```gdscript
func _ready() -> void:
    if not Board.is_on_device:
        return
    Board.session.players_changed.connect(_refresh_avatars)

func _refresh_avatars() -> void:
    Board.avatar.clear_cache()
    for player in Board.session.get_players():
        var tex := await Board.avatar.await_load_avatar(int(player.avatar_id))
        update_badge(player, tex)
```

---

## See Also

- Player Management — how players are added to and removed from a session, and where `avatarId` comes from
- Profile Switcher — invoking the system profile-switcher overlay
- Save Games — rendering avatars for the players stored in a saved game
- Per-SDK API references: Unity, Godot, Web
