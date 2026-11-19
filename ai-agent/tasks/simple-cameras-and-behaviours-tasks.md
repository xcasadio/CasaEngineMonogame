# Plan agent IA — Caméras simples, comportements en composants

<!--
Écrit les 2026-11-18 et 2026-11-19 à la demande de l'auteur. Relu trois fois par des relecteurs neufs avant approbation :
REVISE (cinq points P2), REVISE (deux points P2, venus du choix de modifier la classe de base des caméras 3D : la tranche 1
a alors été ramenée à un ajout pur), REVISE de clôture (un point P2, corrigé ensuite). La version approuvée n'a pas été
relue : aucune relecture n'a rendu READY.
Approuvé par l'auteur le 2026-11-19 (« J'approuve »), avec O1, O2, O3 et O8 selon les propositions, puis lancé en mode AUTO.
Lancé le 2026-11-19 sur `main` `9125b438`, qui contient `origin/main` `da32906e` (O1).
-->

Les décisions D1 → D9 ci-dessous ont été arbitrées avec l'auteur les 2026-11-18 et 2026-11-19 : **ce plan les applique, il ne les rediscute pas**.

Ce fichier doit être mis à jour pendant le travail : l'icône au début de chaque tâche indique son statut courant.

## Objectif

Le moteur a deux caméras simples, une 3D et une 2D, décrites par leurs données : leur pose vient de la transformation de leur composant, leur projection de leurs propriétés. Ce qui fait bouger une caméra (orbite, visée, suivi) sort des classes de caméra et devient des composants du moteur, déclarables dans un monde sans DLL de jeu, et pilotables par script.

Le chantier se fait en trois tranches, chacune livrable seule. Ce plan détaille la tranche 1 ; les tranches 2 et 3 y sont cadrées et recevront leurs tâches à leur lancement.

## État vérifié du dépôt

Relevé le 2026-11-19 sur `main` `9125b438`, base de la branche `chantier/simple-cameras` (T0.1). Chemins sous `CasaEngine/Framework/Scene/Entities/Components/` sauf mention.

### Les caméras

- `CameraComponent` est abstraite. Le getter `ViewMatrix` n'est pas virtuel : il n'appelle `ComputeViewMatrix` que si `_needToComputeViewMatrix` est posé, puis l'efface (`CameraComponent.cs:17-29`). Même forme pour `ProjectionMatrix` et `_needToComputeProjectionMatrix` (`:31-43`). Une classe dérivée n'est donc pas consultée à la lecture.
- `NearPlane` se borne sur la profondeur lointaine, `FarPlane` sur la proche ; les deux posent le drapeau de la projection (`CameraComponent.cs:49-69`).
- `CameraComponent.InitializeWithWorld` remet la taille du viewport à celle de l'écran et les profondeurs à 1 et 1000, quoi que `Load` ait lu (`CameraComponent.cs:91-100`). `OnScreenResized` écrit la taille et pose le drapeau de la projection (`:106-111`).
- `ComputeProjectionMatrix`, `ComputeViewMatrix` et `SetPositionAndTarget` sont abstraites (`CameraComponent.cs:102-104`). `Load` lit `view_distance` et `viewport` (`:118-124`).
- `Camera3dComponent` est abstraite (`Camera3dComponent.cs:7`). **Le champ de vision des données est respecté (D2)** : `DefaultFieldOfView` vaut 45 degrés (`:10`), `FieldOfView` est borné et pose le drapeau de la projection (`:18-26`), `InitializeWithWorld` ne le remplace que s'il est nul ou négatif (`:36-46`), `OnScreenResized` n'est pas redéfinie. ADR-0095, tests `Camera3dFieldOfViewTests`.
- La projection de `Camera3dComponent` est `Matrix.CreatePerspectiveFieldOfView` sur l'angle vertical, le rapport du viewport et ses profondeurs (`Camera3dComponent.cs:48-57`). `Load` lit `fieldOfView` (`:59-64`).
- Trois classes concrètes en dérivent et calculent chacune leur vue : `ArcBallCameraComponent` (lit `target`, `distance`, `yaw`, `pitch` ; pas de roulis), `CameraLookAtComponent` (`CreateLookAt(Position, Target, Up)`, cible par code), `CameraTargeted2dComponent`. `Camera2dComponent` dérive de `CameraComponent`. Aucune ne s'appelle autrement : quatre classes concrètes de caméra en tout.
- `CameraLookAtComponent.Update` compare la position à une valeur qu'il ne rafraîchit jamais (`CameraLookAtComponent.cs:37-52`), et son constructeur de copie écrit dans la source (`:26-30`) : ce n'est pas un modèle à reprendre.
- `Clone` est redéfinie avec un type de retour covariant dans les classes concrètes.

### La transformation

- `WorldMatrixNoScale` compose la matrice locale sans échelle, celle du composant parent, puis celle du composant racine de l'entité parente (`SceneComponent.cs:72-90`).
- `Position` et `Orientation` ne composent pas un parent comme `WorldMatrixNoScale` le fait : les positions s'additionnent, les orientations aussi (`SceneComponent.cs:112-182`).
- Un changement de transformation ne pose aucun drapeau de caméra : il marque seulement la boîte englobante (`SceneComponent.cs:292-295`), et le déplacement d'un parent ne déclenche rien de local.

### Les données

- `ElementFactory` trouve une classe par son nom simple, sans casse, première trouvée (`CasaEngine/Framework/Assets/ElementFactory.cs:393-398`). `LoadComponent` écarte un type abstrait et le remplace par un composant non résolu (`:315-329`). Les listes de types dérivés écartent les types abstraits (`:425`).
- `ElementFactory.Load` appelle `Load` du type, puis `SerializedProperties.Load` (`ElementFactory.cs:32-40`) : une propriété publique marquée `[SerializedProperty("clé")]` est lue après les clés propres du type, une clé absente garde la valeur courante, et l'éditeur l'écrit par `EditorSerializedPropertyWriter` (ADR-0118). Un `Vector2` s'écrit `{ "x": …, "y": … }`. `SerializedProperties.Copy` sert au `Clone`. Aucun composant du runtime ne s'en sert encore.
- `EditorEntityJsonSerializer.SaveComponentCore` teste `ArcBallCameraComponent` (`CasaEngine.EditorServices/EditorEntityJsonSerializer.cs:226`), `Camera2dComponent` (`:234`), `Camera3dComponent` (`:238`), puis `CameraComponent` (`:242`). `SaveCamera3dComponent` écrit les clés de `CameraComponent` et `fieldOfView` (`:385-389`). `SaveComponent` écrit ensuite les propriétés marquées (`:212-217`).
- Aucun fichier du dépôt ne contient `PerspectiveCameraComponent`, `PictureOffset`, `picture_offset` ni `IsViewOutOfDate` : les noms sont libres.
- 16 fichiers de données nomment un composant caméra : `CasaEngine.Demos/Content/EditorWorld/Entities/camera.entity`, `Projects/SampleProject/Entities/camera.entity`, et 14 mondes de `Projects/` (`RPGDemo`, `BufferedResolutionSample`, `PixelUiSample` ×2, `TimedScreenSample` ×2, `VideoSample`, `VideoSpriteSample` ×2, `VideoSpriteExitSample` ×2, `SpriteDrawOrderSample`, `SpriteCutsceneSample` ×2).
- 69 fichiers `.cs` nomment `ArcBallCameraComponent`, `CameraLookAtComponent` ou `CameraTargeted2dComponent`.

### Où la nouvelle classe apparaîtra d'elle-même

- Le dialogue « Add Component » de l'éditeur : il liste tout composant concret à constructeur public sans paramètre, sous son `[DisplayName]` (`CasaEngine.Editor/Controls/EntityDetailsPanel.cs:1718-1730`).
- L'inspecteur : toute `CameraComponent` reçoit `CameraComponentEditor` (`CasaEngine.Editor/Controls/ComponentEditors/ComponentEditorRegistry.cs:15`), qui montre le champ de vision, les deux profondeurs, puis les autres propriétés publiques dans une section générique.
- Aucun de ces deux endroits ne demande de décision.

### Autres

- `ViewportCameraNavigation` (`CasaEngine/Framework/Scene/Cameras/`) calcule la navigation d'une vue et l'écrit dans une `ArcBallCameraComponent` ; l'éditeur et `ScriptArcBallCamera` le partagent (ADR-0089).
- Les sprites sont soumis avec une position x, y, une rotation de 0 et un z : ils restent dans un plan parallèle à XY.
- Dernière ADR : 0143.
- Tests avant tout changement : voir la note de T0.1.

## Décisions verrouillées

| Réf | Décision |
|---|---|
| D1 | Cible : **une caméra 3D et une caméra 2D**, simples. Leur pose est la transformation de leur composant ; leur projection vient de propriétés. |
| D2 | Le champ de vision des données est respecté. Fait sur `origin/main` (ADR-0095) : ce plan ne le refait pas, il le constate en T0.1. |
| D3 | L'ouverture de la caméra 3D s'exprime en **angle vertical**. |
| D4 | La caméra 3D a un **décalage de l'image**, en fraction de l'écran, en x et en y (projection décentrée). |
| D5 | **Pas d'action de cutscene** pour le décalage dans ce chantier. |
| D6 | Sample de la tranche 1 : une image fixe plus grande que l'écran, un maillage posé dessus, le décalage piloté au clavier. |
| D7 | Le chantier se fait en **trois tranches** : la caméra 3D simple ; les comportements sortis ; le retrait des anciennes classes et la migration des données. |
| D8 | Les comportements courants sont des **composants du moteur**, déclarables dans les données sans DLL de jeu ; les scripts s'ajoutent par-dessus. |
| D9 | Base : `main`, après la fusion de l'éditeur de cutscenes et l'intégration d'`origin/main` (O1). |

## Règles d'exécution pour l'agent

- **Branche dédiée `chantier/simple-cameras`**, créée depuis `main`. Ne jamais committer sur `main`.
- **Une seule tâche à la fois.** Avant de commencer une tâche, remplacer son icône `⏳` par `🚧`. À la fin, lancer la validation indiquée, remplacer l'icône par `✅`, `🧪` ou `⚠️`, ajouter une courte note de validation sous la tâche, puis **créer un commit dédié** qui inclut la mise à jour de ce fichier.
- **Un commit par tâche**, atomique et compilable, message en anglais au format `type(area): summary`.
- **Ne jamais pousser.** Le merge sur `main` reste une décision humaine.
- **Ne rien inventer** : toute API, tout fichier, toute règle utilisée existe dans le dépôt, vient d'une réponse de l'auteur, ou d'une doc officielle citée. Sinon : ⚠️ Blocked, question dans « Points ouverts », et **arrêt**.
- **Build obligatoire** avant ✅ dès que du code est touché (`dotnet build CasaEngine.MonoGame.sln` et `dotnet build CasaEngine.Editor.MonoGame.sln`) ; **tests** `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj`.
- **Ne jamais laisser une tâche en 🚧** à la fin d'une session. **Ne jamais indexer** les modifications préexistantes de l'auteur.
- **Langue** : ce plan en français ; code, messages de commit, docs de `docs/` et ADR en anglais.
- Rappel moteur : pas d'allocation, de LINQ ni de closure dans les chemins chauds ; le runtime ne dépend pas de l'éditeur ; sérialisation additive.

## Légende des statuts

- ⏳ Todo · 🚧 In progress · 🧪 Needs testing · ✅ Done · ⚠️ Blocked

## Les trois tranches

| | Tranche 1 — caméra 3D simple | Tranche 2 — comportements sortis | Tranche 3 — retrait et migration |
|---|---|---|---|
| Résultat | Un monde déclare sans code une caméra perspective posée par sa transformation, roulis compris, avec angle vertical et décalage de l'image | Orbite, visée et suivi sont des composants du moteur qui pilotent une caméra simple ; l'éditeur, les démos et les samples s'en servent | Les anciennes classes de caméra n'existent plus ; les données qui les nomment se lisent encore ou sont migrées |
| Périmètre | Une nouvelle classe concrète de caméra perspective (O2) ; un point d'extension dans `CameraComponent`, sans effet par défaut ; son écriture par l'éditeur ; un sample ; la doc | Les composants de comportement, `ViewportCameraNavigation`, l'éditeur, `CasaEngine.Demos`, `Projects/` | Les trois anciennes classes, les 8 fichiers de données recensés, le chargeur |
| Hors périmètre | Toute modification de `Camera3dComponent`, des trois caméras 3D existantes, de `Camera2dComponent` et de leurs utilisateurs | Le retrait des anciennes classes ; le format des données existantes | — |
| Prérequis | D9 et O1 tranchés ; O2, O3 et O8 tranchés | Tranche 1 fusionnée ; recette de son sample | Tranche 2 fusionnée ; O6 tranché par un ADR |
| Acceptation | Tests de la vue et de la projection de la nouvelle classe verts ; les tests existants (compte de T0.1) verts sans modification ; recette du sample par l'auteur | Les mêmes gestes donnent les mêmes poses qu'avant dans l'éditeur et les démos, prouvé par des tests de non-régression écrits avant le déplacement ; recette de l'auteur dans l'éditeur | Tous les mondes et entités du dépôt se chargent et donnent la même vue qu'avant, prouvé par un test par fichier ; plus aucune référence aux anciennes classes |
| Retour arrière | Annuler les commits de la tranche : la nouvelle classe et le point d'extension disparaissent | Annuler les commits de la tranche | Annuler les commits ; les données migrées sont restaurées par git |
| Arrêts | Un test existant qui casse ; un besoin de modifier une caméra existante, ou `CameraComponent` au-delà du point d'extension de T1.2 | Une pose qui diffère de l'ancienne sans décision de l'auteur | Une donnée qui ne se relit pas à l'identique |
| Risque | Moyen : aucun comportement existant ne change, mais un type et une clé nouveaux entrent dans les données enregistrées. Vérification indépendante du résultat avant de dire la tranche faite | Moyen : 68 fichiers nomment les anciennes classes | **Élevé : format de données**. Relecture indépendante du plan et vérification indépendante du résultat obligatoires |

