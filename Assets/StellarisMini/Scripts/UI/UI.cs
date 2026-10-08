using System.Collections.Generic;
using UnityEngine;

namespace StellarisMini
{
    /// Petit kit d'interface basé sur IMGUI :
    ///  - mise à l'échelle automatique (coordonnées virtuelles de hauteur 1080),
    ///  - styles "néon" générés par code,
    ///  - navigation complète à la manette (focus, croix directionnelle, bouton A).
    public static class UI
    {
        public const float RefH = 1080f;
        public static float S { get; private set; }
        public static float W { get; private set; }
        public static float H { get { return RefH; } }

        public static GUIStyle Panel, Pill, Soft, Text, Small, Bold, Title, Huge, Center, CenterSmall, Btn, BtnFocus, BtnOff, BtnSmall, BtnSmallFocus, BtnSmallOff, Shadow;
        static Texture2D white, panelTex, btnTex, btnHover, btnActive, btnFocusTex, btnOffTex, pillTex, softTex;
        static int builtForW = -1, builtForH = -1;

        // --- Navigation manette
        public static bool NavEnabled;
        static int navIndex, navCount, focus;
        static bool activate;
        static int optionDelta;
        static readonly List<bool> optFlags = new List<bool>();
        static readonly List<bool> optFlagsLast = new List<bool>();

        // --- Zones occupées par l'interface (pour ne pas cliquer "à travers")
        static readonly List<Rect> blockers = new List<Rect>();
        static readonly List<Rect> blockersLast = new List<Rect>();

        public static bool HasNavItems { get { return navCount > 0; } }
        public static void ResetFocus() { focus = 0; }

        // ==================================================================
        public static void Begin()
        {
            EnsureStyles();
            navIndex = 0;
            NavEnabled = false;
            if (Event.current.type == EventType.Repaint)
            {
                blockers.Clear();
                optFlags.Clear();
            }
        }

        public static void End()
        {
            if (Event.current.type != EventType.Repaint) return;
            navCount = navIndex;
            if (focus >= navCount) focus = Mathf.Max(0, navCount - 1);
            blockersLast.Clear();
            blockersLast.AddRange(blockers);
            optFlagsLast.Clear();
            optFlagsLast.AddRange(optFlags);
            activate = false;
            optionDelta = 0;
        }

        /// À appeler dans Update() : déplace le focus et déclenche le bouton sélectionné.
        public static void UpdateNav()
        {
            if (navCount <= 0 || !GameInput.UsingGamepad) return;
            var d = GameInput.NavDir;
            bool isOpt = focus < optFlagsLast.Count && optFlagsLast[focus];
            if (d.y > 0) focus = (focus - 1 + navCount) % navCount;
            else if (d.y < 0) focus = (focus + 1) % navCount;
            else if (d.x != 0)
            {
                if (isOpt) optionDelta = d.x;
                else focus = (focus + d.x + navCount) % navCount;
            }
            if (GameInput.PadDown(Pad.A)) activate = true;
        }

        public static bool IsPointerOverUI(Vector2 screenPos)
        {
            var p = new Vector2(screenPos.x, Screen.height - screenPos.y);
            foreach (var r in blockersLast) if (r.Contains(p)) return true;
            return false;
        }

        // ==================================================================
        //  Styles
        // ==================================================================
        static int F(float size) { return Mathf.Max(8, Mathf.RoundToInt(size * S)); }

        /// Texture "9-slice" à coins arrondis et anticrénelés, avec dégradé vertical et liseré.
        /// La bordure du style doit valoir (Corner + 1) pour que les coins ne soient pas déformés.
        static Texture2D Rounded(float radius, float borderW, Color top, Color bottom, Color border, int minHeight)
        {
            int b = Mathf.CeilToInt(radius) + 1;
            int w = 2 * b + 4;
            int h = Mathf.Max(2 * b + 4, minHeight);
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
            t.filterMode = FilterMode.Bilinear;
            t.wrapMode = TextureWrapMode.Clamp;
            var px = new Color[w * h];
            float hw = w * 0.5f, hh = h * 0.5f;
            for (int y = 0; y < h; y++)
            {
                var fill = Color.Lerp(bottom, top, h > 1 ? y / (h - 1f) : 0f);
                for (int x = 0; x < w; x++)
                {
                    // Distance signée au rectangle arrondi (négative à l'intérieur)
                    float qx = Mathf.Abs(x + 0.5f - hw) - (hw - radius);
                    float qy = Mathf.Abs(y + 0.5f - hh) - (hh - radius);
                    float ox = Mathf.Max(qx, 0f), oy = Mathf.Max(qy, 0f);
                    float sd = Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
                    float alpha = Mathf.Clamp01(0.5f - sd);
                    float edge = borderW > 0f ? Mathf.Clamp01(sd + borderW + 0.5f) : 0f;
                    var c = Color.Lerp(fill, border, edge);
                    c.a *= alpha;
                    px[y * w + x] = c;
                }
            }
            t.SetPixels(px);
            t.Apply();
            return t;
        }

