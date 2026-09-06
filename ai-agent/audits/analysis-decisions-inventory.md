# Inventaire des décisions d'architecture existantes (rétro-remplissage ADR)

Relevé du 2026-09-06 pour la tâche T4.3 du plan [ai-guidelines-tasks.md](../tasks/ai-guidelines-tasks.md).

Méthode : six sous-agents en lecture seule (modèle haiku, un par thématique) ont lu intégralement les sources listées dans le plan et rendu, pour chaque décision, le texte ou un résumé fidèle, la source `fichier:ligne`, la date quand le document la donne, les doublons, et un statut d'implémentation constaté par `Grep`/`Glob`. **Les statuts et les résumés sont ceux des relevés** : chaque décision est relue à sa source et vérifiée dans le code au moment d'écrire son ADR (tâches T4.4 → T4.9) ; l'ADR fait foi, pas ce relevé.

Regroupement retenu (décision P5 du plan, amendée par l'auteur : un fichier par décision, regroupement possible par thématique) : voir la dernière section.

## 1. Audio (13 décisions, 2 sources)

Sources : `ai-agent/audits/analysis-audio-system.md` (D1 → D13, D2-bis), `docs/engine/audio-system.md`.

| # | Décision | Source | Date | Doublon avec | Statut | Preuve |
|---|---|---|---|---|---|---|
| D1 | « Les « channels » du moteur sont des **bus nommés** (Master / Music / SFX / Voice / UI / Editor), créés par défaut sous Master » | analysis-audio-system.md:191 | 2026-08-26 | audio-system.md:36-42 | Appliquée | AudioBusNames.cs:8-42 ; AudioBus.cs ; AudioMixer.cs |
| D2 | `MediaPlayer` + `Song` (remplacé par D2-bis) | analysis-audio-system.md:192 | 2026-08-26 | — | Abandonnée | Aucun MediaPlayer/Song dans Framework/Audio |
| D2-bis | « **Streaming maison dès la V1** : `DynamicSoundEffectInstance` + lecteur **RIFF PCM** écrit dans le moteur. `MediaPlayer`/`Song` est **abandonné**. […] Un décodeur Ogg (NVorbis […]) pourra être branché plus tard **sur la même API**, sans rupture. » | analysis-audio-system.md:193 | 2026-08-26 | audio-system.md:120-134 | Appliquée | WavStreamReader.cs ; MusicPlayer.cs ; MonoGameAudioBackend.cs |
| D3 | « **Asset JSON `.sound`** référençant le fichier audio + métadonnées (comme `.texture` référence un `.png`) » | analysis-audio-system.md:194 | 2026-08-26 | audio-system.md:59-74 | Appliquée | SoundAsset.cs ; SoundAssetLoader.cs ; Constants.cs:32 |
| D4 | « Métadonnées `.sound` V1 : Référence fichier (Guid) + **volume** + **pitch** + **loop**, **bus cible**, **mode streaming** explicite. ❌ pas de variations aléatoires en V1. » | analysis-audio-system.md:195 | 2026-08-26 | audio-system.md:77-84 | Appliquée | SoundAsset.cs:28-58 |
| D5 | « **2D seulement** (volume + pan). Pas d'`Apply3D`, pas de listener, pas de Doppler en V1. » | analysis-audio-system.md:196 | 2026-08-26 | audio-system.md:193-194 | Appliquée | IAudioBackend.cs:10-11 |
| D6 | « **WAV pour les SFX et — depuis D2-bis — pour la musique.** L'Ogg Vorbis devient une extension ultérieure […]. Le `.mp3` est **retiré** du mapping du Content Browser […]. » | analysis-audio-system.md:197 | 2026-08-26 | audio-system.md:188-192 | Appliquée | Constants.cs:32 ; SoundEffectLoader.cs ; ContentItem.cs |
| D7 | « **Device + bus globaux** (`GameComponent` sur `CasaEngineGame`), **voix rattachées au monde** et coupées par `World.Clear()`. » | analysis-audio-system.md:198 | 2026-08-26 | audio-system.md:174-182 | Appliquée | AudioSystemComponent.cs ; World.cs |
| D8 | « **Inspecteur d'asset `.sound`** (document ouvert au double-clic, avec preview) + **menu contextuel « Create Sound »** sur les dossiers. ❌ pas de preview directe dans le Content Browser, ❌ pas de panneau mixer, ❌ pas de forme d'onde. » | analysis-audio-system.md:199 | 2026-08-26 | audio-system.md:90-91 | Appliquée | SoundAssetInspectorPanel.cs ; GameEditor.cs |
| D9 | « **Suppression** de `Sound.cs`, `IAudioEmitter.cs`, `AudioComponent.cs` (aucun appelant dans le repo). » | analysis-audio-system.md:200 | 2026-08-26 | — | Appliquée | Aucun de ces fichiers trouvé |
| D10 | « **Abstraction backend `IAudioBackend`** (implémentation OpenAL + fake de test) pour rendre bus, voix, fades et routage testables sans device. […] » | analysis-audio-system.md:201 | 2026-08-26 | — | Appliquée | IAudioBackend.cs ; MonoGameAudioBackend.cs ; NullAudioBackend.cs ; FakeAudioBackend.cs |
| D11 | « **Démo** dans `CasaEngine.Demos` + **commandes de cutscene** (`PlaySound`/`PlayMusic`/`StopMusic`/`FadeMusic`) + **`SoundEmitterComponent`** d'entité. ❌ pas d'événement audio d'animation 2D en V1. » | analysis-audio-system.md:202 | 2026-08-26 | audio-system.md:220-233, 158-169, 139-154 | Appliquée | AudioDemo.cs ; *CutsceneActionData.cs ; SoundEmitterComponent.cs |
| D12 | « **Bus « Editor » séparé** des bus du jeu (volume/mute propres) : la preview de l'inspecteur ne passe jamais par les bus du jeu et survit au `Stop`. » | analysis-audio-system.md:203 | 2026-08-26 | audio-system.md:42, 174-182 | Appliquée | AudioBusNames.cs:24-28 ; SoundAssetInspectorPanel.cs |
| D13 | « Tout le chantier est développé sur une branche dédiée **`audio-system`**, un commit par tâche, merge sur `main` après validation. » | analysis-audio-system.md:204 | 2026-08-26 | — | Non vérifiable par lecture | Règle de processus, couverte par l'ADR des règles de travail des agents |

## 2. Collision et physique (19 décisions distinctes, 2 sources)

Sources : `docs/engine/collision-2d-3d-architecture.md`, `ai-agent/audits/analysis-bepuphysics2-migration.md`.

| # | Décision | Source | Date | Doublon avec | Statut |
|---|---|---|---|---|---|
| 1 | Posture de compatibilité : aucun projet n'utilise CasaEngine hors démos/tests/éditeur ; pas de rétrocompatibilité d'API ni d'assets ; chaque phase supprime ce qu'elle remplace | collision-2d-3d-architecture.md:12 | 2026-08 | analysis-bepuphysics2-migration.md:103 | Appliquée : PhysicsDefinition nettoyée, BepuPhysicsEngine remplace BulletPhysicsEngine |
| 2 = D1 | Règle centrale : une seule simulation physique, en 3D ; la « 2D » d'un jeu est une politique du monde, jamais une seconde pile | collision-2d-3d-architecture.md:19 et :183 | 2026-08 | — | Appliquée : SimulationSpacePolicy (Identity3d / Planar2d / TopDownElevation) ; BulletSharp supprimé |
| D2 | Quatre couches : Shape (géométrie pure immuable), Fixture (forme + pose locale + sémantique), Body, World | collision-2d-3d-architecture.md:194 | 2026-08 | — | Appliquée : ColliderFixture, PhysicsBody, IPhysicsWorld |
| D2.a | Shape3d unique vocabulaire public des volumes ; aucun intermédiaire PhysicsShape | collision-2d-3d-architecture.md:205 | 2026-08 | — | Appliquée : PhysicsShape supprimé |
| D2.b | Shape2d s'abaisse vers Shape3d via SimulationSpacePolicy ; Shape2d perd Position/Rotation (migrent vers Collision2d) | collision-2d-3d-architecture.md:211 | 2026-08 | — | Appliquée |
| D2.c | Aucune forme ne porte de pose ; la pose appartient à la fixture | collision-2d-3d-architecture.md:214 | 2026-08 | :664 | Appliquée |
| D3 | Canaux, réponses, profils nommés (modèle Unreal réduit) : CollisionResponse (Ignore/Overlap/Block), CollisionProfile | collision-2d-3d-architecture.md:240 | 2026-08 | — | Appliquée : CollisionChannels, CollisionResponse, CollisionProfile |
| D3.a | Sémantique de collision = données de projet nommées, jamais des enums backend ni des booléens épars | collision-2d-3d-architecture.md:262 | 2026-08 | :667 | Appliquée : CollisionHitType supprimé |
| D4 | L'espace de simulation est une politique du monde ; composants et assets n'en savent rien | collision-2d-3d-architecture.md:276 | 2026-08 | :662 | Appliquée : ISimulationSpacePolicy |
| D4.a | Sous une politique non-identité, le corps physique lit la pose logique, jamais WorldMatrixNoScale | collision-2d-3d-architecture.md:304 | 2026-08 | :668 | Appliquée : RenderProjectionComponent |
| D5 | Deux familles de colliders : Volumes (fixtures) et Champs (données denses interrogées analytiquement, jamais bakées en corps) | collision-2d-3d-architecture.md:312 | 2026-08 | :669 | Appliquée : ICollisionField, HeightGridCollisionField |
| D6 | Fixtures animables par la timeline : sets de fixtures en keyframes Step dans l'asset d'animation ; remplace la collision par sprite | collision-2d-3d-architecture.md:368 | 2026-08 | — | Appliquée : Animation2dData.CollisionKeyframes |
| 3 | Backend Bepu (2026-08) remplace Bullet ; pas d'échelle de forme ; capteur = décision de contact ; childIndex des deux côtés | collision-2d-3d-architecture.md:36 | 2026-08 | — | Appliquée : BepuPhysicsEngine |
| 4 | Cylindre corrigé (longueur anciennement ignorée) — correction, pas décision d'architecture | analysis-bepuphysics2-migration.md:85 | 2026-08-22 | — | Appliquée |
| 5 | Version Bepu : 2.5.0-beta.29 | analysis-bepuphysics2-migration.md:119 | 2026-08-22 | — | Appliquée : Directory.Packages.props |
| 6 | LinearFactor : annulation de vitesse dans IntegrateVelocity plutôt que contrainte servo | analysis-bepuphysics2-migration.md:247 | 2026-08-22 | — | Appliquée : BepuPoseIntegratorCallbacks |
| 7 | PhysicsDefinition : suppression des champs Bullet | analysis-bepuphysics2-migration.md:250 | 2026-08-22 | — | Appliquée |
| 8 | Capteurs statiques pour les tuiles trigger : tranche 5 ou après | analysis-bepuphysics2-migration.md:251 | 2026-08-22 | — | Partielle (à vérifier) |
| 9 | Multithread hors périmètre (flag présent, non exercé) | analysis-bepuphysics2-migration.md:253 | 2026-08-22 | — | Non vérifiable |
| 10-19 | Liste « Ce qu'il ne faut pas faire » (pas de seconde pile 2D, pas de logique d'espace dans composants/assets, pas de pose sur Shape3d, pas de type Bullet dans le gameplay, pas de double chemin legacy, pas de sémantique en booléens/couleurs, pas d'asservissement pose physique/rendu, pas de terrain baké en corps, pas de hitbox résolue par le solveur, pas de hit sans identité de fixture) | collision-2d-3d-architecture.md:661-671 | 2026-08 | reprises de D1 → D6 et de la posture | Appliquées |

## 3. Rendu, materials, shaders, tilemaps, PBR (30 lignes, 5 sources)

Sources : `docs/engine/shader-naming-convention.md`, `docs/engine/materials-sources-of-truth.md`, `docs/engine/rendering-2d-3d-spaces.md`, `ai-agent/audits/analysis-tilemap-render-spaces.md`, `ai-agent/tasks/pbr-rendering-implementation-plan.md`.

| # | Décision | Source | Date | Doublon avec | Statut |
|---|---|---|---|---|---|
| 1 | `basicEffect.fx` renommé `LitForward.fx` | shader-naming-convention.md:17 | — | — | Appliquée : BuiltInShaderCatalog.cs:23 |
| 2 | Shaders utilitaires morts (`axisComponent.fx`, `simple.fx`) supprimés du contenu livré | shader-naming-convention.md:18 | — | — | Appliquée |
| 3 | `spritebatch.fx` renommé `SpriteBatch.fx` | shader-naming-convention.md:19 | — | — | Appliquée : BuiltInShaderCatalog.cs:28 |
| 4 | `TexturedPrimitive.fx` sous la règle 2D/blit (pas de préfixe Debug) | shader-naming-convention.md:20 | — | — | Appliquée |
| 5 | Règle de projection : une tilemap est un objet world-space ; son espace d'affichage est décidé par la caméra (ortho ou perspective) ou un RT intermédiaire | rendering-2d-3d-spaces.md:8-11 | — | analysis-tilemap-render-spaces.md:62-63 | Appliquée |
| 6 | Aucune logique d'espace/projection dans `TileMapComponent` ni les assets TileMap | rendering-2d-3d-spaces.md:10-11 | — | — | Appliquée |
| 7 | `Camera2dComponent.PixelSnap` arrondit la position caméra au calcul de la view matrix uniquement | rendering-2d-3d-spaces.md:46-48 | — | analysis-tilemap-render-spaces.md:149 | Appliquée : Camera2dComponent.cs:56-64, 97-100 |
| 8 | Projection orthographique `Matrix.CreateOrthographic(viewport.Width / Zoom, viewport.Height / Zoom, near, far)` | rendering-2d-3d-spaces.md:50 | — | — | Appliquée : Camera2dComponent.cs:82-88 |
| 9 | Fenêtre de profondeur [Target.Z - 500, Target.Z + 499], distance caméra 500 | rendering-2d-3d-spaces.md:53-54 | — | — | Appliquée : Camera2dComponent.cs:23 |
| 10-12 | `TileMapComponent.Draw` : fast path axis-aligned (chemin historique) vs chemin rotation (matrice monde, culling par chunk) | rendering-2d-3d-spaces.md:78-84 | — | analysis-tilemap-render-spaces.md:159-164 | Appliquée : TileMapComponent.cs:383, 650 |
| 13 | Mode render-to-texture : `TileMapSurfaceComponent` calqué sur `WorldUIComponent` | rendering-2d-3d-spaces.md:139-209 | — | analysis-tilemap-render-spaces.md:169-185 | Appliquée |
| 14 | Une map routée vers une surface doit être axis-aligned | rendering-2d-3d-spaces.md:180-182 | — | — | Appliquée : TileMapSurfaceComponent.cs:24-26 |
| 15 | `PixelPerfectDiagnostics` évalue le contrat pixel-perfect (`PixelPerfectDegradation`) | rendering-2d-3d-spaces.md:225-232 | — | — | Appliquée |
| 16 | `Camera3dIn2dAxisComponent` retirée au profit de `Camera2dComponent` | rendering-2d-3d-spaces.md:240-264 | — | — | Appliquée : classe absente |
| 17 | Matrice « sources of truth » material/shader figée (canonical / derived / cache / transitional) | materials-sources-of-truth.md:3-54 | — | — | Appliquée (documentée) |
| 18-21 | Recommandations non verrouillées (« Current gaps ») : CompiledMaterial seul descripteur, fusion GetFeatures/RenderFeatureResolver, réduction de l'overlap ShaderVariantLibrary/SelectTechnique, cubemap dans CompiledMaterial.Textures | materials-sources-of-truth.md:49-54 | — | — | Non appliquées ; **pas des décisions** |
| 22-29 | Décisions verrouillées PBR : choix par material (`lit-pbr` en plus de `lit-diffuse`), workflow metallic-roughness, forward only, plafonds de lumières inchangés, `LitPbr.fx` pixel lighting seulement, pipeline couleur par World/vue (défaut Legacy), conventions `Macros.fxh`/mgfxc, skinning PBR hors V1 | pbr-rendering-implementation-plan.md:25-32 | 2026-08-09 | — | Non implémentées (plan ⏳) |
| 30 | Mode screen-space 2D réservé à l'UI/HUD, pas aux tilemaps | analysis-tilemap-render-spaces.md:141-142 | — | rendering-2d-3d-spaces.md:31-32 | Appliquée par convention |

## 4. UI et éditeur (38 décisions distinctes, 8 sources)

Sources : `docs/editor/timeline-generic.md`, `docs/editor/animation2d_editor_casaengine.md`, `docs/editor/ui-screen-editor/architecture.md`, `ai-agent/tasks/casaengine-mgui-backend-extensibility-tasks.md`, `ai-agent/tasks/play-in-editor-tasks.md`, `ai-agent/audits/analysis-play-in-editor.md`, `docs/editor/play-in-editor.md`, `ai-agent/tasks/gltf-import-migration-tasks.md`.

| # | Décision | Source | Date | Doublon avec | Statut |
|---|---|---|---|---|---|
| 1 | Base timeline générique (`Track` / `Item` / `Duration` / `Kind`), validée uniquement par l'éditeur Animation2D | timeline-generic.md:12-13 | 2026-06-13 | — | Appliquée : TimelineModel, TimelineTrack, TimelineItem, ITimelineAdapter |
| 2 | Le modèle cutscene est un arbre d'actions joué par `CutsceneDirector`, pas de track par acteur ; éditeur cutscene = contrôle d'arbre | timeline-generic.md:14-23 | 2026-06-13 | — | Non vérifiable (éditeur cutscene non fait) |
| 3 | Modèle générique `public sealed` dans `CasaEngine.Editor.Controls.Timeline` | timeline-generic.md:24-25 | 2026-06-13 | — | Appliquée |
| 4 | Renommage `Lane`/`Event` → `Track`/`Item` propagé jusqu'à l'API Animation2D | timeline-generic.md:26-28 | 2026-06-13 | — | Appliquée |
| 5 | Approche phasée : cœur d'abord, abstractions ensuite | timeline-generic.md:29-30 | 2026-06-13 | — | Partielle |
| 6 | Conserver l'extension `.anim2d` et le contrat `Animation2dData` | animation2d_editor_casaengine.md:13 | — | — | Appliquée |
| 7 | Conserver le modèle time-based (`time_seconds`) | animation2d_editor_casaengine.md:14 | — | — | Appliquée |
| 8 | Cibler directement une animation 2D composée (`parts`, `tracks`, `events`) | animation2d_editor_casaengine.md:15 | — | — | Partielle : structures présentes, éditeur V1 mono-sprite (contradiction doc, `[dec-27]`, traitée en T5.3) |
| 9 | Timeline read-only graduée en secondes, scrollable, zoomable | animation2d_editor_casaengine.md:16 | — | — | Appliquée |
| 10 | Rester centré sur l'éditeur générique CasaEngine | animation2d_editor_casaengine.md:17 | — | — | Appliquée |
| 11 | Séparer strictement runtime et éditeur | animation2d_editor_casaengine.md:18 | — | — | Appliquée |
| 12 | Document model = source de vérité unique du screen editor | ui-screen-editor/architecture.md:202 | — | — | Appliquée : UIScreenDocument |
| 13 | XAML MGUI = format principal de persistance | ui-screen-editor/architecture.md:203 | — | — | Appliquée |
| 14 | Preview runtime reconstruite intégralement en v1 | ui-screen-editor/architecture.md:204 | — | — | Appliquée : UIScreenPreviewBuilder |
| 15 | Session d'édition centralise dirty state, sélection, document courant, preview | ui-screen-editor/architecture.md:205 | — | — | Appliquée : UIScreenEditorSession |
| 16 | MGUI reste la couche widgets / layout / input / clipping logique | casaengine-mgui-backend-extensibility-tasks.md:17 | — | — | Appliquée |
| 17 | Le backend CasaEngine reste un backend MonoGame concret | …:18 | — | — | Appliquée : CasaDesktopRuntime |
| 18 | Apos.Shapes derrière un contrat CasaEngine (`IShapeRenderer2D`), pas dans `MGUI.Shared` | …:19 | — | — | Appliquée |
| 19 | NvgSharp comme canvas vectoriel d'overlay éditeur derrière un contrat CasaEngine | …:20 | — | — | Partielle |
| 20 | Fallback clair vers le comportement actuel pour Apos.Shapes et NvgSharp | …:21 | — | — | Appliquée : DefaultShapeRenderer |
| 21 | Contrats publics MGUI modifiés seulement sur blocage prouvé, avec compatibilité | …:22 | — | — | Appliquée |
| 22 | Play : monde d'édition sérialisé en `JObject`, monde de play créé par `new World()` + `Load(JObject)`, sans `Clear()` du monde d'édition | play-in-editor-tasks.md:31-35 | — | analysis-play-in-editor.md:128 ; docs/editor/play-in-editor.md:19 | Appliquée : EditorWorldPlaySnapshot |
| 23 | `GameManager.RestoreWorld(World)` réinstalle le monde sans `LoadContent`/`BeginPlay` | play-in-editor-tasks.md:36-38 | — | — | Appliquée |
| 24 | Policy : Play = `EditorSimulation`, édition = `EditorPreview` | play-in-editor-tasks.md:39 | — | — | Appliquée |
| 25 | Caméra : première `CameraComponent` du monde joué, sinon `CreateDefaultCamera()` ; restauration au Stop | play-in-editor-tasks.md:40-43 | — | — | Appliquée |
| 26 | Scripts : types moteur en ALC par défaut, DLL gameplay en ALC collectible ; `ElementFactory` désenregistre l'assembly | play-in-editor-tasks.md:44-46 | — | analysis-play-in-editor.md:186 ; play-in-editor.md:46-47 | Appliquée : ScriptAssemblyHost |
| 27 | Échec de build des scripts = on reste en édition, erreurs loggées, pas de Play | play-in-editor-tasks.md:47 | — | — | Appliquée |
| 28 | Exception de script en Play = arrêt propre + erreur loggée (fail-stop) | play-in-editor-tasks.md:48-49 | — | — | Appliquée |
| 29 | AssimpNetter remplace AssimpNet | gltf-import-migration-tasks.md:15 | — | — | Appliquée |
| 30 | SharpGLTF.Core + SharpGLTF.Toolkit | …:16 | — | — | Appliquée |
| 31 | Skinning runtime réimplémenté via SharpGLTF, sortie `RiggedModel` préservée | …:17 | — | — | Appliquée |
| 32 | Assets convertis : `CasaEngine.Demos/Content/SkinnedMesh` seulement | …:18 | — | — | Appliquée |
| 33 | Sources non glTF supprimées après conversion | …:19 | — | — | Appliquée |
| 34 | Conversion automatique à l'import éditeur | …:20 | — | — | Appliquée : AssimpToGltfConverter |
| 35 | Format de sortie `.glb` | …:21 | — | — | Appliquée |
| 36 → 37 | Option B (adapter les importeurs côté éditeur) remplacée par l'option A exécutée (conversion vers `.glb`, lecteurs SharpGLTF partagés, anciens importeurs supprimés) | …:22 puis :26-47 | — | — | Appliquée (36 remplacée) |
| 38 | Métadonnées d'effet `.X` legacy abandonnées à l'import | …:23 | — | — | Appliquée |
| 39 | Tests typés Assimp supprimés ou réécrits | …:24 | — | — | Appliquée |

## 5. Gameplay (63 décisions distinctes, 9 sources)

Sources : `docs/engine/cutscene_commandes_sequentielles_async_coroutine.md`, `docs/engine/coroutines_specifications.md`, `docs/engine/yarn_spinner_integration.md`, `docs/engine/navigation-engine-features.md`, `docs/engine/gameplay-mode.md`, `docs/engine/character-controller-features.md`, `docs/engine/animation-motion-matching.md`, `docs/engine/animation-deformer-support-policy.md`, `ai-agent/audits/analysis-possession-gameplay-framework.md`.

| # | Décision | Source | Date | Doublon avec | Statut |
|---|---|---|---|---|---|
| 1-10 | Cutscene V1 : utiliser `World.CoroutineManager` ; pas de `CutsceneRunner` ; `CutsceneDirector` dans `World`, façade sans `Update` séparé ; V1 limitée à `Wait`, `Sequence`, `Parallel`, `Stop`, debug, validation, asset, éditeur lecture seule ; commandes gameplay exclues de la V1 ; ne pas inventer `InputManager`, `DialogueSystem`, … ; `CutsceneAsset` chargé par le système d'assets ; actions typées ; pas de `CompleteImmediately` | cutscene_…coroutine.md:44-53 | — | coroutines_specifications.md:1252 (CoroutineManager par World) | Appliquées, sauf 5-6 : `MoveTo` (commande gameplay) livrée en V1 (à vérifier à la rédaction) |
| 11-32 | Coroutines V1 : système attaché au `World`, un `CoroutineManager` par `World`, coroutines détruites avec leur `World` ; `FrameTime` (DeltaTime, UnscaledDeltaTime, TotalTime, UnscaledTotalTime, TimeScale, FrameIndex) ; `WaitForSeconds` sur DeltaTime, `WaitForSecondsRealtime` sur UnscaledDeltaTime ; pause = `TimeScale = 0` ; `WaitForFrames` ; owner (entité, composant) et arrêt sur `Destroy()`/`Detach()`, pas sur `Enabled = false` ; `CoroutineHandle` (ManagerId, Slot, Generation), handle obsolète jamais réutilisé ; `yield return CoroutineHandle` (même manager seulement) ; exception loggée, coroutine seule stoppée ; une seule phase `Update` en V1, `LateUpdate`/`FixedUpdate`/`EndOfFrame` réservés V2 | coroutines_specifications.md:1250-1347 | — | — | Appliquées : CoroutineManager.cs, FrameTime, CoroutineHandle |
| 33-37 | Dialogue Yarn : `DialogueService` attaché au World ou au contexte de jeu ; bloquer l'input gameplay sans figer le World ; compilation Yarn à l'import (éditeur), runtime pour prototype seulement ; UI CasaEngine existante en V1 ; `DialogueRunner` indépendant d'abord, action de cutscene ensuite | yarn_spinner_integration.md:1237-1315 (Décisions 1 → 5) | — | :469-471 (option A/B) | Appliquées : DialogueService, YarnDialogueCompiler, YarnDialogueRunner |
| 38-42 | Navigation V1 : couche TileMap `navigation.role=grid` ; `GridPathfinder2D` dédié (pas `PathPlanner<T>`) ; intégration via `CharacterControllerNavigationDriverComponent` ; tests unitaires requis ; debug draw via `Renderer2DComponent` / `Line3dRendererComponent` | navigation-engine-features.md:126-216 | — | — | Appliquées |
| 43-46 | GameplayMode V1 : `GameplayResult`, `GameplayPhase`, `GameplayState`, `GameplayContext`, `GameplayMode`, `GameplayModeRunner` ; runner dans `World.Update()` à la place de `GameMode.Tick()` ; `AssetLoader<T>` impose `ISerializable, new()` ; décisions à ne pas prendre en V1 (pas de Scene, pas de services globaux sans contrat, pas d'objectifs complets avant runner, pas de dépendance UI/rendu) | gameplay-mode.md:53-125, 251-291, 427-435 | — | — | Appliquées |
| 47-49 | Character controller V1 : contrôleur cinématique gameplay, pas piloté par forces ; base `EntityComponent`, pas `PhysicsBaseComponent` | character-controller-features.md:86-112 | — | — | Appliquées : CharacterControllerComponent |
| 50 | Motion matching : gardé sur la roadmap comme flux R&D séparé | animation-motion-matching.md:118 | — | — | Non vérifiable (R&D) |
| 51-53 | Déformeurs : supportés = skinning (linear blend, dual quaternion) et morph targets ; non supportés = lattice, wire, muscle, cloth, physics-driven ; ordre = morph CPU en espace local, puis skinning GPU | animation-deformer-support-policy.md:5-35 | — | — | Appliquées : MorphTarget, SkinnedMeshAnimationRuntime |
| 54-63 | Possession : `PlayerController` = session de joueur local ; possession = lien `PlayerController ↔ Entity` (`Possess`/`UnPossess`), pilotage de `CharacterControllerComponent.SetControlMode` ; pas de classe `Pawn` ; façade d'input par joueur (`PlayerInput`) ; `PlayerController.IsInputEnable` source unique ; suppression de `Pawn.InputEnabled` ; nettoyage `AIController` (partiel) ; multi-joueur local (partiel : join/leave restants) | analysis-possession-gameplay-framework.md:119-126 | 2026-08-19 | docs/engine/player-input.md:82 ; docs/engine/gameplay-possession.md:65 | Appliquées (commits cités dans l'audit), 62-63 partielles |

## 6. Architecture et organisation (35 lignes, 4 sources)

Sources : `ai-agent/audits/CasaEngine_layering_project_split_evaluation.md`, `ai-agent/audits/CasaEngine_folder_hierarchy_namespace_compatibility.md`, `ai-agent/audits/structure-analyze-tasks.md`, `ai-agent/audits/CasaEngine_folder_hierarchy_audit_report.md`.

| # | Décision | Source | Date | Doublon avec | Statut |
|---|---|---|---|---|---|
| 1 | Découpage en assemblies : « Oui à terme, mais pas immédiatement dans la même phase que la réorganisation physique des dossiers » | CasaEngine_layering_project_split_evaluation.md:15 | — | :57 | Appliquée : un seul CasaEngine.csproj |
| — | Règle de couches Core ← Engine ← Framework | structure-analyze-tasks.md:39 ; CasaEngine_folder_hierarchy_audit_report.md:298, 504 (constat `[dec-20]` de l'audit des fichiers IA) | — | — | À relire à la rédaction (le relevé ne l'a pas isolée) |
| 2-24 | Mappings finaux de namespaces et de dossiers : `Core.Log` → `Core.Logging`, `Core.Maths` → `Core.Math`, `Core.Parser` → `Core.Parsing`, `Core.MultiThreading` → `Core.Threading`, éclatement de `Core.Helpers` ; `Engine.Input.InputDeviceStateProviders` → `Providers`, `InputSequence` → `Sequences`, `EngineEnvironment` → `Engine.Environment`, `Primitives2D`/`3D` → `Primitives.TwoD`/`ThreeD` ; `Framework.Game` → `Application`, `GameFramework` → `Gameplay`, `Debugger` → `Debug`, `GUI` → `UI`, `Graphics` → `Rendering.Models`, `Graphics2D` → `Rendering.Draw2D`, `World`/`Transform`/`Entities`/`SpacePartitioning.Octree` → `Scene.*`, `ObjectBase` → `Framework.Common`, `Constants` → `Framework.Configuration`, `Materials` subdivisé (Runtime, Definitions, Authoring, Compilation, Serialization) | CasaEngine_folder_hierarchy_namespace_compatibility.md:13-49 | — | structure-analyze-tasks.md:191, 246 | Appliquées (dossiers présents), sauf `Parsing` et `Threading` non trouvés (ambigu) |
| 25-35 | Nettoyages exécutés (suppression `Core/Shapes/`, sérialisation de `Coordinates` externalisée, champs BulletSharp retirés de `PhysicsDefinition`, `InputManager.Test.cs` supprimé, `NumericFormatExtensions` déplacé, import mort retiré, `OctreeVisualizer` déplacé, `IGameplayProxy.Clone()` retourne l'interface, chemin de police relatif, index de `AssetCatalog`, cache de `ElementFactory`) | structure-analyze-tasks.md:72-635 | — | — | Appliquées ; **tâches de refactor, pas des décisions d'architecture** (exclues du rétro-remplissage, sauf `IGameplayProxy.Clone()` qui casse un cycle de dépendance, rattaché à la règle de couches) |

## Regroupement retenu pour le rétro-remplissage

Un fichier ADR par thématique cohérente, décisions listées en puces dans `Decision`, source citée par puce. Exclus : les recommandations non tranchées (rendu 18-21), les corrections et tâches de refactor (physique 4, architecture 25-35 hors `Clone()`), les décisions de processus propres à un chantier (audio D13, couverte par l'ADR des règles de travail), et les points marqués « ouverts » dans les sources.

| Tâche | ADR prévus | Décisions couvertes |
|---|---|---|
| T4.4 audio | Audio runtime (bus, streaming, 2D, device et voix, backend, consommateurs) ; Audio asset et éditeur (`.sound`, métadonnées, WAV, inspecteur, suppressions, bus Editor) | D1, D2/D2-bis, D5, D7, D10, D11 ; D3, D4, D6, D8, D9, D12 |
| T4.5 collision, physique | Une seule simulation 3D et espace comme politique ; couches Shape/Fixture/Body/World ; canaux, réponses et profils ; volumes et champs ; fixtures animées par la timeline ; posture de compatibilité ; backend bepuphysics2 | 2/D1, D4, D4.a, 10, 11, 16 ; D2, D2.a-c, 12 ; D3, D3.a, 15, 18, 19 ; D5, 17 ; D6 ; 1, 14 ; 3, 5, 6, 7, 9, 13 |
| T4.6 rendu | Convention de nommage des shaders ; espaces de rendu des tilemaps ; sources de vérité materials ; décisions PBR (acceptées, non implémentées) | 1-4 ; 5-16, 30 ; 17 ; 22-29 |
| T4.7 UI, éditeur | Timeline générique ; éditeur Animation2D V1 ; UI screen editor V1 ; backend MGUI extensible ; play-in-editor ; import glTF | 1-5 ; 6-11 ; 12-15 ; 16-21 ; 22-28 ; 29-39 |
| T4.8 gameplay | Cutscenes V1 ; coroutines V1 ; dialogue Yarn ; navigation V1 ; GameplayMode V1 ; character controller V1 ; déformeurs et motion matching ; possession et input joueur | 1-10 ; 11-32 ; 33-37 ; 38-42 ; 43-46 ; 47-49 ; 50-53 ; 54-63 |
| T4.9 architecture | Couches et découpage en assemblies différé ; hiérarchie de dossiers et namespaces ; règles de ce chantier (source unique et outillage des agents ; règles de travail des agents ; langue et ADR) | 1, règle de couches, `Clone()` ; 2-24 ; D1 → D13 et P4 du plan ai-guidelines |

Estimation : 32 fichiers ADR pour environ 190 décisions individuelles.
