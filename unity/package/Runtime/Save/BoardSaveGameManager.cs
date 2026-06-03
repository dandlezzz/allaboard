// <copyright file="BoardSaveGameManager.cs" company="Harris Hill Products Inc.">
//     Copyright (c) Harris Hill Products Inc. All rights reserved.
// </copyright>

namespace Board.Save
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading.Tasks;

    using Board.Session;
    using Board.Core;

    using UnityEngine;

    // Native wrapper struct for save game data
    [StructLayout(LayoutKind.Sequential)]
    internal struct SaveGameDataWrapper
    {
        public IntPtr data;
        public int dataSize;
    }

    /// <summary>
    /// Manages access to Board's saved game system.
    /// </summary>
    public static class BoardSaveGameManager
    {
        #region Constants

        private const string kLogTag = nameof(BoardSaveGameManager);

        // Editor use only
        private const int kEditorMaxSaveDescriptionLength = 100; // 100 characters
        private const int kEditorMaxPayloadSize = 16 * 1024 * 1024; // 16MB limit
        private const int kEditorMaxAppStorageSize = 64 * 1024 * 1024; // 64MB total limit per app

        #endregion

        #region Private Fields

#if UNITY_ANDROID && !UNITY_EDITOR
        // TaskCompletionSource for async storage operations
        private static TaskCompletionSource<long> s_GetAppStorageUsedTcs;
#endif

        #endregion
        
        #region Private Methods

        /// <summary>
        /// Computes a SHA-256 hash of the given data and returns it as a lowercase hexadecimal string.
        /// </summary>
        /// <param name="data">The data to hash.</param>
        /// <returns>The SHA-256 hash as a lowercase hex string, or null if data is null or empty.</returns>
        private static string ComputeSha256Hex(byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                return null;
            }

            using (var sha256 = SHA256.Create())
            {
                var hashBytes = sha256.ComputeHash(data);
                var sb = new StringBuilder(hashBytes.Length * 2);
                foreach (var b in hashBytes)
                {
                    sb.Append(b.ToString("x2"));
                }
                return sb.ToString();
            }
        }

        /// <summary>
        /// Compares two <see cref="BoardSaveGameMetadata"/> objects and returns a value indicating whether one is less than, equal to, or greater than the other.
        /// </summary>
        /// <param name="x">The first <see cref="BoardSaveGameMetadata"/> to compare.</param>
        /// <param name="y">The second <see cref="BoardSaveGameMetadata"/> to compare.</param>
        /// <returns>A signed integer that indicates the relative values of <paramref name="x"/> and <paramref name="x"/>.</returns>
        private static int CompareSaveGameMetadata(BoardSaveGameMetadata x, BoardSaveGameMetadata y)
        {
            if (x == null)
            {
                if (y == null)
                {
                    return 0;
                }

                return -1;
            }

            if (y == null)
            {
                return 1;
            }

            return -1 * x.updatedAt.CompareTo(y.updatedAt);
        }

        #endregion

        #region Public API
        
        /// <summary>
        /// Gets storage information for save games on the Board device including total allocated space,
        /// used space, and remaining space.
        /// </summary>
        /// <returns>Storage information including total, used, and remaining space.</returns>
        /// <exception cref="InvalidOperationException">Storage information cannot be retrieved.</exception>
        public static async Task<BoardAppStorageInfo> GetAppStorageInfo()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (s_GetAppStorageUsedTcs != null)
            {
                throw new InvalidOperationException(
                    "A storage info request is already in progress. Await the current request before starting another.");
            }

            await BoardSupport.WaitForServicesAsync();

            try
            {
                var tcs = new TaskCompletionSource<long>();
                s_GetAppStorageUsedTcs = tcs;

                Board_SaveGame_GetAppStorageUsed(OnAppStorageUsedRetrieved);

                var usedStorage = await tcs.Task.ConfigureAwait(false);
                var totalStorage = GetMaxAppStorage();
                var remainingStorage = totalStorage - usedStorage;
                var usagePercentage = totalStorage > 0 ? (float)usedStorage / totalStorage : 0f;

                return new BoardAppStorageInfo
                {
                    totalStorage = totalStorage,
                    usedStorage = usedStorage,
                    remainingStorage = Math.Max(0, remainingStorage),
                    usagePercentage = usagePercentage
                };
            }
            catch (Exception ex)
            {
                BoardLogger.LogError(kLogTag, $"Failed to get app storage info: {ex.Message}");
                throw new InvalidOperationException($"Unable to retrieve app storage information: {ex.Message}", ex);
            }
            finally
            {
                s_GetAppStorageUsedTcs = null;
            }
#else
            // In editor/non-Android platforms, return mock data
            await Task.Delay(100); // Simulate async operation
            var totalStorage = GetMaxAppStorage();
            return new BoardAppStorageInfo
            {
                totalStorage = totalStorage,
                usedStorage = 0,
                remainingStorage = totalStorage,
                usagePercentage = 0f
            };
