# Plan agent IA — Éditeur de cutscenes : timeline en lecture seule (tranche C)

Troisième tranche du programme « éditeur de cutscenes visuel » (ADR-0135, plans [cutscene-editor-preview-tasks.md](cutscene-editor-preview-tasks.md) et [cutscene-editor-outliner-details-tasks.md](cutscene-editor-outliner-details-tasks.md)).
Elle ajoute une timeline qui montre la cutscene dans le temps, avec une tête de lecture qui suit l'aperçu et une sélection partagée avec l'arbre. La timeline est une **vue calculée depuis l'arbre** : on n'y édite encore rien.

Les décisions D1 → D5 ci-dessous ont été arbitrées avec l'auteur : **ce plan les applique, il ne les rediscute pas**.
L'auteur a validé les captures de la tranche B le 2026-11-18 et demandé de continuer dans le même mode autonome : ce plan est approuvé d'avance. Les points P1 → P14 sont des arbitrages de l'agent, à confirmer par l'auteur à la recette.

Ce fichier doit être mis à jour pendant le travail : l'icône au début de chaque tâche indique son statut courant.

## Objectif

Quand un document `.cutscene` est actif :

- un panneau **Cutscene Timeline** montre chaque action de l'arbre à sa place dans le temps : barre pour une durée connue, marqueur pour une action instantanée, barre « estimée » pour une durée que seule une lecture peut mesurer ;
- les rangées regroupent les actions par cible : une rangée de déroulement, une rangée par entité, puis écran, son, monde et entrées ;
- pendant une lecture de l'aperçu, la tête de lecture avance avec le temps réel de la cutscene, et la barre de transport affiche ce temps en secondes et en images ;
- après une lecture, les durées mesurées remplacent les estimations ;
- cliquer une action dans la timeline la sélectionne dans l'arbre et dans l'Inspector, et inversement.

## État vérifié du dépôt (2026-11-18)

Branche `chantier/cutscene-property-editing`, tête `d96c42cf`, 28 commits depuis `main` `c365b514`, non fusionnée.
Fichiers non suivis préexistants, à ne jamais indexer : `Projects/PixelUiSample/.casaeditor/`, `Projects/SpriteCutsceneSample/.casaeditor/`.

Contrôle Timeline (`CasaEngine.Editor/Controls/Timeline/`) :

- `TimelineControl(MGWindow)` ; `SetModel(TimelineModel?)` reconstruit les en-têtes de pistes (`TimelineControl.cs:102`, `:183`). Les éléments sont relus à chaque dessin.
- Temps : `CurrentTimeSeconds`, `SetCurrentTimeSeconds(float)` silencieux, `EnsureTimeVisible(float)` (`:52`, `:223-240`, `:246`).
- Sélection : `SetSelectedItemId(Guid?, bool notify)` ; `SelectedItemChanged` n'est levé que si `notify` est vrai (`:285`, `:317`).
- Sans `Adapter`, avec des éléments `IsEditable = false` et `CanMove = false`, rien ne se déplace ; un clic sur un élément le sélectionne quand même et lève `SelectedItemChanged` (`TimelineViewport.cs:326-333`, `:380`, `:393-396`). Un clic dans la règle lève toujours `TimeScrubbed` (`TimelineRuler.cs:197-198`).
- `TimelineModel` : `DurationSeconds`, `TimeUnit`, `FrameRate`, `Tracks`, `Items`. `TimelineItem` : `Id`, `TrackId`, `StartTime`, `Duration`, `Kind` (`Instant`, `Duration`, `Range`, `Marker`), `DisplayName`, `ToolTipText`, `IsEditable`, `CanMove`, `CanResizeStart`, `CanResizeEnd`, `Source` (`TimelineModel.cs:8-67`).
- Chaque piste fait 34 px de haut ; la hauteur voulue de la zone des pistes est `12 + 34 × nombre de pistes` ; il n'y a **aucun défilement vertical** (`TimelineControlMetrics.cs`, `TimelineViewport.cs:61-66`). Les deux rangées de la grille du contrôle sont en hauteur automatique : le contrôle demande toujours toute la hauteur de son contenu (`TimelineControl.cs:110-128`). Les en-têtes de pistes sont de vrais éléments MGUI empilés, pas un dessin (`TimelineTrackHeaderPanel.cs`). La molette n'est traitée que par la règle et la zone du temps, pour zoomer (`TimelineRuler.cs:145-171`, `TimelineViewport.cs:516-540`).
- Test de clic : le premier élément `Duration` ou `Range` sous le curseur est rendu ; les éléments instantanés ne sont cherchés que si aucune barre ne correspond (`TimelineHitTest.cs:73-121`). Un marqueur posé sur une barre, ou une barre posée sur une plage, ne peut donc pas être cliqué.
- `SetModel` ne garde l'élément sélectionné que si son identifiant existe dans le nouveau modèle, et `TimelineItem.Id` vaut par défaut un nouveau GUID (`TimelineControl.cs:196-200`, `TimelineModel.cs:40`).
- `SetCurrentTimeSeconds` borne le temps à la fin de la timeline, qui tient compte de `DurationSeconds` (`TimelineControl.cs:230`, `:357-375`).
- Dessin d'un élément : `ITimelineItemRenderer.DrawItem(drawArgs, context, track, item, bounds, state)`, remplaçable par `TimelineControl.ItemRenderer` ; le rendu par défaut dessine un bloc plein, une plage, un trait ou un losange (`Rendering/ITimelineItemRenderer.cs:9`, `Rendering/DefaultTimelineItemRenderer.cs:50-131`). Aucun découpage n'est appliqué par élément.
- Seul consommateur : `Animation2dTimelineControl`. Tests : `TimelineViewTransformTests`, `TimelineEditingTests`, `TimelinePlayheadFollowTests`, `TimelineTimeTextWidthTests` dans `CasaEngine.Tests/Editor/`.

Hébergement d'un panneau de timeline :

