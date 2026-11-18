# Plan agent IA — Éditeur de cutscenes : aperçu du monde (tranche A)

Programme « éditeur de cutscenes visuel » : un document à quatre zones (aperçu du monde, arbre des actions, détails typés, timeline), inspiré de Sequencer (Unreal), Timeline (Unity) et AnimationPlayer (Godot).
Ce plan détaille la **tranche A** : l'aperçu du monde avec lecture et arrêt. Les tranches suivantes sont décrites en fin de fichier et auront chacune leur plan.

Les décisions D1 → D7 ci-dessous ont été arbitrées avec l'auteur le 2026-11-18 : **ce plan les applique, il ne les rediscute pas**.
L'auteur a annoncé le 2026-11-18 que ce plan serait approuvé automatiquement et que l'exécution s'enchaînait. Les points P1 → P9 sont des arbitrages de l'agent, à confirmer par l'auteur à la recette.

Ce fichier doit être mis à jour pendant le travail : l'icône au début de chaque tâche indique son statut courant.

## Objectif

Ouvrir un `.cutscene` montre un monde et permet d'y jouer la cutscene :

- le document affiche un aperçu d'une **copie isolée** d'un monde du projet, à côté des propriétés existantes ;
- une barre de transport offre lecture, arrêt, redémarrage et le choix du monde ;
- la lecture exécute le vrai `CutsceneDirector` sur l'asset en cours d'édition, modifications non enregistrées comprises ;
- la lecture ne touche ni le monde ouvert dans l'éditeur, ni le son, ni le clavier ;
- l'éditeur sait se lancer, jouer l'aperçu et écrire une capture d'écran sans intervention, pour la validation à distance (D7).

## État vérifié du dépôt (2026-11-18)

Branche `chantier/cutscene-property-editing`, tête `5bf07ca5`, 7 commits depuis `main` `c365b514`, non fusionnée (`git log`, `git status`).
Fichiers non suivis préexistants, à ne jamais indexer : `Projects/PixelUiSample/.casaeditor/`, `Projects/SpriteCutsceneSample/.casaeditor/`.

Ouverture d'une cutscene :

- `GameEditor.TryOpenCutsceneAsset` ne crée qu'un `CutsceneAssetInspectorPanel(_mainWindow)` : ni monde, ni aperçu (`CasaEngine.Editor/GameEditor.cs:5094-5149`). C'est la cause du constat de l'auteur.
- Le panneau est libéré à la fermeture de l'onglet (`GameEditor.cs:3029-3040`). Aucun `Update` par frame ne lui est envoyé ; les panneaux Animation2D en reçoivent un (`GameEditor.cs:6421-6424`).
- `CutsceneAssetInspectorPanelTests` construit le panneau avec `new CutsceneAssetInspectorPanel(harness.Window)` (`CasaEngine.Tests/Editor/CutsceneAssetInspectorPanelTests.cs:119`).

Aperçus existants :

- `PreviewWorldDriver.Rebuild` crée `new World { Name }`, appelle `LoadContent`, puis un rappel de peuplement ; il ne sait pas partir d'un asset monde (`CasaEngine.Editor/Runtime/PreviewWorldDriver.cs:41-61`). `Tick` appelle `World.Update` en mode `Continuous` (`:68-78`).
- `Animation2dAssetInspectorPanel` est le modèle : un `PreviewWorldDriver` continu (`CasaEngine.Editor/Controls/Animation2dAssetInspectorPanel.cs:237-243`), un `WorldViewportPanel` posé en `Dock.Top` avec `SetWorldOverride` (`:254-281`), et `Update` qui appelle `Tick` (`:423-430`).
- Un monde se charge par `AssetContentManager.LoadCopy<World>(id)` puis `LoadContent(game)` (`CasaEngine/Framework/Application/GameManager.cs:130`, `:146`).

Exécution d'une cutscene :

