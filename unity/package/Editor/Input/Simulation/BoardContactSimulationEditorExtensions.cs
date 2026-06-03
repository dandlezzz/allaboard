// <copyright file="BoardContactSimulationEditorExtensions.cs" company="Harris Hill Products Inc.">
//     Copyright (c) Harris Hill Products Inc. All rights reserved.
// </copyright>

namespace Board.Editor.Input.Simulation
{
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// Helper utilities for interacting with the simulator editor window.
    /// </summary>
    public static class BoardContactSimulationEditorExtensions
    {
        /// <summary>
        /// Ensures the simulator window exists and switches to the palette by name.
        /// </summary>
        /// <param name="paletteName">The palette asset name to switch to.</param>
        public static void SwitchToPalette(string paletteName)
        {
            if (string.IsNullOrEmpty(paletteName))
            {
                Debug.LogError("[BoardContactSimulationEditorExtensions] Palette name cannot be null or empty.");
                return;
            }

            var window = GetOrCreateWindow();
            if (window == null)
            {
                Debug.LogError("[BoardContactSimulationEditorExtensions] Could not create or find BoardContactSimulationWindow.");
                return;
            }

            window.SwitchToPaletteByName(paletteName);
        }

        private static BoardContactSimulationWindow GetOrCreateWindow()
        {
            BoardContactSimulationWindow.CreateOrShow();
            return EditorWindow.GetWindow<BoardContactSimulationWindow>();
        }
    }
}
