// <copyright file="BoardSession.cs" company="Harris Hill Products Inc.">
//     Copyright (c) Harris Hill Products Inc. All rights reserved.
// </copyright>

namespace Board.Session
{
    using System;
    
#if UNITY_ANDROID && !UNITY_EDITOR
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.InteropServices;
#endif //UNITY_ANDROID && !UNITY_EDITOR
    using System.Threading.Tasks;
    
    using Board.Core;
    
    using UnityEngine;
    
    /// <summary>
    /// Provides access to Board's app session.
    /// </summary>
    public static class BoardSession
    {
        private const string kLogTag = nameof(BoardSession);

#if UNITY_ANDROID && !UNITY_EDITOR
        // Static storage for TaskCompletionSource instances to work with IL2CPP marshaling
        private static readonly Dictionary<int, TaskCompletionSource<bool>> s_PendingPlayerSelectors = new Dictionary<int, TaskCompletionSource<bool>>();
        private static int s_NextRequestId = 1;
        private static readonly object s_CallbackLock = new object();
        private static bool s_PlayerSelectorInProgress = false;
#endif //UNITY_ANDROID && !UNITY_EDITOR

        private static BoardAIPlayerType[] s_aiPlayerTypes;

        /// <summary>
        /// Gets the array of active <see cref="BoardSessionPlayer">players</see> in the current session
        /// including all <see cref="BoardPlayerType"/> types.
        /// </summary>
        /// <remarks>
        /// At app launch, the initial Profile will be added to <see cref="BoardSession.players"/>.
        /// At least one Player of type <see cref="BoardPlayerType.Profile"/> is required to
        /// be in <see cref="BoardSession.players"/>.
        /// </remarks>
        public static BoardSessionPlayer[] players { get; private set; } = new BoardSessionPlayer[] { };

        /// <summary>
        /// Occurs when the active <see cref="BoardSessionPlayer">players</see> in the session change.
        /// </summary>
#pragma warning disable CS0067 // Event is used in external test assemblies
        public static event Action playersChanged;
#pragma warning restore CS0067

        /// <summary>
        /// Gets the system-wide active profile.
        /// </summary>
        /// <remarks>
        /// This represents the currently active profile at the system level, which may or may not
        /// be in the current session's <see cref="BoardSession.players"/> array.
        /// This is useful for displaying the current user in save game UI and other contexts.
        /// </remarks>
        public static BoardPlayer activeProfile { get; private set; } = null;

        /// <summary>
        /// Occurs when the system-wide active profile changes.
        /// </summary>
#pragma warning disable CS0067 // Event is used in external test assemblies
        public static event Action activeProfileChanged;
#pragma warning restore CS0067

        /// <summary>
        /// Initializes the session client in the native Board input SDK.
        /// </summary>
        internal static void Initialize()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            Board_SDK_Session_Initialize(BoardUnityContext.unityContextHandle);

            // Start polling for native changes (players and pause menu)
            var pollingObject = new UnityEngine.GameObject("BoardSession Native Polling");
            var pollingBehaviour = pollingObject.AddComponent<NativePollingBehaviour>();
            UnityEngine.Object.DontDestroyOnLoad(pollingObject);
#endif //UNITY_ANDROID && !UNITY_EDITOR
        }

