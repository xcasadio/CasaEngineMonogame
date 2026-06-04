# Plan agent IA — Editeur Animation2D CasaEngine

## Decisions verrouillees

- La V1 conserve l'extension `.anim2d`.
- La V1 conserve le modele time-based (`time_seconds`).
- La V1 est volontairement mono-sprite : un seul sprite visible a un instant donne.
- La V1 n'utilise pas encore la composition multi-sprite.
- La V1 n'utilise qu'une seule piste logique d'evenements.
- La V1 ne supporte que deux evenements : `changeSprite` et `restart`.
- La timeline V1 reste read-only pour l'edition des evenements, mais inclut un playhead et le scrub.
- Les evenements de la timeline sont selectionnables pour visualiser leurs proprietes dans l'inspector.
- La V1 du controle Timeline doit etre refactoree en sous-controles explicites : `CornerHeader`, `TimelineRuler`, `TrackHeaderPanel`, `TimelineViewport`, `HorizontalScrollBar`.
- La V1 introduit un modele generique minimal `TimelineModel` avec une liste directe d'evenements.
- La V1 n'introduit pas de `TimelineTrack` explicite.
- La V1 n'introduit pas de payload `Data` non type dans le modele generique.
- Le besoin principal est l'editeur generique CasaEngine, pas un pipeline d'import specifique.

## Regles obligatoires pour l'agent IA

1. Une seule tache a la fois.
2. Avant de commencer une tache, remplacer son icone `⏳` par `🚧`.
3. A la fin d'une tache, remplacer son icone par `✅`, `🧪` ou `⚠️`.
4. Ne pas re-elargir la V1 vers la composition, les tracks de proprietes ou l'edition complete de timeline.
5. Toute tache terminee doit mettre a jour ce fichier dans le meme commit.
6. Chaque tache doit produire un commit non vide si du code ou de la doc change.
7. Le runtime ne doit pas dependre de l'editeur.
8. La compatibilite `.anim2d` doit etre preservee.
9. Ne pas inventer une API non verifiee sans la documenter explicitement comme nouvelle decision.

## Validation globale

- Build editeur cible : `dotnet build CasaEngine.Editor/CasaEngine.Editor.csproj -c Debug --no-restore`
- Tests cibles : `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj -c Debug --filter FullyQualifiedName~Animation2d --no-restore`
- Smoke manuel V1 : ouvrir un `.anim2d`, voir la preview, voir la timeline graduee, scroller horizontalement, zoomer, selectionner un event, verifier l'inspector, verifier le playhead et le scrub.

## Etat verifie au moment de cette mise a jour

- Le depot contient deja une pile plus large autour de `Animation2dData`, `Animation2dPartData`, `Animation2dTrackData` et `Animation2dCompositionSampler`.
- Cette pile est plus large que la V1 demandee et ne doit pas dicter le perimetre de la premiere livraison.
- `GameEditor`, `Animation2dAssetInspectorPanel` et `Animation2dTimelinePanel` existent deja comme surfaces editoriales a reutiliser.
- `Animation2dTimelineControl` existe deja, mais il reste monolithique : il dessine ruler, track, evenements, playhead, hit testing et tooltip dans un seul controle.
- Le scroll horizontal actuel repose sur un `MGScrollViewer` autour du controle, pas sur l'architecture cible a sous-controles.
- `Animation2dTimelineControl` expose deja `EventSelected` et `ScrubRequested` ; le refactor V1 doit conserver ces usages cote editeur.
- `Animation2dTimelinePanel` et `Animation2dAssetInspectorPanel` fournissent deja le point d'integration a conserver pendant le refactor.

## V1 — Socle mono-sprite pilote par events

### ✅ Tache V1.1 — Verrouiller le contrat produit et la doc V1

Objectif : mettre la documentation d'accord sur une V1 mono-sprite, event-only, avec timeline read-only et inspector de visualisation.

Fichiers cibles :

- `docs/animation2d_editor_casaengine.md`
- `docs/animation2d-composed-format-v1.md`
- `ai-agent/animation2d-editor-agent-plan.md`

Validation :

- Relecture de coherence des documents.

Commit attendu :

- `docs(animation2d): narrow v1 to event-only mono-sprite`

### ✅ Tache V1.2 — Verrouiller le contrat minimal des evenements V1

Objectif : definir comment persister proprement `changeSprite` et `restart` dans `.anim2d`.

Contraintes :

- `changeSprite` doit exposer une reference de sprite cible.
- `restart` ne porte pas de propriete supplementaire.
- le contrat doit rester compatible avec `.anim2d`.

