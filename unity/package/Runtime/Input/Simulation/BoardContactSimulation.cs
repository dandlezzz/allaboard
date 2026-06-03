// <copyright file="BoardContactSimulation.cs" company="Harris Hill Products Inc.">
//     Copyright (c) Harris Hill Products Inc. All rights reserved.
// </copyright>

#if UNITY_EDITOR

namespace Board.Input.Simulation
{
    using System;
    using System.Collections;
    using System.Collections.Generic;

    using UnityEngine;
    using UnityEngine.InputSystem;
    using UnityEngine.InputSystem.LowLevel;
    using UnityEngine.UI;

#if UNITY_EDITOR
    using UnityEditor;
#endif

    /// <summary>
    /// Provides a mechanism to simulate input on a Board device in the Unity Editor.
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    [RequireComponent(typeof(CanvasScaler))]
    [ExecuteInEditMode]
    public class BoardContactSimulation : MonoBehaviour
    {
        /// <summary>
        /// Encapsulates information about references to an <see cref="InputAction"/>.
        /// </summary>
        private struct InputActionReferenceState
        {
            /// <summary>
            /// Gets the number of references to an <see cref="InputAction"/>.
            /// </summary>
            public int refCount;

            /// <summary>
            /// Gets a value indicating whether an <see cref="InputAction"/> was enabled by <see cref="BoardContactSimulation"/>.
            /// </summary>
            public bool enabledBySimulator;
        }

        private Canvas m_Canvas;

        private BoardContactSimulationSettings m_Settings;
        private Action<BoardContactSimulationSettings> m_OnSettingsChanged;

        private bool m_ActionsHooked;
        private InputActionReference m_TouchAction;
        private InputActionReference m_PlaceTouchedAction;
        private InputActionReference m_PlaceUntouchedAction;
        private InputActionReference m_LiftAction;
        private InputActionReference m_ClearAction;
        private InputActionReference m_RotateClockwiseAction;
        private InputActionReference m_RotateCounterClockwiseAction;
        private InputActionReference m_RotateAction;
        private InputActionReference m_FastRotateAction;

        private Action<InputAction.CallbackContext> m_OnTouchDelegate;
        private Action<InputAction.CallbackContext> m_OnPlaceTouchedDelegate;
        private Action<InputAction.CallbackContext> m_OnPlaceUntouchedDelegate;
        private Action<InputAction.CallbackContext> m_OnLiftDelegate;
        private Action<InputAction.CallbackContext> m_OnClearDelegate;
        private Action<InputAction.CallbackContext> m_OnSnapRotateClockwiseDelegate;
        private Action<InputAction.CallbackContext> m_OnSnapRotateCounterclockwiseDelegate;
        private Action<InputAction.CallbackContext> m_OnRotateDelegate;
        private Action<InputEventPtr, InputDevice> m_OnEvent;

        private BoardSimulationContact m_CurrentSimulatedContact;
        private BoardContactSimulationIcon m_CurrentIcon;
        private Vector2 m_IconPositionOffset = Vector2.zero;
        private readonly List<BoardSimulationContact> m_SimulatedContacts = new List<BoardSimulationContact>();
        private Coroutine m_RotateCoroutine;

        private static BoardContactSimulation s_Instance;

        private static Dictionary<InputAction, InputActionReferenceState> s_InputActionReferenceCounts =
            new Dictionary<InputAction, InputActionReferenceState>();

        /// <summary>
        /// Gets the current speed modifier to be applied to a <see cref="BoardSimulationContact"/> when rotating.
        /// </summary>
        private float rotationSpeedModifier => m_FastRotateAction.action.phase == InputActionPhase.Performed
            ? m_Settings.fastRotationSpeedMultiplier
            : 1f;

        /// <summary>
        /// Gets the current instance of <see cref="BoardContactSimulation"/>.
        /// </summary>
        public static BoardContactSimulation instance => s_Instance;

        /// <summary>
        /// Occurs when the current <see cref="BoardContactSimulationIcon"/> is cleared.
        /// </summary>
        public static event Action iconCleared;