- `CutsceneDirector` expose `Play`, `Stop`, `IsPlaying`, `GetDebugSnapshot` et l'événement `Ended` ; ni pause, ni temps courant, ni déplacement (`CasaEngine/Framework/Cutscenes/CutsceneDirector.cs:46-123`).
- Les coroutines tournent dans `World.Update` par `RuntimeSystems.Update`, sans condition de politique (`CasaEngine/Framework/Scene/World/World.cs:618`).
- `World.BeginPlay` ne fait rien sous la politique `EditorPreview` (`World.cs:526-531`) : la cutscene de démarrage d'un monde ne part donc pas seule dans l'éditeur.
- `Stop` ne remet pas le monde dans son état initial : rien dans `CutsceneDirector.Stop` ni `StopOwnedWork` ne touche les entités (`CutsceneDirector.cs:88-106`, `:181-186`).

Effets qui sortent du monde (`CasaEngine/Framework/Cutscenes/CutsceneActionCoroutineFactory.cs`) :

- son : `GetAudioService(world)` lit `world.Game?.AudioSystemComponent?.Service` (`:467`) ; sans service, `PlaySound`, `PlayMusic` et `StopMusic` ne font rien et `FadeMusic` n'attend pas (`:95-109`, `:502-538`) ;
- fondus : `GetScreenEffectService(world)` lit `world.Game?.ScreenEffectComponent?.Service` (`:476`) ; sans service, les deux fondus n'attendent pas (`:111-150`) ;
- `ChangeWorld` appelle `GameManager.RequestWorldFromCutscene`, qui appelle `SetWorldToLoad` (`:188-201` ; `GameManager.cs:263-272`). Joué dans un aperçu, il demanderait donc à remplacer le monde courant de l'éditeur ;
- `OpenScreen` et `PlayVideo` passent par `GameManager` (`:152-186`) ; sous `UseExternalViewManagement`, vrai en `EditorPreview`, ils sont ignorés avec un message (`GameManager.cs:231-235`) ;
- `ExitOnInput` lit les périphériques du jeu et arme une veille qui lance la cutscene suivante (`:304-317`, `:714-764` ; `CutsceneInputWatch.cs:53-74`). `CutsceneDirector.ExitOnInputResolver` est un point d'injection interne, réservé aux tests (`CutsceneDirector.cs:48-52`).
- `MoveTo` lève une exception, au lieu d'échouer proprement, sur un délai dépassé (`:54-57`) et sur une entité absente ou sans `CharacterControllerComponent` (`:589-597`). `NavigateTo` échoue par `FailPlay`, défini en `:469-473`.

Politique d'exécution :

- `CasaEngineGame.ExecutionPolicy` est une propriété du jeu entier (`CasaEngine/Framework/Application/CasaEngineGame.cs:82`) ; l'éditeur la met à `EditorPreview` (`GameEditor.cs:1114`).
- `EditorPreview` coupe `UpdateAnimatedSprites`, `UpdatePhysicsComponents`, `UpdatePhysicsEngine` et `UpdateGameplayScripts` (`CasaEngine/Framework/Application/GameplayExecutionPolicy.cs:41-53`).

Lien cutscene ↔ monde :

- `CutsceneAsset` ne référence aucun monde. Le lien inverse existe : `World.StartupCutsceneAssetId`, lu de la clé `startup_cutscene_asset_id` du `.world` (`World.cs:83`, `:1074-1075`).
- `Projects/SpriteCutsceneSample` : `FirstWorldLoaded` vaut `SpriteWorld.world`, dont la cutscene de démarrage est `Cutscenes/loop.cutscene`. `exit.cutscene` contient un `ChangeWorld` vers `EndWorld.world`.
- Les réglages d'éditeur propres à un projet sont dans `<projet>/.casaeditor/` (`GameEditor.cs:123`, `:2604`, `:2640`).

Automatisation existante :

- `EditorAutomationOptions` lit `--project`, `--open-asset`, `--screenshot-out`, `--capture-delay`, `--keep-open` (`CasaEngine.Editor/EditorAutomationOptions.cs:73-217`) ; ses tests sont dans `CasaEngine.Tests/Editor/EditorAutomationOptionsTests.cs`.
- Vérifié le 2026-11-18 : `CasaEngine.Editor.exe --project <SpriteCutsceneSample.json> --open-asset Cutscenes/loop.cutscene --screenshot-out <fichier.png> --capture-delay 4` écrit `<fichier>-final.png` puis quitte. Ce lancement écrit aussi `editor-diagnostics.txt` dans le projet quand `--diagnostics-out` est absent.

