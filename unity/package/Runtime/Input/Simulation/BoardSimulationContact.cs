// <copyright file="BoardSimulationContact.cs" company="Harris Hill Products Inc.">
//     Copyright (c) Harris Hill Products Inc. All rights reserved.
// </copyright>

#if UNITY_EDITOR

namespace Board.Input.Simulation
{
    using UnityEngine;
    using UnityEngine.UI;
    
    /// <summary>
    /// Represents a visual representation of a simulated <see cref="BoardContact"/>.
    /// </summary>
    [RequireComponent(typeof(Image))]
    internal class BoardSimulationContact : MonoBehaviour
    {
        private Image m_Image;
        private Outline m_Outline;
        private Shadow m_Shadow;
        private BoardContactSimulationIcon m_Icon;
        private int m_ContactId = -1;
        private float m_Orientation = 0f;
        private bool m_IsTouched = false;
        private bool m_IsPlaced = false;
        private int m_GlyphId = -1;
        private BoardContactType m_ContactType;
        private BoardContactSimulationSettings m_Settings;

        private static int s_ContactCount = 0;
        private static readonly Vector2 kEffectDirection = new Vector2(1, -1);
        
        /// <summary>
        /// Gets the <see cref="RectTransform"/> for the contact.
        /// </summary>
        private RectTransform rectTransform => (RectTransform)transform;
        
        /// <summary>
        /// Gets a value indicating whether the contact is currently being touched.
        /// </summary>
        public bool isTouched => m_IsTouched;

        /// <summary>
        /// Gets a value indicating whether the contact is currently placed on the Board.
        /// </summary>
        public bool isPlaced => m_IsPlaced;
        
        /// <summary>
        /// Gets or sets the current <see cref="BoardContactSimulationIcon"/> that represents the contact.
        /// </summary>
        public BoardContactSimulationIcon icon
        {
            get => m_Icon;
            set
            {
                if (m_Icon != null && m_Icon != null)
                {
                    m_Icon.changed -= OnIconChanged;
                }
                
                m_Icon = value;
                
                if (m_Icon == null)
                {
                    m_ContactType = BoardContactType.Finger;
                    m_GlyphId = -1;
                    m_Image.enabled = false;
                    return;
                }
                
                OnIconChanged(m_Icon);
            }
        }

        /// <summary>
        /// Callback invoked by Unity when the script becomes enabled.
        /// </summary>
        private void OnEnable()
        {
            if (m_Shadow == null)
            {
                m_Shadow = GetComponent<Shadow>();
                if (m_Shadow == null)
                {
                    m_Shadow = gameObject.AddComponent<Shadow>();
                }
            }
            
            if (m_Outline == null)
            {
                m_Outline = GetComponent<Outline>();
                if (m_Outline == null)
                {
                    m_Outline = gameObject.AddComponent<Outline>();
                }
            }

            m_Settings = BoardContactSimulationSettings.instance;
            
            m_Image = GetComponent<Image>();
            m_Image.raycastTarget = false;
            m_Image.color = m_IsPlaced ? Color.white : m_Settings.liftedIconColor;
            m_Shadow.effectColor = m_Settings.liftedIconShadowColor;
            m_Shadow.effectDistance = kEffectDirection * m_Settings.liftedIconShadowDistance;
            m_Shadow.enabled = !m_IsPlaced;
            m_Outline.effectColor = m_IsTouched ? m_Settings.touchedIconOutlineColor : m_Settings.iconOutlineColor;
            m_Outline.effectDistance = kEffectDirection * (m_IsPlaced ? m_Settings.touchedIconOutlineThickness : m_Settings.iconOutlineThickness);
            transform.localScale = Vector3.one;
            BoardContactSimulationSettings.changed += OnSimulationSettingsChanged;
        }
        
        /// <summary>
        /// Callback invoked by Unity when the script becomes disabled.
        /// </summary>
        private void OnDisable()
        {
            BoardContactSimulationSettings.changed -= OnSimulationSettingsChanged;
            
            if (m_Icon != null)
            {
                m_Icon.changed -= OnIconChanged;
            }
        }

