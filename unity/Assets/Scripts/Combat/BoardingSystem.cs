// <copyright file="BoardingSystem.cs" company="board.fun game">
//     Trafalgar - Age of Sail. Procedural board.fun RTS.
// </copyright>

namespace Trafalgar.Combat
{
    using System.Collections.Generic;
    using Trafalgar.Core;
    using Trafalgar.Ships;
    using UnityEngine;

    /// <summary>
    /// Resolves boarding actions. When an attacker lies alongside a sufficiently weakened enemy
    /// (low hull and/or decimated crew), grappling hooks fly across and, after a short struggle,
    /// the enemy strikes her colours and is captured rather than sunk.
    /// </summary>
    public class BoardingSystem
    {
        private readonly Dictionary<long, float> m_Progress = new Dictionary<long, float>();
        private readonly List<long> m_Stale = new List<long>();

        /// <summary>Raised when a ship is captured. Args: the captured ship and the new owner.</summary>
        public event System.Action<Ship, Faction> ShipCaptured;

        /// <summary>
        /// Steps boarding for all ships.
        /// </summary>
        /// <param name="ships">All ships currently in play.</param>
        /// <param name="dt">Delta time in seconds.</param>
        public void Tick(IReadOnlyList<Ship> ships, float dt)
        {
            var active = new HashSet<long>();

            for (int i = 0; i < ships.Count; i++)
            {
                Ship attacker = ships[i];
                if (!attacker.IsAlive)
                {
                    continue;
                }

                Faction enemy = attacker.Faction.Enemy();

                for (int j = 0; j < ships.Count; j++)
                {
                    Ship victim = ships[j];
                    if (victim == attacker || !victim.IsAlive || victim.Faction != enemy)
                    {
                        continue;
                    }

                    if (!victim.IsBoardable)
                    {
                        continue;
                    }

                    float dist = Vector3.Distance(attacker.Position, victim.Position);
                    if (dist > GameConfig.BoardingRange)
                    {
                        continue;
                    }

                    long key = PairKey(attacker, victim);
                    active.Add(key);
                    m_Progress.TryGetValue(key, out float t);
                    t += dt;
                    m_Progress[key] = t;

                    if (t >= GameConfig.BoardingDuration)
                    {
                        Faction captor = attacker.Faction;
                        victim.Capture(captor);
                        m_Progress.Remove(key);
                        ShipCaptured?.Invoke(victim, captor);
                    }
                }
            }

            // Drop progress for pairs that are no longer adjacent (boarders beaten back).
            m_Stale.Clear();
            foreach (var kvp in m_Progress)
            {
                if (!active.Contains(kvp.Key))
                {
                    m_Stale.Add(kvp.Key);
                }
            }

            for (int i = 0; i < m_Stale.Count; i++)
            {
                m_Progress.Remove(m_Stale[i]);
            }
        }

        /// <summary>Gets the boarding progress (0..1) targeting a victim, for HUD display.</summary>
        /// <param name="victim">The ship being boarded.</param>
        /// <param name="ships">All ships in play.</param>
        /// <returns>The highest boarding progress against the victim, or 0.</returns>
        public float ProgressAgainst(Ship victim, IReadOnlyList<Ship> ships)
        {
            float best = 0f;
            for (int i = 0; i < ships.Count; i++)
            {
                Ship attacker = ships[i];
                if (!attacker.IsAlive || attacker.Faction == victim.Faction)
                {
                    continue;
                }

                if (m_Progress.TryGetValue(PairKey(attacker, victim), out float t))
                {
                    best = Mathf.Max(best, t / GameConfig.BoardingDuration);
                }
            }

            return Mathf.Clamp01(best);
        }

        private static long PairKey(Ship attacker, Ship victim)
        {
            return ((long)attacker.GetInstanceID() << 32) ^ (uint)victim.GetInstanceID();
        }
    }
}
