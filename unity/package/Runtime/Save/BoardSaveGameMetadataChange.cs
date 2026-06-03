// <copyright file="BoardSaveGameMetadataChange.cs" company="Harris Hill Products Inc.">
//     Copyright (c) Harris Hill Products Inc. All rights reserved.
// </copyright>

namespace Board.Save
{
    using System;
    
    using UnityEngine;
    
    /// <summary>
    /// Encapsulates required metadata for save game creation and updates. 
    /// </summary>
    public class BoardSaveGameMetadataChange
    {
        /// <summary>
        /// Gets or sets the description of the saved game.
        /// </summary>
        /// <remarks>
        /// This field is required and cannot be null or empty.
        /// </remarks>
        public string description { get; set; }
        
        /// <summary>
        /// Gets or sets the cover image of the saved game (will be converted to 432x243 PNG).
        /// </summary>
        /// <remarks>
        /// This field is optional. If provided, the image will be saved with the save game.
        /// Cover images can also be loaded separately using BoardSaveGameManager.LoadSaveGameCoverImage.
        /// </remarks>
        public Texture2D coverImage { get; set; }
        
        /// <summary>
        /// Gets or sets how long the players have played the saved game in seconds.
        /// </summary>
        /// <remarks>
        /// This field is required and must be >= 0.
        /// </remarks>
        public ulong playedTime { get; set; }
        
        /// <summary>
        /// Gets or sets he game version for the saved game.
        /// </summary>
        /// <remarks>
        /// This field is required and cannot be null or empty.
        /// </remarks>
        public string gameVersion { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="BoardSaveGameMetadataChange"/> class
        /// </summary>
        /// <remarks>
        /// All properties are required before use.
        /// </remarks>
        public BoardSaveGameMetadataChange() { }
        
        /// <summary>
        /// Initializes a new instance of the <see cref="BoardSaveGameMetadataChange"/> class with the specified
        /// description, cover image, played time, and game version.
        /// </summary>
        /// <param name="description">The description of the saved game. Cannot be null or empty.</param>
        /// <param name="coverImage">The cover image (will be converted to 432x243 PNG).</param>
        /// <param name="playedTime">How long the players have played in seconds.</param>
        /// <param name="gameVersion">The game version string. Cannot be null or empty.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="description"/> is <c>null</c><br/>
        /// -or- <br/>
        /// <paramref name="coverImage"/> is <c>null</c>.<br/>
        /// -or- <br/>
        /// <paramref name="gameVersion"/> is <c>null</c>.</exception>
        public BoardSaveGameMetadataChange(string description, Texture2D coverImage, ulong playedTime, string gameVersion)
        {
            if (string.IsNullOrEmpty(description))
            {
                throw new ArgumentNullException(nameof(description));
            }

            if (coverImage == null)
            {
                throw new ArgumentNullException(nameof(coverImage));
            }

            if (string.IsNullOrEmpty(gameVersion))
            {
                throw new ArgumentNullException(nameof(gameVersion));
            }
            
            this.description = description;
            this.coverImage = coverImage;
            this.playedTime = playedTime;
            this.gameVersion = gameVersion;
        }
    }
}
