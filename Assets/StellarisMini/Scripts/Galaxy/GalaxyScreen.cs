using System.Collections.Generic;
using UnityEngine;

namespace StellarisMini
{
    /// Écran de la carte galactique : contrôles (souris / manette), panneaux et fenêtres.
    public class GalaxyScreen
    {
        readonly GameManager gm;
        readonly Galaxy g;
        readonly GalaxySim sim;
        readonly GalaxyView view;

        StarSystem selSystem, hoverSystem;
        Fleet selFleet, hoverFleet;
        bool menuMode;      // manette : navigation dans les panneaux
        bool empireOpen;
        bool pauseMenu;
        bool helpOpen;
        bool battleAlerted;
        Vector2? camTarget;

        const float TopH = 56f;
        const float PanelW = 440f;
        const float HintH = 44f;

        public GalaxyScreen(GameManager manager, Galaxy galaxy, GalaxySim simulation, GalaxyView galaxyView)
        {
            gm = manager;
            g = galaxy;
            sim = simulation;
            view = galaxyView;
            selSystem = g.player.capital;
        }

        bool Modal { get { return sim.gameOver || sim.pendingBattle != null || pauseMenu || helpOpen; } }

        // ==================================================================
        //  Mise à jour
        // ==================================================================
        public void Update()
        {
            float dt = Time.unscaledDeltaTime;
            ValidateSelection();

            if (sim.gameOver)
            {
                view.Update(selSystem, selFleet, null, null);
                return;
            }

            if (sim.pendingBattle != null)
            {
                if (!battleAlerted)
                {
                    battleAlerted = true;
                    Sfx.Alert();
                    GameInput.Rumble(0.4f, 0.2f, 0.3f);
                    UI.ResetFocus();
                    menuMode = false;
                    pauseMenu = false;
                    helpOpen = false;
                }
                camTarget = sim.pendingBattle.system.pos;
                if (GameInput.KeyDown(KeyId.Enter)) { gm.StartBattle(sim.pendingBattle); return; }
                UpdateCamera(dt);
                view.Update(selSystem, selFleet, null, null);
                return;
            }
            battleAlerted = false;

            if (helpOpen)
            {
                if (GameInput.Back || GameInput.PadDown(Pad.View) || GameInput.KeyDown(KeyId.F1)) helpOpen = false;
                view.Update(selSystem, selFleet, null, null);
                return;
            }
            if (pauseMenu)
            {
                if (GameInput.Back || GameInput.PadDown(Pad.Start)) pauseMenu = false;
                view.Update(selSystem, selFleet, null, null);
                return;
            }

            // Raccourcis généraux
            if (GameInput.KeyDown(KeyId.Escape))
            {
                if (selFleet != null || selSystem != null) { selFleet = null; selSystem = null; }
                else if (empireOpen) empireOpen = false;
                else { pauseMenu = true; UI.ResetFocus(); }
            }
            if (GameInput.PadDown(Pad.Start)) { pauseMenu = true; menuMode = false; UI.ResetFocus(); }
            if (GameInput.KeyDown(KeyId.F1) || GameInput.KeyDown(KeyId.H) || GameInput.PadDown(Pad.View)) { helpOpen = true; UI.ResetFocus(); }

            sim.Tick(dt);

            HandleTime();
            if (!GameInput.UsingGamepad) menuMode = false;
            if (menuMode) HandleMenuMode();
            else HandleMap(dt);

            UpdateCamera(dt);
            view.Update(selSystem, selFleet, hoverSystem, hoverFleet);
        }

        void ValidateSelection()
        {
            if (selFleet != null && (!g.fleets.Contains(selFleet) || !sim.IsFleetVisible(selFleet))) selFleet = null;
            if (hoverFleet != null && !g.fleets.Contains(hoverFleet)) hoverFleet = null;
        }

        void HandleTime()
        {
            if (GameInput.KeyDown(KeyId.Space) || (!menuMode && GameInput.PadDown(Pad.DDown))) sim.paused = !sim.paused;
            if (GameInput.KeyDown(KeyId.Alpha1)) { sim.speed = 1; sim.paused = false; }
            if (GameInput.KeyDown(KeyId.Alpha2)) { sim.speed = 2; sim.paused = false; }
            if (GameInput.KeyDown(KeyId.Alpha3)) { sim.speed = 3; sim.paused = false; }
            if (GameInput.KeyDown(KeyId.Plus) || (!menuMode && GameInput.PadDown(Pad.DRight))) sim.speed = Mathf.Min(3, sim.speed + 1);
            if (GameInput.KeyDown(KeyId.Minus) || (!menuMode && GameInput.PadDown(Pad.DLeft))) sim.speed = Mathf.Max(1, sim.speed - 1);
            if (GameInput.KeyDown(KeyId.E) || (!menuMode && GameInput.PadDown(Pad.DUp))) empireOpen = !empireOpen;
        }

