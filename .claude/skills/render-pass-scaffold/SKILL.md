---
name: render-pass-scaffold
description: Ajouter une passe de rendu au pipeline CasaEngine (RenderPipeline, RenderPass) avec ses états, sa draw list et sa scène de validation.
---

# Skill : render-pass-scaffold

## But

Ajouter une passe au pipeline de rendu (`RenderPipeline`, `ForwardRenderPipeline`, passes dérivées de `RenderPass` comme `OpaquePass`, `TransparentPass`, `ShadowPass`, `SkyPass`).

## Étapes

1. Définir les entrées et sorties : render targets, depth, textures.
2. Définir les états : blend, depth, rasterizer.
3. Construire la draw list côté CPU : tri, culling, batching.
4. Exécuter côté GPU, puis restaurer les états (`AGENTS.md` §9.4).
5. Brancher la passe dans le pipeline, à sa place dans l'ordre des passes.
6. Scène de sample pour la validation.

## Checklist

- Technique de repli si un render target n'est pas supporté ou si un shader manque.
- Mesure simple : draw calls, changements d'état.
- Règle par chemin `rendering` respectée.

## Done

Passe intégrée, scène de sample, build OK.
