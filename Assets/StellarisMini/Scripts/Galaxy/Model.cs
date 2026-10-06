using System.Collections.Generic;
using UnityEngine;

namespace StellarisMini
{
    // =====================================================================
    //  Modèle de données de la partie (couche stratégique).
    //  Aucune dépendance à l'affichage : tout l'état du jeu vit ici.
    // =====================================================================

    public enum ShipClass { Chasseur = 0, Corvette = 1, Croiseur = 2 }

    public enum Specialization { Energie = 0, Mines = 1, Industrie = 2, Recherche = 3 }

    public enum TechTrack { Armes = 0, Boucliers = 1, Propulsion = 2, Production = 3 }

    public enum PlanetType { Continentale, Oceanique, Desertique, Arctique, Tropicale, Toxique, GeanteGazeuse, Sterile }

    public enum StarClass { Jaune, Orange, Rouge, Blanche, Bleue }

    public static class Names
    {
        public static readonly string[] Specs = { "Énergie", "Mines", "Industrie", "Recherche" };
        public static readonly string[] Techs = { "Armes", "Boucliers", "Propulsion", "Production" };
        public static readonly string[] TechEffects =
        {
            "+15 % de dégâts par niveau",
            "+25 % de boucliers par niveau",
            "+10 % de vitesse par niveau",
            "+10 % de production par niveau",
        };
        public static readonly string[] Ships = { "Chasseur", "Corvette", "Croiseur" };
        public static readonly string[] ShipsPlural = { "chasseurs", "corvettes", "croiseurs" };
        public static readonly string[] Stars = { "Naine jaune", "Naine orange", "Naine rouge", "Étoile blanche", "Géante bleue" };

        public static string Planet(PlanetType t)
        {
            switch (t)
            {
                case PlanetType.Continentale: return "Continentale";
                case PlanetType.Oceanique: return "Océanique";
                case PlanetType.Desertique: return "Désertique";
                case PlanetType.Arctique: return "Arctique";
                case PlanetType.Tropicale: return "Tropicale";
                case PlanetType.Toxique: return "Toxique";
                case PlanetType.GeanteGazeuse: return "Géante gazeuse";
                default: return "Stérile";
            }
        }
    }

    public class Planet
    {
        public string name;
        public PlanetType type;
        public int size;
        public bool Habitable { get { return type <= PlanetType.Tropicale; } }
    }

    public class BuildOrder
    {
        public ShipClass cls;
        public float daysLeft;
        public float totalDays;
    }

    public class Colony
    {
        public float pop = 1;
        public int maxPop = 5;
        public float growth;
        public bool capital;
        public Specialization spec = Specialization.Mines;
        public readonly List<BuildOrder> queue = new List<BuildOrder>();
    }

    public class StarSystem
    {
        public int id;
        public string name;
        public Vector2 pos;
        public StarClass star;
        public readonly List<Planet> planets = new List<Planet>();
        public readonly List<StarSystem> lanes = new List<StarSystem>();

        public Empire owner;
        public Colony colony;          // null = avant-poste (si owner != null)

        public Empire occupier;        // empire qui assiège le système
        public float occupation;       // jours d'occupation accumulés

        public int MaxPop
        {
            get
            {
                int p = 0;
                foreach (var pl in planets) if (pl.Habitable) p += pl.size;
                return p;
            }
        }

        public bool Habitable { get { return MaxPop > 0; } }
        public bool IsColony { get { return owner != null && colony != null; } }
        public bool IsOutpost { get { return owner != null && colony == null; } }
    }

    public class ShipData
    {
        public ShipClass cls;
        public string name;
        public float hull;
        public bool destroyed;

        public ShipSpec Spec { get { return ShipCatalog.Get(cls); } }
        public float HullRatio { get { return Mathf.Clamp01(hull / Spec.hull); } }
    }

    public class Fleet
    {
        public int id;
        public string name;
        public Empire owner;
        public readonly List<ShipData> ships = new List<ShipData>();

        public StarSystem system;      // système actuel (ou de départ si en transit)
        public StarSystem next;        // prochain système (null = à l'arrêt)
        public float progress;         // 0..1 sur l'hyperligne system -> next
        public readonly List<StarSystem> path = new List<StarSystem>(); // étapes après "next"
        public StarSystem cameFrom;

