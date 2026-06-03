// <copyright file="Faction.cs" company="board.fun game">
//     Trafalgar - Age of Sail. Procedural board.fun RTS.
// </copyright>

namespace Trafalgar.Core
{
    using UnityEngine;

    /// <summary>
    /// The two opposing sides (plus a neutral state used while a ship is sinking).
    /// </summary>
    public enum Faction
    {
        /// <summary>No allegiance (e.g. a wreck mid-sink).</summary>
        Neutral = 0,

        /// <summary>The British / Royal Navy fleet (Player 1).</summary>
        British = 1,

        /// <summary>The Franco-Spanish combined fleet (Player 2).</summary>
        FrancoSpanish = 2,
    }

    /// <summary>
    /// Who issues orders for a given <see cref="Faction"/>.
    /// </summary>
    public enum ControlMode
    {
        /// <summary>Controlled by a human at the table (touch / mouse).</summary>
        Human = 0,

        /// <summary>Controlled by the built-in fleet AI.</summary>
        AI = 1,
    }

    /// <summary>
    /// Convenience helpers for working with <see cref="Faction"/> values.
    /// </summary>
    public static class FactionExtensions
    {
        /// <summary>Gets the opposing fleet for a given faction.</summary>
        /// <param name="faction">The faction to query.</param>
        /// <returns>The enemy faction, or <see cref="Faction.Neutral"/> for neutral.</returns>
        public static Faction Enemy(this Faction faction)
        {
            switch (faction)
            {
                case Faction.British: return Faction.FrancoSpanish;
                case Faction.FrancoSpanish: return Faction.British;
                default: return Faction.Neutral;
            }
        }

        /// <summary>Gets the hull / banner colour used to render a faction's ships.</summary>
        /// <param name="faction">The faction to query.</param>
        /// <returns>A display colour.</returns>
        public static Color BannerColor(this Faction faction)
        {
            switch (faction)
            {
                case Faction.British: return new Color(0.85f, 0.78f, 0.62f);
                case Faction.FrancoSpanish: return new Color(0.72f, 0.36f, 0.34f);
                default: return Color.gray;
            }
        }

        /// <summary>Gets the accent colour (sails / flags / UI) for a faction.</summary>
        /// <param name="faction">The faction to query.</param>
        /// <returns>An accent colour.</returns>
        public static Color AccentColor(this Faction faction)
        {
            switch (faction)
            {
                case Faction.British: return new Color(0.20f, 0.45f, 0.85f);
                case Faction.FrancoSpanish: return new Color(0.90f, 0.55f, 0.20f);
                default: return Color.white;
            }
        }

        /// <summary>Gets a short human-readable name for the faction.</summary>
        /// <param name="faction">The faction to query.</param>
        /// <returns>A display string.</returns>
        public static string DisplayName(this Faction faction)
        {
            switch (faction)
            {
                case Faction.British: return "British";
                case Faction.FrancoSpanish: return "Franco-Spanish";
                default: return "Neutral";
            }
        }
    }
}
