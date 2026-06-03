// <copyright file="BoardUIInputModule.cs" company="Harris Hill Products Inc.">
//     Copyright (c) Harris Hill Products Inc. All rights reserved.
// </copyright>

namespace Board.Input
{
    using System.Collections.Generic;
    using System.Text;

    using Board.Core;

    using UnityEngine;
    using UnityEngine.EventSystems;

    /// <summary>
    /// UI input module for input from Board.
    /// </summary>
    /// <remarks>
    /// When running on Board hardware and <see cref="m_DisableCompetingModules"/> is
    /// <c>true</c> (the default), this module automatically disables any other
    /// <see cref="BaseInputModule"/> components on the same GameObject (e.g.,
    /// <c>InputSystemUIInputModule</c> or <c>StandaloneInputModule</c>) to prevent
    /// input conflicts. In the Editor, no modules are disabled so standard mouse/keyboard
    /// input continues to work. Set <see cref="m_DisableCompetingModules"/> to <c>false</c>
    /// in the Inspector to opt out of this behavior.
    /// </remarks>
    public class BoardUIInputModule : PointerInputModule
    {
        /// <summary>
        /// Maximum number of pointer slots for UIToolkit compatibility.
        /// UIToolkit's PointerDispatchState supports up to 32 pointers (PointerId.maxPointers).
        /// We use 30 to leave headroom for system pointers.
        /// </summary>
        private const int kMaxPointerSlots = 30;

        [SerializeField] private bool m_ForceModuleActive;
        [SerializeField] private BoardContactTypeMask m_InputMask = new BoardContactTypeMask(BoardContactType.Finger);

        [SerializeField]
        [Tooltip("When running on Board hardware, automatically disable other input modules on this GameObject.")]
        private bool m_DisableCompetingModules = true;

        private PointerEventData m_InputPointerEvent;

        /// <summary>
        /// Maps native contact IDs to assigned pointer slots.
        /// </summary>
        private readonly Dictionary<int, int> m_ContactToSlot = new Dictionary<int, int>();

        /// <summary>
        /// Pool of available pointer slots for assignment.
        /// </summary>
        private readonly Queue<int> m_FreeSlots = new Queue<int>();
        
        /// <summary>
        /// Gets a value that indicates whether the <see cref="BoardUIInputModule"/> should be forced to be active.
        /// </summary>
        /// <value>
        /// <c>true</c> if the <see cref="BoardUIInputModule"/> should be forced to be active; otherwise, <c>false</c>.
        /// </value>
        public bool forceModuleActive
        {
            get { return m_ForceModuleActive; }
            set { m_ForceModuleActive = value; }
        }
        
        /// <summary>
        /// Gets a value that indicates whether the <see cref="BoardUIInputModule"/> should use the mouse as fake input.
        /// </summary>
        /// <value>
        /// <c>true</c> if the <see cref="BoardUIInputModule"/> should use the mouse as fake input; otherwise, <c>false</c>.
        /// </value>
        private bool useFakeInput => !BoardSupport.enabled;
        
        /// <inheritdoc/>
        protected override void OnEnable()
        {
            base.OnEnable();
            ResetPointerSlots();
            if (m_DisableCompetingModules)
            {
                DisableCompetingInputModules();
            }
        }

        /// <summary>
        /// Disables other <see cref="BaseInputModule"/> components on the same GameObject.
        /// Only runs on Board hardware to avoid interfering with Editor input.
        /// </summary>
        private void DisableCompetingInputModules()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            foreach (var module in GetComponents<BaseInputModule>())
            {
                if (module != this && module.enabled)
                {
                    module.enabled = false;
                    UnityEngine.Debug.Log($"[BoardUIInputModule] Disabled {module.GetType().Name} on '{gameObject.name}' — Board handles UI input on device.");
                }
            }
#endif
        }

        /// <inheritdoc/>
        protected override void OnDisable()
        {
            ResetPointerSlots();
            base.OnDisable();
        }

        /// <summary>
        /// Resets the pool of pointer identifier slots.
        /// </summary>
        private void ResetPointerSlots()
        {
            m_FreeSlots.Clear();
            m_ContactToSlot.Clear();

            for (var i = 0; i < kMaxPointerSlots; i++)
            {
                m_FreeSlots.Enqueue(i);
            }
        }

        /// <summary>
        /// Assigns a pointer slot for the given contact identifier.
        /// Returns an existing slot if already assigned, or allocates from the free pool.
        /// </summary>
        /// <param name="contactId">The native contact identifier.</param>
        /// <returns>The assigned pointer slot identifier.</returns>
        private int GetOrAssignPointerSlot(int contactId)
        {
            if (m_ContactToSlot.TryGetValue(contactId, out var existingSlot))
            {
                return existingSlot;
            }

            if (m_FreeSlots.Count > 0)
            {
                var slot = m_FreeSlots.Dequeue();
                m_ContactToSlot[contactId] = slot;
                return slot;
            }
            
            return -1;
        }

