> Source: https://docs.dev.board.fun/unity/getting-started/setup-reference

# Setup Reference

Detailed configuration reference for Board SDK projects.

Looking for the quick path? See Quick Start to get running in 5 minutes.

## Developer Access

The Board SDK and deployment tools are provided separately to developers in the program. If you don't have access yet, request access here.

Once approved, you'll receive:

- Board SDK - Unity package (.tgz) for touch input, session management, save games, and pause screen integration
- Piece Set Models - Machine learning models (.tflite) for Piece recognition

You'll deploy your builds to Board hardware with Board Connect, the device's built-in HTTP API, driven by the `board-connect` CLI. See Install Board Connect below.

## Prerequisites

Before installing the SDK:

- Unity 2021.3 or newer (2022.3 LTS recommended) (Unity 6 also supported)
- Android Build Support module installed via Unity Hub
- Unity Input System package (1.7.0+)

Windows Users: You may need to run Unity as Administrator for the first build if you encounter "SDK directory is not writable" errors.

## Create a New Unity Project

If starting fresh:

1. Open Unity Hub and click New Project
2. Select a template (2D or 3D, both work with Board)
3. Both the Built-in Render Pipeline and Universal Render Pipeline (URP) are supported.

Note: The High Definition Render Pipeline (HDRP) is not supported on Android and is not compatible with Board.

## Install the SDK

### Step 1: Open Package Manager

In Unity, select Window > Package Manager from the menu bar.

### Step 2: Add Package from Tarball

Click the + button in the top-left of the Package Manager window and select Add package from tarball….

### Step 3: Select the SDK Package

Navigate to your downloaded SDK file (the .tgz file you received) and click Open.

The package will install as "Board SDK" in your project.

Collaborative Projects: If you're sharing your project via source control, consider storing the SDK tarball inside your project directory (but not inside the `Assets` folder or Unity's cache folders). This ensures collaborators can locate the package without needing the original download. This is general Unity best practice for any tarball-based package, not specific to the Board SDK.

## Install Board Connect

Board Connect is the device's built-in HTTP API for deploying and managing apps over your LAN (port 8843), so there's no USB cable involved. You drive it with the `board-connect` CLI, which works the same on macOS, Linux, and Windows.

Legacy: The USB-serial `bdb` workflow is still available if you need it. See the bdb reference. Board Connect is the documented path.

### Install the CLI

macOS and Linux:

```
curl -fsSL https://dev.board.fun/connect/install | sh
```

Windows (PowerShell):

```
irm https://dev.board.fun/connect/install.ps1 | iex
```

The installer drops a single binary at `~/.local/bin/board-connect` (on Windows, `%USERPROFILE%\.local\bin\board-connect.exe`). Add that directory to your `PATH` if it isn't already.

### Verify Installation

Open a terminal and run:

```
board-connect --version
```

You should see the CLI version printed.

### Pair with your Board

Pair once to authorize your machine. The Board shows its address under Settings > System.

```
board-connect pair <addr>
```

The Board displays an approval prompt on screen; tap Approve on the device. The CLI saves that Board as your default, so later commands (`install`, `launch`, `logs`, and so on) take no address. Run `board-connect pair` with no address to discover a Board on your network instead.

## Project Setup Wizard

The SDK includes a setup wizard that configures your Unity project automatically.

1. Open Board > Configure Unity Project…
2. Review the settings (all should be checked by default)
3. Click Apply Selected Settings

The wizard configures platform settings, API levels, scripting backend, and Input System. Settings already configured correctly show [OK] and are skipped.

Note: Enabling the Input System requires an editor restart. The wizard will prompt you when this is needed.

## Platform Settings Reference

The setup wizard configures these settings automatically. This section documents the required values for reference.

### Android Platform

Board requires Android as the build target:

- Platform: Android
- Minimum API Level: Android 13.0 (API Level 33)
- Target API Level: Android 13.0 (API Level 33)
- Scripting Backend: IL2CPP
- Target Architectures: ARM64 (required; ARMv7/x86 optional)

Unity 6 Only: Application Entry Point must be set to Activity (not "Game Activity").

### Screen Orientation

Board is a landscape tabletop device. Apps must use Landscape Left orientation:

- Default Orientation: Landscape Left (required)

Important: Other orientations cause touch input to not register correctly. The build will fail if the default orientation is not set to Landscape Left.

### Input System

Board requires Unity's Input System package (version 1.7.0 or higher). The setup wizard enables this automatically.

To verify manually:

1. Open Window > Package Manager
2. Confirm Input System is installed
3. In Edit > Project Settings > Player > Other Settings, verify Active Input Handling is set to Input System Package (New)

## Board SDK Settings

