// <copyright file="BoardContactTypeMaskDrawer.cs" company="Harris Hill Products Inc.">
//     Copyright (c) Harris Hill Products Inc. All rights reserved.
// </copyright>

namespace Board.Input
{
    using System;
    using System.Collections.Generic;

    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// Provides a custom property drawer for <see cref="BoardContactTypeMask"/>.
    /// </summary>
    [CustomPropertyDrawer(typeof(BoardContactTypeMask))]
    public class BoardContactTypeMaskDrawer : PropertyDrawer
    {
        /// <summary>
        /// Encapsulates all GUI content for <see cref="BoardContactTypeMaskDrawer"/>.
        /// </summary>
        private static class Contents
        {
            public static readonly GUIContent nothing = EditorGUIUtility.TrTextContent("Nothing");
            public static readonly GUIContent everything = EditorGUIUtility.TrTextContent("Everything");
            public static readonly GUIContent mixed = EditorGUIUtility.TrTextContent("Mixed...");
        }

        /// <summary>
        /// Encapsulates all the data for an option in the <see cref="BoardContactTypeMask"/> editor menu.
        /// </summary>
        private class SetPropertyMaskParameter
        {
            /// <summary>
            /// The <see cref="BoardContactType"/> mask value.
            /// </summary>
            public readonly int maskValue;
            
            /// <summary>
            /// The <see cref="SerializedProperty"/> that this parameter will set.
            /// </summary>
            public readonly SerializedProperty serializedProperty;

            /// <summary>
            /// Initializes a new instance of the <see cref="SetPropertyMaskParameter"/> class.
            /// </summary>
            /// <param name="maskValue">The <see cref="BoardContactType"/> mask value.</param>
            /// <param name="serializedProperty">The <see cref="SerializedProperty"/> that this parameter will set.</param>
            public SetPropertyMaskParameter(int maskValue, SerializedProperty serializedProperty)
            {
                this.maskValue = maskValue;
                this.serializedProperty = serializedProperty;
            }
        }

        private const string kPropertyName = "m_Bits";
        private static readonly int s_PropertyMaskField = nameof(s_PropertyMaskField).GetHashCode();
        private static readonly string[] s_DisplayOptions = Enum.GetNames(typeof(BoardContactType));
        private static readonly int[] s_EnumValues = (int[])Enum.GetValues(typeof(BoardContactType));
        private static readonly List<int> s_ValueOptions = new List<int>();
        
        /// <inheritdoc cref="PropertyDrawer.OnGUI"/>
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            s_ValueOptions.Clear();
            s_ValueOptions.AddRange(s_EnumValues);
            var maskProperty = property.FindPropertyRelative(kPropertyName);
            label = EditorGUI.BeginProperty(position, label, maskProperty);

            // draw the property label
            var controlId = GUIUtility.GetControlID(s_PropertyMaskField, FocusType.Keyboard, position);
            position = EditorGUI.PrefixLabel(position, controlId, label);

            var mask = maskProperty.intValue;
            var currentEvent = Event.current;
            if (currentEvent.type == EventType.Repaint)
            {
                // draw the drop-down button
                var content = GetMaskContent(mask, s_DisplayOptions, s_ValueOptions);
                EditorStyles.popup.Draw(position, content, controlId, false,
                    position.Contains(currentEvent.mousePosition));
            }
            else if (currentEvent.type == EventType.MouseDown && position.Contains(currentEvent.mousePosition) ||
                     IsMainActionKeyForControl(currentEvent, controlId))
            {
                currentEvent.Use();

                for (var i = 0; i < s_ValueOptions.Count; i++)
                {
                    s_ValueOptions[i] = 1 << s_ValueOptions[i];
                }

                CalculateMaskValues(mask, s_ValueOptions, out var optionMaskValues);

                // show the contact type options menu
                var menu = new GenericMenu();
                menu.AddItem(Contents.nothing, mask == 0, SetPropertyMask,
                    new SetPropertyMaskParameter(0, maskProperty));
                menu.AddItem(Contents.everything, mask == -1, SetPropertyMask,
                    new SetPropertyMaskParameter(-1, maskProperty));

                var size = Mathf.Min(s_DisplayOptions.Length, s_ValueOptions.Count);
                for (var i = 0; i < size; i++)
                {
                    var displayedOption = s_DisplayOptions[i];
                    var optionMaskValue = s_ValueOptions[i];

                    menu.AddItem(new GUIContent(displayedOption), (mask & optionMaskValue) != 0, SetPropertyMask,
                        new SetPropertyMaskParameter(optionMaskValues[i + 2], maskProperty));
                }

                menu.DropDown(position);
                GUIUtility.keyboardControl = controlId;
            }

