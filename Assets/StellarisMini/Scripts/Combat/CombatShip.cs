using UnityEngine;

namespace StellarisMini
{
    /// Un vaisseau dans l'arène de combat, piloté par le joueur ou par l'IA.
    public class CombatShip
    {
        public readonly CombatSession cs;
        public readonly ShipData data;
        public readonly ShipSpec spec;
        public readonly Empire empire;
        public readonly int team;

        public Vector2 pos, vel;
        public float heading;           // degrés, 0 = +X
        public float hull, maxHull, shield, maxShield;
        public bool alive = true;
        public bool IsPlayer { get; private set; }
        public float boost = 1f;
        public bool boosting;

        readonly float dmgMul, speedMul;
        float shieldDelay, gunCd, missileCd, turretCd;
        int barrel;

        // IA
        CombatShip target;
        float retarget, orbitTimer, orbitDir = 1f, breakTimer;
        Vector2 breakDir;

        // Visuel
        readonly Transform tf;
        readonly SpriteRenderer engine, shieldFx, marker;
        readonly Color engineColor, shieldColor;
        public readonly Color boltColor;
        float shieldFlash, thrustVis;

        public Vector2 Forward
        {
            get { float a = heading * Mathf.Deg2Rad; return new Vector2(Mathf.Cos(a), Mathf.Sin(a)); }
        }

        public float MissileReady { get { return spec.missileSalvo > 0 ? Mathf.Clamp01(1f - missileCd / spec.missileCooldown) : 0f; } }

        public CombatShip(CombatSession session, ShipData d, Empire e, int teamId, Vector2 position, float headingDeg)
        {
            cs = session;
            data = d;
            spec = d.Spec;
            empire = e;
            team = teamId;
            pos = position;
            heading = headingDeg;

            dmgMul = e.DamageMul;
            speedMul = e.SpeedMul;
            maxHull = spec.hull;
            hull = Mathf.Clamp(d.hull, 1f, maxHull);
            maxShield = spec.shield * e.ShieldMul;
            shield = maxShield;
            gunCd = Random.Range(0f, spec.gunCooldown);
            missileCd = Random.Range(1f, spec.missileCooldown + 1f);
            orbitDir = Random.value < 0.5f ? -1f : 1f;

            engineColor = Color.Lerp(e.color, new Color(1f, 0.7f, 0.3f), 0.5f);
            shieldColor = Color.Lerp(e.color, new Color(0.5f, 0.85f, 1f), 0.6f);
            boltColor = Color.Lerp(e.color, Color.white, 0.45f);

            tf = new GameObject(d.name).transform;
            tf.SetParent(cs.root.transform, false);
            var hullMr = Gfx.MakeMeshObj("Hull", tf, Gfx.ShipMesh(spec.cls, e.color), 20);
            var hullTf = hullMr.transform;
            hullTf.localScale = new Vector3(spec.radius, spec.radius, 1f);
            engine = Gfx.MakeSpriteObj("Engine", hullTf, Gfx.GlowSprite, engineColor, 0.8f, 19, true);
            engine.transform.localPosition = new Vector3(0, -0.85f, 0.05f);
            shieldFx = Gfx.MakeSpriteObj("Shield", tf, Gfx.RingSprite, Color.clear, spec.radius * 2.8f, 24, true);
            marker = Gfx.MakeSpriteObj("Marker", tf, Gfx.RingSprite, Color.clear, spec.radius * 3.8f, 18, false);
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

            Vector2 thrust;
            float desired;
            bool fire, fireMissile, wantBoost;
            if (IsPlayer) PlayerControl(out thrust, out desired, out fire, out fireMissile, out wantBoost);
            else AIControl(dt, out thrust, out desired, out fire, out fireMissile, out wantBoost);

            // Postcombustion
            bool wasBoosting = boosting;
            boosting = wantBoost && boost > 0.05f && thrust.sqrMagnitude > 0.01f;
            if (boosting) boost = Mathf.Max(0f, boost - dt / 2.2f);
            else boost = Mathf.Min(1f, boost + dt / 5f);
            if (boosting && !wasBoosting && IsPlayer) Sfx.Boost();

            // Physique "spatiale" avec un peu d'amortissement pour rester jouable
            float acc = spec.accel * speedMul * (boosting ? 2f : 1f);
            float maxSp = spec.maxSpeed * speedMul * (boosting ? 1.7f : 1f);
            vel += thrust * acc * dt;
            if (thrust.sqrMagnitude < 0.01f) vel *= Mathf.Exp(-1.3f * dt);
            float sp = vel.magnitude;
            if (sp > maxSp) vel = vel / sp * Mathf.Lerp(sp, maxSp, 1f - Mathf.Exp(-5f * dt));

            // Limite de l'arène
            float d = pos.magnitude;
            if (d > CombatSession.ArenaRadius) vel -= pos / d * (d - CombatSession.ArenaRadius) * 6f * dt;
            pos += vel * dt;

            float turn = spec.turnRate * (IsPlayer ? 1.25f : 1f);
            heading = Mathf.MoveTowardsAngle(heading, desired, turn * dt);

            // Armes
            gunCd -= dt;
            missileCd -= dt;
            turretCd -= dt;
            if (cs.WeaponsFree)
            {
                if (fire && gunCd <= 0f) FireGun();
                if (fireMissile && missileCd <= 0f && spec.missileSalvo > 0) FireMissiles();
                if (spec.turrets > 0 && turretCd <= 0f) FireTurrets();
            }

            thrustVis = Mathf.Lerp(thrustVis, thrust.magnitude * (boosting ? 1.6f : 1f), 1f - Mathf.Exp(-10f * dt));
            UpdateVisuals(dt);
        }