- La timeline Animation2D est un panneau d'outil contextuel : identifiant `EditorPanelIds.Animation2dTimeline`, descripteur dans `CreatePanelRegistry` (`CasaEngine.Editor/GameEditor.cs:2331-2335`), entrée dans `ContextualToolPanelRegistry` pour `EditorDocumentKind.Animation2d` (`:3389-3398`). `SyncContextualToolPanels` l'ajoute au groupe d'onglets du bas quand un document de ce type devient actif et l'enlève sinon, sans prendre le focus (`:3410-3489`).
- Cinq endroits de `GameEditor.cs` nomment en dur la timeline Animation2D : contenu créé au démarrage (`:1070`), fabrique de contenu qui relie le document actif (`:1798-1804`), détachement au retrait du panneau (`:3497-3500`), `EnsureContextualToolPanelPresent` avant `--activate-panel` (`:7304-7307`), alias (`:7562-7563`). `ActivateDockPanel(id)` met un onglet au premier plan (`:2394-2405`).
- Le panneau Animation2D pose son contrôle dans un `MGStackPanel` vertical, donc sans hauteur bornée (`CasaEngine.Editor/Controls/Animation2dTimelinePanel.cs:34-38`).
- Le contenu est relié au document actif par `SetInspectorPanel` (`CasaEngine.Editor/Controls/Animation2dTimelinePanel.cs:21-63`).
- `--activate-panel` sait activer un panneau par alias (`GameEditor.cs:7289-7322`, `:7551-7566`).
- Document cutscene : `RefreshCutsceneViews` relie l'arbre et les détails (`:3992-3996`) ; ouverture `TryOpenCutsceneAsset` (`:5173-5226`) ; fermeture (`:3085-3103`) ; mise à jour par frame de tous les documents cutscene (`:6505-6508`).

Exécution et temps (`CasaEngine/Framework/Cutscenes/`) :

- `CutsceneDirector` n'expose ni temps écoulé ni notification par action ; `Ended` est son seul événement (`CutsceneDirector.cs:46`).
- `CutsceneActionCoroutineFactory.ExecuteAction` est un itérateur : une action commence à son entrée et finit après son dernier `yield`. `Sequence` imbrique l'itérateur de chaque enfant ; `Repeat` rejoue ses enfants à chaque tour ; `Parallel` lance une coroutine par enfant, qui ne fait son premier pas qu'à la mise à jour suivante (`CutsceneActionCoroutineFactory.cs:344-444`).
- À l'arrêt d'une lecture, une coroutine arrêtée n'est jamais reprise : le code placé après un `yield` ne s'exécute pas. Ses blocs `finally` s'exécutent plus tard, quand l'itérateur est libéré.
- Sorties d'`ExecuteAction` : les échecs passent par `FailPlay` puis `yield break` ; un refus de l'hôte (`OpenScreen`, `PlayVideo`, `ChangeWorld`, `ExitOnInput`) est un `break` ordinaire ; un `Parallel` vide et les contrôles d'abandon de `Sequence`, `Repeat` et `Parallel` sortent par `yield break` sans échec ; plusieurs actions ne rendent jamais la main.
- `WaitForSeconds` ne consomme rien dans la mise à jour où il est rendu ; avec un pas de 0,1 s, un `Wait 1.0` commencé à la mise à jour 0 finit à la mise à jour 10, et l'action suivante s'exécute dans cette même mise à jour (`CasaEngine/Framework/Scripting/Coroutines/WaitForSeconds.cs:18-22`, `CoroutineManager.cs:186-249`).
- Une lecture lancée depuis un abonné d'`Ended` ou par `ExitOnInput` démarre **pendant** `CoroutineManager.Update` ; sa coroutine ne fait son premier pas qu'à la mise à jour suivante.
- `MoveSprite` et `TintSprite` ne bloquent pas la séquence ; leur effet continue dans `StaticSpriteComponent.Update`, observable par `IsMoving` et `IsTinting` (`CasaEngine/Framework/Scene/Entities/Components/StaticSpriteComponent.cs:150-153`). `IsMoving` repasse à faux dans la mise à jour où la cible est atteinte ; un autre `StartMove`, `ShowSprite` ou `HideSprite` interrompt un déplacement (`:160-249`).
- `WaitFrames` et `FadeScreenFrames` comptent des mises à jour, pas du temps ; aucun code ne convertit des images en secondes.
- `WorldRuntimeSystems.Update` appelle `CoroutineManager.Update` puis `CharacterMotion.Update` (`CasaEngine/Framework/Scene/World/WorldRuntimeSystems.cs:19-25`).

Aperçu :

- `CutscenePreviewSession.Play` joue une **copie** de l'asset, obtenue par sérialisation : les actions exécutées ne sont pas les instances éditées, mais l'arbre a la même forme (`CasaEngine.Editor/Runtime/CutscenePreviewSession.cs:138-171`, `:258-271`).
- `PreviewWorldDriver.Tick` appelle `World.Update(float)` avec le temps réel écoulé, sans échelle de temps.
- Le document relance l'aperçu sur un changement pendant une lecture, et rafraîchit sa ligne d'état seulement quand elle change (`CasaEngine.Editor/Controls/CutsceneAssetInspectorPanel.cs`).

Sample `Projects/SpriteCutsceneSample` : `loop.cutscene` est un `ExitOnInput` suivi d'un `Repeat` sans fin de 24 actions sur quatre entités : quatre `ShowSprite`, quatre `MoveSprite` de 130 unités à la vitesse 120 (1,083 s), `Wait 1.3`, quatre `TintSprite` de 0,8 s, `Wait 2.5`, quatre `TintSprite` de 1 s, `Wait 1.2`, quatre `HideSprite`, `Wait 0.6`. Un tour dure donc 5,6 s. `exit.cutscene` est un `FadeScreen` d'une seconde suivi d'un `ChangeWorld`.

## Décisions verrouillées

| Réf | Décision |
|---|---|
| D1 | L'arbre d'actions reste le modèle enregistré ; la timeline en est une projection. Le format `.cutscene` ne change pas. |
| D2 | Dans cette tranche la timeline est en lecture seule. Réordonner et placer librement viendront dans les tranches E et F ; déplacer la tête de lecture, dans la tranche D. |
| D3 | Le temps s'affiche en secondes et en images. |
| D4 | Mode autonome : plan approuvé d'avance, exécution enchaînée, aucun push ni fusion. |
| D5 | L'auteur valide à distance sur captures d'écran ; sa recette réelle a lieu à la fin. |

## Arbitrages de l'agent, à confirmer

