// <copyright file="BoardAppStorageInfo.cs" company="Harris Hill Products Inc.">
//     Copyright (c) Harris Hill Products Inc. All rights reserved.
// </copyright>

namespace Board.Save
{
    /// <summary>
    /// Encapsulates app storage information on the Board device.
    /// </summary>
    public struct BoardAppStorageInfo
    {
        /// <summary>
        /// Total storage space allocated per app (in bytes).
        /// </summary>
        public long totalStorage;

        /// <summary>
        /// Currently used storage space by this app (in bytes).
        /// </summary>
        public long usedStorage;

        /// <summary>
        /// Remaining available storage space by this app (in bytes).
        /// </summary>
        public long remainingStorage;

        /// <summary>
        /// Percentage of storage used by this app (0.0 to 1.0).
        /// </summary>
        public float usagePercentage;
    }
}