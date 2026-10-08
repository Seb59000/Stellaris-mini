using UnityEngine;

namespace StellarisMini
{
    /// Une bataille pilotée (vue de dessus ou 3D), vue par le GameManager.
    public interface ICombat
    {
        void Update();
        void LateUpdate();
        void OnGUI();
        void Dispose();
        bool HidesCursor { get; }
    }

    public enum PauseChoice { None, Resume, Retreat, AutoResolve, Help }

    /// Éléments d'interface communs aux deux modes de combat.
    public static class CombatHud
    {
        public static readonly Color ShieldColor = new Color(0.45f, 0.75f, 1f);
        public static readonly Color AllyColor = new Color(0.4f, 1f, 0.55f);
        public static readonly Color EnemyColor = new Color(1f, 0.4f, 0.35f);

        public static void Dim()
        {
            UI.FillScreen(new Rect(0, 0, Screen.width, Screen.height), new Color(0, 0, 0, 0.55f));
        }

        public static void TopInfo(string systemName, int allies, int enemies, Color playerColor, Color enemyColor, string extra)
        {
            float w = 640;
            var r = new Rect((UI.W - w) / 2f, 10, w, 64);
            UI.PanelBox(r);
            UI.Label(new Rect(r.x, r.y + 6, w, 26), "<b>Bataille de " + systemName + "</b>", UI.Center);
            var pc = Palette.Tint("Alliés : " + allies, Color.Lerp(playerColor, Color.white, 0.3f));
            var ec = Palette.Tint("Ennemis : " + enemies, Color.Lerp(enemyColor, Color.white, 0.3f));
            string line = pc + "        " + ec;
            if (!string.IsNullOrEmpty(extra)) line += "        " + extra;
            UI.Label(new Rect(r.x, r.y + 34, w, 24), line, UI.CenterSmall);
        }

        public static void PlayerPanel(bool alive, ShipSpec spec, string shipName, float hull, float maxHull, float shield, float maxShield,
                                       float boost, float missileReady, string extra)
        {
            float h = string.IsNullOrEmpty(extra) ? 180 : 208;
            var r = new Rect(16, UI.H - h - 16, 440, h);
            UI.PanelBox(r);
            var L = new VLayout(r.x + 16, r.y + 12, r.width - 32, 6);
            if (!alive)
            {
                UI.Label(L.Next(30), Palette.Tint("Transfert du commandement…", Palette.Warning), UI.Bold);
                return;
            }
            UI.Label(L.Next(28), "<b>" + Names.Ships[(int)spec.cls] + " « " + shipName + " »</b>", UI.Bold);
            BarRow(L.Next(22), "Coque", hull / maxHull, Color.Lerp(Palette.Bad, Palette.Good, hull / maxHull));
            BarRow(L.Next(22), "Boucliers", maxShield > 0 ? shield / maxShield : 0f, ShieldColor);
            BarRow(L.Next(22), "Postcombustion", boost, new Color(0.6f, 0.85f, 1f));
            if (spec.missileSalvo > 0)
                BarRow(L.Next(22), missileReady >= 1f ? "Missiles prêts" : "Missiles", missileReady, new Color(1f, 0.7f, 0.3f));
            else UI.Label(L.Next(22), Palette.Tint("Pas de missiles sur ce modèle", Palette.Neutral), UI.Small);
            if (!string.IsNullOrEmpty(extra)) UI.Label(L.Next(22), extra, UI.Small);
        }

        public static void BarRow(Rect r, string label, float t, Color c)
        {
            UI.Label(new Rect(r.x, r.y, 150, r.height), label, UI.Small);
            UI.Bar(new Rect(r.x + 160, r.y + 6, r.width - 160, 10), t, c);
        }

        /// Petites jauges (boucliers + coque) au-dessus d'un vaisseau, en pixels écran.
        public static void ShipBars(Vector2 gp, float width, float hullRatio, float shieldRatio, bool ally)
        {
            float s = UI.S;
            var r = new Rect(gp.x - width / 2f, gp.y, width, 5f * s);
            UI.BarScreen(r, hullRatio, ally ? AllyColor : EnemyColor);
            if (shieldRatio > 0f) UI.BarScreen(new Rect(r.x, r.y - 6f * s, width, 4f * s), shieldRatio, ShieldColor);
        }

        /// Indicateur d'un ennemi hors de l'écran, collé au bord.
        public static void Offscreen(Vector2 gp, bool front, float size, Color c)
        {
            float m = 24f * UI.S;
            var center = new Vector2(Screen.width / 2f, Screen.height / 2f);
            if (front && gp.x > m && gp.y > m && gp.x < Screen.width - m && gp.y < Screen.height - m) return;
            var d = gp - center;
            if (!front) d = -d;
            if (d.sqrMagnitude < 1f) d = Vector2.down;
            float k = Mathf.Min((center.x - m) / Mathf.Max(0.001f, Mathf.Abs(d.x)), (center.y - m) / Mathf.Max(0.001f, Mathf.Abs(d.y)));
            var p = center + d * k;
            UI.DotScreen(new Rect(p.x - size / 2f - 3, p.y - size / 2f - 3, size + 6, size + 6), new Color(0, 0, 0, 0.6f));
            UI.DotScreen(new Rect(p.x - size / 2f, p.y - size / 2f, size, size), c);
        }

