# ai-agent — audits et suivi des tâches

Ce dossier contient le travail des agents IA : audits/analyses (`audits/`) et plans de tâches (`tasks/`).
La documentation du moteur et de l'éditeur vit dans [`docs/`](../docs/README.md).

- `audits/` — rapports d'audit, analyses et checklists de validation. Lecture seule : on ne « termine » pas un audit, on le consulte.
- `tasks/` — plans avec du travail restant. Chaque fichier utilise la légende ⏳ Todo · 🚧 In progress · 🧪 Needs testing · ✅ Done · ⚠️ Blocked.
- `tasks/archive/` — plans terminés ou abandonnés (marqués `[ARCHIVE]`), conservés pour l'historique.

## Tâches restantes (`tasks/`)

État constaté le 2026-08-09 en lisant les statuts dans chaque fichier.

| Fichier | Sujet | Reste à faire |
| --- | --- | --- |
| [ui-integration.md](tasks/ui-integration.md) | UI Root, screen stack, overlay/world, game states | Plan quasi entier : 77 cases non cochées. |
| [tilemap-modernization-tasks.md](tasks/tilemap-modernization-tasks.md) | Modernisation TileMap + import Tiled | Validations 🧪 sur les phases 1–9 ; phase 10 (streaming) ⏳ ; outils éditeur (palette, paint, undo, overlays) ⏳. |
| [editor-undo-redo-tasks.md](tasks/editor-undo-redo-tasks.md) | Undo/redo global de l'éditeur | Phases 3–7 en 🚧/🧪 : screen editor, world editor, material editor, content browser, stabilisation. |
| [gltf-import-migration-tasks.md](tasks/gltf-import-migration-tasks.md) | Migration import glTF (SharpGLTF/AssimpNetter) | B4 (retrait Assimp runtime), C1–C3 (convertisseur éditeur), E1–E4 (tests, retrait AssimpNet, validation). |
| [yarn-spinner-integration-agent-plan.md](tasks/yarn-spinner-integration-agent-plan.md) | Intégration Yarn Spinner | Tâches 12–18 ⏳ (lignes multiples, choix, variables, commandes, cutscenes, doc) ; tâches 2/4/5/6 en 🧪. |
| [SCREEN_EDITOR_REFACTOR_TASKS.md](tasks/SCREEN_EDITOR_REFACTOR_TASKS.md) | UI Screen Editor post-V1 | Backlog de refactor complet. |
| [animation2d-editor-agent-plan.md](tasks/animation2d-editor-agent-plan.md) | Éditeur Animation2D | V3.1 (controller + graphe 2D) et V3.2 (intégration gameplay, extensions UI) ⏳. |
| [animation2d-modernization-tasks.md](tasks/animation2d-modernization-tasks.md) | Modernisation animations 2D | Tâches 1.1 → 3.2 en 🧪 (validations à faire). |
| [shader-source-hot-reload-tasks.md](tasks/shader-source-hot-reload-tasks.md) | Hot reload des shaders source | T04.02 ⏳, T05.01 🧪, T06.01–T06.02 ⏳ (tests + smoke test documenté). |
| [racinggame-car-profile-parity-plan.md](tasks/racinggame-car-profile-parity-plan.md) | Profils de voiture RacingGame | 9.1–9.3 ⏳ : nettoyage des reliquats, documentation, clôture. |
| [casaengine-mgui-renderer-tasks.md](tasks/casaengine-mgui-renderer-tasks.md) | Renderer MGUI CasaEngine | T06–T12 en 🧪 (validations ciblées). |
| [casaengine-mgui-backend-extensibility-tasks.md](tasks/casaengine-mgui-backend-extensibility-tasks.md) | Backend extensible Apos.Shapes / NvgSharp | Phases 4–6 en 🧪 (Apos, NvgSharp, durcissement final). |
| [mgui-native-dark-theme-plan.md](tasks/mgui-native-dark-theme-plan.md) | Thème dark natif MGUI | T10 🧪 (suite de tests complète), T11 🧪 (smoke visuel manuel). |
| [editor-mgui-dark-theme-plan.md](tasks/editor-mgui-dark-theme-plan.md) | Thème dark de l'éditeur | T7.2/T7.3 🧪 (validation visuelle manuelle + perf). |
| [cutscene-implementation-plan.md](tasks/cutscene-implementation-plan.md) | Cutscenes | Tâches 11 et 16 en 🧪 (validation visuelle utilisateur) ; tests bloqués par des erreurs de compilation de `CasaEngine.Tests`. |
| [sky-environment-implementation-plan.md](tasks/sky-environment-implementation-plan.md) | Sky / Environment | ENV-009 🧪 (centraliser la source de lighting au niveau environnement). |
| [gameplay-mode-implementation-plan.md](tasks/gameplay-mode-implementation-plan.md) | GameplayMode | Tâches 6 et 15 ⚠️ (validations finales V1/V2 bloquées). |
| [static-model-import-tasks.md](tasks/static-model-import-tasks.md) | Import de modèles 3D statiques | Task 6.2 : tester l'import via l'éditeur. |
| [character-controller-tasks.md](tasks/character-controller-tasks.md) | Character Controller | V3.3 (locomotion avancée) ⚠️ bloquée. |

