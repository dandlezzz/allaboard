// <copyright file="FleetAI.cs" company="board.fun game">
//     Trafalgar - Age of Sail. Procedural board.fun RTS.
// </copyright>

namespace Trafalgar.AI
{
    using System.Collections.Generic;
    using Trafalgar.Combat;
    using Trafalgar.Core;
    using Trafalgar.Ships;
    using UnityEngine;

    /// <summary>
    /// A lightweight tactical AI that commands an entire fleet. For each of its ships it picks the
    /// nearest enemy, manoeuvres to bring a broadside to bear (or closes to board a crippled prize),
    /// trims sail to the situation, and selects ammunition to match its intent.
    /// </summary>
    public class FleetAI
    {
        private readonly Faction m_Faction;

        /// <summary>
        /// Initialises a new instance of the <see cref="FleetAI"/> class.
        /// </summary>
        /// <param name="faction">The faction this AI commands.</param>
        public FleetAI(Faction faction)
        {
            m_Faction = faction;
        }

        /// <summary>Gets the faction commanded by this AI.</summary>
        public Faction Faction => m_Faction;

        /// <summary>
        /// Issues orders to every living ship of the controlled faction.
        /// </summary>
        /// <param name="ships">All ships in play.</param>
        /// <param name="wind">The global wind.</param>
        public void Tick(IReadOnlyList<Ship> ships, Wind wind)
        {
            for (int i = 0; i < ships.Count; i++)
            {
                Ship ship = ships[i];
                if (!ship.IsAlive || ship.Faction != m_Faction)
                {
                    continue;
                }

                CommandShip(ship, ships, wind);
            }
        }

        private void CommandShip(Ship ship, IReadOnlyList<Ship> ships, Wind wind)
        {
            Ship target = NearestEnemy(ship, ships, out float dist);

            if (target == null)
            {
                // No enemies in sight: hold a steady reach across the wind.
                ship.SetTargetHeading(Nav.Normalize360(wind.FromDegrees + 90f));
                ship.SetSail(SailSetting.BattleSail);
                return;
            }

            float bearing = Nav.VectorToHeading(target.Position - ship.Position);
            float desiredHeading;

            if (target.IsBoardable && dist < GameConfig.BoardingRange * 3f)
            {
                // Crippled prize within reach: bear down to grapple and board.
                desiredHeading = bearing;
                ship.SetAmmo(AmmoType.GrapeShot);
                ship.SetSail(SailSetting.BattleSail);
            }
            else if (dist > ship.Stats.gunRange * 0.8f)
            {
                // Out of effective range: close the distance under full sail.
                desiredHeading = bearing;
                ship.SetSail(SailSetting.FullSail);
                ship.SetAmmo(ChooseAmmo(ship, target));
            }
            else
            {
                // In the killing zone: present a broadside and pound away.
                desiredHeading = BroadsideHeading(ship, bearing);
                ship.SetSail(SailSetting.BattleSail);
                ship.SetAmmo(ChooseAmmo(ship, target));
            }

            desiredHeading = AvoidNoGo(desiredHeading, wind);
            desiredHeading = AvoidEdges(ship, desiredHeading);
            ship.SetTargetHeading(desiredHeading);
        }

        private static AmmoType ChooseAmmo(Ship ship, Ship target)
        {
            // Soften a sturdy enemy for boarding once its hull is failing.
            if (target.HullFraction < 0.4f && target.CrewFraction > GameConfig.BoardingCrewThreshold)
            {
                return AmmoType.GrapeShot;
            }

            // Cripple a fast, healthy runner so it cannot escape.
            if (target.RiggingFraction > 0.6f && target.Stats.topSpeed > ship.Stats.topSpeed)
            {
                return AmmoType.BarShot;
            }

            return AmmoType.RoundShot;
        }

        private static float BroadsideHeading(Ship ship, float bearingToTarget)
        {
            // Two ways to present a broadside: target on the port beam or the starboard beam.
            float portOption = Nav.Normalize360(bearingToTarget + 90f);
            float starboardOption = Nav.Normalize360(bearingToTarget - 90f);

            float toPort = Nav.AngleDifference(ship.HeadingDeg, portOption);
            float toStarboard = Nav.AngleDifference(ship.HeadingDeg, starboardOption);
            return toPort <= toStarboard ? portOption : starboardOption;
        }

        private static float AvoidNoGo(float desiredHeading, Wind wind)
        {
            float off = Nav.AngleDifference(desiredHeading, wind.FromDegrees);
            float limit = GameConfig.NoGoAngle + 6f;
            if (off >= limit)
            {
                return desiredHeading;
            }

            // Pinching too high: fall off onto the nearest sailable close-hauled tack.
            float tackA = Nav.Normalize360(wind.FromDegrees + limit);
            float tackB = Nav.Normalize360(wind.FromDegrees - limit);
            return Nav.AngleDifference(desiredHeading, tackA) <= Nav.AngleDifference(desiredHeading, tackB) ? tackA : tackB;
        }

        private static float AvoidEdges(Ship ship, float desiredHeading)
        {
            // Per-axis bounds on the rectangular 16:9 field, using the same threshold the ship's own
            // edge turn-around uses so the AI and the hard turn-around agree near the boundary.
            float edgeX = GameConfig.ArenaHalfX * GameConfig.EdgeTurnThreshold;
            float edgeZ = GameConfig.ArenaHalfZ * GameConfig.EdgeTurnThreshold;
            Vector3 p = ship.Position;
            if (Mathf.Abs(p.x) < edgeX && Mathf.Abs(p.z) < edgeZ)
            {
                return desiredHeading;
            }

            // Near a boundary: steer back toward the centre of the engagement.
            return Nav.VectorToHeading(-p);
        }

        private Ship NearestEnemy(Ship ship, IReadOnlyList<Ship> ships, out float dist)
        {
            dist = float.MaxValue;
            Ship best = null;
            Faction enemy = m_Faction.Enemy();

            for (int i = 0; i < ships.Count; i++)
            {
                Ship candidate = ships[i];
                if (!candidate.IsAlive || candidate.Faction != enemy)
                {
                    continue;
                }

                float d = Vector3.Distance(ship.Position, candidate.Position);
                if (d < dist)
                {
                    dist = d;
                    best = candidate;
                }
            }

            return best;
        }
    }
}
