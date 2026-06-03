// <copyright file="BoardContactSimulatorWindow.cs" company="Harris Hill Products Inc.">
//     Copyright (c) Harris Hill Products Inc. All rights reserved.
// </copyright>

namespace Board.Editor.Input.Simulation
{
    using Board.Input.Simulation;

    using UnityEditor;
    using UnityEditor.UIElements;

    using UnityEngine;
    using UnityEngine.UIElements;

    /// <summary>
    /// Represents the method that will handle the <c>iconChanged</c> event of a <see cref="BoardContactSimulationIconPaletteCell"/> class.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    public delegate void IconChangedHandler(BoardContactSimulationIconPaletteCell sender);

    /// <summary>
    /// Represents the visual representation of a <see cref="BoardContactSimulationIcon"/> in the <see cref="BoardContactSimulationWindow"/>.
    /// </summary>
    public class BoardContactSimulationIconPaletteCell : BaseField<bool>
    {
        private Image m_IconImage;
        private Image m_DragAndDropOverlay;
        private Label m_ContactTypeLabel;
        private BoardContactSimulationIcon m_Icon;
        private BoardContactSimulationIcon m_DraggedIcon;

        private const string kBaseUssClassName = "contact-palette-cell";
        private const string kIconUssClassName = "icon";
        private const string kTypeOverlayUssClassName = "type-overlay";
        private const string kOverlayUssClassName = "overlay";
        private const string kToggleUssClassName = "toggle-active";
        private const string kDisplayNamePath = "m_DisplayName";

        /// <summary>
        /// Gets the current <see cref="BoardContactSimulationIcon"/> represented by this cell.
        /// </summary>
        public BoardContactSimulationIcon icon
        {
            get => m_Icon;
            private set
            {
                if (m_Icon == value)
                {
                    return;
                }

                if (m_Icon != null)
                {
                    m_Icon.changed -= UpdateVisuals;
                }

                m_Icon = value;
                if (value != null)
                {
                    m_Icon.changed += UpdateVisuals;
                }

                labelElement.Unbind();
                if (m_Icon != null)
                {
                    labelElement.Bind(new SerializedObject(m_Icon));
                }

                UpdateVisuals(m_Icon);
                iconChanged?.Invoke(this);
            }
        }

        /// <summary>
        /// Gets or sets the asset path for the current icon represent by this cell.
        /// </summary>
        internal string iconAssetPath { get; set; }

        /// <summary>
        /// Occurs when the icon for a <see cref="BoardContactSimulationIconPaletteCell"/> is changed.
        /// </summary>
        public event IconChangedHandler iconChanged;

        /// <summary>
        /// Initializes a new instance of the <see cref="BoardContactSimulationIconPaletteCell"/> class with the
        /// specified <see cref="BoardContactSimulationIcon"/>.
        /// </summary>
        /// <param name="icon">The <see cref="BoardContactSimulationIcon"/> represented by this cell.</param>
        /// <param name="canEdit"><c>true</c> if the cell can be edited; otherwise, <c>false</c>.</param>
        public BoardContactSimulationIconPaletteCell(BoardContactSimulationIcon icon, bool canEdit) : base(
            icon != null ? icon.displayName : " ", null)
        {
            m_Icon = icon;

            // Style the control overall.
            AddToClassList(ussClassName);
            AddToClassList(kBaseUssClassName);

            m_IconImage = new Image();
            m_IconImage.AddToClassList(kIconUssClassName);
            m_IconImage.sprite = icon?.sprite;
            Insert(0, m_IconImage);
            
            var contactTypeOverlay = new Image();
            contactTypeOverlay.AddToClassList(kTypeOverlayUssClassName);
            m_ContactTypeLabel = new Label();
            m_ContactTypeLabel.text = GetOverlayText();
            contactTypeOverlay.Add(m_ContactTypeLabel);
            Insert(1, contactTypeOverlay);

            if (canEdit)
            {
                m_DragAndDropOverlay = new Image();
                m_DragAndDropOverlay.AddToClassList(kOverlayUssClassName);
                Add(m_DragAndDropOverlay);
                m_DragAndDropOverlay.visible = false;
                m_DragAndDropOverlay.focusable = false;
                m_DragAndDropOverlay.SetEnabled(false);
            }

            if (m_Icon != null)
            {
                m_Icon.changed += UpdateVisuals;
                iconAssetPath = AssetDatabase.GetAssetPath(m_Icon);
            }

            labelElement.focusable = false;
            labelElement.bindingPath = kDisplayNamePath;
            if (icon != null)
            {
                labelElement.Bind(new SerializedObject(icon));
            }

            RegisterCallback<ClickEvent>(OnClick);
            RegisterCallback<KeyDownEvent>(OnKeydownEvent);
            RegisterCallback<NavigationSubmitEvent>(OnSubmit);
            if (canEdit)
            {
                RegisterCallback<DragEnterEvent>(OnDragEnter);
                RegisterCallback<DragLeaveEvent>(OnDragLeave);
                RegisterCallback<DragUpdatedEvent>(OnDragUpdate);
                RegisterCallback<DragPerformEvent>(OnDragPerform);
            }

            this.RegisterValueChangedCallback((evt) =>
            {
                if (evt.newValue)
                {
                    AddToClassList(kToggleUssClassName);
                }
                else
                {
                    RemoveFromClassList(kToggleUssClassName);
                }
            });
        }