---

## Tranche 1 — La caméra 3D simple

### Validation globale de la tranche

- Les deux solutions à 0 erreur.
- `CasaEngine.Tests` entièrement vert, sans test existant modifié.
- Smoke manuel : le sample de T1.4 au Launcher. L'image défile sous une caméra fixe ; le maillage reste posé au même endroit de l'image pendant le défilement.

### Vérification indépendante (2026-11-19, `ed354b5c`)

Un vérificateur neuf a cherché à réfuter sept affirmations sur `9125b438..ed354b5c` : **toutes confirmées**, aucun point P0 à P2.

- Aucune caméra existante ne change ; aucun test existant modifié.
- La vue égale l'inverse de `WorldMatrixNoScale` à la première lecture, après `Clone`, après `Load`, après le déplacement d'une entité parente ou la rotation d'un composant parent, sans `Update`.
- Le sens et l'ampleur du décalage, redérivés de `Matrix.CreatePerspectiveOffCenter` et mesurés par `Viewport.Project` sur 320x240, 1920x1080, 2560x1080 et 240x320 : écart maximal 0,00024 pixel.
- L'écriture et la relecture : `picture_offset` écrit une fois, clé absente lue (0, 0).
- Builds à 0 erreur, 8064 tests verts.
- Les données du sample se chargeraient telles quelles, d'après les chemins de chargement lus. **Ce que le sample dessine n'est pas vérifié** (T1.4 reste 🧪).
- Trois remarques P4, sans suite dans cette tranche : `WorldMatrixNoScale` applique deux fois la transformation de l'entité parente à un composant enfant d'un composant racine d'une entité enfant, défaut de `SceneComponent` antérieur à ce plan, que la caméra hérite comme tout composant ; la vue recompose la chaîne des matrices à chaque lecture, sans allocation ; le cas de la nouvelle classe dans `EditorEntityJsonSerializer` appelle la même méthode que celui de `Camera3dComponent`.

### ✅ T0.1 — Recenser sur la base réelle

- Objectif : remplacer la section « État vérifié du dépôt » par des faits lus sur la base du lancement, avec fichier:ligne.
- Sources : les cinq classes de caméra, `CameraComponent`, `ElementFactory` (comment un `type` de composant devient une classe), `EditorEntityJsonSerializer`, les tests de caméra existants, les fichiers de données qui nomment une caméra.
- Étapes :
  1. Vérifier que le champ de vision des données est respecté sur la base (D2). S'il ne l'est pas, ⚠️ Blocked : la base n'est pas celle que le plan attend.
  2. Vérifier qu'aucun type ne porte déjà le nom retenu en O2 pour la recherche par nom d'`ElementFactory`.
  3. Lister les endroits où la nouvelle classe apparaîtra d'elle-même : listes de types dérivés, sélecteurs de composants de l'éditeur. Les écrire sous cette tâche ; si l'un d'eux demande une décision, l'ajouter aux points ouverts.
  4. Compter les tests ; noter le compte attendu.
- Validation : la section est réécrite ; aucune référence de ce plan ne pointe sur un état non relu.
- Commit : `docs(plan): record the verified state of the cameras`
- Validation (2026-11-19, `main` `9125b438`) : la section « État vérifié du dépôt » est réécrite d'après le code lu. Le champ de vision des données est respecté (étape 1). Les noms retenus sont libres (étape 2). La nouvelle classe apparaîtra dans le dialogue « Add Component » et dans l'inspecteur des caméras, sans décision à prendre (étape 3). `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj` : **8031 tests, 0 échec** ; c'est le compte attendu avant les tests de ce plan (étape 4).
- Constat pour T1.3 : le dépôt a déjà un moyen d'écrire et de lire une propriété sans toucher `EditorEntityJsonSerializer`, l'attribut `[SerializedProperty]` (ADR-0118). Le décalage l'emploie avec la clé `picture_offset` décidée en O3 : même clé, même forme `x`, `y`, clé absente = valeur par défaut. `EditorEntityJsonSerializer.cs` ne change donc qu'en T1.2.

### ✅ T1.1 — ADR

- Objectif : enregistrer D1, D3, D4, D7, D8, la forme retenue en O2, le nom de la clé (O3) et le sens du décalage (O8).
- Fichiers : `docs/decisions/<numéro>-<titre>.md`, `docs/decisions/README.md`.
- Validation : l'index liste l'ADR ; le numéro est le suivant de la série du dépôt à ce moment.
- Commit : `docs(decisions): record the simple cameras and their behaviour components`
- Validation (2026-11-19) : ADR-0144, numéro suivant de la série (dernière : 0143), listée dans l'index. (Devenue ADR-0147 le 2026-11-20, à l'intégration d'`origin/main`.)

### ✅ T1.2 — Caméra perspective posée par sa transformation

- Objectif : une nouvelle classe concrète, dérivée de `Camera3dComponent`, nommée selon O2. Sa vue est `Matrix.Invert(WorldMatrixNoScale)` : la transformation monde du composant, parents compris, sans l'échelle. Elle hérite du champ de vision et de la projection de `Camera3dComponent`, sans les modifier.
- Fichiers :
  - la nouvelle classe, à côté des autres caméras ;
  - `CameraComponent.cs` : un seul ajout, la méthode `protected virtual bool IsViewOutOfDate()`, qui rend `false`, et sa consultation par le getter `ViewMatrix` en plus du drapeau existant ;
  - `EditorEntityJsonSerializer.cs` : un cas pour la nouvelle classe, placé **avant** celui de `Camera3dComponent` ;
  - tests dans `CasaEngine.Tests/Scene/`.
- Rafraîchissement : la nouvelle classe redéfinit `IsViewOutOfDate` et rend vrai quand `WorldMatrixNoScale` diffère de la matrice gardée du dernier calcul de la vue. Comparaison de deux matrices, sans allocation. Aucun appel à `Update` n'est nécessaire.
- Effet sur l'existant : aucune des quatre caméras existantes ne redéfinit `IsViewOutOfDate`. Pour `Camera2dComponent`, `ArcBallCameraComponent`, `CameraLookAtComponent` et `CameraTargeted2dComponent`, le getter suit donc le même chemin qu'aujourd'hui. Aucune autre ligne de `CameraComponent`, de `Camera3dComponent` ni de ces classes ne change.
- Étapes :
  1. Tests d'abord, chacun avec un viewport explicite. Sans `InitializeWithWorld` le viewport est à zéro : poser `FarPlane` avant `NearPlane`, qui se borne sur lui (`CameraComponent.cs:49-69`), puis appeler `OnScreenResized(largeur, hauteur)`.
     - la vue égale `Matrix.Invert(WorldMatrixNoScale)` pour une orientation avec lacet, tangage et roulis, à 1e-5 près par terme ;
     - après un changement de position ou d'orientation du composant, la vue lue sans `Update` est à jour ;
     - après le déplacement de l'entité parente, la vue lue sans `Update` est à jour ;
     - un composant mis à l'échelle (2, 3, 4) donne la même vue que sans échelle ;
     - `SetPositionAndTarget(position, cible)`, sur une caméra sans parent, la place en `position` et l'oriente vers `cible` avec `Vector3.Up` pour haut, sans roulis : la vue égale `Matrix.CreateLookAt(position, cible, Vector3.Up)` à 1e-5 près. Avec un parent, la méthode garde ce contrat en repère local ; la documentation le dit ;
     - `Clone` rend une caméra de la nouvelle classe, de même champ de vision et de même transformation ;
     - chargement par le nom de type, écriture, relecture : mêmes valeurs ; le champ de vision chargé est gardé ;
     - pour chacune des quatre classes existantes, le type qui déclare `IsViewOutOfDate` est `CameraComponent` (test par réflexion).
  2. Écrire le point d'extension, puis la classe : vue, `IsViewOutOfDate`, `Load`, `Clone`, `SetPositionAndTarget`.
  3. L'écriture par l'éditeur.
- Validation : build des deux solutions ; tests verts ; aucun test existant modifié.
- Commit : `feat(camera): add a perspective camera posed by its transform`
- Validation (2026-11-19) : `PerspectiveCameraComponentTests`, 14 cas verts ; les deux solutions à 0 erreur ; `CasaEngine.Tests` : 8045 tests, 0 échec (8031 + 14), aucun test existant modifié. Ajouts hors liste : un test du roulis vu à l'écran, et le refus par `ArgumentException` d'une cible confondue avec la position ou à la verticale (un look-at y est indéfini). Runtime : `PerspectiveCameraComponent.cs`, `CameraComponent.cs`. Éditeur : `EditorEntityJsonSerializer.cs`.
- Limite constatée : `CameraComponent.Load` exige les clés `view_distance` et `viewport` ; une caméra écrite à la main doit les porter, comme les caméras existantes.

### ✅ T1.3 — Décalage de l'image

- Objectif : une propriété de la nouvelle classe, lue dans les données et modifiable en jeu, qui décale l'image sans changer le point de vue. La nouvelle classe redéfinit `ComputeProjectionMatrix` ; celle de `Camera3dComponent` n'est pas touchée. L'écriture de la propriété pose `_needToComputeProjectionMatrix`, comme le fait déjà `FieldOfView`.
- Convention, proposée en O8 et enregistrée dans l'ADR de T1.1 : le décalage (x, y) est en fraction de la largeur et de la hauteur entières du viewport. Un point qui paraissait au pixel p de l'écran, y vers le bas, paraît en p + (x × largeur, y × hauteur). Clé absente des données : (0, 0).
- Fichiers : la classe de T1.2 ; `EditorEntityJsonSerializer.cs` ; tests.
- Étapes :
  1. Tests d'abord, chacun avec un viewport explicite et des profondeurs dans l'intervalle 1 à 1000 que `CameraComponent.InitializeWithWorld` impose :
     - décalage nul : la projection égale celle de `Matrix.CreatePerspectiveFieldOfView` à 1e-6 près par terme ;
     - décalage (0,25 ; −0,1) sur un viewport de 320x240 : trois points, aux profondeurs 2, 50 et 900, se projettent 80 pixels plus à droite et 24 pixels plus haut qu'avec un décalage nul, à 0,01 pixel près ;
     - pour chaque décalage de n pixels entiers en x, n de −320 à 320, exprimé n ÷ 320 : le déplacement projeté diffère de n pixels de moins de 0,01 pixel ;
     - le frustum tiré de la vue et de la projection contient un point que le décalage amène à l'écran et exclut un point qu'il en sort ;
     - un JSON de caméra sans la clé du décalage se charge avec un décalage de (0, 0) ;
     - écriture puis relecture : même décalage ;
     - modification en jeu : lire `ProjectionMatrix`, changer le décalage, la relire ; la seconde lecture égale la projection décentrée attendue, à 1e-6 près par terme. Ce test échoue si l'écriture de la propriété ne pose pas le drapeau.
  2. Projection par `Matrix.CreatePerspectiveOffCenter`, bornes tirées de l'angle vertical, du rapport et du décalage.
- Validation : build des deux solutions ; tests verts ; aucun test existant modifié.
- Commit : `feat(camera): shift the picture of the posed perspective camera`
- Validation (2026-11-19) : 11 cas ajoutés à `PerspectiveCameraComponentTests` (25 en tout), verts ; les deux solutions à 0 erreur ; `CasaEngine.Tests` : 8056 tests, 0 échec, aucun test existant modifié. Sur 320x240, tout décalage de −320 à 320 pixels entiers déplace l'image du nombre de pixels demandé à moins de 0,01 pixel. La clé est écrite et lue par `[SerializedProperty]` : `EditorEntityJsonSerializer.cs` n'est pas touché. Avec un décalage nul, la classe appelle la projection de `Camera3dComponent` : la matrice est alors identique bit à bit à celle des autres caméras 3D.

### ✅ T1.4 — Sample

- Objectif : un projet de `Projects/` qui montre la caméra et son décalage (D6).
- Fichiers : le projet, son `README.md`, un test de données comme les autres samples en ont.
- Étapes :
  1. Constater dans le dépôt comment un sample sans DLL lit le clavier pour agir sur un composant. S'il n'y a aucun moyen, appliquer O4.
  2. Un monde : la caméra regarde le long de l'axe z ; une image fixe plus grande que l'écran, face à elle ; un maillage posé à un endroit reconnaissable de l'image. Toute la scène tient entre les profondeurs 1 et 1000.
  3. README : ce qu'il faut observer.
- Validation : test de données vert ; essai au Launcher par l'auteur (🧪 tant qu'il n'est pas fait).
- Commit : `feat(samples): add a sample of the posed perspective camera`
- Validation (2026-11-19) : `Projects/PerspectiveCameraSample` (monde, image 640x240 générée, cube à matériau sans lumière) et `Projects/CasaEngine.PerspectiveCameraSample` (DLL minimale, ajoutée à `CasaEngine.MonoGame.sln`). Étape 1 : aucun sample sans DLL ne lit le clavier pour agir sur un composant (un script se cherche seulement dans une DLL de jeu enregistrée, ADR-0075) ; O4 s'applique, le script `PictureOffsetKeys` écrit `PictureOffset` aux flèches. `PerspectiveCameraSampleDataTests` : 8 cas verts ; les deux solutions à 0 erreur ; `CasaEngine.Tests` : 8064 tests, 0 échec.
- **Reste 🧪 : le sample n'a pas été lancé.** L'agent n'a pas pu ouvrir le Launcher ; le dessin (sprite sous une caméra perspective, cube, défilement) n'est prouvé que par la lecture du code. Essai par l'auteur : voir le README du sample.
- Essai du 2026-11-19, au Launcher, fenêtre de 960x720, sur quatre copies du sample dont seul `picture_offset` change dans le monde : (0 ; 0) montre les deux colonnes du milieu et le cube sur le carré jaune ; (−0,5 ; 0) décale l'image d'une demi-largeur vers la gauche, le cube reste sur son carré et montre les mêmes faces ; (0,5 ; 0) la décale d'une demi-largeur vers la droite ; (0,25 ; −0,1) la décale de 240 pixels à droite et de 72 vers le haut. Le dessin est donc prouvé. **Reste 🧪 : le script clavier `PictureOffsetKeys`**, qu'aucune touche n'a exercé (l'agent n'envoie pas d'entrées à une fenêtre).

