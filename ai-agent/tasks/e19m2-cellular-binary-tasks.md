# Plan agent IA — Couches de cellules comme le binaire (période, position dessinée, tirage de la réapparition)

Plan d'exécution de la partie moteur de la tranche E19.m2 du portage Alundra (plan parent `docs/plan-e19-opcodes.md`, §1.2s.3,
dépôt `alundra-casaengine-project-converter`). **Approuvé : plan parent §1.2s.3 READY le 2026-10-03, travail dans le moteur
autorisé par l'auteur le 2026-10-03.** Les décisions ci-dessous viennent du plan parent (D-E19-66, règles M2-R1 à M2-R3) : **ce plan
les applique, il ne les rediscute pas**.

Ce fichier doit être mis à jour pendant le travail : l'icône au début de chaque tâche indique son statut courant.

## Objectif

`CellularLayerService` se comporte comme les deux routines de cellules du binaire (`ALUN_CD.EXE`, France) :

- la période avance une cellule d'un pas tous les `|P| + 2` ticks (et non tous les `|P|`) ;
- la position dessinée est celle d'avant le bouclage et la réapparition (la cellule est hors écran un tick) ;
- la réapparition d'une cellule `FallRespawn` pose `posX = rand() / 102`, où le délégué injecté rend la prochaine valeur du
  `rand()` de la bibliothèque C (0 à 0x7FFF), division signée tronquée.

## Hors périmètre

