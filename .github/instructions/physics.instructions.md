---
applyTo: "{CasaEngine/**/Physics/**,CasaEngine/**/Physic/**,ThirdParties/**,CasaEngine.Demos/**/Physics/**}"
---

# Instructions — Intégration Physique (backends multiples)

## Objectifs
- Interface stable (IPhysicsWorld, IBody, IShape, IConstraint…).
- Adaptateurs par backend (Bullet, Jolt, BEPU…).
- Debug draw standard + stepping déterministe (optionnel).

## Règles
- Clarifier “source of truth” transform (physique vs scene).
- Prévoir conversion unités (mètres) et axes.
- Éviter allocations dans simulation step.

## Validation
- Sample obligatoire :
  - drop cubes, raycast, character controller simple,
  - colliders primitives,
  - debug draw activable.
