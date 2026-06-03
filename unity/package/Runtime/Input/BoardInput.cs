// <copyright file="BoardInput.cs" company="Harris Hill Products Inc.">
//     Copyright (c) Harris Hill Products Inc. All rights reserved.
// </copyright>

namespace Board.Input
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.InteropServices;
    
    using Board.Core;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    using Board.Input.Debug;
#endif //UNITY_EDITOR || DEVELOPMENT_BUILD

#if UNITY_ANDROID && !UNITY_EDITOR
    using Unity.Collections;
#endif //UNITY_ANDROID && !UNITY_EDITOR
    using Unity.Collections.LowLevel.Unsafe;

#if UNITY_EDITOR
    using UnityEditor;
#endif //UNITY_EDITOR

    using UnityEngine;
    using UnityEngine.InputSystem;
    using UnityEngine.InputSystem.LowLevel;
    using UnityEngine.Profiling;
    
    /// <summary>
    /// Provides access to Board's touch input. 
    /// </summary>
#if UNITY_EDITOR
    [InitializeOnLoad]
#endif
    public static class BoardInput
    {
        private delegate void BoardContactInputEventHandler(int offset, int count);

        private delegate void BoardActivityStartedEventHandler();

        private delegate void BoardActivityStoppedEventHandler();

        private const int kMaxContacts = 1000;
        private const string kLogTag = nameof(BoardInput);
        
#if UNITY_EDITOR
        internal const string kEditorBuildSettingsConfigKey = "fun.board.input.settings";
#endif //UNITY_EDITOR
        
#if UNITY_ANDROID && !UNITY_EDITOR
        private const int kNativeBufferLength = 500;
        private const string kBoardSDKClassName = "co.harrishill.boardunitynativeplugin.touch.detection.RawDataGlyphDetector";
        private const string kGlyphDictItemClassName = "touchhandling.GlyphDictionaryItem";
        private const string kBoardSDKInitializeMethodName = "initializeDetector";
        private const string kBoardSDKApplyInputSettingsMethodName = "applyInputSettings";

        private static NativeArray<BoardContactEvent> s_NativeInputBuffer;
        private static bool s_HasNativeInputBufferBeenDisposed = false;
        private static int s_LastReadIndex = 0;
#endif //UNITY_ANDROID && !UNITY_EDITOR
        
#if UNITY_ANDROID || UNITY_EDITOR
        private static readonly BoardContact[] s_State;
        private static readonly HashSet<int>[] s_ActiveContactIndices;
        private static readonly HashSet<int> s_PendingDeletionContactIndices = new HashSet<int>();
        private static readonly HashSet<int> s_PendingCancellationContactIndices = new HashSet<int>();
        private static readonly HashSet<int> s_BeganDuringCurrentFrameIndices = new HashSet<int>();
#endif //UNITY_ANDROID || !UNITY_EDITOR
        
        private static readonly Queue<BoardContactEvent> s_EventQueue = new Queue<BoardContactEvent>();
        private static readonly object s_Lock = new object();
        private static BoardContactType[] s_ContactTypes = (BoardContactType [])Enum.GetValues(typeof(BoardContactType));
        private static BoardInputSettings s_Settings;

        /// <summary>
        /// Occurs when the current <see cref="BoardInputSettings"/> change.
        /// </summary>
        public static event Action settingsChanged;
        
        /// <summary>
        /// Gets or sets the current <see cref="BoardInputSettings"/>.
        /// </summary>
        /// <exception cref="ArgumentNullException">An attempt was made to set this property to <c>null</c>.</exception>
        public static BoardInputSettings settings
        {
            get => s_Settings;
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                if (s_Settings == value)
                {
                    return;
                }

#if UNITY_EDITOR
                if (!string.IsNullOrEmpty(AssetDatabase.GetAssetPath(value)))
                {
                    EditorBuildSettings.AddConfigObject(kEditorBuildSettingsConfigKey,
                        value, true);
                }
#endif
                
                s_Settings = value;
                ApplySettings();
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the <see cref="BoardInput"/> debug view is enabled.
        /// </summary>
        public static bool enableDebugView
        {
            get
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                return BoardInputDebugView.instance != null && BoardInputDebugView.instance.isActiveAndEnabled;
#else
                return false;
#endif //UNITY_EDITOR || DEVELOPMENT_BUILD
            }
            set
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (value) 
                {
                    BoardInputDebugView.Enable();
                }
                else
                {
                    BoardInputDebugView.Disable();
                }
#endif //UNITY_EDITOR || DEVELOPMENT_BUILD
            }
        }
        
        /// <summary>
        /// Static constructor for <see cref="BoardInput"/>.
        /// </summary>
        static BoardInput()
        {
#if UNITY_ANDROID || UNITY_EDITOR
            // Initialize the state array as well as the active contact indices lookup tables.
            s_State = new BoardContact[kMaxContacts];
            s_ActiveContactIndices = new HashSet<int>[s_ContactTypes.Length];
            for (var i = 0; i < s_ContactTypes.Length; i++)
            {
                s_ActiveContactIndices[i] = new HashSet<int>();
            }
#endif //UNITY_ANDROID || UNITY_EDITOR


#if UNITY_EDITOR
            if (EditorBuildSettings.TryGetConfigObject(kEditorBuildSettingsConfigKey,
                    out BoardInputSettings settingsAsset))
            {
                s_Settings = settingsAsset;
            }
            else
            {
                // Use delayCall to avoid creating assets during editor startup
                EditorApplication.delayCall += EnsureInputSettingsExists;
                // Create a temporary instance until the asset is created
                s_Settings = ScriptableObject.CreateInstance<BoardInputSettings>();
                s_Settings.hideFlags = HideFlags.HideAndDontSave;
            }

            ApplySettings();

            EditorApplication.projectChanged -= OnProjectChanged;
            EditorApplication.projectChanged += OnProjectChanged;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
#else
            if (s_Settings == null)
            {
                s_Settings = Resources.FindObjectsOfTypeAll<BoardInputSettings>().FirstOrDefault() ??
                           ScriptableObject.CreateInstance<BoardInputSettings>();
            }
#endif //UNITY_EDITOR
        }

