// <copyright file="BoardUIInputModuleSetup.cs" company="Harris Hill Products Inc.">
//     Copyright (c) Harris Hill Products Inc. All rights reserved.
// </copyright>

namespace Board.Editor.Input
{
    using System.Collections.Generic;
    using System.Linq;

    using Board.Input;

    using UnityEditor;
    using UnityEditor.Build;
    using UnityEditor.Build.Reporting;
    using UnityEngine;
    using UnityEngine.EventSystems;
    using UnityEngine.InputSystem.UI;
    using UnityEngine.SceneManagement;

#if UNITY_ANDROID
    /// <summary>
    /// Warns at build time if EventSystems are missing BoardUIInputModule.
    /// </summary>
    /// <remarks>
    /// <para>
    /// On Board hardware, the SDK blocks system-level touch events, so InputSystemUIInputModule
    /// will not receive input. BoardUIInputModule is required for UI to respond to touch.
    /// </para>
    /// <para>
    /// This is a warning (not an error) because some projects add BoardUIInputModule at runtime
    /// rather than configuring it in the scene ahead of time.
    /// </para>
    /// </remarks>
    internal class BoardUIInputModuleBuildValidator : IProcessSceneWithReport
    {
        /// <summary>
        /// Gets the relative callback order. Runs after other Board validators.
        /// </summary>
        public int callbackOrder => 2;

        /// <summary>
        /// Called for each scene during the build process.
        /// </summary>
        /// <param name="scene">The scene being processed.</param>
        /// <param name="report">The build report.</param>
        public void OnProcessScene(Scene scene, BuildReport report)
        {
            // Skip if not building (e.g., entering play mode)
            if (report == null)
            {
                return;
            }

            var eventSystemsMissingModule = FindEventSystemsMissingBoardModule(scene);

            if (eventSystemsMissingModule.Count > 0)
            {
                var missing = eventSystemsMissingModule.Where(x => !x.hasDisabledModule).Select(x => x.eventSystem).ToList();
                var disabled = eventSystemsMissingModule.Where(x => x.hasDisabledModule).Select(x => x.eventSystem).ToList();

                var message = $"[Board SDK] Scene \"{scene.name}\" has EventSystem(s) without BoardUIInputModule. " +
                              "UI may not respond to touch on Board hardware.\n";

                if (missing.Count > 0)
                {
                    var names = string.Join(", ", missing.Select(es => $"\"{es.name}\""));
                    message += $"Missing BoardUIInputModule: {names}\n";
                }

                if (disabled.Count > 0)
                {
                    var names = string.Join(", ", disabled.Select(es => $"\"{es.name}\""));
                    message += $"BoardUIInputModule is disabled: {names}\n";
                }

                message += "\nIf you add BoardUIInputModule at runtime, you can ignore this warning. " +
                           "Otherwise, use Board > Input > Add BoardUIInputModule to EventSystems.\n" +
                           "See: https://docs.dev.board.fun/getting-started/project-setup#using-boarduiinputmodule";

                Debug.LogWarning(message);
            }

            // Check for dual-module conflict
            CheckForDualModuleConflict(scene);
        }

        /// <summary>
        /// Checks for EventSystems that have both BoardUIInputModule and InputSystemUIInputModule enabled.
        /// Unity's EventSystem only uses one input module at a time (whichever initializes first),
        /// so having both enabled can cause unpredictable behavior.
        /// </summary>
        private static void CheckForDualModuleConflict(Scene scene)
        {
            var rootObjects = scene.GetRootGameObjects();
            foreach (var root in rootObjects)
            {
                var eventSystems = root.GetComponentsInChildren<EventSystem>(true);
                foreach (var eventSystem in eventSystems)
                {
                    var boardModule = eventSystem.GetComponent<BoardUIInputModule>();
                    var inputSystemModule = eventSystem.GetComponent<InputSystemUIInputModule>();

                    if (boardModule != null && boardModule.enabled &&
                        inputSystemModule != null && inputSystemModule.enabled)
                    {
                        Debug.Log(
                            $"[Board SDK] EventSystem '{eventSystem.gameObject.name}' in scene '{scene.name}' " +
                            "has both BoardUIInputModule and InputSystemUIInputModule enabled.\n\n" +
                            "BoardUIInputModule will automatically disable competing input modules on Board hardware. " +
                            "Both modules can coexist in the scene for Editor compatibility.\n" +
                            "See: https://docs.dev.board.fun/getting-started/project-setup#ui-input");
                    }
                }
            }
        }

