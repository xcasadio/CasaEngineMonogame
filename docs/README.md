# Documentation CasaEngine

Ce dossier contient uniquement la documentation du moteur (`engine/`) et de l'éditeur (`editor/`).
Les audits, analyses et listes de tâches vivent dans [`ai-agent/`](../ai-agent/README.md).

## Décisions d'architecture (`decisions/`)

- [decisions/README.md](decisions/README.md) — index des Architecture Decision Records (un fichier par décision, en anglais, modèle [decisions/template.md](decisions/template.md)).

## Moteur (`engine/`)

### Animation

- [animation-blend-demo.md](engine/animation-blend-demo.md) — démo de blend d'animations squelettiques.
- [animation-ik-demo.md](engine/animation-ik-demo.md) — démo d'IK.
- [animation-clip-loop-period.md](engine/animation-clip-loop-period.md) — `AnimationClip.LoopPeriodSeconds` : période de boucle distincte de la durée (dernière clé distincte de la première).
- [animation-foot-lock.md](engine/animation-foot-lock.md) — foot-lock (épinglage des pieds via IK deux-os) : usage `AttachFootLock`, réglages, limites.
- [animation-motion-matching.md](engine/animation-motion-matching.md) — périmètre du motion matching dans CasaEngine.
- [animation-deformer-support-policy.md](engine/animation-deformer-support-policy.md) — politique de support des déformeurs.
- [animation2d-composed-format-v1.md](engine/animation2d-composed-format-v1.md) — format composé `.anim2d` V1 (parts, tracks, events, collision keyframes), règles de chargement et échantillonnage runtime, décrits depuis le code.

### Rendu, materials et shaders

- [materials-workflow.md](engine/materials-workflow.md) — workflow cible des materials (authoring → runtime).
- [materials-sources-of-truth.md](engine/materials-sources-of-truth.md) — sources de vérité materials/shaders.
- [material-hot-reload-flow.md](engine/material-hot-reload-flow.md) — flux de hot reload des materials.
- [shader-naming-convention.md](engine/shader-naming-convention.md) — convention de nommage des shaders.
- [effect-file-inventory.md](engine/effect-file-inventory.md) — inventaire des fichiers `.fx` / `.fxh` et leurs consommateurs.
- [light-component.md](engine/light-component.md) — `LightComponent`.
- [environment-system-v1.md](engine/environment-system-v1.md) — système d'environnement V1.
- [render-stats-demo-workflow.md](engine/render-stats-demo-workflow.md) — workflow de la démo de render stats.
- [rendering-2d-3d-spaces.md](engine/rendering-2d-3d-spaces.md) — espaces de rendu 2D/3D (caméra ortho, tilemap 3D, render-to-texture, pixel-perfect).

### Systèmes 2D

- [tilemaps-gestion-profondeur.md](engine/tilemaps-gestion-profondeur.md) — gestion moderne de la profondeur des TileMaps.
- [particle-system-features.md](engine/particle-system-features.md) — spécification fonctionnelle du système de particules.
- [particle-system-v1-v2-migration.md](engine/particle-system-v1-v2-migration.md) — migration particules V1 → V2.

### Physique et collision

- [collision-2d-3d-architecture.md](engine/collision-2d-3d-architecture.md) — architecture cible de la collision 2D / 2.5D / 3D : une seule simulation 3D, espace de simulation comme politique du monde, couches Shape/Fixture/Body/World, canaux et profils, volumes et champs, backend bepuphysics2 (décisions D1 → D6, ADR-0003 → ADR-0009).

### Gameplay et scripting

- [gameplay-mode.md](engine/gameplay-mode.md) — concept `GameplayMode` (règles de gameplay par `World`).
- [character-controller-features.md](engine/character-controller-features.md) — spécification du Character Controller.
- [navigation-engine-features.md](engine/navigation-engine-features.md) — navigation des personnages.
- [coroutines_specifications.md](engine/coroutines_specifications.md) — spécification du système de coroutines.
- [cutscene_commandes_sequentielles_async_coroutine.md](engine/cutscene_commandes_sequentielles_async_coroutine.md) — séquences scriptées avec commandes séquentielles async/coroutine.
- [yarn_spinner_integration.md](engine/yarn_spinner_integration.md) — intégration de Yarn Spinner (dialogues).
- [dialogue-choices-and-bitmap-fonts.md](engine/dialogue-choices-and-bitmap-fonts.md) — choix de dialogue et police bitmap dans le pipeline de dialogue existant (`DialogueService`, `IDialoguePresenter`, `DialogueScreen`).
- [world-message-bus-migration-notes.md](engine/world-message-bus-migration-notes.md) — bus de messages scopé au `World` (pattern recommandé).
- [player-input.md](engine/player-input.md) — façade `PlayerInput` par joueur (gates input enable / routage par vue / capture UI).
- [gameplay-possession.md](engine/gameplay-possession.md) — possession `Controller`/`Entity` (`Possess`/`UnPossess`, pilotage de `CharacterControlMode`) et multi-joueur local.