        static int Corner(float radius) { return Mathf.CeilToInt(radius) + 1; }

        static void DestroyTex(Texture2D t)
        {
            if (t != null) Object.Destroy(t);
        }

        static void EnsureStyles()
        {
            if (builtForW == Screen.width && builtForH == Screen.height && Panel != null) return;
            builtForW = Screen.width;
            builtForH = Screen.height;
            S = Mathf.Max(0.4f, Screen.height / RefH);
            W = Screen.width / S;

            if (white == null)
            {
                white = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                white.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
                white.Apply();
            }

            // Les coins sont recalculés à chaque changement de résolution pour garder le même arrondi.
            DestroyTex(panelTex); DestroyTex(btnTex); DestroyTex(btnHover); DestroyTex(btnActive);
            DestroyTex(btnFocusTex); DestroyTex(btnOffTex); DestroyTex(pillTex); DestroyTex(softTex);
            float rp = Mathf.Max(5f, 16f * S);    // panneaux
            float rb = Mathf.Max(4f, 12f * S);    // boutons
            float rs = Mathf.Max(2f, 4f * S);     // jauges
            float line = Mathf.Max(1f, 1.5f * S);
            int tall = Mathf.RoundToInt(64 * S);
            panelTex = Rounded(rp, line, new Color(0.05f, 0.1f, 0.19f, 0.93f), new Color(0.02f, 0.04f, 0.09f, 0.93f), new Color(0.32f, 0.58f, 0.9f, 0.55f), tall);
            btnTex = Rounded(rb, line, new Color(0.15f, 0.3f, 0.5f, 0.97f), new Color(0.06f, 0.13f, 0.26f, 0.97f), new Color(0.38f, 0.64f, 1f, 0.85f), tall);
            btnHover = Rounded(rb, line, new Color(0.22f, 0.42f, 0.66f, 1f), new Color(0.1f, 0.21f, 0.38f, 1f), new Color(0.6f, 0.85f, 1f, 1f), tall);
            btnActive = Rounded(rb, line, new Color(0.3f, 0.55f, 0.82f, 1f), new Color(0.17f, 0.34f, 0.58f, 1f), new Color(0.8f, 0.95f, 1f, 1f), tall);
            btnFocusTex = Rounded(rb, Mathf.Max(2f, 3f * S), new Color(0.25f, 0.45f, 0.7f, 1f), new Color(0.12f, 0.25f, 0.44f, 1f), new Color(1f, 0.82f, 0.3f, 1f), tall);
            btnOffTex = Rounded(rb, line, new Color(0.08f, 0.1f, 0.14f, 0.92f), new Color(0.05f, 0.06f, 0.09f, 0.92f), new Color(0.22f, 0.26f, 0.32f, 0.7f), tall);
            pillTex = Rounded(rs, 0f, Color.white, Color.white, Color.white, 0);
            softTex = Rounded(Mathf.Max(4f, 10f * S), 0f, Color.white, Color.white, Color.white, 0);

            var textColor = new Color(0.85f, 0.9f, 1f);
            int cp = Corner(rp), cb = Corner(rb);

            Panel = new GUIStyle();
            Panel.normal.background = panelTex;
            Panel.border = new RectOffset(cp, cp, cp, cp);

            Pill = new GUIStyle();
            Pill.normal.background = pillTex;
            int cs = Corner(rs);
            Pill.border = new RectOffset(cs, cs, cs, cs);

            Soft = new GUIStyle();
            Soft.normal.background = softTex;
            int cf = Corner(Mathf.Max(4f, 10f * S));
            Soft.border = new RectOffset(cf, cf, cf, cf);

            Text = new GUIStyle(GUI.skin.label) { fontSize = F(19), richText = true, wordWrap = true, alignment = TextAnchor.UpperLeft };
            Text.normal.textColor = textColor;
            Text.padding = new RectOffset(0, 0, 0, 0);
            Small = new GUIStyle(Text) { fontSize = F(16) };
            Small.normal.textColor = new Color(0.7f, 0.78f, 0.9f);
            Bold = new GUIStyle(Text) { fontSize = F(21), fontStyle = FontStyle.Bold };
            Bold.normal.textColor = Color.white;
            Title = new GUIStyle(Bold) { fontSize = F(28) };
            Huge = new GUIStyle(Bold) { fontSize = F(72), alignment = TextAnchor.MiddleCenter, wordWrap = false };
            Center = new GUIStyle(Text) { alignment = TextAnchor.MiddleCenter };
            CenterSmall = new GUIStyle(Small) { alignment = TextAnchor.MiddleCenter, wordWrap = false };
            Shadow = new GUIStyle(CenterSmall);
            Shadow.normal.textColor = new Color(0, 0, 0, 0.85f);

            Btn = MakeButton(F(19), btnTex, textColor, cb);
            BtnFocus = MakeButton(F(19), btnFocusTex, Color.white, cb);
            BtnOff = MakeButton(F(19), btnOffTex, new Color(0.45f, 0.5f, 0.58f), cb);
            BtnSmall = MakeButton(F(16), btnTex, textColor, cb);
            BtnSmallFocus = MakeButton(F(16), btnFocusTex, Color.white, cb);
            BtnSmallOff = MakeButton(F(16), btnOffTex, new Color(0.45f, 0.5f, 0.58f), cb);
        }

