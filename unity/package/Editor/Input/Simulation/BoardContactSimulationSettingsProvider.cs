// <copyright file="BoardContactSimulationSettingsProvider.cs" company="Harris Hill Products Inc.">
//     Copyright (c) Harris Hill Products Inc. All rights reserved.
// </copyright>

namespace Board.Editor.Input.Simulation
{
    using System;
    using System.Linq;

    using Board.Input.Simulation;

    using UnityEditor;

    using UnityEngine;
    using UnityEngine.InputSystem;
    using UnityEngine.UIElements;

    /// <summary>
    /// Provides the user interface for modifying <see cref="BoardContactSimulationSettings"/>.
    /// </summary>
    internal class BoardContactSimulationSettingsProvider : SettingsProvider
    {
        /// <summary>
        /// Encapsulates all GUI content for <see cref="BoardContactSimulationSettingsProvider"/>.
        /// </summary>
        private class Styles
        {
            public static GUIContent iconOutlineColor = new GUIContent("Icon Outline Color", "Outline color for icons");

            public static GUIContent iconOutlineThickness =
                new GUIContent("Icon Outline Thickness", "Outline thickness for icons");

            public static GUIContent liftedIconColor =
                new GUIContent("Lifted Icon Color", "Icon color when contact is lifted off the board");

            public static GUIContent liftedShadowColor = new GUIContent("Lifted Shadow Color",
                "Shadow color when contact is lifted off the board");

            public static GUIContent liftedShadowDistance = new GUIContent("Lifted Shadow Distance",
                "Shadow distance when contact is lifted off the board");

            public static GUIContent touchedIconOutlineColor = new GUIContent("Touched Icon Outline Color",
                "Outline color for icons when contact is touched");

            public static GUIContent touchedIconOutlineThickness = new GUIContent("Touched Icon Outline Thickness",
                "Outline thickness for icons when contact is touched");

            public static GUIContent snapRotation =
                new GUIContent("Snap Rotation Degrees", "Degrees to rotate contact when snap rotating");

            public static GUIContent rotationSpeed =
                new GUIContent("Rotation Speed", "Speed to rotate contact when free rotating");

            public static GUIContent fastRotationSpeedMultiplier = new GUIContent("Fast Rotation Speed Multiplier",
                "Multiplier applied to rotation speed when fast rotating");
        }
        
        [NonSerialized] private SerializedObject m_SettingsObject;

        [NonSerialized] private SerializedProperty m_ActionsAssetProperty;
        [NonSerialized] private SerializedProperty[] m_ReferenceProperties;
        [NonSerialized] private SerializedProperty m_IconOutlineColorProperty;
        [NonSerialized] private SerializedProperty m_IconOutlineThicknessProperty;
        [NonSerialized] private SerializedProperty m_LiftedIconColorProperty;
        [NonSerialized] private SerializedProperty m_LiftedIconShadowColorProperty;
        [NonSerialized] private SerializedProperty m_LiftedIconShadowDistanceProperty;
        [NonSerialized] private SerializedProperty m_TouchedIconOutlineColorProperty;
        [NonSerialized] private SerializedProperty m_TouchedIconOutlineThicknessProperty;
        [NonSerialized] private SerializedProperty m_SnapRotationDegreesProperty;
        [NonSerialized] private SerializedProperty m_RotationSpeedProperty;
        [NonSerialized] private SerializedProperty m_FastRotationSpeedMultiplierProperty;

        [NonSerialized] private InputActionReference[] m_AvailableActionReferencesInAssetDatabase;
        [NonSerialized] private string[] m_AvailableActionsInAssetNames;
        [NonSerialized] private bool m_AdvancedFoldoutState;
        [NonSerialized] private InputActionAsset m_PreviousActionsAsset;
        [NonSerialized] private InputActionAsset m_DefaultActionsAsset;

