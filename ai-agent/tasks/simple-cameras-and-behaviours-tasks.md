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
- Validation (2026-11-19) : ADR-0144, numéro suivant de la série (dernière : 0143), listée dans l'index.

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
- Fichiers : `docs/decisions/0145-<titre>.md`, `docs/decisions/README.md`.
- Commit : `docs(decisions): record the camera behaviour components`
- Validation (2026-11-19) : ADR-0145, listée dans l'index. O10 tranché par la lecture : un composant que `EntityPolicyResolver` ne liste pas laisse le constructeur intact, et celui-ci retombe sur `DynamicDefault` (`EntityPolicyResolver.cs:71-86`), la politique que demandait `CameraLookAtComponent`. `EntityPolicyResolver.cs` ne change donc pas.

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

### ⏳ T2.3 — Navigation de vue et script

- Fichiers : `ViewportCameraNavigation.cs` (deux méthodes ajoutées à `ViewportCameraState`) ; `ScriptArcBallCamera.cs` ; tests.
- Étapes :
  1. Tests d'abord : `From` puis `ApplyTo` sur un composant d'orbite rendent le même état ; le script, sur une entité à caméra simple et orbite, écrit le même état que sur une `ArcBallCameraComponent` pour les mêmes entrées ; sur une entité à `ArcBallCameraComponent` seule, il se comporte comme avant (`EngineOwnedScriptObjectsTests` et `EditorWorldPlaySnapshotTests` verts sans modification).
  2. Écrire les deux méthodes, puis le choix du chemin dans le script.
- Commit : `feat(camera): drive the orbit component from the viewport navigation`

### ⏳ T2.4 — Composant de suivi 2D

- Prérequis : O9 tranché.
- Fichiers : `Follow2dCameraComponent.cs` ; `EditorEntityJsonSerializer.cs` ou `[SerializedProperty]` selon les types de ses réglages ; tests.
- Étapes : tests d'abord, un par règle de O9 (cible dans la zone, sortie par chaque bord, limites, cible absente ou retirée du monde, écriture et relecture, aucune allocation dans `Update`) ; puis le composant.
- Commit : `feat(camera): add a 2d follow behaviour component`

### ⏳ T2.5 — Vue 3D de l'éditeur

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

### ⏳ T2.6 — Aperçus, vignettes et caméra par défaut

- Fichiers : les sept fichiers de l'éditeur listés plus haut ; `World.cs` (`CreateDefaultCamera`) ; `EntityPolicyResolver.cs` selon O10.
- Étapes :
  1. Pour chacun des huit appels à `SetPositionAndTarget`, lire la direction visée. `PerspectiveCameraComponent` refuse une visée à la verticale : **si un appel peut viser à la verticale, ⚠️ Blocked**, la question va aux points ouverts.
  2. Remplacer le type construit ; rien d'autre ne change dans ces fichiers.
- Délégation : éditions de même forme sur huit fichiers, à un `mech-executor`, après l'étape 1 faite en session principale.
- Validation : build ; tests ; essai des aperçus par l'auteur.
- Commit : `refactor(editor): pose the preview cameras by their transform`

### ⏳ T2.7 — Démos et bac à sable

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

### ⏳ T2.8 — Documentation

- Fichiers : `docs/engine/` (les deux composants), `docs/editor/viewport-camera-controls.md`, `docs/editor/editor-2d-viewport.md`, `docs/engine/demos-main-menu.md`, `docs/README.md`.
- Commit : `docs(engine): describe the camera behaviour components`

### ⏳ T2.9 — Vérification indépendante et recette

- Un vérificateur neuf cherche à réfuter l'égalité des poses, l'absence de changement pour les données existantes et l'état des builds et des tests.
- Recette de l'auteur : navigation de la vue 3D de l'éditeur, aperçus, démos.

## Tranche 3 — Retrait et migration (cadrage)

- Les trois anciennes classes sont retirées.
- Les données qui les nomment sont traitées selon l'ADR de O6.
- Un test par fichier de données du dépôt compare la vue obtenue avant et après.
- Tâches détaillées à écrire au lancement de la tranche. Relecture indépendante du plan avant approbation, vérification indépendante du résultat avant de le dire fait.

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
| O6 | Tranche 3 : les données qui nomment une ancienne classe se lisent-elles encore (le chargeur les traduit en caméra simple plus composant de comportement), ou sont-elles migrées une fois pour toutes ? La règle du dépôt est la sérialisation additive. Proposition : le chargeur les traduit, et l'éditeur écrit la nouvelle forme | Tranche 3 |
| O9 | **Tranché le 2026-11-19, selon la proposition.** Spécification du suivi 2D (D12), à neuf puisque l'ancienne classe ne suit rien. Proposition : `Follow2dCameraComponent`, posé sur l'entité d'une caméra simple. Réglages lus dans les données : `target_entity_name` (nom d'une entité du monde), `dead_zone` (largeur et hauteur en unités du monde, centrée sur la caméra), `limits` (rectangle du monde x, y, largeur, hauteur ; largeur ou hauteur nulle : pas de limite). À chaque `Update` : sur chaque axe, si la cible dépasse le bord de la zone morte, la caméra avance du dépassement ; puis sa position x, y est ramenée dans les limites ; son z ne change pas. Il écrit la position locale du composant caméra. Cible absente ou introuvable : il ne fait rien et le signale une fois. Il ne pilote pas `Camera2dComponent`, qui se place par sa propre cible (O7) | T2.1, T2.4 |
| O10 | **Tranché le 2026-11-19 : aucun changement, voir T2.1.** Politique d'entité. `EntityPolicyResolver` donne `DynamicDefault` à `CameraLookAtComponent`, et rien de propre à `ArcBallCameraComponent` ni à `PerspectiveCameraComponent`. Les usages de la visée passant sur la caméra simple (D11), leur politique changerait. Proposition : lire en T2.6 ce que vaut la politique d'un composant non listé ; si elle diffère de `DynamicDefault`, ajouter `PerspectiveCameraComponent` au même cas que `CameraLookAtComponent`, et le dire dans l'ADR | T2.1, T2.6 |
| O7 | La caméra 2D. `Camera2dComponent` est déjà simple, mais se place d'après une cible et non d'après sa transformation. Faut-il l'aligner sur la caméra 3D en tranche 2, au prix d'un changement de ses clés ? Proposition : ne pas y toucher tant qu'aucun besoin ne le demande | Tranche 2 |

## Hors périmètre

- Une action de cutscene pour le décalage (D5).
- Un sprite orientable en 3D ou toujours face à la caméra.
- Une matrice de projection entièrement libre.
- Tout changement du rendu.
