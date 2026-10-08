using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace StellarisMini
{
    /// Projectile (laser ou missile) de l'arène 3D, dessiné avec un trait lumineux.
    public class Projectile3D
    {
        public Vector3 pos, vel;
        public float damage, life, trail, length;
        public int team;
        public bool missile;
        public CombatShip3D owner, target;
        public LineRenderer lr;
    }

    /// Bataille pilotée en 3D, caméra de poursuite derrière le vaisseau du joueur.
    /// Mêmes règles qu'en vue de dessus : le joueur dirige le vaisseau amiral, l'IA le reste,
    /// et le commandement passe à un autre vaisseau si le sien est détruit.
    public class CombatSession3D : ICombat
    {
        public const float ArenaRadius = 150f;

        readonly GameManager gm;
        readonly Battle battle;
        public readonly GameObject root;
        public readonly List<CombatShip3D> ships = new List<CombatShip3D>();
        readonly List<Projectile3D> bolts = new List<Projectile3D>();
        readonly Stack<Projectile3D> pool = new Stack<Projectile3D>();
        public CombatShip3D player;

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

        // Caméra
        Vector3 camPos, lastFocus;
        Quaternion camRot = Quaternion.identity;
        float shake, orbitAngle;

        // Réglages de rendu à restaurer en sortie
        readonly AmbientMode savedAmbientMode;
        readonly Color savedAmbient;
        readonly float savedReflection, savedFov, savedNear, savedFar;

        public bool WeaponsFree { get { return phase != Phase.Intro || phaseTime > 1.6f; } }
        public bool HidesCursor { get { return !paused && phase != Phase.Result; } }
        public Vector3 CamPos { get { return camPos; } }
        public Quaternion CamRotation { get { return gm.Cam.transform.rotation; } }

        /// Position de la souris par rapport au centre de l'écran, comme un manche (-1..1).
        public Vector2 MouseStick { get; private set; }

        public CombatSession3D(GameManager manager, Battle b)
        {
            gm = manager;
            battle = b;
            root = new GameObject("Combat 3D");
            root.transform.SetParent(gm.transform, false);

            var cam = gm.Cam;
            savedFov = cam.fieldOfView;
            savedNear = cam.nearClipPlane;
            savedFar = cam.farClipPlane;
            savedAmbientMode = RenderSettings.ambientMode;
            savedAmbient = RenderSettings.ambientLight;
            savedReflection = RenderSettings.reflectionIntensity;
            cam.fieldOfView = 65f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 5000f;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.16f, 0.18f, 0.26f);
            RenderSettings.reflectionIntensity = 0.3f;

            BuildEnvironment();

            var mine = new List<KeyValuePair<ShipData, Empire>>();
            var theirs = new List<KeyValuePair<ShipData, Empire>>();
            foreach (var f in b.playerFleets) foreach (var s in f.ships) if (!s.destroyed) mine.Add(new KeyValuePair<ShipData, Empire>(s, f.owner));
            foreach (var f in b.enemyFleets) foreach (var s in f.ships) if (!s.destroyed) theirs.Add(new KeyValuePair<ShipData, Empire>(s, f.owner));
            Spawn(mine, 0);
            Spawn(theirs, 1);

            var flagData = new List<ShipData>();
            foreach (var kv in mine) flagData.Add(kv.Key);
            var flag = CombatSession.Flagship(flagData);
            foreach (var s in ships) if (s.data == flag) { SetPlayer(s, false); break; }
            if (player == null) foreach (var s in ships) if (s.team == 0) { SetPlayer(s, false); break; }

            if (player != null)
            {
                lastFocus = player.pos;
                camPos = player.pos - player.Forward * 12f + player.Up * 4f;
                camRot = Quaternion.LookRotation(player.pos + player.Forward * 30f - camPos, player.Up);
            }
            cam.transform.SetPositionAndRotation(camPos, camRot);
            banner = "Bataille de " + b.system.name;
            bannerTime = 2.5f;
            Sfx.Alert();
            GameInput.CenterMouse();
        }

        // ==================================================================
        //  Décor : étoiles, nébuleuses, soleil, planète, poussière
        // ==================================================================
        void BuildEnvironment()
        {
            var t = root.transform;
            rng = new System.Random(battle.system.id * 977 + 13);

            var starCol = Palette.Star(battle.system.star);
            var sunDir = Quaternion.Euler(R(-20f, 35f), R(0f, 360f), 0f) * Vector3.forward;

            // Lumières : le soleil du système et une lumière d'appoint bleutée
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.transform.SetParent(t, false);
            sun.type = LightType.Directional;
            sun.color = Color.Lerp(starCol, Color.white, 0.5f);
            sun.intensity = 1.25f;
            sun.shadows = LightShadows.None;
            sun.transform.rotation = Quaternion.LookRotation(-sunDir);
            var fill = new GameObject("Fill").AddComponent<Light>();
            fill.transform.SetParent(t, false);
            fill.type = LightType.Directional;
            fill.color = new Color(0.4f, 0.5f, 0.85f);
            fill.intensity = 0.35f;
            fill.shadows = LightShadows.None;
            fill.transform.rotation = Quaternion.LookRotation(sunDir + Vector3.down * 0.5f);

            // Ciel : étoiles et nébuleuses sur une grande sphère
            var sky = Gfx.StaticParticles(t, "Sky", 3000, -100);
            for (int i = 0; i < 2600; i++)
            {
                float k = R(0f, 1f);
                var c = k < 0.6f ? new Color(0.8f, 0.85f, 1f) : k < 0.85f ? new Color(1f, 0.9f, 0.75f) : new Color(0.6f, 0.75f, 1f);
                c *= R(0.35f, 1f);
                c.a = 1f;
                float size = R(3f, 8f) * (R(0f, 1f) < 0.05f ? 2.2f : 1f);
                Gfx.EmitStatic(sky, OnSphere() * 1500f, c, size);
            }
            Color[] tints = { new Color(0.25f, 0.15f, 0.5f), new Color(0.1f, 0.25f, 0.45f), new Color(0.4f, 0.12f, 0.3f), new Color(0.1f, 0.3f, 0.3f) };
            for (int i = 0; i < 16; i++)
            {
                var c = tints[rng.Next(tints.Length)];
                c.a = R(0.25f, 0.5f);
                Gfx.EmitStatic(sky, OnSphere() * 1700f, c, R(700f, 1300f));
            }
            Gfx.EmitStatic(sky, sunDir * 1500f, starCol, 520f);
            Gfx.EmitStatic(sky, sunDir * 1490f, Color.Lerp(starCol, Color.white, 0.7f), 160f);

            // Planète éclairée par le soleil
            if (battle.system.planets.Count > 0)
            {
                var p = battle.system.planets[0];
                var dir = Quaternion.AngleAxis(R(70f, 140f), Vector3.up) * sunDir;
                dir.y = Mathf.Clamp(dir.y - 0.3f, -0.6f, 0.3f);
                var ppos = dir.normalized * 1100f;
                float size = R(380f, 620f);
                var planet = new GameObject("Planet");
                planet.transform.SetParent(t, false);
                planet.transform.localPosition = ppos;
                planet.transform.localScale = Vector3.one * size;
                planet.AddComponent<MeshFilter>().sharedMesh = Gfx.Sphere();
                var pmr = planet.AddComponent<MeshRenderer>();
                pmr.sharedMaterial = Gfx.Lit(Palette.Planet(p.type), 0.15f, 0f);
                pmr.shadowCastingMode = ShadowCastingMode.Off;
                var atmo = Palette.Planet(p.type);
                atmo.a = 0.45f;
                Gfx.EmitStatic(sky, ppos, atmo, size * 1.35f);
            }

            // Poussière spatiale : donne la sensation de vitesse
            var dust = Gfx.StaticParticles(t, "Dust", 1600, -50);
            dust.GetComponent<ParticleSystemRenderer>().maxParticleSize = 0.015f;
            for (int i = 0; i < 1500; i++)
            {
                var v = OnSphere() * Mathf.Pow(R(0f, 1f), 0.33f) * ArenaRadius * 1.2f;
                Gfx.EmitStatic(dust, v, new Color(0.6f, 0.7f, 0.9f, R(0.2f, 0.45f)), R(0.12f, 0.3f));
            }
        }

        System.Random rng;

        float R(float a, float b) { return a + (float)rng.NextDouble() * (b - a); }

        Vector3 OnSphere()
        {
            var v = new Vector3(R(-1f, 1f), R(-1f, 1f), R(-1f, 1f));
            return v.sqrMagnitude < 1e-4f ? Vector3.up : v.normalized;
        }

        void Spawn(List<KeyValuePair<ShipData, Empire>> list, int team)
        {
            float side = team == 0 ? -1f : 1f;
            var facing = Quaternion.LookRotation(team == 0 ? Vector3.forward : Vector3.back, Vector3.up);
            for (int c = 0; c < 3; c++)
            {
                var group = new List<KeyValuePair<ShipData, Empire>>();
                foreach (var kv in list) if ((int)kv.Key.cls == c) group.Add(kv);
                float baseZ = c == 0 ? 60f : c == 1 ? 72f : 86f;
                float spacing = c == 0 ? 6f : c == 1 ? 8f : 12f;
                const int perRow = 6;
                for (int i = 0; i < group.Count; i++)
                {
                    int row = i / perRow, col = i % perRow;
                    int inRow = Mathf.Min(perRow, group.Count - row * perRow);
                    float x = (col - (inRow - 1) / 2f) * spacing;
                    float y = (row % 2 == 0 ? 1f : -1f) * ((row + 1) / 2) * 6f;
                    var pos = new Vector3(x, y, side * (baseZ + row * 4f));
                    ships.Add(new CombatShip3D(this, group[i].Key, group[i].Value, team, pos, facing));
                }
            }
        }

        void SetPlayer(CombatShip3D s, bool announce)
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

            bool lockMouse = HidesCursor && !GameInput.UsingGamepad;
            var wantLock = lockMouse ? CursorLockMode.Confined : CursorLockMode.None;
            if (Cursor.lockState != wantLock) Cursor.lockState = wantLock;

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

            // Manche virtuel à la souris
            if (!GameInput.UsingGamepad)
            {
                var off = (GameInput.MousePosition - new Vector2(Screen.width * 0.5f, Screen.height * 0.5f)) / (Screen.height * 0.32f);
                float m = off.magnitude;
                if (m > 1f) { off /= m; m = 1f; }
                MouseStick = m < 0.06f ? Vector2.zero : off / m * ((m - 0.06f) / 0.94f);
            }
            else MouseStick = Vector2.zero;

            if (phase == Phase.Intro && phaseTime > 1.6f)
            {
                phase = Phase.Fight;
                banner = "Engagez l'ennemi !";
                bannerTime = 1.5f;
            }

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
            if (player != null && player.alive)
            {
                var p = player;
                lastFocus = p.pos;
                float r = p.spec.radius;
                var desiredPos = p.pos - p.Forward * (5f + r * 5.5f) + p.Up * (1.2f + r * 1.7f);
                camPos = Vector3.Lerp(camPos, desiredPos, 1f - Mathf.Exp(-12f * udt));
                var want = Quaternion.LookRotation(p.pos + p.Forward * 30f - camPos, p.Up);
                camRot = Quaternion.Slerp(camRot, want, 1f - Mathf.Exp(-10f * udt));
            }
            else
            {
                // Vue orbitale lente en attendant le transfert ou la fin de la bataille
                orbitAngle += udt * 0.35f;
                var offset = Quaternion.Euler(18f, orbitAngle * Mathf.Rad2Deg, 0f) * new Vector3(0f, 0f, -28f);
                camPos = Vector3.Lerp(camPos, lastFocus + offset, 1f - Mathf.Exp(-3f * udt));
                camRot = Quaternion.Slerp(camRot, Quaternion.LookRotation(lastFocus - camPos), 1f - Mathf.Exp(-4f * udt));
            }
            var sh = shake > 0f && !paused ? camRot * (Random.insideUnitSphere * shake * 0.15f) : Vector3.zero;
            gm.Cam.transform.SetPositionAndRotation(camPos + sh, camRot);
        }

        void SetPaused(bool p)
        {
            paused = p;
            Time.timeScale = p ? 0f : (phase == Phase.Ending ? 0.35f : 1f);
            if (p) { GameInput.StopRumble(); UI.ResetFocus(); }
            else GameInput.CenterMouse();
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

        public void OnShipDestroyed(CombatShip3D s)
        {
            if (s.team == 0) playerLost++;
            else enemyLost++;
            if (s == player)
            {
                player = null;
                s.SetPlayer(false);
                lastFocus = s.pos;
                GameInput.Rumble(0.9f, 0.9f, 0.6f);
                AddShake(1.5f);
                if (Alive(0) > 0)
                {
                    transferTimer = 1.6f;
                    banner = "Vaisseau amiral détruit !";
                    bannerTime = 1.6f;
                }
            }
        }

        public int Alive(int team)
        {
            int n = 0;
            foreach (var s in ships) if (s.alive && s.team == team) n++;
            return n;
        }

        CombatShip3D BestAlly()
        {
            CombatShip3D best = null;
            foreach (var s in ships)
            {
                if (!s.alive || s.team != 0) continue;
                if (best == null || s.spec.cls > best.spec.cls || (s.spec.cls == best.spec.cls && s.hull > best.hull)) best = s;
            }
            return best;
        }

        CombatShip3D NextAlly(CombatShip3D current)
        {
            var allies = new List<CombatShip3D>();
            foreach (var s in ships) if (s.alive && s.team == 0) allies.Add(s);
            if (allies.Count == 0) return null;
            allies.Sort((x, y) => y.spec.cls != x.spec.cls ? y.spec.cls.CompareTo(x.spec.cls) : y.hull.CompareTo(x.hull));
            int i = allies.IndexOf(current);
            return allies[(i + 1) % allies.Count];
        }

        // ==================================================================
        //  Cibles
        // ==================================================================
        public CombatShip3D FindTarget(CombatShip3D self)
        {
            CombatShip3D best = null;
            float bestScore = float.MaxValue;
            float focus = gm.Galaxy.settings.Diff.playerFocus;
            foreach (var s in ships)
            {
                if (!s.alive || s.team == self.team) continue;
                float score = (s.pos - self.pos).magnitude + Random.Range(0f, 8f);
                if (s.IsPlayer) score -= focus * 1.5f;
                if (self.spec.cls == ShipClass.Chasseur && s.spec.cls == ShipClass.Chasseur) score -= 6f;
                if (self.spec.cls == ShipClass.Croiseur && s.spec.cls == ShipClass.Croiseur) score -= 6f;
                if (score < bestScore) { bestScore = score; best = s; }
            }
            return best;
        }

        public CombatShip3D FindNearestEnemy(Vector3 p, int team, float range)
        {
            CombatShip3D best = null;
            float bestD = range * range;
            foreach (var s in ships)
            {
                if (!s.alive || s.team == team) continue;
                float d = (s.pos - p).sqrMagnitude;
                if (d < bestD) { bestD = d; best = s; }
            }
            return best;
        }

        /// Ennemi le plus proche de l'axe du vaisseau (visée assistée, missiles, réticule).
        public CombatShip3D FindAimTarget(CombatShip3D self, float coneDeg, float range)
        {
            CombatShip3D best = null;
            float bestA = coneDeg;
            var fwd = self.Forward;
            foreach (var s in ships)
            {
                if (!s.alive || s.team == self.team) continue;
                var to = s.pos - self.pos;
                if (to.sqrMagnitude > range * range) continue;
                float a = Vector3.Angle(fwd, to);
                if (a < bestA) { bestA = a; best = s; }
            }
            return best;
        }

        // ==================================================================
        //  Projectiles
        // ==================================================================
        Projectile3D NewProjectile()
        {
            if (pool.Count > 0)
            {
                var p = pool.Pop();
                p.lr.enabled = true;
                return p;
            }
            var lr = new GameObject("Bolt").AddComponent<LineRenderer>();
            lr.transform.SetParent(root.transform, false);
            lr.useWorldSpace = true;
            lr.positionCount = 2;
            lr.sharedMaterial = Gfx.GlowMat;
            lr.textureMode = LineTextureMode.Stretch;
            lr.numCapVertices = 0;
            lr.sortingOrder = 30;
            lr.shadowCastingMode = ShadowCastingMode.Off;
            lr.receiveShadows = false;
            return new Projectile3D { lr = lr };
        }

        public void SpawnBolt(CombatShip3D owner, Vector3 origin, Vector3 velocity, float damage, float life, float width)
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
            p.length = Mathf.Clamp(velocity.magnitude * 0.05f, 1.2f, 3f) * (0.7f + width);
            var c = owner.boltColor;
            p.lr.startColor = c;
            p.lr.endColor = c;
            p.lr.widthMultiplier = width * 2.2f;
            Place(p);
            bolts.Add(p);
        }

        public void SpawnMissile(CombatShip3D owner, Vector3 origin, Vector3 velocity, float damage, CombatShip3D target)
        {
            var p = NewProjectile();
            p.pos = origin;
            p.vel = velocity;
            p.damage = damage;
            p.life = 5f;
            p.team = owner.team;
            p.owner = owner;
            p.missile = true;
            p.target = target;
            p.trail = 0f;
            p.length = 1.3f;
            var c = new Color(1f, 0.75f, 0.35f);
            p.lr.startColor = c;
            p.lr.endColor = c;
            p.lr.widthMultiplier = 0.7f;
            Place(p);
            bolts.Add(p);
        }

        static void Place(Projectile3D p)
        {
            var dir = p.vel.sqrMagnitude > 1e-4f ? p.vel.normalized : Vector3.forward;
            p.lr.SetPosition(0, p.pos - dir * p.length);
            p.lr.SetPosition(1, p.pos);
        }

        void Recycle(int i)
        {
            var p = bolts[i];
            int last = bolts.Count - 1;
            bolts[i] = bolts[last];
            bolts.RemoveAt(last);
            p.lr.enabled = false;
            p.owner = null;
            p.target = null;
            pool.Push(p);
        }

        static float SegmentDistSq(Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 ab = b - a;
            float len2 = ab.sqrMagnitude;
            float t = len2 > 1e-6f ? Mathf.Clamp01(Vector3.Dot(c - a, ab) / len2) : 0f;
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
                    float sp = Mathf.Min(p.vel.magnitude + 30f * dt, 34f);
                    var dir = p.vel.sqrMagnitude > 0.001f ? p.vel.normalized : Vector3.forward;
                    if (p.target != null && p.target.alive)
                        dir = Vector3.RotateTowards(dir, (p.target.pos - p.pos).normalized, 170f * Mathf.Deg2Rad * dt, 0f).normalized;
                    p.vel = dir * sp;
                    p.trail -= dt;
                    if (p.trail <= 0f)
                    {
                        p.trail = 0.025f;
                        Fx.Spark3(p.pos - dir * 0.6f, -dir * 3f + Random.insideUnitSphere, new Color(1f, 0.6f, 0.3f, 0.7f), 0.5f, 0.4f);
                    }
                }

                var prev = p.pos;
                p.pos += p.vel * dt;

                CombatShip3D hit = null;
                for (int k = 0; k < ships.Count; k++)
                {
                    var s = ships[k];
                    if (!s.alive || s.team == p.team) continue;
                    float r = s.HitRadius;
                    if (SegmentDistSq(prev, p.pos, s.pos) < r * r) { hit = s; break; }
                }
                if (hit != null)
                {
                    var n = p.vel.sqrMagnitude > 0.001f ? -p.vel.normalized : Vector3.up;
                    hit.Damage(p.damage, p.pos, n, p.owner);
                    if (p.missile) Fx.Explosion3(p.pos, 0.35f, new Color(1f, 0.6f, 0.3f));
                    Recycle(i);
                    continue;
                }
                if (p.life <= 0f)
                {
                    if (p.missile) Fx.Glow3(p.pos, Vector3.zero, new Color(1f, 0.6f, 0.3f), 1.5f, 0.2f);
                    Recycle(i);
                    continue;
                }
                Place(p);
            }
        }

        // ==================================================================
        //  Utilitaires
        // ==================================================================
        public float TeamDamageMul(int team)
        {
            var d = gm.Galaxy.settings.Diff;
            return team == 0 ? d.playerDamage : d.enemyDamage;
        }

        public float VolumeAt(Vector3 p)
        {
            return Mathf.Clamp01(1f - (p - camPos).magnitude / 90f);
        }

        public void AddShake(float a)
        {
            shake = Mathf.Min(2f, Mathf.Max(shake, a));
        }

        Vector2 WorldToGui(Vector3 w, out bool inFront)
        {
            var sp = gm.Cam.WorldToScreenPoint(w);
            inFront = sp.z > 0.1f;
            return new Vector2(sp.x, Screen.height - sp.y);
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
                if (s.data.destroyed) s.Die();
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
            var cam = gm.Cam;
            cam.fieldOfView = savedFov;
            cam.nearClipPlane = savedNear;
            cam.farClipPlane = savedFar;
            cam.transform.rotation = Quaternion.identity;
            RenderSettings.ambientMode = savedAmbientMode;
            RenderSettings.ambientLight = savedAmbient;
            RenderSettings.reflectionIntensity = savedReflection;
            Cursor.lockState = CursorLockMode.None;
            Time.timeScale = 1f;
            GameInput.StopRumble();
        }

        // ==================================================================
        //  Interface
        // ==================================================================
        public void OnGUI()
        {
            if (phase != Phase.Result && !paused)
            {
                DrawShipMarkers();
                DrawAim();
                CombatHud.TopInfo(battle.system.name, Alive(0), Alive(1), gm.Galaxy.player.color, battle.enemy.color, null);
                if (player != null && player.alive)
                {
                    string speed = "Vitesse : <b>" + Mathf.RoundToInt(player.vel.magnitude * 10f) + "</b>";
                    CombatHud.PlayerPanel(true, player.spec, player.data.name, player.hull, player.maxHull, player.shield, player.maxShield, player.boost, player.MissileReady, speed);
                    if (player.pos.magnitude > ArenaRadius * 0.9f)
                        UI.Label(new Rect(0, UI.H * 0.32f, UI.W, 40), Palette.Tint("Limite de la zone de combat : faites demi-tour !", Palette.Warning), UI.Center);
                }
                else CombatHud.PlayerPanel(false, null, null, 0, 1, 0, 1, 0, 0, null);
                CombatHud.Hints(GameInput.UsingGamepad
                    ? "[Stick D] Diriger   [Stick G] Vitesse / glisser   [LB/RB] Tonneau   [RT] Canons   [LT] Missiles   [A] Postcombustion   [Y] Changer de vaisseau   [Start] Pause"
                    : "[Souris] Diriger   [Z/S] Vitesse   [Q/D] Glisser   [A/E] Tonneau   [Clic G] Canons   [Clic D] Missiles   [Maj] Postcombustion   [Tab] Vaisseau   [Échap] Pause");
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

        /// Jauges au-dessus des vaisseaux proches et indicateurs des ennemis hors champ.
        void DrawShipMarkers()
        {
            float s = UI.S;
            var camUp = CamRotation * Vector3.up;
            foreach (var sh in ships)
            {
                if (!sh.alive || sh.IsPlayer) continue;
                bool front;
                var gp = WorldToGui(sh.pos, out front);
                if (sh.team == 1)
                {
                    float size = (sh.spec.cls == ShipClass.Croiseur ? 16f : sh.spec.cls == ShipClass.Corvette ? 12f : 9f) * s;
                    CombatHud.Offscreen(gp, front, size, sh.empire.color);
                }
                float dist = (sh.pos - camPos).magnitude;
                if (!front || dist > 140f) continue;
                var top = WorldToGui(sh.pos + camUp * (sh.spec.radius * 1.4f + 0.6f), out front);
                if (!front || top.x < 0 || top.y < 0 || top.x > Screen.width || top.y > Screen.height) continue;
                float w = Mathf.Clamp(900f / Mathf.Max(1f, dist), 18f, 70f) * s;
                CombatHud.ShipBars(top, w, sh.hull / sh.maxHull, sh.maxShield > 0f ? sh.shield / sh.maxShield : 0f, sh.team == 0);
            }
        }

        /// Réticule de tir, cible verrouillée, point d'interception et manche virtuel de la souris.
        void DrawAim()
        {
            if (player == null || !player.alive) return;
            float s = UI.S;
            bool front;

            // Point visé par les canons
            var aim = WorldToGui(player.pos + player.Forward * player.GunRange * 0.8f, out front);
            if (front) CombatHud.Cross(aim, 14f, new Color(1f, 1f, 1f, 0.9f));

            // Cible la plus proche de l'axe : crochets + point d'interception
            var t = FindAimTarget(player, 25f, player.GunRange * 2.5f);
            if (t != null)
            {
                var tp = WorldToGui(t.pos, out front);
                if (front)
                {
                    float dist = (t.pos - camPos).magnitude;
                    float half = Mathf.Clamp(t.spec.radius * 900f / Mathf.Max(1f, dist), 14f, 80f) * s;
                    bool inRange = (t.pos - player.pos).magnitude < player.GunRange;
                    CombatHud.Brackets(tp, half, inRange ? new Color(1f, 0.45f, 0.35f, 0.95f) : new Color(1f, 0.85f, 0.4f, 0.8f));
                    var lp = WorldToGui(player.LeadPoint(t, player.spec.gunSpeed), out front);
                    if (front) UI.DotScreen(new Rect(lp.x - 5f * s, lp.y - 5f * s, 10f * s, 10f * s), new Color(1f, 0.5f, 0.4f, 0.9f));
                    UI.ScreenLabel(tp + new Vector2(0f, half + 12f * s), Names.Ships[(int)t.spec.cls] + "  " + Mathf.RoundToInt((t.pos - player.pos).magnitude * 10f) + " m", new Color(1f, 0.85f, 0.75f));
                }
            }

            // Manche virtuel : cercle central et position de la souris
            if (!GameInput.UsingGamepad)
            {
                var c = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
                float dz = Screen.height * 0.32f * 0.06f;
                UI.DotScreen(new Rect(c.x - dz, c.y - dz, dz * 2f, dz * 2f), new Color(1f, 1f, 1f, 0.12f));
                var m = GameInput.MousePosition;
                var mg = new Vector2(m.x, Screen.height - m.y);
                UI.DotScreen(new Rect(mg.x - 6f * s, mg.y - 6f * s, 12f * s, 12f * s), new Color(0.6f, 0.9f, 1f, 0.9f));
            }
        }
    }
}
