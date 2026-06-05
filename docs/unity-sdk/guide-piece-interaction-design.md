> Source: https://docs.dev.board.fun/guides/piece-interaction-design
>
> Cross-SDK guide. Unity code is the primary target; Godot/Web samples retained for reference.

# Piece Interaction Design

Board is a hybrid digital-physical platform. The Pieces are the controller: the way a player picks them up, slides them, rotates them, and sets them down is the input loop your game responds to. Good Board games treat the physical motion as a first-class part of the design rather than as a translation layer on top of conventional UI.

This guide is mostly platform-general design guidance. See Touch for the full code side: the contact model, phases, coordinate and orientation conventions, and how each SDK delivers contacts (Unity polls, Godot emits a signal, Web subscribes a callback).

New to Board's input model? Read Touch for how recognition works and Pieces for what a Glyph is and how a Piece Set maps to Glyph ids.

---

## Why Pieces matter

A Piece is more than an avatar for a player. It is a physical object the player must touch, move, and reason about in three-space. That carries design consequences:

- Pieces have inertia. Moving a Piece takes longer than tapping a button. Pace your game around the player's hands, not their reflexes.
- Pieces have presence. A Piece sitting on the board is visible to everyone around the table. State that lives on Pieces is shared state.
- Pieces are tactile. Players notice the weight, finish, and shape of each Piece.
- Pieces are fragile in motion. Tracking can momentarily drop when a Piece is moved very quickly, lifted at an angle, or covered by a hand. Design forgiving interactions.

The best Board games are ones a player would still understand if you took the screen away: the Pieces alone tell you what is happening, and the screen is the amplifier.

On Board the Pieces cannot be a neutral controller. If a player ever thinks "this would be easier with a mouse," then the game's mechanics are not native to Board. It has to be fun to play with Pieces.

---

## Interaction types

There are seven core interaction primitives a player can perform with a Piece. Each maps cleanly to data your game reads from the contact stream: a contact's lifecycle phase, its position, its orientation, and (for Pieces) whether a hand is touching it.

### Place and Lift

A Piece arrives on the display (a contact in the `Began` phase) and eventually leaves (a contact in the `Ended` phase). Place-and-lift is the right primitive for turn-based moves, spawn and despawn, and inventory.

