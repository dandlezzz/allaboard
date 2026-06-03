// <copyright file="BoardApplication.cs" company="Harris Hill Products Inc.">
//     Copyright (c) Harris Hill Products Inc. All rights reserved.
// </copyright>

namespace Board.Core
{
    using System;
#if UNITY_ANDROID && !UNITY_EDITOR
    using System.Runtime.InteropServices;
    
    using Board.Save;
#endif
    
    using UnityEngine;
    
    /// <summary>
    /// Represents the method that will handle the <c>customPauseScreenButtonPressed</c> event of the <see cref="BoardApplication"/> class.
    /// </summary>
    /// <param name="customButtonId">The identifier of the custom button that was tapped by the user.</param>
    /// <param name="audioTracks">The new state of all <see cref="BoardPauseAudioTrack">audio tracks</see>.</param>
    public delegate void PauseScreenCustomButtonPressedHandler(string customButtonId, BoardPauseAudioTrack[] audioTracks);

    /// <summary>
    /// Represents the method that will handle the <c>pauseScreenActionReceived</c> event of the <see cref="BoardApplication"/> class.
    /// </summary>
    /// <param name="pauseAction">The <see cref="BoardPauseAction">action</see> that was taken by the user.</param>
    /// <param name="audioTracks">The new state of all <see cref="BoardPauseAudioTrack">audio tracks</see>.</param>
    public delegate void PauseScreenActionReceivedHandler(BoardPauseAction pauseAction, BoardPauseAudioTrack[] audioTracks);
    
    /// <summary>
    /// Provides access to the application's runtime data and settings.
    /// </summary>
    public static class BoardApplication
    {
        private const string kLogTag = nameof(BoardApplication);

#if UNITY_ANDROID && !UNITY_EDITOR
        // Track current pause screen context for merging updates
        private static string s_CurrentPauseScreenApplicationName = null;
        private static bool s_CurrentPauseScreenShowSaveOption = false;
        private static BoardPauseCustomButton[] s_CurrentPauseScreenCustomButtons = null;
        private static BoardPauseAudioTrack[] s_CurrentPauseScreenAudioTracks = null;
        private static bool s_HasInitializedPauseScreenContext = false;
#endif
        
#pragma warning disable CS0067
        /// <summary>
        /// Occurs when a custom button is pressed in Board's pause screen.
        /// </summary>
        public static event PauseScreenCustomButtonPressedHandler customPauseScreenButtonPressed;

        /// <summary>
        /// Occurs when an action is received from Board's pause screen.
        /// </summary>
        public static event PauseScreenActionReceivedHandler pauseScreenActionReceived;
#pragma warning restore CS0067
        
        /// <summary>
        /// Initializes the session client in the native Board input SDK.
        /// </summary>
        internal static void Initialize()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            // Set default pause screen context so pause screen works out of the box
            SetPauseScreenContext();
#endif //UNITY_ANDROID && !UNITY_EDITOR
        }
        
        /// <summary>
        /// Shows the profile switcher button overlay in the top-left corner.
        /// </summary>
        /// <remarks>
        /// This should be called when the application allows the user to switch between profiles.
        /// The button will remain visible until <see cref="HideProfileSwitcher"/> is called.
        /// This is typically used in menu screens or lobby areas where profile switching is appropriate.
        /// </remarks>
        public static void ShowProfileSwitcher()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            Board_SDK_Session_Show_Profile_Switcher(BoardUnityContext.unityContextHandle);
#endif //UNITY_ANDROID && !UNITY_EDITOR
        }

        /// <summary>
        /// Hides the profile switcher button overlay.
        /// </summary>
        /// <remarks>
        /// This should be called when the application does not allow the user to switch between profiles.
        /// This is typically used when entering gameplay or other areas where profile switching
        /// should not be available.
        /// </remarks>
        public static void HideProfileSwitcher()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            Board_SDK_Session_Hide_Profile_Switcher(BoardUnityContext.unityContextHandle);