Décisions antérieures concernées :

- ADR-0014, décision 2 : pas de `CutsceneTimelineAdapter`, l'éditeur de cutscenes reste un contrôle d'arbre, une timeline n'est qu'une projection en lecture seule (`docs/decisions/0014-generic-timeline-base-for-the-editor.md`).
- ADR-0095 : l'arbre d'actions est la seule source de vérité ; l'aperçu dans un monde isolé et la timeline y sont reportés (`docs/decisions/0095-cutscene-assets-are-edited-in-the-editor-on-their-action-tree.md`).

## Décisions verrouillées

| Réf | Décision |
|---|---|
| D1 | L'arbre d'actions reste le modèle enregistré. La timeline en sera une projection, puis une action « section temporelle » permettra le placement libre des clips. Pas de nouvel asset à pistes. |
| D2 | Dans l'aperçu, les sprites animés, la physique et les scripts restent figés pour cette tranche. Rendre la politique d'exécution propre à chaque monde est reporté. |
| D3 | En aperçu : `ChangeWorld` et l'enchaînement d'`ExitOnInput` sont seulement signalés ; le son est coupé par défaut, avec un bouton pour l'activer ; les fondus sont dessinés sur l'aperçu. |
| D4 | Le monde affiché est trouvé automatiquement, avec une liste de choix mémorisée dans les réglages de l'éditeur. Le format `.cutscene` ne change pas. |
| D5 | Le temps s'affichera en secondes et en images. |
| D6 | Le travail continue sur la branche `chantier/cutscene-property-editing`. ADR-0095 est conservé et complété. |
| D7 | L'auteur valide à distance sur captures d'écran de l'éditeur ; sa recette réelle a lieu à la fin. |

## Arbitrages de l'agent, à confirmer

| Réf | Arbitrage |
|---|---|
| P1 | Point d'injection du runtime : une interface publique `ICutsceneEffectHost` et une propriété `CutsceneDirector.EffectHost`, nulle par défaut. Trois membres : `AudioService Audio`, `ScreenEffectService ScreenEffects`, `bool AllowGameRequest(CutsceneActionData action)`. Nulle, le comportement est celui d'aujourd'hui. |
| P2 | `AllowGameRequest` est interrogé avant `ChangeWorld`, `OpenScreen`, `PlayVideo` et `ExitOnInput`. Sur `false`, l'action est sautée et la lecture continue. |
| P3 | En aperçu, un `ChangeWorld` sauté termine la lecture au pas suivant, avec le message « changerait de monde vers X ». C'est l'équivalent de l'effacement du monde dans le jeu. |
| P4 | `MoveTo` échoue proprement (`FailPlay`, donc `Ended` avec `Failed`) au lieu de lever une exception. Aucun test existant ne vérifie l'exception (`rg TimeoutException CasaEngine.Tests` : aucun résultat dans `Cutscenes/`). |
| P5 | Ordre de choix du monde : choix mémorisé pour cette cutscene, puis monde dont elle est la cutscene de démarrage, puis `FirstWorldLoaded`, puis premier monde par nom de fichier. |
| P6 | Le choix est mémorisé dans `<projet>/.casaeditor/cutscene-preview.editor.json`, par identifiant de cutscene. |
| P7 | Lecture et redémarrage reconstruisent toujours le monde d'aperçu depuis l'asset, puisque `Stop` ne restaure rien. |
| P8 | Tranche A livre D3 en partie : son coupé sans bouton, et fondus exécutés avec leur durée mais affichés en texte dans la ligne d'état. Le bouton du son et le dessin des fondus sont en tranche G. |
| P9 | ADR-0014 n'est pas réécrit : sa ligne `Status` reçoit la mention « decision 2 superseded by ADR-0096 ». |

## Règles d'exécution pour l'agent

