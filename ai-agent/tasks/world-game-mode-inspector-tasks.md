# Plan agent IA — Mode de jeu, mode de gameplay et pion par défaut dans l'inspecteur du monde

Plan d'exécution du point ouvert O3 du plan moteur R0 (`ai-agent/tasks/script-types-full-name-tasks.md`, chantier parent
`docs/plan-restructuration-dll.md`) : rétablir dans l'inspecteur MGUI du monde les lignes « Game Mode » et « Default Pawn » de l'éditeur
WPF supprimé par `dc2f4d25` (2026-03-30, « remove unused projects »), et ajouter « Gameplay Mode ».
Les décisions D1 → D8 ci-dessous ont été arbitrées avec l'auteur le 2026-10-17 : **ce plan les applique, il ne les rediscute pas**.

Ce fichier doit être mis à jour pendant le travail : l'icône au début de chaque tâche indique son statut courant.

Approuvé par l'auteur le 2026-10-17 (« merge main avant et le plan est approuvé. Fais toutes les tâches »). Relu par deux
`plan-verifier` frais : REVISE (une P2 : un `.gameMode` dont l'écriture échoue était abandonné sans question, corrigée dans D4 et
T3), puis READY.

**Prérequis rempli le 2026-10-17.** À la demande de l'auteur, la branche R0 `chantier/script-types-full-name` (`fcc5280b`, qui
ajoute la ligne « Script » au même panneau, T3.2 `c7124da7`) est fusionnée dans `main` du moteur par la branche d'intégration
`chantier/merge-script-types-into-main` (fusion `a611ff57` au-dessus de `main` `9a1f50f8`, puis avance rapide de `main`). La
branche de ce plan, `chantier/world-game-mode-inspector`, part de ce `main`.

## Objectif

Pour le monde sélectionné, `EntityDetailsPanel` montre, sous la ligne « Script » de R0 :
- « Game Mode » : le fichier `.gameMode` du monde (`World.PlayerStartupSettingsAssetId`), choisi parmi les seuls `.gameMode` ;
- « Gameplay Mode » : le fichier `.gameplayMode` du monde (`World.GameplayModeAssetId`), choisi parmi les seuls `.gameplayMode` ;
- « Default Pawn » : le pion du mode choisi (`PlayerStartupSettings.DefaultPawnAssetId`), choisi parmi les seuls `.entity`, écrit
  dans le `.gameMode` à la sauvegarde du projet (Ctrl+S).

Chaque choix est une commande d'historique annulable. Le livrable inclut l'écriture d'un `.gameMode` par l'éditeur, qui n'existe pas.

Hors périmètre : un choix « aucun » dans `AssetSelector` (D6) ; un éditeur de documents `.gameMode` ; les autres champs du mode
(`PlayerControllerClass`, `AIControllerClass`, `HUDClass`) dans l'inspecteur ; un pion propre à un monde.

## État vérifié du dépôt (2026-10-17, branche R0 `fcc5280b`, revérifié en T0 sur `main` `a611ff57`)

- Le modèle a changé depuis l'éditeur WPF : `f72b7907` (« migrate player startup settings ») et `08de2874` (« remove legacy game
  mode ») ont remplacé `World.GameModeAssetId` / `World.GameMode` par `World.PlayerStartupSettingsAssetId` (`World.cs:76`) et
  `World.PlayerStartupSettings` (`World.cs:77`), classe `CasaEngine/Framework/Gameplay/PlayerStartupSettings.cs`, même extension
  (`Constants.FileNameExtensions.PlayerStartupSettings = ".gameMode"`). `World.GameplayModeAssetId` (`World.cs:79`,
  `.gameplayMode`) est un autre asset, lu au démarrage du jeu (`World.StartGameplayModeAsset`, `:494-508`).
- Lecture du monde : `player_startup_settings_asset_id`, ou l'ancienne clé `game_mode_asset_id` (`World.cs:943-951`) ;
  `gameplay_mode_asset_id` (`:953-956`). Écriture : `EditorEntityJsonSerializer.cs:42-43`. Les deux identifiants sont donc déjà
  sauvés avec le monde.
