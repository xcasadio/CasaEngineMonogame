# Particle System — Plan d'implementation IA

## Source analysee

Document de depart : `docs/particle-system-features.md`

Objectif de ce fichier : transformer la specification fonctionnelle en plan d'execution pour un agent IA. Le plan doit rester maintenu pendant l'implementation : l'agent change l'icone de statut de chaque tache, execute une seule tache a la fois, valide, puis commit le code et ce fichier de suivi.

---

## Regles obligatoires pour l'agent IA

1. Une seule tache a la fois.
2. Avant de commencer une tache, remplacer son icone `⏳` par `🚧`.
3. Ne jamais commencer la tache suivante avant d'avoir termine, valide et committe la tache courante.
4. Une tache terminee doit laisser le repo dans un etat compilable.
5. A la fin de chaque tache :
   - lancer une validation bornee ;
   - mettre a jour le statut de la tache dans ce fichier ;
   - committer le code + la mise a jour du plan dans le meme commit.
6. Si une tache est bloquee :
   - remplacer `🚧` par `⚠️` ;
   - ajouter une note courte sous la tache avec la cause precise ;
   - ne pas masquer un build casse ;
   - committer seulement si le repo reste dans un etat coherent.
7. Statuts autorises :
   - `⏳ Todo`
   - `🚧 In progress`
   - `✅ Done`
   - `🧪 Needs testing`
   - `⚠️ Blocked`
8. L'agent doit respecter les instructions repo : pas de LINQ/closures/allocations dans `Update`/`Draw`, pas de nouveau code WPF, restauration des etats `GraphicsDevice`, et build local avant de considerer une tache finie.

## Validation minimale par tache

- Build principal : `dotnet build CasaEngine.Editor.MonoGame.sln -c Debug --no-restore`
- Tests runtime purs : `dotnet test CasaEngine.Tests\CasaEngine.Tests.csproj -c Debug --filter FullyQualifiedName~Particle --no-restore`
- Tests rendu/materials si touches : `dotnet test CasaEngine.Tests\CasaEngine.Tests.csproj -c Debug --filter FullyQualifiedName~Rendering --no-restore`
- Demo si workflow visible : lancer depuis `CasaEngine.Demos` car les demos resolvent leur `Content` depuis le repertoire courant.

---

## Critique de la specification initiale

### Points solides

- La separation `ParticleEffectAsset` / instance runtime / rendu est la bonne base pour CasaEngine.
- Le choix CPU-first est adapte a MonoGame et facilite debug, serialisation, preview et fallback.
- La spec insiste correctement sur le pooling, les buffers prealloues et l'absence d'allocation par frame.
- Les courbes et gradients sont identifies comme briques transverses utiles, pas seulement comme options cosmetiques.
- L'editeur, la preview, le hot reload, les stats et les tests sont traites comme des parties du produit, pas comme du bonus.
- La roadmap V1 -> V4 est saine : d'abord robuste et simple, puis production, modulaire, rendu avance, GPU.

### Points a recadrer avant implementation

- Le perimetre V1 est trop large pour une premiere passe. Il faut livrer des tranches verticales petites : asset + serializer + runtime teste, puis composant, puis renderer, puis editor.
- Le backlog final est trop granulaire et ne mappe pas assez aux frontieres CasaEngine existantes. Il faut regrouper les micro-fonctions dans des taches compilables et testables.
- Le format `.particle.json` n'est pas aligne avec les extensions existantes (`.material`, `.environment`, `.staticModel`). Pour CasaEngine, utiliser une extension logique `.particle` contenant du JSON.
- Les exemples `AssetReference<Texture2D>` ne correspondent pas au style courant. Preferer des `Guid` d'asset et la resolution via `AssetContentManager` / `AssetCatalog`.
- L'éditeur doit etre MGUI. Aucun nouveau controle WPF ne doit etre introduit.
- Le renderer particules ne doit pas etre un simple `Draw` direct depuis le composant. Il doit suivre le modele `IViewFlushableRenderer` pour etre compatible multi-view, editor viewport, split-screen et `RenderStats`.
- `SpriteBatch` peut aider pour une V1 tres simple, mais le chemin principal des billboards 3D doit viser un renderer dedie avec buffer dynamique, sinon les futures features (tri, shaders, flipbook, instancing) seront bloquees.
- Le document demande beaucoup de features editor V1. Il faut repousser undo/redo complet, thumbnails, presets riches, gizmos avances et hot reload complet en V1.5, apres une base runtime fiable.
- La serialisation doit eviter tout objet MonoGame (`Texture2D`, `Effect`, states GPU) dans les assets. Seuls les IDs, noms de states et valeurs authoring doivent etre persistés.
- Les bounds/culling sont critiques dans CasaEngine, car `World.Draw` passe par les entites visibles. Le composant particules doit exposer des bounds coherents et prevoir le cas `AlwaysVisible`.