| Réf | Arbitrage |
|---|---|
| P1 | La timeline est un panneau d'outil contextuel « Cutscene Timeline », comme celle d'Animation2D : ancrable, redimensionnable, présent seulement quand un document cutscene est actif. À l'**ouverture** d'une cutscene, son onglet passe au premier plan dans le groupe du bas. |
| P2 | Runtime, ajouts seulement : `CutsceneDirector.PlayElapsedSeconds` et `PlayElapsedFrames`, remis à zéro à chaque `Play` ; notifications `ActionStarted` et `ActionEnded`, sans allocation, qui portent l'action, le temps et l'image. |
| P3 | L'horloge avance après la mise à jour des coroutines, et seulement si la lecture en cours après la mise à jour est celle qui l'était avant : le premier pas d'une lecture voit toujours le temps 0, y compris quand elle est lancée depuis un abonné d'`Ended` ou par `ExitOnInput`. |
| P4 | `ActionEnded` est levé quand le code d'une action se termine sans échec : fin normale, action qui ne rend jamais la main, demande refusée par l'hôte, `Parallel` vide. Il n'est pas levé pour une action qui échoue, ni pour les actions en cours d'une lecture arrêtée, échouée ou remplacée, qui ne lèvent que `Ended` comme aujourd'hui. Aucune notification ne part d'un bloc `finally`. Pour `MoveSprite` et `TintSprite`, `ActionEnded` suit immédiatement `ActionStarted` : la durée de leur effet n'est pas celle de l'action. |
| P5 | Rangées, dans cet ordre, créées seulement si elles servent : « Loops » (plages de `Repeat`), « Flow » (`Wait`, `WaitFrames`), une rangée par entité dans l'ordre de première apparition (`ShowSprite`, `HideSprite`, `MoveSprite`, `TintSprite`, `MoveTo`, `NavigateTo`, `PlayVideoSprite`, `StartVideoSprite` ; « (no entity) » si le nom est vide), « Screen » (`FadeScreen`, `FadeScreenFrames`, `OpenScreen`, `PlayVideo`), « Audio » (`PlaySound`, `PlayMusic`, `StopMusic`, `FadeMusic`), « World and input » (`ChangeWorld`, `ExitOnInput`), « Other » (type inconnu, en marqueur). `Sequence` et `Parallel` n'ont pas d'élément. Dans une rangée, deux barres ou deux plages qui se chevauchent sont réparties sur des sous-rangées ; un marqueur reste sur la rangée de sa barre. |
| P6 | Durée qui fait avancer le temps de la séquence : `Wait`, `WaitFrames`, `FadeScreen`, `FadeScreenFrames`, `FadeMusic` par leur valeur ; `MoveTo`, `NavigateTo`, `PlayVideo`, `PlayVideoSprite` par leur durée mesurée, sinon 1 s affichée comme estimée ; toutes les autres actions, zéro. |
| P7 | Longueur de la barre d'une action qui ne bloque pas : `TintSprite` par sa durée ; `MoveSprite` par la durée mesurée de son effet, sinon 1 s estimée ; `PlayMusic` et `StopMusic` par leur fondu. `ExitOnInput` est une barre ouverte jusqu'à la fin. Les autres actions instantanées sont des marqueurs. |
| P8 | `Parallel` : ses enfants partent au même instant et sa durée est la plus longue de ses branches. Le décalage d'une mise à jour du runtime n'est pas dessiné. |
| P9 | `Repeat` : un seul tour est dessiné, sous une plage « Repeat ×N » ou « Repeat forever » de la rangée Loops. Pour N > 1, une seconde plage « ×(N−1) more » couvre les tours suivants et la suite est placée après eux. Pour une boucle sans fin, la tête de lecture reboucle sur le tour dessiné. |
| P10 | Les images sont converties en secondes à 60 images par seconde, et l'élément est marqué estimé. |
| P11 | Les durées mesurées sont celles de la première occurrence de chaque action pendant la dernière lecture. Tout changement du document les efface ; si une lecture était en cours elle redémarre et mesure à nouveau. La durée d'effet d'un `MoveSprite` est fausse si un autre déplacement, `ShowSprite` ou `HideSprite` l'interrompt : limite documentée. |
| P12 | Un clic dans la règle ne déplace rien dans cette tranche : la tête de lecture revient au temps de la lecture. Un clic dans le vide de la timeline ne change pas la sélection du document. |
| P13 | Le contrôle Timeline générique reçoit deux options, **désactivées par défaut** pour qu'Animation2D ne change en rien : un défilement vertical des pistes à règle fixe, et un test de clic « le plus précis gagne » (un marqueur avant une barre, la barre la plus courte avant une plus longue). |
| P14 | Le panneau de la timeline est rattaché au document cutscene actif et détaché quand ce document se ferme, comme l'arbre et les détails. Il lit le temps de la lecture du document auquel il est rattaché. |

## Règles d'exécution pour l'agent

- **Branche `chantier/cutscene-property-editing`**. Ne jamais committer sur `main`.
- **Une seule tâche à la fois.** Avant de commencer une tâche, remplacer son icône `⏳` par `🚧`. À la fin, lancer la validation indiquée, remplacer l'icône par `✅`, `🧪` ou `⚠️`, ajouter une courte note de validation sous la tâche, puis **créer un commit dédié** qui inclut la mise à jour de ce fichier.
- **Un commit par tâche**, atomique et compilable, message en anglais au format `type(area): summary`.
- **Ne jamais pousser.** Le merge sur `main` reste une décision humaine.
- **Ne rien inventer** : toute API utilisée existe dans le dépôt et a été lue. Sinon : ⚠️ Blocked, question dans « Points ouverts », arrêt.
- **Build obligatoire** avant ✅ : `dotnet build CasaEngine.MonoGame.sln` quand le runtime est touché, `dotnet build CasaEngine.Editor.MonoGame.sln` ; **tests** `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj`.
- **Ne jamais indexer** les fichiers préexistants non suivis : `git add` fichier par fichier.
- **Aucune entrée clavier ou souris envoyée à l'éditeur.** Les captures passent par les options d'automatisation, avec `--diagnostics-out` hors dépôt. Aucun lancement automatisé n'enregistre un asset.
- **Aucun fichier de `Projects/` ni de `MGUI/` n'est modifié.** Le runtime `CasaEngine/` n'est touché que par T1.1.
- **Aucun test existant n'est modifié.**
- **Langue** : ce plan en français ; code, commits, `docs/` et ADR en anglais.
- Rappel moteur : pas d'allocation, de LINQ, de closure ni de formatage de chaîne dans `Update`, `Draw` et le layout. Le modèle de la timeline ne se reconstruit que sur un changement ; par frame, seule la position de la tête de lecture est poussée.

## Légende des statuts

- ⏳ Todo : pas encore commencé.
- 🚧 In progress : en cours de modification locale.
- 🧪 Needs testing : code écrit, validation incomplète ou en attente.
- ✅ Done : code validé, build/tests OK, commit effectué.
- ⚠️ Blocked : bloqué par une erreur non résolue ou une décision manquante.

## Validation globale

- Les deux solutions buildent à 0 erreur.
- `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj` : tous verts ; référence avant ce plan : 6070 tests ; aucun test existant modifié.
- `git diff d96c42cf -- Projects/ MGUI/` : vide. `git diff d96c42cf -- CasaEngine/` ne contient que les fichiers de T1.1.
- Captures automatisées (T2.5) : la timeline de `loop.cutscene` au repos et en lecture, celle d'`exit.cutscene`, et une sélection partagée.
- Retour arrière : un commit par tâche ; annuler une tâche revient à annuler son commit.

