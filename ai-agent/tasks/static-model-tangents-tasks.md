# Plan agent IA — Tangentes dans le format `.staticModel`

Plan d'exécution de la tranche S1d du chantier du jeu RacingGame (`ai-agent/tasks/rgce-pbr-handover-tasks.md` dans le dépôt du jeu).
Les décisions D1 → D3 ci-dessous ont été arbitrées avec l'auteur le 2026-11-16 : **ce plan les applique, il ne les rediscute pas**.

Ce fichier doit être mis à jour pendant le travail : l'icône au début de chaque tâche indique son statut courant.

## Objectif

Un `.staticModel` garde les tangentes de ses maillages. Un modèle cuit depuis un glTF qui a des tangentes peut alors être dessiné avec sa normal map, comme un modèle lu directement depuis le glTF.

## État vérifié du dépôt (2026-11-16)

- `StaticModelMesh` tient deux tableaux : `_vertices` (`VertexPositionNormalTexture`) et `_tangentVertices` (`VertexPositionNormalTextureTangent`, tangente en `Vector4`). `HasTangents` vaut `_tangentVertices.Length > 0` (`CasaEngine/Framework/Rendering/Models/StaticModelMesh.cs:15-16`, `:63`).
- `StaticModelMesh.Load` vide les tangentes et ne lit que `vertices` (`StaticModelMesh.cs:165-166`).
- `EditorAssetJsonSerializer.SaveStaticModelMesh` n'écrit que `vertices` et `indices` (`CasaEngine.EditorServices/EditorAssetJsonSerializer.cs:806-827`). Aucun accès public aux sommets avec tangentes n'existe.
- Sans tangentes, `RenderFeatureResolver` retire la normal map du dessin (`RenderFeatureResolver.AdjustToMesh`).
- `GetVector4` et `Save(this Vector4)` existent (`CasaEngine/Core/Serialization/JsonHelper.cs:102`, `CasaEngine.EditorServices/EditorJsonSaveHelper.cs:60`).
- Tests voisins : `CasaEngine.Tests/EditorServices/StaticModelAssetDocumentTests.cs` sérialise un modèle par `EditorAssetJsonSerializer.TrySerialize`.
- Branche `chantier/static-model-tangents`, créée depuis `main` (`b429b79ec`). Arbre propre.

## Décisions verrouillées

| Réf | Décision |
|---|---|
| D1 | Le format `.staticModel` stocke les tangentes (tranche S1d du plan du jeu, enveloppe approuvée, mode AUTO). |
| D2 | `main` local du moteur est sur la fusion `b429b79ec` ; ce chantier part de `main`. Pas de push, pas de fusion dans `main` sans demande. |
| D3 | Changement additif : un fichier existant se charge comme avant, un fichier sans tangentes reste sans tangentes. |

## Règles d'exécution pour l'agent

- **Branche dédiée `chantier/static-model-tangents`**, créée depuis `main`. Ne jamais committer sur `main`.
- **Une seule tâche à la fois**, icône de statut tenue à jour, **un commit par tâche** qui inclut la mise à jour de ce fichier, message en anglais `type(area): summary`.
- **Ne jamais pousser.**
- **Ne rien inventer** : sinon ⚠️ Blocked, question dans « Points ouverts », arrêt.
- **Build obligatoire** avant ✅ : `dotnet build CasaEngine.MonoGame.sln` et `dotnet build CasaEngine.Editor.MonoGame.sln` ; **tests** `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj --blame-hang-timeout 5m`.
- **Ne jamais indexer** `.serena/`, `.codegraph/` ; `git add` fichier par fichier. Aucune commande git dans `MGUI` ni `NvgSharp`.
- Rappel moteur : sérialisation additive, erreurs de données signalées avec contexte, le runtime ne dépend pas de l'éditeur.

## Format

Chaque maillage d'un `.staticModel` peut porter un tableau `tangents`, à côté de `vertices` : une entrée par sommet, dans le même ordre, objet `{ "x", "y", "z", "w" }` (`w` est le signe de la bitangente, comme dans glTF).

- Absent : le maillage n'a pas de tangentes (comportement actuel).
- Présent avec autant d'entrées que `vertices` : le maillage a des tangentes.
- Présent avec un autre nombre d'entrées : erreur de données, signalée avec le nom du maillage et les deux nombres ; le maillage se charge sans tangentes.

## Légende des statuts

- ⏳ Todo : pas encore commencé.
- 🚧 In progress : en cours de modification locale.
- 🧪 Needs testing : code écrit, validation incomplète ou en attente.
- ✅ Done : code validé, build/tests OK, commit effectué.
- ⚠️ Blocked : bloqué par une erreur non résolue ou une décision manquante.