#if UNITY_EDITOR
        /// <summary>
        /// Ensures that a BoardInputSettings asset exists, creating one if necessary.
        /// </summary>
        private static void EnsureInputSettingsExists()
        {
            // Check again in case it was created between the static constructor and this callback
            if (EditorBuildSettings.TryGetConfigObject(kEditorBuildSettingsConfigKey,
                    out BoardInputSettings existingSettings))
            {
                if (s_Settings != existingSettings)
                {
                    s_Settings = existingSettings;
                    settingsChanged?.Invoke();
                }
                return;
            }

            // Create the default settings asset
            var newSettings = BoardInputSettings.CreateInstanceAsset();
            s_Settings = newSettings;
            settingsChanged?.Invoke();

            BoardLogger.LogInfo(kLogTag, "Created default BoardInputSettings asset at Assets/Board/Settings/BoardInputSettings.asset");
        }
#endif //UNITY_EDITOR
        
        /// <summary>
        /// Gets all currently active contacts on the Board.
        /// </summary>
        /// <param name="boardContactTypes">An array of <see cref="BoardContactType">contact types</see> to filter which <see cref="BoardContact">BoardContacts</see> to return.</param>
        /// <returns>An array of currently active <see cref="BoardContact">BoardContacts</see> whose <see cref="BoardContact.type"/> is included in <paramref name="boardContactTypes"/>.</returns>
        public static BoardContact[] GetActiveContacts(params BoardContactType[] boardContactTypes)
        {
#if UNITY_ANDROID || UNITY_EDITOR
            // Exit immediately if this platform is not supported
            if (!BoardSupport.enabled)
            {
                return Array.Empty<BoardContact>();
            }
            
            // If no parameters then default to all contact types
            if (boardContactTypes.Length == 0)
            {
                boardContactTypes = s_ContactTypes;
            }
            
            // First calculate how many total contacts we are returning
            var count = 0;
            for (var i = 0; i < boardContactTypes.Length; i++)
            {
                count += s_ActiveContactIndices[(int)boardContactTypes[i]].Count;
            }

            // Now allocate an array and copy the relevant matching contact types into it
            var contacts = new BoardContact[count];
            var current = 0;
            for (var i = 0; i < boardContactTypes.Length; i++)
            {
                foreach (var activeIndex in s_ActiveContactIndices[(int)boardContactTypes[i]])
                {
                    contacts[current++] = s_State[activeIndex];
                }
            }
            
            return contacts;
#else
            return Array.Empty<BoardContact>();
#endif //UNITY_ANDROID || UNITY_EDITOR
        }
        
        /// <summary>
        /// Gets all currently active contacts on the Board.
        /// </summary>
        /// <param name="mask">A mask to filter which <see cref="BoardContact">BoardContacts</see> to return.</param>
        /// <returns>An array of currently active <see cref="BoardContact">BoardContacts</see> whose <see cref="BoardContact.type"/> matches <paramref name="mask"/>.</returns>
        public static BoardContact[] GetActiveContacts(BoardContactTypeMask mask)
        {
#if UNITY_ANDROID || UNITY_EDITOR
            // Exit immediately if this platform is not supported
            if (!BoardSupport.enabled)
            {
                return Array.Empty<BoardContact>();
            }
            
            // First calculate how many total contacts we are returning
            var count = 0;
            for (var i = 0; i < s_ActiveContactIndices.Length; i++)
            {
                if ((mask & (1 << i)) != 0)
                {
                    count += s_ActiveContactIndices[i].Count;
                }
            }
            
            // Now allocate an array and copy the relevant matching contact types into it
            var contacts = new BoardContact[count];
            var current = 0;
            for (var i = 0; i < s_ActiveContactIndices.Length; i++)
            {
                if ((mask & (1 << i)) != 0)
                {
                    foreach (var activeIndex in s_ActiveContactIndices[i])
                    {
                        contacts[current++] = s_State[activeIndex];
                    }
                }
            }

            return contacts;
#else
            return Array.Empty<BoardContact>();
#endif //UNITY_ANDROID || UNITY_EDITOR
        }
        
        /// <summary>
        /// Queues a <see cref="BoardContactEvent"/> for processing.
        /// </summary>
        /// <param name="contactEvent">A <see cref="BoardContactEvent"/>.</param>
        internal static void QueueStateEvent(BoardContactEvent contactEvent)
        {
            lock (s_Lock)
            {
                s_EventQueue.Enqueue(contactEvent);
            }
        }

        /// <summary>
        /// Gets a value indicating whether a currently active <see cref="BoardContact"/> has the provided contact identifier.
        /// </summary>
        /// <param name="contactId">A contact identifier.</param>
        /// <returns><see langword="true"/> if there is an active <see cref="BoardContact"/> whose contact identifier is
        /// <paramref name="contactId"/>; otherwise, <see langword="false"/>.</returns>
        internal static bool IsContactIdActive(int contactId)
        {
#if UNITY_ANDROID && UNITY_EDITOR
            return s_ActiveContactIndices.Any(set => set.Any(index => s_State[index].contactId == contactId));
#else
            return false;
#endif //UNITY_ANDROID && UNITY_EDITOR
        }
        
        /// <summary>
        /// Initializes the input client in the native Board input SDK.
        /// </summary>
        internal static void Initialize()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            s_NativeInputBuffer = new NativeArray<BoardContactEvent>(kNativeBufferLength, Allocator.Persistent);
            unsafe
            {
                InitializeNative(OnContactInputEvent, OnActivityStarted, OnActivityStopped,
                    NativeArrayUnsafeUtility.GetUnsafePtr(s_NativeInputBuffer),
                    s_NativeInputBuffer.Length);
            }

            ApplySettings();
            InputSystem.onBeforeUpdate += InputSystemOnBeforeUpdate;