---

## Phase 0 — Décisions

### ✅ T0.1 — ADR-0137

- Objectif : enregistrer P1 → P14.
- Fichiers : `docs/decisions/0098-<short-title>.md` (créé), `docs/decisions/README.md`, `ai-agent/README.md` (ligne de ce plan), ce plan.
- Étapes :
  1. Écrire l'ADR en anglais avec le skill `adr`, statut `Accepted`, date 2026-11-18, source : ce plan et ADR-0135. Contenu : la timeline est une projection en lecture seule de l'arbre ; horloge de lecture et notifications par action ajoutées au directeur, avec leur sémantique exacte (P2 → P4) ; règles de projection (P5 → P11) ; panneau d'outil contextuel (P1, P14), en disant que cela précise ADR-0135, qui plaçait la timeline parmi les zones du document ; options du contrôle générique (P13) ; ce qui est reporté (déplacement de la tête de lecture, édition sur la timeline).
  2. Ligne d'index ; ligne de ce plan dans `ai-agent/README.md`.
- Validation : les liens de l'index pointent sur des fichiers existants ; aucun nom de jeu ni de projet tiers dans les fichiers touchés.
- Commit : `docs(decisions): record the read-only cutscene timeline and the play clock`
- Note de validation : ADR-0137 écrite, index et `ai-agent/README.md` mis à jour ; liens vérifiés ; aucun nom de jeu ni de projet tiers.

---

## Phase 1 — Runtime et services

### ✅ T1.1 — Horloge de lecture et notifications par action (`CasaEngine`)

- Objectif : un observateur peut suivre une lecture dans le temps sans toucher aux coroutines (P2 → P4).
- Fichiers : `CasaEngine/Framework/Cutscenes/CutsceneDirector.cs`, `CutsceneActionCoroutineFactory.cs`, `CutsceneActionNotification.cs` (créé), `CasaEngine/Framework/Scene/World/WorldRuntimeSystems.cs`, tests dans `CasaEngine.Tests/Cutscenes/`.
- Étapes :
  1. `PlayElapsedSeconds` (double) et `PlayElapsedFrames` (long), publics en lecture, remis à zéro dans `Play`. Une méthode interne les avance, appelée par `WorldRuntimeSystems.Update` **après** `CoroutineManager.Update`, seulement pendant qu'une lecture est en cours, et seulement si le jeton de lecture n'a pas changé pendant cette mise à jour (P3).
  2. Une structure en lecture seule `CutsceneActionNotification` (action, secondes, images) et un type délégué qui la reçoit par `in`. Deux événements publics sur le directeur : `ActionStarted` et `ActionEnded`.
  3. Dans `ExecuteAction` : lever `ActionStarted` à l'entrée et `ActionEnded` après le `switch`, pour toutes les actions, conteneurs compris, selon P4. Les deux notifications ne partent que si la lecture n'est ni abandonnée ni remplacée (`IsPlayAborted`). Les sorties par `yield break` sans échec (`Parallel` vide) lèvent `ActionEnded` ; les sorties par échec et les contrôles d'abandon ne le lèvent pas. Rien dans un `finally`. Aucune allocation ajoutée par action ni par frame : pas d'itérateur enveloppant, pas de closure.
  4. Une exception levée par un abonné est traitée comme celle d'un abonné d'`Ended` : journalisée, sans interrompre la lecture.
- Validation : build des deux solutions ; tests ajoutés, avec un pas fixe de 0,1 s : une lecture `Wait 1.0` lève `ActionStarted` au temps 0 et `ActionEnded` à 1,0 s à un pas près ; ordre des notifications pour `Sequence`, `Parallel` et `Repeat` de deux tours ; `MoveSprite` lève début puis fin dans la même mise à jour ; une lecture arrêtée ne lève pas `ActionEnded` pour l'action en cours ; une action refusée par l'hôte lève début et fin ; un échec dans une branche de `Parallel` ne lève plus rien pour les autres branches ; un `Parallel` vide lève début et fin ; une lecture lancée depuis un abonné d'`Ended` voit son premier pas au temps 0 ; l'horloge repart de zéro à un second `Play` et ne bouge pas hors lecture ; sans abonné, le comportement est celui d'avant. Tous les tests existants de `CasaEngine.Tests/Cutscenes` passent sans modification.
- Commit : `feat(cutscenes): expose the play clock and per-action notifications of the director`
- Note de validation : les deux solutions buildent à 0 erreur ; `CasaEngine.Tests` 6082/6082 (6070 + 12 nouveaux dans `CutsceneActionNotificationTests`), aucun test existant modifié. Horloge avancée par `frameTime.DeltaTime` (le même delta que les coroutines) ; notifications gardées par `IsPlayAborted` et l'état `Playing`.

### ✅ T1.2 — Projection de l'arbre sur le temps (`CasaEngine.EditorServices`)

