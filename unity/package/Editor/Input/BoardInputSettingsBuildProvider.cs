// <copyright file="BoardInputSettingsBuildProvider.cs" company="Harris Hill Products Inc.">
//     Copyright (c) Harris Hill Products Inc. All rights reserved.
// </copyright>

#if UNITY_ANDROID
namespace Board.Editor.Input
{
    using System;
    using System.IO;
    using System.Linq;

    using Board.Input;

    using UnityEngine;
    using UnityEditor;
    using UnityEditor.Build;
    using UnityEditor.Build.Reporting;

    /// <summary>
    /// Provides a mechanism to configure and validate <see cref="BoardInputSettings"/> on build.
    /// </summary>
    internal class BoardInputSettingsBuildProvider : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        /// <summary>
        /// Gets the relative callback order of the build provider.
        /// </summary>
        public int callbackOrder => 1;

        /// <summary>
        /// Callback invoked by Unity before a Player build is started.
        /// </summary>
        /// <param name="report">A <see cref="BuildReport"/> containing information about the build, such as its target platform and output path.</param>
        public void OnPreprocessBuild(BuildReport report)
        {
            if (BoardInput.settings == null)
            {
                throw new BuildFailedException(
                    "[Board SDK] BoardInputSettings asset not found.\n\n" +
                    "This is unexpected as BoardInputSettings is auto-created on SDK import.\n" +
                    "Try reimporting the Board SDK package.");
            }

            if (!EditorUtility.IsPersistent(BoardInput.settings))
            {
                throw new BuildFailedException(
                    "[Board SDK] No BoardInputSettings asset is active.\n\n" +
                    "Go to Project Settings > Board > Input Settings and select a settings asset.");
            }

            var pieceSetModelFilename = BoardInput.settings.pieceSetModelFilename;
            if (string.IsNullOrEmpty(pieceSetModelFilename))
            {
                throw new BuildFailedException(
                    "[Board SDK] No Piece Set Model configured.\n\n" +
                    "Go to Project Settings > Board > Input Settings\n" +
                    "and use the Piece Set Model section to select and download a model.");
            }

            var directory = Path.Combine(Application.dataPath, "StreamingAssets");
            var file = string.Empty;
            try
            {
                file = Directory.GetFiles(directory, pieceSetModelFilename, SearchOption.AllDirectories)
                    .FirstOrDefault();
            }
            catch (Exception)
            {
                throw new BuildFailedException(
                    $"[Board SDK] Piece Set Model file \"{pieceSetModelFilename}\" not found.\n\n" +
                    "Go to Project Settings > Board > Input Settings\n" +
                    "and use the Piece Set Model section to download the model.");
            }

            if (string.IsNullOrEmpty(file))
            {
                throw new BuildFailedException(
                    $"[Board SDK] Piece Set Model file \"{pieceSetModelFilename}\" not found in StreamingAssets.\n\n" +
                    "Go to Project Settings > Board > Input Settings\n" +
                    "and use the Piece Set Model section to download the model.");
            }

            // Log the active settings for developer visibility
            var settingsPath = AssetDatabase.GetAssetPath(BoardInput.settings);
            Debug.Log($"[Board SDK]\n" +
                      $"            Active BoardInputSettings: {settingsPath}\n" +
                      $"            Piece Set Model: {pieceSetModelFilename}");

            var preloadedAssets = PlayerSettings.GetPreloadedAssets();
            if (!preloadedAssets.Contains(BoardInput.settings))
            {
                var preloadedAssetsList = preloadedAssets.ToList();
                preloadedAssetsList.Add(BoardInput.settings);
                PlayerSettings.SetPreloadedAssets(preloadedAssetsList.ToArray());
            }
        }

        /// <summary>
        /// Callback invoked by Unity after a Player build is completed.
        /// </summary>
        /// <param name="report">A <see cref="BuildReport"/> containing information about the build, such as its target platform and output path.</param>
        public void OnPostprocessBuild(BuildReport report)
        {
            var preloadedAssets = PlayerSettings.GetPreloadedAssets();

            if (preloadedAssets == null || preloadedAssets.Length == 0)
            {
                return;
            }

            var preloadedAssetsList = preloadedAssets.ToList();
            for (var i = 0; i < preloadedAssetsList.Count; i++)
            {
                if (preloadedAssets[i] is BoardInputSettings)
                {
                    preloadedAssetsList.RemoveAt(i);
                    i--;
                }
            }

            PlayerSettings.SetPreloadedAssets(preloadedAssetsList.ToArray());
        }
    }
}
#endif //UNITY_ANDROID