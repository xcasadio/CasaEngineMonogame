---
name: physics-backend-adapter
description: "Étendre la physique CasaEngine sur le backend bepuphysics2 derrière IPhysicsWorld : nouvelle feature, requêtes, debug draw, sample."
---

# Skill : physics-backend-adapter

## But

Ajouter ou étendre une feature physique en passant par le backend bepuphysics2 (`CasaEngine/Framework/Physics/Bepu/`) et l'API stable existante (`IPhysicsWorld`, `CasaEngine/Framework/Application/Components/Physics/`).

## Étapes

1. Inspecter l'API existante (`IPhysicsWorld`, `PhysicsWorld`) et le backend Bepu avant toute modification ; ne rien inventer sur ce qu'ils offrent.
2. Implémenter la feature dans le backend, puis l'exposer par l'API existante, sans type Bepu dans les API gameplay.
3. Si la tâche le demande : couches et masques de collision, matériaux (friction, restitution), requêtes raycast et overlap.
4. Debug draw activable.
5. Sample : pile de cubes, raycast, character controller simple.

## Checklist

- Qui pilote le transform (physique ou gameplay) est écrit.
- Debug draw activable.
- Règle par chemin `physics` et `AGENTS.md` §9.6 respectés.

## Done

Feature branchée, sample, build OK.
