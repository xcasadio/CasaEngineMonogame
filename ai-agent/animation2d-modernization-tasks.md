# Modernisation des animations 2D - Analyse critique et plan agent IA

## Objectif

Moderniser le systeme d'animation 2D de CasaEngine autour de deux besoins verifies dans la note [docs/animation2d_editor_casaengine.md](../docs/animation2d_editor_casaengine.md) :

- composer une animation avec plusieurs images visibles en meme temps ;
- associer des events a une animation 2D.

Ce plan ne traite pas l'import Alundra comme sujet principal. Les donnees Alundra peuvent rester une source de test ou d'import ulterieure, mais la cible de ce plan est le runtime et les assets generiques CasaEngine.

## Regles obligatoires pour l'agent IA

1. Une seule tache a la fois.
2. Avant de commencer une tache, remplacer son icone `⏳` par `🚧`.
3. A la fin de chaque tache, remplacer l'icone par `✅`, `🧪` ou `⚠️`.
4. Chaque tache doit laisser le depot dans un etat compilable.
5. Commiter le code, les tests et la mise a jour de ce plan dans le meme commit.
6. Ne pas inventer un contrat non verifie : si une API, un editor control ou un format asset n'existe pas, le documenter comme nouvelle decision a prendre.
7. Ne pas supprimer le format `.anim2d` existant tant que les assets des projets ne sont pas migres.
8. Ne pas introduire de LINQ, closures ou allocations evitables dans les chemins `Update` et `Draw`.
9. Statuts autorises :
   - `⏳ Todo`
   - `🚧 In progress`
   - `✅ Done`
   - `🧪 Needs testing`
   - `⚠️ Blocked`

## Etat verifie dans le depot

- Le format 2D runtime actuel est `Animation2dData`, charge par `AssetLoader<Animation2dData>()` et associe a l'extension `.anim2d`.
- `Animation2dData` contient uniquement une liste de `FrameData`.
- `FrameData` contient `Duration` et `SpriteId`.
- `Animation2d` transforme chaque frame en `SetFrameEvent`, puis ajoute un `AnimationEndEvent` a la fin.
- `SetFrameEvent` change `Animation2d.CurrentFrame`.
- `Animation` possede deja une liste d'`AnimationEvent`, mais dans le chemin 2D actuel elle sert surtout au changement de frame et a la fin d'animation.
- `AnimatedSpriteComponent` charge des assets `Animation2dData`, cree des `Animation2d`, selectionne une animation courante et dessine un seul `SpriteData` correspondant a `CurrentFrame`.
- `AnimatedSpriteComponent.Draw()` recree actuellement un `Sprite` depuis le `SpriteData` courant avec un commentaire `TODO : create list with all spriteData`.
- Les collisions du `AnimatedSpriteComponent` sont indexees par `SpriteId` de frame, pas par partie composee.
- Les fichiers `.anim2d` existants dans `Projects/SampleProject` et `Projects/RPGDemo` utilisent le format `animation_type`, `id`, `name`, `frames`, `duration`, `sprite_id`.
- `EditorAssetJsonSerializer` sait sauvegarder `Animation2dData` au format actuel avec `frames`.
- Aucun fichier concret `Animation2DAsset`, `SpriteLibrary`, `SpriteComposition`, `AnimationClip2D`, `AnimationTrack2D`, `Keyframe2D`, `SpriteCompositionRenderer` ou `AnimationPlayer2D` n'a ete trouve dans le code. Ces noms existent dans la note, pas dans le runtime actuel.
- Aucun control editeur concret `Animation2dEditor` ou `GameEditorAnimation2d` n'a ete retrouve dans les fichiers actuels, meme si des anciens plans les mentionnent.
- La pile d'animation 3D moderne contient deja `AnimationEventTrack`, `AnimationEventKeyframe`, `AnimationClipAsset.Events` et des tests de dispatch d'events dans `AnimationControllerTests`.
- `SpriteData` contient deja un rectangle source, une origine, des sockets et des collisions.
- `SpriteRendererComponent` expose deja des surcharges de dessin de sprite avec position, rotation, scale, couleur, z-order, sort key et `SpriteEffects`.

