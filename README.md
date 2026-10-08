# Stellaris Mini

Un petit jeu de stratégie spatiale pour **Unity 6**, inspiré de Stellaris en très simplifié, avec une particularité : **quand une bataille éclate, vous pilotez vous-même votre vaisseau amiral**, pendant que l'IA dirige le reste de votre flotte.

- Carte galactique en **temps réel avec pause** (vitesses 1, 2 et 3).
- Exploration, colonisation, économie, recherche, construction de flottes.
- Empires rivaux contrôlés par l'IA, qui s'étendent et vous attaquent.
- Combats pilotés **en 3D** (caméra de poursuite, vol libre avec roulis) ou **en vue de dessus** (style « twin-stick »), au choix : si votre vaisseau est détruit, **le commandement passe automatiquement à un autre vaisseau de la flotte**.
- **5 niveaux de difficulté**, de « Très facile » à « Très difficile ».
- Jouable au **clavier + souris** et à la **manette Xbox** (ou toute manette compatible XInput), y compris dans les menus.

Tout est généré par code : graphismes « néon vectoriel », sons synthétisés, interface. Aucun modèle 3D, aucune texture et aucun fichier audio ne sont nécessaires.

---

## Lancer le jeu

1. Installez **Unity Hub** et un éditeur **Unity 6** (n'importe quelle version 6000.x).
2. Dans Unity Hub : **Add → Add project from disk**, puis choisissez ce dossier.
   Si Unity Hub propose une autre version de l'éditeur que celle indiquée, choisissez la vôtre et acceptez la mise à niveau.
3. À la première ouverture, Unity installe le paquet **Input System**. S'il demande d'activer les nouveaux « backends » d'entrée, répondez **Oui** : l'éditeur redémarre.
4. Le script `ProjectSetup` crée automatiquement la scène `Assets/StellarisMini/Scenes/Main.unity`, l'ajoute au build et l'ouvre.
5. Appuyez sur **Play**.

> Le jeu démarre tout seul, quelle que soit la scène ouverte : un `GameManager` est créé au lancement grâce à `[RuntimeInitializeOnLoadMethod]`.
> En cas de souci, le menu **Stellaris Mini → Configurer le projet** relance la configuration.

### Faire une version PC (.exe)

**File → Build Profiles** (ou **Build Settings**), choisissez **Windows**, vérifiez que la scène `Main` est cochée, puis **Build**.

### Manette

- Branchez la manette en USB ou en Bluetooth avant ou pendant le jeu : elle est détectée automatiquement.
- L'interface bascule d'elle-même entre les indications clavier et manette, selon le dernier périphérique utilisé.
- La manette vibre quand vous êtes touché ou qu'un vaisseau explose.
- Si la manette ne répond pas, vérifiez **Edit → Project Settings → Player → Active Input Handling** : choisissez `Input System Package (New)` ou `Both`.

---

## Contrôles

### Carte galactique

| Action | Clavier / souris | Manette Xbox |
|---|---|---|
| Déplacer la caméra | ZQSD / WASD / flèches, ou glisser avec le clic molette | Stick gauche (viseur au centre de l'écran) |
| Zoom | Molette | Stick droit, ou LT / RT |
| Sélectionner un système ou une flotte | Clic gauche | A |
| Déplacer la flotte sélectionnée | Clic droit sur un système | X (en visant un système) |
| Annuler la sélection | Échap | B |
| Utiliser les boutons des panneaux | Souris | Y, puis croix ou stick et A (B pour revenir) |
| Pause | Espace | Croix bas |
| Vitesse | 1, 2, 3 (ou + / −) | Croix gauche / droite |
| Panneau Empire (recherche) | E | Croix haut |
| Flotte suivante / précédente | Tab | RB / LB |
| Menu | Échap | Start |
| Aide | F1 | View |

### Combat piloté en 3D

La souris sert de manche : plus le curseur s'éloigne du centre de l'écran, plus le vaisseau tourne vite dans cette direction.

| Action | Clavier AZERTY / souris | Manette Xbox |
|---|---|---|
| Diriger (tangage, lacet) | Souris | Stick droit |
| Accélérer / ralentir | Z / S | Stick gauche haut / bas |
| Glisser sur le côté | Q / D | Stick gauche gauche / droite |
| Tonneau (roulis) | A / E | LB / RB |
| Canons (visée assistée sur la cible encadrée) | Clic gauche | RT |
| Missiles à tête chercheuse | Clic droit | LT |
| Postcombustion | Maj | A |
| Changer de vaisseau | Tab | Y |
| Pause (retraite, résolution automatique) | Échap | Start |

Sur un clavier QWERTY, les touches sont W/S, A/D et Q/E : elles sont repérées par leur position, pas par leur lettre.
Les crochets désignent la cible la plus proche de votre axe de tir (rouges quand elle est à portée) et le point rouge indique où viser pour la toucher.

### Combat piloté en vue de dessus

| Action | Clavier / souris | Manette Xbox |
|---|---|---|
| Propulsion | ZQSD / WASD | Stick gauche |
| Viser | Souris | Stick droit (avec visée assistée) |
| Canons | Clic gauche | RT |
| Missiles à tête chercheuse | Clic droit | LT |
| Postcombustion | Maj | A ou LB |
| Changer de vaisseau | Tab | Y |
| Pause (retraite, résolution automatique) | Échap | Start |

---

## Règles en bref

- **Temps** : il s'écoule en continu. La partie commence en pause, le temps de donner vos premiers ordres.
- **Expansion** : revendiquez un système voisin de votre territoire. S'il contient une planète habitable, vous y fondez une colonie ; sinon, un avant-poste.
- **Économie** : chaque colonie a une population qui grandit avec le temps et une spécialisation :
  - Énergie, Mines, Industrie (alliages) ou Recherche.
  - Les **alliages** servent à construire les vaisseaux, l'**énergie** à les entretenir.
- **Recherche** : quatre domaines de 5 niveaux chacun.
  - Armes : +15 % de dégâts par niveau.
  - Boucliers : +25 % par niveau.
  - Propulsion : +10 % de vitesse par niveau.
  - Production : +10 % par niveau.
- **Vaisseaux** :
  - **Chasseur** : rapide et fragile.
  - **Corvette** : polyvalente, avec missiles.
  - **Croiseur** : lent et très résistant, canon lourd, tourelles automatiques et missiles.
- **Guerre** : une flotte qui reste dans un système ennemi sans défenseur l'assiège, puis le conquiert. Quand des flottes ennemies se rencontrent, vous choisissez entre **piloter la bataille** et la **résoudre automatiquement**.
- **Victoire** : éliminez tous les empires rivaux. **Défaite** : vous perdez votre dernier système.

Les empires rivaux laissent une période de paix au début de la partie. Profitez-en pour vous développer.

### Difficulté et vue des combats

Ces deux réglages se choisissent dans le menu principal et sont mémorisés. La vue des combats peut aussi être changée en cours de partie, depuis la fenêtre d'une bataille ou le menu pause.

| Niveau | Rivaux | Combats | Bonus de départ |
|---|---|---|---|
| Très facile | production −50 %, paix ≈ 4 ans, sièges contre vous 80 % plus longs | l'ennemi inflige −55 % de dégâts, vous +35 %, il ne s'acharne pas sur votre vaisseau | +1 corvette, +1 chasseur, +150 alliages |
| Facile | production −28 %, paix ≈ 2 ans 10 mois | l'ennemi inflige −30 % de dégâts, vous +15 % | +1 chasseur, +75 alliages |
| Normale | à armes égales, paix ≈ 1 an 10 mois | équilibrés | — |
| Difficile | production +30 %, attaques plus précoces | l'ennemi inflige +15 % de dégâts | — |
| Très difficile | production +60 %, paix ≈ 11 mois | l'ennemi inflige +30 % de dégâts | — |

Votre production est aussi augmentée en Très facile (+30 %) et en Facile (+15 %).

---

## Organisation du code

```
Assets/StellarisMini/
├── Editor/ProjectSetup.cs           Configuration automatique (scène, build, matériau)
└── Scripts/
    ├── Core/
    │   ├── GameManager.cs           Point d'entrée, transitions entre écrans
    │   ├── GameInput.cs             Clavier / souris / manette Xbox (Input System), vibrations
    │   ├── Gfx.cs                   Matériaux, textures, formes des vaisseaux, champ d'étoiles
    │   ├── Fx.cs                    Particules (impacts, explosions, traînées)
    │   └── Sfx.cs                   Sons synthétisés au démarrage
    ├── UI/
    │   ├── UI.cs                    Kit d'interface IMGUI (boutons arrondis) avec navigation à la manette
    │   └── MainMenuScreen.cs        Menu principal et écran d'aide
    ├── Galaxy/
    │   ├── Model.cs                 Données : systèmes, planètes, empires, flottes…
    │   ├── ShipCatalog.cs           Caractéristiques des vaisseaux (à modifier pour l'équilibrage)
    │   ├── Difficulty.cs            Les 5 niveaux de difficulté et leurs réglages
    │   ├── GalaxyGenerator.cs       Génération procédurale de la galaxie
    │   ├── GalaxySim.cs             Simulation : temps, économie, déplacements, sièges, batailles
    │   ├── EmpireAI.cs              IA des empires rivaux
    │   ├── AutoResolve.cs           Résolution automatique des batailles
    │   ├── GalaxyView.cs            Rendu de la carte
    │   └── GalaxyScreen.cs          Contrôles et panneaux de la carte
    └── Combat/
        ├── CombatHud.cs             Interface commune aux deux modes (jauges, pause, résultat)
        ├── CombatSession.cs         Combat en vue de dessus : arène, caméra, projectiles
        ├── CombatShip.cs            Vaisseau en vue de dessus : pilotage, IA, armes, dégâts
        ├── CombatSession3D.cs       Combat en 3D : décor, caméra de poursuite, projectiles, visée
        └── CombatShip3D.cs          Vaisseau 3D : vol libre, IA, armes, dégâts
```

Pour l'**équilibrage**, les réglages principaux se trouvent ici :

- `ShipCatalog.cs` : coûts, vitesses, points de vie et armes des vaisseaux.
- `GalaxySim.cs` : vitesse du temps (`DaysPerSecond`), coûts de colonisation et production des colonies.
- `Difficulty.cs` : tous les réglages de chaque niveau de difficulté.

---

## Limites actuelles et pistes

- Pas de sauvegarde / chargement.
- Pas de diplomatie : tous les empires sont en guerre les uns contre les autres.
- Interface en IMGUI, simple et entièrement pilotable à la manette. Elle pourra être remplacée par UI Toolkit ou uGUI pour un rendu plus soigné.
- Les graphismes sont procéduraux et peuvent être remplacés par de vrais modèles : il suffit de modifier `Gfx.ShipMesh3D` et `CombatShip3D` (ou `Gfx.ShipMesh` et `CombatShip` en vue de dessus).
- Le code a été compilé contre les assemblies de référence d'Unity et la simulation stratégique a été testée hors de l'éditeur, mais le jeu n'a pas encore été lancé dans l'éditeur Unity. Signalez tout problème rencontré à l'ouverture.
