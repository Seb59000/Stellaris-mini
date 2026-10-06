using System.Collections.Generic;
using UnityEngine;

namespace StellarisMini
{
    public class Notice
    {
        public string text;
        public Color color;
        public StarSystem system;
        public float time;
    }

    /// Simulation de la couche stratégique, en temps réel avec pause.
    public class GalaxySim
    {
        public readonly Galaxy g;
        public int speed = 1;
        public bool paused = true;
        public Battle pendingBattle;
        public bool gameOver, victory;
        public readonly List<Notice> notices = new List<Notice>();
        public readonly HashSet<StarSystem> sensors = new HashSet<StarSystem>();

        /// Jours de jeu écoulés par seconde réelle pour chaque vitesse.
        public static readonly float[] DaysPerSecond = { 0f, 2.5f, 5f, 10f };
        public const int MaxClaims = 2;
        public const int MaxQueue = 5;
        public const int MaxTech = 5;

        float dayFrac;

        public GalaxySim(Galaxy galaxy)
        {
            g = galaxy;
            foreach (var e in g.empires)
            {
                if (!e.isPlayer) e.ai = new EmpireAI(e, this);
                e.lastIncome = ComputeIncome(e);
            }
            UpdateSensors();
        }

        // ==================================================================
        //  Boucle principale
        // ==================================================================
        public void Tick(float realDt)
        {
            if (paused || pendingBattle != null || gameOver) return;
            float days = Mathf.Min(realDt * DaysPerSecond[speed], 2f);

            MoveFleets(days);
            CheckBattles();
            if (pendingBattle != null) return;

            dayFrac += days;
            while (dayFrac >= 1f)
            {
                dayFrac -= 1f;
                DailyTick();
                if (pendingBattle != null || gameOver) break;
            }
        }

        void DailyTick()
        {
            g.day++;

            // Revendications (colonies / avant-postes)
            foreach (var e in g.empires)
            {
                if (!e.alive) continue;
                for (int i = e.claims.Count - 1; i >= 0; i--)
                {
                    var c = e.claims[i];
                    if (c.target.owner != null)
                    {
                        e.claims.RemoveAt(i);
                        if (e.isPlayer) Notify("Revendication de " + c.target.name + " annulée : le système a été pris.", Palette.Warning, c.target);
                        continue;
                    }
                    if (HasHostileFleets(c.target, e)) continue; // en pause tant que l'ennemi est là
                    c.daysLeft -= 1f;
                    if (c.daysLeft <= 0)
                    {
                        e.claims.RemoveAt(i);
                        Establish(e, c.target, c.colony);
                    }
                }
            }

            // Chantiers spatiaux
            foreach (var s in g.systems)
            {
                if (!s.IsColony || s.colony.queue.Count == 0) continue;
                if (HasHostileFleets(s, s.owner)) continue;
                var o = s.colony.queue[0];
                o.daysLeft -= 1f;
                if (o.daysLeft <= 0)
                {
                    s.colony.queue.RemoveAt(0);
                    var f = SpawnShip(s.owner, s, o.cls);
                    if (s.owner.isPlayer)
                        Notify(Names.Ships[(int)o.cls] + " construit à " + s.name + " (" + f.name + ").", Palette.Good, s);
                }
            }

            // Occupation des systèmes ennemis
            foreach (var s in g.systems)
            {
                if (s.owner == null) continue;
                Empire occ = null;
                bool defended = false;
                foreach (var f in g.fleets)
                {
                    if (f.InTransit || f.system != s) continue;
                    if (f.owner == s.owner) defended = true;
                    else if (occ == null) occ = f.owner;
                }
                if (occ != null && !defended)
                {
                    if (s.occupier != occ)
                    {
                        s.occupier = occ;
                        s.occupation = 0;
                        if (s.owner.isPlayer) Notify(occ.Rich + " assiège " + s.name + " !", Palette.Bad, s);
                        else if (occ.isPlayer) Notify("Vos forces assiègent " + s.name + ".", Palette.Info, s);
                    }
                    s.occupation += 1f;
                    if (s.occupation >= OccupationDays(s)) Capture(s, occ);
                }
                else
                {
                    s.occupier = null;
                    s.occupation = 0;
                }
            }

            // Réparations
            foreach (var f in g.fleets)
            {
                if (f.InTransit || f.owner.energyDeficit) continue;
                float rate = f.system.owner == f.owner ? 0.02f : 0.004f;
                foreach (var sh in f.ships) sh.hull = Mathf.Min(sh.Spec.hull, sh.hull + sh.Spec.hull * rate);
            }

            UpdateSensors();

            if (g.day % 5 == 0)
                foreach (var e in g.empires)
                    if (e.alive && e.ai != null) e.ai.Think();

            if (g.day % 30 == 0) MonthlyTick();

            CheckBattles();
        }