        /// <summary>
        /// Registers the AI player types this game supports.
        /// </summary>
        /// <remarks>
        /// Call this once during game initialization, before presenting any player selectors.
        /// The registered types determine what AI options appear in the player selector UI.
        /// Maximum 8 AI player types are supported.
        /// On older OS versions that do not support AI players, this is a no-op.
        /// </remarks>
        /// <param name="types">Array of AI player types the game supports.</param>
        public static void SetAIPlayerTypes(BoardAIPlayerType[] types)
        {
            s_aiPlayerTypes = types;
#if UNITY_ANDROID && !UNITY_EDITOR
            if (types == null || types.Length == 0)
            {
                return;
            }

            var names = types.Select(t => t.name ?? string.Empty).ToArray();
            var descriptions = types.Select(t => t.description ?? string.Empty).ToArray();
            Board_SDK_Session_Set_AI_Player_Types(BoardUnityContext.unityContextHandle, names, descriptions, types.Length);
#endif //UNITY_ANDROID && !UNITY_EDITOR
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        /// <summary>
        /// Poll for native changes to players list and active profile.
        /// </summary>
        /// <remarks>
        /// The array typically has a small number of elements (e.g.,1-6). Further, Unity
        /// is strictly a consumer of player data, so there is no need to
        /// perform an element-wise sync. Therefore, on any change, the active players array
        /// is fully refreshed from the native layer.
        /// </remarks>
        internal static void PollForNativeChanges()
        {
            try
            {
                // Poll for players changes
                if (Board_SDK_Session_HasPlayersChanged())
                {
                    var count = Board_SDK_Session_GetPlayersCount();

                    if (count > 0)
                    {
                        var playerData = new BoardSessionPlayerData[count];
                        var success = Board_SDK_Session_GetPlayers(playerData, count);

                        if (success)
                        {
                            var newPlayers = new List<BoardSessionPlayer>();
                            for (var i = 0; i < count; i++)
                            {
                                var player = new BoardSessionPlayer(ref playerData[i]);
                                newPlayers.Add(player);
                            }

                            players = newPlayers.ToArray();
                        }
                        else
                        {
                            BoardLogger.LogError(kLogTag, "Failed to retrieve player data from native");
                        }
                    }

                    playersChanged?.Invoke();
                }

                // Poll for active profile changes
                if (Board_SDK_Session_HasActiveProfileChanged())
                {
                    var profileData = new BoardSessionPlayerData();
                    var success = Board_SDK_Session_GetActiveProfile(ref profileData);

                    if (success)
                    {
                        // Create a base BoardPlayer (no sessionId, just the core player info)
                        activeProfile = new Core.BoardPlayer(
                            profileData.nameString,
                            profileData.playerIdString,
                            profileData.avatarIdString,
                            (BoardPlayerType)profileData.type
                        );
                    }
                    else
                    {
                        activeProfile = null;
                    }

                    activeProfileChanged?.Invoke();
                }
            }
            catch (System.Exception ex)
            {
                BoardLogger.LogError(kLogTag, $"Failed to poll for native changes: {ex.Message}");
            }
        }

#endif //UNITY_ANDROID && !UNITY_EDITOR
        
        /// <summary>
        /// Presents the native player selector to add a new player to the current session.
        /// </summary>
        /// <remarks>Any resulting changes to <see cref="BoardSession.players"/> will trigger the <see cref="BoardSession.playersChanged"/> event.</remarks>
        /// <returns>A task that completes with <c>true</c> if a player was added; otherwise, <c>false</c> if the selector was dismissed</returns>
        /// <exception cref="InvalidOperationException">If the native player selector failed to open.</exception>
        public static Task<bool> PresentAddPlayerSelector()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return PresentPlayerSelectorAsync(() =>
                Board_SDK_Session_Present_Player_Selector_Add(BoardUnityContext.unityContextHandle,
                    OnPlayerSelectorCompleted, OnPlayerSelectorDismissed));
#else
            return Task.FromException<bool>(new InvalidOperationException("Player selector not available in editor"));
#endif //UNITY_ANDROID && !UNITY_EDITOR
        }

        /// <summary>
        /// Presents the native player selector to add a new player to the current session,
        /// with optional filtering of which AI types to show.
        /// </summary>
        /// <remarks>
        /// Any resulting changes to <see cref="BoardSession.players"/> will trigger the <see cref="BoardSession.playersChanged"/> event.
        /// If <paramref name="aiTypeIndices"/> is null, all registered AI types are shown.
        /// On older OS versions, AI types are not shown (graceful degradation).
        /// </remarks>
        /// <param name="aiTypeIndices">Indices into the registered AI types to show, or null for all.</param>
        /// <returns>A task that completes with <c>true</c> if a player was added; otherwise, <c>false</c> if the selector was dismissed.</returns>
        /// <exception cref="InvalidOperationException">If the native player selector failed to open.</exception>
        public static Task<bool> PresentAddPlayerSelector(int[] aiTypeIndices)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return PresentPlayerSelectorAsync(() =>
                Board_SDK_Session_Present_Player_Selector_Add_V2(BoardUnityContext.unityContextHandle,
                    aiTypeIndices, aiTypeIndices?.Length ?? 0,
                    OnPlayerSelectorCompleted, OnPlayerSelectorDismissed));
#else
            return Task.FromException<bool>(new InvalidOperationException("Player selector not available in editor"));
#endif //UNITY_ANDROID && !UNITY_EDITOR
        }

        /// <summary>
        /// Resets the session players to the initial state.
        /// </summary>
        /// <remarks>
        /// This method clears all current players and re-adds only the active profile as the first player,
        /// returning the session to its initialized state. Any resulting changes to <see cref="BoardSession.players"/>
        /// will trigger the <see cref="BoardSession.playersChanged"/> event.
        /// </remarks>
        /// <returns><c>true</c> if the reset was successful; otherwise, <c>false</c>.</returns>
        public static bool ResetPlayers()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            var success = Board_SDK_Session_Reset_Players(BoardUnityContext.unityContextHandle);
            if (!success)
            {
                BoardLogger.LogError(kLogTag, "Failed to reset players");
            }
            return success;
