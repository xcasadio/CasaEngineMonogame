# Plan agent IA — Obstacles dynamiques dans l'étage champ du contrôleur de personnage

Plan d'exécution de la partie moteur de la tranche E19.d2b du portage Alundra (plan parent
`docs/plan-e19-opcodes.md`, §1.2h.2, dépôt `alundra-casaengine-project-converter`). Les décisions ci-dessous ont été
arbitrées avec l'auteur le 2026-10-02 : **ce plan les applique, il ne les rediscute pas**.

Ce fichier doit être mis à jour pendant le travail : l'icône au début de chaque tâche indique son statut courant.

## Objectif

- Permettre à un jeu de déclarer des **obstacles dynamiques** que le contrôleur de personnage respecte dans son étage
  champ, avec le **contact exact** d'ADR-0045 : un point d'extension optionnel, installé sur `World` comme
  `CollisionField`, interrogé avec l'entité qui bouge et la position candidate de sa racine.
- Publier dans `CharacterControllerContactReport`, par axe, **quel obstacle** a raccourci le pas.
- Un tracé de débogage de ces obstacles (AGENTS §9.6).

Hors périmètre : toute règle d'obstacle propre à un jeu (elle vit dans le jeu) ; le glissement le long d'un obstacle
et la division conjointe des deux axes (le contrôleur garde l'avance par axe, ADR-0045 D2) ; le balayage rigide, la
marche d'escalier et le vertical ; un registre d'obstacles natif du moteur ; l'éditeur ; une démo moteur (voir
« Points ouverts »).

## État vérifié du dépôt (2026-10-02)

- Branche `chantier/field-movement-obstacles` créée depuis `chantier/animation-logical-end-clock` (`f205683a`), elle-même
  8 commits devant `main` (`74e97293`) et pas encore mergée : le parent pointe sur `f205683a`. Merge par l'auteur :
  `chantier/animation-logical-end-clock` d'abord, puis cette branche. Modification locale de l'auteur dans
  `CasaEngine.Launcher/Program.cs` : ne jamais l'indexer.
- `CharacterControllerComponent.MoveWithCollisions` (`CasaEngine/Framework/Scene/Entities/Components/CharacterControllerComponent.cs:1026-1093`)
  appelle `ResolveHorizontalDisplacementAgainstField` (`:1108-1169`), qui teste le pas entier de h1 puis de h2 avec
  `IsHorizontalMoveBlocked` (`:1251-1286`, reçoit un **centre** de pied), puis `AdvanceBlockedAxisToContact`
  (`:1193-1249`) cherche le contact sur la **racine** : pré-sonde d'un ULP, 24 itérations, contrôle final. Ce sont
  les 5 sites de test ; aucun autre appelant dans le dépôt.
- L'étage champ sort tout de suite quand `World.CollisionField` est null (`:1123-1127`).
- `CharacterControllerContactReport` (`CharacterControllerContactReport.cs`) est une `readonly struct` à constructeur
  `internal` (`:34`), construite dans son seul fichier ; la moitié déplacement est remise à zéro à l'entrée de `Move` et
  d'`Update` ; `Stop` et `RestoreStateSnapshot` effacent tout. Aucun consommateur de production de
  `H1Curtailed`/`H2Curtailed`/`LastContact` dans le moteur.
- `World.CollisionField` (`CasaEngine/Framework/Scene/World/World.cs:50-59`) : non sérialisé ; `Clear()` (`:108`) le
  remet à null (`:138`) ; `ClearEntities()` (`:230`) le garde ; trois tests le figent (`CollisionFieldTests.cs:329-363`).
- Débogage : `PhysicsDebugViewRendererComponent` (`CasaEngine/Framework/Application/Components/Physics/`) dessine le
  monde physique quand `DisplayPhysics` est vrai ; `IPhysicsDebugDrawer.DrawLine(ref Vector3, ref Vector3, Color)`
  existe (`CasaEngine/Engine/Physics/IPhysicsDebugDrawer.cs`) ; rien ne dessine aujourd'hui le champ ni l'étage
  champ du contrôleur ; `CountingDebugDrawer` existe dans les tests (`BepuDebugDrawTests.cs:20`).