#endif //UNITY_ANDROID && !UNITY_EDITOR
        }

        /// <summary>
        /// Sets the pause screen context for the current application.
        /// This performs a full replacement of all pause screen settings.
        /// Unspecified parameters will be set to their default values.
        /// Use <see cref="UpdatePauseScreenContext"/> to update only specific fields while preserving others.
        /// </summary>
        /// <param name="context">The <see cref="BoardPauseScreenContext"/> to use.</param>
        public static void SetPauseScreenContext(BoardPauseScreenContext context)
        {
            SetPauseScreenContext(context.applicationName, context.showSaveOptionUponExit, context.customButtons, context.audioTracks);
        }

        /// <summary>
        /// Sets the pause screen context for the current game with full replacement.
        /// This replaces ALL pause screen settings. Unspecified optional parameters will use default values.
        /// To update only specific fields while preserving others, use <see cref="UpdatePauseScreenContext"/> instead.
        /// </summary>
        /// <param name="applicationName">The application name to display. If unspecified, defaults to <see cref="Application.productName"/>.</param>
        /// <param name="showSaveOptionUponExit"><c>true</c> if the save progress dialog should be shown upon exiting the application. If unspecified, defaults to <c>false</c>.</param>
        /// <param name="customButtons">A collection of custom action buttons. If unspecified, defaults to an empty array.</param>
        /// <param name="audioTracks">A collection of configurable audio tracks. If unspecified, defaults to an empty array.</param>
        public static void SetPauseScreenContext(
            string applicationName = null,
            bool? showSaveOptionUponExit = null,
            BoardPauseCustomButton[] customButtons = null,
            BoardPauseAudioTrack[] audioTracks = null)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            // Set with defaults - full replacement
            var finalName = applicationName ?? Application.productName;
            var finalShowSaveOption = showSaveOptionUponExit ?? false;
            var finalCustomButtons = customButtons ?? new BoardPauseCustomButton[0];
            var finalAudioTracks = audioTracks ?? new BoardPauseAudioTrack[0];

            // Update tracked state
            s_CurrentPauseScreenApplicationName = finalName;
            s_CurrentPauseScreenShowSaveOption = finalShowSaveOption;
            s_CurrentPauseScreenCustomButtons = finalCustomButtons;
            s_CurrentPauseScreenAudioTracks = finalAudioTracks;
            s_HasInitializedPauseScreenContext = true;

            // Send to native layer
            SendPauseContextToNative(finalName, finalShowSaveOption, finalCustomButtons, finalAudioTracks);
#endif
        }

        /// <summary>
        /// Updates specific fields of the pause screen context while preserving others.
        /// Only the parameters you specify will be updated. All other settings remain unchanged.
        /// Pass an empty array to clear a field (e.g., customButtons: new BoardPauseCustomButton[0]).
        /// </summary>
        /// <param name="applicationName">The application name to display. If null, keeps current value.</param>
        /// <param name="showSaveOptionUponExit"><c>true</c> if the save progress dialog should be shown. If null, keeps current value.</param>
        /// <param name="customButtons">A collection of custom action buttons. If null, keeps current value. Specify an empty array to clear.</param>
        /// <param name="audioTracks">A collection of configurable audio tracks. If null, keeps current value. Specify an empty array to clear.</param>
        public static void UpdatePauseScreenContext(
            string applicationName = null,
            bool? showSaveOptionUponExit = null,
            BoardPauseCustomButton[] customButtons = null,
            BoardPauseAudioTrack[] audioTracks = null)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            // Initialize with defaults if never set before
            if (!s_HasInitializedPauseScreenContext)
            {
                SetPauseScreenContext();
                return;
            }

            // Merge with current state - only update what was specified
            var finalName = applicationName ?? s_CurrentPauseScreenApplicationName;
            var finalShowSaveOption = showSaveOptionUponExit ?? s_CurrentPauseScreenShowSaveOption;
            var finalCustomButtons = customButtons ?? s_CurrentPauseScreenCustomButtons;
            var finalAudioTracks = audioTracks ?? s_CurrentPauseScreenAudioTracks;

            // Update tracked state
            s_CurrentPauseScreenApplicationName = finalName;
            s_CurrentPauseScreenShowSaveOption = finalShowSaveOption;
            s_CurrentPauseScreenCustomButtons = finalCustomButtons;
            s_CurrentPauseScreenAudioTracks = finalAudioTracks;

            // Send to native layer
            SendPauseContextToNative(finalName, finalShowSaveOption, finalCustomButtons, finalAudioTracks);
#endif
        }

        /// <summary>
        /// Clears the current pause screen context and resets tracked state.
        /// </summary>
        public static void ClearPauseScreenContext()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            Board_SDK_Session_ClearPauseScreenContext();

            // Reset tracked state
            s_CurrentPauseScreenApplicationName = null;
            s_CurrentPauseScreenShowSaveOption = false;
            s_CurrentPauseScreenCustomButtons = null;
            s_CurrentPauseScreenAudioTracks = null;
            s_HasInitializedPauseScreenContext = false;