- Objectif : une fonction pure qui transforme un arbre d'actions en rangées et en éléments datés, selon P5 → P11.
- Fichiers : `CasaEngine.EditorServices/Cutscenes/CutsceneTimelineProjection.cs`, `CutsceneTimelineLayout.cs` (créés), tests dans `CasaEngine.Tests/EditorServices/Cutscenes/`.
- Étapes :
  1. `CutsceneTimelineLayout` : rangées (libellé, nature), éléments (action, rôle de l'élément, rangée, début, durée, forme : barre, marqueur, plage, barre ouverte ; drapeaux « estimé » et « mesuré » ; libellé repris de `CutsceneActionSummary`), durée totale, et les bornes du tour dessiné quand la cutscene finit par une boucle sans fin. Les éléments sont rendus dans l'ordre plages, barres, marqueurs.
  2. `CutsceneTimelineProjection.Build(root, timings)` applique P5 → P10. `timings` est facultatif et donne, par action, la durée mesurée (P11).
  3. `MapPlayTime(secondes)` : rend la position de la tête de lecture, rebouclée sur le tour dessiné pour une boucle sans fin dont le tour a une durée non nulle (P9).
- Validation : tests : `loop.cutscene` et `exit.cutscene` du sample, avec rangées, débuts et durées attendus écrits en clair (pour `loop.cutscene` : rangées Loops, Flow, quatre entités, World and input ; un tour de 5,6 s ; attentes à 0, 1,3, 3,8 et 5,0 s ; `TintSprite` de 0,8 s à 1,3 s et de 1 s à 3,8 s ; `HideSprite` à 5,0 s ; pour `exit.cutscene` : `FadeScreen` de 0 à 1 s, `ChangeWorld` à 1 s) ; chaque type d'action de `CutsceneActionTypes` a une rangée ou la mention explicite « sans élément » ; `Parallel` imbriqué dans un `Repeat` ; `Repeat` de trois tours suivi d'une action ; deux barres qui se chevauchent sur la même entité ; action de durée inconnue avec et sans mesure ; images converties et marquées estimées ; `MapPlayTime` avant, dans et après le premier tour, avec un `ExitOnInput` placé avant la boucle (8 s donne 2,4 s sur `loop.cutscene`) ; arbre vide et racine qui n'est pas un conteneur. La projection ne modifie pas l'arbre.
- Commit : `feat(editor-services): project a cutscene action tree onto timeline rows and times`
- Note de validation : `CasaEngine.Editor.MonoGame.sln` 0 erreur ; `CasaEngine.Tests` 6097/6097 (6082 + 15 nouveaux dans `CutsceneTimelineProjectionTests`), aucun test existant modifié. `Build(root, timings)` prend un `IReadOnlyDictionary<CutsceneActionData, double>` facultatif. Choix de détail : un marqueur reste sur la sous-rangée 0 ; une plage `Repeat` dont le compte est absent est dessinée « Repeat ×? » comme un tour ; seule une boucle sans fin atteinte par une chaîne de `Sequence` depuis la racine fixe les bornes du tour (`LoopStartSeconds`, `LoopEndSeconds`) ; les actions placées après elle sont dessinées à la fin du tour. Le test de `loop.cutscene` donne 8 s → 2,4 s.

### ✅ T1.3 — Relevé des durées d'une lecture (`CasaEngine.EditorServices`)

- Objectif : transformer les notifications d'une lecture, qui portent des actions copiées, en durées rattachées aux actions éditées.
- Fichiers : `CasaEngine.EditorServices/Cutscenes/CutscenePlayTimings.cs` (créé), `CutsceneActionTree.cs` (ajout), tests.
- Étapes :
  1. `CutsceneActionTree.MapByShape(copie, original)` : correspondance action copiée → action éditée, par parcours parallèle de deux arbres de même forme ; échec propre si les formes diffèrent.
  2. `CutscenePlayTimings` : `Begin(carte)`, `OnStarted`, `OnEnded`, `SetEffectDuration(action, secondes)` ; ne retient que la première occurrence de chaque action (P11) ; `Clear()`, qui n'efface pas la carte d'une lecture en cours. Aucune allocation dans `OnStarted` et `OnEnded` une fois la lecture commencée.
- Validation : tests : correspondance sur `loop.cutscene` copié par sérialisation ; formes différentes refusées ; première occurrence retenue sur deux tours ; durée d'effet posée à part ; effacement.
- Commit : `feat(editor-services): record the measured timings of a cutscene play`
- Note de validation : `CasaEngine.Editor.MonoGame.sln` 0 erreur ; `CasaEngine.Tests` 6106/6106 (6097 + 9 nouveaux dans `CutscenePlayTimingsTests`), aucun test existant modifié. `MapByShape` rend `null` quand les formes diffèrent (pas de variante `Try`). `OnEnded` ignore `MoveSprite` et `TintSprite` (leur fin suit leur début, P4) : leur durée vient de `SetEffectDuration`. `Begin` dimensionne les tables sur la carte ; un test mesure zéro octet alloué dans `OnStarted` et `OnEnded`. Une propriété `Version` change à chaque mesure ou effacement.

---

## Phase 2 — Éditeur (`CasaEngine.Editor`)

### ✅ T2.1 — Options du contrôle Timeline : défilement vertical et clic précis

- Objectif : le contrôle générique reste utilisable quand les pistes dépassent sa hauteur, et tout élément visible peut être cliqué (P13).
- Fichiers : `CasaEngine.Editor/Controls/Timeline/` (contrôle, zone des pistes, en-têtes, test de clic, état de vue), tests dans `CasaEngine.Tests/Editor/`.
- Étapes :
  1. Les deux options sont données à la construction du contrôle et valent faux par défaut. Sans elles, la structure et le comportement du contrôle sont ceux d'aujourd'hui : `Animation2dTimelineControl` n'est pas modifié.
  2. Défilement vertical : la règle et le coin restent fixes ; les en-têtes de pistes et la zone des pistes défilent ensemble dans un `MGScrollViewer` vertical placé sous la règle, qui n'affiche sa barre que si le contenu dépasse. Le contrôle occupe alors la hauteur que son parent lui donne au lieu d'exiger celle de son contenu. La molette au-dessus de la zone du temps continue de zoomer et ne fait pas défiler ; au-dessus des en-têtes elle fait défiler. Si le routage des événements de MGUI ne le permet pas sans modifier MGUI : ⚠️ Blocked.
  3. `EnsureTrackVisible(trackId)` amène une piste dans la vue.
  4. Test de clic précis : sous le curseur, un élément instantané à portée gagne sur une barre, et parmi les barres et plages la plus courte gagne. Fonction pure à côté du test actuel, qui reste inchangé.
- Validation : build ; tests ajoutés : choix de l'élément dans les quatre cas (marqueur sur barre, barre sur plage, deux barres imbriquées, barre seule) ; `EnsureTrackVisible` ; avec l'option de défilement et un parent de 200 px pour 12 pistes, la hauteur du contrôle est celle du parent et le contenu défile ; sans option, la hauteur voulue d'un contrôle à 3 pistes vaut toujours règle + `12 + 34 × 3`, sans barre verticale. Les quatre fichiers de tests existants du contrôle passent sans modification.
- Commit : `feat(editor): add vertical scrolling and precise hit testing options to the timeline control`
- Note de validation : `CasaEngine.MonoGame.sln` et `CasaEngine.Editor.MonoGame.sln` 0 erreur ; `CasaEngine.Tests` 6118/6118 (6106 + 12 nouveaux dans `TimelineVerticalScrollTests` et `TimelinePreciseHitTestTests`), aucun test existant modifié. Options données par le constructeur `TimelineControl(window, verticalScroll, preciseHitTest)` ; `TimelineControl(window)` les met à faux et `Animation2dTimelineControl` n'est pas touché (hauteur d'un contrôle à 3 pistes mesurée à 156 px avant et après). Avec le défilement : règle et coin fixes dans la rangée du haut, en-têtes et zone des pistes dans un `MGScrollViewer` vertical (barre automatique) ; la règle reçoit une marge droite égale à la largeur de la barre pour rester alignée sur le temps. Le routage de la molette de MGUI suffit : sur la zone du temps elle zoome sans défiler, sur les en-têtes elle défile (testé). Le test d'entrée de la zone des pistes lit alors `LayoutBounds` (espace de mise en page non défilé) au lieu de `ActualLayoutBounds` (coordonnées écran, rognées), sinon les rangées sont décalées après un défilement ; sans l'option, le code est inchangé. Ajouts publics du contrôle : `EnsureTrackVisible`, `ScrollY`, `TrackAreaViewportHeight`, `TrackAreaContentHeight`, `IsVerticalScrollBarVisible` (pour les diagnostics de T2.4). Écart : le banc `ContentBrowserViewTestHarness.AdvanceFrame` reçoit un paramètre facultatif `scrollWheelValue` (valeur par défaut 0, comportement inchangé) pour simuler la molette. Limite : le test de clic « le plus précis gagne » prend le marqueur dont le centre est à moins de 14 px, même au-dessus d'une barre ; à égalité de durée, une barre `Duration` passe avant une plage `Range`, puis l'élément le plus tard dans la liste.