        void HandleMenuMode()
        {
            if (GameInput.PadDown(Pad.B) || GameInput.PadDown(Pad.Y)) menuMode = false;
            if (selFleet == null && selSystem == null && !empireOpen) menuMode = false;
            hoverFleet = null;
            hoverSystem = null;
        }

        void HandleMap(float dt)
        {
            // --- Caméra
            var pan = GameInput.Move;
            if (pan.sqrMagnitude > 0.01f)
            {
                view.camPos += pan * view.camDist * 1.1f * dt;
                camTarget = null;
            }
            bool overUI = !GameInput.UsingGamepad && UI.IsPointerOverUI(GameInput.MousePosition);
            if (GameInput.Scroll != 0 && !overUI) view.camDist *= GameInput.Scroll > 0 ? 0.87f : 1.15f;
            float zoom = -GameInput.RightStick.y + (GameInput.LeftTrigger - GameInput.RightTrigger);
            if (Mathf.Abs(zoom) > 0.05f) view.camDist *= 1f + zoom * 1.6f * dt;
            if (GameInput.MouseHeld(2))
            {
                float worldPerPixel = 2f * view.camDist * Mathf.Tan(gm.Cam.fieldOfView * 0.5f * Mathf.Deg2Rad) / Screen.height;
                view.camPos -= GameInput.MouseDelta * worldPerPixel;
                camTarget = null;
            }

            // --- Survol
            Vector2 pointer = GameInput.UsingGamepad ? new Vector2(Screen.width * 0.5f, Screen.height * 0.5f) : GameInput.MousePosition;
            if (overUI)
            {
                hoverFleet = null;
                hoverSystem = null;
            }
            else
            {
                hoverFleet = view.PickFleet(pointer, (GameInput.UsingGamepad ? 34f : 22f) * UI.S);
                hoverSystem = view.PickSystem(pointer, (GameInput.UsingGamepad ? 70f : 32f) * UI.S);
            }

            // --- Sélection
            bool select = (!GameInput.UsingGamepad && GameInput.MouseDown(0) && !overUI) || GameInput.PadDown(Pad.A);
            if (select)
            {
                if (hoverFleet != null && hoverFleet != selFleet) { selFleet = hoverFleet; selSystem = null; Sfx.Click(); }
                else if (hoverSystem != null) { selSystem = hoverSystem; selFleet = null; Sfx.Click(); }
                else if (!GameInput.UsingGamepad) { selSystem = null; selFleet = null; }
            }

            // --- Ordre de déplacement
            bool order = (!GameInput.UsingGamepad && GameInput.MouseDown(1) && !overUI) || GameInput.PadDown(Pad.X);
            if (order && selFleet != null && selFleet.owner == g.player)
            {
                var target = hoverSystem;
                if (target == null && hoverFleet != null && !hoverFleet.InTransit) target = hoverFleet.system;
                if (target != null)
                {
                    sim.OrderMove(selFleet, target);
                    Sfx.Confirm();
                }
            }

            if (GameInput.PadDown(Pad.B)) { selFleet = null; selSystem = null; }
            if (GameInput.PadDown(Pad.Y) && (selFleet != null || selSystem != null || empireOpen))
            {
                menuMode = true;
                UI.ResetFocus();
            }

            if (GameInput.KeyDown(KeyId.Tab) || GameInput.PadDown(Pad.RB)) CycleFleet(1);
            if (GameInput.PadDown(Pad.LB)) CycleFleet(-1);
        }

        void CycleFleet(int dir)
        {
            var mine = new List<Fleet>();
            foreach (var f in g.fleets) if (f.owner == g.player) mine.Add(f);
            if (mine.Count == 0) return;
            int i = selFleet != null ? mine.IndexOf(selFleet) : -1;
            i = ((i + dir) % mine.Count + mine.Count) % mine.Count;
            selFleet = mine[i];
            selSystem = null;
            camTarget = selFleet.Position;
            Sfx.Click();
        }

        void UpdateCamera(float dt)
        {
            if (camTarget.HasValue)
            {
                view.camPos = Vector2.Lerp(view.camPos, camTarget.Value, 1f - Mathf.Exp(-6f * dt));
                if ((view.camPos - camTarget.Value).sqrMagnitude < 0.01f) camTarget = null;
            }
            view.ClampCamera();
        }

