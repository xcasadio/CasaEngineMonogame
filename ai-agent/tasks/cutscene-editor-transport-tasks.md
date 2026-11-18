# Plan agent IA — Éditeur de cutscenes : pause, pas à pas et déplacement de la tête de lecture (tranche D)

Quatrième tranche du programme « éditeur de cutscenes visuel » (ADR-0096, plans [cutscene-editor-preview-tasks.md](cutscene-editor-preview-tasks.md), [cutscene-editor-outliner-details-tasks.md](cutscene-editor-outliner-details-tasks.md) et [cutscene-editor-timeline-tasks.md](cutscene-editor-timeline-tasks.md)).
Elle rend la tête de lecture utilisable : on met l'aperçu en pause, on avance ou recule d'une image, et on clique dans la timeline pour voir la cutscene à cet instant. La timeline reste une **vue calculée depuis l'arbre** : on n'y déplace encore aucune action.

Les décisions D1 → D5 ci-dessous ont été arbitrées avec l'auteur : **ce plan les applique, il ne les rediscute pas**.
L'auteur a validé les captures de la tranche C le 2026-11-18 (« Approuvé continue en mode AUTo ») : ce plan est approuvé d'avance. Les points P1 → P15 sont des arbitrages de l'agent, à confirmer par l'auteur à la recette.

Ce fichier doit être mis à jour pendant le travail : l'icône au début de chaque tâche indique son statut courant.

## Objectif

Quand un document `.cutscene` est actif et qu'un monde d'aperçu est chargé :

- un bouton **Play / Pause** fige l'aperçu et le reprend là où il s'était arrêté ;
- deux boutons avancent et reculent d'**une image** (1/60 s) ;
- un **clic dans la règle** de la timeline, ou dans le vide de la zone des pistes, amène l'aperçu à cet instant et le laisse en pause ; faire **glisser** dans la règle déplace la tête de lecture en continu ;
- **Stop** remet l'aperçu à son état de départ, tête de lecture à 0 ;
- à la fin d'une lecture, la tête de lecture **reste à la fin** ;
- modifier une propriété pendant une pause **rejoue jusqu'au même instant** : l'aperçu montre le résultat de la modification sous la tête de lecture.

## État vérifié du dépôt (2026-11-18)

Branche `chantier/cutscene-property-editing`, tête `18bf386e`, 39 commits depuis `main` `c365b514`, non fusionnée.
Fichiers non suivis préexistants, à ne jamais indexer : `Projects/PixelUiSample/.casaeditor/`, `Projects/SpriteCutsceneSample/.casaeditor/`.
Référence des tests avant ce plan : 6134 tests verts.

Session d'aperçu (`CasaEngine.Editor/Runtime/CutscenePreviewSession.cs`) :

