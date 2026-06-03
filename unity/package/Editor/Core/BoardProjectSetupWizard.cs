// <copyright file="BoardProjectSetupWizard.cs" company="Harris Hill Products Inc.">
//     Copyright (c) Harris Hill Products Inc. All rights reserved.
// </copyright>

namespace Board.Editor.Core
{
    using System.Collections.Generic;
    using System.Text;

    using UnityEditor;

    using UnityEngine;

    /// <summary>
    /// Setup wizard that configures Unity project settings for the Board SDK.
    /// </summary>
    internal class BoardProjectSetupWizard : EditorWindow
    {
        private const string kWizardShownKey = "Board_ProjectSetupWizardShown";
        private const string kSwitchToAndroidKey = "Board_SetupWizard_SwitchToAndroid";
        private const string kSetApiLevelsKey = "Board_SetupWizard_SetApiLevels";
        private const string kSetScriptingBackendKey = "Board_SetupWizard_SetScriptingBackend";
        private const string kSetTargetArchitectureKey = "Board_SetupWizard_SetTargetArchitecture";
        private const string kEnableInputSystemKey = "Board_SetupWizard_EnableInputSystem";
        private const string kSetApplicationEntryPointKey = "Board_SetupWizard_SetApplicationEntryPoint";
        private const string kSetLandscapeOrientationKey = "Board_SetupWizard_SetLandscapeOrientation";

        private const int kWindowWidth = 400;
        private const int kWindowHeight = 375;

        private bool m_SwitchToAndroid;
        private bool m_SetApiLevels;
        private bool m_SetScriptingBackend;
        private bool m_SetTargetArchitecture;
        private bool m_EnableInputSystem;
        private bool m_SetApplicationEntryPoint;
        private bool m_SetLandscapeOrientation;
        private Texture m_BoardLogoTexture;
        private bool m_AnyToggleEnabled;

        /// <summary>
        /// Shows the wizard window.
        /// </summary>
        [MenuItem("Board/Configure Unity Project...", priority = 0)]
        public static void ShowWindow()
        {
            var window = GetWindow<BoardProjectSetupWizard>(true, "Board Setup Wizard", true);
            window.minSize = new Vector2(kWindowWidth, kWindowHeight);
            window.maxSize = new Vector2(kWindowWidth, kWindowHeight);
            window.Show();
        }

        /// <summary>
        /// Shows the wizard when the SDK is first imported.
        /// </summary>
        internal static void ShowOnFirstImport()
        {
            EditorPrefs.SetBool(kWizardShownKey, true);
            ShowWindow();
        }

        /// <summary>
        /// Called when the window is enabled.
        /// </summary>
        private void OnEnable()
        {
            // Load sticky preferences (default to true for new users)
            m_SwitchToAndroid = EditorPrefs.GetBool(kSwitchToAndroidKey, true);
            m_SetApiLevels = EditorPrefs.GetBool(kSetApiLevelsKey, true);
            m_SetScriptingBackend = EditorPrefs.GetBool(kSetScriptingBackendKey, true);
            m_SetTargetArchitecture = EditorPrefs.GetBool(kSetTargetArchitectureKey, true);
            m_EnableInputSystem = EditorPrefs.GetBool(kEnableInputSystemKey, true);
            m_SetApplicationEntryPoint = EditorPrefs.GetBool(kSetApplicationEntryPointKey, true);
            m_SetLandscapeOrientation = EditorPrefs.GetBool(kSetLandscapeOrientationKey, true);
            m_BoardLogoTexture =
                AssetDatabase.LoadAssetAtPath("Packages/fun.board/Editor/Assets/BoardLogo.png", typeof(Texture)) as
                    Texture;
        }

        /// <summary>
        /// Draws the wizard GUI.
        /// </summary>
        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            
            // Board Logo
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label(m_BoardLogoTexture);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            
            // Settings area
            GUILayout.BeginArea(new Rect(20, 120, kWindowWidth - 20, kWindowHeight - 120));
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Board Required Settings:", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            m_AnyToggleEnabled = false;

