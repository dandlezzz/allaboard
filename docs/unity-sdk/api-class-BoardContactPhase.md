> Source: https://docs.dev.board.fun/unity/api/BoardContactPhase.html

# BoardContactPhase Enum

Namespace: Board.Input

Specifies the phase in the lifecycle of a contact on the Board.

```csharp
public enum BoardContactPhase
```

| Field | Value | Description |
| --- | --- | --- |
| None | 0 | No activity has been registered on the contact yet. A given contact will not go back to None once there has been input for it; it indicates a default-initialized contact record. |
| Began | 1 | A contact has just begun, i.e. a finger has touched the screen. |
| Moved | 2 | An ongoing contact has changed position. |
| Ended | 3 | An ongoing contact has just ended, i.e. the respective finger has been lifted off the screen. |
| Canceled | 4 | An ongoing contact has been cancelled, i.e. ended in a way other than through user interaction (for example, if focus moves away from the application while the contact is ongoing). |
| Stationary | 5 | An ongoing contact has not been moved (not received any input) in a frame. |
