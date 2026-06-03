// <copyright file="PointerSample.cs" company="board.fun game">
//     Trafalgar - Age of Sail. Procedural board.fun RTS.
// </copyright>

namespace Trafalgar.InputLayer
{
    using UnityEngine;

    /// <summary>
    /// The lifecycle phase of a <see cref="PointerSample"/>, abstracted across Board contacts,
    /// the new Input System mouse, and the new Input System touchscreen.
    /// </summary>
    public enum PointerPhase
    {
        /// <summary>The pointer touched down this frame.</summary>
        Began,

        /// <summary>The pointer moved this frame.</summary>
        Moved,

        /// <summary>The pointer is held but did not move this frame.</summary>
        Stationary,

        /// <summary>The pointer lifted (or was cancelled) this frame.</summary>
        Ended,
    }

    /// <summary>
    /// A single unified pointer observation for one frame. This is the only input shape the
    /// game logic ever sees, regardless of whether it came from a finger on the Board, a glyph
    /// piece, or a mouse in the editor.
    /// </summary>
    public struct PointerSample
    {
        /// <summary>Stable identifier for this pointer across frames.</summary>
        public int id;

        /// <summary>Position in screen-space pixels, origin bottom-left (matches Board + Unity screen space).</summary>
        public Vector2 screenPosition;

        /// <summary>This frame's phase.</summary>
        public PointerPhase phase;

        /// <summary>True when this pointer is a recognised physical Board glyph piece.</summary>
        public bool isGlyph;

        /// <summary>The glyph identifier (or -1 for fingers / mouse).</summary>
        public int glyphId;

        /// <summary>Glyph orientation in radians, counter-clockwise from vertical (0 for non-glyphs).</summary>
        public float orientation;

        /// <summary>Gets a value indicating whether the pointer just began this frame.</summary>
        public bool IsBegan => phase == PointerPhase.Began;

        /// <summary>Gets a value indicating whether the pointer just ended this frame.</summary>
        public bool IsEnded => phase == PointerPhase.Ended;

        /// <summary>Gets a value indicating whether the pointer is currently down (began/moved/stationary).</summary>
        public bool IsDown => phase != PointerPhase.Ended;
    }
}
