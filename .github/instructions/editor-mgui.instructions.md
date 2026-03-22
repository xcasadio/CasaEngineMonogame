---
applyTo: "{CasaEngine.Editor/**,CasaEngine.EditorUI/**,CasaEngine.EditorServices/**,Editor/**,GizmoTool/**}"
---

# Instructions — Éditeur (MGUI)

## Objectifs
- Migrer/implémenter l’UI de l’éditeur avec MGUI (runtime UI).
- Multi-vues : viewport(s), inspector, hierarchy, console, asset browser.
- UX “engine editor” : docking, split panes, onglets, layout persisté.

## Règles
- Aucun code WPF nouveau (sauf maintenance legacy).
- Éviter la logique “éditeur” dans le runtime : préférer `EditorServices`/`EditorUI`.

## Fonctionnalités fréquentes
- Undo/Redo transactionnel
- Gizmos (move/rotate/scale), snapping
- Selection & picking
- Docking layout + persistence (JSON)
- Command palette / shortcuts

## Validation
- Toujours fournir une scène de démo (sample editor) ou une page/écran testable.
- Tests : au moins des tests pour layout docking (compute pur).