        public bool InTransit { get { return next != null; } }

        public Vector2 Position
        {
            get { return next == null ? system.pos : Vector2.Lerp(system.pos, next.pos, progress); }
        }

        public StarSystem Destination
        {
            get
            {
                if (path.Count > 0) return path[path.Count - 1];
                return next;
            }
        }

        public float HullRatio
        {
            get
            {
                float h = 0, m = 0;
                foreach (var s in ships) { h += s.hull; m += s.Spec.hull; }
                return m > 0 ? h / m : 0;
            }
        }

        public int Count(ShipClass c)
        {
            int n = 0;
            foreach (var s in ships) if (s.cls == c) n++;
            return n;
        }

        public string Composition()
        {
            var parts = new List<string>();
            for (int c = 2; c >= 0; c--)
            {
                int n = Count((ShipClass)c);
                if (n > 0) parts.Add(n + " " + (n > 1 ? Names.ShipsPlural[c] : Names.Ships[c].ToLower()));
            }
            return parts.Count == 0 ? "aucun vaisseau" : string.Join(", ", parts.ToArray());
        }
    }

    public struct Yield
    {
        public float energy, minerals, alloys, research;
    }

    public class ClaimOrder
    {
        public Empire empire;
        public StarSystem target;
        public bool colony;
        public float daysLeft;
        public float totalDays;
    }

    public class Empire
    {
        public int id;
        public string name;
        public Color color;
        public bool isPlayer;
        public bool alive = true;

        public float energy = 100, minerals = 150, alloys = 100;
        public float researchStock;
        public readonly int[] tech = new int[4];
        public int currentResearch = -1;   // index de TechTrack, -1 = aucune
        public bool energyDeficit;
        public Yield lastIncome;

        public StarSystem capital;
        public readonly List<ClaimOrder> claims = new List<ClaimOrder>();
        public readonly HashSet<StarSystem> explored = new HashSet<StarSystem>();
        public EmpireAI ai;
        public int shipCounter;
        public int fleetCounter;

        public float DamageMul { get { return 1f + 0.15f * tech[(int)TechTrack.Armes]; } }
        public float ShieldMul { get { return 1f + 0.25f * tech[(int)TechTrack.Boucliers]; } }
        public float SpeedMul { get { return 1f + 0.10f * tech[(int)TechTrack.Propulsion]; } }
        public float ProductionMul { get { return 1f + 0.10f * tech[(int)TechTrack.Production]; } }

        public string ColorHex { get { return ColorUtility.ToHtmlStringRGB(color); } }
        public string Rich { get { return "<color=#" + ColorHex + ">" + name + "</color>"; } }
    }

    public class GameSettings
    {
        public int galaxySize = 1;   // 0 petite, 1 moyenne, 2 grande
        public int rivals = 2;       // nombre d'empires IA
        public int difficulty = 1;   // 0 facile, 1 normale, 2 difficile
        public int seed;

        public static readonly string[] SizeNames = { "Petite", "Moyenne", "Grande" };
        public static readonly int[] SystemCounts = { 20, 30, 44 };
        public static readonly string[] DifficultyNames = { "Facile", "Normale", "Difficile" };
    }

    public class Galaxy
    {
        public GameSettings settings;
        public readonly List<StarSystem> systems = new List<StarSystem>();
        public readonly List<Empire> empires = new List<Empire>();
        public readonly List<Fleet> fleets = new List<Fleet>();
        public Empire player;
        public int day;
        public float radius;
        public int nextFleetId = 1;

        public string DateString
        {
            get
            {
                int y = 2200 + day / 360;
                int m = (day % 360) / 30 + 1;
                int d = day % 30 + 1;
                return y + "." + m.ToString("00") + "." + d.ToString("00");
            }
        }
    }

    /// Une bataille en attente entre la flotte du joueur et des flottes hostiles.
    public class Battle
    {
        public StarSystem system;
        public readonly List<Fleet> playerFleets = new List<Fleet>();
        public readonly List<Fleet> enemyFleets = new List<Fleet>();
        public Empire enemy;   // empire ennemi principal (pour l'affichage)
    }

    public enum BattleOutcome { Victory, Defeat, Retreat }
}
