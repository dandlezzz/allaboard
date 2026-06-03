// <copyright file="BoardContactPhase.cs" company="Harris Hill Products Inc.">
//     Copyright (c) Harris Hill Products Inc. All rights reserved.
// </copyright>

namespace Board.Input
{
    /// <summary>
    /// Specifies the phase in the lifecycle of a contact on the Board.
    /// </summary>
    /// <seealso cref="BoardContact.phase"/>
    public enum BoardContactPhase
    {
        /// <summary>
        /// No activity has been registered on the contact yet.
        /// </summary>
        /// <remarks>
        /// A given contact will not go back to None once there has been input for it. Meaning that
        /// it indicates a default-initialized contact record.
        /// </remarks>
        None,

        /// <summary>
        /// A contact has just begun, i.e. a finger has touched the screen.
        /// </summary>
        Began,

        /// <summary>
        /// An ongoing contact has changed position.
        /// </summary>
        Moved,

        /// <summary>
        /// An ongoing contact has just ended, i.e. the respective finger has been lifted off of the screen.
        /// </summary>
        Ended,

        /// <summary>
        /// An ongoing contact has been cancelled, i.e. ended in a way other than through user interaction.
        /// </summary>
        /// <remarks>This happens, for example, if focus is moved away from the application while the contact is ongoing.</remarks>
        Canceled,

        /// <summary>
        /// An ongoing contact has not been moved (not received any input) in a frame.
        /// </summary>
        Stationary,
    }

    /// <summary>
    /// Provides extension methods for <see cref="BoardContactPhase"/>.
    /// </summary>
    public static class BoardContactPhaseExtensions
    {
        /// <summary>
        /// Gets a value that indicates whether the phase indicates that a contact has ended.
        /// </summary>
        /// <param name="phase">The contact phase.</param>
        /// <returns><see langword="true"/> if <paramref name="phase"/> indicates a contact that has ended; otherwise, <see langword="false"/>.</returns>
        /// <seealso cref="BoardContact.phase"/>
        public static bool IsEndedOrCanceled(this BoardContactPhase phase)
        {
            return phase == BoardContactPhase.Canceled || phase == BoardContactPhase.Ended;
        }

        /// <summary>
        /// Gets a value that indicates whether the phase indicates that a contact is ongoing.
        /// </summary>
        /// <param name="phase">The contact phase.</param>
        /// <returns><see langword="true"/> if <paramref name="phase"/> indicates a contact that is ongoing; otherwise, <see langword="false"/>.</returns>
        /// <seealso cref="BoardContact.phase"/>
        public static bool IsActive(this BoardContactPhase phase)
        {
            switch (phase)
            {
                case BoardContactPhase.Began:
                case BoardContactPhase.Moved:
                case BoardContactPhase.Stationary:
                    return true;
            }

            return false;
        }
    }
}
