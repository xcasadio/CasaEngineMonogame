# Plan agent IA — Démos lisibles par un nouvel utilisateur

Plan d'exécution du chantier issu de l'analyse de `CasaEngine.Demos` faite en session le 2026-11-14 (pas de fichier d'audit : les constats sont repris dans « État vérifié du dépôt »).
Les décisions D1 → D11 ci-dessous ont été arbitrées avec l'auteur le 2026-11-14 : **ce plan les applique, il ne les rediscute pas**. Les points P1 → P7 sont des arbitrages proposés par l'agent, validés par l'auteur à l'approbation du plan le 2026-11-14 (« Approuvé, lance-le », mode AUTO).

Ce fichier doit être mis à jour pendant le travail : l'icône au début de chaque tâche indique son statut courant.

## Objectif

Faire de `CasaEngine.Demos` le point d'entrée d'un nouvel utilisateur : chaque démo dit ce qu'elle montre, quoi essayer et où lire la suite, dans le menu et pendant la démo ; un parcours « Getting started » ouvre la liste ; une démo charge un monde fait dans l'éditeur ; les démos de base deviennent interactives et commentées ; les fonctionnalités documentées sans démo en reçoivent une.

## État vérifié du dépôt (2026-11-14)

- 25 démos enregistrées dans 8 thèmes, dans l'ordre historique ; l'index 0 est « Cutscene MoveTo » (`CasaEngine.Demos/DemosGame.cs:104-133`). `CASAENGINE_START_DEMO` accepte un index ou un titre (`DemosGame.cs:167-197`).
- Le contrat `Demo` n'expose que `Title` et `Description` (`CasaEngine.Demos/Demo.cs:12-15`). La fiche du menu affiche titre, thème, description (`Content/Screens/main-menu.xaml:22-24`, `MainMenuScreen.Entry` à `Demos/DemoUI/MainMenuScreen.cs:25`).
- En démo, l'overlay commun ne contient que « Esc / Select: back to the menu » (`Content/Screens/demo-hint.xaml`, `DemosGame.cs:331-349`) ; il est caché pendant une capture ou une sonde.
- Texte affiché en démo (`rg -c "DrawText\(|DrawString\(|TextBlock|\.Text ="`) : seulement Audio, Animation blend, Animation IK, ViewManager Sandbox, UI Overlay (HUD), plus le panneau de Skeletal blending. Touches sans rappel à l'écran : Environment (B/E/L), Material (T), Cutscene MoveTo et NavigateTo (Espace/R/S), Tile map (1/2).
- Aucune démo ne lit la manette (`rg -c GamePad Demos` : 0). `DemosGame.cs` ne lit que Échap, Entrée, Back, A (`DemosGame.cs:368-376`).
- Contradictions : `Demos/SkinnedMeshDemo.README.md` annonce linear blend contre dual quaternion, le code fait trois colonnes d'ombres en `LinearBlend` (`SkinnedMeshDemo.cs:106-108`, `:181`) ; la description de Scene management dit « added and removed at runtime », `SceneManagementDemo.cs` ne fait que déplacer ; `README.md` racine nomme le projet « DemosGame » et pointe vers `docs/rendering/`, dossier absent (`ls docs/rendering`).
- Descriptions orientées mainteneur : `FrozenFrameDemo.cs:70` (« E19.TR-4a… »), `MaterialDemo.cs:46-49`, « Validates… » dans les deux cutscenes, Split-screen, Static shadow validation.
- Démos sans commentaire : CutsceneMoveTo, CutsceneNavigateTo, EnvironmentShowcase, ParticleSystem, SceneManagement, StaticShadowValidation. Plus grosses démos : Audio 1197 lignes, AnimationBlend 1152, Material 823.
- Aucune démo ne charge un `.world` (`rg "\.world" CasaEngine.Demos --glob "*.cs"` : 0). `Content/DemosGame.json:8` nomme `DefaultWorld.world` ; `GameManager.SetWorldToLoad(string)` et `SetWorldToLoad(World)` existent (`CasaEngine/Framework/Application/GameManager.cs:384`, `:389`).
- Aucune référence dans `CasaEngine.Demos/**/*.cs` à : `SaveGame`, `Possess`, `PlayerInput`, `Coroutine`, `ScrollingLayer`, `Cellular`, `EffectQuad`, `BitmapFont`, `RayCast` ; ces mots existent dans `CasaEngine/` et les docs `docs/engine/save-games.md`, `player-input.md`, `gameplay-possession.md`, `coroutines_specifications.md`, `scrolling-layers.md`, `cellular-layers.md` existent.
- `Collision2dBasicDemo.cs:91` porte un `//TODO` ; les deux démos de collision n'ont aucune entrée.
- Index numériques utilisés dans la doc : `docs/engine/render-stats-demo-workflow.md:30` (`'6'`) et `:66` (`'9'`). Les autres docs passent un titre.
- Tests existants : `CasaEngine.Tests/UI/DemoMainMenuTests.cs`, `CasaEngine.Tests/UI/DemoScreenXamlTests.cs` (chaque écran livré chargé en mode strict). Automatisation : `CASAENGINE_DEMO_CYCLE` (`DemoCycleAutomation.cs`, `docs/engine/demos-main-menu.md`).
- Entrées du moteur : façade `PlayerInput` par joueur et `InputMappingManager` (`docs/engine/player-input.md`) ; des fichiers `.buttonsMapping` existent dans `Projects/PixelUiSample/ui-input.buttonsMapping` et `Projects/RPGDemo/buttonsMapping.buttonsMapping`, aucun dans `CasaEngine.Demos` (`fd -e buttonsMapping`).
- Non vérifié : aucune démo n'a été lancée pendant l'analyse.
- Chantier voisin : `ai-agent/tasks/unreal-viewport-camera-tasks.md` (approuvé le 2026-11-14) modifie `ScriptArcBallCamera`, la caméra par défaut des démos, et réserve des touches. Sa branche `chantier/unreal-viewport-camera` existe en local (tête `2d473456`, `git branch --list`) et n'est pas fusionnée ; elle déplace le fondu d'`AudioDemo` de F à U. Voir D6.
- Sauvegarde : `SaveGameFileStorage` écrit par défaut sous `LocalApplicationData` (`CasaEngine/Framework/SaveGames/SaveGameFileStorage.cs:67`) et a un constructeur public `SaveGameFileStorage(string folderPath)` (`:73`). Le service s'obtient par `GameSettings.SaveGames` (`docs/engine/save-games.md`). Non vérifié : comment brancher un stockage d'un autre dossier sur le service.
- Arbre de travail de l'auteur au 2026-11-14 (branche `chore/mcp-codegraph-serena`), à ne jamais indexer : `.serena/project.yml`, `MGUI`, `ai-agent/README.md` (ligne du plan caméra), `.codegraph/`, `.serena/memories/`, `Projects/SampleProject/.casaeditor/viewport.editor.json`, `ai-agent/tasks/unreal-viewport-camera-tasks.md`.

## Décisions verrouillées

| Réf | Décision |
|---|---|
| D1 | Périmètre : les quatre lots sont dans le plan — aide et fiches, parcours débutant, monde de l'éditeur, interactivité et manques. |
| D2 | L'ordre d'enregistrement des démos peut changer : les index de `CASAENGINE_START_DEMO` changent, les titres restent stables. La doc qui passe un index passe un titre. |
| D3 | Aide en démo : un panneau MGUI commun, affiché au lancement (titre, contrôles, à observer), repliable par une touche, caché pendant les captures et les sondes. |
| D4 | Flux éditeur → jeu : un `.world` dans `CasaEngine.Demos/Content/`, chargé par une démo du menu ; la fiche dit comment l'ouvrir dans l'éditeur. |
| D5 | Le contrat `Demo` s'enrichit de façon additive (membres virtuels avec valeur par défaut) ; aucune démo existante ne casse à la compilation. Source : principe « changements additifs » d'`AGENTS.md` §9.8, rappelé à l'auteur dans l'analyse. |
| D6 | Ce plan **n'attend pas la fusion du chantier caméra**. Les contrôles de la caméra par défaut et les touches réservées sont lus sur la branche `chantier/unreal-viewport-camera` (`git show chantier/unreal-viewport-camera:<chemin>`), qui fait foi pour les fiches et le panneau d'aide. |
| D7 | Le `.world` de la démo éditeur est créé par l'agent, par l'automatisation de l'éditeur. |
| D8 | En fin de chantier, toutes les démos lisent leurs entrées par la configuration d'entrées du moteur, pour que chaque action ait sa correspondance clavier et manette ; le panneau d'aide et la fiche affichent les deux. Bouton `Start` pour le panneau d'aide. |
| D9 | La démo de sauvegarde est dans le plan : elle écrit son fichier dans le dossier de l'exécutable lancé (`AppContext.BaseDirectory`, le dossier `bin/…` du build), pas dans le dossier par utilisateur ni dans le répertoire courant du processus. Remplace la réponse précédente (« pas de vrai fichier »). |
| D10 | La branche `chantier/demos-onboarding` part de `chantier/unreal-viewport-camera`, pas de `main` : exception à `AGENTS.md` §4 décidée par l'auteur le 2026-11-14. Ce chantier ne fusionne dans `main` qu'après le chantier caméra. |
| D11 | Le monde de la démo éditeur est une copie de `Projects/SampleProject/DefaultWorld.world` et de ses assets dans `CasaEngine.Demos/Content/` (réponse à O1 du 2026-11-14, « copie sampleproject »). Remplace D7. À garder en mémoire : donner plus tard à l'éditeur le moyen de créer ce monde (commandes d'automatisation), dans un chantier à part. |