- Le balayage rigide ignore les corps sans réponse de contact (`BepuPhysicsEngine.cs:452-470`) : les entités Alundra
  sont des corps cinématiques fantômes, invisibles au balayage et à `TryStepMove`.
- Un pas de longueur ≤ 1e-3 px est abandonné (`:16`, `:1052-1055`) : il ne peut ni contourner l'étage ni s'appliquer
  brut dans un obstacle.
- Tests de l'étage champ : `CharacterControllerFieldAwareMoverTests` (22 Fact + 2 Theory), `CharacterControllerComponentTests`
  (41 Fact), `ExternalVerticalOwnership` (5), `SetVerticalVelocity` (4), `CollisionFieldTests` (19 + 3 Theory),
  `CharacterMotionSystemFixedStepTests` ; outils `AllocationWindow` et `CountingDebugDrawer`. `CasaEngine.Tests` :
  2405 réussis à `f205683a`.
- Dernière ADR : ADR-0046. Prochaine : **ADR-0047**.

## Décisions verrouillées

| Réf | Décision |
|---|---|
| D1 | Le blocage XY entre entités d'Alundra se fait par un **point d'extension du moteur** dans l'étage champ du contrôleur, dont la règle est fournie par le jeu (D-E19-27 du parent ; règle « un manque du moteur se corrige dans le moteur »). |
| D2 | Le contact contre un obstacle est **exact**, par la même bisection qu'ADR-0045 ; l'avance reste par axe (h1 puis h2) ; aucun glissement propre aux obstacles (écart au binaire accepté, E19.h du parent). |
| D3 | Le rapport de contact dit, par axe, quel obstacle a raccourci le pas (D-E19-29 du parent : le contact du dialogue vient de ce rapport). |
| D4 | Branche partie de `chantier/animation-logical-end-clock` (`f205683a`), non mergée : merge par l'auteur dans cet ordre. |

## Conception retenue

- **Interface** `IMovementObstacleProbe` (`CasaEngine/Engine/Physics/IMovementObstacleProbe.cs`, à côté de
  `ICollisionField`) :
  - `bool TryFindObstacle(Entity mover, in Vector3 candidateRootPosition, out Entity obstacle)` : vrai quand
    l'obstacle trouvé bloque la racine candidate ; contrat écrit en doc XML : O(nombre d'obstacles), **sans
    allocation**, appelable plusieurs fois par entité et par image, ne modifie rien ;
  - `void DrawDebug(IPhysicsDebugDrawer drawer) { }` : méthode par défaut vide.
- **Installation** : `World.MovementObstacleProbe` (public, non sérialisé), mêmes règles que `CollisionField` :
  `Clear()` la remet à null, `ClearEntities()` la garde.