Unity (C#):

```csharp
using Board.Input;

void ProcessContact(BoardContact contact)
{
    switch (contact.phase)
    {
        case BoardContactPhase.Began:
            OnPiecePlaced(contact);   // arrived on the display
            break;
        case BoardContactPhase.Ended:
        case BoardContactPhase.Canceled:
            OnPieceLifted(contact);   // removed from the display
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
      onPiecePlaced(c);
      break;
    case BoardContactPhase.Ended:
    case BoardContactPhase.Canceled:
      onPieceLifted(c);
      break;
  }
}
```

Godot (GDScript):

```gdscript
func process_contact(c: Dictionary) -> void:
    match c.phase_id:
        Board.input.PHASE_BEGAN:
            on_piece_placed(c)
        Board.input.PHASE_ENDED, Board.input.PHASE_CANCELED:
            on_piece_lifted(c)
```

Design tips:

- Make the placement zone visually clear.
- Give the player time to commit (a 200ms hold-to-confirm window).
- Confirm placement with both visual and audio feedback.

### Slide

The Piece stays on the display while the player drags it. Sliding generates a stream of contacts in the `Moved` phase. Read the contact's position on every `Moved` frame.

Unity (C#):

```csharp
// Vector2 in Unity screen space (origin bottom-left, Y up).
if (contact.phase == BoardContactPhase.Moved)
{
    Vector2 pos = contact.screenPosition;
    DragUnitTo(pos);
}
```

Web (JS):

```js
if (c.phase === BoardContactPhase.Moved) {
  dragUnitTo(c.x, c.y);
}
```

Godot (GDScript):

```gdscript
if c.phase_id == Board.input.PHASE_MOVED:
    var pos := Vector2(c.x, c.y)
    drag_unit_to(pos)
```

Design tips:

- Do not fight the player's hand.
- Visualize the slide path.
- Respect the device's smoothing (the SDK already applies position smoothing).

### Trace

A specialized slide where the shape of the path matters, not just the endpoint. Mechanically it is the same `Moved` stream as Slide; the difference is that you accumulate the path and match it against a target shape.

Design tips: provide a guide stencil, be lenient (70% match), give scoring feedback as the player traces.

### Shake

Rapid back-and-forth motion of a Piece. Detect shake from velocity, not absolute position: compare the current position against the previous frame's position for the same contact id and watch for rapid direction reversals.

Design tips: detect from velocity not displacement, reset the detector when the player lifts the Piece, use audio cues.

### Rotate

The player turns the Piece in place. The contact's orientation changes while its position stays roughly constant. Orientation is reported only for Pieces. Unity reports radians, Godot and Web report degrees.

Unity (C#):

```csharp
// Radians, counter-clockwise from vertical.
float rotation = contact.orientation;
transform.rotation = Quaternion.Euler(0, 0, rotation * Mathf.Rad2Deg);
```

Web (JS):

```js
// Degrees.
element.style.transform = `rotate(${c.orientation}deg)`;
```

Godot (GDScript):

```gdscript
# Degrees, screen-clockwise positive.
sprite.rotation_degrees = c.orientation
```

Design tips: give visual feedback on the current angle with deadbands, do not punish overshoot (5-degree tolerance), make the rotation axis obvious. Rotate is best supported by Pieces with rotational symmetry.

### Twist

A quick, discrete rotational change, distinct from continuous rotation. Twist is gesture, not value: do not read the final angle, read that the player turned the Piece quickly past a threshold.

Design tips: read the rate of change not the resting angle, give a satisfying punctuation.

### Touch and Release (Hold)

The Piece stays on the display, but the player picks it up and holds it. Almost all of Board's Pieces are made with a capacitive coating, so Board can detect whether a hand is on the Piece. That state is exposed as the contact's touched flag. (Strata's blocks are the exception: they are not capacitive, so they never report touched.)

There is no edge signal for touch and release: each SDK hands you a full per-frame snapshot of contacts. To detect the touch and release edge, keep your own previous-frame map keyed by contact id and diff the touched flag each frame.

Unity (C#):

```csharp
using System.Collections.Generic;
using Board.Input;

// contactId -> was the Piece touched last frame
readonly Dictionary<int, bool> _prevTouched = new();

void Update()
{
    var contacts = BoardInput.GetActiveContacts(BoardContactType.Glyph);
    foreach (var c in contacts)
    {
        bool wasTouched = _prevTouched.TryGetValue(c.contactId, out var t) && t;
        if (c.isTouched && !wasTouched)
            OnHoldBegan(c);       // picked up
        else if (wasTouched && !c.isTouched)
            OnHoldReleased(c);    // set down
    }

    _prevTouched.Clear();
    foreach (var c in contacts)
        _prevTouched[c.contactId] = c.isTouched;
}
```

Web (JS):

```js
import { type BoardContact } from "@board.fun/web-sdk";

// contactId -> was the Piece touched last frame
let prevTouched = new Map<number, boolean>();

function onContacts(contacts: ReadonlyArray<BoardContact>) {
  for (const c of contacts) {
    if (c.glyphId <= 0) continue;   // fingers have no hold state
    const wasTouched = prevTouched.get(c.contactId) ?? false;
    if (c.isTouched && !wasTouched) {
      onHoldBegan(c);       // picked up
    } else if (wasTouched && !c.isTouched) {
      onHoldReleased(c);    // set down
    }
  }
  prevTouched = new Map(contacts.map(c => [c.contactId, c.isTouched]));
}
```

Godot (GDScript):

```gdscript
var _prev := {}  # contact_id -> is_touched

func _on_contacts_received(contacts: Array) -> void:
    for c in contacts:
        if c.glyph_id <= 0:        # fingers, not Pieces, have no hold state
            continue
        var id: int = c.contact_id
        var was_touched: bool = _prev.get(id, false)
        if c.is_touched and not was_touched:
            on_hold_began(c)       # picked up
        elif was_touched and not c.is_touched:
            on_hold_released(c)    # set down
    _prev.clear()
    for c in contacts:
        _prev[c.contact_id] = c.is_touched
```

On Unity, fingers always report touched as true; on Godot and Web, fingers (Glyph id 0) carry no meaningful hold state. Gate hold detection on Piece contacts.

Design tips: a short hold (200ms) is comfortable, quick taps are hard to rely on, give continuous feedback during the hold.

---

## Piece categories

Pieces in a set typically fall into one of three functional categories. These are conventions, not enforced by the SDK.

### Pawn Pieces (Character Pieces)

Pieces that represent the player or a player's units. They move around the board through placement and sliding, and they are persistent.

- One Pawn per player.
- Distinct shapes or colors per Pawn.
- Pawn motion is the primary input.

Examples: the robots in Cosmic Crush and Snek; the chopsticks in Omakase; Little Chef in Chop Chop.

### Action Pieces (Verb Pieces)

Single-purpose Pieces that trigger discrete events when interacted with. The physical design advertises how it is meant to be used.

- One affordance per Piece.
- Action Pieces are reusable but stateless.
- Action Pieces respond best to a clear commit moment.

Examples: the knife in Chop Chop; a watering can; a spaceship in Space Rocks or Starfire.

### Reaction Pieces (Platform Pieces)

Pieces placed on the board as persistent world elements. They do not move (or move rarely), and the digital game world responds to them being where they are.

- Reaction Pieces define terrain.
- They are typically placed once per setup or per turn.
- The game's reaction to a Reaction Piece is the gameplay.

Examples: the stairs and blocks in Save the Bloogs; terrain tiles; resource nodes.

To branch behavior by which physical Piece is on the board, switch on the contact's Glyph id.

Unity (C#):

```csharp
if (contact.type == BoardContactType.Glyph)
{
    switch (contact.glyphId)
    {
        case 0: HandlePawn(contact);     break;  // Pawn: place + slide
        case 1: HandleActionPiece(contact); break;  // Action: tap to commit
        case 2: HandleReactionPiece(contact); break;  // Reaction: persistent terrain
    }
}
```

Web (JS):

```js
if (c.glyphId > 0) {
  switch (c.glyphId) {
    case 1: handlePawn(c);          break;  // Pawn: place + slide
    case 2: handleActionPiece(c);   break;  // Action: tap to commit
    case 3: handleReactionPiece(c); break;  // Reaction: persistent terrain
  }
}
```

Godot (GDScript):

```gdscript
# On the Godot channel use glyph_id (not type_id) to tell Pieces apart.
if c.glyph_id > 0:
    match c.glyph_id:
        1: handle_pawn(c)            # Pawn: place + slide
        2: handle_action_piece(c)    # Action: tap to commit
        3: handle_reaction_piece(c)  # Reaction: persistent terrain
```

Glyph ids are indices into your Piece Set. On Unity, finger contacts report a Glyph id of -1 and Piece Glyph ids are 0-based, so isolate Pieces by checking `type == BoardContactType.Glyph` (not `glyphId > 0`). On Godot and Web, Piece Glyph ids are 1-based and fingers are 0, so the `glyphId > 0` guard isolates Pieces there.

---

## Interfaces and signifiers

With a new kind of controller, you have to teach players how and where Pieces are used.

### Piece Indicators

On-screen cues that tell the player which Pieces are in play and where to place them.

- Show a render of the physical Piece next to its name.
- Use the same color and icon throughout.
- Place indicators near where the player is expected to look.

### Icons

Establish a consistent icon vocabulary so players learn each Piece's meaning once.

- Reuse the same icon at every scale.
- Lean on iconic silhouettes.
- Modulate the same icon for multiple states rather than swapping glyphs.

### Action Indicators

Visual cues for how to interact with a Piece: arrows for motion, rotational glyphs for twist, particles for shake.

- Animate the indicator.
- Fade indicators out after the player demonstrates understanding.
- Use sound to reinforce.

### Placement Confirmation

Feedback the moment a Piece arrives on a valid target. This is the most important signifier you can ship.

- Confirm visually and audibly.
- Make rejection unambiguous by not advancing.

### Action Confirmation

Feedback that an Action Piece triggered, distinct from placement.

- Pair every Action with a visible state change.
- Make the audio match the action category.
- Do not repeat the confirmation.

### Communicating Invalid Placement

Tell the player that the wrong Piece was placed. Silent rejection feels like a bug.

- Show the rejected target with a blocked outline.
- Briefly flash an explanation.
- Do not auto-correct.

### Tutorialization

The first 30 seconds is where you teach the mapping from physical Piece to game effect.

- Pick the first Piece for them.
- Confirm the first action loudly.
- Stage interactions one at a time.
- Let failure teach.

---

## Design tips

- Match interaction complexity to player skill (start with Place, then Slide, then Twist/Shake/Trace).
- Avoid simultaneous required interactions.
- Let the table read the state.
- Account for table position (players sit on all sides).
- Design forgiving thresholds.
- Use sound generously.
- Test on hardware.

---

## Putting it together

1. Identify the Pieces in your set. Sort them into Pawn, Action, and Reaction categories.
2. Map each Piece to a primary interaction.
3. Plan the tutorialization.
4. Identify the failure modes.
5. Prototype on hardware.

---

## See Also

- Pieces — how physical Pieces work and how Piece Sets map to Glyph ids
- Touch — how Board's touch tech works
- Touch — the SDK side of every interaction described here
- Player Management — handling the players who pick up those Pieces
- Per-SDK API references: Unity, Godot, Web
