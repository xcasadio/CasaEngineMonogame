# Plan agent IA — Écran principal des démos et un monde par démo

Plan d'exécution né d'une demande de l'auteur le 2026-10-08 : remplacer la fenêtre en deux parties de `CasaEngine.Demos` (navigateur à gauche, scène à droite, ADR-0070) par un écran principal qui présente les démos et permet d'en choisir une ; une fois la démo choisie, un monde est chargé pour elle ; pendant une démo, Échap ou le bouton Select de la manette ramènent à l'écran principal.
Les décisions D1 → D8 ci-dessous ont été arbitrées avec l'auteur le 2026-10-08 (plan approuvé le même jour, exécution en mode AUTO) : **ce plan les applique, il ne les rediscute pas**.

Ce fichier doit être mis à jour pendant le travail : l'icône au début de chaque tâche indique son statut courant.

> **Quand écrire un plan** : dès que le travail demande plus d'un commit. En dessous, exécution directe avec le rapport de fin de tâche d'`AGENTS.md`.
> **Avant d'écrire le plan** : poser toutes les questions en une seule fois ; ne rien inventer, ne rien supposer.
> **Après approbation** : exécution autonome, tâche par tâche ; arrêt uniquement sur ⚠️ Blocked.

## Objectif

`CasaEngine.Demos` démarre sur un **écran principal** plein écran, dans un **monde « menu »** qui lui est propre :

- à gauche, la liste des thèmes et de leurs démos ; à droite, la fiche de la démo sélectionnée (titre, thème, description et touches) ; des boutons « Lancer » et « Quitter » ;
- navigation à la souris, au clavier (flèches, Entrée) et à la manette (croix directionnelle, A), avec le thème Dark de MGUI ;
- lancer une démo affiche « Chargement… » pendant une image, puis charge un **monde neuf** que la démo construit en code ;
- pendant une démo, **Échap** ou **Back/Select** (manette) ramènent à l'écran principal, sur la démo qu'on vient de quitter ; le monde de la démo est entièrement démonté ;
- sur l'écran principal, ni Échap ni Back ne quittent : on quitte avec « Quitter » ou la croix de la fenêtre.

Le navigateur latéral, F1, le repli et le partage de la fenêtre disparaissent : chaque démo occupe toute la fenêtre. L'automatisation (`CASAENGINE_START_DEMO`, captures, sonde de pixels) continue de fonctionner. La décision est enregistrée dans l'ADR-0072 ; la doc `docs/engine/demos-browser.md` est remplacée.

## État vérifié du dépôt (2026-10-08)

Arbre et branches :

