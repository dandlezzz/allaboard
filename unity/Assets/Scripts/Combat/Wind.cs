// <copyright file="Wind.cs" company="board.fun game">
//     Trafalgar - Age of Sail. Procedural board.fun RTS.
// </copyright>

namespace Trafalgar.Combat
{
    using Trafalgar.Core;
    using UnityEngine;

    /// <summary>
    /// The global wind. Age-of-sail ships cannot sail directly into the wind, so wind direction
    /// is the central tactical constraint. The wind slowly veers over the course of a battle.
    /// </summary>
    public class Wind
    {
        private float m_ShiftTimer;
        private float m_TargetFromDeg;

        /// <summary>
        /// Initialises a new instance of the <see cref="Wind"/> class.
        /// </summary>
        /// <param name="initialFromDeg">Initial direction the wind blows *from*, in compass degrees.</param>
        public Wind(float initialFromDeg)
        {
            FromDegrees = Nav.Normalize360(initialFromDeg);
            m_TargetFromDeg = FromDegrees;
            m_ShiftTimer = GameConfig.WindShiftInterval;
        }

        /// <summary>Gets the direction the wind blows *from*, in compass degrees (the "weather gauge").</summary>
        public float FromDegrees { get; private set; }

        /// <summary>Gets a unit vector pointing in the direction the wind is blowing *toward* (downwind).</summary>
        public Vector3 BlowingToward => Nav.HeadingToVector(Nav.Normalize360(FromDegrees + 180f));

        /// <summary>Gets a unit vector pointing toward the source of the wind (upwind).</summary>
        public Vector3 Source => Nav.HeadingToVector(FromDegrees);

        /// <summary>
        /// Advances the wind simulation, occasionally veering the direction.
        /// </summary>
        /// <param name="dt">Delta time in seconds.</param>
        public void Tick(float dt)
        {
            m_ShiftTimer -= dt;
            if (m_ShiftTimer <= 0f)
            {
                m_ShiftTimer = GameConfig.WindShiftInterval;
                float shift = Random.Range(-GameConfig.WindShiftMagnitude, GameConfig.WindShiftMagnitude);
                m_TargetFromDeg = Nav.Normalize360(m_TargetFromDeg + shift);
            }

            // Ease toward the target direction so shifts feel gradual.
            FromDegrees = Nav.MoveTowardsAngle(FromDegrees, m_TargetFromDeg, 3f * dt);
        }

        /// <summary>
        /// Computes the speed multiplier for a ship on a given heading (its "point of sail").
        /// </summary>
        /// <param name="headingDeg">The ship's heading in compass degrees.</param>
        /// <returns>A multiplier in [<see cref="GameConfig.InIronsFactor"/>, 1].</returns>
        public float PointOfSailFactor(float headingDeg)
        {
            // Angle between where we are pointing and the wind's source.
            // 0   => sailing straight into the wind (in irons)
            // 180 => running dead downwind
            float offWind = Nav.AngleDifference(headingDeg, FromDegrees);
            return PointOfSailFactor(offWind, out _);
        }

        /// <summary>
        /// Computes the speed multiplier and classifies the point of sail.
        /// </summary>
        /// <param name="offWindAngle">Angle between heading and the wind source, in [0, 180].</param>
        /// <param name="pointOfSail">Outputs a friendly classification.</param>
        /// <returns>A speed multiplier in [<see cref="GameConfig.InIronsFactor"/>, 1].</returns>
        public static float PointOfSailFactor(float offWindAngle, out string pointOfSail)
        {
            offWindAngle = Mathf.Abs(offWindAngle);

            if (offWindAngle < GameConfig.NoGoAngle)
            {
                pointOfSail = "In Irons";

                // Ramp from the floor at dead-into-wind up toward close-hauled.
                float t = Mathf.InverseLerp(0f, GameConfig.NoGoAngle, offWindAngle);
                return Mathf.Lerp(GameConfig.InIronsFactor, 0.45f, t);
            }

            if (offWindAngle < 75f)
            {
                pointOfSail = "Close-Hauled";
                return Mathf.Lerp(0.45f, 0.85f, Mathf.InverseLerp(GameConfig.NoGoAngle, 75f, offWindAngle));
            }

            if (offWindAngle < 115f)
            {
                pointOfSail = "Beam Reach";
                return Mathf.Lerp(0.85f, 1.0f, Mathf.InverseLerp(75f, 100f, offWindAngle));
            }

            if (offWindAngle < 150f)
            {
                pointOfSail = "Broad Reach";
                return Mathf.Lerp(1.0f, 0.9f, Mathf.InverseLerp(115f, 150f, offWindAngle));
            }

            pointOfSail = "Running";
            return Mathf.Lerp(0.9f, 0.78f, Mathf.InverseLerp(150f, 180f, offWindAngle));
        }
    }
}