## Points à valider (arbitrages de l'agent)

| Réf | Proposition |
|---|---|
| P1 | Touche du panneau d'aide : `F1` au clavier (`Start` à la manette : D8). `F1` n'est lue par aucune démo listée par `rg -o "Keys\.\w+" Demos` ; à revérifier en T1.3. |
| P2 | Thème « Getting started », premier de la liste, trois démos neuves : « Hello world » (une entité, un mesh, une caméra, une lumière), « Moving an entity » (entrée clavier et manette qui déplace une entité), « Loading a model » (un `.glb` de `Content/` avec son material). |
| P3 | Thème « Engine validation », dernier de la liste : Frozen frame, Static shadow validation, ViewManager v2 Sandbox. Split-screen reste dans « Scene and views » avec une description réécrite. |
| P4 | Nouvelles démos de la phase 5 : input et possession ; coroutines ; sauvegarde de partie (D9) ; couches 2D (scrolling layers et cellular layers dans une même démo). Effect quads, bitmap fonts : hors plan. |
| P5 | Scene management : corriger la description (le code reste tel quel), pas d'ajout ni de retrait d'entités. |
| P6 | Les textes des fiches et du panneau sont en anglais, comme les descriptions actuelles ; les `.md` à côté des démos passent en anglais dans `docs/engine/`. |
| P7 | Allègement des grosses démos (T4.4) : extraction du HUD et de la validation dans des fichiers voisins, sans changement de comportement ; Audio, AnimationBlend, Material seulement. |

