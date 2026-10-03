# Plan agent IA — Couches cellulaires : compteur des vagues, parallaxe du type 0, ordre des cellules

Plan d'exécution de la partie moteur de la tranche E19.m3 du portage Alundra (plan parent `docs/plan-e19-opcodes.md`, §1.2s.4,
dépôt `alundra-casaengine-project-converter`). **Approuvé : plan parent §1.2s.4 READY (relecture n°2) le 2026-10-03, travail dans
le moteur autorisé par l'auteur le 2026-10-03.** Les règles ci-dessous viennent du plan parent : **ce plan les applique, il ne les
rediscute pas**. Règle de l'auteur : le binaire (`ALUN_CD.EXE`, France) tranche.

Ce fichier doit être mis à jour pendant le travail : l'icône au début de chaque tâche indique son statut courant.

## Objectif

Trois écarts du `CellularLayerService` et du `CellularLayerComponent` avec le binaire, relevés par l'audit d'E19.m3 (O-E19-51 à
O-E19-53) : le compteur des vagues (un seul pour tout le service), le facteur de parallaxe des cellules normales (tronqué une
fois), l'ordre de dessin des cellules d'une couche (la cellule 0 dessus).

## Hors périmètre

- Le code de la DLL Alundra (le parent ne change que le pointeur du sous-module et son plan).
- La pluie opaque de la 391 (O-E19-54), les fondus de passage (O-E19-55), le décalage de palette du type 2 (O-E19-56).
- Un tri stable de la file de sprites.

## État vérifié du dépôt (2026-10-03)

- Branche `chantier/e19m3-cellular-order` créée depuis `chantier/e19g2a-psx-semi` (`b5a9fbcf`) : le pointeur du sous-module du
  parent ne peut désigner qu'un commit et doit garder G2a. Modification locale de l'auteur dans
  `CasaEngine.Launcher/Program.cs` : ne jamais l'indexer, ne jamais la modifier.
- `CellularLayerService.cs` : `LayerRuntime.WaveTick` par couche, incrémenté dans `AdvanceLayerOneTick`, remis à 0 par
  `SetLayers` et `ResetLayerRuntimeState` ; `ComputeCameraBase` (`cam * num / den`) pour les cellules normales et `FallRespawn`.
- `CellularLayerComponent.Submit` : une clé de tri par couche, `LocalSortOffset` 0, pour toutes ses cellules.
- `SpriteRendererComponent.FillVertices` (interne, `InternalsVisibleTo` des tests) trie `_spriteDatas` par `List.Sort`.

## Décisions verrouillées

| Réf | Décision |
|---|---|
| M3-R1 | Compteur des vagues : un octet au niveau du service remplace le compteur par couche ; +1 par tick avant la boucle des couches, dès que `SetLayers` a été appelé depuis le dernier `Clear` (même liste vide) ; un service neuf et `Clear` ferment la porte ; `SetLayers`, `Clear` et `ResetLayerRuntimeState` ne le remettent jamais à 0 ; le masque ne le fige pas ; `TryGetLayerState` le rapporte ; aucune API publique nouvelle ; ADR-0052 amende ADR-0049 (vagues seulement). |
| M3-R2 | Parallaxe : cellules normales seulement, `den != 0 ? cam * (num / den) : 0` en X et en Y (division entière tronquée) ; `FallRespawn` garde `ComputeCameraBase`. |
| M3-R3 | Ordre : une clé par cellule, `LocalSortOffset = -c` (c = indice de cellule) ; la cellule 0 est dessinée en dernier ; aucun tri stable. Couches de (passe, `SortingLayer`, `OrderInLayer`) distincts : ordre entre couches inchangé ; deux couches qui partagent ces trois champs voient leurs cellules entrelacées par indice. |

## Règles d'exécution pour l'agent

- **Branche dédiée `chantier/e19m3-cellular-order`**. Ne jamais committer sur `main`. Ne jamais pousser, ne jamais merger.
- Une seule tâche à la fois, statut mis à jour dans le même commit que la tâche. Tests d'abord : rouge constaté (valeurs lues
  égales aux valeurs « aujourd'hui » écrites), puis vert. Une valeur écrite que la mesure contredit est un arrêt.
- Liste fermée des tests existants qui bougent : `CellularLayerMaskTests` (`:93` réécrit, `:101`, `:122`, `:133`) et
  `CellularLayerServiceTests` (`:487`) ; tout autre test qui change ou rougit est un arrêt.
- **Ne jamais indexer** `CasaEngine.Launcher/Program.cs` ; `git add` fichier par fichier.
- `CasaEngine.Tests` n'est pas dans le `.sln` : le construire explicitement.
- Langue : plan en français ; code, commits, docs et ADR en anglais.

