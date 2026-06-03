// <copyright file="BoardSaveGameData.cs" company="Harris Hill Products Inc.">
//     Copyright (c) Harris Hill Products Inc. All rights reserved.
// </copyright>

namespace Board.Save
{
    using System.Runtime.InteropServices;

    /// <summary>
    /// Encapsulates the metadata for a saved game as defined in the native Board API.
    /// Uses fixed byte arrays to match the C++ UnitySaveGameMetadata struct.
    /// </summary>
    /// <seealso cref="BoardSaveGameMetadata"/>
    /// <seealso cref="BoardSaveGameManager"/>
    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct BoardSaveGameData
    {
        /// <value>The save game identifier as a byte array.</value>
        public fixed byte saveId[256];

        /// <summary> Gets the string representation of the saved game's identifier.</summary>
        /// <value>The string representation of the Save Game ID.</value>
        public string saveIdString
        {
            get
            {
                fixed (byte* fixedPtr = saveId)
                {
                    return new string((sbyte*)fixedPtr);
                }
            }
        }

        /// <value>The description of the save game as a byte array.</value>
        public fixed byte description[256];

        /// <value>The string representation of the save game description.</value>
        public string descriptionString
        {
            get
            {
                fixed (byte* fixedPtr = description)
                {
                    return new string((sbyte*)fixedPtr);
                }
            }
        }

        /// <value>The creation timestamp (milliseconds since epoch).</value>
        public long createdAt;

        /// <value>The last update timestamp (milliseconds since epoch).</value>
        public long updatedAt;

        /// <value>The total played time in seconds.</value>
        public long playedTime;

        /// <value>The game version as a byte array.</value>
        public fixed byte gameVersion[64];

        /// <value>The string representation of the game version.</value>
        public string gameVersionString
        {
            get
            {
                fixed (byte* fixedPtr = gameVersion)
                {
                    return new string((sbyte*)fixedPtr);
                }
            }
        }

        /// <value>The cover image size (>0 if cover image exists).</value>
        public int coverImageSize;

        /// <value>Whether this save game has a cover image.</value>
        public bool hasCoverImage => coverImageSize > 0;

        /// <value>The player IDs associated with this save as a byte array.</value>
        public fixed byte playerIds[1024];

        /// <value>The string representation of player IDs (comma-separated).</value>
        public string playerIdsString
        {
            get
            {
                fixed (byte* fixedPtr = playerIds)
                {
                    return new string((sbyte*)fixedPtr);
                }
            }
        }

        /// <value>The payload checksum (SHA-256) as a byte array.</value>
        public fixed byte payloadChecksum[65];

        /// <value>The string representation of the payload checksum.</value>
        public string payloadChecksumString
        {
            get
            {
                fixed (byte* fixedPtr = payloadChecksum)
                {
                    return new string((sbyte*)fixedPtr);
                }
            }
        }

        /// <value>The cover image checksum (SHA-256) as a byte array.</value>
        public fixed byte coverImageChecksum[65];

        /// <value>The string representation of the cover image checksum.</value>
        public string coverImageChecksumString
        {
            get
            {
                fixed (byte* fixedPtr = coverImageChecksum)
                {
                    return new string((sbyte*)fixedPtr);
                }
            }
        }

        // <inheritdoc/>
        public override string ToString()
        {
            return
                $"{{saveId={saveIdString} description={descriptionString} gameVersion={gameVersionString} createdAt={createdAt} updatedAt={updatedAt} playedTime={playedTime} hasCover={hasCoverImage} playerIds={playerIdsString} payloadChecksum={payloadChecksumString} coverImageChecksum={coverImageChecksumString}}}";
        }
    }
}