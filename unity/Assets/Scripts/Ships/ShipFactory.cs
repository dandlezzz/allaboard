// <copyright file="ShipFactory.cs" company="board.fun game">
//     Trafalgar - Age of Sail. Procedural board.fun RTS.
// </copyright>

namespace Trafalgar.Ships
{
    using Trafalgar.Core;
    using UnityEngine;

    /// <summary>
    /// Spawns fully-wired, procedurally-rendered ships.
    /// </summary>
    public static class ShipFactory
    {
        /// <summary>
        /// Creates a ship GameObject, attaches its simulation + view, and initialises it.
        /// </summary>
        /// <param name="shipClass">The class of ship to build.</param>
        /// <param name="faction">The owning faction.</param>
        /// <param name="position">Initial world position.</param>
        /// <param name="headingDeg">Initial compass heading.</param>
        /// <param name="parent">Optional parent transform.</param>
        /// <returns>The created <see cref="Ship"/>.</returns>
        public static Ship Create(ShipClass shipClass, Faction faction, Vector3 position, float headingDeg, Transform parent = null)
        {
            var go = new GameObject($"{faction}-{shipClass}");
            if (parent != null)
            {
                go.transform.SetParent(parent, false);
            }

            var ship = go.AddComponent<Ship>();
            go.AddComponent<ShipView>();
            ship.Initialize(ShipCatalog.Stats(shipClass), faction, position, headingDeg);
            return ship;
        }
    }
}
