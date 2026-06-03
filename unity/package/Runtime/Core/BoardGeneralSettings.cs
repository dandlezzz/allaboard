// <copyright file="BoardGeneralSettings.cs" company="Harris Hill Products Inc.">
//     Copyright (c) Harris Hill Products Inc. All rights reserved.
// </copyright>

namespace Board.Core
{
#if UNITY_EDITOR
    using System;
    using System.IO;
    
    using UnityEditor;
#endif //UNITY_EDITOR
    using UnityEngine;

    /// <summary>
    /// Encapsulates all general settings for the Board platform.
    /// </summary>
    public class BoardGeneralSettings : ScriptableObject
    {
        [SerializeField] private string m_ApplicationId;
        [SerializeField] private BoardLogLevel m_LogLevel = BoardLogLevel.All;

        private static BoardGeneralSettings s_Instance;
        
#if UNITY_EDITOR
        internal const string kAssetsPath = "Assets";
        internal static readonly string kAssetsRelativeSettingsPath = Path.Combine("Board","Settings");
        internal const string kEditorBuildSettingsConfigKey = "fun.board.settings";
#endif //UNITY_EDITOR

        /// <summary>
        /// Gets the single instance of <see cref="BoardGeneralSettings"/>.
        /// </summary>
        public static BoardGeneralSettings instance
        {
            get => GetOrCreateAssetInstance();
#if UNITY_EDITOR
            internal set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }
                
                EditorBuildSettings.AddConfigObject(kEditorBuildSettingsConfigKey, value, true);
            }
#endif
        }

        /// <summary>
        /// Gets the application identifier for the current application.
        /// </summary>
        public string applicationId
        {
            get => m_ApplicationId;
#if UNITY_EDITOR
            set => m_ApplicationId = value;
#endif //UNITY_EDITOR
        }

        /// <summary>
        /// Gets the <see cref="BoardLogLevel"/> for the current application.
        /// </summary>
        public BoardLogLevel logLevel
        {
            get => m_LogLevel;
#if UNITY_EDITOR
            set => m_LogLevel = value;
#endif //UNITY_EDITOR
        }
        
#if !UNITY_EDITOR
        /// <summary>
        /// Static instance that will hold the runtime asset instance created during the build process.
        /// </summary>
        private static BoardGeneralSettings s_RuntimeInstance;
#endif

        /// <summary>
        /// On devices, this will be called when the application is loaded.
        /// </summary>
        private void Awake()
        {
#if !UNITY_EDITOR
            s_RuntimeInstance = this;
#endif
        }

        /// <summary>
        /// Gets or creates an instance of <see cref="BoardGeneralSettings"/>.
        /// </summary>
        /// <returns>A new or existing instance of <see cref="BoardGeneralSettings"/>.</returns>
        private static BoardGeneralSettings GetOrCreateAssetInstance()
        {
            BoardGeneralSettings settings = null;

#if UNITY_EDITOR
            if (!EditorBuildSettings.TryGetConfigObject(kEditorBuildSettingsConfigKey, out settings))
            {
                settings = CreateInstanceAsset();
            }
#else
            settings = s_RuntimeInstance;
            if (settings == null)
            {
                Debug.LogWarning("[Board] BoardGeneralSettings asset cannot be found. Creating a new one.");
                settings = CreateInstance<BoardGeneralSettings>();
            }
#endif

            return settings;
        }
        
#if UNITY_EDITOR
        /// <summary>
        /// Creates a new scriptable object asset for <see cref="BoardGeneralSettings"/>.
        /// </summary>
        /// <returns>A new instance of <see cref="BoardGeneralSettings"/>.</returns>
        internal static BoardGeneralSettings CreateInstanceAsset()
        {
            // ensure all parent directories of settings asset exists
            var settingsPath = Path.Combine(kAssetsPath, kAssetsRelativeSettingsPath, "BoardGeneralSettings.asset");
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

            // create settings asset at specified path
            var settings = CreateInstance<BoardGeneralSettings>();
            settings.applicationId = Guid.NewGuid().ToString();
            AssetDatabase.CreateAsset(settings, settingsPath);

            EditorBuildSettings.AddConfigObject(kEditorBuildSettingsConfigKey, settings, true);

            return settings;
        }
#endif //UNITY_EDITOR
    }
}