using System.Collections.Generic;
using UnityEngine;

namespace StellarisMini
{
    /// Projectile (laser ou missile) dans l'arène.
    public class Projectile
    {
        public Vector2 pos, vel;
        public float damage, life, trail;
        public int team;
        public bool missile;
        public CombatShip owner, target;
        public Transform tf;
        public SpriteRenderer sr;
    }

    /// Une bataille pilotée : le joueur contrôle le vaisseau amiral, l'IA dirige le reste.
    /// Si le vaisseau du joueur est détruit, le commandement passe à un autre vaisseau de la flotte.
    public class CombatSession : ICombat
    {
        public const float ArenaRadius = 75f;

        readonly GameManager gm;
        readonly Battle battle;
        public readonly GameObject root;
        public readonly List<CombatShip> ships = new List<CombatShip>();
        readonly List<Projectile> bolts = new List<Projectile>();
        readonly Stack<Projectile> pool = new Stack<Projectile>();
        public CombatShip player;

        enum Phase { Intro, Fight, Ending, Result }
        Phase phase = Phase.Intro;
        float phaseTime;
        float transferTimer = -1f;
        float switchCooldown;
        bool paused, helpOpen;
        BattleOutcome outcome;
        int playerLost, enemyLost;
        string banner;
        float bannerTime;
        Vector2 camPos, lastFocus;
        float camDist = 46f;
        float shake;

        public bool WeaponsFree { get { return phase != Phase.Intro || phaseTime > 1.6f; } }
        public bool HidesCursor { get { return !paused && phase != Phase.Result; } }
        public Vector2 CamPos { get { return camPos; } }
        public Vector2 MouseWorld { get; private set; }

        public CombatSession(GameManager manager, Battle b)
        {
            gm = manager;
            battle = b;
            root = new GameObject("Combat");
            root.transform.SetParent(gm.transform, false);
            BuildBackdrop();

            var mine = new List<KeyValuePair<ShipData, Empire>>();
            var theirs = new List<KeyValuePair<ShipData, Empire>>();
            foreach (var f in b.playerFleets) foreach (var s in f.ships) if (!s.destroyed) mine.Add(new KeyValuePair<ShipData, Empire>(s, f.owner));
            foreach (var f in b.enemyFleets) foreach (var s in f.ships) if (!s.destroyed) theirs.Add(new KeyValuePair<ShipData, Empire>(s, f.owner));
            Spawn(mine, 0, -1f);
            Spawn(theirs, 1, 1f);

            var flagData = new List<ShipData>();
            foreach (var kv in mine) flagData.Add(kv.Key);
            var flag = Flagship(flagData);
            foreach (var s in ships) if (s.data == flag) { SetPlayer(s, false); break; }
            if (player == null) foreach (var s in ships) if (s.team == 0) { SetPlayer(s, false); break; }

            if (player != null) { camPos = player.pos; lastFocus = player.pos; }
            banner = "Bataille de " + b.system.name;
            bannerTime = 2.5f;
            Sfx.Alert();
        }

        /// Vaisseau amiral : la plus grosse classe, puis la coque la plus solide.
        public static ShipData Flagship(List<ShipData> list)
        {
            ShipData best = null;
            foreach (var s in list)
            {
                if (s.destroyed) continue;
                if (best == null || s.cls > best.cls || (s.cls == best.cls && s.hull > best.hull)) best = s;
            }
            return best;
        }

