# Plan agent IA — Un pas bloqué sur le champ de cellules avance jusqu'au contact

Plan d'exécution de la part moteur de la tranche E19.a2 du portage Alundra (plan parent
`docs/plan-e19-opcodes.md`, étape E19 de `docs/plan-conversion-totale.md`).
Les décisions D1 → D4 ci-dessous ont été arbitrées avec l'auteur le 2026-09-29 : **ce plan les applique, il ne les rediscute pas**.

Ce fichier doit être mis à jour pendant le travail : l'icône au début de chaque tâche indique son statut courant.

> **Statut** : proposé le 2026-09-29, **approuvé par l'auteur le 2026-09-29** (exécution en mode ASK).
> Relecture de plan : **READY**.
> Audit des citations et de la faisabilité en parallèle : aucun P1. Ses P2 et P3 sont intégrés :
> géométrie du test aux puissances de deux, aides de test, contrat de la bisection, limites acceptées
> P5, citations et langue des documents. Relecture neuve de clôture : **REVISE**, deux P2 corrigés
> mot pour mot, dont un dans ce plan : la grille doit contenir le coin lointain de l'empreinte de
> départ. Le plafond de relecture est atteint ; cette version n'est pas relue à nouveau.
>
> Fichier non commité : le sous-module est sur `main` avec une modification en cours de l'auteur
> (`CasaEngine.Launcher/Program.cs`). Aucune branche n'est donc créée avant l'approbation, pour ne
> pas déplacer son checkout. Le fichier sera commité en T0.1, sur la branche du chantier.

## Objectif

Sur le champ de cellules (`World.CollisionField`), un pas horizontal bloqué avance jusqu'au contact
au lieu d'être rejeté en entier. C'est ce que font les contrôleurs de personnage courants, et ce que
fait l'original d'Alundra (`ComputeXYPosition`, `0x80037730`) : il coupe la force en deux jusqu'à la
limite de précision.

Le défaut a été trouvé en jeu. Dans la cabine de la carte 390, le script fait marcher le héros de
80 px exactement jusqu'à une butée. Aujourd'hui le contrôleur s'arrête 1,34 px avant, et le script
attend indéfiniment. Posé au point de contact de l'original (y = 215,0), le héros finit la scène
(plan parent, E19.a, « Recette en jeu T7 »).

Ce chantier ne touche ni au balayage contre les obstacles rigides, qui va déjà au contact, ni à
l'interface `ICollisionField`.

## État vérifié du dépôt (2026-09-29)

- `CharacterControllerComponent.Move` appelle `MoveWithCollisions`, qui passe d'abord le champ de
  cellules (`ResolveHorizontalDisplacementAgainstField`,
  `CasaEngine/Framework/Scene/Entities/Components/CharacterControllerComponent.cs:1102-1157`), puis
  le balayage rigide sur le déplacement déjà raccourci (`:1047-1086`). Un contournement sans aucun
  test de champ existe pour un déplacement de moins de 1e-3 px, ou sans monde physique, collision
  ou forme (`:1032-1037`).
- Le champ teste h1 puis h2. Pour chaque axe, si **un** des quatre coins de l'empreinte à la position
  candidate est bloqué (`IsHorizontalMoveBlocked`, `:1159-1194`, qui rend vrai au premier coin
  bloqué), l'axe est mis à **zéro** et `h1Curtailed`/`h2Curtailed` passent à vrai (`:1132-1154`).
  - Un coin bloque si le champ ne rend pas de sol, si la cellule n'est pas praticable pour le
    masque, ou si le sol dépasse `footUp + StepHeight`.
  - Les coins lointains valent `MathF.BitDecrement(centre + demi-taille)` (`:951-975`).
  - Le champ n'utilise pas `SkinWidth`. Le balayage rigide, lui, va au contact moins `SkinWidth` et
    glisse (`:1080-1085`).