#elif UNITY_EDITOR
            InputSystem.onBeforeUpdate += InputSystemOnBeforeUpdate;
#endif //UNITY_EDITOR
        }

#if UNITY_EDITOR
        /// <summary>
        /// Editor-only callback invoked when the state of the project changes.
        /// </summary>
        private static void OnProjectChanged()
        {
            if (s_Settings == null || EditorUtility.InstanceIDToObject(s_Settings.GetInstanceID()) == null)
            {
                var newSettings = ScriptableObject.CreateInstance<BoardInputSettings>();
                newSettings.hideFlags = HideFlags.HideAndDontSave;
                settings = newSettings;
            }
        }

        /// <summary>
        /// Editor-only callback invoked when the play mode state changes.
        /// </summary>
        private static void OnPlayModeStateChanged(PlayModeStateChange stateChange)
        {
            if (stateChange == PlayModeStateChange.ExitingPlayMode)
            {
                BoardInputDebugView.Destroy();
            }
        }
#endif //UNITY_EDITOR
        
#if UNITY_ANDROID || UNITY_EDITOR
        /// <summary>
        /// Callback invoked before the Unity <see cref="InputSystem"/> updates.
        /// </summary>
        private static void InputSystemOnBeforeUpdate()
        {
            // Don't update during the EditorApplication's update
            if (InputState.currentUpdateType == InputUpdateType.Editor)
            {
                return;
            }

            // Clear the list of contacts that began in this frame
            s_BeganDuringCurrentFrameIndices.Clear();
            
            // Iterate through the list of active contacts indices
            for (var i = 0; i < s_ActiveContactIndices.Length; i++)
            {
                // Removing all indices that were marked for deletion
                s_ActiveContactIndices[i].ExceptWith(s_PendingDeletionContactIndices);

                // Finally go through all the active indices and set their previous screen position and orientation
                foreach (var index in s_ActiveContactIndices[i])
                {
                    s_State[index].OnBeforeUpdate();
                }
            }

            s_PendingDeletionContactIndices.Clear();
            
#if UNITY_ANDROID && !UNITY_EDITOR
            var eventCount = GetEventCount(s_LastReadIndex);
            
            // Collect all the events since the last update in the shared ring buffer
            while (eventCount > 0)
            {
                s_EventQueue.Enqueue(s_NativeInputBuffer[s_LastReadIndex]);
            
                s_LastReadIndex++;
                if (s_LastReadIndex == s_NativeInputBuffer.Length)
                {
                    s_LastReadIndex = 0;
                }
            
                eventCount--;
            }
#endif //UNITY_ANDROID && !UNITY_EDITOR
            
            // Iterate through all the events that were queued since the last update
            while (s_EventQueue.Any())
            {
                var contactEvent = s_EventQueue.Dequeue();
                ProcessStateEvent(ref contactEvent);
            }

            if (s_PendingCancellationContactIndices.Count > 0)
            {
                foreach (var index in s_PendingCancellationContactIndices)
                {
                    s_State[index].phase = BoardContactPhase.Canceled;
                    s_PendingDeletionContactIndices.Add(index);
                }

                s_PendingCancellationContactIndices.Clear();
            }
        }

        /// <summary>
        /// Processes an individual <see cref="BoardContactEvent"/>.
        /// </summary>
        /// <param name="stateEvent">A <see cref="BoardContactEvent"/>.</param>
        private static unsafe void ProcessStateEvent(ref BoardContactEvent stateEvent)
        {
            Profiler.BeginSample("BoardInputProcessStateEvent");
            
            // Get the pointer to the state array
            var currentContactState = (BoardContact*)UnsafeUtility.AddressOf(ref s_State[0]);
            
            // If it's an ongoing contact, try to find the BoardContactState we have allocated to the contact previously.
            var phase = stateEvent.phase;
            if (phase != BoardContactPhase.Began)
            {
                var contactId = stateEvent.contactId;
                for (var i = 0; i < s_State.Length; ++i, ++currentContactState)
                {
                    if (currentContactState->contactId == contactId)
                    {
                        currentContactState->timestamp = Time.realtimeSinceStartupAsDouble;
                        
                        // Keep the phase of the contact state as began if the contact began in this frame.
                        if (stateEvent.phase == BoardContactPhase.Moved)
                        {
                            stateEvent.phase = s_BeganDuringCurrentFrameIndices.Contains(i)
                                ? BoardContactPhase.Began
                                : BoardContactPhase.Moved;
                        }
                        
                        UnsafeUtility.MemCpy(UnsafeUtility.AddressOf(ref currentContactState->inputEvent), UnsafeUtility.AddressOf(ref stateEvent), BoardContactEvent.kSizeInBytes);
                        
                        if (stateEvent.phase.IsEndedOrCanceled())
                        {
                            // If this contact has began and ended within the same frame do not surface it
                            if (s_BeganDuringCurrentFrameIndices.Contains(i))
                            {
                                s_BeganDuringCurrentFrameIndices.Remove(i);
                                s_ActiveContactIndices[stateEvent.typeId].Remove(i);
                            }
                            else // otherwise, mark it for deletion in the next frame so the End/Cancel event is visible for one frame 
                            {
                                s_PendingDeletionContactIndices.Add(i);
                            }
                        }
                        
                        Profiler.EndSample();
                        return;
                    }
                }
            
                // Couldn't find an existing entry. Either it was a contact that we previously ran out of available
                // entries for, or it's an event sent out of sequence. Ignore it to be consistent.
                Profiler.EndSample();
                return;
            }
            
            // It is a new contact. Try to find an unused BoardContactState.
            for (var i = 0; i < s_State.Length; ++i, ++currentContactState)
            {
                // Skip any indices that are already in the active contacts lookup tables or still pending deletion
                if (s_ActiveContactIndices[stateEvent.typeId].Contains(i) || s_PendingDeletionContactIndices.Contains(i))
                {
                    continue;
                }
                
                // NOTE: This overwrites any contact considered ended immediately even if there are later unused slots.
                // This results in a compact list of contacts (i.e. contact #N is only used if there #N contacts on the screen). 
                if (currentContactState->isNoneEndedOrCanceled)
                {
                    s_BeganDuringCurrentFrameIndices.Add(i);
                    currentContactState->previousPhase = BoardContactPhase.None;
                    currentContactState->previousScreenPosition = stateEvent.position;
                    currentContactState->previousOrientation = stateEvent.orientation;
                    currentContactState->timestamp = Time.realtimeSinceStartupAsDouble;
                    UnsafeUtility.MemCpy(UnsafeUtility.AddressOf(ref currentContactState->inputEvent),
                        UnsafeUtility.AddressOf(ref stateEvent), BoardContactEvent.kSizeInBytes);
                    s_ActiveContactIndices[stateEvent.typeId].Add(i);
                    Profiler.EndSample();
                    return;
                }
            }
            
            Profiler.EndSample();
        }
