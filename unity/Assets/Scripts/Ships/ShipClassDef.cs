// <copyright file="ShipClassDef.cs" company="board.fun game">
//     Trafalgar - Age of Sail. Procedural board.fun RTS.
// </copyright>

namespace Trafalgar.Ships
{
    using Trafalgar.Core;

    /// <summary>
    /// Rated classes of sailing warship, from nimble frigates to towering first-rates.
    /// </summary>
    public enum ShipClass
    {
        /// <summary>Fast, lightly-armed scout / raider.</summary>
        Frigate = 0,

        /// <summary>Workhorse 74-gun ship-of-the-line.</summary>
        ThirdRate = 1,

        /// <summary>Massive 100+ gun flagship: slow, ponderous, devastating.</summary>
        FirstRate = 2,
    }

    /// <summary>
    /// Immutable tuning stats describing a <see cref="ShipClass"/>. Kept as plain data so
    /// designers can rebalance without touching simulation code.
    /// </summary>
    public class ShipStats
    {
        /// <summary>The class these stats describe.</summary>
        public ShipClass shipClass;

        /// <summary>Human-readable class name.</summary>
        public string displayName;

        /// <summary>Guns per broadside (one side), capped at 8. Each fires its own cannonball.</summary>
        public int gunsPerBroadside;

        /// <summary>Fore/aft chase guns (fired toward enemies ahead or astern; weak).</summary>
        public int chaseGuns;

        /// <summary>Maximum hull integrity (round-shot soak before sinking).</summary>
        public float maxHull;

        /// <summary>Maximum rigging integrity (bar-shot soak before crippled).</summary>
        public float maxRigging;

        /// <summary>Maximum crew complement (grape-shot soak; gates boarding).</summary>
        public float maxCrew;

        /// <summary>Top speed in world units / second at the optimal point of sail and full sail.</summary>
        public float topSpeed;

        /// <summary>Best-case turn rate in degrees / second (degraded by rigging damage).</summary>
        public float turnRate;

        /// <summary>Acceleration in units / second^2 used when easing toward target speed.</summary>
        public float acceleration;

        /// <summary>Maximum effective gun range in world units.</summary>
        public float gunRange;

        /// <summary>Seconds required to reload a broadside.</summary>
        public float reloadTime;

        /// <summary>Approximate hull length in world units (used for procedural mesh + collision).</summary>
        public float length;

        /// <summary>Approximate beam (width) in world units.</summary>
        public float beam;
    }

    /// <summary>
    /// Catalogue of the ship classes available in the game.
    /// </summary>
    public static class ShipCatalog
    {
        /// <summary>Gets the tuning stats for a given <see cref="ShipClass"/>.</summary>
        /// <param name="shipClass">The class to look up.</param>
        /// <returns>A fresh <see cref="ShipStats"/> instance.</returns>
        public static ShipStats Stats(ShipClass shipClass)
        {
            // All length-like quantities (hull length/beam, top speed, acceleration, and gun range)
            // are expressed at the base tabletop scale below and multiplied by GameConfig.ShipScale,
            // so they stay proportional to the hull at any scale. Purely angular or time-based stats
            // (turnRate in deg/s, reloadTime in seconds, the damage/crew pools) are scale-invariant
            // and are intentionally left untouched.
            float s = GameConfig.ShipScale;

            // Speed and acceleration also carry the global BaseSpeedMultiplier so the whole fleet's
            // sailing pace can be tuned independently of the world scale. Both move together to keep
            // the time/distance to reach top speed consistent.
            float speedScale = s * GameConfig.BaseSpeedMultiplier;

            // Gun range carries the global BaseRangeMultiplier so broadside reach can be tuned
            // independently of the world scale. Firing arcs are angular and stay untouched.
            float rangeScale = s * GameConfig.BaseRangeMultiplier;

            // Hull length/beam carry the extra HullSizeBoost (NOT applied to arena/camera) so the
            // ships occupy more of the fixed view. Gun-counts are a realistic handful per side now,
            // capped at 8 on the first-rate and fewer on smaller classes.
            float hullScale = s * GameConfig.HullSizeBoost;

            // Beam is trimmed relative to length so hulls read long and narrow (≈4:1) like the
            // reference tall ship instead of a fat oval.
            float beamScale = hullScale * 0.90f;
            switch (shipClass)
            {
                case ShipClass.Frigate:
                    return new ShipStats
                    {
                        shipClass = ShipClass.Frigate,
                        displayName = "Frigate",
                        gunsPerBroadside = 4,
                        chaseGuns = 2,
                        maxHull = 70f,
                        maxRigging = 60f,
                        maxCrew = 50f,
                        topSpeed = 7.5f * speedScale,
                        turnRate = 26f,
                        acceleration = 2.2f * speedScale,
                        gunRange = 30f * rangeScale,
                        reloadTime = 6f,
                        length = 5.0f * hullScale,
                        beam = 1.3f * beamScale,
                    };
                case ShipClass.ThirdRate:
                    return new ShipStats
                    {
                        shipClass = ShipClass.ThirdRate,
                        displayName = "Third Rate (74)",
                        gunsPerBroadside = 6,
                        chaseGuns = 2,
                        maxHull = 130f,
                        maxRigging = 90f,
                        maxCrew = 90f,
                        topSpeed = 5.5f * speedScale,
                        turnRate = 15f,
                        acceleration = 1.3f * speedScale,
                        gunRange = 36f * rangeScale,
                        reloadTime = 8f,
                        length = 7.0f * hullScale,
                        beam = 1.9f * beamScale,
                    };
                case ShipClass.FirstRate:
                    return new ShipStats
                    {
                        shipClass = ShipClass.FirstRate,
                        displayName = "First Rate (100+)",
                        gunsPerBroadside = 8,
                        chaseGuns = 2,
                        maxHull = 190f,
                        maxRigging = 120f,
                        maxCrew = 130f,
                        topSpeed = 4.5f * speedScale,
                        turnRate = 10f,
                        acceleration = 0.9f * speedScale,
                        gunRange = 40f * rangeScale,
                        reloadTime = 10f,
                        length = 8.5f * hullScale,
                        beam = 2.3f * beamScale,
                    };
                default:
                    goto case ShipClass.ThirdRate;
            }
        }
    }
}
