# Plan agent IA — Navigateur de démos en deux parties

Plan d'exécution issu de l'analyse en lecture seule menée avec l'auteur le 2026-10-06 : le panneau « Demo Navigator » (300x440 en haut à droite) masque la scène et contient trop d'informations ; l'auteur propose une fenêtre en deux parties redimensionnables, navigateur à gauche et scène à droite.
Les décisions D1 → D8 ci-dessous ont été arbitrées avec l'auteur le 2026-10-06 (plan approuvé le même jour, exécution en mode AUTO) : **ce plan les applique, il ne les rediscute pas**.

Ce fichier doit être mis à jour pendant le travail : l'icône au début de chaque tâche indique son statut courant.

> **Quand écrire un plan** : dès que le travail demande plus d'un commit. En dessous, exécution directe avec le rapport de fin de tâche d'`AGENTS.md`.
> **Avant d'écrire le plan** : poser toutes les questions en une seule fois ; ne rien inventer, ne rien supposer.
> **Après approbation** : exécution autonome, tâche par tâche ; arrêt uniquement sur ⚠️ Blocked.

## Objectif

L'application `CasaEngine.Demos` présente ses démos dans une fenêtre en deux parties :

- **à gauche, le navigateur** : un arbre à deux niveaux (thèmes, puis démos) en haut, la description de la démo sélectionnée en bas, les deux séparés par un séparateur vertical redimensionnable ; thème Dark natif de MGUI ; repliable avec F1 ;
- **à droite, la scène** de la démo, rendue comme aujourd'hui dans le back-buffer, mais dans une zone qui exclut le navigateur ;
- **entre les deux**, une poignée qui règle la largeur du navigateur.

Pour cela, le moteur gagne deux ajouts sans rupture (ADR-0070) : une zone de layout dans laquelle se placent les vues du back-buffer, et une UI de fenêtre hors des vues, prise en compte par l'`InputRouter`. Côté démos : changement de démo différé, suivi du redimensionnement de la fenêtre, contrat de viewport de `PostDraw`, trois démos adaptées à la zone, clavier des démos neutralisé quand le pointeur est sur le navigateur (règle P9), automatisation inchangée (navigateur replié).

## État vérifié du dépôt (2026-10-06)

Arbre et branches :

- `main` = `19aa21fe` ; c'est un ancêtre de `chantier/e19-suite2` (`dd1f8afa`, deux commits devant : piste de coins `.anim2d`). Les deux enregistrent 30 démos (`git show main:CasaEngine.Demos/DemosGame.cs | grep -c "_demos.Add("` = 30).
- Modification préexistante de l'auteur dans son checkout (`chantier/e19-suite2`) : `CasaEngine.Launcher/Program.cs`. Le chantier travaille dans un worktree séparé et n'y touche pas.
- Worktree `.claude/worktrees/demo-browser` créé le 2026-10-06 sur `chantier/demo-browser` depuis `main` `19aa21fe`. Le dépôt du moteur est un sous-module : son `core.worktree` partagé (`../../../CasaEngineMonogame`) fait résoudre un worktree lié vers le gitdir ; comme pour les autres worktrees, un `config.worktree` propre au worktree fixe `core.worktree` sur son chemin. Les sous-modules `MGUI` et `NvgSharp` ne sont pas encore initialisés dans ce worktree.
- Prochain numéro d'ADR libre : 0070 (aucune branche locale n'a de `docs/decisions/007*`, vérifié par `git ls-tree` sur toutes les branches).

Navigateur actuel :