### Decisions de cadrage V1

- V1 cible : particules CPU, billboards 3D non eclaires, alpha/additive, emission continue + bursts, formes simples, courbes/gradients lineaires, serialization `.particle`, composant scene, renderer dedie, demo minimale, editor preview basique.
- Non-objectifs V1 : collisions, sub-emitters, ribbons/trails, soft particles, distortion, lit particles, simulation GPU, compute shaders, mesh particles, module stack generique.
- V1.5 cible : confort production editor, undo/redo complet, thumbnails, presets, gizmos, flipbook, hot reload plus robuste, profiling detaille.
- V2+ cible : architecture modulaire type Spawn/Initialize/Update/Render.

---

## Architecture cible V1

### Fichiers et namespaces probables

- `CasaEngine/Framework/Particles/Authoring/`
  - `ParticleEffectAsset`
  - `ParticleEmitterDefinition`
  - modules authoring : emission, shape, initial, simulation, renderer
  - `FloatRange`, `Vector2Range`, `FloatCurve`, `ColorGradient`
- `CasaEngine/Framework/Particles/Runtime/`
  - `ParticleRuntimeInstance`
  - `ParticleEmitterRuntime`
  - `Particle`
  - `ParticlePlaybackState`
  - samplers de shapes
- `CasaEngine/Framework/Particles/Rendering/`
  - `ParticleRendererComponent : DrawableGameComponent, IViewFlushableRenderer`
  - `ParticleRenderPacket`
  - vertex types, sort keys, blend/depth state mapping
- `CasaEngine/Framework/Scene/Entities/Components/`
  - `ParticleSystemComponent : PrimitiveComponent`
- `CasaEngine/Framework/Assets/Loaders/`
  - `ParticleEffectAssetLoader`
- `CasaEngine/Framework/Particles/Serialization/`
  - serializer JSON dedie
- `CasaEngine.Editor/Controls/`
  - `ParticleAssetInspectorPanel`
  - `ParticlePreviewViewport`
- `CasaEngine.EditorServices/`
  - support creation/sauvegarde `.particle` et catalog si necessaire
- `CasaEngine.Tests/Particles/`
  - tests deterministes runtime, courbes, gradients, serialisation
- `CasaEngine.Demos/`
  - demo visuelle minimale

### Principes techniques

- Un asset ne contient jamais l'etat vivant des particules.
- Une instance runtime ne garde pas de reference forte a une camera.
- La simulation se fait dans `Update`, pas dans `Draw`.
- Le draw du composant enfile des packets dans le renderer ; le renderer flush par `RenderFrame`.
- Les buffers particules sont prealloues par emetteur selon `MaxParticles`.
- Les listes/buffers internes sont reutilises et `Clear()` uniquement.
- Le renderer restaure ou encadre les etats GPU qu'il modifie.
- Les textures/effects sont resolus par ID au runtime, avec fallback stable.
- Les tests de logique pure doivent etre ajoutes avant ou avec la feature correspondante.

---

## Criteres d'acceptation V1

- Un asset `.particle` peut etre cree, charge, sauvegarde et recharge sans perte majeure.
- Un `ParticleSystemComponent` peut referencer un asset particule et jouer l'effet dans une scene.
- Au moins deux emetteurs peuvent vivre dans un meme effet.
- L'emission continue et les bursts sont deterministes avec seed fixe.
- Les formes point, disc/circle, box, sphere et cone ont des tests de sampling basiques.
- Les courbes size/alpha et le gradient color sont evalues par age normalise.
- Les particules supportent lifetime, position, velocity, acceleration, size, rotation, color et alpha.
- Les modes alpha et additive fonctionnent visuellement.
- Le renderer particules contribue a `RenderStats`.
- Les bounds/culling de base fonctionnent, avec option `AlwaysVisible`.
- Aucune allocation evitable par frame dans les boucles de simulation/rendu V1.
- Une demo permet de verifier fumee/feu/etincelles ou effets equivalents.
- Une preview editor basique permet play/pause/stop/restart et edition/sauvegarde des proprietes principales.