            EditorGUI.EndProperty();
        }

        /// <summary>
        /// Returns true if the event is a main keyboard action for the supplied control id.
        /// </summary>
        /// <param name="evt">The target gui event.</param>
        /// <param name="controlId">The target control id.</param>
        /// <returns>Returns whether the supplied event is a main keyboard action for the supplied control id.</returns>
        private static bool IsMainActionKeyForControl(Event evt, int controlId)
        {
            if (GUIUtility.keyboardControl != controlId)
            {
                return false;
            }

            var modifier = evt.alt || evt.shift || evt.command || evt.control;
            return evt.type == EventType.KeyDown && (evt.keyCode == KeyCode.Space || evt.keyCode == KeyCode.Return ||
                                                     evt.keyCode == KeyCode.KeypadEnter) && !modifier;
        }

        /// <summary>
        /// Callback invoked when a <see cref="BoardContactType"/> menu option is selected. 
        /// </summary>
        /// <param name="parameter">The <see cref="SetPropertyMaskParameter"/> associated with the menu option.</param>
        private static void SetPropertyMask(System.Object parameter)
        {
            if (!(parameter is SetPropertyMaskParameter setPropertyMaskParameter))
            {
                return;
            }

            var serializedProperty = setPropertyMaskParameter.serializedProperty;
            serializedProperty.longValue = (uint)setPropertyMaskParameter.maskValue;
            serializedProperty.serializedObject.ApplyModifiedProperties();
        }

        /// <summary>
        /// Calculates all possible mask values to display in the GUI. 
        /// </summary>
        /// <param name="mask">The current mask.</param>
        /// <param name="valueOptions">A list of the possible mask values for each <see cref="BoardContactType"/>.</param>
        /// <param name="typeMaskValues">When this method returns, contains all possible mask values.</param>
        /// <remarks>This logic is pulled from <see cref="MaskFieldGUI.GetMenuOptions"/>.</remarks>
        private static void CalculateMaskValues(int mask, IList<int> valueOptions, out int[] typeMaskValues)
        {
            // Account for "Nothing" and "Everything" options
            var bufferLength = valueOptions.Count + 2;
            var numberOfUserLayers = valueOptions.Count;

            var totalMaskValue = 0;
            var selectedOptionsMaskValue = 0;
            // Calculate mask for selected options 
            for (var index = 0; index < numberOfUserLayers; ++index)
            {
                var flagValue = valueOptions[index];
                totalMaskValue |= flagValue;
                if ((mask & flagValue) == flagValue)
                {
                    selectedOptionsMaskValue |= flagValue;
                }
            }

            typeMaskValues = new int[bufferLength];
            typeMaskValues[0] = 0; // Default mask value for "Nothing"
            typeMaskValues[1] = -1; // Default mask value for "Everything" 

            for (var valueOptionIndex = 0; valueOptionIndex < numberOfUserLayers; ++valueOptionIndex)
            {
                var typeMaskValueIndex = valueOptionIndex + 2;
                var flagValue = valueOptions[valueOptionIndex];
                var optionMaskValue = (selectedOptionsMaskValue & flagValue) == flagValue
                    ? selectedOptionsMaskValue & ~flagValue
                    : selectedOptionsMaskValue | flagValue;
                if (optionMaskValue == totalMaskValue)
                {
                    optionMaskValue = -1;
                }

                typeMaskValues[typeMaskValueIndex] = optionMaskValue;
            }
        }

        /// <summary>
        /// Gets the GUI content given a list of display and value options
        /// </summary>
        /// <param name="mask">A mask represented as a 32-bit signed integer.</param>
        /// <param name="displayedOptions">A list of possible display options.</param>
        /// <param name="valueOptions">A list of possible value options that comprise the mask.</param>
        /// <returns>The GUI content that corresponds to <paramref name="mask"/>.</returns>
        private static GUIContent GetMaskContent(int mask, IList<string> displayedOptions, IList<int> valueOptions)
        {
            switch (mask)
            {
                case 0:
                    return Contents.nothing;
                case -1:
                    return Contents.everything;
            }

            var count = 0;
            var displayedMaskContent = Contents.mixed;
            var size = Mathf.Min(displayedOptions.Count, valueOptions.Count);
            for (var i = 0; i < size; i++)
            {
                if ((mask & 1 << valueOptions[i]) != 0)
                {
                    if (count == 0)
                    {
                        displayedMaskContent = EditorGUIUtility.TrTempContent(displayedOptions[i]);
                    }

                    ++count;
                    if (count >= 2)
                    {
                        return Contents.mixed;
                    }
                }
            }

            return displayedMaskContent;
        }
    }
}