- Le pion n'est pas dans le monde : il est dans le `.gameMode`, acquis par handle compté pour la vie du monde
  (`World.LoadPlayerStartupSettings`, `:346-359`, ADR-0037), donc une instance partagée par tous les mondes de ce mode.
  `LoadPlayerStartupSettings` est `internal` (`InternalsVisibleTo` : EditorServices, Tests, AposShapes ; pas `CasaEngine.Editor`).
- `AssetContentManager` (`Acquire<T>`, `:67-88`) : un asset que plus personne ne tient reste en cache jusqu'à `CollectUnreferenced`
  (appelé au début d'un changement de monde) ; un `Acquire` avant cela rend la même instance.
- Aucune écriture d'un `.gameMode` : `EditorAssetJsonSerializer.TrySerialize` (`CasaEngine.EditorServices`) a des cas pour
  `ButtonsMapping`, `Animation2dData`, `Entity`, `World`, matériaux, particules, sprites, tilemaps, écrans, etc., aucun pour
  `PlayerStartupSettings` : `EditorAssetWriterService.SaveAsset` lève « not supported ». `default_pawn_asset_id` n'est écrit nulle
  part. `SaveObjectBase` (`:115`) écrit `id` et `name` via `EditorJsonSaveHelper.SaveObjectBase`.
- `PlayerStartupSettings.Load` lit `default_pawn_asset_id`, `player_controller_class`, `ai_controller_class` (s'il existe),
  `hud_class` ou à défaut l'ancienne clé `hud_classClass`. `Projects/RPGDemo/RpgGameMode.gameMode` porte encore `hud_classClass`.
- L'éditeur WPF ne sauvait pas le pion (`ValidateDefaultPawnAsset` changeait l'instance en mémoire ; l'ancien `GameMode` n'avait pas
  d'écriture) et ne rechargeait pas le mode quand on en changeait.
- Motif à suivre dans `CasaEngine.Editor/Controls/EntityDetailsPanel.cs` (branche R0) : `BuildWorldPropertyEditors` (`:620`) ajoute
  la ligne Script (`AddScriptRow`, `:959`) puis la grille d'environnement ; lignes d'asset = `AssetSelector` + `Filter` sur
  `AssetInfo.AssetType` + `AssetChanged` → `ExecuteHistoryCommand` (`:633-667` sur `main`, `ApplyWorldEnvironmentChange`).
  `AssetInfo.InferAssetType` = extension sans point, en minuscules : `gamemode`, `gameplaymode`, `entity` (le catalogue de RPGDemo
  ne déclare pas `asset_type` pour `RpgGameMode.gameMode`, `AssetInfos.json:1156`).
- `AssetSelector` : `Filter`, `AssetChanged`, `GetPickableAssets()` et `SelectAsset(AssetInfo)` internes ; pas de choix « aucun ».
- Sauvegarde de l'éditeur : `GameEditor.SaveCurrentProject()` (`GameEditor.cs:2416`, menu Save, raccourci, « sauver avant d'ouvrir un
  monde ») sauve les documents modifiés puis `EditorProjectAuthoringService.SaveProject(world)` → `EditorWorldWriter.SaveWorld`, puis
  `MarkSaved` du contexte World. `OpenWorldAsset` (`:4539`) vide l'historique du monde (`:4593`) avant `currentWorld?.Clear()`. Le
  rechargement des scripts vide aussi cet historique (`:1265`) mais garde les modifications du monde par un instantané.
- `EditorAssetSaveSource` (`EditorAssetWriterService.cs:9-18`) : une valeur par panneau qui écrit ; `GameEditor.OnEditorAssetSaved`
  (`:663`) ne traite pas `.gameMode`.
- Tests : `CasaEngine.Tests/Editor/EntityDetailsScriptRowTests.cs` (R0) construit le panneau sur un bureau sans tête
  (`HeadlessUiTestHarness.NewDesktop`, `includeComponentTree: false`, contexte d'historique de test) ;
  `CasaEngine.Tests/Application/WorldAssetHandleTests.cs` construit un monde avec un `Game` et un `AssetContentManager` de test
  (`NewManager`, `NewWorldWithGame`) et pilote `LoadPlayerStartupSettings`.
- Prochain ADR du moteur : 0076 (0075 = R0).
- Changement préexistant de l'auteur dans le checkout du moteur : `CasaEngine.Launcher/Program.cs`. Ne jamais l'indexer.

## Décisions verrouillées

| Réf | Décision |
|---|---|
| D1 | (Q2) Ligne « Game Mode » = `World.PlayerStartupSettingsAssetId` ; filtre `AssetType == "gamemode"` ; commande « Change World Game Mode ». Exécution et annulation fixent l'identifiant puis rechargent les réglages du monde (`World.LoadPlayerStartupSettings`, rendue publique : ajout d'API), pour que « Default Pawn » montre le pion du nouveau mode. Sauvé avec le monde (clé existante). |
| D2 | (Q3) Ligne « Gameplay Mode » = `World.GameplayModeAssetId` ; filtre `"gameplaymode"` ; commande « Change World Gameplay Mode » ; sauvé avec le monde (clé existante) ; aucun rechargement (lu au démarrage du jeu). |
| D3 | (Q1 = B) Ligne « Default Pawn » = `DefaultPawnAssetId` de l'instance `World.PlayerStartupSettings` ; filtre `"entity"` ; commande « Change Game Mode Default Pawn », dans le contexte d'historique du monde. Un texte sous la ligne nomme le mode et dit que le pion vaut pour tous les mondes de ce mode. Ligne désactivée quand le monde n'a pas de mode (identifiant vide) ou quand le panneau n'a pas de registre des modes modifiés (D4). |
| D4 | (question du 2026-10-17) Le `.gameMode` est écrit avec Ctrl+S. Un registre des modes modifiés (`PendingGameModeEdits`, `CasaEngine.Editor/Controls/`) garde, pour chaque mode touché depuis la dernière sauvegarde, un handle sur l'instance (elle n'est pas libérée tant qu'il est tenu : un rechargement du monde la retrouve) et le pion d'origine. L'exécution **et** l'annulation enregistrent le mode avant de changer le pion (une annulation après une sauvegarde doit être écrite). `SaveCurrentProject` écrit chaque mode dont le pion diffère de l'origine (`TrySaveAll`), puis relâche les entrées écrites ; un échec d'écriture est journalisé, laisse son entrée en attente, et `TrySaveAll` le rend (faux) : `SaveCurrentProject` n'appelle alors **pas** `MarkSaved` du contexte World (aujourd'hui inconditionnel, `GameEditor.cs:2441`), pour que le monde garde son « * » et qu'ouvrir un autre monde pose encore la question de sauvegarde (`GameEditor.cs:4573`) ; répondre « Save » échoue de nouveau et n'ouvre pas l'autre monde (`TrySaveProjectBeforeOpeningWorld`, `:4637-4650`). Ouvrir un autre monde sans sauver (réponse « Don't Save », ou monde non modifié) remet le pion d'origine puis relâche tout. Le rechargement des scripts garde le registre. `GameEditor` possède le registre et le donne au panneau du monde. |
| D5 | Écriture d'un `.gameMode` : un cas `PlayerStartupSettings` dans `EditorAssetJsonSerializer.TrySerialize` : `SaveObjectBase`, puis `default_pawn_asset_id`, `player_controller_class`, `ai_controller_class`, `hud_class`. `Load` n'est pas modifié. L'ancienne clé `hud_classClass`, toujours lue, n'est plus écrite (aucun champ renommé : `hud_class` est déjà la clé lue en premier). Nouvelle valeur `EditorAssetSaveSource.WorldInspector = 8`. |
| D6 | (Q4) Pas de choix « aucun » : `AssetSelector` n'est pas modifié. |
| D7 | Les trois lignes sont dans une grille sous la ligne « Script », dans l'ordre Game Mode, Default Pawn, Gameplay Mode, avec les libellés exacts « Game Mode », « Default Pawn », « Gameplay Mode ». |
| D8 | ADR-0076 (numéro revérifié en T0) enregistre D1, D3, D4, D5 (écriture d'un format d'asset, API publique, règle de sauvegarde d'un asset partagé depuis l'inspecteur du monde). |