        void MonthlyTick()
        {
            foreach (var e in g.empires)
            {
                if (!e.alive) continue;
                var inc = ComputeIncome(e);
                e.lastIncome = inc;
                e.energy += inc.energy;
                e.minerals += inc.minerals;
                e.alloys += inc.alloys;
                e.researchStock += inc.research;

                bool wasDeficit = e.energyDeficit;
                e.energyDeficit = e.energy < 0;
                if (e.energy < 0) e.energy = 0;
                if (e.isPlayer && e.energyDeficit && !wasDeficit)
                    Notify("Déficit d'énergie : vos flottes ne sont plus réparées.", Palette.Bad, null);
                e.minerals = Mathf.Max(0, e.minerals);

                // Recherche
                if (e.currentResearch >= 0)
                {
                    int t = e.currentResearch;
                    float cost = ResearchCost(e, t);
                    if (e.tech[t] < MaxTech && e.researchStock >= cost)
                    {
                        e.researchStock -= cost;
                        e.tech[t]++;
                        if (e.isPlayer)
                            Notify("Recherche terminée : " + Names.Techs[t] + " niveau " + e.tech[t] + ".", Palette.Research, null);
                        if (e.tech[t] >= MaxTech || e.ai != null) e.currentResearch = -1;
                    }
                }

                // Croissance de la population
                foreach (var s in g.systems)
                {
                    if (s.owner != e || s.colony == null) continue;
                    var c = s.colony;
                    if (c.pop >= c.maxPop) continue;
                    c.growth += c.pop < 3 ? 0.5f : 0.34f;
                    if (c.growth >= 1f)
                    {
                        c.growth -= 1f;
                        c.pop = Mathf.Min(c.maxPop, c.pop + 1);
                    }
                }

                if (e.ai != null) e.ai.Monthly();
            }
        }

        // ==================================================================
        //  Économie
        // ==================================================================
        /// Production brute d'un système (avant les bonus de l'empire).
        public static Yield SystemYield(StarSystem s)
        {
            var y = new Yield();
            if (s.owner == null) return y;
            if (s.colony != null)
            {
                float p = s.colony.pop;
                y.energy += p;
                y.minerals += p;
                switch (s.colony.spec)
                {
                    case Specialization.Energie: y.energy += p * 2f; break;
                    case Specialization.Mines: y.minerals += p * 2f; break;
                    case Specialization.Industrie: y.alloys += p * 1.2f; break;
                    case Specialization.Recherche: y.research += p * 2f; break;
                }
                if (s.colony.capital) { y.energy += 4; y.minerals += 4; y.alloys += 3; y.research += 5; }
            }
            else
            {
                y.energy += 1;
                y.minerals += 2;
            }
            return y;
        }

        public float EmpireMul(Empire e)
        {
            float mul = e.ProductionMul;
            if (!e.isPlayer) mul *= DifficultyMul;
            return mul;
        }

        public Yield ComputeIncome(Empire e)
        {
            var y = new Yield();
            foreach (var s in g.systems)
            {
                if (s.owner != e) continue;
                var sy = SystemYield(s);
                y.energy += sy.energy;
                y.minerals += sy.minerals;
                y.alloys += sy.alloys;
                y.research += sy.research;
            }

            float mul = EmpireMul(e);
            y.energy *= mul; y.minerals *= mul; y.alloys *= mul; y.research *= mul;

            foreach (var f in g.fleets)
                if (f.owner == e)
                    foreach (var sh in f.ships) y.energy -= sh.Spec.upkeep;
            return y;
        }

