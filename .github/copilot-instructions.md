# Copilot Instructions — CasaEngineMonogame (C# / MonoGame)

Tu codes en C# dans un moteur MonoGame orienté éditeur + runtime.

## Priorités
1) Correctness (input/layout/render)  
2) Stabilité API (compat > breaking)  
3) Perf (hot path)  
4) Lisibilité (code clair, noms explicites)  
5) Démo / sample (quand utile)

## Perf (règles strictes)
- Pas de LINQ / closures / allocations dans Update/Draw.
- Pas de nouvelles Lists/Dictionaries par frame : réutiliser + Clear.
- Éviter string formatting dans Draw (pré-calculer/cacher).
- Batching SpriteBatch : minimiser Begin/End.
- Si clipping : stack (Push/Pop) et **restaurer** l’état GraphicsDevice.

## Layout / UI
- Toute modif de propriété impactant la taille/position doit invalider le layout proprement.
- Hit-test déterministe : z-order + visiblité + enabled + clipping.
- Input : capture souris pour drag, focus clavier unique, navigation tab si applicable.

## Shaders / Rendering
- Séparer : données (materials/meshes) / pipeline (passes) / backend (GraphicsDevice).
- Éviter les “state leaks” : RasterizerState/BlendState/DepthStencilState.
- Préférer des structures de passes (ForwardPass, GBufferPass, LightingPass…).
- Prévoir fallback (matériel sans feature, shader manquant, etc.)

## Physique
- Viser une abstraction stable (interfaces) + adaptateurs par backend.
- Ajouter debug draw et synchronisation transform claire (qui drive quoi ?).

## Commits
- 1 commit par sous-tâche, message explicite.
- Toujours laisser le build dans un état OK.

## Documentation
- Si API publique : doc courte + snippet dans README/docs.
- Si feature “éditeur” : au moins un sample / écran de démo.
