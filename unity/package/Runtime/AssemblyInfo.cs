// <copyright file="AssemblyInfo.cs" company="Harris Hill Products Inc.">
//     Copyright (c) Harris Hill Products Inc. All rights reserved.
// </copyright>

using System.Runtime.CompilerServices;

#if UNITY_ANDROID
[assembly: UnityEngine.Scripting.AlwaysLinkAssembly]
#endif

[assembly: InternalsVisibleTo("Board.Editor")]