        public float DifficultyMul
        {
            get { return g.settings.difficulty == 0 ? 0.75f : g.settings.difficulty == 2 ? 1.3f : 1f; }
        }

        public float ResearchCost(Empire e, int track)
        {
            return 60f * (e.tech[track] + 1);
        }

        public void SetResearch(Empire e, int track)
        {
            if (track >= 0 && e.tech[track] >= MaxTech) return;
            e.currentResearch = track;
        }

        public void SetSpecialization(StarSystem s, Specialization spec)
        {
            if (s.colony != null) s.colony.spec = spec;
        }

        // ==================================================================
        //  Revendications
        // ==================================================================
        public static void ClaimCost(bool colony, out int minerals, out int energy, out int days)
        {
            if (colony) { minerals = 120; energy = 40; days = 90; }
            else { minerals = 50; energy = 0; days = 30; }
        }

        public bool IsBeingClaimed(StarSystem s)
        {
            foreach (var e in g.empires)
                foreach (var c in e.claims)
                    if (c.target == s) return true;
            return false;
        }

        /// Renvoie null si l'empire peut revendiquer le système, sinon la raison.
        public string CanClaim(Empire e, StarSystem s)
        {
            if (s.owner != null) return "Système déjà revendiqué.";
            if (e.isPlayer && !e.explored.Contains(s)) return "Système inexploré.";
            if (IsBeingClaimed(s)) return "Revendication déjà en cours.";
            bool adjacent = false;
            foreach (var n in s.lanes) if (n.owner == e) { adjacent = true; break; }
            if (!adjacent) return "Doit être adjacent à votre territoire.";
            if (e.claims.Count >= MaxClaims) return "Maximum " + MaxClaims + " revendications simultanées.";
            if (HasHostileFleets(s, e)) return "Des flottes ennemies sont présentes.";
            int m, en, d;
            ClaimCost(s.Habitable, out m, out en, out d);
            if (e.minerals < m || e.energy < en) return "Ressources insuffisantes.";
            return null;
        }

        public bool StartClaim(Empire e, StarSystem s)
        {
            if (CanClaim(e, s) != null) return false;
            int m, en, d;
            bool colony = s.Habitable;
            ClaimCost(colony, out m, out en, out d);
            e.minerals -= m;
            e.energy -= en;
            e.claims.Add(new ClaimOrder { empire = e, target = s, colony = colony, daysLeft = d, totalDays = d });
            return true;
        }

        public ClaimOrder ClaimOn(StarSystem s)
        {
            foreach (var e in g.empires)
                foreach (var c in e.claims)
                    if (c.target == s) return c;
            return null;
        }

        void Establish(Empire e, StarSystem s, bool colony)
        {
            s.owner = e;
            if (colony && s.Habitable)
            {
                s.colony = new Colony { pop = 1, maxPop = s.MaxPop, spec = Specialization.Mines };
                if (e.capital == null) { s.colony.capital = true; e.capital = s; }
            }
            e.explored.Add(s);
            foreach (var n in s.lanes) e.explored.Add(n);
            if (e.isPlayer)
                Notify((colony ? "Nouvelle colonie fondée à " : "Avant-poste établi à ") + s.name + ".", Palette.Good, s);
            UpdateSensors();
        }

        public static float OccupationDays(StarSystem s)
        {
            if (s.colony == null) return 20;
            return s.colony.capital ? 60 : 40;
        }

        void Capture(StarSystem s, Empire occ)
        {
            var old = s.owner;
            s.owner = occ;
            s.occupier = null;
            s.occupation = 0;
            if (s.colony != null)
            {
                s.colony.pop = Mathf.Max(1, Mathf.Floor(s.colony.pop * 0.5f));
                s.colony.queue.Clear();
                if (s.colony.capital)
                {
                    s.colony.capital = false;
                    old.capital = null;
                    PickNewCapital(old);
                }
                if (occ.capital == null) { s.colony.capital = true; occ.capital = s; }
            }
            occ.explored.Add(s);
            foreach (var n in s.lanes) occ.explored.Add(n);

            if (old.isPlayer) Notify(s.name + " est tombé aux mains de " + occ.Rich + " !", Palette.Bad, s);
            else if (occ.isPlayer) Notify("Vous avez conquis " + s.name + " !", Palette.Good, s);

            CheckElimination(old);
            UpdateSensors();
        }

