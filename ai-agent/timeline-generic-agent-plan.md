# Plan agent IA — Timeline generique CasaEngine

Source de conception : [`docs/timeline-generic.md`](../docs/timeline-generic.md) (lire la
« Note de revue » en tete, elle fait autorite). Ce plan transforme la vision en petites
taches executables et verifiables.

But : faire evoluer le controle `TimelineControl` (actuellement specialise « animation
event ponctuel ») vers une base generique `Track` / `Item` reutilisable, **sans casser
l'editeur Animation2D existant**, seul consommateur reel et seul critere de validation.

## Decisions verrouillees

- Le perimetre est la **base timeline generique**, validee **uniquement** avec l'editeur
  Animation2D existant.
- Le modele generique passe en **`public sealed`**, au meme emplacement
  `CasaEngine.Editor.Controls.Timeline`.
- Le renommage `Lane`/`Event` -> `Track`/`Item` est propage **jusqu'a l'API Animation2D**
  (`Animation2dTimelineControl`, ses records, ses evenements publics,
  `Animation2dAssetInspectorPanel`).
- L'approche est **phasee** : Phase 1 renommage (comportement strictement inchange),
  Phase 2 `Duration`/`Kind`, puis abstractions (policy, adapter, renderer, menu, playback,
  unite de temps).
- **Pas de modele cutscene plat.** Le modele cutscene reel est l'arbre d'actions
  `Sequence` / `Parallel` (`CutsceneAsset.RootAction`) joue par `CutsceneDirector` (sans
  `Seek`/`Pause`/`Update`). L'editeur cutscene reste un controle d'arbre, sujet distinct
  (voir `ai-agent/cutscene-implementation-plan.md`). Aucune tache de ce plan ne cree de
  `CutsceneTimelineAdapter`.
- `TimelineEditOperation` (cite dans la vision mais jamais defini) est **abandonne**.

## Regles obligatoires pour l'agent IA

1. Une seule tache a la fois, dans l'ordre du plan.
2. Avant de commencer une tache, remplacer son icone `⏳` par `🚧`.
3. A la fin d'une tache, remplacer son icone par `✅`, `🧪` ou `⚠️`, et ajouter un court
   bloc « Etat actuel » sous la tache si une nuance merite d'etre tracee.
4. Toute tache terminee doit mettre a jour ce fichier **dans le meme commit**.
5. Chaque tache produit **un commit non vide** si du code ou de la doc change.
6. Le build editeur doit rester vert **a la fin de chaque tache** (jamais d'etat non
   compilable entre deux commits).
7. L'editeur Animation2D doit rester fonctionnel a chaque etape : aucune regression sur la
   selection, le scrub, le rename de track, le copy/paste/duplicate, le drag, le zoom et le
   scroll deja en place.
8. Ne pas reintroduire la cutscene comme timeline plate, ni inventer d'API cutscene.
9. Ne pas inventer une API non verifiee sans la documenter explicitement comme nouvelle
   decision dans ce fichier.
10. Pas de regression de performance du viewport : pas de controle enfant par item, pas de
    LINQ dans le rendu ou le hit testing, pas d'allocation par frame, pas de dessin hors
    zone visible.

## Validation globale

- Build editeur : `dotnet build CasaEngine.Editor/CasaEngine.Editor.csproj -c Debug --no-restore`
- Build solution editeur : `dotnet build CasaEngine.Editor.MonoGame.sln -c Debug --no-restore`
- Tests timeline : `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj -c Debug --filter FullyQualifiedName~Timeline --no-restore`
- Tests animation2d : `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj -c Debug --filter FullyQualifiedName~Animation2d --no-restore`
- Smoke manuel : ouvrir un `.anim2d` dans `GameEditor`, verifier preview, timeline graduee,
  scroll, zoom, selection d'item, inspector, playhead, scrub, rename de track,
  copy/paste/duplicate, drag d'item.

## Etat verifie du code (point de depart)