        void BuildBackdrop()
        {
            var t = root.transform;
            int seed = battle.system.id * 31 + 5;
            Gfx.Starfield(t, 2200, 80f, 30f, 280f, seed);
            Gfx.Nebulae(t, 7, 140f, 240f, seed + 3);

            // L'étoile du système, au loin
            var starCol = Palette.Star(battle.system.star);
            var star = Gfx.MakeSpriteObj("Star", t, Gfx.GlowSprite, starCol, 160f, -80, true);
            star.transform.localPosition = new Vector3(110f, 70f, 200f);
            var core = Gfx.MakeSpriteObj("StarCore", t, Gfx.GlowSprite, Color.Lerp(starCol, Color.white, 0.7f), 45f, -79, true);
            core.transform.localPosition = new Vector3(110f, 70f, 199f);

            // Une planète du système en toile de fond
            if (battle.system.planets.Count > 0)
            {
                var p = battle.system.planets[0];
                var pc = Palette.Planet(p.type) * 0.55f;
                pc.a = 1f;
                var atmo = Gfx.MakeSpriteObj("Atmosphere", t, Gfx.GlowSprite, Palette.WithAlpha(Palette.Planet(p.type), 0.5f), 70f, -71, true);
                atmo.transform.localPosition = new Vector3(-70f, -45f, 120f);
                var disc = Gfx.MakeSpriteObj("Planet", t, Gfx.DiscSprite, pc, 48f, -70, false);
                disc.transform.localPosition = new Vector3(-70f, -45f, 119f);
                var shadow = Gfx.MakeSpriteObj("Shadow", t, Gfx.DiscSprite, new Color(0, 0, 0, 0.6f), 46f, -69, false);
                shadow.transform.localPosition = new Vector3(-62f, -52f, 118.5f);
            }

            var ring = Gfx.MakeLine("Arena", t, new Color(0.4f, 0.6f, 1f, 0.18f), 0.35f, -10);
            Gfx.SetCircle(ring, Vector2.zero, ArenaRadius, 96, 0.5f);
        }

        void Spawn(List<KeyValuePair<ShipData, Empire>> list, int team, float side)
        {
            for (int c = 0; c < 3; c++)
            {
                var group = new List<KeyValuePair<ShipData, Empire>>();
                foreach (var kv in list) if ((int)kv.Key.cls == c) group.Add(kv);
                float baseX = c == 0 ? 26f : c == 1 ? 33f : 41f;
                float spacing = c == 0 ? 3.5f : c == 1 ? 5f : 7f;
                const int perColumn = 8;
                for (int i = 0; i < group.Count; i++)
                {
                    int col = i / perColumn;
                    int row = i % perColumn;
                    int inCol = Mathf.Min(perColumn, group.Count - col * perColumn);
                    float y = (row - (inCol - 1) / 2f) * spacing;
                    float x = side * (baseX + col * 4f);
                    var ship = new CombatShip(this, group[i].Key, group[i].Value, team, new Vector2(x, y), side < 0 ? 0f : 180f);
                    ships.Add(ship);
                }
            }
        }

        void SetPlayer(CombatShip s, bool announce)
        {
            if (player != null) player.SetPlayer(false);
            player = s;
            if (s == null) return;
            s.SetPlayer(true);
            lastFocus = s.pos;
            if (announce)
            {
                banner = "Commandement transféré : " + Names.Ships[(int)s.spec.cls] + " « " + s.data.name + " »";
                bannerTime = 2.5f;
                Sfx.Confirm();
            }
        }

        // ==================================================================
        //  Boucle
        // ==================================================================
        public void Update()
        {
            float dt = Time.deltaTime;
            float udt = Time.unscaledDeltaTime;
            phaseTime += udt;
            bannerTime -= udt;
            shake = Mathf.Max(0f, shake - udt * 2.5f);

            if (phase != Phase.Result)
            {
                if (helpOpen)
                {
                    if (GameInput.Back || GameInput.PadDown(Pad.View)) helpOpen = false;
                    return;
                }
                if (!paused && (GameInput.PadDown(Pad.Start) || GameInput.KeyDown(KeyId.Escape)))
                {
                    SetPaused(true);
                    return;
                }
                if (paused)
                {
                    if (GameInput.PadDown(Pad.Start) || GameInput.Back) SetPaused(false);
                    return;
                }
            }

            if (!GameInput.UsingGamepad) MouseWorld = ScreenToWorld(GameInput.MousePosition);

            if (phase == Phase.Intro && phaseTime > 1.6f)
            {
                phase = Phase.Fight;
                banner = "Engagez l'ennemi !";
                bannerTime = 1.5f;
            }

            // Changement manuel de vaisseau
            switchCooldown -= udt;
            if ((phase == Phase.Intro || phase == Phase.Fight) && player != null && switchCooldown <= 0f &&
                (GameInput.KeyDown(KeyId.Tab) || GameInput.PadDown(Pad.Y)))
            {
                var next = NextAlly(player);
                if (next != null && next != player)
                {
                    SetPlayer(next, true);
                    switchCooldown = 0.4f;
                }
            }

            // Transfert du commandement après la destruction du vaisseau du joueur
            if (transferTimer > 0f)
            {
                transferTimer -= udt;
                if (transferTimer <= 0f)
                {
                    var n = BestAlly();
                    if (n != null) SetPlayer(n, true);
                }
            }

            for (int i = 0; i < ships.Count; i++) ships[i].Tick(dt);
            UpdateProjectiles(dt);

            if (phase == Phase.Intro || phase == Phase.Fight) CheckEnd();
            else if (phase == Phase.Ending && phaseTime > 1.6f)
            {
                phase = Phase.Result;
                Time.timeScale = 1f;
                UI.ResetFocus();
            }
            else if (phase == Phase.Result && GameInput.KeyDown(KeyId.Enter))
            {
                Finish();
            }
        }