Validation :

- serializer et loader couvrent les deux evenements ;
- documentation du contrat mise a jour.

Commit attendu :

- `feat(animation2d): define v1 event payload contract`

### ✅ Tache V1.3 — Runtime mono-sprite pilote par une piste d'evenements

Objectif : faire jouer une animation V1 a partir d'un sprite courant et d'une unique piste logique d'evenements.

Resultat attendu :

- `changeSprite` remplace le sprite courant ;
- `restart` relance la sequence depuis le debut.

Validation :

- smoke runtime avec une sequence `changeSprite` puis `restart`.

Commit attendu :

- `feat(animation2d): play v1 mono-sprite event timeline`

### ✅ Tache V1.4 — Affichage de la timeline avec graduation

Objectif : afficher une timeline V1 en secondes avec une vraie graduation visible.

Contraintes :

- la graduation est time-based ;
- la timeline reste read-only ;
- une seule piste logique d'evenements.

Validation :

- ouvrir un `.anim2d` et voir une echelle de temps graduee sur la timeline.

Commit attendu :

- `feat(editor): render graduated animation2d timeline`

### ✅ Tache V1.5 — Scroll horizontale sur la timeline

Objectif : permettre d'explorer la timeline quand sa largeur depasse la vue disponible.

Validation :

- charger une animation assez longue et verifier le deplacement horizontal sur la timeline.

Commit attendu :

- `feat(editor): add horizontal scroll to animation2d timeline`

### ✅ Tache V1.6 — Zoom : modifier l'espace entre les graduations

Objectif : faire varier visuellement l'espacement des graduations sans changer le contrat temps de l'asset.

Validation :

- changer le zoom et verifier que l'espacement entre les graduations varie.

Commit attendu :

- `feat(editor): add zoomable animation2d timeline`

### ✅ Tache V1.7 — Affichage d'un event sur la timeline

Objectif : afficher chaque evenement V1 directement sur la piste unique sous forme de marqueur visuel.

Validation :

- ouvrir un `.anim2d` et voir les evenements materialises sur la piste.

Commit attendu :

- `feat(editor): render animation2d events on timeline`

### ✅ Tache V1.8 — Selection d'un event

Objectif : permettre la selection visuelle d'un evenement depuis la timeline.

Validation :

- selectionner plusieurs evenements sur la timeline et verifier la mise en evidence visuelle.

Commit attendu :

- `feat(editor): select animation2d timeline event`

### ✅ Tache V1.9 — Synchroniser la selection timeline vers l'inspector

Objectif : quand un event est selectionne, ses proprietes sont visibles dans l'inspector.

Contraintes :

- l'inspector V1 est prioritairement un inspector de visualisation ;
- `changeSprite` montre le sprite cible ;
- `restart` montre son type et son temps.

Validation :

- selectionner `changeSprite` puis `restart` et verifier l'inspector.

Commit attendu :

- `feat(editor): inspect selected animation2d event`

### ✅ Tache V1.10 — Validation de base des assets V1

Objectif : signaler les erreurs minimales utiles au sous-ensemble V1.

Doit couvrir au moins :

- event type inconnu ;
- event `changeSprite` sans sprite cible ;
- sprite cible introuvable ;
- events non tries ;
- animation vide.

Commit attendu :

- `feat(editor): validate v1 animation2d events`

### ⚠️ Tache V1.11 — Build, tests et smoke de la tranche V1

Objectif : cloturer la tranche V1 avec une verification reproductible.

Validation :

- `dotnet build CasaEngine.Editor/CasaEngine.Editor.csproj -c Debug --no-restore`
- `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj -c Debug --filter FullyQualifiedName~Animation2d --no-restore`
- smoke manuel dans `GameEditor`.

Etat actuel :

- `dotnet build CasaEngine.Editor/CasaEngine.Editor.csproj -c Debug --no-restore` valide ;
- la tranche Animation2D / Timeline compile sans erreur locale ;
- la commande documentee `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj -c Debug --filter FullyQualifiedName~Animation2d --no-restore` reste bloquee par des erreurs pre-existantes hors tranche Animation2D (`Pool<>`, `DualQuaternion`, `PreviewEnvironmentFactory`, `LightComponent.Coordinates`, signatures de `EditorViewportCameraController.Update`) ;
- une tentative ciblee `FullyQualifiedName~TimelineViewTransformTests` echoue sur les memes blocages globaux du projet de tests ;
- le smoke manuel `GameEditor` n'a pas ete execute dans cette session CLI.