## Règles d'exécution pour l'agent

- **Branche dédiée `chantier/demos-onboarding`**, créée depuis `chantier/unreal-viewport-camera` (D10). Si cette branche avance pendant le chantier, le signaler à l'auteur : ne pas rebaser ni fusionner sans demande. Ne jamais committer sur `main`.
- **Une seule tâche à la fois.** Avant de commencer une tâche, remplacer son icône `⏳` par `🚧`. À la fin, lancer la validation indiquée, remplacer l'icône par `✅`, `🧪` ou `⚠️`, ajouter une courte note de validation sous la tâche, puis **créer un commit dédié** qui inclut la mise à jour de ce fichier.
- **Un commit par tâche**, atomique et compilable, message en anglais au format `type(area): summary`. Le message suggéré est donné dans chaque tâche.
- **Ne jamais pousser.** Le merge sur `main` reste une décision humaine.
- **Ne rien inventer** : toute API, tout fichier, toute règle utilisée existe dans le dépôt, vient d'une réponse de l'auteur, ou d'une doc officielle citée (URL). Sinon : passer la tâche en ⚠️ Blocked, écrire la question dans « Points ouverts », et **s'arrêter**. Les tâches de nouvelles démos commencent par lire la doc et le code de la fonctionnalité : aucune API n'est supposée par ce plan.
- **Build obligatoire** avant de passer une tâche en ✅ dès que du code est touché (`dotnet build CasaEngine.MonoGame.sln`) ; **tests** `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj` dès qu'une tâche touche du code testé. Si le build est impossible, la tâche reste 🧪 avec la raison écrite.
- Toute tâche qui touche une démo la lance au moins une fois (`CASAENGINE_START_DEMO=<titre>` depuis `CasaEngine.Demos/`). Si le code est écrit mais qu'une vérification visuelle ou manuelle manque, utiliser `🧪 Needs testing` et noter précisément ce qui manque.
- **Ne jamais laisser une tâche en 🚧** à la fin d'une session.
- **Ne jamais indexer** les modifications préexistantes de l'auteur : `git add` fichier par fichier, jamais `git add -A` ni `git add .`.
- **Langue** : ce plan en français ; code, messages de commit, docs de `docs/` et ADR en anglais.
- Rappel moteur : pas d'allocation, de LINQ ni de closure dans les chemins chauds, y compris dans `Update` et `PostDraw` des démos, qui servent d'exemple ; restaurer tout état GPU modifié ; le runtime ne dépend pas de l'éditeur. Ce chantier ne modifie que `CasaEngine.Demos`, `CasaEngine.Tests`, `docs/` et `README.md` : toute modification nécessaire dans `CasaEngine/` passe par ⚠️ Blocked.

## Légende des statuts

- ⏳ Todo : pas encore commencé.
- 🚧 In progress : en cours de modification locale.
- 🧪 Needs testing : code écrit, validation incomplète ou en attente.
- ✅ Done : code validé, build/tests OK, commit effectué.
- ⚠️ Blocked : bloqué par une erreur non résolue ou une décision manquante.

## Validation globale

- `dotnet build CasaEngine.MonoGame.sln` : 0 erreur.
- `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj` : suite verte.
- Depuis `CasaEngine.Demos/`, `CASAENGINE_DEMO_CYCLE=60` : code de sortie 0, toutes les démos, nouvelles comprises.
- Après T6.1, `rg "Keyboard\.GetState|GamePad\.GetState" CasaEngine.Demos` ne renvoie que les exceptions notées dans T6.1.
- Smoke manuel par l'auteur : chaque action d'une démo répond au clavier et à la manette ; le menu s'ouvre sur « Hello world » ; chaque fiche liste contrôles et « à observer » ; dans chaque démo le panneau d'aide s'affiche, se replie et correspond aux touches réelles.

---

## Phase 0 — Cadre

### ✅ T0.1 — ADR du contrat de démo et du parcours