        void PickNewCapital(Empire e)
        {
            StarSystem best = null;
            foreach (var s in g.systems)
                if (s.owner == e && s.colony != null && (best == null || s.colony.pop > best.colony.pop)) best = s;
            if (best != null) { best.colony.capital = true; e.capital = best; }
        }

        void CheckElimination(Empire e)
        {
            if (!e.alive) return;
            foreach (var s in g.systems) if (s.owner == e) return;

            e.alive = false;
            e.claims.Clear();
            g.fleets.RemoveAll(f => f.owner == e);
            if (e.isPlayer)
            {
                gameOver = true;
                victory = false;
                return;
            }
            Notify("L'empire " + e.Rich + " s'est effondré !", Palette.Good, null);

            bool anyAlive = false;
            foreach (var o in g.empires) if (!o.isPlayer && o.alive) anyAlive = true;
            if (!anyAlive) { gameOver = true; victory = true; }
        }

        // ==================================================================
        //  Construction
        // ==================================================================
        public string CanBuild(Empire e, StarSystem s, ShipClass c)
        {
            if (s.owner != e || s.colony == null) return "Aucun chantier spatial ici.";
            if (s.colony.queue.Count >= MaxQueue) return "File de construction pleine.";
            if (e.alloys < ShipCatalog.Get(c).alloyCost) return "Alliages insuffisants.";
            return null;
        }

        public bool QueueShip(Empire e, StarSystem s, ShipClass c)
        {
            if (CanBuild(e, s, c) != null) return false;
            var spec = ShipCatalog.Get(c);
            e.alloys -= spec.alloyCost;
            s.colony.queue.Add(new BuildOrder { cls = c, daysLeft = spec.buildDays, totalDays = spec.buildDays });
            return true;
        }

        public void CancelLastBuild(StarSystem s)
        {
            if (s.colony == null || s.colony.queue.Count == 0) return;
            var o = s.colony.queue[s.colony.queue.Count - 1];
            s.colony.queue.RemoveAt(s.colony.queue.Count - 1);
            s.owner.alloys += ShipCatalog.Get(o.cls).alloyCost;
        }

        Fleet SpawnShip(Empire e, StarSystem s, ShipClass c)
        {
            Fleet target = null;
            foreach (var f in g.fleets)
                if (f.owner == e && !f.InTransit && f.system == s) { target = f; break; }
            if (target == null) target = NewFleet(e, s);
            target.ships.Add(GalaxyGenerator.NewShip(e, c));
            return target;
        }

        public Fleet NewFleet(Empire e, StarSystem s)
        {
            e.fleetCounter++;
            var f = new Fleet { id = g.nextFleetId++, owner = e, system = s, name = (e.fleetCounter + 1) + "e Flotte" };
            g.fleets.Add(f);
            return f;
        }

        // ==================================================================
        //  Flottes
        // ==================================================================
        public float FleetSpeed(Fleet f)
        {
            float sp = 99f;
            foreach (var s in f.ships) sp = Mathf.Min(sp, s.Spec.galaxySpeed);
            if (f.ships.Count == 0) sp = 1f;
            return sp * f.owner.SpeedMul;
        }

        public float FleetPower(Fleet f)
        {
            float p = 0;
            foreach (var s in f.ships) p += s.Spec.power * (0.4f + 0.6f * s.HullRatio);
            return p * (1f + 0.08f * (f.owner.tech[0] + f.owner.tech[1]));
        }

        void MoveFleets(float days)
        {
            for (int i = 0; i < g.fleets.Count; i++)
            {
                var f = g.fleets[i];
                if (f.next == null) continue;
                float len = Mathf.Max(0.1f, Vector2.Distance(f.system.pos, f.next.pos));
                f.progress += FleetSpeed(f) * days / len;
                if (f.progress >= 1f) Arrive(f);
            }
        }

