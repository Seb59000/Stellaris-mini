using System.Collections.Generic;
using UnityEngine;

namespace StellarisMini
{
    public static class Palette
    {
        public static readonly Color Background = new Color(0.008f, 0.012f, 0.03f);
        public static readonly Color Good = new Color(0.45f, 1f, 0.55f);
        public static readonly Color Bad = new Color(1f, 0.4f, 0.35f);
        public static readonly Color Warning = new Color(1f, 0.8f, 0.3f);
        public static readonly Color Info = new Color(0.6f, 0.85f, 1f);
        public static readonly Color Research = new Color(0.55f, 0.9f, 1f);
        public static readonly Color Energy = new Color(1f, 0.9f, 0.3f);
        public static readonly Color Minerals = new Color(0.95f, 0.45f, 0.35f);
        public static readonly Color Alloys = new Color(0.8f, 0.6f, 1f);
        public static readonly Color ResearchRes = new Color(0.35f, 0.8f, 1f);
        public static readonly Color Neutral = new Color(0.55f, 0.6f, 0.7f);

        public static Color Star(StarClass c)
        {
            switch (c)
            {
                case StarClass.Jaune: return new Color(1f, 0.9f, 0.55f);
                case StarClass.Orange: return new Color(1f, 0.65f, 0.3f);
                case StarClass.Rouge: return new Color(1f, 0.4f, 0.3f);
                case StarClass.Blanche: return new Color(0.95f, 0.95f, 1f);
                default: return new Color(0.55f, 0.75f, 1f);
            }
        }

        public static Color Planet(PlanetType t)
        {
            switch (t)
            {
                case PlanetType.Continentale: return new Color(0.35f, 0.75f, 0.45f);
                case PlanetType.Oceanique: return new Color(0.3f, 0.55f, 0.95f);
                case PlanetType.Desertique: return new Color(0.9f, 0.7f, 0.4f);
                case PlanetType.Arctique: return new Color(0.85f, 0.95f, 1f);
                case PlanetType.Tropicale: return new Color(0.5f, 0.85f, 0.35f);
                case PlanetType.Toxique: return new Color(0.7f, 0.85f, 0.2f);
                case PlanetType.GeanteGazeuse: return new Color(0.85f, 0.6f, 0.45f);
                default: return new Color(0.55f, 0.52f, 0.5f);
            }
        }

        public static string Hex(Color c) { return "#" + ColorUtility.ToHtmlStringRGB(c); }
        public static string Tint(string text, Color c) { return "<color=" + Hex(c) + ">" + text + "</color>"; }

        public static Color WithAlpha(Color c, float a) { c.a = a; return c; }
    }

    /// Ressources graphiques générées par code : matériaux, textures, sprites et maillages.
    /// Tout est "néon vectoriel" : aucun modèle 3D ni image externe n'est nécessaire.
    public static class Gfx
    {
        public static Material SpriteMat;     // Sprites/Default (alpha), toujours disponible
        public static Material AddMat;        // additif (lueurs)
        public static Material GlowMat;       // additif + texture radiale (particules, étoiles de fond)
        public static Texture2D GlowTex, RingTex, DiscTex;
        public static Sprite GlowSprite, RingSprite, DiscSprite, SquareSprite;

        static readonly Dictionary<long, Mesh> meshCache = new Dictionary<long, Mesh>();

        public static void Init()
        {
            if (SpriteMat != null) return;

            // Le matériau par défaut d'un SpriteRenderer est toujours inclus dans les builds.
            var tmp = new GameObject("tmp");
            SpriteMat = tmp.AddComponent<SpriteRenderer>().sharedMaterial;
            Object.Destroy(tmp);

            AddMat = LoadMaterial("StellarisMini/Additive", "Legacy Shaders/Particles/Additive");

            GlowTex = MakeRadial(64);
            RingTex = MakeRing(128, 0.8f, 0.12f);
            DiscTex = MakeDisc(64);
            var white = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var px = new Color[16];
            for (int i = 0; i < 16; i++) px[i] = Color.white;
            white.SetPixels(px);
            white.Apply();

            GlowSprite = MakeSprite(GlowTex);
            RingSprite = MakeSprite(RingTex);
            DiscSprite = MakeSprite(DiscTex);
            SquareSprite = MakeSprite(white);

            GlowMat = new Material(AddMat) { mainTexture = GlowTex };
        }