        /// <summary>
        /// Occurs when the current <see cref="BoardContactSimulationIcon"/> is changed.
        /// </summary>
#pragma warning disable CS0067
        public static event Action iconChanged;
#pragma warning restore CS0067

        /// <summary>
        /// Gets or sets the current <see cref="BoardContactSimulationIcon"/>.
        /// </summary>
        public BoardContactSimulationIcon currentIcon
        {
            get => m_CurrentIcon;
            set
            {
                if (m_CurrentIcon == value)
                {
                    return;
                }

                m_CurrentIcon = value;
                m_IconPositionOffset = Vector2.zero;
                if (m_CurrentSimulatedContact != null)
                {
                    if (m_CurrentIcon == null)
                    {
                        m_CurrentSimulatedContact.gameObject.SetActive(false);
                    }

                    m_CurrentSimulatedContact.icon = m_CurrentIcon;
                }
            }
        }

        /// <summary>
        /// Gets or sets a value determining whether the mouse should be simulated as a finger.
        /// </summary>
        public bool useMouseAsFinger { get; set; }

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

            m_Settings = BoardContactSimulationSettings.instance;
            ApplySettings();

            if (m_OnSettingsChanged == null)
            {
                m_OnSettingsChanged = OnSettingsChanged;
            }

            if (m_OnEvent == null)
            {
                m_OnEvent = OnEvent;
            }

            BoardContactSimulationSettings.changed += m_OnSettingsChanged;
            InputSystem.onEvent += m_OnEvent;
        }

        /// <summary>
        /// Callback invoked when the <see cref="MonoBehaviour"/> disabled and inactive.
        /// </summary>
        private void OnDisable()
        {
            // Cancel all contacts that are ongoing.
            for (var i = 0; i < m_SimulatedContacts.Count; i++)
            {
                var contact = m_SimulatedContacts[i];
                contact.Cancel();
                Destroy(contact.gameObject);
            }

            m_SimulatedContacts.Clear();

            if (m_RotateCoroutine != null)
            {
                StopCoroutine(m_RotateCoroutine);
            }

            DisableAllActions();
            UnhookActions();
            BoardContactSimulationSettings.changed -= m_OnSettingsChanged;
            InputSystem.onEvent -= m_OnEvent;
        }

        /// <summary>
        /// Callback invoked when the <see cref="MonoBehaviour"/> is destroyed.
        /// </summary>
        private void OnDestroy()
        {
            UnhookActions();
            if (m_OnSettingsChanged == null)
            {
                BoardContactSimulationSettings.changed -= m_OnSettingsChanged;
            }

            if (m_OnEvent != null)
            {
                InputSystem.onEvent -= m_OnEvent;
            }
        }

        /// <summary>
        /// Callback invoked when the <see cref="MonoBehaviour"/> updates.
        /// </summary>
        private void Update()
        {
            for (var i = 0; i < m_SimulatedContacts.Count; i++)
            {
                var contact = m_SimulatedContacts[i];
                if (contact.icon == null && contact.isPlaced)
                {
                    contact.Cancel();
                    Destroy(contact.gameObject);
                    m_SimulatedContacts.RemoveAt(i);
                    i--;
                }
            }

            if (useMouseAsFinger && m_CurrentIcon == null)
            {
                var position = Mouse.current.position.ReadValue();
                if (Mouse.current.leftButton.wasPressedThisFrame && m_CurrentSimulatedContact == null)
                {
                    foreach (var contact in m_SimulatedContacts)
                    {
                        if (contact.Raycast(position) && contact.isPlaced)
                        {
                            return;
                        }
                    }

                    m_CurrentSimulatedContact = CreateOrGetUnplacedContact();
                    m_CurrentSimulatedContact.icon = null;
                    m_CurrentSimulatedContact.gameObject.SetActive(true);
                    m_CurrentSimulatedContact.MoveTo(position);
                    m_CurrentSimulatedContact.Place(true);
                }
                else if (Mouse.current.leftButton.wasReleasedThisFrame && m_CurrentSimulatedContact != null)
                {
                    m_CurrentSimulatedContact.Lift();
                    ClearCurrentContact();
                    m_CurrentSimulatedContact = null;
                }
                else if (Mouse.current.leftButton.isPressed && m_CurrentSimulatedContact != null &&
                         m_CurrentSimulatedContact.icon == null)
                {
                    m_CurrentSimulatedContact.MoveTo(position);
                }
            }
        }