- `Play(asset)` arrête la lecture en cours, reconstruit le monde depuis son asset (`TryLoadWorld`), copie l'asset, associe la copie à l'arbre édité (`CutsceneActionTree.MapByShape`), appelle `_timings.Begin`, puis `_director.Play(copy)` (`:161-200`).
- `Stop()` n'appelle que `_director.Stop()` : le monde garde l'état laissé par la lecture (`:202-206`).
- `Tick(GameTime)` borne le premier pas après un chargement à 1/30 s, puis appelle `_driver.Tick`, `_host.Update`, `UpdateMoveTracks`, et arrête la lecture par `_director.Stop()` si l'hôte a reçu une demande de changement de monde (`:209-233`). C'est le seul endroit qui fait avancer le temps du monde d'aperçu ; `PreviewWorldDriver.RefreshNow` appelle aussi `World.Update(0f)` après une reconstruction (`PreviewWorldDriver.cs:85-88`).
- Seul `Play` remet à zéro l'hôte d'effets (`_host.Reset()` : fondu, demandes refusées, demande de fin), `EndedByWorldChange` et `FailureReason` (`:173-176`). `OnEnded` recopie l'état de fin du directeur (`:285-291`). L'arrêt sur demande de changement de monde ne se produit que si la lecture court encore au moment du contrôle (`:228-232`) : elle finit alors à l'état `Stopped` avec `EndedByWorldChange` à vrai. Quand le `ChangeWorld` refusé est la dernière action, la lecture est déjà terminée à l'état `Completed` dans la même mise à jour, et `EndedByWorldChange` reste faux (une action refusée par l'hôte sort par un simple `break`, `CutsceneActionCoroutineFactory.cs:206-211`).
- `ElapsedSeconds` et `ElapsedFrames` lisent l'horloge du directeur du monde courant (`:81-84`). `TryLoadWorld` remplace le monde, donc le directeur (`:247-271`).
- `ClearTimings()` est appelé par le document à chaque changement (`:93-97` ; `CutsceneAssetInspectorPanel.cs:337`).
- **Aucun test ne construit de `CutscenePreviewSession`** (`rg "CutscenePreviewSession" CasaEngine.Tests` : aucun résultat). Les tests du document utilisent le constructeur sans aperçu (`new CutsceneAssetInspectorPanel(harness.Window)`).

Pilote du monde d'aperçu (`CasaEngine.Editor/Runtime/PreviewWorldDriver.cs`) : `Tick(GameTime)` appelle `World.Update((float)gameTime.ElapsedGameTime.TotalSeconds)` (`:90-100`). Il n'existe pas de surcharge qui prend des secondes.

Directeur (`CasaEngine/Framework/Cutscenes/CutsceneDirector.cs`) :

- `PlayElapsedSeconds` et `PlayElapsedFrames` ne sont remis à zéro que dans `Play` (`:105-106`) ; `AdvancePlayClock` ne les avance que pendant une lecture en cours et non terminée (`:262-272`). Après la fin d'une lecture, ils gardent donc leur dernière valeur.
- `Stop()` passe l'état à `Stopped` et lève `Ended` (`:122-139`) ; sur une lecture déjà terminée il sort sans rien lever (`:125-128`).

Samples (`Projects/SpriteCutsceneSample/Cutscenes/`) : `loop.cutscene` est une boucle sans fin. `exit.cutscene` est un `FadeScreen` d'une seconde puis un `ChangeWorld` en dernière action : dans l'aperçu elle se termine à l'état `Completed`, `EndedByWorldChange` faux, avec « Skipped: ChangeWorld -> EndWorld.world » dans la ligne d'état (mesuré à la tranche A, `cutscene-editor-preview-tasks.md:239`). `end.cutscene` finit aussi par un `ChangeWorld`. **Aucun sample ne finit à l'état `Stopped` avec `EndedByWorldChange`** : ce cas ne peut être prouvé que par un test.

Relevé des durées (`CasaEngine.EditorServices/Cutscenes/CutscenePlayTimings.cs`) : `Begin` efface les mesures précédentes et incrémente `Version` (`:29-39`) ; une durée déjà mesurée n'est jamais remplacée (`:73`, `:93`).

Projection (`CasaEngine.EditorServices/Cutscenes/`) : `CutsceneTimelineLayout.MapPlayTime` rend le temps tel quel, sauf pour une boucle sans fin où il le replie sur le tour dessiné (`CutsceneTimelineLayout.cs:168-182`). `CutsceneTimelineProjection.FramesPerSecond` vaut 60 (`CutsceneTimelineProjection.cs:12`).

Contrôle Timeline générique (`CasaEngine.Editor/Controls/Timeline/`) :

- Un relâchement du bouton gauche dans la règle appelle `SetCurrentTimeSeconds` puis `NotifyTimeScrubbed` (`TimelineRuler.cs:173-200`). La règle n'écoute que `LMBReleasedInside` et `Scrolled` (`:25-26`) : **il n'y a pas de glisser dans la règle**.
- Un clic dans le vide de la zone des pistes fait la même chose (`TimelineViewport.cs:285-292`). La zone des pistes écoute `DragStart`, `Dragged` et `DragEnd` pour déplacer des éléments (`:53-55`).
- `SetCurrentTimeSeconds` borne le temps à `[0, fin de la timeline]` (`TimelineControl.cs:363-378`). Événement `TimeScrubbed` (`:94`).

Panneau de la timeline (`CasaEngine.Editor/Controls/CutsceneTimelinePanel.cs`) :

- `OnTimelineTimeScrubbed` remet la tête de lecture au temps de la lecture (`:320-324`).
- `Update` pousse `MapPlayTime(temps de lecture)` pendant une lecture et 0 sinon (`:146-153`).
- Test existant qui décrit ce comportement : `ScrubbingTheRuler_DoesNotMoveThePlayhead` (`CasaEngine.Tests/Editor/CutsceneTimelinePanelTests.cs:264-265`), sur un document sans aperçu.

Document (`CasaEngine.Editor/Controls/CutsceneAssetInspectorPanel.cs`) :

- Barre de transport : liste des mondes, boutons `Play`, `Stop`, `Restart`, texte du temps (`:610-659`). `Play` et `Restart` appellent tous deux `PlayPreview` (`:645-647`).
- `RefreshTimeText` écrit « Playing » ou « Stopped », et 0 quand rien ne joue (`:536-557`).
- `PlayPreview` appelle `_preview.Play`, puis `ApplyPreviewWorldToViewport`, qui remplace le monde du viewport **et recadre la caméra** sur les entités (`:854-864`, `:873-918`).
- `RaiseDocumentChanged` efface les mesures et relance la lecture si elle était en cours (`:326-344`).
- `Update` appelle `_preview.Tick` une fois par frame (`:520-530`).

Viewport (`CasaEngine.Editor/Controls/WorldViewportPanel.cs`) : `SetWorldOverride` sort tout de suite si le monde est le même objet ; sinon, pour tout monde d'aperçu non nul, il **remet la caméra à son état de départ** (`_cameraController.SetState`, `:687-702`). Seule la sauvegarde de la caméra du monde principal est réservée à l'entrée en aperçu. Chaque reconstruction du monde d'aperçu remet donc la caméra à zéro, puis `FocusBounds` la recadre. Le contrôleur de caméra sait capturer et restaurer son état (`CaptureState`, `RestoreState`, `:682`, `:705`).

Automatisation : options `--cutscene-preview-play`, `--cutscene-select-action`, `--cutscene-add-action`, `--cutscene-reactivate` (`CasaEngine.Editor/EditorAutomationOptions.cs:34-37`, `:208-223`) ; `TryApplyAutomationCutscenePreviewPlay` (`GameEditor.cs:7836-7854`) ; le délai de capture se compte depuis la dernière étape d'automatisation (`:7255-7281`).

## Décisions verrouillées

| Réf | Décision |
|---|---|
| D1 | L'arbre d'actions reste le modèle enregistré ; la timeline en est une projection. Le format `.cutscene` ne change pas. |
| D2 | Cette tranche apporte la pause et le déplacement de la tête de lecture, par relecture depuis le début. Réordonner et placer librement restent pour les tranches E et F. |
| D3 | Le temps s'affiche en secondes et en images. |
| D4 | Mode autonome : plan approuvé d'avance, exécution enchaînée, aucun push ni fusion. |
| D5 | L'auteur valide à distance sur captures d'écran ; sa recette réelle a lieu à la fin. |

## Arbitrages de l'agent, à confirmer

| Réf | Arbitrage |
|---|---|
| P1 | La pause est un arrêt de l'avance du monde d'aperçu, côté éditeur. Le runtime `CasaEngine/` n'est pas touché : aucune API de pause n'est ajoutée au directeur. |
| P2 | Aller à un instant, c'est rejouer : le monde est reconstruit depuis son asset, la copie de la cutscene est lancée, puis le monde avance par pas fixes de 1/60 s jusqu'à ce que l'horloge de lecture atteigne l'instant visé ; l'aperçu passe alors en pause. Quand l'instant visé est après l'instant courant, que la lecture court encore et que le document n'a pas changé depuis son lancement, l'avance repart de l'état courant, sans reconstruire le monde. |
| P3 | L'avance rapide est répartie sur les frames de l'éditeur : chaque frame y consacre au plus 8 ms, et fait au moins un pas. Pendant ce temps l'état est « Seeking » et le viewport montre l'avance. L'éditeur reste donc réactif quelle que soit la longueur de la cutscene. |
| P4 | L'instant visé est un temps de lecture égal au temps de la timeline (premier tour pour une boucle sans fin), borné entre 0 et la durée de la projection. Si la lecture se termine avant (échec, demande de changement de monde, cutscene plus courte), l'avance s'arrête là. |
| P5 | La logique du transport (modes Lecture, Pause, Recherche ; budget ; pas fixes) vit dans `CasaEngine.EditorServices`, dans une classe testable sans l'éditeur, parce qu'aucun test ne peut construire la session d'aperçu. La session ne fait qu'exécuter les pas qu'elle demande. |
| P6 | Barre de transport : un bouton `Play` / `Pause` dont le libellé suit l'état, `Stop`, `Restart`, un bouton image précédente et un bouton image suivante. `Play` pendant une pause reprend ; après une fin ou un `Stop`, il relance depuis le début. Image suivante : pause, puis un pas de 1/60 s. Image précédente : aller à l'instant courant moins 1/60 s. |
| P7 | `Stop` remet l'aperçu à l'état de départ : le monde est reconstruit depuis son asset et la tête de lecture revient à 0. Cela remplace le comportement de la tranche A, où le monde gardait l'état laissé par la lecture. |
| P8 | Quand une lecture se termine d'elle-même (fin normale, échec, ou demande de changement de monde refusée par l'aperçu), le monde garde son état final et la tête de lecture reste au temps de fin ; la barre de transport affiche « Ended ». « Stopped » ne s'affiche qu'avant toute lecture et après un `Stop`. La table exacte est dans T2.2. |
| P9 | Un changement du document pendant une pause ou une recherche rejoue jusqu'au même instant et reste en pause. Pendant une lecture, il la relance depuis le début, comme aujourd'hui. |
| P10 | Un clic dans la règle ou dans le vide de la zone des pistes déplace la tête de lecture. Le glisser dans la règle est une option du contrôle générique, **désactivée par défaut** : Animation2D ne change pas. |
| P11 | Aller à un instant, reculer d'une image et `Stop` gardent la caméra de l'aperçu telle que l'utilisateur l'a laissée, alors qu'ils reconstruisent le monde ; `Play` depuis le début et `Restart` gardent le recadrage actuel. Le viewport reçoit pour cela un moyen de remplacer le monde d'aperçu en gardant sa caméra. |
| P12 | Les durées mesurées sont gardées d'une relecture à l'autre tant que le document ne change pas : seuls `Play` depuis le début et `Restart` repartent d'un relevé vide. Sans cela les barres mesurées redeviendraient « estimées » à chaque retour en arrière. |
| P13 | Automatisation : `--cutscene-preview-seek <secondes[,secondes…]>` (va à chaque instant dans l'ordre, chacun après la fin du précédent), `--cutscene-preview-step <n>` (n images en avant, après les recherches), `--cutscene-preview-stop` (Stop, après le reste). Le délai de capture se compte depuis la fin de la dernière étape. |
| P14 | Aucun raccourci clavier dans cette tranche (espace, flèches) : ils demandent de toucher au routage global des entrées de l'éditeur. |
| P15 | Les pas d'une recherche sont fixes (1/60 s) alors que ceux d'une lecture suivent les frames de l'éditeur. Une action comptée en images (`WaitFrames`, `FadeScreenFrames`) peut donc tomber à un instant légèrement différent entre une lecture et une recherche. C'est accepté et documenté. |

## Règles d'exécution pour l'agent

- **Branche `chantier/cutscene-property-editing`**. Ne jamais committer sur `main`.
- **Une seule tâche à la fois.** Avant de commencer une tâche, remplacer son icône `⏳` par `🚧`. À la fin, lancer la validation indiquée, remplacer l'icône par `✅`, `🧪` ou `⚠️`, ajouter une courte note de validation sous la tâche, puis **créer un commit dédié** qui inclut la mise à jour de ce fichier.
- **Un commit par tâche**, atomique et compilable, message en anglais au format `type(area): summary`.
- **Ne jamais pousser.** Le merge sur `main` reste une décision humaine.
- **Ne rien inventer** : toute API utilisée existe dans le dépôt et a été lue. Sinon : ⚠️ Blocked, question dans « Points ouverts », arrêt.
- **Build obligatoire** avant ✅ : `dotnet build CasaEngine.Editor.MonoGame.sln`, et `dotnet build CasaEngine.MonoGame.sln` pour T1.1 et T1.2 ; **tests** `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj`.
- **Ne jamais indexer** les fichiers préexistants non suivis : `git add` fichier par fichier.
- **Aucune entrée clavier ou souris envoyée à l'éditeur.** Les captures passent par les options d'automatisation, avec `--diagnostics-out` hors dépôt. Aucun lancement automatisé n'enregistre un asset.
- **Aucun fichier de `Projects/`, de `MGUI/` ni de `CasaEngine/` (runtime) n'est modifié.**
- **Aucun test existant n'est modifié**, à une exception possible : un test qui décrit le comportement remplacé par P7, P8 ou P10. Dans ce cas il est réécrit pour le nouveau comportement et la note de validation le nomme.
- **Langue** : ce plan en français ; code, commits, `docs/` et ADR en anglais.
- Rappel moteur : pas d'allocation, de LINQ, de closure ni de formatage de chaîne dans `Update`, `Draw` et le layout. La boucle d'avance rapide tourne dans `Update` : elle n'alloue rien par pas (pas de `GameTime` créé par pas, délégué d'horloge mis en cache). Le texte de la barre de transport ne se réécrit que quand ce qu'il montre change.