## Critique de la note existante

La note de [docs/animation2d_editor_casaengine.md](../docs/animation2d_editor_casaengine.md) pose une bonne direction fonctionnelle : une animation 2D moderne ne doit pas etre limitee a une succession d'images completes. Les notions de parties, attachments, ordre d'affichage, pistes, keyframes et events correspondent bien au besoin de composer plusieurs images.

Le probleme principal est que la note decrit surtout une architecture cible conceptuelle. Plusieurs classes citees n'existent pas dans le depot. Elles ne doivent donc pas etre considerees comme une API deja disponible. Toute implementation doit commencer par une phase d'inventaire et de compatibilite avec `Animation2dData`, `Animation2d`, `FrameData` et `AnimatedSpriteComponent`.

La partie Alundra prend beaucoup de place par rapport au besoin moteur. Elle est utile pour expliquer pourquoi la composition et la preservation de donnees sources peuvent etre importantes, mais elle peut biaiser l'architecture si les metadonnees d'import deviennent centrales dans le runtime. La modernisation CasaEngine doit definir d'abord un format compose generique ; les metadonnees d'import doivent rester optionnelles et separees.

Le modele propose melange parfois asset, runtime et editeur dans un meme flux. Pour eviter un couplage durable, il faut separer : donnees authoring serialisables, etat runtime mutable, sampling/update, rendu, et outils editeur. Cette separation existe deja dans la pile 3D moderne et doit servir de precedent, sans copier aveuglement les types 3D.

La note mentionne des events 2D, mais le depot possede deja deux precedents differents : l'ancien `AnimationEvent` utilise par `Animation2d` pour piloter les frames, et le nouveau `AnimationEventTrack` 3D avec `AnimationEventKeyframe`. La V1 2D doit choisir explicitement si elle reutilise le modele moderne d'event keyframes ou si elle cree un equivalent 2D frame-based. Elle ne doit pas ajouter un troisieme systeme d'events sans justification.

La note recommande un modele frame-based pour la V1. C'est coherent avec le sujet 2D, mais le format actuel utilise des durees en secondes par frame. La migration doit donc traiter le pont entre `Duration` existant et une representation frame-based ou time-based. Il ne faut pas changer brutalement la semantique des assets `.anim2d` existants.

Le point le plus fragile est la compatibilite. Les projets contiennent deja de nombreux `.anim2d` simples. Remplacer directement `Animation2dData` par un asset compose casserait le chargement, le rendu et possiblement les tilemaps qui conservent une reference legacy `animation_2d_id`. La bonne V1 doit permettre de charger les anciens assets et de les adapter vers une animation composee a une seule partie.

La note ne traite pas assez le hot path. Une animation composee va dessiner plusieurs sprites par frame ; il faut donc precharger/resoudre les `SpriteData` et eviter de recreer les `Sprite` dans `Draw()`. Le commentaire actuel dans `AnimatedSpriteComponent.Draw()` confirme deja un manque a corriger.

Enfin, l'editeur est decrit de facon ambitieuse, mais aucun control concret actuel n'a ete trouve. Le plan doit donc livrer d'abord un runtime testable et une validation minimale, puis seulement ensuite un editeur ou un viewer. L'interface complete timeline/property grid ne doit pas bloquer la base runtime.

## Architecture cible V1 limitee

La V1 doit rester generique et compatible.

Elements a introduire ou confirmer par tache :

- un asset 2D compose versionne, ou une extension compatible de `Animation2dData` ;
- une representation de partie composee avec attachment sprite, position, visibilite, draw order, flip et couleur si necessaire ;
- un clip 2D compose qui peut evaluer l'etat de plusieurs parties ;
- des events 2D alignes sur le precedent moderne `AnimationEventTrack` quand c'est compatible ;
- une instance runtime qui garde l'etat courant sans muter l'asset ;
- un renderer ou composant capable de dessiner plusieurs sprites dans un ordre stable ;
- un adaptateur legacy qui convertit un `.anim2d` simple en composition a une seule partie.

