---
applyTo: "{CasaEngine.Shaders/**,CasaEngine/**/Rendering/**,CasaEngine/**/Graphics/**,CasaEngine/**/Materials/**,CasaEngine/**/Effects/**}"
---

# Instructions — Rendering (Forward/Deferred, Shaders, Materials)

## Objectifs
- Pipeline modulaire (passes).
- Materials stables (paramètres, textures, variants).
- Skinned meshes : perf CPU/GPU + compat.

## Règles
- Aucune fuite d’état GPU : toujours restaurer states.
- Prévoir fallback (shader absent / feature non supportée).
- Mesurer : draw calls, state changes, allocations.

## Shader variants
- Variants pilotés par flags (skinned, instancing, normal map, etc.).
- Cache des permutations / techniques.
- Binding explicite : paramètres, samplers, constant buffers.

## Pipeline
- Encapsuler chaque pass : inputs/outputs (RenderTargets), states, draw list.
- Séparer build de draw list et exécution GPU.
