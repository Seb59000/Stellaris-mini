using System.Collections.Generic;
using UnityEngine;

namespace StellarisMini
{
    /// Génère une galaxie procédurale : systèmes, hyperlignes, planètes et empires de départ.
    public static class GalaxyGenerator
    {
        static readonly string[] Pre = { "Al", "Ar", "Bel", "Cor", "Cy", "Dra", "El", "Fen", "Gal", "Hel", "Ix", "Kel", "Kor", "Lum", "Mar", "Myr", "Nex", "Nor", "Or", "Pha", "Quil", "Rho", "Sel", "Sor", "Tal", "Tha", "Ul", "Vel", "Vor", "Xan", "Yr", "Zan", "Zeph" };
        static readonly string[] Mid = { "", "", "", "a", "e", "i", "o", "ar", "en", "is", "or" };
        static readonly string[] End = { "ion", "ara", "is", "os", "eth", "ar", "une", "ia", "ax", "on", "us", "ei", "ora", "yn", "ade" };
        static readonly string[] Greek = { "Alpha", "Bêta", "Gamma", "Delta", "Prime", "Majoris", "Minoris" };
        static readonly string[] Roman = { "I", "II", "III", "IV", "V", "VI" };

        struct EmpireDef
        {
            public string name; public Color color;
            public EmpireDef(string n, Color c) { name = n; color = c; }
        }

        static readonly EmpireDef PlayerDef = new EmpireDef("Fédération Terrienne", new Color(0.25f, 0.65f, 1f));
        static readonly EmpireDef[] AiDefs =
        {
            new EmpireDef("Hégémonie Vorax", new Color(1f, 0.32f, 0.26f)),
            new EmpireDef("Collectif Zhar", new Color(0.35f, 0.95f, 0.42f)),
            new EmpireDef("Dominion Kel'tar", new Color(1f, 0.78f, 0.2f)),
            new EmpireDef("Essaim Myrrh", new Color(0.78f, 0.42f, 1f)),
        };

        public static Galaxy Generate(GameSettings settings)
        {
            var rng = new System.Random(settings.seed);
            var g = new Galaxy { settings = settings };
            int n = GameSettings.SystemCounts[Mathf.Clamp(settings.galaxySize, 0, 2)];
            float minDist = 8.5f;
            g.radius = Mathf.Sqrt(n) * 7.4f;

            // --- Placement des systèmes (échantillonnage par rejet dans un disque)
            var names = new HashSet<string>();
            int attempts = 0;
            while (g.systems.Count < n && attempts < 20000)
            {
                attempts++;
                float a = (float)rng.NextDouble() * Mathf.PI * 2f;
                float r = Mathf.Sqrt((float)rng.NextDouble()) * g.radius;
                var p = new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r * 0.8f);
                bool ok = true;
                foreach (var s in g.systems)
                    if ((s.pos - p).sqrMagnitude < minDist * minDist) { ok = false; break; }
                if (!ok) continue;

                var sys = new StarSystem { id = g.systems.Count, pos = p, name = UniqueName(rng, names) };
                sys.star = RandomStar(rng);
                GeneratePlanets(rng, sys);
                g.systems.Add(sys);
            }

            BuildLanes(rng, g.systems);

            // --- Empires
            int rivals = Mathf.Clamp(settings.rivals, 1, AiDefs.Length);
            var homes = PickHomes(rng, g, rivals + 1);

            for (int i = 0; i <= rivals; i++)
            {
                var def = i == 0 ? PlayerDef : AiDefs[i - 1];
                var e = new Empire { id = i, name = def.name, color = def.color, isPlayer = i == 0 };
                g.empires.Add(e);
                SetupHome(g, e, homes[i], i == 0);
            }
            g.player = g.empires[0];
            return g;
        }

        // ------------------------------------------------------------------
        static string UniqueName(System.Random rng, HashSet<string> used)
        {
            for (int tries = 0; tries < 200; tries++)
            {
                string s = Pre[rng.Next(Pre.Length)] + Mid[rng.Next(Mid.Length)] + End[rng.Next(End.Length)];
                if (rng.NextDouble() < 0.15) s += " " + Greek[rng.Next(Greek.Length)];
                if (used.Add(s)) return s;
            }
            string fallback = "Système " + used.Count;
            used.Add(fallback);
            return fallback;
        }

        static StarClass RandomStar(System.Random rng)
        {
            double r = rng.NextDouble();
            if (r < 0.32) return StarClass.Jaune;
            if (r < 0.56) return StarClass.Orange;
            if (r < 0.80) return StarClass.Rouge;
            if (r < 0.92) return StarClass.Blanche;
            return StarClass.Bleue;
        }

        static void GeneratePlanets(System.Random rng, StarSystem sys)
        {
            int count = rng.Next(0, 5);
            for (int i = 0; i < count; i++)
            {
                var p = new Planet { name = sys.name + " " + Roman[i] };
                if (rng.NextDouble() < 0.34)
                {
                    p.type = (PlanetType)rng.Next(0, 5);   // habitable
                    p.size = rng.Next(2, 6);
                }
                else
                {
                    p.type = (PlanetType)rng.Next(5, 8);   // inhabitable
                    p.size = rng.Next(1, 6);
                }
                sys.planets.Add(p);
            }
        }

