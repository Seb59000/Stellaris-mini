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

        public static GUIStyle Panel, Text, Small, Bold, Title, Huge, Center, CenterSmall, Btn, BtnFocus, BtnOff, BtnSmall, BtnSmallFocus, BtnSmallOff, Shadow;
        static Texture2D white, panelTex, btnTex, btnHover, btnActive, btnFocusTex, btnOffTex;
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

        static Texture2D Frame(Color fill, Color border)
        {
            var t = new Texture2D(8, 8, TextureFormat.RGBA32, false);
            t.filterMode = FilterMode.Point;
            var px = new Color[64];
            for (int y = 0; y < 8; y++)
                for (int x = 0; x < 8; x++)
                    px[y * 8 + x] = (x == 0 || y == 0 || x == 7 || y == 7) ? border : fill;
            t.SetPixels(px);
            t.Apply();
            return t;
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
                panelTex = Frame(new Color(0.03f, 0.06f, 0.12f, 0.9f), new Color(0.3f, 0.55f, 0.85f, 0.55f));
                btnTex = Frame(new Color(0.07f, 0.15f, 0.27f, 0.95f), new Color(0.3f, 0.55f, 0.9f, 0.8f));
                btnHover = Frame(new Color(0.12f, 0.25f, 0.42f, 0.98f), new Color(0.5f, 0.75f, 1f, 0.9f));
                btnActive = Frame(new Color(0.2f, 0.4f, 0.65f, 1f), new Color(0.7f, 0.9f, 1f, 1f));
                btnFocusTex = Frame(new Color(0.16f, 0.32f, 0.55f, 1f), new Color(1f, 0.82f, 0.3f, 1f));
                btnOffTex = Frame(new Color(0.05f, 0.07f, 0.1f, 0.9f), new Color(0.2f, 0.24f, 0.3f, 0.8f));
            }

            var textColor = new Color(0.85f, 0.9f, 1f);
            var border = new RectOffset(2, 2, 2, 2);

            Panel = new GUIStyle();
            Panel.normal.background = panelTex;
            Panel.border = border;

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

            Btn = MakeButton(F(19), btnTex, textColor);
            BtnFocus = MakeButton(F(19), btnFocusTex, Color.white);
            BtnOff = MakeButton(F(19), btnOffTex, new Color(0.45f, 0.5f, 0.58f));
            BtnSmall = MakeButton(F(16), btnTex, textColor);
            BtnSmallFocus = MakeButton(F(16), btnFocusTex, Color.white);
            BtnSmallOff = MakeButton(F(16), btnOffTex, new Color(0.45f, 0.5f, 0.58f));
        }

        static GUIStyle MakeButton(int size, Texture2D normal, Color text)
        {
            var b = new GUIStyle(GUI.skin.button) { fontSize = size, richText = true, alignment = TextAnchor.MiddleCenter, wordWrap = false };
            b.border = new RectOffset(2, 2, 2, 2);
            b.padding = new RectOffset(6, 6, 2, 2);
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

        public static void Bar(Rect v, float t, Color c)
        {
            Fill(v, new Color(0, 0, 0, 0.55f));
            var f = v;
            f.width = v.width * Mathf.Clamp01(t);
            Fill(f, c);
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
