// <copyright file="BoardContactSimulationIcon.cs" company="Harris Hill Products Inc.">
//     Copyright (c) Harris Hill Products Inc. All rights reserved.
// </copyright>

namespace Board.Input.Simulation
{
    using System;
    
    using UnityEngine;

    /// <summary>
    /// Represents an icon used for Board input simulation.
    /// </summary>
    [CreateAssetMenu(menuName = "Board/Simulation/Icon")]
    public class BoardContactSimulationIcon : ScriptableObject
    {
        [SerializeField] private string m_DisplayName;
        [SerializeField] private Sprite m_Sprite;
        [SerializeField] private Vector2 m_Size = new Vector2(100, 100);
        [SerializeField] private float m_AlphaHitTestMinimumThreshold = 0;
        [SerializeField] private BoardContactType m_ContactType;
        [SerializeField] private int m_GlyphId;

        private string m_PreviousDisplayName;
        private int m_PreviousSpriteInstanceId;
        private Vector2 m_PreviousSize;
        private float m_PreviousAlphaHitTestMinimumThreshold;
        private BoardContactType m_PreviousContactType;
        private int m_PreviousGlyphId;

        /// <summary>
        /// Gets the display name for the icon.
        /// </summary>
        public string displayName => m_DisplayName;
        
        /// <summary>
        /// Gets the contact type that the icon represents.
        /// </summary>
        public BoardContactType contactType => m_ContactType;
        
        /// <summary>
        /// Gets the glyph identifier that the icon represents.
        /// </summary>
        public int glyphId => m_GlyphId;
        
        /// <summary>
        /// Gets the sprite for the icon.
        /// </summary>
        public Sprite sprite => m_Sprite;
        
        /// <summary>
        /// Gets the desired size to display the icon.
        /// </summary>
        public Vector2 size => m_Size;
        
        /// <summary>
        /// Gets the minimum alpha a pixel must have for a pointer event to be considered a "hit" on the icon.
        /// </summary>
        public float alphaHitTestMinimumThreshold => m_AlphaHitTestMinimumThreshold;

        /// <summary>
        /// Occurs when a property on the icon changes.
        /// </summary>
        public event Action<BoardContactSimulationIcon> changed;
        
        /// <summary>
        /// Editor-only function invoked by Unity when the script is loaded or a value changes in the Inspector.
        /// </summary>
        private void OnValidate()
        {
            var invokeChangeEvent = false;

            if (m_PreviousDisplayName != m_DisplayName)
            {
                invokeChangeEvent = true;
                m_PreviousDisplayName = m_DisplayName;
            }

            if ((m_PreviousSpriteInstanceId > 0 &&
                 (m_Sprite == null || m_PreviousSpriteInstanceId != m_Sprite?.GetInstanceID())) ||
                (m_PreviousSpriteInstanceId == 0 && m_Sprite != null))
            {
                invokeChangeEvent = true;
                m_PreviousSpriteInstanceId = m_Sprite == null ? 0 : m_Sprite.GetInstanceID();
            }

            if (m_PreviousSize != m_Size)
            {
                invokeChangeEvent = true;
                m_PreviousSize = m_Size;
            }

            if (m_PreviousAlphaHitTestMinimumThreshold != m_AlphaHitTestMinimumThreshold)
            {
                invokeChangeEvent = true;
                m_PreviousAlphaHitTestMinimumThreshold = m_AlphaHitTestMinimumThreshold;
            }

            if (m_PreviousContactType != m_ContactType)
            {
                invokeChangeEvent = true;
                m_PreviousContactType = m_ContactType;
            }

            if (m_PreviousGlyphId != m_GlyphId)
            {
                invokeChangeEvent = true;
                m_PreviousGlyphId = m_GlyphId;
            }

            if (invokeChangeEvent)
            {
                changed?.Invoke(this);
            }
        }
    }
}