        /// <summary>
        /// Callback invoked when the <see cref="BoardContactSimulationSettings"/> changes
        /// </summary>
        /// <param name="settings">The <see cref="BoardContactSimulationSettings"/> that have changed</param>
        private void OnSettingsChanged(BoardContactSimulationSettings settings)
        {
            m_Settings = settings;
            ApplySettings();
        }

        /// <summary>
        /// Applies current <see cref="BoardContactSimulationSettings"/> to the simulator.
        /// </summary>
        private void ApplySettings()
        {
            if (m_Settings.actionsAsset == null)
            {
                Debug.LogWarning(
                    "[Board] Simulator is missing input actions asset. Please update in Simulator settings.");
            }

            UnhookActions();

            if (m_TouchAction == null)
            {
                m_TouchAction = m_Settings.touchAction;
            }
            else
            {
                m_TouchAction = UpdateReferenceForNewAsset(m_TouchAction);
            }

            if (m_TouchAction == null)
            {
                Debug.LogWarning(
                    "[Board] Simulator is missing Touch input action. Please update in Simulator settings.");
            }

            if (m_PlaceTouchedAction == null)
            {
                m_PlaceTouchedAction = m_Settings.placeTouchedAction;
            }
            else
            {
                m_PlaceTouchedAction = UpdateReferenceForNewAsset(m_PlaceTouchedAction);
            }

            if (m_PlaceTouchedAction == null)
            {
                Debug.LogWarning(
                    "[Board] Simulator is missing Place Touched input action. Please update in Simulator settings.");
            }

            if (m_PlaceUntouchedAction == null)
            {
                m_PlaceUntouchedAction = m_Settings.placeUntouchedAction;
            }
            else
            {
                m_PlaceUntouchedAction = UpdateReferenceForNewAsset(m_PlaceUntouchedAction);
            }

            if (m_PlaceUntouchedAction == null)
            {
                Debug.LogWarning(
                    "[Board] Simulator is missing Place Untouched input action. Please update in Simulator settings.");
            }

            if (m_LiftAction == null)
            {
                m_LiftAction = m_Settings.liftAction;
            }
            else
            {
                m_LiftAction = UpdateReferenceForNewAsset(m_LiftAction);
            }

            if (m_LiftAction == null)
            {
                Debug.LogWarning(
                    "[Board] Simulator is missing Lift input action. Please update in Simulator settings.");
            }

            if (m_ClearAction == null)
            {
                m_ClearAction = m_Settings.clearAction;
            }
            else
            {
                m_ClearAction = UpdateReferenceForNewAsset(m_ClearAction);
            }

            if (m_ClearAction == null)
            {
                Debug.LogWarning(
                    "[Board] Simulator is missing Cancel input action. Please update in Simulator settings.");
            }

            if (m_RotateClockwiseAction == null)
            {
                m_RotateClockwiseAction = m_Settings.snapRotateClockwiseAction;
            }
            else
            {
                m_RotateClockwiseAction = UpdateReferenceForNewAsset(m_RotateClockwiseAction);
            }

            if (m_RotateClockwiseAction == null)
            {
                Debug.LogWarning(
                    "[Board] Simulator is missing Snap Rotate Clockwise input action. Please update in Simulator settings.");
            }

            if (m_RotateCounterClockwiseAction == null)
            {
                m_RotateCounterClockwiseAction = m_Settings.snapRotateCounterclockwiseAction;
            }
            else
            {
                m_RotateCounterClockwiseAction = UpdateReferenceForNewAsset(m_RotateCounterClockwiseAction);
            }

            if (m_RotateCounterClockwiseAction == null)
            {
                Debug.LogWarning(
                    "[Board] Simulator is missing Snap Rotate Counterclockwise input action. Please update in Simulator settings.");
            }

            if (m_RotateAction == null)
            {
                m_RotateAction = m_Settings.rotateAction;
            }
            else
            {
                m_RotateAction = UpdateReferenceForNewAsset(m_RotateAction);
            }

            if (m_RotateAction == null)
            {
                Debug.LogWarning(
                    "[Board] Simulator is missing Rotate input action. Please update in Simulator settings.");
            }

            if (m_FastRotateAction == null)
            {
                m_FastRotateAction = m_Settings.fastRotateAction;
            }
            else
            {
                m_FastRotateAction = UpdateReferenceForNewAsset(m_FastRotateAction);
            }

            if (m_FastRotateAction == null)
            {
                Debug.LogWarning(
                    "[Board] Simulator is missing Fast Rotate input action. Please update in Simulator settings.");
            }

            HookActions();
            EnableAllActions();
        }