### Points d'attention

- `ai-agent/tasks/animation2d-modernization-tasks.md` déclare une documentation ajoutée dans `docs/engine/animation2d-composed-format-v1.md`, mais ce fichier n'existe pas dans le dépôt : la doc du format composé reste à écrire.
- Les notes de blocage de `cutscene-implementation-plan.md` (erreurs de compilation `CasaEngine.Tests`) datent d'une session précédente : à revérifier avant de reprendre.

## Audits et analyses (`audits/`)

### Architecture

- [CasaEngine_architecture_audit_report.md](audits/CasaEngine_architecture_audit_report.md) — audit d'architecture globale (hébergement d'un runtime UI type MGUI/XAML).
- [CasaEngine_folder_hierarchy_audit_report.md](audits/CasaEngine_folder_hierarchy_audit_report.md) — audit de la hiérarchie de dossiers `Core`/`Engine`/`Framework`.
- [CasaEngine_folder_hierarchy_namespace_compatibility.md](audits/CasaEngine_folder_hierarchy_namespace_compatibility.md) — compatibilité namespaces vs dossiers.
- [CasaEngine_layering_project_split_evaluation.md](audits/CasaEngine_layering_project_split_evaluation.md) — évaluation d'un découpage en projets.
- [structure-analyze-tasks.md](audits/structure-analyze-tasks.md) — analyse détaillée structure & architecture (constats ⚠️ encore exploitables).
- [editor-runtime-separation-audit-report.md](audits/editor-runtime-separation-audit-report.md) — audit de la séparation éditeur/runtime.
- [runtime-editor-separation-reliquats-audit.md](audits/runtime-editor-separation-reliquats-audit.md) — reliquats de séparation runtime/éditeur (dernière passe).

### Rendu et materials

- [material-shader-class-audit.md](audits/material-shader-class-audit.md) — classification des types material/shader (actif, transition, dead code).
- [analysis-graphic-pipeline.md](audits/analysis-graphic-pipeline.md) — analyse du pipeline graphique.

### Animation et 2D

- [animation-example-analysis-report.md](audits/animation-example-analysis-report.md) — analyse DigitalRune / GameAnimationProgramming.
- [animation2d-editor-surface-notes.md](audits/animation2d-editor-surface-notes.md) — surface éditeur Animation2D vérifiée.
- [analysis_tilemaps_casaengine.md](audits/analysis_tilemaps_casaengine.md) — analyse de la gestion des TileMaps (source du plan de modernisation).

### Démos et assets

- [analysis-example-project.md](audits/analysis-example-project.md) — qualité des démos (`CasaEngine.Demos`, `Projects`).
- [analysis-LoadDirectly.md](audits/analysis-LoadDirectly.md) — chargement d'assets dans les démos (`LoadDirectly`).

### Validations et checklists

- [editor-input-routing-validation.md](audits/editor-input-routing-validation.md) — validation du routage input éditeur.
- [ui-screen-editor-perf-assessment.md](audits/ui-screen-editor-perf-assessment.md) — perf de la preview du screen editor.
- [editor-final-smoke-checklist.md](audits/editor-final-smoke-checklist.md) — checklist finale de smoke tests éditeur.
- [editor-history-smoke.md](audits/editor-history-smoke.md) — checklist smoke undo/redo.

## Archive (`tasks/archive/`)

Plans terminés (ou explicitement archivés) : nouvel éditeur MGUI, multi-view/ViewManager v2, drag & drop d'assets, content browser, panels contextuels, layouts par mode, import de modèles statiques (V1), materials (plans `[ARCHIVE]` remplacés par la modernisation), audits d'architecture et de hiérarchie exécutés, coroutines V1, particules, animation moderne, timeline générique, profondeur TileMap, sprite viewer/thumbnails, portage skeletal blending, LightComponent, forward lighting/shadows, navigation V1, message bus World, nettoyage `Save(JObject)`, backlog V1 du screen editor.
