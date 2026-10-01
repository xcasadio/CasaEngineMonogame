# Plan agent IA — Horloge logique exacte des fins d'animation 2D

Plan d'exécution de la partie moteur de la tranche E19.c2 du portage Alundra (plan parent
`docs/plan-e19-opcodes.md`, §1.2f, dépôt `alundra-casaengine-project-converter`). Les décisions ci-dessous ont
été arbitrées avec l'auteur le 2026-10-01 : **ce plan les applique, il ne les rediscute pas**.

Ce fichier doit être mis à jour pendant le travail : l'icône au début de chaque tâche indique son statut courant.

## Objectif

- Donner à `AnimatedSpriteComponent` une **horloge logique en ticks entiers**, optionnelle, pilotée par la
  couche de jeu. Quand elle est active, c'est elle seule qui lève `AnimationFinished` (animation Once) et un
  nouvel événement `AnimationLooped` (tour d'une animation Loop), au tick exact. Le rendu (images, chronologie
  des collisions, événements dessinés) reste en temps réel.
- Corriger un défaut du chemin en temps réel : certaines animations Loop ne bouclent plus jamais et montrent
  la clé cachée de fin (O-E19-10 du plan parent).

Hors périmètre : avancer le rendu des sprites par ticks (refusé par l'auteur) ; une horloge logique au niveau
du monde ; tout changement du format `.anim2d` ou du convertisseur ; l'éditeur.

## État vérifié du dépôt (2026-10-01)

- `main` = `origin/main` = `74e97293` ; il contient `chantier/field-move-to-contact` (merge en avance rapide de
  l'auteur, reflog du 2026-10-01). Cette branche part de `main`. L'arbre de travail est propre.
- `Animation2dCompositionSampler.Update` (`CasaEngine/Framework/Assets/Animations/Animation2dCompositionSampler.cs:48-74`) :
  une Once finit à la première mise à jour où `CurrentTime > DurationSeconds` (strict) ; une Loop passe par
  `UpdateLooping` (`:76-115`) et ne signale rien.
- **Défaut** (`:92-104`) : la somme float32 peut tomber exactement sur la durée. À la mise à jour suivante,
  `timeUntilRestart` ≤ 0 est remplacé par une durée entière sans remettre `previousTime` à 0, et le temps
  dépasse la durée pour toujours. Exemple : animation 53 du héros (0,32 s) à 0,02 s par image : 0,29999998
  après 15 mises à jour, 0,32 à la 16e, puis 0,34, 0,36… ; le sprite échantillonne la clé cachée de fin.
  `Seek(Duration)` sur une Loop entre dans le même état (`WrapLoopTime`, `:150-158`). Modèle float32 (vérifié
  aussi par `dotnet fsi`) sur les 9623 `.anim2d` exportés : à 0,02 s, 197 des 5205 Loop de durée positive ne
  bouclent jamais, aucune à 1/60, 1/144 ou 1/30 s. La doc `docs/engine/animation2d-composed-format-v1.md`
  (`:168-170`) dit déjà que le temps boucle modulo la durée : la correction ramène le code à la règle écrite.
- `AnimatedSpriteComponent` (`CasaEngine/Framework/Scene/Entities/Components/AnimatedSpriteComponent.cs`) :
  `AnimationFinished` levé dans `Update` sur la transition de fin (`:204-226`) ; `SetCurrentAnimation`
  (`:104-127`) remet l'échantillonneur à zéro sur un changement, ou sur le même nom avec `forceReset` ;
  `InitializeWithWorld` (`:153-191`) reconstruit les échantillonneurs puis `SetCurrentAnimation(0, true)` ;
  `SeekCurrentAnimation` (`:228-240`) ; le constructeur de copie (`:94-102`) ne copie ni abonnements ni
  échantillonneurs ; `ShouldUpdateWhenConditional` (`:198-202`) garde à jour les entités en
  `TickPolicy.Conditional`, dont les préfabs Alundra sans autre composant.
- Utilisateurs : le jeu Alundra (DLL du dépôt parent, pont des fins d'animation), la démo RPG (abonnée à
  `AnimationFinished`, reste en mode par défaut), l'éditeur (`SeekCurrentAnimation` en aperçu),
  `CasaUIAnimatedImage` (son propre échantillonneur : il profite de la correction de boucle).
- Données : chaque instant de clé exporté est à moins de 1,02e-4 tick de la grille à 50 Hz ; la durée vient
  toujours de la dernière clé d'une piste ; aucune Once de durée nulle ; 5 Loop de durée nulle ; aucun
  `.anim2d` n'a d'événement dessiné ; 5568 ont des clés de collision.
- Dernière ADR : ADR-0045. Prochaine : **ADR-0046**.

## Décisions verrouillées

| Réf | Décision |
|---|---|
| D1 | Le moteur reçoit une horloge logique exacte des fins d'animation, optionnelle, pilotée par la couche de jeu (D-E19-16 et D-E19-17 du plan parent). |
| D2 | Le rendu des sprites reste en temps réel : seules les fins logiques (Once, tour de Loop) sont exactes (D-E19-17). |
| D3 | Le défaut des Loop figées du chemin en temps réel se corrige dans ce chantier (D-E19-19). |
| D4 | Branche partie de `main` après le merge de `chantier/field-move-to-contact` par l'auteur (fait). |

## Conception retenue

- **Où vit le compteur** : sur `AnimatedSpriteComponent`, en champs valeur (taux, tick courant, durée en
  ticks, type, fin atteinte, nombre de tours, version de remise à zéro). L'échantillonneur reste pur ; il ne
  gagne que deux accesseurs en lecture (`DurationSeconds`, `AnimationType`) et le compte des tours de son
  chemin en temps réel.
- **API additive** :
  - `SetLogicalTickRate(int ticksPerSecond)` : 0 = désactivé (défaut) ; une valeur négative lève
    `ArgumentOutOfRangeException` ; **la même valeur ne fait rien** ; une autre valeur remet l'horloge à zéro ;
  - `int AdvanceLogicalTicks(int ticks)` : avance de `ticks` ticks, lève les événements de manière synchrone et
    rend le nombre de ticks appliqués (moins que demandé si un gestionnaire a changé d'animation) ; 0 ne fait
    rien ; une valeur négative lève `ArgumentOutOfRangeException` ; avec le taux à 0, lève
    `InvalidOperationException` (usage invalide) ;
  - en lecture : `LogicalTickRate`, `LogicalTick`, `LogicalDurationTicks`, `IsLogicalEndReached`,
    `CompletedLoopCount` ;
  - `event EventHandler<Animation2d> AnimationLooped`, levé **une fois par tour**.
- **Règles** :
  - D = 0 si `DurationSeconds` ≤ 0, sinon max(1, arrondi au plus proche, demi loin de zéro, de
    `DurationSeconds × taux`) ;
  - une Once finit à la D-ième avance après une remise à zéro : `AnimationFinished` une seule fois, puis plus
    rien ; une Loop boucle à chaque D-ième avance : `AnimationLooped`, `CompletedLoopCount` + 1, tick à 0 ;
  - D = 0 : une Once finit à la première avance, une Loop ne boucle jamais. **Ce sont des règles du moteur**,
    pas une copie du binaire d'Alundra (où une image de délai 0 fige l'animation) ;
  - avec l'horloge active, le chemin en temps réel ne lève plus jamais `AnimationFinished` ni
    `AnimationLooped` ; il garde les images, la chronologie des collisions et les événements dessinés. Aucun
    recalage du rendu à la fin logique ;
  - remise à zéro : à chaque `SetCurrentAnimation` qui remet l'échantillonneur à zéro, à `InitializeWithWorld`
    et à un changement de taux. `SeekCurrentAnimation` recale le tick logique sur le temps demandé, sans
    événement ;
  - ni `IsPlaybackPaused` ni `ExecutionPolicy` ne bloquent l'avance logique (c'est la couche de jeu qui décide
    quand avancer) ; le constructeur de copie ne copie pas le taux ; `InitializeWithWorld` le garde ;
    `ShouldUpdateWhenConditional` est inchangé ;
  - si un gestionnaire change d'animation pendant une avance, la version de remise à zéro arrête l'avance ;
    les ticks restants sont abandonnés et la valeur rendue le dit ;
  - aucune allocation dans `AdvanceLogicalTicks` ;
  - **chemin en temps réel (horloge désactivée)** : `AnimationLooped` est aussi levé une fois par tour, compté
    par `UpdateLooping`, pour que le contrat de l'événement soit le même dans les deux modes.
- **Correction de boucle** dans `UpdateLooping` : (ii), nécessaire et suffisante — n'accepter
  `previousTime + remainingTime` comme intérieur au cycle que s'il est **strictement inférieur** à la durée,
  sinon boucler ; (i), raffinement conforme à la règle modulo écrite — quand le temps est déjà à la durée ou
  au-delà (`timeUntilRestart` ≤ 0, entrée par `Seek(Duration)`), boucler tout de suite (`previousTime` = 0).

## Règles d'exécution pour l'agent

- **Branche dédiée `chantier/animation-logical-end-clock`**, créée depuis `main`. Ne jamais committer sur `main`.
- **Une seule tâche à la fois.** Avant de commencer une tâche, remplacer son icône `⏳` par `🚧`. À la fin, lancer
  la validation indiquée, remplacer l'icône par `✅`, `🧪` ou `⚠️`, ajouter une courte note de validation sous la
  tâche, puis **créer un commit dédié** qui inclut la mise à jour de ce fichier.
- **Un commit par tâche**, atomique et compilable, message en anglais au format `type(area): summary`.
- **Ne jamais pousser.** Le merge sur `main` reste une décision humaine (moteur d'abord, puis le parent).
- **Ne rien inventer** : sinon ⚠️ Blocked, question dans « Points ouverts », arrêt.
- **Build** `dotnet build CasaEngine.MonoGame.sln` ; **tests** : `CasaEngine.Tests` n'est pas dans la solution,
  le builder explicitement (`dotnet build CasaEngine.Tests/CasaEngine.Tests.csproj`) puis
  `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj --no-build --blame-hang-timeout 60s`, au premier plan.
- **Tests rouges d'abord** pour R1 à R3 : ils échouent sur le code actuel avant la correction (cela prouve
  aussi que le C# reproduit le modèle float32).
- **Ne jamais indexer** les modifications préexistantes de l'auteur : `git add` fichier par fichier.
- **Langue** : ce plan en français ; code, commits, docs de `docs/` et ADR en anglais.
- **Arrêts** : une valeur écrite d'avance contredite (ne jamais la ré-épingler) ; un test existant qui bouge ;
  une allocation dans l'avance logique ; deux échecs sur une même tâche.

## Légende des statuts

- ⏳ Todo : pas encore commencé.
- 🚧 In progress : en cours de modification locale.
- 🧪 Needs testing : code écrit, validation incomplète ou en attente.
- ✅ Done : code validé, build/tests OK, commit effectué.
- ⚠️ Blocked : bloqué par une erreur non résolue ou une décision manquante.

## Validation globale

- `dotnet build CasaEngine.MonoGame.sln` : 0 erreur.
- `CasaEngine.Tests` : base 2375 réussis, 0 échec ; après le chantier, la base plus les nouveaux tests, 0 échec,
  aucun test existant modifié.
- La partie Alundra (pilotage, pont, arcs) est vérifiée dans le plan parent.

---

## Phase 0 — Cadre

### ⏳ T0.1 — ADR-0046 et documentation de la conception

- Objectif : enregistrer la décision avant le code.
- Fichiers : `docs/decisions/0046-a-logical-tick-clock-for-2d-animation-ends.md` (nouveau), ligne
  `docs/decisions/README.md`.
- Contenu : contexte (horloge en temps réel float32, fins décalées d'un tick à 50 Hz, Loop figées), décision
  (conception retenue ci-dessus, règles D = 0 présentées comme règles du moteur), conséquences (deux horloges
  pour une animation, rendu et fin logique peuvent différer d'environ un tick ; un mode actif sans pilote ne
  lève plus de fin ; clés hors grille arrondies au tick).
- Validation : relecture.
- Commit : `docs(adr): add ADR-0046 on the logical tick clock of 2D animation ends`

## Phase 1 — Correction du chemin en temps réel

### ⏳ T1.1 — Les Loop ne restent plus figées après la durée

- Objectif : corriger `UpdateLooping` (branches ii et i) et lever `AnimationLooped` une fois par tour sur le
  chemin en temps réel.
- Fichiers : `Animation2dCompositionSampler.cs` (correction, accesseurs `DurationSeconds` et `AnimationType`,
  compte des tours de la dernière mise à jour), `AnimatedSpriteComponent.cs` (levée d'`AnimationLooped` en
  mode par défaut), `CasaEngine.Tests/Animation/Animation2dCompositionSamplerLoopWrapTests.cs` (nouveau).
- Tests (valeurs écrites d'avance, R1 à R3 rouges sur le code actuel) :
  - **R1** — Loop façon animation 53 (clés de sprite A, B, C, D à 0, 0,08, 0,16, 0,24 ; visible à ces clés,
    caché à 0,32 ; partie invisible par défaut), `Update(0.02f)` répété : après 15 mises à jour,
    `CurrentTime` = 0,29999998f, visible, sprite D ; 16e : `CurrentTime` = 0f, visible, sprite A (aujourd'hui
    0,32f, caché) ; 17e : 0,02f. Sur 160 mises à jour : toujours visible, `CurrentTime` < 0,32f, et = 0f
    exactement aux mises à jour 16, 32, …, 160. Changements d'image : B à la 4e, C à la 9e, D à la 13e, puis
    + 16 par cycle.
  - **R2** — théorie sur les durées 0,27999997f (D 14), 0,29999998f (D 15), 0,32f (D 16), 0,36f (D 18),
    0,38000003f (D 19), une clé de sprite à 0 et une clé cachée à la durée : après D − 1 mises à jour,
    `CurrentTime` > 0 ; après D, = 0f ; jamais ≥ la durée sur 10 × D mises à jour.
  - **R3** — `Seek(0.32f)` donne 0,32f, caché (inchangé) ; puis un `Update(0.02f)` donne 0,02f, visible,
    sprite A (aujourd'hui 0,34f, caché).
  - **R4** — composant dans un monde factice (politique Runtime), données de R1, 48 × `Update(0.02f)` :
    `AnimationLooped` aux mises à jour 16, 32 et 48 seulement. `Update(1f)` sur une Loop de 0,32f : 3
    événements et `CurrentTime` 0,04000002f. `Seek(0.32f)` puis `Update(0.02f)` : 1 événement.
- Validation : R1 à R3 rouges avant la correction, verts après ; R4 vert ; `CasaEngine.Tests` 0 échec, aucun
  test existant modifié (un modèle donne des valeurs identiques pour toutes les suites de Loop des tests
  existants).
- Commit : `fix(animation): wrap a 2D loop whose time lands on its duration`

## Phase 2 — Horloge logique

### ⏳ T2.1 — Horloge logique des fins sur `AnimatedSpriteComponent`

- Objectif : la conception retenue, API additive, sans allocation.
- Fichiers : `AnimatedSpriteComponent.cs`, `CasaEngine.Tests/Animation/AnimatedSpriteLogicalEndClockTests.cs`
  (nouveau).
- Tests (taux 50 sauf mention ; valeurs écrites d'avance) :
  - **L1** — Once D 24 (clés de l'animation 6 de Ronan : 0, 0,12, 0,24, 0,35999998, 0,48) :
    `LogicalDurationTicks` 24 ; avances 1 à 23 : rien, `LogicalTick` = k ; avance 24 : exactement un
    `AnimationFinished`, tick 24, fin atteinte ; avances 25 à 30 : plus rien.
  - **L2** — fins : animation 83 du héros (0,64f) à l'avance 32 ; 85 (0,79999995f) à 40 ; 12 de Jess (0,48f)
    à 24 ; marche du héros (0,59999996f) à 30.
  - **L3** — Loop D 54 (animation 0 du héros : 0, 0,79999995, 0,99999994, 1,06, 1,0799999) :
    `AnimationLooped` aux avances 54, 108 et 162 seulement, `CompletedLoopCount` 1, 2, 3 ; `LogicalTick` 53
    après l'avance 53, 0 après la 54e ; jamais d'`AnimationFinished`.
  - **L4** — Loop D 24 : `AdvanceLogicalTicks(50)` lève 2 tours, laisse le tick à 2 et rend 50. Once D 24 :
    `AdvanceLogicalTicks(100)` lève une fin, laisse le tick à 24. `AdvanceLogicalTicks(0)` ne fait rien.
    `AdvanceLogicalTicks(-1)` et `SetLogicalTickRate(-1)` lèvent `ArgumentOutOfRangeException`.
  - **L5** — remises à zéro : après la fin, `SetCurrentAnimation(même, true)` remet le tick à 0, et la fin
    suivante vient à la 24e avance ; au tick 10, `SetCurrentAnimation(même, false)` laisse 10 ; au tick 10, un
    changement vers la Loop D 54 donne tick 0, D 54, tours 0 ; au tick 10, `SetLogicalTickRate(50)` à nouveau
    laisse 10 (même valeur), `SetLogicalTickRate(25)` remet à 0.
  - **L6** — coexistence, par image `Update(0.02f)` puis `AdvanceLogicalTicks(1)`. Ronan : le rendu finit à la
    24e mise à jour (0,46000007f après 23, puis 0,48f) sans rien lever ; le compte des fins vaut 0 après le
    `Update` de l'image 24 et 1 après son avance, et reste 1 après 30 images. Héros 83 : 1 après l'avance de
    l'image 32 ; le rendu finit à la 33e mise à jour sans second événement. `Update(1f)` d'abord : 0
    événement, puis 24 avances : 1 (à la 24e). Les avances ne bougent pas `CurrentAnimationTimeSeconds`, les
    mises à jour ne bougent pas `LogicalTick`.
  - **L7** — mode par défaut : taux 0 et `Update(1f)` sur une Once : exactement un `AnimationFinished` (comme
    aujourd'hui) ; `AdvanceLogicalTicks(1)` avec le taux 0 lève `InvalidOperationException`.
  - **L8** — durées nulles et petites : une Loop à une seule clé en 0 : 0 tour et tick 0 sur 100 avances ; une
    Once à une seule clé en 0 finit à l'avance 1 (tick 0) et ne lève rien ensuite ; une Loop de 0,005f a D 1
    et boucle à chaque avance.
  - **L9** — arrondi à 50 : 0,25f donne D 13 (demi loin de zéro ; l'arrondi bancaire donnerait 12), 0,75f
    donne 38, 0,12f donne 6.
  - **L10** — `SeekCurrentAnimation` avec l'horloge : Once D 24, `Seek(0.12f)` donne tick 6 sans événement,
    puis la fin à la 18e avance ; `Seek(0.48f)` donne tick 24 et fin atteinte, sans événement, puis plus rien.
    Loop D 54 : `Seek(0.5f)` donne tick 25, `Seek(1.0799999f)` donne tick 0 ; le nombre de tours ne change pas.
  - **L11** — taux 50 et 5 avances, puis `InitializeWithWorld` : taux 50, tick 0, D de l'animation 0 ; un
    second `InitializeWithWorld` garde 50.
  - **L12** — la copie d'un composant à horloge active a `LogicalTickRate` 0 ; après `InitializeWithWorld`,
    `Update(1f)` sur une Once lève `AnimationFinished`.
  - **L13** — horloge active et Once finie sur les deux horloges : `ShouldUpdateWhenConditional` vaut vrai en
    Runtime, faux en EditorPreview.
  - **L14** — Loop D 24, gestionnaires statiques, 10 avances de chauffe puis 1000 × `AdvanceLogicalTicks(1)` :
    0 octet alloué (`GC.GetAllocatedBytesForCurrentThread`).
  - **L15** — un gestionnaire d'`AnimationLooped` qui change d'animation, puis `AdvanceLogicalTicks(50)` sur
    Loop D 24 : exactement un `AnimationLooped`, puis tick 0 sur la nouvelle animation, valeur rendue 24.
  - **L16** — `IsPlaybackPaused` vrai, par image `Update(0.02f)` puis une avance : la fin vient à l'image 24,
    `CurrentAnimationTimeSeconds` reste 0f.
- Validation : `CasaEngine.Tests` 0 échec ; aucun test existant modifié.
- Commit : `feat(animation): add a logical tick clock for 2D animation ends`

### ⏳ T3.1 — Documentation

- Fichiers : `docs/engine/animation2d-composed-format-v1.md` (règle de bouclage, section « deux horloges » :
  temps réel pour le rendu, horloge logique optionnelle pour les fins), ligne de `docs/README.md` si un
  nouveau document apparaît, mise à jour de la ligne de ce plan dans `ai-agent/README.md`.
- Validation : relecture.
- Commit : `docs(animation): document the logical end clock and the loop wrap rule`

---

## Points ouverts

- Aucun à l'écriture du plan.