Hors V1 :

- importer Alundra complet ;
- timeline editor complete ;
- animation graph 2D ;
- interpolation avancee ;
- blending entre clips ;
- hitbox/hurtbox editeur avance ;
- state machine d'animation.

## Validation minimale globale

- Build principal : `dotnet build CasaEngine.Editor.MonoGame.sln -c Debug --no-restore`.
- Tests runtime 2D cibles a ajouter : `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj -c Debug --filter FullyQualifiedName~Animation2d --no-restore`.
- Tests serializer cibles a ajouter : `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj -c Debug --filter FullyQualifiedName~Animation2dAuthoring --no-restore`.
- Validation compatibilite : charger au moins un `.anim2d` existant de `Projects/SampleProject` ou `Projects/RPGDemo` sans modifier son JSON.
- Validation visuelle quand le rendu compose arrive : un sample ou demo qui affiche au moins deux sprites dans une meme animation 2D.

---

## Phase 0 - Inventaire verrouille et decisions de compatibilite

### ✅ Tache 0.1 - Verifier tous les usages runtime de `Animation2dData`

Objectif : produire une liste verifiee des usages actuels avant toute modification.

Actions :

- Rechercher les references a `Animation2dData`, `Animation2d`, `FrameData`, `AnimatedSpriteComponent`, `.anim2d` et `animation_2d_id`.
- Identifier les chemins runtime, serializer, tests, projets et tilemaps.
- Documenter les fichiers consommateurs dans cette section ou dans une note dediee.
- Ne modifier aucun code fonctionnel dans cette tache.

Resultat d'audit :

- Runtime assets : `CasaEngine/Framework/Assets/Animations/Animation2dData.cs`, `Animation2d.cs`, `Animation.cs`, `AnimationEvent.cs`, `SetFrameEvent.cs`, `AnimationEndEvent.cs` et `FrameData.cs` forment le chemin legacy.
- Chargement assets : `CasaEngine/Framework/Assets/AssetLoaderRegistry.cs` enregistre `AssetLoader<Animation2dData>()` pour les assets `.anim2d`.
- Composant runtime : `CasaEngine/Framework/Scene/Entities/Components/AnimatedSpriteComponent.cs` charge les ids d'animations, cree les `Animation2d`, ecoute `FrameChanged`/`AnimationFinished`, met a jour les collisions par `SpriteId` et dessine le sprite courant.
- Serializer editor : `CasaEngine.EditorServices/EditorAssetJsonSerializer.cs` sauvegarde `Animation2dData` via `SaveAnimation2dData()` et conserve `duration` + `sprite_id` dans `SaveFrameData()`.
- Serializer entity : `CasaEngine.EditorServices/EditorEntityJsonSerializer.cs` sauvegarde les references du `AnimatedSpriteComponent` via `SaveAnimatedSpriteComponent()`.
- Demos : `CasaEngine.Demos/Demos/TileMapDemo.cs` charge tous les assets dont l'extension est `Constants.FileNameExtensions.Animation2d`, puis ajoute `new Animation2d(animation)` a un `AnimatedSpriteComponent`; `CasaEngine.Demos/PlayerComponent.cs` pilote les animations par nom et accede a `CurrentAnimation.Animation2dData.Name`.
- RPGDemo : plusieurs scripts et controllers consomment `AnimatedSpriteComponent`, `CurrentAnimation`, `Animation2dData.Name`, `AnimationFinished` et `SetCurrentAnimation()`.
- Tilemaps : `CasaEngine/Framework/Assets/TileMap/AnimatedTileData.cs` conserve la compatibilite `animation_2d_id`; `CasaEngine.Tests/TileMap/TileSetDataTests.cs` verrouille cette compatibilite legacy.
- Tests actuels : aucun test dedie `Animation2dData`/`AnimatedSpriteComponent` n'a ete trouve; les tests existants touchent surtout la compat `animation_2d_id` des tilemaps et la pile d'animation 3D moderne.
- Projets : `Projects/SampleProject` et `Projects/RPGDemo` contiennent de nombreux `.anim2d` simples au format `animation_type`, `id`, `name`, `frames`, `duration`, `sprite_id`.
- Precedent events moderne : `CasaEngine/Framework/Animations/AnimationEventTrack.cs`, `AnimationEventKeyframe.cs`, `AnimationClipAsset.Events` et les tests `AnimationControllerTests` couvrent deja un modele time-based d'events pour la pile 3D.