- **Étage champ** : un test privé `IsCandidateBlocked(...)` remplace l'appel direct aux 5 sites. Il interroge **d'abord
  la sonde** avec la racine candidate (préséance du binaire : l'entité avant la case), **puis le champ** avec le centre
  recalculé comme aujourd'hui. `IsHorizontalMoveBlocked` et la bisection restent tels quels : **sans sonde installée,
  le comportement est identique au bit près**.
- **Racine candidate** : celle que la bisection construit déjà (`rootPosition + axis × montant`) ; pour le pas
  entier, `rootPosition + h × montant`. Jamais un centre reconverti (erreur d'un ULP, 4 à 8 unités 16.16 au-delà de
  512 px, qui finirait le pas dans l'obstacle).
- **Sans champ** : l'étage n'est plus court-circuité si une sonde est installée (`field == null && probe == null`) ;
  avec la sonde seule, seul l'obstacle bloque.
- **Obstacle rapporté** : pour chaque axe, l'obstacle du **dernier test bloqué** (pas entier, pré-sonde, bisection ou
  contrôle final) ; null si ce dernier test a été bloqué par le champ ou si l'axe n'a pas été raccourci. C'est
  l'obstacle le plus proche du contact, celui que nomme le binaire quand deux obstacles se suivent. Aucun appel de
  plus.
- **Rapport** : `CharacterControllerContactReport.H1Obstacle` et `H2Obstacle` (`Entity`, publics en lecture, nouveaux
  paramètres du constructeur `internal`) ; effacés avec la moitié déplacement (entrée de `Move` et d'`Update`, `Stop`,
  `Teleport`, `RestoreStateSnapshot`). `H1Curtailed`/`H2Curtailed` gardent leur sens (D-E19-11).
- **Coût** : par axe non nul, 1 appel quand l'axe est libre, 2 quand le mobile pousse un contact déjà établi
  (pas entier et pré-sonde bloqués), 26 à 30 au seul tick où le contact s'établit. Aucun pré-filtre.
- **Tracé** : `PhysicsDebugViewRendererComponent` appelle `World.MovementObstacleProbe?.DrawDebug(drawer)` sous
  `DisplayPhysics` ; un utilitaire public `DrawAabb(IPhysicsDebugDrawer, Vector3 min, Vector3 max, Color)` (12 lignes)
  sert aux implémentations.

## Règles d'exécution pour l'agent

- **Branche dédiée `chantier/field-movement-obstacles`** (déjà créée). Ne jamais committer sur `main`.
- **Une seule tâche à la fois.** Avant de commencer une tâche, remplacer son icône `⏳` par `🚧`. À la fin, lancer la
  validation indiquée, remplacer l'icône par `✅`, `🧪` ou `⚠️`, ajouter une courte note de validation sous la tâche,
  puis **créer un commit dédié** qui inclut la mise à jour de ce fichier.
- **Un commit par tâche**, atomique et compilable, message en anglais au format `type(area): summary`.
- **Ne jamais pousser.** Le merge reste une décision humaine (moteur d'abord, dans l'ordre de D4, puis le parent).
- **Ne rien inventer** : sinon ⚠️ Blocked, question dans « Points ouverts », arrêt.
- **Build** `dotnet build CasaEngine.MonoGame.sln` ; **tests** : `CasaEngine.Tests` n'est pas dans la solution, le
  builder explicitement (`dotnet build CasaEngine.Tests/CasaEngine.Tests.csproj`) puis
  `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj --no-build --blame-hang-timeout 60s`, au premier plan.
- **Tests rouges d'abord** pour T1.1 et T1.2 : ils échouent (ou ne compilent pas) avant le code.
- **Ne jamais indexer** les modifications préexistantes de l'auteur : `git add` fichier par fichier.
- **Langue** : ce plan en français ; code, commits, docs de `docs/` et ADR en anglais.
- **Arrêts** : une valeur écrite d'avance contredite (ne jamais la ré-épingler : les valeurs ci-dessous viennent d'une
  réplique float32 du code lu, pas d'une exécution du moteur) ; un test existant qui bouge ; une allocation dans
  `Move` avec une sonde installée ; deux échecs sur une même tâche.

## Légende des statuts

- ⏳ Todo : pas encore commencé.
- 🚧 In progress : en cours de modification locale.
- 🧪 Needs testing : code écrit, validation incomplète ou en attente.
- ✅ Done : code validé, build/tests OK, commit effectué.
- ⚠️ Blocked : bloqué par une erreur non résolue ou une décision manquante.

## Validation globale

- `dotnet build CasaEngine.MonoGame.sln` : 0 erreur.
- `CasaEngine.Tests` : base 2405 réussis, 0 échec ; après le chantier, la base plus les nouveaux tests, 0 échec,
  aucun test existant modifié.
- La règle d'obstacle d'Alundra, les arcs et la recette sont vérifiés dans le plan parent (E19.d2b).

---

## Phase 0 — Cadre

### ⏳ T0.1 — ADR-0047

- Objectif : enregistrer la décision avant le code.
- Fichiers : `docs/decisions/0047-dynamic-movement-obstacles-in-the-character-controller-field-stage.md` (nouveau),
  ligne de `docs/decisions/README.md`.
- Contenu : contexte (entités sans blocage dans l'étage champ, contact exact d'ADR-0045, besoin d'Alundra), décision
  (conception retenue ci-dessus), conséquences (sans sonde rien ne change ; avance par axe gardée, pas de glissement
  propre ; rapport additif ; coût par appels ; tracé).
- Validation : relecture.
- Commit : `docs(adr): add ADR-0047 on dynamic movement obstacles in the character controller field stage`

## Phase 1 — Point d'extension

### ⏳ T1.1 — Interface et installation sur `World`

- Fichiers : `CasaEngine/Engine/Physics/IMovementObstacleProbe.cs` (nouveau), `CasaEngine/Framework/Scene/World/World.cs`,
  tests dans `CasaEngine.Tests/Physics/` (même fichier que les tests de `World.CollisionField`, ou un fichier voisin).
- Tests **T-ENG-9** (vie du `World`) : défaut null ; accepte une sonde ; `ClearEntities()` la garde ; `Clear()` la met
  à null.
- Commit : `feat(physics): add an optional movement obstacle probe on World`

### ⏳ T1.2 — Étage champ et rapport

- Fichiers : `CharacterControllerComponent.cs`, `CharacterControllerContactReport.cs`, tests dans
  `CasaEngine.Tests/Physics/` (nouveau fichier `CharacterControllerMovementObstacleTests.cs`).
- Montage commun des tests : pion à boîte 20 × 14 × 32 (demi-étendues 10 × 7), racine posée sur un champ plat de
  hauteur 48 sauf mention ; sonde de test à boîtes semi-ouvertes `[min ; max)` en X, Y et Z, qui compte ses appels et
  garde l'entité reçue ; aucune allocation par appel.
- Valeurs écrites d'avance :
  - **T-ENG-1** contact exact et rapport : racine (968 ; 552 ; 48), obstacle `[949,5 ; 969,5) × [497 ; 511) × [48 ; 80)`,
    `Move(0, -0,5, 0)` puis `Move(0, -1, 0)` à chaque tick : Y = 551,5 après le pas 1 (1 appel) ; 550,5 après le pas 2 ;
    518,5 après le pas 34 (1 appel) ; **518,0 exactement** après le pas 35, déplacement publié -0,5, `H2Curtailed`
    vrai, `H2Obstacle` = l'obstacle, 26 à 30 appels (27 attendus) ; pas 36 : Y = 518,0, déplacement 0, `H2Obstacle`
    posé, 2 appels. 518,0 est libre et `BitDecrement(518,0)` est bloqué.
  - **T-ENG-2** l'affleurant ne bloque pas : obstacle `[938 ; 958) × [497 ; 511) × [48 ; 80)`, départ (968 ; 551,5),
    80 fois `Move(0, -1, 0)` : Y finale 471,5, jamais de `H2Obstacle`, 80 appels.
  - **T-ENG-3** exactitude aux fenêtres de puissance de deux : champ plat de hauteur 0, obstacle
    `[0 ; 5000) × [top - 40 ; top) × [0 ; 1000)`, pion en (60 ; départ ; 0), `Move(0, -10, 0)` : top 240, départs 255,5,
    255,75 et 255,9995 → Y 247,0 ; top 1008, départs 1023,5, 1023,75 et 1023,9995 → 1015,0. Vers l'ouest
    (`Move(-10, 0, 0)`, obstacle `[edge - 40 ; edge)` en X) : edge 240, départs 255,5 et 255,9995 → X 250,0 ; edge
    1008, départs 1023,5 et 1023,9995 → 1018,0. Dans chaque cas, la position atteinte est libre et le flottant suivant
    vers l'obstacle est bloqué.
  - **T-ENG-4** départ en chevauchement (obstacle de T-ENG-1, pion en (968 ; 515 ; 48)) : `Move(0, +1, 0)` et
    `Move(0, +2, 0)` → Y 515,0, déplacement 0, `H2Obstacle` posé, 2 appels ; `Move(0, +3, 0)` → 518,0, déplacement 3,0,
    sans obstacle ; `Move(0, +4, 0)` depuis 515 → 519,0 ; `Move(0, -1, 0)` depuis 515 → 515,0 bloqué. (Un mobile qui
    chevauche ne sort que par un pas qui quitte entièrement le chevauchement, comme le binaire : D-E19-36.)
  - **T-ENG-5** diagonales et sources : (a) pion (930 ; 504 ; 48), `Move(+25, -1, 0)` → (939,5 ; 503,0), déplacement
    (9,5 ; -1,0), `H1Curtailed` et `H1Obstacle`, h2 libre, 28 appels ; (b) pion (945 ; 520 ; 48), `Move(+2, -5, 0)` →
    (947,0 ; 518,0), déplacement (2,0 ; -2,0), `H2Curtailed` et `H2Obstacle`, 28 appels ; (c) mur de champ sur la rangée
    `[224 ; 240)` et obstacle `[0 ; 5000) × [192 ; 216)`, pion (60 ; 260 ; 0), `Move(0, -60, 0)` → Y 247,0, déplacement
    -13,0, `H2Curtailed` vrai, `H2Obstacle` null (le champ a bloqué en dernier) ; (d) obstacle `[0 ; 5000) × [232 ; 244)`
    et le même mur, `Move(0, -20, 0)` → Y 251,0, `H2Obstacle` = l'obstacle ; (e) obstacle de (d) sans le mur,
    `Move(0, -60, 0)` → Y 200,0, aucun axe raccourci (seul le point d'arrivée est testé, comme le champ et le binaire :
    documenté).
  - **T-ENG-6** budget d'appels (sonde comptante) : axe libre exactement 1 ; mobile au contact qui pousse exactement
    2 ; tick de contact entre 26 et 30 ; pas libre sur x et y non nuls : 2 ; l'entité reçue est celle du contrôleur ; la
    racine candidate garde le X (pour h2) et le Z de la racine.
  - **T-ENG-7** sans sonde rien ne change : suite existante inchangée ; plus une sonde jamais bloquante dont les racines
    finales sont égales au bit près à celles du chemin sans sonde sur 100 pas variés.
  - **T-ENG-8** zéro allocation (`AllocationWindow`) : 100 `Move` avec la sonde comptante allouent autant que 100 `Move`
    sans sonde (écart 0).
  - **T-ENG-10** sans champ, sonde seule : montage de T-ENG-1 sans `CollisionField` → Y 518,0 au pas 35.
  - **T-ENG-11** chemin `Update` : racine (968 ; 520 ; 48), gravité nulle, `MaxHorizontalSpeed` 500, `Acceleration`
    100000, intention (0 ; 1), `Update(0,02)` → pas demandé -10, Y 518,0, `H2Curtailed` et `H2Obstacle`, `Velocity.Y`
    proche de -100 (précision 1) ; `FixedTimeStep` 0,02 avec une image de 0,04 → 2 pas, la sonde vue à chaque pas.
  - **T-ENG-12** effacement : `H1Obstacle`/`H2Obstacle` null après `Stop`, `Teleport`, `RestoreStateSnapshot`, et
    après un `Move`/`Update` sans déplacement (étendre les tests existants de l'effacement du rapport).
- Commit : `feat(physics): stop the character controller field stage at dynamic movement obstacles`

### ⏳ T1.3 — Tracé de débogage

- Fichiers : `PhysicsDebugViewRendererComponent.cs`, l'utilitaire `DrawAabb` (classe statique publique à côté de
  `IPhysicsDebugDrawer`), test.
- **T-ENG-13** : `DrawAabb` sur un `CountingDebugDrawer` émet 12 `DrawLine` dont les extrémités sont les 8 sommets
  attendus ; le composant appelle `DrawDebug` de la sonde installée seulement quand `DisplayPhysics` est vrai.
- Commit : `feat(physics): draw movement obstacle probes in the physics debug view`

## Phase 2 — Documentation

### ⏳ T2.1 — Docs

- Fichiers : `docs/engine/character-controller-features.md`, `docs/engine/collision-2d-3d-architecture.md` (et leur index
  si une ligne change), ligne de ce plan dans `ai-agent/README.md`.
- Contenu : la sonde, son contrat, l'ordre sonde puis champ, le contact exact, le rapport par axe, le coût, le tracé,
  les limites (avance par axe, pas de glissement propre, seul le point d'arrivée est testé).
- Commit : `docs(physics): describe dynamic movement obstacles in the controller docs`

---

## Points ouverts

- **Démo moteur** (AGENTS §6) : le point d'extension n'est pas une fonction visible en soi ; son tracé l'est. Le plan
  propose le tracé (T1.3) et la recette Alundra du parent comme échantillon, sans démo dans `CasaEngine.Demos`. À
  confirmer par l'auteur à l'approbation ; une démo s'ajoute en tâche T1.4 si elle est demandée.
- **Pas fixe** : sous `FixedTimeStep > 0`, les sous-pas d'une image voient l'état des obstacles du moment de chaque
  appel ; sans effet pour Alundra (pas fixe à 0), documenté.
