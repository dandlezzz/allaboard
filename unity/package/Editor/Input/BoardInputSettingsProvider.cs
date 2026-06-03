// <copyright file="BoardInputSettingsProvider.cs" company="Harris Hill Products Inc.">
//     Copyright (c) Harris Hill Products Inc. All rights reserved.
// </copyright>

namespace Board.Editor.Input
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    using Board.Editor.Core;
    using Board.Input;

    using UnityEditor;

    using UnityEngine;
    using UnityEngine.UIElements;
    
    /// <summary>
    /// Provides the user interface for modifying <see cref="BoardInputSettings"/>.
    /// </summary>
    internal class BoardInputSettingsProvider : SettingsProvider
    {
        [SerializeField] private BoardInputSettings m_Settings;
        [SerializeField] private bool m_SettingsIsNotAnAsset;
        
        [NonSerialized] private List<string> m_AvailableInputSettingsAssets;
        [NonSerialized] private GUIContent[] m_AvailableSettingsAssetsHeaderOptions;
        [NonSerialized] private GUIContent[] m_AvailableSettingsAssetsOptions;
        [NonSerialized] private int m_CurrentSelectedInputSettingsAsset = -1;
        [NonSerialized] private Editor m_SettingsEditor;

        // Model download state
        [NonSerialized] private BoardModelManifest m_Manifest;
        [NonSerialized] private bool m_IsLoadingManifest;
        [NonSerialized] private bool m_IsDownloading;
        [NonSerialized] private float m_DownloadProgress;
        [NonSerialized] private int m_SelectedModelIndex = -1;
        [NonSerialized] private string m_ManifestErrorMessage;
        
        private const string kSettingsPath = "Project/Board/Input Settings";
        
        /// <summary>
        /// Creates a new instance of the <see cref="BoardInputSettingsProvider"/> class.
        /// </summary>
        /// <param name="path">Path used to place the SettingsProvider in the tree view of the Settings window.</param>
        /// <param name="scope"><see cref="SettingsScope"/> of the SettingsProvider.</param>
        private BoardInputSettingsProvider(string path, SettingsScope scope)
            : base(path, scope)
        {
            label = "Input Settings";
        }
        
        /// <summary>
        /// Creates a new <see cref="BoardInputSettings"/> asset at the specified path
        /// </summary>
        /// <param name="relativePath">The relative path to save the newly created asset.</param>
        private static void CreateNewSettingsAsset(string relativePath)
        {
            var settings = ScriptableObject.CreateInstance<BoardInputSettings>();
            AssetDatabase.CreateAsset(settings, relativePath);
            EditorGUIUtility.PingObject(settings);
            BoardInput.settings = settings;
        }

        /// <summary>
        /// Creates a new <see cref="BoardInputSettings"/> asset.
        /// </summary>
        private static void CreateNewSettingsAsset()
        {
            // Query for file name.
            var projectName = PlayerSettings.productName;
            var path = EditorUtility.SaveFilePanel("Create Board Input Settings File", "Assets",
                string.Join(string.Empty, projectName.Split(default(string[]), StringSplitOptions.RemoveEmptyEntries)) +
                "BoardInputSettings", "asset");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            // Make sure the path is in the Assets/ folder.
            path = path.Replace("\\", "/"); // Make sure we only get '/' separators.
            var dataPath = Application.dataPath + "/";
            if (!path.StartsWith(dataPath, StringComparison.CurrentCultureIgnoreCase))
            {
                Debug.LogError($"Board input settings must be stored in Assets folder of the project (got: '{path}')");
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
            CreateNewSettingsAsset(relativePath);
        }

        /// <summary>
        /// Grab <see cref="BoardInput.settings"/> and set it up for editing.
        /// </summary>
        private void InitializeWithCurrentSettings()
        {
            // Find the set of available assets in the project.
            m_AvailableInputSettingsAssets = FindInputSettingsInProject().ToList();

            // See which is the active one
            m_Settings = BoardInput.settings;
            var currentSettingsPath = AssetDatabase.GetAssetPath(m_Settings);
            if (string.IsNullOrEmpty(currentSettingsPath))
            {
                if (m_AvailableInputSettingsAssets.Count != 0)
                {
                    m_CurrentSelectedInputSettingsAsset = 0;
                    m_Settings = AssetDatabase.LoadAssetAtPath<BoardInputSettings>(m_AvailableInputSettingsAssets[0]);
                    BoardInput.settings = m_Settings;
                }
            }
            else
            {
                m_CurrentSelectedInputSettingsAsset = m_AvailableInputSettingsAssets.IndexOf(currentSettingsPath);
                if (m_CurrentSelectedInputSettingsAsset == -1)
                {
                    // This is odd and shouldn't happen. Solve by just adding the path to the list.
                    m_AvailableInputSettingsAssets.Add(currentSettingsPath);
                    m_CurrentSelectedInputSettingsAsset = m_AvailableInputSettingsAssets.Count - 1;
                }
            }
            
            // Refresh the list of assets we display in the UI.
            m_AvailableSettingsAssetsOptions = new GUIContent[m_AvailableInputSettingsAssets.Count];
            m_AvailableSettingsAssetsHeaderOptions = new GUIContent[m_AvailableInputSettingsAssets.Count];
            for (var i = 0; i < m_AvailableInputSettingsAssets.Count; ++i)
            {
                var name = m_AvailableInputSettingsAssets[i];
                if (name.StartsWith("Assets/"))
                {
                    name = name.Substring("Assets/".Length);
                }

                if (name.EndsWith(".asset"))
                {
                    name = name.Substring(0, name.Length - ".asset".Length);
                }
                
                m_AvailableSettingsAssetsOptions[i] = new GUIContent(name.Replace("/", "\u29f8"));
                m_AvailableSettingsAssetsHeaderOptions[i] = new GUIContent(name);
            }
            
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
        /// Selects the <see cref="BoardInputSettings"/> asset at the specified asset path.
        /// </summary>
        /// <param name="path">The path to a <see cref="BoardInputSettings"/> asset.</param>
        private void SelectSettingsAsset(string path)
        {
            m_CurrentSelectedInputSettingsAsset =
                m_AvailableInputSettingsAssets.IndexOf((string)path);
            m_Settings = AssetDatabase.LoadAssetAtPath<BoardInputSettings>(m_AvailableInputSettingsAssets[m_CurrentSelectedInputSettingsAsset]);
            
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
        /// Callback invoked by Board's Input system when the settings change.
        /// </summary>
        private void OnSettingsChanged()
        {
            if (m_Settings == null)
            {
                InitializeWithCurrentSettings();
                Repaint();
            }
        }
        
        /// <summary>
        /// Find all <see cref="BoardInputSettings"/> stored in assets in the current project.
        /// </summary>
        /// <returns>List of GUIDs of all <see cref="BoardInputSettings"/> in project.</returns>
        private static string[] FindInputSettingsInProject()
        {
            var guids = AssetDatabase.FindAssets("t:BoardInputSettings");
            return guids.Select(guid => AssetDatabase.GUIDToAssetPath(guid)).ToArray();
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
            BoardInput.settingsChanged += OnSettingsChanged;
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
            
            BoardInput.settingsChanged -= OnSettingsChanged;
            base.OnDeactivate();
        }
        
        /// <summary>
        /// Callback invoked by Unity to draw the UI.
        /// </summary>
        /// <param name="searchContext">Search context in the search box on the Settings window.</param>
        public override void OnGUI(string searchContext)
        {
            if (m_AvailableInputSettingsAssets.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "Settings for the Board input system are stored in an asset. Click the button below to create a settings asset you can edit.",
                    MessageType.Info);
                if (GUILayout.Button("Create settings asset", GUILayout.Height(30)))
                {
                    CreateNewSettingsAsset("Assets/BoardInputSettings.asset");
                    InitializeWithCurrentSettings();
                }

                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.Separator();
            EditorGUILayout.Space();

            Debug.Assert(m_Settings != null);

            if (EditorGUILayout.DropdownButton(
                    m_CurrentSelectedInputSettingsAsset < 0 || m_CurrentSelectedInputSettingsAsset >=
                    m_AvailableSettingsAssetsOptions.Length
                        ? GUIContent.none
                        : m_AvailableSettingsAssetsHeaderOptions[m_CurrentSelectedInputSettingsAsset], FocusType.Passive))
            {
                var menu = new GenericMenu();
                menu.AddDisabledItem(new GUIContent("Available Settings Assets:"));
                menu.AddSeparator("");
                for (var i = 0; i < m_AvailableSettingsAssetsOptions.Length; i++)
                    menu.AddItem(new GUIContent(m_AvailableSettingsAssetsOptions[i]),
                        m_CurrentSelectedInputSettingsAsset == i,
                        (path) =>
                        {
                            SelectSettingsAsset((string)path);
                        }, m_AvailableInputSettingsAssets[i]);
                menu.AddSeparator("");
                menu.AddItem(new GUIContent("New Settings Asset…"), false, CreateNewSettingsAsset);
                menu.ShowAsContext();
                Event.current.Use();
            }

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUI.indentLevel = 1;
            if (m_SettingsEditor != null)
            {
                m_SettingsEditor.OnInspectorGUI();
            }
            EditorGUI.indentLevel = 0;

            EditorGUILayout.Space();
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);

            // Model download section
            DrawModelDownloadSection();
        }

        /// <summary>
        /// Fetches the model manifest from the server.
        /// </summary>
        private async void FetchManifest()
        {
            m_IsLoadingManifest = true;
            m_ManifestErrorMessage = null;
            Repaint();

            m_Manifest = await BoardModelDownloader.FetchManifestAsync();

            if (m_Manifest == null)
            {
                m_ManifestErrorMessage = "Could not fetch model list.";
            }
            else if (m_Manifest.models == null || m_Manifest.models.Count == 0)
            {
                m_ManifestErrorMessage = "No models available.";
            }
            else
            {
                // Select first model by default
                m_SelectedModelIndex = 0;
            }

            m_IsLoadingManifest = false;
            Repaint();
        }

        /// <summary>
        /// Draws the model download section.
        /// </summary>
        private void DrawModelDownloadSection()
        {
            EditorGUILayout.LabelField("Piece Set Model", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            // Loading state
            if (m_IsLoadingManifest)
            {
                EditorGUILayout.LabelField("Loading available models...", EditorStyles.miniLabel);
                return;
            }

            // Not yet loaded
            if (m_Manifest == null && string.IsNullOrEmpty(m_ManifestErrorMessage))
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Load Available Models", GUILayout.Width(160)))
                {
                    FetchManifest();
                }
                EditorGUILayout.EndHorizontal();

                // Show current model status
                DrawCurrentModelStatus();
                return;
            }

            // Error state
            if (!string.IsNullOrEmpty(m_ManifestErrorMessage))
            {
                EditorGUILayout.BeginHorizontal();
                var originalColor = GUI.color;
                GUI.color = new Color(1f, 0.6f, 0.2f);
                EditorGUILayout.LabelField(m_ManifestErrorMessage, EditorStyles.miniLabel);
                GUI.color = originalColor;
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(5);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Open Developer Portal", GUILayout.Width(140)))
                {
                    Application.OpenURL(BoardModelDownloader.developerPortalUrl);
                }
                if (GUILayout.Button("Retry", GUILayout.Width(60)))
                {
                    FetchManifest();
                }
                EditorGUILayout.EndHorizontal();

                DrawCurrentModelStatus();
                return;
            }

            // Normal state - show model selection
            if (m_Manifest != null && m_Manifest.models != null && m_Manifest.models.Count > 0)
            {
                EditorGUILayout.LabelField(
                    "Select a Piece Set Model below. If not already downloaded, it will be fetched automatically.",
                    EditorStyles.wordWrappedMiniLabel);

                EditorGUILayout.Space(5);

                // Model dropdown
                var modelNames = new string[m_Manifest.models.Count];
                for (var i = 0; i < m_Manifest.models.Count; i++)
                {
                    var model = m_Manifest.models[i];
                    modelNames[i] = $"{model.displayName} ({model.sizeFormatted})";
                }

                m_SelectedModelIndex = EditorGUILayout.Popup("Available Models", m_SelectedModelIndex, modelNames);

                EditorGUILayout.Space(5);

                // Select button or progress
                if (m_IsDownloading)
                {
                    EditorGUI.ProgressBar(EditorGUILayout.GetControlRect(false, 20), m_DownloadProgress, "Downloading...");
                }
                else
                {
                    EditorGUILayout.BeginHorizontal();
                    GUI.enabled = m_SelectedModelIndex >= 0;
                    if (GUILayout.Button("Select", GUILayout.Width(80)))
                    {
                        SelectModel();
                    }
                    GUI.enabled = true;
                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.Space(5);
                DrawCurrentModelStatus();
            }
        }

        /// <summary>
        /// Draws the current model status line.
        /// </summary>
        private void DrawCurrentModelStatus()
        {
            var currentModel = m_Settings?.pieceSetModelFilename;
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Current model:", GUILayout.Width(90));

            var originalColor = GUI.color;
            if (string.IsNullOrEmpty(currentModel))
            {
                GUI.color = new Color(1f, 0.6f, 0.2f);
                EditorGUILayout.LabelField("(none configured)");
            }
            else if (!BoardModelDownloader.ModelFileExists(currentModel))
            {
                GUI.color = new Color(1f, 0.4f, 0.4f);
                EditorGUILayout.LabelField($"{currentModel} (file missing!)");
            }
            else
            {
                GUI.color = new Color(0.3f, 0.8f, 0.3f);
                EditorGUILayout.LabelField(currentModel);
            }
            GUI.color = originalColor;

            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// Selects the model, downloading if necessary, and configures settings.
        /// </summary>
        private async void SelectModel()
        {
            if (m_SelectedModelIndex < 0 || m_Manifest == null || m_SelectedModelIndex >= m_Manifest.models.Count)
                return;

            var model = m_Manifest.models[m_SelectedModelIndex];

            // Check if model already exists locally
            if (!BoardModelDownloader.ModelFileExists(model.filename))
            {
                m_IsDownloading = true;
                m_DownloadProgress = 0;
                Repaint();

                var progress = new Progress<float>(p =>
                {
                    m_DownloadProgress = p;
                    Repaint();
                });

                var success = await BoardModelDownloader.DownloadModelAsync(model, progress);

                m_IsDownloading = false;

                if (!success)
                {
                    Repaint();
                    return;
                }
            }

            // Configure the inspected settings
            if (m_Settings != null)
            {
                m_Settings.pieceSetModelFilename = model.filename;
                EditorUtility.SetDirty(m_Settings);
                AssetDatabase.SaveAssets();

                // Refresh the settings editor to show the updated value
                if (m_SettingsEditor != null)
                {
                    UnityEngine.Object.DestroyImmediate(m_SettingsEditor);
                    m_SettingsEditor = Editor.CreateEditor(m_Settings);
                }
            }

            Repaint();
        }

        /// <summary>
        /// Opens Unity's project settings window to this provider.
        /// </summary>
        public static void Open()
        {
            SettingsService.OpenProjectSettings(kSettingsPath);
        }
        
        /// <summary>
        /// Creates the settings provider for <see cref="BoardInputSettings"/>.
        /// </summary>
        [SettingsProvider]
        public static SettingsProvider CreateInputSettingsProvider()
        {
            return new BoardInputSettingsProvider(kSettingsPath, SettingsScope.Project);
        }
    }
}