- **Branche `chantier/cutscene-property-editing`** (D6). Ne jamais committer sur `main`.
- **Une seule tâche à la fois.** Avant de commencer une tâche, remplacer son icône `⏳` par `🚧`. À la fin, lancer la validation indiquée, remplacer l'icône par `✅`, `🧪` ou `⚠️`, ajouter une courte note de validation sous la tâche, puis **créer un commit dédié** qui inclut la mise à jour de ce fichier.
- **Un commit par tâche**, atomique et compilable, message en anglais au format `type(area): summary`.
- **Ne jamais pousser.** Le merge sur `main` reste une décision humaine.
- **Ne rien inventer** : toute API, tout fichier, toute règle utilisée existe dans le dépôt, vient d'une réponse de l'auteur, ou d'une doc officielle citée. Sinon : passer la tâche en ⚠️ Blocked, écrire la question dans « Points ouverts », et **s'arrêter**.
- **Build obligatoire** avant ✅ : `dotnet build CasaEngine.MonoGame.sln` pour le runtime, `dotnet build CasaEngine.Editor.MonoGame.sln` pour l'éditeur ; **tests** `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj`.
- **Ne jamais indexer** les fichiers préexistants non suivis : `git add` fichier par fichier, jamais `git add -A` ni `git add .`.
- **Aucune entrée clavier ou souris envoyée à l'éditeur.** Les captures passent par `--screenshot-out` ; les lancements automatisés passent `--diagnostics-out` vers un dossier hors dépôt et ne laissent aucun fichier dans `Projects/`.
- **Aucun fichier `.cutscene`, `.world` ou `AssetInfos.json` de `Projects/` n'est modifié.**
- **Langue** : ce plan en français ; code, messages de commit, docs de `docs/` et ADR en anglais.
- Rappel moteur : pas d'allocation, de LINQ ni de closure dans les chemins chauds ; le runtime ne dépend pas de l'éditeur ; API publique additive.

## Légende des statuts

- ⏳ Todo : pas encore commencé.
- 🚧 In progress : en cours de modification locale.
- 🧪 Needs testing : code écrit, validation incomplète ou en attente.
- ✅ Done : code validé, build/tests OK, commit effectué.
- ⚠️ Blocked : bloqué par une erreur non résolue ou une décision manquante.

## Validation globale

- `dotnet build CasaEngine.MonoGame.sln` et `dotnet build CasaEngine.Editor.MonoGame.sln` : 0 erreur.
- `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj` : tous verts, aucun test existant modifié sauf mention dans une tâche. Référence avant ce plan : 5891 tests.
- Captures automatisées (T2.5) : sur `loop.cutscene`, 0,5 s puis 2 s après le début de la lecture, l'entité `orb-left` est en cours de déplacement puis arrivée en x = -60, sur la capture comme dans les diagnostics ; sur `exit.cutscene`, la ligne d'état annonce le changement de monde et le monde courant de l'éditeur est inchangé.
- Retour arrière : un commit par tâche sur la branche ; annuler une tâche revient à annuler son commit.
- `git diff c365b514 -- Projects/` : vide.

---

## Phase 0 — Décisions

### ✅ T0.1 — ADR-0096 et clôture de la recette précédente

> Validation (2026-11-18) : ADR-0096 écrit selon le modèle et ajouté à l'index ; mention de P9 sur ADR-0014 (fichier et index) ; note de recette sous T3.2 de `cutscene-property-editing-tasks.md` ; ligne ajoutée à `ai-agent/README.md`. `rg -n -i "ff7|decomp"` sur les fichiers de l'ADR, de l'index et des deux plans hors lignes qui citent la commande elle-même : aucun résultat ; le lien de l'index pointe sur un fichier existant.

- Objectif : enregistrer D1 → D5 et P1 → P4, et noter le résultat de la recette de la tranche « propriétés ».
- Fichiers : `docs/decisions/0096-<short-title>.md` (créé), `docs/decisions/README.md`, `docs/decisions/0014-generic-timeline-base-for-the-editor.md` (ligne `Status` seule, P9), `ai-agent/tasks/cutscene-property-editing-tasks.md` (note sous T3.2), `ai-agent/README.md` (ligne de ce plan), ce plan.
- Étapes :
  1. Écrire l'ADR en anglais avec le skill `adr`, statut `Accepted`. Contenu : document à quatre zones ; arbre conservé, timeline projetée, section temporelle plus tard (D1) ; aperçu dans une copie isolée du monde ; point d'injection `ICutsceneEffectHost` (P1, P2) ; comportement des effets globaux en aperçu (D3) ; choix du monde (D4) ; limite des animations figées (D2) ; `MoveTo` qui échoue au lieu de lever une exception (P4). L'ADR dit explicitement qu'il remplace la décision 2 d'ADR-0014 et lève l'exclusion du plan archivé `timeline-generic-agent-plan.md`, parce que l'adaptateur de timeline projettera les nœuds de l'arbre et non un modèle plat. Il complète les points reportés d'ADR-0095.
  2. Ajouter la ligne d'index, et la mention de P9 sur ADR-0014.
  3. Sous T3.2 du plan `cutscene-property-editing-tasks.md`, noter : recette de l'auteur du 2026-11-18, refusée sur l'ergonomie (pas de monde, champs texte), suite dans ce plan. Le statut reste 🧪.
  4. Ajouter ce plan au tableau de `ai-agent/README.md`.
