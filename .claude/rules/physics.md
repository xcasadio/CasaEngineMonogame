---
paths:
  - "CasaEngine/Engine/Physics/**"
  - "CasaEngine/Framework/Physics/**"
  - "CasaEngine/Framework/Application/Components/Physics/**"
---

<!-- Jumeau de .github/instructions/physics.instructions.md : modifier les deux. -->


# Instructions — Physique

Les règles générales sont dans `AGENTS.md` (§9.6). Ce fichier détaille ce qui est propre à la physique.

## Backend

- Backend : bepuphysics2, dans `CasaEngine/Framework/Physics/Bepu/`, derrière `IPhysicsWorld` (`CasaEngine/Framework/Application/Components/Physics/`).
- Le backend reste derrière des interfaces stables ; aucun type du backend ne remonte dans les API gameplay de haut niveau, sauf usage déjà établi.
- Prévoir la conversion d'unités (mètres) et d'axes entre le moteur et le backend.

## Propriété et synchronisation

- Clarifier qui pilote le transform : la physique ou le gameplay. Pas de synchronisation bidirectionnelle sans règle écrite.
- Le comportement à pas fixe reste déterministe.

## Modification du backend

- Inspecter le backend existant avant de le modifier ; préserver le comportement actuel sauf changement explicite ; documenter tout risque de migration.

## Debug draw

- Toute nouvelle feature de collision ou de physique ajoute son debug draw, activable.

## Validation propre à la physique

Un sample couvre la feature : chute de cubes, raycast, character controller simple, colliders primitifs, debug draw activable.
