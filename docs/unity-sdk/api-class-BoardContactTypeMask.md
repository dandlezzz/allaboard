> Source: https://docs.dev.board.fun/unity/api/BoardContactTypeMask.html

# BoardContactTypeMask Struct

Namespace: Board.Input

Provides a mechanism to filter by BoardContactType.

```csharp
public struct BoardContactTypeMask
```

| Constructors | Description |
| --- | --- |
| BoardContactTypeMask(BoardContactType[]) | Instantiates a new instance of BoardContactTypeMask with the specified contact types. |

| Properties | Description |
| --- | --- |
| value | Gets the integer value equivalent of this BoardContactTypeMask. |

| Methods | Description |
| --- | --- |
| OnAfterDeserialize() | See UnityEngine.ISerializationCallbackReceiver. |
| OnBeforeSerialize() | See UnityEngine.ISerializationCallbackReceiver. |

| Operators | Description |
| --- | --- |
| implicit operator BoardContactTypeMask(int) | Implicitly converts a 32-bit signed integer to a BoardContactTypeMask. |
| implicit operator int(BoardContactTypeMask) | Implicitly converts a BoardContactTypeMask to a 32-bit signed integer. |