## Légende des statuts

- ⏳ Todo : pas encore commencé.
- 🚧 In progress : en cours de modification locale.
- 🧪 Needs testing : code écrit, validation incomplète ou en attente.
- ✅ Done : code validé, build/tests OK, commit effectué.
- ⚠️ Blocked : bloqué par une erreur non résolue ou une décision manquante.

## Validation globale

- Les deux solutions buildent à 0 erreur.
- `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj` : tous verts ; référence avant ce plan : 6134 tests.
- `git diff 18bf386e -- Projects/ MGUI/ CasaEngine/` : vide.
- Captures automatisées (T2.6) : pause à 0,5 s et à 2 s, retour en arrière, pas à pas, fin de lecture, fin pendant une recherche, Stop en cours et après la fin.
- Retour arrière : un commit par tâche ; annuler une tâche revient à annuler son commit.

---

## Phase 0 — Décisions

### ✅ T0.1 — ADR-0099

- Objectif : enregistrer P1 → P15.
- Fichiers : `docs/decisions/0099-<short-title>.md` (créé), `docs/decisions/README.md`, `ai-agent/README.md` (ligne de ce plan), ce plan.
- Étapes :
  1. Écrire l'ADR en anglais avec le skill `adr`, statut `Accepted`, date 2026-11-18, source : ce plan, ADR-0096 et ADR-0098. Contenu : la pause est un gel du monde d'aperçu côté éditeur, sans API runtime (P1) ; aller à un instant se fait par relecture à pas fixes, avec reprise depuis l'état courant quand c'est possible, et un budget par frame (P2 → P4) ; la logique du transport est dans les services d'édition (P5) ; sémantique des boutons, de `Stop` et de la fin de lecture, en disant que P7 remplace le point d'ADR-0096 sur l'état laissé par une lecture et que P10 remplace le point « Scrubbing » d'ADR-0098 (P6 → P10) ; caméra, mesures gardées, automatisation (P11 → P13) ; limites acceptées (P14, P15).
  2. Ligne d'index ; ligne de ce plan dans `ai-agent/README.md`.
- Validation : les liens de l'index pointent sur des fichiers existants ; aucun nom de jeu ni de projet tiers dans les fichiers touchés.
- Commit : `docs(decisions): record the pause, step and seek of the cutscene preview`
- Note de validation (2026-11-18) : `docs/decisions/0099-the-cutscene-preview-pauses-steps-and-seeks-by-replay.md` créé (statut `Accepted`), ligne ajoutée à `docs/decisions/README.md` et à `ai-agent/README.md`. P1 → P15 sont tous couverts ; l'ADR dit que P7 remplace le point d'ADR-0096 sur l'état laissé par une lecture et que P10 remplace le point « Scrubbing » d'ADR-0098. Les liens de l'index pointent sur des fichiers existants (vérifié) ; aucun nom de jeu ni de projet tiers dans les fichiers touchés (vérifié par recherche). Aucun code touché : ni build ni tests lancés (référence : 6134 tests).

---

## Phase 1 — Services d'édition (`CasaEngine.EditorServices`)

### ✅ T1.1 — Transport de l'aperçu

- Objectif : une classe qui décide, à chaque frame de l'éditeur, de combien de pas le monde d'aperçu avance (P1 → P5), testable sans l'éditeur.
- Fichiers : `CasaEngine.EditorServices/Cutscenes/CutscenePreviewTransport.cs` (créé, avec l'interface de pas et l'énumération des modes, ou dans des fichiers voisins), tests dans `CasaEngine.Tests/EditorServices/Cutscenes/`.
- Étapes :
  1. Une interface que la session implémentera : la lecture court-elle encore, temps de lecture en secondes, avancer d'un pas de `n` secondes.
  2. La classe de transport : mode `Running`, `Paused` ou `Seeking` ; instant visé ; constante du pas fixe (1/60 s, tirée de `CutsceneTimelineProjection.FramesPerSecond`) ; budget par appel (8 ms par défaut, modifiable) mesuré par une horloge injectable (par défaut `Stopwatch.GetTimestamp`), pour que les tests soient déterministes.
  3. Opérations : lancer (mode `Running`), mettre en pause, reprendre, viser un instant (mode `Seeking`), demander `n` pas d'une image (passe en pause, les pas sont faits au prochain appel).
  4. `Advance(stepper, secondes de la frame)` : en `Running`, un pas de la durée de la frame ; en `Paused`, les pas d'image demandés puis rien ; en `Seeking`, des pas fixes tant que la lecture court, que le temps de lecture est avant l'instant visé (à une tolérance près, inférieure à un pas) et que le budget n'est pas dépassé, avec au moins un pas par appel ; quand l'instant est atteint ou que la lecture ne court plus, passage en `Paused`.
  5. Viser un instant déjà atteint, à moins d'un pas, passe directement en pause sans faire de pas.
  6. Une fonction pure, à côté du transport, qui rend l'état à afficher (`Seeking`, `Paused`, `Playing`, `Ended`, `Stopped`) à partir de quatre entrées : la lecture court-elle, le mode du transport, l'état de fin de la lecture (`CutsceneRuntimeState`) et l'indicateur de fin par changement de monde. C'est la table de T2.2 ; le document et les diagnostics passent par elle.
