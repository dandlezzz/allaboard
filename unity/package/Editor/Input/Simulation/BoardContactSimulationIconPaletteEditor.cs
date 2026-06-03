// <copyright file="BoardContactSimulationIconPaletteEditor.cs" company="Harris Hill Products Inc.">
//     Copyright (c) Harris Hill Products Inc. All rights reserved.
// </copyright>

namespace Board.Editor.Input.Simulation
{
    using UnityEditor;
    
    using UnityEngine;
    
    /// <summary>
    /// Provides a custom editor for <see cref="BoardContactSimulationIconPalette"/>.
    /// </summary>
    [CustomEditor(typeof(BoardContactSimulationIconPalette))]
    public class BoardContactSimulationIconPaletteEditor : Editor
    {
        /// <inheritdoc cref="Editor.OnInspectorGUI"/>
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox(
                $"Simulator Icon Palettes can be edited in the Board Input Simulator.",
                MessageType.Info);
            
            EditorGUILayout.Space();
    
            if (GUILayout.Button("Open Board Input Simulator Window", GUILayout.Height(30)))
            {
                BoardContactSimulationWindow.CreateOrShow();
            }
        }
    
        /// <inheritdoc cref="Editor.ShouldHideOpenButton"/>
        protected override bool ShouldHideOpenButton()
        {
            return true;
        }
    }
}