- `TimelineModel.cs` : `internal sealed` ; `TimelineModel { DurationSeconds, Lanes, Events }`,
  `TimelineLane { Id, Label, IsEditable }`,
  `TimelineEvent { Id, LaneId, TimeSeconds, EventType, ValueText, ToolTipText, IsEditable }`.
- `TimelineControl.cs` : evenements publics `PixelsPerSecondChanged`, `SelectedEventChanged`,
  `SelectedLaneChanged`, `TimeScrubbed`, `EventTimeEditCommitted`, `InsertRequested`,
  `DuplicateRequested`, `DeleteRequested`, `CopyRequested`, `PasteRequested`,
  `TrackRenameRequested`, `LaneLabelEditCommitted` ; methodes internes `GetLane*`,
  `FindEvent`, `GetSelectedEvent`, `SetSelectedEventId`, `SetSelectedLaneId`,
  `CreateContextMenu`, `CreateTrackHeaderContextMenu`.
- `TimelineViewState.cs` : `SelectedEventId`, `SelectedLaneId`.
- `TimelineViewport.cs` : `DrawEvents`, `DrawDiamond`, hit testing via
  `TimelineHitTest.HitTestNearestEvent`, drag d'event, tooltip.
- `TimelineHitTest.cs` : `HitTestNearestEvent`.
- `Animation2dTimelineControl.cs` : sous-classe-adaptateur. Records
  `Animation2dTimelineLaneData`, `Animation2dTimelineEventData`. Evenements `EventSelected`,
  `LaneSelected`, `LaneLabelEdited`, `TrackPropertyInsertRequested`, `TrackRequested`,
  `TrackDeleted`, `EventCopied`, `EventPasted`, `PersistedEventInsertRequested`,
  `ScrubRequested`, `EventTimeEdited`, `EventDuplicated`, `EventDeleted`,
  `LaneInsertRequested`. Override de `CreateContextMenu` / `CreateTrackHeaderContextMenu`.
- `Animation2dAssetInspectorPanel.cs` : abonne ~14 evenements ci-dessus, appelle
  `SetTimelineData(...)` / `SetPlaybackState(...)`, et porte des handlers
  `OnTimelineEvent*` / `OnTimelineLane*` a renommer en consequence.
- Tests existants : `TimelineViewTransformTests` dans `CasaEngine.Tests`.

---

# Phase 1 — Renommage generique Lane/Event -> Track/Item (comportement inchange)

Objectif global de la phase : adopter le vocabulaire generique partout et rendre le modele
public, **sans aucun changement de comportement** (tous les items restent ponctuels). Aucun
ajout de `Duration` dans cette phase.

### ✅ Tache 1.1 — Rendre le modele public et renommer les types

Objectif : `TimelineModel`/`TimelineTrack`/`TimelineItem` publics, avec le vocabulaire
generique, en gardant un build vert.

Contraintes :

- `internal sealed` -> `public sealed` pour `TimelineModel`, `TimelineTrack`, `TimelineItem`.
- Renommer : `TimelineLane` -> `TimelineTrack`, `TimelineEvent` -> `TimelineItem`,
  `TimelineModel.Lanes` -> `Tracks`, `TimelineEvent.LaneId` -> `TimelineItem.TrackId`,
  `TimelineEvent.TimeSeconds` -> `TimelineItem.StartTime`,
  `TimelineEvent.EventType` -> `TimelineItem.ItemType`.
- Conserver `Id`, `Label`, `IsEditable`, `ValueText`, `ToolTipText`. Aucun champ nouveau.
- Mettre a jour **toutes** les references du dossier `Controls/Timeline` pour compiler.

Fichiers cibles :

- `CasaEngine.Editor/Controls/Timeline/TimelineModel.cs`
- toutes les references internes : `TimelineControl.cs`, `TimelineViewport.cs`,
  `TimelineHitTest.cs`, `TimelineTrackHeaderPanel.cs`, `TimelineRuler.cs`,
  `TimelineViewState.cs`, et `Animation2dTimelineControl.cs` (uniquement pour compiler).