- `demo-info.xaml` : fenêtre 300x440, `ScreenHorizontalAlignment="Right"`, `ScreenVerticalAlignment="Top"`, `ScreenMargin="10"` (`CasaEngine.Demos/Content/Screens/demo-info.xaml:1-7`).
- `DemosGame.RefreshDemoUI` recrée `DemoInfoScreen` et `DemoHintOverlay` à chaque changement de démo (`CasaEngine.Demos/DemosGame.cs:204-222`) ; F1 bascule leur visibilité (`DemosGame.cs:254-260`) ; un clic dans la liste appelle `ChangeDemo` directement (`DemoInfoScreen.cs:69`, `DemosGame.cs:213`).
- `DemoInfoScreen.Escape` double les crochets (`DemoInfoScreen.cs:186-187`) alors que MGUI échappe `[` par `\` (`MGUI/MGUI.Core/UI/Text/FormattedTextTokenizer.cs:114`, `EscapeMarkdown` à la ligne 408) : d'où `[[Space]]` et `[/color]` affichés.
- Enregistrement : 30 appels `_demos.Add(...)` dans un ordre plat (`DemosGame.cs:72-110`). `CASAENGINE_START_DEMO` accepte un index ou un titre (`DemosGame.cs:115-147`) ; la doc utilise les index `6` et `9` (`docs/engine/render-stats-demo-workflow.md:30,66`).
- `Content/DemosGame.json` : 1024x768, `AllowUserResizing: true`, pas de résolution virtuelle.

Vues et rendu :

- `BackBufferSurface.Apply` règle le viewport sur son rectangle (`CasaEngine/Framework/Rendering/BackBufferSurface.cs:26`).
- `ViewManager.AutoLayoutMode` (`ViewManager.cs:49`) ; `ApplyBackBufferLayout(w, h)` répartit les vues back-buffer avec `SplitScreenLayout.Compute(w, h, count, mode)` sur toute la fenêtre (`ViewManager.cs:57-84`) ; `Clear()` ne remet pas `AutoLayoutMode` à null (`ViewManager.cs:224-237`).
- `SplitScreenLayout.Compute(int screenWidth, int screenHeight, int viewCount, SplitMode mode)` est la seule surcharge (`SplitScreenLayout.cs:30`).
- `DefaultRuntimeViewBootstrapper.CreateDefaultView` crée la vue par défaut sur `Rectangle(0, 0, w, h)` ou, avec une résolution virtuelle, l'ajuste par `VirtualResolutionLayout.Apply` (`CasaEngine/Framework/Application/DefaultRuntimeViewBootstrapper.cs`, méthode `CreateDefaultView`).
- `CasaEngineGame.OnScreenResized` : une seule vue back-buffer → `VirtualResolutionRuntime.ResizeSingleBackBufferView`, qui pose `Rectangle(0, 0, w, h)` sans résolution virtuelle (`VirtualResolutionRuntime.cs:16-27`) ; sinon `ApplyBackBufferLayout` si `AutoLayoutMode` ; sinon la caméra de la vue active ; puis `OnViewsResized` (`CasaEngineGame.cs:277-336`).
- Sans résolution virtuelle, un redimensionnement de la fenêtre par l'utilisateur ne relance pas le layout : `OnWindowClientSizeChanged` ne fait que réinitialiser le ciseau puis retourne (`CasaEngineGame.cs:343-359`).
- `Update` : `PreviewUpdate`, puis mise à jour des UI de vue avant le gameplay (`CasaEngineGame.cs` ~530-541). `Draw` : pipeline, puis `AfterRenderPipeline`, puis la phase 3 des composants (`CasaEngineGame.cs` ~641-662). Le pipeline remet le viewport sur tout le back-buffer à la fin (`RenderPipeline.cs:310`), donc `PostDraw` s'exécute aujourd'hui sur toute la fenêtre.
- L'UI d'une vue est composée dans `DefaultViewPipeline` avant `AfterRenderPipeline` (`DefaultViewPipeline.cs:50-55`).
- Une vue sans caméra n'est pas possible : le pipeline lit `view.Camera` sans test (`RenderPipeline.cs:165-167`). Le navigateur ne peut donc pas être une vue.
- `PhysicsDebugViewRendererComponent.Draw` est vide, le tracé physique passe par les vues (`PhysicsDebugViewRendererComponent.cs:76-78`).
- `Camera3dComponent.OnScreenResized` recalcule le FOV vertical = (π/4 × 1,7778) / aspect, borné à [0,1 ; π − 0,1] (`Camera3dComponent.cs:16, 47-57`) : 60° à 1024x768, environ 83° pour une scène de 744x768.

UI et entrée :

- `UIRoot(CasaEngineGame, IRenderSurface, EngineRuntimeContext)` crée un `ViewRenderHost` abonné à `PreviewUpdate` (`UIRoot.cs:77-91`, `ViewRenderHost.cs:37-38`) ; `IsKeyboardCaptured` = `Desktop.FocusedKeyboardHandler != null` (`UIRoot.cs:54`) ; `UIScale` n'est lu nulle part ailleurs que dans `UIRoot.cs:64` (`rg "\.UIScale|Metrics\.Scale" CasaEngine`).
- `IUIViewRuntime` expose `Update(GameTime)`, `Draw()`, `InputState`, `UpdateMetrics`, `PushScreen`, `RemoveScreen` (`CasaEngine/Framework/UI/IUIViewRuntime.cs:9-49`).
- `SyncUIViewMetrics(view)` calcule les métriques avec `view.UIScaler.ComputeMetrics` (`CasaEngineGame.cs:729-749`) ; `RenderView.UIScaler` vaut par défaut `new UIScaler(new Point(1920, 1080))` (`RenderView.cs:125`).
- `CasaEngineGame` crée toujours l'`InputRouter` (`CasaEngineGame.cs:435`). `IsMouseHandledByUI` et `IsKeyboardCapturedByUI` ne consultent que `view.UIView` (`InputRouter.cs:345-358`). `ScriptArcBallCamera.ResolveUiOwnership` s'en sert sur la vue active (`ScriptArcBallCamera.cs:157-176`).
- 12 lectures directes de `Keyboard.GetState()` dans 11 fichiers des démos, hors `DemosGame` : `PlayerComponent.cs:45`, `ViewManagerSandbox.cs:188`, `UIOverlayDemo.cs:118`, `EnvironmentShowcaseDemo.cs:76`, `MaterialDemo.cs:391`, `AudioDemo.cs:186` et `:326`, `AnimationIkDemo.cs:98`, `TileMapDemo.cs:143`, `AnimationBlendDemo.cs:124`, `CutsceneMoveToDemo.cs:69`, `CutsceneNavigateToDemo.cs:74` (`rg -n "Keyboard\.GetState" CasaEngine.Demos`).

MGUI :

- Balises XAML `TreeView` et `GridSplitter` (`MGUI/MGUI.Core/UI/XAML/XAMLParser.cs:48, 89`) ; `TextBlock.AllowsInlineFormatting` (`XAML/Controls.cs:3265`).
- `MGTreeView` : `SelectionChanged` (`MGTreeView.cs:306`), `ScrollIntoView` (`:819`) ; `NotifyItemSelected` ne fait rien si l'élément est déjà sélectionné (`:774-777`) ; Entrée et Espace plient ou déplient l'élément sélectionné et sont marqués traités (`:492-497`) ; `ItemDoubleClicked` n'est levé que pour un élément qui a des enfants (`MGTreeViewItem.cs:57-61`).
- Focus clavier (vérifié dans le `MGUI` du checkout principal, le sous-module n'étant pas encore initialisé dans le worktree) : à chaque `Desktop.Update`, `SanitizeKeyboardFocusState` retire le focus d'un élément qui ne peut plus recevoir le clavier (`MGDesktop.cs:1091-1101`, appelé à `:1565`), puis `QueueAutoFocusIfNeeded` donne le focus au premier élément focusable dès qu'il y a une activité clavier hors mode pointeur et que rien n'a le focus (`Navigation/UIFocusNavigationService.cs:298-309`, appelé à `MGDesktop.cs:1571`). `CanHandleKeyboardInput` vaut `IsFocusable` par défaut (`MGElement.cs:2816`) ; `IsFocusable` est public, et un élément focusable prend le focus quand on appuie dessus (`MGElement.cs:2788-2809`). Le setter de `FocusedKeyboardHandler` est privé (`MGDesktop.cs:1106-1109`) : la seule façon publique de retirer le focus est de rendre l'élément non focusable. `MGTreeView` (`MGTreeView.cs:342`) et `MGButton` (`MGButton.cs:178`) sont focusables à la construction. L'ancien panneau applique déjà ce mécanisme : boutons focusables seulement quand la souris est dans la fenêtre (`DemoInfoScreen.cs:77-80, 147-183`).
- Thème : `MGTheme.BuiltInTheme { Light_Gray, Dark_Blue, Dark }` (`MGUI/MGUI.Core/UI/MGTheme.cs:644-649`) ; l'éditeur applique `new MGTheme(MGTheme.BuiltInTheme.Dark, _desktop.DefaultFontFamily)` à `_desktop.Resources.DefaultTheme` (`CasaEngine.Editor/GameEditor.cs:1079-1081`) ; `MGDesktop.DefaultFontFamily` (`MGDesktop.cs:94`).

Démos qui supposent toute la fenêtre :

- `SplitScreenDemo.cs:110` (`SplitScreenLayout.Compute(pp.BackBufferWidth, pp.BackBufferHeight, 2, Vertical)`) ; `ViewManagerSandbox.cs:130` (`Grid4`), et `:216`, `:232` via `ApplyBackBufferLayout` ; `RenderToTextureDemo.cs:85` (caméra 1), `:102` (rectangle de la vue principale), `:133-134` (vignette) ; `UIOverlayDemo.cs:129` via `ApplyBackBufferLayout` ; `PsxFreeQuadDemo.cs:96, 134`.
- `BackBufferProbe` projette avec `device.Viewport.Project` (`BackBufferProbe.cs:67`) et lit tout le back-buffer (`:56`).

Automatisation et tests :

- `CASAENGINE_CAPTURE_SCREENSHOT_PATH` masque le panneau (`DemosGame.cs:51-54, 219-221`) ; `CASAENGINE_DEMO_PIXELS_PATH` (`BackBufferProbe.cs:21`) ; `CASAENGINE_PSXQUAD_DUMP_PATH` (`PsxFreeQuadDemo.cs:32`).
- `CasaEngine.Tests/UI/DemoScreenXamlTests.cs` charge en mode strict chaque `*.xaml` de `CasaEngine.Demos/Content/Screens` (`:19-32`) et épingle `demo-info.xaml` (`:80`, `:128`, `:152`) et `demo-hint.xaml` (`:126`).
- `CasaEngine.Tests` ne référence pas `CasaEngine.Demos` (`CasaEngine.Tests.csproj:31-35`). Tests moteur existants utiles : `Input/InputRouterTests.cs`, `Application/VirtualResolutionRuntimeTests.cs`, `Rendering/VirtualResolutionLayoutTests.cs`.
- `docs/engine/animation-blend-demo.md:14` documente F1.

## Décisions verrouillées

| Réf | Décision |
|---|---|
| D1 | Stratégie A : la scène reste rendue dans le back-buffer par ses vues `BackBufferSurface`, placées dans une zone de layout qui exclut le navigateur ; le navigateur est une UI MGUI hors du `ViewManager`. Les ajouts au moteur (zone de layout, UI de fenêtre prise en compte par l'`InputRouter`) sont acceptés, avec une ADR. |
| D2 | Une démo se charge au clic sur la démo dans l'arbre, ou par Entrée ; les flèches ne changent que la sélection et la description. |
| D3 | La taille de fenêtre par défaut ne change pas (1024x768, `Content/DemosGame.json`). |
| D4 | Les démos qui ont besoin de toute la fenêtre (Split-screen, ViewManager Sandbox, PSX free quads) replient le navigateur quand on les choisit. |
| D5 | Le conflit clavier entre l'arbre et les démos qui lisent le clavier directement est corrigé dans ce chantier. |
| D6 | Thèmes de l'arbre validés : Rendering, Animation, Physics, Scene, Cutscenes, 2D and tile maps, Views and cameras, UI, Audio, PSX rendering (voir O1 pour la fusion des thèmes à une seule démo). |
| D7 | Le navigateur utilise le thème Dark natif de MGUI (`MGTheme.BuiltInTheme.Dark`). |
| D8 | Les thèmes à une seule démo sont fusionnés (réponse à O1) : « Scene and views » réunit Scene management, Split-screen, ViewManager Sandbox et Render-to-texture ; Audio n'a pas d'hôte naturel et reste seul. Thèmes finaux : Rendering, Animation, Physics, Scene and views, Cutscenes, 2D and tile maps, UI, Audio, PSX rendering. |

## Points à valider (proposés par l'agent)

| Réf | Proposition |
|---|---|
| P1 | Largeur du navigateur : 280 px par défaut, 200 px au minimum. La scène garde au moins `ceil(0,89 × hauteur)` px de large (FOV vertical ≤ 90°, voir `Camera3dComponent.cs:47-57`) : à 1024x768 le navigateur va jusqu'à 340 px ; s'il ne reste pas 200 px, il se replie. Conséquence de D3 : navigateur ouvert à 280 px, le FOV vertical de la scène passe de 60° à environ 83° (la scène paraît plus petite) ; F1 rend toute la fenêtre. |
| P2 | Description : 180 px de haut par défaut, 80 au minimum ; l'arbre garde au moins 120 px et absorbe les changements de hauteur. |
| P3 | F1 garde son sens actuel (afficher ou cacher) appliqué au navigateur ; un bouton « ‹‹ » dans l'en-tête fait de même ; navigateur replié, le rappel existant (`demo-hint.xaml`) dit « Press F1 to show the demo browser ». Échap quitte toujours immédiatement (inchangé). |
| P4 | Automatisation : navigateur replié dès que `CASAENGINE_CAPTURE_SCREENSHOT_PATH`, `CASAENGINE_DEMO_PIXELS_PATH` ou `CASAENGINE_PSXQUAD_DUMP_PATH` est défini, ce qui reproduit l'image d'aujourd'hui ; nouvelle variable `CASAENGINE_DEMO_BROWSER=open|collapsed` pour forcer l'état (capture de validation du navigateur). |
| P5 | Thème et repli « pleine fenêtre » déclarés à l'enregistrement dans `DemosGame` (`AddDemo(demo, theme, collapsesBrowser)`), pas dans les 30 classes ; l'ordre plat et les index de `CASAENGINE_START_DEMO` restent identiques. |
| P6 | Zone de layout et résolution virtuelle ensemble ne sont pas pris en charge : des marges non nulles avec une résolution virtuelle active lèvent une `InvalidOperationException` (échec tôt, AGENTS §9.10), documenté dans l'ADR. |
| P7 | Pas de nouvelle référence de `CasaEngine.Tests` vers `CasaEngine.Demos` : tests moteur unitaires, tests XAML existants étendus, validation manuelle et automatisée des démos. |
| P8 | Le rappel F1 (`demo-hint.xaml`) garde son propre style ; seul le navigateur prend le thème Dark. |
| P9 | **Règle de propriété du clavier (applique D5)** : le navigateur possède le clavier si et seulement si il est ouvert, le jeu actif, et le pointeur dans son rectangle ; `DemosGame` le calcule au début de chaque image avec l'état brut de la souris (`DemosGame.BrowserOwnsKeyboard`). Conséquences : (a) les démos lisent un clavier vide exactement quand la règle est vraie (T4.4) ; (b) côté MGUI, le navigateur est « armé » (arbre et bouton « ‹‹ » focusables, arbre focalisé) quand la règle devient vraie et « désarmé » (`IsFocusable = false` sur ces éléments, collectés une fois au chargement) quand elle devient fausse : `SanitizeKeyboardFocusState` retire alors le focus à la mise à jour suivante et l'auto-focus ne trouve rien, comme dans l'ancien panneau ; (c) l'`InputRouter` reste générique (état de l'UI de fenêtre, T2.1) et suit la règle par le désarmement, avec au plus une image de retard. Après un chargement par clic ou par Entrée, les touches de la démo répondent dès que le pointeur est sur la scène. |

## Règles d'exécution pour l'agent

- **Branche dédiée `chantier/demo-browser`**, créée depuis `main` (`19aa21fe`), dans le worktree `.claude/worktrees/demo-browser`. Ne jamais committer sur `main` ni sur la branche de l'auteur.
- **Une seule tâche à la fois.** Avant de commencer une tâche, remplacer son icône `⏳` par `🚧`. À la fin, lancer la validation indiquée, remplacer l'icône par `✅`, `🧪` ou `⚠️`, ajouter une courte note de validation sous la tâche, puis **créer un commit dédié** qui inclut la mise à jour de ce fichier.
- **Un commit par tâche**, atomique et compilable, message en anglais au format `type(area): summary`. Le message suggéré est donné dans chaque tâche.
- **Ne jamais pousser.** Le merge sur `main` reste une décision humaine.
- **Ne rien inventer** : toute API, tout fichier, toute règle utilisée existe dans le dépôt, vient d'une réponse de l'auteur, ou d'une doc officielle citée (URL). Sinon : passer la tâche en ⚠️ Blocked, écrire la question dans « Points ouverts », et **s'arrêter**.
- **Build obligatoire** avant de passer une tâche en ✅ dès que du code est touché : `dotnet build CasaEngine.MonoGame.sln` et, dès que `CasaEngine/` change, `dotnet build CasaEngine.Editor.MonoGame.sln` (l'éditeur utilise `ViewManager` et `InputRouter`) ; **tests** `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj` dès qu'une tâche touche du code testé. Si le build est impossible, la tâche reste 🧪 avec la raison écrite.
- Si le code est écrit mais qu'une vérification visuelle ou manuelle manque, utiliser `🧪 Needs testing` et noter précisément ce qui manque.
- **Ne jamais laisser une tâche en 🚧** à la fin d'une session.
- **Ne jamais indexer** les modifications préexistantes de l'auteur : `git add` fichier par fichier, jamais `git add -A` ni `git add .`.
- **Langue** : ce plan en français ; code, messages de commit, docs de `docs/` et ADR en anglais.
- Rappel moteur : pas d'allocation, de LINQ ni de closure dans les chemins chauds (`Update`, `Draw`, layout, input) ; restaurer tout état GPU modifié (viewport) ; le runtime ne dépend pas de l'éditeur ; changements d'API publique additifs. MGUI n'est pas modifié par ce chantier.
- Ce chantier touche le moteur et l'entrée de plusieurs composants : vérification fraîche par un `verifier` avant de le déclarer fait (T5.3).

## Légende des statuts

- ⏳ Todo : pas encore commencé.
- 🚧 In progress : en cours de modification locale.
- 🧪 Needs testing : code écrit, validation incomplète ou en attente.
- ✅ Done : code validé, build/tests OK, commit effectué.
- ⚠️ Blocked : bloqué par une erreur non résolue ou une décision manquante.

## Validation globale

- `dotnet build CasaEngine.MonoGame.sln` et `dotnet build CasaEngine.Editor.MonoGame.sln` : 0 erreur.
- `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj` : tout vert ; le nombre de tests est celui de la référence T0.1 plus les tests ajoutés.
- Sondes automatisées : Split-screen et les six démos PSX lancées avec `CASAENGINE_START_DEMO`, `CASAENGINE_CAPTURE_SCREENSHOT_PATH` et `CASAENGINE_DEMO_PIXELS_PATH` donnent les mêmes relevés que la référence T0.1 ; les captures font 1024x768, sans navigateur.
- Smoke manuel à 1024x768 : navigateur à gauche en thème Dark, arbre par thèmes, description en bas ; clic et Entrée chargent, flèches ne chargent pas ; les deux séparateurs se déplacent ; F1 replie et rouvre ; la caméra ignore la souris et le clavier quand le pointeur est sur le navigateur ; règle P9 : touches d'une démo pressées avant tout contact avec le navigateur → la démo répond et l'arbre ne prend pas le focus ; après un chargement par clic puis par Entrée, pointeur ramené sur la scène → les touches de la démo répondent ; pointeur sur l'arbre → flèches et Espace sans effet sur la démo ; Split-screen, ViewManager Sandbox et PSX free quads replient le navigateur ; redimensionner la fenêtre garde la scène correcte.

---

## Phase 0 — Préparation

### ✅ T0.1 — Worktree, sous-modules, plan et référence

- Objectif : rendre le worktree buildable, committer le plan, mesurer la référence avant tout changement.
- Fichiers : `ai-agent/tasks/demo-browser-split-tasks.md`, `ai-agent/README.md`.
- Étapes :
  1. Initialiser `MGUI` et `NvgSharp` dans le worktree (`git submodule update --init MGUI NvgSharp`) ; si l'opération échoue ou exige une configuration partagée, ⚠️ Blocked et question (voir la note mémoire sur les gitdirs de sous-modules des worktrees).
  2. Builder les deux solutions ; lancer `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj` et noter le total.
  3. Référence des sondes : pour Split-screen et les six démos PSX, lancer `CasaEngine.Demos` avec `CASAENGINE_START_DEMO=<titre>`, `CASAENGINE_CAPTURE_SCREENSHOT_PATH` et `CASAENGINE_DEMO_PIXELS_PATH` (mode d'emploi en tête de `PsxSemiTransparencyDemo.cs:22`), garder relevés et captures dans le scratchpad (pas dans le dépôt).
  4. Committer le plan et sa ligne dans `ai-agent/README.md`.
- Validation : builds sans erreur ; total des tests et relevés des sept sondes notés sous la tâche.
- Commit : `docs(ai-agent): plan the split-window demo browser`
- Note de validation (2026-10-06) : sous-modules initialisés depuis les clones locaux du checkout principal (`git -c protocol.file.allow=always -c submodule.<n>.url=<chemin local> submodule update --init MGUI NvgSharp`, configuration partagée inchangée) : MGUI `18b2c14`, NvgSharp `9c0da03`. `CasaEngine.MonoGame.sln` et `CasaEngine.Editor.MonoGame.sln` : 0 erreur. `CasaEngine.Tests` : **4105/4105**. Sondes de référence (index 18 Split-screen, 24 à 29 PSX ; `CASAENGINE_START_DEMO=<index>` + capture + relevé, lancées depuis `CasaEngine.Demos`) : **7 PASS** (Split-screen 1 contrôle, PSX sprite 15, file de sprites 2, couches de fond 6, teinte mode 1 3, teinte mode 0 3, quads libres 3), captures 1024x768 ; captures, relevés, dump `29.quad.bin` et leurs empreintes SHA-256 gardés dans le scratchpad de la session (`probes/baseline`).

---

## Phase 1 — Moteur : zone de layout des vues

### ✅ T1.1 — Marges de layout du `ViewManager` et découpage dans une zone

- Objectif : les vues back-buffer réparties automatiquement se placent dans une zone réduite par des marges ; marges nulles = comportement identique.
- Fichiers : `CasaEngine/Framework/Rendering/ViewLayoutInsets.cs` (nouveau), `CasaEngine/Framework/Rendering/ViewManager.cs`, `CasaEngine/Framework/Rendering/SplitScreenLayout.cs`, `CasaEngine.Tests/Rendering/ViewLayoutAreaTests.cs` (nouveau).
- Étapes :
  1. `public readonly record struct ViewLayoutInsets(int Left, int Top, int Right, int Bottom)` avec `Zero`.
  2. `ViewManager.LayoutInsets` (défaut `Zero`) et `ViewManager.GetLayoutArea(int screenWidth, int screenHeight)` : le rectangle de la fenêtre moins les marges, largeur et hauteur ramenées à 1 au minimum.
  3. Surcharge `SplitScreenLayout.Compute(Rectangle area, int viewCount, SplitMode mode)` ; l'ancienne surcharge appelle la nouvelle avec `(0, 0, w, h)` et rend exactement les mêmes rectangles.
  4. `ApplyBackBufferLayout(w, h)` découpe `GetLayoutArea(w, h)`.
- Validation : tests ajoutés (égalité des deux surcharges pour 1 à 4 vues et chaque `SplitMode` ; décalage d'une zone ; `GetLayoutArea` avec `Zero` et avec des marges trop grandes ; `ApplyBackBufferLayout` avec marges) ; deux solutions buildées ; suite verte.
- Commit : `feat(rendering): view layout insets confine back-buffer views to an area`
- Note de validation (2026-10-06) : `ViewLayoutInsets`, `ViewManager.LayoutInsets` / `GetLayoutArea`, surcharge `SplitScreenLayout.Compute(Rectangle, …)` (l'ancienne l'appelle avec `(0, 0, w, h)`), `ApplyBackBufferLayout` sur la zone. `ViewLayoutAreaTests` : 31 cas (égalité des deux surcharges et décalage pour 1 à 4 vues × 3 modes, zone avec et sans marges, marges trop grandes ou négatives, `ApplyBackBufferLayout` avec et sans marge). Deux solutions : 0 erreur. `CasaEngine.Tests` : 4136/4136.

### ✅ T1.2 — Vue unique et vue par défaut dans la zone

- Objectif : la vue par défaut et la vue unique redimensionnée suivent la zone ; refus explicite avec une résolution virtuelle (P6).
- Fichiers : `CasaEngine/Framework/Application/VirtualResolutionRuntime.cs`, `CasaEngine/Framework/Application/DefaultRuntimeViewBootstrapper.cs`, `CasaEngine/Framework/Application/CasaEngineGame.cs`, `CasaEngine.Tests/Application/VirtualResolutionRuntimeTests.cs` (et le fichier de tests du bootstrapper s'il existe : `rg -l CreateDefaultView CasaEngine.Tests`).
- Étapes :
  1. `ResizeSingleBackBufferView` et `CreateDefaultView` (internes) reçoivent la zone : sans résolution virtuelle, rectangle = zone et caméra redimensionnée à la taille de la zone.
  2. Avec une résolution virtuelle et des marges non nulles : `InvalidOperationException` dont le message cite ADR-0070 ; sans marges, chemin inchangé.
  3. `CasaEngineGame.OnScreenResized` et le bootstrapper passent `GameManager.ViewManager.GetLayoutArea(w, h)` / `viewManager.GetLayoutArea(w, h)`.
- Validation : tests (zone avec et sans marges, exception avec résolution virtuelle, cas existants inchangés) ; deux solutions buildées ; suite verte.
- Commit : `feat(rendering): the single and default back-buffer views follow the layout area`
- Note de validation (2026-10-06) : `ResizeSingleBackBufferView` gagne une surcharge avec la zone (l'ancienne l'appelle avec la fenêtre entière) ; `CreateDefaultView` prend `viewManager.GetLayoutArea` et ne redimensionne la caméra que si la zone n'est pas la fenêtre (comportement sans marge inchangé) ; `ThrowIfLayoutAreaIsNotTheWindow` refuse marges + résolution virtuelle (P6) ; `CasaEngineGame.OnScreenResized` passe la zone. 5 tests ajoutés à `VirtualResolutionRuntimeTests`. Deux solutions : 0 erreur ; `CasaEngine.Tests` 4141/4141. Sondes : 7 PASS, relevés identiques à la référence ; captures et dump identiques octet pour octet sauf Split-screen, dont les deux tableaux de statistiques par vue (FPS, temps : coin haut-gauche de chaque vue) changent à chaque lancement — critère retenu pour cette démo : relevé identique.

---

## Phase 2 — Moteur : UI de fenêtre

### ✅ T2.1 — UI de fenêtre hors des vues, arbitrage de l'entrée

- Objectif : un jeu peut installer une UI qui n'appartient à aucune vue ; elle est mise à jour avec les UI de vue, dessinée par-dessus tout, et l'`InputRouter` la consulte.
- Fichiers : `CasaEngine/Framework/Application/CasaEngineGame.cs`, `CasaEngine/Framework/Input/InputRouter.cs`, `CasaEngine.Tests/Input/InputRouterTests.cs`.
- Étapes :
  1. `CasaEngineGame` : `WindowUI` (lecture), `SetWindowUI(IUIViewRuntime ui, IRenderSurface surface)`, `ClearWindowUI()` ; le jeu ne dispose pas l'UI (l'appelant la possède, doc XML).
  2. `Update` : juste après la boucle des UI de vue, synchroniser ses métriques comme `SyncUIViewMetrics` (`CasaEngineGame.cs:729-749`) à partir du rectangle de la surface, avec un `UIScaler` par défaut gardé en champ (même valeur que `RenderView.cs:125`, sans allocation par image), puis `WindowUI.Update(gameTime)`.
  3. `Draw` : après la phase 3 des composants (fin du bloc de dessin), `surface.Apply(GraphicsDevice)`, `WindowUI.Draw()`, puis viewport remis sur tout le back-buffer.
  4. `InputRouter.WindowUI` (posé par `SetWindowUI` / `ClearWindowUI`) ; `IsMouseHandledByUI(view)` et `IsKeyboardCapturedByUI(view)` renvoient aussi vrai quand l'`InputState` de l'UI de fenêtre a le pointeur ou le clavier. Le reste du routeur ne change pas. Règle générique du moteur : c'est au jeu de garder le focus de son UI de fenêtre cohérent (pour les démos, désarmement de P9, T4.1) ; à écrire dans la doc XML et l'ADR.
- Validation : tests du routeur (UI de fenêtre absente : résultats inchangés ; pointeur ou clavier pris par l'UI de fenêtre : vrai même si l'UI de vue dit faux) avec un faux `IUIViewRuntime` (réutiliser celui des tests s'il existe) ; deux solutions buildées ; suite verte.
- Commit : `feat(ui): a window-level UI outside the views, honoured by the input router`
- Note de validation (2026-10-06) : `CasaEngineGame.WindowUI`, `SetWindowUI(ui, surface)`, `ClearWindowUI()` ; mise à jour juste après la boucle des UI de vue avec ses métriques (`UIScaler` 1920x1080 gardé en champ, comme `RenderView.cs:125`) ; dessin après la phase 3 (`surface.Apply`, `Draw`, viewport remis sur tout le back-buffer). `InputRouter.WindowUI` lu par `IsMouseHandledByUI` et `IsKeyboardCapturedByUI` (propriétés `IsPointerOverUI` / `IsKeyboardCaptured` lues directement). 4 tests ajoutés à `InputRouterTests`. Deux solutions : 0 erreur, avertissements inchangés (134 / 194) ; `CasaEngine.Tests` 4145/4145. Le dessin réel de l'UI de fenêtre se vérifie en T4.1 (aucun test sans `GraphicsDevice`).

### ✅ T2.2 — ADR-0070 et documentation moteur

- Objectif : enregistrer la décision (zone de layout, UI de fenêtre, refus avec résolution virtuelle).
- Fichiers : `docs/decisions/0070-view-layout-area-and-window-ui.md`, `docs/decisions/README.md`, le document de `docs/engine/` qui décrit `ViewManager` (`rg -l ViewManager docs/engine`).
- Étapes :
  1. Revérifier le numéro sur toutes les branches locales (`git ls-tree <b> docs/decisions/`) ; si 0070 est pris, prendre le suivant et corriger ce plan.
  2. Écrire l'ADR avec le skill `adr` et le modèle `docs/decisions/template.md` ; source : ce plan et la conversation du 2026-10-06.
- Validation : liens de l'index vérifiés.
- Commit : `docs(adr): ADR-0070 view layout area and window-level UI`
- Note de validation (2026-10-06) : numéro revérifié sur toutes les branches locales (aucun `007*` ; ADR-0069 n'existe que sur `chantier/e19-suite2`, pas sur `main`). ADR écrite avec le skill `adr`, ligne ajoutée à l'index. Aucun document de `docs/engine/` ne décrit `ViewManager` (`rg -l ViewManager docs/engine` : `render-stats-demo-workflow.md` et `screen-effects.md` ne font que le citer) : rien d'autre à mettre à jour ici, la doc d'usage vient en T5.1 (`demos-browser.md`).

---

## Phase 3 — Démos : base

### 🧪 T3.1 — Changement de démo différé, redimensionnement, layout automatique remis à zéro

- Objectif : aucun changement de démo depuis un callback d'UI ; la scène suit la fenêtre ; une démo multi-vues ne laisse pas son `AutoLayoutMode` à la suivante.
- Fichiers : `CasaEngine.Demos/DemosGame.cs`, `CasaEngine.Demos/Demos/DemoUI/DemoInfoScreen.cs` (callback seulement, l'écran est retiré en T4.3).
- Étapes :
  1. `_pendingDemoIndex` (-1 = aucun), consommé au début de `DemosGame.Update` ; les callbacks ne font que le poser.
  2. `ChangeDemo` remet `GameManager.ViewManager.AutoLayoutMode` à null avant de vider les vues.
  3. Abonnement à `Window.ClientSizeChanged` dans `DemosGame` : sans résolution virtuelle et avec des bornes non nulles, `OnScreenResized(bounds.Width, bounds.Height)` (même lecture des bornes que `CasaEngineGame.cs:352-359`).
- Validation : build ; manuel : redimensionner la fenêtre sur Material et sur Split-screen ; passer de Split-screen à Material (une seule vue plein cadre) ; changer de démo par l'ancien panneau.
- Commit : `fix(demos): defer demo changes, follow window resizes, reset the auto layout`
- Note de validation (2026-10-06) : `_pendingDemoIndex` posé par `RequestDemo` (callback de l'ancien panneau) et consommé au début de `DemosGame.Update` ; `AutoLayoutMode = null` dans `ChangeDemo` avant `InitializeCamera` de la démo suivante (Split-screen et Sandbox le reposent dans leur `InitializeCamera`) ; `Window.ClientSizeChanged` → `OnScreenResized(bounds)` sans résolution virtuelle. Build : 0 erreur. Redimensionnement vérifié sans toucher aux entrées de l'auteur (script `resize-capture.ps1` du scratchpad : lancement, `MoveWindow` à 1296x839, capture par la démo) : Material remplit 1280x800, Split-screen passe à deux vues de 640x800. **Reste à la main (T5.2)** : changer de démo par l'ancien panneau, et Split-screen → Material (une seule vue plein cadre).

### ✅ T3.2 — Contrat de viewport de `PostDraw` et démos placées dans la zone

- Objectif : `PostDraw` dessine dans la zone de la scène ; les démos qui posaient leurs vues sur toute la fenêtre utilisent la zone.
- Fichiers : `CasaEngine.Demos/DemosGame.cs`, `CasaEngine.Demos/Demo.cs` (doc XML de `PostDraw`), `CasaEngine.Demos/Demos/SplitScreenDemo.cs`, `CasaEngine.Demos/Demos/ViewManagerSandbox.cs`, `CasaEngine.Demos/Demos/RenderToTextureDemo.cs`.
- Étapes :
  1. `AfterRenderPipeline` : viewport = `GetLayoutArea(pp.BackBufferWidth, pp.BackBufferHeight)` avant `PostDraw`, viewport précédent restauré après.
  2. `SplitScreenDemo.cs:110` et `ViewManagerSandbox.cs:130` : `SplitScreenLayout.Compute(viewManager.GetLayoutArea(...), …)`.
  3. `RenderToTextureDemo` : caméra 1 et vue principale sur la zone (`:85`, `:102`), vignette calculée sur `GraphicsDevice.Viewport` (`:133-134`), surcharge `OnScreenResized` qui rend à la caméra 2 sa taille de cible (`_camera2.OnScreenResized(RtSize, RtSize)`).
- Validation : build ; marges encore nulles, donc image identique : sonde Split-screen égale à la référence T0.1 ; les trois démos lancées à la main.
- Commit : `refactor(demos): demos lay their views out in the scene area`
- Note de validation (2026-10-06) : `AfterRenderPipeline` pose le viewport sur `GetLayoutArea` autour de `PostDraw` puis restaure le précédent ; doc XML de `Demo.PostDraw` ; Split-screen et Sandbox découpent `GetLayoutArea` ; Render-to-texture pose caméra et vue principale sur la zone, place la vignette depuis `GraphicsDevice.Viewport` et rend à la caméra RT sa taille (`OnScreenResized`). Build : 0 erreur. Marges nulles : 7 sondes PASS, relevés identiques, captures identiques sauf les statistiques de Split-screen. Après agrandissement à 1280x800 (script `resize-capture.ps1`) : Render-to-texture plein cadre, vignette carrée en bas à droite ; Sandbox en 4 vues de 640x400. Constat préexistant, hors périmètre : la vue 3 de Sandbox (mode `OnDemand`) garde son ancienne image après un redimensionnement, une vue `OnDemand` dans le back-buffer ne se redessine pas à chaque image.

---

## Phase 4 — Démos : navigateur

### 🧪 T4.1 — Coquille du navigateur : UI de fenêtre, thème Dark, poignée, F1

- Objectif : le navigateur existe à gauche, vide, en thème Dark ; la poignée règle sa largeur ; F1 le replie et le rouvre.
- Fichiers : `CasaEngine.Demos/Content/Screens/demo-browser.xaml` (nouveau), `CasaEngine.Demos/Demos/DemoUI/DemoBrowserScreen.cs` (nouveau), `CasaEngine.Demos/DemosGame.cs`, `CasaEngine.Tests/UI/DemoScreenXamlTests.cs`.
- Étapes :
  1. Prototype d'abord : vérifier qu'un glissement commencé sur la poignée continue quand la souris sort du bureau du navigateur. Sinon, repli : `DemosGame` suit le glissement avec l'état brut de la souris (appui dans le rectangle de la poignée, suivi jusqu'au relâchement).
  2. `demo-browser.xaml` : fenêtre étirée (`Stretch`), sans barre de titre ni fond imposé ; `Grid` à deux colonnes (contenu, poignée de 6 px) et cinq rangées (en-tête avec bouton « ‹‹ », arbre `treeDemos`, `GridSplitter`, description `lblDescription` en `AllowsInlineFormatting="False"`, ligne d'aide) ; tailles P1/P2.
  3. `DemosGame` crée une fois une `BackBufferSurface` à gauche, un `UIRoot` dessus avec `Desktop.Resources.DefaultTheme = new MGTheme(MGTheme.BuiltInTheme.Dark, Desktop.DefaultFontFamily)` (comme `GameEditor.cs:1079-1081`), y pousse `DemoBrowserScreen`, puis `SetWindowUI` ; marge gauche du `ViewManager` = largeur ; relayout (`OnScreenResized`) seulement quand la largeur change.
  4. F1 et « ‹‹ » : repli (`ClearWindowUI`, marges nulles, relayout) et réouverture à la largeur précédente ; bornes et repli automatique de P1.
  5. Règle P9 : `DemosGame.BrowserOwnsKeyboard` calculé au début de `DemosGame.Update` (rectangle du navigateur contre la position brute de la souris, sans allocation) ; à chaque changement, `DemoBrowserScreen.SetKeyboardArmed(bool)` : armé = `IsFocusable = true` sur les éléments focusables collectés au chargement (arbre, bouton « ‹‹ ») et `treeDemos.Focus(KeyboardFocusSource.Pointer)` ; désarmé = `IsFocusable = false` sur ces mêmes éléments. Désarmé au chargement.
  6. Prototype de P9, avant de continuer : (i) touches d'une démo pressées sans jamais toucher le navigateur → `WindowUI.IsKeyboardCaptured` reste faux ; (ii) pointeur sur le navigateur puis sur la scène → `IsKeyboardCaptured` redevient faux à l'image suivante. Si MGUI garde le focus malgré le désarmement, ⚠️ Blocked : la seule autre voie publique serait une API de MGUI, hors périmètre (question à l'auteur).
- Validation : build ; `DemoScreenXamlTests` vert (le nouveau XAML est chargé en strict) avec un test des noms et du placement étiré ; prototype P9 (étape 6) noté sous la tâche ; manuel : thème Dark, scène à droite au bon aspect, poignée, F1, molette et orbite sans effet au-dessus du navigateur, redimensionnement de la fenêtre.
- Commit : `feat(demos): split-window demo browser shell with the MGUI dark theme`
- Note de validation (2026-10-06) :
  - Code : `demo-browser.xaml` (fenêtre étirée, `Grid` `*,6` × `Auto,*[120,],6,180[80,],Auto`, `TreeView`, `GridSplitter`, description en `AllowsInlineFormatting="False"`, poignée `bdrSceneHandle`) ; `DemoBrowserScreen` (bouton « << », `SetKeyboardArmed`) ; `DemosGame` crée une fois une `BackBufferSurface` et un `UIRoot` au thème `MGTheme.BuiltInTheme.Dark`, pose les marges (`ApplyBrowserLayout`, bornes P1, repli s'il ne reste pas 200 px), `SetWindowUI` / `ClearWindowUI`, F1 et « << » différés à la mise à jour suivante, règle P9 (`BrowserOwnsKeyboard`) et glissement de la poignée depuis l'état brut de la souris, `Dispose` du `UIRoot`.
  - Écart assumé à l'étape 1 : le prototype d'un glissement MGUI hors du bureau ne peut pas se vérifier sans simuler la souris de l'auteur ; le repli (glissement suivi par `DemosGame` avec l'état brut de la souris) est retenu d'emblée.
  - Avancé depuis T5.1 pour pouvoir capturer le navigateur : P4 complet (`ResolveInitialBrowserOpen` : replié sous `CASAENGINE_CAPTURE_SCREENSHOT_PATH`, `CASAENGINE_DEMO_PIXELS_PATH`, `CASAENGINE_PSXQUAD_DUMP_PATH`, surchargé par `CASAENGINE_DEMO_BROWSER=open|collapsed`, valeur inconnue signalée). Transitoire jusqu'à T4.3 : l'ancien panneau reste affiché dans la scène (hors automatisation) pour naviguer tant que l'arbre est vide ; le rappel F1 est masqué.
  - Prototype P9 (étape 6) automatisé sans toucher aux entrées de l'auteur : `DemoBrowserFocusTests` (3 tests sur le vrai `demo-browser.xaml`, bureau sans affichage) : armé, une touche fait prendre le focus par MGUI (la menace existe) ; désarmé, les touches ne focalisent rien ; désarmer un navigateur focalisé retire le focus dès la mise à jour suivante. Plus un test des noms et un cas de placement étiré dans `DemoScreenXamlTests`.
  - Deux solutions : 0 erreur ; `CasaEngine.Tests` 4151/4151. Sondes en automatisation : 7 PASS, relevés identiques à la référence. Capture Material avec `CASAENGINE_DEMO_BROWSER=open` : navigateur de 280 px en thème Dark, scène à droite au bon aspect. Navigateur forcé ouvert : sonde Split-screen PASS (vue décalée en 652) ; sonde du sprite PSX FAIL sur 2 points attendus (scène en pixels 1:1 centrée sur la fenêtre, environ 140 px rognés de chaque côté, voir O4).
  - **Reste à la main (T5.2)** : glisser la poignée (bornes 200 et 340 px à 1024x768), F1 et « << », molette et orbite sans effet au-dessus du navigateur, règle P9 en vrai.

### ⏳ T4.2 — Arbre par thèmes, description, chargement au clic ou à Entrée

- Objectif : l'arbre range les 30 démos par thème (D6) ; sélection = description ; clic ou Entrée = chargement (D2) ; repli pour les démos pleine fenêtre (D4).
- Fichiers : `CasaEngine.Demos/DemosGame.cs`, `CasaEngine.Demos/Demos/DemoUI/DemoBrowserScreen.cs`.
- Étapes :
  1. `AddDemo(Demo demo, string theme, bool collapsesBrowser = false)` remplace les 30 `_demos.Add` dans le même ordre (P5) ; `collapsesBrowser` pour Split-screen, ViewManager Sandbox et PSX free quads. Répartition : Rendering (Static model, Material system, Environment showcase, Static shadow validation, Particle system), Animation (Skinned mesh, Animation blend, Animation IK, Skeletal animation blending), Physics (Collision 3d, Collision 2d, Top down elevation), Scene and views (Scene management, Split-screen, ViewManager Sandbox, Render-to-texture), Cutscenes (MoveTo, NavigateTo), 2D and tile maps (Tile map, Tile map 3d, Tile map surface screen), UI (MGUI UI Overlay, World-space UI), Audio (Audio demo), PSX rendering (les six démos PSX) ; D8.
  2. Arbre construit une fois : un élément par thème, une feuille par démo dont l'en-tête est un `MGTextBlock` créé en code (`AllowsInlineFormatting = false`), démo courante mise en évidence ; abonnements faits une seule fois.
  3. `SelectionChanged` : description (titre, thème, `Description`). Clic sur l'en-tête d'une feuille : pose `_pendingDemoIndex` (pas `SelectionChanged`, qui ne se relève pas sur un élément déjà sélectionné). Entrée : quand `BrowserOwnsKeyboard` (P9) est vrai et que la sélection est une feuille, `DemosGame` lit le front d'Entrée et pose l'index.
  4. Après un chargement : démo courante mise en évidence, son thème déplié, `ScrollIntoView` ; démo `collapsesBrowser` : repli.
- Validation : build ; suite verte ; manuel : clic charge, flèches ne chargent pas, Entrée charge, Split-screen replie, F1 rouvre, `CASAENGINE_START_DEMO=6` lance la même démo qu'avant.
- Commit : `feat(demos): demo tree by theme with a description pane`

### ⏳ T4.3 — Retrait de l'ancien panneau, rappel F1, tests XAML

- Objectif : `DemoInfoScreen` disparaît (et avec lui le bug des crochets) ; le rappel F1 parle du navigateur.
- Fichiers : `CasaEngine.Demos/Demos/DemoUI/DemoInfoScreen.cs` (supprimé), `CasaEngine.Demos/Content/Screens/demo-info.xaml` (supprimé), `CasaEngine.Demos/Content/Screens/demo-hint.xaml`, `CasaEngine.Demos/Demos/DemoUI/DemoHintOverlay.cs` (doc), `CasaEngine.Demos/DemosGame.cs`, `CasaEngine.Tests/UI/DemoScreenXamlTests.cs`.
- Étapes :
  1. Supprimer l'écran et son XAML ; `RefreshDemoUI` ne pousse plus que le rappel, visible quand le navigateur est replié.
  2. Texte du rappel : « Press F1 to show the demo browser » (P3, P8).
  3. Tests : retirer les cas de `demo-info.xaml` (`:74-86`, `:128`, `:145-157` s'ils en dépendent), garder `demo-hint.xaml`, ajouter ce qui manque pour `demo-browser.xaml`.
- Validation : build ; suite verte ; manuel : rappel visible navigateur replié.
- Commit : `refactor(demos): the demo browser replaces the demo info panel`

### ⏳ T4.4 — Clavier des démos neutralisé quand le navigateur possède le clavier (D5, P9)

- Objectif : les flèches, Espace et Entrée tapés dans l'arbre ne pilotent plus la démo.
- Fichiers : `CasaEngine.Demos/DemoKeyboard.cs` (nouveau) et les 11 fichiers listés dans l'état vérifié.
- Étapes :
  1. `internal static KeyboardState DemoKeyboard.Read(CasaEngineGame game)` : clavier vide si `game` est null ou inactif, ou si `game is DemosGame { BrowserOwnsKeyboard: true }` (règle P9, la même que celle qui arme le navigateur) ; sinon `Keyboard.GetState()`. Pas d'allocation.
  2. Remplacer les 12 lectures directes (mécanique, délégable à un `mech-executor`) ; `EnvironmentShowcaseDemo.cs:76` gagne ainsi le test d'activité des autres démos (changement noté).
- Validation : build ; manuel, sur Audio, Animation blend et le joueur de Tile map : touches pressées avant tout contact avec le navigateur → la démo répond ; pointeur sur l'arbre → flèches et Espace sans effet sur la démo ; démo chargée par clic puis par Entrée, pointeur ramené sur la scène → les touches de la démo répondent.
- Commit : `fix(demos): demos ignore the keyboard while the demo browser has focus`

---

## Phase 5 — Automatisation, documentation, validation

### ⏳ T5.1 — Automatisation et documentation

- Objectif : l'automatisation produit la même image qu'avant ; le navigateur est documenté.
- Fichiers : `CasaEngine.Demos/DemosGame.cs`, `docs/engine/animation-blend-demo.md`, `docs/engine/demos-browser.md` (nouveau), `docs/README.md`.
- Étapes :
  1. Navigateur replié (UI de fenêtre non installée, marges nulles) sous les trois variables de P4 ; `CASAENGINE_DEMO_BROWSER=open|collapsed` les surcharge.
  2. `animation-blend-demo.md:14` : F1 affiche ou cache le navigateur. Nouveau `demos-browser.md` : disposition, touches, thèmes, variables, limites (FOV, résolution virtuelle) ; index `docs/README.md`.
- Validation : build ; sondes de Split-screen et des six démos PSX égales à la référence T0.1 ; capture avec `CASAENGINE_DEMO_BROWSER=open` gardée dans le scratchpad pour le coup d'œil de l'auteur.
- Commit : `docs(demos): document the demo browser and its automation switches`

### ⏳ T5.2 — Validation globale

- Objectif : dérouler la validation globale sur les 30 démos.
- Fichiers : ce plan (notes).
- Étapes :
  1. Deux builds, suite de tests, sondes.
  2. Passage manuel sur les 30 démos (chargement, redimensionnement, séparateurs, F1, clavier, souris, HUD propres dans la scène).
- Validation : notes sous la tâche ; 🧪 tant que l'auteur n'a pas regardé (thème, FOV à 1024x768 : O3).
- Commit : `docs(ai-agent): record the demo browser validation`

### ⏳ T5.3 — Vérification fraîche

- Objectif : un `verifier` frais confirme ou réfute l'objectif sur le diff complet du chantier.
- Fichiers : ce plan (verdict et dispositions).
- Étapes : transmettre l'objectif, les décisions, la validation globale et le diff `main..chantier/demo-browser` ; disposer chaque constat (corriger, reporter, rejeter avec preuve).
- Validation : verdict CONFIRMED, ou constats disposés.
- Commit : `docs(ai-agent): record the demo browser verification`

---

## Points ouverts

À trancher pendant l'exécution, ou à remonter en ⚠️ Blocked si la réponse manque.

| Réf | Sujet | Tâche concernée |
|---|---|---|
| O1 | ~~Fusion des thèmes à une seule démo~~ : **tranché le 2026-10-06 par l'auteur (« on fusionne »), voir D8.** | T4.2 |
| O2 | ~~Glissement de la poignée hors du bureau du navigateur~~ : **tranché en T4.1**, le repli (glissement suivi par `DemosGame` avec l'état brut de la souris) est retenu d'emblée ; à vérifier à la main en T5.2. | T4.1 |
| O3 | FOV de la scène navigateur ouvert à 1024x768 (60° → environ 83°) : conséquence acceptée de D3, à constater par l'auteur. | T5.2 |
| O4 | Navigateur ouvert, les scènes PSX en pixels 1:1 (centrées sur la fenêtre) sont rognées d'environ 140 px de chaque côté dans une zone de 744 px, et leur sonde échoue si on la lance navigateur ouvert (constaté en T4.1, sprite PSX : premier sprite sous le navigateur). En automatisation le navigateur est replié (P4), donc rien ne change pour les sondes. Question à l'auteur : replier aussi le navigateur pour les cinq autres démos PSX (D4 n'en cite qu'une), ou garder le rognage (F1 rend toute la fenêtre) ? | T4.2, T5.2 |

## Hors périmètre

- Le défaut de `BackBufferProbe` à la seconde visite d'une démo PSX (vérifications ajoutées à chaque `Initialize`, `_done` jamais remis à zéro) : tâche séparée.
- Changer la règle de FOV de `Camera3dComponent`.
- Zone de layout combinée à une résolution virtuelle (refusée, P6).
- Recherche ou filtre dans l'arbre, mémorisation des largeurs entre deux lancements, touches démo précédente/suivante, navigation à la manette dédiée.
- Toute modification de MGUI.