Validation :

- `dotnet build CasaEngine.Editor.MonoGame.sln -c Debug --no-restore`.

Commit attendu :

- `docs(animation2d): audit legacy animation usages`

### ⏳ Tache 0.2 - Choisir la strategie de format V1

Objectif : decider si la V1 etend `Animation2dData` ou ajoute un nouveau type asset compose.

Contraintes verifiees :

- `Animation2dData` est charge par `AssetLoader<Animation2dData>()`.
- Les assets existants ont une cle `frames` avec `duration` et `sprite_id`.
- `EditorAssetJsonSerializer` sauvegarde deja ce format.

Actions :

- Comparer deux options : extension compatible de `Animation2dData` ou nouveau type asset avec loader dedie.
- Choisir l'option qui preserve le chargement des `.anim2d` existants.
- Documenter la decision dans ce plan avant implementation.

Validation :

- Pas de code obligatoire.
- Si du code est modifie, lancer le build principal.

Commit attendu :

- `docs(animation2d): choose composed asset compatibility strategy`

## Phase 1 - Modele de donnees authoring compose

### ⏳ Tache 1.1 - Ajouter les types de donnees pour parties composees

Objectif : representer plusieurs sprites visibles dans une meme animation sans toucher au rendu.

Actions :

- Ajouter un type de partie authoring avec identifiant stable, nom, sprite par defaut, position par defaut, draw order, visible, flip X/Y.
- Ajouter un type d'attachment ou reference sprite seulement si la strategie de la tache 0.2 le justifie.
- Utiliser des collections explicites, initialisees, compatibles avec le serializer existant.
- Garder les champs legacy `Frames` intacts.

Criteres d'acceptation :

- Un asset legacy sans donnees composees se charge encore.
- Un asset compose minimal peut etre construit en memoire avec deux parties.

Validation :

- Tests unitaires de chargement legacy.
- Tests unitaires de construction d'un asset compose minimal.
- Build principal.

Commit attendu :

- `feat(animation2d): add composed authoring data`

### ⏳ Tache 1.2 - Ajouter les types de pistes et keyframes 2D V1

Objectif : animer les proprietes necessaires a la composition.

Actions :

- Ajouter les pistes V1 strictement necessaires : sprite/attachment, position, visible, draw order, flip X, flip Y.
- Definir une representation keyframe compatible avec le choix frame-based ou duration-based de la tache 0.2.
- Ne pas ajouter interpolation avancee en V1, sauf valeur explicite `Step` si elle sert au format.
- Valider que les pistes reference une partie existante.

Criteres d'acceptation :

- Une animation peut exprimer deux parties avec changement de sprite et position.
- Les pistes invalides sont detectables par validation ou exception controlee hors hot path.

Validation :

- Tests unitaires du modele et des erreurs de reference.
- Build principal.

Commit attendu :

- `feat(animation2d): add composed tracks and keyframes`

### ⏳ Tache 1.3 - Ajouter les events authoring 2D

Objectif : permettre des events d'animation 2D sans dupliquer inutilement le systeme moderne 3D.

Actions :

