// <copyright file="GameBootstrap.cs" company="board.fun game">
//     Trafalgar - Age of Sail. Procedural board.fun RTS.
// </copyright>

namespace Trafalgar.Core
{
    using UnityEngine;

    /// <summary>
    /// Entry point. Because the whole game is built procedurally, there is no authored scene to
    /// open: pressing Play in any empty scene is enough. This bootstrap runs automatically as the
    /// runtime spins up and creates the single persistent <see cref="GameManager"/>, which in turn
    /// constructs the camera, sea, fleets and HUD.
    /// </summary>
    public static class GameBootstrap
    {
        private static bool s_Started;

        /// <summary>
        /// Resets static state before each play session so the bootstrap still runs when the editor
        /// has "Reload Domain" disabled in Enter Play Mode options.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetState()
        {
            s_Started = false;
        }

        /// <summary>
        /// Creates the <see cref="GameManager"/> after the first scene loads. Guarded so it only
        /// ever runs once per play session.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Launch()
        {
            if (s_Started)
            {
                return;
            }

            s_Started = true;

            var go = new GameObject("Trafalgar Game");
            go.AddComponent<GameManager>();
            Object.DontDestroyOnLoad(go);
        }
    }
}