        /// <summary>
        /// Hooks delegates into the current input actions for the simulator.
        /// </summary>
        private void HookActions()
        {
            if (m_ActionsHooked)
            {
                return;
            }

            if (m_OnTouchDelegate == null)
            {
                m_OnTouchDelegate = OnTouchCallback;
            }

            if (m_OnPlaceTouchedDelegate == null)
            {
                m_OnPlaceTouchedDelegate = OnPlaceTouchedCallback;
            }

            if (m_OnPlaceUntouchedDelegate == null)
            {
                m_OnPlaceUntouchedDelegate = OnPlaceUntouchedCallback;
            }

            if (m_OnLiftDelegate == null)
            {
                m_OnLiftDelegate = OnLiftCallback;
            }

            if (m_OnClearDelegate == null)
            {
                m_OnClearDelegate = OnClearCallback;
            }

            if (m_OnSnapRotateClockwiseDelegate == null)
            {
                m_OnSnapRotateClockwiseDelegate = OnSnapRotateClockwiseCallback;
            }

            if (m_OnSnapRotateCounterclockwiseDelegate == null)
            {
                m_OnSnapRotateCounterclockwiseDelegate = OnSnapRotateCounterclockwiseCallback;
            }

            if (m_OnRotateDelegate == null)
            {
                m_OnRotateDelegate = OnRotateCallback;
            }

            SetActionCallbacks(true);
        }

        /// <summary>
        /// Unhooks delegates from the current input actions for the simulator.
        /// </summary>
        private void UnhookActions()
        {
            if (!m_ActionsHooked)
            {
                return;
            }

            SetActionCallbacks(false);
        }

        /// <summary>
        /// Subscribes or unsubscribes to the input actions for the current <see cref="BoardContactSimulationSettings"/>.
        /// </summary>
        /// <param name="subscribe"><see langword="true"/> if the simulator should subscribe; otherwise, <see langword="false"/>.</param>
        private void SetActionCallbacks(bool subscribe)
        {
            m_ActionsHooked = subscribe;
            SetActionCallback(m_TouchAction, m_OnTouchDelegate, subscribe);
            SetActionCallback(m_PlaceTouchedAction, m_OnPlaceTouchedDelegate, subscribe);
            SetActionCallback(m_PlaceUntouchedAction, m_OnPlaceUntouchedDelegate, subscribe);
            SetActionCallback(m_LiftAction, m_OnLiftDelegate, subscribe);
            SetActionCallback(m_ClearAction, m_OnClearDelegate, subscribe);
            SetActionCallback(m_RotateClockwiseAction, m_OnSnapRotateClockwiseDelegate, subscribe);
            SetActionCallback(m_RotateCounterClockwiseAction, m_OnSnapRotateCounterclockwiseDelegate, subscribe);
            SetActionCallback(m_RotateAction, m_OnRotateDelegate, subscribe);
        }

