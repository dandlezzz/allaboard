> Source: https://docs.dev.board.fun/guides/touch-input
>
> Cross-SDK guide. Unity code is the primary target; Godot/Web samples retained for reference.

# Touch

How to read finger touches and Piece placements in your game. The data model is identical across the three SDKs; only the way you receive contacts and the per-engine conventions for coordinates and orientation differ. This guide covers both, side by side.

New to Board's touch system? Read Touch for how recognition, noise rejection, and unlimited contacts work, and Pieces for what a Glyph is.

---

## The contact model

Every SDK reports touch through the same logical object: a contact. A contact is either a finger or a Piece, and it carries the same information across Unity, Godot, and Web. A contact keeps a stable identity from the moment it appears until it's lifted or canceled, so you can follow one finger or one Piece across frames.

| Field | Meaning |
| --- | --- |
| contact id | Stable identifier for this contact, constant for its whole lifetime |
| type | Whether this is a finger or a Glyph (Piece) |
| position | Where the contact is on the display |
| orientation | Rotation of the Piece (Pieces only; fingers have no orientation) |
| phase | Where the contact is in its lifecycle (see below) |
| glyph id | Which Piece in the Piece Set this is (Pieces only) |
| touched | Whether a hand is currently touching the Piece (Pieces only; always true for fingers) |

### Phases

A contact moves through a lifecycle. Every SDK reports the same phases:

| Phase | Meaning |
| --- | --- |
| Began | The contact appeared this frame |
| Moved | The contact's position or orientation changed |
| Stationary | The contact is present but unchanged this frame |
| Ended | The contact was lifted normally |
| Canceled | The contact ended abnormally (for example, the pause screen opened) |

When the pause screen opens, all active contacts are canceled. Clean up any per-contact state when you receive a `Canceled` phase.

### Fingers vs Pieces

- Fingers have a position and a phase. They have no meaningful orientation, no Glyph id, and are always considered "touched."
- Pieces (Glyphs) additionally carry an orientation, a glyph id that tells you which Piece from the set it is, and a touched flag that tells you whether a hand is currently on the Piece.

### Per-SDK conventions

The model above is identical across SDKs. Three representation details differ — each SDK presents data in its host engine's native convention so it feels native and you don't have to convert it yourself. When porting code or reading across SDKs, mind these:

| Convention | Unity | Godot | Web |
| --- | --- | --- | --- |
| Position type | `Vector2` (`screenPosition`) | `x`/`y` in a Dictionary | `x`/`y` numbers |
| Origin / Y axis | Bottom-left, Y up (Unity screen space) | Top-left, Y down | Top-left, Y down |
| Orientation units | Radians | Degrees | Degrees |
| Field naming | camelCase (`contactId`, `glyphId`) | snake_case (`contact_id`, `glyph_id`) | camelCase (`contactId`, `glyphId`) |

Coordinates are in display pixels in every SDK (the panel is 1920×1080). Only the origin, axis direction, and orientation units change.

---

## Reading active contacts

How you receive contacts is the biggest difference between the SDKs. Unity polls them each frame. Godot emits a signal once per inference frame. Web subscribes a callback that fires per frame.

### Unity (C#)

```csharp
using Board.Input;

void Update()
{
    // Get all active contacts (fingers and Pieces) each frame.
    var contacts = BoardInput.GetActiveContacts();

    foreach (var contact in contacts)
    {
        ProcessContact(contact);
    }
}
```

### Web (JS)

```js
import { Board } from "@board.fun/web-sdk";

if (Board.isOnDevice) {
  Board.input.subscribe((contacts) => {
    // Called once per inference frame with the full current contact set.
    for (const c of contacts) {
      processContact(c);
    }
  });
}
```

### Godot (GDScript)

```gdscript
func _ready() -> void:
    if not Board.is_on_device:
        return
    # Activate the detector with your Piece Set model, then subscribe.
    Board.input.activate("models/your_piece_set.tflite")
    Board.input.contacts_received.connect(_on_contacts)
    Board.input.subscribe()

func _on_contacts(contacts: Array) -> void:
    # Called once per inference frame with the full current contact set.
    for c in contacts:
        process_contact(c)
```

### Filtering by type

Handle fingers and Pieces separately by filtering. Unity and Web split on the `type` field; on the Godot channel every contact is reported as a Glyph type, so split on `glyph_id` there (`0` is a finger, `1+` is a Piece).

