using System.Collections.Generic;
using UnityEngine;

namespace StellarisMini
{
    /// Affichage de la carte galactique : étoiles, hyperlignes, territoires et flottes.
    public class GalaxyView
    {
        public readonly GameObject root;
        readonly GameManager gm;
        readonly Galaxy g;
        readonly GalaxySim sim;

        class SysVis
        {
            public StarSystem s;
            public SpriteRenderer glow, core, territory, marker;
        }

        class LaneVis
        {
            public StarSystem a, b;
            public LineRenderer lr;
        }

        readonly List<SysVis> systems = new List<SysVis>();
        readonly List<LaneVis> lanes = new List<LaneVis>();
        readonly List<MeshRenderer> fleetIcons = new List<MeshRenderer>();
        readonly Transform fleetRoot;
        readonly SpriteRenderer selRing, hoverRing;
        readonly LineRenderer pathLine;

        /// Positions d'affichage des flottes visibles (pour la sélection et les étiquettes).
        public readonly List<KeyValuePair<Fleet, Vector2>> fleetDraw = new List<KeyValuePair<Fleet, Vector2>>();

        public Vector2 camPos;
        public float camDist = 75f;
        public const float MinDist = 25f;
        public float MaxDist { get { return Mathf.Max(90f, g.radius * 2.6f); } }
        public float IconScale { get { return camDist * 0.017f; } }

        public GalaxyView(GameManager manager, Galaxy galaxy, GalaxySim simulation)
        {
            gm = manager;
            g = galaxy;
            sim = simulation;
            root = new GameObject("Galaxy View");
            root.transform.SetParent(gm.transform, false);

            Gfx.Starfield(root.transform, 3000, g.radius + 20f, 30f, 320f, g.settings.seed);
            Gfx.Nebulae(root.transform, 10, g.radius * 1.6f, 260f, g.settings.seed + 1);

            // Disque galactique diffus
            var disk = Gfx.MakeSpriteObj("Galactic Disk", root.transform, Gfx.GlowSprite, new Color(0.25f, 0.3f, 0.55f, 0.25f), g.radius * 3.2f, -60, true);
            disk.transform.localPosition = new Vector3(0, 0, 40f);

            // Hyperlignes
            var seen = new HashSet<long>();
            foreach (var s in g.systems)
            {
                foreach (var t in s.lanes)
                {
                    long key = s.id < t.id ? s.id * 100000L + t.id : t.id * 100000L + s.id;
                    if (!seen.Add(key)) continue;
                    var lr = Gfx.MakeLine("Lane", root.transform, new Color(0.5f, 0.6f, 0.8f, 0.3f), 0.18f, -5);
                    lr.SetPosition(0, new Vector3(s.pos.x, s.pos.y, 0.1f));
                    lr.SetPosition(1, new Vector3(t.pos.x, t.pos.y, 0.1f));
                    lanes.Add(new LaneVis { a = s, b = t, lr = lr });
                }
            }

            // Systèmes
            foreach (var s in g.systems)
            {
                var v = new SysVis { s = s };
                var holder = new GameObject(s.name).transform;
                holder.SetParent(root.transform, false);
                holder.localPosition = new Vector3(s.pos.x, s.pos.y, 0);

                v.territory = Gfx.MakeSpriteObj("Territory", holder, Gfx.GlowSprite, Color.clear, 17f, -20, false);
                v.territory.transform.localPosition = new Vector3(0, 0, 0.3f);
                float size = s.star == StarClass.Bleue ? 5.5f : s.star == StarClass.Rouge ? 3.4f : 4.3f;
                v.glow = Gfx.MakeSpriteObj("Glow", holder, Gfx.GlowSprite, Palette.Star(s.star), size, 0, true);
                v.core = Gfx.MakeSpriteObj("Core", holder, Gfx.DiscSprite, Color.white, size * 0.16f, 2, false);
                v.marker = Gfx.MakeSpriteObj("Marker", holder, Gfx.RingSprite, Color.clear, 3.2f, 1, false);
                systems.Add(v);
            }

            fleetRoot = new GameObject("Fleets").transform;
            fleetRoot.SetParent(root.transform, false);

            selRing = Gfx.MakeSpriteObj("Selection", root.transform, Gfx.RingSprite, new Color(1f, 0.9f, 0.4f, 0.9f), 4f, 10, true);
            hoverRing = Gfx.MakeSpriteObj("Hover", root.transform, Gfx.RingSprite, new Color(0.7f, 0.9f, 1f, 0.5f), 4f, 9, true);
            pathLine = Gfx.MakeLine("Path", root.transform, new Color(0.5f, 1f, 0.6f, 0.8f), 0.3f, 8);
            pathLine.enabled = false;

            var home = g.player.capital;
            if (home != null) camPos = home.pos;
        }