Validation :

- build editeur ;
- aucun changement de rendu ou d'interaction (verification visuelle equivalente).

Commit attendu :

- `refactor(timeline): rename model to generic track/item and make it public`

### ✅ Tache 1.2 — Renommer l'API publique et interne de `TimelineControl`

Objectif : aligner evenements et methodes du controle sur le vocabulaire `Item`/`Track`.

Contraintes (renommages) :

- Evenements : `SelectedEventChanged` -> `SelectedItemChanged`,
  `SelectedLaneChanged` -> `SelectedTrackChanged`,
  `EventTimeEditCommitted` -> `ItemTimeEditCommitted`,
  `LaneLabelEditCommitted` -> `TrackLabelEditCommitted`. (Les payloads passent a
  `TimelineItem?` / `TimelineTrack`.)
- `ViewState` : `SelectedEventId` -> `SelectedItemId`, `SelectedLaneId` -> `SelectedTrackId`.
- Methodes : `GetLane*`/`GetSelectedLane`/`GetLaneAtY`/`GetLaneIndex`/`GetLaneBounds`/
  `GetLaneCount` -> equivalents `Track` ; `FindEvent`/`GetSelectedEvent` -> `FindItem`/
  `GetSelectedItem` ; `SetSelectedEventId`/`SetSelectedLaneId` -> `SetSelectedItemId`/
  `SetSelectedTrackId` ; `CommitDraggedEventTime`/`DuplicateDraggedEvent` -> `*Item*`.
- Garder `TrackRenameRequested`, `InsertRequested`, `DuplicateRequested`, `DeleteRequested`,
  `CopyRequested`, `PasteRequested`, `TimeScrubbed` (deja generiques) mais ajuster les
  payloads typed vers `TimelineItem`/`TimelineTrack`.

Fichiers cibles :

- `TimelineControl.cs`, `TimelineViewState.cs`, `TimelineViewport.cs`, `TimelineHitTest.cs`
  (`HitTestNearestEvent` -> `HitTestNearestItem`), `TimelineTrackHeaderPanel.cs`,
  `Animation2dTimelineControl.cs` (mise a jour des abonnements pour compiler).

Validation :

- build editeur ;
- comportement editeur inchange.

Commit attendu :

- `refactor(timeline): rename control events and methods to item/track vocabulary`

### ✅ Tache 1.3 — Propager le renommage a la couche Animation2D et aux tests

Objectif : terminer le renommage jusqu'a l'API Animation2D et l'inspector, sans regression.

Contraintes (renommages) :

- Records : `Animation2dTimelineLaneData` -> `Animation2dTimelineTrackData`,
  `Animation2dTimelineEventData` -> `Animation2dTimelineItemData`.
- Evenements de `Animation2dTimelineControl` :
  `EventSelected` -> `ItemSelected`, `LaneSelected` -> `TrackSelected`,
  `LaneLabelEdited` -> `TrackLabelEdited`, `EventCopied` -> `ItemCopied`,
  `EventPasted` -> `ItemPasted`, `PersistedEventInsertRequested` ->
  `PersistedItemInsertRequested`, `EventTimeEdited` -> `ItemTimeEdited`,
  `EventDuplicated` -> `ItemDuplicated`, `EventDeleted` -> `ItemDeleted`,
  `LaneInsertRequested` -> `TrackInsertRequested`. Conserver `TrackPropertyInsertRequested`,
  `TrackRequested`, `TrackDeleted`, `ScrubRequested` (deja en vocabulaire track/scrub).
- `Animation2dAssetInspectorPanel.cs` : mettre a jour les abonnements et renommer les
  handlers `OnTimelineEvent*`/`OnTimelineLane*` de maniere coherente.
- Mettre a jour les tests qui referencent les anciens noms.

Fichiers cibles :

