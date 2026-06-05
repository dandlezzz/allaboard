> Source: https://docs.dev.board.fun/tools/bdb — fetched 2026-06-04T18:38 (UTC-7)

# bdb (Board Developer Bridge)

Legacy. `bdb` is being phased out in favor of Board Connect and may be deprecated in a future release. Existing `bdb` workflows will keep working for the foreseeable future, but new workflows, scripts, and integrations should be built on Board Connect rather than `bdb`. See Tools for the comparison.

`bdb` is the original USB-based command-line tool for installing and managing builds on Board hardware. It works the same way regardless of which SDK you build with: any Board-compatible APK can be installed, launched, and inspected with `bdb`.

Getting `bdb`: Board Developer Bridge is distributed separately from the SDKs. Request access if you don't have it yet.

## Connect Board

Connect Board to your computer via USB-C. The developer service runs automatically on Board, so `bdb` auto-detects the connection.

### Check connection

```bash
bdb status
```

Displays connection status and device information.

### Check Board OS version

```bash
bdb version
```

Displays the Board OS version. `bdb` requires Board OS 1.3.8 or later.

## Install your app

```bash
bdb install path/to/your-game.apk
```

`bdb` uploads the APK and installs it on Board, showing upload progress and a success message when complete.

```
$ bdb install ./Builds/MyGame.apk
Installing MyGame.apk (45000000 bytes)
Detected Board at /dev/cu.usbmodem14201 (Serial: BD12345)
Uploading: 100%
Installation successful
```

## Launch and stop

```bash
bdb launch com.yourcompany.yourgame
bdb stop com.yourcompany.yourgame
```

The package name is whatever you configured as your app's application ID at build time. To list packages you've installed via `bdb`:

```bash
bdb list
```

## Accessing sideloaded games on Board

After installing a game with `bdb install`, you can launch it from the device itself. Open Settings > Sideloaded from the Home screen to see all games installed via `bdb`, and launch any of them directly.

Note: The Sideloaded panel only appears when at least one game has been sideloaded, and is only visible when Settings is opened from the Home screen (not while a game is running). Requires Board OS 1.6.2+.

## Viewing logs

```bash
bdb logs com.yourcompany.yourgame
```

Press Control+C to stop streaming.

## Managing apps

```bash
# Remove a single developer-installed app
bdb remove com.yourcompany.yourgame

# Remove all apps you've installed via bdb (system apps are preserved)
bdb cleanup
```

## Command reference

| Command | Description |
| --- | --- |
| `bdb version` | Show Board OS version |
| `bdb status` | Check connection status |
| `bdb install <apk>` | Install an APK to Board |
| `bdb launch <package>` | Launch an installed app |
| `bdb stop <package>` | Stop a running app |
| `bdb logs <package>` | Stream logs from an app |
| `bdb list` | List developer-installed apps |
| `bdb remove <package>` | Remove an installed app |
| `bdb cleanup` | Remove all developer-installed apps |
| `bdb list-ports` | List available serial ports (debugging) |
| `bdb help` | Show help message |

## Troubleshooting

### Board not detected
1. Verify the USB cable is connected (data-capable, not charge-only)
2. Try a different USB port
3. Run `bdb list-ports` to see available serial ports

### Installation fails
- Ensure the APK was built for ARM64 (`arm64-v8a`)
- Verify sufficient storage on Board (`bdb status` shows device info)
- Check any SDK-specific build requirements (see your SDK's Build & Deploy guide)
