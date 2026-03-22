# Skill: feature-workflow

## Quand l’utiliser
Quand tu dois implémenter une feature complète (Editor/UI/Rendering/Physics/Sample).

## Entrées attendues
- Feature name
- Dossier(s) ciblés
- Comportement attendu + edge cases
- Comment tester (ou à défaut : comment le prouver)

## Workflow (obligatoire)
1) **Mini plan** (3–8 étapes) + risques + critères d’acceptation.
2) **Scaffold** (interfaces / classes / points d’extension).
3) Implémenter par sous-tâches, avec **1 commit par sous-tâche**.
4) Ajouter une **démo / sample** si feature visible.
5) Vérifier build + run.
6) Doc courte.

## Commit discipline
- `feat(<area>): ...` / `fix(<area>): ...`
- Chaque commit doit compiler.

## Definition of Done
- Feature testable + build OK + doc + aucune alloc évitable en hot path.
