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
- [component-persistence.md](engine/component-persistence.md) — chargement et sauvegarde des composants, propriétés `[SerializedProperty]` des composants de jeu (ADR-0118).
- [environment-system-v1.md](engine/environment-system-v1.md) — système d'environnement V1.
- [lit-ambient-and-ambient-source.md](engine/lit-ambient-and-ambient-source.md) — terme ambiant des matériaux éclairés (couleur de surface appliquée une fois) et source d'ambiant `AmbientSource` (`Auto` / `Flat`) d'un monde.
- [lit-detail-texture.md](engine/lit-detail-texture.md) — texture de détail de `LitDiffuseMaterial` (`DetailTexture`, `DetailTiling`, formule `base * 2 * détail`, limites).
- [shadow-settings.md](engine/shadow-settings.md) — réglages d'ombre directionnelle du monde (`ShadowSettings` : mode `OccludeDirectLight` / `MultiplyLitColor` et teinte, rayon de filtre, fondu, profondeur de carte, biais de profondeur, limites).
- [texture-mipmaps.md](engine/texture-mipmaps.md) — chaîne de mipmaps du chargeur de textures (`Texture2DLoader.GenerateMipmaps`, `MipmapChain`, rafraîchissement d'une texture peinte à l'exécution).
- [color-pipeline.md](engine/color-pipeline.md) — pipeline couleur `Legacy` / `LinearHdr` (clé `color_pipeline`, ordre de l'image, décodage shader ou CPU, tonemapping, limites connues ; ADR-0104 à ADR-0108, bloom compris ; FXAA et SSAO, ADR-0113, ADR-0114 et ADR-0116).
- [pbr-rendering.md](engine/pbr-rendering.md) — rendu PBR metallic-roughness (matériau `lit-pbr`, IBL et sondes de réflexion, bloom, transparent, skinné, import glTF, coût, limites ; ADR-0103 à ADR-0116, clear coat, FXAA et SSAO compris).
- [render-stats-demo-workflow.md](engine/render-stats-demo-workflow.md) — workflow de la démo de render stats.
- [rendering-2d-3d-spaces.md](engine/rendering-2d-3d-spaces.md) — espaces de rendu 2D/3D (caméra ortho, tilemap 3D, render-to-texture, pixel-perfect).

### Systèmes 2D

- [tilemaps-gestion-profondeur.md](engine/tilemaps-gestion-profondeur.md) — gestion moderne de la profondeur des TileMaps.
- [particle-system-features.md](engine/particle-system-features.md) — spécification fonctionnelle du système de particules.
- [particle-system-v1-v2-migration.md](engine/particle-system-v1-v2-migration.md) — migration particules V1 → V2.
- [video.md](engine/video.md) — lecture de vidéos MDEC ([ADR-0128](decisions/0128-the-engine-plays-mdec-videos-through-a-video-element.md)) : spécification du flux MDEC que le décodeur suit, conteneur `.mdecvideo`, lecteur, balise `<Video>`, saut, action `PlayVideo`, limites et mesure de décodage, composant vidéo du monde `VideoSpriteComponent` et action `PlayVideoSprite` ([ADR-0130](decisions/0130-a-world-video-component-and-an-any-key-input.md)), action `StartVideoSprite`, dont le film va à sa fin après la cutscene ([ADR-0132](decisions/0132-a-world-video-can-run-to-its-end-after-its-cutscene.md)) (en anglais). Le côté écrans de données est dans [data-driven-screens.md](engine/data-driven-screens.md#video-element-and-playvideo).

### Physique et collision

- [collision-2d-3d-architecture.md](engine/collision-2d-3d-architecture.md) — architecture cible de la collision 2D / 2.5D / 3D : une seule simulation 3D, espace de simulation comme politique du monde, couches Shape/Fixture/Body/World, canaux et profils, volumes et champs, backend bepuphysics2 (décisions D1 → D6, ADR-0003 → ADR-0009).

### Gameplay et scripting

- [gameplay-mode.md](engine/gameplay-mode.md) — concept `GameplayMode` (règles de gameplay par `World`).
- [character-controller-features.md](engine/character-controller-features.md) — spécification du Character Controller.
- [navigation-engine-features.md](engine/navigation-engine-features.md) — navigation des personnages.
- [coroutines_specifications.md](engine/coroutines_specifications.md) — spécification du système de coroutines.
- [cutscene_commandes_sequentielles_async_coroutine.md](engine/cutscene_commandes_sequentielles_async_coroutine.md) — séquences scriptées avec commandes séquentielles async/coroutine.
- [yarn_spinner_integration.md](engine/yarn_spinner_integration.md) — intégration de Yarn Spinner (dialogues).
- [dialogue-choices-and-bitmap-fonts.md](engine/dialogue-choices-and-bitmap-fonts.md) — choix de dialogue et police bitmap dans le pipeline de dialogue existant (`DialogueService`, `IDialoguePresenter`, `DialogueScreen`) ; remplacement du balisage de la boîte par un asset du projet (`DialogueScreenAsset`) et son contrat d'éléments.
- [world-message-bus-migration-notes.md](engine/world-message-bus-migration-notes.md) — bus de messages scopé au `World` (pattern recommandé).
- [player-input.md](engine/player-input.md) — façade `PlayerInput` par joueur (gates input enable / routage par vue / capture UI).
- [gameplay-possession.md](engine/gameplay-possession.md) — possession `Controller`/`Entity` (`Possess`/`UnPossess`, pilotage de `CharacterControlMode`) et multi-joueur local.

### Audio

- [audio-system.md](engine/audio-system.md) — système audio V1 : bus de mixage (et, sous le backend logiciel, graphe de bus avec effets, départs, ducking, limiteur, fondus à l'échantillon et snapshots, ADR-0059), asset `.sound` (variations aléatoires et priorité de vol de voix, ADR-0063), écouteur, sons 2D et 3D, Doppler et paramètres de jeu (ADR-0064), asset de mixeur `.audioMixer` du projet (ADR-0067), SFX one-shot/loop, streaming des musiques (fade, crossfade), `SoundEmitterComponent`, actions de cutscene, règles play-in-editor et limites connues.
- [psx-spu.md](engine/psx-spu.md) — SPU PlayStation logiciel (ADR-0058, ADR-0062) : registres, ADPCM, ADSR, bruit, PMON, réverbération, tables par défaut (`CreateDefault`) ou fournies par l'appelant, hébergement par le backend logiciel (`AudioService.TryCreatePsxSpu`, `PsxSpuPort`), bornes et ordre des écritures, points de précision ouverts.

### Rendu 2D

- [screen-effects.md](engine/screen-effects.md) — fondu/teinte plein écran V1 : `ScreenEffectService` (rampe sans MonoGame), `ScreenEffectComponent` (cran `RenderPass2D.ScreenEffects`, formule de placement caméra-annulée), `SpriteBlendMode.Additive`/`.Subtractive`, action de cutscene `FadeScreen`, action d'écran `fade_screen`, pas exact par image.
- [save-games.md](engine/save-games.md) — service de sauvegarde de partie au runtime (ADR-0044) : `SaveGameService` (`GameSettings.SaveGames`), objet `ISaveGameData` sérialisé par une archive symétrique, formats JSON et binaire, résultats, contrat de validation par le jeu.
- [scrolling-layers.md](engine/scrolling-layers.md) — mécanisme des couches défilantes V1 : `ScrollingLayerService` (parallaxe/auto-défilement/cadence par tick entier, sans type GPU), `ScrollingLayerComponent` (résolution des textures, ciseaux en paramètre, soumission des quads couvrants), politique Z = 0.
- [cellular-layers.md](engine/cellular-layers.md) — mécanisme des couches à cellules V1 (le second mode de fond d'Alundra) : `CellularLayerService` (dérive/période/parallaxe par cellule, `WaveX` sans état, cadence V-animation et `WaveTick`, sans type GPU), `CellularLayerComponent` (résolution des planches par `PalDex`, soumission cellule par cellule), mécanisme parallèle à `scrolling-layers.md` et non une extension.
- [camera-behaviour-components.md](engine/camera-behaviour-components.md) — composants de comportement de caméra (ADR-0145) : `OrbitCameraComponent` (cible, distance, lacet, tangage ; pose une caméra simple, piloté par la navigation de vue et `ScriptArcBallCamera`) et `Follow2dCameraComponent` (suivi 2D avec zone morte et limites).
- [perspective-camera-component.md](engine/perspective-camera-component.md) — `PerspectiveCameraComponent` : caméra perspective posée par la transformation de son composant, roulis compris (ADR-0144) ; angle vertical `fieldOfView`, décalage de l'image `picture_offset` en fraction du viewport (projection décentrée) ; sample `Projects/PerspectiveCameraSample`.
- [static-sprite-component.md](engine/static-sprite-component.md) — `StaticSpriteComponent` : champs JSON `color`, `blend_mode` et `is_visible` (ADR-0129), résolution du sprite nommé à `InitializeWithWorld`, ordre de dessin d'un sprite mélangé avec ou sans `DepthSortable2DComponent`, lien vers le film du monde (ADR-0130), convention 2D mesurée, déplacement et teinte dans le temps.
- [sprite-psx-semi-transparency.md](engine/sprite-psx-semi-transparency.md) — semi-transparence PSX par texel d'un sprite : champ `SpriteData.PsxSemiTransparency` (`psx_semi_transparency`), deux dessins disjoints par fenêtres d'alpha brut (`AlphaWindow`), états de mélange des modes 0 à 3, capacité de la file au-delà de 10 000 entrées, démos qui lisent leur back-buffer.
- [effect-quads.md](engine/effect-quads.md) — quads d'effets du jeu (ADR-0071) : `EffectQuad` (planche, fenêtre brute, quatre coins du monde, mode PSX, clé de tri), `EffectQuadService` (sans GPU, planches en `Guid`, image poussée qui persiste jusqu'au prochain `SetFrame` ou `Clear`), `EffectQuadComponent` (résolution des planches par handles comptés, couture `ResolveTextures` qui enregistre la version, soumission par `DrawPsxQuad`, ne trie pas), démo `Effect quads` comparée à une prévision exacte.
- [frozen-frame.md](engine/frozen-frame.md) — image figée (ADR-0086) : `FrozenFrameService` (sans GPU : option `RetainLastFrame` éteinte par défaut, `RequestCapture`/`ShowBase`/`Release`, `LastCapture`), `FrozenFrameComponent` (superposition d'ordre -100 sous le voile), contrat « image du tour précédent, interface et voiles compris », devoir de libérer, limites, démo `Frozen frame demo`.

### Ressources

- [asset-handles-and-bitmap-fonts.md](engine/asset-handles-and-bitmap-fonts.md) — seule API des ressources : handles comptés sur une instance unique par ressource (`AssetContentManager.Acquire<T>`), copies pour les modèles (`LoadCopy<T>`), objets fabriqués (`Register`, `Replace`), libération différée au début de chaque changement de monde (`CollectUnreferenced`), détachement de tout l'arbre d'une entité jetée ; polices bitmap `.fnt` comme ressources et registre de polices du jeu (`CasaEngineGame.UIFonts`) résolu par nom de famille dans le XAML (ADR-0036, ADR-0037).

### Démos

- [demos-main-menu.md](engine/demos-main-menu.md) — écran principal de `CasaEngine.Demos` dans un monde « menu » (liste des démos par thème et fiche, thème Dark de MGUI), un monde neuf par démo, retour au menu par Échap ou Select, fiche et panneau d'aide de chaque démo, parcours par thèmes, entrées clavier et manette par la configuration `demos-input.buttonsMapping`, ajout d'une démo, automatisation (`CASAENGINE_START_DEMO`, `CASAENGINE_DEMO_CYCLE`), limites (ADR-0072, ADR-0090).
- [demos-check-notes.md](engine/demos-check-notes.md) — ce qu'il faut vérifier à l'œil dans les démos Cutscene MoveTo, Cutscene NavigateTo, Environment showcase et Skinned mesh (remplace les `.md` posés à côté des démos).

### UI runtime (MGUI)

- [casaengine-mgui-backend.md](engine/casaengine-mgui-backend.md) — backend MGUI CasaEngine ; texte sans allocation par `SetText(ReadOnlySpan<char>)` ; image du bureau sans allocation et ses limites.
- [data-driven-screens.md](engine/data-driven-screens.md) — écrans pilotés par les données, sans DLL : `FirstScreenName`, champs `is_modal`, `default_focus`, `fonts`, `ui_commands`, `focus_wrap`, `cancel_command`, `move_command`, `on_open_command` et `focus_markers` du `.uiscreen`, actions, fondus et attente (`fade_screen`, `wait_fade`), image soustractive (`BlendType`), navigation (`UIInputMapping`, répétition), écrans en pixels (`ScaleUI`, police bitmap), vidéo (`<Video>`, `PlayVideo`), appareil d'entrée `AnyKey` (touche quelconque, ADR-0130), erreurs au chargement, limites (ADR-0078, ADR-0079, ADR-0080, ADR-0081, ADR-0082, ADR-0128).
- [casaengine-mgui-backend-extensibility.md](engine/casaengine-mgui-backend-extensibility.md) — backend MonoGame extensible (Apos.Shapes, NvgSharp).

## Éditeur (`editor/`)

- [editor-workspace-layouts.md](editor/editor-workspace-layouts.md) — shell MGUI, panneaux dockables et layout par défaut.
- [editor-history.md](editor/editor-history.md) — undo/redo global (un historique par contexte d'édition).
- [editor-message-boxes.md](editor/editor-message-boxes.md) — boîtes de message MGUI de l'éditeur : file, libellés, questions d'enregistrement asynchrones (fermer un écran, quitter, ouvrir un monde), sélecteurs natifs gardés.
- [editor-input-routing-architecture.md](editor/editor-input-routing-architecture.md) — architecture du routage des inputs (vues, viewports, MGUI).
- [editor-2d-viewport.md](editor/editor-2d-viewport.md) — mode 2D du viewport monde (bascule, navigation, grille, gizmo XY, persistance).
- [viewport-camera-controls.md](editor/viewport-camera-controls.md) — contrôles de la caméra perspective du viewport et des démos, style Unreal (souris, vol au clic droit, F, W/E/R, sélection au clic relâché, ADR-0089).
- [editor-preferences.md](editor/editor-preferences.md) — panneau « Editor Preferences » et fichier `%APPDATA%/CasaEngine/editor.preferences.json` : sensibilités des caméras 3D et 2D, vitesse de vol mémorisée, inversion de Y, bornes et lecture tolérante (ADR-0091).
- [editor-remote-control.md](editor/editor-remote-control.md) — pilotage de l'éditeur ouvert par la CLI `casa-editor` et le serveur MCP `casa-editor-mcp` : activation, commandes, règles d'annulation, sécurité (ADR-0092).
- [audio-profiler-panel.md](editor/audio-profiler-panel.md) — panneau « Audio » (Windows > Audio) : statistiques du backend audio et vu-mètres par bus, en lecture seule (ADR-0060).
- [audio-mixer-panel.md](editor/audio-mixer-panel.md) — panneau de mixage d'un asset `.audioMixer` : faders, muet, solo, vu-mètres, niveau de la sortie dans le temps, effets, départs et ducking, annuler et enregistrer (ADR-0067).
- [asset-documents.md](editor/asset-documents.md) — documents des assets sans éditeur dédié : aperçu en lecture seule (images, cubemaps, audio, polices bitmap, glTF, données), documents `.texture` et `.staticModel`, options d'automatisation (ADR-0120).
- [cutscene-editor.md](editor/cutscene-editor.md) — document d'un asset `.cutscene` : arbre des actions dans Hierarchy, champs typés dans Inspector, ajout, duplication et suppression, annuler et rétablir, enregistrement refusé plutôt que perte de données, aperçu fidèle : fondus dessinés, entrée simulée, son à la demande (ADR-0134, ADR-0136, ADR-0142).
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
- [screen-authoring-conventions.md](editor/ui-screen-editor/screen-authoring-conventions.md) — conventions d'authoring des screens : enveloppe `.uiscreen`, écrans liés à un view-model, images nommées par asset (sprites et animations 2D), données de conception.
- [xaml-support-matrix.md](editor/ui-screen-editor/xaml-support-matrix.md) — matrice de support XAML.

## Voir aussi

- `MGUI/docs/` — documentation propre au framework MGUI (graph view, plans MGUI).
- [ai-agent/README.md](../ai-agent/README.md) — audits, analyses et suivi des tâches restantes.