        /// <summary>
        /// Callback invoked by Unity when the script is destroyed.
        /// </summary>
        private void OnDestroy()
        {
            if (m_ContactId >= 0)
            {
                QueueStateEvent(BoardContactPhase.Canceled);
            }
        }

        /// <summary>
        /// Callback invoked whenever the <see cref="BoardContactSimulationSettings"/> change.
        /// </summary>
        /// <param name="newSettings">The new settings.</param>
        private void OnSimulationSettingsChanged(BoardContactSimulationSettings newSettings)
        {
            m_Settings = newSettings;
            if (!m_IsPlaced)
            {
                m_Image.color = m_Settings.liftedIconColor;
            }
            else
            {
                m_Outline.effectColor =
                    m_IsTouched ? m_Settings.touchedIconOutlineColor : m_Settings.iconOutlineColor;
                m_Outline.effectDistance = new Vector2(1, -1) *
                                           (m_IsTouched
                                               ? m_Settings.touchedIconOutlineThickness
                                               : m_Settings.iconOutlineThickness);
            }

            m_Shadow.effectColor = m_Settings.liftedIconShadowColor;
            m_Shadow.effectDistance = new Vector2(1, -1) * m_Settings.liftedIconShadowDistance;
        }

        /// <summary>
        /// Callback invoked whenever a property for the cached icon changes.
        /// </summary>
        /// <param name="sender">The <see cref="BoardContactSimulationIcon"/> that changed.</param>
        private void OnIconChanged(BoardContactSimulationIcon sender)
        {
            var updateContact = false;
            if (m_ContactId >= 0 && (m_ContactType != m_Icon.contactType || m_GlyphId != m_Icon.glyphId))
            {
                updateContact = true;
                QueueStateEvent(BoardContactPhase.Canceled);
                m_ContactId = -1;
            }

            rectTransform.sizeDelta = m_Icon.size;
            m_ContactType = m_Icon.contactType;
            m_GlyphId = m_ContactType == BoardContactType.Glyph ? m_Icon.glyphId : -1;
            m_Image.enabled = m_Icon != null;
            m_Image.sprite = m_Icon.sprite;
            if (m_Icon.alphaHitTestMinimumThreshold > 0 || m_Image.alphaHitTestMinimumThreshold != 0)
            {
                m_Image.alphaHitTestMinimumThreshold = m_Icon.alphaHitTestMinimumThreshold;
            }

            if (updateContact)
            {
                m_ContactId = s_ContactCount++;
                QueueStateEvent(BoardContactPhase.Began);
            }
        }

        /// <summary>
        /// Queues a state event into the Board input system.
        /// </summary>
        /// <param name="phase">The <see cref="BoardContactPhase"/> of the event.</param>
        private void QueueStateEvent(BoardContactPhase phase)
        {
            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(transform);
            BoardInput.QueueStateEvent(new BoardContactEvent()
            {
                contactId = m_ContactId,
                glyphId = m_GlyphId,
                phase = phase,
                position = transform.position,
                orientation = m_Orientation * Mathf.Deg2Rad,
                type = m_ContactType,
                center = bounds.center,
                extents = bounds.extents,
                isTouched = m_IsTouched
            });
        }

        /// <summary>
        /// Transitions the contact into the touched state.
        /// </summary>
        public void Touch()
        {
            if (m_IsTouched)
            {
                return;
            }

            m_IsTouched = true;
            m_Outline.effectColor = m_Settings.touchedIconOutlineColor;
            m_Outline.effectDistance = kEffectDirection * m_Settings.touchedIconOutlineThickness;
            QueueStateEvent(BoardContactPhase.Stationary);
        }

        /// <summary>
        /// Transitions the contact out of the touched state.
        /// </summary>
        public void Untouch()
        {
            if (!m_IsTouched)
            {
                return;
            }

            m_IsTouched = false;
            m_Outline.effectColor = m_Settings.iconOutlineColor;
            m_Outline.effectDistance = kEffectDirection * m_Settings.iconOutlineThickness;
            QueueStateEvent(BoardContactPhase.Stationary);
        }
        