### Audio

- [audio-system.md](engine/audio-system.md) — système audio V1 : bus de mixage, asset `.sound`, SFX one-shot/loop, streaming des musiques (fade, crossfade), `SoundEmitterComponent`, actions de cutscene, règles play-in-editor et limites connues.

### Rendu 2D

- [screen-effects.md](engine/screen-effects.md) — fondu/teinte plein écran V1 : `ScreenEffectService` (rampe sans MonoGame), `ScreenEffectComponent` (cran `RenderPass2D.ScreenEffects`, formule de placement caméra-annulée), `SpriteBlendMode.Additive`/`.Subtractive`, action de cutscene `FadeScreen`.
- [scrolling-layers.md](engine/scrolling-layers.md) — mécanisme des couches défilantes V1 : `ScrollingLayerService` (parallaxe/auto-défilement/cadence par tick entier, sans type GPU), `ScrollingLayerComponent` (résolution des textures, ciseaux en paramètre, soumission des quads couvrants), politique Z = 0.

### UI runtime (MGUI)

- [casaengine-mgui-backend.md](engine/casaengine-mgui-backend.md) — backend MGUI CasaEngine.
- [casaengine-mgui-backend-extensibility.md](engine/casaengine-mgui-backend-extensibility.md) — backend MonoGame extensible (Apos.Shapes, NvgSharp).

## Éditeur (`editor/`)

- [editor-workspace-layouts.md](editor/editor-workspace-layouts.md) — shell MGUI, panneaux dockables et layout par défaut.
- [editor-history.md](editor/editor-history.md) — undo/redo global (un historique par contexte d'édition).
- [editor-input-routing-architecture.md](editor/editor-input-routing-architecture.md) — architecture du routage des inputs (vues, viewports, MGUI).
- [editor-2d-viewport.md](editor/editor-2d-viewport.md) — mode 2D du viewport monde (bascule, navigation, grille, gizmo XY, persistance).
- [play-in-editor.md](editor/play-in-editor.md) — mode Play (tester le niveau dans le viewport, scripts rechargeables à la volée).
- [gameplay-csproj-scaffolding.md](editor/gameplay-csproj-scaffolding.md) — conception du scaffolding du projet C# gameplay à `CreateProject` (csproj/sln générés, Phase 1 DLL éditeur → Phase 2 NuGet).
- [timeline_control_architecture.md](editor/timeline_control_architecture.md) — architecture du contrôle Timeline V1.
- [timeline-generic.md](editor/timeline-generic.md) — base générique réutilisable pour les timelines.
- [animation2d_editor_casaengine.md](editor/animation2d_editor_casaengine.md) — éditeur d'animations 2D (direction produit et contraintes).
- [graph_node_architecture_recommendation.md](editor/graph_node_architecture_recommendation.md) — intégration des graphes à nœuds (MGUI `MGGraphView`).

### UI Screen Editor (`editor/ui-screen-editor/`)

- [README.md](editor/ui-screen-editor/README.md) — objectif et périmètre du screen editor.
- [architecture.md](editor/ui-screen-editor/architecture.md) — architecture (document model, preview, XAML).
- [architecture-entry-points.md](editor/ui-screen-editor/architecture-entry-points.md) — points d'entrée dans le code.
- [screen-authoring-conventions.md](editor/ui-screen-editor/screen-authoring-conventions.md) — conventions d'authoring des screens.
- [xaml-support-matrix.md](editor/ui-screen-editor/xaml-support-matrix.md) — matrice de support XAML.

## Voir aussi

- `MGUI/docs/` — documentation propre au framework MGUI (graph view, plans MGUI).
- [ai-agent/README.md](../ai-agent/README.md) — audits, analyses et suivi des tâches restantes.
