using UnityEngine;

namespace StellarisMini
{
    /// Effets sonores synthétisés au démarrage (aucun fichier audio nécessaire).
    public static class Sfx
    {
        const int Rate = 22050;
        static AudioSource[] sources;
        static int next;
        static AudioClip laser, heavy, missile, hit, shieldHit, explosion, bigExplosion, click, confirm, alert, notify, boost;
        static readonly float[] lastPlay = new float[16];
        public static float Volume = 0.7f;

        public static void Init(GameObject host)
        {
            if (sources != null && sources[0] != null) return;
            sources = new AudioSource[12];
            for (int i = 0; i < sources.Length; i++)
            {
                var s = host.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.spatialBlend = 0f;
                sources[i] = s;
            }
            var rng = new System.Random(1234);

            laser = Sweep("laser", 0.13f, 1500f, 380f, 28f, 0.55f, rng, 0.05f);
            heavy = Sweep("heavy", 0.4f, 420f, 70f, 7f, 0.75f, rng, 0.25f);
            missile = Noise("missile", 0.45f, rng, 0.6f, 6f, true);
            hit = Noise("hit", 0.09f, rng, 0.5f, 40f, false);
            shieldHit = Sweep("shield", 0.12f, 900f, 1300f, 30f, 0.35f, rng, 0f);
            explosion = Boom("explosion", 1.0f, rng, 3.5f);
            bigExplosion = Boom("bigexplosion", 2.0f, rng, 1.8f);
            click = Sweep("click", 0.05f, 1100f, 900f, 60f, 0.35f, rng, 0f);
            confirm = Tones("confirm", new[] { 660f, 990f }, 0.08f);
            alert = Tones("alert", new[] { 620f, 830f, 620f, 830f }, 0.13f);
            notify = Tones("notify", new[] { 880f, 1320f }, 0.06f);
            boost = Noise("boost", 0.35f, rng, 0.35f, 5f, true);
        }

        // ------------------------------------------------------------------
        static AudioClip Make(string name, float[] data)
        {
            var c = AudioClip.Create(name, data.Length, 1, Rate, false);
            c.SetData(data, 0);
            return c;
        }

        static AudioClip Sweep(string name, float dur, float f0, float f1, float decay, float vol, System.Random rng, float noise)
        {
            int n = (int)(dur * Rate);
            var d = new float[n];
            double phase = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float f = Mathf.Lerp(f0, f1, t / dur);
                phase += 2 * Mathf.PI * f / Rate;
                float s = Mathf.Sin((float)phase);
                float sq = s > 0 ? 1f : -1f;
                float v = (s * 0.6f + sq * 0.25f) + ((float)rng.NextDouble() * 2f - 1f) * noise;
                float env = Mathf.Exp(-t * decay) * Mathf.Clamp01(t * 400f);
                d[i] = v * env * vol;
            }
            return Make(name, d);
        }

        static AudioClip Noise(string name, float dur, System.Random rng, float vol, float decay, bool swell)
        {
            int n = (int)(dur * Rate);
            var d = new float[n];
            float lp = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float w = (float)rng.NextDouble() * 2f - 1f;
                lp += (w - lp) * (swell ? Mathf.Lerp(0.05f, 0.4f, t / dur) : 0.6f);
                float env = swell ? Mathf.Sin(Mathf.PI * t / dur) : Mathf.Exp(-t * decay);
                d[i] = lp * env * vol;
            }
            return Make(name, d);
        }

        static AudioClip Boom(string name, float dur, System.Random rng, float decay)
        {
            int n = (int)(dur * Rate);
            var d = new float[n];
            float brown = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float w = (float)rng.NextDouble() * 2f - 1f;
                brown = (brown + 0.04f * w) / 1.02f;
                float env = Mathf.Clamp01(t * 200f) * Mathf.Exp(-t * decay);
                float rumble = Mathf.Sin(2 * Mathf.PI * 48f * t) * Mathf.Exp(-t * decay * 1.2f) * 0.4f;
                d[i] = Mathf.Clamp((brown * 5f + rumble) * env, -1f, 1f) * 0.9f;
            }
            return Make(name, d);
        }

        static AudioClip Tones(string name, float[] freqs, float each)
        {
            int per = (int)(each * Rate);
            var d = new float[per * freqs.Length];
            for (int k = 0; k < freqs.Length; k++)
                for (int i = 0; i < per; i++)
                {
                    float t = i / (float)Rate;
                    float env = Mathf.Clamp01(t * 300f) * Mathf.Exp(-t * 18f);
                    d[k * per + i] = Mathf.Sin(2 * Mathf.PI * freqs[k] * t) * env * 0.35f;
                }
            return Make(name, d);
        }

        // ------------------------------------------------------------------
        static void Play(AudioClip clip, float vol, float pitch, int slot, float minInterval)
        {
            if (sources == null || clip == null || vol <= 0.01f) return;
            float now = Time.unscaledTime;
            if (now - lastPlay[slot] < minInterval) return;
            lastPlay[slot] = now;
            var s = sources[next];
            next = (next + 1) % sources.Length;
            s.pitch = pitch;
            s.PlayOneShot(clip, Mathf.Clamp01(vol * Volume));
        }

        public static void Laser(float vol, bool heavyGun)
        {
            if (heavyGun) Play(heavy, vol, Random.Range(0.9f, 1.1f), 0, 0.05f);
            else Play(laser, vol * 0.6f, Random.Range(0.9f, 1.15f), 1, 0.03f);
        }

        public static void Missile(float vol) { Play(missile, vol, Random.Range(0.9f, 1.1f), 2, 0.08f); }
        public static void Hit(float vol, bool shield)
        {
            if (shield) Play(shieldHit, vol * 0.5f, Random.Range(0.9f, 1.2f), 3, 0.04f);
            else Play(hit, vol, Random.Range(0.8f, 1.2f), 4, 0.04f);
        }
        public static void Explosion(float vol, bool big)
        {
            if (big) Play(bigExplosion, vol, Random.Range(0.85f, 1f), 5, 0.1f);
            else Play(explosion, vol, Random.Range(0.9f, 1.2f), 6, 0.06f);
        }
        public static void Click() { Play(click, 0.5f, 1f, 7, 0.03f); }
        public static void Confirm() { Play(confirm, 0.6f, 1f, 8, 0.05f); }
        public static void Alert() { Play(alert, 0.6f, 1f, 9, 0.5f); }
        public static void Notify() { Play(notify, 0.4f, 1f, 10, 0.25f); }
        public static void Boost() { Play(boost, 0.5f, 1f, 11, 0.3f); }
    }
}
