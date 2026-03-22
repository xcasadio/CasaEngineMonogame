# Skill: render-pass-scaffold

## But
Ajouter une render pass au pipeline (forward/deferred).

## Étapes
1) Définir inputs/outputs (RenderTargets, depth, textures)
2) Définir states (Blend/Depth/Rasterizer)
3) Construire draw-list CPU (tri, culling, batching)
4) Exécuter GPU (draw) + restaurer states
5) Hook pipeline (ordre des passes)
6) Sample scène de validation

## Checklist
- Pas de fuite d’état GPU
- Fallback si RT non supporté / shader absent
- Mesure simple : draw calls / state changes

## Done
- Pass intégrée + scène sample + build OK.
