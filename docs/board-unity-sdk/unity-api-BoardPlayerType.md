> Source: https://docs.dev.board.fun/unity/api/BoardPlayerType.html — fetched 2026-06-04T18:38 (UTC-7)

# BoardPlayerType Enum (Board.Core)

Specifies the type of a player on Board.

```csharp
public enum BoardPlayerType
```

### Fields

- `Profile` = 0 — A human player that has a profile on this device.
- `Guest` = 1 — A human player that does not have a profile on this device. (Guest players are ephemeral. They do not persist beyond a single game session nor are they accessible outside of the app they were created in.)
- `AI` = 2 — An AI player controlled by the game. (AI players are ephemeral like guests. They are created when the user selects an AI type from the player selector and do not persist across sessions.)