        // ==================================================================
        //  Interface
        // ==================================================================
        public void OnGUI()
        {
            bool modal = Modal;
            DrawWorldLabels();

            GUI.enabled = !modal;
            DrawTopBar();
            UI.NavEnabled = menuMode && !modal;
            if (selFleet != null) DrawFleetPanel(selFleet);
            else if (selSystem != null) DrawSystemPanel(selSystem);
            if (empireOpen) DrawEmpirePanel();
            UI.NavEnabled = false;
            DrawNotices();
            DrawHints();
            GUI.enabled = true;

            if (GameInput.UsingGamepad && !menuMode && !modal) DrawReticle();

            if (sim.gameOver) DrawGameOver();
            else if (sim.pendingBattle != null) DrawBattlePrompt(sim.pendingBattle);
            else if (helpOpen) { if (Help.Draw()) helpOpen = false; }
            else if (pauseMenu) DrawPauseMenu();
        }

        static Color Light(Color c) { return Color.Lerp(c, Color.white, 0.35f); }

        void DrawWorldLabels()
        {
            var player = g.player;
            bool far = view.camDist > 160f;
            foreach (var s in g.systems)
            {
                var gp = view.WorldToGui(s.pos + new Vector2(0, -2.7f));
                if (gp.x < -100 || gp.y < -50 || gp.x > Screen.width + 100 || gp.y > Screen.height + 50) continue;
                bool explored = player.explored.Contains(s);
                if (far && s.owner == null) continue;
                Color c = explored ? (s.owner != null ? Light(s.owner.color) : new Color(0.85f, 0.9f, 1f)) : new Color(0.5f, 0.55f, 0.65f, 0.8f);
                UI.ScreenLabel(gp, s.name, c);

                string sub = null;
                Color subCol = Palette.Warning;
                var claim = sim.ClaimOn(s);
                if (claim != null && (claim.empire == player || sim.sensors.Contains(s)))
                {
                    sub = (claim.colony ? "Colonisation " : "Avant-poste ") + Mathf.RoundToInt(100f * (1f - claim.daysLeft / claim.totalDays)) + " %";
                    subCol = Light(claim.empire.color);
                }
                if (s.occupier != null && explored)
                {
                    sub = "Siège " + Mathf.RoundToInt(100f * s.occupation / sim.OccupationRequired(s)) + " %";
                    subCol = Palette.Bad;
                }
                if (sub != null) UI.ScreenLabel(gp + new Vector2(0, 17f * UI.S), sub, subCol);
            }

            float icon = view.IconScale;
            foreach (var kv in view.fleetDraw)
            {
                var gp = view.WorldToGui(kv.Value + new Vector2(icon * 1.1f, -icon * 0.1f));
                UI.ScreenLabel(gp, kv.Key.ships.Count.ToString(), Light(kv.Key.owner.color));
            }
        }

        void DrawTopBar()
        {
            var p = g.player;
            UI.PanelBox(new Rect(0, 0, UI.W, TopH));
            bool wide = UI.W >= 1600f;
            if (wide) UI.Label(new Rect(16, 14, 240, 30), Palette.Tint(p.name, Light(p.color)), UI.Bold);

            float x = wide ? 260 : 16;
            x = Resource(x, "Énergie", p.energy, p.lastIncome.energy, Palette.Energy);
            x = Resource(x, "Minerais", p.minerals, p.lastIncome.minerals, Palette.Minerals);
            x = Resource(x, "Alliages", p.alloys, p.lastIncome.alloys, Palette.Alloys);
            Resource(x, "Recherche", p.researchStock, p.lastIncome.research, Palette.ResearchRes);

            // Date et vitesse
            float bx = UI.W - 16 - (4 * 46 + 2 * 112 + 5 * 6);
            string speed = sim.paused
                ? Palette.Tint(Mathf.Repeat(Time.unscaledTime, 1f) < 0.6f ? "PAUSE" : "", Palette.Warning)
                : "Vitesse " + new string('>', sim.speed);
            UI.Label(new Rect(bx - 260, 8, 250, 22), "<b>" + g.DateString + "</b>", UI.Text);
            UI.Label(new Rect(bx - 260, 30, 250, 22), speed, UI.Small);

            float by = 10, bh = 36;
            if (UI.Button(new Rect(bx, by, 46, bh), sim.paused ? ">" : "II", true, true)) sim.paused = !sim.paused;
            bx += 52;
            for (int i = 1; i <= 3; i++)
            {
                string lbl = sim.speed == i && !sim.paused ? "<b>" + i + "</b>" : i.ToString();
                if (UI.Button(new Rect(bx, by, 46, bh), lbl, true, true)) { sim.speed = i; sim.paused = false; }
                bx += 52;
            }
            if (UI.Button(new Rect(bx, by, 112, bh), empireOpen ? "<b>Empire</b>" : "Empire", true, true)) empireOpen = !empireOpen;
            bx += 118;
            if (UI.Button(new Rect(bx, by, 112, bh), "Menu", true, true)) { pauseMenu = true; UI.ResetFocus(); }
        }

