// <copyright file="BoardInputSettingsEditor.cs" company="Harris Hill Products Inc.">
//     Copyright (c) Harris Hill Products Inc. All rights reserved.
// </copyright>

namespace Board.Editor.Input
{
    using System;
    using System.IO;

    using Board.Input;

    using UnityEditor;

    using UnityEngine;

    /// <summary>
    /// Provides a custom editor for <see cref="BoardInputSettings"/>.
    /// </summary>
    [CustomEditor(typeof(BoardInputSettings), editorForChildClasses: true)]
    internal class BoardInputSettingsEditor : Editor
    {
        /// <summary>
        /// Encapsulates all GUI content for <see cref="BoardInputSettings"/>.
        /// </summary>
        internal class Styles
        {
            public static GUIContent translationSmoothing = new GUIContent("Translation Smoothing",
                "<b>Description:</b> Controls the smoothing applied to the translation of glyphs due to game piece motion.\n\n<b>Effect:</b> Higher values result in smoother movement, but introduce more lag and potential oscillation. Lower values respond more quickly, but can introduce jitter.\n\n<b>Recommended Use:</b> Increase for smoother translation; decrease for faster response (default = .50).");

            public static GUIContent rotationSmoothing = new GUIContent("Rotation Smoothing",
                "<b>Description:</b> Controls the smoothing applied to the rotation of glyphs due to game piece motion.\n\n<b>Effect:</b> Higher values provide smoother rotation, but introduce more lag and potential oscillation. Lower values respond more quickly, but can introduce jitter.\n\n<b>Recommended Use:</b> Increase for smoother rotation; decrease for faster response (default = 0.50).");

            public static GUIContent persistence = new GUIContent("Persistence",
                "<b>Description:</b> Controls how long a glyph remains active without confirmation from new input.\n\n<b>Effect:</b> Higher values make glyphs slower to appear and disappear, improving state stability, but inducing lag in appearance and removal. Lower values make tracking more responsive, but may prematurely remove glyphs.\n\n<b>Recommended Use:</b> Increase for more stable states; decrease for faster reaction to state change (default = 4).");
            
            public static GUIContent pieceSetModel = new GUIContent("Piece Set Model",
                "<b>Description:</b> Filename for the Piece Set Model used by this settings asset.\n\n<b>Recommended Use:</b> Use the Piece Set Model section in Project Settings > Board > Input Settings to download and configure models. The model file is stored in StreamingAssets.");
        }

        [NonSerialized] private SerializedProperty m_TranslationSmoothing;
        [NonSerialized] private SerializedProperty m_RotationSmoothing;
        [NonSerialized] private SerializedProperty m_Persistence;
        [NonSerialized] private SerializedProperty m_PieceSetModelFilename;
        [NonSerialized] private string m_GlyphsLibraryPath;

        private const string kTranslationSmoothingPropertyName = "m_TranslationSmoothing";
        private const string kRotationSmoothingPropertyName = "m_RotationSmoothing";
        private const string kPersistencePropertyName = "m_Persistence";
        private const string kPieceSetModelFilenamePropertyName = "m_GlyphModelFilename"; // Serialized field name kept for asset compatibility

        private const float kTranslationSmoothingMinimum = 0f;
        private const float kTranslationSmoothingMaximum = 1f;
        private const float kRotationAlphaMinimum = 0f;
        private const float kRotationAlphaMaximum = 1f;
        private const int kPersistenceMinimum = 4;
        private const int kPersistenceMaximum = 40;

        private const int kBrowseButtonWidth = 75;

        /// <summary>
        /// Callback invoked by Unity when the script is enabled.
        /// </summary>
        private void OnEnable()
        {
            if (target == null)
            {
                return;
            }

            m_TranslationSmoothing = serializedObject.FindProperty(kTranslationSmoothingPropertyName);
            m_RotationSmoothing = serializedObject.FindProperty(kRotationSmoothingPropertyName);
            m_Persistence = serializedObject.FindProperty(kPersistencePropertyName);
            m_PieceSetModelFilename = serializedObject.FindProperty(kPieceSetModelFilenamePropertyName);
            BoardInput.settingsChanged += OnBoardInputSettingsChanged;
        }