#endif
        }
        
        /// <summary>
        /// Exits the application. Functions similar to swiping the application away from the Recent Apps screen.
        /// This is a fire-and-forget operation; the app will be terminated immediately and cannot receive a response.
        /// </summary>
        /// <remarks>
        /// This method cleanly closes the app by removing its task from the system. All activity lifecycle callbacks
        /// will be properly triggered. Use this when you need to programmatically exit the application.
        /// </remarks>
        public static void Exit()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            Board_SDK_Session_Terminate_Application(BoardUnityContext.unityContextHandle);
#elif UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR

        /// <summary>
        /// Helper method to send pause context to native layer.
        /// </summary>
        private static void SendPauseContextToNative(
            string applicationName,
            bool showSaveOption,
            BoardPauseCustomButton[] customButtons,
            BoardPauseAudioTrack[] audioTracks)
        {
            Board_SDK_Session_SetPauseScreenContext(
                applicationName,
                showSaveOption,
                customButtons,
                customButtons.Length,
                audioTracks,
                audioTracks.Length
            );
        }

        /// <summary>
        /// Poll for native changes to the pause menu actions.
        /// </summary>
        internal static void PollForNativeChanges()
        {
            try
            {
                if (Board_SDK_Session_HasPauseMenuAction())
                {
                    var result = new BoardPauseScreenResult();
                    if (Board_SDK_Session_GetPauseMenuResult(ref result))
                    {
                        var audioTracks = new BoardPauseAudioTrack[result.audioTrackCount];
                        Array.Copy(result.audioTracks, audioTracks, result.audioTrackCount);

                        if (result.actionType == BoardPauseAction.CustomButton)
                        {
                            customPauseScreenButtonPressed?.Invoke(result.customButtonId, audioTracks);
                        }
                        else
                        {
                            pauseScreenActionReceived?.Invoke(result.actionType, audioTracks);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                BoardLogger.LogError(kLogTag, $"Failed to poll for native changes: {ex.Message}");
            }
        }

        /// <summary>
        /// Terminates the calling application cleanly.
        /// </summary>
        /// <param name="unityContext">The Unity context of the API call.</param>
        [DllImport(BoardSupport.kNativePluginName)]
        private static extern void Board_SDK_Session_Terminate_Application(IntPtr unityContext);

        /// <summary>
        /// Shows the profile switcher button overlay.
        /// </summary>
        /// <param name="unityContext">The Unity context of the API call.</param>
        [DllImport(BoardSupport.kNativePluginName)]
        private static extern void Board_SDK_Session_Show_Profile_Switcher(IntPtr unityContext);

        /// <summary>
        /// Hides the profile switcher button overlay.
        /// </summary>
        /// <param name="unityContext">The Unity context of the API call.</param>
        [DllImport(BoardSupport.kNativePluginName)]
        private static extern void Board_SDK_Session_Hide_Profile_Switcher(IntPtr unityContext);

        /// <summary>
        /// Checks if a pause menu action has been received.
        /// </summary>
        /// <returns><c>true</c> if a pause menu action is available; otherwise, <c>false</c>.</returns>
        [DllImport(BoardSupport.kNativePluginName)]
        private static extern bool Board_SDK_Session_HasPauseMenuAction();

        /// <summary>
        /// Gets the pause menu result.
        /// </summary>
        /// <param name="outResult">The result structure to populate.</param>
        /// <returns><c>true</c> if the result was retrieved successfully; otherwise, <c>false</c>.</returns>
        [DllImport(BoardSupport.kNativePluginName)]
        private static extern bool Board_SDK_Session_GetPauseMenuResult(ref BoardPauseScreenResult outResult);

        /// <summary>
        /// Sets the pause screen context.
        /// </summary>
        [DllImport(BoardSupport.kNativePluginName)]
        private static extern void Board_SDK_Session_SetPauseScreenContext(
            string applicationName,
            bool offerSaveOption,
            BoardPauseCustomButton[] customButtons,
            int customButtonCount,
            BoardPauseAudioTrack[] audioTracks,
            int audioTrackCount
        );

        /// <summary>
        /// Clears the pause screen context.
        /// </summary>
        [DllImport(BoardSupport.kNativePluginName)]
        private static extern void Board_SDK_Session_ClearPauseScreenContext();
#endif //UNITY_ANDROID && !UNITY_EDITOR
    }
}