- Validation : `rg -n -i "ff7|decomp"` sur les fichiers touchés ne renvoie rien ; les liens de l'index pointent sur des fichiers existants.
- Commit : `docs(decisions): record the cutscene editor preview and timeline direction`

---

## Phase 1 — Runtime (`CasaEngine`)

### ⏳ T1.1 — `MoveTo` échoue proprement

- Objectif : une lecture dont un `MoveTo` dépasse son délai, vise une entité absente ou une entité sans `CharacterControllerComponent` se termine par `Ended` avec l'état `Failed`, comme `NavigateTo` (P4).
- Fichiers : `CasaEngine/Framework/Cutscenes/CutsceneActionCoroutineFactory.cs`, `CasaEngine.Tests/Cutscenes/` (tests ajoutés).
- Étapes :
  1. Remplacer les trois `throw` de `MoveTo` (`:54-57`, `:589-597`) par le chemin `FailPlay` déjà utilisé par `NavigateTo`, avec les mêmes messages.
  2. Tests : pour chacun des trois cas, `Ended` est levé une fois avec `Failed`, et `GetDebugSnapshot().State` vaut `Failed`.
- Validation : build `CasaEngine.MonoGame.sln` ; `dotnet test` vert ; aucun test existant modifié. Si un test existant échoue parce qu'il attendait l'exception : ⚠️ Blocked.
- Commit : `fix(cutscenes): fail the play instead of throwing when MoveTo cannot run`

### ⏳ T1.2 — Point d'injection des effets globaux

- Objectif : permettre à un hôte de fournir le son et les fondus et de refuser les demandes adressées au jeu, sans changer le comportement par défaut (P1, P2).
- Fichiers : `CasaEngine/Framework/Cutscenes/ICutsceneEffectHost.cs` (créé), `CutsceneDirector.cs`, `CutsceneActionCoroutineFactory.cs`, `CasaEngine.Tests/Cutscenes/` (tests ajoutés).
- Étapes :
  1. Créer l'interface publique `ICutsceneEffectHost` avec ses trois membres et leur doc XML.
  2. Ajouter `public ICutsceneEffectHost EffectHost { get; set; }` à `CutsceneDirector`.
  3. Dans la fabrique : `GetAudioService` et `GetScreenEffectService` rendent le service de l'hôte quand un hôte est posé, celui du jeu sinon. Avant `ChangeWorld`, `OpenScreen`, `PlayVideo` et `ExitOnInput`, si un hôte est posé et que `AllowGameRequest` rend `false`, l'action est sautée. Ce contrôle passe **avant** le test « pas de `GameManager` » et avant `ExitOnInputResolver`, pour qu'un monde sans jeu donne un refus et non une erreur. `Repeat` et `Parallel` repassent par `ExecuteAction` : le contrôle couvre donc les branches imbriquées.
  4. Tests avec un hôte de test : chaque type d'action est présenté à `AllowGameRequest` ; sur refus, `GameManager.PendingWorldToLoad` reste nul, aucune veille n'est armée, la lecture continue jusqu'à `Completed` ; un `FadeScreen` utilise le `ScreenEffectService` de l'hôte et dure sa durée ; un hôte dont `Audio` est nul ne joue rien.
- Validation : build `CasaEngine.MonoGame.sln` ; `dotnet test` vert ; les tests existants de `CasaEngine.Tests/Cutscenes` passent sans modification, ce qui prouve le comportement par défaut inchangé.
- Commit : `feat(cutscenes): let a host supply or refuse the game-wide effects of a play`

