# Plan agent IA — Editeur Animation2D CasaEngine

## Decisions verrouillees

- La V1 conserve l'extension `.anim2d`.
- La V1 conserve le modele time-based (`time_seconds`).
- La V1 est volontairement mono-sprite : un seul sprite visible a un instant donne.
- La V1 n'utilise pas encore la composition multi-sprite.
- La V1 n'utilise qu'une seule piste logique d'evenements.
- La V1 ne supporte que deux evenements : `changeSprite` et `restart`.
- La timeline V1 est read-only et n'affiche que les evenements.
- Les evenements de la timeline sont selectionnables pour visualiser leurs proprietes dans l'inspector.
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
- Smoke manuel V1 : ouvrir un `.anim2d`, voir la preview, voir la timeline graduee, scroller horizontalement, zoomer, selectionner un event, verifier l'inspector.

## Etat verifie au moment de cette mise a jour

- Le depot contient deja une pile plus large autour de `Animation2dData`, `Animation2dPartData`, `Animation2dTrackData` et `Animation2dCompositionSampler`.
- Cette pile est plus large que la V1 demandee et ne doit pas dicter le perimetre de la premiere livraison.
- `GameEditor`, `Animation2dAssetInspectorPanel` et `Animation2dTimelinePanel` existent deja comme surfaces editoriales a reutiliser.
- `AnimationEventAssetJsonSerializer` persiste maintenant `time_seconds`, `event_name` et `sprite_asset_id` pour `changeSprite`.
- L'adapter/runtime V1 sait synthetiser une timeline mono-sprite a partir d'une liste d'evenements `changeSprite` / `restart`.
- Le panneau Animation2D affiche maintenant une timeline read-only graduee, scrollable horizontalement, zoomable, avec selection visuelle des evenements, et l'inspector montre l'evenement selectionne.

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

- build editeur valide ;
- la tranche Animation2D compile sans erreur locale ;
- la commande `dotnet test` reste bloquee par des erreurs pre-existantes hors tranche Animation2D (`Pool<>`, `DualQuaternion`, `PreviewEnvironmentFactory`, `LightComponent.Coordinates`, signatures de `EditorViewportCameraController.Update`) ;
- le smoke manuel `GameEditor` n'a pas ete execute dans cette session CLI.

Commit attendu :

- `test(animation2d): validate v1 event-only editor slice`

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