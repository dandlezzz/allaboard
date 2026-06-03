// <copyright file="BoardContactDebugView.cs" company="Harris Hill Products Inc.">
//     Copyright (c) Harris Hill Products Inc. All rights reserved.
// </copyright>

namespace Board.Input.Debug
{
    using UnityEngine;
    using UnityEngine.UI;

    /// <summary>
    /// Provides a mechanism to display debug information about a <see cref="BoardContact"/>.
    /// </summary>
    public class BoardContactDebugView : MonoBehaviour
    {
        private Image m_RotationIndicatorImage;
        private Text m_TouchLabel;
        private Text m_GlyphLabel;
        private Text m_Label;
        private Image m_VerticalLine;
        private Image m_HorizontalLine;
        private Image m_BoundingBoxBackground;
        private RectTransform m_Transform;
        
        private static readonly Vector3 kLabelOffset = new Vector3(50f, 50f, 0f);

        /// <summary>
        /// Callback invoked by Unity when the enabled <see cref="MonoBehaviour"/> is being loaded.
        /// </summary>
        private void Awake()
        {
            // Position Indicator
            m_Transform = (RectTransform)transform;
            var verticalLineGameObject = new GameObject("VerticalLine");
            verticalLineGameObject.transform.SetParent(m_Transform, false);
            m_VerticalLine = verticalLineGameObject.AddComponent<Image>();
            m_VerticalLine.rectTransform.sizeDelta = new Vector2(2, Screen.height * 2f);
            m_VerticalLine.color = Color.blue;
            m_VerticalLine.raycastTarget = false;
            verticalLineGameObject.AddComponent<Shadow>();

            var horizontalLineGameObject = new GameObject("HorizontalLine");
            horizontalLineGameObject.transform.SetParent(m_Transform, false);
            m_HorizontalLine = horizontalLineGameObject.AddComponent<Image>();
            m_HorizontalLine.rectTransform.sizeDelta = new Vector2(Screen.width * 2f, 2);
            m_HorizontalLine.color = Color.blue;
            m_HorizontalLine.raycastTarget = false;
            horizontalLineGameObject.AddComponent<Shadow>();
            
            // Rotation Indicator
            var rotationIndicatorGameObject = new GameObject("RotationIndicator");
            rotationIndicatorGameObject.transform.SetParent(m_Transform, false);
            m_RotationIndicatorImage = rotationIndicatorGameObject.AddComponent<Image>();
            m_RotationIndicatorImage.rectTransform.sizeDelta = new Vector2(2, 100);
            m_RotationIndicatorImage.rectTransform.pivot = new Vector2(0.5f, 0);
            m_RotationIndicatorImage.color = Color.green;
            m_RotationIndicatorImage.raycastTarget = false;
            rotationIndicatorGameObject.AddComponent<Shadow>();
            
            // Bounding box
            var boundingBoxGameObject = new GameObject("BoundingBox");
            boundingBoxGameObject.transform.SetParent(m_Transform, false);
            m_BoundingBoxBackground = boundingBoxGameObject.AddComponent<Image>();
            m_BoundingBoxBackground.color = new  Color(1, 1, 1, 0f);
            m_BoundingBoxBackground.raycastTarget = false;
            
            var leftEdgeBoundaryGameObject = new GameObject("LeftEdgeBoundary");
            leftEdgeBoundaryGameObject.transform.SetParent(boundingBoxGameObject.transform, false);
            var image = leftEdgeBoundaryGameObject.AddComponent<Image>();
            image.raycastTarget = false;
            image.rectTransform.anchorMin = Vector2.zero;
            image.rectTransform.anchorMax = new Vector2(0, 1);
            image.rectTransform.pivot = new Vector2(0, 0.5f);
            image.rectTransform.sizeDelta = new Vector2(2f, 0);
            image.color = Color.red;
            
            var rightEdgeBoundaryGameObject = new GameObject("RightEdgeBoundary");
            rightEdgeBoundaryGameObject.transform.SetParent(boundingBoxGameObject.transform, false);
            image = rightEdgeBoundaryGameObject.AddComponent<Image>();
            image.raycastTarget = false;
            image.rectTransform.anchorMin = new Vector2(1, 0);
            image.rectTransform.anchorMax = Vector2.one;
            image.rectTransform.pivot = new Vector2(1, 0.5f);
            image.rectTransform.sizeDelta = new Vector2(2f, 0);
            image.color = Color.red;
            
            var topEdgeBoundaryGameObject = new GameObject("TopEdgeBoundary");
            topEdgeBoundaryGameObject.transform.SetParent(boundingBoxGameObject.transform, false);
            image = topEdgeBoundaryGameObject.AddComponent<Image>();
            image.raycastTarget = false;
            image.rectTransform.anchorMin = new Vector2(0, 1);
            image.rectTransform.anchorMax = Vector2.one;
            image.rectTransform.pivot = new Vector2(0.5f, 1);
            image.rectTransform.sizeDelta = new Vector2(0, 2f);
            image.color = Color.red;
            
            var bottomEdgeBoundaryGameObject = new GameObject("BottomEdgeBoundary");
            bottomEdgeBoundaryGameObject.transform.SetParent(boundingBoxGameObject.transform, false);
            image = bottomEdgeBoundaryGameObject.AddComponent<Image>();
            image.raycastTarget = false;
            image.rectTransform.anchorMin = Vector2.zero;
            image.rectTransform.anchorMax = new Vector2(1, 0);
            image.rectTransform.pivot = new Vector2(0.5f, 0);
            image.rectTransform.sizeDelta = new Vector2(0, 2f);
            image.color = Color.red;
            
            // Label
            var labelGameObject = new GameObject("Label");
            labelGameObject.transform.SetParent(m_Transform, false);
            m_Label = labelGameObject.AddComponent<Text>();
            m_Label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            labelGameObject.AddComponent<Outline>();
            labelGameObject.AddComponent<Shadow>();
            var contentSizeFitter = labelGameObject.AddComponent<ContentSizeFitter>();
            contentSizeFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            contentSizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            m_Label.rectTransform.pivot = Vector2.zero;
            m_Label.fontSize = 18;
            m_Label.raycastTarget = false;
        }

