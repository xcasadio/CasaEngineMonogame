# Plan agent IA — Déplacement minimal du contrôleur de personnage (réglage par contrôleur)

Plan d'exécution de la partie moteur de la tranche E19.h1b3 du portage Alundra (plan parent `docs/plan-e19-opcodes.md`, §1.2n.1d,
dépôt `alundra-casaengine-project-converter`). **Approuvé : plan parent E19.h1b3 READY (relecture n°2), approuvé par l'auteur le
2026-10-06.** Les décisions viennent du plan parent (D-E19-94, D-E19-98, règle H1B3-R1) : **ce plan les applique, il ne les rediscute pas**.

Ce fichier doit être mis à jour pendant le travail : l'icône au début de chaque tâche indique son statut courant.

## Objectif

`CharacterControllerSettings.MinMoveDistance` (défaut `0.001`, clé `min_move_distance`) remplace la constante `MinMoveDistanceSquared`
aux six usages de `CharacterControllerComponent`. À 0, un déplacement non nul, même d'une unité fixe (1/65536 px), est appliqué ; un
déplacement nul reste jeté. Clonage, chargement, validation (négatif refusé) et sérialiseur de l'éditeur suivent.

## Hors périmètre

- La DLL du parent (règle `WaitHeightTarget`, réglage à 0 dans la fabrique) : tâche H1B3-2 du plan parent.
- Tout autre réglage du contrôleur.

## État vérifié du dépôt (2026-10-06)

- Branche `chantier/e19h1b3-min-move-distance` créée par la règle R-BR (1) depuis le commit épinglé par le parent (`33324030`).
  Modification locale de l'auteur dans `CasaEngine.Launcher/Program.cs` : ne jamais l'indexer, ne jamais la modifier.

## Règles d'exécution pour l'agent

- **Branche dédiée**. Ne jamais committer sur `main`. Ne jamais pousser, ne jamais merger.
- Tests d'abord : rouge constaté (valeurs lues) sur le code d'avant avec la surface d'API minimale, puis vert.
- `git add` fichier par fichier. `CasaEngine.Tests` n'est pas dans le `.sln` : le construire explicitement.
- Langue : plan en français ; code, commits, docs et ADR en anglais.

## Légende des statuts

- ⏳ Todo · 🚧 In progress · 🧪 Needs testing · ✅ Done · ⚠️ Blocked

---

## Phase 1 — Moteur

### ✅ H1B3-1 — Réglage `MinMoveDistance`

- Fichiers : `CasaEngine/Framework/Scene/Entities/Components/CharacterControllerSettings.cs`, `CharacterControllerComponent.cs`,
  `CasaEngine.EditorServices/EditorEntityJsonSerializer.cs`, `CasaEngine.Tests/Physics/CharacterControllerSettingsTests.cs`,
  `CharacterControllerComponentTests.cs`, `CasaEngine.Tests/EditorServices/CharacterControllerComponentSerializationTests.cs`,
  `docs/engine/character-controller-features.md`.
- Valeurs de test : défaut 0,001 ; `Move` de 0,0005 sur X → déplacement nul par défaut, 0,0005 appliqué à 0 ; `Move` nul à 0 → jeté ;
  chargement `min_move_distance` = 0 → 0 ; clonage 0,25 ; −0,5 refusé ; aller-retour du sérialiseur à 0.
- Validation faite : rouges d'abord sur la propriété ajoutée sans comportement : 5 tests rouges (Move à 0 : attendu (0,0005, 0, 0), lu
  (0, 0, 0) ; chargement : attendu 0, lu 0,001 ; clonage : attendu 0,25, lu 0,001 ; validation : aucune exception ; sérialiseur :
  attendu 0, lu 0,001) ; les défauts, `Move` par défaut et `Move` nul étaient verts d'avance (gardes). Verts après : suite du moteur
  2748/2748.
- Commit : `feat(character-controller): the minimum move distance is a per-controller setting`

### ✅ H1B3-2 — Documentation et ADR

- `docs/engine/character-controller-features.md` (ligne `MinMoveDistance`), ADR-0065 (`Accepted`) et sa ligne d'index. Numéro : 1 + le
  plus grand numéro trouvé sur `main` (0064), `chantier/audio-modern` (0064) et les branches `chantier/e19*` (0056) par la règle R-BR (2),
  soit 0065 (le plan parent attendait 0062 : `main` du moteur porte depuis 0062 à 0064, l'audio mergé par l'auteur).
- Commit : `docs(adr): ADR-0065 the character controller minimum move distance is a per-controller setting`

---

## Points ouverts

| Réf | Sujet | Tâche concernée |
|---|---|---|
| — | Aucun. | — |
