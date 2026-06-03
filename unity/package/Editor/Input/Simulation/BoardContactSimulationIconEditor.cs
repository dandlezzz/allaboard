// <copyright file="BoardContactSimulationIconEditor.cs" company="Harris Hill Products Inc.">
//     Copyright (c) Harris Hill Products Inc. All rights reserved.
// </copyright>

namespace Board.Input.Simulation
{
    using UnityEditor;

    using UnityEngine;

    /// <summary>
    /// Provides a custom editor for <see cref="BoardContactSimulationIcon"/>.
    /// </summary>
    [CustomEditor(typeof(BoardContactSimulationIcon), true)]
    [CanEditMultipleObjects]
    public class BoardContactSimulationIconEditor : Editor
    {
        /// <summary>
        /// Encapsulates all GUI content for <see cref="BoardContactSimulationIconEditor"/>.
        /// </summary>
        private static class Contents
        {
            public static readonly GUIContent displayNameLabel =
                new GUIContent("Display Name", "The display name for this icon.");

            public static readonly GUIContent contactTypeLabel =
                new GUIContent("Contact Type", "The contact type for this icon represents.");

            public static readonly GUIContent glyphIdLabel =
                new GUIContent("Glyph ID", "The glyph identifier this icon represents.");

            public static readonly GUIContent spriteLabel =
                new GUIContent("Sprite", "Sprite used for the Board simulator.");
            
            public static readonly GUIContent sizeLabel =
                new GUIContent("Size", "Screen size of the sprite when used for the Board simulator.");
            
            public static readonly GUIContent alphaThresholdLabel =
                new GUIContent("Alpha Hit Test Minimum Threshold", "The minimum alpha a pixel must have for a pointer event to be considered a \"hit\" on the icon.");
        }

        private const string kNamePropertyName = "m_Name";
        private const string kDisplayNamePropertyName = "m_DisplayName";
        private const string kContactTypePropertyName = "m_ContactType";
        private const string kGlyphIdPropertyName = "m_GlyphId";
        private const string kSpritePropertyName = "m_Sprite";
        private const string kSizePropertyName = "m_Size";
        private const string kAlphaThresholdProperty = "m_AlphaHitTestMinimumThreshold";
        private const string kDefaultSpriteAssetPath = "Packages/fun.board/Editor/Assets/Simulation/Images/DefaultIcon.png";

        private SerializedProperty m_NameProperty;
        private SerializedProperty m_DisplayNameProperty;
        private SerializedProperty m_ContactTypeProperty;
        private SerializedProperty m_GlyphIdProperty;
        private SerializedProperty m_SpriteProperty;
        private SerializedProperty m_SizeProperty;
        private SerializedProperty m_AlphaThresholdProperty;
        
        /// <summary>
        /// Method invoked when this <see cref="CustomEditor"/> is enabled.
        /// </summary>
        private void OnEnable()
        {
            m_NameProperty = serializedObject.FindProperty(kNamePropertyName);
            m_DisplayNameProperty = serializedObject.FindProperty(kDisplayNamePropertyName);
            m_ContactTypeProperty = serializedObject.FindProperty(kContactTypePropertyName);
            m_GlyphIdProperty = serializedObject.FindProperty(kGlyphIdPropertyName);
            m_SpriteProperty = serializedObject.FindProperty(kSpritePropertyName);
            m_SizeProperty = serializedObject.FindProperty(kSizePropertyName);
            m_AlphaThresholdProperty = serializedObject.FindProperty(kAlphaThresholdProperty);
        }

        /// <inheritdoc cref="Editor.OnInspectorGUI"/>
        public override void OnInspectorGUI()
        {
            // Ensure that the display name can never be blank
            if (string.IsNullOrEmpty(m_DisplayNameProperty.stringValue))
            {
                m_DisplayNameProperty.stringValue = m_NameProperty.stringValue;
            }

            // Ensure that the sprite can never be blank
            if (m_SpriteProperty.objectReferenceValue == null)
            {
                m_SpriteProperty.objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(kDefaultSpriteAssetPath);
            }
            
            var contactType = (BoardContactType)m_ContactTypeProperty.enumValueIndex;
            EditorGUILayout.PropertyField(m_DisplayNameProperty, Contents.displayNameLabel);
            EditorGUILayout.PropertyField(m_ContactTypeProperty, Contents.contactTypeLabel);
            if (contactType == BoardContactType.Glyph)
            {
                EditorGUILayout.PropertyField(m_GlyphIdProperty, Contents.glyphIdLabel);
            }
            else
            {
                m_GlyphIdProperty.intValue = -1;
            }

            EditorGUILayout.PropertyField(m_SpriteProperty, Contents.spriteLabel);
            EditorGUILayout.PropertyField(m_SizeProperty, Contents.sizeLabel);
            EditorGUILayout.Slider(m_AlphaThresholdProperty, 0f, 1f, Contents.alphaThresholdLabel);
            serializedObject.ApplyModifiedProperties();
        }
    }
}