---

## Phase 0 — Audit et cadrage d'integration

- ✅ **T00.01 — Auditer les points d'ancrage CasaEngine**
  Objectif :
  - Identifier les chemins exacts pour asset loading, editor serializer, component serialization, renderer registration, demos et tests.
  - Verifier comment `RenderPipeline` recoit la liste des `IViewFlushableRenderer`.
  - Verifier comment les components sont charges/sauvegardes dans `EditorEntityJsonSerializer`.
  - Ajouter sous cette tache une note courte si le plan doit changer.
  Notes d'audit :
  - Asset loading : `AssetLoaderRegistry.RegisterLoaders` enregistre les loaders par type ; ajouter `ParticleEffectAssetLoader` et la constante `.particle`.
  - Sauvegarde editor : `EditorAssetJsonSerializer.TrySerialize` doit connaitre `ParticleEffectAsset`, puis `EditorAssetWriterService.SaveDocument` publie `AssetSaved`.
  - Components : `Entity.Load` et `SceneComponent.Load` passent par `ElementFactory.Load<T>` et le champ JSON `type`; `EditorEntityJsonSerializer.SaveComponent` doit ajouter un cas `ParticleSystemComponent` pour sauver les champs dedies.
  - Renderers : `CasaEngineGame.Initialize` construit explicitement le tableau `IViewFlushableRenderer[]`; le renderer particules doit y etre ajoute apres les meshes/skinned et avant les lignes/UI selon l'ordre retenu.
  - Multi-view : `RenderPipeline` appelle `view.World.Draw(in frame)` puis `renderer.Flush(in frame, view.RenderStats)` par vue ; la simulation particules doit donc rester dans `Update`, et `Draw` ne doit qu'enfiler des donnees de rendu.
  - Tests : ajouter les tests purs sous `CasaEngine.Tests/Particles/` avec filtre `FullyQualifiedName~Particle`.
  Validation :
  - Lecture ciblee + build solution si aucun build recent n'est disponible.
  Commit conseille :
  - `docs(particles): audit implementation entry points`

- ✅ **T00.02 — Ajouter les constantes et squelettes de namespaces**
  Objectif :
  - Ajouter `Constants.FileNameExtensions.Particle = ".particle"`.
  - Creer les dossiers/namespaces sans logique lourde.
  - Ajouter les types enum publics stables : blend, sort, shape, simulation space, render mode, playback state.
  Validation :
  - Build solution.
  Commit conseille :
  - `feat(particles): add particle extension and core enums`

---

## Phase 1 — Donnees authoring et serialisation

- ✅ **T01.01 — Ajouter les primitives de valeurs particules**
  Objectif :
  - Creer `FloatRange`, `Vector2Range` et helper random deterministe sans allocation.
  - Encadrer les valeurs invalides (`min > max`, NaN, infini) par normalisation ou validation explicite.
  Validation :
  - Tests cibles ranges/random seed.
  - Build solution.
  Commit conseille :
  - `feat(particles): add deterministic particle value ranges`

- ✅ **T01.02 — Ajouter `FloatCurve` lineaire**
  Objectif :
  - Supporter points temps/valeur, clamp temps 0..1, evaluation lineaire, presets constant/fade/bell/pulse.
  - Eviter toute allocation pendant `Evaluate`.
  Validation :
  - Tests evaluation, clamp, presets, points non tries si supportes.
  - Build solution.
  Commit conseille :
  - `feat(particles): add float curve evaluation`

- ✅ **T01.03 — Ajouter `ColorGradient` lineaire**
  Objectif :
  - Supporter stops couleur et alpha, evaluation par age normalise, presets white/fire/smoke/magic.
  - Garder les structures serialisables et simples pour l'editeur.
  Validation :
  - Tests interpolation couleur/alpha et clamp.
  - Build solution.
  Commit conseille :
  - `feat(particles): add color gradient evaluation`

- ✅ **T01.04 — Creer `ParticleEffectAsset` et definitions d'emetteurs**
  Objectif :
  - Ajouter asset authoring, emitters et modules V1 : emission, shape, initial, simulation, renderer.
  - Stocker seulement des donnees authoring : IDs d'assets, noms de states, nombres, enums, curves, gradients.
  - Ajouter `Validate()` pour erreurs courantes : aucun emitter, max particles <= 0, texture manquante si obligatoire, durees invalides.
  Validation :
  - Tests validation authoring.
  - Build solution.
  Commit conseille :
  - `feat(particles): add particle effect authoring asset`

