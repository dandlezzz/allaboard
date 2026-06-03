// <copyright file="BoardContact.cs" company="Harris Hill Products Inc.">
//     Copyright (c) Harris Hill Products Inc. All rights reserved.
// </copyright>

namespace Board.Input
{
    using System.Runtime.InteropServices;
    
    using UnityEngine;

    /// <summary>
    /// Represents an ongoing contact on the Board's touch screen.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = kSizeInBytes)]
    public struct BoardContact
    {
        /// <summary>
        /// The size of a <see cref="BoardContact"/> in bytes.
        /// </summary>
        internal const int kSizeInBytes = BoardContactEvent.kSizeInBytes + 21;
        
        [FieldOffset(0)]
        internal BoardContactEvent inputEvent;
        
        [FieldOffset(BoardContactEvent.kSizeInBytes)]
        private Vector2 m_PreviousScreenPosition;
        
        [FieldOffset(BoardContactEvent.kSizeInBytes + 8)] 
        private float m_PreviousOrientation;
        
        [FieldOffset(BoardContactEvent.kSizeInBytes + 12)] 
        private double m_Timestamp;
        
        [FieldOffset(BoardContactEvent.kSizeInBytes + 20)] 
        private byte m_previousPhase;
        
        /// <summary>
        /// Gets the unique identifier for the contact.
        /// </summary>
        public int contactId => inputEvent.contactId;

        /// <summary>
        /// Gets the position of the contact in screen space pixel coordinates.
        /// </summary>
        public Vector2 screenPosition => inputEvent.position;
        
        /// <summary>
        /// Gets the position of the contact in screen space pixel coordinates in the previous frame.
        /// </summary>
        public Vector2 previousScreenPosition
        {
            get => m_PreviousScreenPosition;
            internal set => m_PreviousScreenPosition = value;
        }
        
        /// <summary>
        /// Gets the orientation of the contact in radians counter-clockwise from vertical.
        /// </summary>
        public float orientation => inputEvent.orientation;

        /// <summary>
        /// Gets the orientation of the contact in radians counter-clockwise from vertical in the previous frame.
        /// </summary>
        public float previousOrientation
        {
            get => m_PreviousOrientation;
            internal set => m_PreviousOrientation = value;
        }

        /// <summary>
        /// Gets the time in seconds on the same timeline as <c>Time.realTimeSinceStartup</c> when the contact began or when it was last mutated.
        /// </summary>
        public double timestamp 
        {
            get => m_Timestamp;
            internal set => m_Timestamp = value;
        }

        /// <summary>
        /// Gets the current phase of the contact.
        /// </summary>
        /// <seealso cref="BoardContactPhase"/>
        public BoardContactPhase phase
        {
            get => inputEvent.phase;
            internal set => inputEvent.phase = value;
        }

        /// <summary>
        /// Gets the current type of the contact.
        /// </summary>
        /// <seealso cref="BoardContactType"/>
        public BoardContactType type => (BoardContactType)inputEvent.typeId;

        /// <summary>
        /// Gets the glyph identifier associated with the contact.
        /// </summary>
        /// <remarks>For all contacts that are not of type <see cref="BoardContactType.Glyph"/> this value will be -1.</remarks>
        public int glyphId => inputEvent.glyphId;

        /// <summary>
        /// Gets a value indicating whether the contact is currently being touched.
        /// </summary>
        /// <remarks>For all contacts that are not of type <see cref="BoardContactType.Glyph"/> this value will be <see langword="true"/>.</remarks>
        public bool isTouched => inputEvent.isTouched;

        /// <summary>
        /// Gets the screen-space bounds that encapsulates the contact
        /// </summary>
        public Bounds bounds => new Bounds(inputEvent.center, inputEvent.extents);

        /// <summary>
        /// Gets a value indicating whether the phase of the contact is <see cref="BoardContactPhase.None"/>,
        /// <see cref="BoardContactPhase.Ended"/>, or <see cref="BoardContactPhase.Canceled"/>.
        /// </summary>
        /// <value><see langword="true"/> if the phase of the contact is <see cref="BoardContactPhase.None"/>,
        /// <see cref="BoardContactPhase.Ended"/>, or <see cref="BoardContactPhase.Canceled"/>; otherwise, <see langword="false"/>.</value>
        /// <seealso cref="phase"/>
        public bool isNoneEndedOrCanceled =>
            phase == BoardContactPhase.None || phase == BoardContactPhase.Ended || phase == BoardContactPhase.Canceled;

        /// <summary>
        /// Gets a value indicating whether the contact is ongoing.
        /// </summary>
        /// <value><see langword="true"/> if the phase of the contact is <see cref="BoardContactPhase.Began"/>,
        /// <see cref="BoardContactPhase.Moved"/>, or <see cref="BoardContactPhase.Stationary"/>; otherwise, <see langword="false"/>.</value>
        /// <seealso cref="phase"/>
        public bool isInProgress =>
            phase == BoardContactPhase.Began || phase == BoardContactPhase.Moved ||
            phase == BoardContactPhase.Stationary;

        internal BoardContactPhase previousPhase
        {
            get => (BoardContactPhase)m_previousPhase; 
            set => m_previousPhase = (byte)value;
        }

        /// <summary>
        /// Caches previous values before next frame update.
        /// </summary>
        internal void OnBeforeUpdate()
        {
            if (previousPhase == BoardContactPhase.None && phase == BoardContactPhase.Began)
            {
                inputEvent.phase = BoardContactPhase.Stationary;
            }
            else if ((previousPhase == BoardContactPhase.Moved || previousPhase == BoardContactPhase.Stationary) && phase == BoardContactPhase.Moved)
            {
                inputEvent.phase = previousScreenPosition == screenPosition && Mathf.Approximately(previousOrientation, orientation) ? BoardContactPhase.Stationary : BoardContactPhase.Moved;
            }
            
            m_PreviousScreenPosition = screenPosition;
            m_PreviousOrientation = orientation;
            previousPhase = phase;
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return inputEvent.ToString();
        }
    }
}