        /// <summary>
        /// Simulates placing the contact onto the Board.
        /// </summary>
        /// <param name="startTouched"><c>true</c> if the contact should start touched; otherwise, <c>false</c>.</param>
        public void Place(bool startTouched = false)
        {
            if (m_IsPlaced)
            {
                return;
            }
            
            m_Outline.effectColor = m_Settings.iconOutlineColor;
            m_Outline.effectDistance = kEffectDirection * m_Settings.iconOutlineThickness;
            m_Shadow.enabled = false;
            m_Image.color = Color.white;
            m_IsPlaced = true;

            if (startTouched)
            {
                m_IsTouched = true;
                m_Outline.effectColor = m_Settings.touchedIconOutlineColor;
                m_Outline.effectDistance = kEffectDirection * m_Settings.touchedIconOutlineThickness;
            }
            
            if (m_ContactId < 0)
            {
                m_ContactId = s_ContactCount;
                QueueStateEvent(BoardContactPhase.Began);
                gameObject.name = $"Contact{m_ContactId}";
                s_ContactCount++;
            }

            if (m_Icon != null)
            {
                m_Icon.changed += OnIconChanged;
            }
        }
        
        /// <summary>
        /// Simulates lifting the contact from the Board.
        /// </summary>
        /// <returns><see langword="true"/> if the contact was successfully lifted; otherwise, <see langword="false"/>.</returns>
        public bool Lift()
        {
            if (!m_IsPlaced || m_ContactId < 0 || !BoardInput.IsContactIdActive(m_ContactId))
            {
                return false;
            }

            m_IsPlaced = false;
            m_IsTouched = false;
            m_Outline.effectColor = m_Settings.iconOutlineColor;
            m_Outline.effectDistance = kEffectDirection * m_Settings.iconOutlineThickness;
            m_Shadow.enabled = true;
            m_Image.color = m_Settings.liftedIconColor;
                    
            if (m_ContactId >= 0)
            {
                QueueStateEvent(BoardContactPhase.Ended);
            }

            m_ContactId = -1;
            if (m_Icon != null)
            {
                m_Icon.changed -= OnIconChanged;
            }
            return true;
        }

        /// <summary>
        /// Cancels the contact.
        /// </summary>
        public void Cancel()
        {
            QueueStateEvent(BoardContactPhase.Canceled);
        }

        /// <summary>
        /// Casts a ray from the specified position.
        /// </summary>
        /// <param name="position">A screen-space position.</param>
        /// <returns><see langword="true"/> if the ray intersects with this <see cref="BoardSimulationContact"/>;
        /// otherwise, <see langword="false"/>.</returns>
        public bool Raycast(Vector2 position)
        {
            return RectTransformUtility.RectangleContainsScreenPoint(rectTransform, position) &&
                   m_Image.IsRaycastLocationValid(position, null);
        }

        /// <summary>
        /// Rotates the contact by the specified degrees.
        /// </summary>
        /// <param name="deltaDegrees">The amount of degrees to rotate.</param>
        public void Rotate(float deltaDegrees)
        {
            var prevOrientation = m_Orientation;
            m_Orientation += deltaDegrees;

            // sets angle in the range (-360,360)
            m_Orientation %= 360f;

            if (m_Orientation < 0f)
            {
                // shift to [0,360) range
                m_Orientation += 360f;
            }
            
            transform.rotation = Quaternion.AngleAxis(m_Orientation, Vector3.forward);

            if (m_Orientation != prevOrientation && m_ContactId >= 0)
            {
                QueueStateEvent(BoardContactPhase.Moved);
            }
        }
        
        /// <summary>
        /// Moves the contact to the specified screen-space position.
        /// </summary>
        /// <param name="screenPosition">The destination screen-space position.</param>
        public void MoveTo(Vector2 screenPosition)
        {
            var prevPosition = transform.position;
            transform.position = screenPosition;
            
            if (transform.position != prevPosition && m_ContactId >= 0)
            {
                QueueStateEvent(BoardContactPhase.Moved);
            }
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return
                $"{{ id={m_ContactId} position={transform.position} orientation={m_Orientation} type={m_Icon?.contactType} glyphId={m_Icon?.glyphId} isTouched={isTouched}}}";
        }
    }
}

#endif //UNITY_EDITOR