- La règle « pas de déplacement partiel » ne figure dans aucune ADR du moteur. Elle vient de la
  décision C5 du plan parent `docs/plan-e3-collisions.md:423-431`, qui la présente comme une
  simplification de la recherche dichotomique de l'original. Elle a été introduite par `dbda6359`.
  Elle n'est écrite que dans la doc XML (`:1093-1097`) et dans celle de
  `CharacterControllerContactReport` (`H1Curtailed`/`H2Curtailed` = « curtailed to zero »,
  `CasaEngine/Framework/Scene/Entities/Components/CharacterControllerContactReport.cs:83-91`). Aucun
  code de production ne lit ces deux drapeaux.
- ADR-0006 (`docs/decisions/0006-collision-volumes-vs-fields.md:41-46`) dit seulement que le champ
  filtre le déplacement « axis by axis » (`:44`). L'ordre « h1 puis h2 » est écrit dans
  `docs/engine/collision-2d-3d-architecture.md:367`. C'est encore vrai après le chantier, donc rien
  n'est remplacé. La dernière ADR est la 0044 (`docs/decisions/0044-runtime-save-games.md`).
- `ICollisionField` n'expose que `TrySampleGround` (`CasaEngine/Engine/Physics/ICollisionField.cs:57-61`,
  `:76`, `:93-94`) : pas de taille de cellule. Le seul champ de production, celui d'Alundra, a des
  cellules de 24×16 px, échantillonne des pixels entiers et calcule des hauteurs de pente par pixel.
  Un calcul exact de la frontière demanderait donc un nouveau membre d'interface.
- Tests qui figent le comportement actuel (`CasaEngine.Tests/Physics/CharacterControllerFieldAwareMoverTests.cs`) :
  - `MoveThenUpdate_BlockedByACliffTallerThanStepHeight` (`:65`) et
    `MoveThenUpdate_BlockedByNonWalkableCells` (`:173`) attendent (48,24,0). Le contact donne (56,24,0) ;
  - `Move_ContactReport_FieldBlockedAxis_MarksOnlyThatAxis` (`:112`) attend `ActualH1Amount = 0`.
    Le contact donne 8, avec `H1Curtailed` toujours vrai (D3) ;
  - les autres tests de ce fichier et des fichiers voisins ne sont pas touchés. Réutilisables :
    `HeightGridCollisionField` et les aides privées `CreatePawn` (`:363`), `CreateBoxCollision`
    (`:403`), `CreateColumnHeightField` (`:422`), `CreateNonWalkableColumnField` (`:439`),
    `AssertPosition` (`:356`).
- Émulation en flottant 32 bits, faite pendant la découverte du plan parent, pour le cas de la cabine
  (empreinte 21×15, fixture en (0,5 ; 0,5), rangée 12 bloquée, frontière 208) :
  - une bisection sur la position **de la racine** candidate, sur 24 itérations, rend exactement
    y = 215,0, soit le plus petit flottant non bloqué. 24 itérations sont exactes jusqu'à des pas de
    100 px ; l'exactitude demande que |pas| × 2^-24 reste sous l'ULP de la racine ;
  - 20 itérations suffisent pour les pas d'Alundra (moins de 3,2 px), mais laissent parfois la
    position à 1 ULP du contact pour des pas de 16 px et plus ;
  - une bisection sur le **centre** (`centre + montant`) peut finir 1 à 2 ULP dans le mur. Cela
    arrive quand la racine de départ est dans [255,5 ; 256), [511,5 ; 512) ou [1023,5 ; 1024).
- Dépôt : le sous-module est sur `main` en `a550859f`, pointeur du parent identique. Modification de
  l'auteur non indexée : `CasaEngine.Launcher/Program.cs`, **jamais à indexer**.
- `CasaEngine.MonoGame.sln` n'inclut pas `CasaEngine.Tests`, qui se construit explicitement. Tests à
  0 échec attendus (le test `materials` peut rater une fois, à relancer seul).

## Décisions verrouillées