            // Required settings checkboxes
            DrawSettingCheckbox(ref m_SwitchToAndroid, kSwitchToAndroidKey,
                "Switch platform to Android",
                IsPlatformAndroid());

            DrawSettingCheckbox(ref m_SetApiLevels, kSetApiLevelsKey,
                "Set Minimum/Target API Level to 33 (Android 13)",
                AreApiLevelsCorrect());

            DrawSettingCheckbox(ref m_SetScriptingBackend, kSetScriptingBackendKey,
                "Set Scripting Backend to IL2CPP",
                IsScriptingBackendCorrect());

            DrawSettingCheckbox(ref m_SetTargetArchitecture, kSetTargetArchitectureKey,
                "Set Target Architecture to ARM64",
                IsTargetArchitectureCorrect());

            DrawSettingCheckbox(ref m_EnableInputSystem, kEnableInputSystemKey,
                "Enable New Input System (requires editor restart)",
                IsInputSystemEnabled());

#if UNITY_6000_0_OR_NEWER
            DrawSettingCheckbox(ref m_SetApplicationEntryPoint, kSetApplicationEntryPointKey,
                "Set Application Entry Point to Activity (Unity 6 only)",
                IsApplicationEntryPointCorrect());
#endif

            DrawSettingCheckbox(ref m_SetLandscapeOrientation, kSetLandscapeOrientationKey,
                "Set screen orientation to Landscape Left",
                IsLandscapeOrientationCorrect());

            EditorGUILayout.Space(20);

            GUI.enabled = m_AnyToggleEnabled;

