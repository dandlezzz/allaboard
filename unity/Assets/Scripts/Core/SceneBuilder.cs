// <copyright file="SceneBuilder.cs" company="board.fun game">
//     Trafalgar - Age of Sail. Procedural board.fun RTS.
// </copyright>

namespace Trafalgar.Core
{
    using Trafalgar.Rendering;
    using UnityEngine;

    /// <summary>
    /// Procedurally constructs the static scene furniture: an overhead orthographic camera, a
    /// directional light, and the sea. There are no scene or prefab assets to author by hand.
    /// </summary>
    public static class SceneBuilder
    {
        /// <summary>
        /// Builds the overhead RTS camera looking straight down at the sea (no fog of war: the
        /// whole arena is always in frame).
        /// </summary>
        /// <param name="parent">Parent transform for organisation.</param>
        /// <returns>The created <see cref="Camera"/>.</returns>
        public static Camera BuildCamera(Transform parent)
        {
            // An empty Unity scene ships with a default perspective "Main Camera"; disable any
            // pre-existing cameras so only our overhead RTS view renders.
            Camera[] existing = Object.FindObjectsOfType<Camera>();
            for (int i = 0; i < existing.Length; i++)
            {
                existing[i].gameObject.SetActive(false);
            }

            var go = new GameObject("Trafalgar Camera");
            go.transform.SetParent(parent, false);
            go.tag = "MainCamera";
            var cam = go.AddComponent<Camera>();
            go.AddComponent<AudioListener>();
            cam.orthographic = true;
            cam.orthographicSize = GameConfig.CameraOrthoSize;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.08f, 0.16f, 0.26f);
            cam.transform.position = new Vector3(0f, 120f, 0f);
            cam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            cam.farClipPlane = 400f;
            cam.nearClipPlane = 0.3f;
            return cam;
        }

        /// <summary>Builds the key directional light and a little ambient fill.</summary>
        /// <param name="parent">Parent transform for organisation.</param>
        public static void BuildLighting(Transform parent)
        {
            var go = new GameObject("Sun");
            go.transform.SetParent(parent, false);
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.96f, 0.86f);
            light.intensity = 1.1f;
            light.transform.rotation = Quaternion.Euler(60f, 30f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.45f, 0.5f, 0.6f);
        }

        /// <summary>
        /// Builds the sea: a large quad with a deep-blue material, plus a subtle lighter "play area"
        /// inset so players can read the arena bounds.
        /// </summary>
        /// <param name="parent">Parent transform for organisation.</param>
        public static void BuildSea(Transform parent)
        {
            // Rectangular 16:9 field. A clean, flat solid light-blue plane (no texture, no waves,
            // no animation) fills the camera view; a thin lighter frame marks the play bounds.
            float seaX = GameConfig.ArenaHalfX * 2.6f;
            float seaZ = GameConfig.ArenaHalfZ * 2.6f;

            var sea = new GameObject("Sea");
            sea.transform.SetParent(parent, false);
            sea.transform.localScale = new Vector3(seaX, 1f, seaZ);
            sea.AddComponent<MeshFilter>().sharedMesh = MeshUtil.QuadXZ();
            sea.AddComponent<MeshRenderer>().sharedMaterial = MaterialUtil.Lit(new Color(0.42f, 0.66f, 0.86f), 0.1f);

            // Play-area boundary frame (four thin bars at ±ArenaHalfX / ±ArenaHalfZ).
            var frame = new GameObject("ArenaFrame").transform;
            frame.SetParent(parent, false);
            frame.localPosition = new Vector3(0f, 0.02f, 0f);
            Color edge = new Color(0.45f, 0.6f, 0.72f, 1f);
            float thick = 1.2f * GameConfig.ShipScale;
            float halfX = GameConfig.ArenaHalfX;
            float halfZ = GameConfig.ArenaHalfZ;
            MakeBar(frame, edge, new Vector3(0f, 0f, halfZ), new Vector3(halfX * 2f, 1f, thick));
            MakeBar(frame, edge, new Vector3(0f, 0f, -halfZ), new Vector3(halfX * 2f, 1f, thick));
            MakeBar(frame, edge, new Vector3(halfX, 0f, 0f), new Vector3(thick, 1f, halfZ * 2f));
            MakeBar(frame, edge, new Vector3(-halfX, 0f, 0f), new Vector3(thick, 1f, halfZ * 2f));
        }

        private static void MakeBar(Transform parent, Color color, Vector3 localPos, Vector3 localScale)
        {
            var bar = new GameObject("Edge");
            bar.transform.SetParent(parent, false);
            bar.transform.localPosition = localPos;
            bar.transform.localScale = localScale;
            bar.AddComponent<MeshFilter>().sharedMesh = MeshUtil.QuadXZ();
            bar.AddComponent<MeshRenderer>().sharedMaterial = MaterialUtil.Unlit(color);
        }
    }
}
