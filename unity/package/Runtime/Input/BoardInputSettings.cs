// <copyright file="BoardInputSettings.cs" company="Harris Hill Products Inc.">
//     Copyright (c) Harris Hill Products Inc. All rights reserved.
// </copyright>

namespace Board.Input
{
    using System;
#if UNITY_EDITOR
    using System.IO;

    using UnityEditor;
#endif //UNITY_EDITOR
    using UnityEngine;
    
    /// <summary>
    /// Encapsulates all input settings for the Board platform.
    /// </summary>
    [CreateAssetMenu(fileName = "BoardInputSettings", menuName = "Board/Input Settings")]
    public class BoardInputSettings : ScriptableObject
    {
        [SerializeField] private float m_TranslationSmoothing = 0.5f;
        [SerializeField] private float m_RotationSmoothing = 0.5f;
        [SerializeField] private int m_Persistence = 4;
        [SerializeField] private string m_GlyphModelFilename;

#if UNITY_EDITOR
        internal const string kAssetsPath = "Assets";
        internal static readonly string kAssetsRelativeSettingsPath = Path.Combine("Board", "Settings");
#endif //UNITY_EDITOR

        /// <summary>
        /// Gets the Translation Smoothing setting value.
        /// </summary>
        public float translationSmoothing => m_TranslationSmoothing;

        /// <summary>
        /// Gets the Rotation Smoothing setting value.
        /// </summary>
        public float rotationSmoothing => m_RotationSmoothing;

        /// <summary>
        /// Gets the Persistence setting value.
        /// </summary>
        public int persistence => m_Persistence;

        /// <summary>
        /// Gets or sets the Piece Set Model filename.
        /// </summary>
        public string pieceSetModelFilename
        {
            get => m_GlyphModelFilename;
#if UNITY_EDITOR
            set => m_GlyphModelFilename = value;
#endif //UNITY_EDITOR
        }

        /// <summary>
        /// Gets or sets the Piece Set Model filename.
        /// </summary>
        [Obsolete("Use pieceSetModelFilename instead.")]
        public string glyphModelFilename
        {
            get => pieceSetModelFilename;
#if UNITY_EDITOR
            set => pieceSetModelFilename = value;
#endif //UNITY_EDITOR
        }

#if UNITY_EDITOR
        /// <summary>
        /// Creates a new scriptable object asset for <see cref="BoardInputSettings"/>.
        /// </summary>
        /// <returns>A new instance of <see cref="BoardInputSettings"/>.</returns>
        internal static BoardInputSettings CreateInstanceAsset()
        {
            // Ensure all parent directories of settings asset exists
            var settingsPath = Path.Combine(kAssetsPath, kAssetsRelativeSettingsPath, "BoardInputSettings.asset");
            var pathSplits = settingsPath.Split(Path.DirectorySeparatorChar);
            var runningPath = pathSplits[0];
            for (var i = 1; i < pathSplits.Length - 1; i++)
            {
                var pathSplit = pathSplits[i];
                var nextPath = Path.Combine(runningPath, pathSplit);
                if (!AssetDatabase.IsValidFolder(nextPath))
                {
                    AssetDatabase.CreateFolder(runningPath, pathSplit);
                }

                runningPath = nextPath;
            }

            // Create settings asset at specified path
            var settings = CreateInstance<BoardInputSettings>();
            AssetDatabase.CreateAsset(settings, settingsPath);

            EditorBuildSettings.AddConfigObject(BoardInput.kEditorBuildSettingsConfigKey, settings, true);

            return settings;
        }
#endif //UNITY_EDITOR
    }
}
