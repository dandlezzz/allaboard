> Source: https://docs.dev.board.fun/unity/api/BoardSaveGameMetadata.html

# BoardSaveGameMetadata Class

Namespace: Board.Save

Represents the metadata for a save game.

```csharp
public class BoardSaveGameMetadata
```

Inheritance: System.Object → BoardSaveGameMetadata

### Remarks

Read only properties are set by the native layer. Use BoardSaveGameMetadataChange to create/update the metadata for a save game.

| Constructors | Description |
| --- | --- |
| BoardSaveGameMetadata() | Initializes a new instance of the BoardSaveGameMetadata class. |

| Properties | Description |
| --- | --- |
| coverImageChecksum | Gets the SHA-256 checksum of the cover image for integrity verification. |
| createdAt | Gets the timestamp in milliseconds when the saved game was created. |
| description | Gets the description of the saved game. |
| gameVersion | Gets the game version associated with this save game. |
| hasCoverImage | Gets a value indicating whether this save game has a cover image available. |
| id | Gets the unique identifier of the saved game. |
| payloadChecksum | Gets the SHA-256 checksum of the save game payload for integrity verification. |
| playedTime | Gets how long the players have played the saved game in seconds. |
| playerIds | The player identifiers associated with this save game. |
| players | Gets the collection of players associated with this save game. |
| updatedAt | Gets the timestamp in milliseconds when the saved game was last updated. |
