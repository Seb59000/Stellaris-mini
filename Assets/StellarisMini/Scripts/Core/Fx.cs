using UnityEngine;

namespace StellarisMini
{
    /// Effets de particules (étincelles, explosions, traînées) émis à la demande.
    public static class Fx
    {
        static ParticleSystem sparks, glows;

        public static void Init(Transform parent)
        {
            if (sparks != null) return;
            sparks = Create("FX Sparks", parent, 4000, 60);
            glows = Create("FX Glows", parent, 1500, 55);
        }

        static ParticleSystem Create(string name, Transform parent, int max, int order)
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
            main.startLifetime = 1f;
            main.startSize = 1f;

            var em = ps.emission;
            em.enabled = false;
            var shape = ps.shape;
            shape.enabled = false;

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.4f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(grad);

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.2f));

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = Gfx.GlowMat;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.sortingOrder = order;

            ps.Play();
            return ps;
        }

        static void Emit(ParticleSystem ps, Vector2 pos, Vector2 vel, Color c, float size, float life)
        {
            if (ps == null) return;
            var ep = new ParticleSystem.EmitParams();
            ep.position = new Vector3(pos.x, pos.y, -0.3f);
            ep.velocity = new Vector3(vel.x, vel.y, 0f);
            ep.startColor = c;
            ep.startSize = size;
            ep.startLifetime = life;
            ps.Emit(ep, 1);
        }

        public static void Spark(Vector2 pos, Vector2 vel, Color c, float size, float life)
        {
            Emit(sparks, pos, vel, c, size, life);
        }

        public static void Glow(Vector2 pos, Vector2 vel, Color c, float size, float life)
        {
            Emit(glows, pos, vel, c, size, life);
        }

        public static void Hit(Vector2 pos, Vector2 normal, Color c, int count)
        {
            for (int i = 0; i < count; i++)
            {
                var dir = (normal + Random.insideUnitCircle * 0.9f).normalized;
                Spark(pos, dir * Random.Range(6f, 16f), c, Random.Range(0.25f, 0.5f), Random.Range(0.15f, 0.35f));
            }
            Glow(pos, Vector2.zero, c * 0.8f, 1.6f, 0.12f);
        }

        public static void Explosion(Vector2 pos, float scale, Color tint)
        {
            Glow(pos, Vector2.zero, new Color(1f, 0.9f, 0.7f), 6f * scale, 0.25f);
            Glow(pos, Vector2.zero, new Color(1f, 0.55f, 0.2f), 9f * scale, 0.6f);
            int n = Mathf.RoundToInt(28 * scale);
            for (int i = 0; i < n; i++)
            {
                var v = Random.insideUnitCircle.normalized * Random.Range(4f, 22f) * Mathf.Sqrt(scale);
                var c = Random.value < 0.6f ? new Color(1f, Random.Range(0.5f, 0.85f), 0.25f) : tint;
                Spark(pos, v, c, Random.Range(0.3f, 0.8f) * Mathf.Sqrt(scale), Random.Range(0.4f, 1.1f));
            }
            for (int i = 0; i < 6 * scale; i++)
            {
                var v = Random.insideUnitCircle * 5f * scale;
                Glow(pos + Random.insideUnitCircle * scale, v, new Color(0.9f, 0.35f, 0.15f, 0.6f), Random.Range(2f, 4f) * scale, Random.Range(0.6f, 1.4f));
            }
        }

        public static void Clear()
        {
            if (sparks != null) sparks.Clear();
            if (glows != null) glows.Clear();
        }
    }
}