- `CasaEngine.Editor/Controls/Animation2dTimelineControl.cs`
- `CasaEngine.Editor/Controls/Animation2dAssetInspectorPanel.cs`
- tests concernes dans `CasaEngine.Tests` (au moins `TimelineViewTransformTests` si touche).

Validation :

- build editeur + build solution ;
- tests `~Timeline` et `~Animation2d` ;
- smoke manuel complet (selection, scrub, rename, copy/paste/duplicate, drag, zoom, scroll).

Commit attendu :

- `refactor(animation2d): propagate track/item rename through editor integration`

Etat actuel :

- Renommes : les deux records-pont (`Animation2dTimelineTrackData`,
  `Animation2dTimelineItemData`, membres `TrackIndex`/`StartTime`) et les 10 evenements
  publics de `Animation2dTimelineControl`, plus leurs abonnements dans l'inspector.
- **Volontairement non renomme** : le domaine d'affichage interne de l'inspector
  (`TimelineDisplayLane`, `_selectedLaneIndex`, `SelectEvent`/`SelectLane`, et surtout
  `EditorHistorySnapshot.SelectedLaneIndex` qui est serialise). Le renommer casserait la
  compatibilite des snapshots d'historique sans rien apporter a la frontiere timeline. Seule
  l'API qui parle au controle generique a ete migree vers Track/Item.
- Validation : build editeur + build tests verts ; 36 tests `~Timeline`/`~Animation2d`
  reussis. Smoke manuel `GameEditor` non execute en CLI.

---

# Phase 2 — Duree et type d'item (`Duration` + `Kind`)

Objectif global de la phase : introduire la duree et le type visuel d'item dans le modele,
le rendu et l'interaction, avec des defauts **retro-compatibles** (Animation2D reste en items
ponctuels au rendu identique).

### ✅ Tache 2.1 — Ajouter `Duration`, `TimelineItemKind` et les drapeaux d'edition

Objectif : enrichir `TimelineItem` sans changer le comportement par defaut.

Contraintes :

- Ajouter `public float Duration { get; set; } = 0f;`.
- Ajouter `public enum TimelineItemKind { Instant, Duration, Range, Marker }` (public) et
  `public TimelineItemKind Kind { get; set; } = TimelineItemKind.Instant;`.
- Ajouter `DisplayName` (texte affiche), `CanMove = true`, `CanResizeStart = false`,
  `CanResizeEnd = false`, `object? Source = null`.
- Les items Animation2D sont construits en `Instant`, `Duration = 0`. Aucun changement
  visuel ou d'interaction.

Fichiers cibles :

- `TimelineModel.cs`, `Animation2dTimelineControl.cs` (construction des items en `Instant`).

Validation :

- build editeur ; rendu et interaction inchanges.

Commit attendu :

- `feat(timeline): add item duration and kind to generic model`

### ✅ Tache 2.2 — Rendu par type : remplacer `DrawEvents` par `DrawItems`

Objectif : router le rendu selon `Kind` tout en conservant le visuel actuel pour `Instant`.

Contraintes :

- `DrawEvents` -> `DrawItems`, dispatch vers `DrawInstantItem` (losange actuel),
  `DrawDurationItem` (bloc avec `DisplayName`), `DrawMarkerItem`, `DrawRangeItem`.
- `Instant` doit produire exactement le visuel actuel (losange) ; les autres rendus peuvent
  rester minimalistes mais corrects.
- Conserver le filtrage hors zone visible et l'absence d'allocation par frame.

Fichiers cibles :

- `TimelineViewport.cs`.

Validation :

- build editeur ; le rendu Animation2D (losanges) est inchange ; un item de test `Duration`
  s'affiche en bloc.

Commit attendu :

- `feat(timeline): render items by kind (instant/duration/marker/range)`

### ✅ Tache 2.3 — Hit testing generique avec zones

Objectif : un hit testing qui distingue corps d'item et bords de redimensionnement.

Contraintes :