- Objectif : enregistrer D2 à D5 et les points P validés (fiche enrichie, panneau d'aide, thèmes « Getting started » et « Engine validation », index non stables).
- Fichiers : `docs/decisions/<numéro libre>-demos-onboarding-contract.md`, `docs/decisions/README.md`.
- Étapes :
  1. Prendre le premier numéro libre de `docs/decisions/` au moment de l'exécution (ADR-0089 est prise par le chantier caméra, non fusionné : vérifier aussi `git ls-tree chantier/unreal-viewport-camera docs/decisions/`).
  2. Rédiger selon `docs/decisions/template.md` (skill `adr`), en citant ADR-0072.
- Validation : l'ADR est dans l'index ; aucun lien cassé.
- Commit : `docs(decisions): record the demo sheet contract and onboarding path`
- Note de validation (2026-11-14) : ADR-0090 écrite (`docs/decisions/0090-demos-carry-a-sheet-and-follow-a-learning-path.md`), ajoutée à l'index ; ADR-0089 est celle du chantier caméra.

---

## Phase 1 — Aide et fiches

### ✅ T1.1 — Contrat `Demo` enrichi

- Objectif : `Demo` porte, en plus de `Title` et `Description`, la liste des contrôles, les points « à observer » et le chemin de la doc ; valeurs par défaut vides (D5).
- Fichiers : `CasaEngine.Demos/Demo.cs`, un fichier voisin pour le type d'une ligne de contrôle (entrée, effet).
- Étapes :
  1. Ajouter les membres virtuels et le type de ligne de contrôle ; données statiques, aucune allocation par image.
  2. Doc XML courte en anglais.
- Validation : build ; les 25 démos compilent sans modification.
- Commit : `feat(demos): add controls, highlights and doc link to the Demo contract`
- Note de validation (2026-11-14) : `Demo` porte `Controls`, `Highlights` et `DocumentationPath` (valeurs par défaut vides), type `DemoControl` (clavier, effet, manette) ; `dotnet build CasaEngine.MonoGame.sln` : 0 erreur, aucune démo modifiée.

### ✅ T1.2 — Fiche du menu

- Objectif : la fiche affiche contrôles, « à observer » et chemin de la doc de la démo sélectionnée.
- Fichiers : `CasaEngine.Demos/Content/Screens/main-menu.xaml`, `Demos/DemoUI/MainMenuScreen.cs`, `DemosGame.cs` (construction des `Entry`), `CasaEngine.Tests/UI/DemoScreenXamlTests.cs`, `DemoMainMenuTests.cs`.
- Étapes :
  1. Étendre `MainMenuScreen.Entry` et le XAML ; une section vide ne s'affiche pas.
  2. Mettre à jour `TheMainMenu_DeclaresWhatItsScreenLooksUp` et ajouter un test du remplissage de la fiche.
- Validation : build, tests ; capture du menu par `CASAENGINE_CAPTURE_SCREENSHOT_PATH`.
- Commit : `feat(demos): show controls and highlights on the main screen sheet`
- Note de validation (2026-11-14) : fiche étendue (`lblControls`, `lblHighlights`, `lblDocumentation` et leurs titres, repliés quand la démo n'a rien), texte formaté une fois par `DemoSheetText` ; build 0 erreur ; tests `MainMenu`, `DemoSheet`, `DemoScreenXaml` 56/56 ; capture du menu : sections vides invisibles, mise en page inchangée.

### ✅ T1.3 — Panneau d'aide en démo

- Objectif : un panneau MGUI commun remplace le rappel « Esc / Select » : titre, contrôles (dont retour au menu et caméra par défaut), « à observer » ; affiché au lancement, replié par la touche de P1 (D3).
- Fichiers : `Content/Screens/demo-help.xaml` (remplace `demo-hint.xaml`), `Demos/DemoUI/DemoHelpOverlay.cs` (remplace `DemoHintOverlay.cs`), `DemosGame.cs`, `DemoCycleAutomation.cs` si ses contrôles nomment l'ancien écran, `CasaEngine.Tests/UI/DemoScreenXamlTests.cs`.
- Étapes :
  1. Vérifier par `rg` qu'aucune démo ne lit la touche de P1.
  2. Lire les contrôles réels de la caméra par défaut sur la branche `chantier/unreal-viewport-camera` (D6 : `ScriptArcBallCamera.cs` et `docs/editor/viewport-camera-controls.md` de cette branche) et les lister pour les démos qui gardent `Demo.CreateCamera`.
  3. Construire le texte une fois au chargement de la démo ; le repli ne fait que changer la visibilité.
  4. Garder la règle actuelle : caché pendant une capture ou une sonde (`UpdateDemoHintVisibility`).
  5. Vérifier que le panneau ne recouvre pas les HUD existants (Audio, Animation blend, Animation IK, ViewManager Sandbox, UI Overlay, Skeletal blending) ; sinon le replier d'office pour ces démos et le dire dans la note.
- Validation : build, tests ; `CASAENGINE_DEMO_CYCLE=60` à 0 ; une capture référence d'une démo par automatisation identique à avant (panneau caché).
- Commit : `feat(demos): replace the back hint with a collapsible help panel`
- Note de validation (2026-11-14) : panneau `demo-help.xaml` / `DemoHelpOverlay` en bas à droite (titre, contrôles, caméra par défaut si la démo garde `ScriptArcBallCamera`, à observer), visible au lancement, replié par F1 ou Start. Écart au plan : `demo-hint.xaml` est gardé comme ligne de rappel (« F1 / Start: help, Esc / Select: back to the menu », 440 px) au lieu d'être remplacé. Contrôles de caméra lus dans `docs/editor/viewport-camera-controls.md`. Aucune démo ne lit F1 ni Start (`rg`). Les HUD existants sont en haut à gauche ou à droite : pas de recouvrement vu sur Collision 3d et Audio (captures avec la nouvelle variable `CASAENGINE_DEMO_SHOW_HELP`, qui garde le panneau dans une capture). Build 0 erreur ; tests du menu et des écrans 58/58 ; `CASAENGINE_DEMO_CYCLE=60` : PASS, 101 étapes, code 0. Constat hors tâche : dans le cycle, la seconde visite de Frozen frame journalise `FAIL D6c` (CaptureCount 4 au lieu de 2) ; la démo seule donne `result=PASS`. Non corrigé.

### ✅ T1.4 — Fiches des 25 démos

- Objectif : chaque démo a une description en trois temps (ce que ça montre, quoi essayer, quelles classes regarder), ses contrôles vérifiés dans son `Update`, ses points « à observer » et son lien doc quand un fichier de `docs/engine/` existe. La colonne manette des contrôles est remplie en T6.1.
- Fichiers : les 25 `CasaEngine.Demos/Demos/*Demo.cs` et `ViewManagerSandbox.cs`.
- Étapes :
  1. Pour chaque démo, relever les touches par lecture du code, jamais depuis l'ancienne description.
  2. Écarter toute touche réservée par la caméra de la branche `chantier/unreal-viewport-camera` (D6) ; le fondu d'`AudioDemo` est sur U (D10).
  3. Retirer les identifiants internes (« E19.TR-4a », « T2.2 ») et le vocabulaire « Validates ».
  4. Appliquer P5 à Scene management.
- Validation : build ; `CASAENGINE_DEMO_CYCLE=60` à 0 ; relecture par l'auteur d'au moins une fiche par thème (🧪 tant que non faite).
- Commit : `docs(demos): rewrite every demo sheet for a first-time user`
- Note de validation (2026-11-14) : les 25 démos ont une description réécrite, leurs contrôles relevés dans leur `Update` (et dans `PlayerComponent` pour Tile map), leurs points à observer et leur page de doc quand elle existe ; identifiants internes et « Validates » retirés ; Scene management décrit ce que fait le code (P5). Affirmations vérifiées dans le code une à une, cinq corrigées après relecture. Build 0 erreur ; `CASAENGINE_DEMO_CYCLE=30` : PASS, 101 étapes ; capture d'Animation IK avec le panneau rempli. Colonne manette vide jusqu'à T6.1. Recette de l'auteur acceptée le 2026-11-14 (« recette OK »).

### ✅ T1.5 — Docs contradictoires et README racine

- Objectif : plus aucun texte ne contredit le code.
- Fichiers : `CasaEngine.Demos/Demos/SkinnedMeshDemo.README.md`, `EnvironmentShowcaseDemo.README.md`, `CutsceneMoveToDemo.md`, `CutsceneNavigateToDemo.md` (déplacés en anglais dans `docs/engine/` selon P6), `docs/README.md`, `README.md`.
- Étapes :
  1. Réécrire la doc de Skinned mesh d'après `SkinnedMeshDemo.cs`.
  2. README racine : projet `CasaEngine.Demos`, liens `docs/engine/…`, renvoi vers `docs/engine/demos-main-menu.md`.
- Validation : tous les liens modifiés pointent vers un fichier existant (`fd`).
- Commit : `docs(demos): fix demo notes and README entries that contradict the code`
- Note de validation (2026-11-14) : les quatre `.md` posés à côté des démos sont remplacés par `docs/engine/demos-check-notes.md` (anglais, indexé dans `docs/README.md`) ; la note de Skinned mesh décrit les trois colonnes d'ombres du code, en linear blend ; README racine : projet `CasaEngine.Demos`, liens `docs/engine/…` vérifiés par `ls`. Aucun code touché.

---

## Phase 2 — Parcours débutant

### ✅ T2.1 — Thèmes et ordre d'enregistrement

- Objectif : « Getting started » en tête, « Engine validation » en fin (P2, P3) ; l'ordre d'enregistrement suit l'ordre du menu (D2).
- Fichiers : `CasaEngine.Demos/DemosGame.cs`, `docs/engine/demos-main-menu.md`, `docs/engine/render-stats-demo-workflow.md` (index `'6'` et `'9'` remplacés par des titres).
- Étapes :
  1. Ajouter les deux thèmes à `ThemeOrder`, déplacer les démos de P3, réordonner les `AddDemo`.
  2. Retirer le commentaire historique de `DemosGame.cs:113-117`.
  3. `rg "START_DEMO\W+[0-9]"` hors `ai-agent/` : plus aucun index numérique dans `docs/`.
- Validation : build, tests ; `CASAENGINE_DEMO_CYCLE=60` à 0 ; chaque titre cité dans `docs/` démarre la bonne démo (journal).
- Commit : `refactor(demos): order the demos as a learning path`
- Note de validation (2026-11-14) : thèmes « Getting started » (vide jusqu'à T2.2, donc absent de l'arbre) et « Engine validation » (Static shadow validation, ViewManager v2 Sandbox, Frozen frame) ; `AddDemo` dans l'ordre du menu ; commentaire historique retiré. `docs/engine/demos-main-menu.md` et `render-stats-demo-workflow.md` nomment les démos par leur titre : `rg "START_DEMOW+[0-9]" docs` vide. Les sept titres cités dans `docs/` démarrent leur démo ; « PSX sprite semi-transparency » (`docs/engine/sprite-psx-semi-transparency.md:92`) ne correspond à aucune démo enregistrée, déjà avant ce chantier : non corrigé. Build 0 erreur ; cycle PASS.

### ✅ T2.2 — Démo « Hello world »

- Objectif : la plus petite scène possible, chaque ligne commentée : entité, composant racine, mesh primitif, material, lumière, caméra.
- Fichiers : `CasaEngine.Demos/Demos/HelloWorldDemo.cs`, `DemosGame.cs`, `docs/engine/demos-main-menu.md`.
- Étapes : partir des API déjà utilisées par `Collision3dBasicDemo.cs` et `StaticModelDemo.cs` ; enregistrer à l'index 0.
- Validation : build ; lancement par titre ; capture ; cycle à 0.
- Commit : `feat(demos): add the Hello world demo`
- Note de validation (2026-11-14) : `HelloWorldDemo` (thème « Getting started », index 0, le menu s'ouvre dessus) : une entité cube, un material, une lumière directionnelle et la caméra écrites en clair et commentées ; le cube tourne (captures à 1,2 s et 2 s différentes). La lumière vient du côté de la caméra : sans autre lumière, une face non éclairée est noire, et régler `AmbientColor` sur le material seul ou sur le monde seul n'avait rien changé à l'écran. Élucidé le 2026-11-14 : le terme ambiant est le produit des deux (`Lighting.fxh:53-55`, `ComputeAmbientTerm`), `LitDiffuseMaterial.AmbientColor` vaut zéro par défaut et l'ambiante par défaut du monde vaut 0.05 (`EnvironmentResolver.LegacyAmbientColor`) ; chaque essai laissait un facteur nul ou presque. La démo règle maintenant les deux et le commente ; capture avec l'ancienne lumière : la face auparavant noire est brun foncé. Build 0 erreur ; cycle PASS avec 26 démos.

### ✅ T2.3 — Démo « Moving an entity »

- Objectif : déplacer une entité au clavier et à la manette, avec le pas de temps, commentée.
- Fichiers : `CasaEngine.Demos/Demos/MovingAnEntityDemo.cs`, `DemosGame.cs`, doc.
- Étapes : lire le clavier par `DemoKeyboard.Read` ; lire `docs/engine/player-input.md` pour ne pas doublonner la démo d'input de T5.1 (ici : lecture directe, la plus simple) ; la manette arrive avec T6.1.
- Validation : build ; lancement ; cycle à 0.
- Commit : `feat(demos): add the Moving an entity demo`
- Note de validation (2026-11-14) : `MovingAnEntityDemo` (thème « Getting started ») : flèches, Maj gauche pour courir, Origine pour recentrer ; boucle lecture de l'entrée, direction normalisée, vitesse × temps écoulé, position bornée au sol, cube orienté dans le sens du déplacement ; lecture par `DemoKeyboard.Read`, manette en T6.1. Build 0 erreur ; capture : sol et cube affichés ; cycle PASS avec 27 démos. L'automatisation n'appuie sur aucune touche ; recette de l'auteur acceptée le 2026-11-14 (« recette OK »).

### ✅ T2.4 — Démo « Loading a model »

- Objectif : charger un modèle de `Content/` par le chemin d'assets du moteur, lui donner un material, expliquer `AssetCatalog` / `AssetContentManager` tels qu'utilisés par les démos existantes.
- Fichiers : `CasaEngine.Demos/Demos/LoadingAModelDemo.cs`, `DemosGame.cs`, doc.
- Étapes : reprendre le chemin de chargement de `SkinnedMeshDemo.cs` ou `SoldierLocomotionModelFactory.cs` après lecture ; aucun nouvel asset si un `.glb` existant convient.
- Validation : build ; lancement ; capture ; cycle à 0.
- Commit : `feat(demos): add the Loading a model demo`
- Note de validation (2026-11-14) : `LoadingAModelDemo` (thème « Getting started ») : charge `SkinnedMesh/kid_idle.model` par `AssetContentManager.LoadFromFile`, comme `SkinnedMeshDemo`, l'affiche par un `SkinnedMeshComponent` et joue son premier clip ; l'en-tête explique la chaîne `.glb` → `.model` → `AssetInfos.json`. Aucun nouvel asset. Écart : `Content/` ne contient pas de modèle statique en fichier, la démo charge donc un modèle animé. Build 0 erreur ; capture : personnage texturé affiché ; cycle PASS avec 28 démos.

---

## Phase 3 — Monde de l'éditeur

### ✅ T3.1 — Chemin de chargement d'un `.world` dans une démo

- Objectif : établir, par lecture du code, comment une démo du menu peut charger un `.world` de `Content/` à la place du monde neuf construit en code (ADR-0072), et ce que `DemoCycleAutomation` en vérifie.
- Sources : `DemosGame.cs` (changement de monde), `GameManager.SetWorldToLoad`, `Content/DefaultWorld.world`, `Content/DemosGame.json`, `docs/engine/demos-main-menu.md`.
- Étapes : écrire le résultat sous cette tâche ; si le chargement demande une modification de `CasaEngine/` ou contredit ADR-0072, passer en ⚠️ Blocked.
- Validation : note écrite, avec fichier:ligne.
- Commit : `docs(ai-agent): record how a demo can load an editor-made world`
- Note de validation (2026-11-14) : Runtime : `GameManager.UpdateWorld` charge un monde nommé par `AssetContentManager.LoadCopy<World>(assetInfo.Id)` après `AssetCatalog.GetByFileName` (`GameManager.cs:109-131`) ; une démo peut faire la même copie et la donner à `SetWorldToLoad(World)` (`:389`). `DemosGame.ChangeDemo` crée aujourd'hui `new World()` avant `Demo.Initialize` (`DemosGame.cs`, `ChangeDemo`) : il faut un point d'extension additif dans `Demo` (par exemple `CreateWorld`, par défaut `new World()`), dans `CasaEngine.Demos` seulement, aucune modification de `CasaEngine/`. Le monde doit être dans `Content/AssetInfos.json` ; `Content/DefaultWorld.world` existe et est vide (`entity_references: []`). `DemoCycleAutomation.OnWorldLoaded` contrôle que la caméra créée par la démo est dans une vue : la démo garde donc `CreateCamera`, ou le contrôle est adapté. Éditeur : `EditorAutomationOptions` n'a aucune commande qui crée un monde, une entité ou un composant (`--project`, `--entity` et `--component` sélectionnent, `--save-project` enregistre le monde ouvert, le reste concerne particules, materials, écrans, docking) : l'automatisation ne peut pas créer le monde demandé par D7. Voir O1.

### 🧪 T3.2 — Démo « World made in the editor »

- Objectif : une démo charge un `.world` de `CasaEngine.Demos/Content/` ; sa fiche et sa doc disent comment l'ouvrir et le modifier dans l'éditeur (D4).
- Fichiers : `CasaEngine.Demos/Content/<monde>.world` et ses assets, `CasaEngine.Demos/Demos/EditorWorldDemo.cs`, `DemosGame.cs`, `docs/engine/demos-main-menu.md`, une page `docs/editor/`.
- Étapes : dépend de T3.1. L'agent crée le monde par l'automatisation de l'éditeur (D7) : lire d'abord `docs/editor/` et le code d'automatisation de `CasaEngine.Editor` pour établir ce qu'elle permet ; si elle ne permet pas de créer et d'enregistrer un monde, passer en ⚠️ Blocked. Le `.world` n'est jamais écrit à la main.
- Validation : build ; lancement ; cycle à 0 ; l'auteur ouvre le monde dans l'éditeur, le modifie, relance la démo et voit le changement (🧪 tant que non fait).
- Commit : `feat(demos): add a demo that loads a world made in the editor`
- Note de validation (2026-11-14) : D11 : `Content/EditorWorld/` contient la copie de `Projects/SampleProject/DefaultWorld.world` (renommé `EditorWorld.world`, contenu inchangé) et des sept assets qu'il nomme (fermeture calculée par identifiants : `Box.entity`, `Box.staticModel`, `camera.entity`, `ground_ground1.staticModel`, `NewLitDiffuseMaterial.material`, `checkboard.texture`, `checkboard.png`) ; huit entrées ajoutées à `Content/AssetInfos.json`, aucun identifiant en commun avec le catalogue des démos. `Demo.CreateWorld` (additif, `new World()` par défaut) ; `EditorWorldDemo` charge le monde par `AssetCatalog.GetByFileName` + `LoadCopy<World>` et ne crée rien. Constat : la caméra enregistrée dans ce monde est sous le sol ; la démo la recadre dans `InitializeCamera` et lui donne `ScriptArcBallCamera` (`DemosGame` passe la caméra de la vue active quand la démo n'en crée pas). Doc : section « A world made in the editor » de `docs/engine/demos-main-menu.md`, pas de page `docs/editor/` séparée. Build 0 erreur ; capture : sol, boîtes et lumières ; cycle PASS avec 29 démos. Reste à faire par l'auteur : ouvrir `Content/DemosGame.json` dans l'éditeur, modifier le monde, relancer la démo (non vérifié par l'agent).


---

## Phase 4 — Interactivité et lisibilité

### ✅ T4.1 — Collision 3d interactive

- Objectif : tirer une boîte depuis la caméra et désigner un corps par un rayon, avec debug draw ; code commenté (rôle du collider enfant, masse du sol).
- Fichiers : `CasaEngine.Demos/Demos/Collision3dBasicDemo.cs`.
- Étapes : lire d'abord l'API de requêtes de `IPhysicsWorld` (`rg RayCast CasaEngine`) ; plafonner le nombre de boîtes tirées, liste réutilisée.
- Validation : build ; lancement ; cycle à 0.
- Commit : `feat(demos): make the 3D collision demo interactive`
- Note de validation (2026-11-14) : `Collision3dBasicDemo` réécrite en trois parties commentées : corps statique et dynamiques, lancer d'une balle (Espace : position écrite, `SyncTransformFromScene`, puis `Velocity`), requête physique (`IPhysicsWorld.ShapeSweep` depuis la caméra, point et normale dessinés par `Line3dRendererComponent`). Douze balles créées avec la scène et réutilisées, forme de requête créée une fois : rien n'est alloué en jeu. `WorldRayCast` ne rend pas le corps touché, d'où le balayage. Vérifié par une capture avec un tir automatique temporaire (retiré avant commit) : balles lancées, pile dispersée, marqueur jaune sur le premier corps devant la caméra. Build 0 erreur ; cycle PASS. La touche Espace elle-même n'a pas été pressée par l'agent.

### ⏳ T4.2 — Collision 2d interactive et triggers

- Objectif : lever le `//TODO` de `Collision2dBasicDemo.cs:91` (tir de boîtes) et montrer un volume déclencheur avec sa réaction visible.
- Fichiers : `CasaEngine.Demos/Demos/Collision2dBasicDemo.cs`.
- Étapes : lire `docs/engine/collision-2d-3d-architecture.md` et l'API de trigger existante avant d'écrire.
- Validation : build ; lancement ; cycle à 0.
- Commit : `feat(demos): add shooting and a trigger volume to the 2D collision demo`

### ⏳ T4.3 — Commentaires des démos muettes

- Objectif : les six démos sans commentaire expliquent chaque bloc de `Initialize` (pourquoi, pas quoi) ; les séparateurs `//===` deviennent des phrases.
- Fichiers : `CutsceneMoveToDemo.cs`, `CutsceneNavigateToDemo.cs`, `EnvironmentShowcaseDemo.cs`, `ParticleSystemDemo.cs`, `SceneManagementDemo.cs`, `StaticShadowValidationDemo.cs`, `SkinnedMeshDemo.cs`.
- Étapes : commentaires seulement, aucun changement de comportement.
- Validation : build ; `git diff` ne montre que des commentaires.
- Commit : `docs(demos): explain the scene setup of the uncommented demos`

### ⏳ T4.4 — Grosses démos allégées

- Objectif : dans Audio, AnimationBlend et Material, `Initialize` se lit comme un tutoriel ; le HUD et la validation vivent dans des fichiers voisins (P7).
- Fichiers : `AudioDemo.cs`, `AnimationBlendDemo.cs`, `MaterialDemo.cs` et leurs nouveaux fichiers voisins.
- Étapes : capture de référence de chaque démo avant ; déplacement de code sans changement ; capture après.
- Validation : build ; captures avant et après identiques tant que la scène est statique (limite écrite dans `docs/engine/demos-main-menu.md`) ; cycle à 0. Une tâche par démo si le diff d'une seule dépasse ce qu'un commit lisible supporte : le dire dans la note.
- Commit : `refactor(demos): split HUD and validation out of the large demos`

---

## Phase 5 — Fonctionnalités sans démo

Chaque tâche : lire la doc citée et le code de la fonctionnalité, écrire une démo minimale et commentée, remplir sa fiche (T1.1), l'enregistrer dans son thème, l'ajouter à `docs/engine/demos-main-menu.md`. Validation commune : build ; lancement par titre ; cycle à 0.

### ⏳ T5.1 — Démo input et possession

- Fichiers : `CasaEngine.Demos/Demos/PlayerInputDemo.cs`, `DemosGame.cs`, doc. Sources : `docs/engine/player-input.md`, `docs/engine/gameplay-possession.md`.
- Commit : `feat(demos): add the player input and possession demo`

### ⏳ T5.2 — Démo coroutines

- Fichiers : `CasaEngine.Demos/Demos/CoroutineDemo.cs`, `DemosGame.cs`, doc. Source : `docs/engine/coroutines_specifications.md`.
- Commit : `feat(demos): add the coroutine demo`

### ⏳ T5.3 — Démo couches 2D

- Fichiers : `CasaEngine.Demos/Demos/Layers2dDemo.cs`, `DemosGame.cs`, doc. Sources : `docs/engine/scrolling-layers.md`, `docs/engine/cellular-layers.md`, `Content/PsxBackdropLayers`.
- Étapes propres : si les deux systèmes ne tiennent pas dans une scène lisible, faire deux démos et le noter.
- Commit : `feat(demos): add the 2D layers demo`

### ⏳ T5.4 — Démo sauvegarde de partie

- Fichiers : `CasaEngine.Demos/Demos/SaveGameDemo.cs`, `DemosGame.cs`, doc. Sources : `docs/engine/save-games.md`, `CasaEngine/Framework/SaveGames`, ADR-0044.
- Étapes propres :
  1. Lire comment `GameSettings.SaveGames` construit son stockage ; brancher un `SaveGameFileStorage(string folderPath)` sur `AppContext.BaseDirectory` (D9). Si cela demande une modification de `CasaEngine/` : ⚠️ Blocked.
  2. La fiche et le panneau affichent le chemin complet du fichier écrit ; une action de la démo le supprime.
  3. Vérifier que le dossier écrit est ignoré par git (`git status` propre après un lancement).
- Commit : `feat(demos): add the save game demo`

---

## Phase 6 — Clôture

### ⏳ T6.1 — Entrées des démos par la configuration du moteur

- Objectif : chaque action de chaque démo, du menu et du panneau d'aide passe par la configuration d'entrées du moteur, avec une touche et un bouton de manette ; la fiche et le panneau affichent les deux (D8).
- Fichiers : toutes les démos qui lisent une entrée, `DemosGame.cs`, `DemoKeyboard.cs`, le type de ligne de contrôle de T1.1, la configuration d'entrées des démos dans `CasaEngine.Demos/Content/`, `docs/engine/demos-main-menu.md`, tests du menu.
- Étapes :
  1. Lire `docs/engine/player-input.md`, `InputMappingManager`, `PlayerInput` et le format `.buttonsMapping` ; écrire sous cette tâche quel chemin les démos utilisent et pourquoi (les démos n'ont pas toutes un `PlayerController`).
  2. Si ce chemin demande une modification de `CasaEngine/`, ou si une action n'a pas d'équivalent manette raisonnable (par exemple F5 à F9 de ViewManager Sandbox) : ⚠️ Blocked avec la liste, et s'arrêter.
  3. Déclarer les actions une fois par démo ; lecture sans allocation par image ; conserver la règle « rien n'est lu quand la fenêtre est inactive » de `DemoKeyboard`.
  4. Remplir la colonne manette des fiches depuis la configuration, pas à la main.
- Validation : build, tests ; cycle à 0 ; contrôle `rg` de « Validation globale » ; manette réelle à vérifier par l'auteur (🧪 tant que non faite).
- Commit : `feat(demos): read every demo action through the engine input mapping`

### ⏳ T6.2 — Doc, README et vérification

- Objectif : `docs/engine/demos-main-menu.md` décrit le contrat final (fiche, panneau, thèmes, « Adding a demo ») ; le README racine montre une capture du menu ; validation globale passée.
- Fichiers : `docs/engine/demos-main-menu.md`, `docs/README.md`, `README.md`, `github/demos_main_menu.jpg`, `ai-agent/README.md`.
- Étapes : capture par `CASAENGINE_CAPTURE_SCREENSHOT_PATH` ; validation globale ; passe `verifier` neuve sur le chantier (`AGENTS.md` §8) ; rapport de fin (§12).
- Validation : celle de « Validation globale ».
- Commit : `docs(demos): document the onboarding path and close the plan`

---

## Points ouverts

À trancher pendant l'exécution, ou à remonter en ⚠️ Blocked si la réponse manque.

| Réf | Sujet | Tâche concernée |
|---|---|---|
| — | O1 à O4 du premier jet, puis base de la branche et dossier de sauvegarde : tranchés par l'auteur le 2026-11-14, voir D6 à D10. | — |
| — | O1 (monde de la démo éditeur) tranché par l'auteur le 2026-11-14 : copie de SampleProject, voir D11. | — |

## Hors périmètre

- Toute modification de `CasaEngine/`, de l'éditeur, de MGUI ou des formats d'assets.
- Vignettes ou vidéos par démo dans le menu.
- Démos d'effect quads et de bitmap fonts ; fonctionnalités absentes du moteur (post-process, terrain, réseau).
- Traduction des démos ou du menu.
- Les projets de `Projects/` et le Launcher.
