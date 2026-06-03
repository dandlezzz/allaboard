// <copyright file="MeshUtil.cs" company="board.fun game">
//     Trafalgar - Age of Sail. Procedural board.fun RTS.
// </copyright>

namespace Trafalgar.Rendering
{
    using System.Collections.Generic;
    using UnityEngine;

    /// <summary>
    /// Procedural mesh builders. Everything the game renders (hulls, sails, rings, the sea) is
    /// generated here at runtime, so no imported art assets are required.
    /// </summary>
    public static class MeshUtil
    {
        /// <summary>
        /// Builds a flat top-down silhouette of an age-of-sail tall ship on the XZ plane, bow
        /// pointing toward +Z: a fine, tapered (pointed) bow, full curved sides through midships,
        /// and a flat, squared transom stern.
        /// </summary>
        /// <param name="length">Hull length along Z.</param>
        /// <param name="beam">Hull width along X.</param>
        /// <returns>A new <see cref="Mesh"/> (normal facing +Y).</returns>
        public static Mesh ShipHull(float length, float beam)
        {
            return FanPolygon(HullOutline(length, beam), 0f);
        }

        /// <summary>
        /// Same hull silhouette as <see cref="ShipHull"/> but UV-mapped with a world-planar
        /// projection so a tiling plank texture keeps a constant plank width regardless of ship
        /// size (and the same shared deck material works for every class).
        /// </summary>
        /// <param name="length">Hull length along Z.</param>
        /// <param name="beam">Hull width along X.</param>
        /// <param name="worldTile">World-space size mapped to one texture tile.</param>
        /// <returns>A new UV-mapped deck <see cref="Mesh"/>.</returns>
        public static Mesh ShipDeck(float length, float beam, float worldTile)
        {
            return FanPolygon(HullOutline(length, beam), Mathf.Max(0.001f, worldTile));
        }

        /// <summary>Builds the closed top-down hull outline (bow → starboard → transom → port).</summary>
        private static List<Vector3> HullOutline(float length, float beam)
        {
            float hl = length * 0.5f;
            float hb = beam * 0.5f;

            // Sample the half-beam profile along the hull and mirror it to make a closed outline.
            // Wound clockwise from above: bow tip, down the starboard (+X) side to the stern, across
            // the flat transom, then up the port (-X) side back toward the bow.
            const int stations = 13;
            var outline = new List<Vector3>(stations * 2);

            // Starboard side: bow (u=1) down to stern (u=0).
            for (int i = 0; i < stations; i++)
            {
                float u = 1f - (i / (stations - 1f));
                float z = Mathf.Lerp(-hl, hl, u);
                outline.Add(new Vector3(hb * HullHalfWidth(u), 0f, z));
            }

            // Port side: stern corner (u=0) up to just below the bow (excludes the duplicated tip).
            for (int i = 0; i < stations - 1; i++)
            {
                float u = i / (stations - 1f);
                float z = Mathf.Lerp(-hl, hl, u);
                outline.Add(new Vector3(-hb * HullHalfWidth(u), 0f, z));
            }

            return outline;
        }

        /// <summary>
        /// Returns the world-XZ point on the hull's gunwale (deck edge) at longitudinal position
        /// <paramref name="u"/> (0 = stern, 1 = bow) for the given <paramref name="side"/> (+1
        /// starboard, -1 port). Used to lay cannons, gunports and railings along the curved rail.
        /// </summary>
        /// <param name="length">Hull length along Z.</param>
        /// <param name="beam">Hull width along X.</param>
        /// <param name="u">Longitudinal position in [0, 1].</param>
        /// <param name="side">+1 for starboard (+X), -1 for port (-X).</param>
        /// <returns>A point on the XZ plane.</returns>
        public static Vector3 HullEdgePoint(float length, float beam, float u, float side)
        {
            return new Vector3(side * (beam * 0.5f) * HullHalfWidth(u), 0f, Mathf.Lerp(-length * 0.5f, length * 0.5f, u));
        }

