// <copyright file="ShipView.cs" company="board.fun game">
//     Trafalgar - Age of Sail. Procedural board.fun RTS.
// </copyright>

namespace Trafalgar.Ships
{
    using System.Collections.Generic;
    using Trafalgar.Combat;
    using Trafalgar.Core;
    using Trafalgar.Rendering;
    using UnityEngine;

    /// <summary>A tappable on-ring control around a selected ship.</summary>
    public enum ShipControl
    {
        /// <summary>No control hit.</summary>
        None = 0,

        /// <summary>Nudge the ordered heading to port (left).</summary>
        Port,

        /// <summary>Nudge the ordered heading to starboard (right).</summary>
        Starboard,

        /// <summary>Step the sail plan up (more canvas / faster).</summary>
        SailUp,

        /// <summary>Step the sail plan down (less canvas / slower).</summary>
        SailDown,

        /// <summary>Cycle the loaded ammunition (round → bar → grape).</summary>
        AmmoCycle,
    }

    /// <summary>
    /// Builds and animates the fully-procedural visual representation of a <see cref="Ship"/>: a
    /// wooden tall-ship read from overhead — planked deck, bulwark railings, hatches/gratings, a
    /// capstan, masts with collars, a bowsprit + figurehead, broadside + chase guns at gunports,
    /// standing rigging, and translucent sails that reef as the ship slows. Faction identity rides
    /// on a coloured gun-stripe, stern flag and selection ring. Meshes are cached per ship class and
    /// most materials/textures are shared across the whole fleet, so per-ship cost stays low.
    /// </summary>
    [RequireComponent(typeof(Ship))]
    public class ShipView : MonoBehaviour
    {
        // ---- Shared, fleet-wide resources (built once) -------------------------------------
        private static Mesh s_Quad;
        private static Mesh s_Disk;
        private static Mesh s_Triangle;
        private static Material s_IconMat;
        private static Material s_DeckMat;
        private static Material s_SailMat;
        private static Material s_GratingMat;
        private static Material s_IronMat;
        private static Material s_RopeMat;
        private static Material s_WoodMat;
        private static Material s_RailMat;
        private static Material s_StepMat;
        private static Material s_GoldMat;
        private static readonly Dictionary<ShipClass, Mesh> s_HullMesh = new Dictionary<ShipClass, Mesh>();
        private static readonly Dictionary<ShipClass, Mesh> s_StripeMesh = new Dictionary<ShipClass, Mesh>();
        private static readonly Dictionary<ShipClass, Mesh> s_DeckMesh = new Dictionary<ShipClass, Mesh>();
        private static readonly Dictionary<ShipClass, Mesh> s_RailMesh = new Dictionary<ShipClass, Mesh>();
        private static readonly Dictionary<ShipClass, Mesh> s_AmmoRingMesh = new Dictionary<ShipClass, Mesh>();

        // Number of speed-gauge ticks around each ship's status ring.
        private const int kSpeedTicks = 10;

        // ---- Per-ship state ----------------------------------------------------------------
        private Ship m_Ship;
        private MeshRenderer m_HullRenderer;
        private Material m_HullMaterial;
        private Material m_StripeMaterial;
        private Color m_HullBaseColor;

        private Transform m_SailGroup;
        private MeshRenderer[] m_Sails;
        private float[] m_SailWidth;
        private float[] m_SailDepth;
        private float[] m_MastZ;

        private Transform m_Flag;
        private MeshRenderer m_FlagRenderer;
        private Material m_FlagMaterial;

        private Transform m_SelectionRing;
        private Material m_SelectionMaterial;

        // Status rings: ammo (ring colour), speed (lit tick count), course (heading needle).
        private Transform m_StatusRingRoot;
        private Transform m_CoursePivot;
        private Material m_AmmoMat;
        private Material m_CourseMat;
        private Material m_SpeedOnMat;
        private Material m_SpeedOffMat;
        private MeshRenderer[] m_SpeedTicks;
        private float m_StatusBright = 0.55f; // 1.0 when selected, dimmer otherwise
        private int m_LastSpeedLit = -1;
        private AmmoType m_LastAmmo = (AmmoType)(-1);
        private bool m_LastSelected;

        // On-ring control buttons (only shown for the selected ship).
        private Transform m_ControlGroup;
        private List<ControlButton> m_ControlButtons;
        private Material m_AmmoIconMat;
        private float m_ButtonHitRadius;

        private class ControlButton
        {
            public ShipControl type;
            public Transform tf;
            public Material mat;
            public Color baseColor;
            public float flash;
        }

        private Transform m_StatusGroup;
        private Transform m_HullBar;
        private Transform m_RiggingBar;
        private Transform m_CrewBar;
        private float m_BarWidth;

        private ParticleSystem m_Smoke;
        private float m_HitFlash;

        /// <summary>Constructs the visual hierarchy for a ship. Idempotent per ship instance.</summary>
        /// <param name="ship">The owning ship.</param>
        public void Build(Ship ship)
        {
            m_Ship = ship;
            ShipStats stats = ship.Stats;

            EnsureShared();
            ComputeMasts(stats);

            BuildHull(stats);
            BuildDeckFeatures(stats);
            BuildBowsprit(stats);
            BuildCannons(stats);
            BuildRigging(stats);
            BuildSails(stats);
            BuildFlag(stats);
            BuildSelectionRing(stats);
            BuildStatusRings(stats);
            BuildControlButtons(stats);
            BuildStatusBars(stats);
            BuildSmoke();

            OnFactionChanged();
            SetSelected(false, Faction.Neutral);
        }

        // ---- Shared resource setup ---------------------------------------------------------