        /// <summary>
        /// Subscribes or unsubscribes the specified callback function from the specified input action.
        /// </summary>
        /// <param name="actionReference">The reference to the <see cref="InputAction"/> to subscribe or unsubscribe to.</param>
        /// <param name="callback">The callback function to subscribe or unsubscribe.</param>
        /// <param name="subscribe"><see langword="true"/> if subscribing; otherwise, <see langword="false"/>.</param>
        private static void SetActionCallback(InputActionReference actionReference,
            Action<InputAction.CallbackContext> callback, bool subscribe)
        {
            if (!subscribe && callback == null)
            {
                return;
            }

            if (actionReference == null)
            {
                return;
            }

            var action = actionReference.action;
            if (action == null)
            {
                return;
            }

            if (subscribe)
            {
                action.performed += callback;
                action.canceled += callback;
            }
            else
            {
                action.performed -= callback;
                action.canceled -= callback;
            }
        }

        /// <summary>
        /// Creates a new <see cref="InputActionReference"/> from an existing <see cref="InputActionReference"/>.
        /// </summary>
        /// <param name="actionReference">An <see cref="InputActionReference"/>.</param>
        /// <returns>A new <see cref="InputActionReference"/> that references the same <see cref="InputAction"/> as
        /// <paramref name="actionReference"/>.</returns>
        private InputActionReference UpdateReferenceForNewAsset(InputActionReference actionReference)
        {
            var oldAction = actionReference?.action;
            if (oldAction == null)
            {
                return null;
            }

            var oldActionMap = oldAction.actionMap;
            Debug.Assert(oldActionMap != null, "Not expected to end up with a singleton action here");

            var newActionMap = m_Settings.actionsAsset?.FindActionMap(oldActionMap.name);
            if (newActionMap == null)
            {
                return null;
            }

            var newAction = newActionMap.FindAction(oldAction.name);
            if (newAction == null)
            {
                return null;
            }

            return InputActionReference.Create(newAction);
        }