- Evaluer la reutilisation de `AnimationEventKeyframe` ou d'un equivalent 2D frame-based.
- Stocker au minimum un nom d'event et sa position temporelle ou frame selon la decision V1.
- Ne pas executer de logique gameplay dans l'asset.
- Garder la compatibilite avec les anciens `SetFrameEvent` internes de `Animation2d`.

Criteres d'acceptation :

- Les events authoring se serialisent et se chargent.
- Un asset sans events reste valide.

Validation :

- Tests serializer round-trip des events.
- Build principal.

Commit attendu :

- `feat(animation2d): add animation event authoring data`

## Phase 2 - Serialization et compatibilite JSON

### ⏳ Tache 2.1 - Etendre le chargement JSON sans casser les `.anim2d` existants

Objectif : charger le nouveau format compose en plus du format actuel.

Actions :

- Mettre a jour `Load(JObject)` pour accepter les champs composes optionnels.
- Conserver la lecture de `frames` legacy sans changement.
- Ajouter une valeur de version de format seulement si elle est sauvegardee de maniere compatible.
- Ne pas renommer l'extension `.anim2d` dans cette tache.

Criteres d'acceptation :

- Un fichier `.anim2d` existant se charge et conserve ses frames.
- Un JSON compose de test se charge avec ses parties, pistes et events.

Validation :

- Tests de chargement legacy depuis un JSON minimal conforme aux assets existants.
- Tests de chargement compose.
- Build principal.

Commit attendu :

- `feat(animation2d): load composed animation json`

### ⏳ Tache 2.2 - Etendre `EditorAssetJsonSerializer`

Objectif : sauvegarder les donnees composees sans perdre les donnees legacy.

Actions :

- Ajouter la sauvegarde des parties, pistes et events selon le format choisi.
- Conserver la sauvegarde des `frames` existantes.
- Eviter de generer des champs vides inutiles si le style du serializer local prefere l'absence de champ optionnel.

Criteres d'acceptation :

- Round-trip d'un asset legacy.
- Round-trip d'un asset compose.
- Round-trip d'un asset compose avec events.

Validation :

- Tests `Animation2dAuthoring` dans `CasaEngine.Tests`.
- Build principal.

Commit attendu :

- `feat(editorservices): serialize composed animation2d data`

### ⏳ Tache 2.3 - Ajouter un adaptateur legacy vers composition a une partie

Objectif : permettre au runtime compose de lire les anciennes animations simples.

Actions :

- Convertir une `Animation2dData` legacy en representation composee a une seule partie.
- La partie legacy doit utiliser le `SpriteId` de la frame courante.
- La duree totale et le type d'animation doivent rester coherents avec l'ancien comportement.
- Ne pas modifier les fichiers `.anim2d` existants.

Criteres d'acceptation :

- Une animation legacy donne la meme sequence de `SpriteId` qu'avant.
- L'adaptateur ne cree pas de dependance editeur.

Validation :

- Tests unitaires de conversion avec 1 frame et plusieurs frames.
- Tests avec `AnimationType.Once` et `AnimationType.Loop` si le comportement actuel est conserve.
- Build principal.

Commit attendu :

- `feat(animation2d): adapt legacy frames to composed runtime`

## Phase 3 - Runtime compose sans rendu

### ⏳ Tache 3.1 - Ajouter l'etat runtime des parties composees

Objectif : separer l'asset immutable de l'etat courant mutable.

Actions :

- Ajouter une instance runtime qui contient l'etat courant de chaque partie.
- Prevoir l'acces par index ou dictionnaire initialise hors hot path selon les besoins.
- Eviter les allocations par frame dans `Update`.
- Garder les donnees suffisantes pour dessiner : sprite courant, position, draw order, visible, flip.

Criteres d'acceptation :

- Une instance peut etre reinitialisee depuis un asset.
- Les valeurs par defaut des parties sont appliquees.

Validation :

- Tests unitaires de reset et initialisation.
- Build principal.

Commit attendu :

- `feat(animation2d): add composed runtime state`

### ⏳ Tache 3.2 - Ajouter un sampler/update compose