        float Resource(float x, string name, float value, float income, Color c)
        {
            UI.Dot(new Rect(x, 21, 14, 14), c);
            string inc = income >= 0 ? Palette.Tint("+" + income.ToString("0.#"), Palette.Good) : Palette.Tint(income.ToString("0.#"), Palette.Bad);
            UI.Label(new Rect(x + 20, 8, 200, 22), "<b>" + Mathf.FloorToInt(value) + "</b>  " + inc, UI.Text);
            UI.Label(new Rect(x + 20, 30, 200, 22), name + " / mois", UI.Small);
            return x + 175;
        }

        Rect LeftArea { get { return new Rect(12, TopH + 10, PanelW, UI.H - TopH - HintH - 20); } }
        Rect RightArea { get { return new Rect(UI.W - PanelW - 12, TopH + 10, PanelW, UI.H - TopH - HintH - 20); } }

        // ------------------------------------------------------------------
        void DrawSystemPanel(StarSystem s)
        {
            var player = g.player;
            var area = LeftArea;
            UI.PanelBox(area);
            var L = new VLayout(area.x + 16, area.y + 12, area.width - 32, 4);
            bool explored = player.explored.Contains(s);

            UI.Label(L.Next(36), Palette.Tint(s.name, s.owner != null && explored ? Light(s.owner.color) : Color.white), UI.Title);
            UI.Label(L.Next(22), Names.Stars[(int)s.star], UI.Small);
            if (!explored)
            {
                UI.Label(L.Next(70), "Système inexploré. Envoyez une flotte ou étendez votre territoire pour le découvrir.");
                DrawFleetsHere(L, s, area);
                return;
            }
            UI.Label(L.Next(26), "Propriétaire : " + (s.owner != null ? s.owner.Rich : "<i>aucun</i>"));
            L.Space(4);
            UI.Label(L.Next(24), "<b>Planètes</b>");
            if (s.planets.Count == 0) UI.Label(L.Next(22), "Aucune planète, seulement des astéroïdes.", UI.Small);
            foreach (var p in s.planets)
            {
                string line = "• " + p.name + " — " + Palette.Tint(Names.Planet(p.type), Palette.Planet(p.type)) + " (taille " + p.size + ")";
                if (p.Habitable) line += Palette.Tint("  habitable", Palette.Good);
                UI.Label(L.Next(22), line, UI.Small);
            }
            L.Space(6);

            if (s.colony != null)
            {
                var c = s.colony;
                UI.Label(L.Next(24), (c.capital ? "<b>Capitale</b>" : "<b>Colonie</b>") + " — population " + Mathf.FloorToInt(c.pop) + " / " + c.maxPop);
                UI.Bar(L.Next(6), c.pop >= c.maxPop ? 1f : c.growth, Palette.Good);
                var y = GalaxySim.SystemYield(s);
                float mul = sim.EmpireMul(s.owner);
                UI.Label(L.Next(22), "Production : " + YieldText(y, mul), UI.Small);
                if (s.owner == player)
                {
                    UI.Label(L.Next(22), "Spécialisation :", UI.Small);
                    var row = L.Next(32);
                    float bw = (row.width - 3 * 6) / 4f;
                    for (int i = 0; i < 4; i++)
                    {
                        var r = new Rect(row.x + i * (bw + 6), row.y, bw, row.height);
                        string lbl = (int)c.spec == i ? Palette.Tint("<b>" + Names.Specs[i] + "</b>", Palette.Warning) : Names.Specs[i];
                        if (UI.Button(r, lbl, true, true)) sim.SetSpecialization(s, (Specialization)i);
                    }
                }
                else UI.Label(L.Next(22), "Spécialisation : " + Names.Specs[(int)c.spec], UI.Small);
            }
            else if (s.owner != null)
            {
                UI.Label(L.Next(24), "<b>Avant-poste</b> — +1 énergie, +2 minerais / mois", UI.Small);
            }

            if (s.occupier != null)
            {
                UI.Label(L.Next(24), Palette.Tint("Siège par ", Palette.Bad) + s.occupier.Rich + " : " + Mathf.RoundToInt(100f * s.occupation / sim.OccupationRequired(s)) + " %");
                UI.Bar(L.Next(6), s.occupation / sim.OccupationRequired(s), Palette.Bad);
            }

            if (s.owner == player && s.colony != null) DrawShipyard(L, s);
            if (s.owner == null) DrawClaim(L, s);
            DrawFleetsHere(L, s, area);
        }