- Ajouter `TimelineHitTestResult { Track, Item, Area, Time }` et
  `enum TimelineHitTestArea { None, TrackHeader, TrackBody, ItemBody, ResizeStart, ResizeEnd, Ruler, Playhead }`.
- Items `Instant` : `Area = ItemBody` (conserver la tolerance de clic actuelle).
- Items `Duration` : `ItemBody` au centre, `ResizeStart`/`ResizeEnd` sur les bords.
- Pas de LINQ, pas d'allocation par frame.

Fichiers cibles :

- `TimelineHitTest.cs`, `TimelineViewport.cs`.

Validation :

- build editeur ; tests cibles de hit testing (instant et duration) ; selection Animation2D
  inchangee.

Commit attendu :

- `feat(timeline): add area-aware hit testing for items`

### ✅ Tache 2.4 — Deplacement et redimensionnement des items a duree

Objectif : permettre move et resize des items `Duration`, sans casser le drag des items
`Instant`.

Contraintes :

- Move : deplace `StartTime` (les items `Instant` gardent le comportement actuel).
- Resize gauche : modifie `StartTime` et `Duration` ; resize droite : modifie `Duration`.
- Duree minimale respectee ; clamp dans `[0, fin de timeline]`.
- Respecter les drapeaux `CanMove` / `CanResizeStart` / `CanResizeEnd`.
- Validation/snap fins delegues a la policy en Phase 3 ; ici, garde-fous inline minimaux.

Fichiers cibles :

- `TimelineViewport.cs`, `TimelineControl.cs` (notifications `ItemTimeEditCommitted` /
  nouvelle notification de resize si necessaire).

Validation :

- build editeur ; tests cibles de resize (bornes, duree minimale) ; drag d'item Animation2D
  inchange.

Commit attendu :

- `feat(timeline): support move and resize of duration items`

### ✅ Tache 2.5 — Cloture du coeur : build, tests, smoke

Objectif : verrouiller la fin du coeur generique avec une verification reproductible.

Validation :

- `dotnet build CasaEngine.Editor/CasaEngine.Editor.csproj -c Debug --no-restore`
- `dotnet build CasaEngine.Editor.MonoGame.sln -c Debug --no-restore`
- `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj -c Debug --filter FullyQualifiedName~Timeline --no-restore`
- `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj -c Debug --filter FullyQualifiedName~Animation2d --no-restore`
- smoke manuel complet dans `GameEditor`.

Etat actuel :

- build editeur : vert ; build solution `CasaEngine.Editor.MonoGame.sln` : vert (0 erreur) ;
- tests `~Timeline` + `~Animation2d` : 36 reussis, 0 echec ;
- les items Animation2D restent `Instant` (rendu losange inchange) ; le rendu blocs,
  le hit-test par zones et le move/resize ne sont exerces que par des items `Duration`,
  non presents en Animation2D ;
- smoke manuel `GameEditor` non execute en CLI.

Commit attendu :

- `test(timeline): validate generic core (rename + duration/kind)`

---

# Phase 3 — Abstractions d'edition (policy, snap, adapter)

Objectif global : sortir les regles metier et la construction du modele hors du controle,
validees par une implementation Animation2D.

### ✅ Tache 3.1 — `ITimelineEditPolicy` + snap + validation

Objectif : centraliser snap et regles d'edition derriere une policy branchee sur le controle.

Contraintes :

- Definir `ITimelineEditPolicy` (`SnapTime`, `CanMoveItem`, `CanResizeItem`,
  `CanInsertItem`, `ValidateMove`), `TimelineSnapSettings { IsEnabled, Mode, Step, FrameRate }`,
  `enum TimelineSnapMode { None, Step, Frame, Markers, Items }`,
  `TimelineValidationResult { IsValid, Message, Valid, Error(...) }`,
  `TimelineSnapContext`.
- `TimelineControl.EditPolicy` optionnel ; move/resize/insert consultent la policy quand
  presente, sinon comportement actuel.
