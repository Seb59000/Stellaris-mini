using UnityEngine;

namespace StellarisMini
{
    /// Un vaisseau dans l'arène 3D : vol libre (tangage, lacet, roulis), piloté par le joueur ou l'IA.
    public class CombatShip3D
    {
        /// Les distances de tir sont allongées en 3D : viser y est plus difficile qu'en vue de dessus.
        public const float RangeMul = 1.6f;

        public readonly CombatSession3D cs;
        public readonly ShipData data;
        public readonly ShipSpec spec;
        public readonly Empire empire;
        public readonly int team;

        public Vector3 pos, vel;
        public Quaternion rot;
        public float hull, maxHull, shield, maxShield;
        public bool alive = true;
        public bool IsPlayer { get; private set; }
        public float boost = 1f;
        public bool boosting;

        readonly float dmgMul, speedMul;
        float shieldDelay, gunCd, missileCd, turretCd;
        int barrel;

        // IA
        CombatShip3D target;
        float retarget, breakTimer, orbitTimer;
        Vector3 breakDir, orbitAxis = Vector3.up;

        // Visuel
        readonly Transform tf;
        readonly SpriteRenderer engine, shieldFx;
        readonly Color engineColor, shieldColor;
        public readonly Color boltColor;
        float shieldFlash, thrustVis;

        public Vector3 Forward { get { return rot * Vector3.forward; } }
        public Vector3 Up { get { return rot * Vector3.up; } }
        public Vector3 Right { get { return rot * Vector3.right; } }
        public float HitRadius { get { return spec.radius * 1.2f; } }
        public float GunRange { get { return spec.gunRange * RangeMul; } }
        public float MaxSpeed { get { return spec.maxSpeed * speedMul * 1.2f; } }
        public float MissileReady { get { return spec.missileSalvo > 0 ? Mathf.Clamp01(1f - missileCd / spec.missileCooldown) : 0f; } }

        public CombatShip3D(CombatSession3D session, ShipData d, Empire e, int teamId, Vector3 position, Quaternion rotation)
        {
            cs = session;
            data = d;
            spec = d.Spec;
            empire = e;
            team = teamId;
            pos = position;
            rot = rotation;

            dmgMul = e.DamageMul * session.TeamDamageMul(teamId);
            speedMul = e.SpeedMul;
            maxHull = spec.hull;
            hull = Mathf.Clamp(d.hull, 1f, maxHull);
            maxShield = spec.shield * e.ShieldMul;
            shield = maxShield;
            gunCd = Random.Range(0f, spec.gunCooldown);
            missileCd = Random.Range(1f, spec.missileCooldown + 1f);
            vel = Forward * MaxSpeed * 0.4f;

            engineColor = Color.Lerp(e.color, new Color(1f, 0.7f, 0.3f), 0.5f);
            shieldColor = Color.Lerp(e.color, new Color(0.5f, 0.85f, 1f), 0.6f);
            boltColor = Color.Lerp(e.color, Color.white, 0.45f);

            // Coque éclairée + liseré néon aux couleurs de l'empire
            tf = new GameObject(d.name).transform;
            tf.SetParent(cs.root.transform, false);
            var hullTf = new GameObject("Hull").transform;
            hullTf.SetParent(tf, false);
            hullTf.localScale = Vector3.one * spec.radius;
            hullTf.gameObject.AddComponent<MeshFilter>().sharedMesh = Gfx.ShipMesh3D(spec.cls);
            var mr = hullTf.gameObject.AddComponent<MeshRenderer>();
            mr.sharedMaterial = Gfx.Lit(Color.Lerp(new Color(0.55f, 0.58f, 0.65f), e.color, 0.45f));
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;

            var shape = Gfx.ShipShape(spec.cls);
            var lr = new GameObject("Outline").AddComponent<LineRenderer>();
            lr.transform.SetParent(hullTf, false);
            lr.useWorldSpace = false;
            lr.loop = true;
            lr.positionCount = shape.Length;
            for (int i = 0; i < shape.Length; i++) lr.SetPosition(i, new Vector3(shape[i].x, 0f, shape[i].y));
            lr.widthMultiplier = 0.07f * spec.radius;
            lr.sharedMaterial = Gfx.Unlit(Color.Lerp(e.color, Color.white, 0.25f));
            lr.sortingOrder = 21;
            lr.numCornerVertices = 1;

            engine = Gfx.MakeSpriteObj("Engine", tf, Gfx.GlowSprite, engineColor, spec.radius, 19, true);
            engine.transform.localPosition = new Vector3(0f, 0f, -0.95f * spec.radius);
            shieldFx = Gfx.MakeSpriteObj("Shield", tf, Gfx.RingSprite, Color.clear, spec.radius * 2.9f, 24, true);
            UpdateVisuals(0f);
        }