        /// <summary>
        /// Callback invoked by Unity when the script is disabled.
        /// </summary>
        private void OnDisable()
        {
            BoardInput.settingsChanged -= OnBoardInputSettingsChanged;
        }

        /// <summary>
        /// Callback invoked by Board's input system when the settings change.
        /// </summary>
        private void OnBoardInputSettingsChanged()
        {
            Repaint();
        }

        /// <summary>
        /// Callback invoked by Unity when rendering the GUI in the Inspector window.
        /// </summary>
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            GUILayout.Space(10);

            if (BoardInput.settings == target)
            {
                EditorGUILayout.HelpBox("This asset contains the currently active settings for the Board input system.",
                    MessageType.Info);
            }
            else
            {
                string currentlyActiveAssetsPath = null;
                if (BoardInput.settings != null)
                {
                    currentlyActiveAssetsPath = AssetDatabase.GetAssetPath(BoardInput.settings);
                }

                if (!string.IsNullOrEmpty(currentlyActiveAssetsPath))
                {
                    currentlyActiveAssetsPath =
                        $"The currently active settings are stored in {currentlyActiveAssetsPath}. ";
                }

                EditorGUILayout.HelpBox(
                    $"Note that this asset does not contain the currently active settings for the Board input system. {currentlyActiveAssetsPath ?? ""}Click \"Make Active\" below to make {target.name} the active one.",
                    MessageType.Warning);

                if (GUILayout.Button($"Make active", EditorStyles.miniButton))
                {
                    BoardInput.settings = (BoardInputSettings)target;
                }
            }

            m_TranslationSmoothing.floatValue = EditorGUILayout.Slider(Styles.translationSmoothing,
                m_TranslationSmoothing.floatValue, kTranslationSmoothingMinimum, kTranslationSmoothingMaximum);
            m_RotationSmoothing.floatValue = EditorGUILayout.Slider(Styles.rotationSmoothing,
                m_RotationSmoothing.floatValue, kRotationAlphaMinimum, kRotationAlphaMaximum);
            m_Persistence.intValue = EditorGUILayout.IntSlider(Styles.persistence, m_Persistence.intValue,
                kPersistenceMinimum, kPersistenceMaximum);
            EditorGUILayout.LabelField(Styles.pieceSetModel);
            EditorGUILayout.BeginHorizontal();
            m_PieceSetModelFilename.stringValue = EditorGUILayout.TextField(m_PieceSetModelFilename.stringValue,
                GUILayout.MaxWidth(EditorGUIUtility.currentViewWidth - kBrowseButtonWidth));

            if (GUILayout.Button("Browse", GUILayout.Width(kBrowseButtonWidth)))
            {
                var filepath = EditorUtility.OpenFilePanel("Select Piece Set Model",
                    Path.Combine("Assets", "StreamingAssets"), "tflite");
                
                if (!string.IsNullOrEmpty(filepath))
                {
                    if (filepath.StartsWith(Application.dataPath))
                    {
                        var path = Path.Combine(Application.dataPath, "StreamingAssets");
                        m_PieceSetModelFilename.stringValue = filepath.Substring(path.Length + 1);
                    }
                }
                GUIUtility.ExitGUI();
            }
            
            EditorGUILayout.EndHorizontal();
            
            if (string.IsNullOrEmpty(m_PieceSetModelFilename.stringValue))
            {
                EditorGUILayout.HelpBox(
                    "No Piece Set Model configured. Use Project Settings > Board > Input Settings to download a model.",
                    MessageType.Error);
            }

            GUILayout.Space(10);

            // Draw any additional properties from derived classes
            DrawPropertiesExcluding(serializedObject, "m_Script",
                kTranslationSmoothingPropertyName,
                kRotationSmoothingPropertyName,
                kPersistencePropertyName,
                kPieceSetModelFilenamePropertyName);

            serializedObject.ApplyModifiedProperties();
        }
    }
}