        public void SetVisible(bool v) { root.SetActive(v); }

        public void Dispose()
        {
            if (root != null) Object.Destroy(root);
        }

        // ==================================================================
        public void Update(StarSystem selSystem, Fleet selFleet, StarSystem hoverSystem, Fleet hoverFleet)
        {
            var player = g.player;
            float t = Time.unscaledTime;

            foreach (var v in systems)
            {
                var s = v.s;
                bool explored = player.explored.Contains(s);
                var starCol = Palette.Star(s.star);
                v.glow.color = explored ? starCol : new Color(starCol.r * 0.4f, starCol.g * 0.4f, starCol.b * 0.45f, 0.6f);
                v.core.color = explored ? Color.Lerp(starCol, Color.white, 0.6f) : new Color(0.5f, 0.5f, 0.55f, 0.7f);

                if (explored && s.owner != null)
                {
                    var c = s.owner.color;
                    float pulse = s.occupier != null ? 0.12f + 0.08f * Mathf.Sin(t * 6f) : 0.2f;
                    v.territory.color = new Color(c.r, c.g, c.b, pulse);
                    float ring = s.colony != null ? (s.colony.capital ? 3.6f : 3.0f) : 2.2f;
                    v.marker.transform.localScale = new Vector3(ring, ring, 1);
                    v.marker.color = new Color(c.r, c.g, c.b, s.colony != null ? 0.95f : 0.55f);
                }
                else
                {
                    v.territory.color = Color.clear;
                    v.marker.color = Color.clear;
                }
            }

            foreach (var l in lanes)
            {
                bool ea = player.explored.Contains(l.a), eb = player.explored.Contains(l.b);
                Color c;
                if (ea && eb && l.a.owner != null && l.a.owner == l.b.owner)
                {
                    c = l.a.owner.color;
                    c.a = 0.55f;
                }
                else if (ea && eb) c = new Color(0.5f, 0.6f, 0.8f, 0.35f);
                else c = new Color(0.4f, 0.45f, 0.6f, 0.12f);
                l.lr.startColor = c;
                l.lr.endColor = c;
            }

            UpdateFleets(selFleet);

            // Anneaux de sélection et de survol
            float icon = IconScale;
            Vector2? selPos = null;
            float selSize = 4f;
            Vector2 fp, hp;
            if (selFleet != null && TryGetFleetPos(selFleet, out fp)) { selPos = fp; selSize = icon * 2.2f; }
            else if (selSystem != null) { selPos = selSystem.pos; selSize = 4.2f; }
            selRing.enabled = selPos.HasValue;
            if (selPos.HasValue)
            {
                selRing.transform.localPosition = new Vector3(selPos.Value.x, selPos.Value.y, -0.2f);
                float pulse = 1f + 0.06f * Mathf.Sin(t * 5f);
                selRing.transform.localScale = new Vector3(selSize * pulse, selSize * pulse, 1);
            }

            Vector2? hovPos = null;
            float hovSize = 4f;
            if (hoverFleet != null && hoverFleet != selFleet && TryGetFleetPos(hoverFleet, out hp)) { hovPos = hp; hovSize = icon * 2f; }
            else if (hoverSystem != null && hoverSystem != selSystem) { hovPos = hoverSystem.pos; hovSize = 3.8f; }
            hoverRing.enabled = hovPos.HasValue;
            if (hovPos.HasValue)
            {
                hoverRing.transform.localPosition = new Vector3(hovPos.Value.x, hovPos.Value.y, -0.2f);
                hoverRing.transform.localScale = new Vector3(hovSize, hovSize, 1);
            }

            // Trajet de la flotte sélectionnée
            if (selFleet != null && selFleet.next != null && sim.IsFleetVisible(selFleet))
            {
                pathLine.enabled = true;
                pathLine.positionCount = 2 + selFleet.path.Count;
                var p0 = selFleet.Position;
                pathLine.SetPosition(0, new Vector3(p0.x, p0.y, -0.1f));
                pathLine.SetPosition(1, new Vector3(selFleet.next.pos.x, selFleet.next.pos.y, -0.1f));
                for (int i = 0; i < selFleet.path.Count; i++)
                    pathLine.SetPosition(2 + i, new Vector3(selFleet.path[i].pos.x, selFleet.path[i].pos.y, -0.1f));
                var pc = selFleet.owner == player ? new Color(0.5f, 1f, 0.6f, 0.8f) : new Color(1f, 0.5f, 0.4f, 0.8f);
                pathLine.startColor = pc;
                pathLine.endColor = pc;
                pathLine.startWidth = pathLine.endWidth = icon * 0.2f;
            }
            else pathLine.enabled = false;

            // Caméra
            gm.Cam.transform.SetPositionAndRotation(new Vector3(camPos.x, camPos.y, -camDist), Quaternion.identity);
        }

