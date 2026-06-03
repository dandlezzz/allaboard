// <copyright file="BoardLogger.cs" company="Harris Hill Products Inc.">
//     Copyright (c) Harris Hill Products Inc. All rights reserved.
// </copyright>

namespace Board.Core
{
    using System;
    using System.Runtime.CompilerServices;
#if UNITY_ANDROID && !UNITY_EDITOR
    using System.Runtime.InteropServices;
#endif //UNITY_ANDROID && !UNITY_EDITOR

    using UnityEngine;
    using UnityObject = UnityEngine.Object;

    /// <summary>
    /// Specifies the level of logging in the Board SDK.
    /// </summary>
    [Flags]
    public enum BoardLogLevel : uint
    {
        /// <summary>Disables all logging.</summary>
        None = 0,
        /// <summary>Enables debug-level logging.</summary>
        Debug = 1 << 0,
        /// <summary>Enables info-level logging.</summary>
        Info = 1 << 1,
        /// <summary>Enables warning-level logging.</summary>
        Warning = 1 << 2,
        /// <summary>Enables error-level logging.</summary>
        Error = 1 << 3,
        /// <summary>Enables all logging levels.</summary>
        All = Debug | Info | Warning | Error,
    }

    /// <summary>
    /// Provides a mechanism for logging in the Unity layer of the Board SDK.
    /// </summary>
    internal static class BoardLogger
    {
        /// <summary>
        /// Logs a debug message.
        /// </summary>
        /// <param name="tag">The tag for the message.</param>
        /// <param name="message">The message to log.</param>
        /// <param name="lineNumber">The line number of the calling function.</param>
        /// <param name="context">The object to which the message applies.</param>
        /// <exception cref="ArgumentNullException">Either <paramref name="tag"/> or <paramref name="message"/> are <c>null</c> or empty.</exception>
        internal static void LogDebug(string tag, string message, [CallerLineNumber] int lineNumber = 1,
            UnityObject context = null)
        {
            if (string.IsNullOrEmpty(tag))
            {
                throw new ArgumentNullException(nameof(tag));
            }

            if (string.IsNullOrEmpty(message))
            {
                throw new ArgumentNullException(nameof(message));
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            Board_SDK_Log_Debug(BoardUnityContext.unityContextHandle, tag, FormatMessage(message, lineNumber));
#else
            if (!BoardGeneralSettings.instance.logLevel.HasFlag(BoardLogLevel.Debug))
            {
                return;
            }

            Debug.Log($"[{tag}] {message}", context);
#endif //UNITY_ANDROID && !UNITY_EDITOR 
        }

        /// <summary>
        /// Logs an information message.
        /// </summary>
        /// <param name="tag">The tag for the message.</param>
        /// <param name="message">The message to log.</param>
        /// <param name="lineNumber">The line number of the calling function.</param>
        /// <param name="context">The object to which the message applies.</param>
        /// <exception cref="ArgumentNullException">Either <paramref name="tag"/> or <paramref name="message"/> are <c>null</c> or empty.</exception>
        internal static void LogInfo(string tag, string message, [CallerLineNumber] int lineNumber = 1,
            UnityObject context = null)
        {
            if (string.IsNullOrEmpty(tag))
            {
                throw new ArgumentNullException(nameof(tag));
            }

            if (string.IsNullOrEmpty(message))
            {
                throw new ArgumentNullException(nameof(message));
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            Board_SDK_Log_Info(BoardUnityContext.unityContextHandle, tag, FormatMessage(message, lineNumber));
#else
            if (!BoardGeneralSettings.instance.logLevel.HasFlag(BoardLogLevel.Info))
            {
                return;
            }

            Debug.Log($"[{tag}] {message}", context);
#endif //UNITY_ANDROID && !UNITY_EDITOR 
        }

        /// <summary>
        /// Logs a warning message.
        /// </summary>
        /// <param name="tag">The tag for the message.</param>
        /// <param name="message">The message to log.</param>
        /// <param name="lineNumber">The line number of the calling function.</param>
        /// <param name="context">The object to which the message applies.</param>
        /// <exception cref="ArgumentNullException">Either <paramref name="tag"/> or <paramref name="message"/> are <c>null</c> or empty.</exception>
        internal static void LogWarning(string tag, string message, [CallerLineNumber] int lineNumber = 1,
            UnityObject context = null)
        {
            if (string.IsNullOrEmpty(tag))
            {
                throw new ArgumentNullException(nameof(tag));
            }

            if (string.IsNullOrEmpty(message))
            {
                throw new ArgumentNullException(nameof(message));
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            Board_SDK_Log_Warning(BoardUnityContext.unityContextHandle, tag, FormatMessage(message, lineNumber));
#else
            if (!BoardGeneralSettings.instance.logLevel.HasFlag(BoardLogLevel.Warning))
            {
                return;
            }

            Debug.LogWarning($"[{tag}] {message}", context);
#endif //UNITY_ANDROID && !UNITY_EDITOR 
        }

        /// <summary>
        /// Logs an error message.
        /// </summary>
        /// <param name="tag">The tag for the message.</param>
        /// <param name="message">The message to log.</param>
        /// <param name="lineNumber">The line number of the calling function.</param>
        /// <param name="context">The object to which the message applies.</param>
        /// <exception cref="ArgumentNullException">Either <paramref name="tag"/> or <paramref name="message"/> are <c>null</c> or empty.</exception>
        internal static void LogError(string tag, string message, [CallerLineNumber] int lineNumber = 1,
            UnityObject context = null)
        {
            if (string.IsNullOrEmpty(tag))
            {
                throw new ArgumentNullException(nameof(tag));
            }

            if (string.IsNullOrEmpty(message))
            {
                throw new ArgumentNullException(nameof(message));
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            Board_SDK_Log_Error(BoardUnityContext.unityContextHandle, tag, FormatMessage(message, lineNumber));
#else
            if (!BoardGeneralSettings.instance.logLevel.HasFlag(BoardLogLevel.Error))
            {
                return;
            }

            Debug.LogError($"[{tag}] {message}", context);
#endif //UNITY_ANDROID && !UNITY_EDITOR 
        }

        /// <summary>
        /// Formats a log message for Board.
        /// </summary>
        /// <param name="message">The message to log</param>
        /// <param name="lineNumber">The line number that logged the message.</param>
        /// <returns>A formatted log message.</returns>
        private static string FormatMessage(string message, int lineNumber)
        {
            var (fullName, methodName) = GetCallerFromStack(3);
            return $"{fullName}::{methodName} at line {lineNumber} : {message}";
        }

        /// <summary>
        /// Gets the full name (Namespace.Class) and method name of the method calling this function.
        /// </summary>
        /// <param name="nestedLevel">The nested level of this call. For example, to get the direct
        ///   caller of this method, the level would be 1. To get the caller that calls this through
        ///   a helper, the level would be 2. </param>
        /// <returns>A string with the full name of the calling class if reflection succeeds; otherwise "HarrisHill.Board".</returns>
        private static (string fullName, string methodName) GetCallerFromStack(int nestedLevel)
        {
            // Get the frame above the current one (the caller of this method)
            var callerFrame = new System.Diagnostics.StackFrame(nestedLevel, false);

            var caller = callerFrame.GetMethod()?.ReflectedType;
            var methodName = callerFrame.GetMethod()?.Name ?? string.Empty;

            return caller == null ? ("HarrisHill.Board", string.Empty) : (caller.FullName, methodName);
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        /// <summary>
        /// Logs a debug message using the native API.
        /// </summary>
        /// <param name="unityContext">The Unity context of the API call.</param>
        /// <param name="tag">The tag for the message.</param>
        /// <param name="message">The message to log.</param>
        [DllImport(BoardSupport.kNativePluginName)]
        private static extern void Board_SDK_Log_Debug(IntPtr unityContext, [MarshalAs(UnmanagedType.LPUTF8Str)] string tag, [MarshalAs(UnmanagedType.LPUTF8Str)] string message);
        
        /// <summary>
        /// Logs an information message using the native API.
        /// </summary>
        /// <param name="unityContext">The Unity context of the API call.</param>
        /// <param name="tag">The tag for the message.</param>
        /// <param name="message">The message to log.</param>
        [DllImport(BoardSupport.kNativePluginName)]
        private static extern void Board_SDK_Log_Info(IntPtr unityContext, [MarshalAs(UnmanagedType.LPUTF8Str)] string tag, [MarshalAs(UnmanagedType.LPUTF8Str)] string message);
        
        /// <summary>
        /// Logs a warning message using the native API.
        /// </summary>
        /// <param name="unityContext">The Unity context of the API call.</param>
        /// <param name="tag">The tag for the message.</param>
        /// <param name="message">The message to log.</param>
        [DllImport(BoardSupport.kNativePluginName)]
        private static extern void Board_SDK_Log_Warning(IntPtr unityContext, [MarshalAs(UnmanagedType.LPUTF8Str)] string tag, [MarshalAs(UnmanagedType.LPUTF8Str)] string message);
        
        /// <summary>
        /// Logs an error message using the native API.
        /// </summary>
        /// <param name="unityContext">The Unity context of the API call.</param>
        /// <param name="tag">The tag for the message.</param>
        /// <param name="message">The message to log.</param>
        [DllImport(BoardSupport.kNativePluginName)]
        private static extern void Board_SDK_Log_Error(IntPtr unityContext, [MarshalAs(UnmanagedType.LPUTF8Str)] string tag, [MarshalAs(UnmanagedType.LPUTF8Str)] string message);
#endif //UNITY_ANDROID && !UNITY_EDITOR
    }
}