- Validation : build des deux solutions ; tests ajoutés. Avec un faux exécutant de pas : nombre et durée des pas dans chaque mode ; une recherche de 2 s depuis 0 fait 120 pas et finit en pause à 2 s à moins d'un pas ; avec une horloge qui épuise le budget, un appel fait exactement un pas et le mode reste `Seeking` ; une lecture qui se termine pendant la recherche arrête les pas ; `n` pas d'image font `n` pas de 1/60 s et laissent en pause ; en pause sans demande, aucun pas. Avec un vrai `World` et son `CutsceneDirector` (comme `CutsceneDirectorTests`, sans `Game`) sur une séquence `Wait` : après une recherche à 1,5 s, `PlayElapsedSeconds` vaut 1,5 s à moins d'un pas et la lecture court encore. Pour la fonction d'état : un test par ligne de la table de T2.2, dont `Stopped` avec fin par changement de monde → `Ended`, `Stopped` sans cet indicateur → `Stopped`, `Completed` et `Failed` → `Ended`, `Idle` et `Invalid` → `Stopped`, et une lecture terminée avec un transport resté en `Paused` ou `Seeking` → `Ended`.
- Commit : `feat(editor-services): add the transport of the cutscene preview`
- Note de validation (2026-11-18) : créés `ICutscenePreviewStepper.cs`, `CutscenePreviewTransportMode.cs`, `CutscenePreviewStatus.cs` et `CutscenePreviewTransport.cs` (`Start`, `Pause`, `Resume`, `Seek(cible, temps courant)`, `StepFrames`, `Advance`, et la fonction pure `GetStatus`). Le pas fixe vaut `(float)(1/FramesPerSecond)`, la tolérance d'arrivée est d'un demi-pas, le budget par défaut est de 8 ms avec horloge injectable. `Advance` en pause abandonne les pas d'image demandés quand aucune lecture ne court. Tests : `CutscenePreviewTransportTests.cs`, 36 cas, dont une recherche de 2 s qui fait 120 pas, un budget épuisé qui fait exactement un pas, une recherche à 1,5 s sur un vrai `World` (`PlayElapsedSeconds` dans un pas de 1,5 s, lecture toujours en cours) et un cas par ligne de la table d'état. Les deux solutions buildent à 0 erreur ; `dotnet test` : 6170 tests verts (6134 + 36). Aucun test existant modifié.

### ✅ T1.2 — Garder les mesures d'une relecture à l'autre

