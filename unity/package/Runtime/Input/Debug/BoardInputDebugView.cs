// <copyright file="BoardInputDebugView.cs" company="Harris Hill Products Inc.">
//     Copyright (c) Harris Hill Products Inc. All rights reserved.
// </copyright>

#if UNITY_EDITOR || DEVELOPMENT_BUILD

namespace Board.Input.Debug
{
    using System.Collections.Generic;
    using System.Text;

    using UnityEngine;
    using UnityEngine.UI;

#if UNITY_EDITOR
    using UnityEditor;
#endif

    /// <summary>
    /// Provides a mechanism to display debug information about Board input.
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    [RequireComponent(typeof(CanvasScaler))]
    [ExecuteInEditMode]
    internal class BoardInputDebugView : MonoBehaviour
    {
        private Canvas m_Canvas;

        private readonly Dictionary<int, BoardContactDebugView> m_ContactDebugInstances =
            new Dictionary<int, BoardContactDebugView>();

        private StringBuilder m_DebugTextBuilder = new StringBuilder();

        private static BoardInputDebugView s_Instance;
        private GameObject m_DebugLabelContainer;
        private Text m_DebugLabel;

        /// <summary>
        /// Gets the current instance of <see cref="BoardInputDebugView"/>.
        /// </summary>
        public static BoardInputDebugView instance => s_Instance;

        /// <summary>
        /// Callback invoked when the <see cref="MonoBehaviour"/> becomes enabled and active.
        /// </summary>
        private void OnEnable()
        {
            // Make the canvas for rendering an overlay canvas that is above every other canvas
            m_Canvas = GetComponent<Canvas>();
            m_Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            m_Canvas.sortingOrder = short.MaxValue;
            m_Canvas.vertexColorAlwaysGammaSpace = true;

            var canvasScalar = GetComponent<CanvasScaler>();
            canvasScalar.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasScalar.matchWidthOrHeight = 1f;
            canvasScalar.referenceResolution = new Vector2(1920, 1080);


            // Create the container that will display all the contact information
            m_DebugLabelContainer = new GameObject("DebugLabelContainer", typeof(RectTransform));
            m_DebugLabelContainer.transform.SetParent(m_Canvas.transform, false);

            var rectTransform = m_DebugLabelContainer.GetComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0, 1);
            rectTransform.anchorMax = Vector2.one;
            rectTransform.pivot = new Vector2(0.5f, 1);
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;

            var contentSizeFitter = m_DebugLabelContainer.AddComponent<ContentSizeFitter>();
            contentSizeFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            contentSizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var verticalLayoutGroup = m_DebugLabelContainer.AddComponent<VerticalLayoutGroup>();
            verticalLayoutGroup.padding = new RectOffset(10, 0, 10, 0);

            var image = m_DebugLabelContainer.AddComponent<Image>();
            image.color = new Color(0, 0, 0, 0.25f);
            image.raycastTarget = false;

            // Create the text label
            var debugLabel = new GameObject("DebugLabel", typeof(RectTransform));
            debugLabel.transform.SetParent(m_DebugLabelContainer.transform, false);
            rectTransform = debugLabel.GetComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;

            m_DebugLabel = debugLabel.AddComponent<Text>();
            m_DebugLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            m_DebugLabel.fontSize = 18;
            m_DebugLabel.raycastTarget = false;

            // Add every contact that is already active
            var activeGlyphs = BoardInput.GetActiveContacts();

            for (var i = 0; i < activeGlyphs.Length; i++)
            {
                var contact = activeGlyphs[i];
                if (contact.isNoneEndedOrCanceled)
                {
                    continue;
                }
                var view = CreateContactDebugView();
                view.SetPositionAndRotation(contact);
                m_ContactDebugInstances.Add(contact.contactId, view);
            }
        }

        /// <summary>
        /// Callback invoked when the <see cref="MonoBehaviour"/> disabled and inactive.
        /// </summary>
        private void OnDisable()
        {
            // Destroy all children
            for (var i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i);
                GameObject.Destroy(child.gameObject);
            }

