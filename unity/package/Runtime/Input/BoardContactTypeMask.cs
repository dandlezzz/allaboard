// <copyright file="BoardContactTypeMask.cs" company="Harris Hill Products Inc.">
//     Copyright (c) Harris Hill Products Inc. All rights reserved.
// </copyright>

namespace Board.Input
{
    using System;
    
    using UnityEngine;
    
    /// <summary>
    /// Provides a mechanism to filter by <see cref="BoardContactType"/>.
    /// </summary>
    [Serializable]
    public struct BoardContactTypeMask : ISerializationCallbackReceiver
    {
        [SerializeField] private uint m_Bits;
        
        private int m_Mask;
        
        /// <summary>
        /// Implicitly converts a <see cref="BoardContactTypeMask"/> to a 32-bit signed integer.
        /// </summary>
        /// <param name="mask">The mask to be converted.</param>
        /// <returns>Returns the integer value equivalent of the <see cref="BoardContactTypeMask"/>.</returns>
        public static implicit operator int(BoardContactTypeMask mask)
        {
            return mask.m_Mask;
        }
        
        /// <summary>
        /// Implicitly converts a 32-bit signed integer to a <see cref="BoardContactTypeMask"/>.
        /// </summary>
        /// <param name="intVal">The mask value.</param>
        /// <returns>Returns the equivalent <see cref="BoardContactTypeMask"/> for the integer value.</returns>
        public static implicit operator BoardContactTypeMask(int intVal)
        {
            BoardContactTypeMask mask;
            mask.m_Mask = intVal;
            mask.m_Bits = (uint)intVal;
            return mask;
        }
        
        /// <summary>
        /// Gets the integer value equivalent of this <see cref="BoardContactTypeMask"/>
        /// </summary>
        /// <returns>The integer value equivalent of this <see cref="BoardContactTypeMask"/>.</returns>
        public int value
        {
            get => m_Mask;
            set
            {
                m_Mask = value;
                m_Bits = (uint)value;
            }
        }
        
        /// <summary>
        /// Instantiates a new instance of <see cref="BoardContactTypeMask"/> with the specified
        /// <see cref="BoardContactType">contact types</see>.
        /// </summary>
        /// <param name="boardContactTypes">An array of <see cref="BoardContactType">contact types</see>.</param>
        public BoardContactTypeMask(params BoardContactType[] boardContactTypes)
        {
            m_Mask = 0;
            foreach (var boardContactType in boardContactTypes)
            {
                m_Mask |= 1 << (int)boardContactType;
            }

            m_Bits = (uint)m_Mask;
        }
        
        /// <summary>
        /// See <see cref="ISerializationCallbackReceiver"/>.
        /// </summary>
        public void OnAfterDeserialize()
        {
            m_Mask = (int)m_Bits;
        }

        /// <summary>
        /// See <see cref="ISerializationCallbackReceiver"/>.
        /// </summary>
        public void OnBeforeSerialize()
        {
        }
    }
}

