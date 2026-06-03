// <copyright file="CombatSystem.cs" company="board.fun game">
//     Trafalgar - Age of Sail. Procedural board.fun RTS.
// </copyright>

namespace Trafalgar.Combat
{
    using System.Collections.Generic;
    using Trafalgar.Core;
    using Trafalgar.Ships;
    using UnityEngine;

    /// <summary>
    /// Resolves broadside gunnery each frame. Guns are mounted along the hull sides, so a ship can
    /// only engage enemies roughly abeam (port / starboard arcs) — never ahead or astern. Each
    /// broadside reloads independently and automatically fires at the best target in its arc.
    /// </summary>
    public class CombatSystem
    {
        /// <summary>
        /// Steps gunnery for all ships: full broadsides abeam, plus weak fore/aft chase fire.
        /// </summary>
        /// <param name="ships">All ships currently in play.</param>
        public void Tick(IReadOnlyList<Ship> ships)
        {
            for (int i = 0; i < ships.Count; i++)
            {
                Ship shooter = ships[i];
                if (!shooter.IsAlive)
                {
                    continue;
                }

                TryFireBroadside(shooter, BroadsideSide.Port, ships);
                TryFireBroadside(shooter, BroadsideSide.Starboard, ships);
                TryFireChase(shooter, bow: true, ships);
                TryFireChase(shooter, bow: false, ships);
            }
        }

        private static void TryFireBroadside(Ship shooter, BroadsideSide side, IReadOnlyList<Ship> ships)
        {
            if (!shooter.IsBroadsideReady(side))
            {
                return;
            }

            Vector3 normal = shooter.BroadsideNormal(side);
            Ship target = FindBestTarget(shooter, normal, GameConfig.BroadsideArcHalfAngle, ships, out float range);
            if (target == null)
            {
                return;
            }

            AmmoProfile profile = Ammo.Profile(shooter.Ammo);
            float falloff = Mathf.Lerp(1f, GameConfig.RangeFalloffFloor, Mathf.Clamp01(range / shooter.Stats.gunRange));
            int guns = Mathf.Clamp(shooter.Stats.gunsPerBroadside, 1, 8);

            shooter.NotifyFired(side);

            // One cannonball per gun, spaced along the hull side. Each ball carries a single gun's
            // worth of damage, so the *total* volley ≈ guns × PerGunDamageScale (no 8× blow-up).
            Vector3 forward = shooter.transform.forward;
            float beamOffset = shooter.Stats.beam * 0.6f;
            float zBack = -shooter.Stats.length * 0.28f;
            float zFront = shooter.Stats.length * 0.30f;
            for (int g = 0; g < guns; g++)
            {
                float spread = 1f + Random.Range(-GameConfig.DamageSpread, GameConfig.DamageSpread);
                target.ApplyDamage(profile, GameConfig.PerGunDamageScale * falloff * spread);

                float t = guns == 1 ? 0.5f : g / (guns - 1f);
                Vector3 origin = shooter.Position + (normal * beamOffset) + (forward * Mathf.Lerp(zBack, zFront, t));
                Projectile.Spawn(origin, ScatterAround(target.Position, target.Stats.beam), profile.tracerColor);
            }
        }

        private static void TryFireChase(Ship shooter, bool bow, IReadOnlyList<Ship> ships)
        {
            if (shooter.Stats.chaseGuns <= 0 || !shooter.IsChaseReady(bow))
            {
                return;
            }

            Vector3 normal = shooter.ChaseNormal(bow);
            Ship target = FindBestTarget(shooter, normal, GameConfig.ChaseArcHalfAngle, ships, out float range);
            if (target == null)
            {
                return;
            }

            AmmoProfile profile = Ammo.Profile(shooter.Ammo);
            float falloff = Mathf.Lerp(1f, GameConfig.RangeFalloffFloor, Mathf.Clamp01(range / shooter.Stats.gunRange));
            int guns = Mathf.Max(1, shooter.Stats.chaseGuns);

            shooter.NotifyChaseFired(bow);

            // Chase guns are few and weak (ChaseDamageFactor), fired from the bow/stern.
            Vector3 right = shooter.transform.right;
            float reach = shooter.Stats.length * 0.45f;
            for (int g = 0; g < guns; g++)
            {
                float spread = 1f + Random.Range(-GameConfig.DamageSpread, GameConfig.DamageSpread);
                target.ApplyDamage(profile, GameConfig.PerGunDamageScale * GameConfig.ChaseDamageFactor * falloff * spread);

                float lateral = (guns == 1 ? 0f : (g / (guns - 1f)) - 0.5f) * shooter.Stats.beam * 0.5f;
                Vector3 origin = shooter.Position + (normal * reach) + (right * lateral);
                Projectile.Spawn(origin, ScatterAround(target.Position, target.Stats.beam), profile.tracerColor);
            }
        }

        private static Vector3 ScatterAround(Vector3 center, float radius)
        {
            return center + new Vector3(Random.Range(-radius, radius), 0f, Random.Range(-radius, radius));
        }

        private static Ship FindBestTarget(Ship shooter, Vector3 normal, float arcHalfAngle, IReadOnlyList<Ship> ships, out float bestRange)
        {
            bestRange = float.MaxValue;
            Ship best = null;
            Faction enemy = shooter.Faction.Enemy();

            for (int i = 0; i < ships.Count; i++)
            {
                Ship candidate = ships[i];
                if (candidate == shooter || !candidate.IsAlive || candidate.Faction != enemy)
                {
                    continue;
                }

                Vector3 to = candidate.Position - shooter.Position;
                to.y = 0f;
                float dist = to.magnitude;
                if (dist > shooter.Stats.gunRange || dist < 0.001f)
                {
                    continue;
                }

                float angle = Vector3.Angle(normal, to);
                if (angle > arcHalfAngle)
                {
                    continue;
                }

                // Prefer the closest valid target for this battery.
                if (dist < bestRange)
                {
                    bestRange = dist;
                    best = candidate;
                }
            }

            return best;
        }
    }
}