        /// <summary>
        /// Releases a pointer slot back to the free pool when a contact ends.
        /// </summary>
        /// <param name="contactId">The native contact identifier.</param>
        private void ReleaseSlot(int contactId)
        {
            if (m_ContactToSlot.TryGetValue(contactId, out var slot))
            {
                m_ContactToSlot.Remove(contactId);
                m_FreeSlots.Enqueue(slot);
            }
        }

        /// <inheritdoc/>
        public override bool ShouldActivateModule()
        {
            if (!base.ShouldActivateModule())
            {
                return false;
            }

            if (m_ForceModuleActive)
            {
                return true;
            }

            return BoardSupport.enabled;
        }
        
        /// <inheritdoc/>
        public override void UpdateModule()
        {
            if (!eventSystem.isFocused)
            {
                if (m_InputPointerEvent != null && m_InputPointerEvent.pointerDrag != null &&
                    m_InputPointerEvent.dragging)
                {
                    ExecuteEvents.Execute(m_InputPointerEvent.pointerDrag, m_InputPointerEvent,
                        ExecuteEvents.endDragHandler);
                }

                m_InputPointerEvent = null;
            }
        }
        
        /// <inheritdoc/>
        public override bool IsModuleSupported()
        {
            return forceModuleActive || BoardSupport.enabled;
        }
        
        /// <inheritdoc/>
        public override void Process()
        {
            if (useFakeInput)
            {
                FakeTouches();
                return;
            }

            var contacts = BoardInput.GetActiveContacts(m_InputMask);
            for (var i = 0; i < contacts.Length; i++)
            {
                var pointer = GetContactPointerEventData(ref contacts[i], out var pressed, out var released);
                if (pointer == null)
                {
                    continue;
                }

                ProcessPointerData(pointer, pressed, released);

                if (!released)
                {
                    ProcessMove(pointer);
                    ProcessDrag(pointer);
                }
                else
                {
                    RemovePointerData(pointer);
                    ReleaseSlot(contacts[i].contactId);
                }
            }
        }

        /// <inheritdoc/>
        public override void DeactivateModule()
        {
            base.DeactivateModule();
            ClearSelection();
        }
        