        /// <summary>
        /// Callback invoked by the <see cref="InputSystem"/> when an input event has occured. 
        /// </summary>
        /// <param name="eventPtr">The pointer to the input event.</param>
        /// <param name="device">The <see cref="InputDevice"/> that generated the event.</param>
        private unsafe void OnEvent(InputEventPtr eventPtr, InputDevice device)
        {
            if (EditorApplication.isPaused)
            {
                return;
            }

            if (device is Pointer && device == Pointer.current)
            {
                var pointer = (Pointer)device;
                var positionControl = pointer.position;
                var positionStatePtr = positionControl.GetStatePtrFromStateEvent(eventPtr);
                if (positionStatePtr != null)
                {
                    var viewSize = Handles.GetMainGameViewSize();
                    var position = positionControl.ReadValueFromState(positionStatePtr);
                    if (position.x < 0 || position.y < 0 || position.x > viewSize.x || position.y > viewSize.y)
                    {
                        if (m_CurrentSimulatedContact != null)
                        {
                            if (m_CurrentSimulatedContact.isPlaced)
                            {
                                m_CurrentSimulatedContact.Lift();
                            }

                            m_CurrentSimulatedContact.gameObject.SetActive(false);
                        }
                    }
                    else
                    {
                        if (m_CurrentIcon != null)
                        {
                            m_CurrentSimulatedContact = CreateOrGetUnplacedContact();
                            m_CurrentSimulatedContact.gameObject.SetActive(true);
                            m_CurrentSimulatedContact.icon = m_CurrentIcon;
                        }

                        if (m_CurrentSimulatedContact != null && m_CurrentSimulatedContact.icon != null)
                        {
                            m_CurrentSimulatedContact.gameObject.SetActive(true);
                            m_CurrentSimulatedContact.MoveTo(m_IconPositionOffset + position);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Creates or gets an unplaced <see cref="BoardSimulationContact"/>.
        /// </summary>
        /// <returns>An unplaced <see cref="BoardSimulationContact"/>.</returns>
        private BoardSimulationContact CreateOrGetUnplacedContact()
        {
            if (m_CurrentSimulatedContact != null)
            {
                return m_CurrentSimulatedContact;
            }

            var simulatedContactGO = new GameObject("UnplacedContact");
            simulatedContactGO.transform.SetParent(transform);
            return simulatedContactGO.AddComponent<BoardSimulationContact>();
        }

        /// <summary>
        /// Enables all input actions.
        /// </summary>
        private void EnableAllActions()
        {
            EnableInputAction(m_TouchAction);
            EnableInputAction(m_PlaceTouchedAction);
            EnableInputAction(m_PlaceUntouchedAction);
            EnableInputAction(m_LiftAction);
            EnableInputAction(m_ClearAction);
            EnableInputAction(m_RotateClockwiseAction);
            EnableInputAction(m_RotateCounterClockwiseAction);
            EnableInputAction(m_RotateAction);
            EnableInputAction(m_FastRotateAction);
        }

        /// <summary>
        /// Disables all input actions.
        /// </summary>
        private void DisableAllActions()
        {
            TryDisableInputAction(m_TouchAction, true);
            TryDisableInputAction(m_PlaceTouchedAction, true);
            TryDisableInputAction(m_PlaceUntouchedAction, true);
            TryDisableInputAction(m_LiftAction, true);
            TryDisableInputAction(m_ClearAction, true);
            TryDisableInputAction(m_RotateClockwiseAction, true);
            TryDisableInputAction(m_RotateCounterClockwiseAction, true);
            TryDisableInputAction(m_RotateAction, true);
            TryDisableInputAction(m_FastRotateAction, true);
        }

        /// <summary>
        /// Enables an input action.
        /// </summary>
        /// <param name="inputActionReference">A reference to an <see cref="InputAction"/>.</param>
        private void EnableInputAction(InputActionReference inputActionReference)
        {
            var action = inputActionReference?.action;
            if (action == null)
            {
                return;
            }

            if (s_InputActionReferenceCounts.TryGetValue(action, out var referenceState))
            {
                referenceState.refCount++;
                s_InputActionReferenceCounts[action] = referenceState;
            }
            else
            {
                // If the action is already enabled but its reference count is zero then it was enabled by
                // something outside the simulator and thus the simulator should not disable it.
                referenceState = new InputActionReferenceState { refCount = 1, enabledBySimulator = !action.enabled };
                s_InputActionReferenceCounts.Add(action, referenceState);
            }

            action.Enable();
        }

        /// <summary>
        /// Disables a specified <see cref="InputAction"/>.
        /// </summary>
        /// <param name="inputActionReference">A reference to an <see cref="InputAction"/>.</param>
        /// <param name="isComponentDisabling"><see langword="true"/> if this component is disabling the input action;
        /// otherwise, <see langword="false"/>.</param>
        private void TryDisableInputAction(InputActionReference inputActionReference, bool isComponentDisabling = false)
        {
            var action = inputActionReference?.action;
            if (action == null)
            {
                return;
            }

            // Don't decrement refCount when we were not responsible for incrementing it.
            // I.e. when we were not enabled yet. When OnDisabled is called, isActiveAndEnabled will
            // already have been set to false. In that case we pass isComponentDisabling to check if we
            // came from OnDisabled and therefore need to allow disabling.
            if (!isActiveAndEnabled && !isComponentDisabling)
            {
                return;
            }

            if (!s_InputActionReferenceCounts.TryGetValue(action, out var referenceState))
            {
                return;
            }

            if (referenceState.refCount - 1 == 0 && referenceState.enabledBySimulator)
            {
                action.Disable();
                s_InputActionReferenceCounts.Remove(action);
                return;
            }

            referenceState.refCount--;
            s_InputActionReferenceCounts[action] = referenceState;
        }

        /// <summary>
        /// Callback invoked when the touch action occurs.
        /// </summary>
        /// <param name="context">Information about what triggered the touch action.</param>
        private void OnTouchCallback(InputAction.CallbackContext context)
        {
            if (context.performed && m_CurrentSimulatedContact == null)
            {
                var pointer = context.control.device as Pointer;
                if (pointer == null)
                {
                    pointer = Pointer.current;
                    if (pointer == null)
                    {
                        return;
                    }
                }

                var position = pointer.position.value;
                foreach (var contact in m_SimulatedContacts)
                {
                    if (contact.Raycast(position) && contact.isPlaced)
                    {
                        contact.Touch();
                        m_CurrentSimulatedContact = contact;
                        m_IconPositionOffset = (Vector2)contact.transform.position - position;
                        return;
                    }
                }
            }
            else if (context.canceled)
            {
                if (m_CurrentSimulatedContact != null && m_CurrentSimulatedContact.isTouched &&
                    m_CurrentSimulatedContact.icon != null)
                {
                    m_CurrentSimulatedContact.Untouch();
                    m_IconPositionOffset = Vector2.zero;
                    if (m_RotateCoroutine != null)
                    {
                        StopCoroutine(m_RotateCoroutine);
                    }

                    m_CurrentSimulatedContact = null;
                }
            }
        }

        /// <summary>
        /// Callback invoked when the place touched action occurs.
        /// </summary>
        /// <param name="context">Information about what triggered the place action.</param>
        private void OnPlaceTouchedCallback(InputAction.CallbackContext context)
        {
            if (context.performed && m_CurrentSimulatedContact != null &&
                m_CurrentSimulatedContact.gameObject.activeSelf && !m_CurrentSimulatedContact.isPlaced)
            {
                m_CurrentSimulatedContact.Place(true);
                if (!m_SimulatedContacts.Contains(m_CurrentSimulatedContact))
                {
                    m_SimulatedContacts.Add(m_CurrentSimulatedContact);
                }

                m_CurrentIcon = null;
                iconCleared?.Invoke();
            }
            else if (context.canceled && m_CurrentSimulatedContact != null && m_CurrentSimulatedContact.icon != null &&
                     m_CurrentSimulatedContact.gameObject.activeSelf && m_CurrentSimulatedContact.isTouched)
            {
                m_CurrentSimulatedContact.Untouch();
                m_IconPositionOffset = Vector2.zero;
                if (m_RotateCoroutine != null)
                {
                    StopCoroutine(m_RotateCoroutine);
                }

                m_CurrentSimulatedContact = null;
            }
        }

        /// <summary>
        /// Callback invoked when the place untouched action occurs.
        /// </summary>
        /// <param name="context">Information about what triggered the place action.</param>
        private void OnPlaceUntouchedCallback(InputAction.CallbackContext context)
        {
            if (context.performed && m_CurrentSimulatedContact != null &&
                m_CurrentSimulatedContact.gameObject.activeSelf && !m_CurrentSimulatedContact.isPlaced)
            {
                m_CurrentSimulatedContact.Place(false);
                if (!m_SimulatedContacts.Contains(m_CurrentSimulatedContact))
                {
                    m_SimulatedContacts.Add(m_CurrentSimulatedContact);
                }

                m_CurrentIcon = null;
                iconCleared?.Invoke();
            }
            else if (context.canceled && m_CurrentSimulatedContact != null && m_CurrentSimulatedContact.icon != null &&
                     m_CurrentSimulatedContact.gameObject.activeSelf && m_CurrentSimulatedContact.isPlaced)
            {
                m_CurrentSimulatedContact.Untouch();
                m_IconPositionOffset = Vector2.zero;
                if (m_RotateCoroutine != null)
                {
                    StopCoroutine(m_RotateCoroutine);
                }

                m_CurrentSimulatedContact = null;
            }
        }

        /// <summary>
        /// Callback invoked when the lift action occurs.
        /// </summary>
        /// <param name="context">Information about what triggered the lift action.</param>
        private void OnLiftCallback(InputAction.CallbackContext context)
        {
            if (context.performed && m_CurrentIcon == null && (m_CurrentSimulatedContact == null ||
                                                               !m_CurrentSimulatedContact.gameObject.activeSelf))
            {
                var pointer = context.control.device as Pointer;

                if (pointer == null)
                {
                    pointer = Pointer.current;
                    if (pointer == null)
                    {
                        return;
                    }
                }

                var position = pointer.position.value;
                foreach (var contact in m_SimulatedContacts)
                {
                    if (contact.Raycast(position) && contact.isPlaced)
                    {
                        if (contact.Lift())
                        {
                            m_CurrentSimulatedContact = contact;
                            m_CurrentIcon = contact.icon;
                            m_IconPositionOffset = (Vector2)contact.transform.position - position;
                            return;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Callback invoked when the clear action occurs.
        /// </summary>
        /// <param name="context">Information about what triggered the clear action.</param>
        private void OnClearCallback(InputAction.CallbackContext context)
        {
            if (context.performed)
            {
                ClearCurrentContact();
            }
        }

        /// <summary>
        /// Callback invoked when the snap rotate clockwise action occurs.
        /// </summary>
        /// <param name="context">Information about what triggered the snap rotate clockwise action.</param>
        private void OnSnapRotateClockwiseCallback(InputAction.CallbackContext context)
        {
            if (context.performed && m_CurrentSimulatedContact != null)
            {
                m_CurrentSimulatedContact.Rotate(-m_Settings.snapRotationDegrees * rotationSpeedModifier);
            }
        }

        /// <summary>
        /// Callback invoked when the snap rotate counterclockwise action occurs.
        /// </summary>
        /// <param name="context">Information about what triggered the snap rotate counterclockwise action.</param>
        private void OnSnapRotateCounterclockwiseCallback(InputAction.CallbackContext context)
        {
            if (context.performed && m_CurrentSimulatedContact != null)
            {
                m_CurrentSimulatedContact.Rotate(m_Settings.snapRotationDegrees * rotationSpeedModifier);
            }
        }

        /// <summary>
        /// Callback invoked when the rotate action occurs.
        /// </summary>
        /// <param name="context">Information about what triggered the rotation action.</param>
        private void OnRotateCallback(InputAction.CallbackContext context)
        {
            if (context.performed && m_CurrentSimulatedContact != null)
            {
                if (context.valueType != typeof(Single))
                {
                    Debug.LogWarning("[BoardContactSimulation] Rotation action must be of type axis.");
                    return;
                }

                if (m_RotateCoroutine != null)
                {
                    StopCoroutine(this.m_RotateCoroutine);
                }

                m_RotateCoroutine = StartCoroutine(RotateProcess(context));
            }
        }

        /// <summary>
        /// Continuously rotate simulated contact as long as the rotate action is being performed.
        /// </summary>
        /// <param name="callbackContext">Information about what triggered the rotation action.</param>
        private IEnumerator RotateProcess(InputAction.CallbackContext callbackContext)
        {
            var delta = callbackContext.ReadValue<float>();

            while (Mathf.Abs(delta) > 0.01f && m_CurrentSimulatedContact != null)
            {
                m_CurrentSimulatedContact.Rotate(delta * m_Settings.rotationSpeed * rotationSpeedModifier);

                yield return null;

                delta = callbackContext.ReadValue<float>();
            }
        }

        /// <summary>
        /// Clears the current simulated contact.
        /// </summary>
        public void ClearCurrentContact()
        {
            if (m_CurrentSimulatedContact == null)
            {
                m_CurrentIcon = null;
                iconCleared?.Invoke();
                return;
            }

            if (m_SimulatedContacts.Contains(m_CurrentSimulatedContact))
            {
                m_SimulatedContacts.Remove(m_CurrentSimulatedContact);
            }

            if (m_RotateCoroutine != null)
            {
                StopCoroutine(m_RotateCoroutine);
            }

            Destroy(m_CurrentSimulatedContact.gameObject);
            m_CurrentSimulatedContact = null;
            m_IconPositionOffset = Vector2.zero;
            m_CurrentIcon = null;
            iconCleared?.Invoke();
        }

        /// <summary>
        /// Clears all simulated contacts.
        /// </summary>
        public void ClearAllContacts()
        {
            foreach (var contact in m_SimulatedContacts)
            {
                contact.Lift();
                Destroy(contact.gameObject);
            }

            m_SimulatedContacts.Clear();
        }

        /// <summary>
        /// Enables the current instance.
        /// </summary>
        public static void Enable()
        {
            if (instance == null)
            {
                s_Instance = FindObjectOfType<BoardContactSimulation>();
                if (!s_Instance)
                {
                    var hiddenGameObject = new GameObject(nameof(BoardContactSimulation));
                    hiddenGameObject.hideFlags = HideFlags.HideAndDontSave;
                    DontDestroyOnLoad(hiddenGameObject);
                    s_Instance = hiddenGameObject.AddComponent<BoardContactSimulation>();
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

#endif