        private static void EnsureShared()
        {
            if (s_Quad == null)
            {
                s_Quad = MeshUtil.QuadXZ();
            }

            if (s_Disk == null)
            {
                s_Disk = MeshUtil.Disk(0.5f, 24);
            }

            if (s_Triangle == null)
            {
                s_Triangle = MeshUtil.Triangle();
            }

            if (s_IconMat == null)
            {
                s_IconMat = MaterialUtil.Unlit(new Color(0.96f, 0.97f, 1f));
            }

            if (s_DeckMat == null)
            {
                s_DeckMat = MaterialUtil.LitTextured(TextureUtil.PlankTexture(), Color.white, 0.05f);
            }

            if (s_SailMat == null)
            {
                s_SailMat = MaterialUtil.UnlitTransparent(TextureUtil.CanvasTexture());
            }

            if (s_GratingMat == null)
            {
                s_GratingMat = MaterialUtil.UnlitTransparent(TextureUtil.GratingTexture());
            }

            if (s_IronMat == null)
            {
                s_IronMat = MaterialUtil.Lit(new Color(0.09f, 0.09f, 0.11f), 0.3f);
            }

            if (s_RopeMat == null)
            {
                s_RopeMat = MaterialUtil.Lit(new Color(0.16f, 0.13f, 0.10f), 0.05f);
            }

            if (s_WoodMat == null)
            {
                s_WoodMat = MaterialUtil.Lit(new Color(0.45f, 0.32f, 0.20f), 0.08f);
            }

            if (s_RailMat == null)
            {
                s_RailMat = MaterialUtil.Lit(new Color(0.20f, 0.13f, 0.07f), 0.08f);
            }

            if (s_StepMat == null)
            {
                s_StepMat = MaterialUtil.Lit(new Color(0.68f, 0.52f, 0.32f), 0.05f);
            }

            if (s_GoldMat == null)
            {
                s_GoldMat = MaterialUtil.Lit(new Color(0.86f, 0.70f, 0.32f), 0.4f);
            }
        }

        private static Mesh ClassMesh(Dictionary<ShipClass, Mesh> cache, ShipClass cls, System.Func<Mesh> build)
        {
            if (!cache.TryGetValue(cls, out Mesh m) || m == null)
            {
                m = build();
                cache[cls] = m;
            }

            return m;
        }

        private void ComputeMasts(ShipStats stats)
        {
            int masts = stats.shipClass == ShipClass.Frigate ? 2 : 3;
            m_MastZ = new float[masts];
            for (int i = 0; i < masts; i++)
            {
                float t = masts == 1 ? 0.5f : i / (masts - 1f);
                m_MastZ[i] = Mathf.Lerp(stats.length * 0.30f, -stats.length * 0.28f, t);
            }
        }

        // ---- Hull / deck -------------------------------------------------------------------

        private void BuildHull(ShipStats stats)
        {
            // Concentric filled hull silhouettes, all hugging the same curve so the rail and deck
            // follow the true hull shape with no gaps. Bottom-to-top: dark hull planking (waterline)
            // → continuous rail/bulwark cap → faction gun-stripe → planked deck. Each shows as a
            // ring around the layer above it.
            m_HullBaseColor = new Color(0.34f, 0.22f, 0.13f);
            ShipClass cls = stats.shipClass;

            var hull = new GameObject("Hull");
            hull.transform.SetParent(transform, false);
            hull.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            hull.AddComponent<MeshFilter>().sharedMesh = ClassMesh(s_HullMesh, cls, () => MeshUtil.ShipHull(stats.length, stats.beam));
            m_HullRenderer = hull.AddComponent<MeshRenderer>();
            m_HullMaterial = MaterialUtil.Lit(m_HullBaseColor, 0.05f);
            m_HullRenderer.sharedMaterial = m_HullMaterial;

            // Continuous bulwark/rail cap: a slightly-inset filled hull silhouette, so the rail is an
            // unbroken outline following the hull bow-to-stern (replacing the old dotted posts).
            var rail = new GameObject("Rail");
            rail.transform.SetParent(hull.transform, false);
            rail.transform.localPosition = new Vector3(0f, 0.008f, 0f);
            rail.AddComponent<MeshFilter>().sharedMesh = ClassMesh(s_RailMesh, cls, () => MeshUtil.ShipHull(stats.length * 0.95f, stats.beam * 0.90f));
            rail.AddComponent<MeshRenderer>().sharedMaterial = s_RailMat;

            var stripe = new GameObject("Stripe");
            stripe.transform.SetParent(hull.transform, false);
            stripe.transform.localPosition = new Vector3(0f, 0.014f, 0f);
            stripe.AddComponent<MeshFilter>().sharedMesh = ClassMesh(s_StripeMesh, cls, () => MeshUtil.ShipHull(stats.length * 0.88f, stats.beam * 0.80f));
            var sr = stripe.AddComponent<MeshRenderer>();
            m_StripeMaterial = MaterialUtil.Lit(m_Ship.Faction.AccentColor(), 0.05f);
            sr.sharedMaterial = m_StripeMaterial;

            float plankTile = 2f * GameConfig.ShipScale;
            var deck = new GameObject("Deck");
            deck.transform.SetParent(hull.transform, false);
            deck.transform.localPosition = new Vector3(0f, 0.02f, -stats.length * 0.03f);
            deck.AddComponent<MeshFilter>().sharedMesh = ClassMesh(s_DeckMesh, cls, () => MeshUtil.ShipDeck(stats.length * 0.72f, stats.beam * 0.56f, plankTile));
            deck.AddComponent<MeshRenderer>().sharedMaterial = s_DeckMat;
        }

