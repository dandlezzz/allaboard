> Source: https://docs.dev.board.fun/unity/api/BoardPlayerType.html

# BoardPlayerType Enum

Namespace: Board.Core

Specifies the type of a player on Board.

```csharp
public enum BoardPlayerType
```

| Field | Value | Description |
| --- | --- | --- |
| Profile | 0 | A human player that has a profile on this device. |
| Guest | 1 | A human player that does not have a profile on this device. Ephemeral; does not persist beyond a single game session nor outside the app they were created in. |
| AI | 2 | An AI player controlled by the game. Ephemeral like guests; created when the user selects an AI type from the player selector and does not persist across sessions. |