        private const string kActionsAssetPropertyName = "m_ActionsAsset";
        private const string kIconOutlineColorPropertyName = "m_IconOutlineColor";
        private const string kIconOutlineThicknessPropertyName = "m_IconOutlineThickness";
        private const string kLiftedIconColorPropertyName = "m_LiftedIconColor";
        private const string kLiftedIconShadowColorPropertyName = "m_LiftedIconShadowColor";
        private const string kLiftedIconShadowDistancePropertyName = "m_LiftedIconShadowDistance";
        private const string kTouchedIconOutlineColorPropertyName = "m_TouchedIconOutlineColor";
        private const string kTouchedIconOutlineThicknessPropertyName = "m_TouchedIconOutlineThickness";
        private const string kSnapRotationDegreesPropertyName = "m_SnapRotationDegrees";
        private const string kRotationSpeedPropertyName = "m_RotationSpeed";
        private const string kFastRotationSpeedMultiplierPropertyName = "m_FastRotationSpeedMultiplier";

        private const string kSettingsPath = "Project/Board/Simulation Settings";

        private const string kDefaultInputActionsPath =
            "Packages/fun.board/Editor/Assets/Simulation/DefaultSimulationControls.inputactions";

        private const float kLabelWidth = 200f;
        private const string kInputActionsSectionLabel = "Input Actions";
        private const string kAppearanceSectionLabel = "Appearance";
        private const string kRotationSectionLabel = "Rotation";
        private const int kMinimumIconOutlineThickness = 0;
        private const int kMaximumIconOutlineThickness = 10;
        private const float kMinimumIconShadowDistance = 0;
        private const float kMaximumIconShadowDistance = 20;
        private const float kMinimumRotationSpeed = .05f;
        private const float kMaximumRotationSpeed = 3f;
        private const float kMinimumFastRotationModifier = 1;
        private const float kMaximumFastRotationModifer = 10;
        private const string kResetToDefaultLabel = "Reset to Defaults";

        private static readonly string[] s_ActionNames =
        {
            "Touch",
            "PlaceTouched",
            "PlaceUntouched",
            "Lift",
            "Clear",
            "SnapRotateClockwise",
            "SnapRotateCounterclockwise",
            "Rotate",
            "FastRotate"
        };

        private static readonly string[] s_ActionNiceNames =
        {
            "Touch",
            "Place Touched",
            "Place Untouched",
            "Lift",
            "Clear",
            "Snap Rotate Clockwise",
            "Snap Rotate Counterclockwise",
            "Rotate",
            "Fast Rotate"
        };

        /// <summary>
        /// Creates a new instance of the <see cref="BoardContactSimulationSettingsProvider"/> class.
        /// </summary>
        /// <param name="path">Path used to place the SettingsProvider in the tree view of the Settings window.</param>
        /// <param name="scope"><see cref="SettingsScope"/> of the SettingsProvider.</param>
        private BoardContactSimulationSettingsProvider(string path, SettingsScope scope = SettingsScope.User)
            : base(path, scope)
        {
        }

        /// <summary>
        /// Initializes the serialized properties.
        /// </summary>
        private void Initialize()
        {
            InitializeActions();
            m_IconOutlineColorProperty = m_SettingsObject.FindProperty(kIconOutlineColorPropertyName);
            m_IconOutlineThicknessProperty = m_SettingsObject.FindProperty(kIconOutlineThicknessPropertyName);
            m_LiftedIconColorProperty = m_SettingsObject.FindProperty(kLiftedIconColorPropertyName);
            m_LiftedIconShadowColorProperty = m_SettingsObject.FindProperty(kLiftedIconShadowColorPropertyName);
            m_LiftedIconShadowDistanceProperty = m_SettingsObject.FindProperty(kLiftedIconShadowDistancePropertyName);
            m_TouchedIconOutlineColorProperty = m_SettingsObject.FindProperty(kTouchedIconOutlineColorPropertyName);
            m_TouchedIconOutlineThicknessProperty =
                m_SettingsObject.FindProperty(kTouchedIconOutlineThicknessPropertyName);
            m_SnapRotationDegreesProperty = m_SettingsObject.FindProperty(kSnapRotationDegreesPropertyName);
            m_RotationSpeedProperty = m_SettingsObject.FindProperty(kRotationSpeedPropertyName);
            m_FastRotationSpeedMultiplierProperty =
                m_SettingsObject.FindProperty(kFastRotationSpeedMultiplierPropertyName);
        }

