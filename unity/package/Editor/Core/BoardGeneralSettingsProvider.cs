// <copyright file="BoardGeneralSettingsProvider.cs" company="Harris Hill Products Inc.">
//     Copyright (c) Harris Hill Products Inc. All rights reserved.
// </copyright>

namespace Board.Editor.Core
{
    using System;
    using System.IO;
    
    using Board.Core;
    
    using UnityEditor;
    
    using UnityEngine;
    using UnityEngine.UIElements;
    
    /// <summary>
    /// Provides the user interface for modifying <see cref="BoardGeneralSettings"/>.
    /// </summary>
    internal class BoardGeneralSettingsProvider : SettingsProvider
    {
        [SerializeField] private BoardGeneralSettings m_Settings;
        [SerializeField] private bool m_SettingsIsNotAnAsset;
        
        [NonSerialized] private Editor m_SettingsEditor;
        
        private const string kSettingsPath = "Project/Board/General Settings";
  
        /// <summary>
        /// Creates a new instance of the <see cref="BoardGeneralSettingsProvider"/> class.
        /// </summary>
        /// <param name="path">Path used to place the SettingsProvider in the tree view of the Settings window.</param>
        /// <param name="scope"><see cref="SettingsScope"/> of the SettingsProvider.</param>
        private BoardGeneralSettingsProvider(string path, SettingsScope scope)
            : base(path, scope)
        {
            label = "General Settings";
        }
        
        /// <summary>
        /// Creates a new <see cref="BoardGeneralSettings"/> asset.
        /// </summary>
        private static void CreateNewSettingsAsset()
        {
            // Query for file name.
            var projectName = PlayerSettings.productName;
            var path = EditorUtility.SaveFilePanel("Create Board General Settings File", "Assets",
                "BoardGeneralSettings", "asset");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            // Make sure the path is in the Assets/ folder.
            path = path.Replace("\\", "/"); // Make sure we only get '/' separators.
            var dataPath = Application.dataPath + "/";
            if (!path.StartsWith(dataPath, StringComparison.CurrentCultureIgnoreCase))
            {
                Debug.LogError($"Board general settings must be stored in Assets folder of the project (got: '{path}')");
                return;
            }

            // Make sure it ends with .asset.
            var extension = Path.GetExtension(path);
            if (string.Compare(extension, ".asset", StringComparison.InvariantCultureIgnoreCase) != 0)
            {
                path += ".asset";
            }

            // Create settings file.
            var relativePath = "Assets/" + path.Substring(dataPath.Length);
            var settings = ScriptableObject.CreateInstance<BoardGeneralSettings>();
            settings.applicationId = Guid.NewGuid().ToString();
            AssetDatabase.CreateAsset(settings, relativePath);
            BoardGeneralSettings.instance = settings;
            EditorGUIUtility.PingObject(settings);
        }
        
        /// <summary>
        /// Grab the current <see cref="BoardGeneralSettings"/> and set it up for editing.
        /// </summary>
        private void InitializeWithCurrentSettings()
        {
            // Grab the current settings instance
            var settings = BoardGeneralSettings.instance;
            if (settings == m_Settings)
            {
                return;
            }
         
            m_Settings = settings;
            
            // Initialize the custom editor for the settings object
            if (m_Settings != null && (m_SettingsEditor == null || m_SettingsEditor.target != m_Settings))
            {
                if (m_SettingsEditor != null)
                {
                    UnityEngine.Object.DestroyImmediate(m_SettingsEditor);
                }

                m_SettingsEditor = Editor.CreateEditor(m_Settings);
            }
        }
        
        /// <summary>
        /// Callback invoked by Unity when the user clicks on the Settings in the Settings window.
        /// </summary>
        /// <param name="searchContext">Search context in the search box on the Settings window.</param>
        /// <param name="rootElement">Root of the UIElements tree.</param>
        public override void OnActivate(string searchContext, VisualElement rootElement)
        {
            if (m_SettingsEditor != null)
            {
                UnityEngine.Object.DestroyImmediate(m_SettingsEditor);
            }
            
            InitializeWithCurrentSettings();
            base.OnActivate(searchContext, rootElement);
        }
        
        /// <summary>
        /// Callback invoked by Unity when the user clicks on another setting or when the Settings window closes.
        /// </summary>
        public override void OnDeactivate()
        {
            if (m_SettingsEditor != null)
            {
                UnityEngine.Object.DestroyImmediate(m_SettingsEditor);
            }
            
            m_SettingsEditor = null;
            m_Settings = null; 
            base.OnDeactivate();
        }

        /// <summary>
        /// Callback invoked by Unity to draw the UI.
        /// </summary>
        /// <param name="searchContext">Search context in the search box on the Settings window.</param>
        public override void OnGUI(string searchContext)
        {
            InitializeWithCurrentSettings();
            
            using (new EditorGUI.DisabledScope(m_Settings == null))
            {
                EditorGUILayout.Space();
                EditorGUILayout.Separator();
                EditorGUILayout.Space();


                if (m_Settings == null)
                {
                    EditorGUILayout.HelpBox(
                        $"This project does not currently have a Board general settings asset.",
                        MessageType.Warning);

                    if (GUILayout.Button($"Make New Settings Asset", EditorStyles.miniButton))
                    {
                        CreateNewSettingsAsset();
                    }
                } 
                
                if (m_SettingsEditor != null)
                {
                    m_SettingsEditor.OnInspectorGUI();
                }
            }
        }

        /// <summary>
        /// Opens Unity's project settings window to this provider.
        /// </summary>
        [MenuItem("Board/Settings", false, 1)]
        public static void Open()
        {
            SettingsService.OpenProjectSettings(kSettingsPath);
        }
        
        /// <summary>
        /// Creates the settings provider for <see cref="BoardGeneralSettings"/>.
        /// </summary>
        [SettingsProvider]
        public static SettingsProvider CreateInputSettingsProvider()
        {
            return new BoardGeneralSettingsProvider(kSettingsPath, SettingsScope.Project);
        }
    }
}