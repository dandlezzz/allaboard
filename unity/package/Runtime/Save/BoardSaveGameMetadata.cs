// <copyright file="BoardSaveGameMetadata.cs" company="Harris Hill Products Inc.">
//     Copyright (c) Harris Hill Products Inc. All rights reserved.
// </copyright>

namespace Board.Save
{
    using System.Collections.Generic;
    using System.Linq;
    
    using Board.Core;

    /// <summary>
    /// Represents the metadata for a save game.
    /// </summary>
    /// <remarks>
    /// Read only properties are set by the native layer. Use <see cref="BoardSaveGameMetadataChange"/> 
    /// to create/update the metadata for a save game.
    /// </remarks>
    public class BoardSaveGameMetadata
    {
        /// <summary>
        /// Gets the unique identifier of the saved game.
        /// </summary>
        /// <remarks>Used for Load, Save, and Delete operations.</remarks>
        public string id { get; internal set; }

        /// <summary>
        /// Gets the description of the saved game.
        /// </summary>
        public string description { get; internal set; }

        /// <summary>
        /// Gets the timestamp in milliseconds when the saved game was created.
        /// </summary>
        public ulong createdAt { get; internal set; }

        /// <summary>
        /// Gets the timestamp in milliseconds when the saved game was last updated.
        /// </summary>
        public ulong updatedAt { get; internal set; }

        /// <summary>
        /// Gets how long the players have played the saved game in seconds.
        /// </summary>
        public ulong playedTime { get; internal set; }

        /// <summary>
        /// Gets the game version associated with this save game.
        /// </summary>
        /// <remarks>Provided during create/update within <see cref="BoardSaveGameMetadataChange"/>.</remarks>
        public string gameVersion { get; internal set; }

        /// <summary>
        /// Gets a value indicating whether this save game has a cover image available.
        /// </summary>
        /// <remarks>Use <see cref="BoardSaveGameManager.LoadSaveGameCoverImage"/> to load the actual image data.</remarks>
        public bool hasCoverImage { get; internal set; }

        /// <summary>
        /// The player identifiers associated with this save game.
        /// </summary>
        public string[] playerIds { get; internal set; }

        /// <summary>
        /// Gets the collection of <see cref="BoardSaveGamePlayer">players</see> associated with this save game.
        /// </summary>
        public BoardSaveGamePlayer[] players { get; internal set; }

        /// <summary>
        /// Gets the SHA-256 checksum of the save game payload for integrity verification.
        /// </summary>
        /// <remarks>
        /// This 64-character hex string can be used to verify the integrity of the loaded payload.
        /// Will be null for saves created before checksum support was added.
        /// </remarks>
        public string payloadChecksum { get; internal set; }

        /// <summary>
        /// Gets the SHA-256 checksum of the cover image for integrity verification.
        /// </summary>
        /// <remarks>
        /// This 64-character hex string can be used to verify the integrity of the cover image.
        /// Will be null for saves created before checksum support was added.
        /// </remarks>
        public string coverImageChecksum { get; internal set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="BoardSaveGameMetadata"/> class.
        /// </summary>
        public BoardSaveGameMetadata()
        {
        }
    }

    /// <summary>
    /// Provides extension methods for <see cref="BoardSaveGameMetadata"/>.
    /// </summary>
    public static class BoardSaveGameMetadataExtensions
    {
        /// <summary>
        /// Gets a unique collection of all unique profile <see cref="BoardSaveGamePlayer">players</see> from the specified
        /// collection of <see cref="BoardSaveGameMetadata"/> instances.
        /// </summary>
        /// <param name="source">A sequence of <see cref="BoardSaveGameMetadata"/> instances.</param>
        /// <returns>An enumerable collection of all unique <see cref="BoardSaveGamePlayer">players</see>.</returns>
        public static IEnumerable<BoardSaveGamePlayer> GetUniquePlayers(this IEnumerable<BoardSaveGameMetadata> source)
        {
            return source.SelectMany(metadata => metadata.players).Distinct()
                .Where(player => player.type == BoardPlayerType.Profile);
        }
    }
}