## Règles d'exécution pour l'agent

- **Branche dédiée `chantier/world-game-mode-inspector`**, créée depuis `main` du moteur après la fusion de R0, dans un worktree du
  sous-module (écrire son `config.worktree` : piège `core.worktree`). Ne jamais committer sur `main`.
- **Une seule tâche à la fois.** `⏳` → `🚧` avant de commencer ; à la fin, validation, puis `✅`, `🧪` ou `⚠️`, note de validation
  sous la tâche, et **un commit dédié** qui inclut la mise à jour de ce fichier.
- **Un commit par tâche**, atomique et compilable, message en anglais `type(area): summary` (message suggéré dans chaque tâche).
- **Ne jamais pousser.** Le merge sur `main` reste une décision de l'auteur.
- **Ne rien inventer** : sinon ⚠️ Blocked, question dans « Points ouverts », arrêt.
- **Build** avant ✅ dès que du code est touché : `dotnet build CasaEngine.MonoGame.sln` et `dotnet build CasaEngine.Editor.MonoGame.sln`.
  **Tests** : `CasaEngine.Tests` n'est dans aucun `.sln` : `dotnet build CasaEngine.Tests/CasaEngine.Tests.csproj`, puis
  `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj --no-build --blame-hang-timeout 5m`.
- **Tests d'abord** : chaque tâche de code écrit ses tests, les voit rouges sur le code d'avant (noms consignés dans la note), puis
  écrit le code.