The SDK uses two settings assets that are created automatically on first import.

### Application ID (BoardGeneralSettings)

Your application needs a unique identifier for Board platform services including session management and save games.

The SDK automatically creates a BoardGeneralSettings asset with a generated UUID when first imported. To view or modify:

1. Open Edit > Project Settings > Board > General Settings
2. The Application ID is displayed
3. Click Regenerate to generate a new UUID if needed

Important: The build will fail if the Application ID is empty. This ID identifies your application for player sessions and save game data. Changing the Application ID will make existing save games inaccessible.

### Input Settings (BoardInputSettings)

The SDK automatically creates a BoardInputSettings asset at `Assets/Board/Settings/BoardInputSettings.asset` when first imported.

To view or modify:

1. Open Edit > Project Settings > Board > Input Settings
2. Configure the settings as needed

| Setting | Description |
| --- | --- |
| Piece Set Model | The model file (.tflite) used for Piece recognition |
| Translation Smoothing | Smoothing applied to Piece position changes (0–1) |
| Rotation Smoothing | Smoothing applied to Piece rotation changes (0–1) |
| Persistence | How long Pieces remain tracked after losing contact |

For detailed information on these parameters, see the Touch Input guide.

## Piece Set Models

Piece Set Models are machine learning models (.tflite files) that enable the SDK to recognize specific Piece Sets. Each Piece Set (e.g., Board Arcade, Mushka) has its own model.

### Downloading Models

You can download Piece Set Models directly from the Unity Editor:

1. Open Edit > Project Settings > Board > Input Settings
2. Click Load Available Models to fetch the list of available models
3. Select a model from the dropdown
4. Click Select to download and configure it

The model is downloaded to `Assets/StreamingAssets/` and automatically configured in your active BoardInputSettings.

Tip: Models can also be downloaded manually from the Developer Portal. Place the `.tflite` file in `Assets/StreamingAssets/` and enter the filename in your BoardInputSettings.

For using multiple input settings assets and switching between them at runtime, see BoardInputSettings.

## Scene Setup

### Using BoardUIInputModule

Board's SDK blocks system-level touch events, so Unity's standard InputSystemUIInputModule will not receive touch input on Board hardware. Add BoardUIInputModule to enable UI interaction.

Quick Setup: Use Board > Input > Add BoardUIInputModule to EventSystems to automatically add the module to all EventSystems in your open scenes.

Manual Setup:

1. Find your EventSystem GameObject in the scene hierarchy
2. Add a BoardUIInputModule component

BoardUIInputModule processes finger touches for UI while ignoring Piece contacts, preventing accidental button presses from Pieces.

Module Coexistence: {#ui-input}

BoardUIInputModule automatically disables any competing input modules (such as `InputSystemUIInputModule`) on the same EventSystem when running on Board hardware. You can keep both modules on your EventSystem and the SDK will handle the rest at runtime.

In the Unity Editor, auto-disabling is off so that mouse and keyboard input work normally through `InputSystemUIInputModule`.

Build Warning: The SDK warns during Android builds if any EventSystem is missing BoardUIInputModule. This is a warning (not an error) because some projects add the module at runtime.

### Initializing the SDK

The Board SDK initializes automatically when your app starts. No explicit initialization call is required for basic input functionality.

For session management and save games, see:

- Player Management
- Save Game System

## Build Validation

The SDK validates your project configuration when building for Android. Errors will fail the build; warnings are informational.

Errors (build fails):

- Missing Application ID
- Minimum API Level below 33
- ARM64 not enabled in Target Architectures
- Application Entry Point not set to Activity (Unity 6 only)
- Default Screen Orientation not set to Landscape Left

Warnings:

- Target API Level below 33
- Scripting Backend not set to IL2CPP
- Input System not enabled
- EventSystem missing BoardUIInputModule
- Both BoardUIInputModule and InputSystemUIInputModule enabled

Run Board > Configure Unity Project… to fix configuration issues.

## Project Checklist

Before building, verify:

- Setup wizard run (Board > Configure Unity Project…)
- Application ID configured (auto-generated on import)
- Screen orientation set to Landscape Left
- Piece Set Model downloaded and configured
- BoardUIInputModule added to EventSystems

## Verify SDK Installation

To confirm the SDK installed correctly:

1. Open Window > Package Manager
2. Find "Board SDK" in the list of installed packages
3. Check that the version matches what you downloaded

The SDK package includes:

- Runtime libraries for Board input, sessions, and save games
- Editor tools including the Simulator
- Sample scenes demonstrating SDK features

## Next Steps

- Build & Deploy - Build your project and deploy to Board
- Sample Scene - See working examples
- Simulator - Test input without hardware