        /// <summary>
        /// Initializes the serialized properties related to input actions.
        /// </summary>
        private void InitializeActions()
        {
            var numActions = s_ActionNames.Length;
            m_ReferenceProperties = new SerializedProperty[numActions];
            for (var i = 0; i < numActions; i++)
            {
                m_ReferenceProperties[i] = m_SettingsObject.FindProperty($"m_{s_ActionNames[i]}Action");
            }

            m_ActionsAssetProperty = m_SettingsObject.FindProperty(kActionsAssetPropertyName);
            m_AvailableActionReferencesInAssetDatabase =
                GetAllAssetReferencesFromAssetDatabase(m_ActionsAssetProperty.objectReferenceValue as InputActionAsset);
            m_AvailableActionsInAssetNames = new[] { "None" }
                .Concat(m_AvailableActionReferencesInAssetDatabase?.Select(x =>
                    MakeActionReferenceNameUsableInGenericMenu(x.name)) ?? new string[0]).ToArray();
        }
        
        /// <summary>
        /// Reassigns the input action references when a new <see cref="InputActionAsset"/> is applied to the settings.
        /// </summary>
        /// <param name="action">The new <see cref="InputActionAsset"/> for the settings.</param>
        private void ReassignActions(InputActionAsset action)
        {
            var assets = GetAllAssetReferencesFromAssetDatabase(action);
            if (assets != null)
            {
                int i = 0;
                foreach (var property in m_ReferenceProperties)
                {
                    var reference = property.objectReferenceValue as InputActionReference;
                    property.objectReferenceValue =
                        GetActionReferenceFromAssets(assets, reference?.name, s_ActionNames[i], s_ActionNiceNames[i]);
                    i++;
                }

                m_SettingsObject.ApplyModifiedProperties();
            }
        }

        /// <summary>
        /// Gets the index of the specified <see cref="InputAction"/> in the current <see cref="InputActionAsset"/>
        /// used by the settings.
        /// </summary>
        /// <param name="inputAction">An <see cref="InputAction"/></param>
        /// <returns>The index of the <paramref name="inputAction"/> in the current <see cref="InputActionAsset"/>
        /// used by the settings if it exists; otherwise, 0</returns>
        private int IndexOfInputActionInAsset(InputAction inputAction)
        {
            // return 0 instead of -1 here because the zero-th index refers to the 'None' binding.
            if (inputAction == null)
            {
                return 0;
            }

            var index = 0;
            for (var j = 0; j < m_AvailableActionReferencesInAssetDatabase.Length; j++)
            {
                if (m_AvailableActionReferencesInAssetDatabase[j].action != null &&
                    m_AvailableActionReferencesInAssetDatabase[j].action == inputAction)
                {
                    index = j + 1;
                    break;
                }
            }

            return index;
        }

        /// <summary>
        /// Gets the first <see cref="InputActionReference"/> from a specified array of references whose name matches
        /// one of the specified action names.
        /// </summary>
        /// <param name="actions">An array of <see cref="InputActionReference">InputActionReferences</see>.</param>
        /// <param name="actionNames">An array of action names to match.</param>
        /// <returns>The first <see cref="InputActionReference"/> in <paramref name="actions"/> whose name matches a
        /// name from <paramref name="actionNames"/>.</returns>
        private static InputActionReference GetActionReferenceFromAssets(InputActionReference[] actions,
            params string[] actionNames)
        {
            foreach (var actionName in actionNames)
            {
                foreach (var action in actions)
                {
                    if (string.Compare(action.action.name, actionName, StringComparison.InvariantCultureIgnoreCase) ==
                        0)
                    {
                        return action;
                    }
                }
            }

            return null;
        }
        
        /// <summary>
        /// Gets the display name of an <see cref="InputAction"/>.
        /// </summary>
        /// <param name="action">An <see cref="InputAction"/>.</param>
        /// <returns>The display name of <paramref name="action"/>.</returns>
        private static string GetDisplayName(InputAction action)
        {
            return !string.IsNullOrEmpty(action?.actionMap?.name)
                ? $"{action.actionMap?.name}/{action.name}"
                : action?.name;
        }