#endif
        }

        /// <summary>
        /// Gets the maximum allowed size for individual save game payloads.
        /// </summary>
        /// <returns>Maximum payload size in bytes.</returns>
        public static long GetMaxPayloadSize()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                return Board_SaveGame_GetMaxDataSize();
            }
            catch (Exception ex)
            {
                BoardLogger.LogError(kLogTag, $"Failed to get max payload size: {ex.Message}");
                return 0;
            }
#else
            // In editor/non-Android platforms, return mock value
            return kEditorMaxPayloadSize;
#endif
        }

        /// <summary>
        /// Gets the maximum allowed total storage size for all save games for an app.
        /// </summary>
        /// <returns>Maximum app storage size in bytes.</returns>
        public static long GetMaxAppStorage()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                return Board_SaveGame_GetMaxAppStorage();
            }
            catch (Exception ex)
            {
                BoardLogger.LogError(kLogTag, $"Failed to get max app storage size: {ex.Message}");
                return 0;
            }
#else
            // In editor/non-Android platforms, return mock data
            return kEditorMaxAppStorageSize;
#endif
        }

        /// <summary>
        /// Gets the maximum allowed length for save file descriptions.
        /// </summary>
        /// <returns>Maximum save file name length in characters.</returns>
        public static int GetMaxSaveDescriptionLength()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                return Board_SaveGame_GetMaxDescriptionLength();
            }
            catch (Exception ex)
            {
                BoardLogger.LogError(kLogTag, $"Failed to get max save description length: {ex.Message}");
                return 0;
            }
#else
            // In editor/non-Android platforms, return mock data
            return kEditorMaxSaveDescriptionLength;
#endif
        }

        /// <summary>
        /// Gets the <see cref="BoardSaveGameMetadata">metadata</see> for the save games for the current application on the Board device.
        /// </summary>
        /// <returns>The task object representing the asynchronous operation.</returns>
        public static async Task<BoardSaveGameMetadata[]> GetSaveGamesMetadata()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (s_GetSaveGamesTcs != null)
            {
                throw new InvalidOperationException(
                    "A save games metadata request is already in progress. Await the current request before starting another.");
            }

            await BoardSupport.WaitForServicesAsync();

            try
            {
                var tcs = new TaskCompletionSource<BoardSaveGameMetadata[]>();
                s_GetSaveGamesTcs = tcs;

                Board_SaveGame_GetList(OnSaveGamesRetrieved);

                var result = await tcs.Task.ConfigureAwait(false);
                return result;
            }
            catch (Exception ex)
            {
                BoardLogger.LogError(kLogTag, $"Failed to get save games: {ex.Message}");
                throw new InvalidOperationException($"Failed to retrieve save games: {ex.Message}", ex);
            }
            finally
            {
                s_GetSaveGamesTcs = null;
            }

#else
            // In editor/non-Android platforms, return mock data
            await Task.Delay(100); // Simulate async operation
            return Array.Empty<BoardSaveGameMetadata>();