Commit attendu :

- `test(animation2d): validate v1 event-only editor slice`

### ✅ Tache V1.12 — Verrouiller le modele generique minimal du controle Timeline

Objectif : introduire un modele generique minimal pour la timeline, distinct du rendu et branche sur une liste directe d'evenements.

Contraintes :

- `TimelineModel` expose `DurationSeconds` et `Events`.
- `TimelineEvent` reste minimal : identifiant, nom ou type, temps.
- pas de `TimelineTrack` explicite en V1 ;
- pas de payload `Data` non type en V1.

Validation :

- build editeur ;
- aucun branchement direct du controle de rendu sur `Animation2dAssetInspectorPanel`.

Commit attendu :

- `refactor(editor): add minimal timeline model`

### ✅ Tache V1.13 — Introduire `TimelineViewState` et `TimelineViewTransform`

Objectif : separer l'etat de vue et centraliser les conversions temps/ecran pour la ruler, le viewport, le playhead et le scroll.

Contraintes :

- `PixelsPerSecond`, `ScrollX` et la selection appartiennent a l'etat de vue ;
- la conversion temps/X est unique et partagee ;
- le ruler et le viewport utilisent exactement le meme transform.

Validation :

- build editeur ;
- tests cibles sur `TimeToX`, `XToTime` et bornes de scroll.

Etat actuel :

- `TimelineViewState`, `TimelineViewTransform` et `TimelineTickCalculator` sont ajoutes ;
- le calcul d'ancrage du zoom est centralise dans `TimelineViewTransform` ;
- build editeur et build solution editeur valides ;
- des tests cibles existent sur les conversions, les bornes de scroll, les graduations et l'ancrage du zoom ;
- l'execution de `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj` reste bloquee par des erreurs pre-existantes hors tranche Timeline.

Commit attendu :

- `refactor(editor): add timeline view state and transform`

### ✅ Tache V1.14 — Decouper le controle en sous-controles V1

Objectif : remplacer le controle monolithique par une composition explicite `CornerHeader`, `TimelineRuler`, `TrackHeaderPanel`, `TimelineViewport`, `HorizontalScrollBar`.

Contraintes :

- le dessin de la ruler reste dans `TimelineRuler` ;
- le dessin des evenements et du playhead reste dans `TimelineViewport` ;
- le viewport ne cree pas un controle enfant par event.

Validation :

- build editeur ;
- ouverture d'un `.anim2d` avec rendu visuel equivalent ou meilleur sur la timeline.

Etat actuel :

- `CornerHeader`, `TimelineRuler`, `TrackHeaderPanel`, `TimelineViewport` et `HorizontalScrollBar` sont introduits ;
- la grille visible `2 x 2` de la timeline est maintenant portee par un `MGGrid` dedie qui dessine lui-meme les separateurs internes ;
- le build editeur et le build solution editeur sont valides ;
- le smoke visuel manuel dans `GameEditor` n'a pas ete execute dans cette session CLI.

Commit attendu :

- `refactor(editor): split timeline into subcontrols`

### ✅ Tache V1.15 — Rebrancher l'integration Animation2D sur le nouveau controle

Objectif : reconnecter `Animation2dTimelinePanel` et `Animation2dAssetInspectorPanel` au nouveau controle sans reintroduire de couplage fort.

Contraintes :

- l'inspector reste exterieur au controle timeline ;
- la selection remonte via un evenement public ;
- le controle consomme un modele generique, pas directement la surface d'inspector.

Validation :

- smoke manuel : ouvrir un `.anim2d`, voir les evenements, selectionner un event, verifier la synchro avec l'inspector.

Etat actuel :

- `Animation2dTimelineControl` est devenu un adaptateur sur le controle generique ;
- `Animation2dAssetInspectorPanel` consomme le nouveau controle sans `MGScrollViewer` externe ;
- la synchro compile et les evenements publics historiques sont preserves ;
- le smoke manuel reste a faire.

Commit attendu :

- `refactor(editor): reconnect animation2d timeline integration`

### ✅ Tache V1.16 — Rebrancher scroll horizontal et zoom sur un transform partage

Objectif : faire converger ruler, viewport et scrollbar horizontale vers le meme scroll et le meme zoom.

Contraintes :

- la colonne des tracks ne scrolle pas horizontalement ;
- le zoom reste horizontal ;
- les graduations et les evenements restent alignes pendant scroll et zoom.

Validation :