        static string YieldText(Yield y, float mul)
        {
            var parts = new List<string>();
            if (y.energy > 0) parts.Add(Palette.Tint("+" + (y.energy * mul).ToString("0.#") + " én.", Palette.Energy));
            if (y.minerals > 0) parts.Add(Palette.Tint("+" + (y.minerals * mul).ToString("0.#") + " min.", Palette.Minerals));
            if (y.alloys > 0) parts.Add(Palette.Tint("+" + (y.alloys * mul).ToString("0.#") + " all.", Palette.Alloys));
            if (y.research > 0) parts.Add(Palette.Tint("+" + (y.research * mul).ToString("0.#") + " rech.", Palette.ResearchRes));
            return string.Join("  ", parts.ToArray());
        }

        void DrawShipyard(VLayout L, StarSystem s)
        {
            var player = g.player;
            L.Space(8);
            UI.Label(L.Next(24), "<b>Chantier spatial</b>");
            for (int c = 2; c >= 0; c--)
            {
                var spec = ShipCatalog.Get((ShipClass)c);
                string reason = sim.CanBuild(player, s, (ShipClass)c);
                string label = Names.Ships[c] + "  —  " + spec.alloyCost + " alliages, " + spec.buildDays + " j";
                if (UI.Button(L.Next(32), label, reason == null, true))
                {
                    if (sim.QueueShip(player, s, (ShipClass)c)) Sfx.Confirm();
                }
            }
            var q = s.colony.queue;
            if (q.Count > 0)
            {
                for (int i = 0; i < q.Count; i++)
                {
                    var o = q[i];
                    int pct = Mathf.RoundToInt(100f * (1f - o.daysLeft / o.totalDays));
                    UI.Label(L.Next(20), "• " + Names.Ships[(int)o.cls] + (i == 0 ? " — " + pct + " %" : " — en attente"), UI.Small);
                    if (i == 0) UI.Bar(L.Next(5), pct / 100f, Palette.Info);
                }
                if (sim.HasHostileFleets(s, player)) UI.Label(L.Next(20), Palette.Tint("Production suspendue : ennemis en orbite.", Palette.Bad), UI.Small);
                if (UI.Button(L.Next(28), "Annuler la dernière commande", true, true)) sim.CancelLastBuild(s);
            }
        }

        void DrawClaim(VLayout L, StarSystem s)
        {
            var player = g.player;
            L.Space(8);
            var claim = sim.ClaimOn(s);
            if (claim != null)
            {
                int pct = Mathf.RoundToInt(100f * (1f - claim.daysLeft / claim.totalDays));
                string who = claim.empire == player ? "Votre " : claim.empire.Rich + " : ";
                UI.Label(L.Next(24), who + (claim.colony ? "colonisation" : "construction d'avant-poste") + " en cours (" + pct + " %)");
                UI.Bar(L.Next(6), pct / 100f, claim.empire.color);
                return;
            }
            bool col = s.Habitable;
            int m, en, d;
            GalaxySim.ClaimCost(col, out m, out en, out d);
            string label = (col ? "Fonder une colonie" : "Construire un avant-poste") + "  (" + m + " min." + (en > 0 ? ", " + en + " én." : "") + ", " + d + " j)";
            string reason = sim.CanClaim(player, s);
            if (UI.Button(L.Next(34), label, reason == null))
            {
                if (sim.StartClaim(player, s)) Sfx.Confirm();
            }
            if (reason != null) UI.Label(L.Next(22), Palette.Tint(reason, Palette.Warning), UI.Small);
        }

        void DrawFleetsHere(VLayout L, StarSystem s, Rect area)
        {
            var list = new List<Fleet>();
            foreach (var f in sim.FleetsAt(s)) if (sim.IsFleetVisible(f)) list.Add(f);
            if (list.Count == 0) return;
            L.Space(8);
            UI.Label(L.Next(24), "<b>Flottes en orbite</b>");
            foreach (var f in list)
            {
                if (L.y > area.yMax - 40) break;
                if (f.owner == g.player)
                {
                    if (UI.Button(L.Next(30), f.name + " — " + f.ships.Count + " vaisseau(x)", true, true))
                    {
                        selFleet = f;
                        selSystem = null;
                        UI.ResetFocus();
                    }
                }
                else UI.Label(L.Next(40), f.owner.Rich + " : " + f.Composition() + " (puissance " + Mathf.RoundToInt(sim.FleetPower(f)) + ")", UI.Small);
            }
        }