        public void SetPlayer(bool on)
        {
            IsPlayer = on;
            target = null;
        }

        // ==================================================================
        public void Tick(float dt)
        {
            if (!alive || dt <= 0f) return;

            shieldDelay -= dt;
            if (shieldDelay <= 0f) shield = Mathf.Min(maxShield, shield + maxShield * 0.08f * dt);

            float fwdIn, strafe;
            bool fire, fireMissile, wantBoost;
            if (IsPlayer) PlayerControl(dt, out fwdIn, out strafe, out fire, out fireMissile, out wantBoost);
            else AIControl(dt, out fwdIn, out strafe, out fire, out fireMissile, out wantBoost);

            bool wasBoosting = boosting;
            boosting = wantBoost && boost > 0.05f && fwdIn > -0.5f;
            if (boosting) boost = Mathf.Max(0f, boost - dt / 2.2f);
            else boost = Mathf.Min(1f, boost + dt / 5f);
            if (boosting && !wasBoosting && IsPlayer) Sfx.Boost();

            // Vol "arcade" : la vitesse suit l'axe du vaisseau, avec une vitesse de croisière par défaut
            float maxSp = MaxSpeed * (boosting ? 1.7f : 1f);
            float tFwd = boosting ? maxSp : (fwdIn >= 0f ? Mathf.Lerp(0.4f, 1f, fwdIn) : Mathf.Lerp(0.4f, -0.2f, -fwdIn)) * maxSp;
            var targetVel = Forward * tFwd + Right * strafe * MaxSpeed * 0.5f;
            float acc = spec.accel * speedMul * (boosting ? 2f : 1f);
            vel = Vector3.MoveTowards(vel, targetVel, acc * dt);

            float d = pos.magnitude;
            if (d > CombatSession3D.ArenaRadius) vel -= pos / d * (d - CombatSession3D.ArenaRadius) * 3f * dt;
            pos += vel * dt;

            gunCd -= dt;
            missileCd -= dt;
            turretCd -= dt;
            if (cs.WeaponsFree)
            {
                if (fire && gunCd <= 0f) FireGun();
                if (fireMissile && missileCd <= 0f && spec.missileSalvo > 0) FireMissiles();
                if (spec.turrets > 0 && turretCd <= 0f) FireTurrets();
            }

            thrustVis = Mathf.Lerp(thrustVis, Mathf.Clamp01(vel.magnitude / Mathf.Max(1f, MaxSpeed)) * (boosting ? 1.6f : 1f), 1f - Mathf.Exp(-10f * dt));
            UpdateVisuals(dt);
        }

        static Vector3 RandomCone(Vector3 dir, float deg)
        {
            if (deg <= 0f) return dir;
            var axis = Vector3.Cross(dir, Random.onUnitSphere);
            if (axis.sqrMagnitude < 1e-6f) return dir;
            return Quaternion.AngleAxis(Random.Range(0f, deg), axis.normalized) * dir;
        }

        // ------------------------------------------------------------------
        //  Pilotage du joueur
        // ------------------------------------------------------------------
        void PlayerControl(float dt, out float fwdIn, out float strafe, out bool fire, out bool fireMissile, out bool wantBoost)
        {
            Vector2 look;
            float roll;
            if (GameInput.UsingGamepad)
            {
                look = GameInput.RightStick;
                roll = (GameInput.PadHeld(Pad.RB) ? 1f : 0f) - (GameInput.PadHeld(Pad.LB) ? 1f : 0f);
                fwdIn = GameInput.LeftStick.y;
                strafe = GameInput.LeftStick.x;
                fire = GameInput.RightTrigger > 0.35f;
                fireMissile = GameInput.LeftTrigger > 0.35f;
                wantBoost = GameInput.PadHeld(Pad.A);
            }
            else
            {
                // "Manche virtuel" : l'écart entre la souris et le centre de l'écran donne la vitesse de rotation
                look = cs.MouseStick;
                roll = (GameInput.KeyHeld(KeyId.E) ? 1f : 0f) - (GameInput.KeyHeld(KeyId.Q) ? 1f : 0f);
                var mv = GameInput.Move;
                fwdIn = mv.y;
                strafe = mv.x;
                fire = GameInput.MouseHeld(0);
                fireMissile = GameInput.MouseHeld(1);
                wantBoost = GameInput.KeyHeld(KeyId.Shift);
            }
            float tr = spec.turnRate * 0.55f * 1.2f;
            rot = rot * Quaternion.Euler(-look.y * tr * dt, look.x * tr * dt, -roll * 150f * dt);
        }