#else
            return false;
#endif //UNITY_ANDROID && !UNITY_EDITOR
        }

        /// <summary>
        /// Presents the native player selector to replace or remove an existing <see cref="BoardSessionPlayer"/> from the current session.
        /// </summary>
        /// <remarks>Any resulting changes to <see cref="BoardSession.players"/> will trigger the <see cref="BoardSession.playersChanged"/> event.</remarks>
        /// <param name="player">The <see cref="BoardSessionPlayer"/> in the current session to replace or remove.</param>
        /// <returns>A task that completes with <c>true</c> if the player was replaced; otherwise, <c>false</c> if the selector was dismissed.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="player"/> is <c>null</c>.</exception>
        /// <exception cref="InvalidOperationException">The native player selector failed to open.</exception>
        public static Task<bool> PresentReplacePlayerSelector(BoardSessionPlayer player)
        {
            if (player == null)
            {
                throw new ArgumentNullException(nameof(player));
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            return PresentPlayerSelectorAsync(() =>
                Board_SDK_Session_Present_Player_Selector_Replace(BoardUnityContext.unityContextHandle,
                    player.sessionId, OnPlayerSelectorCompleted, OnPlayerSelectorDismissed));
#else
            return Task.FromException<bool>(new InvalidOperationException("Player selector not available in editor"));
#endif //UNITY_ANDROID && !UNITY_EDITOR
        }

        /// <summary>
        /// Presents the native player selector to replace or remove an existing <see cref="BoardSessionPlayer"/> from the current session,
        /// with optional filtering of which AI types to show.
        /// </summary>
        /// <remarks>
        /// Any resulting changes to <see cref="BoardSession.players"/> will trigger the <see cref="BoardSession.playersChanged"/> event.
        /// If <paramref name="aiTypeIndices"/> is null, all registered AI types are shown.
        /// On older OS versions, AI types are not shown (graceful degradation).
        /// </remarks>
        /// <param name="player">The <see cref="BoardSessionPlayer"/> in the current session to replace or remove.</param>
        /// <param name="aiTypeIndices">Indices into the registered AI types to show, or null for all.</param>
        /// <returns>A task that completes with <c>true</c> if the player was replaced; otherwise, <c>false</c> if the selector was dismissed.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="player"/> is <c>null</c>.</exception>
        /// <exception cref="InvalidOperationException">The native player selector failed to open.</exception>
        public static Task<bool> PresentReplacePlayerSelector(BoardSessionPlayer player, int[] aiTypeIndices)
        {
            if (player == null)
            {
                throw new ArgumentNullException(nameof(player));
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            return PresentPlayerSelectorAsync(() =>
                Board_SDK_Session_Present_Player_Selector_Replace_V2(BoardUnityContext.unityContextHandle,
                    player.sessionId, aiTypeIndices, aiTypeIndices?.Length ?? 0,
                    OnPlayerSelectorCompleted, OnPlayerSelectorDismissed));
#else
            return Task.FromException<bool>(new InvalidOperationException("Player selector not available in editor"));
#endif //UNITY_ANDROID && !UNITY_EDITOR
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        /// <summary>
        /// Shared implementation for all player selector presentations.
        /// Handles service readiness, concurrency guarding, TCS lifecycle, and cleanup.
        /// </summary>
        /// <param name="openSelector">A function that invokes the native P/Invoke call and returns whether it opened successfully.</param>
        private static async Task<bool> PresentPlayerSelectorAsync(Func<bool> openSelector)
        {
            await BoardSupport.WaitForServicesAsync();
            var tcs = new TaskCompletionSource<bool>();
            var requestId = 0;

            lock (s_CallbackLock)
            {
                if (s_PlayerSelectorInProgress && s_PendingPlayerSelectors.Count > 0)
                {
                    BoardLogger.LogWarning(kLogTag, "Player selector already in progress");
                    return false;
                }

                if (s_PendingPlayerSelectors.Count == 0)
                {
                    s_PlayerSelectorInProgress = false;
                }

                s_PlayerSelectorInProgress = true;
                requestId = s_NextRequestId++;
                s_PendingPlayerSelectors[requestId] = tcs;
            }

            try
            {
                if (!openSelector())
                {
                    lock (s_CallbackLock)
                    {
                        s_PendingPlayerSelectors.Remove(requestId);
                        s_PlayerSelectorInProgress = false;
                    }

                    throw new InvalidOperationException("Failed to present player selector");
                }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Board.Input.Debug.BoardInputDebugView.Clear();
#endif //UNITY_EDITOR || DEVELOPMENT_BUILD

                return await tcs.Task.ConfigureAwait(false);
            }
            catch
            {
                lock (s_CallbackLock)
                {
                    s_PendingPlayerSelectors.Remove(requestId);
                    s_PlayerSelectorInProgress = false;
                }

                throw;
            }
        }

        // Delegate types for native callbacks
        /// <summary>
        /// Callback delegate for when a profile is selected in the player selector.
        /// </summary>
        public delegate void PlayerSelectorCompletionDelegate();

        /// <summary>
        /// Callback delegate for when the player selector is dismissed.
        /// </summary>
        public delegate void PlayerSelectorDismissDelegate();

        /// <summary>
        /// Callback method for player selector completion that can be marshaled to native code.
        /// Note: Since all requests share the same callback function pointers, and the C++ layer
        /// now properly tracks callbacks by callbackId, we simply complete the first (oldest) pending request.
        /// The C++ layer ensures only the correct callback is invoked.
        /// </summary>
        [AOT.MonoPInvokeCallback(typeof(PlayerSelectorCompletionDelegate))]
        private static void OnPlayerSelectorCompleted()
        {
            lock (s_CallbackLock)
            {
                if (s_PendingPlayerSelectors.Count > 0)
                {
                    var kvp = s_PendingPlayerSelectors.First();
                    var tcs = kvp.Value;
                    s_PendingPlayerSelectors.Remove(kvp.Key);
                    tcs.TrySetResult(true);
                }

                // Always clear the flag, even if no pending selectors (handles edge cases like app backgrounding)
                s_PlayerSelectorInProgress = false;
            }
        }

        /// <summary>
        /// Callback method for player selector dismissal that can be marshaled to native code.
        /// Note: Since all requests share the same callback function pointers, and the C++ layer
        /// now properly tracks callbacks by callbackId, we simply complete the first (oldest) pending request.
        /// The C++ layer ensures only the correct callback is invoked.
        /// </summary>
        [AOT.MonoPInvokeCallback(typeof(PlayerSelectorDismissDelegate))]
        private static void OnPlayerSelectorDismissed()
        {
            lock (s_CallbackLock)
            {
                if (s_PendingPlayerSelectors.Count > 0)
                {
                    var kvp = s_PendingPlayerSelectors.First();
                    var tcs = kvp.Value;
                    s_PendingPlayerSelectors.Remove(kvp.Key);
                    tcs.TrySetResult(false);
                }

                // Always clear the flag, even if no pending selectors (handles edge cases like app backgrounding)
                s_PlayerSelectorInProgress = false;
            }
        }

        /// <summary>
        /// Initializes the native Board Session API.
        /// </summary>
        /// <param name="unityContext">The Unity context of the API call.</param>
        [DllImport(BoardSupport.kNativePluginName)]
        private static extern void Board_SDK_Session_Initialize(IntPtr unityContext);

        /// <summary>
        /// Presents the profile selector for adding a new profile with title context.
        /// </summary>
        /// <param name="unityContext">The Unity context of the API call.</param>
        /// <param name="completionCallback">Callback invoked when a profile is selected.</param>
        /// <param name="dismissCallback">Callback invoked when the selector is dismissed.</param>
        /// <returns><c>true</c> if the profile selector was successfully opened; otherwise, <c>false</c>.</returns>
        [DllImport(BoardSupport.kNativePluginName)]
        private static extern bool Board_SDK_Session_Present_Player_Selector_Add(IntPtr unityContext, PlayerSelectorCompletionDelegate completionCallback, PlayerSelectorDismissDelegate dismissCallback);
        
        /// <summary>
        /// Presents the profile selector for replacing/removing an existing profile.
        /// </summary>
        /// <param name="unityContext">The Unity context of the API call.</param>
        /// <param name="sessionId">The session ID of the profile to replace/remove.</param>
        /// <param name="completionCallback">Callback invoked when a profile is selected.</param>
        /// <param name="dismissCallback">Callback invoked when the selector is dismissed.</param>
        /// <returns><c>true</c> if the profile selector was successfully opened; otherwise, <c>false</c>.</returns>
        [DllImport(BoardSupport.kNativePluginName)]
        private static extern bool Board_SDK_Session_Present_Player_Selector_Replace(IntPtr unityContext, int sessionId, PlayerSelectorCompletionDelegate completionCallback, PlayerSelectorDismissDelegate dismissCallback);

        [DllImport(BoardSupport.kNativePluginName)]
        private static extern void Board_SDK_Session_Set_AI_Player_Types(IntPtr unityContext, string[] names, string[] descriptions, int count);

        [DllImport(BoardSupport.kNativePluginName)]
        private static extern bool Board_SDK_Session_Present_Player_Selector_Add_V2(IntPtr unityContext, int[] aiTypeIndices, int aiTypeCount, PlayerSelectorCompletionDelegate completionCallback, PlayerSelectorDismissDelegate dismissCallback);

        [DllImport(BoardSupport.kNativePluginName)]
        private static extern bool Board_SDK_Session_Present_Player_Selector_Replace_V2(IntPtr unityContext, int sessionId, int[] aiTypeIndices, int aiTypeCount, PlayerSelectorCompletionDelegate completionCallback, PlayerSelectorDismissDelegate dismissCallback);

        /// <summary>
        /// Checks if active players have changed since last poll.
        /// </summary>
        /// <returns><c>true</c> if active players have changed; otherwise, <c>false</c>.</returns>
        [DllImport(BoardSupport.kNativePluginName)]
        private static extern bool Board_SDK_Session_HasPlayersChanged();
        
        /// <summary>
        /// Gets the current count of active players.
        /// </summary>
        /// <returns>The number of active players.</returns>
        [DllImport(BoardSupport.kNativePluginName)]
        private static extern int Board_SDK_Session_GetPlayersCount();
    
        /// <summary>
        /// Gets current active players' data.
        /// </summary>
        /// <param name="players">Array to populate with player data.</param>
        /// <param name="maxCount">Maximum number of players to retrieve.</param>
        /// <returns><c>true</c> if profiles were retrieved successfully; otherwise, <c>false</c>.</returns>
        [DllImport(BoardSupport.kNativePluginName)]
        private static extern bool Board_SDK_Session_GetPlayers([Out] BoardSessionPlayerData[] players, int maxCount);

        /// <summary>
        /// Resets session players to initial state (clears all and re-adds active profile).
        /// </summary>
        /// <param name="unityContext">The Unity context of the API call.</param>
        /// <returns><c>true</c> if the reset was successful; otherwise, <c>false</c>.</returns>
        [DllImport(BoardSupport.kNativePluginName)]
        private static extern bool Board_SDK_Session_Reset_Players(IntPtr unityContext);

        /// <summary>
        /// Checks if active profile has changed since last poll.
        /// </summary>
        /// <returns><c>true</c> if active profile has changed; otherwise, <c>false</c>.</returns>
        [DllImport(BoardSupport.kNativePluginName)]
        private static extern bool Board_SDK_Session_HasActiveProfileChanged();

        /// <summary>
        /// Gets the current active profile data.
        /// </summary>
        /// <param name="outPlayerData">Output parameter to receive the active profile data.</param>
        /// <returns><c>true</c> if active profile was retrieved successfully; otherwise, <c>false</c>.</returns>
        [DllImport(BoardSupport.kNativePluginName)]
        private static extern bool Board_SDK_Session_GetActiveProfile(ref BoardSessionPlayerData outPlayerData);
#endif //UNITY_ANDROID && !UNITY_EDITOR
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    /// <summary>
    /// Provides a mechanism for polling for native changes (players and pause menu) from the native plugin.
    /// </summary>
    internal class NativePollingBehaviour : MonoBehaviour
    {
        /// <summary>
        /// Callback invoked by Unity every frame.
        /// </summary>
        private void Update()
        {
            BoardSession.PollForNativeChanges();
            BoardApplication.PollForNativeChanges();
            BoardAvatarManager.ProcessMainThreadQueue();
        }
    }
#endif //UNITY_ANDROID && !UNITY_EDITOR
}