        static Material LoadMaterial(string resource, string shaderName)
        {
            var m = Resources.Load<Material>(resource);
            if (m != null) return new Material(m);
            var sh = Shader.Find(shaderName);
            if (sh != null) return new Material(sh);
            return new Material(SpriteMat);   // repli : alpha classique
        }

        // ------------------------------------------------------------------
        //  Textures procédurales
        // ------------------------------------------------------------------
        static Texture2D NewTex(int size)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
            t.wrapMode = TextureWrapMode.Clamp;
            t.filterMode = FilterMode.Bilinear;
            return t;
        }

        static Texture2D MakeRadial(int size)
        {
            var t = NewTex(size);
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                    float d = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy));
                    float a = Mathf.Pow(1f - d, 2.2f);
                    px[y * size + x] = new Color(1, 1, 1, a);
                }
            t.SetPixels(px);
            t.Apply();
            return t;
        }

        static Texture2D MakeRing(int size, float radius, float width)
        {
            var t = NewTex(size);
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1f - Mathf.Abs(d - radius) / width);
                    a = a * a;
                    // léger voile intérieur
                    if (d < radius) a = Mathf.Max(a, 0.12f * d / radius);
                    px[y * size + x] = new Color(1, 1, 1, a);
                }
            t.SetPixels(px);
            t.Apply();
            return t;
        }

        static Texture2D MakeDisc(int size)
        {
            var t = NewTex(size);
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01((1f - d) * size * 0.5f);
                    px[y * size + x] = new Color(1, 1, 1, a);
                }
            t.SetPixels(px);
            t.Apply();
            return t;
        }

        static Sprite MakeSprite(Texture2D t)
        {
            return Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), t.width);
        }

        // ------------------------------------------------------------------
        //  Création d'objets
        // ------------------------------------------------------------------
        public static SpriteRenderer MakeSpriteObj(string name, Transform parent, Sprite sprite, Color color, float scale, int order, bool additive)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localScale = new Vector3(scale, scale, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = order;
            if (additive) sr.sharedMaterial = AddMat;
            return sr;
        }

        public static LineRenderer MakeLine(string name, Transform parent, Color color, float width, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = SpriteMat;
            lr.useWorldSpace = true;
            lr.startColor = color;
            lr.endColor = color;
            lr.startWidth = width;
            lr.endWidth = width;
            lr.sortingOrder = order;
            lr.numCapVertices = 2;
            lr.positionCount = 2;
            return lr;
        }

        public static void SetCircle(LineRenderer lr, Vector2 center, float radius, int segments, float z)
        {
            lr.loop = true;
            lr.positionCount = segments;
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                lr.SetPosition(i, new Vector3(center.x + Mathf.Cos(a) * radius, center.y + Mathf.Sin(a) * radius, z));
            }
        }

        public static MeshRenderer MakeMeshObj(string name, Transform parent, Mesh mesh, int order, Material mat = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat != null ? mat : SpriteMat;
            mr.sortingOrder = order;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return mr;
        }

        // ------------------------------------------------------------------
        //  Formes de vaisseaux (avant = +Y, rayon ~1)
        // ------------------------------------------------------------------
        static readonly Vector2[] FighterShape =
        {
            new Vector2(0, 1), new Vector2(0.35f, -0.1f), new Vector2(0.8f, -0.7f), new Vector2(0.25f, -0.5f),
            new Vector2(0, -0.65f), new Vector2(-0.25f, -0.5f), new Vector2(-0.8f, -0.7f), new Vector2(-0.35f, -0.1f),
        };

        static readonly Vector2[] CorvetteShape =
        {
            new Vector2(0, 1), new Vector2(0.25f, 0.5f), new Vector2(0.3f, -0.1f), new Vector2(0.75f, -0.45f),
            new Vector2(0.7f, -0.75f), new Vector2(0.3f, -0.6f), new Vector2(0.2f, -0.85f), new Vector2(-0.2f, -0.85f),
            new Vector2(-0.3f, -0.6f), new Vector2(-0.7f, -0.75f), new Vector2(-0.75f, -0.45f), new Vector2(-0.3f, -0.1f),
            new Vector2(-0.25f, 0.5f),
        };

        static readonly Vector2[] CruiserShape =
        {
            new Vector2(0, 1), new Vector2(0.2f, 0.85f), new Vector2(0.35f, 0.4f), new Vector2(0.5f, 0.2f),
            new Vector2(0.5f, -0.5f), new Vector2(0.35f, -0.8f), new Vector2(0.15f, -0.95f), new Vector2(-0.15f, -0.95f),
            new Vector2(-0.35f, -0.8f), new Vector2(-0.5f, -0.5f), new Vector2(-0.5f, 0.2f), new Vector2(-0.35f, 0.4f),
            new Vector2(-0.2f, 0.85f),
        };

        static readonly Vector2[] ChevronShape =
        {
            new Vector2(0, 1), new Vector2(0.75f, -0.7f), new Vector2(0, -0.3f), new Vector2(-0.75f, -0.7f),
        };

        /// Silhouette d'une classe de vaisseau (avant = +Y, rayon ~1).
        public static Vector2[] ShipShape(ShipClass c)
        {
            return c == ShipClass.Chasseur ? FighterShape : c == ShipClass.Corvette ? CorvetteShape : CruiserShape;
        }

        public static Mesh ShipMesh(ShipClass c, Color color)
        {
            var shape = ShipShape(c);
            return CachedPolygon(shape, color, (int)c, 0.12f);
        }

        public static Mesh ChevronMesh(Color color)
        {
            return CachedPolygon(ChevronShape, color, 10, 0.18f);
        }

        static Mesh CachedPolygon(Vector2[] shape, Color color, int shapeId, float outline)
        {
            Color32 c32 = color;
            long key = ((long)shapeId << 32) | ((long)c32.r << 16) | ((long)c32.g << 8) | c32.b;
            Mesh m;
            if (meshCache.TryGetValue(key, out m) && m != null) return m;
            var fill = color * 0.32f;
            fill.a = 0.92f;
            var line = Color.Lerp(color, Color.white, 0.25f);
            line.a = 1f;
            m = Polygon(shape, fill, line, outline);
            meshCache[key] = m;
            return m;
        }

        /// Polygone étoilé (par rapport à l'origine) avec contour lumineux.
        public static Mesh Polygon(Vector2[] pts, Color fill, Color line, float outline)
        {
            int n = pts.Length;
            var verts = new List<Vector3>();
            var cols = new List<Color>();
            var tris = new List<int>();

            // Centre + points intérieurs (remplissage)
            verts.Add(Vector3.zero);
            cols.Add(Color.Lerp(fill, line, 0.25f));
            for (int i = 0; i < n; i++)
            {
                var p = pts[i];
                var inner = p * Mathf.Max(0.1f, 1f - outline / Mathf.Max(0.01f, p.magnitude));
                verts.Add(inner);
                cols.Add(fill);
            }
            for (int i = 0; i < n; i++)
            {
                tris.Add(0);
                tris.Add(1 + i);
                tris.Add(1 + (i + 1) % n);
            }

            // Contour : bande entre le point extérieur et le point intérieur
            int baseOuter = verts.Count;
            for (int i = 0; i < n; i++)
            {
                verts.Add(pts[i]);
                cols.Add(line);
            }
            int baseInner = verts.Count;
            for (int i = 0; i < n; i++)
            {
                verts.Add(verts[1 + i]);
                cols.Add(line);
            }
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                tris.Add(baseOuter + i); tris.Add(baseOuter + j); tris.Add(baseInner + j);
                tris.Add(baseOuter + i); tris.Add(baseInner + j); tris.Add(baseInner + i);
            }

            var m = new Mesh();
            m.SetVertices(verts);
            m.SetColors(cols);
            m.SetTriangles(tris, 0);
            m.RecalculateBounds();
            return m;
        }

        /// Champ d'étoiles en arrière-plan (parallaxe grâce à la profondeur).
        public static MeshRenderer Starfield(Transform parent, int count, float extent, float zMin, float zMax, int seed)
        {
            var rng = new System.Random(seed);
            var verts = new List<Vector3>();
            var cols = new List<Color>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            for (int i = 0; i < count; i++)
            {
                float z = Mathf.Lerp(zMin, zMax, (float)rng.NextDouble());
                float spread = extent * (1f + z / 60f);
                var c = new Vector3(((float)rng.NextDouble() * 2f - 1f) * spread, ((float)rng.NextDouble() * 2f - 1f) * spread, z);
                float size = (0.25f + (float)rng.NextDouble() * 0.6f) * (1f + z / 80f);
                if (rng.NextDouble() < 0.05) size *= 2.5f;
                float t = (float)rng.NextDouble();
                var col = t < 0.6f ? new Color(0.8f, 0.85f, 1f) : t < 0.85f ? new Color(1f, 0.9f, 0.75f) : new Color(0.6f, 0.75f, 1f);
                col *= 0.35f + (float)rng.NextDouble() * 0.65f;
                col.a = 1f;
                int b = verts.Count;
                verts.Add(c + new Vector3(-size, -size, 0)); uvs.Add(new Vector2(0, 0));
                verts.Add(c + new Vector3(size, -size, 0)); uvs.Add(new Vector2(1, 0));
                verts.Add(c + new Vector3(size, size, 0)); uvs.Add(new Vector2(1, 1));
                verts.Add(c + new Vector3(-size, size, 0)); uvs.Add(new Vector2(0, 1));
                for (int k = 0; k < 4; k++) cols.Add(col);
                tris.Add(b); tris.Add(b + 2); tris.Add(b + 1);
                tris.Add(b); tris.Add(b + 3); tris.Add(b + 2);
            }
            var m = new Mesh();
            m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.SetVertices(verts);
            m.SetColors(cols);
            m.SetUVs(0, uvs);
            m.SetTriangles(tris, 0);
            m.RecalculateBounds();
            return MakeMeshObj("Starfield", parent, m, -100, GlowMat);
        }

        /// Nébuleuses : grandes lueurs colorées très diffuses.
        public static void Nebulae(Transform parent, int count, float extent, float z, int seed)
        {
            var rng = new System.Random(seed);
            Color[] tints =
            {
                new Color(0.25f, 0.15f, 0.5f), new Color(0.1f, 0.25f, 0.45f), new Color(0.4f, 0.12f, 0.3f), new Color(0.1f, 0.3f, 0.3f),
            };
            for (int i = 0; i < count; i++)
            {
                var c = tints[rng.Next(tints.Length)];
                c.a = 0.35f + (float)rng.NextDouble() * 0.3f;
                float s = extent * (0.5f + (float)rng.NextDouble() * 0.9f);
                var sr = MakeSpriteObj("Nebula", parent, GlowSprite, c, s, -90, true);
                sr.transform.localPosition = new Vector3(((float)rng.NextDouble() * 2f - 1f) * extent, ((float)rng.NextDouble() * 2f - 1f) * extent, z);
            }
        }

        // ==================================================================
        //  3D : matériaux éclairés, coques facettées, sphères, particules fixes
        // ==================================================================
        static Material litBase;
        static readonly Dictionary<long, Material> litCache = new Dictionary<long, Material>();
        static readonly Dictionary<long, Material> unlitCache = new Dictionary<long, Material>();
        static readonly Dictionary<int, Mesh> mesh3DCache = new Dictionary<int, Mesh>();
        static Mesh sphereMesh;

        static long ColorKey(Color c)
        {
            Color32 k = c;
            return ((long)k.r << 24) | ((long)k.g << 16) | ((long)k.b << 8) | k.a;
        }

        /// Matériau éclairé (shader Standard) d'une couleur donnée.
        /// Repli sur un matériau non éclairé si le shader n'est pas disponible.
        public static Material Lit(Color c, float gloss = 0.55f, float metal = 0.35f)
        {
            if (litBase == null) litBase = LoadMaterial("StellarisMini/Lit", "Standard");
            long key = ColorKey(c) ^ ((long)Mathf.RoundToInt(gloss * 100f) << 40) ^ ((long)Mathf.RoundToInt(metal * 100f) << 50);
            Material m;
            if (litCache.TryGetValue(key, out m) && m != null) return m;
            m = new Material(litBase);
            m.color = c;
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", gloss);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metal);
            litCache[key] = m;
            return m;
        }

        /// Matériau non éclairé (lignes néon) d'une couleur donnée.
        public static Material Unlit(Color c)
        {
            long key = ColorKey(c);
            Material m;
            if (unlitCache.TryGetValue(key, out m) && m != null) return m;
            m = new Material(SpriteMat);
            m.color = c;
            unlitCache[key] = m;
            return m;
        }

        /// Coque 3D facettée obtenue en "gonflant" la silhouette 2D (avant = +Z).
        public static Mesh ShipMesh3D(ShipClass c)
        {
            Mesh m;
            if (mesh3DCache.TryGetValue((int)c, out m) && m != null) return m;
            var shape = ShipShape(c);
            float hTop = c == ShipClass.Chasseur ? 0.22f : c == ShipClass.Corvette ? 0.28f : 0.34f;
            float hBot = c == ShipClass.Chasseur ? 0.11f : c == ShipClass.Corvette ? 0.16f : 0.22f;
            float apexZ = c == ShipClass.Chasseur ? -0.05f : c == ShipClass.Corvette ? -0.1f : -0.15f;
            var top = new Vector3(0f, hTop, apexZ);
            var bot = new Vector3(0f, -hBot, apexZ);
            var center = new Vector3(0f, 0f, apexZ);
            var light = new Vector3(0.3f, 0.8f, -0.5f).normalized;

            var verts = new List<Vector3>();
            var cols = new List<Color>();
            var tris = new List<int>();
            int n = shape.Length;
            for (int i = 0; i < n; i++)
            {
                var p = shape[i];
                var q = shape[(i + 1) % n];
                var P = new Vector3(p.x, 0f, p.y);
                var Q = new Vector3(q.x, 0f, q.y);
                AddFacet(verts, cols, tris, top, P, Q, center, light);
                AddFacet(verts, cols, tris, bot, Q, P, center, light);
            }
            m = new Mesh();
            m.SetVertices(verts);
            m.SetColors(cols);
            m.SetTriangles(tris, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            mesh3DCache[(int)c] = m;
            return m;
        }

        static void AddFacet(List<Vector3> verts, List<Color> cols, List<int> tris, Vector3 a, Vector3 b, Vector3 c, Vector3 center, Vector3 light)
        {
            var normal = Vector3.Cross(b - a, c - a);
            if (normal.sqrMagnitude < 1e-8f) return;
            // Oriente la face vers l'extérieur (face avant visible)
            if (Vector3.Dot(normal, (a + b + c) / 3f - center) < 0f)
            {
                var t = b; b = c; c = t;
                normal = -normal;
            }
            float shade = 0.55f + 0.45f * Mathf.Max(0f, Vector3.Dot(normal.normalized, light));
            var col = new Color(shade, shade, shade, 1f);
            int i0 = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c);
            cols.Add(col); cols.Add(col); cols.Add(col);
            tris.Add(i0); tris.Add(i0 + 1); tris.Add(i0 + 2);
        }

        /// Sphère UV de rayon 0,5 (comme la primitive Unity).
        public static Mesh Sphere()
        {
            if (sphereMesh != null) return sphereMesh;
            const int lon = 40, lat = 24;
            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var tris = new List<int>();
            for (int y = 0; y <= lat; y++)
            {
                float v = (float)y / lat;
                float phi = v * Mathf.PI;
                for (int x = 0; x <= lon; x++)
                {
                    float theta = (float)x / lon * Mathf.PI * 2f;
                    var n = new Vector3(Mathf.Sin(phi) * Mathf.Cos(theta), Mathf.Cos(phi), Mathf.Sin(phi) * Mathf.Sin(theta));
                    verts.Add(n * 0.5f);
                    normals.Add(n);
                }
            }
            for (int y = 0; y < lat; y++)
                for (int x = 0; x < lon; x++)
                {
                    int a = y * (lon + 1) + x, b = a + lon + 1;
                    tris.Add(a); tris.Add(a + 1); tris.Add(b);
                    tris.Add(a + 1); tris.Add(b + 1); tris.Add(b);
                }
            sphereMesh = new Mesh();
            sphereMesh.SetVertices(verts);
            sphereMesh.SetNormals(normals);
            sphereMesh.SetTriangles(tris, 0);
            sphereMesh.RecalculateBounds();
            return sphereMesh;
        }

        /// Système de particules "fixes" (étoiles, poussière) : on y émet des particules immobiles et quasi éternelles.
        public static ParticleSystem StaticParticles(Transform parent, string name, int max, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.maxParticles = max;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startSpeed = 0f;
            main.startLifetime = 100000f;
            var em = ps.emission;
            em.enabled = false;
            var shape = ps.shape;
            shape.enabled = false;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = GlowMat;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.sortingOrder = order;
            r.maxParticleSize = 3f;
            ps.Play();
            return ps;
        }

        public static void EmitStatic(ParticleSystem ps, Vector3 pos, Color c, float size)
        {
            var ep = new ParticleSystem.EmitParams();
            ep.position = pos;
            ep.velocity = Vector3.zero;
            ep.startColor = c;
            ep.startSize = size;
            ep.startLifetime = 100000f;
            ps.Emit(ep, 1);
        }
    }
}
