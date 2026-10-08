using System.Collections.Generic;
using UnityEngine;

namespace StellarisMini
{
    /// Résolution automatique d'une bataille (IA contre IA, ou si le joueur ne veut pas piloter).
    /// Utilise les mêmes caractéristiques que le combat piloté.
    public static class AutoResolve
    {
        public class Unit
        {
            public ShipData data;
            public Empire empire;
            public float hull, shield, maxShield, dps;
            public bool Alive { get { return hull > 0; } }
        }

        public static Unit MakeUnit(ShipData d, Empire e)
        {
            var spec = d.Spec;
            var u = new Unit { data = d, empire = e, hull = d.hull };
            u.maxShield = spec.shield * e.ShieldMul;
            u.shield = u.maxShield;
            u.dps = spec.Dps * e.DamageMul;
            return u;
        }

        /// Fait combattre deux groupes de flottes jusqu'à la destruction de l'un d'eux.
        /// Renvoie true si le camp A l'emporte.
        /// mulA / mulB : multiplicateurs de dégâts de chaque camp (difficulté).
        public static bool Run(List<Fleet> sideA, List<Fleet> sideB, float mulA = 1f, float mulB = 1f)
        {
            var a = new List<Unit>();
            var b = new List<Unit>();
            foreach (var f in sideA) foreach (var s in f.ships) if (!s.destroyed) { var u = MakeUnit(s, f.owner); u.dps *= mulA; a.Add(u); }
            foreach (var f in sideB) foreach (var s in f.ships) if (!s.destroyed) { var u = MakeUnit(s, f.owner); u.dps *= mulB; b.Add(u); }
            return Run(a, b);
        }

        public static bool Run(List<Unit> a, List<Unit> b)
        {
            var rng = new System.Random(Random.Range(0, int.MaxValue));
            int steps = 0;
            while (AnyAlive(a) && AnyAlive(b) && steps < 5000)
            {
                steps++;
                Volley(rng, a, b);
                Volley(rng, b, a);
            }
            if (AnyAlive(a) && AnyAlive(b))
            {
                // Sécurité : le camp le plus abîmé perd
                if (TotalHull(a) >= TotalHull(b)) KillAll(b); else KillAll(a);
            }
            WriteBack(a);
            WriteBack(b);
            return AnyAlive(a);
        }

        static void Volley(System.Random rng, List<Unit> attackers, List<Unit> defenders)
        {
            foreach (var at in attackers)
            {
                if (!at.Alive) continue;
                var target = RandomAlive(rng, defenders);
                if (target == null) return;
                float dmg = at.dps * (0.75f + (float)rng.NextDouble() * 0.5f);
                float absorbed = Mathf.Min(target.shield, dmg);
                target.shield -= absorbed;
                target.hull -= dmg - absorbed;
            }
        }

        static Unit RandomAlive(System.Random rng, List<Unit> list)
        {
            int alive = 0;
            foreach (var u in list) if (u.Alive) alive++;
            if (alive == 0) return null;
            int k = rng.Next(alive);
            foreach (var u in list)
            {
                if (!u.Alive) continue;
                if (k-- == 0) return u;
            }
            return null;
        }

        static bool AnyAlive(List<Unit> l)
        {
            foreach (var u in l) if (u.Alive) return true;
            return false;
        }

        static float TotalHull(List<Unit> l)
        {
            float h = 0;
            foreach (var u in l) if (u.Alive) h += u.hull;
            return h;
        }

        static void KillAll(List<Unit> l)
        {
            foreach (var u in l) u.hull = 0;
        }

        static void WriteBack(List<Unit> l)
        {
            foreach (var u in l)
            {
                u.data.hull = Mathf.Max(0, u.hull);
                u.data.destroyed = u.hull <= 0;
            }
        }
    }
}
