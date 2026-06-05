> Source: https://docs.dev.board.fun/unity/getting-started/quick-start

# Quick Start

Get a sample running on Board hardware in 5 minutes.

## Prerequisites

- Unity 2021.3 or newer (2022.3 LTS recommended) with Android Build Support (IL2CPP)
- Download the SDK (.tgz package)
- The `board-connect` CLI (installed in Build & Deploy below)

## 1. Install the SDK

1. Open Window > Package Manager
2. Click + and select Add package from tarball…
3. Select your downloaded `.tgz` file

## 2. Run Project Setup Wizard

1. Open Board > Configure Unity Project…
2. Click Apply Selected Settings

The wizard configures Android platform, API levels, and Input System automatically.

## 3. Download a Piece Set Model

1. Open Edit > Project Settings > Board > Input Settings
2. Click Load Available Models
3. Select a model and click Select

## 4. Import the Sample

1. Open Window > Package Manager
2. Select Board SDK from the list
3. Expand Samples and click Import next to "BoardInput Sample"

## 5. Build & Deploy

### Build the APK

In Unity, open File > Build Settings, confirm the platform is Android, and click Build to produce an `.apk`. The package name comes from Edit > Project Settings > Player > Other Settings > Package Name (for example `com.yourcompany.yourgame`); you'll use it later to launch and view logs.

### Install the board-connect CLI

The Board exposes Board Connect, its built-in HTTP API over the LAN, so there's no USB cable involved. The `board-connect` CLI drives it.

macOS and Linux:

```
curl -fsSL https://dev.board.fun/connect/install | sh
```

Windows (PowerShell):

```
irm https://dev.board.fun/connect/install.ps1 | iex
```

The installer drops a single binary at `~/.local/bin/board-connect`. Add that directory to your `PATH` if it isn't already, then verify:

```
board-connect --version
```

### Pair with your Board (once)

The Board shows its address under Settings > System. Pair once; this saves the Board as your default, so every later command resolves the target on its own.

```
board-connect pair <address>
```

Tap Approve on the device when the prompt appears.

### Deploy

Install the APK and launch it in a single step. No address is needed once paired.

```
board-connect install path/to/your-game.apk --launch
```

After install, your game also lives under Settings > Sideloaded Apps on the Board's Home screen. To launch or tail logs later, pass the package name from your Player Settings:

```
board-connect launch com.yourcompany.yourgame
board-connect logs com.yourcompany.yourgame --follow
```

Prefer a browser? Open the Board Connect web UI at the address shown under Settings > System, drag your `.apk` onto the page to install, and launch from the Library or Sideloaded Apps.

The legacy `bdb` (USB) path still works if you need it; see the bdb reference.

## Next Steps

- Sample Scene - Understand the sample code
- Touch Input - Build your own interactions
- Setup Reference - Detailed configuration options
