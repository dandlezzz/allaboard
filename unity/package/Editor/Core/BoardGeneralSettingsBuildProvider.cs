// <copyright file="BoardGeneralSettingsBuildProvider.cs" company="Harris Hill Products Inc.">
//     Copyright (c) Harris Hill Products Inc. All rights reserved.
// </copyright>

#if UNITY_ANDROID
namespace Board.Editor.Core
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Xml;

    using Board.Core;

    using UnityEditor;
    using UnityEditor.Android;
    using UnityEditor.Build;
    using UnityEditor.Build.Reporting;

    /// <summary>
    /// Represents an Android XML document.
    /// </summary>
    internal class AndroidXmlDocument : XmlDocument
    {
        private string m_Path;
        protected XmlNamespaceManager m_NamespaceManager;

        protected readonly string kAndroidXmlNamespace = "http://schemas.android.com/apk/res/android";

        /// <summary>
        /// Initializes a new instance of the <see cref="AndroidXmlDocument"/> class with the given path to a XML file.
        /// </summary>
        /// <param name="path">The path to a XML file.</param>
        public AndroidXmlDocument(string path)
        {
            m_Path = path;
            using (var reader = new XmlTextReader(m_Path))
            {
                reader.Read();
                Load(reader);
            }

            m_NamespaceManager = new XmlNamespaceManager(NameTable);
            m_NamespaceManager.AddNamespace("android", kAndroidXmlNamespace);
        }

        /// <summary>
        /// Saves the <see cref="XmlDocument"/> to disk.
        /// </summary>
        public void Save()
        {
            SaveAs(m_Path);
        }

        /// <summary>
        /// Saves the <see cref="XmlDocument"/> to disk at the specified file path.
        /// </summary>
        /// <param name="path">The file path to save the <see cref="XmlDocument"/>.</param>
        public string SaveAs(string path)
        {
            using (var writer = new XmlTextWriter(path, new UTF8Encoding(false)))
            {
                writer.Formatting = Formatting.Indented;
                Save(writer);
            }

            return path;
        }
    }

    /// <summary>
    /// Represents an Android manifest document.
    /// </summary>
    internal class AndroidManifest : AndroidXmlDocument
    {
        private readonly XmlElement m_ApplicationElement;

        /// <summary>
        /// Initializes a new instance of the <see cref="AndroidManifest"/> class with the given path to the manifest file.
        /// </summary>
        /// <param name="path">The path to the Android manifest file.</param>
        public AndroidManifest(string path) : base(path)
        {
            m_ApplicationElement = SelectSingleNode("/manifest/application") as XmlElement;
        }

        /// <summary>
        /// Creates a new <see cref="XmlAttribute"/> with the given key and value pair.
        /// </summary>
        /// <param name="key">The key for the new attribute.</param>
        /// <param name="value">The value for the new attribute.</param>
        /// <returns>A <see cref="XmlAttribute"/> with <paramref name="key"/> and <paramref name="value"/>.</returns>
        private XmlAttribute CreateAndroidAttribute(string key, string value)
        {
            var attr = CreateAttribute("android", key, kAndroidXmlNamespace);
            attr.Value = value;
            return attr;
        }

        /// <summary>
        /// Sets the application identifier in the Android manifest.
        /// </summary>
        /// <param name="applicationId">The application identifier.</param>
        /// <exception cref="ArgumentNullException">If <paramref name="applicationId"/> is <c>null</c> or empty.</exception>
        internal void SetBoardApplicationId(string applicationId)
        {
            if (string.IsNullOrEmpty(applicationId))
            {
                throw new ArgumentNullException(nameof(applicationId));
            }

            var node = m_ApplicationElement.SelectSingleNode("//meta-data[@android:name=\"co.harrishill.apps.APP_ID\"]",
                m_NamespaceManager);
            if (node != null)
            {
                node.Attributes.SetNamedItem(CreateAndroidAttribute("value", applicationId));
            }
            else
            {
                var element = CreateElement("meta-data");
                element.SetAttributeNode(CreateAndroidAttribute("name", "co.harrishill.apps.APP_ID"));
                element.SetAttributeNode(CreateAndroidAttribute("value", applicationId));
                m_ApplicationElement.PrependChild(element);
            }
        }
    }

    /// <summary>
    /// Provides a mechanism to configure and validate <see cref="BoardGeneralSettings"/> on build.
    /// </summary>
    internal class BoardGeneralSettingsBuildProvider : IPreprocessBuildWithReport, IPostprocessBuildWithReport,
        IPostGenerateGradleAndroidProject
    {
        /// <summary>
        /// Gets the full Android manifest path given the base path to the root of the Unity library Gradle project.
        /// </summary>
        /// <param name="basePath">The path to the root of the Unity library Gradle project.</param>
        /// <returns>The full path to the Android manifest file.</returns>
        private string GetManifestPath(string basePath)
        {
            return Path.Combine(basePath, "src", "main", "AndroidManifest.xml");
        }

        /// <summary>
        /// Gets the relative callback order of the build provider.
        /// </summary>
        public int callbackOrder => 0;

        /// <summary>
        /// Callback invoked by Unity before a Player build is started.
        /// </summary>
        /// <param name="report">A <see cref="BuildReport"/> containing information about the build, such as its target
        /// platform and output path.</param>
        public void OnPreprocessBuild(BuildReport report)
        {
            EditorBuildSettings.TryGetConfigObject(BoardGeneralSettings.kEditorBuildSettingsConfigKey,
                out BoardGeneralSettings settingsObj);

            if (settingsObj == null)
            {
                throw new BuildFailedException(
                    "[Board SDK] BoardGeneralSettings asset not found.\n\n" +
                    "This is unexpected as BoardGeneralSettings is auto-created on SDK import.\n" +
                    "Try reimporting the Board SDK package.");
            }

            if (!EditorUtility.IsPersistent(settingsObj))
            {
                throw new BuildFailedException(
                    "[Board SDK] BoardGeneralSettings asset file is missing or was deleted.\n\n" +
                    "Go to Project Settings > Board > General Settings\n" +
                    "and click \"Make New Settings Asset\" to create a new one.");
            }

            if (string.IsNullOrEmpty(BoardGeneralSettings.instance.applicationId))
            {
                throw new BuildFailedException(
                    "[Board SDK] Application ID is empty.\n\n" +
                    "The Application ID is a UUID that uniquely identifies your app.\n\n" +
                    "Go to Project Settings > Board > General Settings and enter a UUID.\n" +
                    "Generate one with: 'uuidgen' (macOS/Linux) or '[guid]::NewGuid()' (PowerShell)");
            }

            UnityEngine.Object[] preloadedAssets = PlayerSettings.GetPreloadedAssets();

            if (!preloadedAssets.Contains(settingsObj))
            {
                var assets = preloadedAssets.ToList();
                assets.Add(settingsObj);
                PlayerSettings.SetPreloadedAssets(assets.ToArray());
            }
        }

        /// <summary>
        /// Callback invoked by Unity after a Player build is completed.
        /// </summary>
        /// <param name="report">A <see cref="BuildReport"/> containing information about the build, such as its target platform and output path.</param>
        public void OnPostprocessBuild(BuildReport report)
        {
            var preloadedAssets = PlayerSettings.GetPreloadedAssets();
            if (preloadedAssets == null)
            {
                return;
            }

            var oldSettings = from s in preloadedAssets
                where s != null && s.GetType() == typeof(BoardGeneralSettings)
                select s;

            if (oldSettings != null && oldSettings.Any())
            {
                var assets = preloadedAssets.ToList();
                foreach (var s in oldSettings)
                {
                    assets.Remove(s);
                }

                PlayerSettings.SetPreloadedAssets(assets.ToArray());
            }
        }

        /// <summary>
        /// Callback invoked by Unity after the Android Gradle project is generated and before the build process begins.
        /// </summary>
        /// <param name="path">The path to the root of the Unity library Gradle project.</param>
        public void OnPostGenerateGradleAndroidProject(string path)
        {
            var androidManifest = new AndroidManifest(GetManifestPath(path));
            androidManifest.SetBoardApplicationId(BoardGeneralSettings.instance.applicationId);
            androidManifest.Save();
        }
    }
}
#endif //UNITY_ANDROID