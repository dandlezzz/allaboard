> Source: https://docs.dev.board.fun/unity/api/BoardSession.html — fetched 2026-06-04T18:38 (UTC-7)

# BoardSession Class (Board.Session)

Provides access to Board's app session.

```csharp
public static class BoardSession
```

Inheritance: System.Object 🡒 BoardSession

| Properties | Description |
| --- | --- |
| activeProfile | Gets the system-wide active profile. |
| players | Gets the array of active players in the current session including all BoardPlayerType types. |

| Methods | Description |
| --- | --- |
| PresentAddPlayerSelector() | Presents the native player selector to add a new player to the current session. |
| PresentAddPlayerSelector(int[]) | Presents the native player selector to add a new player to the current session, with optional filtering of which AI types to show. |
| PresentReplacePlayerSelector(BoardSessionPlayer) | Presents the native player selector to replace or remove an existing BoardSessionPlayer from the current session. |
| PresentReplacePlayerSelector(BoardSessionPlayer, int[]) | Presents the native player selector to replace or remove an existing BoardSessionPlayer from the current session, with optional filtering of which AI types to show. |
| ResetPlayers() | Resets the session players to the initial state. |
| SetAIPlayerTypes(BoardAIPlayerType[]) | Registers the AI player types this game supports. |

| Events | Description |
| --- | --- |
| activeProfileChanged | Occurs when the system-wide active profile changes. |
| playersChanged | Occurs when the active players in the session change. |
