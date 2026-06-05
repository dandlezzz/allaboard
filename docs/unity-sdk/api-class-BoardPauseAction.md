> Source: https://docs.dev.board.fun/unity/api/BoardPauseAction.html

# BoardPauseAction Enum

Namespace: Board.Core

Specifies the action type for a button in the Board pause screen.

```csharp
public enum BoardPauseAction
```

| Field | Value | Description |
| --- | --- | --- |
| None | 0 | No action. |
| Resume | 1 | The player resumed the game. |
| ExitGameSaved | 2 | The player exited the game with save. |
| ExitGameUnsaved | 3 | The player exited the game without saving. |
| CustomButton | 4 | The player tapped a custom button. |
