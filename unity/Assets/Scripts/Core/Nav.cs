// <copyright file="Nav.cs" company="board.fun game">
//     Trafalgar - Age of Sail. Procedural board.fun RTS.
// </copyright>

namespace Trafalgar.Core
{
    using UnityEngine;

    /// <summary>
    /// Navigation / heading maths shared across the simulation.
    /// </summary>
    /// <remarks>
    /// The game lives on the world XZ plane (Y is up). Headings are compass-style
    /// degrees measured clockwise from +Z ("north"): 0 = +Z, 90 = +X, 180 = -Z, 270 = -X.
    /// </remarks>
    public static class Nav
    {
        /// <summary>Converts a compass heading in degrees to a unit direction on the XZ plane.</summary>
        /// <param name="headingDeg">Heading in degrees, clockwise from +Z.</param>
        /// <returns>A normalised XZ direction (Y = 0).</returns>
        public static Vector3 HeadingToVector(float headingDeg)
        {
            float r = headingDeg * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(r), 0f, Mathf.Cos(r));
        }

        /// <summary>Converts an XZ direction to a compass heading in degrees in [0, 360).</summary>
        /// <param name="dir">A direction (the Y component is ignored).</param>
        /// <returns>Heading in degrees, clockwise from +Z.</returns>
        public static float VectorToHeading(Vector3 dir)
        {
            float deg = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            return Normalize360(deg);
        }

        /// <summary>Wraps an angle to the range [0, 360).</summary>
        /// <param name="deg">An angle in degrees.</param>
        /// <returns>The wrapped angle.</returns>
        public static float Normalize360(float deg)
        {
            deg %= 360f;
            if (deg < 0f)
            {
                deg += 360f;
            }

            return deg;
        }

        /// <summary>Wraps an angle to the range (-180, 180].</summary>
        /// <param name="deg">An angle in degrees.</param>
        /// <returns>The wrapped signed angle.</returns>
        public static float Normalize180(float deg)
        {
            deg = Normalize360(deg);
            if (deg > 180f)
            {
                deg -= 360f;
            }

            return deg;
        }

        /// <summary>Gets the absolute smallest angle between two headings, in [0, 180].</summary>
        /// <param name="a">First heading in degrees.</param>
        /// <param name="b">Second heading in degrees.</param>
        /// <returns>The unsigned separation in degrees.</returns>
        public static float AngleDifference(float a, float b)
        {
            return Mathf.Abs(Normalize180(a - b));
        }

        /// <summary>Gets the signed shortest delta to steer from <paramref name="from"/> toward <paramref name="to"/>.</summary>
        /// <param name="from">Current heading in degrees.</param>
        /// <param name="to">Desired heading in degrees.</param>
        /// <returns>A signed delta in (-180, 180]; positive turns clockwise.</returns>
        public static float SignedDelta(float from, float to)
        {
            return Normalize180(to - from);
        }

        /// <summary>Rotates <paramref name="current"/> toward <paramref name="target"/> by at most <paramref name="maxDeg"/>.</summary>
        /// <param name="current">Current heading in degrees.</param>
        /// <param name="target">Target heading in degrees.</param>
        /// <param name="maxDeg">Maximum rotation this step in degrees.</param>
        /// <returns>The new heading in [0, 360).</returns>
        public static float MoveTowardsAngle(float current, float target, float maxDeg)
        {
            float delta = SignedDelta(current, target);
            float step = Mathf.Clamp(delta, -maxDeg, maxDeg);
            return Normalize360(current + step);
        }
    }
}