### ✅ T1.5 — Documentation

- Objectif : `docs/engine/` décrit la caméra, ses clés, le sens du décalage, l'écart mesuré en T1.3, le contrat de `SetPositionAndTarget` sous un parent, et ce que les tranches 2 et 3 changeront.
- Validation : liens depuis l'index de `docs/`.
- Commit : `docs(engine): describe the posed perspective camera`
- Validation (2026-11-19) : `docs/engine/perspective-camera-component.md`, lié depuis `docs/README.md`. Limite constatée en l'écrivant : la section générique de l'inspecteur n'édite pas un `Vector2` (`ComponentEditorBase.CreatePropertyEditor`), donc le décalage ne s'y voit pas ; non corrigé, hors périmètre de la tranche.

### ✅ T1.6 — Recette de l'auteur

- Validation : « Recette OK » de l'auteur sur le sample.
- Fait le 2026-11-19 : « Recette ok » de l'auteur, sur les quatre captures du Launcher décrites en T1.4. Le script clavier `PictureOffsetKeys` n'a été exercé par personne : l'auteur a validé en le sachant.

---

## Tranche 2 — Comportements sortis

Tâches écrites le 2026-11-19, au lancement de la tranche, après le recensement T2.0. **Soumises à l'auteur : aucun code avant son approbation.**

Relues une fois par un relecteur neuf : REVISE, cinq points P2, tous repris dans cette version (époque 2) : la preuve de T2.5 portait sur un état que le changement ne peut pas modifier ; l'interface du contrôleur à deux composants n'était pas écrite ; un appel à `SetPositionAndTarget` sur une caméra pilotée par l'orbite était perdu sans le dire ; la comparaison d'images de T2.7 ne prouvait pas une pose ; l'entrée de l'orbite dans un monde n'avait pas de test.

Relecture de clôture de l'époque 2 : REVISE, deux points P2, repris dans cette version (époque 3) : T2.5 demandait d'ajouter l'orbite à une entité que `EditorCameraFactory.CreateCameraEntity` crée lui-même, et son test recopiait la construction du panneau au lieu de l'exécuter ; le test de T2.7 ne pouvait pas appeler la méthode de cadrage, `CasaEngine.Tests` ne référençant pas `CasaEngine.Demos`.

Relecture de clôture de l'époque 3 : **READY**.

**Approuvée par l'auteur le 2026-11-19 (« Approuvé en mode AUTO »), avec les propositions de O9 et de O10.**

### Décisions de la tranche (réponses de l'auteur du 2026-11-19)

| Réf | Décision |
|---|---|
| D10 | La tranche 1 est fusionnée dans le `main` local (`ae2a42be`, non poussé). La tranche 2 part de `main`, sur la branche `chantier/camera-behaviours`. |
| D11 | **Pas de composant de visée.** Les usages de `CameraLookAtComponent` passent sur `PerspectiveCameraComponent.SetPositionAndTarget`. |
| D12 | **Un composant de suivi 2D est écrit**, bien qu'aucun usage du dépôt n'existe pour `CameraTargeted2dComponent`. |
| D13 | `ScriptArcBallCamera` garde son nom. Il pilote le composant d'orbite s'il est sur l'entité, sinon l'ancienne `ArcBallCameraComponent`. |

### État vérifié pour la tranche 2 (T2.0, relevé le 2026-11-19 sur `ae2a42be`)

Relevé par trois lectures séparées (runtime, éditeur, démos et tests), puis recoupé sur les points qui portent une décision.

- **Orbite.** `ArcBallCameraComponent` garde `Target`, `Distance`, `Yaw`, `Pitch` ; son orientation est `AxisAngle(Up, −Yaw) × AxisAngle(Right, Pitch)` (`ArcBallCameraComponent.cs:272-277`) ; sa vue est `CreateLookAt(Target − Direction × Distance, Target, Up)` et écrit `Position` au passage (`:153-158`). `SetCamera(position, cible, up)` en déduit distance, lacet et tangage ; `up` n'est pas utilisé (`:245-270`). Elle lit `target`, `distance`, `yaw`, `pitch` (`:279-287`).
- **Navigation de vue.** `ViewportCameraState` (pivot, distance, lacet, tangage) se lit et s'écrit dans une `ArcBallCameraComponent` par `From` et `ApplyTo` (`CasaEngine/Framework/Scene/Cameras/ViewportCameraNavigation.cs:60-74`). `ViewportCameraNavigation.Update` ne travaille que sur l'état. `ScriptArcBallCamera` et `EditorViewportCameraController` passent par ces deux méthodes et écrivent en plus `FieldOfView`.
- **Visée.** `CameraLookAtComponent` ne fait que `CreateLookAt(Position, Target, Up)` ; elle n'a pas de clé propre.
- **Suivi 2D.** `CameraTargeted2dComponent` ne suit rien : son `Update` ne déplace le décalage que si la cible est **dans** la zone morte, puis teste si elle en est sortie, ce qui est alors toujours faux (`CameraTargeted2dComponent.cs:52-70`). Ses réglages ne sont lus d'aucune donnée. Aucun fichier de l'éditeur, des démos, de `Projects/` ni des données ne la nomme. Il n'y a donc **aucun comportement existant à préserver** : le composant de D12 est spécifié à neuf (O9).
- **Utilisateurs de `ArcBallCameraComponent`.** Runtime : `ViewportCameraNavigation.cs`, `ScriptArcBallCamera.cs`. Éditeur : `WorldViewportPanel.cs`, `EditorViewportCameraController.cs`, `EditorEntityJsonSerializer.cs`. Démos : `Demo.cs` et 20 démos, la plupart par `SetCamera`, quatre par un test de type. `Projects/SandBoxGame/SandBoxGame.cs`. Données : `CasaEngine.Demos/Content/EditorWorld/Entities/camera.entity`, `Projects/SampleProject/Entities/camera.entity`.
- **Utilisateurs de `CameraLookAtComponent`.** Runtime : `World.CreateDefaultCamera` (`World.cs:509-511`), `EntityPolicyResolver.cs:132`. Éditeur : cinq aperçus (`SpritePreviewViewport`, `ParticlePreviewViewport`, `ModelPreviewViewport`, `MaterialPreviewViewport`, `AnimationClipPreviewPanel`) et deux rendus de vignettes (`SpriteSceneThumbnailRenderer`, `ParticleSceneThumbnailRenderer`). Démos : `DemosGame.cs` et sept démos. Aucune donnée.
- **Politique d'entité.** `EntityPolicyResolver` range `CameraLookAtComponent` et `CameraTargeted2dComponent` sous `DynamicDefault` (`EntityPolicyResolver.cs:131-138`) ; `ArcBallCameraComponent` et `PerspectiveCameraComponent` n'y figurent pas (O10).
- **Tests qui figent des poses.** `ArcBallCameraComponentTests` (5), `ViewportCameraNavigationTests` (25, sur l'état, plus une égalité avec la position de la caméra). Une vingtaine d'autres fichiers de test prennent une ancienne caméra comme simple support.
- **Ordre de mise à jour d'une entité** : composant racine, autres composants, entités enfants, puis script (`Entity.cs:511-542`).

### Conception

- **`OrbitCameraComponent`**, un `EntityComponent` posé sur l'entité de la caméra. Propriétés `Target`, `Distance`, `Yaw`, `Pitch`, lectures `Direction` et `Right`, et `SetCamera(position, cible)`, avec les conventions, les formules et les clés de `ArcBallCameraComponent` (`target`, `distance`, `yaw`, `pitch`). Il écrit la transformation locale du composant caméra de son entité : position `Target − Direction × Distance`, orientation `AxisAngle(Up, −Yaw) × AxisAngle(Right, Pitch)`.
  - Quand il écrit : à chaque écriture d'une propriété, à `Initialize` de l'entité et à `InitializeWithWorld`. Rien par image. Il ne touche ni au champ de vision ni à la projection.
  - Il cherche la caméra par `Owner.GetComponent<CameraComponent>()` à chaque écriture et ne garde aucune référence : le constructeur de copie de `EntityComponent` recopie `Owner` de la source (`EntityComponent.cs:31-34`), une référence gardée viserait la caméra de l'entité d'origine après un clonage.
  - **Il est le propriétaire de la pose.** Un `SetPositionAndTarget` appelé directement sur une caméra qu'il pilote est écrasé à sa prochaine écriture : c'est à lui qu'on demande un cadrage, par `SetCamera`. L'ADR de T2.1 le dit. `ArcBallCameraComponent.SetPositionAndTarget` nourrissait l'orbite (`ArcBallCameraComponent.cs:160-163`) : les appelants concernés sont recensés en T2.7.
- **`ViewportCameraState`** reçoit `From(OrbitCameraComponent)` et `ApplyTo(OrbitCameraComponent)`, à côté des deux méthodes existantes, qui restent.
- **`ScriptArcBallCamera`** cherche un `OrbitCameraComponent` et une `Camera3dComponent` sur son entité ; s'il n'y a pas d'orbite, il garde le chemin actuel (D13).
- **`Follow2dCameraComponent`**, spécifié en O9.
- Les anciennes classes restent en place, lues par les données existantes et par les tests qui s'en servent de support. Leur retrait est la tranche 3.

### Validation globale de la tranche

- Les deux solutions à 0 erreur ; `CasaEngine.Tests` vert ; aucun test existant modifié, sauf `EditorViewportCameraControllerTests`, dans les limites écrites en T2.5.
- Pour une grille de cibles, distances, lacets et tangages, la vue d'une `PerspectiveCameraComponent` pilotée par `OrbitCameraComponent` égale celle d'une `ArcBallCameraComponent` de mêmes valeurs. Ce test est écrit avant tout déplacement d'usage.
- La pose des démos se prouve par construction, pas par image : mêmes arguments de cadrage qu'avant (constaté dans le diff), et égalité des vues pour des arguments égaux (tests de T2.2 pour l'orbite, de T1.2 pour la visée).
- Recette de l'auteur : la navigation de la vue 3D de l'éditeur, les aperçus, et les démos.

### ✅ T2.0 — Recenser les usages

- Fait le 2026-11-19 : section « État vérifié pour la tranche 2 ».

### ✅ T2.1 — ADR