        public void LateUpdate()
        {
            float udt = Time.unscaledDeltaTime;
            Vector2 focus = lastFocus;
            if (player != null && player.alive)
            {
                lastFocus = player.pos;
                Vector2 ahead;
                if (GameInput.UsingGamepad) ahead = player.Forward * 6f + player.vel * 0.25f;
                else ahead = Vector2.ClampMagnitude((MouseWorld - player.pos) * 0.25f, 12f);
                focus = player.pos + ahead;
            }
            camPos = Vector2.Lerp(camPos, focus, 1f - Mathf.Exp(-6f * udt));
            float target = 46f;
            if (player != null)
            {
                target += player.vel.magnitude * 0.5f;
                if (player.spec.cls == ShipClass.Croiseur) target += 8f;
            }
            camDist = Mathf.Lerp(camDist, target, 1f - Mathf.Exp(-2f * udt));
            Vector2 sh = shake > 0f && !paused ? Random.insideUnitCircle * shake * 0.6f : Vector2.zero;
            gm.Cam.transform.SetPositionAndRotation(new Vector3(camPos.x + sh.x, camPos.y + sh.y, -camDist), Quaternion.identity);
        }

        void SetPaused(bool p)
        {
            paused = p;
            Time.timeScale = p ? 0f : (phase == Phase.Ending ? 0.35f : 1f);
            if (p) { GameInput.StopRumble(); UI.ResetFocus(); }
        }

        void CheckEnd()
        {
            int a = Alive(0), e = Alive(1);
            if (e == 0 || a == 0)
            {
                outcome = e == 0 ? BattleOutcome.Victory : BattleOutcome.Defeat;
                phase = Phase.Ending;
                phaseTime = 0f;
                Time.timeScale = 0.35f;
                banner = e == 0 ? "VICTOIRE" : "DÉFAITE";
                bannerTime = 2f;
                transferTimer = -1f;
                if (e == 0) Sfx.Confirm();
            }
        }

        public void OnShipDestroyed(CombatShip s)
        {
            if (s.team == 0) playerLost++;
            else enemyLost++;
            if (s == player)
            {
                player = null;
                s.SetPlayer(false);
                GameInput.Rumble(0.9f, 0.9f, 0.6f);
                AddShake(1.5f);
                if (Alive(0) > 0)
                {
                    transferTimer = 1.4f;
                    banner = "Vaisseau amiral détruit !";
                    bannerTime = 1.4f;
                }
            }
        }

        public int Alive(int team)
        {
            int n = 0;
            foreach (var s in ships) if (s.alive && s.team == team) n++;
            return n;
        }

        CombatShip BestAlly()
        {
            CombatShip best = null;
            foreach (var s in ships)
            {
                if (!s.alive || s.team != 0) continue;
                if (best == null || s.spec.cls > best.spec.cls || (s.spec.cls == best.spec.cls && s.hull > best.hull)) best = s;
            }
            return best;
        }

        CombatShip NextAlly(CombatShip current)
        {
            var allies = new List<CombatShip>();
            foreach (var s in ships) if (s.alive && s.team == 0) allies.Add(s);
            if (allies.Count == 0) return null;
            allies.Sort((x, y) => y.spec.cls != x.spec.cls ? y.spec.cls.CompareTo(x.spec.cls) : y.hull.CompareTo(x.hull));
            int i = allies.IndexOf(current);
            return allies[(i + 1) % allies.Count];
        }