        /// <summary>
        /// Converts an <see cref="InputActionReference"/> name to a name that can be displayed in a menu UI.
        /// </summary>
        /// <param name="name">An <see cref="InputActionReference"/> name.</param>
        /// <returns>A version of <paramref name="name"/> that can be display in a menu UI.</returns>
        /// <remarks>This is necessary because action names use forward slashes which Unity's UI system interprets as submenu paths.</remarks>
        private static string MakeActionReferenceNameUsableInGenericMenu(string name)
        {
            return name.Replace("/", "\uFF0F");
        }

        /// <summary>
        /// Gets all <see cref="InputActionReference">InputActionReferences</see> from a specified
        /// <see cref="InputActionAsset"/> via Unity's asset database.
        /// </summary>
        /// <param name="actions">An <see cref="InputActionAsset"/>.</param>
        /// <returns>All <see cref="InputActionReference">InputActionReferences</see> contained in <paramref name="actions"/> ordered by name.</returns>
        private static InputActionReference[] GetAllAssetReferencesFromAssetDatabase(InputActionAsset actions)
        {
            if (actions == null)
            {
                return null;
            }

            var path = AssetDatabase.GetAssetPath(actions);
            var assets = AssetDatabase.LoadAllAssetsAtPath(path);
            return assets.Where(asset => asset is InputActionReference)
                .Cast<InputActionReference>()
                .OrderBy(x => x.name)
                .ToArray();
        }

        /// <summary>
        /// Callback invoked by Unity when the user clicks on the Settings in the Settings window.
        /// </summary>
        /// <param name="searchContext">Search context in the search box on the Settings window.</param>
        /// <param name="rootElement">Root of the UIElements tree.</param>
        public override void OnActivate(string searchContext, VisualElement rootElement)
        {
            m_SettingsObject = new SerializedObject(BoardContactSimulationSettings.instance);
            m_DefaultActionsAsset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(kDefaultInputActionsPath);
            Initialize();
        }

