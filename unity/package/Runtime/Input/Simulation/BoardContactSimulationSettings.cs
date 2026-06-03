// <copyright file="BoardContactSimulationSettings.cs" company="Harris Hill Products Inc.">
//     Copyright (c) Harris Hill Products Inc. All rights reserved.
// </copyright>

#if UNITY_EDITOR

namespace Board.Input.Simulation
{
    using System;
    using System.Linq;

    using UnityEditor;

    using UnityEngine;
    using UnityEngine.InputSystem;

    /// <summary>
    /// Encapsulates settings for Board input simulation.
    /// </summary>
    [FilePath("UserSettings/BoardInputSimulationSettings.asset", FilePathAttribute.Location.ProjectFolder)]
    public class BoardContactSimulationSettings : ScriptableSingleton<BoardContactSimulationSettings>
    {
        [SerializeField] private InputActionAsset m_ActionsAsset;
        [SerializeField] private InputActionReference m_TouchAction;
        [SerializeField] private InputActionReference m_PlaceTouchedAction;
        [SerializeField] private InputActionReference m_PlaceUntouchedAction;
        [SerializeField] private InputActionReference m_LiftAction;
        [SerializeField] private InputActionReference m_ClearAction;
        [SerializeField] private InputActionReference m_SnapRotateClockwiseAction;
        [SerializeField] private InputActionReference m_SnapRotateCounterclockwiseAction;
        [SerializeField] private InputActionReference m_RotateAction;
        [SerializeField] private InputActionReference m_FastRotateAction;

        [SerializeField] private Color m_IconOutlineColor = Color.black;
        [SerializeField] private int m_IconOutlineThickness = 1;
        [SerializeField] private Color m_LiftedIconColor = new Color(1, 1, 1, .5f);
        [SerializeField] private Color m_LiftedIconShadowColor = Color.black;
        [SerializeField] private float m_LiftedIconShadowDistance = 10;
        [SerializeField] private Color m_TouchedIconOutlineColor = Color.black;
        [SerializeField] private int m_TouchedIconOutlineThickness = 3;

        [SerializeField] private float m_SnapRotationDegrees = 10;
        [SerializeField] private float m_RotationSpeed = .25f;
        [SerializeField] private float m_FastRotationSpeedMultiplier = 5f;

        private const string kDefaultInputActionsPath =
            "Packages/fun.board/Editor/Assets/Simulation/DefaultSimulationControls.inputactions";

        /// <summary>
        /// Gets the <see cref="InputActionAsset"/> which provides the controls for simulation.
        /// </summary>
        public InputActionAsset actionsAsset => m_ActionsAsset;

        /// <summary>
        /// Gets the <see cref="InputActionReference"/> for the action to simulate touching a contact.
        /// </summary>
        public InputActionReference touchAction => m_TouchAction;
        
        /// <summary>
        /// Gets the <see cref="InputActionReference"/> for the action to simulate placing a touched contact.
        /// </summary>
        public InputActionReference placeTouchedAction => m_PlaceTouchedAction;
        
        /// <summary>
        /// Gets the <see cref="InputActionReference"/> for the action to simulate placing an untounched contact.
        /// </summary>
        public InputActionReference placeUntouchedAction => m_PlaceUntouchedAction;

        /// <summary>
        /// Gets the <see cref="InputActionReference"/> for the action to simulate lifting a contact.
        /// </summary>
        public InputActionReference liftAction => m_LiftAction;

        /// <summary>
        /// Gets the <see cref="InputActionReference"/> for the action to clear the current contact.
        /// </summary>
        public InputActionReference clearAction => m_ClearAction;

        /// <summary>
        /// Gets the <see cref="InputActionReference"/> for the action to simulate rotating a contact clockwise by a fixed amount.
        /// </summary>
        public InputActionReference snapRotateClockwiseAction => m_SnapRotateClockwiseAction;

        /// <summary>
        /// Gets the <see cref="InputActionReference"/> for the action to simulate rotating a contact counterclockwise by a fixed amount.
        /// </summary>
        public InputActionReference snapRotateCounterclockwiseAction => m_SnapRotateCounterclockwiseAction;

        /// <summary>
        /// Gets the <see cref="InputActionReference"/> for the action to simulate rotating a contact.
        /// </summary>
        public InputActionReference rotateAction => m_RotateAction;

        /// <summary>
        /// Gets the <see cref="InputActionReference"/> for the action to simulate rotating a contact quickly.
        /// </summary>
        public InputActionReference fastRotateAction => m_FastRotateAction;

        /// <summary>
        /// Gets the default outline color for the icon representation of a <see cref="BoardContact"/>.
        /// </summary>
        public Color iconOutlineColor => m_IconOutlineColor;