        /// <inheritdoc/>
        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine(useFakeInput ? "Input: Faked" : "Input: Touch");
            if (useFakeInput)
            {
                var pointerData = GetLastPointerEventData(kMouseLeftId);
                if (pointerData != null)
                {
                    sb.AppendLine(pointerData.ToString());
                }
            }
            else
            {
                foreach (var pointerEventData in m_PointerData)
                {
                    sb.AppendLine($"Pointer ID: {pointerEventData.Value.pointerId}");
                    sb.AppendLine(pointerEventData.Value.ToString());
                }
            }
            return sb.ToString();
        }
        
        /// <summary>
        /// Processes the mouse as if were touch input.
        /// </summary>
        private void FakeTouches()
        {
            var pointerData = GetMousePointerEventData(0);

            var leftPressData = pointerData.GetButtonState(PointerEventData.InputButton.Left).eventData;

            // On initial press clear delta
            if (leftPressData.PressedThisFrame())
            {
                leftPressData.buttonData.delta = Vector2.zero;
            }

            ProcessPointerData(leftPressData.buttonData, leftPressData.PressedThisFrame(), leftPressData.ReleasedThisFrame());

            // Only process move/drag if mouse is pressed
            if (input.GetMouseButton(0))
            {
                ProcessMove(leftPressData.buttonData);
                ProcessDrag(leftPressData.buttonData);
            }
        }
        
        /// <summary>
        /// Creates <see cref="PointerEventData"/> from a given <see cref="BoardContact"/>.
        /// </summary>
        /// <param name="contact">The <see cref="BoardContact"/> to be processed.</param>
        /// <param name="pressed"><c>true</c> if the touch was initiated this frame; otherwise, <c>false</c>.</param>
        /// <param name="released"><c>true</c> if the touch was released this frame; otherwise, <c>false</c>.</param>
        /// <returns>The <see cref="PointerEventData"/> for the contact.</returns>
        private PointerEventData GetContactPointerEventData(ref BoardContact contact, out bool pressed, out bool released)
        {
            // Get or assign a pointer slot for UIToolkit compatibility (max 32 pointers supported)
            var pointerId = GetOrAssignPointerSlot(contact.contactId);
            if (pointerId < 0)
            {
                pressed = released = false;
                return null;
            }
            
            var created = GetPointerData(pointerId, out var pointerData, true);

            pointerData.Reset();

            pressed = created || (contact.phase == BoardContactPhase.Began);
            released = (contact.phase == BoardContactPhase.Canceled) || (contact.phase == BoardContactPhase.Ended);

            if (created)
            {
                pointerData.position = contact.screenPosition;
            }

            if (pressed)
            {
                pointerData.delta = Vector2.zero;
            }
            else
            {
                pointerData.delta = contact.screenPosition - pointerData.position;
            }

            pointerData.position = contact.screenPosition;

            pointerData.button = PointerEventData.InputButton.Left;

            if (contact.phase == BoardContactPhase.Canceled)
            {
                pointerData.pointerCurrentRaycast = new RaycastResult();
            }
            else
            {
                eventSystem.RaycastAll(pointerData, m_RaycastResultCache);

                var raycast = FindFirstRaycast(m_RaycastResultCache);
                pointerData.pointerCurrentRaycast = raycast;
                m_RaycastResultCache.Clear();
            }

            return pointerData;
        }

        /// <summary>
        /// Process a specified pointer event.
        /// </summary>
        /// <param name="pointerEvent">The data to process.</param>
        /// <param name="pressed"><c>true</c> if the pointer was initiated this frame; otherwise, <c>false</c>.</param>
        /// <param name="released"><c>true</c> if the pointer was released this frame; otherwise, <c>false</c>.</param>
        private void ProcessPointerData(PointerEventData pointerEvent, bool pressed, bool released)
        {
            var currentOverGo = pointerEvent.pointerCurrentRaycast.gameObject;

            // PointerDown notification
            if (pressed)
            {
                pointerEvent.eligibleForClick = true;
                pointerEvent.delta = Vector2.zero;
                pointerEvent.dragging = false;
                pointerEvent.useDragThreshold = true;
                pointerEvent.pressPosition = pointerEvent.position;
                pointerEvent.pointerPressRaycast = pointerEvent.pointerCurrentRaycast;

                DeselectIfSelectionChanged(currentOverGo, pointerEvent);

                if (pointerEvent.pointerEnter != currentOverGo)
                {
                    // Send a pointer enter to the touched element if it isn't the one to select
                    HandlePointerExitAndEnter(pointerEvent, currentOverGo);
                    pointerEvent.pointerEnter = currentOverGo;
                }

                // Search for the control that will receive the press if we can't find a press handler set the press
                // handler to be what would receive a click.
                var newPressed =
                    ExecuteEvents.ExecuteHierarchy(currentOverGo, pointerEvent, ExecuteEvents.pointerDownHandler);

                // Did not find a press handler so search for a click handler
                if (newPressed == null)
                {
                    newPressed = ExecuteEvents.GetEventHandler<IPointerClickHandler>(currentOverGo);
                }

                var time = Time.unscaledTime;

                if (newPressed == pointerEvent.lastPress)
                {
                    var diffTime = time - pointerEvent.clickTime;
                    if (diffTime < 0.3f)
                    {
                        ++pointerEvent.clickCount;
                    }
                    else
                    {
                        pointerEvent.clickCount = 1;
                    }

                    pointerEvent.clickTime = time;
                }
                else
                {
                    pointerEvent.clickCount = 1;
                }

                pointerEvent.pointerPress = newPressed;
                pointerEvent.rawPointerPress = currentOverGo;

                pointerEvent.clickTime = time;

                // Save the drag handler as well
                pointerEvent.pointerDrag = ExecuteEvents.GetEventHandler<IDragHandler>(currentOverGo);

                if (pointerEvent.pointerDrag != null)
                {
                    ExecuteEvents.Execute(pointerEvent.pointerDrag, pointerEvent,
                        ExecuteEvents.initializePotentialDrag);
                }

                m_InputPointerEvent = pointerEvent;
            }

            // PointerUp notification
            if (released)
            {
                ExecuteEvents.Execute(pointerEvent.pointerPress, pointerEvent, ExecuteEvents.pointerUpHandler);

                // See if we mouse up on the same element that we clicked on...
                var pointerUpHandler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(currentOverGo);

                // PointerClick and Drop events
                if (pointerEvent.pointerPress == pointerUpHandler && pointerEvent.eligibleForClick)
                {
                    ExecuteEvents.Execute(pointerEvent.pointerPress, pointerEvent, ExecuteEvents.pointerClickHandler);
                }
                else if (pointerEvent.pointerDrag != null && pointerEvent.dragging)
                {
                    ExecuteEvents.ExecuteHierarchy(currentOverGo, pointerEvent, ExecuteEvents.dropHandler);
                }

                pointerEvent.eligibleForClick = false;
                pointerEvent.pointerPress = null;
                pointerEvent.rawPointerPress = null;

                if (pointerEvent.pointerDrag != null && pointerEvent.dragging)
                {
                    ExecuteEvents.Execute(pointerEvent.pointerDrag, pointerEvent, ExecuteEvents.endDragHandler);
                }

                pointerEvent.dragging = false;
                pointerEvent.pointerDrag = null;

                // Send exit events as we need to simulate this on touch up on touch device
                ExecuteEvents.ExecuteHierarchy(pointerEvent.pointerEnter, pointerEvent,
                    ExecuteEvents.pointerExitHandler);
                pointerEvent.pointerEnter = null;

                m_InputPointerEvent = pointerEvent;
            }
        }
    }
}