## Validation globale

- Les deux solutions se construisent à 0 erreur.
- Tests : tous les tests verts, à l'exception des 8 tests GPU qui échouent déjà sur `origin/main` sur ce PC (7 empreintes de pixels de `DesktopFrameGpuAllocationTests` et `DrawTransactionRearmGpuTests`, et `IblCubeGeneratorGpuTests.OpenGlChain_IgnoresTheRequestedCubeLevel_SoTheAtlasIsUsed`) ; total passé de 6378 au nombre de nouveaux tests près.
- Changement additif prouvé sur un asset existant, par un test : `Projects/SampleProject/Entities/Box.staticModel` est chargé puis resérialisé par `EditorAssetJsonSerializer.TrySerialize` ; aucun maillage du résultat ne contient la clé `tangents`, chaque maillage rechargé a `HasTangents` faux, et ses sommets et indices sont ceux du fichier. Le test ne compare pas les octets du fichier : le sérialiseur actuel écrit déjà `slot_name` et `material_slot_index`, que ce fichier n'a pas.

Retour arrière : `git switch main` puis `git branch -D chantier/static-model-tangents`, sur demande de l'auteur seulement.

---

## Phase 1 — Format

### ✅ T1 — Lire et écrire les tangentes d'un `.staticModel`

- Objectif : aller-retour complet. Le runtime lit `tangents`, l'éditeur l'écrit.
- Fichiers :
  - runtime : `CasaEngine/Framework/Rendering/Models/StaticModelMesh.cs` (`Load`, et un accès en lecture aux sommets avec tangentes) ;
  - éditeur : `CasaEngine.EditorServices/EditorAssetJsonSerializer.cs` (`SaveStaticModelMesh`) ;
  - tests : `CasaEngine.Tests/EditorServices/` ou `CasaEngine.Tests/Graphics/`, à côté des tests de modèle existants.
- Étapes :
  1. `StaticModelMesh.GetTangentVertices()` : lecture seule, vide quand le maillage n'a pas de tangentes.
  2. `Load` : lit `tangents` selon la section « Format », reconstruit les sommets avec tangentes.
  3. `SaveStaticModelMesh` : écrit `tangents` seulement quand le maillage en a.
  4. Tests : un maillage avec tangentes fait l'aller-retour (positions, normales, UV, tangentes et `w` égaux, `HasTangents` vrai) ; un maillage sans tangentes n'écrit pas `tangents` et se recharge sans ; un nombre d'entrées différent se charge sans tangentes et signale l'erreur par `Logs.WriteError`, vérifiée avec un journal de capture enregistré par `Logs.AddLogger` (modèle : `CasaEngine.Tests/Dialogue/DialogueScreenReplacementTests.cs:57-72`) ; les indices et les sous-maillages ne changent pas ; l'asset existant `Projects/SampleProject/Entities/Box.staticModel` selon la validation globale.
- Validation : validation globale.
- Commit : `feat(assets): keep the tangents of a static model mesh in its asset`

> Validation (2026-11-16) : `StaticModelMesh.GetTangentVertices()` ajouté ; `Load` lit `tangents` ; `SaveStaticModelMesh` l'écrit pour un maillage qui en a. Quatre tests dans `CasaEngine.Tests/EditorServices/StaticModelTangentSerializationTests.cs` (aller-retour, sans tangentes, nombre incohérent signalé par `Logs.WriteError`, asset existant `Box.staticModel`). Les deux solutions à 0 erreur. Tests : 6382 lancés, 6374 verts, les 8 échecs GPU connus et eux seuls.

### ⏳ T2 — Décision et documentation

- Objectif : la décision de format est écrite et le format est documenté.
- Fichiers : `docs/decisions/0123-…md` (skill `adr`, numéro suivant de l'index), `docs/decisions/README.md`, la doc qui décrit `.staticModel` (`docs/editor/asset-documents.md`, section `.staticModel`), ce plan, `ai-agent/README.md`.
- Validation : liens vérifiés par `rg` ; docs seulement, pas de build.
- Commit : `docs(adr): record the tangents of the static model asset`

---

## Points ouverts

- O1 : remontée de ce chantier dans `main` et push : décision de l'auteur.

## Hors périmètre

- Calculer des tangentes absentes (c'est l'import ou la conversion qui les fournit).
- Les modèles skinnés et leur format.
- Recuire les modèles du jeu RacingGame (tranche S1e du plan du jeu).