        // ---- Deck features -----------------------------------------------------------------

        private void BuildDeckFeatures(ShipStats stats)
        {
            var deckGroup = new GameObject("DeckFeatures").transform;
            deckGroup.SetParent(transform, false);

            // Masts (dark disks) with lighter mast-collar rings at their bases.
            float mastR = stats.beam * 0.085f;
            for (int i = 0; i < m_MastZ.Length; i++)
            {
                Vector3 at = new Vector3(0f, 0.088f, m_MastZ[i]);
                MakePiece(deckGroup, "MastCollar", s_Disk, s_WoodMat, at, Diam(mastR * 2.0f));
                MakePiece(deckGroup, "Mast", s_Disk, s_IronMat, at + new Vector3(0f, 0.004f, 0f), Diam(mastR));
            }

            // Capstan: a low wooden drum just aft of midships with a darker cap.
            float capR = stats.beam * 0.13f;
            Vector3 capAt = new Vector3(0f, 0.09f, -stats.length * 0.04f);
            MakePiece(deckGroup, "Capstan", s_Disk, s_WoodMat, capAt, Diam(capR * 2f));
            MakePiece(deckGroup, "CapstanCap", s_Disk, s_IronMat, capAt + new Vector3(0f, 0.004f, 0f), Diam(capR));

            // Hatches / gratings along the centreline (more on grander ships).
            int hatches = stats.shipClass == ShipClass.FirstRate ? 3 : 2;
            float hatch = stats.beam * 0.24f;
            for (int i = 0; i < hatches; i++)
            {
                float z = Mathf.Lerp(stats.length * 0.20f, -stats.length * 0.20f, hatches == 1 ? 0.5f : i / (hatches - 1f));
                MakePiece(deckGroup, "Grating", s_Quad, s_GratingMat, new Vector3(0f, 0.082f, z), new Vector3(hatch, 1f, hatch));
            }

            // Companionway ladder: a short flight of light steps near the forward hatch.
            float stepW = stats.beam * 0.22f;
            float stepGap = stats.length * 0.022f;
            for (int s = 0; s < 4; s++)
            {
                MakePiece(deckGroup, "Step", s_Quad, s_StepMat,
                    new Vector3(0f, 0.083f, (stats.length * 0.30f) + (s * stepGap)),
                    new Vector3(stepW, 1f, stepGap * 0.6f));
            }
        }

        private void BuildBowsprit(ShipStats stats)
        {
            var group = new GameObject("Bow").transform;
            group.SetParent(transform, false);

            // Bowsprit: a wooden spar projecting forward from the bow tip.
            float bowZ = stats.length * 0.5f;
            float spritLen = stats.length * 0.18f;
            MakePiece(group, "Bowsprit", s_Quad, s_WoodMat,
                new Vector3(0f, 0.10f, bowZ + (spritLen * 0.5f)),
                new Vector3(stats.beam * 0.07f, 1f, spritLen));

            // Figurehead: a small gilded disk at the very bow.
            MakePiece(group, "Figurehead", s_Disk, s_GoldMat,
                new Vector3(0f, 0.105f, bowZ + (spritLen * 0.05f)), Diam(stats.beam * 0.12f));
        }

        // ---- Cannons -----------------------------------------------------------------------

        private void BuildCannons(ShipStats stats)
        {
            var guns = new GameObject("Guns").transform;
            guns.SetParent(transform, false);

            float barrelLen = stats.beam * 0.30f;
            float barrelWid = stats.beam * 0.085f;
            float portSize = stats.beam * 0.13f;

            int n = Mathf.Clamp(stats.gunsPerBroadside, 0, 8);
            for (int i = 0; i < n; i++)
            {
                float u = Mathf.Lerp(0.30f, 0.74f, n == 1 ? 0.5f : i / (n - 1f));
                for (int s = 0; s < 2; s++)
                {
                    float side = s == 0 ? 1f : -1f;
                    Vector3 edge = MeshUtil.HullEdgePoint(stats.length, stats.beam, u, side);
                    // Gunport square on the gunwale, then a barrel poking outboard from it.
                    MakePiece(guns, "Gunport", s_Quad, s_IronMat,
                        new Vector3(edge.x * 0.96f, 0.072f, edge.z), new Vector3(portSize, 1f, portSize));
                    MakePiece(guns, "Barrel", s_Quad, s_IronMat,
                        new Vector3(edge.x + (side * barrelLen * 0.35f), 0.095f, edge.z),
                        new Vector3(barrelLen, 1f, barrelWid));
                }
            }

            // Chase guns fire along the keel (bow forward, stern aft); barrels run in Z.
            float chaseLen = stats.beam * 0.34f;
            float chaseWid = stats.beam * 0.10f;
            if (stats.chaseGuns > 0)
            {
                MakePiece(guns, "BowChase", s_Quad, s_IronMat, new Vector3(0f, 0.095f, stats.length * 0.44f), new Vector3(chaseWid, 1f, chaseLen));
            }

            if (stats.chaseGuns > 1)
            {
                MakePiece(guns, "SternChase", s_Quad, s_IronMat, new Vector3(0f, 0.095f, -stats.length * 0.46f), new Vector3(chaseWid, 1f, chaseLen));
            }
        }

        // ---- Rigging -----------------------------------------------------------------------