#endif //UNITY_ANDROID || UNITY_EDITOR

#if UNITY_ANDROID && !UNITY_EDITOR
        /// <summary>
        /// Cancels all active contacts.
        /// When immediate=true, contacts are set to Canceled phase and marked for deletion next frame.
        /// When immediate=false, contacts go through the normal pending cancellation queue.
        /// </summary>
        private static void CancelAllContacts(bool immediate = false)
        {
            for (var i = 0; i < s_ActiveContactIndices.Length; i++)
            {
                s_ActiveContactIndices[i].ExceptWith(s_PendingDeletionContactIndices);

                foreach (var index in s_ActiveContactIndices[i])
                {
                    // Always set phase to Canceled so debug overlay can see it
                    s_State[index].phase = BoardContactPhase.Canceled;

                    if (immediate)
                    {
                        // Mark for deletion on next frame (debug overlay will see Canceled this frame)
                        s_PendingDeletionContactIndices.Add(index);
                    }
                    else
                    {
                        // Use normal pending queue for gradual cleanup
                        s_PendingCancellationContactIndices.Add(index);
                    }
                }
            }
        }
#endif //UNITY_ANDROID && !UNITY_EDITOR        

        /// <summary>
        /// Callback invoked by the native plugin when the Android activity has started.
        /// </summary>
        [AOT.MonoPInvokeCallback(typeof(BoardActivityStartedEventHandler))]
        private static void OnActivityStarted()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            // // Force clear debug overlay crosshairs to prevent ghost contacts
            // Debug.BoardInputDebugView.Clear();

            if (!s_HasNativeInputBufferBeenDisposed)
            {
                BoardLogger.LogError(kLogTag, "Native buffer still exists after resuming activity");
                s_NativeInputBuffer.Dispose();
            }

            s_NativeInputBuffer = new NativeArray<BoardContactEvent>(kNativeBufferLength, Allocator.Persistent);

            unsafe
            {
                SetInputBuffer(NativeArrayUnsafeUtility.GetUnsafePtr(s_NativeInputBuffer),
                    s_NativeInputBuffer.Length);
            }