- Objectif : une relecture du même document ne perd pas les durées déjà mesurées (P12).
- Fichiers : `CasaEngine.EditorServices/Cutscenes/CutscenePlayTimings.cs`, `CasaEngine.Tests/EditorServices/Cutscenes/CutscenePlayTimingsTests.cs` (tests ajoutés).
- Étapes : une opération, à côté de `Begin`, qui installe la table copie → arbre édité d'une nouvelle lecture et oublie les débuts en cours, **sans** effacer les durées mesurées ni changer `Version`.
- Validation : build ; tests ajoutés : après cette opération une durée mesurée est toujours lue, `Version` n'a pas changé, une action non encore mesurée l'est par la nouvelle lecture, une action déjà mesurée garde sa première mesure ; `Begin` et `Clear` se comportent comme avant.
- Commit : `feat(editor-services): keep the measured durations across replays of a cutscene`
- Note de validation (2026-11-18) : ajout de `CutscenePlayTimings.Continue(copyToOriginal)` : il installe la table copie → arbre édité, oublie les débuts en cours et dimensionne les tables, sans toucher aux durées mesurées ni à `Version`. `Begin` et `Clear` sont inchangés. Six tests ajoutés dans `CutscenePlayTimingsTests.cs` : durée et `Version` conservées, action non mesurée mesurée par la nouvelle lecture (un début de l'ancienne lecture est oublié), première mesure conservée, copies de l'ancienne lecture ignorées, table nulle refusée, `Begin` et `Clear` efficaces après un `Continue`. Les deux solutions buildent à 0 erreur ; `dotnet test` : 6176 tests verts (6170 + 6). Aucun test existant modifié.

---

## Phase 2 — Éditeur (`CasaEngine.Editor`)

### ✅ T2.1 — La session d'aperçu met en pause, avance d'une image et va à un instant

- Objectif : la session expose la pause, le pas à pas, la recherche et le retour au départ, en s'appuyant sur T1.1.
- Fichiers : `CasaEngine.Editor/Runtime/CutscenePreviewSession.cs`, `CasaEngine.Editor/Runtime/PreviewWorldDriver.cs`.
- Étapes :
  1. `PreviewWorldDriver` : une surcharge de `Tick` qui prend des secondes, pour ne créer aucun `GameTime` par pas ; `Tick(GameTime)` l'appelle.
  2. La session implémente l'interface de pas : un pas fait ce que `Tick` fait aujourd'hui après la borne du premier pas (avance du monde, `_host.Update`, `UpdateMoveTracks`, arrêt sur demande de changement de monde). `Tick(GameTime)` applique la borne du premier pas puis délègue au transport.
  3. Opérations : `Pause()`, `Resume()`, `StepFrames(n)`, `Seek(asset, secondes)`, `Rewind()`, et la lecture du mode et de l'instant visé. `Seek` applique P2 : reprise depuis l'état courant si l'instant est devant, que la lecture court et que le document n'a pas changé depuis le lancement ; sinon reconstruction du monde et relance, avec l'opération de T1.2 à la place de `Begin`. `Rewind` arrête la lecture, remet à zéro comme `Play` l'hôte d'effets (`_host.Reset()`), `EndedByWorldChange` et `FailureReason`, reconstruit le monde, puis **fixe** l'état à `Stopped` même quand le directeur n'a levé aucun `Ended` (lecture déjà terminée) ; le temps de lecture vaut alors 0, celui du nouveau directeur (P7). Une recherche qui reconstruit le monde fait les mêmes remises à zéro que `Play`.
  4. Cas limites, à écrire tels quels : `Pause` pendant une recherche l'abandonne et reste en pause à l'instant atteint ; `Pause`, `Resume` et `StepFrames` ne font rien quand aucune lecture ne court ; `Seek` sur une lecture terminée ou jamais lancée reconstruit le monde ; un second `Seek` pendant une recherche remplace l'instant visé, avec la règle de P2 pour décider de reconstruire ; quand la lecture se termine pendant une recherche, la recherche s'arrête et l'état de fin est celui de la lecture (table de T2.2).
  5. Le document signale déjà chaque changement par `ClearTimings()` : la session s'en sert pour savoir que la lecture en cours ne correspond plus au document.
  6. `Play` remet le transport en mode `Running`. Quand aucune lecture ne court, le mode du transport ne compte plus : tout ce qui s'affiche part d'abord de « la lecture court-elle » (table de T2.2).
- Validation : build ; tests verts (aucun test ne construit la session : sa validation est celle de T2.5 et T2.6).
- Commit : `feat(editor): pause, step and seek the cutscene preview session`
- Note de validation (2026-11-18) : `PreviewWorldDriver.Tick(float)` ajouté (`Tick(GameTime)` l'appelle). `CutscenePreviewSession` implémente `ICutscenePreviewStepper` (pas = avance du monde, `_host.Update`, `UpdateMoveTracks`, arrêt sur demande de changement de monde) ; `Tick(GameTime)` borne le premier pas puis délègue à `CutscenePreviewTransport.Advance`, sans créer de `GameTime`. Ajoutés : `Pause`, `Resume` (seulement depuis `Paused`), `StepFrames`, `Seek(asset, secondes)` (reprise depuis l'état courant si l'instant est devant, que la lecture court et que le document n'a pas changé depuis le lancement, sinon reconstruction avec `CutscenePlayTimings.Continue`), `Rewind` (état fixé à `Stopped`, mesures gardées), et les lectures `TransportMode`, `SeekTargetSeconds` et `Status` (fonction `GetStatus`). `Play` et `Seek` partagent `Launch` ; `SetWorld` invalide les mesures gardées (le premier `Seek` suivant repart d'un relevé vide). Les deux solutions buildent à 0 erreur ; `dotnet test` : 6176 tests verts, inchangé (aucun test ne construit la session : validation par T2.5 et T2.6). Aucun test existant modifié.

### ✅ T2.2 — Barre de transport du document

- Objectif : les boutons et le texte de P6 → P9 et P11.
- Fichiers : `CasaEngine.Editor/Controls/CutsceneAssetInspectorPanel.cs`, `CasaEngine.Editor/Controls/WorldViewportPanel.cs`, tests dans `CasaEngine.Tests/Editor/`.
- Étapes :
  1. Boutons : `Play` / `Pause` (un seul bouton, libellé mis à jour quand l'état change, jamais par frame), `Stop`, `Restart`, image précédente, image suivante. Sans monde d'aperçu, ils ne font rien.
  2. Méthodes publiques du document, utilisées par la timeline et par l'automatisation : aller à un instant (`SeekPreview(double)`), basculer lecture et pause, avancer de `n` images, arrêter. Elles rendent faux sans aperçu ou sans asset. `SeekPreview` ne borne l'instant qu'à 0 : la borne haute, la durée de la projection, est appliquée par la timeline (T2.3), pas par le document ni par l'automatisation.
  3. `Stop` appelle `Rewind` (P7). `WorldViewportPanel` reçoit un moyen, interne à l'éditeur, de remplacer un monde d'aperçu par un autre **en gardant la caméra** (capture de l'état avant, restauration après, ou paramètre de `SetWorldOverride` ; le comportement des appelants existants ne change pas). L'état à garder est celui de `_cameraController` : avec un monde d'aperçu, `UsesCamera2d` est faux (`WorldViewportPanel.cs:1841`), et le zoom du cadrage 2D de face n'est pas touché par `SetWorldOverride`. Aller à un instant, reculer d'une image et `Stop` s'en servent quand ils ont reconstruit le monde, et n'appellent pas `FocusBounds` ; `Play` depuis le début et `Restart` recadrent comme aujourd'hui (P11).
  4. Texte du temps : un mot d'état, suivi du temps de lecture de la session en secondes et en images, même quand rien ne joue (P8). Il n'est réécrit que quand le mot, le dixième de seconde ou, en pause, le nombre d'images change. Le mot et le libellé du bouton `Play` / `Pause` suivent cette table, lue de haut en bas, première ligne vraie ; le mot vient de la fonction d'état de T1.1 :

     | Condition | Mot | Libellé du bouton | Effet du bouton |
     |---|---|---|---|
     | une lecture court, transport `Seeking` | Seeking | `Pause` | abandonne la recherche, reste en pause |
     | une lecture court, transport `Paused` | Paused | `Play` | reprend |
     | une lecture court, transport `Running` | Playing | `Pause` | met en pause |
     | aucune lecture ne court ; état `Completed` ou `Failed`, ou état `Stopped` avec `EndedByWorldChange` | Ended | `Play` | relance depuis le début |
     | sinon (avant toute lecture, après `Stop`, asset invalide, pas de monde) | Stopped | `Play` | lance depuis le début |

     Image suivante ne fait rien quand aucune lecture ne court ; image précédente va à l'instant courant moins 1/60 s, borné à 0, même après une fin.
  5. `RaiseDocumentChanged` : en pause ou en recherche, aller de nouveau au même instant (l'instant visé pendant une recherche, le temps de lecture pendant une pause) ; en lecture, relancer comme aujourd'hui (P9) ; quand aucune lecture ne court (jamais lancée, terminée, arrêtée), rien n'est rejoué, comme aujourd'hui. `TrySaveLoadedAsset` passe aussi par `RaiseDocumentChanged` (`:502`) : enregistrer pendant une pause rejoue donc jusqu'au même instant, ce qui est accepté.
- Validation : build ; tests ajoutés pour ce qui se teste sans aperçu : les méthodes publiques rendent faux et ne lèvent rien sur un document sans aperçu ; le texte du temps est vide sans aperçu, comme aujourd'hui. Le reste est validé par T2.5 et T2.6.
- Commit : `feat(editor): add pause, frame stepping and rewind to the cutscene transport row`
- Note de validation (2026-11-18) : barre de transport = `Play` / `Pause` (libellé mis à jour seulement quand l'état change, largeur minimale fixe), `Stop`, `Restart`, `-1 frame`, `+1 frame`. Méthodes publiques du document : `SeekPreview(double)` (borne basse 0 seulement), `TogglePreviewPlayback()`, `StepPreviewFrames(int)`, `StepPreviewBack()`, `StopPreview()` (appelle `Rewind`) ; toutes rendent faux sans aperçu ou sans asset, `StepPreviewFrames` rend aussi faux quand aucune lecture ne court ou si `n` n'est pas positif. `WorldViewportPanel.SetWorldOverrideKeepingCamera` (interne) capture l'état de `_cameraController` avant `SetWorldOverride` et le restaure après (il contient aussi la distance du cadrage 2D de face) ; la recherche, l'image précédente et `Stop` s'en servent sans `FocusBounds`, `Play` et `Restart` recadrent comme avant. `RaiseDocumentChanged` : lecture → `PlayPreview`, recherche → `SeekPreview(instant visé)`, pause → `SeekPreview(temps de lecture)`, sinon rien. Le texte du temps (mot de la fonction d'état, secondes, images) n'est réécrit que si le mot, le dixième de seconde ou, en pause, le nombre d'images change. Écart de mise en page : un premier lancement (`--cutscene-preview-play` sur `loop.cutscene`, fenêtre des captures) a montré le texte du temps tronqué à droite de la rangée (O1) ; le texte est donc placé sous la rangée des boutons, et la capture refaite montre la rangée et le texte entiers (`Playing   1.00 s  ·  60 frames`, bouton `Pause`). Tests ajoutés dans `CutsceneAssetInspectorPanelTests.cs` : méthodes publiques fausses et sans effet sans aperçu, texte du temps vide sans aperçu ; le reste est validé par T2.5 et T2.6. Les deux solutions buildent à 0 erreur ; `dotnet test` : 6178 tests verts (6176 + 2). Aucun test existant modifié.

### ✅ T2.3 — Un clic dans la timeline déplace la tête de lecture

- Objectif : P10, premier volet, et tête de lecture qui reste où la lecture s'est arrêtée (P8).
- Fichiers : `CasaEngine.Editor/Controls/CutsceneTimelinePanel.cs`, `CasaEngine.Tests/Editor/CutsceneTimelinePanelTests.cs`.
- Étapes :
  1. `OnTimelineTimeScrubbed` : borner l'instant entre 0 et la durée de la projection, demander au document d'y aller, garder la dernière demande (instant et résultat) pour les diagnostics et les tests. Si le document refuse (pas d'aperçu), la tête de lecture revient au temps de la lecture, comme aujourd'hui.
  2. `Update` : la tête de lecture vaut toujours `MapPlayTime(temps de lecture de la session)`, que la lecture coure ou non ; la vue ne suit la tête de lecture que pendant une lecture ou une recherche, pas pendant une pause.
- Validation : build ; tests ajoutés : une demande de la règle est transmise au document avec un instant borné à la durée de la projection ; sur un document sans aperçu, la demande est refusée et la tête de lecture revient à 0. `ScrubbingTheRuler_DoesNotMoveThePlayhead` doit passer sans modification (document sans aperçu) ; s'il ne passe pas, appliquer la règle d'exception des tests existants.
- Commit : `feat(editor): seek the cutscene preview from the timeline`
- Note de validation (2026-11-18) : `CutsceneTimelinePanel.OnTimelineTimeScrubbed` borne l'instant entre 0 et la durée de la projection, appelle `SeekPreview` du document, garde la dernière demande (`LastSeekRequestSeconds`, `LastSeekRequestAccepted`) pour les diagnostics et les tests, et remet la tête de lecture au temps de lecture quand le document refuse. `Update` : la tête de lecture vaut toujours `MapPlayTime(temps de lecture de la session)` (0 sans aperçu), et la vue ne la suit que pendant une lecture ou une recherche (`Playing` ou `Seeking`), pas en pause. Deux tests ajoutés dans `CutsceneTimelinePanelTests.cs` : demande sans aperçu enregistrée puis refusée avec tête de lecture à 0, demande bornée à la durée de la projection (haut) et à 0 (bas). Le transfert effectif au document avec un aperçu n'est pas testable (aucun test ne construit la session) : il est validé par T2.5 et T2.6. `ScrubbingTheRuler_DoesNotMoveThePlayhead` passe sans modification. Les deux solutions buildent à 0 erreur ; `dotnet test` : 6180 tests verts (6178 + 2). Aucun test existant modifié.

### ✅ T2.4 — Glisser dans la règle

- Objectif : P10, second volet : déplacer la tête de lecture en continu en gardant le bouton enfoncé dans la règle.
- Fichiers : `CasaEngine.Editor/Controls/Timeline/TimelineRuler.cs`, `TimelineControl.cs`, `CasaEngine.Editor/Controls/CutsceneTimelinePanel.cs`, tests dans `CasaEngine.Tests/Editor/`, `docs/editor/timeline-generic.md` si l'option y est décrite avec les autres.
- Étapes :
  1. Une option de construction du contrôle générique, désactivée par défaut, à côté de celles de la tranche C. Activée, la règle écoute le glisser du bouton gauche (les événements que `TimelineViewport` utilise déjà) et appelle `SetCurrentTimeSeconds` puis `NotifyTimeScrubbed` à chaque déplacement, avec le même calcul de temps que le clic. Le relâchement ne déclenche pas une seconde notification pour le même instant.
  2. Le panneau de la timeline des cutscenes active l'option.
- Validation : build ; tests ajoutés avec le harnais de souris des tests existants de la timeline : avec l'option, un glisser de A vers B dans la règle lève `TimeScrubbed` avec des instants croissants qui finissent à l'instant de B ; sans l'option, seul le relâchement lève l'événement, comme aujourd'hui. Les tests existants de la timeline passent sans modification. Si le harnais ne permet pas de simuler un glisser, la tâche reste 🧪 avec la raison, et le clic de T2.3 reste le moyen livré.
- Commit : `feat(editor): scrub the cutscene timeline by dragging in the ruler`
- Note de validation (2026-11-18) : option `rulerDragScrub` (désactivée par défaut) ajoutée à `TimelineControl(window, verticalScroll, preciseHitTest, trackRowHeight, rulerDragScrub)`, propriété `RulerDragScrubEnabled` ; `TimelineRuler` écoute alors `DragStart`, `Dragged` et `DragEnd` du bouton gauche (condition `MouseMovedAfterPress`) et appelle `SetCurrentTimeSeconds` puis `NotifyTimeScrubbed` à chaque déplacement qui change l'instant, avec le même calcul que le clic (extrait dans `GetTimeAtX`) ; le relâchement qui suit un glisser est ignoré (drapeau remis à zéro à l'appui suivant, au cas où le bouton est relâché hors de la règle). Le panneau de la timeline des cutscenes active l'option. Accesseur interne `TimelineControl.Ruler` ajouté pour les tests. Le harnais de souris simule bien un glisser : `TimelineRulerDragTests.cs`, 5 tests (option désactivée par défaut ; avec l'option, un glisser de 1 s à 6 s lève `TimeScrubbed` avec des instants croissants qui finissent à l'instant du point de relâchement, sans doublon ; un clic lève encore une seule notification ; un clic après un glisser notifie à nouveau ; sans l'option, bouger avec le bouton enfoncé ne notifie rien et un clic notifie comme avant). Constat : sans l'option, le relâchement qui suit un glisser n'atteint pas la règle (aucune notification), comme avant ce changement ; aucun test ne dépend de ce cas. `docs/editor/timeline-generic.md` décrit l'option (en français, comme la section). Les deux solutions buildent à 0 erreur ; `dotnet test` : 6185 tests verts (6180 + 5). Aucun test existant modifié.

### ✅ T2.5 — Options d'automatisation et diagnostics

- Objectif : pouvoir prouver par un fichier et par une capture l'état d'une pause, d'une recherche, d'un pas et d'un Stop (P13).
- Fichiers : `CasaEngine.Editor/EditorAutomationOptions.cs`, `CasaEngine.Editor/GameEditor.cs`, `CasaEngine.Editor/Controls/CutsceneAssetInspectorPanel.cs`, `CasaEngine.Editor/Controls/CutsceneTimelinePanel.cs`, `CasaEngine.Tests/Editor/EditorAutomationOptionsTests.cs` (tests ajoutés).
- Étapes :
  1. `--cutscene-preview-seek <liste>` : nombres en culture invariante séparés par des virgules ; une valeur illisible ou négative est une erreur d'option, traitée comme les autres erreurs d'option existantes. `--cutscene-preview-step <n>` (entier positif). `--cutscene-preview-stop`.
  2. Ordre d'application, après l'ouverture de l'asset : lecture (`--cutscene-preview-play`, comme aujourd'hui), puis les éditions demandées (elles attendent déjà la lecture, `GameEditor.cs:7874`), puis chaque recherche, la suivante n'étant lancée que lorsque la précédente a fini (mode `Paused`, ou lecture terminée), puis les pas, puis le Stop. Le délai de capture se compte depuis la fin de la dernière étape.
  3. Diagnostics : mode du transport, instant visé, dernière demande venue de la timeline, libellé du bouton `Play` / `Pause`, en plus des lignes existantes (temps de lecture, tête de lecture, texte de transport, position des entités).
- Validation : build ; tests ajoutés pour l'analyse des trois options (liste, valeur illisible, valeur négative, absence) ; un lancement `--cutscene-preview-seek 0.5` sur `loop.cutscene` écrit un mode `Paused` et un temps de lecture de 0,5 s à moins d'un pas.
- Commit : `feat(editor): drive the pause, seek and step of the cutscene preview from the automation`
- Note de validation (2026-11-18) : options `--cutscene-preview-seek <liste>` (secondes en culture invariante séparées par des virgules), `--cutscene-preview-step <n>` (entier positif) et `--cutscene-preview-stop`, lues par `EditorAutomationOptions` (`CutscenePreviewSeekSeconds`, `CutscenePreviewStepFrames`, `CutscenePreviewStop`, comptées dans `HasAutomation`). Une valeur illisible ou négative dans la liste annule toute l'option, un pas non positif ou non entier est ignoré : c'est le traitement des autres options mal formées (aucune erreur d'option n'existe ailleurs, `--set-screen-property` mal formé est ignoré de même). `GameEditor.TryApplyAutomationCutscenePreviewTransport` applique, après la lecture et les éditions, chaque recherche (la suivante part quand le statut n'est plus `Seeking`), puis les pas (attente de `PendingFrameSteps` à 0), puis le Stop, une étape par frame ; le délai de capture se compte depuis la fin de la dernière étape. Diagnostics ajoutés : options et dernière étape, statut, mode du transport, instant visé, pas en attente, temps de lecture, libellé du bouton `Play` / `Pause`, dernière demande venue de la règle. Pour cela : `CutsceneAssetInspectorPanel.HasPreview`, `PreviewStatus`, `PreviewPendingFrameSteps` et `CutscenePreviewSession.PendingFrameSteps` (une ligne, fichier hors liste de la tâche). Les deux solutions buildent à 0 erreur ; `dotnet test` : 6200 tests verts (6185 + 15, dans `EditorAutomationOptionsTests.cs`). Lancements sur `SpriteCutsceneSample` (diagnostics hors dépôt) : `loop.cutscene --cutscene-preview-seek 0.5 --capture-delay 2` écrit `Paused`, mode `Paused`, 0,500 s, 30 images, tête de lecture 0,500 s, bouton `Play` ; `--cutscene-preview-seek 4.5,0.5` finit à 0,500 s, 30 images ; `--cutscene-preview-seek 2 --cutscene-preview-step 30` finit à 2,500 s, 150 images, `Paused` ; `--cutscene-preview-seek 2 --cutscene-preview-stop` finit `Stopped`, 0 s, mode `Running` ; `exit.cutscene --cutscene-preview-seek 1.5` finit `Completed` (statut `Ended`, 1,017 s). Les captures sont celles de T2.6. Aucun test existant modifié ; `git status` ne montre aucun nouveau fichier sous `Projects/`.

### ✅ T2.6 — Captures de validation

- Objectif : produire les captures de la tranche et vérifier, par les diagnostics, que l'état montré est le bon.
- Étapes, sur `Projects/SpriteCutsceneSample`, timeline au premier plan, `--diagnostics-out` hors dépôt :
  1. `loop.cutscene`, `--cutscene-preview-seek 0.5 --capture-delay 2` : mode `Paused` ; temps de lecture 0,5 s à moins d'un pas, 30 images ; tête de lecture au même instant ; `orb-left` à un x strictement entre son x de départ et son x d'arrivée (les deux valeurs se lisent dans les lancements 6 et 2) ; texte « Paused » ; bouton libellé `Play`. Les deux secondes de délai prouvent que la pause tient.
  2. `loop.cutscene`, `--cutscene-preview-seek 2` : temps 2 s à moins d'un pas ; positions des entités égales, à 0,5 unité près, à celles d'un lancement `--cutscene-preview-play --capture-delay 2` fait dans la même série.
  3. `loop.cutscene`, `--cutscene-preview-seek 4.5,0.5` : état final égal à celui du lancement 1 (mêmes positions, même nombre d'images) : le retour en arrière reconstruit bien le monde. Les barres `MoveSprite` que la première recherche a mesurées sont toujours « measured » (P12).
  4. `loop.cutscene`, `--cutscene-preview-seek 2 --cutscene-preview-step 30` : temps 2,5 s à moins d'un pas, 30 images de plus que le lancement 2, mode `Paused`.
  5. `exit.cutscene`, `--cutscene-preview-play --capture-delay 3` : la lecture est terminée, état `Completed`, `endedByWorldChange` à faux, « Skipped: ChangeWorld -> EndWorld.world » dans la ligne d'état, comme à la tranche A ; la tête de lecture est restée au temps de fin, vers 1 s, et non à 0 ; texte « Ended » (quatrième ligne de la table de T2.2) ; bouton libellé `Play`.
  5 bis. `exit.cutscene`, `--cutscene-preview-seek 1.5` : la lecture se termine pendant la recherche, vers 1 s ; même état final que le lancement 5 (`Completed`), texte « Ended » et non « Paused ».
  6. `loop.cutscene`, `--cutscene-preview-seek 2 --cutscene-preview-stop` : état `Stopped`, temps 0, tête de lecture à 0, entités à leur position de départ (celles d'un lancement sans lecture).
  7. `exit.cutscene`, `--cutscene-preview-seek 1.5 --cutscene-preview-stop` (Stop sur une lecture déjà terminée, voile d'écran en place) : l'état passe de `Completed` à `Stopped`, texte « Stopped », temps 0, plus aucune demande refusée listée, et la ligne d'état de l'aperçu ne signale plus de fondu ni de voile d'écran. La remise à zéro d'`EndedByWorldChange` par `Rewind` ne se voit sur aucun sample : elle est validée par relecture du code.
- Validation : chaque capture est regardée et décrite honnêtement ; les valeurs des diagnostics sont comparées aux attentes ci-dessus. Bouton coupé, texte tronqué ou barre de transport qui déborde : défaut à corriger avant ✅, ou à noter en 🧪. `git status` ne montre aucun nouveau fichier sous `Projects/`.
- Limite d'essais : deux séries de corrections au plus dans cette tâche ; au-delà, 🧪 avec la liste exacte de ce qui reste.
- Commit : `docs(plan): record the validation screenshots of the cutscene transport`, ou `fix(editor): …` si une correction est nécessaire.
- Note de validation (2026-11-18) : dix lancements sur `SpriteCutsceneSample`, timeline au premier plan, diagnostics et captures hors dépôt (préfixe `d`), toutes les captures regardées. Aucune correction nécessaire : aucun fichier de code touché. (0) `loop.cutscene` sans lecture : `Stopped`, 0 s, `orb-left` x=-190 (départ). (1) `--cutscene-preview-seek 0.5 --capture-delay 2` : statut `Paused`, mode `Paused`, 0,500 s, 30 images, tête de lecture 0,500 s, `orb-left` x=-130 (strictement entre -190 et -60), texte `Paused   0.50 s  ·  30 frames`, bouton `Play` ; les barres `MoveSprite` sont encore « estimated » (rien de terminé). (2) `--cutscene-preview-seek 2` : `Paused`, 2,000 s, 120 images, tête de lecture 2,000 s, `orb-left` x=-60 ; `--cutscene-preview-play --capture-delay 2` (même série) : `Playing`, 2,017 s, 121 images, bouton `Pause`, mêmes positions pour les cinq entités (écart 0). (3) `--cutscene-preview-seek 4.5,0.5` : état final identique à (1) (mêmes positions, 0,500 s, 30 images, `Paused`) et barres `MoveSprite` « measured » (1,1 s) : P12 tient. (4) `--cutscene-preview-seek 2 --cutscene-preview-step 30` : 2,500 s, 150 images (120 + 30), `Paused`, positions de (2). (5) `exit.cutscene --cutscene-preview-play --capture-delay 3` : `Completed`, statut `Ended`, `endedByWorldChange=False`, « Skipped: ChangeWorld -> EndWorld.world » et `Screen overlay rgba(255,255,255,255)` dans la ligne d'état, 1,000 s, 60 images, tête de lecture restée à 1,000 s, texte `Ended   1.00 s  ·  60 frames`, bouton `Play`. (5 bis) `--cutscene-preview-seek 1.5` : `Completed`, statut `Ended`, mode `Paused`, 1,017 s, 61 images (un pas de plus que la lecture : P15), texte `Ended   1.02 s  ·  61 frames`, même ligne d'état que (5). (6) `--cutscene-preview-seek 2 --cutscene-preview-stop` : `Stopped`, 0 s, 0 image, tête de lecture 0, entités aux positions de (0), mode `Running`, mesures gardées (« measured »). (7) `exit.cutscene --cutscene-preview-seek 1.5 --cutscene-preview-stop` : `Completed` devient `Stopped`, texte `Stopped   0.00 s  ·  0 frames`, ligne d'état `Preview: Stopped. Sprite animations, physics and scripts are frozen in the preview.` (plus de « Skipped », plus de « Screen overlay »), aucune action refusée listée ; la remise à zéro d'`EndedByWorldChange` reste validée par relecture du code. O1 : sur les captures (fenêtre 1600x900) la barre de transport (`Play` / `Pause`, `Stop`, `Restart`, `-1 frame`, `+1 frame`) tient entière dans la largeur du document et le texte du temps, placé sous la rangée, n'est pas tronqué ; l'état `Playing` élargit le bouton `Pause` de quelques pixels par rapport à `Play` (les boutons suivants se décalent d'environ 5 px, sans débordement). Le viewport des captures n'affiche pas le voile blanc du `FadeScreen` (viewport bleu clair en (5) et (7)) : c'est le comportement de la tranche A, l'hôte d'aperçu ne dessine pas le fondu. La solution éditeur a été reconstruite (0 erreur) avant les lancements ; `dotnet test` : 6200 tests verts (inchangé, aucun code touché). `git status` : aucun nouveau fichier sous `Projects/`. Captures : `scratchpad/shots/d0-idle-final.png`, `d1-seek-0.5-final.png`, `d2-seek-2-final.png`, `d2b-play-2-final.png`, `d3-seek-back-final.png`, `d4-step-final.png`, `d5-exit-play-final.png`, `d5b-exit-seek-final.png`, `d6-stop-final.png`, `d7-exit-stop-final.png`.

---

## Phase 3 — Documentation et recette

### ✅ T3.1 — Documentation

- Objectif : `docs/editor/cutscene-editor.md` décrit la barre de transport, la pause, le pas à pas, le déplacement de la tête de lecture, ce que fait `Stop`, la relecture après une modification en pause, les nouvelles options d'automatisation et les limites (P14, P15, coût d'un retour en arrière) ; `docs/editor/timeline-generic.md` mentionne l'option de glisser dans la règle si T2.4 est livrée.
- Fichiers : ces deux documents, ce plan, `ai-agent/README.md`.
- Validation : relecture contre le code livré ; aucun nom de jeu ni de projet tiers.
- Commit : `docs(editor): document the pause, step and seek of the cutscene preview`
- Note de validation (2026-11-18) : `docs/editor/cutscene-editor.md` mis à jour (en anglais) : renvoi à ADR-0099 ; nouvelle section « Transport » (table des états et du bouton `Play` / `Pause`, pause, `+1 frame` / `-1 frame`, aller à un instant et son avance par pas fixes avec budget, `Stop` qui reconstruit le monde, fin de lecture, relecture après une modification en pause, mesures gardées) ; section Playhead réécrite (clic et glisser dans la règle, tête de lecture qui reste à la fin) ; caméra gardée (P11) ; trois nouvelles options d'automatisation et leurs diagnostics ; limites (pas de raccourci clavier P14, pas pour les actions comptées en images P15, coût d'un retour en arrière non mesuré sur un grand monde) ; liste du code et sections Limits et Next à jour. `docs/editor/timeline-generic.md` décrivait déjà l'option `rulerDragScrub` (livrée par T2.4) : aucun changement. Relu contre le code livré (libellés des boutons, mots d'état, comportement de `Stop`, options et diagnostics) ; aucun nom de jeu ni de projet tiers dans les fichiers touchés (vérifié par recherche). Aucun code touché : ni build ni tests relancés (6200 tests verts à T2.6).

### 🧪 T3.2 — Recette de l'auteur

- Validation à distance sur les captures de T2.6, puis recette réelle à la fin (D5). Passe en 🧪 quand les captures sont envoyées.
- Note (2026-11-18) : les captures de T2.6 ont été envoyées pour validation à distance ; la recette réelle de l'auteur reste à faire (O2, O3, O4).

---

## Points ouverts

| Réf | Sujet | Tâche concernée |
|---|---|---|
| O1 | La barre de transport gagne trois boutons : tient-elle dans la largeur du document à la taille de fenêtre des captures ? À juger sur les captures. | T2.2, T2.6 |
| O2 | Coût d'un retour en arrière : chaque retour reconstruit le monde depuis son asset. Sur le sample c'est immédiat ; sur un grand monde, un glisser vers l'arrière reconstruira le monde à chaque déplacement de la souris. À mesurer à la recette. | T2.1, T2.4 |
| O3 | P11 (caméra gardée) ne peut pas être prouvé par une capture : l'automatisation ne déplace pas la caméra. Il est validé par relecture du code et par la recette de l'auteur. | T2.2 |
| O4 | Confirmation de P1 → P15 par l'auteur. | Toutes |
| O5 | Vérification indépendante du 2026-11-18 : CONFIRMED, sans point bloquant. Reporté : après une fin de lecture, le monde continue d'être mis à jour si la fin est arrivée en lecture, et ne l'est plus si elle est arrivée pendant une recherche ; un effet qui survit à la dernière action (un déplacement de sprite encore en cours) peut donc finir dans un état visible différent pour un même temps de fin. Non reproduit sur les samples. | T2.1 |
| O6 | Reporté : la ligne d'état de l'aperçu écrit encore « Preview: Playing » pendant une pause ou une recherche, à côté du mot « Paused » de la barre de transport. | T2.2 |
| O7 | Reporté : aller à 0, ou reculer depuis la première image, met en pause avant la première mise à jour du monde ; les actions du temps 0 (`ShowSprite`) n'ont pas encore joué et le viewport est vide. | T2.1 |
| O8 | Non couvert par un lancement ni par un test, validé par relecture du code seulement : la reprise après une pause, l'image précédente, et la relecture après une modification en pause. L'automatisation applique toujours les éditions avant les recherches. | T2.2, T2.5 |
| O9 | Reporté : pendant une lecture, le nombre d'images affiché n'est réécrit qu'à chaque dixième de seconde ; un glisser relâché hors de la règle peut faire ignorer un relâchement suivant dans la règle, dans un cas rare. | T2.2, T2.4 |

## Hors périmètre

- Déplacer, étirer, ajouter ou supprimer sur la timeline ; placement libre : tranches E et F.
- Raccourcis clavier du transport ; vitesse de lecture ; lecture en boucle d'une plage.
- Aperçu du son, fondus dessinés, entrées simulées : tranche G.
- Retour en arrière sans relecture (instantanés de l'état du monde).
- Tout changement du format `.cutscene`, du runtime `CasaEngine/` ou de MGUI.