- ✅ **T01.05 — Ajouter serialisation JSON `.particle`**
  Objectif :
  - Creer serializer/load pour `ParticleEffectAsset`.
  - Ajouter version/schema version et migration V1 no-op.
  - Brancher `ParticleEffectAssetLoader` dans `AssetLoaderRegistry`.
  - Brancher `EditorAssetJsonSerializer` pour sauvegarde editor.
  Validation :
  - Tests load/save roundtrip.
  - Build solution.
  Commit conseille :
  - `feat(particles): serialize particle effect assets`

- ✅ **T01.06 — Ajouter un asset particule minimal de test**
  Objectif :
  - Ajouter un fichier `.particle` minimal dans un projet/sample adapte.
  - L'enregistrer dans le catalogue d'assets si necessaire.
  - Garder l'asset sans dependance texture fragile, ou utiliser une texture existante stable.
  Validation :
  - Test de chargement via `AssetContentManager`.
  - Build solution.
  Commit conseille :
  - `test(particles): add minimal particle asset fixture`

---

## Phase 2 — Runtime CPU et simulation

- ✅ **T02.01 — Ajouter les buffers runtime et le pooling**
  Objectif :
  - Creer `ParticleRuntimeInstance`, `ParticleEmitterRuntime` et `Particle`.
  - Preallouer tableaux selon `MaxParticles`.
  - Ajouter freelist d'indices morts/vivants sans allocation par frame.
  Validation :
  - Tests spawn/kill/reuse indices, max particles strict.
  - Build solution.
  Commit conseille :
  - `feat(particles): add pooled particle runtime buffers`

- ✅ **T02.02 — Ajouter le controle playback**
  Objectif :
  - Implementer play/pause/stop/restart, looping, one-shot, duration, start delay, simulation speed.
  - Definir clairement `Stop(clearParticles)` et l'etat `IsAlive`.
  Validation :
  - Tests de transitions et timers.
  - Build solution.
  Commit conseille :
  - `feat(particles): add particle playback state machine`

- ✅ **T02.03 — Implementer emission rate et bursts**
  Objectif :
  - Ajouter rate over time avec accumulateur fractionnaire.
  - Ajouter bursts par temps, count min/max, seed deterministe.
  - Garantir qu'un burst ne se declenche qu'une fois par cycle de loop.
  Validation :
  - Tests bas framerate, haut framerate, seed fixe, loop.
  - Build solution.
  Commit conseille :
  - `feat(particles): implement rate and burst emission`

- ⏳ **T02.04 — Implementer les formes d'emission V1**
  Objectif :
  - Ajouter point, circle/disc, box, sphere et cone.
  - Supporter emission volume/surface quand c'est pertinent.
  - Retourner position + direction initiale sans allocation.
  Validation :
  - Tests de bornes de sampling et direction normalisee.
  - Build solution.
  Commit conseille :
  - `feat(particles): add v1 emitter shape samplers`

- ⏳ **T02.05 — Implementer initialisation et simulation des particules**
  Objectif :
  - Initialiser lifetime, speed, size, rotation, angular velocity, color, alpha.
  - Simuler velocity/acceleration, gravity scale, drag, rotation.
  - Appliquer size/alpha/color over lifetime via curve/gradient.
  Validation :
  - Tests update deterministes sur plusieurs pas de temps.
  - Build solution.
  Commit conseille :
  - `feat(particles): simulate particle lifetime motion and color`

- ⏳ **T02.06 — Ajouter local/world space et bounds runtime**
  Objectif :
  - Supporter simulation locale et monde sans double-transform.
  - Calculer bounds par emitter et effet.
  - Ajouter option `AlwaysVisible` au niveau renderer/component si necessaire.
  Validation :
  - Tests bounds et transformation local/world.
  - Build solution.
  Commit conseille :
  - `feat(particles): support simulation space and runtime bounds`

---

## Phase 3 — Component scene et integration moteur