        private void BuildRigging(ShipStats stats)
        {
            var rig = new GameObject("Rigging").transform;
            rig.SetParent(transform, false);

            float bowZ = stats.length * 0.52f;
            float sternZ = -stats.length * 0.5f;
            float rope = Mathf.Max(0.4f, stats.beam * 0.025f);

            for (int i = 0; i < m_MastZ.Length; i++)
            {
                Vector3 baseAt = new Vector3(0f, 0.12f, m_MastZ[i]);

                // Fore-and-aft stays along the centreline.
                if (i == 0)
                {
                    MakeLine(rig, s_RopeMat, baseAt, new Vector3(0f, 0.12f, bowZ), rope);
                }

                if (i == m_MastZ.Length - 1)
                {
                    MakeLine(rig, s_RopeMat, baseAt, new Vector3(0f, 0.12f, sternZ), rope);
                }

                // Shrouds out to the gunwale on each side.
                float u = Mathf.InverseLerp(-stats.length * 0.5f, stats.length * 0.5f, m_MastZ[i]);
                Vector3 starboard = MeshUtil.HullEdgePoint(stats.length, stats.beam, Mathf.Clamp01(u - 0.02f), 1f);
                Vector3 port = MeshUtil.HullEdgePoint(stats.length, stats.beam, Mathf.Clamp01(u - 0.02f), -1f);
                MakeLine(rig, s_RopeMat, baseAt, new Vector3(starboard.x, 0.12f, starboard.z), rope);
                MakeLine(rig, s_RopeMat, baseAt, new Vector3(port.x, 0.12f, port.z), rope);
            }
        }

        // ---- Sails -------------------------------------------------------------------------

        private void BuildSails(ShipStats stats)
        {
            m_SailGroup = new GameObject("Sails").transform;
            m_SailGroup.SetParent(transform, false);

            int count = m_MastZ.Length;
            m_Sails = new MeshRenderer[count];
            m_SailWidth = new float[count];
            m_SailDepth = new float[count];

            for (int i = 0; i < count; i++)
            {
                // A yard (spar) behind a wide translucent canvas spread across the beam.
                var sailHolder = new GameObject("Sail" + i);
                sailHolder.transform.SetParent(m_SailGroup, false);
                sailHolder.transform.localPosition = new Vector3(0f, 0.5f, m_MastZ[i]);

                float w = stats.beam * 2.0f;
                float d = stats.length * 0.16f;
                m_SailWidth[i] = w;
                m_SailDepth[i] = d;

                MakePiece(sailHolder.transform, "Yard", s_Quad, s_WoodMat, new Vector3(0f, -0.02f, d * 0.5f), new Vector3(w, 1f, stats.beam * 0.06f));

                var canvas = MakePiece(sailHolder.transform, "Canvas", s_Quad, s_SailMat, Vector3.zero, new Vector3(w, 1f, d));
                m_Sails[i] = canvas.GetComponent<MeshRenderer>();
            }
        }

        private void BuildFlag(ShipStats stats)
        {
            var flag = new GameObject("Flag");
            flag.transform.SetParent(transform, false);
            flag.transform.localPosition = new Vector3(0f, 0.7f, -stats.length * 0.46f);
            flag.transform.localScale = new Vector3(stats.beam * 0.9f, 1f, stats.beam * 0.45f);
            flag.AddComponent<MeshFilter>().sharedMesh = s_Quad;
            m_FlagRenderer = flag.AddComponent<MeshRenderer>();
            m_FlagMaterial = MaterialUtil.Unlit(Color.white);
            m_FlagRenderer.sharedMaterial = m_FlagMaterial;
            m_Flag = flag.transform;
        }

        private void BuildSelectionRing(ShipStats stats)
        {
            var ring = new GameObject("Selection");
            ring.transform.SetParent(transform, false);
            ring.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            float r = Mathf.Max(stats.length, stats.beam) * 0.7f;
            ring.AddComponent<MeshFilter>().sharedMesh = MeshUtil.Ring(r, r + (0.6f * GameConfig.ShipScale));
            var mr = ring.AddComponent<MeshRenderer>();
            m_SelectionMaterial = MaterialUtil.Unlit(Color.white);
            mr.sharedMaterial = m_SelectionMaterial;
            m_SelectionRing = ring.transform;
        }