        /// <summary>
        /// Gets the default outline thickness for the icon representation of a <see cref="BoardContact"/>.
        /// </summary>
        public float iconOutlineThickness => m_IconOutlineThickness;

        /// <summary>
        /// Gets the color for the icon representation of a <see cref="BoardContact"/> when it is lifted.
        /// </summary>
        public Color liftedIconColor => m_LiftedIconColor;

        /// <summary>
        /// Gets the shadow color for the icon representation of a <see cref="BoardContact"/> when it is lifted.
        /// </summary>
        public Color liftedIconShadowColor => m_LiftedIconShadowColor;

        /// <summary>
        /// Gets the shadow effect distance for the icon representation of a <see cref="BoardContact"/> when it is lifted.
        /// </summary>
        public float liftedIconShadowDistance => m_LiftedIconShadowDistance;

        /// <summary>
        /// Gets the outline color for the icon representation of a <see cref="BoardContact"/> when it is touched.
        /// </summary>
        public Color touchedIconOutlineColor => m_TouchedIconOutlineColor;

        /// <summary>
        /// Gets the outline thickness for the icon representation of a <see cref="BoardContact"/> when it is touched.
        /// </summary>
        public float touchedIconOutlineThickness => m_TouchedIconOutlineThickness;

        /// <summary>
        /// Gets the degrees to rotate a simulated contact when it is snap rotated.
        /// </summary>
        public float snapRotationDegrees => m_SnapRotationDegrees;

        /// <summary>
        /// Gets the speed at which a simulated contact is rotated.
        /// </summary>
        public float rotationSpeed => m_RotationSpeed;

        /// <summary>
        /// Gets the multiplier to apply to the speed at which a simulated contact is being quickly rotated.
        /// </summary>
        public float fastRotationSpeedMultiplier => m_FastRotationSpeedMultiplier;

        /// <summary>
        /// Occurs when a property in the settings changes.
        /// </summary>
        public static event Action<BoardContactSimulationSettings> changed;
        
        /// <summary>
        /// Resets input actions to their defaults.
        /// </summary>
        private void ResetInputActions()
        {
            m_ActionsAsset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(kDefaultInputActionsPath);
            var assets = AssetDatabase.LoadAllAssetsAtPath(kDefaultInputActionsPath);
            var references = assets.Where(asset => asset is InputActionReference).Cast<InputActionReference>();
            foreach (var reference in references)
            {
                switch (reference.name)
                {
                    case "Simulation/Touch":
                        m_TouchAction = reference;
                        break;
                    case "Simulation/PlaceTouched":
                        m_PlaceTouchedAction = reference;
                        break;
                    case "Simulation/PlaceUntouched":
                        m_PlaceUntouchedAction = reference;
                        break;
                    case "Simulation/Lift":
                        m_LiftAction = reference;
                        break;
                    case "Simulation/Clear":
                        m_ClearAction = reference;
                        break;
                    case "Simulation/SnapRotateClockwise":
                        m_SnapRotateClockwiseAction = reference;
                        break;
                    case "Simulation/SnapRotateCounterclockwise":
                        m_SnapRotateCounterclockwiseAction = reference;
                        break;
                    case "Simulation/Rotate":
                        m_RotateAction = reference;
                        break;
                    case "Simulation/FastRotate":
                        m_FastRotateAction = reference;
                        break;
                }
            }
        }

        /// <summary>
        /// Callback invoked by Unity when the script becomes active and enabled.
        /// </summary>
        private void OnEnable()
        {
            if (m_ActionsAsset == null)
            {
                ResetInputActions();
            }
        }

        /// <summary>
        /// Callback invoked by Unity when the script is loaded or a value is changed in the Inspector.
        /// </summary>
        private void OnValidate()
        {
            if (EditorUtility.IsDirty(this))
            {
                Save(true);
                changed?.Invoke(this);
            }
        }

        /// <summary>
        /// Reset to default state.
        /// </summary>
        public void Reset()
        {
            ResetInputActions();
            m_IconOutlineColor = Color.black;
            m_IconOutlineThickness = 1;
            m_LiftedIconColor = new Color(1, 1, 1, .5f);
            m_LiftedIconShadowColor = Color.black;
            m_LiftedIconShadowDistance = 10;
            m_TouchedIconOutlineColor = Color.black;
            m_TouchedIconOutlineThickness = 3;
            m_SnapRotationDegrees = 10;
            m_RotationSpeed = .25f;
            m_FastRotationSpeedMultiplier = 5f;
            Save(true);
        }
    }
}

#endif //UNITY_EDITOR