- Objectif : enregistrer D11, D12, D13, la forme du composant d'orbite, la spécification du suivi (O9) et la réponse à O10.
- Fichiers : `docs/decisions/0145-<titre>.md` (devenue ADR-0148 le 2026-11-20, fichier `0148-<titre>.md`), `docs/decisions/README.md`.
- Commit : `docs(decisions): record the camera behaviour components`
- Validation (2026-11-19) : ADR-0148 (numérotée ADR-0145 jusqu'au 2026-11-20), listée dans l'index. O10 tranché par la lecture : un composant que `EntityPolicyResolver` ne liste pas laisse le constructeur intact, et celui-ci retombe sur `DynamicDefault` (`EntityPolicyResolver.cs:71-86`), la politique que demandait `CameraLookAtComponent`. `EntityPolicyResolver.cs` ne change donc pas.

### ✅ T2.2 — Composant d'orbite

- Fichiers : `CasaEngine/Framework/Scene/Entities/Components/OrbitCameraComponent.cs` ; `CasaEngine.EditorServices/EditorEntityJsonSerializer.cs` (écriture des quatre clés) ; tests dans `CasaEngine.Tests/Scene/`.
- Étapes :
  1. Tests d'abord :
     - égalité des vues avec `ArcBallCameraComponent` sur une grille : 3 cibles, 3 distances, 8 lacets sur le tour complet, 5 tangages de −89 à 89 degrés, à 1e-4 près par terme ; sur la même grille, `Direction` et `Right` égalent ceux de `ArcBallCameraComponent`, et la position du composant caméra égale `Target − Direction × Distance` ;
     - `SetCamera(position, cible)` donne les mêmes distance, lacet et tangage que `ArcBallCameraComponent.SetCamera`, et une vue égale à `Matrix.CreateLookAt(position, cible, Vector3.Up)` à 1e-4 près, sur les cas de `ArcBallCameraComponentTests` ;
     - `SetCamera` avec une position égale à la cible garde le lacet et le tangage, comme l'ancienne classe ;
     - une écriture de `Target`, `Distance`, `Yaw` ou `Pitch` est vue à la lecture suivante de `ViewMatrix`, sans `Update` ;
     - **entrée sans écriture de propriété** : une entité chargée d'un JSON qui porte une caméra simple dont `local_transform` est volontairement autre, et une orbite, puis `Initialize` de l'entité, sans écrire aucune propriété : la vue égale celle d'une `ArcBallCameraComponent` de mêmes valeurs. Deux cas : la caméra est le composant racine ; la caméra est enfant d'un `TransformComponent` racine. Ces tests échouent si l'orbite n'écrit pas à l'initialisation ;
     - une orbite ajoutée par `AddComponent` à une entité déjà initialisée pose la caméra ;
     - une entité clonée : l'orbite du clone pose la caméra du clone ; la caméra de l'entité d'origine ne bouge pas quand on écrit dans l'orbite du clone ;
     - un `SetPositionAndTarget` direct sur la caméra est écrasé par l'écriture suivante de l'orbite (le contrat écrit en « Conception ») ;
     - un composant d'orbite sur une entité sans caméra ne lève rien et n'écrit rien ;
     - écriture par l'éditeur puis relecture : mêmes valeurs ; `Clone` : mêmes valeurs ;
     - aucune allocation dans les écritures de propriétés (mesure par `GC.GetAllocatedBytesForCurrentThread`, comme les tests du dépôt qui le font déjà).
  2. Écrire le composant, puis son écriture par l'éditeur.
- Validation : build des deux solutions ; tests verts.
- Commit : `feat(camera): add an orbit behaviour component for the posed camera`
- Validation (2026-11-19) : `OrbitCameraComponentTests`, 16 cas verts, dont l'égalité des vues avec `ArcBallCameraComponent` sur les 360 points de la grille à 1e-4 près ; les deux solutions à 0 erreur ; `CasaEngine.Tests` : 8080 tests, 0 échec. Runtime : `OrbitCameraComponent.cs`. Éditeur : `EditorEntityJsonSerializer.cs`.

### ✅ T2.3 — Navigation de vue et script

- Fichiers : `ViewportCameraNavigation.cs` (deux méthodes ajoutées à `ViewportCameraState`) ; `ScriptArcBallCamera.cs` ; tests.
- Étapes :
  1. Tests d'abord : `From` puis `ApplyTo` sur un composant d'orbite rendent le même état ; le script, sur une entité à caméra simple et orbite, écrit le même état que sur une `ArcBallCameraComponent` pour les mêmes entrées ; sur une entité à `ArcBallCameraComponent` seule, il se comporte comme avant (`EngineOwnedScriptObjectsTests` et `EditorWorldPlaySnapshotTests` verts sans modification).
  2. Écrire les deux méthodes, puis le choix du chemin dans le script.
- Commit : `feat(camera): drive the orbit component from the viewport navigation`
- Validation (2026-11-19) : `ScriptArcBallCameraOrbitTests`, 10 cas verts : pour sept gestes (orbite, molette, regard, travelling, panoramique, vol, champ de vision), le script donne à une caméra simple pilotée par une orbite l'état, le champ de vision et la vue qu'il donne à une `ArcBallCameraComponent`, à 1e-4 près. Pour le rendre testable sans jeu, `Update` garde la lecture des entrées et appelle une méthode `internal ApplyNavigation`, que les tests appellent. `EngineOwnedScriptObjectsTests` et `EditorWorldPlaySnapshotTests` verts sans modification ; les deux solutions à 0 erreur ; `CasaEngine.Tests` : 8090 tests, 0 échec.

### ✅ T2.4 — Composant de suivi 2D

- Prérequis : O9 tranché.
- Fichiers : `Follow2dCameraComponent.cs` ; `EditorEntityJsonSerializer.cs` ou `[SerializedProperty]` selon les types de ses réglages ; tests.
- Étapes : tests d'abord, un par règle de O9 (cible dans la zone, sortie par chaque bord, limites, cible absente ou retirée du monde, écriture et relecture, aucune allocation dans `Update`) ; puis le composant.
- Commit : `feat(camera): add a 2d follow behaviour component`
- Validation (2026-11-19) : `Follow2dCameraComponentTests`, 19 cas verts ; les deux solutions à 0 erreur ; `CasaEngine.Tests` : 8109 tests, 0 échec. `target_entity_name` et `dead_zone` passent par `[SerializedProperty]` ; `limits` (x, y, w, h entiers, comme l'ancienne classe) est lue par `Load`, clé facultative, et écrite par `EditorEntityJsonSerializer`. Une cible détruite (`ToBeRemoved`) est lâchée. La recherche par nom se teste par la méthode `internal ResolveTarget`, sur un `World` sans jeu. Limite : le composant écrit la position locale de la caméra et lit la position monde de la cible ; une caméra sous un parent n'est pas prévue.

### ✅ T2.5 — Vue 3D de l'éditeur

- Fichiers : `EditorViewportCameraController.cs`, `WorldViewportPanel.cs`, leurs tests.
- Objectif : la vue 3D crée une `PerspectiveCameraComponent` et un `OrbitCameraComponent` ; le contrôleur écrit dans l'orbite. `EditorCameraFactory` prend déjà une `Camera3dComponent` et ne change pas.
- Interface du contrôleur après la tâche (classe `internal`) :
  - `PerspectiveCameraComponent CreateCameraComponent()` : la caméra, sans réglage d'orbite ;
  - `OrbitCameraComponent CreateOrbitComponent()` : l'orbite, avec la cible, la distance, le lacet et le tangage du contrôleur ;
  - `void ApplyTo(OrbitCameraComponent orbit)` ;
  - `Update(GameTime gameTime, OrbitCameraComponent orbit, Camera3dComponent camera, …)`, la suite des paramètres inchangée : l'état va à l'orbite, le changement de champ de vision à la caméra.
  - `Entity CreateCameraEntity(string name, World world, out PerspectiveCameraComponent camera, out OrbitCameraComponent orbit)` : **la seule construction de la caméra de vue**. Elle appelle `CreateCameraComponent`, puis `EditorCameraFactory.CreateCameraEntity`, qui crée l'entité, l'initialise et la met dans le monde (`EditorCameraFactory.cs:33-48`), puis ajoute l'orbite par `AddComponent` à cette entité déjà initialisée : le chemin que couvre le test « orbite ajoutée par `AddComponent` » de T2.2.
- `WorldViewportPanel` appelle cette méthode (à la place de `:1371-1372`) et garde `_camera` (la caméra simple) et un nouveau champ pour l'orbite. Ses trois lectures changent ainsi : `_camera.Target` (`:1323`) et `_camera.Right` (`:1324`) deviennent celles de l'orbite ; `_camera.FieldOfView` (`:886`) reste sur la caméra.
- Modifications permises dans `EditorViewportCameraControllerTests`, et elles seules : la création des objets (une caméra et une orbite au lieu d'une `ArcBallCameraComponent`), la liste d'arguments des appels à `Update`, et l'objet lu pour `Distance`, `Target`, `Yaw`, `Pitch` (l'orbite). Aucune valeur attendue ne change. La note de la tâche liste les lignes touchées.
- Étapes :
  1. Vérifier que la profondeur lointaine infinie de `EditorCameraFactory` passe par la projection de `Camera3dComponent` (décalage nul) ; les cas de `InfiniteFarPlaneTests` sont rejoués sur la nouvelle classe dans un nouveau fichier de test.
  2. **Référence avant tout changement.** La méthode de construction ci-dessus est d'abord écrite sur l'existant : elle rend une `ArcBallCameraComponent`, sans orbite, et le panneau l'appelle. Un nouveau test l'appelle lui aussi, puis rejoue `EditorCameraFactory.InitializeWithWorld` comme le panneau le fait au changement de monde (`WorldViewportPanel.cs:1804-1806`), applique une suite fixée d'états par `SetState` et `ApplyTo`, et note pour chacun `ViewMatrix` et la position de la caméra. Les valeurs relevées avec `ArcBallCameraComponent` sont écrites en dur dans le test, qui est committé vert avec cette tâche.
  3. Changer le contrôleur, puis le panneau. Le test de l'étape 2 doit rester vert, à 1e-4 près par terme, en appelant la même méthode de construction, qui rend maintenant la caméra simple et l'orbite : il échoue si cette méthode ne pose pas l'orbite sur l'entité ou si la réinitialisation perd la pose. Ce que le test ne couvre pas : un panneau qui n'appellerait pas la méthode ; c'est l'essai dans l'éditeur qui le montre.
- Si le banc de test ne permet pas de rejouer `EditorCameraFactory.InitializeWithWorld` sans carte graphique, ⚠️ Blocked : la question va aux points ouverts.
- Validation : build ; tests ; essai dans l'éditeur par l'auteur (🧪 tant qu'il n'est pas fait).
- Commit : `refactor(editor): drive the 3d viewport through the orbit component`
- Validation (2026-11-19) : la méthode de construction a d'abord été écrite sur `ArcBallCameraComponent` ; les vues et positions de la caméra y ont été relevées pour l'état initial et quatre états, dont un après le changement de monde, puis écrites en dur dans `EditorViewportCameraPoseTests` (3 cas). Après la bascule, le même test est vert sur la caméra simple et l'orbite, à 1e-4 près (relatif au-delà d'une unité). Le banc : un `World` sur un `CasaEngineGame` construit sans constructeur, en politique d'éditeur, sans carte graphique. `EditorViewportCameraControllerTests` : 22 lignes ajoutées et 1 changée, dans les limites permises (une ligne `var orbit = controller.CreateOrbitComponent();` après chacune des 10 créations de caméra, un argument `orbit,` ajouté à chacun des 11 appels à `Update`, `camera.Distance` devenu `orbit.Distance`) ; aucune valeur attendue changée. Les deux solutions à 0 erreur ; `CasaEngine.Tests` : 8112 tests, 0 échec.
- Écart avec l'étape 1 : des six cas de `InfiniteFarPlaneTests`, seul celui de la projection est rejoué sur la nouvelle caméra (dans `EditorViewportCameraPoseTests`) ; les autres ne dépendent que de cette projection, qui est celle de `Camera3dComponent` quand le décalage est nul.
- **Reste 🧪 : l'essai dans l'éditeur par l'auteur** (navigation de la vue 3D, dépôt d'un asset dans la vue, passage en Play et retour). Le test ne couvre pas un panneau qui n'appellerait pas la méthode de construction.

### ✅ T2.6 — Aperçus, vignettes et caméra par défaut

- Fichiers : les sept fichiers de l'éditeur listés plus haut ; `World.cs` (`CreateDefaultCamera`) ; `EntityPolicyResolver.cs` selon O10.
- Étapes :
  1. Pour chacun des huit appels à `SetPositionAndTarget`, lire la direction visée. `PerspectiveCameraComponent` refuse une visée à la verticale : **si un appel peut viser à la verticale, ⚠️ Blocked**, la question va aux points ouverts.
  2. Remplacer le type construit ; rien d'autre ne change dans ces fichiers.
- Délégation : éditions de même forme sur huit fichiers, à un `mech-executor`, après l'étape 1 faite en session principale.
- Validation : build ; tests ; essai des aperçus par l'auteur.
- Commit : `refactor(editor): pose the preview cameras by their transform`
- Validation (2026-11-19) : étape 1, les huit visées lues une à une : les deux aperçus de sprite visent le long de z depuis une distance d'au moins 2,5 ; les aperçus de particules, de modèle, de matériau et d'animation, les vignettes de particules et la caméra par défaut d'un monde ont tous un écart en z d'au moins 3 entre position et cible. Aucune ne peut viser à la verticale. Étape 2 : le nom de la classe construite change dans les huit fichiers, 15 lignes, rien d'autre. Fait en session principale et non par un `mech-executor` : un seul remplacement de nom par fichier ne justifiait pas une délégation. `EntityPolicyResolver.cs` inchangé (O10). Les deux solutions à 0 erreur ; `CasaEngine.Tests` : 8112 tests, 0 échec.
- **Reste 🧪 : l'essai des aperçus et des vignettes dans l'éditeur par l'auteur.**

### ✅ T2.7 — Démos et bac à sable

- Fichiers : `CasaEngine.Demos/Demo.cs`, `DemosGame.cs`, les 27 démos recensées, `Projects/SandBoxGame/SandBoxGame.cs`.
- Objectif : `Demo.CreateCamera` crée une caméra simple, un composant d'orbite et le script ; les démos cadrent par l'orbite ; celles qui créaient une `CameraLookAtComponent` créent une caméra simple, sans orbite.
- Règle de cadrage : une méthode statique `DemoCameraFraming.Frame(CameraComponent camera, Vector3 position, Vector3 target)`, dans son propre fichier `CasaEngine.Demos/DemoCameraFraming.cs`, sans autre dépendance que le runtime. Elle cadre par `SetCamera` de l'orbite de l'entité de la caméra s'il y en a une, sinon par `SetPositionAndTarget`. Le fichier est lié dans `CasaEngine.Tests` comme `DemoControl.cs` l'est déjà (`CasaEngine.Tests.csproj:35-42`). Toute démo qui cadre une caméra venue de `Demo.CreateCamera` passe par elle.
- Appelants recensés au-delà des 27 fichiers qui nomment une ancienne classe, relevés par `rg "SetPositionAndTarget\(|\.Direction\b"` sur `CasaEngine.Demos` et `Projects` :
  - `TileMapSurfaceScreenDemo.cs:119` et `WorldSpaceUIDemo.cs:113` appellent `SetPositionAndTarget` sur la caméra de `Demo.CreateCamera` : ils passent par la méthode de cadrage ;
  - `Collision3dBasicDemo.cs:205` et `:229` lisent `Direction` : ils la lisent sur l'orbite ;
  - les huit démos à `CameraLookAtComponent` créent leur propre caméra, sans orbite : leur `SetPositionAndTarget` reste un appel direct.
- Étapes :
  1. `Demo.cs` et `DemosGame.cs` en session principale : ils fixent la forme que les démos suivent. Un test de `CasaEngine.Tests` construit une entité à caméra simple et orbite, appelle `DemoCameraFraming.Frame`, le même symbole que les démos, écrit une valeur dans l'orbite sans la changer, et constate que la vue n'a pas bougé. Il échoue si `Frame` cadre par `SetPositionAndTarget` une caméra pilotée par une orbite. Ce qu'il ne couvre pas : que `Demo.CreateCamera` pose bien l'orbite et qu'une démo appelle bien `Frame` ; le premier point se lit dans `Demo.cs`, le second se constate par la recherche de l'étape 4.
  2. Même contrôle de visée verticale qu'en T2.6 pour les huit démos à `CameraLookAtComponent`.
  3. Les démos, par un `executor` : quatre d'entre elles testent le type de la caméra et demandent un jugement. Consigne : les arguments de position et de cible de chaque cadrage restent ceux d'avant, au caractère près.
  4. Rejouer la recherche ci-dessus : aucun `SetPositionAndTarget` ne reste sur une caméra pilotée par une orbite.
