> Source: https://docs.dev.board.fun/unity/getting-started/deploy — fetched 2026-06-04T18:38 (UTC-7)

# Build & Deploy

Build your Unity project into an APK and deploy it to Board hardware.

## Building your project

### Build an APK

1. Open File > Build Settings (Unity 6+: File > Build Profiles)
2. Ensure your scenes are added to the build list
3. Click Build and choose a location for your APK

Tip: For development builds, enable Development Build in Build Settings to access debugging features including the debug overlay.

### Player settings to verify

- Architecture: ARM64 (`arm64-v8a`). Board rejects APKs built for other architectures.
- Minimum API Level: API Level 33.
- Scripting backend: IL2CPP.
- Package Name (Player Settings > Other Settings): this is the package you'll pass to `board-connect launch`.

## Install and Launch

The Board exposes Board Connect, its built-in HTTP API, so there's no USB cable involved. The Board shows its address under Settings > System.

Legacy: `bdb` (USB serial) is still available if you need it. See the bdb reference. Board Connect is the documented path.

### From a browser (human)

1. Open the Board Connect web UI in your browser, using the address shown under Settings > System on the Board.
2. Drag your `.apk` onto the page to install it.
3. Launch the game from the Library or Settings > Sideloaded Apps.

### With an agent (board-connect)

The `board-connect` CLI is the agent-facing Board Connect client — no ADB, no scripts. Install it from dev.board.fun/connect/install. You only pass the Board's address to `pair`; that saves it as the default, so every later command resolves the target on its own:

1. `board-connect pair <addr>` — run once; the user taps Approve on the device.
2. `board-connect install path/to/your-game.apk --launch` — install the built APK and bring it to the foreground.
3. `board-connect launch com.yourcompany.yourgame` — launch it by package name.
4. `board-connect logs com.yourcompany.yourgame` — tail the app's logs (add `--follow` to stream live).
5. `board-connect screenshot --out shot.png` — capture the screen.

The `<package>` is your Unity Package Name (Player Settings > Other Settings). See the full Board Connect reference for pairing, status, and app management.

## Viewing Logs

Stream the app's logs over Board Connect: from a browser, the Board Connect web UI shows logs for an installed app; with an agent, use `board-connect logs com.yourcompany.yourgame --follow` while the app runs.

## Troubleshooting

### Board not detected

| Symptom | Fix |
| --- | --- |
| The web UI or agent can't reach the Board | Confirm you're using the Board's current address from Settings > System and that your machine is on the same network (Board Connect listens on port 8843 over the LAN). |
| Pairing fails or is rejected | Re-run `board-connect pair <addr>` (pass `--force` to mint a fresh token) and have the user tap Approve on the device. The Board's address is shown under Settings > System. |

### Installation fails

| Symptom | Fix |
| --- | --- |
| The Board rejects the APK | Ensure the APK was built for ARM64 (`arm64-v8a`) — Board rejects APKs built for other architectures. |
| Build or install errors on API level | Confirm Minimum API Level 33 in Unity Player Settings. |
| Not enough room on the device | Verify sufficient storage on Board (`board-connect status` shows device readiness). |

### App crashes on launch

1. Stream logs to see the crash: `board-connect logs com.yourcompany.yourgame --follow`
2. Rebuild with Development Build enabled for more detailed errors
3. Check that all required settings are configured (see Setup Reference)

## Next Steps

- Sample Scene — Explore SDK features
- Simulator — Test without hardware
- Touch Input — Handle Piece and finger input
