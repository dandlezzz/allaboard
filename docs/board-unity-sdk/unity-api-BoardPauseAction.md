> Source: https://docs.dev.board.fun/unity/api/BoardPauseAction.html — fetched 2026-06-04T18:38 (UTC-7)

# BoardPauseAction Enum (Board.Core)

Specifies the action type for a button in the Board pause screen.

```csharp
public enum BoardPauseAction
```

### Fields

- `None` = 0 — No action.
- `Resume` = 1 — The player resumed the game.
- `ExitGameSaved` = 2 — The player exited the game with save.
- `ExitGameUnsaved` = 3 — The player exited the game without saving.
- `CustomButton` = 4 — The player tapped a custom button.
