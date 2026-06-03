// <copyright file="BoardSessionPlayerData.cs" company="Harris Hill Products Inc.">
//     Copyright (c) Harris Hill Products Inc. All rights reserved.
// </copyright>

namespace Board.Session
{
    using System.Runtime.InteropServices;

    /// <summary>
    /// Encapsulates session player data as defined in the native Board API.
    /// </summary>
    /// <seealso cref="BoardSessionPlayer"/>
    /// <seealso cref="BoardSession"/>
    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct BoardSessionPlayerData
    {
        /// <value>The player identifier as a byte array.</value>
        public fixed byte playerId[64];

        /// <summary>Gets the identifier of the player.</summary>
        /// <value>The string representation of the player identifier.</value>
        public string playerIdString
        {
            get
            {
                fixed (byte* fixedPtr = playerId)
                {
                    return new string((sbyte*)fixedPtr);
                }
            }
        }

        /// <value>The session identifier as an integer.</value>
        public int sessionId;

        /// <value>The name of the player as a byte array.</value>
        public fixed byte name[256];
        
        /// <summary>Gets the name of the player.</summary>
        /// <value>The string representation of the player name.</value>
        public string nameString
        {
            get
            {
                fixed (byte* fixedPtr = name)
                {
                    return new string((sbyte*)fixedPtr);
                }
            }
        }

        /// <value>The player's avatar's identifier as a byte array.</value>
        public fixed byte avatarId[256];
        
        /// <summary>Gets the identifier for the avatar of the player </summary>
        /// <value>The string representation of the player's avatar's identifier.</value>
        public string avatarIdString
        {
            get
            {
                fixed (byte* fixedPtr = avatarId)
                {
                    return new string((sbyte*)fixedPtr);
                }
            }
        }

        /// <value>The <see cref="Core.BoardPlayerType"/> of the user profile.</value>
        public byte type;

        /// <value>The index into registered AI player types (-1 if not AI).</value>
        public int aiTypeIndex;

        // <inheritdoc/>
        public override string ToString()
        {
            return
                $"{{playerId={playerIdString}  sessionId={sessionId} name={nameString} avatarId={avatarIdString} type={type} aiTypeIndex={aiTypeIndex}}}";
        }
    }
}