Objectif : appliquer les pistes a l'etat runtime au fil du temps.

Actions :

- Implementer l'evaluation Step des pistes V1.
- Respecter le type d'animation existant : Once, Loop, PingPong si la compatibilite le requiert.
- Ne pas dispatcher les events dans `Seek` si le precedent 3D est reutilise.
- Ne pas utiliser LINQ dans `Update`.

Criteres d'acceptation :

- Le sampler applique sprite, position, visible, draw order et flip.
- Le sampler reproduit une animation legacy convertie.
- Le sampler gere la fin d'une animation Once.

Validation :

- Tests unitaires d'evaluation de piste.
- Tests unitaires de boucle.
- Tests unitaires de compat legacy.
- Build principal.

Commit attendu :

- `feat(animation2d): sample composed tracks`

### ⏳ Tache 3.3 - Dispatcher les events 2D runtime

Objectif : signaler les events quand l'update traverse leurs keyframes.

Actions :

- Ajouter un event runtime public coherent avec le style existant.
- Dispatcher les events pendant `Update`, pas pendant `Seek` ou reset.
- Gerer les events en boucle en suivant le precedent teste de `AnimationController`.
- Ne pas coupler les events 2D a un systeme gameplay global dans cette tache.

Criteres d'acceptation :

- Un event a mi-animation est emis une fois quand le temps le traverse.
- Les events de boucle se redispatchent apres wrap si l'animation loop.
- Aucun event n'est emis par reset/seek.

Validation :

- Tests equivalents aux cas `AnimationControllerTests` pour animation 2D.
- Build principal.

Commit attendu :

- `feat(animation2d): dispatch composed animation events`

## Phase 4 - Rendu compose

### ⏳ Tache 4.1 - Pre-resoudre les sprites utilises par animation 2D

Objectif : supprimer la creation de sprite dans `Draw()` et preparer le rendu multi-parties.

Actions :

- Charger/resoudre les `SpriteData` ou `Sprite` necessaires lors de l'initialisation du composant ou du runtime.
- Conserver un chemin compatible avec les animations legacy.
- Documenter les cas d'erreur : sprite manquant, asset non charge, reference invalide.
- Ne pas allouer une nouvelle liste ou de nouveaux sprites a chaque frame.

Criteres d'acceptation :

- `Draw()` n'appelle plus `Sprite.Create()` par frame pour le chemin modernise.
- Une animation legacy continue de s'afficher.

Validation :

- Test unitaire si la resolution est isolable.
- Smoke test manuel ou demo si necessaire.
- Build principal.

Commit attendu :

- `perf(animation2d): cache sprites for runtime drawing`

### ⏳ Tache 4.2 - Dessiner plusieurs parties avec ordre stable

Objectif : afficher une composition de plusieurs sprites dans une meme animation.

Actions :

- Mettre a jour ou ajouter un renderer 2D compose qui parcourt les parties visibles.
- Trier ou maintenir l'ordre de dessin par `drawOrder` hors hot path si possible.
- Utiliser les surcharges existantes de `SpriteRendererComponent`.
- Respecter `DepthSortable2DComponent` si le composant actuel l'utilise.
- Appliquer position locale de partie, flip et couleur selon les champs V1 retenus.

Criteres d'acceptation :

- Deux parties visibles peuvent etre dessinees dans le meme clip.
- Changer le draw order change l'ordre visuel.
- Une animation legacy a une seule partie reste equivalente.

Validation :

- Test logique de tri si isole.
- Demo ou sample visuel minimal.
- Build principal.

Commit attendu :

- `feat(animation2d): render composed sprite parts`

### ⏳ Tache 4.3 - Revoir bounds et collisions pour la composition

Objectif : eviter que le composant compose garde des bounds/collisions d'une seule frame sprite.

Actions :

- Calculer la bounding box a partir des parties visibles et de leurs sprites.
- Identifier si les collisions doivent rester legacy par frame ou passer par partie.
- Ne pas inventer de hitbox/hurtbox moderne dans cette tache.
- Si la collision composee est trop large pour V1, documenter le blocage et garder le comportement legacy explicite.