- smoke manuel : scroll horizontal, zoom avant, zoom arriere ;
- verification visuelle de l'alignement ruler / playhead / evenements.

Etat actuel :

- le ruler, le viewport et la scrollbar horizontale utilisent le meme `TimelineViewTransform` ;
- le zoom pilote `PixelsPerSecond` via un transform partage et conserve l'ancre sous le curseur ;
- l'alignement compile et les tests couvrent les conversions, les bornes et l'ancrage de zoom ;
- la verification visuelle manuelle reste a faire.

Commit attendu :

- `feat(editor): align timeline scroll and zoom`

### ✅ Tache V1.17 — Preserver le playhead et le scrub read-only

Objectif : conserver un playhead visible et une interaction de scrub dans la V1 sans ouvrir l'edition des evenements.

Contraintes :

- le playhead est dessine par le viewport ;
- le scrub modifie le temps courant ;
- aucun drag d'event ni edition directe d'evenement en V1.

Validation :

- smoke manuel : cliquer ou scrubber la timeline et verifier la mise a jour du temps courant et du playhead.

Etat actuel :

- le playhead reste visible dans la ruler et le viewport ;
- le clic viewport et le clic ruler mettent a jour le temps courant et relaient le scrub vers l'integration Animation2D ;
- le smoke manuel de scrub reste a faire.

Commit attendu :

- `feat(editor): keep read-only playhead and scrub`

### ✅ Tache V1.18 — Verrouiller les garde-fous de performance du viewport timeline

Objectif : s'assurer que le nouveau decoupage reste leger et respecte les contraintes hot path de l'editeur.

Doit couvrir au moins :

- pas de controle enfant par event ;
- pas de LINQ dans le rendu et le hit testing ;
- pas d'allocations temporaires par frame ;
- pas de dessin des evenements hors zone visible ;
- invalidations limitees aux sous-parties concernees.

Etat actuel :

- aucun controle enfant par event n'est cree ;
- aucun LINQ n'est ajoute dans le rendu, le hit testing ou les updates du controle timeline ;
- les evenements hors fenetre visible ne sont pas dessines ;
- les changements de vue utilisent `ArrangeChanged` sur le controle timeline ;
- les separateurs visuels des cellules visibles sont portes par `MGGrid` plutot que par une multiplication de traits locaux dans chaque cellule ;
- aucun profiling manuel n'a ete execute dans cette session CLI.

Commit attendu :

- `perf(editor): harden timeline viewport hot path`

### ✅ Tache V1.19 — Ajouter les tests cibles du controle Timeline

Objectif : couvrir le coeur deterministe du nouveau controle timeline avec des tests ciblés.

Doit couvrir au moins :

- conversion `TimeToX` / `XToTime` ;
- calcul des graduations majeures ;
- hit testing des events ;
- bornes de scroll horizontal ;
- conservation de l'alignement pendant zoom et scroll.

Etat actuel :

- des tests cibles ont ete ajoutes pour `TimeToX` / `XToTime`, les bornes de scroll, les graduations majeures, le hit testing des events et l'ancrage du zoom ;
- le fichier de tests compile localement sans erreur ;
- l'execution `dotnet test` reste bloquee par des erreurs pre-existantes hors tranche Timeline dans `CasaEngine.Tests`.

Commit attendu :

- `test(editor): cover timeline control core behavior`

## V2 — Composition et authoring de base

### ⏳ Tache V2.1 — Introduire la composition multi-sprite

Objectif : ajouter plusieurs sprites visibles en meme temps, avec parts / slots.

Commit attendu :

- `feat(animation2d): add multi-sprite composition`

### ⏳ Tache V2.2 — Ajouter les tracks de proprietes

Objectif : introduire des tracks `Sprite`, `Position`, `Visible`, `DrawOrder`, `FlipX`, `FlipY`.

Commit attendu :

- `feat(animation2d): add property tracks`

### ⏳ Tache V2.3 — Timeline authoring

Objectif : rendre la timeline editable.

Commit attendu :

- `feat(editor): add animation2d timeline authoring`

## V3 — Systeme avance d'animation 2D

### ⏳ Tache V3.1 — Controller et graphe 2D

Objectif : ajouter controller, etats, transitions et graphe d'animation.

Commit attendu :

- `feat(animation2d): add controller and graph`

### ⏳ Tache V3.2 — Integration gameplay et extensions UI

Objectif : etendre l'animation 2D au gameplay, aux cutscenes et a l'UI.

Commit attendu :

- `feat(animation2d): integrate gameplay and ui`