// <copyright file="BoardSessionPlayer.cs" company="Harris Hill Products Inc.">
//     Copyright (c) Harris Hill Products Inc. All rights reserved.
// </copyright>

namespace Board.Session
{
    using Board.Core;
    
    /// <summary>
    /// Represents a player in a Board session.
    /// </summary>
    /// <see cref="BoardSession"/>
    public sealed class BoardSessionPlayer : BoardPlayer
    {
        private const string kLogTag = nameof(BoardSessionPlayer);
        
        /// <inheritdoc/>
        protected override string logTag => kLogTag;
        
        /// <summary>
        /// Gets the player's persistent app-specific identifier.
        /// </summary>
        /// <remarks>
        /// This identifier is consistent across sessions for the same non-guest player playing the same app.
        /// It is deterministic based on the profile and app, making it suitable for developer use in
        /// app-specific features while maintaining privacy of the underlying player profile. 
        /// 
        /// This identifier is randomly generated for guest players (<see cref="BoardPlayerType.Guest"/>).
        /// </remarks>
        public override string playerId { get; protected set; }
        
        /// <summary>
        /// Gets the index into the registered AI player types for this player.
        /// </summary>
        /// <remarks>
        /// Returns -1 if this player is not an AI player (<see cref="BoardPlayer.type"/> is not <see cref="BoardPlayerType.AI"/>).
        /// </remarks>
        public int aiTypeIndex { get; private set; }

        /// <summary>
        /// Gets the player's unique identifier in the current session.
        /// </summary>
        /// <remarks>
        /// This identifier persists for the duration of a game session
        /// including when a save game is loaded. It should be used for
        /// identifying a player within a saved game.
        ///
        /// Note: The <see cref="BoardPlayer.playerId"/> may change if a player is replaced
        /// within a game session, but the <see cref="sessionId"/> will remain constant. An example
        /// of such a scenario is when a save game is loaded and a profile associated with
        /// the save game no longer exists. A <see cref="BoardPlayerType.Guest"/> will then
        /// fill the slot and inherit the <see cref="sessionId"/> with a different <see cref="playerId"/>.
        /// </remarks>
        public int sessionId { get; private set; }
        
        /// <summary>
        /// Initializes a new instance of <see cref="BoardSessionPlayer"/> with the specified <see cref="BoardSessionPlayerData"/>.
        /// </summary>
        /// <param name="data"><see cref="BoardSessionPlayerData"/> supplied by the Board native API.</param>
        internal BoardSessionPlayer(ref BoardSessionPlayerData data) : base(data.nameString, data.avatarIdString, (BoardPlayerType)data.type)
        {
            playerId = data.playerIdString;
            sessionId = data.sessionId;
            aiTypeIndex = data.aiTypeIndex;
        }
        
        /// <inheritdoc/>
        public override string ToString()
        {
            return $"{{playerId={playerId} sessionId={sessionId} name={name} avatar={avatarId} type={type} aiTypeIndex={aiTypeIndex}}}";
        }
    }
}
