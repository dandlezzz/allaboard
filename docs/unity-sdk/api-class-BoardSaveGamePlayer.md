> Source: https://docs.dev.board.fun/unity/api/BoardSaveGamePlayer.html

# BoardSaveGamePlayer Class

Namespace: Board.Save

Represents player display information for save games.

```csharp
public sealed class BoardSaveGamePlayer : Board.Core.BoardPlayer
```

Inheritance: System.Object → BoardPlayer → BoardSaveGamePlayer

| Properties | Description |
| --- | --- |
| aiTypeIndex | Gets the index into the registered AI player types for this player. |
| playerId | Gets the player's persistent application-specific identifier. |

See also `BoardPlayer` (base class) for `name`, `type`, `avatar`, `avatarId`.
