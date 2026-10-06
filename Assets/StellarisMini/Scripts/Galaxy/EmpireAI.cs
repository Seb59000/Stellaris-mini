using System.Collections.Generic;
using UnityEngine;

namespace StellarisMini
{
    /// IA simple d'un empire rival : expansion, économie, construction et guerre.
    public class EmpireAI
    {
        readonly Empire e;
        readonly GalaxySim sim;
        Galaxy G { get { return sim.g; } }
        bool repairing;

        public EmpireAI(Empire empire, GalaxySim simulation)
        {
            e = empire;
            sim = simulation;
        }

        /// Jours de paix au début de la partie, selon la difficulté.
        int GraceDays
        {
            get { return G.settings.difficulty == 0 ? 900 : G.settings.difficulty == 2 ? 480 : 660; }
        }

        public void Think()
        {
            Expand();
            BuildShips();
            Military();
        }

        public void Monthly()
        {
            ChooseResearch();
            ChooseSpecializations();
        }

        // ------------------------------------------------------------------
        void ChooseResearch()
        {
            if (e.currentResearch >= 0) return;
            int best = -1, bestLevel = int.MaxValue;
            for (int t = 0; t < 4; t++)
            {
                int lvl = e.tech[t] * 10 + Random.Range(0, 8);
                if (e.tech[t] < GalaxySim.MaxTech && lvl < bestLevel) { bestLevel = lvl; best = t; }
            }
            e.currentResearch = best;
        }

        void ChooseSpecializations()
        {
            var cols = new List<StarSystem>();
            foreach (var s in G.systems)
                if (s.owner == e && s.colony != null && !s.colony.capital) cols.Add(s);
            var order = new[] { Specialization.Mines, Specialization.Industrie, Specialization.Recherche, Specialization.Energie };
            for (int i = 0; i < cols.Count; i++) cols[i].colony.spec = order[i % order.Length];
            if (e.capital != null && e.capital.colony != null) e.capital.colony.spec = Specialization.Industrie;

            if (e.lastIncome.energy < 2f && cols.Count > 0)
            {
                // Corrige un déficit d'énergie
                StarSystem big = cols[0];
                foreach (var s in cols) if (s.colony.pop > big.colony.pop) big = s;
                big.colony.spec = Specialization.Energie;
            }
        }

        // ------------------------------------------------------------------
        void Expand()
        {
            if (e.claims.Count >= GalaxySim.MaxClaims) return;
            StarSystem best = null;
            float bestScore = float.MinValue;
            foreach (var s in G.systems)
            {
                if (s.owner != null || sim.IsBeingClaimed(s)) continue;
                bool adjacent = false;
                foreach (var n in s.lanes) if (n.owner == e) { adjacent = true; break; }
                if (!adjacent || sim.HasHostileFleets(s, e)) continue;

                int m, en, d;
                GalaxySim.ClaimCost(s.Habitable, out m, out en, out d);
                if (e.minerals < m || e.energy < en) continue;

                float score = s.Habitable ? 10f + s.MaxPop * 3f : 4f + s.planets.Count;
                if (e.capital != null) score -= Vector2.Distance(s.pos, e.capital.pos) * 0.1f;
                score += Random.Range(0f, 3f);
                if (score > bestScore) { bestScore = score; best = s; }
            }
            if (best != null) sim.StartClaim(e, best);
        }

        void BuildShips()
        {
            int queued = 0;
            foreach (var s in G.systems)
                if (s.owner == e && s.colony != null) queued += s.colony.queue.Count;
            if (queued >= 2) return;
            if (e.lastIncome.energy < 1f) return; // évite de couler l'économie

            StarSystem yard = e.capital;
            if (yard == null || yard.colony == null)
            {
                foreach (var s in G.systems) if (s.owner == e && s.colony != null) { yard = s; break; }
            }
            if (yard == null) return;

            ShipClass c;
            float r = Random.value;
            if (e.alloys >= 150 && G.day > 300 && r < 0.45f) c = ShipClass.Croiseur;
            else if (e.alloys >= 60 && r < 0.7f) c = ShipClass.Corvette;
            else c = ShipClass.Chasseur;
            sim.QueueShip(e, yard, c);
        }

        // ------------------------------------------------------------------
        void Military()
        {
            var mine = new List<Fleet>();
            foreach (var f in G.fleets) if (f.owner == e) mine.Add(f);
            if (mine.Count == 0) return;

            // Flotte principale = la plus puissante
            Fleet main = mine[0];
            foreach (var f in mine) if (sim.FleetPower(f) > sim.FleetPower(main)) main = f;

            // Regroupement des autres flottes sur la flotte principale
            foreach (var f in mine)
            {
                if (f == main) continue;
                if (!f.InTransit && !main.InTransit && f.system == main.system) { sim.Merge(main, f); continue; }
                if (!f.InTransit && !main.InTransit && f.Destination == null) sim.OrderMove(f, main.system);
            }
            if (main.InTransit) return;

            float power = sim.FleetPower(main);

            // Réparations
            if (main.HullRatio < 0.45f) repairing = true;
            if (repairing)
            {
                if (main.HullRatio > 0.85f) repairing = false;
                else
                {
                    var home = NearestOwned(main.system);
                    if (home != null && home != main.system) sim.OrderMove(main, home);
                    return;
                }
            }

            // Défense : un de nos systèmes est assiégé ?
            StarSystem threatened = null;
            float threat = 0;
            foreach (var s in G.systems)
            {
                if (s.owner != e) continue;
                float p = HostilePowerAt(s);
                if (p > threat) { threat = p; threatened = s; }
            }
            if (threatened != null && power >= threat * 0.8f)
            {
                if (main.system != threatened) sim.OrderMove(main, threatened);
                return;
            }

            // On reste sur place si on assiège un système ennemi
            if (main.system.owner != null && main.system.owner != e) return;

            // Attaque
            if (G.day < GraceDays) return;
            float minPower = 55f + G.day / 30f * 1.2f;
            if (power < minPower) return;

            StarSystem target = null;
            float bestScore = float.MaxValue;
            foreach (var s in G.systems)
            {
                if (s.owner == null || s.owner == e || !s.owner.alive) continue;
                var path = sim.FindPath(main.system, s);
                if (path == null) continue;
                float defense = HostilePowerAt(s);
                foreach (var n in s.lanes) defense += HostilePowerAt(n) * 0.5f;
                if (power < defense * 1.25f) continue;
                float score = path.Count * 10f + defense * 0.2f - (s.owner.isPlayer ? 8f : 0f) - (s.colony == null ? 5f : 0f);
                if (score < bestScore) { bestScore = score; target = s; }
            }
            if (target != null) sim.OrderMove(main, target);
        }

        float HostilePowerAt(StarSystem s)
        {
            float p = 0;
            foreach (var f in G.fleets)
                if (f.owner != e && !f.InTransit && f.system == s) p += sim.FleetPower(f);
            return p;
        }

        StarSystem NearestOwned(StarSystem from)
        {
            if (from.owner == e) return from;
            StarSystem best = null;
            int bestLen = int.MaxValue;
            foreach (var s in G.systems)
            {
                if (s.owner != e) continue;
                var p = sim.FindPath(from, s);
                if (p != null && p.Count < bestLen) { bestLen = p.Count; best = s; }
            }
            return best;
        }
    }
}