        /// <summary>
        /// Sets the position and orientation to match a specified <see cref="BoardContact"/>.
        /// </summary>
        /// <param name="contact">A <see cref="BoardContact"/></param>
        public void SetPositionAndRotation(BoardContact contact)
        {
            m_HorizontalLine.color =
                m_VerticalLine.color = contact.type == BoardContactType.Glyph ? Color.blue : Color.red;
            m_RotationIndicatorImage.gameObject.SetActive(contact.type == BoardContactType.Glyph);
            m_Transform.position = contact.screenPosition;
            m_RotationIndicatorImage.transform.rotation =
                Quaternion.AngleAxis(contact.orientation * Mathf.Rad2Deg, Vector3.forward);
            m_RotationIndicatorImage.rectTransform.sizeDelta =
                new Vector2(2, Mathf.Max(kLabelOffset.x, kLabelOffset.y) * 2f);
            m_Label.rectTransform.anchoredPosition = kLabelOffset;
            if (contact.type == BoardContactType.Glyph)
            {
                m_Label.text =
                    $"Contact ID: {contact.contactId}\nGlyph ID: {contact.glyphId}";
            }
            else if (contact.type == BoardContactType.Finger)
            {
                m_Label.text = $"Contact ID: {contact.contactId}";
            }

            var pivot = m_Label.rectTransform.pivot;
            if (contact.screenPosition.x > Screen.width - m_Label.rectTransform.rect.width * 2f) // Near right edge
            {
                pivot.x = 1;
                m_Label.rectTransform.anchoredPosition *= new Vector2(-1f, 1f);
            }
            else
            {
                pivot.x = 0;
            }

            if (contact.screenPosition.y > Screen.height - m_Label.rectTransform.rect.height * 2f) // Near the top edge
            {
                pivot.y = 1;
                m_Label.rectTransform.anchoredPosition *= new Vector2(1f, -1f);
            }
            else
            {
                pivot.y = 0;
            }

            m_Label.rectTransform.pivot = pivot;
            m_BoundingBoxBackground.rectTransform.sizeDelta = contact.bounds.size * 2f;
        }
    }
}