        // ==================================================================
        //  Recherche de cibles
        // ==================================================================
        public CombatShip FindTarget(CombatShip self)
        {
            CombatShip best = null;
            float bestScore = float.MaxValue;
            foreach (var s in ships)
            {
                if (!s.alive || s.team == self.team) continue;
                float score = (s.pos - self.pos).magnitude + Random.Range(0f, 6f);
                if (s.IsPlayer) score -= gm.Galaxy.settings.Diff.playerFocus;
                if (self.spec.cls == ShipClass.Chasseur && s.spec.cls == ShipClass.Chasseur) score -= 5f;
                if (self.spec.cls == ShipClass.Croiseur && s.spec.cls == ShipClass.Croiseur) score -= 5f;
                if (score < bestScore) { bestScore = score; best = s; }
            }
            return best;
        }

        public CombatShip FindNearestEnemy(Vector2 p, int team, float range)
        {
            CombatShip best = null;
            float bestD = range * range;
            foreach (var s in ships)
            {
                if (!s.alive || s.team == team) continue;
                float d = (s.pos - p).sqrMagnitude;
                if (d < bestD) { bestD = d; best = s; }
            }
            return best;
        }

        /// Ennemi le plus proche de l'axe de tir (visée assistée, missiles du joueur).
        public CombatShip FindAimTarget(CombatShip self, float coneDeg, float range)
        {
            CombatShip best = null;
            float bestA = coneDeg;
            Vector2 fwd = self.Forward;
            foreach (var s in ships)
            {
                if (!s.alive || s.team == self.team) continue;
                Vector2 to = s.pos - self.pos;
                if (to.sqrMagnitude > range * range) continue;
                float a = Vector2.Angle(fwd, to);
                if (a < bestA) { bestA = a; best = s; }
            }
            return best;
        }

        // ==================================================================
        //  Projectiles
        // ==================================================================
        Projectile NewProjectile()
        {
            if (pool.Count > 0)
            {
                var p = pool.Pop();
                p.tf.gameObject.SetActive(true);
                return p;
            }
            var sr = Gfx.MakeSpriteObj("Bolt", root.transform, Gfx.GlowSprite, Color.white, 1f, 30, true);
            return new Projectile { tf = sr.transform, sr = sr };
        }

        public void SpawnBolt(CombatShip owner, Vector2 origin, Vector2 velocity, float damage, float life, float width)
        {
            var p = NewProjectile();
            p.pos = origin;
            p.vel = velocity;
            p.damage = damage;
            p.life = life;
            p.team = owner.team;
            p.owner = owner;
            p.missile = false;
            p.target = null;
            p.sr.color = owner.boltColor;
            float len = Mathf.Clamp(velocity.magnitude * 0.05f, 1.2f, 3f) * (0.7f + width);
            p.tf.localScale = new Vector3(width * 2.6f, len, 1f);
            PlaceProjectile(p);
            bolts.Add(p);
        }

        public void SpawnMissile(CombatShip owner, Vector2 origin, Vector2 velocity, float damage, CombatShip target)
        {
            var p = NewProjectile();
            p.pos = origin;
            p.vel = velocity;
            p.damage = damage;
            p.life = 4.5f;
            p.team = owner.team;
            p.owner = owner;
            p.missile = true;
            p.target = target;
            p.trail = 0f;
            p.sr.color = new Color(1f, 0.75f, 0.35f);
            p.tf.localScale = new Vector3(0.7f, 1.4f, 1f);
            PlaceProjectile(p);
            bolts.Add(p);
        }

        static void PlaceProjectile(Projectile p)
        {
            p.tf.localPosition = new Vector3(p.pos.x, p.pos.y, -0.1f);
            p.tf.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(p.vel.y, p.vel.x) * Mathf.Rad2Deg - 90f);
        }

        void Recycle(int i)
        {
            var p = bolts[i];
            int last = bolts.Count - 1;
            bolts[i] = bolts[last];
            bolts.RemoveAt(last);
            p.tf.gameObject.SetActive(false);
            p.owner = null;
            p.target = null;
            pool.Push(p);
        }

        static float SegmentDistSq(Vector2 a, Vector2 b, Vector2 c)
        {
            Vector2 ab = b - a;
            float len2 = ab.sqrMagnitude;
            float t = len2 > 1e-6f ? Mathf.Clamp01(Vector2.Dot(c - a, ab) / len2) : 0f;
            return (a + ab * t - c).sqrMagnitude;
        }

