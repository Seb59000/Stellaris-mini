using UnityEngine;

namespace StellarisMini
{
    public enum GameState { Menu, Galaxy, Combat }

    /// Point d'entrée du jeu. Il est créé automatiquement au lancement (aucune scène à préparer)
    /// et orchestre les trois écrans : menu principal, carte galactique et combat piloté.
    public class GameManager : MonoBehaviour
    {
        public static GameManager I { get; private set; }

        public Camera Cam { get; private set; }
        public GameState State { get; private set; }
        public Galaxy Galaxy { get; private set; }
        public GalaxySim Sim { get; private set; }

        MainMenuScreen menu;
        GalaxyView view;
        GalaxyScreen galaxyScreen;
        ICombat combat;
        GameObject menuBackdrop;
        float menuAngle;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (I != null) return;
            new GameObject("Stellaris Mini").AddComponent<GameManager>();
        }

        void Awake()
        {
            if (I != null && I != this) { Destroy(gameObject); return; }
            I = this;
            Application.targetFrameRate = 120;
            Application.runInBackground = true;

            Gfx.Init();
            Sfx.Init(gameObject);
            Fx.Init(transform);
            SetupCamera();

            menuBackdrop = new GameObject("Menu Backdrop");
            menuBackdrop.transform.SetParent(transform, false);
            Gfx.Starfield(menuBackdrop.transform, 2500, 60f, 20f, 300f, 7);
            Gfx.Nebulae(menuBackdrop.transform, 8, 120f, 250f, 11);

            menu = new MainMenuScreen(this);
            State = GameState.Menu;
        }

        void SetupCamera()
        {
            Cam = Camera.main;
            if (Cam == null)
            {
                var go = new GameObject("Main Camera");
                go.tag = "MainCamera";
                Cam = go.AddComponent<Camera>();
            }
            if (Cam.GetComponent<AudioListener>() == null) Cam.gameObject.AddComponent<AudioListener>();
            Cam.orthographic = false;
            Cam.fieldOfView = 50f;
            Cam.nearClipPlane = 0.3f;
            Cam.farClipPlane = 3000f;
            Cam.clearFlags = CameraClearFlags.SolidColor;
            Cam.backgroundColor = Palette.Background;
            Cam.transform.rotation = Quaternion.identity;   // regarde vers +Z, le plan de jeu est z = 0
            Cam.transform.position = new Vector3(0, 0, -60f);
        }

        void Update()
        {
            GameInput.Update();
            UI.UpdateNav();

            switch (State)
            {
                case GameState.Menu:
                    menu.Update();
                    menuAngle += Time.unscaledDeltaTime * 0.05f;
                    Cam.transform.SetPositionAndRotation(new Vector3(Mathf.Cos(menuAngle) * 25f, Mathf.Sin(menuAngle * 0.7f) * 15f, -60f), Quaternion.identity);
                    break;
                case GameState.Galaxy:
                    galaxyScreen.Update();
                    break;
                case GameState.Combat:
                    combat.Update();
                    break;
            }

            bool showCursor = !GameInput.UsingGamepad;
            if (State == GameState.Combat && combat != null && combat.HidesCursor) showCursor = false;
            if (Cursor.visible != showCursor) Cursor.visible = showCursor;
        }

        void LateUpdate()
        {
            if (State == GameState.Combat && combat != null) combat.LateUpdate();
        }

        void OnGUI()
        {
            UI.Begin();
            switch (State)
            {
                case GameState.Menu: menu.OnGUI(); break;
                case GameState.Galaxy: galaxyScreen.OnGUI(); break;
                case GameState.Combat: combat.OnGUI(); break;
            }
            UI.End();
        }

        void OnDisable()
        {
            GameInput.StopRumble();
            Time.timeScale = 1f;
        }

        void OnApplicationQuit()
        {
            GameInput.StopRumble();
        }

        // ==================================================================
        //  Transitions
        // ==================================================================
        public void NewGame(GameSettings settings)
        {
            CleanupGame();
            settings.seed = Random.Range(1, int.MaxValue);
            Galaxy = GalaxyGenerator.Generate(settings);
            Sim = new GalaxySim(Galaxy);
            view = new GalaxyView(this, Galaxy, Sim);
            galaxyScreen = new GalaxyScreen(this, Galaxy, Sim, view);
            menuBackdrop.SetActive(false);
            State = GameState.Galaxy;
            UI.ResetFocus();
            Sim.Notify("Bienvenue, dirigeant de la " + Galaxy.player.Rich + ". Le temps est en pause.", Palette.Info, Galaxy.player.capital);
        }

        public void ReturnToMenu()
        {
            CleanupGame();
            menuBackdrop.SetActive(true);
            State = GameState.Menu;
            UI.ResetFocus();
        }

        void CleanupGame()
        {
            if (combat != null) { combat.Dispose(); combat = null; }
            if (view != null) { view.Dispose(); view = null; }
            galaxyScreen = null;
            Galaxy = null;
            Sim = null;
            Time.timeScale = 1f;
            Fx.Clear();
        }

        /// Lance le combat piloté pour la bataille en attente.
        public void StartBattle(Battle b)
        {
            view.SetVisible(false);
            Fx.Clear();
            if (Galaxy.settings.combat3D) combat = new CombatSession3D(this, b);
            else combat = new CombatSession(this, b);
            State = GameState.Combat;
            UI.ResetFocus();
        }

        /// Résout la bataille sans la piloter.
        public void AutoResolveBattle(Battle b)
        {
            int playerBefore = CountShips(b.playerFleets), enemyBefore = CountShips(b.enemyFleets);
            var d = Galaxy.settings.Diff;
            bool win = AutoResolve.Run(b.playerFleets, b.enemyFleets, d.playerDamage, d.enemyDamage);
            int playerLost = playerBefore - CountShips(b.playerFleets);
            int enemyLost = enemyBefore - CountShips(b.enemyFleets);
            Sim.FinishBattle(b, win ? BattleOutcome.Victory : BattleOutcome.Defeat, enemyLost, playerLost);
            UI.ResetFocus();
        }

        static int CountShips(System.Collections.Generic.List<Fleet> fleets)
        {
            int n = 0;
            foreach (var f in fleets) foreach (var s in f.ships) if (!s.destroyed && s.hull > 0) n++;
            return n;
        }

        /// Appelé par le combat quand il est terminé.
        public void EndBattle(Battle b, BattleOutcome outcome, int enemyLost, int playerLost)
        {
            if (combat != null) { combat.Dispose(); combat = null; }
            Time.timeScale = 1f;
            Fx.Clear();
            Sim.FinishBattle(b, outcome, enemyLost, playerLost);
            view.SetVisible(true);
            State = GameState.Galaxy;
            UI.ResetFocus();
        }

        public void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
