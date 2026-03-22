---
applyTo: "**/*.cs"
---

# Instructions C# / MonoGame (tous fichiers .cs)

## Style
- Pas de LINQ dans Update/Draw.
- Préférer des méthodes petites et testables.
- Exceptions uniquement pour erreurs de programmation (arguments invalides).

## Patterns
- Invalidation : `InvalidateLayout/Measure/Arrange` (ou équivalent) sur changement de props UI.
- Input : routing clair, capture, focus.
- Rendering : toujours restaurer l’état GPU.

## Tests
- Si tu touches des calculs purs (layout, rect intersections, tri z-order), ajouter des tests unitaires si possible.