- Validation : build ; tests ; le diff montre des arguments de cadrage inchangés, ce que la note de la tâche atteste démo par démo. Ce que la tâche ne prouve pas : le premier geste de souris après le cadrage dans une démo lancée. La règle de cadrage est couverte par le test de l'étape 1 ; la démo lancée l'est par la recette de l'auteur seule (T2.9), 🧪 jusque-là.
- Commit : `refactor(demos): build the demo cameras from the posed camera and the orbit component`
- Validation (2026-11-19) : `DemoCameraFraming.cs`, `Demo.cs` et `DemosGame.cs` en session principale ; `DemoCameraFramingTests` (3 cas) appelle le même `Frame` que les démos, le fichier étant lié dans `CasaEngine.Tests`. Les démos par un `executor` : 25 démos et `SandBoxGame.cs`, relues sur le diff ; les arguments de position et de cible de chaque cadrage sont inchangés au caractère près, le troisième argument `up`, que l'ancienne classe n'utilisait pas, est retiré. Les deux solutions à 0 erreur ; `CasaEngine.Tests` : 8115 tests, 0 échec. Étape 4 : il reste huit `SetPositionAndTarget`, tous sur une caméra sans orbite (six démos qui créent leur caméra fixe, la caméra de l'écran principal, et `DemoCameraFraming` lui-même).
- Cas particuliers : `SandBoxGame` cadre par `SetCamera` de l'orbite, n'ayant pas accès à `CasaEngine.Demos` ; `RenderToTextureDemo` et `ViewManagerSandbox` ont une caméra qui vise à la verticale ou presque, valide parce qu'elle passe par l'orbite, comme avant par l'ancienne caméra ; `Collision3dBasicDemo` lit `Direction` sur l'orbite.
- **Non converti : `EditorWorldDemo.cs`.** Sa caméra vient d'un fichier, `CasaEngine.Demos/Content/EditorWorld/Entities/camera.entity`, qui nomme `ArcBallCameraComponent` ; elle reste donc une ancienne caméra, pilotée par le chemin conservé de `ScriptArcBallCamera`, jusqu'à la tranche 3, qui traite les données.
- **Reste 🧪 : aucune démo n'a été lancée.** La recette de l'auteur porte sur les démos, premier geste de souris compris.

### ✅ T2.8 — Documentation

- Fichiers : `docs/engine/` (les deux composants), `docs/editor/viewport-camera-controls.md`, `docs/editor/editor-2d-viewport.md`, `docs/engine/demos-main-menu.md`, `docs/README.md`.
- Commit : `docs(engine): describe the camera behaviour components`
- Validation (2026-11-19) : `docs/engine/camera-behaviour-components.md`, lié depuis `docs/README.md` et depuis la doc de la caméra simple ; `docs/editor/viewport-camera-controls.md` et `docs/editor/editor-2d-viewport.md` nomment la nouvelle caméra de la vue 3D. `docs/engine/demos-main-menu.md` ne nomme que `ScriptArcBallCamera`, qui garde son nom : inchangé.

### ✅ T2.9 — Vérification indépendante et recette

- Un vérificateur neuf cherche à réfuter l'égalité des poses, l'absence de changement pour les données existantes et l'état des builds et des tests.
- Recette de l'auteur : navigation de la vue 3D de l'éditeur, aperçus, démos.
- Première vérification (2026-11-19, `baf21305`) : **REFUTED**, un point P2. Avec une distance négative, `OrbitCameraComponent` orientait la caméra le long de `Direction` au lieu de la tourner vers la cible, comme le fait `ArcBallCameraComponent` ; `WorldSpaceUIDemo`, qui part d'une distance de −18,25, aurait démarré dos à sa scène. Les neuf autres affirmations étaient confirmées. Corrigé : à distance négative, l'orientation fait un demi-tour autour de son axe vertical ; deux tests ajoutés (grille de 360 points à distances négatives, valeurs de départ de cette démo). Deux remarques P3 traitées au passage : le suivi 2D ne signale plus une cible manquante pendant l'initialisation du monde, où la cible peut ne pas être encore listée ; le commentaire de l'orbite dit maintenant qu'une caméra ajoutée après elle à une entité déjà initialisée n'est posée qu'à l'écriture suivante. Six commentaires de démos qui décrivaient l'ancienne caméra sont corrigés. `CasaEngine.Tests` : 8117 tests, 0 échec.
- Contre-vérification par un vérificateur neuf (2026-11-19, `6ecb692a`) : **CONFIRMED**, aucun point P0 à P2. Distances négatives : sur 200 000 tirages, direction, droite et position égales à celles de l'ancienne caméra à moins de 5e-7 ; les valeurs de départ de `WorldSpaceUIDemo` donnent la même vue ; onze entrées de navigation appliquées aux deux caméras donnent le même état et la même vue à chaque pas. Distances positives non régressées. Builds à 0 erreur, 8117 tests verts. Remarque P4 sans suite : à moins d'un millième d'unité de la cible, la vue de l'ancienne caméra perd sa précision, celle de l'orbite non.
- Recette de l'auteur sur `WorldSpaceUIDemo` (2026-11-19) : la molette fait sauter la caméra, et clic droit + flèches déplacent à l'envers. Cause lue dans le code : la démo part d'une distance négative, dont `ViewportCameraNavigation` ignore le signe (commandes en miroir) et que la molette ramène à `MinDistance` (demi-tour). La navigation et les valeurs de la démo sont les mêmes que sur `main` : défaut antérieur à la tranche, non rejoué à l'écran sur l'ancienne version. Corrigé sur accord de l'auteur : la démo part de la même pose écrite avec une distance positive (18,25, lacet 0, inclinaison −0,165) ; un test prouve que la vue est celle des anciennes valeurs. `CasaEngine.Tests` : 8118 tests, 0 échec. Reste à revoir à l'écran par l'auteur.
- Recette de l'auteur (2026-11-19) : `WorldSpaceUIDemo` corrigée acceptée (« Recette OK ») ; vue 3D de l'éditeur acceptée, T2.5 passe en ✅. L'auteur n'a pas déclaré avoir essayé les aperçus et vignettes (T2.6) ni les autres démos (T2.7) : elles restent en 🧪, et T2.9 avec elles.
- Recette de l'auteur (2026-11-19, « tout est OK ») : aperçus et vignettes de l'éditeur et autres démos acceptés. T2.6, T2.7 et T2.9 passent en ✅ : la tranche 2 est recettée. Elle n'est ni fusionnée ni poussée : la fusion attend la demande de l'auteur.
- **Reste 🧪 : la recette de l'auteur.** Ni l'éditeur ni les démos n'ont été lancés.

## Tranches 3 et 4 — Enveloppe

Écrite le 2026-11-19 à la demande de l'auteur (« Oui, écris le plan »), après ses trois réponses du même jour. **Soumise à l'auteur : aucun code avant son approbation.** L'auteur a demandé d'enchaîner les tâches en mode AUTO une fois le plan approuvé.

Relecture de clôture de l'époque 2 (enveloppe et tranche 3, `d06f98b3`) : REVISE, deux points P2 ; les trois points de l'époque 1 sont confirmés résolus. Disposition : les deux sont **corrigés** dans cette version (époque 3) : T3.4 se contredisait sur le fichier qui reçoit les équivalents de `ArcBallCameraComponentTests` ; le test de données comparait à des termes écrits en dur sans tolérance fixée. Relecture de clôture de l'époque 3 (`13b66aa7`) : **READY**.

Relectures par des relecteurs neufs (2026-11-19, `759a25ad`) : tranche 4 **READY** ; enveloppe et tranche 3 **REVISE**, trois points P2, tous repris dans cette version (époque 2) : la forme migrée de `camera.entity` ne pouvait pas être celle que le sérialiseur écrit avec le diff annoncé ; sept tests, et non trois, comparaient à l'ancienne classe, sans seconde référence pour `Direction`, `Right`, la position, les valeurs de `SetCamera`, le champ de vision et la vue du script ; `Camera3dFieldOfViewTests` n'avait pas de tâche propriétaire.

Les réponses de l'auteur élargissent le cadrage d'origine : le travail restant se fait en **deux tranches**, chacune livrable et approuvable seule.

| | Tranche 3 — retrait, migration, fusion | Tranche 4 — caméra 2D posée par sa transformation |
|---|---|---|
| Résultat | `ArcBallCameraComponent`, `CameraLookAtComponent`, `CameraTargeted2dComponent` et `Camera3dComponent` n'existent plus ; il reste une caméra 3D, `PerspectiveCameraComponent` ; les deux fichiers de données qui nommaient une ancienne caméra sont réécrits | `Camera2dComponent` se place par la transformation de son composant, comme la caméra 3D ; sa clé `target` disparaît ; les 14 mondes du dépôt sont réécrits |
| Périmètre | Les quatre classes, leurs utilisateurs (runtime, éditeur, démos, tests), deux `camera.entity`, la doc | `Camera2dComponent`, ses utilisateurs (runtime, éditeur, démos, `Projects/CasaEngine.RPGDemo`, tests), l'écriture par l'éditeur, 14 mondes de `Projects/`, la doc |
| Hors périmètre | `Camera2dComponent` ; `ScriptArcBallCamera` garde son nom (D13) ; aucun chemin de traduction dans le chargeur (D14) | La projection de la caméra 2D (`Zoom`, taille du viewport) ; le rendu ; les projets hors de ce dépôt |
| Prérequis | Tranche 2 fusionnée dans `main` (fait, `851bfa93`) ; D14 et D15 | Tranche 3 terminée (sa branche sert de base si elle n'est pas fusionnée) ; D16 |
| Branche | `chantier/camera-removal`, depuis `main` `851bfa93` | `chantier/camera-2d-pose`, depuis `main` si la tranche 3 y est fusionnée, sinon depuis la tête de `chantier/camera-removal` |
| Acceptation | Plus aucune occurrence des quatre noms dans un `.cs` ; les deux `camera.entity` se chargent et donnent la vue et la projection d'avant, prouvé par un test par fichier écrit **avant** la migration ; les deux solutions à 0 erreur ; `CasaEngine.Tests` vert | Pour chacun des 14 mondes, la vue et la projection de la caméra chargée égalent celles d'avant, prouvé par un test par fichier écrit **avant** le changement et non modifié ensuite ; plus aucune clé `target` sous un nœud `Camera2dComponent` ; builds et tests verts |
| Retour arrière | Annuler les commits de la branche ; les données sont restaurées par git | Annuler les commits de la branche ; les données sont restaurées par git |
| Arrêts | Une vue ou une projection qui diffère de celle d'avant de plus de 1e-4 sur un terme (tolérance de T3.2.4) ; un usage d'une ancienne classe qui ne se convertit pas sans changer un comportement ; un test supprimé sans équivalent listé | Une vue qui diffère ; un utilisateur de `Target` dont le sens ne se déduit pas du code |
| Risque | **Élevé : format de données et rupture d'API publique.** Relecture indépendante du plan avant approbation ; vérification indépendante du résultat avant de dire la tranche faite | **Élevé : format de données (14 mondes), rupture d'API publique.** Mêmes deux contrôles |
| Budget | Un commit par tâche ; au plus cinq passes de correction après vérification | Idem |

### Décisions des tranches 3 et 4 (réponses de l'auteur du 2026-11-19)

| Réf | Décision |
|---|---|
| D14 | **Les données sont migrées une fois** (O6). Les fichiers du dépôt qui nomment une ancienne caméra sont réécrits ; le chargeur ne reçoit aucun chemin de traduction. C'est la posture de l'ADR-0008 (« replace, don't duplicate ») ; la proposition d'origine de O6 (le chargeur traduit) est abandonnée. Un fichier hors dépôt non migré se charge en composant non résolu, avec une erreur au journal (ADR-0119, `ElementFactory.LoadComponent`). |
| D15 | **`Camera3dComponent` est fondue dans `PerspectiveCameraComponent`.** Le nom des données, `PerspectiveCameraComponent`, ne change pas (O2). |
| D16 | **`Camera2dComponent` est alignée sur la caméra 3D** (O7) : sa pose est la transformation de son composant. Ses clés changent ; les 14 mondes du dépôt sont migrés une fois (D14). |
| D17 | Mode AUTO pour l'exécution, après approbation du plan. Ni push, ni fusion dans `main` sans demande. |

### État vérifié (relevé le 2026-11-19 sur `main` `851bfa93`)

Relevé par `rg` sur les `.cs` hors `bin`, `obj`, `MGUI`, `NvgSharp`, puis lecture des fichiers cités.

- **Données qui nomment une ancienne caméra : deux fichiers**, identiques octet pour octet dans leur nœud de caméra : `CasaEngine.Demos/Content/EditorWorld/Entities/camera.entity` et `Projects/SampleProject/Entities/camera.entity`. Composant racine `ArcBallCameraComponent`, `fieldOfView` 0,7853982, `target` (−8,128628 ; −1,9367545 ; −3,401419), `distance` 5, `yaw` 2,8729265, `pitch` −0,53685135, `components` vide, pas de script. Chacun est référencé par un monde (`EditorWorld.world`, `DefaultWorld.world`, `asset_id` `8abc0021-…`) et par `AssetInfos.json`. Aucune donnée ne nomme `CameraLookAtComponent` ni `CameraTargeted2dComponent`.
- **`ArcBallCameraComponent` hors tests** : sa classe ; `ViewportCameraNavigation.cs` (`From` et `ApplyTo`, lignes 60-74, et deux commentaires) ; `ScriptArcBallCamera.cs` (chemin sans orbite) ; `EditorEntityJsonSerializer.cs` (cas ligne 226, `SaveArcBallCameraComponent` ligne 403) ; `EditorWorldDemo.cs:78-83` (teste le type, appelle `SetCamera`) ; un commentaire dans `OrbitCameraComponent.cs`.
- **`CameraLookAtComponent` et `CameraTargeted2dComponent` hors tests** : leurs classes et `EntityPolicyResolver.cs:132-133`. Un composant non listé y reçoit déjà `DynamicDefault` (O10).
- **Tests** : 27 fichiers de `CasaEngine.Tests` nomment une ancienne classe. Trois s'en servent comme **référence** : `OrbitCameraComponentTests` (sept tests comparent l'orbite à `ArcBallCameraComponent` : vue, `Direction`, `Right`, position, valeurs déduites par `SetCamera`), `ScriptArcBallCameraOrbitTests` (compare le script sur l'orbite au script sur l'ancienne caméra : état, champ de vision, vue ; un test vérifie que le script laisse une ancienne caméra tranquille quand l'entité a une orbite), `ViewportCameraNavigationTests` (`Eye_MatchesTheArcBallComponentPosition`, `ApplyTo_LeavesTheFieldOfViewUntouched`). `Camera3dFieldOfViewTests` prend l'ancienne caméra comme support (`CreateCamera`, lignes 19-21) et charge les deux `camera.entity` par `new ArcBallCameraComponent().Load`. `ArcBallCameraComponentTests` (cinq tests) teste la classe elle-même. `PerspectiveCameraComponentTests:194-198` cite les trois classes dans une théorie. `EditorViewportCameraPoseTests` ne les cite qu'en commentaire : ses références sont déjà des valeurs écrites en dur. Les autres fichiers prennent une ancienne caméra comme simple support.
- **`Camera3dComponent`** : abstraite, 60 lignes (`FieldOfView`, `DefaultFieldOfView`, `InitializeWithWorld`, la projection, la lecture de `fieldOfView`). Nommée dans 13 `.cs` : les quatre classes dérivées, `ScriptArcBallCamera.cs`, `EditorEntityJsonSerializer.cs` (cas et `SaveCamera3dComponent`), `CameraComponentEditor.cs`, `EditorCameraFactory.cs`, `EditorViewportCameraController.cs`, trois fichiers de test. Aucune donnée ne la nomme (un type abstrait n'est pas chargeable).
- **`Camera2dComponent`** (`Camera2dComponent.cs`) : `Target` (Vector3), `Zoom`, `PixelSnap`. Sa vue est `CreateLookAt(target + (0, 0, 500), target, Up)`, la cible étant arrondie à la grille `1 / Zoom` en x et y quand `PixelSnap` est vrai ; le calcul écrit `Position` au passage. `SetPositionAndTarget` n'écrit que la cible. `Load` lit `target`, `zoom`, `pixel_snap`. **La transformation enregistrée n'a aujourd'hui aucun effet sur la vue.**
- **Les 14 mondes à caméra 2D** (`Projects/` : `RPGDemo`, `BufferedResolutionSample`, `PixelUiSample` ×2, `TimedScreenSample` ×2, `VideoSample`, `VideoSpriteSample` ×2, `VideoSpriteExitSample` ×2, `SpriteDrawOrderSample`, `SpriteCutsceneSample` ×2) : dans tous, la caméra est le composant racine d'une entité sans parent, `target` vaut (0, 0, 0), `zoom` 1, `pixel_snap` faux, rotation identité, profondeurs 1 et 1000. La position enregistrée vaut (0, 0, 10) dans treize mondes et (3,0748892 ; 0,5550308 ; 2,5794868) dans `RPGDemo/DefaultWorld.world` : sans effet aujourd'hui.
- **Utilisateurs de `Camera2dComponent.Target` hors tests** : `ScreenEffectComponent.cs:72` et `:147` (lecture : x et y centrent le quad de l'effet, z donne sa profondeur) ; `EditorViewport2dCameraController.cs:87-102` (écriture) ; `Layers2dDemo.cs:208`, `TileMapDemo.cs:139`, `Projects/CasaEngine.RPGDemo/Scripts/ScriptWorld.cs:61` (écriture) ; `EditorEntityJsonSerializer.cs:386-394` (écrit `target`, `zoom`, `pixel_snap`). `VirtualResolutionRuntime`, `VirtualResolutionLayout`, `PixelPerfectDiagnostics` et `DebugOverlay` ne lisent que `Zoom`, `PixelSnap` ou le type.
- `Follow2dCameraComponent` écrit `LocalPosition` de la caméra de son entité ; sa doc dit qu'il ne pilote pas une `Camera2dComponent`, qui se place par sa cible.
- Dernière ADR : 0145. `CasaEngine.Tests` : 8118 tests verts sur `851bfa93`.

---

## Tranche 3 — Retrait, migration, fusion

### Conception

- **Références figées avant le retrait.** Tout test qui prouve aujourd'hui une égalité avec une ancienne classe reçoit d'abord une seconde référence, indépendante de cette classe, et passe au vert **avec les deux** (T3.2). L'ancienne référence n'est retirée qu'avec la classe (T3.5). Aucune preuve de non-régression ne disparaît donc sans avoir été doublée.
  - Orbite : la formule de l'ancienne classe, écrite dans le test : `CreateLookAt(Target − Direction × Distance, Target, Up)`, avec `Direction` = (sin Yaw cos Pitch, sin Pitch, −cos Yaw cos Pitch).
  - Script : `ViewportCameraNavigation.Update` appliqué directement à un `ViewportCameraState` de mêmes valeurs de départ et de mêmes entrées.
  - Données : la vue et la projection de chaque `camera.entity`, écrites en dur dans le test (16 termes chacune, viewport 1280 × 720, profondeurs 1 et 1000), relevées sur l'ancienne classe.
- **Forme migrée d'un `camera.entity`** : composant racine `PerspectiveCameraComponent` (mêmes `id`, `name`, `view_distance`, `viewport`, `fieldOfView`), sans `target`, `distance`, `yaw`, `pitch` ; un `OrbitCameraComponent` dans `components`, avec ces quatre clés et leurs valeurs. Le fichier est **édité à la main, dans sa forme d'aujourd'hui** (clé `coordinates`, pas de clés de politique) : le passer par `EditorEntityJsonSerializer` renommerait `coordinates` en `local_transform` et ajouterait cinq clés de politique et `picture_offset`, des changements sans rapport avec la migration. Le diff attendu, identique dans les deux fichiers, est exactement :
  - `"type": "ArcBallCameraComponent"` devient `"type": "PerspectiveCameraComponent"` ;
  - les clés `target`, `distance`, `yaw`, `pitch` sortent du composant racine, avec leurs valeurs inchangées ;
  - `coordinates.position` devient la position que l'orbite écrit pour ces valeurs, relevée en T3.2 (`coordinates.rotation` égale déjà l'orientation de l'orbite à 1e-6 près, elle ne change pas) ;
  - `"components": []` devient un tableau d'un nœud : `id` (un GUID neuf, le même dans les deux fichiers), `"name": "OrbitCameraComponent"`, `"type": "OrbitCameraComponent"`, puis `target`, `distance`, `yaw`, `pitch`. Ce sont les clés qu'écrit le sérialiseur pour ce composant (`OrbitCameraComponentTests.SavedOrbitEntity`).
  - Rien d'autre : l'`id` de l'entité, celui du composant racine et le nom du fichier ne changent pas, donc ni les mondes ni `AssetInfos.json`.
  Que l'éditeur relise et réécrive cette forme sans perte est prouvé par un test (T3.3).
- **Fusion.** Les membres de `Camera3dComponent` passent dans `PerspectiveCameraComponent`, sans changement de comportement ; `Camera3dComponent.cs` est supprimé ; chaque référence à `Camera3dComponent` devient `PerspectiveCameraComponent`.
- **Ruptures d'API publique** (demandées par l'auteur, dites dans l'ADR et le rapport) : les quatre classes ; `ViewportCameraState.From(ArcBallCameraComponent)` et `ApplyTo(ArcBallCameraComponent)`.

### Validation globale de la tranche

- `rg "\b(ArcBallCameraComponent|CameraLookAtComponent|CameraTargeted2dComponent|Camera3dComponent)\b" --glob "*.cs"` hors `bin`, `obj`, `MGUI`, `NvgSharp` : aucun résultat.
- `rg "ArcBallCameraComponent|CameraLookAtComponent|CameraTargeted2dComponent"` sur les données (`*.entity`, `*.world`, `*.json` de `Projects/` et de `CasaEngine.Demos/Content/`) : aucun résultat.
- Les tests de données de T3.2 verts, sans changement de leurs valeurs attendues entre T3.2 et la fin.
- Les deux solutions à 0 erreur ; `CasaEngine.Tests` vert ; le nombre de tests ne baisse que des suppressions listées dans les notes de T3.4 et T3.5.
- Recette de l'auteur : la démo « World made in the editor » de `CasaEngine.Demos`, et `Projects/SampleProject`.

### ✅ T3.0 — Recenser les usages

Fait à l'écriture de ces tâches : voir « État vérifié » ci-dessus.

### ✅ T3.1 — ADR

- ADR-0149, en anglais, avec le skill `adr`, indexé dans `docs/decisions/README.md` : retrait des trois anciennes caméras ; données migrées une fois, sans traduction dans le chargeur, en application de l'ADR-0008 (D14) ; fusion de `Camera3dComponent` dans `PerspectiveCameraComponent` (D15) ; ruptures d'API listées ; ce qui arrive à un fichier non migré (ADR-0119). Il cite l'ADR-0089 pour dire que `ViewportCameraState` ne se lit plus que dans une orbite.
- Validation : relecture ; aucun nom de projet extérieur.
- **Fait (2026-11-19).** Plan approuvé par l'auteur le 2026-11-19 (« Approuvé, lance »), tranches 3 et 4, en mode AUTO. ADR-0149 écrit et indexé. Documentation seule : ni build ni test.

### ✅ T3.2 — Figer les références

Tests seulement, aucun code du moteur.

1. Chaque affirmation qui compare à `ArcBallCameraComponent` reçoit la seconde référence de cette table, dans le même test, et passe au vert avec les deux. Formules, toutes recopiées de l'ancienne classe (`ArcBallCameraComponent.cs:153-158`, `:245-277`) et écrites dans le test : `Direction` = (sin Yaw cos Pitch, sin Pitch, −cos Yaw cos Pitch) ; `Right` = `Vector3.Transform(Vector3.Right, AxisAngle(Up, −Yaw) × AxisAngle(Right, Pitch))` ; position = `Target − Direction × Distance` ; vue = `CreateLookAt(position, Target, Up)`.

   | Fichier, test | Affirmation sur l'ancienne classe | Seconde référence |
   |---|---|---|
   | `OrbitCameraComponentTests.OverAGridOfValues_TheView_EqualsTheArcBallView` | vue, `Direction`, `Right`, position | les quatre formules |
   | `…WithANegativeDistance_TheCameraStillLooksAtTheTarget_LikeTheArcBall` | vue, `Direction`, `Right`, position | les quatre formules (la vue par `CreateLookAt` regarde la cible quel que soit le signe de la distance) |
   | `…TheValuesADemoStartsWith_ANegativeDistanceIncluded_GiveTheArcBallView` | vue | formule de la vue |
   | `…ThePositiveDistanceOfTheWorldSpaceUiDemo_GivesTheViewOfItsFormerNegativeDistance` | vue des anciennes valeurs | formule de la vue pour les anciennes valeurs |
   | `…SetCamera_GivesTheArcBallValues_AndALookAtView` | `Target`, `Distance`, `Yaw`, `Pitch`, vue | cible = la cible donnée ; distance = longueur de position − cible ; avec z = (position − cible) / distance : `Yaw = Atan2(−z.X, z.Z)`, `Pitch = −Asin(z.Y)` ; vue = `CreateLookAt(position, cible, Up)` (déjà affirmée) |
   | `…SetCamera_WithThePositionOnTheTarget_KeepsTheYawAndThePitch` | `Yaw`, `Pitch`, `Distance` | les valeurs écrites avant l'appel (1,1 ; −0,4) et la distance relevée sur l'ancienne classe, écrite en dur |
   | `…AnEntityLoadedFromData_IsPosedAtItsInitialisation_WithNoPropertyWrite` | vue | formule de la vue |
   | `ScriptArcBallCameraOrbitTests.TheScript_GivesAnOrbitedCameraThePoseItGivesAnArcBall` | état (cible, distance, lacet, tangage), champ de vision, vue, pour chaque geste | état : `ViewportCameraNavigation.Update` sur un `ViewportCameraState` de mêmes valeurs de départ et de mêmes entrées, plus la règle du script pour F (pivot sur `FocusPoint`) ; champ de vision : valeur de départ plus la somme des `FieldOfViewDelta` rendus ; vue : formule de la vue appliquée à cet état |
   | `ScriptArcBallCameraOrbitTests.TheScript_LeavesAnArcBallAloneWhenItsEntityHasAnOrbit` | le script ne touche pas une ancienne caméra | aucune : le test n'a plus d'objet sans l'ancienne classe ; **supprimé en T3.5**, seule suppression de ces deux fichiers |
   | `ViewportCameraNavigationTests.Eye_MatchesTheArcBallComponentPosition` | `Eye` et `Forward` de l'état = position et direction de la caméra | un jumeau sur `OrbitCameraComponent` + `PerspectiveCameraComponent` : `Eye` = `LocalPosition` de la caméra, `Forward` = `Direction` de l'orbite |
   | `ViewportCameraNavigationTests.ApplyTo_LeavesTheFieldOfViewUntouched` | `ApplyTo` ne touche pas le champ de vision | un jumeau sur l'orbite et sa caméra |

2. La note de la tâche reprend cette table et dit, ligne par ligne, que les deux références sont vertes.
3. `ArcBallCameraComponentTests` n'est pas touché ici : il est traité en T3.4.
4. Nouveau `LegacyCameraEntityDataTests` (nom libre) : une théorie à deux lignes, une par `camera.entity`. Elle charge l'entité **par `Entity.Load`** puis l'initialise comme le fait le test existant `AnEntityLoadedFromData_IsPosedAtItsInitialisation_WithNoPropertyWrite`, prend le composant caméra de l'entité, lui donne un viewport de 1280 × 720 et les profondeurs 1 et 1000, et compare sa vue et sa projection aux 32 termes écrits en dur, **à 1e-4 près par terme** (la tolérance de `OrbitCameraComponentTests`) : avant la migration la vue vient de `CreateLookAt`, après de l'inverse de la matrice du monde, et leurs derniers bits diffèrent. Les termes sont relevés par ce même test sur les fichiers d'origine et écrits avec 9 chiffres significatifs. Le test ne nomme aucune classe de caméra concrète : il reste inchangé après la migration.
5. Validation : tests verts ; la note de la tâche donne les 32 termes relevés et le compte de tests.
- **Fait (2026-11-19).** Chaque ligne de la table a ses deux références vertes : les six tests de `OrbitCameraComponentTests` (formules `FormulaDirection`, `FormulaRight`, `FormulaPosition`, `FormulaView` ; valeurs de `SetCamera` recalculées ; position sur la cible : lacet 1,1, tangage −0,4, distance 0) et `AnEntityLoadedFromData…` ; le test du script (navigation sur l'état nu, champ de vision de départ plus la somme des deltas, vue par `CreateLookAt(Eye, Pivot, Up)`) ; deux jumeaux dans `ViewportCameraNavigationTests` (`Eye_MatchesThePositionOfACameraPosedByAnOrbit`, `ApplyTo_AnOrbit_LeavesTheFieldOfViewOfItsCameraUntouched`). Nouveau `LegacyCameraEntityDataTests`, deux lignes. Termes relevés sur les deux fichiers d'origine, identiques pour les deux :
  - vue : −0,964125872 ; 0,135757655 ; −0,228103638 ; 0 / 2,23517418E-08 ; 0,859323382 ; 0,511432827 ; 0 / 0,265445679 ; 0,493085623 ; −0,8284958 ; 0 / −6,93412876 ; 4,44501209 ; −8,6817112 ; 1
  - projection : 1,35799503 ; 0 ; 0 ; 0 / 0 ; 2,41421342 ; 0 ; 0 / 0 ; 0 ; −1,001001 ; −1 / 0 ; 0 ; −1,001001 ; 0
  - Les deux solutions à 0 erreur ; `CasaEngine.Tests` : 8122 tests, 0 échec (8118 + 2 lignes de données + 2 jumeaux).

### 🧪 T3.3 — Migrer les deux `camera.entity` et leurs utilisateurs

1. Éditer les deux fichiers selon la « forme migrée » de la conception. `git diff` montre exactement les quatre changements listés, les mêmes dans les deux fichiers, et rien d'autre.
2. `EditorWorldDemo.InitializeCamera` : cadrer par `DemoCameraFraming.Frame(camera, (0, 14, 30), Vector3.Zero)`, mêmes arguments qu'aujourd'hui, et garder la pose du script.
3. `Camera3dFieldOfViewTests` : `TheSavedCameras_LoadWithFortyFiveDegrees` charge le nœud par `ElementFactory.LoadComponent`, mêmes affirmations ; son support `CreateCamera` passe sur `new PerspectiveCameraComponent()`. Ce fichier appartient à cette tâche : après elle, il ne nomme plus `ArcBallCameraComponent`.
4. Nouveau test : chaque `camera.entity` migré, chargé par `Entity.Load`, écrit par `EditorEntityJsonSerializer.SaveEntity` puis rechargé, donne la même vue et la même projection (les 32 termes de T3.2.4, à la même tolérance de 1e-4 par terme).
5. Validation : les tests de T3.2.4 verts **sans modification** ; builds ; tests. Si `Projects/SampleProject` se lance par `tools/run-launcher.ps1` du dépôt hôte, une capture avant (sur `main`) et une après sont jointes au rapport comme indice, pas comme preuve ; sinon la note le dit. 🧪 jusqu'à la recette de la démo par l'auteur.
- **Fait (2026-11-19), 🧪 jusqu'à la recette.** Les deux fichiers édités, restés identiques entre eux ; `git diff` montre les quatre changements listés et rien d'autre (position de l'orbite : −9,269146 ; 0,6204096 ; −7,543898 ; `id` du nœud d'orbite `8efcd556-e91d-4c06-bbb9-abf1d1f3804f`). `EditorWorldDemo` cadre par `DemoCameraFraming.Frame`, mêmes arguments. `Camera3dFieldOfViewTests` ne nomme plus l'ancienne caméra. Test d'aller-retour par l'éditeur ajouté (deux lignes). Le test de T3.2.4 est vert **sans modification**. Les deux solutions à 0 erreur ; 8124 tests, 0 échec. Indice : `Projects/SampleProject` lancé par le script de capture avant et après, 10 s : les deux images sont les mêmes à l'œil, mais pas octet pour octet (11 octets d'écart sur 42 Ko, cause non recherchée). La démo « World made in the editor » n'a pas été lancée : recette de l'auteur.

### ✅ T3.4 — Convertir les tests qui prennent une ancienne caméra comme support

Travail répétitif à jugement borné : confié à un `executor`, relu par la session principale.

- Règle : `new ArcBallCameraComponent()` ou `new CameraLookAtComponent()` devient `new PerspectiveCameraComponent()`. Une pose donnée par `Target`, `Distance`, `Yaw`, `Pitch`, `SetCamera` ou `Position` + `Target` devient `SetPositionAndTarget(position, cible)` avec la même position et la même cible ; si le test a besoin des valeurs d'orbite elles-mêmes, il pose un `OrbitCameraComponent` sur l'entité. Aucune affirmation n'est affaiblie ni retirée.
- `ArcBallCameraComponentTests` : ses cinq tests sont réécrits sur `OrbitCameraComponent` + `PerspectiveCameraComponent` dans un fichier neuf, `CasaEngine.Tests/Scene/OrbitCameraPlacementTests.cs`, qui ne nomme aucune ancienne classe : mêmes cas, mêmes valeurs, mêmes affirmations (`SetCamera` place la caméra à la position et regarde la cible ; égalité avec `CreateLookAt` ; distance positive ; un changement de `Distance` après `SetCamera` garde la caméra du même côté ; `DemoCameraFraming.Frame` ou `SetCamera` à la place de `SetPositionAndTarget` de l'ancienne classe, qui nourrissait l'orbite). `ArcBallCameraComponentTests.cs` est ensuite supprimé. La note de la tâche donne, pour chacun des cinq tests, le test qui lui correspond dans le fichier neuf.
- `PerspectiveCameraComponentTests:194-198` : les trois lignes des anciennes classes sortent de la théorie.
- Ne sont pas touchés ici : `OrbitCameraComponentTests`, `ScriptArcBallCameraOrbitTests`, `ViewportCameraNavigationTests` (T3.2 puis T3.5), `Camera3dFieldOfViewTests` (T3.3), `EditorViewportCameraPoseTests` (commentaire seul, corrigé en T3.5). Chaque autre fichier de test qui nomme une ancienne classe appartient à cette tâche.
- Arrêt : un test dont le sens dépend d'un comportement propre à l'ancienne classe et que la caméra simple n'a pas.
- Validation : builds ; tests verts ; la note liste les tests supprimés et leur équivalent ; `rg` ne trouve plus les trois noms dans `CasaEngine.Tests` hors de `OrbitCameraComponentTests`, `ScriptArcBallCameraOrbitTests`, `ViewportCameraNavigationTests` et `EditorViewportCameraPoseTests`.
- **Fait (2026-11-19).** Converti par un `executor`, relu par la session principale. 21 fichiers de test passent sur `PerspectiveCameraComponent`. Aucun cas d'arrêt. Dans `SsaoGpuTests`, `SceneDepthPrepassGpuTests` et `FxaaGpuTests`, la caméra reçoit `SetPositionAndTarget((0, 0, −5), (0, 0, 0))` : c'était la pose par défaut de l'ancienne caméra (cible nulle, distance 5, lacet π), que la caméra simple n'a pas. `ArcBallCameraComponentTests` supprimé ; ses cinq tests (13 cas) sont dans le fichier neuf `OrbitCameraPlacementTests`, sous le même nom pour quatre d'entre eux : `SetCamera_PlacesTheCameraAtThePositionLookingAtTheTarget`, `SetCamera_MatchesCreateLookAt`, `SetCamera_StoresAPositiveDistanceLikeTheEditorAndTheDistanceSetter`, `SetCamera_ThenZoomingWithTheDistanceSetter_KeepsTheCameraOnTheSameSide` ; `SetPositionAndTarget_BehavesLikeSetCamera` devient `SetCamera_GivesTheCameraTheViewOfSetPositionAndTarget`. Seules suppressions : les trois lignes des anciennes classes dans la théorie de `PerspectiveCameraComponentTests`. `rg` ne trouve plus les trois noms que dans les quatre fichiers réservés. Les deux solutions à 0 erreur ; 8121 tests, 0 échec (8124 − 3).

### ✅ T3.5 — Retirer les trois anciennes classes

1. Supprimer `ArcBallCameraComponent.cs`, `CameraLookAtComponent.cs`, `CameraTargeted2dComponent.cs`.
2. `ViewportCameraNavigation.cs` : retirer `From(ArcBallCameraComponent)` et `ApplyTo(ArcBallCameraComponent)` ; corriger les deux commentaires.
3. `ScriptArcBallCamera.cs` : retirer le chemin sans orbite ; sans orbite sur l'entité, le script ne fait rien, comme aujourd'hui sans caméra. Sa doc le dit.
4. `EditorEntityJsonSerializer.cs` : retirer le cas et `SaveArcBallCameraComponent`. `EntityPolicyResolver.cs` : retirer les deux cas (aucun effet : O10).
5. Tests de T3.2 : retirer la référence à l'ancienne classe, garder la seconde, selon la table de T3.2 ; supprimer `TheScript_LeavesAnArcBallAloneWhenItsEntityHasAnOrbit` et les deux tests d'origine de `ViewportCameraNavigationTests`, dont les jumeaux restent. Aucune autre affirmation ne disparaît. Corriger le commentaire de `EditorViewportCameraPoseTests`. Corriger les commentaires qui décrivent l'ancienne caméra (`OrbitCameraComponent.cs`, `WorldViewportPanel.cs:1946`, `TileMap3dDemo.cs:13`).
6. Validation : le premier `rg` de la validation globale, restreint aux trois noms, ne trouve rien ; builds ; tests ; la note liste les tests retirés.
- **Fait (2026-11-19).** Les trois fichiers de classe supprimés ; `ViewportCameraState` ne se lit plus que dans une orbite ; `ScriptArcBallCamera` ne fait rien sans orbite ; le cas de l'écriture par l'éditeur et les deux cas de `EntityPolicyResolver` retirés ; commentaires corrigés. Tests : les références à l'ancienne classe retirées selon la table de T3.2, les secondes références gardées ; `TheScript_GivesAnOrbitedCameraThePoseItGivesAnArcBall` renommé `TheScript_GivesAnOrbitedCameraThePoseOfTheNavigation`. Trois tests retirés, comme prévu : `TheScript_LeavesAnArcBallAloneWhenItsEntityHasAnOrbit`, `Eye_MatchesTheArcBallComponentPosition`, `ApplyTo_LeavesTheFieldOfViewUntouched` (les deux derniers ont leurs jumeaux). `rg` sur les trois noms dans les `.cs` : aucun résultat. Les deux solutions à 0 erreur ; 8118 tests, 0 échec (8121 − 3).

### ✅ T3.6 — Fondre `Camera3dComponent` dans `PerspectiveCameraComponent`

1. Déplacer `DefaultFieldOfView`, `FieldOfView`, le constructeur de copie, `InitializeWithWorld`, la projection par défaut et la lecture de `fieldOfView` dans `PerspectiveCameraComponent`, qui dérive alors de `CameraComponent`. Supprimer `Camera3dComponent.cs`.
2. Remplacer `Camera3dComponent` par `PerspectiveCameraComponent` dans `ScriptArcBallCamera.cs`, `EditorEntityJsonSerializer.cs` (un seul cas, une seule méthode d'écriture, mêmes clés dans le même ordre), `CameraComponentEditor.cs`, `EditorCameraFactory.cs`, `EditorViewportCameraController.cs` et les tests.
3. Validation : `rg "\bCamera3dComponent\b" --glob "*.cs"` ne trouve rien ; `Camera3dFieldOfViewTests` et `PerspectiveCameraComponentTests` verts sans changement d'affirmation ; un test relit un `PerspectiveCameraComponent` écrit par l'éditeur et retrouve les mêmes clés qu'avant la fusion ; builds ; tests.
- **Fait (2026-11-19).** `PerspectiveCameraComponent` dérive de `CameraComponent` et porte `DefaultFieldOfView`, `FieldOfView`, `InitializeWithWorld`, la projection par défaut et la lecture de `fieldOfView`, recopiés sans changement. `Camera3dComponent.cs` supprimé. Six fichiers passent sur le nouveau type ; `SaveCamera3dComponent` devient `SavePerspectiveCameraComponent`, corps inchangé (un seul cas dans l'aiguillage). `rg "\bCamera3dComponent\b" --glob "*.cs"` : aucun résultat. Nouveau test `TheEditorWriter_WritesTheKeysOfTheCamera_InTheSameOrderAsBeforeTheFusion` : `id`, `name`, `type`, `local_transform`, `children_component`, `view_distance`, `viewport`, `fieldOfView`, `picture_offset`. Cette liste a été relevée après la fusion ; qu'elle soit celle d'avant se déduit du diff (le corps de la méthode d'écriture n'a pas changé), le test ne l'a pas mesuré sur l'ancien code. `Camera3dFieldOfViewTests` et `PerspectiveCameraComponentTests` verts sans changement d'affirmation. Les deux solutions à 0 erreur ; 8119 tests, 0 échec.

### ⏳ T3.7 — Documentation

- `docs/engine/perspective-camera-component.md`, `docs/engine/camera-behaviour-components.md`, `docs/editor/viewport-camera-controls.md`, `docs/editor/editor-2d-viewport.md`, `docs/README.md` : plus de mention des classes retirées comme existantes ; comment migrer un fichier à la main (les deux formes côte à côte).
- `ai-agent/README.md` : la ligne du plan dit l'état réel (elle dit encore que la tranche 2 attend sa fusion).
- Validation : relecture ; liens vérifiés.

### ⏳ T3.8 — Vérification indépendante et recette

- Un `verifier` neuf reçoit les affirmations de la « Validation globale », le diff de la branche et les notes des tâches ; il relance builds et tests et cherche à réfuter. Un point P0 à P2 est corrigé puis revérifié.
- Recette de l'auteur (🧪 jusque-là) : « World made in the editor » dans `CasaEngine.Demos`, `Projects/SampleProject`, et un enregistrement d'entité à caméra dans l'éditeur.

---

## Tranche 4 — Caméra 2D posée par sa transformation

### Conception

- **Pose.** La vue de `Camera2dComponent` est l'inverse de `WorldMatrixNoScale`, parents compris, comme `PerspectiveCameraComponent` (même crochet `IsViewOutOfDate`, qui compare la matrice du monde à celle du dernier calcul). Elle regarde le long de son −Z local ; un roulis fait tourner l'image. `Target` et son champ disparaissent. Le calcul de la vue n'écrit plus `Position`.
- **`PixelSnap`.** Quand il est vrai, la translation x et y de la matrice du monde est arrondie à la grille `1 / Zoom` pour le calcul de la vue ; la transformation enregistrée n'est pas modifiée. Pour une caméra sans rotation, c'est le calcul d'aujourd'hui.
- **Distance par défaut.** `public const float DefaultDistance = 500f` (la constante privée d'aujourd'hui). Le constructeur sans paramètre pose `LocalPosition` à (0, 0, 500) : une caméra créée par code voit le plan z = 0 comme aujourd'hui avec une cible nulle.
- **Cadrage par code.** `CentreOn(Vector3 point)` pose `LocalPosition` à (point.X, point.Y, point.Z + `DefaultDistance`) : c'est ce que faisait l'écriture de `Target`. `SetPositionAndTarget(position, cible)` appelle `CentreOn(cible)`, comme aujourd'hui où seule la cible compte.
- **Données.** `Load` ne lit plus `target`. Si la clé est présente, une erreur est écrite une fois au journal, avec le nom et l'identifiant du composant : la clé n'est plus lue, la pose vient de `coordinates` (O11). L'éditeur n'écrit plus `target`.
- **Forme migrée d'un monde** : `coordinates.position` = (cible.x, cible.y, cible.z + 500), soit (0, 0, 500) dans les 14 mondes ; la clé `target` est retirée. Édition ciblée du nœud de la caméra : le reste de chaque fichier reste identique octet pour octet.
- **Ordre qui garde chaque commit vert** : d'abord la position dans les données (sans effet sur l'ancien code, qui ne la lit pas), puis le code, puis le retrait de la clé `target`.
- **Effets d'écran.** `ScreenEffectComponent` centre son quad sur la position du monde de la caméra, à la profondeur z − `DefaultDistance` : les valeurs d'aujourd'hui pour une caméra sans rotation.
- **Suivi 2D.** `Follow2dCameraComponent` écrit `LocalPosition` : il pilote désormais aussi une `Camera2dComponent`. Sa doc et sa limite sont corrigées, un test le prouve.
- **Rupture d'API publique** (demandée par l'auteur) : `Camera2dComponent.Target`.

### Validation globale de la tranche

- Le test par monde de T4.1 vert du début à la fin, **sans modification**.
- `rg` ne trouve plus de clé `target` sous un nœud `Camera2dComponent` dans les données du dépôt (test de T4.4).
- Pour une grille de positions, de zooms et de `PixelSnap`, la vue d'une caméra 2D posée en (x, y, z + 500) égale `CreateLookAt` de l'ancienne formule pour la cible (x, y, z), arrondi compris.
- Les deux solutions à 0 erreur ; `CasaEngine.Tests` vert ; aucune affirmation retirée hors de celles qui portaient sur `Target`, listées dans la note de T4.3.
- Recette de l'auteur : la vue 2D de l'éditeur, les démos 2D, `RPGDemo`, un sample à sprites.

### ⏳ T4.0 — ADR

- ADR-0150, en anglais, skill `adr`, indexé : la caméra 2D est posée par sa transformation ; la distance par défaut ; `PixelSnap` ; le sort de la clé `target` ; les 14 mondes migrés une fois (ADR-0008) ; la rupture d'API ; comment migrer un fichier à la main.
- Validation : relecture.

### ⏳ T4.1 — Figer la vue des 14 mondes

Tests seulement.

- Nouveau test, une théorie à 14 lignes, une par monde. Elle lit le fichier, prend le nœud dont `type` vaut `Camera2dComponent`, le charge par `ElementFactory.LoadComponent`, lui donne un viewport de 1280 × 720 et les profondeurs 1 et 1000, et compare sa vue et sa projection à des termes écrits en dur. Tous les mondes ayant une cible nulle, un zoom de 1 et pas d'arrondi, la vue attendue est `CreateLookAt((0, 0, 500), (0, 0, 0), Up)` et la projection `CreateOrthographic(1280, 720, 1, 1000)` : la note le vérifie sur les 14 relevés avant de les réduire à ces deux matrices.
- Nouveau test de grille (validation globale, troisième point), d'abord écrit sur l'ancienne classe par `Target`, avec la vue attendue calculée par la formule dans le test ; il passera sur `CentreOn` en T4.3 sans changer la formule.
- Validation : tests verts sur le code d'aujourd'hui ; la note donne les relevés.

### ⏳ T4.2 — Écrire la position dans les 14 mondes

- Dans chaque monde, `coordinates.position` du nœud de la caméra devient (0, 0, 500). La clé `target` reste.
- Validation : `git diff --stat` montre trois lignes au plus par fichier ; le test de T4.1 vert ; builds ; tests.

### ⏳ T4.3 — La caméra 2D et tous ses utilisateurs

Un seul commit : le dépôt ne compile pas entre les deux.

1. `Camera2dComponent` selon la conception. `EditorEntityJsonSerializer.SaveCamera2dComponent` n'écrit plus `target`.
2. Utilisateurs : `ScreenEffectComponent.cs:72` et `:147` ; `EditorViewport2dCameraController` (`CreateCameraComponent`, `ApplyTo` : `CentreOn` de sa propre cible, qu'il garde) ; `Layers2dDemo.cs`, `TileMapDemo.cs`, `ScriptWorld.cs` (`CentreOn` du même point) ; `Follow2dCameraComponent` (doc).
3. Tests : `Camera2dComponentTests` réécrit sur la pose (projection, zoom, redimensionnement : inchangés ; `PixelSnap` : mêmes cas, la transformation n'est pas modifiée ; clone : zoom, arrondi et transformation ; `SetPositionAndTarget` et `CentreOn` ; roulis ; parent ; le point du plan cible projeté comme avant). `PerspectiveCameraComponentTests` : `Camera2dComponent` sort de la théorie des caméras qui ne redéfinissent pas le crochet. Les autres tests qui écrivent `Target` passent sur `CentreOn` du même point. Un test de chargement : la clé `target` présente donne une erreur au journal et une pose lue dans `coordinates`. Un test : `Follow2dCameraComponent` déplace la vue d'une `Camera2dComponent`.
4. Validation : le test de T4.1 et le test de grille verts ; builds ; tests ; la note liste les affirmations retirées parce qu'elles portaient sur `Target`.

### ⏳ T4.4 — Retirer la clé `target` des 14 mondes

- La ligne `target` (et son objet) sort du nœud de la caméra de chaque monde.
- Nouveau test : aucun fichier `*.world` ni `*.entity` de `Projects/` et de `CasaEngine.Demos/Content/` n'a de clé `target` sous un nœud `Camera2dComponent`.
- Validation : le test de T4.1 vert ; aucune erreur de clé `target` au chargement des 14 mondes (test) ; builds ; tests. Indice joint au rapport, pas preuve : une capture de `Projects/SpriteDrawOrderSample` avant (sur la base de la tranche) et après, par `tools/run-launcher.ps1` du dépôt hôte, si le projet se lance.

### ⏳ T4.5 — Documentation

- `docs/engine/rendering-2d-3d-spaces.md`, `docs/engine/camera-behaviour-components.md`, `docs/engine/data-driven-screens.md`, `docs/engine/screen-effects.md`, `docs/editor/editor-2d-viewport.md`, `docs/README.md`, là où ils décrivent la cible de la caméra 2D ; `ai-agent/README.md`.
- Validation : relecture ; liens vérifiés.

### ⏳ T4.6 — Vérification indépendante et recette

- Un `verifier` neuf, comme en T3.8, sur la « Validation globale » de la tranche 4.
- Recette de l'auteur (🧪 jusque-là) : la vue 2D de l'éditeur (déplacement, zoom, enregistrement d'un monde, rechargement), `TileMapDemo` et `Layers2dDemo`, `RPGDemo`, un sample à sprites.

---

## Points ouverts

| Réf | Sujet | Tâche concernée |
|---|---|---|
| O1 | **Tranché le 2026-11-19** : lancement après l'intégration d'`origin/main` dans `main`, faite en `9125b438` | Lancement |
| O2 | **Tranché le 2026-11-19.** Forme et nom de la caméra 3D simple : une nouvelle classe concrète dérivée de `Camera3dComponent`, nommée `PerspectiveCameraComponent`, la réponse de l'auteur du 2026-11-18. Ce nom reste le nom final : à la tranche 3, quand les trois anciennes classes sont retirées, `Camera3dComponent` n'a plus qu'elle pour classe dérivée et peut y être fondue sans changer les données. Voie écartée après deux relectures : rendre `Camera3dComponent` concrète dès la tranche 1, ce qui modifiait la vue, la projection et l'écriture de toutes les caméras 3D | T1.2 |
| O3 | **Tranché le 2026-11-19** : clé `picture_offset`, avec `x` et `y` | T1.3 |
| O8 | **Tranché le 2026-11-19** : sens du décalage, qui devient un sens de donnée enregistrée : fraction de la largeur et de la hauteur entières du viewport ; x positif déplace l'image vers la droite, y positif vers le bas ; clé absente : (0, 0) | T1.1, T1.3 |
| O4 | Le sample pilote le décalage au clavier (D6) sans action de cutscene (D5). Si un sample sans DLL ne peut pas lire le clavier pour agir sur un composant, il reçoit une DLL de jeu minimale | T1.4 |
| O5 | Les sprites restent dans un plan parallèle à XY : une image fixe ne fait face à la caméra que si la caméra regarde le long de z. Le sample s'en tient à ce cas ; un sprite orientable est hors de ce chantier | T1.4 |
| O6 | **Tranché le 2026-11-19 : migrer une fois, sans traduction dans le chargeur (D14, ADR-0008).** Question d'origine : tranche 3 : les données qui nomment une ancienne classe se lisent-elles encore (le chargeur les traduit en caméra simple plus composant de comportement), ou sont-elles migrées une fois pour toutes ? La règle du dépôt est la sérialisation additive. Proposition : le chargeur les traduit, et l'éditeur écrit la nouvelle forme | Tranche 3 |
| O9 | **Tranché le 2026-11-19, selon la proposition.** Spécification du suivi 2D (D12), à neuf puisque l'ancienne classe ne suit rien. Proposition : `Follow2dCameraComponent`, posé sur l'entité d'une caméra simple. Réglages lus dans les données : `target_entity_name` (nom d'une entité du monde), `dead_zone` (largeur et hauteur en unités du monde, centrée sur la caméra), `limits` (rectangle du monde x, y, largeur, hauteur ; largeur ou hauteur nulle : pas de limite). À chaque `Update` : sur chaque axe, si la cible dépasse le bord de la zone morte, la caméra avance du dépassement ; puis sa position x, y est ramenée dans les limites ; son z ne change pas. Il écrit la position locale du composant caméra. Cible absente ou introuvable : il ne fait rien et le signale une fois. Il ne pilote pas `Camera2dComponent`, qui se place par sa propre cible (O7) | T2.1, T2.4 |
| O10 | **Tranché le 2026-11-19 : aucun changement, voir T2.1.** Politique d'entité. `EntityPolicyResolver` donne `DynamicDefault` à `CameraLookAtComponent`, et rien de propre à `ArcBallCameraComponent` ni à `PerspectiveCameraComponent`. Les usages de la visée passant sur la caméra simple (D11), leur politique changerait. Proposition : lire en T2.6 ce que vaut la politique d'un composant non listé ; si elle diffère de `DynamicDefault`, ajouter `PerspectiveCameraComponent` au même cas que `CameraLookAtComponent`, et le dire dans l'ADR | T2.1, T2.6 |
| O7 | **Tranché le 2026-11-19 : l'aligner (D16, tranche 4).** Question d'origine : la caméra 2D. `Camera2dComponent` est déjà simple, mais se place d'après une cible et non d'après sa transformation. Faut-il l'aligner sur la caméra 3D en tranche 2, au prix d'un changement de ses clés ? Proposition : ne pas y toucher tant qu'aucun besoin ne le demande | Tranche 2 |
| O11 | Tranche 4 : une clé `target` restée sous une caméra 2D dans un fichier non migré. Proposition : erreur au journal, une fois par chargement, et pose lue dans `coordinates` ; pas d'exception, pour qu'un monde ancien s'ouvre encore dans l'éditeur | T4.0, T4.3 |
| O12 | Tranche 4 : un projet hors de ce dépôt qui écrit des mondes à caméra 2D doit écrire la nouvelle forme avant de passer sur ce moteur. Hors périmètre de ce plan : à traiter dans le projet concerné | — |

## Hors périmètre

- Une action de cutscene pour le décalage (D5).
- Un sprite orientable en 3D ou toujours face à la caméra.
- Une matrice de projection entièrement libre.
- Tout changement du rendu.