- Fournir `Animation2dTimelineEditPolicy` (snap sur frames) branchee par l'editeur
  Animation2D.

Fichiers cibles :

- nouveau dossier `Controls/Timeline/Editing/` ; `TimelineControl.cs`, `TimelineViewport.cs` ;
  cote Animation2D, la policy concrete.

Validation :

- build editeur ; tests cibles (snap frame/step, `ValidateMove`, bornes de resize).

Commit attendu :

- `feat(timeline): add edit policy with snapping and validation`

Etat actuel :

- ajoutes : `ITimelineEditPolicy`, `TimelineSnapSettings`, `TimelineSnapMode`,
  `TimelineSnapContext`, `TimelineValidationResult` (dossier `Editing/`), et
  `Animation2dTimelineEditPolicy` (snap frame/step).
- branche : `TimelineControl.EditPolicy` + `SnapSettings` ; le snap est applique pendant le
  drag (`SnapTime`) et les commits move/resize consultent `CanMove`/`CanResize` + `ValidateMove`.
- **comportement Animation2D inchange** : l'editeur branche la policy mais laisse
  `SnapSettings.IsEnabled = false`, donc aucun snap par defaut et les valeurs de drag restent
  identiques. Le snapping est une capacite activable plus tard.

### ✅ Tache 3.2 — `ITimelineAdapter` + `Animation2dTimelineAdapter`

Objectif : isoler la construction du modele et la traduction des intentions d'edition.

Contraintes :

- Definir `ITimelineAdapter` (`BuildModel`, `MoveItem`, `ResizeItem`, `DeleteItem`,
  `DuplicateItem`, `InsertItem`, `RenameTrack`, `OnSelectionChanged`,
  `OnCurrentTimeChanged`).
- Implementer `Animation2dTimelineAdapter` qui reprend le mapping `Guid <-> index` et la
  logique actuellement portee par `Animation2dTimelineControl`, sans regression.
- **Decision verrouillee** : l'adapter **remplace** le cablage par evenements actuel. La
  traduction des intentions d'edition (move/resize/delete/duplicate/insert/rename/selection/
  temps courant) passe desormais par `ITimelineAdapter`, et non plus par les evenements
  publics historiques de `Animation2dTimelineControl` lorsqu'un adapter est present.

Etat actuel :

- ajoute : `ITimelineAdapter` (dossier `Editing/`) + `TimelineControl.Adapter`.
- routage : quand un adapter est present, `TimelineControl` route move, resize, delete,
  duplicate, insert, rename de track et temps courant vers l'adapter (avec repli sur les
  evenements publics quand `Adapter == null`, pour les consommateurs purement event-based).
- `Animation2dTimelineControl` implemente `ITimelineAdapter` (implementation explicite) et se
  branche comme son propre adapter (`Adapter = this`). La traduction `Guid <-> index` et la
  notification de l'inspector (evenements index-based historiques) sont preservees.
- **Restees sur evenements (par securite, comportement identique)** : la selection
  (`SelectedItemChanged`/`SelectedTrackChanged`) et le presse-papier (`CopyRequested`/
  `PasteRequested`). `ITimelineAdapter` ne definit pas copy/paste ; la selection garde sa
  logique de de-duplication d'origine cote inspector. `OnSelectionChanged` existe sur
  l'interface mais n'est pas appele par le controle pour Animation2D.
- subclass `Animation2dTimelineControl` conserve (les menus contextuels restent override ;
  leur passage en provider est la tache 4.2). Le smoke manuel `GameEditor` reste a faire.

Fichiers cibles :

- nouveau dossier `Controls/Timeline/Adapters/` ; `Animation2dTimelineControl.cs` /
  `Animation2dAssetInspectorPanel.cs` selon le cablage retenu.

Validation :