        // ------------------------------------------------------------------
        void DrawFleetPanel(Fleet f)
        {
            var player = g.player;
            var area = LeftArea;
            UI.PanelBox(area);
            var L = new VLayout(area.x + 16, area.y + 12, area.width - 32, 4);

            UI.Label(L.Next(36), Palette.Tint(f.name, Light(f.owner.color)), UI.Title);
            UI.Label(L.Next(24), "Empire : " + f.owner.Rich);
            UI.Label(L.Next(40), "Composition : " + f.Composition());
            UI.Label(L.Next(24), "Puissance : <b>" + Mathf.RoundToInt(sim.FleetPower(f)) + "</b>    Vitesse : " + sim.FleetSpeed(f).ToString("0.0"));
            UI.Label(L.Next(22), "Coque : " + Mathf.RoundToInt(f.HullRatio * 100f) + " %", UI.Small);
            UI.Bar(L.Next(6), f.HullRatio, Color.Lerp(Palette.Bad, Palette.Good, f.HullRatio));

            if (f.InTransit)
            {
                var dest = f.Destination;
                UI.Label(L.Next(44), "En route vers <b>" + dest.name + "</b> (≈ " + Mathf.CeilToInt(sim.PathDays(f)) + " jours)");
            }
            else
            {
                string at = "En orbite de <b>" + f.system.name + "</b>";
                if (f.system.occupier == f.owner) at += Palette.Tint(" — siège en cours", Palette.Warning);
                UI.Label(L.Next(44), at);
            }

            if (f.owner == player)
            {
                if (f.InTransit && f.path.Count > 0 && UI.Button(L.Next(32), "S'arrêter au prochain système", true, true)) sim.StopFleet(f);

                if (!f.InTransit)
                {
                    var others = new List<Fleet>();
                    foreach (var o in sim.FleetsAt(f.system)) if (o != f && o.owner == player) others.Add(o);
                    if (others.Count > 0 && UI.Button(L.Next(32), "Fusionner les flottes en orbite (" + others.Count + ")", true, true))
                    {
                        foreach (var o in others) sim.Merge(f, o);
                        Sfx.Confirm();
                    }
                }
                if (UI.Button(L.Next(32), "Voir le système " + f.system.name, true, true))
                {
                    selSystem = f.system;
                    selFleet = null;
                    UI.ResetFocus();
                    return;
                }
                string hint = GameInput.UsingGamepad
                    ? "Visez un système et appuyez sur <b>[X]</b> pour vous y rendre."
                    : "<b>Clic droit</b> sur un système pour vous y rendre.";
                UI.Label(L.Next(44), Palette.Tint(hint, Palette.Info), UI.Small);
            }

            L.Space(6);
            UI.Label(L.Next(24), "<b>Vaisseaux</b>");
            var flagship = CombatSession.Flagship(f.ships);
            int shown = 0;
            foreach (var sh in f.ships)
            {
                if (L.y > area.yMax - 50)
                {
                    UI.Label(L.Next(22), "… et " + (f.ships.Count - shown) + " autre(s)", UI.Small);
                    break;
                }
                var row = L.Next(22);
                string name = (sh == flagship ? Palette.Tint("★ ", Palette.Warning) : "") + sh.name + "  " + Palette.Tint(Names.Ships[(int)sh.cls], Palette.Neutral);
                UI.Label(new Rect(row.x, row.y, row.width - 110, row.height), name, UI.Small);
                UI.Bar(new Rect(row.xMax - 100, row.y + 8, 100, 6), sh.HullRatio, Color.Lerp(Palette.Bad, Palette.Good, sh.HullRatio));
                shown++;
            }
        }