        /// <summary>
        /// Callback invoked by Unity when a sequence of pointer down and pointer up actions occurs within the bounds
        /// of the cell.
        /// </summary>
        /// <param name="evt">The <see cref="ClickEvent"/> that has occured.</param>
        private static void OnClick(ClickEvent evt)
        {
            var cell = evt.currentTarget as BoardContactSimulationIconPaletteCell;
            if (cell != null)
            {
                cell.ToggleValue();
                evt.StopPropagation();
            }
        }

        /// <summary>
        /// Callback invoked by Unity when the user presses the submit button.
        /// </summary>
        /// <param name="evt">The <see cref="NavigationSubmitEvent"/> that has occured.</param>
        private static void OnSubmit(NavigationSubmitEvent evt)
        {
            var cell = evt.currentTarget as BoardContactSimulationIconPaletteCell;
            if (cell != null)
            {
                cell.ToggleValue();
                evt.StopPropagation();
            }
        }

        /// <summary>
        /// Callback invoked by Unity when the cell has focus and a user presses a key.
        /// </summary>
        /// <param name="evt">The <see cref="KeyDownEvent"/> that has occured.</param>
        private static void OnKeydownEvent(KeyDownEvent evt)
        {
            var cell = evt.currentTarget as BoardContactSimulationIconPaletteCell;

            // NavigationSubmitEvent event already covers keydown events at runtime, so this method shouldn't handle
            // them.
            if (cell.panel?.contextType == ContextType.Player)
            {
                return;
            }

            // Toggle the value only when the user presses Enter, Return, or Space.
            if (evt.keyCode == KeyCode.KeypadEnter || evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.Space)
            {
                cell.ToggleValue();
                evt.StopPropagation();
            }
        }

        /// <summary>
        /// Callback invoked by Unity when a pointer device enters into the bounds of the cell while a drag is in progress.
        /// </summary>
        /// <param name="evt">The <see cref="DragEnterEvent"/> that has occured.</param>
        private void OnDragEnter(DragEnterEvent evt)
        {
            if (DragAndDrop.paths.Length == 0)
            {
                return;
            }

            var assetPath = DragAndDrop.paths[0];
            var assetType = AssetDatabase.GetMainAssetTypeAtPath(assetPath);
            if (assetType != typeof(BoardContactSimulationIcon))
            {
                return;
            }

            var draggedIcon = AssetDatabase.LoadAssetAtPath<BoardContactSimulationIcon>(assetPath);
            if (draggedIcon != null && draggedIcon != m_Icon)
            {
                m_DraggedIcon = draggedIcon;
                m_DragAndDropOverlay.visible = true;
                DragAndDrop.visualMode = DragAndDropVisualMode.Link;
            }
        }

        /// <summary>
        /// Callback invoked by Unity when a pointer device leaves the bounds of this cell while a drag is in progress.
        /// </summary>
        /// <param name="evt">The <see cref="DragLeaveEvent"/> that has occured.</param>
        private void OnDragLeave(DragLeaveEvent evt)
        {
            m_DragAndDropOverlay.visible = false;
            m_DraggedIcon = null;
        }

        /// <summary>
        /// Callback invoked by Unity when a pointer device stays in the bounds of this cell while a drag is in progress.
        /// </summary>
        /// <param name="evt">The <see cref="DragUpdatedEvent"/> that has occured.</param>
        private void OnDragUpdate(DragUpdatedEvent evt)
        {
            if (m_DraggedIcon != null)
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Link;
            }
        }

        /// <summary>
        /// Callback invoked by Unity when an object is dropped onto this object.
        /// </summary>
        /// <param name="evt">The <see cref="DragPerformEvent"/> that has occured.</param>
        private void OnDragPerform(DragPerformEvent evt)
        {
            if (m_DraggedIcon == null)
            {
                return;
            }

            icon = m_DraggedIcon;
            m_DragAndDropOverlay.visible = false;
            m_DraggedIcon = null;
            DragAndDrop.AcceptDrag();
            DragAndDrop.visualMode = DragAndDropVisualMode.None;
        }

        /// <summary>
        /// Toggles the current value of the cell.
        /// </summary>
        private void ToggleValue()
        {
            value = !value;
        }

        /// <summary>
        /// Updates the visuals for the cell.
        /// </summary>
        /// <param name="sender">The <see cref="BoardContactSimulationIcon"/> that changed.</param>
        private void UpdateVisuals(BoardContactSimulationIcon sender)
        {
            if (m_Icon == null)
            {
                labelElement.text = string.Empty;
                m_IconImage.sprite = null;
                iconAssetPath = null;
                m_ContactTypeLabel.text = string.Empty;
            }
            else
            {
                labelElement.text = m_Icon.displayName;
                m_IconImage.sprite = m_Icon.sprite;
                iconAssetPath = AssetDatabase.GetAssetPath(m_Icon);
                m_ContactTypeLabel.text = GetOverlayText();
            }

            MarkDirtyRepaint();
        }

        /// <summary>
        /// Gets the overlay text for the cell.
        /// </summary>
        /// <returns>The overlay text for the cell.</returns>
        /// <remarks>The format of the text is <c>Type (GlyphId)</c> if the glyph identifier is greater than or equal
        /// to 0; otherwise, <c>Type</c>.</remarks>
        private string GetOverlayText()
        {
            if (m_Icon == null)
            {
                return string.Empty;
            }
            
            return m_Icon.glyphId >= 0 ? $"{icon.contactType} ({icon.glyphId})" : $"{icon.contactType}";
        }

        /// <summary>
        /// Clears the current icon represented by the cell.
        /// </summary>
        public void ClearIcon()
        {
            icon = null;
            labelElement.Unbind();
            UpdateVisuals(icon);
        }
    }
}