        void Arrive(Fleet f)
        {
            f.cameFrom = f.system;
            f.system = f.next;
            f.next = null;
            f.progress = 0;
            f.owner.explored.Add(f.system);

            if (HasHostileFleets(f.system, f.owner))
            {
                f.path.Clear();   // on s'arrête pour combattre
            }
            else if (f.path.Count > 0)
            {
                f.next = f.path[0];
                f.path.RemoveAt(0);
            }
            if (f.owner.isPlayer) UpdateSensors();
        }

        public void OrderMove(Fleet f, StarSystem target)
        {
            if (f == null || target == null) return;
            if (f.next != null)
            {
                if (target == f.system)
                {
                    // demi-tour sur l'hyperligne
                    var tmp = f.system; f.system = f.next; f.next = tmp;
                    f.progress = 1f - f.progress;
                    f.path.Clear();
                    return;
                }
                var p = FindPath(f.next, target);
                if (p == null) return;
                f.path.Clear();
                f.path.AddRange(p);
                return;
            }
            if (target == f.system) { f.path.Clear(); return; }
            var path = FindPath(f.system, target);
            if (path == null || path.Count == 0) return;
            f.next = path[0];
            path.RemoveAt(0);
            f.path.Clear();
            f.path.AddRange(path);
            f.progress = 0;
        }

        public void StopFleet(Fleet f)
        {
            f.path.Clear();
        }

        public void Merge(Fleet into, Fleet other)
        {
            if (into == other || into.owner != other.owner) return;
            into.ships.AddRange(other.ships);
            other.ships.Clear();
            g.fleets.Remove(other);
        }

        /// Dijkstra sur les hyperlignes. Renvoie la liste des étapes (sans le départ).
        public List<StarSystem> FindPath(StarSystem from, StarSystem to)
        {
            if (from == to) return new List<StarSystem>();
            int n = g.systems.Count;
            var dist = new float[n];
            var prev = new StarSystem[n];
            var done = new bool[n];
            for (int i = 0; i < n; i++) dist[i] = float.MaxValue;
            dist[from.id] = 0;
            while (true)
            {
                StarSystem u = null;
                float best = float.MaxValue;
                foreach (var s in g.systems)
                    if (!done[s.id] && dist[s.id] < best) { best = dist[s.id]; u = s; }
                if (u == null) return null;
                if (u == to) break;
                done[u.id] = true;
                foreach (var v in u.lanes)
                {
                    float d = best + Vector2.Distance(u.pos, v.pos);
                    if (d < dist[v.id]) { dist[v.id] = d; prev[v.id] = u; }
                }
            }
            var path = new List<StarSystem>();
            for (var s = to; s != from; s = prev[s.id]) path.Add(s);
            path.Reverse();
            return path;
        }

        public float PathDays(Fleet f)
        {
            if (f.next == null) return 0;
            float sp = FleetSpeed(f);
            float d = Vector2.Distance(f.system.pos, f.next.pos) * (1f - f.progress);
            var last = f.next;
            foreach (var s in f.path) { d += Vector2.Distance(last.pos, s.pos); last = s; }
            return d / Mathf.Max(0.01f, sp);
        }

        public bool HasHostileFleets(StarSystem s, Empire e)
        {
            foreach (var f in g.fleets)
                if (!f.InTransit && f.system == s && f.owner != e) return true;
            return false;
        }

        public List<Fleet> FleetsAt(StarSystem s)
        {
            var list = new List<Fleet>();
            foreach (var f in g.fleets)
                if (!f.InTransit && f.system == s) list.Add(f);
            return list;
        }