- **Ne jamais indexer** `CasaEngine.Launcher/Program.cs` ni aucune modification préexistante : `git add` fichier par fichier.
- **Langue** : ce plan en français ; code, commits, docs et ADR en anglais.
- Rappel moteur : le runtime ne dépend pas de l'éditeur ; l'écriture d'assets reste dans `CasaEngine.EditorServices` /
  `CasaEngine.Editor` ; la seule modification du runtime est la visibilité de `World.LoadPlayerStartupSettings`.

## Légende des statuts

- ⏳ Todo · 🚧 In progress · 🧪 Needs testing · ✅ Done · ⚠️ Blocked.

## Validation globale

- `dotnet build CasaEngine.MonoGame.sln` et `dotnet build CasaEngine.Editor.MonoGame.sln` : 0 erreur.
- `dotnet build CasaEngine.Tests/CasaEngine.Tests.csproj` puis `dotnet test ... --no-build --blame-hang-timeout 5m` : 0 échec.
- Vérificateur indépendant (`verifier`) après T3, sur l'affirmation « les trois lignes s'affichent, s'éditent, s'annulent ; le
  `.gameMode` est écrit avec Ctrl+S et seulement alors ; l'abandon le remet ; une écriture du `.gameMode` qui échoue laisse le monde
  marqué « * » et ouvrir un autre monde demande encore de sauver ».
- Recette de l'auteur dans l'éditeur sur RPGDemo (liste en T4).

---

## Phase 0 — Plan

### ✅ T0 — Versionner le plan et l'ADR, revérifier l'état

- Objectif : ce plan dans `ai-agent/tasks/world-game-mode-inspector-tasks.md`, sa ligne dans le tableau de `ai-agent/README.md`,
  l'ADR-0076 (statut Accepted, décisions D1/D3/D4/D5) et son entrée dans `docs/decisions/README.md`.
- Étapes : revérifier sur `main` fusionné chaque fait de « État vérifié » (numéros de ligne, numéro d'ADR libre, emplacement du plan
  R0 : `tasks/` ou `tasks/archive/`) ; inventorier les chemins de `GameEditor` qui quittent le monde courant
  (`rg -n "SetWorldToLoad|_editorHistory.Clear\(" CasaEngine.Editor/GameEditor.cs`) pour y attacher l'abandon de D4, sauf le
  rechargement des scripts ; corriger le plan si un fait a bougé (sans changer une décision : sinon ⚠️).