        static GUIStyle MakeButton(int size, Texture2D normal, Color text, int corner)
        {
            var b = new GUIStyle(GUI.skin.button) { fontSize = size, richText = true, alignment = TextAnchor.MiddleCenter, wordWrap = false };
            b.border = new RectOffset(corner, corner, corner, corner);
            b.padding = new RectOffset(corner, corner, 2, 2);
            b.normal.background = normal;
            b.normal.textColor = text;
            b.hover.background = normal == btnTex ? btnHover : normal;
            b.hover.textColor = Color.white;
            b.active.background = normal == btnOffTex ? normal : btnActive;
            b.active.textColor = Color.white;
            b.focused.background = normal;
            b.focused.textColor = text;
            b.onNormal.background = normal;
            b.onHover.background = normal;
            b.onActive.background = normal;
            b.onFocused.background = normal;
            return b;
        }

        // ==================================================================
        //  Primitives
        // ==================================================================
        public static Rect R(float x, float y, float w, float h) { return new Rect(x * S, y * S, w * S, h * S); }
        public static Rect Sc(Rect v) { return new Rect(v.x * S, v.y * S, v.width * S, v.height * S); }

        public static void PanelBox(Rect v)
        {
            var r = Sc(v);
            GUI.Box(r, GUIContent.none, Panel);
            if (Event.current.type == EventType.Repaint) blockers.Add(r);
        }

        public static void Block(Rect v)
        {
            if (Event.current.type == EventType.Repaint) blockers.Add(Sc(v));
        }

        public static void Label(Rect v, string text, GUIStyle st = null)
        {
            GUI.Label(Sc(v), text, st ?? Text);
        }

        public static void Fill(Rect v, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(Sc(v), white);
            GUI.color = old;
        }

        public static void FillScreen(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, white);
            GUI.color = old;
        }

        /// Dessine un style (texture à coins arrondis) teinté d'une couleur, en pixels écran.
        static void Tinted(GUIStyle st, Rect screenRect, Color c)
        {
            if (Event.current.type != EventType.Repaint) return;
            var old = GUI.color;
            GUI.color = c;
            st.Draw(screenRect, false, false, false, false);
            GUI.color = old;
        }

        /// Rectangle plein à coins arrondis (coordonnées virtuelles).
        public static void RoundFill(Rect v, Color c)
        {
            Tinted(Soft, Sc(v), c);
        }

        /// Pastille arrondie (coordonnées virtuelles).
        public static void Dot(Rect v, Color c)
        {
            Tinted(Soft, Sc(v), c);
        }

        /// Pastille arrondie en pixels écran.
        public static void DotScreen(Rect r, Color c)
        {
            Tinted(Soft, r, c);
        }

        /// Jauge arrondie.
        public static void Bar(Rect v, float t, Color c)
        {
            var r = Sc(v);
            Tinted(Pill, r, new Color(0, 0, 0, 0.55f));
            t = Mathf.Clamp01(t);
            if (t <= 0f) return;
            var f = r;
            f.width = Mathf.Max(r.height, r.width * t);
            Tinted(Pill, f, c);
        }

