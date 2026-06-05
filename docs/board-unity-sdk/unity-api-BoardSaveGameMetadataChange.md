> Source: https://docs.dev.board.fun/unity/api/BoardSaveGameMetadataChange.html — fetched 2026-06-04T18:38 (UTC-7)

# BoardSaveGameMetadataChange Class (Board.Save)

Encapsulates required metadata for save game creation and updates.

```csharp
public class BoardSaveGameMetadataChange
```

Inheritance: System.Object 🡒 BoardSaveGameMetadataChange

| Constructors | Description |
| --- | --- |
| BoardSaveGameMetadataChange() | Initializes a new instance of the BoardSaveGameMetadataChange class |
| BoardSaveGameMetadataChange(string, Texture2D, ulong, string) | Initializes a new instance with the specified description, cover image, played time, and game version. |

| Properties | Description |
| --- | --- |
| coverImage | Gets or sets the cover image of the saved game (will be converted to 432x243 PNG). |
| description | Gets or sets the description of the saved game. |
| gameVersion | Gets or sets the game version for the saved game. |
| playedTime | Gets or sets how long the players have played the saved game in seconds. |