        void UpdateProjectiles(float dt)
        {
            if (dt <= 0f) return;
            for (int i = bolts.Count - 1; i >= 0; i--)
            {
                var p = bolts[i];
                p.life -= dt;

                if (p.missile)
                {
                    float sp = Mathf.Min(p.vel.magnitude + 30f * dt, 30f);
                    Vector2 dir = p.vel.sqrMagnitude > 0.001f ? p.vel.normalized : Vector2.right;
                    if (p.target != null && p.target.alive)
                    {
                        Vector2 want = (p.target.pos - p.pos).normalized;
                        Vector3 r = Vector3.RotateTowards(dir, want, 200f * Mathf.Deg2Rad * dt, 0f);
                        dir = new Vector2(r.x, r.y).normalized;
                    }
                    p.vel = dir * sp;
                    p.trail -= dt;
                    if (p.trail <= 0f)
                    {
                        p.trail = 0.025f;
                        Fx.Spark(p.pos - dir * 0.6f, -dir * 3f + Random.insideUnitCircle, new Color(1f, 0.6f, 0.3f, 0.7f), 0.5f, 0.35f);
                    }
                }

                Vector2 prev = p.pos;
                p.pos += p.vel * dt;

                CombatShip hit = null;
                for (int k = 0; k < ships.Count; k++)
                {
                    var s = ships[k];
                    if (!s.alive || s.team == p.team) continue;
                    float r = s.spec.radius * 0.9f;
                    if (SegmentDistSq(prev, p.pos, s.pos) < r * r) { hit = s; break; }
                }
                if (hit != null)
                {
                    var n = p.vel.sqrMagnitude > 0.001f ? -p.vel.normalized : Vector2.up;
                    hit.Damage(p.damage, p.pos, n, p.owner);
                    if (p.missile) Fx.Explosion(p.pos, 0.35f, new Color(1f, 0.6f, 0.3f));
                    Recycle(i);
                    continue;
                }
                if (p.life <= 0f)
                {
                    if (p.missile) Fx.Glow(p.pos, Vector2.zero, new Color(1f, 0.6f, 0.3f), 1.5f, 0.2f);
                    Recycle(i);
                    continue;
                }
                PlaceProjectile(p);
            }
        }

        // ==================================================================
        //  Utilitaires
        // ==================================================================
        public Vector2 ScreenToWorld(Vector2 sp)
        {
            var ray = gm.Cam.ScreenPointToRay(new Vector3(sp.x, sp.y, 0f));
            if (Mathf.Abs(ray.direction.z) < 1e-5f) return camPos;
            float t = -ray.origin.z / ray.direction.z;
            var p = ray.origin + ray.direction * t;
            return new Vector2(p.x, p.y);
        }

        Vector2 WorldToGui(Vector2 w, out bool inFront)
        {
            var sp = gm.Cam.WorldToScreenPoint(new Vector3(w.x, w.y, 0f));
            inFront = sp.z > 0;
            return new Vector2(sp.x, Screen.height - sp.y);
        }

        /// Multiplicateur de dégâts d'un camp selon la difficulté (0 = joueur, 1 = ennemis).
        public float TeamDamageMul(int team)
        {
            var d = gm.Galaxy.settings.Diff;
            return team == 0 ? d.playerDamage : d.enemyDamage;
        }

        public float VolumeAt(Vector2 p)
        {
            return Mathf.Clamp01(1f - (p - camPos).magnitude / 70f);
        }

        public void AddShake(float a)
        {
            shake = Mathf.Min(2f, Mathf.Max(shake, a));
        }

        // ==================================================================
        //  Fin de bataille
        // ==================================================================
        void WriteBackHulls()
        {
            foreach (var s in ships)
            {
                if (!s.alive) continue;
                s.data.hull = Mathf.Max(1f, s.hull);
                s.data.destroyed = false;
            }
        }

        void Retreat()
        {
            outcome = BattleOutcome.Retreat;
            Finish();
        }