- ⏳ **T03.01 — Creer `ParticleSystemComponent`**
  Objectif :
  - Ajouter composant scene avec `ParticleEffectAssetId`, `PlayOnStart`, `Looping`, `SimulateInEditor`, overrides V1 simples.
  - Charger l'asset via `AssetContentManager` dans `InitializeWithWorld`.
  - Cloner et serializer le composant proprement.
  Validation :
  - Tests serializer component si possible.
  - Build solution.
  Commit conseille :
  - `feat(particles): add particle system component`

- ⏳ **T03.02 — Brancher update component/runtime**
  Objectif :
  - Faire avancer l'instance runtime dans `Update` une seule fois par frame logique.
  - Respecter enabled, owner/world actifs, pause, play on start.
  - Eviter allocation et acces camera dans update.
  Validation :
  - Tests runtime component ou smoke world minimal.
  - Build solution.
  Commit conseille :
  - `feat(particles): update particle component runtime`

- ⏳ **T03.03 — Integrer bounds avec les entites visibles**
  Objectif :
  - Exposer des bounds coherents pour le culling world/editor.
  - Verifier interaction avec `IBoundingBoxable`, `PrimitiveComponent` et spatial index.
  - Ajouter fallback bounds authoring pour effet sans particules vivantes.
  Validation :
  - Tests bounds ou smoke scene avec camera entrant/sortant.
  - Build solution.
  Commit conseille :
  - `feat(particles): expose particle component bounds`

- ⏳ **T03.04 — Ajouter API gameplay minimale**
  Objectif :
  - Exposer `Play`, `Pause`, `Stop`, `Restart`, `Emit(int count)` sur le composant.
  - Ajouter setters d'overrides V1 limites si simples (`SimulationSpeed`, `ColorTint`, `EmissionScale`).
  - Ne pas introduire encore blackboard/module stack.
  Validation :
  - Tests API playback/emit.
  - Build solution.
  Commit conseille :
  - `feat(particles): expose particle gameplay controls`

---

## Phase 4 — Renderer particules V1

- ⏳ **T04.01 — Creer packets de rendu particules**
  Objectif :
  - Definir les donnees extraites du runtime pour le rendu : position, size, rotation, color, alpha, texture/material, sort data.
  - Separer extraction render data et execution GPU.
  Validation :
  - Tests de generation de packets sans GPU si possible.
  - Build solution.
  Commit conseille :
  - `feat(particles): add particle render packets`

- ⏳ **T04.02 — Ajouter `ParticleRendererComponent`**
  Objectif :
  - Implementer `DrawableGameComponent` + `IViewFlushableRenderer`.
  - Utiliser buffers reutilises pour billboards 3D CPU.
  - Restaurer/encadrer les etats `GraphicsDevice` modifies.
  Validation :
  - Build solution.
  - Smoke manuel si le renderer peut etre instancie sans scene.
  Commit conseille :
  - `feat(particles): add particle billboard renderer`

- ⏳ **T04.03 — Brancher le renderer dans le pipeline multi-view**
  Objectif :
  - Enregistrer le renderer dans le jeu et la liste des `IViewFlushableRenderer` au bon ordre, apres geometrie opaque et avant overlays/UI.
  - S'assurer qu'une meme frame multi-camera n'avance pas la simulation plusieurs fois.
  Validation :
  - Build solution.
  - Smoke split-screen/editor viewport si disponible.
  Commit conseille :
  - `feat(particles): flush particles per render view`

- ⏳ **T04.04 — Ajouter blend/depth/sort V1**
  Objectif :
  - Supporter alpha blend et additive.
  - Supporter depth test configurable et depth write off par defaut.
  - Ajouter sorting none, distance back-to-front, layer/render queue.
  Validation :
  - Tests sort keys si purs.
  - Build solution.
  Commit conseille :
  - `feat(particles): add particle blend depth and sorting`

- ⏳ **T04.05 — Brancher textures et fallbacks de material**
  Objectif :
  - Resoudre texture par `Guid` via `AssetContentManager`.
  - Fournir texture fallback visible si asset manquant.
  - Ne pas stocker `Texture2D` dans l'asset authoring.
  Validation :
  - Tests loader/fallback si possible.
  - Build solution.
  Commit conseille :
  - `feat(particles): resolve particle renderer textures`

- ⏳ **T04.06 — Alimenter `RenderStats` et debug stats**
  Objectif :
  - Compter draw calls, texture binds, transparent items et nombre de particules rendues si un champ dedie est ajoute.
  - Afficher ou exposer les stats sans casser l'overlay existant.
  Validation :
  - Tests stats si purs.
  - Build solution.
  Commit conseille :
  - `feat(particles): report particle render stats`

