// <copyright file="InputRouter.cs" company="board.fun game">
//     Trafalgar - Age of Sail. Procedural board.fun RTS.
// </copyright>

namespace Trafalgar.InputLayer
{
    using System.Collections.Generic;
    using Board.Core;
    using Board.Input;
    using UnityEngine;
    using UnityEngine.InputSystem;

    /// <summary>
    /// Single source of truth for pointer input. It reads Board finger and glyph contacts on
    /// real hardware and transparently falls back to the new Input System mouse / touchscreen in
    /// the editor (and on non-Board desktop builds), exposing both as a unified list of
    /// <see cref="PointerSample"/>.
    /// </summary>
    /// <remarks>
    /// <see cref="BoardInput.GetActiveContacts"/> returns empty arrays unless
    /// <see cref="BoardSupport.enabled"/> is true and the app runs on the Board, so without the
    /// editor fallback the game would be unplayable on a developer machine. Both code paths are
    /// always wired up; the active one is chosen at runtime.
    /// </remarks>
    public class InputRouter
    {
        private readonly List<PointerSample> m_Pointers = new List<PointerSample>();
        private readonly List<PointerSample> m_Glyphs = new List<PointerSample>();

        // Mouse-emulation state (editor / desktop fallback).
        private bool m_MouseWasDown;
        private Vector2 m_LastMousePos;

        /// <summary>Gets the unified active finger/mouse/touch pointers for the current frame.</summary>
        public IReadOnlyList<PointerSample> Pointers => m_Pointers;

        /// <summary>Gets the recognised physical Board glyph pieces for the current frame (empty in the editor).</summary>
        public IReadOnlyList<PointerSample> Glyphs => m_Glyphs;

        /// <summary>Gets a value indicating whether real Board hardware is currently feeding input.</summary>
        public bool UsingBoardHardware { get; private set; }

        /// <summary>
        /// Rebuilds the pointer lists for the current frame. Call once per <c>Update</c>.
        /// </summary>
        public void Poll()
        {
            m_Pointers.Clear();
            m_Glyphs.Clear();

            ReadBoardContacts();

            // Use the Unity pointer fallback whenever Board hardware is not driving input. In the
            // editor BoardSupport.enabled is true but no contacts ever arrive, so the mouse is
            // always wanted there. On a real Board the SDK is the single source of truth, so we
            // never read the OS touchscreen (which would double-count).
            bool useUnityFallback;
#if UNITY_EDITOR
            useUnityFallback = true;
#else
            useUnityFallback = !BoardSupport.enabled;
#endif
            if (useUnityFallback)
            {
                ReadUnityPointers();
            }
        }

        private void ReadBoardContacts()
        {
            UsingBoardHardware = false;

            if (!BoardSupport.enabled)
            {
                return;
            }

            BoardContact[] fingers = BoardInput.GetActiveContacts(BoardContactType.Finger);
            for (int i = 0; i < fingers.Length; i++)
            {
                if (TryConvert(fingers[i], out PointerSample sample))
                {
                    m_Pointers.Add(sample);
                    UsingBoardHardware = true;
                }
            }

            BoardContact[] glyphs = BoardInput.GetActiveContacts(BoardContactType.Glyph);
            for (int i = 0; i < glyphs.Length; i++)
            {
                if (TryConvert(glyphs[i], out PointerSample sample))
                {
                    sample.isGlyph = true;
                    sample.glyphId = glyphs[i].glyphId;
                    sample.orientation = glyphs[i].orientation;
                    m_Glyphs.Add(sample);
                    UsingBoardHardware = true;
                }
            }
        }

        private static bool TryConvert(BoardContact contact, out PointerSample sample)
        {
            sample = default;
            sample.id = contact.contactId;
            sample.screenPosition = contact.screenPosition;
            sample.glyphId = -1;

            switch (contact.phase)
            {
                case BoardContactPhase.Began:
                    sample.phase = PointerPhase.Began;
                    return true;
                case BoardContactPhase.Moved:
                    sample.phase = PointerPhase.Moved;
                    return true;
                case BoardContactPhase.Stationary:
                    sample.phase = PointerPhase.Stationary;
                    return true;
                case BoardContactPhase.Ended:
                case BoardContactPhase.Canceled:
                    sample.phase = PointerPhase.Ended;
                    return true;
                default:
                    return false;
            }
        }

        private void ReadUnityPointers()
        {
            ReadMouse();
            ReadTouchscreen();
        }

        private void ReadMouse()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                return;
            }

            Vector2 pos = mouse.position.ReadValue();
            bool down = mouse.leftButton.isPressed;
            bool pressedThisFrame = mouse.leftButton.wasPressedThisFrame;
            bool releasedThisFrame = mouse.leftButton.wasReleasedThisFrame;

            if (releasedThisFrame || (m_MouseWasDown && !down))
            {
                m_Pointers.Add(new PointerSample
                {
                    id = -1,
                    screenPosition = pos,
                    phase = PointerPhase.Ended,
                    glyphId = -1,
                });
                m_MouseWasDown = false;
                m_LastMousePos = pos;
                return;
            }

            if (pressedThisFrame)
            {
                m_Pointers.Add(new PointerSample
                {
                    id = -1,
                    screenPosition = pos,
                    phase = PointerPhase.Began,
                    glyphId = -1,
                });
                m_MouseWasDown = true;
                m_LastMousePos = pos;
                return;
            }

            if (down)
            {
                PointerPhase phase = (pos - m_LastMousePos).sqrMagnitude > 0.01f
                    ? PointerPhase.Moved
                    : PointerPhase.Stationary;
                m_Pointers.Add(new PointerSample
                {
                    id = -1,
                    screenPosition = pos,
                    phase = phase,
                    glyphId = -1,
                });
                m_LastMousePos = pos;
                m_MouseWasDown = true;
            }
        }

        private void ReadTouchscreen()
        {
            Touchscreen ts = Touchscreen.current;
            if (ts == null)
            {
                return;
            }

            var touches = ts.touches;
            for (int i = 0; i < touches.Count; i++)
            {
                var t = touches[i];
                UnityEngine.InputSystem.TouchPhase tp = t.phase.ReadValue();
                PointerPhase phase;
                switch (tp)
                {
                    case UnityEngine.InputSystem.TouchPhase.Began:
                        phase = PointerPhase.Began;
                        break;
                    case UnityEngine.InputSystem.TouchPhase.Moved:
                        phase = PointerPhase.Moved;
                        break;
                    case UnityEngine.InputSystem.TouchPhase.Stationary:
                        phase = PointerPhase.Stationary;
                        break;
                    case UnityEngine.InputSystem.TouchPhase.Ended:
                    case UnityEngine.InputSystem.TouchPhase.Canceled:
                        phase = PointerPhase.Ended;
                        break;
                    default:
                        continue;
                }

                m_Pointers.Add(new PointerSample
                {
                    id = 1000 + t.touchId.ReadValue(),
                    screenPosition = t.position.ReadValue(),
                    phase = phase,
                    glyphId = -1,
                });
            }
        }
    }
}
