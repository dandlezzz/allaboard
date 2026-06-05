> Source: https://docs.dev.board.fun/unity/api/Board.Core

# Board.Core Namespace

| Classes | Description |
| --- | --- |
| BoardApplication | Provides access to the application's runtime data and settings. |
| BoardGeneralSettings | Encapsulates all general settings for the Board platform. |
| BoardPlayer | Represents a player on Board. |
| BoardSupport | Provides access to Board's touch input. |

| Structs | Description |
| --- | --- |
| BoardPauseAudioTrack | Represents an audio track to display in Board's pause screen. |
| BoardPauseCustomButton | Represents a custom button for Board's pause screen. |
| BoardPauseScreenContext | Encapsulates all the context for Board's pause screen. |

| Enums | Description |
| --- | --- |
| BoardLogLevel | Specifies the level of logging in the Board SDK. |
| BoardPauseAction | Specifies the action type for a button in the Board pause screen. |
| BoardPauseButtonIcon | Specifies the icon type for a button in the Board pause screen. |
| BoardPlayerType | Specifies the type of a player on Board. |

| Delegates | Description |
| --- | --- |
| BoardPlayerAvatarLoadedHandler(BoardPlayer) | Handles the `avatarLoaded` event of a BoardPlayer class. |
| PauseScreenActionReceivedHandler(BoardPauseAction, BoardPauseAudioTrack[]) | Handles the `pauseScreenActionReceived` event of the BoardApplication class. |
| PauseScreenCustomButtonPressedHandler(string, BoardPauseAudioTrack[]) | Handles the `customPauseScreenButtonPressed` event of the BoardApplication class. |