        // ------------------------------------------------------------------
        void DrawEmpirePanel()
        {
            var p = g.player;
            var area = RightArea;
            UI.PanelBox(area);
            var L = new VLayout(area.x + 16, area.y + 12, area.width - 32, 4);

            UI.Label(L.Next(36), Palette.Tint(p.name, Light(p.color)), UI.Title);
            int systems = 0, colonies = 0;
            float pop = 0, power = 0;
            foreach (var s in g.systems)
            {
                if (s.owner != p) continue;
                systems++;
                if (s.colony != null) { colonies++; pop += s.colony.pop; }
            }
            int ships = 0;
            foreach (var f in g.fleets) if (f.owner == p) { power += sim.FleetPower(f); ships += f.ships.Count; }
            UI.Label(L.Next(24), "Systèmes : <b>" + systems + "</b> (dont " + colonies + " colonies)");
            UI.Label(L.Next(24), "Population : <b>" + Mathf.FloorToInt(pop) + "</b>    Vaisseaux : <b>" + ships + "</b>");
            UI.Label(L.Next(24), "Puissance militaire : <b>" + Mathf.RoundToInt(power) + "</b>");
            if (p.energyDeficit) UI.Label(L.Next(22), Palette.Tint("Déficit d'énergie : réparations interrompues !", Palette.Bad), UI.Small);

            L.Space(10);
            UI.Label(L.Next(26), "<b>Recherche</b>  —  " + Mathf.FloorToInt(p.researchStock) + " points");
            for (int t = 0; t < 4; t++)
            {
                int lvl = p.tech[t];
                bool max = lvl >= GalaxySim.MaxTech;
                string lbl = Names.Techs[t] + " — niveau " + lvl + (max ? " (max)" : "  →  " + Mathf.RoundToInt(sim.ResearchCost(p, t)) + " pts");
                if (p.currentResearch == t) lbl = Palette.Tint("<b>► " + lbl + "</b>", Palette.Warning);
                if (UI.Button(L.Next(32), lbl, !max, true)) sim.SetResearch(p, t);
                UI.Label(L.Next(20), Names.TechEffects[t], UI.Small);
            }
            if (p.currentResearch >= 0)
            {
                float cost = sim.ResearchCost(p, p.currentResearch);
                UI.Label(L.Next(22), "Progression : " + Mathf.FloorToInt(p.researchStock) + " / " + Mathf.RoundToInt(cost), UI.Small);
                UI.Bar(L.Next(6), p.researchStock / cost, Palette.ResearchRes);
            }
            else UI.Label(L.Next(22), Palette.Tint("Choisissez un domaine de recherche.", Palette.Warning), UI.Small);

            L.Space(10);
            UI.Label(L.Next(26), "<b>Empires rivaux</b>");
            foreach (var e in g.empires)
            {
                if (e.isPlayer) continue;
                bool contact = false;
                int count = 0;
                foreach (var s in g.systems)
                {
                    if (s.owner != e) continue;
                    count++;
                    if (p.explored.Contains(s)) contact = true;
                }
                string line;
                if (!e.alive) line = Palette.Tint(e.name + " — anéanti", Palette.Neutral);
                else if (contact) line = e.Rich + " — " + count + " système(s)";
                else line = Palette.Tint("Empire inconnu", Palette.Neutral);
                UI.Label(L.Next(22), line, UI.Small);
            }
        }

        // ------------------------------------------------------------------
        void DrawNotices()
        {
            float w = 520;
            float x = empireOpen ? UI.W - PanelW - 24 - w : UI.W - w - 12;
            float y = TopH + 10;
            float now = Time.unscaledTime;
            int shown = 0;
            for (int i = sim.notices.Count - 1; i >= 0 && shown < 6; i--)
            {
                var n = sim.notices[i];
                float age = now - n.time;
                if (age > 12f) break;
                float a = Mathf.Clamp01((12f - age) / 2f);
                var c = n.color;
                c.a = a;
                UI.RoundFill(new Rect(x, y, w, 30), new Color(0.02f, 0.05f, 0.1f, 0.75f * a));
                UI.Dot(new Rect(x + 6, y + 9, 12, 12), c);
                var old = UI.Small.normal.textColor;
                UI.Small.normal.textColor = new Color(0.9f, 0.95f, 1f, a);
                UI.Label(new Rect(x + 26, y + 5, w - 30, 24), n.text, UI.Small);
                UI.Small.normal.textColor = old;
                y += 34;
                shown++;
            }
        }

        void DrawHints()
        {
            UI.PanelBox(new Rect(0, UI.H - HintH, UI.W, HintH));
            string h;
            if (GameInput.UsingGamepad)
            {
                if (menuMode) h = "[Croix / stick] Naviguer   [A] Valider   [B] / [Y] Retour à la carte";
                else h = "[Stick G] Caméra   [Stick D / LT RT] Zoom   [A] Sélectionner   [X] Déplacer la flotte   [Y] Actions   [LB/RB] Flottes   [Bas] Pause   [Gauche/Droite] Vitesse   [Haut] Empire   [Start] Menu   [View] Aide";
            }
            else h = "[Clic G] Sélectionner   [Clic D] Déplacer la flotte   [ZQSD / flèches / clic molette] Caméra   [Molette] Zoom   [Espace] Pause   [1-3] Vitesse   [Tab] Flottes   [E] Empire   [Échap] Menu   [F1] Aide";
            UI.Label(new Rect(0, UI.H - HintH + 10, UI.W, 26), h, UI.CenterSmall);
        }

        void DrawReticle()
        {
            var c = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            float s = UI.S;
            var col = new Color(0.8f, 0.95f, 1f, 0.8f);
            UI.FillScreen(new Rect(c.x - 14 * s, c.y - 1 * s, 9 * s, 2 * s), col);
            UI.FillScreen(new Rect(c.x + 5 * s, c.y - 1 * s, 9 * s, 2 * s), col);
            UI.FillScreen(new Rect(c.x - 1 * s, c.y - 14 * s, 2 * s, 9 * s), col);
            UI.FillScreen(new Rect(c.x - 1 * s, c.y + 5 * s, 2 * s, 9 * s), col);
        }

