// <copyright file="BoardContactSimulationWindow.cs" company="Harris Hill Products Inc.">
//     Copyright (c) Harris Hill Products Inc. All rights reserved.
// </copyright>

namespace Board.Editor.Input.Simulation
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using Board.Input.Simulation;

    using UnityEditor;
    using UnityEditor.UIElements;
    using UnityEditor.Toolbars;

    using UnityEngine;
    using UnityEngine.UIElements;

    /// <summary>
    /// Custom editor window for Board input simulation.
    /// </summary>
    internal class BoardContactSimulationWindow : EditorWindow
    {
        /// <summary>
        /// Encapsulates all GUI content for <see cref="BoardContactSimulationWindow"/>.
        /// </summary>
        private static class Styles
        {
            public static readonly GUIContent titleContent = new GUIContent("Board Simulator");

            public static readonly string styleSheetPath = "Simulation/Styles/BoardContactSimulatorWindow.uss";
            public static readonly string darkStyleSheetPath = "Simulation/Styles/BoardContactSimulatorWindowDark.uss";
            public static readonly string lightStyleSheetPath =
                "Simulation/Styles/BoardContactSimulatorWindowLight.uss";

            public static readonly string toolbarButtonUssClassName = "simulation-toolbar-button";
            public static readonly string toolbarToggleUssClassName = "simulation-toolbar-toggle";
            public static readonly string toolbarSpacerUssClassName = "simulation-toolbar-spacer";
            
            public static readonly string mouseAsFingerToggleOnIconDarkAssetPath = "Simulation/Images/mouse_on_d.png";
            public static readonly string mouseAsFingerToggleOnIconLightAssetPath = "Simulation/Images/mouse_on.png";
            public static readonly string mouseAsFingerToggleOffIconDarkAssetPath = "Simulation/Images/mouse_off_d.png";
            public static readonly string mouseAsFingerToggleOffIconLightAssetPath = "Simulation/Images/mouse_off.png";
            public static readonly string mouseAsFingerToggleTooltip = "Use the mouse as a simulated finger";
            
            public static readonly string deleteButtonIconLightAssetPath = "Simulation/Images/trash.png";
            public static readonly string deleteButtonIconDarkAssetPath = "Simulation/Images/trash_d.png";
            public static readonly string deleteButtonTooltip = "Delete icon from palette";

            public static readonly string resetButtonIconLightAssetPath = "Simulation/Images/reset.png";
            public static readonly string resetButtonIconDarkAssetPath = "Simulation/Images/reset_d.png";
            public static readonly string resetButtonTooltip = "Reset to default palette";

            public static readonly string clearButtonIconLightAssetPath = "Simulation/Images/clear.png";
            public static readonly string clearButtonIconDarkAssetPath = "Simulation/Images/clear_d.png";
            public static readonly string clearButtonTooltip = "Clear all simulated contacts";

            public static readonly string addIconCellButtonIconName = "Toolbar Plus";
            public static readonly string addIconCellButtonTooltip = "Add palette cell";
            
            public static readonly string deleteIconCellButtonIconName = "Toolbar Minus";
            public static readonly string deleteIconCellButtonTooltip = "Delete palette cell";
            
            public static readonly string settingsButtonIconName = "SettingsIcon";
            public static readonly string settingsButtonTooltip = "Open settings menu";

            public static readonly string paletteButtonUssClassName = "contact-palette";

            public static readonly string enableSimulationText = "Enable Simulation";
            public static readonly string disableSimulationText = "Disable Simulation";

            public static readonly string resetDialogTitle = "Reset to default?";

            public static readonly string resetDialogMessage =
                "This action will reset your simulator icons to the default.\n\nYou cannot undo this action.";

            public static readonly string resetDialogOkText = "Reset";
            public static readonly string resetDialogCancelText = "Cancel";
        }

        [SerializeField] private int m_SelectedCellIndex = -1;
        private EditorToolbarToggle m_SimulationEnabledToggle;
        private BoardContactSimulationIconPaletteDropdown m_IconPaletteDropdown;
        private EditorToolbarToggle m_MouseAsFingerIconToggle;
        private EditorToolbarButton m_ClearAllContactsButton;
        private EditorToolbarButton m_ResetPaletteButton;
        private EditorToolbarButton m_DeleteIconButton;
        private EditorToolbarButton m_AddIconCellButton;
        private EditorToolbarButton m_DeleteIconCellButton;
        private ScrollView m_ScrollView;
        private List<BoardContactSimulationIconPaletteCell> m_PaletteCells =
            new List<BoardContactSimulationIconPaletteCell>();
        private BoardContactSimulationIconPaletteCell m_SelectedPaletteCell;
        private BoardContactSimulationIconPalette m_CurrentIconPalette;

        private static BoardContactSimulationWindow s_Instance;

        private const string kSimulationStateKey = "Board_InputSimulationEnabled";
        private const string kCurrentIconPaletteKey = "Board_InputCurrentIconPalette";
        private const string kMouseAsFingerStateKey = "Board_InputUseMouseAsFingerEnabled";
        private const string kDefaultIconPalettePath = "Simulation/Palettes/BoardArcade.asset";

        /// <summary>
        /// Gets or sets a value indicating whether Board input simulation is enabled.
        /// </summary>
        private bool isSimulationEnabled
        {
            get => EditorPrefs.GetBool(kSimulationStateKey);
            set => EditorPrefs.SetBool(kSimulationStateKey, value);
        }
        
        /// <summary>
        /// Gets or sets a value indicating whether to use the mouse as a finger is enabled.
        /// </summary>
        private bool isMouseAsFingerEnabled
        {
            get => EditorPrefs.GetBool(kMouseAsFingerStateKey);
            set => EditorPrefs.SetBool(kMouseAsFingerStateKey, value);
        }
        
        /// <summary>
        /// Gets or sets the GUID of the current <see cref="BoardContactSimulationIconPalette"/>.
        /// </summary>
        private string currentIconPaletteGuid
        {
            get => EditorPrefs.GetString(kCurrentIconPaletteKey);
            set => EditorPrefs.SetString(kCurrentIconPaletteKey, value);
        }

        /// <summary>
        /// Gets or sets the currently selected <see cref="BoardContactSimulationIconPaletteCell"/>.
        /// </summary>
        private BoardContactSimulationIconPaletteCell selectedPaletteCell
        {
            get => m_SelectedPaletteCell;
            set
            {
                m_SelectedPaletteCell = value;
                if (BoardContactSimulation.instance != null)
                {
                    BoardContactSimulation.instance.currentIcon = m_SelectedPaletteCell?.icon;
                }

                m_SelectedCellIndex = value != null ? m_PaletteCells.IndexOf(selectedPaletteCell) : -1;
                m_DeleteIconButton.SetEnabled(value != null && value.icon != null && m_CurrentIconPalette != null && m_CurrentIconPalette.canEdit);
            }
        }

        /// <summary>
        /// Callback invoked by Unity when the script is enabled.
        /// </summary>
        private void OnEnable()
        {
            LoadCurrentIconPalette();
            RegisterCallbacks();
            if (EditorApplication.isPlaying && isSimulationEnabled)
            {
                EnableSimulation();
            }
        }

        /// <summary>
        /// Callback invoked by Unity when the script is destroyed.
        /// </summary>
        private void OnDestroy()
        {
            UnregisterCallbacks();
        }

        /// <summary>
        /// Callback invoked by Unity when creating the GUI for the window.
        /// </summary>
        private void CreateGUI()
        {
            var styleSheet = LoadLocalPackageAsset<StyleSheet>(Styles.styleSheetPath, true);
            if (styleSheet != null)
            {
                rootVisualElement.styleSheets.Add(styleSheet);
            }

            var lightOrDarkStyleSheet = LoadLocalPackageAsset<StyleSheet>(
                EditorGUIUtility.isProSkin ? Styles.darkStyleSheetPath : Styles.lightStyleSheetPath, true);
            if (lightOrDarkStyleSheet != null)
            {
                rootVisualElement.styleSheets.Add(lightOrDarkStyleSheet);
            }

            var toolbar = new Toolbar();

            m_SimulationEnabledToggle = new EditorToolbarToggle
                { text = isSimulationEnabled ? Styles.disableSimulationText : Styles.enableSimulationText };
            m_SimulationEnabledToggle.value = isSimulationEnabled;
            m_SimulationEnabledToggle.RegisterValueChangedCallback(ToggleSimulation);
            m_SimulationEnabledToggle.AddToClassList(Styles.toolbarToggleUssClassName);
            toolbar.Add(m_SimulationEnabledToggle);

            toolbar.Add(new ToolbarSpacer());

            m_IconPaletteDropdown = new BoardContactSimulationIconPaletteDropdown(m_CurrentIconPalette);
            m_IconPaletteDropdown.paletteSelectionChanged += OnIconPaletteChanged;
            toolbar.Add(m_IconPaletteDropdown);

            var onTexture = LoadLocalPackageAsset<Texture2D>(
                EditorGUIUtility.isProSkin
                    ? Styles.mouseAsFingerToggleOnIconDarkAssetPath
                    : Styles.mouseAsFingerToggleOnIconLightAssetPath, false);
            var offTexture = LoadLocalPackageAsset<Texture2D>(
                EditorGUIUtility.isProSkin
                    ? Styles.mouseAsFingerToggleOffIconDarkAssetPath
                    : Styles.mouseAsFingerToggleOffIconLightAssetPath, false);

            m_MouseAsFingerIconToggle = new EditorToolbarToggle(string.Empty, onTexture, offTexture)
                { tooltip = Styles.mouseAsFingerToggleTooltip };
            m_MouseAsFingerIconToggle.value = isMouseAsFingerEnabled;
            m_MouseAsFingerIconToggle.AddToClassList(Styles.toolbarToggleUssClassName);
            m_MouseAsFingerIconToggle.RegisterValueChangedCallback(ToggleMouseAsFinger);
            toolbar.Add(m_MouseAsFingerIconToggle);

            var texture = LoadLocalPackageAsset<Texture2D>(
                EditorGUIUtility.isProSkin ? Styles.clearButtonIconDarkAssetPath : Styles.clearButtonIconLightAssetPath,
                false);
            m_ClearAllContactsButton = new EditorToolbarButton(string.Empty, texture, OnClearAllContactsClick)
                { tooltip = Styles.clearButtonTooltip };
            m_ClearAllContactsButton.AddToClassList(Styles.toolbarButtonUssClassName);
            m_ClearAllContactsButton.SetEnabled(BoardContactSimulation.instance != null);
            toolbar.Add(m_ClearAllContactsButton);

            var toolbarSpacer = new ToolbarSpacer();
            toolbarSpacer.AddToClassList(Styles.toolbarSpacerUssClassName);
            toolbar.Add(toolbarSpacer);

            texture = LoadLocalPackageAsset<Texture2D>(
                EditorGUIUtility.isProSkin ? Styles.resetButtonIconDarkAssetPath : Styles.resetButtonIconLightAssetPath,
                false);
            m_ResetPaletteButton = new EditorToolbarButton(string.Empty, texture, OnResetButtonClick)
                { tooltip = Styles.resetButtonTooltip };
            m_ResetPaletteButton.SetEnabled(m_CurrentIconPalette.canEdit);
            m_ResetPaletteButton.AddToClassList(Styles.toolbarButtonUssClassName);
            toolbar.Add(m_ResetPaletteButton);

            texture = LoadLocalPackageAsset<Texture2D>(
                EditorGUIUtility.isProSkin
                    ? Styles.deleteButtonIconDarkAssetPath
                    : Styles.deleteButtonIconLightAssetPath, false);
            m_DeleteIconButton = new EditorToolbarButton(string.Empty, texture, OnDeleteButtonClick)
                { tooltip = Styles.deleteButtonTooltip };
            m_DeleteIconButton.AddToClassList(Styles.toolbarButtonUssClassName);
            m_DeleteIconButton.SetEnabled(false);
            toolbar.Add(m_DeleteIconButton);

            m_AddIconCellButton = new EditorToolbarButton(string.Empty,
                    EditorGUIUtility.FindTexture(Styles.addIconCellButtonIconName), OnAddIconCellClick)
                { tooltip = Styles.addIconCellButtonTooltip };
            m_AddIconCellButton.AddToClassList(Styles.toolbarButtonUssClassName);
            m_AddIconCellButton.SetEnabled(m_CurrentIconPalette.canEdit);
            toolbar.Add(m_AddIconCellButton);

            m_DeleteIconCellButton = new EditorToolbarButton(string.Empty,
                    EditorGUIUtility.FindTexture(Styles.deleteIconCellButtonIconName), OnDeleteIconCellClick)
                { tooltip = Styles.deleteIconCellButtonTooltip };
            m_DeleteIconCellButton.AddToClassList(Styles.toolbarButtonUssClassName);
            m_DeleteIconCellButton.SetEnabled(m_CurrentIconPalette.canEdit);
            toolbar.Add(m_DeleteIconCellButton);

            toolbar.Add(new ToolbarSpacer());

            var settingsButton = new EditorToolbarButton(string.Empty,
                    EditorGUIUtility.FindTexture(Styles.settingsButtonIconName), OnSettingsClick)
                { tooltip = Styles.settingsButtonTooltip };
            settingsButton.AddToClassList(Styles.toolbarButtonUssClassName);
            toolbar.Add(settingsButton);

            EditorToolbarUtility.SetupChildrenAsButtonStrip(toolbar);
            rootVisualElement.Add(toolbar);

            m_ScrollView = new ScrollView();
            m_ScrollView.AddToClassList(Styles.paletteButtonUssClassName);
            m_ScrollView.contentContainer.style.flexDirection = FlexDirection.Row;
            m_ScrollView.contentContainer.style.flexWrap = Wrap.Wrap;
            CreatePaletteIconCells();

            rootVisualElement.Add(m_ScrollView);
        }

        /// <summary>
        /// Loads the current <see cref="BoardContactSimulationIconPalette"/> using the GUID stored in <see cref="EditorPrefs"/>.
        /// </summary>
        private void LoadCurrentIconPalette()
        {
            var guid = currentIconPaletteGuid;
            if (string.IsNullOrEmpty(guid))
            {
                m_CurrentIconPalette =
                    LoadLocalPackageAsset<BoardContactSimulationIconPalette>(kDefaultIconPalettePath, true);
                return;
            }

            var assetPath = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(assetPath))
            {
                m_CurrentIconPalette =
                    LoadLocalPackageAsset<BoardContactSimulationIconPalette>(kDefaultIconPalettePath, true);
                return;
            }

            m_CurrentIconPalette = AssetDatabase.LoadAssetAtPath<BoardContactSimulationIconPalette>(assetPath);
            if (m_CurrentIconPalette == null)
            {
                m_CurrentIconPalette =
                    LoadLocalPackageAsset<BoardContactSimulationIconPalette>(kDefaultIconPalettePath, true);
            }
            
            currentIconPaletteGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(m_CurrentIconPalette));
        }


        /// <summary>
        /// Switches to the <see cref="BoardContactSimulationIconPalette"/> with the given name.
        /// </summary>
        /// <param name="paletteName">The name of the palette to switch to.</param
        public void SwitchToPaletteByName(string paletteName)
        {
            BoardContactSimulationIconPalette newPalette = BoardContactSimulationIconPalette.FindPaletteByName(paletteName);
            if (newPalette == null)
            {
                Debug.LogError($"[BoardContactSimulationWindow] Could not find palette with name {paletteName}.");
                return;
            }

            // Wait for CreateGUI to build the dropdown if it isn't ready yet
            void TrySwitch()
            {
                if (m_IconPaletteDropdown == null)
                {
                    EditorApplication.delayCall += TrySwitch;
                    return;
                }
                m_IconPaletteDropdown.SwitchSelectedPalette(newPalette);
            }

            TrySwitch();            
        }

        /// <summary>
        /// Callback invoked when the current <see cref="BoardContactSimulationIconPalette"/> is changed from the dropdown.
        /// </summary>
        /// <param name="newPalette">The newly selected <see cref="BoardContactSimulationIconPalette"/>.</param>
        private void OnIconPaletteChanged(BoardContactSimulationIconPalette newPalette)
        {
            if (newPalette == null)
            {
                return;
            }
            
            m_CurrentIconPalette = newPalette;
            m_ResetPaletteButton.SetEnabled(m_CurrentIconPalette.canEdit);
            m_AddIconCellButton.SetEnabled(m_CurrentIconPalette.canEdit);
            m_DeleteIconCellButton.SetEnabled(m_CurrentIconPalette.canEdit);
            currentIconPaletteGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(m_CurrentIconPalette));
            foreach (var cell in m_PaletteCells)
            {
                m_ScrollView.Remove(cell);
            }
            
            m_PaletteCells.Clear();
            selectedPaletteCell = null;
            CreatePaletteIconCells();
        }

        /// <summary>
        /// Callback invoked when a <see cref="BoardContactSimulationIconPaletteCell"/> is toggled.
        /// </summary>
        /// <param name="sender">The <see cref="BoardContactSimulationIconPaletteCell"/> that was toggled.</param>
        /// <param name="value">The new value.</param>
        private void OnPaletteCellToggled(BoardContactSimulationIconPaletteCell sender, bool value)
        {
            if (value)
            {
                m_PaletteCells.ForEach(cell =>
                {
                    if (cell != sender)
                    {
                        cell.value = false;
                    }
                });

                selectedPaletteCell = sender;

                if (EditorApplication.isPlaying && BoardContactSimulation.instance != null)
                {
                    // NOTE: this is such a terrible hack, but there doesn't seem to be a better way to switch focus back to the game view
                    EditorApplication.ExecuteMenuItem("Window/General/Game");
                    // NOTE (cont.) Other than this commented out line which uses reflection.
                    //FocusWindowIfItsOpen(Type.GetType("UnityEditor.GameView, UnityEditor.CoreModule, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null"));
                }
            }
            else
            {
                if (selectedPaletteCell == sender)
                {
                    selectedPaletteCell = null;
                }
            }
        }

        /// <summary>
        /// Callback invoked when the <see cref="BoardContactSimulationIcon"/> changes for a <see cref="BoardContactSimulationIconPaletteCell"/>.
        /// </summary>
        /// <param name="sender">The <see cref="BoardContactSimulationIconPaletteCell"/> that changed.</param>
        private void OnPaletteCellIconChanged(BoardContactSimulationIconPaletteCell sender)
        {
            var index = m_PaletteCells.IndexOf(sender);
            if (index < 0 || index >= m_PaletteCells.Count)
            {
                Debug.LogError("[BoardContactSimulationWindow] Could not find platte cell.");
                return;
            }

            try
            {
                m_CurrentIconPalette.SetIcon(index, sender.icon);
            }
            catch (ArgumentOutOfRangeException)
            {
                Debug.LogError("[BoardContactSimulationWindow] Could not update platte cell.");
                return;
            }

            if (!m_CurrentIconPalette.canEdit)
            {
                m_DeleteIconButton.SetEnabled(false);
            }

            if (m_PaletteCells[index].value)
            {
                m_DeleteIconButton.SetEnabled(sender.icon != null && m_CurrentIconPalette.canEdit);
                if (BoardContactSimulation.instance != null)
                {
                    BoardContactSimulation.instance.currentIcon = sender.icon;
                }
            }
        }

        /// <summary>
        /// Creates the <see cref="BoardContactSimulationIconPaletteCell">BoardContactSimulationIconPaletteCells</see>
        /// for the current <see cref="BoardContactSimulationIconPalette"/>.
        /// </summary>
        private void CreatePaletteIconCells()
        {
            var index = 0;
            var icons = m_CurrentIconPalette.icons;
            var canEdit = m_CurrentIconPalette.canEdit;
            foreach (var icon in icons)
            {
                var cell = new BoardContactSimulationIconPaletteCell(icon, canEdit);
                m_ScrollView.Add(cell);
                m_PaletteCells.Add(cell);
                cell.iconChanged += OnPaletteCellIconChanged;
                cell.RegisterValueChangedCallback((evt) => { OnPaletteCellToggled(cell, evt.newValue); });
                if (index == m_SelectedCellIndex)
                {
                    cell.schedule.Execute(() => { cell.value = true; });
                    selectedPaletteCell = cell;
                }

                index++;
            }
        }

        /// <summary>
        /// Registers event listeners for the window.
        /// </summary>
        private void RegisterCallbacks()
        {
            BoardContactSimulation.iconCleared += OnSimulationIconCleared;
            BoardContactSimulation.iconChanged += OnSimulationIconChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        /// <summary>
        /// Unregisters event listeners for the window.
        /// </summary>
        private void UnregisterCallbacks()
        {
            BoardContactSimulation.iconCleared -= OnSimulationIconCleared;
            BoardContactSimulation.iconChanged -= OnSimulationIconChanged;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        }

        /// <summary>
        /// Callback invoked when the simulation toolbar button is toggled.
        /// </summary>
        /// <param name="changeEvent">The <see cref="ChangeEvent{T}"/> that has occured.</param>
        private void ToggleSimulation(ChangeEvent<bool> changeEvent)
        {
            isSimulationEnabled = changeEvent.newValue;

            if (isSimulationEnabled)
            {
                if (EditorApplication.isPlaying)
                {
                    EnableSimulation();
                }

                m_SimulationEnabledToggle.text = Styles.disableSimulationText;
            }
            else
            {
                BoardContactSimulation.Disable();
                m_ClearAllContactsButton.SetEnabled(false);
                m_SimulationEnabledToggle.text = Styles.enableSimulationText;
            }
        }

        /// <summary>
        /// Callback invoked when the mouse as finger toolbar button is toggled.
        /// </summary>
        /// <param name="changeEvent">The <see cref="ChangeEvent{T}"/> that has occured.</param>
        private void ToggleMouseAsFinger(ChangeEvent<bool> changeEvent)
        {
            isMouseAsFingerEnabled = changeEvent.newValue;
            if (BoardContactSimulation.instance != null)
            {
                BoardContactSimulation.instance.useMouseAsFinger = isMouseAsFingerEnabled;
            }
        }

        /// <summary>
        /// Callback function when the delete toolbar button is clicked.
        /// </summary>
        private void OnDeleteButtonClick()
        {
            // Return early if there's no toggle selected
            if (selectedPaletteCell == null)
            {
                return;
            }

            selectedPaletteCell.ClearIcon();
            m_CurrentIconPalette.SetIcon(m_SelectedCellIndex, null);
            m_DeleteIconButton.SetEnabled(false);
            if (BoardContactSimulation.instance != null)
            {
                BoardContactSimulation.instance.ClearCurrentContact();
            }
        }

        /// <summary>
        /// Callback invoked when the reset toolbar button is clicked.
        /// </summary>
        private void OnResetButtonClick()
        {
            // Display a confirmation dialog
            if (!EditorUtility.DisplayDialog(Styles.resetDialogTitle, Styles.resetDialogMessage,
                    Styles.resetDialogOkText, Styles.resetDialogCancelText))
            {
                return;
            }

            foreach (var cell in m_PaletteCells)
            {
                m_ScrollView.Remove(cell);
            }
            
            m_PaletteCells.Clear();
            selectedPaletteCell = null;
            m_CurrentIconPalette.Reset();
            CreatePaletteIconCells();
        }

        /// <summary>
        /// Callback invoked when the clear all contacts toolbar button is clicked.
        /// </summary>
        private void OnClearAllContactsClick()
        {
            if (BoardContactSimulation.instance == null)
            {
                return;
            }

            BoardContactSimulation.instance.ClearAllContacts();
        }

        /// <summary>
        /// Callback invoked when the add icon cell toolbar button is clicked.
        /// </summary>
        private void OnAddIconCellClick()
        {
            if (m_CurrentIconPalette == null || !m_CurrentIconPalette.canEdit)
            {
                return;
            }

            var cell = new BoardContactSimulationIconPaletteCell(null, true);
            var index = m_SelectedCellIndex;
            if (index < 0)
            {
                index = m_PaletteCells.Count - 1;
            }

            index++;
            m_CurrentIconPalette.InsertAt(index);
            m_ScrollView.Insert(index, cell);
            m_PaletteCells.Insert(index, cell);
            cell.iconChanged += OnPaletteCellIconChanged;
            cell.RegisterValueChangedCallback((evt) => { OnPaletteCellToggled(cell, evt.newValue); });
        }

        /// <summary>
        /// Callback invoked when the delete icon cell toolbar button is clicked.
        /// </summary>
        private void OnDeleteIconCellClick()
        {
            if (m_CurrentIconPalette == null || !m_CurrentIconPalette.canEdit)
            {
                return;
            }
            
            var index = m_SelectedCellIndex;
            if (index < 0)
            {
                index = m_PaletteCells.Count - 1;
            }
            
            m_CurrentIconPalette.RemoveAt(index);
            m_ScrollView.RemoveAt(index);
            m_PaletteCells.RemoveAt(index);
            selectedPaletteCell = null;
        }
        
        /// <summary>
        /// Callback invoked when the settings toolbar button is clicked.
        /// </summary>
        private void OnSettingsClick()
        {
            BoardContactSimulationSettingsProvider.Open();
        }

        /// <summary>
        /// Enables Board input simulation.
        /// </summary>
        private void EnableSimulation()
        {
            BoardContactSimulation.Enable();
            if (selectedPaletteCell != null)
            {
                BoardContactSimulation.instance.currentIcon = selectedPaletteCell.icon;
            }
            
            BoardContactSimulation.instance.useMouseAsFinger = isMouseAsFingerEnabled;
            m_ClearAllContactsButton?.SetEnabled(true);
        }

        /// <summary>
        /// Callback invoked when the Editor's play mode state changes.
        /// </summary>
        /// <param name="stateChange">The <see cref="PlayModeStateChange"/> that has occured.</param>
        private void OnPlayModeStateChanged(PlayModeStateChange stateChange)
        {
            if (stateChange == PlayModeStateChange.ExitingPlayMode)
            {
                BoardContactSimulation.Destroy();
            }

            if (stateChange == PlayModeStateChange.EnteredPlayMode)
            {
                if (EditorPrefs.GetBool(kSimulationStateKey))
                {
                    EnableSimulation();
                }
            }
        }

        /// <summary>
        /// Callback invoked when <see cref="BoardContactSimulation"/> clears the currently selected icon.
        /// </summary>
        private void OnSimulationIconCleared()
        {
            if (selectedPaletteCell == null)
            {
                return;
            }

            selectedPaletteCell.value = false;
            selectedPaletteCell = null;
        }

        /// <summary>
        /// Callback invoked when <see cref="BoardContactSimulation"/> changes the currently selected icon.
        /// </summary>
        private void OnSimulationIconChanged()
        {
            if (BoardContactSimulation.instance == null || BoardContactSimulation.instance.currentIcon == null)
            {
                return;
            }

            var icon = BoardContactSimulation.instance.currentIcon;
            var cell = m_PaletteCells.FirstOrDefault(cell => cell.icon == icon);
            if (cell != null)
            {
                selectedPaletteCell = cell;
                cell.value = true;
            }
        }

        /// <summary>
        /// Loads an asset from the local package.
        /// </summary>
        /// <param name="relativeFilePathWithExtension">The relative file path and file extension to load.</param>
        /// <param name="logError"><c>true</c> if an error should be logged </param>
        /// <typeparam name="T">The type of asset being loaded..</typeparam>
        /// <returns>The asset of type <typeparamref name="T"/> at path <paramref name="relativeFilePathWithExtension"/>.</returns>
        private static T LoadLocalPackageAsset<T>(string relativeFilePathWithExtension, bool logError)
            where T : UnityEngine.Object
        {
            var result = default(T);
            var fullPathInProject = $"Packages/fun.board/Editor/Assets/{relativeFilePathWithExtension}";

            if (System.IO.File.Exists(fullPathInProject))
            {
                result = AssetDatabase.LoadAssetAtPath(fullPathInProject, typeof(T)) as T;
            }
            else if (logError)
            {
                Debug.LogError($"Local asset file {fullPathInProject} not found.");
            }

            return result;
        }

        /// <summary>
        /// Handles when assets are deleted.
        /// </summary>
        /// <param name="win">A <see cref="BoardContactSimulationWindow"/> instance.</param>
        /// <param name="assetPaths">Array of paths to assets that have been deleted.</param>
        internal static void OnAssetsDeleted(BoardContactSimulationWindow win, string[] assetPaths)
        {
            var iconCells = win.m_PaletteCells;
            foreach (var iconCell in iconCells)
            {
                if (assetPaths.Contains(iconCell.iconAssetPath))
                {
                    iconCell.ClearIcon();
                    if (win.selectedPaletteCell == iconCell)
                    {
                        win.selectedPaletteCell = null;
                    }
                }
            }
        }

        /// <summary>
        /// Handles when an asset moves.
        /// </summary>
        /// <param name="win">A <see cref="BoardContactSimulationWindow"/> instance.</param>
        /// <param name="sourcePath">The original path of the asset.</param>
        /// <param name="destinationPath">The new path of the asset.</param>
        internal static void OnAssetMoved(BoardContactSimulationWindow win, string sourcePath, string destinationPath)
        {
            var iconCells = win.m_PaletteCells;
            foreach (var iconCell in iconCells)
            {
                if (iconCell.iconAssetPath == sourcePath)
                {
                    iconCell.iconAssetPath = destinationPath;
                }
            }
        }

        /// <summary>
        /// Shows the <see cref="BoardContactSimulationWindow"/>. Creates a new window if one does not already exist.
        /// </summary>
        [MenuItem("Board/Input/Simulator", false, 2100)]
        public static void CreateOrShow()
        {
            if (s_Instance == null)
            {
                s_Instance = GetWindow<BoardContactSimulationWindow>();
                s_Instance.Show();
                s_Instance.titleContent = Styles.titleContent;
            }
            else
            {
                s_Instance.Show();
                s_Instance.Focus();
            }
        }
    }

    /// <summary>
    /// Asset post-processor for <see cref="BoardContactSimulationIcon">BoardContactSimulationIcons</see>.
    /// </summary>
    internal class BoardContactSimulationIconPostprocessor : AssetPostprocessor
    {
        /// <summary>
        /// Gets the order in which the importer is processed.
        /// </summary>
        /// <returns>The order in which the importer is processed.</returns>
        public override int GetPostprocessOrder()
        {
            return 1;
        }

        /// <summary>
        /// Callback invoked by Unity after the importing of any number of assets is complete (when the Assets progress bar has reached the end).
        /// </summary>
        /// <param name="importedAssets">Array of paths to imported assets.</param>
        /// <param name="deletedAssets">Array of paths to deleted assets.</param>
        /// <param name="movedAssets">Array of paths to moved assets.</param>
        /// <param name="movedFromAssetPaths">Array of original paths for moved assets.</param>
        static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            var wins = Resources.FindObjectsOfTypeAll(typeof(BoardContactSimulationWindow));
            var win = wins.Length > 0 ? (EditorWindow)(wins[0]) as BoardContactSimulationWindow : null;
            if (win != null)
            {
                BoardContactSimulationWindow.OnAssetsDeleted(win, deletedAssets);

                for (var i = 0; i < movedAssets.Length; i++)
                {
                    if (AssetDatabase.GetMainAssetTypeAtPath(movedAssets[i]) == typeof(BoardContactSimulationIcon))
                    {
                        BoardContactSimulationWindow.OnAssetMoved(win, movedFromAssetPaths[i], movedAssets[i]);
                    }
                }
            }
        }
    }
}