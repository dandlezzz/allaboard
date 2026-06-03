// <copyright file="BoardProjectSettingsBuildValidator.cs" company="Harris Hill Products Inc.">
//     Copyright (c) Harris Hill Products Inc. All rights reserved.
// </copyright>

#if UNITY_ANDROID
namespace Board.Editor.Core
{
    using System.Text;

    using UnityEditor;
    using UnityEditor.Build;
    using UnityEditor.Build.Reporting;

    using UnityEngine;

    /// <summary>
    /// Validates Unity project settings required for Board development at build time.
    /// </summary>
    internal class BoardProjectSettingsBuildValidator : IPreprocessBuildWithReport
    {
        /// <summary>
        /// Gets the relative callback order. Runs before other Board validators.
        /// </summary>
        public int callbackOrder => -1;

        /// <summary>
        /// Callback invoked by Unity before a Player build is started.
        /// </summary>
        /// <param name="report">A <see cref="BuildReport"/> containing information about the build.</param>
        public void OnPreprocessBuild(BuildReport report)
        {
            var errors = new StringBuilder();
            var warnings = new StringBuilder();

            // Check Min API Level (ERROR - Board is Android 13)
            if (PlayerSettings.Android.minSdkVersion < AndroidSdkVersions.AndroidApiLevel33)
            {
                errors.AppendLine($"- Minimum API Level is {PlayerSettings.Android.minSdkVersion}, but Board requires API 33 (Android 13)");
            }

            // Check Target API Level (WARNING)
            if (PlayerSettings.Android.targetSdkVersion != AndroidSdkVersions.AndroidApiLevelAuto &&
                PlayerSettings.Android.targetSdkVersion < AndroidSdkVersions.AndroidApiLevel33)
            {
                warnings.AppendLine($"- Target API Level is {PlayerSettings.Android.targetSdkVersion}, recommended API 33 (Android 13)");
            }

            // Check ARM64 is enabled (ERROR - Board hardware is ARM64)
            if ((PlayerSettings.Android.targetArchitectures & AndroidArchitecture.ARM64) == 0)
            {
                errors.AppendLine("- ARM64 is not enabled in Target Architectures. Board hardware requires ARM64.");
            }

            // Check Scripting Backend (WARNING)
            if (PlayerSettings.GetScriptingBackend(BuildTargetGroup.Android) != ScriptingImplementation.IL2CPP)
            {
                warnings.AppendLine("- Scripting Backend is not IL2CPP. IL2CPP is recommended for Board development.");
            }

            // Check Input System (WARNING)
            if (!BoardProjectSetupWizard.IsInputSystemEnabled())
            {
                warnings.AppendLine("- New Input System is not enabled. Board SDK requires the Input System package.");
            }

#if UNITY_6000_0_OR_NEWER
            // Check Application Entry Point (ERROR for Unity 6)
            if (PlayerSettings.Android.applicationEntry != AndroidApplicationEntry.Activity)
            {
                errors.AppendLine("- Application Entry Point is not set to Activity. Board requires Activity (not GameActivity) on Unity 6.");
            }
#endif

            // Check Screen Orientation (ERROR - Board requires Landscape Left)
            if (!BoardProjectSetupWizard.IsLandscapeOrientationCorrect())
            {
                errors.AppendLine("- Default Screen Orientation must be set to Landscape Left. Board hardware requires this specific orientation.");
            }

            // Log warnings
            if (warnings.Length > 0)
            {
                Debug.LogWarning(
                    "[Board SDK] Project settings warnings:\n" +
                    warnings.ToString() +
                    "\nRun Board > Configure Unity Project... to fix these issues.");
            }

            // Throw error if any critical issues
            if (errors.Length > 0)
            {
                throw new BuildFailedException(
                    "[Board SDK] Project settings are not configured correctly for Board:\n\n" +
                    errors.ToString() +
                    "\nRun Board > Configure Unity Project... to fix these issues.");
            }
        }
    }
}
#endif // UNITY_ANDROID
