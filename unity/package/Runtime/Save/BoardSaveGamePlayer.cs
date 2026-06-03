// <copyright file="BoardSaveGamePlayer.cs" company="Harris Hill Products Inc.">
//     Copyright (c) Harris Hill Products Inc. All rights reserved.
// </copyright>

namespace Board.Save
{
    using System;
    using Board.Core;

    /// <summary>
    /// Represents player display information for save games.
    /// </summary>
    /// <seealso cref="BoardSaveGamePlayerData"/>
    /// <seealso cref="BoardSaveGameManager"/>
    public sealed class BoardSaveGamePlayer : BoardPlayer
    {
        private const string kLogTag = nameof(BoardSaveGamePlayer);
        
        /// <inheritdoc/>
        protected override string logTag => kLogTag;
        
        /// <summary>
        /// Gets the player's persistent application-specific identifier.
        /// </summary>
        /// <remarks>
        /// This identifier is consistent for the same profile playing the same application.
        /// </remarks>
        public override string playerId {get; protected set;}

        /// <summary>
        /// Gets the index into the registered AI player types for this player.
        /// </summary>
        /// <remarks>
        /// Returns -1 if this player is not an AI player. Parsed from the player ID format <c>ai_&lt;index&gt;_&lt;timestamp&gt;</c>.
        /// </remarks>
        public int aiTypeIndex { get; private set; } = -1;

        /// <summary>
        /// Initializes a new instance of the <see cref="BoardSaveGamePlayer"/> class with the specified <see cref="BoardSaveGamePlayerData"/>.
        /// </summary>
        /// <param name="data"><see cref="BoardSaveGamePlayerData"/> supplied by the Board native API.</param>
        internal BoardSaveGamePlayer(ref BoardSaveGamePlayerData data) : base(data.nameString,
            data.avatarIdString,
            InferPlayerType(data.playerIdString))
        {
            this.playerId = data.playerIdString;
            if (type == BoardPlayerType.AI)
            {
                aiTypeIndex = ParseAITypeIndex(data.playerIdString);
            }
        }

        /// <summary>
        /// Get the <see cref="BoardPlayerType"/> based on a player's saved identifier from the Board native API.
        /// </summary>
        /// <param name="playerId">The player's saved identifier.</param>
        /// <returns>The <see cref="BoardPlayerType"/> corresponding to <paramref name="playerId"/>.</returns>
        /// <exception cref="ArgumentException"><paramref name="playerId"/> is <c>null</c> or empty.</exception>
        private static BoardPlayerType InferPlayerType(string playerId)
        {
            if (string.IsNullOrEmpty(playerId))
            {
                throw new ArgumentException(playerId);
            }

            if (playerId.StartsWith("ai_"))
            {
                return BoardPlayerType.AI;
            }

            if (playerId.StartsWith("guest_"))
            {
                return BoardPlayerType.Guest;
            }

            return BoardPlayerType.Profile;
        }

        /// <summary>
        /// Get the AI type index based on a player's saved identifier from the Board native API.
        /// </summary>
        /// <param name="playerId">The player's saved identifier.</param>
        /// <returns>The AI type index corresponding to <paramref cref="playerId"/>.</returns>
        /// <exception cref="ArgumentException"><paramref name="playerId"/> is <c>null</c> or empty.</exception>
        private static int ParseAITypeIndex(string playerId)
        {
            if (string.IsNullOrEmpty(playerId))
            {
                throw new ArgumentException(playerId);
            }

            // Format: ai_<index>_<timestamp>
            var parts = playerId.Split('_');
            if (parts.Length >= 2 && int.TryParse(parts[1], out var index))
            {
                return index;
            }

            return -1;
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return $"{{playerId={playerId} name={name} avatarId={avatarId} type={type} aiTypeIndex={aiTypeIndex}}}";
        }
    }
}