        /// <summary>
        /// Builds the per-ship status rings drawn flat on the sea, just outside the hull (inside the
        /// selection ring). One thin ring whose COLOUR is the loaded ammo, a 10-tick speed gauge that
        /// fills with speed, and a gold heading needle showing the ordered course. All elements are
        /// children of the (rotating) hull, so the speed gauge frames the boat and the needle is
        /// offset to the ordered heading relative to the bow.
        /// </summary>
        private void BuildStatusRings(ShipStats stats)
        {
            ShipClass cls = stats.shipClass;
            float len = stats.length;
            float rAmmo = len * 0.56f;          // just beyond the bow/stern tips
            float ammoBand = len * 0.028f;
            float rSpeed = len * 0.65f;

            m_StatusRingRoot = new GameObject("StatusRings").transform;
            m_StatusRingRoot.SetParent(transform, false);
            m_StatusRingRoot.localPosition = new Vector3(0f, 0.035f, 0f);

            // Ammo ring (whole ring tinted by ammo type).
            var ammo = new GameObject("AmmoRing");
            ammo.transform.SetParent(m_StatusRingRoot, false);
            ammo.AddComponent<MeshFilter>().sharedMesh = ClassMesh(s_AmmoRingMesh, cls, () => MeshUtil.Ring(rAmmo, rAmmo + ammoBand));
            var ar = ammo.AddComponent<MeshRenderer>();
            m_AmmoMat = MaterialUtil.Unlit(Color.white);
            ar.sharedMaterial = m_AmmoMat;

            // Speed gauge: radial ticks over a 324° arc (a 36° gap at the bow is left for the needle).
            m_SpeedOnMat = MaterialUtil.Unlit(Color.white);
            m_SpeedOffMat = MaterialUtil.Unlit(Color.white);
            m_SpeedTicks = new MeshRenderer[kSpeedTicks];
            float tickTangential = len * 0.020f;
            float tickRadial = len * 0.05f;
            const float gapHalf = 18f;
            float span = 360f - (2f * gapHalf);
            for (int i = 0; i < kSpeedTicks; i++)
            {
                float aDeg = gapHalf + (span * (i / (kSpeedTicks - 1f)));   // clockwise from the bow
                Vector3 dir = Nav.HeadingToVector(aDeg);
                var tick = new GameObject("SpeedTick" + i);
                tick.transform.SetParent(m_StatusRingRoot, false);
                tick.transform.localPosition = dir * rSpeed;
                tick.transform.localRotation = Quaternion.Euler(0f, aDeg, 0f); // long axis points radially
                tick.transform.localScale = new Vector3(tickTangential, 1f, tickRadial);
                tick.AddComponent<MeshFilter>().sharedMesh = s_Quad;
                m_SpeedTicks[i] = tick.AddComponent<MeshRenderer>();
                m_SpeedTicks[i].sharedMaterial = m_SpeedOffMat;
            }

            // Course needle: a long radial pointer that rotates to the ordered heading.
            m_CoursePivot = new GameObject("CoursePivot").transform;
            m_CoursePivot.SetParent(m_StatusRingRoot, false);
            m_CourseMat = MaterialUtil.Unlit(Color.white);
            var needle = new GameObject("CourseNeedle");
            needle.transform.SetParent(m_CoursePivot, false);
            needle.transform.localPosition = new Vector3(0f, 0f, rAmmo + (len * 0.05f));
            needle.transform.localScale = new Vector3(len * 0.035f, 1f, len * 0.11f);
            needle.AddComponent<MeshFilter>().sharedMesh = s_Quad;
            needle.AddComponent<MeshRenderer>().sharedMaterial = m_CourseMat;

            ApplyStatusColors();
        }

        // Fixed colour key for the loaded ammunition (documented, distinct from the faction colours).
        private static Color AmmoColor(AmmoType ammo)
        {
            switch (ammo)
            {
                case AmmoType.RoundShot: return new Color(0.82f, 0.82f, 0.85f); // steel grey – hull-smashing ball
                case AmmoType.BarShot: return new Color(0.32f, 0.80f, 0.46f);   // green – shreds rigging
                case AmmoType.GrapeShot: return new Color(0.88f, 0.26f, 0.26f); // crimson – scythes crew
                default: return Color.gray;
            }
        }

        private static Color Dim(Color c, float f)
        {
            return new Color(c.r * f, c.g * f, c.b * f, 1f);
        }

        private void ApplyStatusColors()
        {
            if (m_AmmoMat != null)
            {
                m_AmmoMat.color = Dim(AmmoColor(m_Ship.Ammo), m_StatusBright);
            }

            if (m_CourseMat != null)
            {
                m_CourseMat.color = Dim(new Color(1f, 0.84f, 0.40f), Mathf.Max(0.7f, m_StatusBright)); // gold
            }

            if (m_SpeedOnMat != null)
            {
                m_SpeedOnMat.color = Dim(new Color(0.55f, 0.85f, 1f), Mathf.Max(0.75f, m_StatusBright)); // cyan
            }

            if (m_SpeedOffMat != null)
            {
                m_SpeedOffMat.color = new Color(0.18f, 0.22f, 0.28f, 1f); // always faint
            }
        }

        // ---- On-ring control buttons -------------------------------------------------------

        private void BuildControlButtons(ShipStats stats)
        {
            m_ControlGroup = new GameObject("Controls").transform;
            m_ControlGroup.SetParent(transform, false);
            m_ControlGroup.localPosition = new Vector3(0f, 0.045f, 0f);
            m_ControlGroup.gameObject.SetActive(false); // only while selected

            m_ControlButtons = new List<ControlButton>(5);
            float len = stats.length;
            float r = len * 0.92f;      // outside the status + selection rings
            float btnR = len * 0.16f;   // large, touch-friendly disc
            m_ButtonHitRadius = btnR * 1.15f;

            // Layout around the (ship-relative) ring: turns on the sides, sail fore, ammo aft.
            AddControl(ShipControl.Starboard, 90f, r, btnR, new Color(0.28f, 0.40f, 0.54f));
            AddControl(ShipControl.Port, 270f, r, btnR, new Color(0.28f, 0.40f, 0.54f));
            AddControl(ShipControl.SailUp, 28f, r, btnR, new Color(0.22f, 0.46f, 0.30f));
            AddControl(ShipControl.SailDown, 332f, r, btnR, new Color(0.44f, 0.34f, 0.22f));
            AddControl(ShipControl.AmmoCycle, 180f, r, btnR, new Color(0.26f, 0.24f, 0.32f));
        }

        private void AddControl(ShipControl type, float angleDeg, float r, float btnR, Color baseColor)
        {
            var go = new GameObject("Ctrl_" + type);
            go.transform.SetParent(m_ControlGroup, false);
            go.transform.localPosition = Nav.HeadingToVector(angleDeg) * r;

            Material mat = MaterialUtil.Unlit(baseColor);
            MakePiece(go.transform, "disc", s_Disk, mat, Vector3.zero, Diam(btnR * 2f));
            AddControlIcon(type, go.transform, btnR);

            m_ControlButtons.Add(new ControlButton { type = type, tf = go.transform, mat = mat, baseColor = baseColor, flash = 0f });
        }

