// <copyright file="AmmoType.cs" company="board.fun game">
//     Trafalgar - Age of Sail. Procedural board.fun RTS.
// </copyright>

namespace Trafalgar.Ships
{
    using UnityEngine;

    /// <summary>
    /// The shot loaded into a ship's guns. Each type targets a different ship system.
    /// </summary>
    public enum AmmoType
    {
        /// <summary>Solid round shot: smashes the hull, sinking the enemy.</summary>
        RoundShot = 0,

        /// <summary>Bar / chain shot: shreds rigging and sails, crippling speed and turning.</summary>
        BarShot = 1,

        /// <summary>Grape shot: scythes the crew, softening a ship up for boarding.</summary>
        GrapeShot = 2,
    }

    /// <summary>
    /// Per-shot damage profile and presentation for an <see cref="AmmoType"/>.
    /// </summary>
    public struct AmmoProfile
    {
        /// <summary>Hull damage per hit at point-blank range.</summary>
        public float hullDamage;

        /// <summary>Rigging damage per hit at point-blank range.</summary>
        public float riggingDamage;

        /// <summary>Crew damage per hit at point-blank range.</summary>
        public float crewDamage;

        /// <summary>Colour used for the projectile / muzzle effect.</summary>
        public Color tracerColor;

        /// <summary>Short HUD label.</summary>
        public string label;
    }

    /// <summary>
    /// Static catalogue of ammunition behaviour.
    /// </summary>
    public static class Ammo
    {
        /// <summary>Gets the <see cref="AmmoProfile"/> describing a shot type.</summary>
        /// <param name="type">The ammo type.</param>
        /// <returns>Its damage / presentation profile.</returns>
        public static AmmoProfile Profile(AmmoType type)
        {
            switch (type)
            {
                case AmmoType.RoundShot:
                    return new AmmoProfile
                    {
                        hullDamage = 7.5f,
                        riggingDamage = 1.0f,
                        crewDamage = 1.5f,
                        tracerColor = new Color(0.15f, 0.15f, 0.15f),
                        label = "Round Shot",
                    };
                case AmmoType.BarShot:
                    return new AmmoProfile
                    {
                        hullDamage = 1.0f,
                        riggingDamage = 8.0f,
                        crewDamage = 1.0f,
                        tracerColor = new Color(0.55f, 0.5f, 0.45f),
                        label = "Bar Shot",
                    };
                case AmmoType.GrapeShot:
                    return new AmmoProfile
                    {
                        hullDamage = 1.0f,
                        riggingDamage = 1.0f,
                        crewDamage = 8.0f,
                        tracerColor = new Color(0.85f, 0.75f, 0.35f),
                        label = "Grape Shot",
                    };
                default:
                    return default;
            }
        }

        /// <summary>Cycles to the next ammo type, wrapping around.</summary>
        /// <param name="type">The current ammo type.</param>
        /// <returns>The next ammo type.</returns>
        public static AmmoType Next(AmmoType type)
        {
            return (AmmoType)(((int)type + 1) % 3);
        }

        /// <summary>Gets a short label for the HUD.</summary>
        /// <param name="type">The ammo type.</param>
        /// <returns>A display string.</returns>
        public static string Label(AmmoType type)
        {
            return Profile(type).label;
        }
    }
}