        // ==================================================================
        //  Batailles
        // ==================================================================
        void CheckBattles()
        {
            if (pendingBattle != null) return;
            var owners = new List<Empire>();
            foreach (var s in g.systems)
            {
                owners.Clear();
                foreach (var f in g.fleets)
                    if (!f.InTransit && f.system == s && !owners.Contains(f.owner)) owners.Add(f.owner);
                if (owners.Count < 2) continue;

                if (owners.Contains(g.player))
                {
                    var b = new Battle { system = s };
                    foreach (var f in g.fleets)
                    {
                        if (f.InTransit || f.system != s) continue;
                        if (f.owner == g.player) b.playerFleets.Add(f);
                        else b.enemyFleets.Add(f);
                    }
                    b.enemy = b.enemyFleets[0].owner;
                    foreach (var f in b.playerFleets) f.path.Clear();
                    foreach (var f in b.enemyFleets) f.path.Clear();
                    pendingBattle = b;
                    return;
                }

                // IA contre IA : résolution automatique
                int guard = 0;
                while (owners.Count >= 2 && guard++ < 8)
                {
                    var a = new List<Fleet>();
                    var bList = new List<Fleet>();
                    foreach (var f in g.fleets)
                    {
                        if (f.InTransit || f.system != s) continue;
                        if (f.owner == owners[0]) a.Add(f);
                        else if (f.owner == owners[1]) bList.Add(f);
                    }
                    AutoResolve.Run(a, bList);
                    CleanupFleets();
                    owners.Clear();
                    foreach (var f in g.fleets)
                        if (!f.InTransit && f.system == s && !owners.Contains(f.owner)) owners.Add(f.owner);
                }
            }
        }

        /// Retire les vaisseaux détruits et les flottes vides.
        public void CleanupFleets()
        {
            foreach (var f in g.fleets) f.ships.RemoveAll(s => s.destroyed || s.hull <= 0);
            g.fleets.RemoveAll(f => f.ships.Count == 0);
        }

        /// Applique le résultat d'une bataille impliquant le joueur.
        public void FinishBattle(Battle b, BattleOutcome outcome, int enemyLosses, int playerLosses)
        {
            CleanupFleets();
            if (outcome == BattleOutcome.Retreat)
            {
                foreach (var f in b.playerFleets)
                {
                    if (!g.fleets.Contains(f)) continue;
                    var dest = RetreatTarget(f, b.system);
                    if (dest != null)
                    {
                        f.path.Clear();
                        f.next = dest;
                        f.progress = 0;
                    }
                }
                Notify("Vos flottes battent en retraite depuis " + b.system.name + ".", Palette.Warning, b.system);
            }
            else if (outcome == BattleOutcome.Victory)
            {
                Notify("Victoire à " + b.system.name + " : " + enemyLosses + " vaisseau(x) ennemi(s) détruit(s), " + playerLosses + " perdu(s).", Palette.Good, b.system);
            }
            else
            {
                Notify("Défaite à " + b.system.name + " : " + playerLosses + " vaisseau(x) perdu(s).", Palette.Bad, b.system);
            }
            pendingBattle = null;
            UpdateSensors();
        }

        StarSystem RetreatTarget(Fleet f, StarSystem from)
        {
            StarSystem best = null;
            foreach (var n in from.lanes)
                if (n.owner == f.owner && !HasHostileFleets(n, f.owner)) { best = n; break; }
            if (best == null && f.cameFrom != null && f.cameFrom != from && from.lanes.Contains(f.cameFrom)) best = f.cameFrom;
            if (best == null && from.lanes.Count > 0) best = from.lanes[0];
            return best;
        }

        // ==================================================================
        //  Visibilité (capteurs du joueur)
        // ==================================================================
        public void UpdateSensors()
        {
            sensors.Clear();
            var p = g.player;
            foreach (var s in g.systems)
            {
                if (s.owner != p) continue;
                sensors.Add(s);
                foreach (var n in s.lanes) sensors.Add(n);
            }
            foreach (var f in g.fleets)
            {
                if (f.owner != p) continue;
                sensors.Add(f.system);
                foreach (var n in f.system.lanes) sensors.Add(n);
                if (f.next != null) sensors.Add(f.next);
            }
            foreach (var s in sensors) p.explored.Add(s);
        }

        public bool IsFleetVisible(Fleet f)
        {
            if (f.owner == g.player) return true;
            return sensors.Contains(f.system) || (f.next != null && sensors.Contains(f.next));
        }

        // ==================================================================
        public void Notify(string text, Color c, StarSystem s)
        {
            notices.Add(new Notice { text = text, color = c, system = s, time = Time.unscaledTime });
            if (notices.Count > 40) notices.RemoveAt(0);
            Sfx.Notify();
        }
    }
}
