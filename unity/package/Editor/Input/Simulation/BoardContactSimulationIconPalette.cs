// <copyright file="BoardContactSimulationIconPalette.cs" company="Harris Hill Products Inc.">
//     Copyright (c) Harris Hill Products Inc. All rights reserved.
// </copyright>

namespace Board.Editor.Input.Simulation
{
    using System;
    using System.IO;
    using System.Collections.Generic;
    
    using Board.Input.Simulation;
    
    using UnityEditor;
    
    using UnityEngine;
    
    /// <summary>
    /// Represents a palette of <see cref="BoardContactSimulationIcon">icons</see> that are used for input simulation.
    /// </summary>
    internal class BoardContactSimulationIconPalette : ScriptableObject
    {
        [SerializeField] private List<BoardContactSimulationIcon> m_Icons = new List<BoardContactSimulationIcon>();

        private static readonly string[] kDefaultIconAssetPaths =
        {
            "Packages/fun.board/Editor/Assets/Simulation/Icons/Finger.asset",
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
        };

        /// <summary>
        /// Gets or sets a value indicating whether this <see cref="BoardContactSimulationIconPalette"/> can be edited.
        /// </summary>
        internal bool canEdit => AssetDatabase.GetAssetPath(this).StartsWith("Assets" + Path.DirectorySeparatorChar);

        /// <summary>
        /// Gets an array of <see cref="BoardContactSimulationIcon">BoardContactSimulationIcons</see> comprising the palette.
        /// </summary>
        internal BoardContactSimulationIcon[] icons => m_Icons.ToArray();
        
        /// <summary>
        /// Sets the <see cref="BoardContactSimulationIcon"/> at the specified index.
        /// </summary>
        /// <param name="index">The index to update.</param>
        /// <param name="icon">A <see cref="BoardContactSimulationIcon"/>.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is less than 0 or greater than the number of icon slots.</exception>
        internal void SetIcon(int index, BoardContactSimulationIcon icon)
        {
            if (index < 0 || index >= m_Icons.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }
        
            m_Icons[index] = icon;
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssetIfDirty(this);
        }

        /// <summary>
        /// Resets the palette to its default state.
        /// </summary>
        internal void Reset()
        {
            m_Icons.Clear();
            
            foreach (var iconAssetPath in kDefaultIconAssetPaths)
            {
                var icon = AssetDatabase.LoadAssetAtPath<BoardContactSimulationIcon>(iconAssetPath);
            
                if (icon == null && !string.IsNullOrEmpty(iconAssetPath))
                {
                    Debug.LogError(
                        $"[Board] Could not load default BoardContactSimulationIcon at asset path: {iconAssetPath}");
                }
                
                m_Icons.Add(icon);
            }
            
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssetIfDirty(this);
        }
        
        /// <summary>
        /// Inserts an empty item to the icons list at the specified position.
        /// </summary>
        /// <param name="index">The zero-based index at which the empty item is to be inserted.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is less than 0 or greater than the length of the icons list.</exception>
        internal void InsertAt(int index)
        {
            if (index < 0 || index > m_Icons.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }
            
            m_Icons.Insert(index, null);
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssetIfDirty(this);
        }

        /// <summary>
        /// Removes the icon at the specified index of the icons list.
        /// </summary>
        /// <param name="index">The zero-based index of the item to remove.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is less than 0 or greater than the length of the icons list.</exception>
        internal void RemoveAt(int index)
        {
            if (index < 0 || index >= m_Icons.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }
            
            m_Icons.RemoveAt(index);
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssetIfDirty(this);
        }

        /// <summary>
        /// Finds a <see cref="BoardContactSimulationIconPalette"/> by name.
        /// </summary>
        /// <param name="paletteName">The name of the palette to find.</param>
        /// <exception cref="ArgumentException"><paramref name="paletteName"/> is null or empty.</exception>
        /// <returns>The found <see cref="BoardContactSimulationIconPalette"/>, or null if not found.</returns>
        internal static BoardContactSimulationIconPalette FindPaletteByName(string paletteName)
        {
            if (string.IsNullOrEmpty(paletteName))
            {
                throw new ArgumentException("Palette name cannot be null or empty.", nameof(paletteName));
            }

            var guids = AssetDatabase.FindAssets($"{paletteName} t:{nameof(BoardContactSimulationIconPalette)}");

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var palette = AssetDatabase.LoadAssetAtPath<BoardContactSimulationIconPalette>(path);

                if (palette != null && string.Equals(palette.name, paletteName, StringComparison.OrdinalIgnoreCase))
                {
                    return palette;
                }
            }

            return null;
        }
    }
}
