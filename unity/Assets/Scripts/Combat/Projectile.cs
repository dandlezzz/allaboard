// <copyright file="Projectile.cs" company="board.fun game">
//     Trafalgar - Age of Sail. Procedural board.fun RTS.
// </copyright>

namespace Trafalgar.Combat
{
    using Trafalgar.Core;
    using Trafalgar.Rendering;
    using UnityEngine;

    /// <summary>
    /// A purely cosmetic cannon shot. Damage is resolved instantly when a broadside fires; this
    /// flying tracer just sells the moment as it streaks from the gun deck to the target.
    /// </summary>
    public class Projectile : MonoBehaviour
    {
        private Vector3 m_Target;
        private float m_Life;

        private static Mesh s_Mesh;

        /// <summary>
        /// Spawns a tracer travelling from <paramref name="origin"/> toward <paramref name="target"/>.
        /// </summary>
        /// <param name="origin">World start position.</param>
        /// <param name="target">World end position.</param>
        /// <param name="color">Tracer colour (from the ammo profile).</param>
        public static void Spawn(Vector3 origin, Vector3 target, Color color)
        {
            var go = new GameObject("Shot");
            go.transform.position = origin + (Vector3.up * 0.4f);
            go.transform.localScale = Vector3.one * (0.45f * GameConfig.ShipScale);

            if (s_Mesh == null)
            {
                s_Mesh = MeshUtil.QuadXZ();
            }

            go.AddComponent<MeshFilter>().sharedMesh = s_Mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = MaterialUtil.Unlit(color);

            var p = go.AddComponent<Projectile>();
            p.m_Target = target + (Vector3.up * 0.4f);
            float dist = Vector3.Distance(go.transform.position, p.m_Target);
            p.m_Life = Mathf.Max(0.05f, dist / GameConfig.ProjectileSpeed);
        }

        private void Update()
        {
            float step = GameConfig.ProjectileSpeed * Time.deltaTime;
            transform.position = Vector3.MoveTowards(transform.position, m_Target, step);
            m_Life -= Time.deltaTime;
            if (m_Life <= 0f || (transform.position - m_Target).sqrMagnitude < 0.05f)
            {
                Destroy(gameObject);
            }
        }
    }
}