### ✅ T2.2 — La session d'aperçu suit la lecture

- Objectif : la session expose le temps de la lecture et relève ses durées.
- Fichiers : `CasaEngine.Editor/Runtime/CutscenePreviewSession.cs`, tests là où le banc sans carte graphique le permet.
- Étapes :
  1. À `Play` : construire la carte copie → original (T1.3), s'abonner aux notifications du directeur du monde d'aperçu, démarrer un `CutscenePlayTimings`. Le directeur change à chaque reconstruction du monde : se désabonner de l'ancien et s'abonner au nouveau à chaque `Play`, et se désabonner à `Dispose`. Tout changement du document efface les mesures (P11).
  2. Exposer `ElapsedSeconds`, `ElapsedFrames`, `Timings` et un numéro de version qui change quand une mesure nouvelle arrive.
  3. Effet de `MoveSprite` : à son `ActionStarted`, suivre l'entité visée et poser la durée de l'effet quand `IsMoving` repasse à faux, dans `Tick`, à partir d'une liste réutilisée.
- Validation : build ; tests verts. Le comportement visible est vérifié en T2.5.
- Commit : `feat(editor): follow the play time and measure action durations in the cutscene preview`
- Note de validation : `CasaEngine.Editor.MonoGame.sln` 0 erreur ; `CasaEngine.Tests` 6118/6118, aucun test existant modifié. Pas de test ajouté : la session exige un `HostedEditorGameAdapter` (carte graphique), absent du banc ; le comportement est vérifié par les captures de T2.5. Abonnement aux notifications fait dans `TryLoadWorld` (appelé par chaque `Play` et `SetWorld`, après `DetachDirector`), désabonnement dans `DetachDirector` (donc à `Dispose`). `ElapsedSeconds`, `ElapsedFrames`, `Timings`, `TimingsVersion`, `ClearTimings()` ; le document appelle `ClearTimings()` dans `RaiseDocumentChanged` avant de relancer une lecture en cours (qui refait `Begin`). Effet de `MoveSprite` : liste réutilisée de suivis, vérifiée dans `Tick` après la mise à jour du monde (le déplacement est déjà lancé dans la même mise à jour) ; durée = horloge − début de l'action quand `IsMoving` est faux.

### ✅ T2.3 — Panneau « Cutscene Timeline »

- Objectif : le panneau montre la projection du document cutscene actif, avec la tête de lecture et la sélection partagée.
- Fichiers : `CasaEngine.Editor/Controls/CutsceneTimelinePanel.cs`, `CasaEngine.Editor/Controls/Cutscenes/CutsceneTimelineItemRenderer.cs` (créés), `CasaEngine.Editor/Controls/CutsceneAssetInspectorPanel.cs`, `CasaEngine.Editor/Workspaces/EditorPanelIds.cs`, `CasaEngine.Editor/GameEditor.cs`, tests.
- Étapes :
  1. `CutsceneTimelinePanel(MGWindow)` avec `SetEditorPanel(document)` : un `TimelineControl` construit avec les deux options de T2.1, sans `Adapter`, pistes et éléments non éditables, dans un conteneur à **hauteur bornée** qui remplit l'onglet (pas un `MGStackPanel` vertical). Le modèle est reconstruit depuis `CutsceneTimelineProjection` sur `DocumentChanged` et quand la version des mesures change, jamais à chaque frame. Chaque élément garde le même identifiant d'une reconstruction à l'autre (table action et rôle → GUID tenue par le panneau), et la sélection est reposée après `SetModel`. `DurationSeconds` vaut la durée totale de la projection. Sans document, un texte d'attente.
  2. `CutsceneTimelineItemRenderer` : barre pleine pour une durée connue ; barre au contour pointillé et au fond plus clair pour une durée estimée ; barre ouverte à droite pour `ExitOnInput` ; plage pour `Repeat` ; losange avec son libellé pour un marqueur. Les libellés sont mesurés une fois par reconstruction, pas à chaque dessin.
  3. Tête de lecture : `GameEditor` appelle `Update` du panneau une fois par frame ; le panneau lit le temps de la lecture du document auquel il est rattaché (P14) et pousse `MapPlayTime(ElapsedSeconds)` par `SetCurrentTimeSeconds`, avec `EnsureTimeVisible` pendant une lecture. Au repos, la tête est à 0. `TimeScrubbed` remet la tête au temps de la lecture (P12).
  4. Sélection : un clic sur un élément appelle `SelectAction` du document ; `SelectedItemChanged(null)` est ignoré (P12) ; `SelectionChanged` du document sélectionne l'élément sans relever d'événement et amène sa piste dans la vue. Sous un drapeau qui évite la boucle.
  5. Barre de transport du document : un texte « secondes · images » (D3), mis à jour au plus dix fois par seconde pendant une lecture.
  6. `GameEditor` : identifiant `EditorPanelIds.CutsceneTimeline`, descripteur dans `CreatePanelRegistry`, entrée dans `ContextualToolPanelRegistry` pour `EditorDocumentKind.Cutscene`, et l'équivalent de chacun des cinq endroits codés en dur pour Animation2D cités dans « État vérifié » : fabrique de contenu qui relie le document actif, détachement au retrait du panneau, `EnsureContextualToolPanelPresent` avant `--activate-panel`, alias `cutscenetimeline`. Liaison dans `RefreshCutsceneViews` ; détachement quand le document rattaché se ferme (P14). À l'ouverture d'une cutscene, après `SyncContextualToolPanels`, `ActivateDockPanel` met l'onglet de la timeline au premier plan (P1).
