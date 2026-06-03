// <copyright file="BoardContactType.cs" company="Harris Hill Products Inc.">
//     Copyright (c) Harris Hill Products Inc. All rights reserved.
// </copyright>

namespace Board.Input
{
    /// <summary>
    /// Specifies the type of a contact on the Board.
    /// </summary>
    public enum BoardContactType
    {
        /// <summary>
        /// A standard finger touch.
        /// </summary>
        Finger,
        
        /// <summary>
        /// A glyph pattern.
        /// </summary>
        Glyph,
        
        /// <summary>
        /// An undefined blob.
        /// </summary>
        Blob,
    }
}