# Particle System V1 And V2 Migration

## Vue d'ensemble

Le systeme de particules V1 fournit une base CPU robuste pour les effets billboard dans le runtime et dans l'editeur MonoGame.

Sources principales:
- `ParticleEffectAsset`
- `ParticleEmitterDefinition`
- `ParticleSystemComponent`
- `ParticleRuntimeInstance`
- `ParticleRendererComponent`

Fichiers d'asset:
- extension `.particle`
- JSON versionne par `ParticleEffectAsset.CurrentVersion`
- resolution via `AssetCatalog`, `ParticleEffectAssetLoader` et `AssetContentManager`

## Support V1 actuel

Authoring:
- plusieurs emitters par effect asset
- `Duration`, `Looping`, `StartDelay`, `MaxParticles`
- emission continue par `RateOverTime`
- bursts avec `Time`, `CountMin`, `CountMax`
- formes `Point`, `Circle`, `Sphere`, `Box`, `Cone`
- taille, vitesse, rotation, angular velocity, lifetime et couleur initiale
- simulation locale ou world
- gravity, gravity scale et drag
- courbes lineaires de taille, alpha et velocity over lifetime
- gradient de couleur over lifetime

Rendering:
- billboards camera-facing
- texture optionnelle via asset id
- fallback texture si la texture est absente
- blend modes `Alpha`, `Additive`, `Multiply`
- sort mode `Distance`
- depth test, depth write, render queue et layer
- flipbook texture sheet V1.5: grid columns/rows, frame count, random start frame, FPS ou frame-over-lifetime curve
- stats render: draw calls, texture binds, state changes, particle count et CPU render particules

Runtime:
- simulation CPU avec pool fixe par emitter
- aucun resize automatique pendant Update/Draw
- free list et alive list reutilisees
- bounds runtime par emitter et par instance
- compteurs de profiling: capacity, alive, dead, emitted, killed, max alive reached, max reached, simulation CPU
- extraction de render packets avec temps CPU d'extraction par `ParticleSystemComponent`

Editeur:
- ouverture des assets `.particle` depuis le Content Browser
- inspector MGUI pour les modules V1 fixes
- preview avec lecture, pause, stop, restart, loop et speed
- courbes et gradients editables avec presets
- creation de presets d'effets fire, smoke et spark
- thumbnails Content Browser stables pour `.particle`, generes par un vrai rendu de scene preview et non par une icone inventee
- drag/drop sur entites pour creer ou configurer un `ParticleSystemComponent`
- hot reload d'assets particules
- undo/redo des edits inspector
- gizmos d'emission et bounds dans viewport/preview
- diagnostics automation pour smoke tests

## Limites V1

Hors scope volontaire:
- collisions particules contre plans, AABB, spheres ou monde physique
- sub emitters et events on birth/death/collision
- trails, ribbons et stretched billboards avances
- soft particles, distortion et refraction
- particules lit, ombres, normal maps ou material graph par particule
- mesh particles et decal particles
- simulation GPU ou instancing GPU avance
- vector fields, turbulence/curl noise, attractors et repulsors
- LOD automatique complet
- blackboard de parametres expose facon Niagara
- modules custom publics chargeables par jeu ou plugin

Limites techniques assumees:
- les modules V1 sont des proprietes fixes, pas une stack extensible
- les courbes V1 sont lineaires et normalisees
- le renderer actuel cible les billboards et `BasicEffect`
- le batching regroupe par texture/blend/depth, pas par material custom
- le flipbook choisit un seul frame par particule et ne gere pas encore les events de frames
- les compteurs de profiling sont des snapshots runtime simples, pas un profiler historique complet
- le preview editor utilise un world isole et ne garantit pas la parite exacte avec tous les worlds de jeu

## Direction V2 modulaire

La V2 doit transformer les modules fixes V1 en pipeline interne plus explicite, sans casser l'UX V1 ni les assets existants.

Objectif de decoupage:
- Spawn: decide combien de particules naissent et quand
- Shape: produit position et direction de spawn
- Initialize: ecrit les valeurs initiales de lifetime, speed, size, color, rotation, custom data
- Update: modifie velocity, position, size, color, alpha, frame, forces et events pendant la vie
- Render: convertit l'etat particule en packets, materiaux ou draw data

Mapping V1 vers V2:
- `ParticleEmissionModule` -> `RateOverTimeSpawnModule` et `BurstSpawnModule`
- `ParticleShapeModule` -> `ShapeSpawnModule`
- `ParticleInitialModule` -> modules initialize lifetime, velocity, size, rotation et color
- `ParticleSimulationModule.Gravity` / `Drag` -> modules update force et drag
- `SizeOverLifetime`, `AlphaOverLifetime`, `VelocityOverLifetime`, `ColorOverLifetime` -> modules update curve/gradient
- `ParticleRendererModule` -> `BillboardRenderModule`
- `ParticleFlipbookModule` -> module render/update de frame sheet

## Regles de migration

Compatibilite:
- les assets V1 doivent continuer a loader sans champ `modules`
- les nouveaux champs restent optionnels avec defaults stables
- un bump de version doit conserver une migration deterministic et testee
- l'editeur ne doit pas reecrire massivement les assets si aucune edition utilisateur n'a lieu

Implementation runtime:
- commencer par des interfaces internes, pas une API publique de plugin
- garder les modules V1 comme facade authoring tant que l'UX n'est pas prete
- compiler les modules fixes V1 en une sequence runtime preallouee au rebuild de l'instance
- ne pas allouer dans Update/Draw: pas de LINQ, pas de closures, pas de List/Dictionary creee par frame
- conserver les pools de particules et les buffers de render packets existants

Implementation editeur:
- presenter d'abord les modules V1 comme sections stables de l'inspector actuel
- ajouter la stack de modules seulement quand l'ordre, l'undo/redo et la serialization sont clairs
- tout nouveau module visible doit avoir un preset ou un sample minimal
- les diagnostics automation doivent continuer a couvrir preview, hot reload, undo/redo et smoke render

Validation attendue:
- tests de round-trip JSON V1 -> courant
- tests d'equivalence entre modules fixes V1 et adapters V2
- tests de simulation sans allocations evitables dans les hot paths critiques
- build solution editor
- smoke preview sur un asset particule existant

## Plan de transition recommande

1. Ajouter des interfaces internes de module runtime et des adapters V1 sans changer le JSON public.
2. Compiler `ParticleEmitterDefinition` en pipeline runtime prealloue pendant `ParticleRuntimeInstance` rebuild.
3. Ajouter des tests d'equivalence module par module avec les comportements V1 actuels.
4. Introduire un champ JSON optionnel `modules` dans une version future, tout en conservant les champs fixes.
5. Faire lire l'editeur depuis les champs fixes ou `modules`, mais n'ecrire `modules` que pour les nouveaux assets opt-in.
6. Deplacer progressivement l'UX inspector vers une stack de modules, avec undo/redo et samples.
7. Deprecier les champs fixes seulement apres migration automatique, docs et validation des samples.