        // ------------------------------------------------------------------
        //  Fenêtres modales
        // ------------------------------------------------------------------
        static void Dim()
        {
            UI.FillScreen(new Rect(0, 0, Screen.width, Screen.height), new Color(0, 0, 0, 0.55f));
            UI.Block(new Rect(0, 0, UI.W, UI.H));
        }

        static string CompositionOf(List<Fleet> fleets)
        {
            var tmp = new Fleet();
            foreach (var f in fleets) foreach (var s in f.ships) if (!s.destroyed) tmp.ships.Add(s);
            return tmp.Composition();
        }

        void DrawBattlePrompt(Battle b)
        {
            Dim();
            UI.NavEnabled = true;
            float w = 760, h = 470;
            var area = new Rect((UI.W - w) / 2f, (UI.H - h) / 2f, w, h);
            UI.PanelBox(area);
            var L = new VLayout(area.x + 30, area.y + 24, w - 60, 8);
            UI.Label(L.Next(40), Palette.Tint("BATAILLE À " + b.system.name.ToUpper() + " !", Palette.Bad), UI.Title);

            float mine = 0, theirs = 0;
            foreach (var f in b.playerFleets) mine += sim.FleetPower(f);
            foreach (var f in b.enemyFleets) theirs += sim.FleetPower(f);
            UI.Label(L.Next(50), "Vos forces : " + CompositionOf(b.playerFleets) + "  (puissance " + Mathf.RoundToInt(mine) + ")");
            UI.Label(L.Next(50), "Ennemi : " + b.enemy.Rich + " — " + CompositionOf(b.enemyFleets) + "  (puissance " + Mathf.RoundToInt(theirs) + ")");
            float ratio = mine * g.settings.Diff.playerDamage / Mathf.Max(1f, theirs * g.settings.Diff.enemyDamage);
            string est = ratio > 1.4f ? Palette.Tint("favorable", Palette.Good) : ratio > 0.8f ? Palette.Tint("incertain", Palette.Warning) : Palette.Tint("défavorable", Palette.Bad);
            UI.Label(L.Next(30), "Rapport de force : " + est);
            L.Space(10);
            if (UI.Button(L.Next(52), "<b>Piloter le vaisseau amiral</b>")) { gm.StartBattle(b); return; }
            if (UI.Option(L.Next(44), "Vue du combat", g.settings.combat3D ? "3D (poursuite)" : "Vue de dessus") != 0)
            {
                g.settings.combat3D = !g.settings.combat3D;
                MainMenuScreen.SaveSettings(g.settings);
            }
            if (UI.Button(L.Next(44), "Résolution automatique")) gm.AutoResolveBattle(b);
        }

        void DrawPauseMenu()
        {
            Dim();
            UI.NavEnabled = true;
            float w = 480, h = 400;
            var area = new Rect((UI.W - w) / 2f, (UI.H - h) / 2f, w, h);
            UI.PanelBox(area);
            var L = new VLayout(area.x + 30, area.y + 24, w - 60, 10);
            UI.Label(L.Next(40), "Pause", UI.Title);
            if (UI.Button(L.Next(48), "Reprendre")) pauseMenu = false;
            if (UI.Button(L.Next(48), "Aide et contrôles")) { helpOpen = true; pauseMenu = false; UI.ResetFocus(); }
            if (UI.Button(L.Next(48), "Abandonner (menu principal)")) { gm.ReturnToMenu(); return; }
            if (UI.Button(L.Next(48), "Combats : " + (g.settings.combat3D ? "3D (poursuite)" : "vue de dessus")))
            {
                g.settings.combat3D = !g.settings.combat3D;
                MainMenuScreen.SaveSettings(g.settings);
            }
            if (UI.Button(L.Next(48), "Quitter le jeu")) gm.Quit();
        }

        void DrawGameOver()
        {
            Dim();
            UI.NavEnabled = true;
            float w = 700, h = 320;
            var area = new Rect((UI.W - w) / 2f, (UI.H - h) / 2f, w, h);
            UI.PanelBox(area);
            UI.Label(new Rect(area.x, area.y + 30, w, 90), sim.victory ? Palette.Tint("VICTOIRE", Palette.Good) : Palette.Tint("DÉFAITE", Palette.Bad), UI.Huge);
            string txt = sim.victory
                ? "Tous les empires rivaux se sont effondrés. La galaxie vous appartient !"
                : "Votre dernier système est tombé. Votre empire n'est plus qu'un souvenir…";
            UI.Label(new Rect(area.x + 30, area.y + 140, w - 60, 60), txt, UI.Center);
            if (UI.Button(new Rect(area.x + (w - 360) / 2f, area.y + 220, 360, 52), "Retour au menu principal")) gm.ReturnToMenu();
        }
    }
}