- Validation : build ; tests sans carte graphique : le modèle du panneau reflète la projection de `loop.cutscene` (pistes, éléments, non éditables) ; **pour chaque élément** de ce modèle, le test de clic précis appliqué à sa position (son instant pour un marqueur, un point de sa durée non couvert par un élément plus court pour une barre ou une plage) rend ce même élément ; clic simulé sur un élément → `SelectedAction` du document ; l'élément sélectionné le reste après une reconstruction ; `SelectAction` → élément sélectionné sans boucle ; reconstruction sur `DocumentChanged` ; détachement sans abonnement restant ; `TimeScrubbed` ne change pas le temps. Capture automatisée de la timeline au repos, regardée et décrite.
- Commit : `feat(editor): show the cutscene on a read-only timeline panel`
- Note de validation : `CasaEngine.Editor.MonoGame.sln` 0 erreur ; `CasaEngine.Tests` 6126/6126 (6118 + 8 nouveaux dans `CutsceneTimelinePanelTests`), aucun test existant modifié. Un clic est simulé par `TimelineControl.SetSelectedItemId(id, true)`, l'appel exact de la zone des pistes ; le test de clic précis est vérifié pour chacun des éléments de `loop.cutscene`. Une sous-rangée de la projection devient une piste du contrôle (libellé seulement sur la première). La tête de lecture vaut `MapPlayTime(temps)` pendant une lecture et 0 sinon (elle revient donc à 0 à la fin d'une lecture). Le panneau est créé avec `CutsceneTimelineItemRenderer` ; libellé complet, puis type seul, puis type tronqué (barres) ; un marqueur n'a pas de libellé s'il est posé sur une barre ou si l'élément suivant de sa piste est trop proche ; un libellé de barre laisse la place d'un marqueur posé sur son début. Capture `60-timeline-idle` (loop.cutscene, délai 3 s, sans `--activate-panel` : l'onglet est au premier plan à l'ouverture) regardée : rangée Loops avec la plage « Repeat forever » de 0 à 5,6 s, rangée Flow avec les attentes, rangées d'entités avec marqueurs en losange, barres `MoveSprite` estimées au contour pointillé et barres `TintSprite` pleines ; texte de transport « Stopped 0.00 s · 0 frames » ; tête de lecture à 0. Constat O1 : dans le groupe du bas, seules quatre rangées sont visibles sur neuf ou plus ; la barre verticale est affichée et les autres rangées défilent.

### ✅ T2.4 — Diagnostics d'automatisation

- Objectif : pouvoir prouver par un fichier ce que la timeline affiche.
- Fichiers : `CasaEngine.Editor/GameEditor.cs`, `CasaEngine.Editor/Controls/CutsceneAssetInspectorPanel.cs` ou le panneau de timeline, tests si une option est ajoutée.
- Étapes :
  1. Les diagnostics d'une cutscene écrivent : le temps de la lecture en secondes et en images, la position de la tête de lecture, le nombre de pistes et d'éléments, puis une ligne par élément (piste, libellé, début, durée, forme, estimé ou mesuré), l'élément sélectionné, la hauteur visible et la hauteur du contenu de la zone des pistes, et si la barre verticale est affichée.
  2. `--activate-panel cutscenetimeline` est appliqué après l'ouverture de la cutscene.
- Validation : build ; tests verts ; un lancement sur `loop.cutscene` produit ces lignes.
- Commit : `feat(editor): write the cutscene timeline state in the automation diagnostics`
- Note de validation : `CasaEngine.Editor.MonoGame.sln` 0 erreur ; `CasaEngine.Tests` 6126/6126, aucun test existant modifié. `CutsceneTimelinePanel.AppendAutomationState` écrit temps de lecture (s, images), tête de lecture, durée totale, texte de transport, nombre de pistes et d'éléments, une ligne par élément (piste, libellé, début, durée, forme, exact/estimé/mesuré), élément sélectionné, hauteurs visible et du contenu, débordement et barre verticale ; appelé par `AppendCutscenePreviewDiagnostics`. Le point 2 (`--activate-panel cutscenetimeline` après ouverture) était déjà livré par T2.3 (alias et `EnsureContextualToolPanelPresent`). Lancement sur `loop.cutscene` : 7 pistes, 26 éléments, zone des pistes 150 px visibles pour 250 px de contenu, barre verticale affichée (O1).

### ✅ T2.5 — Captures de validation

- Objectif : produire les captures de la tranche et vérifier qu'elles montrent une timeline lisible.
- Étapes, sur `Projects/SpriteCutsceneSample`, timeline au premier plan, `--diagnostics-out` hors dépôt :
  1. `loop.cutscene` au repos : rangée Loops avec la plage « Repeat forever », rangée Flow avec les quatre attentes, une rangée par entité avec marqueurs et barres, rangée des entrées avec la barre ouverte d'`ExitOnInput` ; durées de `MoveSprite` estimées.
  2. `loop.cutscene` en lecture, `--capture-delay 2` : le délai se compte depuis la dernière étape d'automatisation, la capture arrive donc quelques frames après 2 s de lecture. Les diagnostics donnent un temps de lecture d'au moins 2 s et une tête de lecture égale à `MapPlayTime` de ce temps ; le temps est affiché dans la barre de transport ; les durées de `MoveSprite` sont mesurées, entre 1,05 et 1,15 s.
  3. `loop.cutscene` en lecture, `--capture-delay 8` : la tête de lecture a rebouclé dans le tour dessiné (P9) : elle vaut le temps de lecture moins 5,6 s, à une frame près.
  4. `exit.cutscene` : barre `FadeScreen` d'une seconde sur la rangée Screen, marqueur `ChangeWorld` à 1 s.
  5. `loop.cutscene` avec une action `TintSprite` sélectionnée par `--cutscene-select-action` : le même élément est en surbrillance dans la timeline et dans l'arbre.