| Réf | Décision |
|---|---|
| D1 | Sur le champ de cellules, un pas bloqué sur un axe avance jusqu'à la position la plus lointaine non bloquée, **sans marge** sur la grille (D-E19-8 du plan parent). |
| D2 | L'ordre des axes est gardé (h1, puis h2 depuis la position avancée). Le glissement de l'original le long d'un mur quand un seul coin touche (`didAdjustForObstacle`) n'est pas porté ici (D-E19-9, tranche E19.h du parent). |
| D3 | `H1Curtailed`/`H2Curtailed` signifient désormais « le pas demandé a été raccourci sur cet axe », qu'il reste nul ou non. La signature ne change pas ; la documentation est reformulée (D-E19-11). |
| D4 | Le moteur ne calcule pas de drapeau « bloqué » pour le jeu. La DLL garde sa règle (manque de déplacement de plus de 0,01 px), qui lève ce drapeau un tick plus tôt que l'original ; l'écart est consigné côté parent (D-E19-10). |

## Points à valider

| Réf | Proposition de l'agent |
|---|---|
| P1 | Algorithme : bisection par axe sur la position candidate **de la racine** (`racine + axe × montant × t`, centre recalculé comme `ResolveFootprint`), t dans [0 ; 1], **24 itérations fixes**, sans allocation. Le pas entier reste testé en premier : un pas libre coûte un seul jeu de 4 échantillons, comme aujourd'hui. Pré-test d'1 ULP : bloqué d'emblée, le montant reste 0, soit 8 échantillons pour une entité qui pousse un mur. |
| P2 | ADR-0045, en anglais, qui précise ADR-0006 sans la remplacer. Source : C5 de `docs/plan-e3-collisions.md` et D-E19-8. |
| P3 | Le changement vaut pour tous les appelants de `MoveWithCollisions` avec un champ, puisque le code est commun : `Move`, et les deux appels de `Update` (déplacement hérité du sol, `:252`, et déplacement par la vitesse, `:271`). Sur le chemin de `Update`, la vitesse recalculée (`actual / dt`, `:286`) garde une valeur partielle à l'image du contact. |
| P5 | Limites acceptées, écrites dans l'ADR-0045 : le balayage rigide abandonne une avance résiduelle de moins de 1e-3 px (`:1049-1052`, seuil `:16`), et un pas demandé de moins de 1e-3 px n'est jamais testé contre le champ (`:1032-1037`, comportement existant). Le contact est donc exact à 1 ULP ou 1e-3 px près. |
| P4 | Branche `chantier/field-move-to-contact`, créée depuis `main` dans le checkout du sous-module en T0.1, après l'approbation. La modification de `Program.cs` suit le changement de branche sans être indexée. |

## Règles d'exécution pour l'agent

- **Branche dédiée `chantier/field-move-to-contact`**, créée depuis `main`. Ne jamais committer sur `main`.
- **Une seule tâche à la fois.** Avant de commencer une tâche, remplacer son icône `⏳` par `🚧`. À la fin, lancer la validation indiquée, remplacer l'icône par `✅`, `🧪` ou `⚠️`, ajouter une courte note de validation sous la tâche, puis **créer un commit dédié** qui inclut la mise à jour de ce fichier.
- **Un commit par tâche**, atomique et compilable, message en anglais au format `type(area): summary`. Le message suggéré est donné dans chaque tâche.
- **Ne jamais pousser.** Le merge sur `main` reste une décision humaine.
- **Ne rien inventer** : toute API, tout fichier, toute règle utilisée existe dans le dépôt, vient d'une réponse de l'auteur, ou d'une doc officielle citée (URL). Sinon : passer la tâche en ⚠️ Blocked, écrire la question dans « Points ouverts », et **s'arrêter**.
- **Build obligatoire** avant de passer une tâche en ✅ dès que du code est touché (`dotnet build CasaEngine.MonoGame.sln` ou `dotnet build CasaEngine.Editor.MonoGame.sln` selon le périmètre) ; **tests** `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj` dès qu'une tâche touche du code testé. Si le build est impossible, la tâche reste 🧪 avec la raison écrite.
- Si le code est écrit mais qu'une vérification visuelle ou manuelle manque, utiliser `🧪 Needs testing` et noter précisément ce qui manque.
- **Ne jamais laisser une tâche en 🚧** à la fin d'une session.
- **Ne jamais indexer** les modifications préexistantes de l'auteur : `git add` fichier par fichier, jamais `git add -A` ni `git add .`.
- **Langue** : ce plan en français ; code, messages de commit, docs de `docs/` et ADR en anglais.
- Rappel moteur : pas d'allocation, de LINQ ni de closure dans les chemins chauds ; restaurer tout état GPU modifié ; le runtime ne dépend pas de l'éditeur ; sérialisation additive (détail dans `AGENTS.md` et les règles par chemin).
- Une commande git par appel, `git -C "<chemin absolu>" …`, jamais `cd … &&` devant un commit.
- Les tests se lancent au premier plan, avec `--blame-hang-timeout 60s`. Une mutation qui prouve un test se fait pour de vrai, par script, puis le code est restauré à l'octet près.

