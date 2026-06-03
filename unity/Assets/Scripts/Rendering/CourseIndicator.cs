// <copyright file="CourseIndicator.cs" company="board.fun game">
//     Trafalgar - Age of Sail. Procedural board.fun RTS.
// </copyright>

namespace Trafalgar.Rendering
{
    using Trafalgar.Core;
    using UnityEngine;

    /// <summary>
    /// World-space steering display for the selected ship: a single thin line from the hull along
    /// its currently-ordered heading, lying flat on the sea under the top-down camera. (The old
    /// drag-to-command preview line + destination marker were removed when steering moved to the
    /// on-ring control buttons; this remains purely as an ordered-heading readout.)
    /// </summary>
    public class CourseIndicator : MonoBehaviour
    {
        private LineRenderer m_HeadingLine;

        private const float kLift = 0.4f; // sit just above the sea plane

        /// <summary>Builds the heading line. Call once after creation.</summary>
        public void Build()
        {
            m_HeadingLine = MakeLine("HeadingLine", 1.1f);
            HideAll();
        }

        /// <summary>Shows the persistent ordered-heading line from a ship along a compass heading.</summary>
        /// <param name="from">Ship world position.</param>
        /// <param name="headingDeg">The ship's ordered heading (compass degrees).</param>
        /// <param name="length">World-space length of the line.</param>
        /// <param name="color">Line colour (typically the owning faction's accent).</param>
        public void ShowHeading(Vector3 from, float headingDeg, float length, Color color)
        {
            Vector3 a = new Vector3(from.x, kLift, from.z);
            Vector3 b = a + (Nav.HeadingToVector(headingDeg) * length);
            SetLine(m_HeadingLine, a, b, color);
        }

        /// <summary>Hides the heading line.</summary>
        public void HideAll()
        {
            if (m_HeadingLine != null)
            {
                m_HeadingLine.gameObject.SetActive(false);
            }
        }

        private LineRenderer MakeLine(string label, float widthScale)
        {
            var go = new GameObject(label);
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.positionCount = 2;
            lr.numCapVertices = 4;
            lr.numCornerVertices = 2;
            lr.widthMultiplier = widthScale * GameConfig.ShipScale;
            lr.sharedMaterial = MaterialUtil.Unlit(Color.white);
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            return lr;
        }

        private static void SetLine(LineRenderer lr, Vector3 a, Vector3 b, Color color)
        {
            lr.gameObject.SetActive(true);
            lr.SetPosition(0, a);
            lr.SetPosition(1, b);
            lr.startColor = color;
            lr.endColor = color;
            if (lr.sharedMaterial != null)
            {
                lr.sharedMaterial.color = color;
            }
        }
    }
}
