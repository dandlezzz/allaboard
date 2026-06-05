> Source: https://docs.dev.board.fun/unity/changelog — fetched 2026-06-04T18:38 (UTC-7)

# Changelog

All notable changes to the Board Unity SDK.

---

## [3.3.0] - April 3, 2026

### Added
- AI player support - Games can register AI player types (e.g., "Easy", "Hard") via BoardSession.SetAIPlayerTypes(). Registered types appear as options in the player selector. The game is responsible for all AI behavior; the SDK provides the UI and session integration. See AI Players.
- Automatic input module management - BoardUIInputModule now automatically disables competing input modules (such as `InputSystemUIInputModule`) on Board hardware. No manual toggling or platform conditionals needed.
- Landscape Left build validation - Android builds now fail if the default screen orientation is not set to Landscape Left, preventing silent touch input issues on device.
- New input simulator icon sets for the `Omakase` and `Thrasos` Piece Sets
- Contact type and Glyph ID overlay in input simulator UI

### Improved
- Setup wizard UX
- Save game operations now throw `InvalidOperationException` if a second async call is made while one is already in-flight, preventing silent state corruption

### Fixed
- Corrected rotation convention in API docs from clockwise to counter-clockwise from vertical

---

## [3.2.1] - January 30, 2026

### Added
- New editor button for `BoardGeneralSettings` that generates a new application identifier

### Fixed
- Update `BoardUIInputModule` to be compatible with UI Toolkit's pointer limit

---

## [3.2.0] - January 29, 2026

### Added
- New project setup wizard to streamline setting up a project for Board

### Improved
- Error messages for build failures

### Changed
- Renamed `glyphModelFilename` to `pieceSetModelFilename` in `BoardInputSettings`

### Fixed
- Prevent save game APIs from executing before SDK services are initialized
- Ability to delete an icon out of a simulator icon palette that is not editable

---

## [3.1.0] - January 23, 2026

### Added
- Ability to switch active input simulator icon palette programmatically by name

### Improved
- End to end observability and validation for save game API
- Error messages for build failures

### Fixed
- Input simulator not canceling a contact when swapping icons
- Assertion in editor if `BoardInputSettings` does not exist

---

## [3.0.0] - December 15, 2025

Initial public release of the Board Unity SDK.
