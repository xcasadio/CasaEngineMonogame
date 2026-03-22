# AGENTS.md — CasaEngineMonogame

Ces instructions s’appliquent à tous les agents (Copilot) dans ce repo.

## Objectif
Développer des fonctionnalités C# MonoGame pour :
- l’éditeur (migration UI vers MGUI),
- le framework UI (MGUI),
- le rendu (3D, skinned mesh, shaders, forward/deferred),
- l’intégration de moteurs physiques,
- des samples de gameplay.

## Règles de livraison (obligatoires)
1. **Commits fréquents** : 1 commit par sous-tâche atomique (compilable).
2. **Ne pas casser l’API** sans compat (obsolètes/overloads).
3. **Hot path** (Update/Draw) : zéro alloc évitable, pas de LINQ.
4. Toujours **restaurer l’état** du GraphicsDevice (scissor/stencil/rasterizer).
5. Ajouter un **sample minimal** dès qu’une feature n’est pas triviale.
6. Si tu crées un plan de travail, utilise ces statuts :
   - ✅ Done
   - 🚧 In progress
   - ⏳ Todo
   - 🧪 Needs testing
   - ⚠️ Blocked

## Build & vérifs
- Toujours lancer un build local (solution) avant de considérer une tâche terminée.
- Si un sample existe pour la zone touchée, le lancer au moins une fois.

## Dépendances
- Éviter les dépendances lourdes.
- Si ajout nécessaire : expliquer pourquoi, scope minimal, et fallback si possible.
