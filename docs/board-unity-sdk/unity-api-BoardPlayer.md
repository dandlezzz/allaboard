> Source: https://docs.dev.board.fun/unity/api/BoardPlayer.html — fetched 2026-06-04T18:38 (UTC-7)

# BoardPlayer Class (Board.Core)

Represents a player on Board.

```csharp
public class BoardPlayer
```

Inheritance: System.Object 🡒 BoardPlayer
Derived: ↳ BoardSaveGamePlayer  ↳ BoardSessionPlayer

| Properties | Description |
| --- | --- |
| avatar | Gets the player's avatar UnityEngine.Texture2D. |
| avatarId | Gets the player's avatar identifier. |
| name | Gets the player's name. |
| playerId | Gets the Player's persistent app-specific identifier. |
| type | Gets the player's type. |

| Methods | Description |
| --- | --- |
| GetDefaultAvatar() | Gets the default avatar texture (avatar ID 0). |

| Events | Description |
| --- | --- |
| avatarLoaded | Occurs when the avatar UnityEngine.Texture2D is loaded. |
