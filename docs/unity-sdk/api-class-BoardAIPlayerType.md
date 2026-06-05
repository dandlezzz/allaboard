> Source: https://docs.dev.board.fun/unity/api/BoardAIPlayerType.html

# BoardAIPlayerType Struct

Namespace: Board.Session

Defines an AI player type that a game supports.

```csharp
public struct BoardAIPlayerType
```

### Remarks

Games register their available AI types (e.g., "Easy", "Hard", "Master") via SetAIPlayerTypes(BoardAIPlayerType[]). Each type has a display name shown in the player selector and an optional description.

| Fields | Description |
| --- | --- |
| description | A description of this AI type's behavior (e.g., "Plays conservatively"). |
| name | The display name of this AI type (e.g., "Easy", "Hard", "Master"). |