        /// Jauge arrondie en pixels écran (HUD de combat).
        public static void BarScreen(Rect r, float t, Color c)
        {
            Tinted(Pill, r, new Color(0, 0, 0, 0.6f));
            t = Mathf.Clamp01(t);
            if (t <= 0f) return;
            var f = r;
            f.width = Mathf.Max(r.height, r.width * t);
            Tinted(Pill, f, c);
        }

        /// Texte avec ombre, centré sur une position écran (coordonnées GUI, en pixels réels).
        public static void ScreenLabel(Vector2 guiPos, string text, Color color, GUIStyle st = null)
        {
            st = st ?? CenterSmall;
            var size = st.CalcSize(new GUIContent(text));
            var r = new Rect(guiPos.x - size.x / 2f, guiPos.y - size.y / 2f, size.x, size.y);
            var sh = r;
            sh.x += 1; sh.y += 1;
            var old = st.normal.textColor;
            st.normal.textColor = new Color(0, 0, 0, 0.8f * color.a);
            GUI.Label(sh, StripColor(text), st);
            st.normal.textColor = color;
            GUI.Label(r, text, st);
            st.normal.textColor = old;
        }

        static string StripColor(string s)
        {
            if (s.IndexOf('<') < 0) return s;
            var sb = new System.Text.StringBuilder();
            bool tag = false;
            foreach (char ch in s)
            {
                if (ch == '<') tag = true;
                else if (ch == '>') tag = false;
                else if (!tag) sb.Append(ch);
            }
            return sb.ToString();
        }

        // ==================================================================
        //  Boutons (souris + manette)
        // ==================================================================
        public static bool Button(Rect v, string text, bool enabled = true, bool small = false)
        {
            var r = Sc(v);
            int idx = -1;
            if (NavEnabled)
            {
                idx = navIndex++;
                if (Event.current.type == EventType.Repaint) optFlags.Add(false);
            }
            bool focused = idx >= 0 && GameInput.UsingGamepad && idx == focus;
            GUIStyle st = !enabled ? (small ? BtnSmallOff : BtnOff) : focused ? (small ? BtnSmallFocus : BtnFocus) : (small ? BtnSmall : Btn);

            bool clicked = false;
            if (enabled) clicked = GUI.Button(r, text, st);
            else GUI.Box(r, text, st);

            if (focused && activate && Event.current.type == EventType.Repaint)
            {
                activate = false;
                if (enabled) clicked = true;
            }
            if (clicked) Sfx.Click();
            if (Event.current.type == EventType.Repaint) blockers.Add(r);
            return clicked;
        }

        /// Ligne d'option à valeurs multiples. Renvoie -1, 0 ou +1.
        public static int Option(Rect v, string label, string value)
        {
            var r = Sc(v);
            int idx = -1;
            if (NavEnabled)
            {
                idx = navIndex++;
                if (Event.current.type == EventType.Repaint) optFlags.Add(true);
            }
            bool focused = idx >= 0 && GameInput.UsingGamepad && idx == focus;
            var st = focused ? BtnFocus : Btn;

            int delta = 0;
            float aw = 48f;
            var left = new Rect(v.x, v.y, aw, v.height);
            var mid = new Rect(v.x + aw + 4, v.y, v.width - 2 * aw - 8, v.height);
            var right = new Rect(v.x + v.width - aw, v.y, aw, v.height);
            if (GUI.Button(Sc(left), "<", st)) delta = -1;
            if (GUI.Button(Sc(mid), label + " : <b>" + value + "</b>", st)) delta = 1;
            if (GUI.Button(Sc(right), ">", st)) delta = 1;

            if (focused && Event.current.type == EventType.Repaint)
            {
                if (activate) { activate = false; delta = 1; }
                else if (optionDelta != 0) { delta = optionDelta; optionDelta = 0; }
            }
            if (delta != 0) Sfx.Click();
            if (Event.current.type == EventType.Repaint) blockers.Add(r);
            return delta;
        }
    }

    /// Mise en page verticale simple.
    public class VLayout
    {
        public float x, y, w, gap;
        public VLayout(float x, float y, float w, float gap = 6f) { this.x = x; this.y = y; this.w = w; this.gap = gap; }
        public Rect Next(float h) { var r = new Rect(x, y, w, h); y += h + gap; return r; }
        public void Space(float h) { y += h; }
    }
}