---

## Phase 5 — Demo et validation V1 runtime

- ⏳ **T05.01 — Creer assets d'effets V1 de demonstration**
  Objectif :
  - Ajouter au moins trois effets simples : smoke puff, spark burst, fire loop ou equivalents.
  - Utiliser textures existantes ou assets minimaux clairement catalogues.
  - Garder les assets petits et lisibles.
  Validation :
  - Chargement des assets via tests ou smoke demo.
  - Build solution.
  Commit conseille :
  - `feat(particles): add sample particle effects`

- ⏳ **T05.02 — Ajouter une demo `ParticleSystemDemo`**
  Objectif :
  - Creer une scene/demo qui joue un effet looping et des bursts successifs.
  - Ajouter controles minimaux si les demos existantes ont un pattern equivalent.
  - Montrer alpha/additive et plusieurs emitters.
  Validation :
  - Build solution.
  - Lancer la demo depuis `CasaEngine.Demos`.
  Commit conseille :
  - `feat(demos): add particle system demo`

- ⏳ **T05.03 — Ajouter tests de stabilite runtime**
  Objectif :
  - Couvrir 100 explosions one-shot successives.
  - Couvrir effet looping long en temps simule.
  - Couvrir activation/desactivation component et restart.
  Validation :
  - Tests filtres `FullyQualifiedName~Particle`.
  - Build solution.
  Commit conseille :
  - `test(particles): cover runtime stability cases`

---

## Phase 6 — Editeur MGUI V1

- ⏳ **T06.01 — Ajouter creation/ouverture d'asset `.particle`**
  Objectif :
  - Ajouter creation depuis Content Browser si le pattern existe.
  - Ouvrir un document/panel particules depuis `GameEditor`.
  - Reutiliser `EditorAssetWriterService` et `EditorDirtyStateService`.
  Validation :
  - Build solution.
  - Smoke creation/open asset dans l'editeur.
  Commit conseille :
  - `feat(editor): open particle assets from content browser`

- ⏳ **T06.02 — Creer `ParticleAssetInspectorPanel` basique**
  Objectif :
  - Afficher nom asset, source, statut dirty.
  - Afficher liste d'emitters et proprietes principales : duration, looping, max particles, rate, shape, lifetime, speed, size, color, texture, blend.
  - Sauvegarder l'asset et marquer dirty proprement.
  Validation :
  - Build solution.
  - Smoke edit/save/reload.
  Commit conseille :
  - `feat(editor): add particle asset inspector panel`

- ⏳ **T06.03 — Ajouter preview viewport particules**
  Objectif :
  - Creer preview isolee inspiree du material preview/world viewport.
  - Toolbar : play, pause, stop, restart, loop, sim speed, reset camera.
  - Afficher compteur de particules vivantes et draw calls si disponible.
  Validation :
  - Build solution.
  - Smoke preview avec les trois assets sample.
  Commit conseille :
  - `feat(editor): preview particle effects in editor`

- ⏳ **T06.04 — Ajouter edition basique curves/gradients**
  Objectif :
  - Fournir UI MGUI simple pour points de courbe/gradient.
  - Supporter presets et reset.
  - Eviter un graphe avance en V1 si trop couteux ; une edition tabulaire est acceptable.
  Validation :
  - Build solution.
  - Smoke edit curve/gradient puis sauvegarde/reload.
  Commit conseille :
  - `feat(editor): edit particle curves and gradients`

- ⏳ **T06.05 — Ajouter drag and drop particule vers entite**
  Objectif :
  - Permettre de deposer un asset `.particle` sur le viewport ou une entite.
  - Creer/mettre a jour un `ParticleSystemComponent` avec l'asset ID.
  - Integrer undo/redo avec `EditorHistoryService` pour l'ajout component.
  Validation :
  - Build solution.
  - Smoke drag/drop dans l'editeur.
  Commit conseille :
  - `feat(editor): drop particle assets onto entities`

- ⏳ **T06.06 — Ajouter hot reload simple des assets particules**
  Objectif :
  - Ecouter sauvegarde `.particle` et rafraichir previews/instances concernees.
  - Definir comportement V1 : restart runtime ou conserver etat si compatible.
  - Documenter la decision dans ce fichier si necessaire.
  Validation :
  - Build solution.
  - Smoke edition asset utilise par une scene ouverte.
  Commit conseille :
  - `feat(editor): hot reload particle assets`

