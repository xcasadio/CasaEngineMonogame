# Architecture Decision Records

This folder records the architecture decisions of CasaEngine: engine and editor architecture, asset formats, public APIs, backends, and the rules that govern the AI agents working on the repository.

## Rules

- One file per decision, named `NNNN-short-title.md` with an increasing four-digit number. Decisions taken together on the same theme may share one file.
- Written in English, from the template [template.md](template.md): Status, Date, Source, Context, Decision, Consequences.
- Every decision taken during a plan or a discussion is recorded here, at the time it is taken (skill `adr`; rule in `AGENTS.md` §10).
- Backfilled decisions keep a `Source` pointing at the original document. Audits under `ai-agent/audits/` are read-only: the decision is copied here, the audit is not edited.
- A decision is never rewritten: a change is a new record that supersedes the old one, whose status becomes `Superseded by ADR-XXXX`.

## Index

| ADR | Title | Status | Date |
|---|---|---|---|
| [ADR-0001](0001-audio-runtime-architecture-v1.md) | Audio runtime architecture V1 (buses, streaming, backend) | Accepted (MonoGame-only backend superseded in part by ADR-0055; folded bus gain superseded in part by ADR-0059; 2D only superseded in part by ADR-0064) | 2026-08-26 |
| [ADR-0002](0002-audio-asset-format-and-editor-scope-v1.md) | Audio asset format and editor scope V1 | Accepted (variations and priority added by ADR-0063) | 2026-08-26 |
| [ADR-0003](0003-single-3d-simulation.md) | Single 3D simulation, simulation space as a world policy | Accepted | 2026-08 |
| [ADR-0004](0004-collision-layers.md) | Collision layers Shape / Fixture / Body / World, Shape3d as the only public volume vocabulary, no pose on shapes | Accepted | 2026-08 |
| [ADR-0005](0005-collision-channels-profiles.md) | Collision channels, responses and named profiles | Accepted | 2026-08 |
| [ADR-0006](0006-collision-volumes-vs-fields.md) | Collision volumes vs collision fields | Accepted | 2026-08 |
| [ADR-0007](0007-fixtures-animated-by-timeline.md) | Fixtures animated by the animation timeline | Accepted | 2026-08 |
| [ADR-0008](0008-compatibility-posture-replace-not-duplicate.md) | Compatibility posture, replace rather than duplicate | Accepted | 2026-08 |
| [ADR-0009](0009-bepu-backend-replaces-bullet.md) | Physics backend bepuphysics2 replacing BulletSharp | Accepted | 2026-08-22 |
| [ADR-0010](0010-shader-naming-convention.md) | Shader naming convention and applied renames | Accepted | 2026-09-06 (backfill) |
| [ADR-0011](0011-tilemap-render-spaces.md) | Tilemap render spaces | Accepted | 2026-09-06 (backfill) |
| [ADR-0012](0012-materials-shaders-sources-of-truth.md) | Materials and shaders sources of truth matrix | Accepted | 2026-09-06 (backfill) |
| [ADR-0013](0013-pbr-rendering-decisions.md) | PBR rendering decisions | Accepted, amended by ADR-0103 | 2026-08-09 |
| [ADR-0014](0014-generic-timeline-base-for-the-editor.md) | Generic timeline base for the editor | Accepted (decision 2 superseded by ADR-0135) | 2026-06-13 |
| [ADR-0015](0015-animation2d-editor-v1.md) | Animation2D editor V1 | Accepted | 2026-09-06 (backfill) |
| [ADR-0016](0016-ui-screen-editor-v1.md) | UI screen editor V1 | Accepted | 2026-09-06 (backfill) |
| [ADR-0017](0017-mgui-backend-extensibility.md) | MGUI backend extensibility, Apos.Shapes and NvgSharp behind CasaEngine contracts | Accepted | 2026-09-06 (backfill) |
| [ADR-0018](0018-play-in-editor-session-model.md) | Play-in-editor session model | Accepted | 2026-09-06 (backfill) |
| [ADR-0019](0019-gltf-import-migration.md) | glTF import migration | Accepted | 2026-09-06 (backfill) |
| [ADR-0020](0020-cutscenes-v1-on-the-world-coroutine-manager.md) | Cutscenes V1 on the World coroutine manager | Accepted | 2026-09-06 (backfill) |
| [ADR-0021](0021-coroutines-v1.md) | Coroutines V1 | Accepted | 2026-09-06 (backfill) |
| [ADR-0022](0022-yarn-spinner-dialogue-integration.md) | Yarn Spinner dialogue integration | Accepted | 2026-09-06 (backfill) |
| [ADR-0023](0023-navigation-v1-on-tilemap-grids.md) | Navigation V1 on tilemap grids | Accepted | 2026-09-06 (backfill) |
| [ADR-0024](0024-gameplaymode-v1.md) | GameplayMode V1 | Accepted | 2026-09-06 (backfill) |
| [ADR-0025](0025-kinematic-character-controller-v1.md) | Kinematic character controller V1 | Accepted | 2026-09-06 (backfill) |
| [ADR-0026](0026-animation-deformer-support-policy-and-motion-matching-rd.md) | Animation deformer support policy and motion matching as a separate R&D stream | Accepted | 2026-09-06 (backfill) |
| [ADR-0027](0027-possession-and-per-player-input.md) | Possession and per-player input | Accepted | 2026-08-19 |
| [ADR-0028](0028-layering-rule-and-deferred-assembly-split.md) | Core-Engine-Framework layering rule and deferred assembly split | Accepted | 2026-09-06 (backfill) |
| [ADR-0029](0029-folder-hierarchy-and-namespace-layout.md) | Folder hierarchy and namespace layout | Accepted | 2026-09-06 (backfill) |
| [ADR-0030](0030-agents-md-single-source-and-tooling-layout.md) | AGENTS.md as the single source of AI agent rules and the tooling layout | Accepted | 2026-09-06 |
| [ADR-0031](0031-ai-agent-working-rules.md) | AI agent working rules | Accepted | 2026-09-06 |
| [ADR-0032](0032-language-policy-and-adr-practice.md) | Language policy and ADR practice | Accepted | 2026-09-06 |
| [ADR-0033](0033-screen-effect-above-ui-layer.md) | Screen effect above-UI layer | Accepted | 2026-09-19 |
| [ADR-0034](0034-screen-effect-alpha-and-depthless-veil.md) | Screen effect alpha channel and a veil that ignores depth | Accepted | 2026-09-20 |
| [ADR-0035](0035-xaml-authored-ui-screens.md) | UI screens are authored in XAML and loaded at runtime | Accepted (amended by ADR-0078) | 2026-09-20 |
| [ADR-0036](0036-reference-counted-asset-handles-and-bitmap-fonts.md) | Reference-counted asset handles with deferred release, and bitmap fonts as assets | Accepted (compatibility clause superseded by ADR-0037) | 2026-09-21 |
| [ADR-0037](0037-counted-handles-replace-load-and-categories.md) | Counted handles replace Load and asset categories | Accepted | 2026-09-21 |
| [ADR-0038](0038-game-screens-are-assets-bound-to-view-models.md) | Game screens are catalogued assets bound to observable view models | Accepted (amended by ADR-0078) | 2026-09-24 |
| [ADR-0039](0039-software-stereo-voices.md) | Software stereo voices with exact left/right gains | Accepted (game-thread path superseded in part by ADR-0056 under the software backend) | 2026-09-25 |
| [ADR-0040](0040-project-audio-mute-setting.md) | The audio mute is a project setting | Accepted | 2026-09-25 |
| [ADR-0041](0041-editor-message-boxes-are-mgui.md) | Editor message boxes are MGUI message boxes, answered asynchronously | Accepted | 2026-09-25 |
| [ADR-0042](0042-yarn-runner-extension-points.md) | Yarn dialogue runner extension points | Accepted | 2026-09-27 |
| [ADR-0043](0043-yarn-function-declarations-at-compile-time.md) | Yarn scripts compile with function declarations supplied by the game | Accepted | 2026-09-27 |
| [ADR-0044](0044-runtime-save-games.md) | The runtime owns a generic save-game service for the player's game saves | Accepted | 2026-09-28 |
| [ADR-0045](0045-a-blocked-field-step-advances-to-contact.md) | A blocked step on the cell collision field advances to the contact | Accepted | 2026-09-29 |
| [ADR-0046](0046-a-logical-tick-clock-for-2d-animation-ends.md) | A logical tick clock for the ends of 2D animations | Accepted | 2026-10-01 |
| [ADR-0047](0047-dynamic-movement-obstacles-in-the-character-controller-field-stage.md) | Dynamic movement obstacles in the character controller field stage | Accepted | 2026-10-02 |
| [ADR-0048](0048-virtual-resolution-with-integer-fit-and-black-bands.md) | A project virtual resolution fitted at an integer factor with black bands | Accepted (amended by ADR-0054, ADR-0079, ADR-0087) | 2026-10-03 |
| [ADR-0049](0049-background-layers-can-be-switched-off-by-identifier.md) | Background layers can be switched off by identifier, frozen and not drawn | Accepted | 2026-10-03 |
| [ADR-0050](0050-cellular-layers-follow-the-originals-period-drawn-position-and-c-rand.md) | Cellular layers follow the original's period, drawn position and C library rand | Accepted | 2026-10-03 |
| [ADR-0051](0051-psx-semi-transparency-of-sprites-as-two-disjoint-passes.md) | PSX semi-transparency of sprites as two disjoint passes | Accepted | 2026-10-03 |
| [ADR-0052](0052-cellular-wave-counter-truncated-type-0-parallax-and-cell-order.md) | One global wave counter, a truncated type-0 parallax factor and cell 0 drawn on top in cellular layers | Accepted | 2026-10-03 |
| [ADR-0053](0053-background-layers-draw-per-texel-psx-semi-transparency.md) | Background layers draw per-texel PSX semi-transparency | Accepted | 2026-10-05 |
| [ADR-0054](0054-mgui-clip-rectangles-are-local-to-the-view.md) | MGUI clip rectangles are local to the view, the device scissor is absolute | Accepted | 2026-10-05 |
| [ADR-0055](0055-engine-owned-software-audio-mixer-with-thin-native-outputs.md) | Engine-owned software audio mixer with thin native outputs | Accepted | 2026-10-05 |
| [ADR-0056](0056-optional-audio-backend-capabilities-and-real-time-stereo-voices.md) | Optional audio backend capabilities, real-time stereo voices, loop regions and background music reads | Accepted | 2026-10-05 |
| [ADR-0057](0057-ogg-vorbis-resident-clips-and-adpcm-decoding.md) | Ogg Vorbis resident clips and ADPCM decoding in one clip loader | Accepted | 2026-10-05 |
| [ADR-0058](0058-a-software-playstation-spu-hosted-by-the-software-audio-backend.md) | A software PlayStation SPU hosted by the software audio backend | Accepted (hardware tables policy superseded in part by ADR-0061 and ADR-0062, amended by ADR-0084) | 2026-10-06 |
| [ADR-0059](0059-a-bus-graph-with-effects-mixed-by-the-software-audio-backend.md) | A bus graph with effects, sends, ramps and snapshots mixed by the software audio backend | Accepted | 2026-10-06 |
| [ADR-0060](0060-lock-free-bus-metering-and-a-read-only-audio-profiler-panel.md) | Lock-free bus metering and a read-only audio profiler panel | Accepted | 2026-10-06 |
| [ADR-0061](0061-author-decisions-for-the-audio-game-layer-mixer-asset-and-psx-module.md) | Author's decisions for the audio game layer, the mixer asset and the PSX module | Accepted (amended by ADR-0084) | 2026-10-06 |
| [ADR-0062](0062-default-spu-tables-and-a-project-switch-for-the-master-limiter.md) | Default SPU tables and a project switch for the Master limiter | Accepted | 2026-10-06 |
| [ADR-0063](0063-sound-variations-and-voice-priorities.md) | Sound variations and voice priorities | Accepted | 2026-10-06 |
| [ADR-0064](0064-listener-spatial-audio-doppler-and-game-parameters.md) | Listener, spatial audio, Doppler and game parameters | Accepted | 2026-10-06 |
| [ADR-0065](0065-character-controller-min-move-distance-setting.md) | The character controller minimum move distance is a per-controller setting | Accepted | 2026-10-06 |
| [ADR-0066](0066-scrolling-layer-tint-draws-with-the-psx-mode-of-the-map.md) | The scrolling-layer tint draws with the PSX mode of the map | Accepted | 2026-10-06 |
| [ADR-0067](0067-a-project-mixer-asset-applied-to-the-live-mixer.md) | A project mixer asset applied to the live mixer, and an editable mixer panel | Accepted | 2026-10-06 |
| [ADR-0068](0068-free-psx-quads-draw-at-the-screen-resolution.md) | Free PSX quads draw at the screen resolution | Accepted | 2026-10-06 |
| [ADR-0069](0069-anim2d-corner-track-draws-a-part-as-a-free-psx-quad.md) | The `.anim2d` corner track draws a part as a free PSX quad | Accepted | 2026-10-06 |
| [ADR-0070](0070-view-layout-area-and-window-ui.md) | View layout area and window-level UI | Accepted | 2026-10-06 |
| [ADR-0071](0071-the-engine-draws-the-effect-quads-the-game-pushes.md) | The engine draws the effect quads the game pushes | Accepted | 2026-10-07 |
| [ADR-0072](0072-demos-start-on-a-menu-world-and-load-a-world-per-demo.md) | The demos start on a menu world and load a fresh world per demo | Accepted | 2026-10-08 |
| [ADR-0073](0073-the-default-music-queue-is-24-buffers-deep.md) | The default music queue is 24 buffers deep | Accepted | 2026-10-08 |
| [ADR-0074](0074-a-streamed-music-track-loops-back-to-its-loop-start-sample.md) | A streamed music track loops back to its loop-start sample (numbered ADR-0072 until 2026-10-10) | Accepted | 2026-10-08 |
| [ADR-0075](0075-script-objects-by-full-name-from-the-gameplay-assembly.md) | Script objects are created by full name, from the gameplay assembly only | Accepted | 2026-10-17 |
| [ADR-0076](0076-an-entity-lives-in-a-world-list-or-under-a-parent-never-both.md) | An entity lives in a world's entity list or under a parent entity, never both | Accepted | 2026-10-17 |
| [ADR-0077](0077-the-world-inspector-edits-the-world-game-mode.md) | The world inspector edits the world's game mode, and the project save writes it (numbered ADR-0076 until 2026-10-17) | Accepted | 2026-10-17 |
| [ADR-0078](0078-ui-screens-declare-commands-in-data.md) | UI screens declare their commands in data | Accepted (amended by ADR-0079, ADR-0080, ADR-0081, ADR-0082) | 2026-10-31 |
| [ADR-0079](0079-data-driven-screens-follow-the-design-resolution-and-hold-their-fonts.md) | Data-driven screens follow the design resolution and hold their fonts | Accepted (amended by ADR-0087) | 2026-10-31 |
| [ADR-0080](0080-data-driven-navigation-through-a-ui-input-mapping.md) | Data-driven navigation through a UI input mapping | Accepted | 2026-10-31 |
| [ADR-0081](0081-screen-fades-and-waits-in-data-driven-commands.md) | Screen fades and waits in data-driven commands | Accepted (amended by ADR-0082) | 2026-10-31 |
| [ADR-0082](0082-screens-run-a-command-on-open-and-mark-the-focus.md) | Screens run a command on open and mark the focus | Accepted | 2026-11-01 |
| [ADR-0083](0083-sounds-declare-an-exclusion-group.md) | Sounds declare an exclusion group | Accepted | 2026-11-01 |
| [ADR-0084](0084-the-software-spu-follows-published-captures.md) | The software SPU follows published captures | Accepted | 2026-11-01 |
| [ADR-0085](0085-the-tiled-json-map-import-has-a-content-entry.md) | The Tiled JSON map import has a content entry beside the path entry (numbered ADR-0078 until 2026-11-07) | Accepted | 2026-11-07 |
| [ADR-0086](0086-the-engine-keeps-the-last-frame-and-shows-it-frozen.md) | The engine keeps the last frame of the active view and shows it frozen (numbered ADR-0085 until 2026-11-08) | Accepted | 2026-11-08 |
| [ADR-0087](0087-a-world-declares-its-design-resolution-and-may-render-into-a-buffer.md) | A world declares its design resolution and may render into a buffer (numbered ADR-0086 until 2026-11-08) | Accepted | 2026-11-08 |
| [ADR-0088](0088-a-world-starts-a-cutscene-and-cutscenes-count-frames.md) | A world starts a cutscene, and cutscenes count frames, open screens and change worlds (numbered ADR-0087 until 2026-11-08) | Accepted | 2026-11-08 |
| [ADR-0089](0089-viewport-camera-navigation-follows-unreal.md) | Viewport camera navigation follows Unreal's perspective viewport, through one shared runtime class | Accepted | 2026-11-14 |
| [ADR-0090](0090-demos-carry-a-sheet-and-follow-a-learning-path.md) | Demos carry a sheet (controls, things to observe, documentation) and follow a learning path | Accepted | 2026-11-14 |
| [ADR-0091](0091-editor-user-preferences-file.md) | The editor keeps per-user mouse and camera preferences in one JSON file | Accepted | 2026-11-14 |
| [ADR-0092](0092-editor-remote-control-for-tools-and-ai-agents.md) | The running editor exposes typed commands to a CLI and an MCP server over a local pipe | Accepted | 2026-11-14 |
| [ADR-0093](0093-editor-remote-control-covers-assets-panels-and-automation.md) | Editor remote control covers assets, panels and the command-line automation | Accepted | 2026-11-14 |
| [ADR-0094](0094-converted-rigged-imports-keep-their-glb.md) | A rigged model imported from a non-glTF source keeps its converted glb | Accepted | 2026-11-15 |
| [ADR-0095](0095-3d-camera-keeps-its-vertical-field-of-view.md) | The 3d camera keeps its vertical field of view | Accepted | 2026-11-15 |
| [ADR-0096](0096-keyboard-bindings-are-key-positions.md) | Keyboard bindings are key positions | Accepted | 2026-11-15 |
| [ADR-0097](0097-desktop-runtime-owns-its-draw-transaction.md) | The desktop runtime owns its draw transaction | Accepted | 2026-11-15 |
| [ADR-0098](0098-the-ambient-term-applies-the-surface-colour-once.md) | The ambient term applies the surface colour once (numbered ADR-0089 until 2026-11-15) | Accepted | 2026-11-14 |
| [ADR-0099](0099-a-world-may-use-a-flat-ambient-term-with-a-reflection-cube.md) | A world may use a flat ambient term while keeping a reflection cube (numbered ADR-0090 until 2026-11-15) | Accepted | 2026-11-14 |
| [ADR-0100](0100-a-lit-diffuse-material-may-carry-a-detail-texture.md) | A lit diffuse material may carry a detail texture (numbered ADR-0091 until 2026-11-15) | Accepted | 2026-11-14 |
| [ADR-0101](0101-the-texture-loader-can-build-a-mipmap-chain.md) | The texture loader can build a mipmap chain (numbered ADR-0092 until 2026-11-15) | Accepted | 2026-11-14 |
| [ADR-0102](0102-shadow-settings-mode-colour-filter-fade-and-depth-range.md) | Shadow settings: application mode and colour, filter radius, fade distance and depth range (numbered ADR-0093 until 2026-11-15) | Accepted | 2026-11-14 |
| [ADR-0103](0103-pbr-scope-extends-to-bloom-transparency-skinning-and-local-probes.md) | PBR scope extends to bloom, transparency, skinning and local probes (numbered ADR-0094 until 2026-11-15) | Accepted, amended by ADR-0113 | 2026-11-14 |
| [ADR-0104](0104-a-world-selects-its-colour-pipeline-through-color-pipeline-settings.md) | A world selects its colour pipeline through colour pipeline settings (numbered ADR-0095 until 2026-11-15) | Proposed | 2026-11-15 |
| [ADR-0105](0105-the-linear-hdr-pipeline-renders-the-scene-into-a-pooled-hdr-target-and-tonemaps-before-the-ui.md) | The linear HDR pipeline renders the scene into a pooled HDR target and tonemaps before the interface (numbered ADR-0096 until 2026-11-15) | Proposed | 2026-11-15 |
| [ADR-0106](0106-display-colours-are-decoded-to-linear-light-for-a-view-that-really-renders-through-the-hdr-target.md) | Display colours are decoded to linear light for a view that really renders through the HDR target (numbered ADR-0097 until 2026-11-15) | Proposed | 2026-11-15 |
| [ADR-0107](0107-screen-sprites-and-text-are-flushed-after-the-tonemapping-in-the-linear-pipeline.md) | Screen sprites and text are flushed after the tonemapping in the linear pipeline (numbered ADR-0098 until 2026-11-15) | Proposed | 2026-11-15 |
| [ADR-0108](0108-bloom-is-a-threshold-and-blur-chain-added-to-the-hdr-scene-before-the-tonemapping.md) | Bloom is a threshold and blur chain added to the HDR scene before the tonemapping (numbered ADR-0099 until 2026-11-15) | Proposed | 2026-11-15 |
| [ADR-0109](0109-lit-pbr-is-a-built-in-material-definition-that-names-its-own-technique.md) | lit-pbr is a built-in material definition that names its own technique | Proposed | 2026-11-15 |
| [ADR-0110](0110-image-based-lighting-is-generated-on-the-gpu-and-the-specular-part-is-a-roughness-atlas.md) | Image-based lighting is generated on the GPU and its specular part is stored as a roughness atlas | Proposed | 2026-11-15 |
| [ADR-0111](0111-a-skinned-mesh-may-carry-a-lit-pbr-material.md) | A skinned mesh may carry a lit-pbr material | Proposed | 2026-11-15 |
| [ADR-0112](0112-the-gltf-static-model-reader-keeps-the-gltf-pbr-material-channels.md) | The glTF static model reader keeps the glTF PBR material channels | Proposed | 2026-11-15 |
| [ADR-0113](0113-pbr-scope-extends-to-clear-coat-screen-space-ambient-occlusion-and-fxaa.md) | PBR scope extends to clear coat, screen-space ambient occlusion and FXAA | Proposed | 2026-11-15 |
| [ADR-0114](0114-fxaa-filters-the-tonemapped-scene-before-the-post-tonemap-renderers-and-the-interface.md) | FXAA filters the tonemapped scene before the post-tonemap renderers and the interface | Proposed | 2026-11-15 |
| [ADR-0115](0115-lit-pbr-carries-a-clear-coat-layer-evaluated-as-a-second-lobe.md) | lit-pbr carries a clear coat layer evaluated as a second lobe | Proposed | 2026-11-15 |
| [ADR-0116](0116-a-scene-depth-pre-pass-feeds-screen-space-ambient-occlusion.md) | A scene depth pre-pass feeds screen-space ambient occlusion | Proposed | 2026-11-15 |
| [ADR-0117](0117-lit-pbr-may-carry-a-detail-texture.md) | lit-pbr may carry a detail texture | Proposed | 2026-11-16 |
| [ADR-0118](0118-serialized-properties-of-game-components.md) | Game components persist the properties they mark with [SerializedProperty] (numbered ADR-0071 until 2026-11-16) | Accepted | 2026-10-08 |
| [ADR-0119](0119-unresolved-components-are-kept.md) | A component of unknown type is kept as an unresolved component (numbered ADR-0072 until 2026-11-16) | Accepted | 2026-10-08 |
| [ADR-0120](0120-preview-and-edit-documents-for-assets-without-one.md) | Assets without a document open in a read-only preview, and textures and static models get edit documents (numbered ADR-0073 until 2026-11-16) | Accepted | 2026-10-08 |
| [ADR-0121](0121-editor-cameras-have-an-infinite-far-plane.md) | The editor cameras have an infinite far plane (numbered ADR-0074 until 2026-11-16) | Accepted | 2026-10-09 |
| [ADR-0122](0122-the-editor-names-the-elements-and-components-it-creates.md) | The editor names the screen elements and components it creates (numbered ADR-0075 until 2026-11-16) | Accepted | 2026-10-09 |
| [ADR-0123](0123-a-static-model-asset-keeps-the-tangents-of-its-meshes.md) | A static model asset keeps the tangents of its meshes | Accepted | 2026-11-16 |
| [ADR-0124](0124-a-texture-asset-can-ask-for-its-mipmap-chain.md) | A texture asset can ask for its mipmap chain | Accepted | 2026-11-17 |
| [ADR-0125](0125-the-static-mesh-draw-order-is-fully-deterministic.md) | The static mesh draw order is fully deterministic | Accepted | 2026-11-17 |
| [ADR-0126](0126-a-lit-pbr-material-can-ask-for-its-emissive-to-receive-the-shadow.md) | A lit-pbr material can ask for its emissive to receive the shadow | Accepted | 2026-11-17 |
| [ADR-0127](0127-the-tap-count-of-the-shadow-filter-is-a-setting.md) | The tap count of the shadow filter is a setting | Accepted | 2026-11-18 |
| [ADR-0128](0128-the-engine-plays-mdec-videos-through-a-video-element.md) | The engine plays MDEC videos through a Video element (numbered ADR-0089 until 2026-11-19) | Accepted | 2026-11-08 |
| [ADR-0129](0129-cutscene-actions-drive-named-sprite-entities-over-time.md) | Cutscene actions drive named sprite entities over time (numbered ADR-0090 until 2026-11-19) | Accepted | 2026-11-09 |
| [ADR-0130](0130-a-world-video-component-and-an-any-key-input.md) | A world video component plays under sprites, and an input entry can react to any key (numbered ADR-0091 until 2026-11-19) | Accepted | 2026-11-09 |
| [ADR-0131](0131-sprites-of-equal-depth-follow-a-draw-order.md) | Sprites of equal depth follow a draw order, then their submission (numbered ADR-0092 until 2026-11-19) | Accepted | 2026-11-10 |
| [ADR-0132](0132-a-world-video-can-run-to-its-end-after-its-cutscene.md) | A world video can run to its end after the cutscene that started it (numbered ADR-0093 until 2026-11-19) | Accepted | 2026-11-12 |
| [ADR-0133](0133-the-openal-output-pulls-the-mix-with-a-small-device-buffer.md) | The OpenAL output lets OpenAL Soft pull the mix, with a small device buffer (numbered ADR-0094 until 2026-11-19) | Accepted | 2026-11-17 |
| [ADR-0134](0134-cutscene-assets-are-edited-in-the-editor-on-their-action-tree.md) | Cutscene assets are edited in the editor, on their action tree (numbered ADR-0095 until 2026-11-19) | Accepted | 2026-11-17 |
| [ADR-0135](0135-the-cutscene-editor-previews-in-an-isolated-world-and-projects-the-tree-on-a-timeline.md) | The cutscene editor previews in an isolated world and projects its action tree on a timeline (numbered ADR-0096 until 2026-11-19) | Accepted | 2026-11-18 |
| [ADR-0136](0136-the-cutscene-editor-shows-an-outliner-and-typed-details-and-edits-the-structure.md) | The cutscene editor shows an action outliner and typed details, and edits the tree structure (numbered ADR-0097 until 2026-11-19) | Accepted | 2026-11-18 |
| [ADR-0137](0137-the-cutscene-timeline-is-a-read-only-projection-with-a-play-clock.md) | The cutscene timeline is a read-only projection of the action tree, fed by a play clock (numbered ADR-0098 until 2026-11-19) | Accepted | 2026-11-18 |
| [ADR-0138](0138-the-cutscene-preview-pauses-steps-and-seeks-by-replay.md) | The cutscene preview pauses, steps and seeks by replay, driven by a transport of the editing services (numbered ADR-0099 until 2026-11-19) | Accepted | 2026-11-18 |
| [ADR-0139](0139-the-cutscene-preview-shows-the-first-instant-and-keeps-the-world-live-after-the-end.md) | The cutscene preview shows the first instant at time 0 and keeps its world live once no play runs (numbered ADR-0100 until 2026-11-19) | Accepted | 2026-11-18 |
| [ADR-0140](0140-the-cutscene-editor-edits-the-action-tree-structure-by-mouse.md) | The cutscene editor edits the structure of the action tree by mouse (numbered ADR-0101 until 2026-11-19) | Accepted | 2026-11-18 |
| [ADR-0141](0141-the-cutscene-format-gains-an-at-action-and-the-timeline-places-actions-in-time.md) | The cutscene format gains an At action, and the timeline places actions in time (numbered ADR-0102 until 2026-11-19) | Accepted | 2026-11-18 |
| [ADR-0142](0142-the-cutscene-preview-draws-fades-simulates-an-input-and-plays-sound-on-demand.md) | The cutscene preview draws screen fades, simulates an input and plays sound on demand (numbered ADR-0103 until 2026-11-19) | Accepted | 2026-11-19 |
| [ADR-0143](0143-every-modified-document-asks-before-its-changes-are-lost.md) | Every modified document asks before its changes are lost (numbered ADR-0104 until 2026-11-19) | Accepted | 2026-11-19 |
| [ADR-0144](0144-simple-cameras-and-behaviour-components.md) | Cameras become simple, their behaviours become components | Accepted | 2026-11-19 |
| [ADR-0145](0145-camera-behaviours-are-components-that-pose-a-simple-camera.md) | Camera behaviours are components that pose a simple camera | Accepted | 2026-11-19 |