Criteres d'acceptation :

- Les bounds couvrent toutes les parties visibles.
- Les collisions legacy ne regressent pas pour les anciennes animations.

Validation :

- Tests unitaires de bounds avec deux parties.
- Smoke test collision legacy si disponible.
- Build principal.

Commit attendu :

- `feat(animation2d): compute composed bounds`

## Phase 5 - Integration composant et compatibilite projets

### ⏳ Tache 5.1 - Integrer le runtime compose dans `AnimatedSpriteComponent`

Objectif : brancher le nouveau runtime sans casser l'API publique existante.

Actions :

- Conserver `SetCurrentAnimation`, `AddAnimation`, `FrameChanged` et `AnimationFinished` tant qu'ils sont utilises.
- Ajouter le chemin compose derriere les animations chargees.
- Adapter `GetCurrentFrameName()` et `GetCurrentFrameIndex()` seulement si leur semantique legacy reste claire.
- Documenter toute API devenue ambigue avec les compositions multi-sprites.

Criteres d'acceptation :

- Les appels existants de `PlayerComponent` et demos continuent de compiler.
- Une animation composee peut etre selectionnee et mise a jour.

Validation :

- Tests de composant si disponibles ou tests d'integration limites.
- Build principal.

Commit attendu :

- `feat(animation2d): integrate composed runtime in animated sprite component`

### ⏳ Tache 5.2 - Ajouter une validation de chargement d'assets existants

Objectif : verrouiller la compatibilite avec les projets existants.

Actions :

- Ajouter un test ou smoke ciblant un `.anim2d` existant simple.
- Verifier que `frames`, `duration` et `sprite_id` restent lus.
- Verifier que l'adaptateur compose produit une seule partie.

Criteres d'acceptation :

- Le test echoue si le format legacy est casse.

Validation :

- `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj -c Debug --filter FullyQualifiedName~Animation2d --no-restore`.
- Build principal.

Commit attendu :

- `test(animation2d): preserve legacy anim2d loading`

## Phase 6 - Sample minimal

### ⏳ Tache 6.1 - Ajouter un asset compose minimal de demonstration

Objectif : avoir un cas concret avec plusieurs images dans une animation.

Actions :

- Creer ou reutiliser des sprites existants du projet sample sans modifier massivement les assets.
- Ajouter un asset d'animation composee minimal selon le format choisi.
- Le sample doit montrer au moins deux parties visibles et un changement de draw order ou position.

Criteres d'acceptation :

- L'asset se charge par le pipeline existant.
- L'animation est visible dans une demo ou un scenario simple.

Validation :

- Smoke test manuel documente.
- Build principal.

Commit attendu :

- `sample(animation2d): add composed sprite animation asset`

### ⏳ Tache 6.2 - Ajouter une demo ou etendre une demo existante

Objectif : valider visuellement le rendu compose et les events.

Actions :

- Reutiliser une demo 2D existante si elle existe et reste adaptee.
- Afficher l'animation composee.
- Logger ou exposer temporairement les events d'animation de maniere non intrusive.
- Documenter les etapes de lancement.

Criteres d'acceptation :

- La demo affiche plusieurs sprites dans une animation.
- Un event 2D est observable pendant le playback.

Validation :

- Lancer la demo si l'environnement le permet.
- Build principal.

Commit attendu :

- `demo(animation2d): show composed animation playback`

## Phase 7 - Editeur minimal apres runtime

### ⏳ Tache 7.1 - Identifier la surface editeur reelle

Objectif : ne pas construire l'editeur sur des classes fantomes.

Actions :

- Rechercher les vues et controles editeur actuels disponibles.
- Identifier ou creer le point d'entree minimal pour inspecter un asset `.anim2d`.
- Documenter les dependances MGUI/WPF reelles avant implementation.

Criteres d'acceptation :