- Validation : relecture ; aucun code touché.
- Note de validation (2026-10-17) : faits revérifiés sur `main` `a611ff57` (la fusion n'ajoute à R0 que le correctif SandBoxGame
  `9a1f50f8` ; les numéros de ligne cités de `World.cs`, `EntityDetailsPanel.cs`, `GameEditor.cs`, `AssetContentManager.cs`,
  `EditorAssetJsonSerializer.cs` et `EditorAssetWriterService.cs` sont inchangés). ADR-0076 libre (dernier : 0075). Plan R0 dans
  `ai-agent/tasks/`. Validation de la fusion : les deux `.sln` 0 erreur, `CasaEngine.Tests` **4314/4314** (même compte que R0 seule ; résultat aussi dans la ligne
  du tableau d'`ai-agent/README.md`). Inventaire des chemins qui quittent le monde courant (`GameEditor.cs`) : `OpenWorldAsset`
  (`:4593` historique vidé, `:4597` `currentWorld?.Clear()`, `:4598` `SetWorldToLoad`) et `LoadInitialWorldIntoEditorRuntime`
  (`:1371`, premier monde d'un projet ouvert) reçoivent l'abandon de D4 ; le rechargement des scripts (`:1265-1270`) le garde
  (D4) ; les autres `_editorHistory.Clear` (`:812`, `:835`, `:905`, `:8106`, `:8149`) visent les contextes Material, Sprite,
  Particle : hors sujet.
- Commit : `docs(ai-agent): version the world game mode inspector plan and ADR-0076`

## Phase 1 — Écrire un `.gameMode`

### ✅ T1 — Le cas `PlayerStartupSettings` de l'écriture d'assets (D5)

- Fichiers : `CasaEngine.EditorServices/EditorAssetJsonSerializer.cs`, `CasaEngine.EditorServices/EditorAssetWriterService.cs`
  (valeur `WorldInspector`) ; `CasaEngine.Tests/EditorServices/PlayerStartupSettingsWriterTests.cs`.
- Étapes : tests d'abord, rouges : (i) `SaveAsset` d'un `PlayerStartupSettings` dans un projet temporaire écrit un fichier (rouge
  aujourd'hui : « not supported ») ; (ii) aller-retour `Load` → `SaveAsset` → `Load` : identifiant, nom, pion, contrôleur (y compris
  vide), contrôleur d'IA, HUD (y compris null) identiques ; (iii) le contenu de `RpgGameMode.gameMode` (avec `hud_classClass`) relu
  puis écrit donne `hud_class` et plus `hud_classClass`, et se relit pareil ; (iv) les clés écrites sont exactement `id`, `name`,
  `default_pawn_asset_id`, `player_controller_class`, `ai_controller_class`, `hud_class`. Puis le code.
- Validation : tests rouges puis verts ; suite verte ; les deux `.sln`.
- Note de validation (2026-10-17) : `CasaEngine.Tests/EditorServices/PlayerStartupSettingsWriterTests.cs` (5 cas : fichier écrit et
  `AssetSaved` levé avec l'identifiant et la source `WorldInspector` ; aller-retour complet ; contrôleur vide et HUD null gardés ;
  contenu de `RpgGameMode.gameMode` tel que commité, avec `hud_classClass`, écrit en `hud_class` et relu pareil ; clés écrites
  exactement `id`, `name`, `default_pawn_asset_id`, `player_controller_class`, `ai_controller_class`, `hud_class`, dans cet ordre).
  Rouges avant le code : les 5, `InvalidOperationException` « Asset type 'CasaEngine.Framework.Gameplay.PlayerStartupSettings' is not
  supported by the editor writer service » (la valeur `EditorAssetSaveSource.WorldInspector = 8` ajoutée d'abord, seule, pour que les
  tests compilent). Code : cas `PlayerStartupSettings` de `EditorAssetJsonSerializer.TrySerialize` (`SavePlayerStartupSettings` :
  `SaveObjectBase` puis les quatre clés). Mutation réelle (fichier copié, ligne `ai_controller_class` retirée, tests, fichier rendu,
  `cmp` identique) : 3 cas rouges (aller-retour, contrôleur vide, clés exactes). Les deux `.sln` : 0 erreur ; `CasaEngine.Tests` :
  **4319/4319** (4314 + 5).
- Commit : `feat(editor-services): write player startup settings (.gameMode) assets`

## Phase 2 — Les lignes du monde

### ✅ T2 — « Game Mode » et « Gameplay Mode » (D1, D2, D7)

- Fichiers : `CasaEngine.Editor/Controls/EntityDetailsPanel.cs` ; `CasaEngine/Framework/Scene/World/World.cs`
  (`LoadPlayerStartupSettings` publique, commentaire XML mis à jour) ; `CasaEngine.Tests/Editor/EntityDetailsGameModeRowsTests.cs`.
- Étapes : tests d'abord, rouges (bureau sans tête comme `EntityDetailsScriptRowTests`, monde avec `Game` et gestionnaire d'assets
  de test comme `WorldAssetHandleTests`, catalogue de test avec deux `.gameMode`, un `.gameplayMode`, deux `.entity`) : (i) un monde
  sélectionné montre les lignes « Game Mode » et « Gameplay Mode » avec le nom de l'asset (ou None) ; (ii) leurs sélecteurs ne
  proposent que les `.gameMode`, resp. `.gameplayMode` ; (iii) choisir un autre mode change `PlayerStartupSettingsAssetId`, recharge
  `World.PlayerStartupSettings` (instance du nouveau mode) et l'annulation rend l'identifiant et l'instance d'origine ; (iv) idem
  pour `GameplayModeAssetId` ; (v) une entité sélectionnée n'a pas ces lignes. Exposer les sélecteurs en `internal` comme
  `ScriptCombo` de R0 si les tests en ont besoin. Puis le code.
- Validation : tests rouges puis verts ; suite verte ; les deux `.sln`.
- Note de validation (2026-10-17) : `CasaEngine.Tests/Editor/EntityDetailsGameModeRowsTests.cs` (9 cas : lignes avec ids et noms ;
  monde sans mode = None ; filtres `.gameMode` / `.gameplayMode` seulement ; choix d'un autre mode = id changé, instance rechargée,
  annulation qui rend l'id et l'instance d'origine ; même choix = aucune entrée d'historique ; Gameplay Mode sans rechargement ;
  nom de l'entrée d'historique ; monde sans `Game` = id changé sans chargement ; entité = pas de sélecteurs). Le monde de test a un
  `Game` et un gestionnaire d'assets de test (motif de `WorldAssetHandleTests`) ; le catalogue a deux `.gameplayMode` (il en faut
  un second pour changer). Rouges avant le code : les 9, les propriétés `GameModeSelector` / `GameplayModeSelector` existaient sans
  les lignes et restaient nulles. Code : `EntityDetailsPanel.AddWorldModeRows` (une grille sous la ligne Script, Game Mode puis
  Gameplay Mode, prête pour Default Pawn en T3) et `CreateWorldAssetSelector` (filtre par type, commandes `Change World Game Mode` /
  `Change World Gameplay Mode`, aucune commande si l'id ne change pas, rechargement des réglages seulement si `world.Game` n'est pas
  nul, puis `RebuildPropertyEditors`) ; `World.LoadPlayerStartupSettings` publique, commentaire XML mis à jour ; sélecteurs remis à
  null dans `ClearDetailsContent`. Mutations réelles par script (fichier copié, rendu, `cmp` identique) : (a) sans rechargement →
  1 rouge ; (b) filtre retiré → 1 rouge ; (c) changement appliqué sans commande d'historique → 3 rouges. Les deux `.sln` : 0
  erreur ; `CasaEngine.Tests` : **4328/4328** (4319 + 9), relancé par la session principale.
