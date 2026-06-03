// <copyright file="BoardAvatarManager.cs" company="Harris Hill Products Inc.">
//     Copyright (c) Harris Hill Products Inc. All rights reserved.
// </copyright>

namespace Board.Core
{
    using System;
    using System.Collections.Generic;
#if UNITY_ANDROID && !UNITY_EDITOR
    using System.Runtime.InteropServices;
#endif
    using System.Threading.Tasks;
    
    using UnityEngine;

    /// <summary>
    /// Manages avatar loading and caching for Board players.
    /// </summary>
    internal static class BoardAvatarManager
    {
        private const string kLogTag = nameof(BoardAvatarManager);

        // Static cache for loaded avatars
        private static readonly Dictionary<int, Texture2D> s_AvatarCache = new Dictionary<int, Texture2D>();

        // Dictionary of pending avatar load requests (avatarId -> list of TaskCompletionSources)
        private static readonly Dictionary<int, List<TaskCompletionSource<Texture2D>>> s_PendingLoads = new Dictionary<int, List<TaskCompletionSource<Texture2D>>>();
        private static readonly object s_PendingLoadsLock = new object();

        // Main thread work queue
        private static readonly System.Collections.Generic.Queue<System.Action> s_MainThreadQueue = new System.Collections.Generic.Queue<System.Action>();
        private static readonly object s_MainThreadLock = new object();

        #region Public API

