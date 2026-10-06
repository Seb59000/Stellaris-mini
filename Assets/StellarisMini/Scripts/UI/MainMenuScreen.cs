using UnityEngine;

namespace StellarisMini
{
    /// Menu principal : paramètres de la partie, aide et lancement.
    public class MainMenuScreen
    {
        readonly GameManager gm;
        readonly GameSettings settings = new GameSettings();
        bool helpOpen;

        public MainMenuScreen(GameManager manager)
        {
            gm = manager;
        }

        public void Update()
        {
            if (helpOpen && (GameInput.Back || GameInput.PadDown(Pad.View) || GameInput.KeyDown(KeyId.F1))) helpOpen = false;
            else if (!helpOpen && (GameInput.KeyDown(KeyId.F1) || GameInput.PadDown(Pad.View))) { helpOpen = true; UI.ResetFocus(); }
        }

        public void OnGUI()
        {
            if (helpOpen)
            {
                if (Help.Draw()) { helpOpen = false; UI.ResetFocus(); }
                return;
            }

            float cx = UI.W / 2f;
            float t = Time.unscaledTime;
            var glow = Color.Lerp(new Color(0.45f, 0.75f, 1f), new Color(0.8f, 0.9f, 1f), 0.5f + 0.5f * Mathf.Sin(t * 1.5f));
            UI.Label(new Rect(cx - 600, 150, 1200, 110), Palette.Tint("STELLARIS MINI", glow), UI.Huge);
            UI.Label(new Rect(cx - 600, 255, 1200, 36), "Bâtissez un empire galactique… et pilotez vous-même vos vaisseaux au combat.", UI.Center);

            float w = 520;
            var L = new VLayout(cx - w / 2f, 360, w, 12);
            UI.PanelBox(new Rect(cx - w / 2f - 24, 336, w + 48, 470));
            UI.NavEnabled = true;

            int d = UI.Option(L.Next(50), "Galaxie", GameSettings.SizeNames[settings.galaxySize] + " (" + GameSettings.SystemCounts[settings.galaxySize] + " systèmes)");
            if (d != 0) settings.galaxySize = (settings.galaxySize + d + 3) % 3;

            d = UI.Option(L.Next(50), "Empires rivaux", settings.rivals.ToString());
            if (d != 0) settings.rivals = Mathf.Clamp(settings.rivals + d, 1, 4);

            d = UI.Option(L.Next(50), "Difficulté", GameSettings.DifficultyNames[settings.difficulty]);
            if (d != 0) settings.difficulty = (settings.difficulty + d + 3) % 3;

            L.Space(10);
            if (UI.Button(L.Next(60), "<b>Nouvelle partie</b>")) { gm.NewGame(settings); return; }
            if (UI.Button(L.Next(50), "Aide et contrôles")) { helpOpen = true; UI.ResetFocus(); }
            if (UI.Button(L.Next(50), "Quitter")) gm.Quit();
            UI.NavEnabled = false;

            string device = GameInput.UsingGamepad
                ? "Manette détectée — [Croix] naviguer, [A] valider"
                : GameInput.GamepadConnected ? "Manette connectée : appuyez sur un bouton pour l'utiliser" : "Clavier et souris";
            UI.Label(new Rect(0, UI.H - 50, UI.W, 30), device, UI.CenterSmall);
        }
    }

    /// Écran d'aide (contrôles et règles), partagé par tous les écrans.
    public static class Help
    {
        /// Dessine l'aide. Renvoie true si le joueur demande à la fermer.
        public static bool Draw()
        {
            UI.FillScreen(new Rect(0, 0, Screen.width, Screen.height), new Color(0, 0, 0, 0.6f));
            float w = Mathf.Min(1500, UI.W - 60), h = 900;
            var area = new Rect((UI.W - w) / 2f, (UI.H - h) / 2f, w, h);
            UI.PanelBox(area);
            UI.Label(new Rect(area.x + 30, area.y + 20, w - 60, 40), "Aide et contrôles", UI.Title);

            float colW = (w - 90) / 3f;
            float y = area.y + 76;
            Column(new Rect(area.x + 30, y, colW, h - 170), "<b>Carte galactique</b>",
                "<b>Clavier / souris</b>\n" +
                "Clic gauche : sélectionner\n" +
                "Clic droit : déplacer la flotte\n" +
                "ZQSD / WASD / flèches : caméra\n" +
                "Clic molette (glisser) : caméra\n" +
                "Molette : zoom\n" +
                "Espace : pause   •   1, 2, 3 : vitesse\n" +
                "Tab : flotte suivante   •   E : empire\n" +
                "Échap : désélection / menu\n\n" +
                "<b>Manette</b>\n" +
                "Stick gauche : caméra (viseur au centre)\n" +
                "Stick droit, LT / RT : zoom\n" +
                "A : sélectionner   •   B : annuler\n" +
                "X : déplacer la flotte vers le système visé\n" +
                "Y : naviguer dans les panneaux\n" +
                "LB / RB : flotte précédente / suivante\n" +
                "Croix bas : pause   •   gauche/droite : vitesse\n" +
                "Croix haut : panneau Empire\n" +
                "Start : menu   •   View : aide");

            Column(new Rect(area.x + 45 + colW, y, colW, h - 170), "<b>Combat piloté</b>",
                "<b>Clavier / souris</b>\n" +
                "ZQSD / WASD : propulsion\n" +
                "Souris : viser\n" +
                "Clic gauche : canons\n" +
                "Clic droit : missiles\n" +
                "Maj : postcombustion\n" +
                "Tab : changer de vaisseau\n" +
                "Échap : pause (retraite, résolution auto)\n\n" +
                "<b>Manette</b>\n" +
                "Stick gauche : propulsion\n" +
                "Stick droit : viser (visée assistée)\n" +
                "RT : canons   •   LT : missiles\n" +
                "A ou LB : postcombustion\n" +
                "Y : changer de vaisseau\n" +
                "Start : pause\n\n" +
                "Vous pilotez le vaisseau amiral, l'IA dirige le reste de la flotte. " +
                "S'il est détruit, vous prenez automatiquement le contrôle d'un autre vaisseau.");

            Column(new Rect(area.x + 60 + 2 * colW, y, colW, h - 170), "<b>Règles</b>",
                "Le temps s'écoule en continu : mettez en pause pour donner vos ordres.\n\n" +
                "<b>Expansion</b> : revendiquez les systèmes voisins de votre territoire. Une planète habitable permet une colonie, sinon un avant-poste.\n\n" +
                "<b>Économie</b> : chaque colonie a une spécialisation (énergie, mines, industrie, recherche). Les alliages servent à construire des vaisseaux, l'énergie à les entretenir.\n\n" +
                "<b>Recherche</b> : améliore armes, boucliers, propulsion et production.\n\n" +
                "<b>Guerre</b> : une flotte immobile dans un système ennemi sans défenseur l'assiège puis le conquiert. " +
                "Quand vos flottes rencontrent l'ennemi, vous pouvez piloter la bataille ou la résoudre automatiquement.\n\n" +
                "<b>Victoire</b> : éliminez tous les empires rivaux.");

            UI.NavEnabled = true;
            bool close = UI.Button(new Rect(area.x + (w - 300) / 2f, area.yMax - 74, 300, 50), "Fermer");
            UI.NavEnabled = false;
            return close;
        }

        static void Column(Rect r, string title, string body)
        {
            UI.Label(new Rect(r.x, r.y, r.width, 30), title, UI.Bold);
            UI.Label(new Rect(r.x, r.y + 38, r.width, r.height - 38), body, UI.Small);
        }
    }
}