- Validation : chaque capture est regardée et décrite honnêtement ; les valeurs des diagnostics sont comparées aux attentes ci-dessus ; les diagnostics disent si la zone des pistes déborde et si la barre verticale est alors affichée (O1). Élément illisible, piste coupée ou libellés qui se chevauchent : défaut à corriger avant ✅, ou à noter en 🧪. `git status` ne montre aucun nouveau fichier sous `Projects/`.
- Limite d'essais : deux séries de corrections au plus dans cette tâche ; au-delà, 🧪 avec la liste exacte de ce qui reste.
- Commit : `docs(plan): record the validation screenshots of the cutscene timeline`, ou `fix(editor): …` si une correction est nécessaire.
- Note de validation : captures `70-idle`, `71-play-2s`, `72-play-8s`, `73-exit`, `74-select-tint` dans le dossier de travail de l'agent (hors dépôt), toutes regardées ; `git status` : aucun nouveau fichier sous `Projects/` ; aucune correction de code (donc commit `docs(plan)`). Diagnostics comparés : au repos 7 pistes, 26 éléments, plage « Repeat forever » 0 à 5,6 s, attentes à 0, 1,3, 3,8 et 5,0 s, `MoveSprite` estimés d'1 s. En lecture 2 s : temps 2,000 s (120 images), tête à 2,000 s, texte « Playing 2.00 s · 120 frames », `MoveSprite` mesurés à 1,083 s. En lecture 8 s : temps 8,000 s, tête à 2,400 s (= 8 − 5,6, rebouclée), visible à la bonne position sur la capture. `exit.cutscene` : `FadeScreen` de 0 à 1 s sur Screen, `ChangeWorld` à 1 s sur World and input, pas de débordement. Sélection : le `TintSprite` d'`orb-left` a un contour blanc et sa rangée est en surbrillance dans la timeline, et la ligne est surlignée dans l'arbre ; l'Inspector montre ses propriétés. Constats honnêtes (O1 confirmé) : (1) dans le groupe du bas, 150 px de zone des pistes pour 250 px de contenu : seules Loops, Flow, orb-left et orb-right sont visibles ; les entités du bas et la barre ouverte d'`ExitOnInput` demandent de défiler (barre verticale affichée) ; (2) les libellés de barres courtes sont tronqués (« TintSp.. », « MoveSpr.. ») ; ce sont des limites de place, pas des chevauchements. Non corrigé : agrandir le groupe du bas relève de la disposition par défaut du dock (hors périmètre, l'utilisateur peut redimensionner ou maximiser l'onglet). À trancher à la recette (O1).
- Note du 2026-11-18 (corrections après les captures) : (1) au premier agencement avec un modèle, et à chaque nouveau document, le zoom ajuste la projection plus 4 % de marge à la largeur de la zone du temps, dans les limites du contrôle (`FitTimeToWidth`) ; une reconstruction du même document garde le zoom de l'utilisateur ; tant que l'utilisateur n'a pas zoomé, un changement de largeur (barre verticale, redimensionnement) réajuste. (2) La piste de la sélection est amenée en vue par `Update`, deux images après la sélection et dès que la zone des pistes a une hauteur : avant l'agencement, la hauteur était nulle. (3) Le temps de lecture passe de la timeline à la barre de transport du document (rafraîchi au plus dix fois par seconde) ; le contrôle générique accepte `trackRowHeight` (34 par défaut) et la timeline des cutscenes l'utilise à 23 px (24 px laissait 1 px de trop et la barre verticale réapparaissait). (4) `CutscenePlayTimings.Version` ne change plus que pour les durées utilisées par la projection (`CutsceneTimelineProjection.UsesMeasuredDuration`). (5) Sans sélection, aucune piste n'est en surbrillance. Captures `80-idle`, `81-play-2s`, `82-select-last-row`, `83-reactivate`, `84-exit`, toutes regardées : `loop.cutscene` au repos, tour de 5,6 s sur presque toute la largeur, les 7 rangées visibles sans défilement (zone 179 px, aucune barre verticale), libellés « MoveSprite » et « TintSprite » entiers, aucune rangée en surbrillance ; en lecture 2 s : « Playing 2.00 s · 120 frames » dans la barre de transport du document, tête à 2 s, `MoveSprite` mesurés ; sélection de la dernière ligne : `TintSprite` de ring-right visible, contour blanc, rangée en surbrillance ; `exit.cutscene` : zoom au maximum du contrôle (288 px/s), la barre d'1 s n'occupe qu'environ un cinquième de la largeur (limite de zoom, rien de plus à faire). Limite : sur `83-reactivate` l'onglet du bas affiché après la réactivation est le Content Browser, la timeline n'est donc pas visible sur cette capture (diagnostic : sélection conservée) ; la mise en vue attend que l'onglet ait une hauteur, ce que les tests headless couvrent mais que la capture ne montre pas.

---

## Phase 3 — Documentation et recette

### ✅ T3.1 — Documentation

- Objectif : `docs/editor/cutscene-editor.md` décrit la timeline, ses rangées, la lecture des formes (barre, estimée, ouverte, plage, marqueur), la tête de lecture et ses limites ; `docs/editor/timeline-generic.md` mentionne le défilement vertical.
- Fichiers : ces deux documents, ce plan, `ai-agent/README.md`.
- Validation : relecture contre le code livré.
- Commit : `docs(editor): document the cutscene timeline`
- Note de validation : relu contre le code livré (`CutsceneTimelinePanel`, `CutsceneTimelineItemRenderer`, `TimelineControl`, `CutsceneTimelineLayout`, registre de `GameEditor`). `cutscene-editor.md` gagne la section Timeline (rangées, formes, mise en page du temps, tête de lecture, sélection, défilement, limites), les diagnostics et la section Code, et ses limites et suites sont mises à jour ; `timeline-generic.md` gagne une note sur les deux options du contrôle. Ligne de `ai-agent/README.md` mise à jour. Aucun nom de jeu ni de projet tiers.

### ✅ T3.2 — Recette de l'auteur

- Validation à distance sur les captures de T2.5, puis recette réelle à la fin (D5). Reste 🧪 d'ici là.
- Note de validation : l'auteur valide sur les captures de T2.5 et fera la recette réelle à la fin ; rien d'autre à faire côté agent.
- Note de clôture (2026-11-19) : ✅. Recette réelle de l'auteur dans l'éditeur acceptée le 2026-11-19 (« recette OK »), pour les tranches A à G ensemble. L'auteur accepte le même jour les arbitrages P1 à P14 en bloc (« Acceptés en bloc »). Les autres points ouverts restent écrits tels quels : cette clôture ne les tranche pas. Plan terminé et archivé.

---

## Points ouverts

| Réf | Sujet | Tâche concernée |
|---|---|---|
| O1 | Le groupe d'onglets du bas fait environ 250 px de haut à 900 px : la timeline de `loop.cutscene` y tient-elle sans défilement ? À juger sur les captures. | T2.5 |
| O2 | En aperçu, `FadeMusic` n'attend pas faute de service de son : sa barre montre la durée déclarée, la lecture va plus vite. | T1.2 |
| O3 | Une boucle sans fin dont le tour contient une action de durée inconnue non encore mesurée : le rebouclage de la tête de lecture ne devient exact qu'après un premier tour. | T1.2, T2.3 |
| O4 | Levé le 2026-11-19 : l'auteur accepte en bloc les arbitrages P1 à P14 (« Acceptés en bloc »). | Toutes |

## Hors périmètre

- Déplacer la tête de lecture, pause, pas à pas : tranche D.
- Déplacer, étirer, ajouter ou supprimer sur la timeline ; placement libre : tranches E et F.
- Courbes et images clés par propriété.
- Surbrillance de l'action en cours d'exécution dans l'arbre.
- Zoom vertical, repli des rangées, renommage des rangées.
- Tout changement du format `.cutscene` ou de MGUI.
