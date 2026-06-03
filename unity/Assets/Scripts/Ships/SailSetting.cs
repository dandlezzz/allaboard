// <copyright file="SailSetting.cs" company="board.fun game">
//     Trafalgar - Age of Sail. Procedural board.fun RTS.
// </copyright>

namespace Trafalgar.Ships
{
    /// <summary>
    /// How much canvas a ship is carrying. More sail means more speed but less control.
    /// </summary>
    public enum SailSetting
    {
        /// <summary>Sails furled; the ship coasts to a stop. Hardest to be raked while stationary but a sitting duck.</summary>
        Furled = 0,

        /// <summary>Fighting sail: reduced canvas for steady, controllable gun platform.</summary>
        BattleSail = 1,

        /// <summary>All plain sail set for maximum speed.</summary>
        FullSail = 2,
    }

    /// <summary>
    /// Helpers mapping <see cref="SailSetting"/> to gameplay multipliers.
    /// </summary>
    public static class SailSettingExtensions
    {
        /// <summary>Fraction of the ship's top speed permitted by this sail plan.</summary>
        /// <param name="setting">The sail setting.</param>
        /// <returns>A speed multiplier in [0, 1].</returns>
        public static float ThrottleFactor(this SailSetting setting)
        {
            switch (setting)
            {
                case SailSetting.Furled: return 0f;
                case SailSetting.BattleSail: return 0.6f;
                case SailSetting.FullSail: return 1f;
                default: return 0f;
            }
        }

        /// <summary>Advances to the next sail setting, wrapping around.</summary>
        /// <param name="setting">The current setting.</param>
        /// <returns>The next setting.</returns>
        public static SailSetting Next(this SailSetting setting)
        {
            return (SailSetting)(((int)setting + 1) % 3);
        }

        /// <summary>Gets a short label for the HUD.</summary>
        /// <param name="setting">The current setting.</param>
        /// <returns>A display string.</returns>
        public static string Label(this SailSetting setting)
        {
            switch (setting)
            {
                case SailSetting.Furled: return "Furled";
                case SailSetting.BattleSail: return "Battle Sail";
                case SailSetting.FullSail: return "Full Sail";
                default: return "?";
            }
        }
    }
}
