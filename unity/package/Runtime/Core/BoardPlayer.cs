// <copyright file="BoardPlayer.cs" company="Harris Hill Products Inc.">
//     Copyright (c) Harris Hill Products Inc. All rights reserved.
// </copyright>

namespace Board.Core
{
    using System;
    using System.Threading.Tasks;

    using UnityEngine;

    /// <summary>
    /// Represents the method that will handle the <c>avatarLoaded</c> event of a <see cref="BoardPlayer"/> class.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    public delegate void BoardPlayerAvatarLoadedHandler(BoardPlayer sender);

    /// <summary>
    /// Represents a player on Board. 
    /// </summary>
    public class BoardPlayer
    {
        /// <summary>
        /// The backing texture for the player's avatar.
        /// </summary>
        protected Texture2D m_AvatarTexture;
        private bool m_IsLoadingAvatar = false;

        /// <summary>
        /// Gets the log tag for this <see cref="BoardPlayer"/>
        /// </summary>
        protected virtual string logTag { get; }

        /// <summary>
        /// Gets the Player's persistent app-specific identifier.
        /// </summary>
        /// <remarks>
        /// This identifier is consistent across sessions for the same non-guest Player playing the same app.
        /// It is deterministic based on the profile and app, making it suitable for developer use in
        /// app-specific features while maintaining privacy of the underlying Player profile. 
        /// 
        /// This identifier is randomly generated for Guest Players (<see cref="BoardPlayerType.Guest"/>).
        /// </remarks>
        public virtual string playerId { get; protected set; }

        /// <summary>
        /// Gets the player's name.
        /// </summary>
        /// <remarks>
        /// Suitable for displaying in game, but not guaranteed to be static or unique. 
        /// Do not use as an identifier, use <see cref="Session.BoardSessionPlayer.sessionId">sessionId</see> or <see cref="playerId"/> as appropriate.
        /// </remarks>
        public string name { get; protected set; }

        /// <summary>
        /// Gets the player's avatar identifier.
        /// </summary>
        public string avatarId { get; protected set; }

        /// <summary>
        /// Gets the player's <see cref="BoardPlayerType">type</see>.
        /// </summary>
        public BoardPlayerType type { get; protected set; }

        /// <summary>
        /// Gets the player's avatar <see cref="Texture2D"/>.
        /// </summary>
        public Texture2D avatar
        {
            get
            {
                if (m_AvatarTexture == null && !m_IsLoadingAvatar)
                {
                    LoadAvatarAsync();
                }

                return m_AvatarTexture;
            }
            set => m_AvatarTexture = value;
        }

        /// <summary>
        /// Occurs when the avatar <see cref="Texture2D"/> is loaded.
        /// </summary>
        public event BoardPlayerAvatarLoadedHandler avatarLoaded;

        /// <summary>
        /// Gets the default avatar texture (avatar ID 0).
        /// </summary>
        /// <remarks>
        /// Useful for displaying unknown or guest players in save game UI.
        /// </remarks>
        /// <returns>Texture2D of the default avatar, or null if loading fails.</returns>
        public static Task<Texture2D> GetDefaultAvatar()
        {
            return BoardAvatarManager.GetDefaultAvatar();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BoardPlayer"/> class with the specified name, avatar identifier, and type.
        /// </summary>
        /// <param name="name">The name of the player.</param>
        /// <param name="avatarId">The avatar identifier of the player.</param>
        /// <param name="type">The player type (Profile or Guest).</param>
        internal BoardPlayer(string name, string avatarId, BoardPlayerType type)
        {
            this.name = name;
            this.avatarId = avatarId;
            this.type = type;

            // Load avatar asynchronously (fire and forget - avatar will be null until loaded)
            LoadAvatarAsync();
        }
        
        /// <summary>
        /// Initializes a new instance of the <see cref="BoardPlayer"/> class with the specified name, player identifier, avatar identifier, and type.
        /// </summary>
        /// <param name="name">The name of the player.</param>
        /// <param name="playerId">The unique identifier of the player.</param>
        /// <param name="avatarId">The avatar identifier of the player.</param>
        /// <param name="type">The player type (Profile or Guest).</param>
        internal BoardPlayer(string name, string playerId, string avatarId, BoardPlayerType type)
        {
            this.name = name;
            this.playerId =  playerId;
            this.avatarId = avatarId;
            this.type = type;

            // Load avatar asynchronously (fire and forget - avatar will be null until loaded)
            LoadAvatarAsync();
        }

        /// <summary>
        /// Loads the player's avatar texture asynchronously.
        /// </summary>
        private async void LoadAvatarAsync()
        {
            // Prevent duplicate loads from the same player instance
            if (m_IsLoadingAvatar)
            {
                return;
            }

            m_IsLoadingAvatar = true;

            try
            {
                if (int.TryParse(avatarId, out int avatarIdInt))
                {
                    avatar = await BoardAvatarManager.LoadAvatarAsync(avatarIdInt);

                    if (avatar != null)
                    {
                        avatarLoaded?.Invoke(this);
                    }
                }
                else
                {
                    BoardLogger.LogError(logTag, $"Failed to parse avatarId={avatarId}");
                }
            }
            catch (Exception e)
            {
                BoardLogger.LogError(logTag, $"Failed to load avatarId={avatarId}: {e.Message}");
            }
            finally
            {
                m_IsLoadingAvatar = false;
            }
        }
    }
}
