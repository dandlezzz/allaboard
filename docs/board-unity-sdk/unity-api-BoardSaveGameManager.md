> Source: https://docs.dev.board.fun/unity/api/BoardSaveGameManager.html — fetched 2026-06-04T18:38 (UTC-7)

# BoardSaveGameManager Class (Board.Save)

Manages access to Board's saved game system.

```csharp
public static class BoardSaveGameManager
```

Inheritance: System.Object 🡒 BoardSaveGameManager

| Methods | Description |
| --- | --- |
| CreateSaveGame(byte[], BoardSaveGameMetadataChange) | Creates a saved game with the specified payload and metadata, returning the metadata for the saved game. |
| GetAppStorageInfo() | Gets storage information for save games on the Board device including total allocated space, used space, and remaining space. |
| GetMaxAppStorage() | Gets the maximum allowed total storage size for all save games for an app. |
| GetMaxPayloadSize() | Gets the maximum allowed size for individual save game payloads. |
| GetMaxSaveDescriptionLength() | Gets the maximum allowed length for save file descriptions. |
| GetSaveGamesMetadata() | Gets the metadata for the save games for the current application on the Board device. |
| LoadSaveGame(string) | Loads and returns a saved game's payload. |
| LoadSaveGameCoverImage(string) | Loads the cover image for a saved game. |
| RemoveActiveProfileFromSaveGame(string) | Removes only the active profile from the specified saved game. If the saved game is not associated with any profiles after removal, the saved game is deleted. |
| RemovePlayersFromSaveGame(string) | Removes players from the specified saved game and returns a value asynchronously indicating whether the operation was successful. If the saved game is not associated with any active players, the saved game is deleted. |
| UpdateSaveGame(string, byte[], BoardSaveGameMetadataChange) | Updates an existing saved game with new payload and metadata. |
