> Source: https://docs.dev.board.fun/unity/performance

# Performance

Best practices for smooth rendering and responsive input on Board hardware.

## Frame Rate

Unity defaults to 30fps on Android. Board's display runs at 60Hz, so without adjusting this setting your game renders at half the display's refresh rate, causing visible lag — especially for fast-moving objects like game Pieces.

Set the target frame rate early in your game:

```csharp
void Awake()
{
    Application.targetFrameRate = 60;
}
```

Place this in a manager script that loads with your first scene (e.g., a `GameManager` or startup initializer).

Note: Setting `targetFrameRate` to -1 removes the cap entirely, but on Board hardware 60fps matches the display refresh rate and is the recommended target.

## See Also

- Touch Input - Input smoothing and persistence settings
- Hardware - Display and processing specifications