- Le générateur lui-même (il vit dans la DLL du jeu, `AlundraLibcRandom`, tranche M2-3/M2-4 du plan parent).
- Les autres écarts relevés (compteur de vagues global, parallaxe tronquée du type 0, ordre de dessin d'une couche) : O-E19-51 à
  O-E19-53 du plan parent, tranche E19.m3.
- `ScrollingLayerService` (son rythme `|P|` est celui du binaire, `0x8005C7E0`).

## État vérifié du dépôt (2026-10-03)

- Branche `chantier/e19m2-cellular-binary` créée depuis `chantier/e19k2-layer-mask` (`987f0c7f`) : le pointeur du sous-module du
  parent ne peut désigner qu'un commit et doit garder E19.k2. Modification locale de l'auteur dans
  `CasaEngine.Launcher/Program.cs` : ne jamais l'indexer.
- Période : `CellularLayerService.cs:303`, `:313`, `:364`, `:371` (`++Tick >= |P|`).
- Position dessinée : recalculs `:328/333/341/346` puis `:349-350` (Normal), `:385/390/399-400` puis `:403-404` (FallRespawn).
- Tirage : `:397` `(ulong)next() * 320 >> 32`.
- Tests existants touchés (liste fermée) : `CellularLayerServiceTests.cs:111-128`, `:132-192`, `:196-219`.

## Décisions verrouillées

| Réf | Décision |
|---|---|
| M2-R1 | Période : aux quatre endroits, le pas s'applique quand le compteur d'avant l'incrément dépasse `|P|`, puis le compteur repasse à 0 (`if (cell.TickX++ > Math.Abs(PeriodX)) { ... TickX = 0; }`, de même en Y) ; doc de `CellularCellDefinition.cs` et `docs/engine/cellular-layers.md` §4. |
| M2-R2 | Position dessinée : `DrawX` reçoit `sx` juste avant le bouclage en X, `DrawY` reçoit `sy` juste avant le bouclage en Y ou la réapparition ; les recalculs d'après sont retirés. |
| M2-R3 | Réapparition : `cell.PosX = (int)next() / 102` ; le délégué garde sa signature `Func<uint>`, sa doc dit « la prochaine valeur de `rand()` de la bibliothèque C, 0 à 0x7FFF » (`CellularLayerService.cs`, `CellularLayerComponent.cs`, `cellular-layers.md` §5 et §12) ; ADR-0050. |

## Règles d'exécution pour l'agent

- **Branche dédiée `chantier/e19m2-cellular-binary`**, créée depuis `chantier/e19k2-layer-mask`. Ne jamais committer sur `main`.
- **Une seule tâche à la fois**, statut mis à jour dans le même commit que la tâche. Tests d'abord : rouge constaté, puis vert.
- **Un commit par tâche**, message en anglais `type(area): summary`. **Ne jamais pousser, ne jamais merger.**
- **Ne jamais indexer** `CasaEngine.Launcher/Program.cs` ; `git add` fichier par fichier.
- Une valeur écrite d'avance que la mesure contredit est un ARRÊT ; un test existant hors de la liste fermée qui change ou rougit
  aussi.
- `CasaEngine.Tests` n'est pas dans le `.sln` : le construire explicitement.
- Langue : plan en français ; code, commits, docs et ADR en anglais (`docs/engine/cellular-layers.md` reste dans sa langue).

## Légende des statuts

- ⏳ Todo · 🚧 In progress · 🧪 Needs testing · ✅ Done · ⚠️ Blocked

## Validation globale

- `dotnet build CasaEngine.Tests/CasaEngine.Tests.csproj -c Debug` sans erreur.
- `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj -c Debug --no-build --blame-hang-timeout 300s` tout vert (un test de
  matériaux est connu pour être instable).
- Recette en jeu : par le plan parent (E19.m2).

---

## Phase 1 — Moteur

### ✅ M2-1 — Tests d'abord (rouges sur le code d'avant)

- Objectif : épingler M2-R1 à M2-R3 par des tests dont les valeurs sont écrites d'avance, un tick à la fois.
- Fichiers : `CasaEngine.Tests/Rendering/CellularLayers/CellularLayerServiceTests.cs`.
- Valeurs :
  - T-P1 : Normal, x0 100, u1 1000, période X 1 : `DrawX` 100, 101, 102 aux ticks 1, 3, 6 (aujourd'hui 101, 103, 106).
  - T-P2 : Normal, y0 100, v1 1000, période Y −2 : `DrawY` 100, 99, 98 aux ticks 2, 4, 8 (aujourd'hui 99, 98, 96).
  - T-P3 : `FallRespawn`, x0 100, u0 = u1 = 0, période X 2 : `DrawX` 100, 101, 102 aux ticks 2, 4, 8 (aujourd'hui 101, 102, 104).
  - Bouclages : x0 −20 → −20 puis 315 ; x0 400 → 400 puis 65 ; y0 −20 → −20 puis 235 ; y0 400 → 400 puis 145 (aujourd'hui la valeur
    bouclée dès le tick 1).
  - T-W5 : x0 −13, dx −1 : ticks 1 à 4 : −14, −15, −16, 318 (aujourd'hui −14, −15, 319, 318).
  - T-F1 (remplace `:196-219`) : `FallRespawn` x0 7, y0 232, dy 8, u1 = v1 = 15, source 16320 : tick 1 (7, 240), un appel ;
    tick 2 (160, −7) ; tick 3 (160, 1) (aujourd'hui (0, −15) au tick 1). T-F2 : source 32767 : (7, 240), (321, −7), (−14, 1) ;
    source 32640 : DrawX 320 au tick 2.
  - `:111-128` réécrit : 12 ticks, 87 (OU de signes ; le OU EXCLUSIF donnerait 89).
- Validation faite : sur le code d'avant, 13 tests rouges sur 55 (`FullyQualifiedName~CellularLayerServiceTests`), toutes les valeurs
  lues égales aux valeurs « aujourd'hui » du plan : T-P1 tick 1 attendu 100 lu 101 ; T-P2 tick 2 attendu 100 lu 99 ; T-P3 tick 2
  attendu 100 lu 101 ; bouclages tick 1 attendu −20 lu 315, attendu 400 lu 65, attendu −20 lu 235, attendu 400 lu 145 ; T-W5 tick 3
  attendu −16 lu 319 ; T-F1 tick 1 attendu (7, 240) lu (0, −15) ; T-F2 tick 2 : 32767 attendu 321 lu 0, 32640 attendu 320 lu 0, 102
  attendu 1 lu 0 (101 → 0 vert d'avance). Le test `:111-128` réécrit (12 ticks, 87) était vert d'avance, comme annoncé. Tests écrits
  dans `CellularLayerServiceTests.cs` (+9 net). Aucune valeur écrite d'avance contredite.
- Commit : avec M2-2 (le commit de tests seuls serait rouge).

### ✅ M2-2 — Code

- Objectif : M2-R1 à M2-R3 dans `CellularLayerService`, docs (`CellularCellDefinition.cs`, `CellularLayerComponent.cs`,
  `cellular-layers.md`).
- Validation faite : tests de M2-1 verts (55 sur 55 en filtre), `CasaEngine.Tests` 2511 sur 2511 (Debug, 2502 avant, +9), aucun test
  hors de la liste fermée touché. Un seul commit vert (tests, code et docs ensemble : séparer la période et la position dessinée du
  tirage aurait laissé des tests rouges).
- Commit : `fix(rendering): cellular layers follow the original's period, drawn position and C library rand respawn`.

### ⏳ M2-2b — ADR-0050

- Objectif : ADR du moteur, `Accepted` (anglais), ligne à l'index `docs/decisions/README.md`.
- Commit : `docs(adr): ADR-0050 cellular layers follow the original's period, drawn position and C library rand`

---

## Points ouverts

| Réf | Sujet | Tâche concernée |
|---|---|---|
| — | Aucun. | — |