#endif
        }

        /// <summary>
        /// Creates a saved game with the specified payload and metadata, returning the <see cref="BoardSaveGameMetadata">metadata</see>
        /// for the saved game.
        /// </summary>
        /// <remarks>
        /// The save game is automatically associated with <see cref="BoardSession.players"/>.
        /// </remarks>
        /// <param name="payload">The save game payload (serialized game state).</param>
        /// <param name="metadataChange">The <see cref="BoardSaveGameMetadataChange">metadata</see> for the save game.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="payload"/> or <paramref name="metadataChange"/> is <c>null</c>.</exception>
        public static async Task<BoardSaveGameMetadata> CreateSaveGame(byte[] payload,
            BoardSaveGameMetadataChange metadataChange)
        {
            if (payload == null)
            {
                throw new ArgumentNullException(nameof(payload));
            }

            if (metadataChange == null)
            {
                throw new ArgumentNullException(nameof(metadataChange));
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            if (s_CreateSaveGameTcs != null)
            {
                throw new InvalidOperationException(
                    "A save game creation is already in progress. Await the current operation before starting another.");
            }

            await BoardSupport.WaitForServicesAsync();

            try
            {
                // Convert cover image to PNG
                var coverImagePNG = ImageProcessor.ConvertToStandardizedPNG(metadataChange.coverImage);

                // Compute checksums for end-to-end verification
                var blobChecksum = ComputeSha256Hex(payload);
                var coverChecksum = ComputeSha256Hex(coverImagePNG);

                var tcs = new TaskCompletionSource<BoardSaveGameMetadata>();
                s_CreateSaveGameTcs = tcs;

                Board_SaveGame_Create(payload, payload.Length, metadataChange.description,
                    coverImagePNG, coverImagePNG?.Length ?? 0, (long)metadataChange.playedTime,
                    metadataChange.gameVersion, blobChecksum, coverChecksum, OnSaveGameCreated);

                var result = await tcs.Task.ConfigureAwait(false);
                return result;
            }
            catch (Exception ex)
            {
                BoardLogger.LogError(kLogTag,
                    $"Failed to create save game '{metadataChange?.description}': {ex.Message}");
                throw new InvalidOperationException($"Failed to create save game: {ex.Message}", ex);
            }
            finally
            {
                s_CreateSaveGameTcs = null;
            }

#else
            // In editor/non-Android platforms, create a mock metadata object
            await Task.Delay(100); // Simulate async operation

            // Convert cover image for consistency
            var coverImagePNG = ImageProcessor.ConvertToStandardizedPNG(metadataChange.coverImage);

            // For testing purposes, create a basic metadata object
            // In real implementation, this would be populated by the native layer
            var now = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            return new BoardSaveGameMetadata
            {
                id = Guid.NewGuid().ToString(),
                description = metadataChange.description,
                gameVersion = metadataChange.gameVersion,
                playedTime = metadataChange.playedTime,
                createdAt = now,
                updatedAt = now,
                hasCoverImage = coverImagePNG != null && coverImagePNG.Length > 0
            };
#endif
        }

        /// <summary>
        /// Updates an existing saved game with new payload and metadata.
        /// </summary>
        /// <param name="saveId">The unique identifier for the save game to update.</param>
        /// <param name="payload">The new save game payload (serialized game state).</param>
        /// <param name="metadataChange">The new metadata for the save game (required).</param>
        /// <returns>The task object representing the asynchronous operation, returning the updated metadata.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="saveId"/>, <paramref name="payload"/>, or <paramref name="metadataChange"/> is <c>null</c>.</exception>
        public static async Task<BoardSaveGameMetadata> UpdateSaveGame(string saveId, byte[] payload,
            BoardSaveGameMetadataChange metadataChange)
        {
            if (string.IsNullOrEmpty(saveId))
            {
                throw new ArgumentNullException(nameof(saveId), "Save ID cannot be null or empty.");
            }

            if (payload == null)
            {
                throw new ArgumentNullException(nameof(payload));
            }

            if (metadataChange == null)
            {
                throw new ArgumentNullException(nameof(metadataChange));
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            if (s_UpdateSaveGameTcs != null)
            {
                throw new InvalidOperationException(
                    "A save game update is already in progress. Await the current operation before starting another.");
            }

            await BoardSupport.WaitForServicesAsync();

            try
            {
                // Convert cover image to PNG
                var coverImagePNG = ImageProcessor.ConvertToStandardizedPNG(metadataChange.coverImage);

                // Compute checksums for end-to-end verification
                var blobChecksum = ComputeSha256Hex(payload);
                var coverChecksum = ComputeSha256Hex(coverImagePNG);

                var tcs = new TaskCompletionSource<BoardSaveGameMetadata>();
                s_UpdateSaveGameTcs = tcs;

                Board_SaveGame_Update(saveId, payload, payload.Length, metadataChange.description,
                    coverImagePNG, coverImagePNG?.Length ?? 0, (long)metadataChange.playedTime,
                    metadataChange.gameVersion, blobChecksum, coverChecksum, OnSaveGameUpdated);

                var result = await tcs.Task.ConfigureAwait(false);
                return result;
            }
            catch (Exception ex)
            {
                BoardLogger.LogError(kLogTag, $"Failed to update save game '{saveId}': {ex.Message}");
                throw new InvalidOperationException($"Failed to update save game: {ex.Message}", ex);
            }
            finally
            {
                s_UpdateSaveGameTcs = null;
            }

#else
            // In editor/non-Android platforms, simulate successful update
            await Task.Delay(100);

            // Convert cover image for consistency
            var coverImagePNG = ImageProcessor.ConvertToStandardizedPNG(metadataChange.coverImage);

            // For testing purposes, create a basic metadata object
            // In real implementation, this would be populated by the native layer
            return new BoardSaveGameMetadata
            {
                id = saveId,
                description = metadataChange.description,
                gameVersion = metadataChange.gameVersion,
                playedTime = metadataChange.playedTime,
                updatedAt = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                hasCoverImage = coverImagePNG != null && coverImagePNG.Length > 0
            };
#endif
        }

        /// <summary>
        /// Loads and returns a saved game's payload.
        /// </summary>
        /// <remarks>
        /// Loading a save game automatically activates the players associated with the save game,
        /// replacing missing profiles with guest players. Therefore, expect to receive an
        /// event for the <see cref="BoardSession.players"/> to be updated when the save game is loaded.
        /// </remarks>
        /// <param name="saveId">The unique identifier for the saved game to load.</param>
        /// <returns>The task object representing the asynchronous operation, returning the payload bytes.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="saveId"/> is <c>null</c> or empty.</exception>
        /// <exception cref="InvalidOperationException">The load save game operation fails.</exception>
        public static async Task<byte[]> LoadSaveGame(string saveId)
        {
            if (string.IsNullOrEmpty(saveId))
            {
                throw new ArgumentNullException(nameof(saveId), "Save ID cannot be null or empty.");
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            if (s_LoadSaveGameTcs != null)
            {
                throw new InvalidOperationException(
                    "A save game load is already in progress. Await the current load before starting another.");
            }

            await BoardSupport.WaitForServicesAsync();

            try
            {
                var tcs = new TaskCompletionSource<byte[]>();
                s_LoadSaveGameTcs = tcs;

                Board_SaveGame_Load(saveId, OnSaveGameLoaded);

                var result = await tcs.Task.ConfigureAwait(false);

                return result;
            }
            catch (Exception ex)
            {
                BoardLogger.LogError(kLogTag, $"Failed to load save game: {ex.Message}");
                throw new InvalidOperationException($"Failed to load save game: {ex.Message}", ex);
            }
            finally
            {
                s_LoadSaveGameTcs = null;
            }

#else
            // In editor/non-Android platforms, simulate load operation
            await Task.Delay(100);

            // Player activation is handled automatically in Android builds
            // In editor, players remain as-is for testing
            return new byte[] { 0x01, 0x02, 0x03, 0x04 }; // Mock payload
#endif
        }

        /// <summary>
        /// Loads the cover image for a saved game.
        /// </summary>
        /// <param name="saveId">The unique identifier for the saved game.</param>
        /// <returns>The task object representing the asynchronous operation, returning the cover image as a <see cref="Texture2D"/> or <c>null</c> if no cover image exists.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="saveId"/> is <c>null</c> or empty.</exception>
        /// <exception cref="InvalidOperationException">The load image operation fails.</exception>
        public static async Task<Texture2D> LoadSaveGameCoverImage(string saveId)
        {
            if (string.IsNullOrEmpty(saveId))
            {
                throw new ArgumentNullException(nameof(saveId), "Save ID cannot be null or empty.");
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            if (s_LoadCoverImageTcs != null)
            {
                throw new InvalidOperationException(
                    "A cover image load is already in progress. Await the current load before starting another.");
            }

            await BoardSupport.WaitForServicesAsync();

            try
            {
                var tcs = new TaskCompletionSource<byte[]>();
                s_LoadCoverImageTcs = tcs;

                Board_SaveGame_LoadCoverImage(saveId, OnSaveGameCoverImageLoaded);

                var coverImageBytes = await tcs.Task.ConfigureAwait(false);

                if (coverImageBytes == null || coverImageBytes.Length == 0)
                {
                    return null;
                }

                return ImageProcessor.LoadTextureFromPNG(coverImageBytes);
            }
            catch (Exception ex)
            {
                BoardLogger.LogError(kLogTag, $"Failed to load save game cover image: {ex.Message}");
                throw new InvalidOperationException($"Failed to load save game cover image: {ex.Message}", ex);
            }
            finally
            {
                s_LoadCoverImageTcs = null;
            }

#else
            // In editor/non-Android platforms, simulate load operation
            await Task.Delay(100);

            // Return a test texture in editor mode
            return ImageProcessor.CreateTestCoverImage(Color.blue, "Test Cover");
#endif
        }

        /// <summary>
        /// Removes <see cref="BoardSession.players"/> from the specified saved game and returns a value 
        /// asynchronously indicating whether the operation was successful.
        /// 
        /// If the saved game is not associated with any active players, the saved game is deleted.
        /// </summary>
        /// <param name="saveId">The unique identifier for the saved game to remove players from.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="saveId"/> is <c>null</c> or empty.</exception>
        public static async Task<bool> RemovePlayersFromSaveGame(string saveId)
        {
            if (string.IsNullOrEmpty(saveId))
            {
                throw new ArgumentNullException(nameof(saveId), "Save ID cannot be null or empty.");
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            if (s_RemovePlayersTcs != null)
            {
                throw new InvalidOperationException(
                    "A remove players operation is already in progress. Await the current operation before starting another.");
            }

            await BoardSupport.WaitForServicesAsync();

            try
            {
                var tcs = new TaskCompletionSource<bool>();
                s_RemovePlayersTcs = tcs;

                // Call native layer to remove players from save game using static callback (IL2CPP compatible)
                Board_SaveGame_RemovePlayers(saveId, OnSaveGameRemovePlayers);

                var result = await tcs.Task.ConfigureAwait(false);
                return result;
            }
            catch (Exception ex)
            {
                BoardLogger.LogError(kLogTag, $"Failed to remove players from save game: {ex.Message}");
                throw new InvalidOperationException($"Failed to remove players from save game: {ex.Message}", ex);
            }
            finally
            {
                s_RemovePlayersTcs = null;
            }

#else
            // In editor/non-Android platforms, simulate successful player removal
            await Task.Delay(100);
            return true;
#endif
        }


        /// <summary>
        /// Removes only the active profile from the specified saved game. If the saved game is not associated with any
        /// profiles after removal, the saved game is deleted.
        /// </summary>
        /// <param name="saveId">The unique identifier for a saved game.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="saveId"/> is <c>null</c> or empty.</exception>
        public static async Task<bool> RemoveActiveProfileFromSaveGame(string saveId)
        {
            if (string.IsNullOrEmpty(saveId))
            {
                throw new ArgumentNullException(nameof(saveId), "Save ID cannot be null or empty.");
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            if (s_RemoveActiveProfileTcs != null)
            {
                throw new InvalidOperationException(
                    "A remove active profile operation is already in progress. Await the current operation before starting another.");
            }

            await BoardSupport.WaitForServicesAsync();

            try
            {
                var tcs = new TaskCompletionSource<bool>();
                s_RemoveActiveProfileTcs = tcs;

                // Call native layer to remove active profile from save game using static callback (IL2CPP compatible)
                Board_SaveGame_RemoveActiveProfile(saveId, OnSaveGameRemoveActiveProfile);

                var result = await tcs.Task.ConfigureAwait(false);
                return result;
            }
            catch (Exception ex)
            {
                BoardLogger.LogError(kLogTag, $"Failed to remove active profile from save game: {ex.Message}");
                throw new InvalidOperationException($"Failed to remove active profile from save game: {ex.Message}", ex);
            }
            finally
            {
                s_RemoveActiveProfileTcs = null;
            }
#else
            // In editor/non-Android platforms, simulate successful active profile removal
            await Task.Delay(100);
            return true;
#endif
        }
        #endregion
        
        #region Native Method Declarations

#if UNITY_ANDROID && !UNITY_EDITOR
        /// <summary>
        /// Gets the list of save games from the native layer.
        /// </summary>
        /// <param name="callback">Callback to receive the result.</param>
        [DllImport(BoardSupport.kNativePluginName)]
        private static extern void Board_SaveGame_GetList(SaveGameListCallback callback);

        /// <summary>
        /// Creates a new save game via the native layer with end-to-end checksum verification.
        /// </summary>
        /// <param name="data">The save game payload.</param>
        /// <param name="dataSize">The size of the payload in bytes.</param>
        /// <param name="description">The description for the save.</param>
        /// <param name="coverImagePNG">The cover image as PNG bytes (432x243).</param>
        /// <param name="coverImageSize">The size of the cover image data.</param>
        /// <param name="playedTime">The played time in seconds.</param>
        /// <param name="gameVersion">The game version string.</param>
        /// <param name="blobChecksum">SHA-256 hex checksum of the save data for transit verification.</param>
        /// <param name="coverChecksum">SHA-256 hex checksum of the cover image for transit verification.</param>
        /// <param name="callback">Callback to receive the result.</param>
        [DllImport(BoardSupport.kNativePluginName)]
        private static extern void Board_SaveGame_Create(byte[] data, int dataSize, string description,
            byte[] coverImagePNG, int coverImageSize, long playedTime, string gameVersion,
            string blobChecksum, string coverChecksum, SaveGameCreatedCallback callback);

        /// <summary>
        /// Updates an existing save game via the native layer with end-to-end checksum verification.
        /// </summary>
        /// <param name="saveId">The save game ID to update.</param>
        /// <param name="data">The save game payload.</param>
        /// <param name="dataSize">The size of the payload.</param>
        /// <param name="description">The updated description.</param>
        /// <param name="coverImagePNG">The updated cover image as PNG bytes (432x243).</param>
        /// <param name="coverImageSize">The size of the cover image data.</param>
        /// <param name="playedTime">The updated played time in seconds.</param>
        /// <param name="gameVersion">The updated game version string.</param>
        /// <param name="blobChecksum">SHA-256 hex checksum of the save data for transit verification.</param>
        /// <param name="coverChecksum">SHA-256 hex checksum of the cover image for transit verification.</param>
        /// <param name="callback">Callback to receive the result.</param>
        [DllImport(BoardSupport.kNativePluginName)]
        private static extern void Board_SaveGame_Update(string saveId, byte[] data, int dataSize, string description,
            byte[] coverImagePNG, int coverImageSize, long playedTime, string gameVersion,
            string blobChecksum, string coverChecksum, SaveGameUpdatedCallback callback);

        /// <summary>
        /// Loads save game payload via the native layer.
        /// </summary>
        /// <param name="saveId">The save game ID.</param>
        /// <param name="callback">Callback to receive the result.</param>
        [DllImport(BoardSupport.kNativePluginName)]
        private static extern void Board_SaveGame_Load(string saveId, SaveGameLoadedCallback callback);

        /// <summary>
        /// Removes players from a save game via the native layer.
        /// </summary>
        /// <param name="saveId">The save game ID.</param>
        /// <param name="callback">Callback to receive the result.</param>
        [DllImport(BoardSupport.kNativePluginName)]
        private static extern void Board_SaveGame_RemovePlayers(string saveId, SaveGameRemovePlayersCallback callback);

        /// <summary>
        /// Removes the active profile from a save game via the native layer.
        /// </summary>
        /// <param name="saveId">The save game ID.</param>
        /// <param name="callback">Callback to receive the result.</param>
        [DllImport(BoardSupport.kNativePluginName)]
        private static extern void Board_SaveGame_RemoveActiveProfile(string saveId, SaveGameRemovePlayersCallback callback);

        /// <summary>
        /// Gets the app's save storage used space from the native layer (async callback version).
        /// </summary>
        /// <param name="callback">Callback to receive the result.</param>
        [DllImport(BoardSupport.kNativePluginName)]
        private static extern void Board_SaveGame_GetAppStorageUsed(AppStorageUsedCallback callback);

        /// <summary>
        /// Gets the maximum allowed size for individual save game payloads from the native layer.
        /// </summary>
        /// <returns>Maximum payload size in bytes.</returns>
        [DllImport(BoardSupport.kNativePluginName)]
        private static extern long Board_SaveGame_GetMaxDataSize();

        /// <summary>
        /// Gets the maximum allowed total size for all save games for an app from the native layer.
        /// </summary>
        /// <returns>Maximum total app storage size in bytes.</returns>
        [DllImport(BoardSupport.kNativePluginName)]
        private static extern long Board_SaveGame_GetMaxAppStorage();

        /// <summary>
        /// Gets the maximum allowed length for save game descriptions from the native layer.
        /// </summary>
        /// <returns>Maximum save description length in characters.</returns>
        [DllImport(BoardSupport.kNativePluginName)]
        private static extern int Board_SaveGame_GetMaxDescriptionLength();

        /// <summary>
        /// Loads the cover image for a save game via the native layer.
        /// </summary>
        /// <param name="saveId">The save game ID.</param>
        /// <param name="callback">Callback to receive the result.</param>
        [DllImport(BoardSupport.kNativePluginName)]
        private static extern void Board_SaveGame_LoadCoverImage(string saveId, SaveGameCoverImageLoadedCallback callback);
#endif

        #endregion

        #region Callback Management

#if UNITY_ANDROID && !UNITY_EDITOR
        // IL2CPP-compatible callback delegate types
        public delegate void SaveGameCreatedCallback(IntPtr metadataPtr, string error);

        public delegate void SaveGameLoadedCallback(IntPtr dataWrapperPtr, string error);

        public delegate void SaveGameRemovePlayersCallback(bool success, string error);

        public delegate void SaveGameUpdatedCallback(IntPtr metadataPtr, string error);

        public delegate void SaveGameListCallback(IntPtr savesPtr, int count, IntPtr playersPtr, int playerCount, string error);

        public delegate void AppStorageUsedCallback(long usedBytes, string error);

        public delegate void SaveGameCoverImageLoadedCallback(IntPtr imageWrapperPtr, string error);

        // Static callback storage
        private static TaskCompletionSource<BoardSaveGameMetadata> s_CreateSaveGameTcs;
        private static TaskCompletionSource<BoardSaveGameMetadata[]> s_GetSaveGamesTcs;
        private static TaskCompletionSource<byte[]> s_LoadSaveGameTcs;
        private static TaskCompletionSource<BoardSaveGameMetadata> s_UpdateSaveGameTcs;
        private static TaskCompletionSource<bool> s_RemovePlayersTcs;
        private static TaskCompletionSource<bool> s_RemoveActiveProfileTcs;
        private static TaskCompletionSource<long> s_GetStorageUsedTcs;
        private static TaskCompletionSource<byte[]> s_LoadCoverImageTcs;

        // Using BoardSaveGameData struct (matches BoardSessionPlayerData pattern)
        // This struct has fixed byte arrays that directly match the C++ struct

        private static BoardSaveGameMetadata MarshalBoardSaveGameData(IntPtr dataPtr)
        {
            try
            {
                if (dataPtr == IntPtr.Zero)
                {
                    BoardLogger.LogError(kLogTag, "Marshal failed: null pointer");
                    return null;
                }

                var native = Marshal.PtrToStructure<BoardSaveGameData>(dataPtr);

                var saveId = native.saveIdString ?? string.Empty;
                var description = native.descriptionString ?? string.Empty;
                var gameVersion = native.gameVersionString ?? string.Empty;

                string[] playerIds = Array.Empty<string>();
                if (!string.IsNullOrEmpty(native.playerIdsString))
                {
                    playerIds = native.playerIdsString.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                }

                var metadata = new BoardSaveGameMetadata
                {
                    id = saveId,
                    description = description,
                    gameVersion = gameVersion,
                    createdAt = (ulong)Math.Max(0, native.createdAt),
                    updatedAt = (ulong)Math.Max(0, native.updatedAt),
                    playedTime = (ulong)Math.Max(0, native.playedTime),
                    playerIds = playerIds,
                    payloadChecksum = native.payloadChecksumString,
                    coverImageChecksum = native.coverImageChecksumString,
                    hasCoverImage = native.coverImageSize > 0
                };

                return metadata;
            }
            catch (Exception ex)
            {
                BoardLogger.LogError(kLogTag, $"Marshal exception: {ex.Message}");
                return null;
            }
        }

        private static BoardSaveGamePlayer MarshalBoardSaveGamePlayerInfoData(IntPtr dataPtr)
        {
            try
            {
                if (dataPtr == IntPtr.Zero)
                {
                    BoardLogger.LogError(kLogTag, "Marshal failed: null player pointer");
                    return null;
                }

                var native = Marshal.PtrToStructure<BoardSaveGamePlayerData>(dataPtr);
                var playerInfo = new BoardSaveGamePlayer(ref native);

                return playerInfo;
            }
            catch (Exception ex)
            {
                BoardLogger.LogError(kLogTag, $"Marshal player exception: {ex.Message}");
                return null;
            }
        }

        [AOT.MonoPInvokeCallback(typeof(SaveGameCreatedCallback))]
        private static void OnSaveGameCreated(IntPtr metadataPtr, string error)
        {
            var tcs = s_CreateSaveGameTcs;
            s_CreateSaveGameTcs = null;

            if (tcs != null)
            {
                try
                {
                    if (!string.IsNullOrEmpty(error))
                    {
                        tcs.SetException(new InvalidOperationException(error));
                    }
                    else if (metadataPtr != IntPtr.Zero)
                    {
                        var metadata = MarshalBoardSaveGameData(metadataPtr);
                        if (metadata != null)
                        {
                            tcs.SetResult(metadata);
                        }
                        else
                        {
                            tcs.SetException(new InvalidOperationException("Failed to marshal metadata"));
                        }
                    }
                    else
                    {
                        tcs.SetException(new InvalidOperationException("No metadata returned"));
                    }
                }
                catch
                {
                    tcs.SetException(new InvalidOperationException("Callback processing failed"));
                }
            }
        }


        [AOT.MonoPInvokeCallback(typeof(SaveGameListCallback))]
        private static void OnSaveGamesRetrieved(IntPtr savesPtr, int count, IntPtr playersPtr, int playerCount, string error)
        {
            var tcs = s_GetSaveGamesTcs;

            if (tcs != null)
            {
                try
                {
                    if (!string.IsNullOrEmpty(error))
                    {
                        BoardLogger.LogError(kLogTag, $"Save games callback error: {error}");
                        tcs.SetResult(Array.Empty<BoardSaveGameMetadata>());
                    }
                    else if (savesPtr != IntPtr.Zero && count > 0)
                    {
                        var saveGames = new List<BoardSaveGameMetadata>();

                        for (var i = 0; i < count; i++)
                        {
                            try
                            {
                                var currentPtr = IntPtr.Add(savesPtr, i * Marshal.SizeOf<BoardSaveGameData>());
                                var metadata = MarshalBoardSaveGameData(currentPtr);
                                if (metadata != null)
                                {
                                    saveGames.Add(metadata);
                                }
                            }
                            catch (Exception ex)
                            {
                                BoardLogger.LogError(kLogTag, $"Exception marshaling save game {i}: {ex.Message}");
                            }
                        }

                        var playerInfos = new List<BoardSaveGamePlayer>();
                        if (playersPtr != IntPtr.Zero && playerCount > 0)
                        {
                            for (var i = 0; i < playerCount; i++)
                            {
                                try
                                {
                                    var currentPtr = IntPtr.Add(playersPtr, i * Marshal.SizeOf<BoardSaveGamePlayerData>());
                                    var playerInfo = MarshalBoardSaveGamePlayerInfoData(currentPtr);
                                    if (playerInfo != null)
                                    {
                                        playerInfos.Add(playerInfo);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    BoardLogger.LogError(kLogTag, $"Exception marshaling player info {i}: {ex.Message}");
                                }
                            }
                        }

                        foreach (var saveGame in saveGames)
                        {
                            saveGame.players = playerInfos
                                .Where(playerInfo => saveGame.playerIds.Contains(playerInfo.playerId)).ToArray();
                        }

                        saveGames.Sort(CompareSaveGameMetadata);
                        tcs.SetResult(saveGames.ToArray());
                    }
                    else
                    {
                        tcs.SetResult(Array.Empty<BoardSaveGameMetadata>());
                    }
                }
                catch (Exception ex)
                {
                    BoardLogger.LogError(kLogTag, $"Exception in OnSaveGamesRetrieved: {ex.Message}");
                    tcs.SetResult(Array.Empty<BoardSaveGameMetadata>());
                }
            }
            else
            {
                BoardLogger.LogError(kLogTag, "TaskCompletionSource is null in OnSaveGamesRetrieved");
            }
        }

        [AOT.MonoPInvokeCallback(typeof(SaveGameLoadedCallback))]
        private static void OnSaveGameLoaded(IntPtr dataWrapperPtr, string error)
        {
            var tcs = s_LoadSaveGameTcs;
            s_LoadSaveGameTcs = null;

            if (tcs != null)
            {
                try
                {
                    if (!string.IsNullOrEmpty(error))
                    {
                        tcs.SetException(new InvalidOperationException(error));
                    }
                    else if (dataWrapperPtr != IntPtr.Zero)
                    {
                        var wrapper = Marshal.PtrToStructure<SaveGameDataWrapper>(dataWrapperPtr);

                        if (wrapper.data != IntPtr.Zero && wrapper.dataSize > 0)
                        {
                            var data = new byte[wrapper.dataSize];
                            Marshal.Copy(wrapper.data, data, 0, wrapper.dataSize);
                            tcs.SetResult(data);
                        }
                        else
                        {
                            tcs.SetException(new InvalidOperationException("Invalid data in wrapper"));
                        }
                    }
                    else
                    {
                        tcs.SetException(new InvalidOperationException("No data returned"));
                    }
                }
                catch (Exception ex)
                {
                    BoardLogger.LogError(kLogTag, $"Exception in callback: {ex.Message}");
                    tcs.SetException(new InvalidOperationException($"Callback processing failed: {ex.Message}"));
                }
            }
        }

        [AOT.MonoPInvokeCallback(typeof(SaveGameUpdatedCallback))]
        private static void OnSaveGameUpdated(IntPtr metadataPtr, string error)
        {
            var tcs = s_UpdateSaveGameTcs;
            s_UpdateSaveGameTcs = null;

            if (tcs != null)
            {
                try
                {
                    if (!string.IsNullOrEmpty(error))
                    {
                        tcs.SetException(new InvalidOperationException(error));
                    }
                    else if (metadataPtr != IntPtr.Zero)
                    {
                        var metadata = MarshalBoardSaveGameData(metadataPtr);
                        if (metadata != null)
                        {
                            tcs.SetResult(metadata);
                        }
                        else
                        {
                            tcs.SetException(new InvalidOperationException("Failed to marshal updated metadata"));
                        }
                    }
                    else
                    {
                        tcs.SetException(new InvalidOperationException("No updated metadata returned"));
                    }
                }
                catch
                {
                    tcs.SetException(new InvalidOperationException("Callback processing failed"));
                }
            }
        }

        [AOT.MonoPInvokeCallback(typeof(SaveGameRemovePlayersCallback))]
        private static void OnSaveGameRemovePlayers(bool success, string error)
        {
            var tcs = s_RemovePlayersTcs;
            s_RemovePlayersTcs = null;

            if (tcs != null)
            {
                try
                {
                    if (!success || !string.IsNullOrEmpty(error))
                    {
                        tcs.SetException(new InvalidOperationException(error ?? "Operation failed"));
                    }
                    else
                    {
                        tcs.SetResult(success);
                    }
                }
                catch
                {
                    tcs.SetException(new InvalidOperationException("Callback processing failed"));
                }
            }
        }

        [AOT.MonoPInvokeCallback(typeof(SaveGameRemovePlayersCallback))]
        private static void OnSaveGameRemoveActiveProfile(bool success, string error)
        {
            var tcs = s_RemoveActiveProfileTcs;
            s_RemoveActiveProfileTcs = null;

            if (tcs != null)
            {
                try
                {
                    if (!success || !string.IsNullOrEmpty(error))
                    {
                        tcs.SetException(new InvalidOperationException(error ?? "Operation failed"));
                    }
                    else
                    {
                        tcs.SetResult(success);
                    }
                }
                catch
                {
                    tcs.SetException(new InvalidOperationException("Callback processing failed"));
                }
            }
        }

        [AOT.MonoPInvokeCallback(typeof(AppStorageUsedCallback))]
        private static void OnAppStorageUsedRetrieved(long usedBytes, string error)
        {
            var tcs = s_GetAppStorageUsedTcs;
            s_GetAppStorageUsedTcs = null;

            if (tcs != null)
            {
                try
                {
                    if (!string.IsNullOrEmpty(error))
                    {
                        BoardLogger.LogError(kLogTag, $"Storage usage callback error: {error}");
                        tcs.SetResult(0);
                    }
                    else
                    {
                        tcs.SetResult(usedBytes);
                    }
                }
                catch (Exception ex)
                {
                    BoardLogger.LogError(kLogTag, $"Storage usage callback processing failed: {ex.Message}");
                    tcs.SetResult(0);
                }
            }
        }

        [AOT.MonoPInvokeCallback(typeof(SaveGameCoverImageLoadedCallback))]
        private static void OnSaveGameCoverImageLoaded(IntPtr imageWrapperPtr, string error)
        {
            var tcs = s_LoadCoverImageTcs;
            s_LoadCoverImageTcs = null;

            if (tcs != null)
            {
                try
                {
                    if (!string.IsNullOrEmpty(error))
                    {
                        BoardLogger.LogError(kLogTag, $"Cover image load callback error: {error}");
                        tcs.SetException(new InvalidOperationException(error));
                    }
                    else if (imageWrapperPtr != IntPtr.Zero)
                    {
                        var wrapper = Marshal.PtrToStructure<SaveGameDataWrapper>(imageWrapperPtr);

                        if (wrapper.data != IntPtr.Zero && wrapper.dataSize > 0)
                        {
                            var imageData = new byte[wrapper.dataSize];
                            Marshal.Copy(wrapper.data, imageData, 0, wrapper.dataSize);
                            tcs.SetResult(imageData);
                        }
                        else
                        {
                            BoardLogger.LogError(kLogTag, "Invalid cover image data in wrapper");
                            tcs.SetResult(null);
                        }
                    }
                    else
                    {
                        tcs.SetResult(null);
                    }
                }
                catch (Exception ex)
                {
                    BoardLogger.LogError(kLogTag, $"Cover image callback processing failed: {ex.Message}");
                    tcs.SetException(new InvalidOperationException("Callback processing failed", ex));
                }
            }
        }

#endif //UNITY_ANDROID && !UNITY_EDITOR

        #endregion
    }
}
