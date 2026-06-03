# Changelog
All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](http://keepachangelog.com/en/1.0.0/) and this project adheres to [Semantic Versioning](http://semver.org/spec/v2.0.0.html).

## [3.3.0] - 2026-04-30
### Quality of life upgrades and new AI player type
- Added support for new user type `AI` that allows developers to add support for AI players.
- Added new input simulator icon sets for the `Omakase` and `Thrasos` piece sets.
- Added contact type and glyph ID information to input simulator UI.
- Improved UX for the Board setup wizard.
- Added ability for `BoardUIInputModule` to disable all other input modules.
- Support for derived classes in `BoardInputSettingsEditor`.
- Improved save game operation robustness.

## [3.2.1] - 2026-01-30
### Fix compatibility with UI Toolkit
- Update `BoardUIInputModule` to be compatible with UI Toolkit's pointer limit.
- New editor button for `BoardGeneralSettings` that generates a new application identifier.

## [3.2.0] - 2026-01-29
### Improved project setup experience and bug fixes
- Added new project setup wizard to streamline setting up a project for Board.
- Improved error messages for build failures.
- Renamed `glyphModelFilename` to `pieceSetModelFilename` in `BoardInputSettings`
- Bugfix: Prevent save game APIs from executing before SDK services are initialized.
- Bugfix: Fix ability to delete an icon out of a simulator icon palette that is not editable.

## [3.1.0] - 2026-01-23
### Improved save game observability and bug fixes.
- Add ability to switch active input simulator icon palette programmatically by name.
- Improved end to end observability and validation for save game API.
- Improved error messages for build failures.
- Bugfix: Fix input simulator not canceling a contact when swapping icons.
- Bugfix: Fix assertion in editor if `BoardInputSettings` does not exist.

## [3.0.0] - 2025-12-15
### Initial public release
- Changed package name from `co.harrishill.board` to `fun.board`.
- Changed root namespace from `HarrisHill.Board` to `Board`.
- Updated license and documentation URLs.
- UI and Quality of life improvements to the input simulator.
- Added new simulator icon palettes for Mushka, Save the Bloogs, Board Arcade, and Chop Chop.
- Bugfix: Fix null reference exception in the input simulator when removing a contact that was still rotating.

## [2.2.4] - 2025-11-24
### Bug fixes.
- Bugfix: Fix profile switcher not reappearing after waking device.
- Bugfix: Fix crashes from switching `BoardInputSettings` at runtime.

## [2.2.3] - 2025-11-05
### Bug fix.
- Bugfix: Fix missing or duplicate active profile at startup.

## [2.2.2] - 2025-11-03
### Bug fixes.
- Bugfix: Fix profile switcher not reappearing after closing it.
- Bugfix: Fix wrong avatar sometimes being loaded for a player.

## [2.2.1] - 2025-10-25
### Access to active user profile and bug fixes.
- Adds new `BoardSession.activeProfile` property for accessing the user profile in the profile switcher as well as `BoardSession.activeProfileChanged` event for when the active profile changes.
  - New method `BoardSaveGameManager.RemoveActiveProfileFromSaveGame` added to allow for removing the active profile from a saved game.
  - NOTE: `BaseBoardPlayer` has been renamed to `BoardPlayer`. The class previously named `BoardPlayer` has been renamed to `BoardSessionPlayer`.
- Improved memory and lifecycle management to reduce crashes.
- Bugfix: Fix bug where contacts were orphaned when opening the profile selector.

## [2.2.0] - 2025-10-20
### Updated Pause Menu interface and modified active profile session replacement.
- Adds new `BoardApplication.UpdatePauseScreenContext()` method for partial updates to the pause menu
  - Preserves existing settings when updating only specific fields (e.g. buttons, audio tracks, etc.).
  - Tracks current pause screen state internally to enable merge-based updates.
  - Updated documentation to clarify the difference between `SetPauseScreenContext()` (full replacement) and `UpdatePauseScreenContext()` (partial update).
- Modified active profile session replacement behavior with profile switcher
  - When the active profile changes, automatically replaces the previous active profile in the game session if present, maintaining session continuity.
- Changed when Board SDK initializes to BEFORE the first scene loads instead of after.

## [2.1.0] - 2025-10-15
### Saved Games, Session, and Pause Screen features implemented.
- Complete save game system implementation with metadata management, cover images, and data integrity
  - `BoardSaveGameSystem` replaced by static class `BoardSaveGameManager`
- List of players currently in game session and ability to add, remove, or replace players via player selector UI.
- Profile switcher overlay control for multi-profile support.
- Enhanced pause screen with custom contexts, font color styles, and icon customization.
- Avatar loading and management system with default avatar support.
- New `BoardApplication` class for application-level controls (exit, profile switching, pause screen)
- Touch performance refinement.

## [2.0.1] - 2025-06-14
### Bug fix.
- Bugfix: Fix data formatting issue that prevented correct functioning of v1.x models.

## [2.0.0] - 2025-06-11
### New touch data frame handling.
- Modified handling of touch data frames to improve data consistency.

## [1.3.2] - 2025-06-02
### Bug fix.
- Bugfix: Fix gradle error that prevented making Android builds.

## [1.3.1] - 2025-05-28
### Tracker improvements and better memory management.
- Tracker improvements
  - Improved dampening on higher translation smoothing values (e.g., greater than 0.7) to remove the position oscillation.
  - Finger contacts can come in much closer proximity without merging. 
  - Bugfix: FastMode disabled is now functional.
- Improved memory management handling to reduce crashes.


## [1.3.0] - 2025-05-16
### New input debug view.
- New input debug view that can be enabled by setting `BoardInput.enableDebugView`. 
- New input setting for controlling fast tracking.
- Changes to input setting values.

## [1.2.1] - 2025-05-01
### Bug fixes.
- Bugfix: Fix bug with default editor path for `BoardGeneralSettings` on Windows.
- Bugfix: Fix bug with SDK failing to initialize for Unity 6.

## [1.2.0] - 2025-04-02
### New API's for user profiles and saved games.
- Added new asset `BoardGeneralSettings` for per title settings.
- Added new API definitions for user profiles that can be accessed via `BoardSession`.
- Added new API definitions for saved games that can be accessed via `BoardSaveGameSystem`.

## [1.1.5] - 2025-03-09
### Input tracking improvements.
- `isTouched` is functional and reports when a piece is detected as being touched by a human (i.e. a finger is physically in contact with a conductive area of the piece).
- Rework of tracking to maintain `contactId` across fast, erratic movement.
- Further tuning to position tracking.
- Bugfix: Improved positioning updates were not being correctly forwarded to Unity.
- Bugfix: Resolved memory leaks that were causing crashes.

## [1.1.4] - 2025-03-07
### Input tracking improvements.
- More robust jitter detection.
- Input tracker speed enhancement.
- Improved position tracking accuracy.

## [1.1.3] - 2025-03-06
### Input tracking improvements.
- Further rotation jitter refinement.
- Further reduction of tracking jumps.
- Bugfix: Fixed bug with lifecycle management handling.

## [1.1.2] - 2025-03-05
### Input tracking improvements.
- Made improvements to position and rotation tracking for input contacts.
- Bugfix: Fixed missing `Profiler.BeginSample` call in input system.

## [1.1.1] - 2025-02-28
### Input tracking bug fixes.
- Bugfix: Contacts no longer change glyph ID. Fingers no longer become glyphs or vice versa. 
- Bugfix: Resolved issue with model files sometimes failing to load.

## [1.1.0] - 2025-02-25
### Improved input tracking and bug fixes.
- Improved accuracy and speed of input tracking.
- Removed `Unique Glyph Count` setting from Board input settings.
- Bugfix: Fix Board input not working after suspending application.

## [1.0.0] - 2025-02-18
### New input tracking system.
- New input tracking system implemented
  - New native implementation of contact tracking.
  - `BoardInput.ConfigureInputSettings(BoardInputSettings settings)` has been replaced by property `BoardInput.settings`.
  - `BoardInputSettings` has new fields for the new tracking system.
  - `GlyphPattern` and `GlyphPatternDatabase` have been removed.
  - `Input` sample has been updated.
- Enabled multi-edit for `BoardContactSimulationIcon`.

## [0.4.0] - 2024-09-30
### New input API and improved input simulation.
- `BoardScreen` replaced by static class `BoardInput`
  - Board not longer integrates with Unity's New Input System.
  - `BoardInput.GetActiveContacts(BoardContactType.Finger)` and `BoardInput.GetActiveContacts(BoardContactType.Glyph)` replaces `BoardScreen.current.activeTouches` and `BoardScreen.current.activeGlyphs` respectively.
  - `BoardContact` has new and updated fields
    - `id` changed to `contactId`
    - `deltaScreenPosition` removed
    - `startScreenPosition` removed
    - `previousScreenPosition` added
    - `startTime` removed
    - `timestamp` added
    - `glyphPatternId` changed to `glyphId`
    - `previousOrientation` added
    - `bounds` added (not functional yet)
    - `isTouched` added (not functional yet)
  - Updated the "Using the Board SDK" section of the Getting Started Guide to reflect changes to the SDK.
- New editor window for input simulation. Accessible via main menu bar `Board -> Input -> Simulator`
  - New section titled "Simulate Board Input in the Unity Editor" added to the Getting Started Guide to show how to use simulator.
  - `SimulateGlyphInput` has been removed.

## [0.3.0] - 2024-06-13
### More flexible board input settings configuration and bug fixes.
- Board input settings (glyph types and counts) can be dynamically re-configured
  - New usage pattern: board input settings are now set via a separate method from the initialization routine, and can be called any number of times.
  - Updated the "Using the Board SDK" section of the Getting Started Guide to reflect changes to the SDK.
- Bugfix: IDs of new and already deleted contacts no longer clash.

## [0.2.1] - 2024-06-11
### Bug fix.
- Bugfix: Correctly converting detected coordinates from physical to screen space.

## [0.2.0] - 2024-06-10
### Updated detection & tracking system, new glyph patterns, and bug fixes.
- Improved performance when Pieces are close to each other and moved quickly
  - Supports "nested" pieces (e.g., Circle inside of Big Ring).
  - New usage pattern
    - The system only detects Pieces explicitly specified by type (e.g., Square, Triangle, etc) and maximum instances (e.g, 0 to 3). This approach improves detection and prevents false positives of unspecified Pieces.
    - If no Piece types and maximum instances are specified, the default is 1 instance of each Piece type.
    - System is not intended to and will not detect unspecified Pieces. Rather unspecified Pieces may be detected as a specified Piece since the system attempts to best fit the glyph.
  - Detection and tracking system has moved to native side which deprecates GlyphDetector and ActiveGlyph.
  - Updated the "Using the Board SDK" section of the Getting Started Guide to reflect changes to the SDK.
- Adding glyph patterns for Grabber Top and Grabber Bottom Pieces.
- Bugfix: Consider rotation when marking contact as Stationary or Moved.

## [0.1.2] - 2024-05-29
### New glyph pattern.
- Adding glyph patterns for new long block, cannon, and outer lens pieces
- Bugfix: Fix null reference exception in `GlyphDetector`

## [0.1.1] - 2024-05-22
### New glyph pattern.
- Adding glyph pattern for new triangle piece

## [0.1.0] - 2024-05-18
### New glyph patterns and bug fixes.
- Adding glyph patterns for new pieces
- Bugfix: Position and angle offsets are now applied to glyphs
- Bugfix: `SimulateGlyphInput` now works if there's no `EventSystem` present

## [0.0.1] - 2024-05-14
### Initial version.