        public static void Cross(Vector2 c, float size, Color col)
        {
            float s = UI.S;
            float a = size * s, g = size * 0.35f * s, t = Mathf.Max(1f, 2f * s);
            UI.FillScreen(new Rect(c.x - a, c.y - t / 2f, a - g, t), col);
            UI.FillScreen(new Rect(c.x + g, c.y - t / 2f, a - g, t), col);
            UI.FillScreen(new Rect(c.x - t / 2f, c.y - a, t, a - g), col);
            UI.FillScreen(new Rect(c.x - t / 2f, c.y + g, t, a - g), col);
        }

        /// Crochets autour d'une cible.
        public static void Brackets(Vector2 c, float half, Color col)
        {
            float t = Mathf.Max(1f, 2f * UI.S), l = half * 0.45f;
            UI.FillScreen(new Rect(c.x - half, c.y - half, l, t), col);
            UI.FillScreen(new Rect(c.x - half, c.y - half, t, l), col);
            UI.FillScreen(new Rect(c.x + half - l, c.y - half, l, t), col);
            UI.FillScreen(new Rect(c.x + half - t, c.y - half, t, l), col);
            UI.FillScreen(new Rect(c.x - half, c.y + half - t, l, t), col);
            UI.FillScreen(new Rect(c.x - half, c.y + half - l, t, l), col);
            UI.FillScreen(new Rect(c.x + half - l, c.y + half - t, l, t), col);
            UI.FillScreen(new Rect(c.x + half - t, c.y + half - l, t, l), col);
        }

        public static void Hints(string text)
        {
            UI.Label(new Rect(470, UI.H - 34, UI.W - 490, 26), text, UI.CenterSmall);
        }

        public static void Banner(string text, float time, bool ending, bool victory)
        {
            if (time <= 0f || string.IsNullOrEmpty(text)) return;
            var col = ending ? (victory ? Palette.Good : Palette.Bad) : new Color(1f, 0.95f, 0.8f);
            col.a = Mathf.Clamp01(time / 0.5f);
            var st = ending ? UI.Huge : UI.Title;
            var old = st.alignment;
            st.alignment = TextAnchor.MiddleCenter;
            UI.Label(new Rect(0, UI.H * 0.22f, UI.W, 100), Palette.Tint(text, col), st);
            st.alignment = old;
        }

        public static PauseChoice PauseMenu()
        {
            Dim();
            UI.NavEnabled = true;
            float w = 520, h = 420;
            var area = new Rect((UI.W - w) / 2f, (UI.H - h) / 2f, w, h);
            UI.PanelBox(area);
            var L = new VLayout(area.x + 30, area.y + 24, w - 60, 10);
            UI.Label(L.Next(40), "Combat en pause", UI.Title);
            var choice = PauseChoice.None;
            if (UI.Button(L.Next(50), "Reprendre")) choice = PauseChoice.Resume;
            if (UI.Button(L.Next(50), "Battre en retraite")) choice = PauseChoice.Retreat;
            if (UI.Button(L.Next(50), "Résolution automatique")) choice = PauseChoice.AutoResolve;
            if (UI.Button(L.Next(50), "Aide et contrôles")) choice = PauseChoice.Help;
            UI.Label(L.Next(60), "La retraite ramène vos vaisseaux survivants dans un système voisin.", UI.Small);
            return choice;
        }

        /// Écran de fin de bataille. Renvoie true quand le joueur veut revenir à la carte.
        public static bool Result(bool win, int enemyLost, int playerLost)
        {
            Dim();
            UI.NavEnabled = true;
            float w = 620, h = 330;
            var area = new Rect((UI.W - w) / 2f, (UI.H - h) / 2f, w, h);
            UI.PanelBox(area);
            UI.Label(new Rect(area.x, area.y + 20, w, 90), win ? Palette.Tint("VICTOIRE", Palette.Good) : Palette.Tint("DÉFAITE", Palette.Bad), UI.Huge);
            UI.Label(new Rect(area.x, area.y + 125, w, 30), "Vaisseaux ennemis détruits : <b>" + enemyLost + "</b>", UI.Center);
            UI.Label(new Rect(area.x, area.y + 158, w, 30), "Vos pertes : <b>" + playerLost + "</b>", UI.Center);
            return UI.Button(new Rect(area.x + (w - 380) / 2f, area.y + 230, 380, 54), "Retour à la carte galactique");
        }
    }
}