#endif //UNITY_ANDROID && !UNITY_EDITOR
        }

        /// <summary>
        /// Callback invoked by the native plugin when the Android activity has stopped.
        /// </summary>
        [AOT.MonoPInvokeCallback(typeof(BoardActivityStoppedEventHandler))]
        private static void OnActivityStopped()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            s_NativeInputBuffer.Dispose();
            s_HasNativeInputBufferBeenDisposed = true;
            s_LastReadIndex = 0;
            CancelAllContacts();
#endif //UNITY_ANDROID && !UNITY_EDITOR
        }
        
        /// <summary>
        /// Called by native code after it puts new events in our shared buffer.  
        /// </summary>
        /// <param name="offset">The offset in the buffer to begin reading.</param>
        /// <param name="count">The number of events that are available to read</param>
        /// <remarks>This function needs to read the new states from the buffer starting at offset, wrapping around to 0 when we hit the end.</remarks>
        [AOT.MonoPInvokeCallback(typeof(BoardContactInputEventHandler))]
        private static unsafe void OnContactInputEvent(int offset, int count)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            while (count > 0) 
            {
                lock (s_Lock)
                {
                    s_EventQueue.Enqueue(s_NativeInputBuffer[offset]);
                }

                offset++;

                if (offset == s_NativeInputBuffer.Length)
                {
                    offset = 0;
                }
            
                count--;
            }