- Commit : `fix(editor): show and pick the world game mode and gameplay mode in the inspector again`

### ⏳ T3 — « Default Pawn » et l'écriture avec Ctrl+S (D3, D4)

- Fichiers : `CasaEngine.Editor/Controls/PendingGameModeEdits.cs` (nouveau), `CasaEngine.Editor/Controls/EntityDetailsPanel.cs`,
  `CasaEngine.Editor/GameEditor.cs` (registre possédé, donné à `_entityDetailsPanel`, écrit dans `SaveCurrentProject` avant
  `EditorProjectAuthoringService.SaveProject`, `MarkSaved` du contexte World seulement si `TrySaveAll` a réussi, abandonné sur les
  chemins inventoriés en T0) ;
  `CasaEngine.Tests/Editor/EntityDetailsDefaultPawnRowTests.cs`, `CasaEngine.Tests/Editor/PendingGameModeEditsTests.cs`.
- Étapes : tests d'abord, rouges : (i) un monde avec un mode montre « Default Pawn » avec le pion du mode et un texte qui nomme le
  mode ; sans mode, ou sans registre, la ligne est désactivée ; (ii) le sélecteur ne propose que les `.entity` ; (iii) choisir un pion
  change `DefaultPawnAssetId` de l'instance partagée et le registre contient le mode ; l'annulation rend le pion ; (iv) après le
  changement, `world.ReleasePlayerStartupSettings()` puis `CollectUnreferenced()` ne libère pas le mode (handle du registre) et un
  nouveau `LoadPlayerStartupSettings` rend la même instance au pion modifié ; (v) `TrySaveAll` écrit le `.gameMode` (fichier relu :
  nouveau pion), relâche le handle et vide le registre ; un mode revenu à son pion d'origine n'est pas écrit ; (vi) changer, sauver,
  annuler, sauver : le fichier a de nouveau l'ancien pion ; (vii) `DiscardAll` remet le pion d'origine dans l'instance et relâche ;
  (viii) une écriture qui échoue (`.gameMode` en lecture seule) est journalisée, `TrySaveAll` rend faux, l'entrée reste en attente
  avec son handle et son pion d'origine, et une seconde tentative après retrait de la lecture seule écrit et vide le registre. Puis le
  code, y compris dans `SaveCurrentProject` le `MarkSaved` du contexte World conditionné au succès de `TrySaveAll` (câblage de
  `GameEditor` non couvert par un test unitaire : couvert par le vérificateur et la recette T4).