        /// <summary>Builds a flat filled disk on the XZ plane (capstans, mast collars, figurehead).</summary>
        /// <param name="radius">Disk radius.</param>
        /// <param name="segments">Number of perimeter segments.</param>
        /// <returns>A new disk <see cref="Mesh"/> (normal facing +Y).</returns>
        public static Mesh Disk(float radius, int segments = 24)
        {
            segments = Mathf.Max(6, segments);
            var verts = new Vector3[segments + 1];
            var norms = new Vector3[segments + 1];
            var uvs = new Vector2[segments + 1];
            verts[0] = Vector3.zero;
            norms[0] = Vector3.up;
            uvs[0] = new Vector2(0.5f, 0.5f);
            for (int i = 0; i < segments; i++)
            {
                float a = (i / (float)segments) * Mathf.PI * 2f;
                float cos = Mathf.Cos(a);
                float sin = Mathf.Sin(a);
                verts[i + 1] = new Vector3(cos * radius, 0f, sin * radius);
                norms[i + 1] = Vector3.up;
                uvs[i + 1] = new Vector2((cos * 0.5f) + 0.5f, (sin * 0.5f) + 0.5f);
            }

            var tris = new int[segments * 3];
            int t = 0;
            for (int i = 0; i < segments; i++)
            {
                tris[t++] = 0;
                tris[t++] = i + 1;
                tris[t++] = ((i + 1) % segments) + 1;
            }

            var mesh = new Mesh { name = "Disk" };
            mesh.vertices = verts;
            mesh.normals = norms;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// Normalised half-beam (0..1) of the hull at longitudinal position <paramref name="u"/>,
        /// where u = 0 is the stern and u = 1 the bow. Gives a fairly full, squared transom, a
        /// maximum beam a little aft of midships, and a fine cosine taper to a point at the bow.
        /// </summary>
        private static float HullHalfWidth(float u)
        {
            // Sleeker, more ship-like sections: a narrow flat transom, max beam a little aft of
            // midships, and a long, fine entry tapering to a sharp point at the bow (the power on the
            // cosine sharpens the bow so the hull reads as a tall ship, not an oval/lozenge).
            const float transom = 0.50f;  // flat stern width as a fraction of max beam
            const float widestAt = 0.42f; // longitudinal position of maximum beam (just aft of mid)

            if (u <= widestAt)
            {
                float t = Mathf.SmoothStep(0f, 1f, u / widestAt);
                return Mathf.Lerp(transom, 1f, t);
            }

            float k = (u - widestAt) / (1f - widestAt);
            return Mathf.Pow(Mathf.Cos(k * Mathf.PI * 0.5f), 1.35f); // full midbody → fine, pointed bow
        }

        /// <summary>
        /// Builds a flat annulus (ring) on the XZ plane, used for selection / range indicators.
        /// </summary>
        /// <param name="innerRadius">Inner radius.</param>
        /// <param name="outerRadius">Outer radius.</param>
        /// <param name="segments">Number of radial segments.</param>
        /// <returns>A new ring <see cref="Mesh"/> (normal facing +Y).</returns>
        public static Mesh Ring(float innerRadius, float outerRadius, int segments = 48)
        {
            segments = Mathf.Max(8, segments);
            var verts = new Vector3[segments * 2];
            var norms = new Vector3[segments * 2];
            var tris = new int[segments * 6];

            for (int i = 0; i < segments; i++)
            {
                float a = (i / (float)segments) * Mathf.PI * 2f;
                float cos = Mathf.Cos(a);
                float sin = Mathf.Sin(a);
                verts[i * 2] = new Vector3(cos * innerRadius, 0f, sin * innerRadius);
                verts[(i * 2) + 1] = new Vector3(cos * outerRadius, 0f, sin * outerRadius);
                norms[i * 2] = Vector3.up;
                norms[(i * 2) + 1] = Vector3.up;
            }

            int t = 0;
            for (int i = 0; i < segments; i++)
            {
                int inner0 = i * 2;
                int outer0 = (i * 2) + 1;
                int inner1 = ((i + 1) % segments) * 2;
                int outer1 = (((i + 1) % segments) * 2) + 1;

                tris[t++] = inner0;
                tris[t++] = outer1;
                tris[t++] = outer0;

                tris[t++] = inner0;
                tris[t++] = inner1;
                tris[t++] = outer1;
            }

            var mesh = new Mesh { name = "Ring" };
            mesh.vertices = verts;
            mesh.normals = norms;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// Builds a small flat isoceles triangle on the XZ plane, apex toward +Z, fitting a unit
        /// square. Double-sided so it always reads under the top-down camera. Used for arrow icons.
        /// </summary>
        /// <returns>A new triangle <see cref="Mesh"/>.</returns>
        public static Mesh Triangle()
        {
            var mesh = new Mesh { name = "Triangle" };
            mesh.vertices = new[]
            {
                new Vector3(0f, 0f, 0.5f),    // apex (+Z)
                new Vector3(-0.5f, 0f, -0.5f),
                new Vector3(0.5f, 0f, -0.5f),
            };
            mesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up };
            mesh.uv = new[] { new Vector2(0.5f, 1f), new Vector2(0f, 0f), new Vector2(1f, 0f) };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 1 }; // both windings → visible from either side
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// Builds a unit quad on the XZ plane (1x1, centred), normal facing +Y.
        /// </summary>
        /// <returns>A new quad <see cref="Mesh"/>.</returns>
        public static Mesh QuadXZ()
        {
            var mesh = new Mesh { name = "QuadXZ" };
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, 0f, -0.5f),
                new Vector3(-0.5f, 0f, 0.5f),
                new Vector3(0.5f, 0f, 0.5f),
                new Vector3(0.5f, 0f, -0.5f),
            };
            mesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            mesh.uv = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 0f),
            };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// Triangulates a convex/star-convex polygon outline as a triangle fan from its centroid.
        /// </summary>
        /// <param name="outline">Ordered XZ outline vertices.</param>
        /// <param name="worldTile">
        /// If &gt; 0, UVs are a world-planar projection where this many world units map to one
        /// texture tile (so a tiling texture keeps constant feature size). 0 = no UVs needed.
        /// </param>
        /// <returns>A new filled <see cref="Mesh"/> (normal facing +Y).</returns>
        private static Mesh FanPolygon(List<Vector3> outline, float worldTile)
        {
            int n = outline.Count;
            var verts = new Vector3[n + 1];
            var norms = new Vector3[n + 1];
            var uvs = new Vector2[n + 1];

            Vector3 centroid = Vector3.zero;
            for (int i = 0; i < n; i++)
            {
                centroid += outline[i];
            }

            centroid /= n;

            float inv = worldTile > 0f ? 1f / worldTile : 0f;
            verts[0] = centroid;
            norms[0] = Vector3.up;
            uvs[0] = new Vector2(centroid.x * inv, centroid.z * inv);
            for (int i = 0; i < n; i++)
            {
                verts[i + 1] = outline[i];
                norms[i + 1] = Vector3.up;
                uvs[i + 1] = new Vector2(outline[i].x * inv, outline[i].z * inv);
            }

            var tris = new int[n * 3];
            int t = 0;
            for (int i = 0; i < n; i++)
            {
                int a = i + 1;
                int b = ((i + 1) % n) + 1;
                tris[t++] = 0;
                tris[t++] = a;
                tris[t++] = b;
            }

            var mesh = new Mesh { name = "Polygon" };
            mesh.vertices = verts;
            mesh.normals = norms;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