## Légende des statuts

- ⏳ Todo : pas encore commencé.
- 🚧 In progress : en cours de modification locale.
- 🧪 Needs testing : code écrit, validation incomplète ou en attente.
- ✅ Done : code validé, build/tests OK, commit effectué.
- ⚠️ Blocked : bloqué par une erreur non résolue ou une décision manquante.

## Validation globale

- `dotnet build CasaEngine.MonoGame.sln` : 0 erreur.
- `dotnet build CasaEngine.Tests/CasaEngine.Tests.csproj`, puis `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj --no-build --blame-hang-timeout 60s` : 0 échec.
- Côté parent (tranche E19.a2, après le déplacement du pointeur) : l'arc A1c de la cabine avec un vrai
  contrôleur atteint le `0x53` vers 476, et la recette en jeu de l'auteur passe.

---

## Phase 0 — Mise en place

### ✅ T0.1 — Branche, plan et ADR-0045

*(fait le 2026-09-29 : branche `chantier/field-move-to-contact` créée depuis `main` en `a550859f`, `Program.cs` resté non indexé ; ADR-0045 en anglais, ligne d'index et ligne du tableau d'`ai-agent/README.md` ajoutées. Preuve rouge d'A1c côté parent faite avant cette tâche : `HEAD` du sous-module `a550859f`, échec à l'image 899 sur `0x1E @658`.)*

- Objectif : créer la branche du chantier, commiter ce plan et consigner la décision.
- Fichiers : `ai-agent/tasks/field-move-to-contact-tasks.md`, `ai-agent/README.md` (ligne du tableau), `docs/decisions/0045-a-blocked-field-step-advances-to-contact.md`, `docs/decisions/README.md` (index).
- Étapes :
  1. Créer `chantier/field-move-to-contact` depuis `main`, sans toucher à `CasaEngine.Launcher/Program.cs`.
  2. Écrire ADR-0045 avec le skill `adr` du dépôt :
     - Context : le fait de l'original, la décision C5 et le défaut de la cabine ;
     - Decision : D1, D3 et P1 ;
     - Consequences : les entités s'arrêtent désormais contre les murs, jusqu'à un pas plus près
       qu'avant ; pas de glissement (D2) ; la précision est de 1 ULP ou 1e-3 px (seuil du balayage).
     Statut « Accepted ». La Source cite `docs/plan-e3-collisions.md` (C5) et `docs/plan-e19-opcodes.md`
     (D-E19-8, D-E19-11) du parent.
  3. Ajouter la ligne de ce plan au tableau d'`ai-agent/README.md`.
- Validation : `git -C <sous-module> status --short` ne montre que ces fichiers, plus ` M CasaEngine.Launcher/Program.cs` non indexé.
- Commit : `docs(physics): plan and ADR-0045 for advancing a blocked field step to contact`

---

## Phase 1 — Le contact

### ✅ T1.1 — Bisection jusqu'au contact sur le champ, et tests

