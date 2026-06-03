// <copyright file="BoardContactTypeMask.cs" company="Harris Hill Products Inc.">
//     Copyright (c) Harris Hill Products Inc. All rights reserved.
// </copyright>

namespace Board.Input
{
    using System.Runtime.InteropServices;
    
    using UnityEngine;

    /// <summary>
    /// Represents a contact input event from the Board.
    /// </summary>
    /// <seealso cref="BoardContact"/>
    /// <seealso cref="BoardInput"/>
    [StructLayout(LayoutKind.Explicit, Size = kSizeInBytes)]
    internal struct BoardContactEvent
    {
        /// <summary>
        /// The size of a <see cref="BoardContactEvent"/> in bytes.
        /// </summary>
        internal const int kSizeInBytes = 39;

        /// <summary>
        /// The unique identifier for the contact.
        /// </summary>
        /// <value>Numeric identifier of the contact.</value>
        /// <remarks>
        /// While a contact is ongoing, it must have a non-zero identifier different from
        /// all other ongoing contacts. Starting with <see cref="BoardContactPhase.Began"/>
        /// and ending with <see cref="BoardContactPhase.Ended"/> or <see cref="BoardContactPhase.Canceled"/>,
        /// a contact is identified by its identifier, i.e. a <see cref="BoardContact"/> with the same identifier
        /// belongs to the same contact.
        ///
        /// After a contact has ended or been canceled, an identifier can be reused.
        /// </remarks>
        [FieldOffset(0)] public int contactId;

        /// <summary>
        /// Screen-space position of the contact in pixels.
        /// </summary>
        /// <value>Screen-space position of the contact.</value>
        [FieldOffset(4)] public Vector2 position;

        /// <summary>
        /// Orientation of the contact in radians counter-clockwise from vertical.
        /// </summary>
        /// <value>Screen-space orientation of the contact.</value>
        [FieldOffset(12)] public float orientation;

        /// <summary>
        /// <see cref="BoardContactType"/> value of contact.
        /// </summary>
        /// <value>Current <see cref="BoardContactType"/></value>
        [FieldOffset(16)] public byte typeId;

        /// <summary>
        /// <see cref="BoardContactPhase"/> value of the contact.
        /// </summary>
        /// <value>Current <see cref="BoardContactPhase"/>.</value>
        /// <seealso cref="phase"/>
        [FieldOffset(17)] public byte phaseId;

        /// <summary>
        /// The glyph identifier associated with the contact.
        /// </summary>
        /// <value>Glyph identifier associated with the contact.</value>
        /// <remarks>This value is always -1 for all contacts that are not of type <see cref="BoardContactType.Glyph"/>.</remarks>
        [FieldOffset(18)] public int glyphId;
        
        /// <summary>
        /// The screen-space center of the axis-aligned bounding box that contains the contact.
        /// </summary>
        /// <value>Screen-space center of the axis-aligned bounding box that contains the contact.</value>
        [FieldOffset(22)] public Vector2 center;

        /// <summary>
        /// The screen-space extents of the axis-aligned bounding box that contains the contact.
        /// </summary>
        /// <value>Screen-space extents of the axis-aligned bounding box that contains the contact.</value>
        [FieldOffset(30)] public Vector2 extents;

        /// <summary>
        /// A value indicating whether the contact is currently being touched.
        /// </summary>
        /// <value>Value indicating whether the contact is currently being touched.</value>
        /// <remarks>This value is always <see langword="true"/> for all contacts that are not of type <see cref="BoardContactType.Glyph"/>.</remarks>
        [FieldOffset(38)] public bool isTouched;

        /// <summary>
        /// Gets or sets the phase of the contact.
        /// </summary>
        /// <value>Phase of the contact.</value>
        /// <seealso cref="BoardContact.phase"/>
        public BoardContactPhase phase
        {
            get => (BoardContactPhase)phaseId;
            internal set => phaseId = (byte)value;
        }
        
        /// <summary>
        /// Gets or sets the type of the contact.
        /// </summary>
        /// <value>Type of the contact.</value>
        /// <seealso cref="BoardContact.type"/>
        public BoardContactType type
        {
            get => (BoardContactType)typeId;
            internal set => typeId = (byte)value;
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return
                $"{{ id={contactId} position={position} orientation={orientation} phase={phaseId} type={typeId} glyphId={glyphId} isTouched={isTouched}}}";
        }
    }
}