- Worktree `.claude/worktrees/demos-main-menu` sur `chantier/demos-main-menu`, créé depuis `main` `07e354d0` (« remove useless demo », commit de l'auteur qui retire les sept démos PSX). Sous-modules : `MGUI` `3db3c769` (`develop`), `NvgSharp` `9c0da031`.
- Modifications préexistantes de l'auteur dans son checkout principal : `CasaEngine.Launcher/Program.cs`, `Projects/SampleProject/.casaeditor/viewport.editor.json` (non suivi). Le chantier ne les touche pas.
- Numéro d'ADR : `docs/decisions/` va jusqu'à 0070 sur toutes les branches de ce dépôt, mais la copie du moteur dans `alundra-casaengine-project-converter` a déjà une ADR-0069 et une ADR-0071. Ce chantier prend **0072** pour ne pas entrer en collision lors d'un rapprochement.

Application des démos (découverte en lecture seule, quatre relevés, faits revérifiés sur les points qui portent le design) :

- 24 démos enregistrées par `AddDemo(demo, theme, collapsesBrowser)` dans `DemosGame.LoadContentPrivate` (`CasaEngine.Demos/DemosGame.cs:112-140`). Toutes sont du code : `Demo` a `Title`, `Description` (texte simple), `Initialize`, `ConfigureSceneLighting`, `CreateCamera`, `InitializeCamera`, `Update`, `PostDraw`, `OnScreenResized`, `Clean` (`CasaEngine.Demos/Demo.cs:10-62`).
- Un seul `World`, créé au démarrage (`DemosGame.cs:109-110`) et vidé à chaque changement : `ChangeDemo` appelle `ClearEntities` puis `Clean` de l'ancienne démo, puis `Initialize`, `ConfigureSceneLighting`, `CreateCamera` de la nouvelle ; quand le monde est déjà chargé, `ViewManager.Clear`, `World.LoadContent`, `BootstrapViews` et `InitializeCamera` (`DemosGame.cs:336`, `ChangeDemo`). Le changement est différé à l'update suivant (`RequestDemo`), et `_currentDemo.Update` (`DemosGame.cs:501`) n'est jamais protégé contre l'absence de démo.
- Échap et Back de la manette appellent `Exit()` (`DemosGame.cs:520-523`). Aucune démo ne lit Échap ou Back.
- Navigateur : `DemoBrowserScreen` sur sa propre `UIRoot` installée comme UI de fenêtre (ADR-0070), `demo-browser.xaml`, F1, `DemoHintOverlay` (« Press F1 to show the demo browser », `Content/Screens/demo-hint.xaml:9`), règle P9 de `DemoKeyboard` (`BrowserOwnsKeyboard`), repli pour `SplitScreenDemo` et `ViewManagerSandbox` (`DemosGame.cs:135, 138`). `Content/Screens/ui-overlay-hud.xaml:19` parle encore du « demo navigator » et de F1.
- `DemoKeyboard.Read` est lu par dix démos et par `PlayerComponent` (`CasaEngine.Demos/PlayerComponent.cs`).
- Automatisation : `CASAENGINE_START_DEMO` (index ou titre), `CASAENGINE_DEMO_BROWSER`, `CASAENGINE_CAPTURE_SCREENSHOT_PATH` et `_DELAY_MS`, `CASAENGINE_SHOW_DEBUG_OVERLAY`, `CASAENGINE_DEMO_PIXELS_PATH` (sonde de `SplitScreenDemo`, `SplitScreenDemo.cs:156-201`) ; `DemosGame.cs:420-421, 617-618` testent encore `CASAENGINE_PSXQUAD_DUMP_PATH`, reste des démos PSX retirées.
- Tests liés : `CasaEngine.Tests/UI/DemoScreenXamlTests.cs` (chargement strict de tous les écrans des démos), `DemoBrowserTreeTests.cs`, `DemoBrowserFocusTests.cs`, `CasaMguiBackendOwnershipTests.cs` (analyse des sources des démos : jetons interdits). Aucun test n'instancie une démo.

Moteur (vérifié dans le code) :

- `GameManager.SetWorldToLoad(World)` remplace le monde courant **sans le vider** : seul le chargement par chemin (`SetWorldToLoad(string)`) appelle `Clear()` sur l'ancien monde (`CasaEngine/Framework/Application/GameManager.cs:84-158`). Le changement a lieu dans `UpdateWorld`, appelé par `CasaEngineGame.Update` (`CasaEngineGame.cs:571`) pendant le `base.Update` de `DemosGame` : il collecte les assets non référencés (ADR-0036, ADR-0037), vide les vues, charge le monde, recrée les vues, lance `BeginPlay`, puis lève `WorldLoaded`. L'éditeur s'appuie sur ce comportement pour mettre de côté son monde d'édition pendant une session de jeu (`EditorPlaySessionController.cs:51`, `RestoreWorld`).
- `World.Clear()` arrête le gameplay, les voix possédées par le monde, détruit les UI de composants et les entités, libère le contexte physique (`CasaEngine/Framework/Scene/World/World.cs:117-151`).
- Les écrans poussés dans une vue disparaissent avec les vues à chaque changement de monde ; l'écran-titre du RPGDemo est poussé sur la vue active au `OnBeginPlay` de son monde et retiré puis libéré au `OnEndPlay` (`Projects/CasaEngine.RPGDemo/Scripts/ScriptTitleScreenWorld.cs:34-49`, `GameScreenManager.PushScreenToActiveView`, `RemoveScreenFromActiveView`).
- Pas de chargement asynchrone ni d'écran de chargement dans le moteur (`docs/engine/asset-handles-and-bitmap-fonts.md:156`).
- Une démo doit être construite **avant** le `LoadContent` de son monde :
  - `World.AddEntity` ne fait que mettre l'entité en file d'attente (`World.cs:168-173`) ; elle n'entre dans le monde qu'au `LoadContent` ou au `World.Update` suivant, et une exception à l'ajout est seulement journalisée (`World.cs:797-800`).
  - `DefaultRuntimeViewBootstrapper.BootstrapViews` choisit la caméra de la vue parmi les entités du monde et en crée une par défaut s'il n'en trouve pas (`DefaultRuntimeViewBootstrapper.cs:29-35`, avertissement « No camera found in the world » à `World.cs:466`).
  - `TopDownElevationDemo.Initialize` règle la politique d'espace du monde, lue à la création du contexte physique dans `World.LoadContent` (`TopDownElevationDemo.cs:48-49`).
  - Le démarrage actuel respecte cet ordre : la démo est construite sur le monde pas encore chargé, sa caméra est gardée (`_pendingStartupCamera`) et `InitializeCamera` ne s'exécute qu'au `WorldLoaded` (`DemosGame.cs:357-392`).
- `Logs.AddLogger(ILogger)` permet d'écouter le journal (`CasaEngine/Core/Logging/Logs.cs:14`).
- MGUI navigue au clavier (Échap = annuler, Entrée et Espace = valider, flèches) et à la manette (A = valider, B et Back = annuler, croix directionnelle et stick) (`MGUI/MGUI.Core/UI/Navigation/UIFocusNavigationService.cs:270-283, 519-581`). Le runtime UI du moteur met à jour l'`InputTracker` de MGUI (`CasaEngine/Framework/UI/Backend/MonoGame/CasaDesktopRuntime.cs:113`), qui lit lui-même la manette (`MGUI/MGUI.Shared/Input/InputTracker.cs:64-71`). Aucune vérification n'a encore été faite avec une vraie manette.
- MGUI a `MGTreeView`, `MGListBox`, `MGScrollViewer`, `MGImage` ; pas de composant « carte ». Les lignes de texte sont plus hautes depuis MGUI ADR-0023.

## Décisions verrouillées

| Réf | Décision |
|---|---|
| D1 | L'écran principal vit dans un **monde « menu »** à lui (réponse « Monde menu ») : le monde est reconstruit à chaque retour ; son écran est poussé sur la vue active quand le monde est chargé, et retiré puis libéré avant d'en sortir, comme l'écran-titre du RPGDemo. |
| D2 | Chaque lancement de démo crée un **World neuf**, que la démo construit en code comme aujourd'hui (réponse « Monde neuf en code »). Au retour, ce monde est entièrement démonté. Comme `SetWorldToLoad(World)` ne vide pas l'ancien monde, l'application des démos appelle `World.Clear()` sur le monde sortant avant de le remplacer ; le moteur n'est pas modifié (l'éditeur dépend du comportement actuel). |
| D3 | Présentation **liste et fiche** (réponse) : thèmes et démos à gauche, fiche à droite, boutons « Lancer » et « Quitter ». |
| D4 | Sur l'écran principal, **ni Échap ni Back ne quittent** (réponse « Aucun ne quitte ») : on quitte avec « Quitter » ou la croix de la fenêtre. |
| D5 | Pendant une démo, **Échap** ou **Back/Select** de la manette ramènent à l'écran principal (demande de l'auteur), sur la démo quittée. |
| D6 | Choix annoncés à l'auteur et confirmés à l'approbation du plan : `CASAENGINE_START_DEMO` lance directement la démo sans passer par le menu ; le navigateur latéral, F1 et le repli disparaissent ; un rappel « Échap / Select : menu » s'affiche dans chaque démo ; « Chargement… » s'affiche pendant une image avant le changement de monde. |
| D7 | Le code de l'écran principal reste dans `CasaEngine.Demos` : pas de nouvelle API du moteur ni de MGUI. L'UI de fenêtre et la zone de layout (ADR-0070) restent dans le moteur, sans utilisateur dans les démos. |
| D8 | Décor du monde menu (point O1, réponse « Fond uni sombre ») : pas de décor ; seul l'écran principal, sur le thème Dark. Un décor pourra venir dans un chantier à part. |

## Règles d'exécution pour l'agent

- **Branche dédiée `chantier/demos-main-menu`**, créée depuis `main` `07e354d0`, worktree `.claude/worktrees/demos-main-menu`. Ne jamais committer sur `main`.
- **Une seule tâche à la fois.** Avant de commencer une tâche, remplacer son icône `⏳` par `🚧`. À la fin, lancer la validation indiquée, remplacer l'icône par `✅`, `🧪` ou `⚠️`, ajouter une courte note de validation sous la tâche, puis **créer un commit dédié** qui inclut la mise à jour de ce fichier.
- **Un commit par tâche**, atomique et compilable, message en anglais au format `type(area): summary`.
- **Ne jamais pousser.** Le merge sur `main` reste une décision humaine.
- **Ne rien inventer** : toute API, tout fichier, toute règle utilisée existe dans le dépôt, vient d'une réponse de l'auteur, ou d'une doc officielle citée (URL). Sinon : passer la tâche en ⚠️ Blocked, écrire la question dans « Points ouverts », et **s'arrêter**.
- **Build obligatoire** (`dotnet build CasaEngine.MonoGame.sln`, et `CasaEngine.Editor.MonoGame.sln` dès qu'un fichier partagé change) et **tests** `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj` avant de passer une tâche en ✅.
- Si le code est écrit mais qu'une vérification visuelle ou manuelle manque, utiliser `🧪 Needs testing` et noter précisément ce qui manque.
- **Ne jamais laisser une tâche en 🚧** à la fin d'une session.
- **Ne jamais indexer** les modifications préexistantes de l'auteur : `git add` fichier par fichier.
- **Périmètre** : une tâche ne modifie que les fichiers de sa ligne « Fichiers ». Toute autre modification nécessaire : ⚠️ Blocked, question dans « Points ouverts », arrêt.
- **Défaut antérieur** : un échec qui se reproduit aussi sur l'état de référence (par exemple au démarrage direct par `CASAENGINE_START_DEMO`, chemin inchangé par ce chantier) n'est pas corrigé ici : ⚠️ Blocked, question dans « Points ouverts », arrêt.
- **Budget** : deux passes de correction et de revalidation par tâche ; au-delà, ⚠️ Blocked avec l'état et les preuves.
- **Retour arrière** : chaque tâche est un commit sur la branche du chantier ; l'annuler revient à `git revert` de ce commit (les tâches suivantes en dépendent selon leur ligne « Prérequis »).
- **Langue** : ce plan en français ; code, messages de commit, docs de `docs/` et ADR en anglais.
- Rappel moteur : pas d'allocation, de LINQ ni de closure dans les chemins chauds (l'update de l'écran principal et la lecture des touches en sont) ; le runtime ne dépend pas de l'éditeur.

## Légende des statuts

- ⏳ Todo : pas encore commencé.
- 🚧 In progress : en cours de modification locale.
- 🧪 Needs testing : code écrit, validation incomplète ou en attente.
- ✅ Done : code validé, build/tests OK, commit effectué.
- ⚠️ Blocked : bloqué par une erreur non résolue ou une décision manquante.

## Validation globale

- `dotnet build CasaEngine.MonoGame.sln` et `dotnet build CasaEngine.Editor.MonoGame.sln` : 0 erreur ; `CasaEngine.Tests` vert, nouveaux tests compris.
- Parcours automatisé `CASAENGINE_DEMO_CYCLE` : menu, chaque démo, retour au menu, sans exception ; à chaque retour, le monde menu ne contient que ses propres entités et une seule vue.
- Captures : l'écran principal (sans `CASAENGINE_START_DEMO`), et trois démos lancées par `CASAENGINE_START_DEMO` identiques aux captures de référence prises en T0.1 en pleine fenêtre ; sonde de pixels de `SplitScreenDemo` identique à la référence.
- Vérifications de l'auteur : navigation à la manette (croix, A, Back) et au clavier.
- Vérificateur frais sur l'ensemble du chantier.

---

## Phase 0 — Cadrage

### ✅ T0.1 — Plan et mesures de référence

- Objectif : ce plan approuvé, et des références pour prouver que les démos ne changent pas.
- Fichiers : `ai-agent/tasks/demos-main-menu-tasks.md`, `ai-agent/README.md`.
- Étapes :
  1. Builds et tests de référence.
  2. Captures de référence de trois démos (une 3D avec physique, une 2D, une démo à UI) avec `CASAENGINE_START_DEMO`, `CASAENGINE_DEMO_BROWSER=collapsed` (pleine fenêtre, comme après le chantier) et la sonde de pixels de `SplitScreenDemo` ; ligne « elevation separated pair » de `TopDownElevationDemo` au démarrage.
  3. Ligne du tableau de `ai-agent/README.md`.
- Validation : chiffres et fichiers de référence notés ici (images dans le dossier temporaire de session, non commitées).
- Commit : `docs(ai-agent): plan the demos main screen and one world per demo`
- Note :
  - Builds des deux solutions : 0 erreur ; `CasaEngine.Tests` 4151/4151.
  - Références (dossier temporaire de session `scratchpad/dm/`, non commitées), toutes en pleine fenêtre 1024x768 :
    - captures `base-collision3d`, `base-tilemap`, `base-uioverlay` par `CASAENGINE_START_DEMO` = 2, 9, 22 ; deux exécutions par démo, identiques au pixel près, donc comparables ;
    - sonde de `SplitScreenDemo` (`CASAENGINE_START_DEMO=18`) : `OK UI element of the offset view pixel=(612,312) read=(255,0,0)`, `result=PASS` ;
    - `TopDownElevationDemo` (`CASAENGINE_START_DEMO=4`) journalise « elevation separated pair: not colliding ».
  - Aucun `[Error]` ni `[Warning]` dans les journaux de ces exécutions : le critère « aucune erreur journalisée » du parcours est tenable.

---

## Phase 1 — Un monde par démo

### ✅ T1.1 — Lancer chaque démo dans un monde neuf

- Objectif : D2, avant tout changement d'interface ; le navigateur actuel sert encore à changer de démo.
- Prérequis : T0.1 ✅, avec ses captures, sa sonde de `SplitScreenDemo` et sa ligne « elevation separated pair » notées dans ce plan.
- Fichiers : `CasaEngine.Demos/DemosGame.cs`, et un fichier nouveau pour l'automatisation du parcours dans `CasaEngine.Demos/` (par exemple `DemoCycleAutomation.cs`). Aucune démo n'est modifiée : si le parcours montre qu'une démo dépend de l'ancien ordre (`ClearEntities` sur un monde réutilisé), la tâche passe en ⚠️ Blocked avec la démo et la preuve, et l'auteur décide.
- Étapes :
  1. Un seul chemin pour lancer une démo, appliqué dans `DemosGame.Update` avant `base.Update` (donc avant le `UpdateWorld` de la même image), et emprunté aussi par le démarrage :
     1. si une démo tourne : `World.Clear()` sur le monde sortant, puis `Clean` de la démo sortante (même ordre qu'aujourd'hui : entités d'abord) ;
     2. remise à zéro de `ViewManager.AutoLayoutMode`, titre de la fenêtre, sélection et repli du navigateur tant qu'il existe ;
     3. `var world = new World()` et `GameManager.SetWorldToLoad(world)` : `CurrentWorld` est le nouveau monde dès cet appel (`GameManager.cs:159-163`) ;
     4. `Initialize`, `ConfigureSceneLighting(world)` et `CreateCamera` sur ce monde **avant** son `LoadContent`, caméra gardée en attente ;
     5. au `WorldLoaded` (fin du `UpdateWorld`, après `LoadContent`, `BootstrapViews` et `BeginPlay`) : seulement `InitializeCamera`, réglages de vue de l'automatisation et UI de la démo.
     C'est l'ordre du démarrage actuel, étendu aux changements en cours de session. Comme le démarrage crée son monde par ce chemin, `EndLoadContent` ne retombe jamais sur `FirstWorldLoaded`.
  2. `_currentDemo` peut être absent : `Update`, `PostDraw` et `OnScreenResized` le testent.
  3. Automatisation `CASAENGINE_DEMO_CYCLE` : charge chaque démo à tour de rôle par ce chemin (changements en cours de session), un nombre fixe d'images chacune. Après chaque `WorldLoaded`, elle vérifie et journalise :
     - `CurrentWorld` est une nouvelle instance ;
     - le monde sortant n'a plus aucune entité ;
     - la caméra de chaque vue appartient au nouveau monde, sans caméra par défaut.
     Un `ILogger` ajouté par `Logs.AddLogger` compte les erreurs, les exceptions et l'avertissement « No camera found ». Le parcours quitte avec le code 1 si une vérification échoue ou si une erreur ou une exception a été journalisée, sinon avec le code 0. Avec `CASAENGINE_DEMO_PIXELS_PATH`, la sonde de `SplitScreenDemo` s'exécute quand le parcours l'atteint, donc après un changement. Le parcours passera par le menu en T2.3.
- Validation :
  - build, `CasaEngine.Tests` ;
  - `CASAENGINE_DEMO_CYCLE` quitte avec le code 0 sur les 24 démos ; le journal contient « elevation separated pair: not colliding » pour `TopDownElevationDemo` et aucun « No camera found » ;
  - preuve que le parcours détecte l'erreur visée : une mutation temporaire qui construit la démo au `WorldLoaded` le fait quitter avec le code 1 (mutation annulée ensuite) ;
  - captures des trois démos par `CASAENGINE_START_DEMO` identiques à T0.1 ; sonde de `SplitScreenDemo` identique à T0.1 au démarrage et atteinte par le parcours, après un changement ;
  - une démo qui échoue au parcours mais pas au démarrage direct relève de ce chantier (voir « Fichiers ») ; une démo qui échoue aussi au démarrage direct relève d'un défaut antérieur (voir « Règles d'exécution »).
- Commit : `feat(demos): load each demo in a fresh world`
- Note :
  - `ChangeDemo` vide le monde sortant (`World.Clear()`), appelle `Clean` de la démo sortante, crée un `World` neuf, `SetWorldToLoad`, puis construit la démo sur ce monde avant son chargement ; `OnWorldLoaded` n'initialise que la caméra, les réglages de vue et l'UI. Le démarrage passe par le même chemin. Aucune démo modifiée.
  - `CASAENGINE_DEMO_CYCLE=60` (nouveau `DemoCycleAutomation.cs`) : 48 étapes (24 démos, deux tours), toutes « OK », `result=PASS failedChecks=0 errors=0 defaultCameraWarnings=0`, code 0. « elevation separated pair: not colliding » journalisé. Sonde de `SplitScreenDemo` atteinte après un changement : `result=PASS`.
  - Mutation (démo construite au `WorldLoaded`, annulée ensuite) : 17 étapes « FAIL » (caméra de la démo absente des vues, avertissement « No camera found »), puis exception dans `SceneManagementDemo.Update` et sortie avec un code non nul (127). Le parcours détecte bien l'erreur visée.
  - Builds des deux solutions : 0 erreur ; `CasaEngine.Tests` 4151/4151.
  - Captures par `CASAENGINE_START_DEMO` : `tilemap` et `uioverlay` identiques à T0.1 au pixel près.
  - `collision3d` à 1500 ms ne se compare pas : le pas de temps est variable (capture à l'update 106, soit 1500,4 ms, avant le chantier ; à l'update 108, soit 1507,8 ms, après ; monde chargé à l'update 1 dans les deux cas, mesuré par une instrumentation temporaire retirée), donc la chute des boîtes dépend du temps réel. À 300 ms, scène encore statique, l'ancien binaire et le nouveau donnent la même image au pixel près (deux exécutions chacun).
  - `splitscreen` : seuls les compteurs de performance diffèrent (FPS, temps d'update et de dessin) ; la sonde passe au démarrage comme à T0.1.

---

## Phase 2 — Écran principal

### ✅ T2.1 — Écran principal (liste et fiche)

- Objectif : l'écran de D3, testé seul, pas encore branché.
- Prérequis : T0.1 ✅.
- Fichiers : `CasaEngine.Demos/Content/Screens/main-menu.xaml`, `CasaEngine.Demos/Demos/DemoUI/MainMenuScreen.cs` (noms définitifs fixés à l'écriture), tests dans `CasaEngine.Tests/UI/`.
- Étapes :
  1. XAML : liste des thèmes et des démos (arbre comme le navigateur actuel), fiche (titre, thème, description), boutons « Lancer » et « Quitter », zone « Chargement… » ; thème Dark.
  2. Code : construction de la liste depuis le registre des démos (ordre des thèmes inchangé), sélection initiale (première démo, ou la démo quittée), fiche suivant la sélection, « Lancer » et la validation d'une démo appellent un rappel du jeu, « Quitter » un autre.
  3. Tests : chargement strict du XAML et noms attendus, arbre et ordre des thèmes, sélection initiale et restaurée, validation au clavier (Entrée) qui déclenche le lancement, « Quitter » qui déclenche son rappel.
- Validation : build, `CasaEngine.Tests` (nouveaux tests et `DemoScreenXamlTests`).
- Commit : `feat(demos): add the demos main screen`
- Note :
  - `Content/Screens/main-menu.xaml` : en-tête, arbre `treeDemos`, fiche (`lblTitle`, `lblTheme`, `lblDescription`), boutons `btnLaunch` et `btnQuit`, ligne d'aide `lblStatus`.
  - `Demos/DemoUI/MainMenuScreen.cs` : thème Dark sur sa propre fenêtre, arbre par thèmes dans l'ordre du jeu, sélection initiale (démo donnée, sinon la première), fiche qui suit la sélection (bouton Launch désactivé sur un thème), lancement par le bouton ou un double-clic sur la démo, Quit, `TryGetSelectedDemo` pour Entrée et A (lus par le jeu en T2.3), `ShowLoading`.
  - Deux faits MGUI ont guidé le code :
    - `MGTreeView.ItemDoubleClicked` ne se déclenche que sur un élément qui a des enfants (`MGTreeViewItem.cs:57-61`) : chaque démo écoute donc son propre `LMBDoubleClickedInside` ;
    - Entrée et la validation de la manette ne font que déplier ou replier l'élément sélectionné (`MGTreeView.cs:492-497, 567-570`) : le jeu lit Entrée et A lui-même.
  - Tests : `CasaEngine.Tests` ne référence pas l'application des démos (comme pour l'ancien navigateur). Les tests portent donc sur le vrai `main-menu.xaml` dans un bureau sans affichage :
    - `TheMainMenu_DeclaresWhatItsScreenLooksUp` (noms, fenêtre pleine), plus le chargement strict de tous les écrans livrés ;
    - `DemoMainMenuTests` : les flèches déplacent la sélection de l'arbre ayant le focus, le double-clic atteint la démo et pas `ItemDoubleClicked`, Entrée et Échap ne changent rien, les boutons exécutent leur commande.
    - La logique propre à la classe (ordre des thèmes, sélection initiale) n'a qu'une preuve par capture (T2.3, écran au démarrage : ordre des thèmes et première démo sélectionnée) ; le parcours ne la vérifie pas, et la sélection restaurée au retour d'une démo est à regarder par l'auteur (T2.3).
  - Les deux solutions sans erreur ; `CasaEngine.Tests` 4158/4158 (7 nouveaux).

### ✅ T2.2 — Retrait du navigateur latéral

- Objectif : D6 côté interface ; chaque démo occupe toute la fenêtre.
- Prérequis : T1.1 ✅ et T2.1 ✅ (les tests de l'écran principal remplacent ceux du navigateur).
- Fichiers :
  - `CasaEngine.Demos/DemosGame.cs`, `CasaEngine.Demos/DemoKeyboard.cs` ;
  - suppression de `CasaEngine.Demos/Demos/DemoUI/DemoBrowserScreen.cs` et `CasaEngine.Demos/Content/Screens/demo-browser.xaml` ;
  - `CasaEngine.Demos/Content/Screens/demo-hint.xaml` (gardé, nouveau texte) et `CasaEngine.Demos/Content/Screens/ui-overlay-hud.xaml` (texte F1) ;
  - tests : suppression de `CasaEngine.Tests/UI/DemoBrowserTreeTests.cs` et `DemoBrowserFocusTests.cs`, mise à jour de `CasaEngine.Tests/UI/DemoScreenXamlTests.cs` ;
  - ajoutés pendant l'exécution (nettoyage de commentaires requis par le critère `rg`) : `CasaEngine.Demos/Demos/ViewManagerSandbox.cs`, deux lignes d'un commentaire de classe qui renvoyaient à l'ancien « demo navigator panel » et à F1 ; `CasaEngine.Demos/Demos/DemoUI/DemoHintOverlay.cs`, commentaire de classe qui nommait `DemoBrowserScreen` (oubli de cette liste relevé par le vérificateur). Aucun code de démo modifié.
- Étapes :
  1. Retirer le navigateur, F1, la zone de layout et la poignée, les replis (`collapsesBrowser`), `BrowserOwnsKeyboard` (`DemoKeyboard` ne garde que la condition « fenêtre active »), `CASAENGINE_DEMO_BROWSER` et le reste `CASAENGINE_PSXQUAD_DUMP_PATH`.
  2. Rappel dans les démos : `demo-hint.xaml` affiche « Échap / Select : menu » ; la ligne F1 de `ui-overlay-hud.xaml` est remplacée.
  3. `DemoScreenXamlTests.cs` :
     - retirer le cas `demo-browser.xaml` de `EachPlacedScreen_LandsWhereItsOldArithmeticPutIt` et le test `TheDemoBrowser_DeclaresWhatItsScreenLooksUp` (couvert par les tests de T2.1) ;
     - mettre à jour la ligne attendue de `ui-overlay-hud.xaml` dans `TheTwoCornerHuds_GrowWithTheirText` ;
     - garder `demo-hint.xaml` dans le test de marge (`APlacedScreen_KeepsItsFullContentArea`), puisque l'écran reste.
  4. Démarrage temporaire sur la démo 0 ou `CASAENGINE_START_DEMO` (l'écran principal arrive en T2.3).
- Validation : build, `CasaEngine.Tests` ; `rg "demo-browser|DemoBrowser|Press F1" CasaEngine.Tests CasaEngine.Demos` ne trouve plus rien ; captures des trois démos et sonde de `SplitScreenDemo` identiques à T0.1.
- Commit : `refactor(demos): remove the side demo browser`
- Note :
  - `DemosGame` sans navigateur : plus de `UIRoot` de fenêtre, de zone de layout, de poignée, de F1, d'Entrée pour le navigateur, de repli, de `CASAENGINE_DEMO_BROWSER` ni de reste `CASAENGINE_PSXQUAD_DUMP_PATH`. Thème PSX retiré de l'ordre des thèmes (plus aucune démo PSX). Le redimensionnement de la fenêtre remet les vues en page par `OnScreenResized`, comme avant pour une fenêtre sans navigateur. Démarrage encore sur la démo 0 ou `CASAENGINE_START_DEMO` ; Échap quitte encore (le retour au menu vient en T2.3).
  - `DemoKeyboard` ne garde que « fenêtre active » ; `demo-hint.xaml` affiche « Esc / Select: back to the menu » ; la ligne d'aide de `ui-overlay-hud.xaml` devient « Esc or Select: back to the menu ».
  - `rg "demo-browser|DemoBrowser|Press F1|BrowserOwnsKeyboard" CasaEngine.Tests CasaEngine.Demos` : rien.
  - Les deux solutions sans erreur ; `CasaEngine.Tests` 4149/4149 (les 9 tests du navigateur retirés).
  - Captures : `tilemap` identique à T0.1 ; `collision3d` identique à 300 ms au binaire d'avant le chantier. `uioverlay` ne diffère que par la nouvelle ligne d'aide, et `splitscreen` que par les compteurs de temps ; la sonde passe.

### 🧪 T2.3 — Monde « menu », lancement et retour

- Objectif : D1, D4, D5 et le reste de D6.
- Prérequis : T1.1 ✅, T2.1 ✅, T2.2 ✅.
- Fichiers : `CasaEngine.Demos/DemosGame.cs`, `CasaEngine.Demos/DemoCycleAutomation.cs` (le parcours passe par le menu, étape 4).
- Étapes :
  1. Démarrage dans le monde menu (sans `CASAENGINE_START_DEMO`) : monde neuf, écran principal poussé sur la vue active au `WorldLoaded`, retiré et libéré avant d'en sortir.
  2. « Lancer » : « Chargement… » pendant une image, puis le chemin de T1.1.
  3. Pendant une démo, Échap ou Back (sur front montant) : retour au monde menu, sélection sur la démo quittée. Sur l'écran principal, Échap et Back ne quittent pas ; « Quitter » appelle `Exit()`.
  4. `CASAENGINE_DEMO_CYCLE` passe par le menu : menu, démo, menu, démo suivante, avec les vérifications de T1.1. À chaque retour au menu, il vérifie aussi que le monde menu est neuf, que le monde de la démo n'a plus d'entité, que l'écran principal est poussé une seule fois et qu'il n'y a qu'une vue.
- Validation : build, `CasaEngine.Tests` ; parcours complet avec le code 0 ; capture de l'écran principal ; navigation clavier et manette à vérifier par l'auteur (🧪 si non faite).
- Commit : `feat(demos): start on a menu world and return to it from a demo`
- Note :
  - Démarrage sans `CASAENGINE_START_DEMO` : `EnterMainScreen` crée un monde menu neuf avec sa propre caméra fixe (`CameraLookAtComponent`, pour que le moteur n'en crée pas une par défaut) et sans décor (D8). Au `WorldLoaded`, l'écran principal est poussé sur la vue active (`GameScreenManager.PushScreenToActiveView`, comme l'écran-titre du RPGDemo) ; il est retiré et libéré avant de quitter ce monde.
  - Un chemin unique pour quitter un monde (`LeaveCurrentWorld` : écran principal retiré, monde vidé, `Clean` de la démo), partagé par le lancement d'une démo et le retour au menu.
  - Lancement (bouton Launch, double-clic, Entrée ou A sur front montant) : « Loading … » pendant une image, puis le chemin de T1.1. Pendant une démo, Échap ou Back sur front montant ramènent au menu, sur la démo quittée. Sur l'écran principal, ni Échap ni Back ne quittent ; Quit quitte.
  - `CASAENGINE_DEMO_CYCLE=60` depuis le menu : 97 étapes (49 menus, 48 lancements : 24 démos, deux tours), toutes « OK », `result=PASS failedChecks=0 errors=0 defaultCameraWarnings=0`, code 0 ; menu : 1 entité, 1 vue, écran poussé une fois. La sonde de `SplitScreenDemo`, atteinte depuis le menu, passe.
  - Capture de l'écran principal (`scratchpad/dm/t23-menu.png`) : thème Dark, arbre des thèmes, première démo sélectionnée sous son thème déplié, fiche, boutons, ligne d'aide ; journal sans erreur ni avertissement.
  - Démarrage direct inchangé : `tilemap` identique à T0.1, `uioverlay` identique à T2.2, `collision3d` identique à 300 ms ; sonde PASS ; « elevation separated pair: not colliding » présent.
  - Les deux solutions sans erreur ; `CasaEngine.Tests` 4149/4149.
  - Reste pour l'auteur (🧪, aucune automatisation ne simule ces entrées) :
    - Échap et Select (manette) pendant une démo ;
    - Entrée et A sur l'écran principal, les flèches et la croix directionnelle ;
    - le double-clic ;
    - l'affichage d'une image de « Loading … » ;
    - au retour d'une démo, la sélection de l'écran principal sur la démo quittée.

---

## Phase 3 — Documentation

### ✅ T3.1 — ADR-0072 et documentation

- Objectif : enregistrer la décision et remplacer la doc du navigateur.
- Prérequis : T2.3 ✅ ou 🧪 (seules les vérifications manuelles de l'auteur manquent).
- Fichiers : `docs/decisions/0072-demos-start-on-a-menu-world-and-load-a-world-per-demo.md` (nom indicatif), `docs/decisions/README.md`, `docs/engine/demos-main-menu.md` (remplace `docs/engine/demos-browser.md`), `docs/README.md`, `docs/engine/animation-blend-demo.md` (mention de F1 et du navigateur).
- Validation : relecture ; liens et index à jour.
- Commit : `docs(demos): record the demos main screen (ADR-0072)`
- Note : `docs/decisions/0072-demos-start-on-a-menu-world-and-load-a-world-per-demo.md` et son index ; `docs/engine/demos-browser.md` renommé en `demos-main-menu.md` et réécrit (écran principal, touches, mondes, thèmes, ajout d'une démo, automatisation dont `CASAENGINE_DEMO_CYCLE`, limites) ; index `docs/README.md` ; `animation-blend-demo.md` (Échap ou Select au lieu de F1, lancement depuis l'écran principal). Plus aucune doc hors ADR ne parle du navigateur, de F1 ou de `CASAENGINE_DEMO_BROWSER`.

---

## Phase 4 — Vérification

### ✅ T4.1 — Vérification finale

- Objectif : prouver le chantier et rendre le rapport.
- Prérequis : T3.1 ✅.
- Étapes : parcours `CASAENGINE_DEMO_CYCLE`, captures, sonde ; `verifier` frais ; traitement des constats ; plan et `ai-agent/README.md` à jour ; rapport de fin.
- Commit : `docs(ai-agent): record the demos main screen verification`
- Note :
  - Trois vérificateurs frais en parallèle, chacun **CONFIRMED**, aucun constat P0 à P2.
    - Exécution : les deux solutions sans erreur, `CasaEngine.Tests` 4149/4149, `CASAENGINE_DEMO_CYCLE=60` à `result=PASS` en 97 étapes, captures de l'écran principal et du démarrage direct. Cas limites : titre inconnu, retour à la démo 0 ; parcours avec `CASAENGINE_START_DEMO=22`, `PASS` en 96 étapes.
    - Relecture du code : l'ordre de sortie et d'entrée des mondes est juste ; le `Clean` de chaque démo reste sans effet néfaste après `World.Clear`, grâce à des doubles libérations protégées.
    - Exactitude : le plan, l'ADR et la doc sont fidèles au code.
  - Avis reportés en O3 à O8 (P3 et P4) ; erreurs de compte rendu du plan corrigées dans ce commit (liste de T2.2, note de T2.1, liste de l'auteur en T2.3).

---

## Phase 5 — Suivi des avis O3 à O7

Demandée le 2026-10-08 par la session qui a mené le chantier (brief : corriger O3, O4, O6 et O7, O5 en option après question à l'auteur), sans rediscuter D1 → D8. Branche `chantier/demos-main-menu-advisories`, créée depuis `chantier/demos-main-menu` `a7363b94`, dans le même worktree. Mêmes règles d'exécution que les phases 0 à 4.

### ✅ T5.1 — O3 : un lancement demandé pendant « Loading … » est ignoré

- Objectif : la démo affichée comme en chargement est la seule qui se charge ; une seconde demande pendant l'image « Loading … » (double-clic, bouton) ne lance pas un second changement de monde.
- Fichiers : `CasaEngine.Demos/DemosGame.cs`, `CasaEngine.Demos/DemoCycleAutomation.cs`, `docs/engine/demos-main-menu.md`.
- Étapes :
  1. `ApplyWorldRequests` : quand le lancement en attente s'applique, la demande arrivée entre-temps (`_pendingDemoIndex`) est abandonnée.
  2. Le parcours redemande la même démo pendant chaque image « Loading … », comme le ferait un second clic, et échoue si un monde qu'il n'a pas demandé se charge.
- Validation : build des deux solutions, `CasaEngine.Tests` ; parcours `CASAENGINE_DEMO_CYCLE=60` à `result=PASS`, code 0 ; preuve : le même parcours sans l'étape 1 sort avec le code 1.
- Commit : `fix(demos): drop a launch asked during the loading frame`
- Note :
  - `ApplyWorldRequests` remet `_pendingDemoIndex` à -1 quand le lancement affiché comme en chargement s'applique.
  - Le parcours redemande la démo lancée à l'update suivant (l'image « Loading … ») et échoue sur un monde chargé sans demande (« this world was not asked for »). Doc `docs/engine/demos-main-menu.md` à jour.
  - Preuve : parcours avec la nouvelle vérification mais sans le correctif : `result=FAIL steps=145 failedChecks=48`, code 1 (chaque lancement charge son monde deux fois).
  - Avec le correctif : `CASAENGINE_DEMO_CYCLE=60` à `result=PASS steps=97 failedChecks=0 errors=0 defaultCameraWarnings=0`, code 0 ; « elevation separated pair: not colliding » journalisé.
  - Les deux solutions sans erreur ; `CasaEngine.Tests` 4149/4149.

### ✅ T5.2 — O6 et O7 : écran principal sur la pile de la vue, attente bornée

- Objectif : le parcours vérifie la présence réelle de l'écran principal et ne bloque jamais.
- Fichiers : `CasaEngine.Demos/DemoCycleAutomation.cs`, `CasaEngine.Demos/DemosGame.cs`, `docs/engine/demos-main-menu.md`.
- Étapes :
  1. O6 : au chargement d'un monde, la pile d'écrans de la vue active contient l'écran principal du jeu exactement une fois dans le monde menu, et aucun dans une démo (remplace le compteur d'appels `_mainMenuPushes`).
  2. O7 : si aucun monde ne se charge dans un nombre fixe d'images après une demande, le parcours échoue, journalise la cause et quitte avec le code 1.
- Validation : build des deux solutions, `CasaEngine.Tests` ; parcours à `result=PASS`, code 0 ; preuves par mutations temporaires (annulées ensuite) : écran principal créé sans être poussé → code 1 ; un `WorldLoaded` non transmis au parcours → code 1 au lieu d'un blocage.
- Commit : `test(demos): check the main screen stack and bound the world wait in the demo cycle`
- Note :
  - O6 : `OnWorldLoaded` reçoit l'écran principal du jeu (plus le compteur `_mainMenuPushes`, retiré) et compte les `MainMenuScreen` de la pile d'écrans (`UIRoot.ScreenStack`) de la vue active : exactement un, celui du jeu, dans le monde menu ; aucun dans une démo. La ligne d'étape journalise `main screens=`.
  - O7 : au-delà de 300 updates sans monde chargé après une demande, le parcours journalise « no world loaded 300 updates after step N », termine en échec et le jeu quitte avec le code 1. Doc `docs/engine/demos-main-menu.md` à jour.
  - Preuves par mutations temporaires (annulées ensuite), une seule exécution `CASAENGINE_DEMO_CYCLE=2` : écran principal retiré de la pile juste après l'avoir poussé → étapes menu « holds 0 main screen(s), none of them the one the game opened » ; `WorldLoaded` non transmis au parcours pour la démo 1 → « no world loaded 300 updates after step 3 », `result=FAIL`, code 1 en 7 s au lieu d'un blocage.
  - Sans mutation : `CASAENGINE_DEMO_CYCLE=60` à `result=PASS steps=97 failedChecks=0 errors=0 defaultCameraWarnings=0`, code 0 ; menus à `main screens=1`, démos à `main screens=0`.
  - Les deux solutions sans erreur ; `CasaEngine.Tests` 4149/4149 (projet de tests reconstruit).

### ⏳ T5.3 — O4 : commentaires périmés

- Objectif : plus de mention du navigateur ni d'un monde partagé dans les démos.
- Fichiers : `CasaEngine.Demos/Demos/UIOverlayDemo.cs`, `CasaEngine.Demos/Demos/TopDownElevationDemo.cs`, `ai-agent/README.md`.
- Validation : build ; `rg "navigator|share one world" CasaEngine.Demos --glob "*.cs"` ne trouve plus rien.
- Commit : `docs(demos): update stale demo comments`

### ⏳ T5.4 — O5 : tester `MainMenuScreen` lui-même

- Objectif : qu'une régression de `BuildTree`, `SelectDemo` ou du branchement des boutons fasse échouer un test.
- Prérequis : réponse de l'auteur (voir O5) : `CasaEngine.Tests` ne référence pas l'application des démos, et toute solution change la structure du projet de tests.

---

## Points ouverts

| Réf | Sujet | Tâche concernée |
|---|---|---|
| O1 | ~~Décor du monde menu~~ : tranché par D8 (fond uni sombre). | T2.3 |
| O2 | La navigation à la manette n'a jamais été vérifiée avec une vraie manette dans l'UI du moteur : à faire par l'auteur (T2.3 en 🧪). | T2.3, T4.1 |
| O3 | ✅ Corrigé en T5.1. Avis P3 du vérificateur : une demande de lancement qui arrive pendant l'image « Loading … » (double-clic ou bouton) survit et lance un second changement de démo une image après le premier (`DemosGame.cs:406-411` ne remet pas `_pendingDemoIndex` à zéro). Peu probable pour un joueur. Reporté. | T2.3 |
| O4 | Avis P3 : commentaire périmé dans `UIOverlayDemo.cs:29` (« MGUI demo navigator panel »), antérieur au chantier ; et `TopDownElevationDemo.cs:152` dit encore que les démos partagent un monde (P4). Reporté. | T2.2, T1.1 |
| O5 | Avis P3 : `DemoMainMenuTests` construit son arbre à la main et n'instancie pas `MainMenuScreen` (le projet de tests ne référence pas l'application des démos) ; une régression de `BuildTree`, `SelectDemo` ou du branchement des boutons ne ferait échouer aucun test. Reporté. | T2.1 |
| O6 | ✅ Corrigé en T5.2. Avis P4 : le contrôle « écran principal poussé une fois » du parcours compte les appels, pas la présence réelle de l'écran sur la pile de la vue. Reporté. | T2.3 |
| O7 | ✅ Corrigé en T5.2. Avis P4 : le parcours attend `WorldLoaded` sans limite ; un monde qui ne se chargerait jamais bloquerait l'automatisation au lieu de sortir avec le code 1. Reporté. | T1.1 |
| O8 | Avis P4 : le premier `Update` d'une démo lancée s'exécute avant le chargement de son monde ; aucune démo n'en souffre (lecture de chaque `Update`, parcours sans erreur). Reporté. | T1.1 |

## Hors périmètre

- Vignettes et grille de cartes (D3 retient la liste et la fiche).
- Mondes `.world` faits dans l'éditeur pour les démos (D2 retient le monde en code).
- Chargement asynchrone et vrai écran de chargement dans le moteur.
- Les index périmés de `docs/engine/render-stats-demo-workflow.md` (déjà faux avant ce chantier).
- L'API d'UI de fenêtre et de zone de layout du moteur (ADR-0070) reste telle quelle.
