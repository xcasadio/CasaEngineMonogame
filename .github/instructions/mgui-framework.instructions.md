---
applyTo: "MGUI/**"
---

# Instructions — Framework UI MGUI

## Objectifs
- Layout stable, input fiable, rendu performant.
- Clipping composable : scissor par défaut, stencil/mask si forme non rectangulaire.
- Contrôles : standards + extensibles.

## Règles hot path
- Zéro allocations par frame.
- Hit-test sans allocations.
- Cache text measurement si besoin (invalidate sur font/text/width).

## Rendering
- Scissor/Stencil : stack Push/Pop obligatoire.
- Restaurer GraphicsDevice (states + scissor rect).
- Batching SpriteBatch : limiter Begin/End.

## Input
- Capture souris pour drag.
- Focus clavier (Tab order si supporté).
- Propagation : respecter la convention existante du framework.

## API
- Éviter les breaking changes.
- Toute nouvelle feature doit être testée via un sample/démo.
