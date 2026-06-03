// <copyright file="BoardUnityContext.cs" company="Harris Hill Products Inc.">
//     Copyright (c) Harris Hill Products Inc. All rights reserved.
// </copyright>

namespace Board.Core
{
    using System;
    using System.Runtime.InteropServices;

    /// <summary>
    /// Encapsulates all the Unity context for Board's native API.
    /// </summary>
    internal class BoardUnityContext
    {
        /// <summary>
        /// Encapsulates configuration data defined in Unity for the native Board API.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct BoardEnvironmentConfig
        {
            public string applicationId;
            public int logLevel;
        }
        
        /// <summary>
        /// Gets a pointer to the <see cref="BoardUnityContext"/>.
        /// </summary>
        public static IntPtr unityContextHandle { get; private set; } = IntPtr.Zero;

        /// <summary>
        /// Initializes the <see cref="BoardUnityContext"/> system.
        /// </summary>
        internal static void Initialize()
        {
            var settings = BoardGeneralSettings.instance;

            if (settings == null)
            {
                BoardLogger.LogError("BoardUnityContext", "BoardGeneralSettings.instance is null");
                return;
            }

            if (string.IsNullOrEmpty(settings.applicationId))
            {
                BoardLogger.LogError("BoardUnityContext", "Application ID is null or empty");
                return;
            }

            var envConfig = new BoardEnvironmentConfig()
            {
                applicationId = settings.applicationId,
                logLevel = (int)settings.logLevel
            };

            unityContextHandle = Board_SDK_Unity_Context_Create(ref envConfig);
        }
        
        /// <summary>
        /// Creates a Unity context in the native API.
        /// </summary>
        /// <param name="environmentConfig">The configuration data for the Unity context.</param>
        /// <returns>A pointer to a Unity context allocated in the native API.</returns>
        [DllImport(BoardSupport.kNativePluginName)]
        private static extern IntPtr Board_SDK_Unity_Context_Create(ref BoardEnvironmentConfig environmentConfig);
    }
}