---

## Phase 2 — Éditeur

### ⏳ T2.1 — Choix du monde d'aperçu (`CasaEngine.EditorServices`)

- Objectif : logique pure qui donne la liste des mondes candidats et le monde par défaut d'une cutscene (P5), et qui lit et écrit le choix mémorisé (P6).
- Fichiers : `CasaEngine.EditorServices/Cutscenes/CutscenePreviewWorldResolver.cs`, `CutscenePreviewSettings.cs` (créés), tests dans `CasaEngine.Tests/EditorServices/Cutscenes/`.
- Étapes :
  1. `CutscenePreviewWorldResolver` : à partir de la liste des mondes (identifiant, nom de fichier, identifiant de cutscene de démarrage), de l'identifiant de la cutscene, de `FirstWorldLoaded` et du choix mémorisé, rend la liste ordonnée et le monde par défaut. Aucun accès disque dans cette classe.
  2. Lecture de `startup_cutscene_asset_id` d'un fichier `.world` sans charger le monde : lecture de la seule clé dans le JSON.
  3. `CutscenePreviewSettings` : charge et enregistre le fichier de P6 ; un fichier absent ou illisible donne des réglages vides, avec un message de log pour le second cas.
- Validation : tests des quatre rangs de P5, d'un choix mémorisé qui ne correspond plus à aucun monde, d'un projet sans monde, de l'aller-retour du fichier de réglages.
- Commit : `feat(editor-services): resolve and remember the preview world of a cutscene`

### ⏳ T2.2 — Hôte d'aperçu (`CasaEngine.EditorServices`)

- Objectif : l'implémentation d'`ICutsceneEffectHost` pour l'aperçu, conforme à D3 et P8.
- Fichiers : `CasaEngine.EditorServices/Cutscenes/CutscenePreviewEffectHost.cs` (créé), tests dans `CasaEngine.Tests/EditorServices/Cutscenes/`.
- Étapes :
  1. `Audio` rend nul. `ScreenEffects` rend un `ScreenEffectService` propre à l'hôte, avancé par une méthode `Update(float elapsedSeconds)` de l'hôte.
  2. `AllowGameRequest` rend toujours `false` et enregistre l'action dans une liste lisible par l'interface (type, cible). La liste ne contient qu'une entrée par action : une action refusée à chaque tour d'un `Repeat` sans fin n'y figure qu'une fois. Un `ChangeWorld` lève en plus un indicateur « fin demandée » (P3).
  3. Méthode de remise à zéro, appelée à chaque nouvelle lecture.
- Validation : tests avec un vrai `CutsceneDirector` : `exit.cutscene` du sample ne demande aucun monde et lève l'indicateur ; une cutscene avec `ExitOnInput` puis `Wait` se termine en `Completed` et l'action est enregistrée ; un `FadeScreen` de 0,5 s avance le service de l'hôte.
- Commit : `feat(editor-services): add the effect host of the cutscene preview`

### ⏳ T2.3 — Session d'aperçu (`CasaEngine.Editor`)

- Objectif : charger une copie d'un monde, y jouer la cutscene éditée, l'arrêter, la redémarrer.
- Fichiers : `CasaEngine.Editor/Runtime/PreviewWorldDriver.cs` (surcharge additive), `CasaEngine.Editor/Runtime/CutscenePreviewSession.cs` (créé).
- Étapes :
  1. `PreviewWorldDriver` : ajouter une façon de reconstruire à partir d'une fabrique de monde, pour un monde issu de `LoadCopy<World>` puis `LoadContent`. Les appelants actuels ne changent pas.
  2. `CutscenePreviewSession` : possède le pilote en mode `Continuous` et un `CutscenePreviewEffectHost`. `SetWorld` charge une copie du monde choisi. `Play(CutsceneAsset)` reconstruit le monde (P7), pose l'hôte sur le `CutsceneDirector` du monde, puis joue une **copie** de l'asset obtenue par `CutsceneAssetJsonSerializer.Save` puis `Load`. `Stop` arrête le directeur. `Tick` avance le pilote et l'hôte, puis arrête la lecture si l'indicateur de P3 est levé. Le premier pas après une reconstruction du monde est borné à 1/30 s, pour que le temps de chargement ne fasse pas sauter la lecture.
  3. État lisible par l'interface : état du directeur, raison d'échec, actions signalées, couleur du fondu en cours.
  4. `Dispose` efface le monde d'aperçu.