        private void AddControlIcon(ShipControl type, Transform parent, float btnR)
        {
            const float y = 0.004f;
            switch (type)
            {
                case ShipControl.Starboard: // arrow pointing to the ship's right
                    MakeIcon(parent, s_Triangle, s_IconMat, y, 90f, new Vector3(btnR * 0.95f, 1f, btnR * 0.95f));
                    break;
                case ShipControl.Port: // arrow pointing to the ship's left
                    MakeIcon(parent, s_Triangle, s_IconMat, y, 270f, new Vector3(btnR * 0.95f, 1f, btnR * 0.95f));
                    break;
                case ShipControl.SailUp: // "+"
                    MakeIcon(parent, s_Quad, s_IconMat, y, 0f, new Vector3(btnR * 1.1f, 1f, btnR * 0.30f));
                    MakeIcon(parent, s_Quad, s_IconMat, y, 0f, new Vector3(btnR * 0.30f, 1f, btnR * 1.1f));
                    break;
                case ShipControl.SailDown: // "−"
                    MakeIcon(parent, s_Quad, s_IconMat, y, 0f, new Vector3(btnR * 1.1f, 1f, btnR * 0.30f));
                    break;
                case ShipControl.AmmoCycle: // disc tinted by the loaded shot (updates on cycle)
                    m_AmmoIconMat = MaterialUtil.Unlit(AmmoColor(m_Ship.Ammo));
                    MakeIcon(parent, s_Disk, m_AmmoIconMat, y, 0f, Diam(btnR * 0.9f));
                    break;
            }
        }

        private static void MakeIcon(Transform parent, Mesh mesh, Material mat, float y, float yaw, Vector3 scale)
        {
            var go = new GameObject("icon");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, y, 0f);
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        }

        /// <summary>
        /// Tests a world-sea point against this ship's active control buttons, returning the nearest
        /// one hit (or <see cref="ShipControl.None"/>). Used by the input layer before select/deselect.
        /// </summary>
        /// <param name="worldPoint">Tapped point on the sea plane.</param>
        /// <param name="control">The hit control, if any.</param>
        /// <returns><c>true</c> if a control button was hit.</returns>
        public bool TryHitControl(Vector3 worldPoint, out ShipControl control)
        {
            control = ShipControl.None;
            if (m_ControlButtons == null || m_ControlGroup == null || !m_ControlGroup.gameObject.activeSelf)
            {
                return false;
            }

            float best = m_ButtonHitRadius;
            for (int i = 0; i < m_ControlButtons.Count; i++)
            {
                ControlButton b = m_ControlButtons[i];
                float d = Vector3.Distance(worldPoint, b.tf.position);
                if (d <= best)
                {
                    best = d;
                    control = b.type;
                }
            }

            return control != ShipControl.None;
        }

        /// <summary>Briefly highlights a control button to acknowledge a press.</summary>
        /// <param name="control">The pressed control.</param>
        public void FlashControl(ShipControl control)
        {
            if (m_ControlButtons == null)
            {
                return;
            }

            for (int i = 0; i < m_ControlButtons.Count; i++)
            {
                if (m_ControlButtons[i].type == control)
                {
                    m_ControlButtons[i].flash = 0.18f;
                    m_ControlButtons[i].mat.color = Color.white;
                    break;
                }
            }
        }

        /// <summary>Shows or hides this ship's on-ring control buttons (shown only when selected).</summary>
        /// <param name="visible">Whether the buttons should be visible.</param>
        public void SetControlsVisible(bool visible)
        {
            if (m_ControlGroup != null)
            {
                m_ControlGroup.gameObject.SetActive(visible);
            }
        }

        private void UpdateControlButtons(float dt)
        {
            if (m_ControlButtons == null || m_ControlGroup == null || !m_ControlGroup.gameObject.activeSelf)
            {
                return;
            }

            for (int i = 0; i < m_ControlButtons.Count; i++)
            {
                ControlButton b = m_ControlButtons[i];
                if (b.flash > 0f)
                {
                    b.flash -= dt;
                    b.mat.color = Color.Lerp(b.baseColor, Color.white, Mathf.Clamp01(b.flash / 0.18f));
                }
            }
        }

        private void BuildStatusBars(ShipStats stats)
        {
            // Status bars stay world-axis-aligned (north-up) so they read clearly from any seat
            // around the table, regardless of the hull's heading. Width and thickness scale with the
            // (large) ships so the bars are easy to read.
            m_StatusGroup = new GameObject("Status").transform;
            m_StatusGroup.SetParent(transform, false);
            float lift = Mathf.Max(stats.length, stats.beam) * 0.80f;
            m_StatusGroup.localPosition = new Vector3(0f, 0.1f, lift);

            m_BarWidth = stats.length * 1.5f;     // was ≈ stats.length (×1.0) → now 1.5× wider
            float thick = stats.length * 0.07f;   // was a flat 0.45 world units (near-invisible at scale)
            float gap = stats.length * 0.095f;    // vertical spacing between the three bars
            m_HullBar = MakeBar("HullBar", new Color(0.85f, 0.25f, 0.2f), m_BarWidth, thick, 0f);
            m_RiggingBar = MakeBar("RiggingBar", new Color(0.35f, 0.8f, 0.4f), m_BarWidth, thick, gap);
            m_CrewBar = MakeBar("CrewBar", new Color(0.9f, 0.8f, 0.3f), m_BarWidth, thick, gap * 2f);
        }