            // Clear the contact view list
            m_ContactDebugInstances.Clear();
        }

        /// <summary>
        /// Callback invoked when the <see cref="MonoBehaviour"/> updates.
        /// </summary>
        private void Update()
        {
            ProcessContacts();
        }

        /// <summary>
        /// Creates a new <see cref="BoardContactDebugView"/>.
        /// </summary>
        /// <returns>A new instance of <see cref="BoardContactDebugView"/>.</returns>
        private BoardContactDebugView CreateContactDebugView()
        {
            var contactDebugView = new GameObject("BoardContactView", typeof(RectTransform));
            contactDebugView.transform.SetParent(m_Canvas.transform, false);
            return contactDebugView.AddComponent<BoardContactDebugView>();
        }

        /// <summary>
        /// Process the current active contacts.
        /// </summary>
        private void ProcessContacts()
        {
            var contacts = BoardInput.GetActiveContacts();
            m_DebugTextBuilder.Clear();

            // Iterate through the list of contacts
            for (var i = 0; i < contacts.Length; i++)
            {
                BoardContactDebugView info;
                var contact = contacts[i];
                var position = contact.screenPosition;
                switch (contact.phase)
                {
                    case BoardContactPhase.Began:
                        // Make sure we haven't already made a debug game object for this contact
                        if (m_ContactDebugInstances.ContainsKey(contact.contactId))
                        {
                            return;
                        }

                        // Create a new debug info game object and assign this contact to it.
                        info = CreateContactDebugView();
                        info.SetPositionAndRotation(contact);
                        m_ContactDebugInstances.Add(contact.contactId, info);
                        break;
                    case BoardContactPhase.Moved:
                    case BoardContactPhase.Stationary:
                        if (m_ContactDebugInstances.TryGetValue(contact.contactId, out info))
                        {
                            info.SetPositionAndRotation(contact);
                        }

                        break;
                    case BoardContactPhase.Canceled:
                    case BoardContactPhase.Ended:
                        // Find the debug info object associated with this contact
                        if (m_ContactDebugInstances.TryGetValue(contact.contactId, out info))
                        {
                            // Remove it from the dictionary and destroy it
                            m_ContactDebugInstances.Remove(contact.contactId);
                            Destroy(info.gameObject);
                        }

                        break;
                    default:
                        break;
                }

                // Format a string for debug text to log to the screen
                if (contact.type == BoardContactType.Glyph)
                {

                    m_DebugTextBuilder.AppendLine(
                        $"{contact.contactId,3}:  {contact.type,6}  {contact.phase,10}  {position,20}  {(contact.orientation * Mathf.Rad2Deg).ToString("n2"),7}  Touched: {contact.isTouched,5}  Glyph: {contact.glyphId}");
                }
                else
                {
                    m_DebugTextBuilder.AppendLine(
                        $"{contact.contactId,3}:  {contact.type,6}  {contact.phase,10}  {position,20}  {(contact.orientation * Mathf.Rad2Deg).ToString("n2"),7}  Touched: {contact.isTouched,5}");
                }
            }

            // Update the debug text label
            m_DebugLabel.text = m_DebugTextBuilder.ToString();
        }
        
        /// <summary>
        /// Clears all debug crosshairs immediately.
        /// Used when activity stops to clean up any remaining visual artifacts.
        /// </summary>
        internal static void Clear()
        {
            if (instance == null)
            {
                return;
            }

            foreach (var kvp in instance.m_ContactDebugInstances)
            {
                if (kvp.Value != null)
                {
                    DestroyImmediate(kvp.Value.gameObject);
                }
            }

            instance.m_ContactDebugInstances.Clear();
        }

        /// <summary>
        /// Enables the current instance.
        /// </summary>
        public static void Enable()
        {
            if (instance == null)
            {
                s_Instance = FindObjectOfType<BoardInputDebugView>();
                if (!s_Instance)
                {
                    var hiddenGameObject = new GameObject(nameof(BoardInputDebugView));
                    hiddenGameObject.hideFlags = HideFlags.HideAndDontSave;
                    DontDestroyOnLoad(hiddenGameObject);
                    s_Instance = hiddenGameObject.AddComponent<BoardInputDebugView>();
                }

                instance.gameObject.SetActive(true);
            }

            instance.gameObject.SetActive(true);
        }

        /// <summary>
        /// Disables the current instance.
        /// </summary>
        public static void Disable()
        {
            if (instance != null)
            {
                instance.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Destroys the current instance.
        /// </summary>
        public static void Destroy()
        {
            Disable();

            if (s_Instance != null)
            {
                Destroy(s_Instance.gameObject);
                s_Instance = null;
            }
        }
    }
}

#endif // UNITY_EDITOR || DEVELOPMENT_BUILD