## Légende des statuts

- ⏳ Todo · 🚧 In progress · 🧪 Needs testing · ✅ Done · ⚠️ Blocked

## Validation globale

- `dotnet build CasaEngine.Tests/CasaEngine.Tests.csproj -c Debug` sans erreur.
- `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj -c Debug --no-build --blame-hang-timeout 300s` tout vert (un test de
  matériaux est connu pour être instable).
- Recette en jeu : par le plan parent (E19.m3).

---

## Phase 1 — Moteur

### ✅ M3-1a — Compteur des vagues global (M3-R1), tests d'abord

- Valeurs écrites (table des vagues `lut[i] = i`, `BWavePhase` 1, `BWaveWeight` 128, cellule de vague `X0 = Y0 = 50`, donc
  `DrawX = 42 + compteur`) : `SetLayers`, 3 ticks, `SetLayers`, 1 tick → 4 / 46 (aujourd'hui 1 / 43) ; 2 ticks, `Clear`, 3 ticks,
  `SetWaveLut`, `SetLayers`, 1 tick → 3 / 45 (1 / 43) ; 5 ticks, `ResetLayerRuntimeState`, 1 tick → 6 / 48 (1 / 43) ; deux couches,
  couche 0 masquée 2 ticks, démasquée, 1 tick → 3 / 45 pour les deux (1 / 43 et 3 / 45) ; test du masque avec la table : 1 tick,
  masque, 3 ticks, reprise, 1 tick → 5 / 47 (2 / 44) ; `SetLayers(L)`, 1 tick, `SetLayers([])`, 4 ticks, `SetLayers(L)`, 1 tick →
  6 / 48 (1 / 43).
- Tests existants touchés : voir la liste fermée.
- Validation faite : nouveau fichier `CellularLayerWaveCounterTests` (6 tests) et les cinq assertions de la liste fermée. Rouge lu
  sur le code d'avant, égal aux valeurs « aujourd'hui » écrites : 1 / 43 (recharge, `Clear`, remise à zéro, liste vide, couche 0
  masquée), 2 / 44 (reprise du masque, avec la table), `:122` attendu 3 lu 1, `:133` attendu 2 lu 0, `:93` attendu 4 lu 1,
  `:487` attendu 5 lu 0 ; `:101` n'est pas atteint tant que `:93` échoue (valeur d'aujourd'hui 2, écrite au plan). Vert après :
  61 sur 61 aux tests `CellularLayers`.
- Commit : `fix(rendering): one global wave counter like the original`

### ⏳ M3-1b — Parallaxe tronquée du type 0 (M3-R2), tests d'abord

- Valeurs écrites (cellule normale en (100, 100), un tick) : facteur 1/2, caméra (100, 60) → (100, 100) (aujourd'hui (50, 70)) ;
  3/2, caméra (10, 10) → (90, 90) ((85, 85)) ; −1/2, caméra (10, 10) → (100, 100) ((105, 105)) ; gardes : 1/1 caméra (37, 21) →
  (63, 79) et 2/1 caméra (20, 10) → (60, 80) inchangés ; `FallRespawn` 1/2, caméra (100, 60) → (50, 70) inchangé.
- Commit : `fix(rendering): type-0 cells use a truncated parallax factor`

### ⏳ M3-1c — Cellule 0 dessinée dessus (M3-R3), tests d'abord

- Valeurs écrites : trois cellules → `LocalSortOffset` 0, −1, −2 et des `CompareTo` strictement ordonnés (aujourd'hui tous 0) ;
  `[Normal, ScriptTrack, Normal]` → décalages 0 et −2 ; garde : couche 0 (`OrderInLayer` 1) et couche 1 (`OrderInLayer` 0), trois
  cellules chacune → toute clé de la couche 1 se trie avant toute clé de la couche 0 ; après `FillVertices`, l'ordre des cellules
  est 2, 1, 0 (aujourd'hui 0, 1, 2).
- Commit : `fix(rendering): cell 0 of a cellular layer is drawn on top`

### ⏳ M3-2 — Docs et ADR-0052

- Docs : `CellularLayerState.cs`, `CellularLayerService.cs` (résumés), `CellularCellDefinition.cs`, `cellular-layers.md` (§3, §4,
  §5, §13) ; ADR-0052 (`Accepted`), ADR-0049 reçoit « Amended by ADR-0052 » ; ligne dans `docs/decisions/README.md` et dans
  `ai-agent/README.md`.
- Commit : `docs(adr): ADR-0052 cellular wave counter, truncated type-0 parallax and cell order`

---

## Points ouverts

| Réf | Sujet | Tâche concernée |
|---|---|---|
| — | Aucun. | — |