        /// <summary>
        /// Callback invoked by Unity to draw the UI.
        /// </summary>
        /// <param name="searchContext">Search context in the search box on the Settings window.</param>
        public override void OnGUI(string searchContext)
        {
            EditorGUIUtility.labelWidth = kLabelWidth;
            EditorGUILayout.LabelField(kInputActionsSectionLabel, EditorStyles.boldLabel);
            EditorGUILayout.Space();

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(m_ActionsAssetProperty);
            if (EditorGUI.EndChangeCheck())
            {
                var actions = m_ActionsAssetProperty.objectReferenceValue as InputActionAsset;

                if (actions == null)
                {
                    actions = m_DefaultActionsAsset;
                    m_ActionsAssetProperty.objectReferenceValue = actions;
                    m_SettingsObject.ApplyModifiedProperties();
                }

                if (actions != null)
                {
                    m_SettingsObject.ApplyModifiedProperties();

                    ReassignActions(actions);

                    m_SettingsObject.Update();
                }

                // reinitialize action types
                InitializeActions();
            }

            var numActions = s_ActionNames.Length;
            if ((m_AvailableActionReferencesInAssetDatabase != null &&
                 m_AvailableActionReferencesInAssetDatabase.Length > 0))
            {
                for (var i = 0; i < numActions; i++)
                {
                    // find the input action reference from the asset that matches the input action reference from the
                    // InputSystemUIInputModule that is currently selected. Note we can't use reference equality of the
                    // two InputActionReference objects here because in ReassignActions above, we create new instances
                    // every time it runs.
                    var index = IndexOfInputActionInAsset(
                        ((InputActionReference)m_ReferenceProperties[i]?.objectReferenceValue)?.action);

                    EditorGUI.BeginChangeCheck();
                    index = EditorGUILayout.Popup(s_ActionNiceNames[i], index, m_AvailableActionsInAssetNames);

                    if (EditorGUI.EndChangeCheck())
                    {
                        m_ReferenceProperties[i].objectReferenceValue =
                            index > 0 ? m_AvailableActionReferencesInAssetDatabase[index - 1] : null;
                    }
                }
            }
            else if (m_ActionsAssetProperty.objectReferenceValue != null)
            {
                // Somehow we have an asset but no asset references from the database, pull out references manually and show them in read only UI
                EditorGUILayout.HelpBox(
                    "Showing fields as read-only because current action asset seems to be created by a script and assigned programmatically.",
                    MessageType.Info);

                EditorGUI.BeginDisabledGroup(true);
                for (var i = 0; i < numActions; i++)
                {
                    var retrievedName = "None";
                    if (m_ReferenceProperties[i].objectReferenceValue != null &&
                        (m_ReferenceProperties[i].objectReferenceValue is InputActionReference reference))
                    {
                        retrievedName = MakeActionReferenceNameUsableInGenericMenu(GetDisplayName(reference));
                    }

                    EditorGUILayout.Popup(s_ActionNiceNames[i], 0, new[] { retrievedName });
                }

                EditorGUI.EndDisabledGroup();
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField(kAppearanceSectionLabel, EditorStyles.boldLabel);
            EditorGUILayout.Space();

            m_IconOutlineColorProperty.colorValue =
                EditorGUILayout.ColorField(Styles.iconOutlineColor, m_IconOutlineColorProperty.colorValue);
            m_IconOutlineThicknessProperty.intValue = EditorGUILayout.IntSlider(Styles.iconOutlineThickness,
                m_IconOutlineThicknessProperty.intValue, kMinimumIconOutlineThickness, kMaximumIconOutlineThickness);
            EditorGUILayout.Space();

            m_LiftedIconColorProperty.colorValue =
                EditorGUILayout.ColorField(Styles.liftedIconColor, m_LiftedIconColorProperty.colorValue);
            m_LiftedIconShadowColorProperty.colorValue = EditorGUILayout.ColorField(Styles.liftedShadowColor,
                m_LiftedIconShadowColorProperty.colorValue);
            m_LiftedIconShadowDistanceProperty.floatValue = EditorGUILayout.Slider(Styles.liftedShadowDistance,
                m_LiftedIconShadowDistanceProperty.floatValue, kMinimumIconShadowDistance, kMaximumIconShadowDistance);
            EditorGUILayout.Space();

            m_TouchedIconOutlineColorProperty.colorValue = EditorGUILayout.ColorField(Styles.touchedIconOutlineColor,
                m_TouchedIconOutlineColorProperty.colorValue);
            m_TouchedIconOutlineThicknessProperty.intValue = EditorGUILayout.IntSlider(
                Styles.touchedIconOutlineThickness, m_TouchedIconOutlineThicknessProperty.intValue,
                kMinimumIconOutlineThickness, kMaximumIconOutlineThickness);
            EditorGUILayout.Space();

            EditorGUILayout.LabelField(kRotationSectionLabel, EditorStyles.boldLabel);
            m_SnapRotationDegreesProperty.floatValue = EditorGUILayout.Slider(Styles.snapRotation,
                m_SnapRotationDegreesProperty.floatValue, 0, 360f);
            m_RotationSpeedProperty.floatValue = EditorGUILayout.Slider(Styles.rotationSpeed,
                m_RotationSpeedProperty.floatValue, kMinimumRotationSpeed, kMaximumRotationSpeed);
            m_FastRotationSpeedMultiplierProperty.floatValue = EditorGUILayout.Slider(
                Styles.fastRotationSpeedMultiplier, m_FastRotationSpeedMultiplierProperty.floatValue,
                kMinimumFastRotationModifier, kMaximumFastRotationModifer);
            EditorGUILayout.Space();
            EditorGUILayout.Space();

            if (GUI.changed)
            {
                m_SettingsObject.ApplyModifiedProperties();
            }

            if (GUILayout.Button(kResetToDefaultLabel))
            {
                BoardContactSimulationSettings.instance.Reset();
                m_SettingsObject = new SerializedObject(BoardContactSimulationSettings.instance);
                Initialize();
            }

            EditorGUIUtility.labelWidth = 0f;
        }

        /// <summary>
        /// Opens the Unity settings window to the <see cref="BoardContactSimulationSettingsProvider"/>.
        /// </summary>
        public static void Open()
        {
            SettingsService.OpenProjectSettings(kSettingsPath);
        }

        /// <summary>
        /// Creates the settings provider for <see cref="BoardContactSimulationSettings"/>.
        /// </summary>
        [SettingsProvider]
        public static SettingsProvider CreateSettingsProvider()
        {
            return new BoardContactSimulationSettingsProvider(kSettingsPath, SettingsScope.Project);
        }
    }
}
