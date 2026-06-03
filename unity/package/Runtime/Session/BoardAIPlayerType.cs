// <copyright file="BoardAIPlayerType.cs" company="Harris Hill Products Inc.">
//     Copyright (c) Harris Hill Products Inc. All rights reserved.
// </copyright>

namespace Board.Session
{
    /// <summary>
    /// Defines an AI player type that a game supports.
    /// </summary>
    /// <remarks>
    /// Games register their available AI types (e.g., "Easy", "Hard", "Master") via
    /// <see cref="BoardSession.SetAIPlayerTypes"/>. Each type has a display name shown in
    /// the player selector and an optional description.
    /// </remarks>
    public struct BoardAIPlayerType
    {
        /// <summary>
        /// The display name of this AI type (e.g., "Easy", "Hard", "Master").
        /// </summary>
        public string name;

        /// <summary>
        /// A description of this AI type's behavior (e.g., "Plays conservatively").
        /// </summary>
        public string description;
    }
}
