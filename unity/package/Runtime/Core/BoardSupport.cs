// <copyright file="BoardSupport.cs" company="Harris Hill Products Inc.">
//     Copyright (c) Harris Hill Products Inc. All rights reserved.
// </copyright>

namespace Board.Core
{
    using Board.Input;
#if UNITY_ANDROID && !UNITY_EDITOR
    using Board.Session;
#endif //UNITY_ANDROID && !UNITY_EDITOR

#if UNITY_EDITOR
    using UnityEditor;
#endif //UNITY_EDITOR
    using UnityEngine;
    using System.Collections;
    using System.Runtime.InteropServices;
    using System.Threading.Tasks;

    /// <summary>
    /// Provides access to Board's touch input. 
    /// </summary>
#if UNITY_EDITOR
    [InitializeOnLoad]
#endif
    public static class BoardSupport
    {
        internal const string kNativePluginName = "nativeBoardSDK";
#if UNITY_ANDROID && !UNITY_EDITOR
        internal const string kUnityPlayerClassName = "com.unity3d.player.UnityPlayer";
        private const string kBoardSDKClassName = "co.harrishill.boardunitynativeplugin.core.BoardNativePlugin";
        private const string kBoardSDKInitializeMethodName = "isDeviceSupported";

        /// <summary>
        /// Maximum time to wait for services to be ready before timing out (in seconds).
        /// Native plugin retries for ~5 seconds, so we use 6 seconds as a safe upper bound.
        /// </summary>
        private const float kServiceTimeoutSeconds = 6f;

        private static bool? s_ServicesReady = null;
        private static TaskCompletionSource<bool> s_ServicesTcs = new TaskCompletionSource<bool>();
#endif //UNITY_ANDROID && !UNITY_EDITOR

        /// <summary>
        /// Gets a value indicating whether the current platform supports the Board SDK.
        /// </summary>
        /// <returns><see langword="true"/> if the current platform supports the Board SDK; otherwise <see langword="false"/>.</returns>
        public static bool enabled { get; private set; }
        
        /// <summary>
        /// Static constructor for <see cref="BoardSupport"/>.
        /// </summary>
        static BoardSupport()
        {
#if UNITY_EDITOR
            // Defer asset creation to avoid errors during SDK import
            EditorApplication.delayCall += EnsureSettingsAssetExists;
#endif
        }

#if UNITY_EDITOR
        /// <summary>
        /// Ensures the BoardGeneralSettings asset exists, creating it if necessary.
        /// </summary>
        private static void EnsureSettingsAssetExists()
        {
            if (!EditorBuildSettings.TryGetConfigObject(BoardGeneralSettings.kEditorBuildSettingsConfigKey,
                    out BoardGeneralSettings settingsAsset))
            {
                BoardGeneralSettings.CreateInstanceAsset();
            }
        }
#endif

        /// <summary>
        /// Initializes the native Board input SDK.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        internal static void Initialize()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            using (AndroidJavaClass activityClass = new AndroidJavaClass(BoardSupport.kUnityPlayerClassName),
                   androidPlugin = new AndroidJavaClass(kBoardSDKClassName))
            {
                enabled = androidPlugin.CallStatic<bool>(kBoardSDKInitializeMethodName);

                if (!enabled)
                {
                    BoardLogger.LogWarning("BoardSupport", "Device not supported - skipping Board SDK initialization");
                    return;
                }
            }

            BoardUnityContext.Initialize();
            BoardSession.Initialize();
            BoardApplication.Initialize();
            BoardInput.Initialize();

            var go = new GameObject("BoardServicePoller");
            go.AddComponent<ServicePoller>();
            UnityEngine.Object.DontDestroyOnLoad(go);
#elif UNITY_EDITOR
            enabled = true;
            BoardInput.Initialize();
#endif //UNITY_EDITOR
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        /// <summary>
        /// Wait for services to be ready before proceeding.
        /// </summary>
        internal static async Task WaitForServicesAsync()
        {
            if (s_ServicesReady == true)
            {
                return;
            }

            await s_ServicesTcs.Task;
        }

        /// <summary>
        /// Provides a mechanism to poll for service readiness from the native plugin.
        /// </summary>
        private class ServicePoller : MonoBehaviour
        {
            private float m_ElapsedTime = 0f;

            /// <summary>
            /// Callback invoked by Unity before the first update call.
            /// </summary>
            private void Start()
            {
                StartCoroutine(PollServices());
            }

            /// <summary>
            /// Polls the readiness of the services from the native plugin.
            /// </summary>
            private IEnumerator PollServices()
            {
                const float pollInterval = 0.1f;

                while (s_ServicesReady != true)
                {
                    // Check if services are ready
                    if (Board_SDK_Are_Services_Ready())
                    {
                        s_ServicesReady = true;
                        s_ServicesTcs.TrySetResult(true);
                        Destroy(gameObject);
                        yield break;
                    }

                    // Check if native plugin reported binding failure
                    if (Board_SDK_Have_Services_Failed())
                    {
                        s_ServicesReady = false;
                        var ex = new System.TimeoutException("Board service binding failed after exhausting retries");
                        BoardLogger.LogError("BoardSupport", ex.Message);
                        s_ServicesTcs.TrySetException(ex);
                        Destroy(gameObject);
                        yield break;
                    }

                    // Check for timeout
                    m_ElapsedTime += pollInterval;
                    if (m_ElapsedTime >= kServiceTimeoutSeconds)
                    {
                        s_ServicesReady = false;
                        var ex = new System.TimeoutException($"Board services did not become ready within {kServiceTimeoutSeconds} seconds");
                        BoardLogger.LogError("BoardSupport", ex.Message);
                        s_ServicesTcs.TrySetException(ex);
                        Destroy(gameObject);
                        yield break;
                    }

                    yield return new WaitForSeconds(pollInterval);
                }
            }
        }

        /// <summary>
        /// Check if services are ready from the native plugin.
        /// </summary>
        /// <returns><c>true</c> if services are ready; otherwise, <c>false</c>.</returns>
        [DllImport(kNativePluginName)]
        private static extern bool Board_SDK_Are_Services_Ready();

        /// <summary>
        /// Check if service binding has failed from the native plugin.
        /// </summary>
        /// <returns><c>true</c> if service binding failed; otherwise, <c>false</c>.</returns>
        [DllImport(kNativePluginName)]
        private static extern bool Board_SDK_Have_Services_Failed();
#endif //UNITY_ANDROID && !UNITY_EDITOR
    }
}