- La surface concrete a modifier est nommee.
- Les anciennes mentions de `GameEditorAnimation2d` ne sont pas traitees comme existantes sans verification.

Validation :

- Build principal si code modifie.

Commit attendu :

- `docs(editor): identify animation2d editor surface`

### ⏳ Tache 7.2 - Ajouter une inspection read-only des compositions

Objectif : permettre de verifier un asset compose sans editeur complet.

Actions :

- Afficher les parties, pistes et events en lecture seule.
- Afficher la frame ou le temps courant si un viewport existe.
- Ne pas ajouter timeline editable dans cette tache.

Criteres d'acceptation :

- Un asset compose peut etre inspecte.
- Les donnees legacy restent lisibles.

Validation :

- Smoke test manuel de l'UI.
- Build principal.

Commit attendu :

- `feat(editor): inspect composed animation2d assets`

### ⏳ Tache 7.3 - Ajouter l'edition minimale des parties et events

Objectif : couvrir seulement les modifications indispensables apres l'inspection.

Actions :

- Editer nom de partie, sprite par defaut, position par defaut, draw order et visibilite.
- Editer les events authoring V1.
- Sauvegarder via `EditorAssetJsonSerializer`.
- Ne pas ajouter multi-selection, onion skinning, graph, blending ou import specifique.

Criteres d'acceptation :

- Les modifications sont sauvegardees et rechargees.
- Les erreurs de reference sont affichees ou refusees proprement.

Validation :

- Tests serializer si des cas nouveaux apparaissent.
- Smoke test manuel UI.
- Build principal.

Commit attendu :

- `feat(editor): edit basic composed animation2d data`

## Phase 8 - Nettoyage et documentation

### ⏳ Tache 8.1 - Documenter le format compose V1

Objectif : fournir une reference sans melanger import specifique et runtime generique.

Actions :

- Ajouter ou mettre a jour une doc du format `.anim2d` compose.
- Inclure un exemple legacy et un exemple compose minimal.
- Expliquer la strategie de compatibilite.
- Mentionner explicitement ce qui n'est pas dans la V1.

Validation :

- Relecture du markdown.
- Build principal si le repo l'exige pour la livraison.

Commit attendu :

- `docs(animation2d): document composed format v1`

### ⏳ Tache 8.2 - Nettoyer les noms et TODO obsoletes

Objectif : reduire l'ambiguite apres livraison du runtime compose.

Actions :

- Remplacer ou supprimer les TODO devenus faux, notamment autour de la creation de sprites en draw.
- Ajouter des commentaires courts uniquement si une compatibilite legacy n'est pas evidente.
- Ne pas renommer massivement les API publiques sans plan de migration.

Validation :

- Build principal.
- Tests animation 2D cibles.

Commit attendu :

- `chore(animation2d): clean composed animation migration leftovers`

## Risques a suivre

- Casser les nombreux `.anim2d` existants en changeant le format trop vite.
- Melanger les events internes de changement de frame avec les events gameplay/authoring.
- Introduire des allocations par frame lors du sampling, du tri de draw order ou du dessin.
- Coupler le runtime 2D a l'editeur ou a un importer specifique.
- Rendre `AnimatedSpriteComponent` ambigu entre frame unique et composition multi-parties sans API claire.
- Sous-estimer les bounds et collisions quand plusieurs sprites composent une meme animation.
- Ajouter un editeur timeline complet avant d'avoir un runtime compose stable.

## Definition de fini V1

- Les anciens `.anim2d` se chargent encore sans migration de fichier.
- Une animation 2D composee peut afficher plusieurs sprites simultanement.
- Les parties supportent au minimum sprite courant, position, visibilite, draw order et flip.
- Les events d'animation 2D sont serialises, charges et dispatches pendant `Update`.
- Le rendu compose n'alloue pas de sprites ou listes par frame.
- Les tests couvrent chargement legacy, serialization composee, sampling, events et bounds de base.
- Une demo ou validation visuelle montre une composition multi-images.