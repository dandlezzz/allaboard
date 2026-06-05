> Source: https://docs.dev.board.fun/unity/api/BoardApplication.html

# BoardApplication Class

Namespace: Board.Core

Provides access to the application's runtime data and settings.

```csharp
public static class BoardApplication
```

Inheritance: System.Object → BoardApplication

| Methods | Description |
| --- | --- |
| ClearPauseScreenContext() | Clears the current pause screen context and resets tracked state. |
| Exit() | Exits the application. Functions similar to swiping the application away from the Recent Apps screen. This is a fire-and-forget operation; the app will be terminated immediately and cannot receive a response. |
| HideProfileSwitcher() | Hides the profile switcher button overlay. |
| SetPauseScreenContext(BoardPauseScreenContext) | Sets the pause screen context for the current application. This performs a full replacement of all pause screen settings. Unspecified parameters will be set to their default values. |
| SetPauseScreenContext(string, Nullable, BoardPauseCustomButton[], BoardPauseAudioTrack[]) | Sets the pause screen context for the current game with full replacement. This replaces ALL pause screen settings. Unspecified optional parameters will use default values. |
| ShowProfileSwitcher() | Shows the profile switcher button overlay in the top-left corner. |
| UpdatePauseScreenContext(string, Nullable, BoardPauseCustomButton[], BoardPauseAudioTrack[]) | Updates specific fields of the pause screen context while preserving others. Only the parameters you specify will be updated. Pass an empty array to clear a field (e.g., customButtons: new BoardPauseCustomButton[0]). |

| Events | Description |
| --- | --- |
| customPauseScreenButtonPressed | Occurs when a custom button is pressed in Board's pause screen. |
| pauseScreenActionReceived | Occurs when an action is received from Board's pause screen. |