        /// <summary>
        /// Finds EventSystems in the scene that are missing an enabled BoardUIInputModule.
        /// </summary>
        private static List<(EventSystem eventSystem, bool hasDisabledModule)> FindEventSystemsMissingBoardModule(Scene scene)
        {
            var result = new List<(EventSystem, bool)>();

            var rootObjects = scene.GetRootGameObjects();
            foreach (var root in rootObjects)
            {
                var eventSystems = root.GetComponentsInChildren<EventSystem>(true);
                foreach (var eventSystem in eventSystems)
                {
                    var boardModule = eventSystem.GetComponent<BoardUIInputModule>();
                    var hasBoardModuleEnabled = boardModule != null && boardModule.enabled;

                    if (!hasBoardModuleEnabled)
                    {
                        var hasDisabledModule = boardModule != null && !boardModule.enabled;
                        result.Add((eventSystem, hasDisabledModule));
                    }
                }
            }

            return result;
        }
    }
#endif // UNITY_ANDROID

    /// <summary>
    /// Provides menu items for adding BoardUIInputModule to EventSystems.
    /// </summary>
    internal static class BoardUIInputModuleSetup
    {
        /// <summary>
        /// Finds all EventSystems in open scenes that need BoardUIInputModule added or enabled.
        /// </summary>
        /// <returns>List of EventSystems that need setup, with info about whether they have a disabled module.</returns>
        internal static List<(EventSystem eventSystem, bool hasDisabledModule)> FindEventSystemsNeedingSetup()
        {
            var result = new List<(EventSystem, bool)>();

            var eventSystems = Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None);
            foreach (var eventSystem in eventSystems)
            {
                var boardModule = eventSystem.GetComponent<BoardUIInputModule>();
                var hasBoardModuleEnabled = boardModule != null && boardModule.enabled;

                if (!hasBoardModuleEnabled)
                {
                    var hasDisabledModule = boardModule != null && !boardModule.enabled;
                    result.Add((eventSystem, hasDisabledModule));
                }
            }

            return result;
        }

        /// <summary>
        /// Adds or enables BoardUIInputModule on EventSystems.
        /// </summary>
        /// <param name="eventSystems">The EventSystems to set up, with info about disabled modules.</param>
        internal static void SetupEventSystems(List<(EventSystem eventSystem, bool hasDisabledModule)> eventSystems)
        {
            var addedCount = 0;
            var enabledCount = 0;

            foreach (var (eventSystem, hasDisabledModule) in eventSystems)
            {
                Undo.SetCurrentGroupName("Add BoardUIInputModule");

                if (hasDisabledModule)
                {
                    // Enable existing disabled module
                    var boardModule = eventSystem.GetComponent<BoardUIInputModule>();
                    Undo.RecordObject(boardModule, "Enable BoardUIInputModule");
                    boardModule.enabled = true;
                    enabledCount++;
                }
                else
                {
                    // Add new module
                    Undo.AddComponent<BoardUIInputModule>(eventSystem.gameObject);
                    addedCount++;
                }

                EditorUtility.SetDirty(eventSystem.gameObject);
            }

            if (addedCount > 0 || enabledCount > 0)
            {
                var message = "[Board SDK] ";
                if (addedCount > 0)
                {
                    message += $"Added BoardUIInputModule to {addedCount} EventSystem(s). ";
                }
                if (enabledCount > 0)
                {
                    message += $"Enabled BoardUIInputModule on {enabledCount} EventSystem(s).";
                }
                Debug.Log(message);
            }
        }

        /// <summary>
        /// Menu item to add BoardUIInputModule to EventSystems.
        /// </summary>
        [MenuItem("Board/Input/Add BoardUIInputModule to EventSystems", priority = 2111)]
        private static void AddBoardUIInputModuleMenuItem()
        {
            var eventSystems = FindEventSystemsNeedingSetup();

            if (eventSystems.Count == 0)
            {
                EditorUtility.DisplayDialog(
                    "Board SDK",
                    "All EventSystems in the open scene(s) already have BoardUIInputModule enabled.",
                    "OK");
                return;
            }

            var missing = eventSystems.Where(x => !x.hasDisabledModule).Select(x => x.eventSystem).ToList();
            var disabled = eventSystems.Where(x => x.hasDisabledModule).Select(x => x.eventSystem).ToList();

            var message = "The following changes will be made:\n\n";
            if (missing.Count > 0)
            {
                var names = string.Join("\n  - ", missing.Select(es => $"{es.name} ({es.gameObject.scene.name})"));
                message += $"Add BoardUIInputModule to:\n  - {names}\n\n";
            }
            if (disabled.Count > 0)
            {
                var names = string.Join("\n  - ", disabled.Select(es => $"{es.name} ({es.gameObject.scene.name})"));
                message += $"Enable BoardUIInputModule on:\n  - {names}\n\n";
            }
            message += "This allows UI to respond to touch input on Board hardware.";

            var confirm = EditorUtility.DisplayDialog(
                "Board SDK - Add BoardUIInputModule",
                message,
                "Continue",
                "Cancel");

            if (confirm)
            {
                SetupEventSystems(eventSystems);
            }
        }

        /// <summary>
        /// Validates the menu item.
        /// </summary>
        [MenuItem("Board/Input/Add BoardUIInputModule to EventSystems", validate = true, priority = 2111)]
        private static bool AddBoardUIInputModuleMenuItemValidate()
        {
            return !EditorApplication.isPlaying;
        }
    }
}