        /// Hyperlignes : graphe de voisinage relatif (connexe et sans croisement)
        /// enrichi de quelques arêtes du graphe de Gabriel pour créer des boucles.
        static void BuildLanes(System.Random rng, List<StarSystem> systems)
        {
            int n = systems.Count;
            for (int i = 0; i < n; i++)
            {
                for (int j = i + 1; j < n; j++)
                {
                    Vector2 a = systems[i].pos, b = systems[j].pos;
                    float dab = (a - b).sqrMagnitude;
                    bool rng1 = true, gabriel = true;
                    for (int k = 0; k < n && (rng1 || gabriel); k++)
                    {
                        if (k == i || k == j) continue;
                        Vector2 c = systems[k].pos;
                        float dac = (a - c).sqrMagnitude, dbc = (b - c).sqrMagnitude;
                        if (Mathf.Max(dac, dbc) < dab) rng1 = false;
                        if (dac + dbc < dab) gabriel = false;
                    }
                    bool add = rng1 || (gabriel && rng.NextDouble() < 0.4);
                    if (add)
                    {
                        systems[i].lanes.Add(systems[j]);
                        systems[j].lanes.Add(systems[i]);
                    }
                }
            }
        }

        static List<StarSystem> PickHomes(System.Random rng, Galaxy g, int count)
        {
            var homes = new List<StarSystem>();
            // Joueur : un système plutôt en périphérie
            var outer = new List<StarSystem>();
            foreach (var s in g.systems)
                if (s.pos.magnitude > g.radius * 0.55f) outer.Add(s);
            if (outer.Count == 0) outer.AddRange(g.systems);
            homes.Add(outer[rng.Next(outer.Count)]);

            // Les autres : le plus loin possible (en sauts) des capitales déjà choisies
            while (homes.Count < count)
            {
                var dist = MultiSourceHops(g, homes);
                int best = -1;
                StarSystem pick = null;
                foreach (var s in g.systems)
                {
                    if (homes.Contains(s)) continue;
                    int d = dist[s.id] * 100 + rng.Next(50);
                    if (d > best) { best = d; pick = s; }
                }
                homes.Add(pick);
            }
            return homes;
        }

        static int[] MultiSourceHops(Galaxy g, List<StarSystem> sources)
        {
            var dist = new int[g.systems.Count];
            for (int i = 0; i < dist.Length; i++) dist[i] = int.MaxValue;
            var q = new Queue<StarSystem>();
            foreach (var s in sources) { dist[s.id] = 0; q.Enqueue(s); }
            while (q.Count > 0)
            {
                var s = q.Dequeue();
                foreach (var t in s.lanes)
                    if (dist[t.id] == int.MaxValue) { dist[t.id] = dist[s.id] + 1; q.Enqueue(t); }
            }
            return dist;
        }

        static void SetupHome(Galaxy g, Empire e, StarSystem home, bool player)
        {
            if (player) home.name = "Sol";
            // Garantit une bonne planète habitable
            home.planets.Clear();
            home.planets.Add(new Planet { name = player ? "Terre" : home.name + " Prime", type = PlanetType.Continentale, size = 5 });
            home.planets.Add(new Planet { name = home.name + " II", type = PlanetType.Desertique, size = 3 });
            home.planets.Add(new Planet { name = home.name + " III", type = PlanetType.GeanteGazeuse, size = 5 });
            home.star = StarClass.Jaune;

            home.owner = e;
            home.colony = new Colony { pop = 8, maxPop = home.MaxPop + 4, capital = true, spec = Specialization.Industrie };
            e.capital = home;

            e.explored.Add(home);
            foreach (var n in home.lanes) e.explored.Add(n);

            var f = new Fleet { id = g.nextFleetId++, owner = e, system = home, name = "Flotte du Foyer" };
            f.ships.Add(NewShip(e, ShipClass.Corvette));
            f.ships.Add(NewShip(e, ShipClass.Chasseur));
            f.ships.Add(NewShip(e, ShipClass.Chasseur));
            if (player)
            {
                // Coup de pouce selon la difficulté
                var d = g.settings.Diff;
                for (int i = 0; i < d.bonusCorvettes; i++) f.ships.Add(NewShip(e, ShipClass.Corvette));
                for (int i = 0; i < d.bonusFighters; i++) f.ships.Add(NewShip(e, ShipClass.Chasseur));
                e.alloys += d.bonusAlloys;
            }
            g.fleets.Add(f);
        }

        static readonly string[] ShipNames =
        {
            "Intrépide", "Audacieux", "Vaillant", "Foudroyant", "Redoutable", "Téméraire", "Éclair", "Tempête",
            "Aurore", "Persévérance", "Hermione", "Triomphant", "Indomptable", "Sirius", "Orion", "Pégase",
            "Phénix", "Comète", "Héraclès", "Vigilant", "Fougueux", "Implacable", "Résolu", "Arcturus",
        };

        public static ShipData NewShip(Empire e, ShipClass cls)
        {
            int i = e.shipCounter++;
            string name;
            if (e.isPlayer)
            {
                name = ShipNames[i % ShipNames.Length];
                if (i >= ShipNames.Length) name += " " + (i / ShipNames.Length + 1);
            }
            else
            {
                name = Names.Ships[(int)cls] + " " + e.name.Split(' ')[1] + "-" + (i + 1);
            }
            return new ShipData { cls = cls, name = name, hull = ShipCatalog.Get(cls).hull };
        }
    }
}