#endif //UNITY_ANDROID && !UNITY_EDITOR
        }

        /// <summary>
        /// Apply the settings in <see cref="s_Settings"/>.
        /// </summary>
        private static void ApplySettings()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            using (AndroidJavaClass activityClass = new AndroidJavaClass(BoardSupport.kUnityPlayerClassName),
                   androidPlugin = new AndroidJavaClass(kBoardSDKClassName))
            {
                androidPlugin.CallStatic(kBoardSDKApplyInputSettingsMethodName, s_Settings.translationSmoothing,
                    s_Settings.rotationSmoothing, s_Settings.persistence, s_Settings.pieceSetModelFilename);
            }
            
            CancelAllContacts();
#endif //UNITY_ANDROID && !UNITY_EDITOR
            settingsChanged?.Invoke();
        }

        /// <summary>
        /// Initializes the native Board SDK.
        /// </summary>
        /// <param name="inputEventHandler">A delegate to be invoked when the application recieves an input event.</param>
        /// <param name="startedHandler">A delegate to be invoked when the application starts.</param>
        /// <param name="stoppedHandler">A delegate to be invoked when the application stops.</param>
        /// <param name="buffer">A pointer to the buffer.</param>
        /// <param name="bufferLength">The length of the buffer.</param>
        [DllImport(BoardSupport.kNativePluginName)]
        private static extern unsafe void InitializeNative(BoardContactInputEventHandler inputEventHandler,
            BoardActivityStartedEventHandler startedHandler,
            BoardActivityStoppedEventHandler stoppedHandler,
            void* buffer,
            int bufferLength);

        /// <summary>
        /// Sets the shared input ring buffer used by the native Board SDK.
        /// </summary>
        /// <param name="buffer">A pointer to the buffer.</param>
        /// <param name="bufferLength">The length of the buffer.</param>
        [DllImport(BoardSupport.kNativePluginName)]
        private static extern unsafe void SetInputBuffer(void* buffer, int bufferLength);
        
        /// <summary>
        /// Gets the number of input events that are in the native ring buffer since the last read index.
        /// </summary>
        /// <param name="lastReadIndex">The last index that has been read from the native ring buffer.</param>
        /// <returns>The number of input events in the native ring buffer since <paramref name="lastReadIndex"/>.</returns>
        [DllImport(BoardSupport.kNativePluginName)]
        private static extern int GetEventCount(int lastReadIndex);
    }
}