        static float Angle(Vector2 v) { return Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg; }

        static Vector2 Rotate(Vector2 v, float deg)
        {
            float a = deg * Mathf.Deg2Rad, c = Mathf.Cos(a), s = Mathf.Sin(a);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        static Vector2 Perp(Vector2 v) { return new Vector2(-v.y, v.x); }

        // ------------------------------------------------------------------
        //  Contrôle du joueur
        // ------------------------------------------------------------------
        void PlayerControl(out Vector2 thrust, out float desired, out bool fire, out bool fireMissile, out bool wantBoost)
        {
            thrust = GameInput.Move;
            desired = heading;
            if (GameInput.UsingGamepad)
            {
                var aim = GameInput.RightStick;
                if (aim.sqrMagnitude > 0.05f) desired = Angle(aim);
                else if (thrust.sqrMagnitude > 0.05f) desired = Angle(thrust);
                fire = GameInput.RightTrigger > 0.35f;
                fireMissile = GameInput.LeftTrigger > 0.35f;
                wantBoost = GameInput.PadHeld(Pad.A) || GameInput.PadHeld(Pad.LB);
            }
            else
            {
                var dir = cs.MouseWorld - pos;
                if (dir.sqrMagnitude > 0.25f) desired = Angle(dir);
                fire = GameInput.MouseHeld(0);
                fireMissile = GameInput.MouseHeld(1);
                wantBoost = GameInput.KeyHeld(KeyId.Shift);
            }
        }

        // ------------------------------------------------------------------
        //  Intelligence artificielle
        // ------------------------------------------------------------------
        void AIControl(float dt, out Vector2 thrust, out float desired, out bool fire, out bool fireMissile, out bool wantBoost)
        {
            thrust = Vector2.zero;
            desired = heading;
            fire = fireMissile = wantBoost = false;

            retarget -= dt;
            if (target == null || !target.alive || retarget <= 0f)
            {
                target = cs.FindTarget(this);
                retarget = Random.Range(1.5f, 3.5f);
            }
            if (target == null) return;

            Vector2 to = target.pos - pos;
            float dist = to.magnitude;
            Vector2 dir = dist > 0.01f ? to / dist : Forward;
            Vector2 lead = LeadPoint(target, spec.gunSpeed);
            float aimHeading = Angle(lead - pos);
            bool fighter = spec.cls == ShipClass.Chasseur;

            if (fighter)
            {
                // Passes d'attaque : on fonce, on tire, on dégage
                if (breakTimer > 0f)
                {
                    breakTimer -= dt;
                    thrust = breakDir;
                    desired = Angle(breakDir);
                }
                else
                {
                    thrust = dir;
                    desired = aimHeading;
                    if (dist < 6f)
                    {
                        breakTimer = Random.Range(0.9f, 1.6f);
                        breakDir = (Perp(dir) * orbitDir - dir * 0.3f).normalized;
                        orbitDir = -orbitDir;
                    }
                }
                wantBoost = dist > 28f || breakTimer > 0f;
            }
            else
            {
                // Maintien à distance de tir en tournant autour de la cible
                float want = spec.gunRange * 0.6f;
                if (dist > want + 4f) thrust = dir;
                else if (dist < want - 4f) thrust = -dir * 0.8f;
                else thrust = Perp(dir) * orbitDir * 0.6f;
                orbitTimer -= dt;
                if (orbitTimer <= 0f)
                {
                    orbitTimer = Random.Range(2.5f, 5f);
                    orbitDir = Random.value < 0.5f ? -1f : 1f;
                }
                desired = aimHeading;
            }

            // Séparation entre alliés
            Vector2 sep = Vector2.zero;
            foreach (var o in cs.ships)
            {
                if (o == this || !o.alive || o.team != team) continue;
                Vector2 delta = pos - o.pos;
                float min = spec.radius + o.spec.radius + 2.5f;
                float d2 = delta.sqrMagnitude;
                if (d2 < min * min && d2 > 0.0001f) sep += delta / d2 * min;
            }
            thrust += sep * 0.8f;
            if (pos.magnitude > CombatSession.ArenaRadius * 0.85f) thrust -= pos.normalized * 1.5f;
            thrust = Vector2.ClampMagnitude(thrust, 1f);

            float cone = fighter ? 12f : 8f;
            fire = (breakTimer <= 0f || !fighter) && dist < spec.gunRange * 1.05f && Mathf.Abs(Mathf.DeltaAngle(heading, aimHeading)) < cone;
            fireMissile = spec.missileSalvo > 0 && dist < 26f;
        }

        public Vector2 LeadPoint(CombatShip t, float projSpeed)
        {
            Vector2 rel = t.pos - pos;
            float time = rel.magnitude / Mathf.Max(1f, projSpeed);
            return t.pos + (t.vel - vel * 0.3f) * time;
        }

        // ------------------------------------------------------------------
        //  Armes
        // ------------------------------------------------------------------
        void FireGun()
        {
            gunCd = spec.gunCooldown;
            Vector2 fwd = Forward;
            Vector2 right = new Vector2(fwd.y, -fwd.x);
            float offset = 0f;
            if (spec.cls == ShipClass.Chasseur) offset = (barrel++ % 2 == 0 ? 0.4f : -0.4f) * spec.radius;
            else if (spec.cls == ShipClass.Corvette) offset = (barrel++ % 2 == 0 ? 0.25f : -0.25f) * spec.radius;
            Vector2 origin = pos + fwd * spec.radius * 0.9f + right * offset;

            Vector2 dir = fwd;
            if (IsPlayer && GameInput.UsingGamepad)
            {
                // Visée assistée à la manette
                var t = cs.FindAimTarget(this, 14f, spec.gunRange * 1.1f);
                if (t != null) dir = (LeadPoint(t, spec.gunSpeed) - origin).normalized;
            }
            dir = Rotate(dir, Random.Range(-spec.gunSpread, spec.gunSpread));

            cs.SpawnBolt(this, origin, dir * spec.gunSpeed + vel * 0.3f, spec.gunDamage * dmgMul, spec.gunRange / spec.gunSpeed, spec.gunWidth);
            Fx.Glow(origin, vel, boltColor, spec.gunWidth * 3f, 0.06f);
            bool heavy = spec.cls == ShipClass.Croiseur;
            Sfx.Laser(IsPlayer ? 0.8f : cs.VolumeAt(pos) * 0.35f, heavy);
            if (IsPlayer && heavy) { GameInput.Rumble(0.3f, 0.1f, 0.1f); cs.AddShake(0.2f); }
        }

        void FireMissiles()
        {
            missileCd = spec.missileCooldown;
            var t = IsPlayer ? (cs.FindAimTarget(this, 50f, 45f) ?? cs.FindNearestEnemy(pos, team, 60f)) : target;
            Vector2 fwd = Forward;
            Vector2 right = new Vector2(fwd.y, -fwd.x);
            for (int i = 0; i < spec.missileSalvo; i++)
            {
                float side = spec.missileSalvo == 1 ? (barrel % 2 == 0 ? 1f : -1f) : (i == 0 ? 1f : -1f);
                Vector2 origin = pos + right * side * spec.radius * 0.6f;
                Vector2 v = vel + Rotate(fwd, side * -35f) * 8f;
                cs.SpawnMissile(this, origin, v, spec.missileDamage * dmgMul, t);
            }
            Sfx.Missile(IsPlayer ? 0.7f : cs.VolumeAt(pos) * 0.4f);
        }

        void FireTurrets()
        {
            turretCd = spec.turretCooldown;
            var t = cs.FindNearestEnemy(pos, team, spec.turretRange);
            if (t == null) return;
            Vector2 fwd = Forward;
            Vector2 right = new Vector2(fwd.y, -fwd.x);
            const float speed = 40f;
            for (int i = 0; i < spec.turrets; i++)
            {
                Vector2 origin = pos + right * (i == 0 ? 0.55f : -0.55f) * spec.radius;
                Vector2 dir = (LeadPoint(t, speed) - origin).normalized;
                dir = Rotate(dir, Random.Range(-3f, 3f));
                cs.SpawnBolt(this, origin, dir * speed + vel * 0.3f, spec.turretDamage * dmgMul, spec.turretRange / speed, 0.18f);
            }
            Sfx.Laser(IsPlayer ? 0.35f : cs.VolumeAt(pos) * 0.2f, false);
        }

        // ------------------------------------------------------------------
        //  Dégâts
        // ------------------------------------------------------------------
        public void Damage(float amount, Vector2 point, Vector2 normal, CombatShip attacker)
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
                Fx.Hit(point, normal, shieldColor, 3);
                Sfx.Hit(vol, true);
            }
            if (amount > 0f)
            {
                hull -= amount;
                Fx.Hit(point, normal, new Color(1f, 0.6f, 0.2f), 5);
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
            Fx.Explosion(pos, sc, empire.color);
            Sfx.Explosion(IsPlayer ? 1f : cs.VolumeAt(pos) * 0.8f, spec.cls == ShipClass.Croiseur);
            float d = (pos - cs.CamPos).magnitude;
            if (d < 40f) cs.AddShake(sc * 0.6f * (1f - d / 40f));
            tf.gameObject.SetActive(false);
            cs.OnShipDestroyed(this);
        }

        // ------------------------------------------------------------------
        void UpdateVisuals(float dt)
        {
            tf.localPosition = new Vector3(pos.x, pos.y, 0f);
            tf.localRotation = Quaternion.Euler(0f, 0f, heading - 90f);

            float e = 0.45f + thrustVis * 0.8f;
            engine.transform.localScale = new Vector3(e * 0.8f, e * 1.4f, 1f);
            var ec = boosting ? new Color(0.6f, 0.85f, 1f) : engineColor;
            ec.a = 0.7f + 0.3f * Random.value;
            engine.color = ec;

            shieldFlash = Mathf.Max(0f, shieldFlash - dt * 3f);
            var sc = shieldColor;
            sc.a = shieldFlash * 0.8f;
            shieldFx.color = sc;

            marker.color = IsPlayer ? new Color(1f, 1f, 1f, 0.22f + 0.08f * Mathf.Sin(Time.unscaledTime * 4f)) : Color.clear;
        }
    }
}
