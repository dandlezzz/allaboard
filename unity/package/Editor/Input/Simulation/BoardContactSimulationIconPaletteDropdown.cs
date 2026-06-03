// <copyright file="BoardContactSimulationIconPaletteDropdown.cs" company="Harris Hill Products Inc.">
//     Copyright (c) Harris Hill Products Inc. All rights reserved.
// </copyright>

namespace Board.Editor.Input.Simulation
{
    using System;
    using System.IO;
    using System.Linq;
    
    using UnityEditor;
    using UnityEditor.Toolbars;
    
    using UnityEngine;
    
    /// <summary>
    /// Provides a mechanism to select the active <see cref="BoardContactSimulationIconPalette"/> in the <see cref="BoardContactSimulationWindow"/>.
    /// </summary>
    internal class BoardContactSimulationIconPaletteDropdown : EditorToolbarDropdown
    {
        /// <summary>
        /// Encapsulates all GUI content for <see cref="BoardContactSimulationIconPaletteDropdown"/>.
        /// </summary>
        private static class Styles
        {
            public static readonly string invalidFolderTitle = L10n.Tr("Cannot save to an invalid folder");
            public static readonly string invalidFolderContent = L10n.Tr("You cannot save to an invalid folder.");
            public static readonly string nonAssetFolderTitle = L10n.Tr("Cannot save to a non-asset folder");
            public static readonly string nonAssetFolderContent = L10n.Tr("You cannot save to a non-asset folder.");
            public static readonly string readOnlyFolderTitle = L10n.Tr("Cannot save to a read-only path");
            public static readonly string readOnlyFolderContent = L10n.Tr("You cannot save to a read-only path.");
            public static readonly string ok = L10n.Tr("OK");
        }

        private static readonly string[] kBuiltinPalettes = new string[]
        {
            "Packages/fun.board/Editor/Assets/Simulation/Palettes/BoardArcade.asset",
            "Packages/fun.board/Editor/Assets/Simulation/Palettes/ChopChop.asset",
            "Packages/fun.board/Editor/Assets/Simulation/Palettes/Mushka.asset",
            "Packages/fun.board/Editor/Assets/Simulation/Palettes/Omakase.asset",
            "Packages/fun.board/Editor/Assets/Simulation/Palettes/SaveTheBloogs.asset",
            "Packages/fun.board/Editor/Assets/Simulation/Palettes/Thrasos.asset",
        };
        
        /// <summary>
        /// Occurs when the currently selected <see cref="BoardContactSimulationIconPalette"/> is changed.
        /// </summary>
        internal event Action<BoardContactSimulationIconPalette> paletteSelectionChanged;
        
        private BoardContactSimulationIconPalette m_CurrentPalette;
        
        /// <summary>
        /// Initializes a new instance of the <see cref="BoardContactSimulationIconPaletteDropdown"/> class with the
        /// specified initially selected <see cref="BoardContactSimulationIconPalette"/>.
        /// </summary>
        /// <param name="palette">The initially selected <see cref="BoardContactSimulationIconPalette"/>.</param>
        public BoardContactSimulationIconPaletteDropdown(BoardContactSimulationIconPalette palette)
        {
            m_CurrentPalette = palette;
            text = palette.name;
            clicked += OnClicked;
        }

        /// <summary>
        /// Callback invoked when the dropdown is clicked.
        /// </summary>
        private void OnClicked()
        {
            var menu = new GenericMenu();
            var palettePaths = kBuiltinPalettes.Union(FindPalettesInProject());
            foreach (var palettePath in palettePaths)
            {
                var palette = AssetDatabase.LoadAssetAtPath<BoardContactSimulationIconPalette>(palettePath);
                if (palette != null)
                {
                    menu.AddItem(new GUIContent(palette.name), false, () =>
                    {
                        SwitchSelectedPalette(palette);
                    });
                }
            }
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Add new palette ..."), false, CreateNewPalette);
            menu.DropDown(worldBound);
        }

        /// <summary>
        /// Switches the currently selected <see cref="BoardContactSimulationIconPalette"/> to a new palette.
        /// </summary>
        /// <param name="palette">The newly selected <see cref="BoardContactSimulationIconPalette"/>.</param>
        internal void SwitchSelectedPalette(BoardContactSimulationIconPalette palette)
        {
            if (m_CurrentPalette == palette)
            {
                return;
            }
            
            m_CurrentPalette = palette;
            text = m_CurrentPalette.name;
            paletteSelectionChanged?.Invoke(m_CurrentPalette);
        }

        /// <summary>
        /// Creates a new <see cref="BoardContactSimulationIconPalette"/> and saves it to disk.
        /// </summary>
        private void CreateNewPalette()
        {
            var folderPath = EditorUtility.SaveFilePanelInProject("Create palette into folder", "New Palette", "asset",
                string.Empty);

            if (!ValidateSavePath(folderPath, out var filename))
                return;

            var newPalette = ScriptableObject.CreateInstance<BoardContactSimulationIconPalette>();
            newPalette.name = "New Palette";
            AssetDatabase.CreateAsset(newPalette, folderPath);
            SwitchSelectedPalette(newPalette);
        }

        /// <summary>
        /// Determines whether the specified file path is valid to save an asset.
        /// </summary>
        /// <param name="filePath">The full path to the destination file.</param>
        /// <param name="filename">When this method returns, contains the filename of the destination file if valid; otherwise, an empty string.</param>
        /// <returns><c>true</c> if an asset can be saved to <paramref name="filePath"/>; otherwise, <c>false</c>.</returns>
        private static bool ValidateSavePath(string filePath, out string filename)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                EditorUtility.DisplayDialog(Styles.invalidFolderTitle, Styles.invalidFolderContent, Styles.ok);
                filename = string.Empty;
                return false;
            }

            var fileInfo = new FileInfo(filePath);
            if (fileInfo.Directory == null || !fileInfo.Directory.Exists)
            {
                EditorUtility.DisplayDialog(Styles.invalidFolderTitle, Styles.invalidFolderContent, Styles.ok);
                filename = string.Empty;
                return false;
            }

            filePath = fileInfo.Directory.FullName;
            var assetDirectory = Application.dataPath;
            var assetDirectoryInfo = new DirectoryInfo(assetDirectory);
            var destinationDirectoryInfo = new DirectoryInfo(filePath);
            
            if (!destinationDirectoryInfo.FullName.StartsWith(assetDirectoryInfo.FullName))
            {
                EditorUtility.DisplayDialog(Styles.nonAssetFolderTitle, Styles.nonAssetFolderContent, Styles.ok);
                filename = string.Empty;
                return false;
            }

            if (destinationDirectoryInfo.Attributes.HasFlag(FileAttributes.ReadOnly))
            {
                EditorUtility.DisplayDialog(Styles.readOnlyFolderTitle, Styles.readOnlyFolderContent, Styles.ok);
                filename = string.Empty;
                return false;
            }

            filename = fileInfo.Name;
            return true;
        }

        /// <summary>
        /// Finds all <see cref="BoardContactSimulationIconPalette"/> assets stored in the current project.
        /// </summary>
        /// <returns>List of simulator icon palettes in project.</returns>
        private static string[] FindPalettesInProject()
        {
            var guids = AssetDatabase.FindAssets("t:BoardContactSimulationIconPalette");
            return guids.Select(guid => AssetDatabase.GUIDToAssetPath(guid)).ToArray();
        }
    }
}