*(fait le 2026-09-29 : bisection sur la position de la racine dans `AdvanceBlockedAxisToContact` (24 itérations, pré-test d'1 ULP, contrôle final), `ResolveFootprint` expose le décalage de la fixture, docs XML reformulées. 20 tests ajoutés ou changés dans `CharacterControllerFieldAwareMoverTests`, tous vus en échec sur l'ancien code (23 échecs sur 34, dont les 3 tests changés) ; le filtre `CharacterController` passe 121/121, la suite entière 2375/2375, solution sans erreur. Mutations réelles par script, code restauré à l'octet près : rejet entier remis (13 noms de test échouent), recherche sur le centre (les deux théories des puissances de deux échouent), 12 itérations (les tests de contact échouent). Aucun test hors du fichier n'a changé de résultat (O1). Constat de mesure : au bord de la grille, la borne lointaine étant exclusive d'un ULP, le dernier flottant libre est `BitIncrement(120)` et non 120 (test écrit en conséquence).)*

- Objectif : un axe bloqué avance jusqu'au contact (D1, P1), et les tests le prouvent.
- Fichiers : `CasaEngine/Framework/Scene/Entities/Components/CharacterControllerComponent.cs`, `CasaEngine.Tests/Physics/CharacterControllerFieldAwareMoverTests.cs` (et au besoin un fichier de tests voisin).
- Étapes :
  1. Écrire d'abord les nouveaux tests et les voir échouer sur le code actuel. Ils utilisent
     `HeightGridCollisionField` (cellules de 16, haut = Z) et l'empreinte du héros (boîte
     21×15×32 en (0,5 ; 0,5 ; 16)), les valeurs attendues étant écrites à la main. Aides à ajouter au
     fichier de tests : un champ à **rangée** non praticable et un champ à rangée surélevée (seul le
     champ à colonne existe, `:422`, `:439`), et un pion à l'empreinte du héros (`CreatePawn`, `:363`,
     fait une boîte de 16×16). La grille du test de la cabine compte au moins 15 rangées : le coin
     lointain, à y = 224,34, est dans la rangée 14.
     - **cabine** : rangée 12 non praticable, racine en y = 216,34, `Move(0,-1,6,0)`. Attendu :
       y == 215,0 exactement, `H2Curtailed` vrai, `ActualH2Amount` ≈ -1,34 ;
     - **marche de 80 px** : depuis y = 295,16 vers le nord par pas de 1,625. La marche s'arrête
       en y == 215,0 et |dy| ≥ 80 ;
     - **au contact** : des poussées répétées rendent un déplacement nul et restent raccourcies ;
     - **falaise et cellules non praticables** : les tests `:65` et `:173` attendent (56,24,0), et
       `:112` attend `ActualH1Amount = 8` avec `H1Curtailed` vrai ;
     - **diagonale** : h1 raccourci au contact, puis h2 libre depuis la position avancée ;
     - **sens positifs** sur les deux axes ;
     - **frontières de puissance de deux** : une racine de départ dans [255,5 ; 256), puis une dans
       [1023,5 ; 1024). Avec des cellules de 16 et l'empreinte du héros, les contacts tombent sur des
       entiers (16k+7 vers y-, 16k-8 vers y+, 16k+10 vers x-, 16k-11 vers x+), jamais dans ces
       intervalles. Le pas doit donc aller jusqu'à une frontière plus loin : au moins 8,5 px vers y-
       (frontière 240, contact 247), ou 5,5 px vers x- (contact 250) ; pour la seconde fenêtre,
       frontière 1008, contact 1015. **La grille doit contenir le coin lointain de l'empreinte de
       départ** : hors de la grille, `HeightGridCollisionField` ne rend pas de sol, ce qui bloque. Il faut
       donc au moins 17 rangées pour une racine dans [255,5 ; 256) vers y- (coin lointain dans la rangée
       16), au moins 65 rangées pour [1023,5 ; 1024) (rangée 64), et assez de colonnes vers x- (coin
       lointain vers racine + 11, soit la colonne 16 pour la première fenêtre). La position atteinte,
       retestée, n'est pas bloquée. Une bisection sur le centre échoue à ce test dans environ un quart des cas ;
       celle sur la racine, jamais (émulation) ;
     - **bord de la grille** : l'empreinte s'arrête exactement sur le bord ;
     - **marche d'escalier** : une hauteur dans `StepHeight` n'est jamais un contact (test
       existant `:77` inchangé) ;
     - **chemin de `Update`** : un pas raccourci par la vitesse avance aussi jusqu'au contact. Le test
       règle `SetMoveIntent`, `MaxHorizontalSpeed` et `Acceleration`, puisque `Update` recalcule la
       vitesse horizontale.
  2. Coder la bisection :
     - dans `ResolveHorizontalDisplacementAgainstField`, avec une méthode privée à paramètres
       explicites, sans lambda ni allocation ;
     - la variable de recherche est la position de la racine candidate. `ResolveFootprint` expose
       le décalage de la fixture pour recalculer le centre à chaque essai ;
     - le déplacement rendu vaut exactement le montant testé (`montant × t`), de sorte que
       `départ + déplacement` retombe au bit près sur la racine candidate validée ;
     - le pré-test « 1 ULP » avance la racine d'un seul flottant représentable dans le sens du pas
       (`MathF.BitIncrement`/`BitDecrement` de la coordonnée), pas d'un montant fixe ;
     - après la recherche, un contrôle final recalcule le centre depuis la racine retenue. S'il est
       bloqué, la racine recule d'1 ULP, au plus quelques fois. C'est une garde pour une fixture
       décalée.
  3. Reformuler les docs XML : `:1092-1101`, et `CharacterControllerContactReport.cs:83-91` (D3).
  4. Prouver les nouveaux tests par une vraie mutation : remettre le rejet entier par script, voir
     les tests échouer, puis restaurer le code à l'octet près.