        // ------------------------------------------------------------------
        //  Intelligence artificielle
        // ------------------------------------------------------------------
        void AIControl(float dt, out float fwdIn, out float strafe, out bool fire, out bool fireMissile, out bool wantBoost)
        {
            fwdIn = 0f;
            strafe = 0f;
            fire = fireMissile = wantBoost = false;

            retarget -= dt;
            if (target == null || !target.alive || retarget <= 0f)
            {
                target = cs.FindTarget(this);
                retarget = Random.Range(1.5f, 3.5f);
            }
            if (target == null) return;

            Vector3 to = target.pos - pos;
            float dist = to.magnitude;
            Vector3 dir = dist > 0.01f ? to / dist : Forward;
            Vector3 lead = LeadPoint(target, spec.gunSpeed);
            Vector3 aimDir = (lead - pos).normalized;
            bool fighter = spec.cls == ShipClass.Chasseur;
            Vector3 desired;
            fwdIn = 1f;

            if (fighter)
            {
                // Passes d'attaque : on fonce, on tire, on dégage
                if (breakTimer > 0f)
                {
                    breakTimer -= dt;
                    desired = breakDir;
                    wantBoost = true;
                }
                else
                {
                    desired = aimDir;
                    if (dist < 10f)
                    {
                        breakTimer = Random.Range(1.2f, 2.2f);
                        breakDir = (Random.onUnitSphere + Vector3.Cross(dir, Up) * 1.2f - dir * 0.3f).normalized;
                    }
                    wantBoost = dist > 45f;
                }
            }
            else
            {
                // Les gros vaisseaux restent à distance de tir et tournent autour de la cible
                float want = GunRange * 0.55f;
                if (dist > want + 8f) desired = aimDir;
                else
                {
                    orbitTimer -= dt;
                    if (orbitTimer <= 0f)
                    {
                        orbitTimer = Random.Range(3f, 6f);
                        orbitAxis = Random.onUnitSphere;
                    }
                    var tangent = Vector3.Cross(dir, orbitAxis).normalized;
                    desired = aimDir * 0.75f + tangent * 0.5f;
                    if (dist < want - 8f) desired -= dir * 0.8f;
                    fwdIn = 0.1f;
                }
            }

            // Séparation entre alliés
            Vector3 sep = Vector3.zero;
            foreach (var o in cs.ships)
            {
                if (o == this || !o.alive || o.team != team) continue;
                Vector3 delta = pos - o.pos;
                float min = spec.radius + o.spec.radius + 3f;
                float d2 = delta.sqrMagnitude;
                if (d2 < min * min && d2 > 0.0001f) sep += delta / d2 * min;
            }
            desired += sep * 0.6f;
            if (pos.magnitude > CombatSession3D.ArenaRadius * 0.8f) desired -= pos.normalized * 2f;
            if (desired.sqrMagnitude < 1e-4f) desired = Forward;

            var targetRot = Quaternion.LookRotation(desired.normalized, Up);
            rot = Quaternion.RotateTowards(rot, targetRot, spec.turnRate * 0.5f * dt);

            float cone = fighter ? 8f : 6f;
            fire = (!fighter || breakTimer <= 0f) && dist < GunRange * 1.05f && Vector3.Angle(Forward, lead - pos) < cone;
            fireMissile = spec.missileSalvo > 0 && dist < 45f;
        }

        public Vector3 LeadPoint(CombatShip3D t, float projSpeed)
        {
            Vector3 rel = t.pos - pos;
            float time = rel.magnitude / Mathf.Max(1f, projSpeed);
            return t.pos + (t.vel - vel * 0.3f) * time;
        }

        // ------------------------------------------------------------------
        //  Armes
        // ------------------------------------------------------------------
        void FireGun()
        {
            gunCd = spec.gunCooldown;
            float offset = 0f;
            if (spec.cls == ShipClass.Chasseur) offset = (barrel++ % 2 == 0 ? 0.4f : -0.4f) * spec.radius;
            else if (spec.cls == ShipClass.Corvette) offset = (barrel++ % 2 == 0 ? 0.25f : -0.25f) * spec.radius;
            Vector3 origin = pos + Forward * spec.radius * 0.9f + Right * offset;

            Vector3 dir = Forward;
            if (IsPlayer)
            {
                // Visée assistée : les tirs convergent vers la cible proche du réticule
                var t = cs.FindAimTarget(this, GameInput.UsingGamepad ? 8f : 5f, GunRange * 1.1f);
                if (t != null) dir = (LeadPoint(t, spec.gunSpeed) - origin).normalized;
            }
            dir = RandomCone(dir, spec.gunSpread * 0.6f);

            cs.SpawnBolt(this, origin, dir * spec.gunSpeed + vel * 0.3f, spec.gunDamage * dmgMul, GunRange / spec.gunSpeed, spec.gunWidth);
            Fx.Glow3(origin, vel, boltColor, spec.gunWidth * 3f, 0.06f);
            bool heavy = spec.cls == ShipClass.Croiseur;
            Sfx.Laser(IsPlayer ? 0.8f : cs.VolumeAt(pos) * 0.35f, heavy);
            if (IsPlayer && heavy) { GameInput.Rumble(0.3f, 0.1f, 0.1f); cs.AddShake(0.2f); }
        }

