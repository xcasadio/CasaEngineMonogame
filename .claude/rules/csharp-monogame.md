---
paths:
  - "**/*.cs"
---

<!-- Jumeau de .github/instructions/csharp-monogame.instructions.md : modifier les deux. -->


# Instructions — C# / MonoGame (tous fichiers .cs)

Les règles générales (chemins chauds, état GPU, API publique, style, erreurs, tests) sont dans `AGENTS.md` à la racine. Ce fichier n'ajoute que ce qui est propre au C#.

- Exceptions uniquement pour les erreurs de programmation (arguments invalides). Les erreurs de données et d'assets se signalent avec du contexte, sans lever à chaque frame.
- Un calcul pur (layout, intersections de rectangles, tri par z-order) touché par la tâche reçoit un test unitaire dans `CasaEngine.Tests`.
