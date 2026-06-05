> Source: https://docs.dev.board.fun/unity/api/BoardContact.html

# BoardContact Struct

Namespace: Board.Input

Represents an ongoing contact on the Board's touch screen.

```csharp
public struct BoardContact
```

| Properties | Description |
| --- | --- |
| bounds | Gets the screen-space bounds that encapsulates the contact. |
| contactId | Gets the unique identifier for the contact. |
| glyphId | Gets the glyph identifier associated with the contact. |
| isInProgress | Gets a value indicating whether the contact is ongoing. |
| isNoneEndedOrCanceled | Gets a value indicating whether the phase of the contact is None, Ended, or Canceled. |
| isTouched | Gets a value indicating whether the contact is currently being touched. |
| orientation | Gets the orientation of the contact in radians counter-clockwise from vertical. |
| phase | Gets the current phase of the contact. |
| previousOrientation | Gets the orientation of the contact in radians counter-clockwise from vertical in the previous frame. |
| previousScreenPosition | Gets the position of the contact in screen space pixel coordinates in the previous frame. |
| screenPosition | Gets the position of the contact in screen space pixel coordinates. |
| timestamp | Gets the time in seconds on the same timeline as `Time.realTimeSinceStartup` when the contact began or when it was last mutated. |
| type | Gets the current type of the contact. |
