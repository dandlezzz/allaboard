// <copyright file="BoardGeneralSettingsEditor.cs" company="Harris Hill Products Inc.">
//     Copyright (c) Harris Hill Products Inc. All rights reserved.
// </copyright>

namespace Board.Editor.Core
{
    using System;

    using Board.Core;

    using UnityEditor;

    using UnityEngine;

    /// <summary>
    /// Provides a custom editor for <see cref="BoardGeneralSettingsEditor"/>.
    /// </summary>
    [CustomEditor(typeof(BoardGeneralSettings))]
    internal class BoardGeneralSettingsEditor : Editor
    {
        /// <summary>
        /// Encapsulates all GUI content for <see cref="BoardGeneralSettingsEditor"/>.
        /// </summary>
        internal class Styles
        {
            public static GUIContent applicationId = new GUIContent("Application ID",
                "The unique identifier for this application.");

            public static GUIContent regenerateId = new GUIContent("↻",
                "Generate a new Application ID.");

            public static GUIContent logLevel = new GUIContent("Log Level",
                "The desired level of event logging.");
        }

        [NonSerialized] private SerializedProperty m_ApplicationIdProperty;
        [NonSerialized] private SerializedProperty m_LogLevelProperty;

        private const string kApplicationIdPropertyName = "m_ApplicationId";
        private const string kLogLevelPropertyName = "m_LogLevel";

        /// <summary>
        /// Callback invoked by Unity when the script is enabled.
        /// </summary>
        private void OnEnable()
        {
            if (target == null)
            {
                return;
            }

            m_ApplicationIdProperty = serializedObject.FindProperty(kApplicationIdPropertyName);
            m_LogLevelProperty = serializedObject.FindProperty(kLogLevelPropertyName);
        }

        /// <summary>
        /// Callback invoked by Unity when rendering the GUI in the Inspector window.
        /// </summary>
        public override void OnInspectorGUI()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PropertyField(m_ApplicationIdProperty, Styles.applicationId);
                if (GUILayout.Button(Styles.regenerateId, GUILayout.Width(24)))
                {
                    m_ApplicationIdProperty.stringValue = Guid.NewGuid().ToString();
                }
            }

            if (string.IsNullOrEmpty(m_ApplicationIdProperty.stringValue))
            {
                EditorGUILayout.HelpBox("Application ID cannot be null or empty.", MessageType.Error);
            }

            EditorGUILayout.PropertyField(m_LogLevelProperty, Styles.logLevel);

            serializedObject.ApplyModifiedProperties();
        }
    }
}
