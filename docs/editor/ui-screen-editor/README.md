# UI Screen Editor

## Objectif

Créer un éditeur de screens MGUI intégré à CasaEngine avec un modèle de document dédié, une prévisualisation runtime reconstruite depuis ce document, et une sérialisation XAML comme format principal.

## Périmètre

- édition d'un screen UI MGUI comme asset CasaEngine
- preview temps réel dans l'éditeur
- hiérarchie des contrôles
- inspector de propriétés
- sauvegarde et rechargement via XAML
- fondations pour sélection visuelle, drag and drop, resize et guides

## Non-objectifs v1

- édition directe des instances runtime MGUI comme source de vérité
- support complet de tous les scénarios XAML avancés dès la première version
- design surface avancée complète dès les premières tâches
- couplage à un screen concret ou à un seul workflow d'éditeur

## Documents de suivi

- backlog d'implémentation V1 (terminé) : `ai-agent/tasks/archive/SCREEN_EDITOR_IMPLEMENTATION_TASKS.md`
- backlog de refactor post-V1 : `ai-agent/tasks/SCREEN_EDITOR_REFACTOR_TASKS.md`
- évaluation de performance de la preview : `ai-agent/audits/ui-screen-editor-perf-assessment.md`
- les documents d'architecture et de cadrage vivent dans ce dossier (`architecture.md`, `architecture-entry-points.md`, `screen-authoring-conventions.md`, `xaml-support-matrix.md`)
