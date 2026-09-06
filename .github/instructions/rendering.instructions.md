---
name: rendering
description: Règles propres au rendu CasaEngine (passes, materials, shaders, performance GPU).
applyTo: "{CasaEngine.Shaders/**,CasaEngine/Content/Shaders/**,CasaEngine/Framework/Rendering/**,CasaEngine/Framework/Materials/**,CasaEngine/Framework/Particles/Rendering/**}"
---

# Instructions — Rendu, shaders, materials

Les règles générales sont dans `AGENTS.md` (§9.4 : restauration de l'état GPU, séparation données / pipeline / backend). Ce fichier détaille ce qui est propre au rendu.

## Structure

- Données : materials, meshes, textures, lumières, caméras. Pipeline : `RenderPipeline`, `ForwardRenderPipeline`, passes `RenderPass` (`OpaquePass`, `TransparentPass`, `ShadowPass`, `SkyPass`). Backend : `GraphicsDevice`, `Effect`, `RenderTarget2D` de MonoGame.
- Chaque passe encapsule ses entrées et sorties (render targets, depth, textures), ses états (blend, depth, rasterizer) et sa draw list. Construire la draw list côté CPU (tri, culling, batching) séparément de son exécution GPU.
- Préférer des passes explicites. Pour une nouvelle feature, préférer les structures extensibles existantes (pipeline, passes, blocs de paramètres de material, couches de lumières, visualisation de debug) plutôt qu'un chemin ad hoc. Ne pas implémenter d'architecture de rendu avancée sans demande explicite.

## Materials et shaders

- Les paramètres de material sont des données d'asset ou d'éditeur, jamais codés en dur.
- Variants de shader pilotés par des flags (skinned, instancing, normal map, etc.), cache des permutations et des techniques, binding explicite des paramètres, samplers et constant buffers.
- Un shader absent ou une feature non supportée est géré proprement : technique de repli quand c'est raisonnable.
- Les skinned meshes visent la performance CPU et GPU et la compatibilité.

## Performance

- Limiter les `SpriteBatch.Begin` / `End` ; éviter les changements d'état redondants et les changements de texture ; batcher les draw calls ; aucune allocation pendant le rendu.
- Mesurer : draw calls, changements d'état, allocations.
- Logs uniquement au chargement, jamais dans `Draw`.