        private Transform MakeBar(string label, Color color, float width, float thickness, float zOffset)
        {
            var holder = new GameObject(label);
            holder.transform.SetParent(m_StatusGroup, false);
            holder.transform.localPosition = new Vector3(0f, 0f, zOffset);

            var bg = new GameObject("bg");
            bg.transform.SetParent(holder.transform, false);
            bg.transform.localScale = new Vector3(width, 1f, thickness);
            bg.AddComponent<MeshFilter>().sharedMesh = s_Quad;
            bg.AddComponent<MeshRenderer>().sharedMaterial = MaterialUtil.Unlit(new Color(0.05f, 0.05f, 0.07f, 1f));

            var fill = new GameObject("fill");
            fill.transform.SetParent(holder.transform, false);
            fill.transform.localPosition = new Vector3(0f, 0.01f, 0f);
            fill.transform.localScale = new Vector3(width, 1f, thickness * 0.88f);
            fill.AddComponent<MeshFilter>().sharedMesh = s_Quad;
            fill.AddComponent<MeshRenderer>().sharedMaterial = MaterialUtil.Unlit(color);

            return fill.transform;
        }

        private void BuildSmoke()
        {
            var smokeGo = new GameObject("Smoke");
            smokeGo.transform.SetParent(transform, false);
            m_Smoke = smokeGo.AddComponent<ParticleSystem>();

            float s = GameConfig.ShipScale;
            var main = m_Smoke.main;
            main.loop = true;
            main.startLifetime = 1.6f;
            main.startSpeed = 1.2f * s;
            main.startSize = 1.5f * s;
            main.startColor = new Color(0.7f, 0.7f, 0.7f, 0.6f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 200;

            var emission = m_Smoke.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;

            var shape = m_Smoke.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.4f * s;

            var renderer = smokeGo.GetComponent<ParticleSystemRenderer>();
            renderer.material = MaterialUtil.Unlit(new Color(0.8f, 0.8f, 0.8f, 0.5f));
            renderer.sortingOrder = 5;
        }

        // ---- Helpers -----------------------------------------------------------------------

        private static GameObject MakePiece(Transform parent, string name, Mesh mesh, Material mat, Vector3 pos, Vector3 scale)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        private static void MakeLine(Transform parent, Material mat, Vector3 from, Vector3 to, float width)
        {
            Vector3 d = to - from;
            d.y = 0f;
            float len = d.magnitude;
            if (len < 0.001f)
            {
                return;
            }

            var go = new GameObject("Line");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = (from + to) * 0.5f;
            go.transform.localRotation = Quaternion.Euler(0f, Nav.VectorToHeading(d), 0f);
            go.transform.localScale = new Vector3(width, 1f, len);
            go.AddComponent<MeshFilter>().sharedMesh = s_Quad;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        }

        private static Vector3 Diam(float diameter)
        {
            // The unit disk has radius 0.5, so scaling by the diameter yields the requested radius.
            return new Vector3(diameter, 1f, diameter);
        }

        // ---- Live updates ------------------------------------------------------------------

        /// <summary>Updates colours when the ship changes allegiance (capture).</summary>
        public void OnFactionChanged()
        {
            Faction f = m_Ship.Faction;

            // The hull stays wood; faction colour lives on the gun stripe and the flag.
            if (m_StripeMaterial != null)
            {
                m_StripeMaterial.color = f.AccentColor();
            }

            if (m_HullMaterial != null)
            {
                m_HullMaterial.color = m_HullBaseColor;
            }

            if (m_FlagMaterial != null)
            {
                m_FlagMaterial.color = f.AccentColor();
            }
        }

        /// <summary>Shows or hides the selection ring and tints it for the selecting side.</summary>
        /// <param name="selected">Whether the ship is selected.</param>
        /// <param name="selector">The faction that selected it (for tint).</param>
        public void SetSelected(bool selected, Faction selector)
        {
            if (m_SelectionRing == null)
            {
                return;
            }

            m_SelectionRing.gameObject.SetActive(selected);
            if (selected && m_SelectionMaterial != null)
            {
                m_SelectionMaterial.color = selector.AccentColor();
            }

            // Brighten this ship's status rings while selected; keep them subtle otherwise.
            if (selected != m_LastSelected)
            {
                m_LastSelected = selected;
                m_StatusBright = selected ? 1f : 0.55f;
                ApplyStatusColors();
            }

            // Control buttons appear only for the selected ship.
            SetControlsVisible(selected);
        }

        private void UpdateStatusRings(Ship ship)
        {
            if (m_StatusRingRoot == null)
            {
                return;
            }

            // Course needle points to the ORDERED heading (offset from the current hull heading), so
            // it swings when a new course is set and lines up with the bow as the ship finishes turning.
            if (m_CoursePivot != null)
            {
                m_CoursePivot.localEulerAngles = new Vector3(0f, Nav.SignedDelta(ship.HeadingDeg, ship.TargetHeadingDeg), 0f);
            }

            // Ammo ring: recolour only when the loaded shot actually changes.
            if (ship.Ammo != m_LastAmmo)
            {
                m_LastAmmo = ship.Ammo;
                if (m_AmmoMat != null)
                {
                    m_AmmoMat.color = Dim(AmmoColor(ship.Ammo), m_StatusBright);
                }

                if (m_AmmoIconMat != null)
                {
                    m_AmmoIconMat.color = AmmoColor(ship.Ammo); // ammo button glyph tracks the shot
                }
            }

            // Speed gauge: number of lit ticks tracks the current speed; reassign only on change.
            float frac = ship.Stats.topSpeed > 0.01f ? Mathf.Clamp01(ship.Speed / ship.Stats.topSpeed) : 0f;
            int lit = Mathf.RoundToInt(frac * kSpeedTicks);
            if (lit != m_LastSpeedLit && m_SpeedTicks != null)
            {
                m_LastSpeedLit = lit;
                for (int i = 0; i < m_SpeedTicks.Length; i++)
                {
                    if (m_SpeedTicks[i] != null)
                    {
                        m_SpeedTicks[i].sharedMaterial = i < lit ? m_SpeedOnMat : m_SpeedOffMat;
                    }
                }
            }
        }

        /// <summary>Briefly flashes the hull white to acknowledge a hit.</summary>
        public void FlashHit()
        {
            m_HitFlash = 0.15f;
        }

        /// <summary>Emits a puff of powder smoke from the firing broadside.</summary>
        /// <param name="side">Which broadside fired.</param>
        public void PlayBroadsideSmoke(BroadsideSide side)
        {
            if (m_Smoke == null)
            {
                return;
            }

            float s = GameConfig.ShipScale;
            Vector3 normal = m_Ship.BroadsideNormal(side);
            Vector3 origin = transform.position + (normal * (m_Ship.Stats.beam * 0.6f)) + (Vector3.up * (0.4f * s));
            var ep = new ParticleSystem.EmitParams
            {
                position = origin,
                velocity = (normal * (2.5f * s)) + (Vector3.up * (0.5f * s)),
            };
            m_Smoke.Emit(ep, 14);
        }

        /// <summary>Per-frame visual update.</summary>
        /// <param name="ship">The owning ship.</param>
        /// <param name="wind">The global wind.</param>
        /// <param name="dt">Delta time in seconds.</param>
        public void UpdateVisuals(Ship ship, Wind wind, float dt)
        {
            UpdateSails(ship, wind);
            UpdateStatusRings(ship);
            UpdateControlButtons(dt);

            // Keep status bars north-up and update fills.
            if (m_StatusGroup != null)
            {
                m_StatusGroup.rotation = Quaternion.identity;
                UpdateBar(m_HullBar, ship.HullFraction, m_BarWidth, 4f);
                UpdateBar(m_RiggingBar, ship.RiggingFraction, m_BarWidth, 4f);
                UpdateBar(m_CrewBar, ship.CrewFraction, m_BarWidth, 4f);
            }

            // Continuous damage smoke once the hull is hurt.
            if (m_Smoke != null)
            {
                var emission = m_Smoke.emission;
                float dmg = 1f - ship.HullFraction;
                emission.rateOverTime = dmg > 0.4f ? Mathf.Lerp(0f, 30f, dmg) : 0f;
            }

            // Hit flash decay.
            if (m_HitFlash > 0f)
            {
                m_HitFlash -= dt;
                if (m_HullMaterial != null)
                {
                    m_HullMaterial.color = Color.Lerp(m_HullBaseColor, Color.white, Mathf.Clamp01(m_HitFlash / 0.15f));
                }
            }
        }

        private void UpdateSails(Ship ship, Wind wind)
        {
            if (m_Sails == null || m_SailGroup == null)
            {
                return;
            }

            // Sail "set": full when driving hard under full sail with intact rigging, reefed (shrunk)
            // as the ship slows, furls sail, or loses rigging — but never fully gone, so the canvas
            // stays visible.
            float throttle = ship.Sail.ThrottleFactor();
            float speedFrac = ship.Stats.topSpeed > 0.01f ? Mathf.Clamp01(ship.Speed / ship.Stats.topSpeed) : 0f;
            float rig = ship.RiggingFraction;
            float set = Mathf.Clamp(0.2f + (0.8f * throttle * (0.35f + (0.65f * speedFrac)) * rig), 0.14f, 1f);

            for (int i = 0; i < m_Sails.Length; i++)
            {
                if (m_Sails[i] == null)
                {
                    continue;
                }

                Transform t = m_Sails[i].transform;
                Vector3 s = t.localScale;
                s.x = m_SailWidth[i] * Mathf.Lerp(0.5f, 1f, set); // reef narrows the spread
                s.z = m_SailDepth[i] * set;                       // and shortens the drop
                t.localScale = s;
            }

            // Yards swing to present the canvas to the wind (subtle, clamped).
            float blowing = Nav.Normalize360(wind.FromDegrees + 180f);
            float swing = Mathf.Clamp(Nav.Normalize180(blowing - ship.HeadingDeg) * 0.18f, -28f, 28f);
            m_SailGroup.localRotation = Quaternion.Euler(0f, swing, 0f);
        }

        /// <summary>Updates the visuals while a ship is sinking.</summary>
        /// <param name="t">Sink progress in [0, 1].</param>
        public void UpdateSinking(float t)
        {
            SetSelected(false, Faction.Neutral);
            if (m_StatusGroup != null)
            {
                m_StatusGroup.gameObject.SetActive(false);
            }

            if (m_StatusRingRoot != null)
            {
                m_StatusRingRoot.gameObject.SetActive(false);
            }

            SetControlsVisible(false);

            if (m_Smoke != null)
            {
                var emission = m_Smoke.emission;
                emission.rateOverTime = Mathf.Lerp(40f, 0f, t);
            }
        }

        private static void UpdateBar(Transform fill, float fraction, float width, float minWidth)
        {
            if (fill == null)
            {
                return;
            }

            float w = Mathf.Max(width, minWidth);
            fraction = Mathf.Clamp01(fraction);
            Vector3 s = fill.localScale;
            s.x = w * fraction;
            fill.localScale = s;

            // Anchor the bar's left edge by shifting the (centre-pivoted) quad left as it shrinks.
            Vector3 p = fill.localPosition;
            p.x = -(w * (1f - fraction)) * 0.5f;
            fill.localPosition = p;
        }
    }
}