Unity (C#):

```csharp
// Only finger touches
var fingers = BoardInput.GetActiveContacts(BoardContactType.Finger);

// Only Pieces
var pieces = BoardInput.GetActiveContacts(BoardContactType.Glyph);
```

Web (JS):

```js
import { Board, BoardContactType } from "@board.fun/web-sdk";

Board.input.subscribe((contacts) => {
  const fingers = contacts.filter(c => c.type === BoardContactType.Finger);
  const pieces  = contacts.filter(c => c.type === BoardContactType.Glyph);
});
```

Godot (GDScript):

```gdscript
func _on_contacts(contacts: Array) -> void:
    # On the Godot channel the tracker stamps every contact as type_id 1,
    # so filter finger vs Piece on glyph_id (0 = finger, 1+ = Piece).
    for c in contacts:
        if int(c.glyph_id) == 0:
            handle_finger(c)
        else:
            handle_piece(c)
```

---

## Position

Unity (C#):

```csharp
// Vector2 in Unity screen space (origin bottom-left, Y up).
Vector2 position = contact.screenPosition;

// Convert to a world position for 2D games.
Vector3 worldPos = Camera.main.ScreenToWorldPoint(
    new Vector3(position.x, position.y, 10f)
);
```

Web (JS):

```js
// Pixels, origin top-left, Y down (matches canvas / DOM coordinates).
const { x, y } = contact;
element.style.transform = `translate(${x}px, ${y}px)`;
```

Godot (GDScript):

```gdscript
# Pixels, origin top-left, Y down (matches Godot's 2D screen space).
var pos := Vector2(c.x, c.y)
sprite.position = pos
```

## Orientation

Piece rotation only. Fingers have no orientation.

Unity (C#):

```csharp
// Radians, counter-clockwise from vertical.
float rotation = contact.orientation;

// Apply to a transform (convert radians to degrees for Euler).
transform.rotation = Quaternion.Euler(0, 0, rotation * Mathf.Rad2Deg);
```

Web (JS):

```js
// Degrees.
const rotation = contact.orientation;
element.style.transform = `rotate(${rotation}deg)`;
```

Godot (GDScript):

```gdscript
# Degrees, screen-CW positive.
sprite.rotation_degrees = c.orientation
```

---

## Handling phases

A typical contact handler spawns a visual on `Began`, updates it on `Moved`, and tears it down on `Ended` or `Canceled`.

Unity (C#):

```csharp
void ProcessContact(BoardContact contact)
{
    switch (contact.phase)
    {
        case BoardContactPhase.Began:
            SpawnContactVisual(contact);
            break;

        case BoardContactPhase.Moved:
            UpdateContactVisual(contact);
            break;

        case BoardContactPhase.Stationary:
            // Contact unchanged, may still need processing
            break;

        case BoardContactPhase.Ended:
        case BoardContactPhase.Canceled:
            RemoveContactVisual(contact);
            break;
    }
}
```

Web (JS):

```js
import { BoardContactPhase, type BoardContact } from "@board.fun/web-sdk";

function processContact(c: BoardContact) {
  switch (c.phase) {
    case BoardContactPhase.Began:
      spawnContactVisual(c);
      break;
    case BoardContactPhase.Moved:
      updateContactVisual(c);
      break;
    case BoardContactPhase.Stationary:
      // unchanged
      break;
    case BoardContactPhase.Ended:
    case BoardContactPhase.Canceled:
      removeContactVisual(c);
      break;
  }
}
```

Godot (GDScript):

```gdscript
func process_contact(c: Dictionary) -> void:
    match c.phase_id:
        Board.input.PHASE_BEGAN:
            spawn_contact_visual(c)
        Board.input.PHASE_MOVED:
            update_contact_visual(c)
        Board.input.PHASE_STATIONARY:
            pass
        Board.input.PHASE_ENDED, Board.input.PHASE_CANCELED:
            remove_contact_visual(c)
```

---

## Piece touch state

Pieces report whether a finger is touching them. Use this to highlight Pieces when picked up, run different behavior for held vs resting Pieces, and detect releases.

Unity (C#):

```csharp
if (contact.type == BoardContactType.Glyph)
{
    if (contact.isTouched)
    {
        HighlightPiece(contact); // player is holding it
    }
    else
    {
        ShowIdleState(contact);  // resting on the board
    }
}
```

Web (JS):

```js
if (contact.type === BoardContactType.Glyph) {
  if (contact.isTouched) {
    highlightPiece(contact);
  } else {
    showIdleState(contact);
  }
}
```

Godot (GDScript):

```gdscript
if int(c.glyph_id) > 0:  # glyph_id 0 is a finger; 1+ is a Piece
    if c.is_touched:
        highlight_piece(c)
    else:
        show_idle_state(c)
```

## Glyph IDs

For Piece contacts, the glyph id tells you which Piece from the set this is. Each id is a small integer that is stable for that Piece type within your Piece Set (in Godot and Web, id `0` means a finger and Piece ids start at `1`). To find out which id corresponds to which physical Piece, place each Piece on Board one at a time and log the id.

Unity (C#):

```csharp
if (contact.type == BoardContactType.Glyph)
{
    switch (contact.glyphId)
    {
        case 0: SpawnWarrior(contact); break;
        case 1: SpawnMage(contact);    break;
        case 2: SpawnArcher(contact);  break;
    }
}
```

Web (JS):

```js
// glyphId 0 is a finger; Piece ids start at 1.
if (contact.type === BoardContactType.Glyph) {
  switch (contact.glyphId) {
    case 1: spawnWarrior(contact); break;
    case 2: spawnMage(contact);    break;
    case 3: spawnArcher(contact);  break;
  }
}
```

Godot (GDScript):

```gdscript
# glyph_id 0 is a finger; Piece ids start at 1.
if int(c.glyph_id) > 0:
    match c.glyph_id:
        1: spawn_warrior(c)
        2: spawn_mage(c)
        3: spawn_archer(c)
```

---

## See Also

- Touch — how Board's touch tech works
- Pieces — what a Glyph is and how Piece Sets map to ids
- Piece Interaction Design — patterns for using Piece input in gameplay
- Per-SDK API references: Unity, Godot, Web