        void AutoResolveNow()
        {
            var a = new List<AutoResolve.Unit>();
            var b = new List<AutoResolve.Unit>();
            foreach (var s in ships)
            {
                if (!s.alive) continue;
                var u = AutoResolve.MakeUnit(s.data, s.empire);
                u.dps *= TeamDamageMul(s.team);
                u.hull = s.hull;
                u.shield = s.shield;
                if (s.team == 0) a.Add(u); else b.Add(u);
            }
            bool win = AutoResolve.Run(a, b);
            foreach (var s in ships)
            {
                if (!s.alive) continue;
                if (s.data.destroyed)
                {
                    s.Die();
                }
                else s.hull = s.data.hull;
            }
            outcome = win ? BattleOutcome.Victory : BattleOutcome.Defeat;
            paused = false;
            phase = Phase.Result;
            transferTimer = -1f;
            Time.timeScale = 1f;
            UI.ResetFocus();
        }

        void Finish()
        {
            WriteBackHulls();
            gm.EndBattle(battle, outcome, enemyLost, playerLost);
        }

        public void Dispose()
        {
            if (root != null) Object.Destroy(root);
            Time.timeScale = 1f;
            GameInput.StopRumble();
        }

        // ==================================================================
        //  Interface de combat
        // ==================================================================
        public void OnGUI()
        {
            if (phase != Phase.Result && !paused)
            {
                DrawShipBars();
                DrawOffscreen();
                CombatHud.TopInfo(battle.system.name, Alive(0), Alive(1), gm.Galaxy.player.color, battle.enemy.color, null);
                bool alive = player != null && player.alive;
                if (alive) CombatHud.PlayerPanel(true, player.spec, player.data.name, player.hull, player.maxHull, player.shield, player.maxShield, player.boost, player.MissileReady, null);
                else CombatHud.PlayerPanel(false, null, null, 0, 1, 0, 1, 0, 0, null);
                DrawCrosshair();
                CombatHud.Hints(GameInput.UsingGamepad
                    ? "[Stick G] Propulsion   [Stick D] Viser   [RT] Canons   [LT] Missiles   [A/LB] Postcombustion   [Y] Changer de vaisseau   [Start] Pause"
                    : "[ZQSD] Propulsion   [Souris] Viser   [Clic G] Canons   [Clic D] Missiles   [Maj] Postcombustion   [Tab] Changer de vaisseau   [Échap] Pause");
            }
            CombatHud.Banner(banner, bannerTime, phase == Phase.Ending, outcome == BattleOutcome.Victory);

            if (paused)
            {
                if (helpOpen) { if (Help.Draw()) helpOpen = false; }
                else
                {
                    switch (CombatHud.PauseMenu())
                    {
                        case PauseChoice.Resume: SetPaused(false); break;
                        case PauseChoice.Retreat: Retreat(); return;
                        case PauseChoice.AutoResolve: AutoResolveNow(); return;
                        case PauseChoice.Help: helpOpen = true; UI.ResetFocus(); break;
                    }
                }
            }
            else if (phase == Phase.Result)
            {
                if (CombatHud.Result(outcome == BattleOutcome.Victory, enemyLost, playerLost)) Finish();
            }
        }

        void DrawShipBars()
        {
            float s = UI.S;
            foreach (var sh in ships)
            {
                if (!sh.alive || sh.IsPlayer) continue;
                bool front;
                var gp = WorldToGui(sh.pos + new Vector2(0f, sh.spec.radius + 1.4f), out front);
                if (!front || gp.x < 0 || gp.y < 0 || gp.x > Screen.width || gp.y > Screen.height) continue;
                float w = (24f + sh.spec.radius * 10f) * s;
                CombatHud.ShipBars(gp, w, sh.hull / sh.maxHull, sh.maxShield > 0f ? sh.shield / sh.maxShield : 0f, sh.team == 0);
            }
        }

        void DrawOffscreen()
        {
            float s = UI.S;
            foreach (var sh in ships)
            {
                if (!sh.alive || sh.team == 0) continue;
                bool front;
                var gp = WorldToGui(sh.pos, out front);
                float size = (sh.spec.cls == ShipClass.Croiseur ? 16f : sh.spec.cls == ShipClass.Corvette ? 12f : 9f) * s;
                CombatHud.Offscreen(gp, front, size, sh.empire.color);
            }
        }

        void DrawCrosshair()
        {
            if (GameInput.UsingGamepad || player == null) return;
            var m = GameInput.MousePosition;
            CombatHud.Cross(new Vector2(m.x, Screen.height - m.y), 12f, new Color(1f, 1f, 1f, 0.85f));
        }
    }
}
