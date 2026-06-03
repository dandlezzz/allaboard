// <copyright file="BoardModelDownloader.cs" company="Harris Hill Products Inc.">
//     Copyright (c) Harris Hill Products Inc. All rights reserved.
// </copyright>

namespace Board.Editor.Core
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;

    using Board.Input;

    using UnityEditor;

    using UnityEngine;
    using UnityEngine.Networking;

    /// <summary>
    /// Represents the manifest of available Piece Set Models.
    /// </summary>
    [Serializable]
    public class BoardModelManifest
    {
        /// <summary>
        /// Gets or sets the manifest version.
        /// </summary>
        public int version;

        /// <summary>
        /// Gets or sets the list of available models.
        /// </summary>
        public List<BoardModelInfo> models;
    }

    /// <summary>
    /// Represents information about a single Piece Set Model.
    /// </summary>
    [Serializable]
    public class BoardModelInfo
    {
        /// <summary>
        /// Gets or sets the unique identifier for the model.
        /// </summary>
        public string id;

        /// <summary>
        /// Gets or sets the display name of the model.
        /// </summary>
        public string name;

        /// <summary>
        /// Gets or sets the version of the model.
        /// </summary>
        public string modelVersion;

        /// <summary>
        /// Gets or sets the filename for the model.
        /// </summary>
        public string filename;

        /// <summary>
        /// Gets or sets the size of the model file in bytes.
        /// </summary>
        public long size;

        /// <summary>
        /// Gets or sets the download URL for the model.
        /// </summary>
        public string url;

        /// <summary>
        /// Gets the display name with version.
        /// </summary>
        public string displayName => string.IsNullOrEmpty(modelVersion) ? name : $"{name} v{modelVersion}";

        /// <summary>
        /// Gets a human-readable size string.
        /// </summary>
        public string sizeFormatted
        {
            get
            {
                if (size < 1024)
                    return $"{size} B";
                if (size < 1024 * 1024)
                    return $"{size / 1024.0:F1} KB";
                return $"{size / (1024.0 * 1024.0):F1} MB";
            }
        }
    }

    /// <summary>
    /// Provides utilities for downloading Piece Set Models from the Board developer portal.
    /// </summary>
    public static class BoardModelDownloader
    {
        private const string kManifestUrl = "https://dev.board.fun/downloads/models/manifest.json";
        private const string kDeveloperPortalUrl = "https://dev.board.fun";
        private const string kStreamingAssetsPath = "Assets/StreamingAssets";

        /// <summary>
        /// Gets the URL of the developer portal.
        /// </summary>
        public static string developerPortalUrl => kDeveloperPortalUrl;

        /// <summary>
        /// Fetches the model manifest from the server.
        /// </summary>
        /// <returns>The model manifest, or null if the fetch failed.</returns>
        public static async Task<BoardModelManifest> FetchManifestAsync()
        {
            using (var request = UnityWebRequest.Get(kManifestUrl))
            {
                var operation = request.SendWebRequest();

                while (!operation.isDone)
                {
                    await Task.Delay(100);
                }

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"[Board SDK] Failed to fetch model manifest: {request.error}");
                    return null;
                }

                try
                {
                    var manifest = JsonUtility.FromJson<BoardModelManifest>(request.downloadHandler.text);
                    return manifest;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[Board SDK] Failed to parse model manifest: {ex.Message}");
                    return null;
                }
            }
        }

        /// <summary>
        /// Downloads a model to the StreamingAssets folder.
        /// </summary>
        /// <param name="model">The model information.</param>
        /// <param name="progress">Optional progress reporter (0.0 to 1.0).</param>
        /// <returns>True if the download was successful, false otherwise.</returns>
        public static async Task<bool> DownloadModelAsync(BoardModelInfo model, IProgress<float> progress = null)
        {
            // Ensure StreamingAssets folder exists
            if (!AssetDatabase.IsValidFolder(kStreamingAssetsPath))
            {
                AssetDatabase.CreateFolder("Assets", "StreamingAssets");
            }

            var destinationPath = Path.Combine(kStreamingAssetsPath, model.filename);

            using (var request = UnityWebRequest.Get(model.url))
            {
                var operation = request.SendWebRequest();

                while (!operation.isDone)
                {
                    progress?.Report(operation.progress);
                    await Task.Delay(100);
                }

                progress?.Report(1.0f);

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"[Board SDK] Failed to download model '{model.name}': {request.error}");
                    return false;
                }

                try
                {
                    File.WriteAllBytes(destinationPath, request.downloadHandler.data);
                    AssetDatabase.Refresh();
                    Debug.Log($"[Board SDK] Downloaded Piece Set Model '{model.name}' to {destinationPath}");
                    return true;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[Board SDK] Failed to save model '{model.name}': {ex.Message}");
                    return false;
                }
            }
        }

        /// <summary>
        /// Configures the active BoardInputSettings with the specified model filename.
        /// </summary>
        /// <param name="filename">The model filename.</param>
        public static void ConfigureActiveSettings(string filename)
        {
            var settings = BoardInput.settings;
            if (settings == null)
            {
                Debug.LogWarning("[Board SDK] No BoardInputSettings found to configure.");
                return;
            }

            settings.pieceSetModelFilename = filename;
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Board SDK] Configured BoardInputSettings with model: {filename}");
        }

        /// <summary>
        /// Gets the currently configured model filename from BoardInputSettings.
        /// </summary>
        /// <returns>The configured model filename, or null if not set.</returns>
        public static string GetConfiguredModelFilename()
        {
            var settings = BoardInput.settings;
            return settings?.pieceSetModelFilename;
        }

        /// <summary>
        /// Checks if a model file exists in StreamingAssets.
        /// </summary>
        /// <param name="filename">The filename to check.</param>
        /// <returns>True if the file exists, false otherwise.</returns>
        public static bool ModelFileExists(string filename)
        {
            if (string.IsNullOrEmpty(filename))
                return false;

            var path = Path.Combine(kStreamingAssetsPath, filename);
            return File.Exists(path);
        }
    }
}