- Validation : tests rouges puis verts ; suite verte ; les deux `.sln` ; puis vérificateur indépendant (`verifier`) sur
  l'affirmation de « Validation globale » ; ses constats sont tranchés (FIX / DEFER / REJECT) avant ✅.
- Commit : `fix(editor): edit the default pawn of the world's game mode again, written with the project save`

## Phase 3 — Clôture

### ⏳ T4 — Docs, O3, recette

- Fichiers : `docs/engine/gameplay-mode.md` (courte section « Editor » : les trois lignes, la règle d'écriture du `.gameMode`,
  lien ADR-0076) ; le plan R0 (O3 fermé, avec renvoi à ce plan) ; ce plan.
- Étapes : validation globale complète ; liste de recette pour l'auteur : ouvrir RPGDemo, sélectionner le monde ; voir Script,
  Game Mode (RpgGameMode), Default Pawn (le pion du mode) et Gameplay Mode (None) ; changer le pion → « * » sur le monde ; Ctrl+S →
  `Projects/RPGDemo/RpgGameMode.gameMode` porte le nouveau pion et `hud_class` ; annuler puis Ctrl+S → pion d'origine ; changer le
  pion puis ouvrir un autre monde sans sauver → le fichier est inchangé et, au retour, l'ancien pion s'affiche ; mettre
  `RpgGameMode.gameMode` en lecture seule, changer le pion, Ctrl+S → avertissement au journal, le monde garde « * », ouvrir un autre
  monde pose la question de sauvegarde (retirer ensuite la lecture seule). Rendre ensuite
  `RpgGameMode.gameMode` à son état commité (`git diff` vérifié avant).
- Validation : suite verte ; les deux `.sln` ; recette 🧪 jusqu'au retour de l'auteur.
- Commit : `docs(gameplay): document editing the world's game mode in the inspector; close O3`

---

## Points ouverts

| Réf | Question | Réponse |
|---|---|---|
| — | — | — |
