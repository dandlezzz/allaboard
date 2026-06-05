# Board Unity SDK — Documentation Mirror

Local mirror of the Board developer documentation (https://docs.dev.board.fun/), focused on the **Unity SDK** plus the shared concept / guide / tool / reference pages the Unity SDK relies on. Fetched 2026-06-04 (UTC-7).

Each file begins with a one-line header recording its source URL and fetch timestamp. Godot- and Web-specific SDK pages were intentionally skipped; shared guides include their Godot/Web code samples since the docs interleave them.

## Top-level / Shared

| Title | Source URL | Local file |
| --- | --- | --- |
| Overview — Building for Board | https://docs.dev.board.fun/ | `overview.md` |
| Getting Started (pick an SDK) | https://docs.dev.board.fun/getting-started | `getting-started.md` |
| FAQ (Developer Program) | https://docs.dev.board.fun/faq | `faq.md` |

## Learn

| Title | Source URL | Local file |
| --- | --- | --- |
| Learn (index) | https://docs.dev.board.fun/learn/ | `learn-index.md` |
| Concepts | https://docs.dev.board.fun/learn/concepts | `learn-concepts.md` |
| Architecture (platform pipeline) | https://docs.dev.board.fun/learn/architecture | `learn-architecture.md` |
| Touch (touch system) | https://docs.dev.board.fun/learn/touch-system | `learn-touch-system.md` |
| Pieces | https://docs.dev.board.fun/learn/pieces | `learn-pieces.md` |
| Hardware | https://docs.dev.board.fun/learn/hardware | `learn-hardware.md` |

## Guides (shared, all SDKs)

| Title | Source URL | Local file |
| --- | --- | --- |
| Guides (index) | https://docs.dev.board.fun/guides/ | `guides-index.md` |
| Touch (touch input) | https://docs.dev.board.fun/guides/touch-input | `guides-touch-input.md` |
| Piece Interaction Design | https://docs.dev.board.fun/guides/piece-interaction-design | `guides-piece-interaction-design.md` |
| Player Management | https://docs.dev.board.fun/guides/player-management | `guides-player-management.md` |
| Profile Switcher | https://docs.dev.board.fun/guides/profile-switcher | `guides-profile-switcher.md` |
| Pause Menu | https://docs.dev.board.fun/guides/pause-menu | `guides-pause-menu.md` |
| Save Games | https://docs.dev.board.fun/guides/save-games | `guides-save-games.md` |
| Avatars | https://docs.dev.board.fun/guides/avatars | `guides-avatars.md` |
| App Lifecycle | https://docs.dev.board.fun/guides/app-lifecycle | `guides-app-lifecycle.md` |

## Unity SDK

| Title | Source URL | Local file |
| --- | --- | --- |
| Getting Started (Unity) | https://docs.dev.board.fun/unity/getting-started/ | `unity-getting-started.md` |
| Quick Start | https://docs.dev.board.fun/unity/getting-started/quick-start | `unity-getting-started-quick-start.md` |
| Setup Reference | https://docs.dev.board.fun/unity/getting-started/setup-reference | `unity-getting-started-setup-reference.md` |
| Build & Deploy | https://docs.dev.board.fun/unity/getting-started/deploy | `unity-getting-started-deploy.md` |
| Upgrading the SDK | https://docs.dev.board.fun/unity/getting-started/upgrading | `unity-getting-started-upgrading.md` |
| Sample Scene | https://docs.dev.board.fun/unity/getting-started/sample | `unity-getting-started-sample.md` |
| Unity SDK Architecture | https://docs.dev.board.fun/unity/architecture | `unity-architecture.md` |
| Performance | https://docs.dev.board.fun/unity/performance | `unity-performance.md` |
| Simulator | https://docs.dev.board.fun/unity/simulator | `unity-simulator.md` |
| AI Assistant Setup | https://docs.dev.board.fun/unity/ai-assistant | `unity-ai-assistant.md` |
| Changelog | https://docs.dev.board.fun/unity/changelog | `unity-changelog.md` |

## Unity API Reference

### Namespace indexes

| Title | Source URL | Local file |
| --- | --- | --- |
| API Reference (assembly index) | https://docs.dev.board.fun/unity/api/ | `unity-api.md` |
| Board.Core namespace | https://docs.dev.board.fun/unity/api/Board.Core | `unity-api-Board.Core.md` |
| Board.Input namespace | https://docs.dev.board.fun/unity/api/Board.Input | `unity-api-Board.Input.md` |
| Board.Input.Debug namespace | https://docs.dev.board.fun/unity/api/Board.Input.Debug | `unity-api-Board.Input.Debug.md` |
| Board.Input.Simulation namespace | https://docs.dev.board.fun/unity/api/Board.Input.Simulation | `unity-api-Board.Input.Simulation.md` |
| Board.Save namespace | https://docs.dev.board.fun/unity/api/Board.Save | `unity-api-Board.Save.md` |
| Board.Session namespace | https://docs.dev.board.fun/unity/api/Board.Session | `unity-api-Board.Session.md` |

### Type pages (all types in the assembly)

**Board.Core**

| Type | Source URL | Local file |
| --- | --- | --- |
| BoardApplication (class) | https://docs.dev.board.fun/unity/api/BoardApplication.html | `unity-api-BoardApplication.md` |
| BoardGeneralSettings (class) | https://docs.dev.board.fun/unity/api/BoardGeneralSettings.html | `unity-api-BoardGeneralSettings.md` |
| BoardPlayer (class) | https://docs.dev.board.fun/unity/api/BoardPlayer.html | `unity-api-BoardPlayer.md` |
| BoardSupport (class) | https://docs.dev.board.fun/unity/api/BoardSupport.html | `unity-api-BoardSupport.md` |
| BoardPauseScreenContext (struct) | https://docs.dev.board.fun/unity/api/BoardPauseScreenContext.html | `unity-api-BoardPauseScreenContext.md` |
| BoardPauseCustomButton (struct) | https://docs.dev.board.fun/unity/api/BoardPauseCustomButton.html | `unity-api-BoardPauseCustomButton.md` |
| BoardPauseAudioTrack (struct) | https://docs.dev.board.fun/unity/api/BoardPauseAudioTrack.html | `unity-api-BoardPauseAudioTrack.md` |
| BoardLogLevel (enum) | https://docs.dev.board.fun/unity/api/BoardLogLevel.html | `unity-api-BoardLogLevel.md` |
| BoardPauseAction (enum) | https://docs.dev.board.fun/unity/api/BoardPauseAction.html | `unity-api-BoardPauseAction.md` |
| BoardPauseButtonIcon (enum) | https://docs.dev.board.fun/unity/api/BoardPauseButtonIcon.html | `unity-api-BoardPauseButtonIcon.md` |
| BoardPlayerType (enum) | https://docs.dev.board.fun/unity/api/BoardPlayerType.html | `unity-api-BoardPlayerType.md` |

**Board.Input**

| Type | Source URL | Local file |
| --- | --- | --- |
| BoardInput (class) | https://docs.dev.board.fun/unity/api/BoardInput.html | `unity-api-BoardInput.md` |
| BoardInputSettings (class) | https://docs.dev.board.fun/unity/api/BoardInputSettings.html | `unity-api-BoardInputSettings.md` |
| BoardContactPhaseExtensions (class) | https://docs.dev.board.fun/unity/api/BoardContactPhaseExtensions.html | `unity-api-BoardContactPhaseExtensions.md` |
| BoardUIInputModule (class) | https://docs.dev.board.fun/unity/api/BoardUIInputModule.html | `unity-api-BoardUIInputModule.md` |
| BoardContact (struct) | https://docs.dev.board.fun/unity/api/BoardContact.html | `unity-api-BoardContact.md` |
| BoardContactTypeMask (struct) | https://docs.dev.board.fun/unity/api/BoardContactTypeMask.html | `unity-api-BoardContactTypeMask.md` |
| BoardContactPhase (enum) | https://docs.dev.board.fun/unity/api/BoardContactPhase.html | `unity-api-BoardContactPhase.md` |
| BoardContactType (enum) | https://docs.dev.board.fun/unity/api/BoardContactType.html | `unity-api-BoardContactType.md` |

**Board.Input.Debug / Board.Input.Simulation**

| Type | Source URL | Local file |
| --- | --- | --- |
| BoardContactDebugView (class) | https://docs.dev.board.fun/unity/api/BoardContactDebugView.html | `unity-api-BoardContactDebugView.md` |
| BoardContactSimulationIcon (class) | https://docs.dev.board.fun/unity/api/BoardContactSimulationIcon.html | `unity-api-BoardContactSimulationIcon.md` |

**Board.Save**

| Type | Source URL | Local file |
| --- | --- | --- |
| BoardSaveGameManager (class) | https://docs.dev.board.fun/unity/api/BoardSaveGameManager.html | `unity-api-BoardSaveGameManager.md` |
| BoardSaveGameMetadata (class) | https://docs.dev.board.fun/unity/api/BoardSaveGameMetadata.html | `unity-api-BoardSaveGameMetadata.md` |
| BoardSaveGameMetadataChange (class) | https://docs.dev.board.fun/unity/api/BoardSaveGameMetadataChange.html | `unity-api-BoardSaveGameMetadataChange.md` |
| BoardSaveGameMetadataExtensions (class) | https://docs.dev.board.fun/unity/api/BoardSaveGameMetadataExtensions.html | `unity-api-BoardSaveGameMetadataExtensions.md` |
| BoardSaveGamePlayer (class) | https://docs.dev.board.fun/unity/api/BoardSaveGamePlayer.html | `unity-api-BoardSaveGamePlayer.md` |
| ImageProcessor (class) | https://docs.dev.board.fun/unity/api/ImageProcessor.html | `unity-api-ImageProcessor.md` |
| BoardAppStorageInfo (struct) | https://docs.dev.board.fun/unity/api/BoardAppStorageInfo.html | `unity-api-BoardAppStorageInfo.md` |

**Board.Session**

| Type | Source URL | Local file |
| --- | --- | --- |
| BoardSession (class) | https://docs.dev.board.fun/unity/api/BoardSession.html | `unity-api-BoardSession.md` |
| BoardSessionPlayer (class) | https://docs.dev.board.fun/unity/api/BoardSessionPlayer.html | `unity-api-BoardSessionPlayer.md` |
| BoardAIPlayerType (struct) | https://docs.dev.board.fun/unity/api/BoardAIPlayerType.html | `unity-api-BoardAIPlayerType.md` |

## Tools

| Title | Source URL | Local file |
| --- | --- | --- |
| Tools (index) | https://docs.dev.board.fun/tools/ | `tools-index.md` |
| Board Connect | https://docs.dev.board.fun/tools/board-connect | `tools-board-connect.md` |
| bdb (Board Developer Bridge) | https://docs.dev.board.fun/tools/bdb | `tools-bdb.md` |

## More

| Title | Source URL | Local file |
| --- | --- | --- |
| More (index) | https://docs.dev.board.fun/more/ | `more-index.md` |
| License (Developer Terms of Use) | https://docs.dev.board.fun/more/license | `more-license.md` |
| Support | https://docs.dev.board.fun/more/support | `more-support.md` |

---

## Failed / Not downloaded

**No links failed.** Every Unity-relevant and shared page resolved successfully. Two pages (`BoardAppStorageInfo` and a few large guides) returned a single timeout / temp-file response but succeeded on retry, and all their content was captured. Nothing is missing from the Unity doc set.

The only Unity API pages not saved as standalone files are the **three delegate-signature pages** in Board.Core (`BoardPlayerAvatarLoadedHandler`, `PauseScreenActionReceivedHandler`, `PauseScreenCustomButtonPressedHandler`) — their full signatures are already documented in `unity-api-Board.Core.md` and in `unity-api-BoardApplication.md` / `unity-api-BoardPlayer.md`, so they were not duplicated.

The following were **intentionally not fetched** (out of scope per the task — Godot/Web-specific SDK pages):

- `/godot/...` (all Godot SDK pages: ai-assistant, architecture, changelog, getting-started/*, off-device-development, performance, reference/api)
- `/web/...` (all Web SDK pages: ai-assistant, architecture, changelog, getting-started/*, reference/api)