        void UpdateFleets(Fleet selFleet)
        {
            fleetDraw.Clear();
            float icon = IconScale;
            int used = 0;
            var slots = new Dictionary<StarSystem, int>();

            foreach (var f in g.fleets)
            {
                if (!sim.IsFleetVisible(f)) continue;
                Vector2 pos;
                float angle;
                if (f.next == null)
                {
                    int k;
                    slots.TryGetValue(f.system, out k);
                    slots[f.system] = k + 1;
                    pos = f.system.pos + new Vector2(1.6f + icon * 0.4f + k * icon * 1.4f, 1.3f + icon * 0.3f);
                    angle = 0f;
                }
                else
                {
                    pos = f.Position;
                    var d = f.next.pos - f.system.pos;
                    angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg - 90f;
                }
                fleetDraw.Add(new KeyValuePair<Fleet, Vector2>(f, pos));

                MeshRenderer mr;
                if (used < fleetIcons.Count) mr = fleetIcons[used];
                else
                {
                    mr = Gfx.MakeMeshObj("Fleet", fleetRoot, Gfx.ChevronMesh(Color.white), 6);
                    fleetIcons.Add(mr);
                }
                used++;
                mr.gameObject.SetActive(true);
                mr.GetComponent<MeshFilter>().sharedMesh = Gfx.ChevronMesh(f.owner.color);
                float sc = icon * (f == selFleet ? 1.15f : 1f) * (0.85f + Mathf.Min(0.5f, f.ships.Count * 0.04f));
                mr.transform.localPosition = new Vector3(pos.x, pos.y, -0.15f);
                mr.transform.localRotation = Quaternion.Euler(0, 0, angle);
                mr.transform.localScale = new Vector3(sc, sc, 1);
            }
            for (int i = used; i < fleetIcons.Count; i++) fleetIcons[i].gameObject.SetActive(false);
        }

        public bool TryGetFleetPos(Fleet f, out Vector2 pos)
        {
            foreach (var kv in fleetDraw)
                if (kv.Key == f) { pos = kv.Value; return true; }
            pos = Vector2.zero;
            return false;
        }

        // ==================================================================
        //  Sélection à l'écran
        // ==================================================================
        public Vector2 WorldToGui(Vector2 world)
        {
            var sp = gm.Cam.WorldToScreenPoint(new Vector3(world.x, world.y, 0));
            return new Vector2(sp.x, Screen.height - sp.y);
        }

        Vector2 WorldToScreen(Vector2 world)
        {
            var sp = gm.Cam.WorldToScreenPoint(new Vector3(world.x, world.y, 0));
            return new Vector2(sp.x, sp.y);
        }

        public Fleet PickFleet(Vector2 screen, float maxPx)
        {
            Fleet best = null;
            float bestD = maxPx * maxPx;
            foreach (var kv in fleetDraw)
            {
                float d = (WorldToScreen(kv.Value) - screen).sqrMagnitude;
                if (d < bestD) { bestD = d; best = kv.Key; }
            }
            return best;
        }

        public StarSystem PickSystem(Vector2 screen, float maxPx)
        {
            StarSystem best = null;
            float bestD = maxPx * maxPx;
            foreach (var s in g.systems)
            {
                float d = (WorldToScreen(s.pos) - screen).sqrMagnitude;
                if (d < bestD) { bestD = d; best = s; }
            }
            return best;
        }

        public void ClampCamera()
        {
            float lim = g.radius + 10f;
            camPos.x = Mathf.Clamp(camPos.x, -lim, lim);
            camPos.y = Mathf.Clamp(camPos.y, -lim, lim);
            camDist = Mathf.Clamp(camDist, MinDist, MaxDist);
        }
    }
}