- Validation : build de la solution, `CasaEngine.Tests` construit puis lancé, 0 échec. Filtre
  `CharacterController` d'abord, puis la suite entière.
- Commit : `fix(physics): advance a blocked cell-field step to contact instead of rejecting it`

### ⏳ T1.2 — Documentation

- Objectif : les docs du moteur décrivent le contact.
- Fichiers : `docs/engine/collision-2d-3d-architecture.md` (paragraphe « État de D5 », `:362-368`), `docs/engine/character-controller-features.md` (paragraphe E3.c, `:312-324`).
- Étapes :
  1. Écrire que le champ avance un axe bloqué jusqu'au contact, avec la précision réelle
     (1 ULP ou 1e-3 px), sans glissement le long des murs, avec renvoi à ADR-0045.
  2. Ces deux documents sont en français : les paragraphes ajoutés restent en français, pour ne pas
     mélanger les langues d'un même document. C'est une exception à la règle « docs en anglais » de
     ce plan, limitée à ces deux paragraphes.
- Validation : relecture ; `git diff` limité à ces paragraphes.
- Commit : `docs(physics): describe the field contact in the collision and controller docs`

---

## Points ouverts

À trancher pendant l'exécution, ou à remonter en ⚠️ Blocked si la réponse manque.

| Réf | Sujet | Tâche concernée |
|---|---|---|
| O1 | Si un test existant hors du fichier `CharacterControllerFieldAwareMoverTests` change de résultat, le signaler au lieu de l'adapter en silence : cela voudrait dire que le changement a plus de portée que prévu. | T1.1 |
| O2 | La règle de physique d'`AGENTS.md` demande debug draw et sample pour une nouvelle fonction de collision. Ce chantier corrige un comportement existant ; si un relecteur juge le contraire, arrêt et question. | T1.1 |

## Hors périmètre

- Le glissement le long d'un mur quand un seul coin touche (D2 ; tranche E19.h du parent).
- Toute modification d'`ICollisionField`, du balayage rigide, de `SkinWidth` ou de `StepHeight`.
- Les tests, traces de référence et commentaires du portage Alundra que le contact fait bouger :
  ils sont re-mesurés dans la tranche E19.a2 du parent, après le déplacement du pointeur.