- Validation : build `CasaEngine.Editor.MonoGame.sln` ; `dotnet test` vert. La validation visuelle est celle de T2.5.
- Commit : `feat(editor): play a cutscene in an isolated copy of a world`

### ⏳ T2.4 — Document cutscene avec aperçu et barre de transport

- Objectif : le document `.cutscene` montre l'aperçu et ses commandes, à côté des propriétés existantes.
- Fichiers : `CasaEngine.Editor/Controls/CutsceneAssetInspectorPanel.cs`, `CasaEngine.Editor/GameEditor.cs`, `CasaEngine.Tests/Editor/CutsceneAssetInspectorPanelTests.cs` (tests ajoutés).
- Étapes :
  1. Le constructeur actuel `CutsceneAssetInspectorPanel(MGWindow)` est conservé : sans runtime, le panneau n'a pas d'aperçu et se comporte comme aujourd'hui. Une surcharge reçoit en plus `GraphicsDevice`, `HostedEditorGameAdapter` et `IWindowInputSource`, comme `Animation2dAssetInspectorPanel`.
  2. Avec runtime : barre de transport en haut (liste déroulante des mondes, Play, Stop, Restart, ligne d'état), `WorldViewportPanel` d'aperçu, et les propriétés existantes dans une colonne à droite. La ligne d'état donne l'état de la lecture, les actions signalées et la limite de D2 ; son texte n'est reconstruit que quand cet état change, jamais à chaque frame. L'aperçu utilise la caméra 2D de face de l'éditeur (`UseFront2dCamera`), cadrée sur l'ensemble des entités du monde par `FocusBounds` (O2).
  3. `GameEditor.TryOpenCutsceneAsset` utilise la surcharge ; la boucle de mise à jour appelle `Update(gameTime)` des panneaux cutscene, à côté des panneaux Animation2D ; la fermeture libère la session.
  4. Le changement de monde dans la liste est mémorisé (T2.1).
- Validation : build éditeur ; `dotnet test` vert, les 16 tests existants du panneau inchangés ; tests ajoutés sur ce qui est testable sans carte graphique. Capture d'écran automatisée du document ouvert (sans lecture), envoyée à l'auteur.
- Commit : `feat(editor): show a world preview and transport controls in the cutscene document`

### ⏳ T2.5 — Lecture automatisée pour la validation à distance

- Objectif : lancer l'éditeur, ouvrir une cutscene, démarrer l'aperçu et capturer l'écran sans entrée utilisateur (D7).
- Fichiers : `CasaEngine.Editor/EditorAutomationOptions.cs`, `CasaEngine.Editor/GameEditor.cs`, `CasaEngine.Tests/Editor/EditorAutomationOptionsTests.cs`.
- Étapes :
  1. Option `--cutscene-preview-play` : après l'ouverture de l'asset de `--open-asset` (`TryApplyAutomationAssetOpen`, `GameEditor.cs:7666-7687`), si c'est une cutscene, démarrer la lecture de l'aperçu. Elle compte dans `HasAutomation`.
  2. L'instant du début de lecture entre dans le calcul de `readyAt` (`GameEditor.cs:7131-7139`) : `--capture-delay` se compte alors **depuis le début de la lecture**.
  3. Écrire dans le fichier de diagnostics : l'état de l'aperçu, les actions signalées, le nom et la position de chaque entité du monde d'aperçu, et le nom du monde courant de l'éditeur.
  4. Test d'analyse de l'option.
- Validation : avec `--diagnostics-out` vers un dossier hors dépôt :
  1. `loop.cutscene` : `MoveSprite` y amène `orb-left` de x = -190 à x = -60 à la vitesse 120, soit environ 1,08 s (`Projects/SpriteCutsceneSample/Cutscenes/loop.cutscene:23-99`). Avec `--capture-delay 0.5`, les diagnostics donnent pour `orb-left` un x strictement entre -190 et -60 ; avec `--capture-delay 2`, x = -60. Les deux captures montrent ces deux positions ;
  2. `exit.cutscene`, `--capture-delay 3` (son `FadeScreen` dure 1 s, puis vient `ChangeWorld`) : la ligne d'état annonce le changement de monde vers `EndWorld.world`, et les diagnostics montrent que le monde courant de l'éditeur est toujours `SpriteWorld` ;
  3. `git status` ne montre aucun nouveau fichier sous `Projects/`.
  Les captures sont envoyées à l'auteur.
- Commit : `feat(editor): add an automation switch that plays the cutscene preview`

---

## Phase 3 — Documentation et recette

### ⏳ T3.1 — Documentation

- Objectif : `docs/editor/cutscene-editor.md` décrit l'aperçu, ses commandes, le choix du monde, les effets signalés et les limites.
- Fichiers : `docs/editor/cutscene-editor.md`, ce plan, `ai-agent/README.md`.
- Validation : relecture ; `rg -n -i "ff7|decomp"` sur le diff de la branche ne renvoie rien.
- Commit : `docs(editor): document the cutscene preview`

### ⏳ T3.2 — Recette de l'auteur

- Objectif : validation à distance sur les captures de T2.4 et T2.5, puis recette réelle dans l'éditeur à la fin (D7).
- Cette tâche reste 🧪 tant que l'auteur n'a pas fait sa recette réelle.

---

## Tranches suivantes (un plan chacune)

| Tranche | Contenu | Résultat visible |
|---|---|---|
| B | Arbre des actions (`MGTreeView`) et détails typés : champs numériques, couleur, listes, sélecteurs d'entité et d'asset. Plus de champ texte pour un type connu. | Édition confortable des propriétés |
| C | Timeline en lecture seule projetée de l'arbre, tête de lecture vivante. Runtime : temps écoulé et événements de début et de fin d'action. | La cutscene vue dans le temps |
| D | Pause et déplacement de la tête de lecture, par relecture accélérée depuis le début. | Aller et retour dans la cutscene |
| E | Édition de la structure : ajouter, supprimer, dupliquer, regrouper, réordonner. | Construire une cutscene à la souris |
| F | Action « section temporelle » et placement libre des clips (D1). ADR de format. | Placement libre façon Sequencer |
| G | Bac à sable de l'aperçu : fondus dessinés, bouton du son, simulation de l'entrée, politique d'exécution par monde pour les animations (D2, D3). | Aperçu fidèle au jeu |

## Points ouverts

| Réf | Sujet | Tâche concernée |
|---|---|---|
| O1 | La résolution virtuelle propre au monde (`UseWorldVirtualResolution`, ADR-0087) est un réglage du jeu entier ; l'aperçu ne l'applique pas. Effet sur le cadrage à constater sur les captures. | T2.4 |
| O2 | Caméra de l'aperçu : `WorldViewportPanel` n'utilise jamais la caméra du monde (`WorldViewportPanel.cs:793-815`, `:1841`). La tranche A cadre la caméra 2D de face sur l'ensemble des entités ; le cadrage du jeu est à juger sur les captures. | T2.4 |
| O3 | En aperçu, `FadeMusic` n'attend pas sa durée, faute de service de son : le minutage diffère du jeu pour les cutscenes qui l'utilisent. | T2.2, tranche G |
| O4 | Coût d'un monde d'aperçu par document ouvert : non mesuré. Le monde d'aperçu partage la politique d'exécution du jeu de l'éditeur : pendant le mode Play de l'éditeur, ses scripts et sa physique tourneraient, comme pour l'aperçu Animation2D. | T2.3 |
| O5 | Confirmation de P1 → P9 par l'auteur. | Toutes |

## Hors périmètre

- Timeline, arbre, détails typés, édition de structure : tranches B à F.
- Pause, temps courant et déplacement dans la lecture.
- Dessin des fondus, son en aperçu, simulation de l'entrée : tranche G.
- Animations, physique et scripts dans l'aperçu (D2).
- Recherche du monde par les enchaînements d'`ExitOnInput`.
- Question « modifications non enregistrées » à la fermeture d'un onglet.
- Tout changement du format `.cutscene`.