---

## Phase 7 — V1.5 production editor

- ⏳ **T07.01 — Ajouter undo/redo complet de l'inspector particules**
  Objectif :
  - Couvrir changements numeriques, enums, texture, ajout/suppression emitter, bursts, curves, gradients.
  - Grouper les edits drag continus en transactions si le framework le permet.
  Validation :
  - Build solution.
  - Smoke undo/redo editor.
  Commit conseille :
  - `feat(editor): make particle edits undoable`

- ⏳ **T07.02 — Ajouter gizmos d'emission**
  Objectif :
  - Dessiner point, circle/sphere, box, cone et bounds runtime dans viewport/preview.
  - Respecter les overlays editor existants.
  Validation :
  - Build solution.
  - Smoke gizmos on/off.
  Commit conseille :
  - `feat(editor): draw particle emission gizmos`

- ⏳ **T07.03 — Ajouter thumbnails et presets**
  Objectif :
  - Generer thumbnail offscreen ou placeholder stable pour assets `.particle`.
  - Ajouter presets d'effets et presets de courbes/gradients.
  Validation :
  - Build solution.
  - Smoke Content Browser thumbnails.
  Commit conseille :
  - `feat(editor): add particle thumbnails and presets`

- ⏳ **T07.04 — Ajouter flipbook texture sheet V1.5**
  Objectif :
  - Ajouter atlas grid, frame count, random start frame, frame over lifetime/FPS.
  - Etendre serializer, runtime et renderer sans casser les assets V1.
  Validation :
  - Tests frame selection.
  - Build solution.
  - Smoke demo explosion/fire flipbook si asset disponible.
  Commit conseille :
  - `feat(particles): add flipbook texture sheet animation`

- ⏳ **T07.05 — Ajouter profiling detaille particules**
  Objectif :
  - Mesurer simulation CPU, extraction/render CPU, live/dead/emitted/killed counts, max reached.
  - Afficher dans preview et debug overlay si pertinent.
  Validation :
  - Tests compteurs si purs.
  - Build solution.
  Commit conseille :
  - `feat(particles): expose particle profiling metrics`

---

## Phase 8 — Preparation V2 modulaire

- ⏳ **T08.01 — Documenter les limites V1 et migrations V2**
  Objectif :
  - Mettre a jour docs avec ce qui est supporte, ce qui ne l'est pas, et la trajectoire module stack.
  - Decrire comment migrer les modules fixes V1 vers Spawn/Initialize/Update/Render.
  Validation :
  - Relecture docs.
  - Build non requis sauf changement code adjacent.
  Commit conseille :
  - `docs(particles): document v1 limits and v2 migration path`

- ⏳ **T08.02 — Introduire des interfaces internes sans changer l'UX V1**
  Objectif :
  - Ajouter abstractions minimales pour modules internes si elles reduisent vraiment la duplication.
  - Ne pas exposer encore une API publique de modules custom.
  Validation :
  - Tests runtime existants.
  - Build solution.
  Commit conseille :
  - `refactor(particles): prepare runtime module boundaries`

---

## Backlog explicitement hors V1

- Collisions plan/AABB/sphere/physics world.
- Sub emitters et events on death/collision.
- Trails, ribbons et stretched billboards avances.
- Soft particles, distortion/refraction, lit particles, shadows.
- Mesh particles et decal particles.
- GPU instancing avance et simulation GPU.
- Vector fields, turbulence/curl noise, attractors/repulsors.
- LOD automatique complet.
- Blackboard de parametres expose a la Niagara.

---

## Notes de suivi

- 2026-05-20 : le repo ne contient pas encore de fichiers `Particle*`. `ComponentUpdateOrder` et `ComponentDrawOrder` contiennent deja une entree `ParticleComponent`, ce qui suggere une place prevue mais non implementee.
- 2026-05-20 : le systeme de rendu actuel flush des renderers par vue via `IViewFlushableRenderer`; le renderer particules doit suivre ce pattern pour rester compatible multi-view/editor.
- 2026-05-20 : les assets modernes comme `MaterialAsset` utilisent `ObjectBase`, `AssetContentManager`, `AssetLoaderRegistry`, `EditorAssetJsonSerializer` et `EditorAssetWriterService`; le systeme particules doit s'aligner dessus.
- 2026-05-20 : les instructions editeur imposent MGUI et pas de nouveau WPF.