        void FireMissiles()
        {
            missileCd = spec.missileCooldown;
            var t = IsPlayer ? (cs.FindAimTarget(this, 35f, 100f) ?? cs.FindNearestEnemy(pos, team, 140f)) : target;
            for (int i = 0; i < spec.missileSalvo; i++)
            {
                float side = spec.missileSalvo == 1 ? (barrel % 2 == 0 ? 1f : -1f) : (i == 0 ? 1f : -1f);
                Vector3 origin = pos + Right * side * spec.radius * 0.6f - Up * spec.radius * 0.1f;
                Vector3 v = vel + Right * side * 6f - Up * 2f + Forward * 4f;
                cs.SpawnMissile(this, origin, v, spec.missileDamage * dmgMul, t);
            }
            Sfx.Missile(IsPlayer ? 0.7f : cs.VolumeAt(pos) * 0.4f);
        }

        void FireTurrets()
        {
            turretCd = spec.turretCooldown;
            var t = cs.FindNearestEnemy(pos, team, spec.turretRange * RangeMul);
            if (t == null) return;
            const float speed = 40f;
            for (int i = 0; i < spec.turrets; i++)
            {
                Vector3 origin = pos + Right * (i == 0 ? 0.55f : -0.55f) * spec.radius + Up * spec.radius * 0.25f;
                Vector3 dir = RandomCone((LeadPoint(t, speed) - origin).normalized, 3f);
                cs.SpawnBolt(this, origin, dir * speed + vel * 0.3f, spec.turretDamage * dmgMul, spec.turretRange * RangeMul / speed, 0.18f);
            }
            Sfx.Laser(IsPlayer ? 0.35f : cs.VolumeAt(pos) * 0.2f, false);
        }

        // ------------------------------------------------------------------
        //  Dégâts
        // ------------------------------------------------------------------
        public void Damage(float amount, Vector3 point, Vector3 normal, CombatShip3D attacker)
        {
            if (!alive) return;
            shieldDelay = 3.5f;
            float vol = IsPlayer ? 0.8f : cs.VolumeAt(pos) * 0.5f;
            if (shield > 0f)
            {
                float a = Mathf.Min(shield, amount);
                shield -= a;
                amount -= a;
                shieldFlash = 1f;
                Fx.Hit3(point, normal, shieldColor, 3);
                Sfx.Hit(vol, true);
            }
            if (amount > 0f)
            {
                hull -= amount;
                Fx.Hit3(point, normal, new Color(1f, 0.6f, 0.2f), 5);
                Sfx.Hit(vol, false);
            }
            if (IsPlayer)
            {
                GameInput.Rumble(0.25f, 0.4f, 0.12f);
                cs.AddShake(0.3f);
            }
            if (hull <= 0f)
            {
                Die();
                return;
            }
            if (!IsPlayer && attacker != null && attacker.alive && Random.value < 0.3f) target = attacker;
        }

        public void Die()
        {
            if (!alive) return;
            alive = false;
            hull = 0f;
            data.hull = 0f;
            data.destroyed = true;
            float sc = spec.cls == ShipClass.Croiseur ? 2f : spec.cls == ShipClass.Corvette ? 1.2f : 0.8f;
            Fx.Explosion3(pos, sc, empire.color);
            Sfx.Explosion(IsPlayer ? 1f : cs.VolumeAt(pos) * 0.8f, spec.cls == ShipClass.Croiseur);
            float d = (pos - cs.CamPos).magnitude;
            if (d < 50f) cs.AddShake(sc * 0.6f * (1f - d / 50f));
            tf.gameObject.SetActive(false);
            cs.OnShipDestroyed(this);
        }

        // ------------------------------------------------------------------
        void UpdateVisuals(float dt)
        {
            tf.localPosition = pos;
            tf.localRotation = rot;
            var camRot = cs.CamRotation;

            float e = 0.5f + thrustVis * 0.9f;
            engine.transform.rotation = camRot;
            engine.transform.localScale = Vector3.one * spec.radius * e;
            var ec = boosting ? new Color(0.6f, 0.85f, 1f) : engineColor;
            ec.a = 0.7f + 0.3f * Random.value;
            engine.color = ec;

            shieldFlash = Mathf.Max(0f, shieldFlash - dt * 3f);
            shieldFx.transform.rotation = camRot;
            var sc = shieldColor;
            sc.a = shieldFlash * 0.8f;
            shieldFx.color = sc;
        }
    }
}
