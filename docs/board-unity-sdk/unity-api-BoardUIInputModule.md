> Source: https://docs.dev.board.fun/unity/api/BoardUIInputModule.html — fetched 2026-06-04T18:38 (UTC-7)

# BoardUIInputModule Class (Board.Input)

UI input module for input from Board.

```csharp
public class BoardUIInputModule : UnityEngine.EventSystems.PointerInputModule
```

Inheritance: UnityEngine.MonoBehaviour 🡒 UnityEngine.EventSystems.UIBehaviour 🡒 UnityEngine.EventSystems.BaseInputModule 🡒 UnityEngine.EventSystems.PointerInputModule 🡒 BoardUIInputModule

Remarks: When running on Board hardware and `m_DisableCompetingModules` is `true` (the default), this module automatically disables any other UnityEngine.EventSystems.BaseInputModule components on the same GameObject (e.g., `InputSystemUIInputModule` or `StandaloneInputModule`) to prevent input conflicts. In the Editor, no modules are disabled so standard mouse/keyboard input continues to work. Set `m_DisableCompetingModules` to `false` in the Inspector to opt out of this behavior.

| Properties | Description |
| --- | --- |
| forceModuleActive | Gets a value that indicates whether the BoardUIInputModule should be forced to be active. |