            // Button
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Apply Selected Settings", GUILayout.Width(160), GUILayout.Height(30)))
            {
                ApplySettings();
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        /// <summary>
        /// Draws a setting checkbox with current status indicator.
        /// </summary>
        private void DrawSettingCheckbox(ref bool value, string prefsKey, string label, bool isCurrentlyCorrect)
        {
            EditorGUILayout.BeginHorizontal();

            if (!isCurrentlyCorrect)
            {
                var newValue = EditorGUILayout.ToggleLeft(label, value);
                if (newValue != value)
                {
                    value = newValue;
                    EditorPrefs.SetBool(prefsKey, value);
                }

                m_AnyToggleEnabled |= newValue;
            } 
            else
            {
                EditorGUILayout.BeginHorizontal();
                var originalColor = GUI.color;
                GUI.color = new Color(0.3f, 0.8f, 0.3f);
                GUILayout.Label("[OK]", GUILayout.Width(30));
                GUI.color = originalColor;
                GUILayout.Label(label);
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// Applies the selected settings.
        /// </summary>
        private void ApplySettings()
        {
            var appliedSettings = new List<string>();
            var requiresRestart = false;

            // Apply non-restart settings first
            if (m_SwitchToAndroid && !IsPlatformAndroid())
            {
                if (SwitchToAndroid())
                {
                    appliedSettings.Add("Switched platform to Android");
                }
            }

            if (m_SetApiLevels && !AreApiLevelsCorrect())
            {
                SetApiLevels();
                appliedSettings.Add("Set API levels to 33");
            }

            if (m_SetScriptingBackend && !IsScriptingBackendCorrect())
            {
                SetScriptingBackend();
                appliedSettings.Add("Set scripting backend to IL2CPP");
            }

            if (m_SetTargetArchitecture && !IsTargetArchitectureCorrect())
            {
                SetTargetArchitecture();
                appliedSettings.Add("Set target architecture to ARM64");
            }

#if UNITY_6000_0_OR_NEWER
            if (m_SetApplicationEntryPoint && !IsApplicationEntryPointCorrect())
            {
                SetApplicationEntryPoint();
                appliedSettings.Add("Set application entry point to Activity");
            }
#endif

            // Handle Input System separately (requires restart)
            if (m_EnableInputSystem && !IsInputSystemEnabled())
            {
                SetInputSystem();
                appliedSettings.Add("Enabled New Input System");
                requiresRestart = true;
            }

            if (m_SetLandscapeOrientation && !IsLandscapeOrientationCorrect())
            {
                SetLandscapeOrientation();
                appliedSettings.Add("Configured landscape orientation");
            }

            // Log summary
            if (appliedSettings.Count > 0)
            {
                var summary = new StringBuilder();
                summary.AppendLine("[Board SDK] Applied project settings:");
                foreach (var setting in appliedSettings)
                {
                    summary.AppendLine($"  - {setting}");
                }
                Debug.Log(summary.ToString());
            }
            else
            {
                Debug.Log("[Board SDK] All selected settings are already configured correctly.");
            }

            // Handle restart if needed
            if (requiresRestart)
            {
                var restart = EditorUtility.DisplayDialog(
                    "Board SDK - Restart Required",
                    "The Input System setting requires an editor restart to take effect.\n\nWould you like to restart now?",
                    "Restart",
                    "Later");

                if (restart)
                {
                    EditorApplication.OpenProject(System.IO.Directory.GetCurrentDirectory());
                    return;
                }
            }

            Close();
        }

        #region Platform Check Methods

        private static bool IsPlatformAndroid()
        {
            return EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android;
        }

        private static bool AreApiLevelsCorrect()
        {
            return PlayerSettings.Android.minSdkVersion == AndroidSdkVersions.AndroidApiLevel33 &&
                   PlayerSettings.Android.targetSdkVersion == AndroidSdkVersions.AndroidApiLevel33;
        }

        private static bool IsScriptingBackendCorrect()
        {
            return PlayerSettings.GetScriptingBackend(BuildTargetGroup.Android) == ScriptingImplementation.IL2CPP;
        }

        private static bool IsTargetArchitectureCorrect()
        {
            return (PlayerSettings.Android.targetArchitectures & AndroidArchitecture.ARM64) != 0;
        }

        internal static bool IsInputSystemEnabled()
        {
            var settings = AssetDatabase.LoadMainAssetAtPath("ProjectSettings/ProjectSettings.asset");
            if (settings == null)
            {
                return false;
            }

            var so = new SerializedObject(settings);
            var prop = so.FindProperty("activeInputHandler");
            // 0 = Old, 1 = New, 2 = Both (unsupported on Android)
            return prop != null && prop.intValue == 1;
        }

#if UNITY_6000_0_OR_NEWER
        private static bool IsApplicationEntryPointCorrect()
        {
            return PlayerSettings.Android.applicationEntry == AndroidApplicationEntry.Activity;
        }
#endif

        internal static bool IsLandscapeOrientationCorrect()
        {
            return PlayerSettings.defaultInterfaceOrientation == UIOrientation.LandscapeLeft;
        }

        #endregion

        #region Apply Setting Methods

        private static bool SwitchToAndroid()
        {
            try
            {
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
                return true;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[Board SDK] Failed to switch to Android platform. Make sure Android Build Support is installed.\n{ex.Message}");
                EditorUtility.DisplayDialog(
                    "Board SDK - Platform Switch Failed",
                    "Failed to switch to Android platform.\n\nMake sure Android Build Support is installed via Unity Hub.",
                    "OK");
                return false;
            }
        }

        private static void SetApiLevels()
        {
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel33;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevel33;
        }

        private static void SetScriptingBackend()
        {
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
        }

        private static void SetTargetArchitecture()
        {
            PlayerSettings.Android.targetArchitectures |= AndroidArchitecture.ARM64;
        }

        private static void SetInputSystem()
        {
            var settings = AssetDatabase.LoadMainAssetAtPath("ProjectSettings/ProjectSettings.asset");
            if (settings == null)
            {
                Debug.LogError("[Board SDK] Failed to load ProjectSettings.asset");
                return;
            }

            var so = new SerializedObject(settings);
            var prop = so.FindProperty("activeInputHandler");
            if (prop != null)
            {
                // Set to 1 = New (Both is unsupported on Android)
                prop.intValue = 1;
                so.ApplyModifiedProperties();
                AssetDatabase.SaveAssets();
            }
        }

#if UNITY_6000_0_OR_NEWER
        private static void SetApplicationEntryPoint()
        {
            PlayerSettings.Android.applicationEntry = AndroidApplicationEntry.Activity;
        }
#endif

        private static void SetLandscapeOrientation()
        {
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
        }

        #endregion

        #region Validation Methods

        /// <summary>
        /// Gets a list of misconfigured settings.
        /// </summary>
        /// <returns>List of setting names that are not configured correctly.</returns>
        internal static List<string> GetMisconfiguredSettings()
        {
            var issues = new List<string>();

            if (!IsPlatformAndroid())
            {
                issues.Add("Platform is not set to Android");
            }

            if (!AreApiLevelsCorrect())
            {
                issues.Add("API levels are not set to 33 (Android 13)");
            }

            if (!IsScriptingBackendCorrect())
            {
                issues.Add("Scripting backend is not IL2CPP");
            }

            if (!IsTargetArchitectureCorrect())
            {
                issues.Add("Target architecture does not include ARM64");
            }

            if (!IsInputSystemEnabled())
            {
                issues.Add("New Input System is not enabled");
            }

            if (!IsLandscapeOrientationCorrect())
            {
                issues.Add("Screen orientation is not configured for landscape");
            }

#if UNITY_6000_0_OR_NEWER
            if (!IsApplicationEntryPointCorrect())
            {
                issues.Add("Application entry point is not set to Activity");
            }
#endif

            return issues;
        }

        /// <summary>
        /// Checks if the wizard has been shown before.
        /// </summary>
        internal static bool HasWizardBeenShown()
        {
            return EditorPrefs.GetBool(kWizardShownKey, false);
        }

        #endregion
    }

    /// <summary>
    /// Detects when the Board SDK is imported and logs a message about project setup.
    /// </summary>
    internal class BoardProjectSetupPostprocessor : AssetPostprocessor
    {
        private const string kImportMessageShownKey = "Board_ProjectSetupImportMessageShown";

        /// <summary>
        /// Called after importing assets.
        /// </summary>
        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            foreach (var asset in importedAssets)
            {
                // Detect SDK import by looking for the package.json
                if (asset.Contains("fun.board") && asset.EndsWith("package.json"))
                {
                    if (!EditorPrefs.GetBool(kImportMessageShownKey, false))
                    {
                        EditorPrefs.SetBool(kImportMessageShownKey, true);
                        Debug.Log("[Board SDK] Imported successfully. Run <b>Board > Configure Unity Project...</b> to set up your project.");
                    }
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Validates project settings on domain reload and logs warnings if misconfigured.
    /// </summary>
    [InitializeOnLoad]
    internal static class BoardProjectSettingsValidator
    {
        static BoardProjectSettingsValidator()
        {
            // Only validate if the wizard has been shown (user is aware of SDK)
            if (!BoardProjectSetupWizard.HasWizardBeenShown())
            {
                return;
            }

            // Delay the check to ensure the editor is fully loaded
            EditorApplication.delayCall += ValidateSettings;
        }

        private static void ValidateSettings()
        {
            var issues = BoardProjectSetupWizard.GetMisconfiguredSettings();

            if (issues.Count > 0)
            {
                var message = new StringBuilder();
                message.AppendLine("[Board SDK] Project settings are not configured correctly for Board development:");
                foreach (var issue in issues)
                {
                    message.AppendLine($"  - {issue}");
                }
                message.AppendLine("\nRun Board > Configure Unity Project... to fix these issues.");

                Debug.LogWarning(message.ToString());
            }
        }
    }
}