- build editeur ; smoke manuel complet (toutes les actions d'edition Animation2D).

Commit attendu :

- `feat(timeline): add timeline adapter and animation2d implementation`

### ✅ Tache 3.3 — Tests des abstractions d'edition

Objectif : couvrir le coeur deterministe des nouvelles abstractions.

Doit couvrir au moins :

- snap `Frame` et `Step` ;
- `ValidateMove` (cas valides et invalides) ;
- bornes de resize et duree minimale ;
- construction de modele par l'adapter Animation2D (tracks/items attendus).

Fichiers cibles :

- `CasaEngine.Tests` (tests timeline).

Validation :

- `dotnet test ... --filter FullyQualifiedName~Timeline`.

Commit attendu :

- `test(timeline): cover edit policy and adapter behavior`

---

# Phase 4 — Rendu et menus extensibles

### ✅ Tache 4.1 — `ITimelineItemRenderer` + renderer par defaut

Objectif : extraire le dessin des items hors du viewport.

Contraintes :

- Definir `ITimelineItemRenderer` (`DrawItem`, `HitTest`), `TimelineRenderContext`,
  `[Flags] TimelineItemVisualState { None, Selected, Hovered, Dragging, Invalid, Disabled }`.
- `DefaultTimelineItemRenderer` reproduit le visuel actuel (losange instant, bloc duration).
- `TimelineControl.ItemRenderer` optionnel ; le viewport delegue au renderer.

Fichiers cibles :

- nouveau dossier `Controls/Timeline/Rendering/` ; `TimelineViewport.cs`.

Validation :

- build editeur ; rendu Animation2D inchange.

Commit attendu :

- `feat(timeline): extract item rendering behind a renderer interface`

### ✅ Tache 4.2 — `ITimelineContextMenuProvider` + migration Animation2D

Objectif : remplacer le sous-classement des menus par une composition.

Contraintes :

- Definir `ITimelineContextMenuProvider` (`CreateContextMenu(timeline, track, item, cursorTime)`
  et un equivalent pour le header de track).
- Deplacer les overrides actuels (`CreateContextMenu` / `CreateTrackHeaderContextMenu` de
  `Animation2dTimelineControl`) dans un `Animation2dTimelineContextMenuProvider`.
- `TimelineControl` utilise le provider si present, sinon son menu par defaut. Si la
  suppression du sous-classement de `Animation2dTimelineControl` est trop large pour une
  tache, garder la classe mais deleguer aux provider, et le noter.

Fichiers cibles :

- nouveau dossier `Controls/Timeline/Menu/` ; `TimelineControl.cs`,
  `Animation2dTimelineControl.cs`.

Validation :

- build editeur ; menus contextuels Animation2D identiques (insertion par propriete, custom
  event, copy/paste/delete, add/delete track).

Commit attendu :

- `refactor(timeline): move context menus to a provider`

Etat actuel :

- ajoute : `ITimelineContextMenuProvider` (dossier `Menu/`, interne) +
  `TimelineControl.ContextMenuProvider`.
- `TimelineControl.CreateContextMenu` / `CreateTrackHeaderContextMenu` ne sont plus virtuels :
  ils routent vers le provider s'il est present, sinon vers `BuildDefaultContextMenu` /
  `BuildDefaultTrackHeaderContextMenu` (menu generique par evenements).
- `Animation2dTimelineControl` implemente `ITimelineContextMenuProvider` (implementation
  explicite) et se branche via `ContextMenuProvider = this` ; les anciens `override` sont
  devenus les methodes du provider, avec la meme logique (sous-menus d'insertion par
  propriete, custom event, copy/paste/delete, add/delete track). Menus identiques.
- la sous-classe `Animation2dTimelineControl` subsiste mais ne fait plus d'override de menu ;
  elle agit comme adapter + provider + porteur des evenements selection/copy/paste. Son
  elimination complete au profit d'un `TimelineControl` nu reste un objectif futur.

---

# Phase 5 — Playback et unite de temps

### ✅ Tache 5.1 — `ITimelinePlaybackController` + controleur Animation2D

Objectif : separer la lecture/scrub du controle.

Contraintes :

- Definir `ITimelinePlaybackController` (`IsPlaying`, `CurrentTime`, `Play`, `Pause`,
  `Stop`, `Seek`, `Update`).
- `AnimationTimelinePlaybackController` pilote la preview/scrub Animation2D existante.
- `TimelineControl.PlaybackController` optionnel + point d'`Update`. La timeline ne sait
  pas comment jouer un domaine.
- Aucun controleur cutscene (hors perimetre).

Fichiers cibles :

- nouveau dossier `Controls/Timeline/Playback/` ; integration Animation2D.

Validation :

- build editeur ; smoke manuel : play/pause/stop/scrub Animation2D.

Commit attendu :

- `feat(timeline): add playback controller abstraction`

Etat actuel :

- ajoute : `ITimelinePlaybackController` (dossier `Playback/`),
  `Animation2dTimelinePlaybackController` (wrapper sur `_previewTimeSeconds` + `SeekPreviewTime`),
  `TimelineControl.PlaybackController` + le hook `UpdatePlayback(deltaTime)`.
- branche cote inspector. **Sans regression** : la preview Animation2D reste pilotee par le
  sprite component (lecture auto, lue chaque frame), donc `Update` du controleur est un no-op
  et `UpdatePlayback` n'est pas appele dans la boucle Animation2D pour eviter un double pilotage.
  Le controleur expose neanmoins play/pause/stop/seek/etat pour un futur bouton de lecture.

### ✅ Tache 5.2 — `TimelineTimeUnit` (secondes / frames)

Objectif : permettre a la ruler d'afficher secondes ou frames sans changer le stockage.

Contraintes :

- Ajouter `enum TimelineTimeUnit { Seconds, Frames }` et `TimelineModel.TimeUnit`
  (defaut `Seconds`).
- La ruler adapte ses labels selon l'unite ; stockage interne en secondes inchange.
- Animation2D peut choisir `Frames` si pertinent, sinon reste en `Seconds`.

Fichiers cibles :

- `TimelineModel.cs`, `TimelineRuler.cs`, `TimelineTickCalculator.cs` si necessaire.

Validation :

- build editeur ; tests cibles d'affichage de la ruler ; rendu secondes inchange par defaut.

Commit attendu :

- `feat(timeline): support frames time unit on the ruler`

Etat actuel :

- ajoute : `TimelineTimeUnit { Seconds, Frames }`, `TimelineModel.TimeUnit` (defaut Seconds)
  et `TimelineModel.FrameRate` (defaut 60).
- la ruler formate ses labels via `TimelineTickCalculator.FormatTimeLabel` (secondes en
  `0.##`, frames en numero de frame). Le pas des graduations reste base sur les secondes ;
  seul l'affichage change. Stockage interne en secondes inchange.
- defaut Seconds : Animation2D n'opte pas pour Frames, rendu de la ruler inchange.
- tests : `FormatTimeLabel` couvert pour secondes et frames.

---

# Hors perimetre / Futur (a NE PAS implementer dans ce plan)

- **Adapter cutscene plat** : abandonne. La cutscene est un arbre `Sequence`/`Parallel`
  (`CutsceneAsset.RootAction`) joue par `CutsceneDirector`. L'editeur cutscene est un
  controle d'arbre, traite par `ai-agent/cutscene-implementation-plan.md`.
- **Timeline comme projection de preview cutscene** : envisageable plus tard, en **lecture
  seule**, calculee depuis l'arbre, sans devenir la source de verite. Prerequis non encore
  reunis : timing absolu derive de l'arbre, et un `CutsceneDirector` capable de `Seek`. A
  ouvrir comme sujet distinct quand ces prerequis existeront.
- **Audio timeline** : aucun asset audio dans le depot ; purement hypothetique tant qu'un
  modele audio n'existe pas.
- **Renderers specialises** (`AnimationTimelineItemRenderer`, etc.), selection multiple,
  undo/redo timeline-specifique, zoom vertical : a planifier apres stabilisation du coeur.