        /// <summary>
        /// Process queued main thread work. Must be called from Unity main thread.
        /// </summary>
        internal static void ProcessMainThreadQueue()
        {
            lock (s_MainThreadLock)
            {
                while (s_MainThreadQueue.Count > 0)
                {
                    var action = s_MainThreadQueue.Dequeue();
                    try
                    {
                        action?.Invoke();
                    }
                    catch (Exception e)
                    {
                        BoardLogger.LogError(kLogTag, $"Exception processing main thread work: {e.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// Loads an avatar PNG image and converts it to a Texture2D.
        /// Results are cached for subsequent requests.
        /// Multiple concurrent requests for the same avatar will be deduplicated.
        /// </summary>
        /// <param name="avatarId">Avatar identifier (0-8).</param>
        /// <returns>Texture2D of the avatar, or null if loading fails.</returns>
        public static Task<Texture2D> LoadAvatarAsync(int avatarId)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            // Check cache first
            if (s_AvatarCache.TryGetValue(avatarId, out Texture2D cachedAvatar))
            {
                return Task.FromResult(cachedAvatar);
            }

            lock (s_PendingLoadsLock)
            {
                // Create a new TCS for this request
                var tcs = new TaskCompletionSource<Texture2D>();

                // Check if we already have a pending load for this avatar
                if (s_PendingLoads.TryGetValue(avatarId, out List<TaskCompletionSource<Texture2D>> pendingList))
                {
                    // Add this TCS to the list of pending requests for this avatar
                    pendingList.Add(tcs);
                    return tcs.Task;
                }

                // First request for this avatar - create the pending list
                var newList = new List<TaskCompletionSource<Texture2D>> { tcs };
                s_PendingLoads[avatarId] = newList;

                try
                {
                    // Call native plugin to load the avatar
                    Board_Avatar_LoadPNG(avatarId, OnAvatarLoaded);
                }
                catch (Exception e)
                {
                    BoardLogger.LogError(kLogTag, $"Failed to initiate avatar load for {avatarId}: {e.Message}");
                    s_PendingLoads.Remove(avatarId);
                    tcs.TrySetException(e);
                }

                return tcs.Task;
            }
#else
            return Task.FromResult<Texture2D>(null);
#endif
        }

        /// <summary>
        /// Gets the default avatar (avatar ID 0).
        /// </summary>
        /// <returns>Texture2D of the default avatar, or null if loading fails.</returns>
        public static Task<Texture2D> GetDefaultAvatar()
        {
            return LoadAvatarAsync(0);
        }

        /// <summary>
        /// Clears the avatar cache.
        /// </summary>
        public static void ClearCache()
        {
            foreach (var kvp in s_AvatarCache)
            {
                if (kvp.Value != null)
                {
                    UnityEngine.Object.Destroy(kvp.Value);
                }
            }

            s_AvatarCache.Clear();
        }

        #endregion

        #region Native Callbacks

#if UNITY_ANDROID && !UNITY_EDITOR
        [AOT.MonoPInvokeCallback(typeof(AvatarLoadedCallback))]
        private static void OnAvatarLoaded(int avatarId, IntPtr avatarDataPtr, int dataSize, string error)
        {
            List<TaskCompletionSource<Texture2D>> pendingRequests = null;

            try
            {
                // Get all pending requests for this avatar
                lock (s_PendingLoadsLock)
                {
                    if (!s_PendingLoads.TryGetValue(avatarId, out pendingRequests))
                    {
                        BoardLogger.LogError(kLogTag, $"Avatar loaded callback received for avatar {avatarId} but no pending requests found");
                        if (avatarDataPtr != IntPtr.Zero)
                        {
                            Board_Avatar_FreeData(avatarDataPtr);
                        }
                        return;
                    }

                    // Remove from pending loads immediately
                    s_PendingLoads.Remove(avatarId);
                }

                // Handle error case
                if (!string.IsNullOrEmpty(error))
                {
                    BoardLogger.LogError(kLogTag, $"Avatar load error for avatar {avatarId}: {error}");
                    foreach (var tcs in pendingRequests)
                    {
                        tcs?.TrySetResult(null);
                    }
                    return;
                }

                // Handle null/empty data case
                if (avatarDataPtr == IntPtr.Zero || dataSize <= 0)
                {
                    BoardLogger.LogError(kLogTag, $"Avatar load returned null/empty data for avatar {avatarId}");
                    foreach (var tcs in pendingRequests)
                    {
                        tcs?.TrySetResult(null);
                    }
                    return;
                }

                // Copy PNG data
                byte[] pngData = new byte[dataSize];
                Marshal.Copy(avatarDataPtr, pngData, 0, dataSize);

                // Free native data
                Board_Avatar_FreeData(avatarDataPtr);

                // Queue texture creation on main thread
                lock (s_MainThreadLock)
                {
                    s_MainThreadQueue.Enqueue(() => {
                        try
                        {
                            Texture2D texture = new Texture2D(2, 2);
                            if (ImageConversion.LoadImage(texture, pngData))
                            {
                                // Cache the texture
                                s_AvatarCache[avatarId] = texture;

                                // Complete all pending requests
                                foreach (var tcs in pendingRequests)
                                {
                                    tcs?.TrySetResult(texture);
                                }
                            }
                            else
                            {
                                BoardLogger.LogError(kLogTag, $"Failed to load PNG image data for avatar {avatarId}");
                                UnityEngine.Object.Destroy(texture);

                                // Complete all pending requests with null
                                foreach (var tcs in pendingRequests)
                                {
                                    tcs?.TrySetResult(null);
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            BoardLogger.LogError(kLogTag, $"Exception creating texture for avatar {avatarId}: {e.Message}");

                            // Complete all pending requests with exception
                            foreach (var tcs in pendingRequests)
                            {
                                tcs?.TrySetException(e);
                            }
                        }
                    });
                }
            }
            catch (Exception e)
            {
                BoardLogger.LogError(kLogTag, $"Exception in avatar loaded callback for avatar {avatarId}: {e.Message}");

                // Complete all pending requests with exception
                if (pendingRequests != null)
                {
                    foreach (var tcs in pendingRequests)
                    {
                        tcs?.TrySetException(e);
                    }
                }
            }
        }
#endif

        #endregion

        #region Native Interop

#if UNITY_ANDROID && !UNITY_EDITOR
        // Callback delegate for avatar loading
        private delegate void AvatarLoadedCallback(int avatarId, IntPtr avatarDataPtr, int dataSize, string error);

        /// <summary>
        /// Load avatar PNG image (P/Invoke to native plugin).
        /// </summary>
        [DllImport(BoardSupport.kNativePluginName)]
        private static extern void Board_Avatar_LoadPNG(int avatarId, AvatarLoadedCallback callback);

        /// <summary>
        /// Free native avatar data (P/Invoke to native plugin).
        /// </summary>
        [DllImport(BoardSupport.kNativePluginName)]
        private static extern void Board_Avatar_FreeData(IntPtr avatarDataPtr);
#endif

        #endregion
    }
}
