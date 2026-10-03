# Plan agent IA — Masque des couches de fond (défilement et cellulaire)

Plan d'exécution de la partie moteur de la tranche E19.k2 du portage Alundra (plan parent `docs/plan-e19-opcodes.md`, §1.2k.2,
dépôt `alundra-casaengine-project-converter`). **Approuvé : plan parent §1.2k.2 READY le 2026-10-03, travail dans le moteur
autorisé par l'auteur le 2026-10-03.** Les décisions ci-dessous viennent du plan parent : **ce plan les applique, il ne les
rediscute pas**.

Ce fichier doit être mis à jour pendant le travail : l'icône au début de chaque tâche indique son statut courant.

## Objectif

Les services `ScrollingLayerService` et `CellularLayerService` savent activer ou désactiver une couche par son identifiant
(celui que porte déjà sa définition). Une couche inactive est **figée** (aucun état par tick n'avance : cadence d'animation,
défilement automatique, vagues, tirage aléatoire) et **non dessinée**. Le jeu (la DLL Alundra) traduit l'opcode `0xA4` en
appels à cette API.

## Hors périmètre

- Le cycle de palettes (`0xA4` avec banque > 0) : consigné dans le plan parent (O-E19-43), non porté.
- Toute règle propre à Alundra (elle vit dans le jeu).

## État vérifié du dépôt (2026-10-03)

- Branche `chantier/e19k2-layer-mask` créée depuis `chantier/e19s-virtual-resolution` (`dfaed7a6`) : le pointeur du sous-module
  du parent ne peut désigner qu'un commit et doit garder E19.s. Modification locale de l'auteur dans
  `CasaEngine.Launcher/Program.cs` : ne jamais l'indexer.
- Aucune couche active ou inactive dans `ScrollingLayerService` ni dans `CellularLayerService` (lu dans les deux fichiers).
- Identifiants portés par les définitions : `ScrollingLayerDefinition.StableId`, `CellularLayerDefinition.LayerId`.
- `ScrollingLayerComponent.Submit` et `CellularLayerComponent.Submit` bouclent sur les couches par index.

## Décisions verrouillées

| Réf | Décision |
|---|---|
| K2-R1 | `ScrollingLayerService.SetLayerActive(int stableId, bool active)` et `CellularLayerService.SetLayerActive(int layerId, bool active)` : chaque couche dont l'identifiant correspond prend l'état ; une couche inactive n'avance pas (ni `AdvanceLayerOneTick`, ni vagues, ni tirage aléatoire, son état figé) et ne se dessine pas ; un identifiant absent est sans effet ; `SetLayers` et `Clear` remettent toutes les couches actives. |

## Règles d'exécution pour l'agent

- **Branche dédiée `chantier/e19k2-layer-mask`**, créée depuis `chantier/e19s-virtual-resolution`. Ne jamais committer sur `main`.
- **Une seule tâche à la fois**, statut mis à jour dans le même commit que la tâche. Tests d'abord : rouge constaté, puis vert.
- **Un commit par tâche**, message en anglais `type(area): summary`. **Ne jamais pousser, ne jamais merger.**
- **Ne jamais indexer** `CasaEngine.Launcher/Program.cs` ; `git add` fichier par fichier.
- `CasaEngine.Tests` n'est pas dans le `.sln` : le construire explicitement.
- Langue : plan en français ; code, commits, docs et ADR en anglais.

## Légende des statuts

- ⏳ Todo · 🚧 In progress · 🧪 Needs testing · ✅ Done · ⚠️ Blocked

## Validation globale

- `dotnet build CasaEngine.Tests/CasaEngine.Tests.csproj -c Debug` sans erreur.
- `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj -c Debug --no-build --blame-hang-timeout 300s` tout vert (un test de
  matériaux est connu pour être instable).
- Recette en jeu : par le plan parent (E19.k2, recette K2-4).

---

## Phase 1 — Moteur

### ✅ K2-1 — Masque des couches, tests d'abord

- Objectif : K2-R1 dans les deux services et les deux composants, tests à côté de `ScrollingLayerServiceTests` et
  `CellularLayerServiceTests`.
- Validation : défilement, deux couches d'identifiants 0 et 1 avec défilement automatique et cadence : `SetLayerActive(1, false)`
  puis trois ticks, la couche 0 avance, la couche 1 garde ses compteurs et n'est pas soumise ; `true` : les deux avancent ; une
  seule couche d'identifiant 1 : `SetLayerActive(0, false)` sans effet, `SetLayerActive(1, false)` la fige et la cache ;
  `SetLayers` après un masque : toutes actives. Cellulaire, une couche d'identifiant 0 : `false` puis trois ticks, positions,
  vagues et générateur aléatoire inchangés (aucun tirage), aucune soumission ; `true` : elle reprend.
- Validation faite : API posée d'abord en talon sans effet (`SetLayerActive` ne fait rien, `IsLayerActive` rend vrai), puis 12 tests
  (`ScrollingLayerMaskTests`, `CellularLayerMaskTests`) : 8 rouges (le premier assert lu est `IsLayerActive` faux attendu, vrai lu, ou
  un compteur attendu 0 lu 2, ou une soumission attendue vide non vide), 4 verts d'avance (`SetLayers` et `Clear` remettent tout actif,
  vrais par construction avec le talon). Puis implémentation : toutes les valeurs écrites tenues, aucune ré-épinglée. `CasaEngine.Tests`
  2502 sur 2502 (Debug, 2490 avant, +12), aucun test existant touché. Une couche inactive est figée en entier (cadence, défilement
  automatique, décalages, vagues, positions des cellules, aucun tirage aléatoire) et `IsLayerActive` rend faux pour un index sans couche.
- Commit : `feat(rendering): scrolling and cellular layers can be switched off, frozen and not drawn`

### ✅ K2-1b — ADR

- Objectif : ADR du moteur (le prochain numéro libre), `Accepted`, et sa ligne à l'index.
- Validation faite : ADR-0049 (`Accepted`, en anglais) et sa ligne à l'index ; sections ajoutées à `docs/engine/scrolling-layers.md` (§9) et `docs/engine/cellular-layers.md` (§13), et la limite devenue fausse du premier retirée.
- Commit : `docs(adr): ADR-0049 background layers can be switched off, frozen and not drawn`

---

## Points ouverts

| Réf | Sujet | Tâche concernée |
|---|---|---|
| — | Aucun. | — |
