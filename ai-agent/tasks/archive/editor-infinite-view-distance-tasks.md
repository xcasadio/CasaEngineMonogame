# Plan agent IA — Distance d'affichage infinie des caméras de l'éditeur

Plan d'exécution d'une demande de l'auteur (2026-10-09) : à l'ouverture d'un grand monde (la piste Beginner de RacingGameCasaEngine, environ 1 500 unités), la vue 3D de l'éditeur coupe la géométrie lointaine. En mode édition, la caméra doit avoir une distance d'affichage infinie.
Les décisions D1 → D4 ci-dessous ont été arbitrées avec l'auteur le 2026-10-09 : **ce plan les applique, il ne les rediscute pas**.

Ce fichier doit être mis à jour pendant le travail : l'icône au début de chaque tâche indique son statut courant.

> **Historique des relectures** (2026-10-09) :
> - **Première relecture : REVISE**, avec deux blocages.
>   1. Le ciel par cubemap casse avec une projection infinie : `SkyCubemap.fx` déprojette le point de clip z = 1, qui devient le point à l'infini (w = 0).
>   2. Il manquait le retour arrière et le budget.
>   Corrigés par P6 et par la section « Retour arrière, budget et arrêts ».
> - **Seconde relecture : REVISE**, avec deux blocages : le rayon du gizmo (viewport de profondeur 0 à 1, profondeur 1 à l'infini), et la précision du rayon de sélection loin de l'origine. Corrigés par P7 (rayons en espace vue).
> - **Relecture de clôture : READY.**
> - **Révision demandée par l'auteur** (D4) : au lieu d'une propriété `Camera3dComponent.InfiniteFarPlane` dans le moteur, une **fabrique de l'éditeur** règle les caméras de l'éditeur avec `FarPlane = +∞`.
>   - P7 rend cela possible : les rayons ne lisent plus la profondeur du viewport, qui ne sert alors qu'à la projection et à l'enregistrement.
>   - P1 et P3 changent, et T1.1 et T1.2 avec eux ; P6 et P7 restent.
>   - Cette révision ouvre une relecture de clôture.
> - **Relecture de clôture de cette révision : REVISE**, sur un point qu'aucune relecture n'avait vu :
>   - la plage de tuiles visibles d'une tilemap vient des huit coins du frustum (`TileMapComponent.cs:1932-1938`). Avec un lointain infini, les quatre coins lointains sont NaN ;
>   - le calcul du découpage par la tranche de la tilemap ne garde alors que les arêtes du quadrilatère proche. Il peut perdre le culling des tuiles, ou réduire la plage au quadrilatère proche et ne plus dessiner la tilemap.
>
>   **Disposition : corrigé**, par la correction minimale du relecteur : `TryGetFrustumSlabBounds` renvoie faux dès qu'un coin n'est pas fini, et l'appelant garde toute la plage de tuiles, ce que sa doc prévoit déjà. T1.1 l'inclut, avec un test.
>
>   L'auteur ayant demandé d'enchaîner toutes les tâches (2026-10-09), l'exécution suit sans nouvelle boucle de relecture. Le vérificateur de fin de chantier contrôle ce point.

> **Quand écrire un plan** : dès que le travail demande plus d'un commit. En dessous, exécution directe avec le rapport de fin de tâche d'`AGENTS.md`.
> **Avant d'écrire le plan** : poser toutes les questions en une seule fois ; ne rien inventer, ne rien supposer.
> **Après approbation** : exécution autonome, tâche par tâche ; arrêt uniquement sur ⚠️ Blocked.

## Objectif

- La caméra d'édition de la vue du monde dessine toute la géométrie devant elle, sans plan lointain.
- Elle peut reculer jusqu'à 100 000 unités de sa cible, à la molette comme au cadrage par double-clic. Une piste de 1 500 unités se voit donc en entier.
- Les caméras des aperçus d'assets (modèle, matériau, sprite, particules, clip d'animation) ont elles aussi une distance d'affichage infinie.
- Une fabrique de l'éditeur est le seul endroit qui crée et règle ces caméras.
- Rien ne change pour le jeu : en mode Play et au runtime, les caméras gardent leur plan lointain, et aucun format d'asset ne change.

Ce que le chantier ne livre pas est dans « Hors périmètre ».

## État vérifié du dépôt (2026-10-09)

Découverte en lecture seule (workflow de 4 lecteurs et d'un critique ; les 6 faits porteurs revérifiés dans le code), plus deux sondes du scratchpad sur les types MonoGame eux-mêmes (`MonoGame.Framework.dll` de `CasaEngine.Editor/bin`, dossiers `infcam/probe` et `infcam/probe2`).

- **Branche** : `chantier/rgce-editor-project`, arbre propre. HEAD `ba7b4e41f`, un commit de l'auteur (`fix(editor): skip GDI thumbnails for tga and dds textures`, `ThumbnailCache.cs`), fait le 2026-10-09 après `359a0a98b`, le commit que pointe RacingGame. Il ne touche aucun fichier de ce plan. C'est la branche moteur du chantier RacingGameCasaEngine, dont la décision D3 veut que les manques de l'éditeur s'y corrigent.
- **Plan lointain** :
  - `CameraComponent` n'a pas de champ propre pour le proche et le lointain : ce sont `Viewport.MinDepth` et `Viewport.MaxDepth`, utilisés comme des distances (`CasaEngine/Framework/Scene/Entities/Components/CameraComponent.cs:49-69`) ;
  - le setter de `FarPlane` fait `MaxDepth = Math.Max(valeur, MinDepth + 0,001)` et accepte donc `+∞`. `ViewDistance` vaut alors `+∞`. Le setter de `NearPlane` borne par `MaxDepth − 0,001`, qui reste `+∞` (`:49-69`) ;
  - `InitializeWithWorld` remet `MinDepth` à 1, `MaxDepth` à 1 000 et `ViewDistance` à 999 (`:95-99`). Tout réglage du plan lointain se refait donc après chaque `InitializeWithWorld` ;
  - la projection n'est construite qu'à un endroit, `Camera3dComponent.ComputeProjectionMatrix` : `Matrix.CreatePerspectiveFieldOfView(fov, aspect, MinDepth, MaxDepth)` (`Camera3dComponent.cs:36-45`), avec un indicateur de recalcul (`CameraComponent.cs:31-43`).
- **Qui lit la profondeur du viewport d'une caméra** (`MinDepth`, `MaxDepth`), par `rg "Camera\??\.Viewport"` et la lecture de `CameraComponent` :
  - la projection (3D et 2D) ;
  - `RayHelper`, par `ViewManager.ViewToWorldRay` (`ViewManager.cs:304-312`) ;
  - l'enregistrement (`view_distance`, viewport) ;
  - des tests de la caméra 2D.
  `RenderFrameFactory` n'en lit que les bornes (`RenderFrameFactory.cs:72`), et `ScreenEffectComponent` la largeur et la hauteur (`:174-182`). Ce viewport n'est jamais posé sur le `GraphicsDevice` : seul `CasaEngineGame.cs:717-719` y pose un viewport, celui du back buffer.
- **Caméras de l'éditeur** : toutes sont créées de la même façon. Une entité cachée (`Name`, `IsVisible = false`) reçoit la caméra par `AddComponent`, puis `Initialize()` et `InitializeWithWorld(monde)` :
  - **vue du monde** : `ArcBallCameraComponent` créée par `EditorViewportCameraController.CreateCameraComponent` (`CasaEngine.Editor/Runtime/EditorViewportCameraController.cs:47-56`), entité `EditorViewportCamera` (`CasaEngine.Editor/Controls/WorldViewportPanel.cs:1270-1281`). La vue la **réinitialise** dans le nouveau monde à chaque changement, entrée et sortie du mode Play comprises (`:1708`) ;
  - **aperçus**, avec un `CameraLookAtComponent` chacun :
    - `ModelPreviewViewport.cs:329-341`, qui règle ensuite proche 0,05 et lointain 50 ;
    - `MaterialPreviewViewport.cs:319-329` ;
    - `SpritePreviewViewport.cs:314-323` ;
    - `ParticlePreviewViewport.cs:478-488` ;
    - `AnimationClipPreviewPanel.cs:526-536`.
    Ils ne les réinitialisent pas ailleurs, et leurs `ConfigureCamera` ne touchent ni le proche ni le lointain ;
  - aucune caméra de l'éditeur n'est enregistrée : `.casaeditor/viewport.editor.json` ne garde que la vue 2D (`EditorViewportViewStateSerializer.cs:29-46`), et les aperçus créent la leur en code ;
  - la distance de la caméra d'édition à sa cible est bornée à [0,5 ; 1 000] (`MaxDistance`, `EditorViewportCameraController.cs:33-34`, appliquée `:83`, `:93`, `:230`). `FocusBounds` demande max(5, diagonale × 1,5), que `Focus` borne ensuite (`WorldViewportPanel.cs:774-777`) ;
  - en mode Play, la vue prend la première caméra du monde joué, ou `world.CreateDefaultCamera()` (`WorldViewportPanel.cs:380-410`). Le retour restaure la caméra d'édition ;
  - les vignettes du navigateur de contenu (`ParticleSceneThumbnailRenderer.cs:280`, `SpriteSceneThumbnailRenderer.cs:250`) ont aussi leur caméra.
- **Ce que MonoGame fait d'un lointain infini**, mesuré par la première sonde :
  - `Matrix.CreatePerspectiveFieldOfView(fov, aspect, 1, float.PositiveInfinity)` ne lève pas d'exception, et donne la matrice à lointain infini exacte : `M33 = -1`, `M34 = -1`, `M43 = -near`, `M44 = 0`. Une profondeur de vue de 1 500 donne z = 0,99933 : la précision d'un tampon de 24 bits dépend du plan proche, et reste celle d'aujourd'hui dans les 1 000 premières unités ;
  - `new BoundingFrustum(view × projection infinie)` a un plan lointain indéfini (normale NaN). Pourtant, `Contains` rend `Intersects` pour des boîtes devant la caméra (à 10, 1 500 et 100 000 unités) et `Disjoint` pour une boîte derrière : le culling par les cinq autres plans continue ;
  - `Matrix.Invert` de cette matrice est fini. Mais le ciel par cubemap s'en sert pour déprojeter le point de clip z = 1 (`SkyCubemapRenderer.cs:62`, `CasaEngine/Content/Shaders/SkyCubemap.fx:24-26`), puis divise par w. Avec la projection infinie, z = 1 est le point à l'infini, w y vaut 0, et la direction devient infinie ou NaN. C'est le seul consommateur de l'inverse de `ViewProjection` (`rg "Invert\("`), et le seul shader qui lit une profondeur de clip de la caméra ;
  - `Viewport.Unproject` normalise la profondeur par `MinDepth` et `MaxDepth`. Avec `MaxDepth` infini, les deux points du rayon de sélection coïncident, et la direction devient NaN.
- **Qui lit la projection** :
  - `RenderFrameFactory` → `RenderFrame.ViewProjection` (`RenderFrame.cs:55-58`). De là viennent :
    - le frustum de culling du monde (`World.cs:816`), par ses plans ;
    - le culling par blocs des tilemaps tournées (`TileMapComponent.cs:749-754`), par ses plans ;
    - la plage de tuiles visibles des tilemaps alignées. Elle vient, elle, des **huit coins** du frustum (`TileMapComponent.cs:1932-1938`) : leurs arêtes sont découpées par la tranche Z de la tilemap (`TryGetFrustumSlabBounds`, `:1974-2034`). La doc de la méthode suppose un lointain fini (`:1914-1918`). Avec le lointain infini, les quatre coins lointains sont NaN, et seules les arêtes du quadrilatère proche passent. C'est le seul appel de `GetCorners` sur un frustum de caméra (`rg "GetCorners\("`) ;
  - la sélection, par deux chemins qui déprojettent les profondeurs écran 0 et 1, puis soustraient les deux points du monde :
    - `ViewManager.ViewToWorldRay` → `RayHelper.CalculateRayFromScreenCoordinate` (`RayHelper.cs:8-29`), sur le viewport de la caméra. Les deux points tombent à environ 0,001 unité l'un de l'autre près de l'œil : la direction perd sa précision quand la caméra est loin de l'origine. À 100 000 unités, l'écart entre deux flottants (0,0078) dépasse celui des points. Avec `MaxDepth` infini, la direction est NaN ;
    - `Gizmo.ConvertMouseToRay` (`GizmoTool/Gizmo.cs:1273-1291`), sur un viewport neuf de profondeur 0 à 1 que lui donne l'éditeur (`EditorViewportGizmoController.cs:253-254,448-457`, `TransformGizmoComponent.cs:101`). Avec la projection infinie, la profondeur 1 est le point à l'infini ;
    - `GizmoTool` ne référence pas CasaEngine (`GizmoTool.csproj`), et `CasaEngine.Tests` référence `CasaEngine.Editor`, donc `GizmoTool` ;
    - **mesuré** par la seconde sonde, erreur de la direction du rayon au centre de l'écran, pour une caméra à 10, 3 000 et 100 000 unités de sa cible :

      | Méthode | 10 | 3 000 | 100 000 |
      |---|---|---|---|
      | `RayHelper` aujourd'hui, projection finie | 5,6e-3 | 8,8e-3 | 0,88 |
      | `RayHelper` aujourd'hui, projection infinie (`MaxDepth` fini) | 2,7e-3 | 3,6e-2 | NaN |
      | gizmo aujourd'hui, projection infinie | NaN | NaN | NaN |
      | calcul en espace vue (P7), finie, infinie ou orthographique | ≤ 1e-7 | ≤ 1e-7 | ≤ 1e-7 |

      Avec le calcul de P7, le rayon du pixel d'un point connu passe à moins de 2e-7 × sa distance de ce point ;
  - les icônes de lumière : `Viewport.Project` sur un viewport neuf, de profondeur 0 à 1 (`EditorLightBillboardOverlayRenderer.cs:50,83-93`) ;
  - le gizmo ;
  - le soleil du ciel procédural (`SkyBackgroundViewPipeline.cs:85-97`), placé à 2 048 unités de la caméra et rejeté quand sa profondeur projetée dépasse 1. Avec un lointain de 1 000, il n'est donc jamais dessiné ; avec la projection infinie, il le sera.
  Les ombres n'utilisent pas le lointain de la caméra (`ShadowPass.cs:227-253`, `ShadowSettings.MaxDistance`).
- **Shaders intégrés** : `Content/Content.mgcb` les compile au build, par le paquet `MonoGame.Content.Builder.Task` (`CasaEngine/CasaEngine.csproj:23`, `Content.mgcb:70-74` pour `SkyCubemap.fx`). Aucun `.xnb` n'est suivi par git.
- **Enregistrement** : les caméras 3D écrivent `view_distance`, le viewport (`min_depth`, `max_depth`) et `fieldOfView` (`CasaEngine.EditorServices/EditorEntityJsonSerializer.cs:335-361`, `JsonHelper.cs:142-143`).
- **Tests existants** :
  - `CasaEngine.Tests/Scene/ArcBallCameraComponentTests.cs` construit la caméra sans monde, par `OnScreenResized`, `FarPlane` puis `NearPlane` (`:16-25`) ;
  - `CasaEngine.Tests/Editor/EditorViewportCameraControllerTests.cs` existe ;
  - aucun test ne couvre `Camera3dComponent.ComputeProjectionMatrix`.
- **Monde** : la racine de l'octree s'étend à ±100 000 (`WorldSpatialServices.cs:28`).

## Décisions verrouillées

| Réf | Décision |
|---|---|
| D1 | En mode édition, la caméra de la vue du monde a une distance d'affichage infinie. En mode Play, les caméras du jeu gardent leur plan lointain. |
| D2 | La distance maximale de la caméra d'édition à sa cible passe de 1 000 à 100 000, pour la molette comme pour le cadrage par double-clic. |
| D3 | Les caméras des aperçus d'assets ont aussi une distance d'affichage infinie. |
| D4 | Pas de nouvelle propriété de caméra dans le moteur : une **fabrique de l'éditeur** crée et règle les caméras de l'éditeur, avec `FarPlane = +∞`. |

## Points à valider avec ce plan

Arbitrages proposés par l'agent : l'approbation du plan les valide.

| Réf | Proposition |
|---|---|
| P1 | **Lointain infini par l'API existante** : `FarPlane = float.PositiveInfinity`.<br>• `ComputeProjectionMatrix` passe alors `+∞` à `CreatePerspectiveFieldOfView`, qui donne la matrice à lointain infini exacte (mesuré) ;<br>• `ViewDistance` vaut `+∞`. Les caméras de l'éditeur n'étant jamais enregistrées, aucun fichier n'en contient ;<br>• le moteur ne change pas son API de caméra. |
| P2 | **Culling** :<br>• **par les plans** (monde, blocs des tilemaps tournées) : le frustum reste construit depuis `RenderFrame.ViewProjection`. Avec la projection infinie, son plan lointain ne rejette rien, et les cinq autres plans cullent comme avant (mesuré sur MonoGame). Ce chemin ne change pas, mais un test le fixe ;<br>• **par les coins** (plage de tuiles d'une tilemap alignée) : `TryGetFrustumSlabBounds` renvoie faux dès qu'un coin n'est pas fini. L'appelant garde alors toute la plage de tuiles, comme quand le frustum n'atteint pas la tranche. Avec une caméra finie, rien ne change ;<br>• **conséquence**, seulement avec une caméra de l'éditeur à lointain infini : les tuiles d'une tilemap alignée ne sont plus cullées une à une. Le culling de l'entité reste actif. |
| P3 | **`EditorCameraFactory`** (`CasaEngine.Editor/Runtime/EditorCameraFactory.cs`, interne), seul endroit qui crée et règle les caméras de l'éditeur :<br>• `FarPlane` : la constante `float.PositiveInfinity` ;<br>• `ApplySettings(Camera3dComponent camera)` pose ce lointain ;<br>• `CreateCameraEntity(string name, Camera3dComponent camera, World world)` crée l'entité cachée, ajoute la caméra, appelle `Initialize`, puis `InitializeWithWorld`, et applique les réglages ;<br>• `InitializeWithWorld(Entity entity, Camera3dComponent camera, World world)` réinitialise la caméra dans un autre monde, puis réapplique les réglages, puisque l'initialisation remet le lointain à 1 000.<br>**Utilisateurs** :<br>• la vue du monde, à la création (`WorldViewportPanel.cs:1270-1281`) et au changement de monde (`:1708`) ;<br>• les cinq aperçus, à la création. L'aperçu de modèle garde son proche de 0,05 et perd son lointain de 50.<br>Les vignettes ne changent pas (« Hors périmètre »). La caméra 2D de la vue (`Camera2dComponent`, orthographique) non plus. Les caméras du mode Play viennent du monde joué et ne passent pas par la fabrique. |
| P4 | **Branche** : `chantier/rgce-editor-project`, à la suite de son dernier commit (D3 du plan RacingGame `ai-agent/tasks/rgce-editor-project-tasks.md`). Après T1.3, RacingGame enregistre le pointeur `CasaEngine` dans un commit `chore(casaengine): …` sur `remaster`. Ni push ni fusion. |
| P5 | **ADR-0074** :<br>• la fabrique et le lointain `+∞` par l'API existante ;<br>• la correction du ciel (P6) ;<br>• les rayons en espace vue (P7) ;<br>• la limite : une caméra de scène réglée à `+∞` (par exemple dans l'inspecteur) aurait `ViewDistance` et `max_depth` infinis, et l'enregistrement d'une valeur non finie n'est pas pris en charge par ce chantier. |
| P6 | **Ciel par cubemap** :<br>• `SkyCubemap.fx` calcule la direction par `normalize(worldPosition.xyz - EyePosition * worldPosition.w)` au lieu de `normalize(worldPosition.xyz / worldPosition.w - EyePosition)` ;<br>• pour toute projection finie, w > 0, et les deux formes donnent la même direction ;<br>• pour la projection infinie, w vaut 0 au point de clip z = 1, et la nouvelle forme donne la direction du point à l'infini, sans division ;<br>• rien d'autre ne change dans le shader ni dans `SkyCubemapRenderer`.<br>**Effet attendu**, à décrire dans la note : avec la projection infinie, le soleil du ciel procédural, à 2 048 unités, devient visible là où le lointain de 1 000 le rejetait. |
| P7 | **Rayons de sélection en espace vue** :<br>• `RayHelper.CalculateRayFromScreenCoordinate` et la conversion du gizmo calculent le rayon ainsi :<br>  – les coordonnées normalisées du pixel, par X, Y, la largeur et la hauteur du viewport ;<br>  – deux points en espace vue, par l'inverse de la projection, aux profondeurs de clip 0 (plan proche) et 0,5 (finie aussi avec la projection infinie) ;<br>  – l'origine (le point proche) et la direction (leur différence, normalisée) passent en monde par l'inverse de la vue.<br>• En espace vue, les coordonnées restent petites, quelle que soit la distance de la caméra à l'origine. La méthode vaut pour une projection perspective finie ou infinie, et orthographique, et **ne lit plus `MinDepth` ni `MaxDepth`** du viewport : un `FarPlane` infini ne la touche pas ;<br>• **comportement** : pour une caméra près de l'origine, l'origine (au plan proche) et la direction sont celles d'aujourd'hui, à la précision près. Loin de l'origine, le rayon devient juste, là où il perdait sa précision ;<br>• **gizmo** : `GizmoTool` ne voit pas CasaEngine, donc il reçoit une fonction publique `GizmoRay.FromScreen(position, viewport, view, projection)`, qui reprend la même méthode. `Gizmo.ConvertMouseToRay` l'appelle. |

## Règles d'exécution pour l'agent

- **Branche `chantier/rgce-editor-project`** (P4). Ne jamais committer sur `main`.
- **Une seule tâche à la fois.** Avant de commencer une tâche, remplacer son icône `⏳` par `🚧`. À la fin, lancer la validation indiquée, remplacer l'icône par `✅`, `🧪` ou `⚠️`, ajouter une courte note de validation sous la tâche, puis **créer un commit dédié** qui inclut la mise à jour de ce fichier.
- **Un commit par tâche**, atomique et compilable, message en anglais au format `type(area): summary`, terminé par la ligne `Co-Authored-By` de la session. Les commandes de commit commencent par `cd D:/development/repo/RacingGame/CasaEngine &&`.
- **Ne jamais pousser.** Le merge sur `main` reste une décision humaine.
- **Ne rien inventer** : toute API, tout fichier, toute règle utilisée existe dans le dépôt, vient d'une réponse de l'auteur, ou d'une doc officielle citée (URL). Sinon : passer la tâche en ⚠️ Blocked, écrire la question dans « Points ouverts », et **s'arrêter**.
- **Build obligatoire** avant ✅ : `dotnet build CasaEngine.MonoGame.sln` et `dotnet build CasaEngine.Editor.MonoGame.sln` ; **tests** `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj`. Si le build est impossible, la tâche reste 🧪 avec la raison écrite.
- Si le code est écrit mais qu'une vérification visuelle manque, utiliser `🧪 Needs testing` et noter précisément ce qui manque.
- **Ne jamais laisser une tâche en 🚧** à la fin d'une session.
- **Ne jamais indexer** les modifications de l'auteur : `git add` fichier par fichier.
- **Langue** : ce plan en français ; code, messages de commit, docs de `docs/` et ADR en anglais.
- **Lancer l'éditeur** sur des copies de l'éditeur et du contenu seulement, un seul processus de jeu ou d'éditeur à la fois ; sondes dans le scratchpad.
- Rappel moteur : `RayHelper` n'est pas dans une boucle par image, et la fabrique ne s'exécute qu'à la création ou au changement de monde : ni allocation ni coût ajoutés au rendu.

## Légende des statuts

- ⏳ Todo : pas encore commencé.
- 🚧 In progress : en cours de modification locale.
- 🧪 Needs testing : code écrit, validation incomplète ou en attente.
- ✅ Done : code validé, build/tests OK, commit effectué.
- ⚠️ Blocked : bloqué par une erreur non résolue ou une décision manquante.

## Validation globale

- `dotnet build CasaEngine.MonoGame.sln` et `dotnet build CasaEngine.Editor.MonoGame.sln` : 0 erreur.
- `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj` : tout passe, nouveaux tests compris.
- **Éditeur**, sur une copie (T1.2) : un objet placé à 5 000 unités devant la caméra d'édition par défaut est absent de la capture de l'éditeur d'avant, et présent dans celle d'après.
- **Jeu** : RacingGameCasaEngine, compilé avec le nouveau pointeur, garde son export de scène identique à `artifacts/o7-track-helpers/after/scene-a.json`, et `--smoke-frontend` sort à 0.
- Vérificateur indépendant : CONFIRMED.

## Retour arrière, budget et arrêts

- **Retour arrière** :
  - dans CasaEngine, `git revert` des commits de T1.3, T1.2 puis T1.1 sur `chantier/rgce-editor-project`, dans cet ordre. Aucun ne réécrit l'historique ;
  - dans RacingGame, `git revert` du commit `chore(casaengine): …` sur `remaster`, qui remet le pointeur sur `359a0a98b`, puis `git submodule update` ;
  - aucun push, dans aucun des deux dépôts.
- **Budget** :
  - au plus 3 cycles de correction par tâche, chaque cycle étant une correction suivie de la revalidation complète de la tâche ;
  - au plus 2 lancements du contrôle visuel (étape 2 de T1.2) et de la comparaison d'export (T1.3) qui ne passent pas.
  Au-delà, la tâche passe en ⚠️ Blocked, avec la question dans « Points ouverts ».
- **Arrêts** :
  - un test existant qui casse sans explication ;
  - un écart de l'export de scène RacingGame ;
  - un ciel différent sur la capture de T1.2, autre que l'effet attendu de P6 ;
  - un constat P0 ou P1 du vérificateur.

---

## Phase 1 — Lointain infini et caméras de l'éditeur

### ✅ T1.1 — Ciel et rayons de sélection exacts sans plan lointain (P1, P2, P6, P7)

- Objectif : avec `FarPlane = +∞`, le ciel par cubemap, le culling et les rayons de sélection restent justes, et les rayons le restent loin de l'origine. Aucune API de caméra ne change.
- Fichiers :
  - `CasaEngine/Core/Math/Geometry/RayHelper.cs` : le calcul de P7 ;
  - `CasaEngine/Content/Shaders/SkyCubemap.fx` : la direction de P6 ;
  - `CasaEngine/Framework/Scene/Entities/Components/TileMapComponent.cs` : `TryGetFrustumSlabBounds` renvoie faux sur un coin non fini, et sa doc le dit (P2) ;
  - `CasaEngine.Tests/Scene/InfiniteFarPlaneTests.cs` (nouveau) ;
  - `CasaEngine.Tests/TileMap/TileMapVisibleTileRangeTests.cs` : le cas du lointain infini.
- Étapes :
  1. Faire les deux changements.
  2. Écrire les tests, sur des caméras construites sans monde, comme dans `ArcBallCameraComponentTests` (`OnScreenResized`, `FarPlane`, puis `NearPlane`) :
     - **projection** : avec `FarPlane = float.PositiveInfinity`, la projection d'un `ArcBallCameraComponent` a `M33 = -1`, `M34 = -1` et `M43 = -NearPlane`, tous finis ;
     - **culling** : un `BoundingFrustum` construit depuis `view × projection` ne rejette pas une boîte à 5 000 unités devant la caméra (`Disjoint` avec `FarPlane = 1000`), et rejette une boîte derrière ;
     - **rayon de sélection** (P7), par `RayHelper.CalculateRayFromScreenCoordinate` avec le viewport de la caméra, sur un `ArcBallCameraComponent` de cible (100, 20, −50), à `Distance` 10, 3 000 et 100 000, avec `FarPlane` 1 000 et `+∞` :
       - au centre du viewport, la direction est finie et égale à l'axe de la caméra, à 1e-4 près ;
       - pour un point du monde connu devant la caméra, projeté sur l'écran par `Viewport.Project` (viewport de profondeur 0 à 1), le rayon qui part de ce pixel passe à moins de 1e-3 × sa distance de ce point ;
       - en orthographique, sur un `Camera2dComponent` : la direction est l'axe de la caméra, et l'origine suit le pixel ;
     - **tilemap** : les coins de `BoundingFrustum(view × projection infinie)`, passés à `TryGetFrustumSlabBounds` avec une tranche que le quadrilatère proche traverse, donnent faux (toute la plage), et non une étendue réduite au quadrilatère proche. Les tests existants de `TileMapVisibleTileRangeTests`, au lointain fini, passent toujours ;
     - **ciel** : le calcul de P6, refait en C# avec `Matrix.Invert(view × projection)`, donne pour le point de clip (0, 0, 1, 1) et les quatre coins (±1, ±1, 1, 1) une direction finie :
       - avec la projection infinie, celle du centre suit l'axe de la caméra, à 1e-4 près ;
       - avec la projection finie, chaque direction est égale, à 1e-4 près, à celle de l'ancienne forme (division par w).
- Validation : les deux solutions compilent avec 0 erreur ; `CasaEngine.Tests` passe en entier.
- Commit : `fix(rendering): keep the sky, picking rays and tile ranges exact without a far plane`

> Validation (2026-10-09) :
> - **Code** :
>   - `RayHelper.CalculateRayFromScreenCoordinate` calcule le rayon en espace vue (P7). Il ne lit plus la profondeur du viewport ;
>   - `SkyCubemap.fx` calcule `normalize(worldPosition.xyz - EyePosition * worldPosition.w)` (P6). Le `.xnb` de l'éditeur est recompilé au build (07:02, après la modification du `.fx`) ;
>   - `TileMapComponent.TryGetFrustumSlabBounds` renvoie faux sur un coin non fini, et sa doc le dit (P2).
> - **Tests** :
>   - `InfiniteFarPlaneTests` (17 cas) :
>     - la projection avec `FarPlane = +∞` (`M33 = M34 = -1`, `M43 = -near`, `ViewDistance` infini) ;
>     - le culling : une boîte à 5 000 unités est gardée, alors qu'elle est rejetée au lointain 1 000 ; une boîte derrière est rejetée ;
>     - le rayon au centre et le rayon du pixel d'un point connu, à 10, 3 000 et 100 000 unités, au lointain fini et infini ;
>     - le rayon orthographique ;
>     - le ciel, au centre et aux coins, au lointain fini et infini ;
>   - `TileMapVisibleTileRangeTests` : le cas du lointain infini.
> - **Les tests prouvent la correction**, mesuré en remettant l'ancien code un instant :
>   - avec l'ancien `RayHelper`, 12 des 13 cas de rayon échouent, dès 10 unités ;
>   - avec l'ancien `TryGetFrustumSlabBounds`, le cas de la tilemap échoue.
>   Le code corrigé a ensuite été remis, octet pour octet.
> - **Précision du ciel** : la direction au centre s'écarte de l'axe de 1e-6 à 10 unités, de 1e-4 à 3 000 et de 4e-3 à 100 000, avec l'ancienne formule comme avec la nouvelle (sonde `infcam/probe3`). C'est la précision de l'inverse `float` de la vue × projection, inchangée. Le test vérifie donc 1e-3 à 3 000 unités. La nouvelle formule reste définie au lointain infini, là où l'ancienne donne NaN.
> - `dotnet build CasaEngine.MonoGame.sln` et `CasaEngine.Editor.MonoGame.sln` : 0 erreur, et aucun avertissement dans les fichiers touchés. `CasaEngine.Tests` : 4 344 sur 4 344.

### ✅ T1.2 — Fabrique des caméras de l'éditeur à distance infinie (D1 à D4, P3, P7)

- Objectif : la caméra d'édition et les aperçus dessinent toute la géométrie devant eux, la caméra d'édition recule jusqu'à 100 000, et la fabrique est le seul endroit qui crée et règle ces caméras.
- Fichiers :
  - `CasaEngine.Editor/Runtime/EditorCameraFactory.cs` (nouveau, P3) ;
  - `CasaEngine.Editor/Controls/WorldViewportPanel.cs` : la caméra d'édition par la fabrique, à la création et au changement de monde ;
  - `CasaEngine.Editor/Runtime/EditorViewportCameraController.cs` : `MaxDistance` passe à 100 000 ;
  - `CasaEngine.Editor/Controls/ModelPreviewViewport.cs`, `MaterialPreviewViewport.cs`, `SpritePreviewViewport.cs`, `ParticlePreviewViewport.cs` et `AnimationClipPreviewPanel.cs` : leur caméra par la fabrique ;
  - `GizmoTool/GizmoRay.cs` (nouveau) : `GizmoRay.FromScreen` (P7) ; `GizmoTool/Gizmo.cs` : `ConvertMouseToRay` l'appelle ;
  - `CasaEngine.Tests/Editor/EditorCameraFactoryTests.cs` (nouveau) : `ApplySettings` pose `FarPlane = +∞` sur une caméra construite sans monde, et le refait après un `FarPlane = 1000`, comme le fait `InitializeWithWorld` ;
  - `CasaEngine.Tests/Editor/GizmoRayTests.cs` (nouveau) : avec le viewport de profondeur 0 à 1 que l'éditeur donne au gizmo et la projection d'une caméra à `FarPlane = +∞`, à `Distance` 10, 3 000 et 100 000, le rayon du centre est fini et suit l'axe de la caméra à 1e-4 près. Un point connu se retrouve sur le rayon de son pixel, comme pour `RayHelper` ;
  - `CasaEngine.Tests/Editor/EditorViewportCameraControllerTests.cs` : une distance de 50 000 est gardée, et 200 000 est ramenée à 100 000.
- Étapes :
  1. Faire les changements, puis lancer les tests.
  2. **Contrôle visuel automatique**, sur des copies de l'éditeur et du contenu RacingGameCasaEngine :
     - une copie de `Worlds/Test/TrackBeginnerGenerators.world` ne garde que sa lumière, et reçoit une entité `StaticModelComponent` (`StaticModel.Hotel01`, échelle 100). Elle est placée à 5 000 unités devant la caméra d'édition par défaut (cible à l'origine, lacet π/4, tangage −π/6), dans l'axe de sa vue ;
     - ouverture par `--open-asset`, capture par l'éditeur de `HEAD` puis par le nouveau : l'objet est absent avant et présent après ;
     - l'ouverture du monde de test complet reste à code 0, et la section `Logs:` n'a que les avertissements connus ;
     - le survol et le déplacement des axes du gizmo, que l'automatisation ne sait pas simuler, sont couverts par `GizmoRayTests`, et écrits dans le contrôle manuel de l'auteur.
  3. Ouvrir dans l'éditeur un `.staticModel` (aperçu de modèle) et un `.material` (aperçu de matériau) : code 0, et les captures sont lues.
  4. **Ciel par cubemap**, sur une copie :
     - le même monde de l'étape 2 reçoit dans son `environment` `background_mode` Environment et `background_cubemap_asset_id` = `Texture.SkyCubeMap` du contenu RacingGameCasaEngine ;
     - capture par l'éditeur de `HEAD`, puis par le nouveau : le ciel est le même ;
     - un écart de pixels est lu et expliqué. Seuls sont admis l'objet lointain, devenu visible, et le soleil s'il est dans le champ (P6).
- Validation : les deux solutions compilent avec 0 erreur ; `CasaEngine.Tests` passe ; les contrôles des étapes 2 à 4 sont décrits dans la note. Le contrôle manuel de l'auteur (molette jusqu'à voir toute la piste, double-clic de cadrage, axes du gizmo) est écrit dans la note ; la tâche est ✅ si les contrôles automatiques passent.
- Commit : `feat(editor): give the editor cameras an infinite view distance`

> Validation (2026-10-09) :
> - **Code** :
>   - `EditorCameraFactory` (`ApplySettings`, `CreateCameraEntity`, `InitializeWithWorld`) crée et règle les six caméras de l'éditeur. La vue du monde l'appelle à la création et au changement de monde, et les cinq aperçus à la création ;
>   - l'aperçu de modèle garde son proche de 0,05 et perd son lointain de 50 ;
>   - `MaxDistance` de la caméra d'édition passe à 100 000 ;
>   - `GizmoRay.FromScreen` (GizmoTool) calcule le rayon en espace vue, et `Gizmo.ConvertMouseToRay` l'appelle.
> - **Tests** :
>   - `EditorCameraFactoryTests` (2) : le lointain infini, réappliqué après une remise à 1 000 ;
>   - `GizmoRayTests` (10) : avec le viewport de profondeur 0 à 1 du gizmo, à 10, 3 000 et 100 000 unités, au lointain fini et infini ;
>   - `EditorViewportCameraControllerTests` : 50 000 est gardé, 200 000 ramené à 100 000, et `Focus(…, 3 000)` gardé.
>   `CasaEngine.Tests` : 4 357 sur 4 357. Les deux solutions compilent avec 0 erreur, et aucun avertissement dans les fichiers touchés.
> - **Éditeur**, sur des copies du contenu RacingGameCasaEngine. L'éditeur « avant » est une copie du build du 2026-10-08, antérieure à ce chantier, et l'éditeur « après » le build de cette tâche (`infcam/editor-checks/` du scratchpad) :
>   - **objet lointain** : le monde de contrôle garde la lumière et reçoit `StaticModel.Hotel01` à l'échelle 100, à 5 000 unités dans l'axe de la caméra d'édition par défaut. Cet axe est calculé par le moteur lui-même (sonde `infcam/axisprobe`), avec l'œil en (−6,12 ; 5 ; 6,12) et le point en (3 055,7 ; −2 495 ; −3 055,7) :
>     - avant, la vue est vide ;
>     - après, l'hôtel est dessiné ;
>   - **ciel par cubemap** : le même monde, sans l'hôtel, avec `background_mode` Environment sur `Texture.SkyCubeMap`. Avant et après, le ciel est le même : écart moyen de 0,003 par canal sur la vue, et 6 pixels sur 538 670 au-delà de 8, sur les lignes de la grille, où la profondeur change très légèrement ;
>   - le monde de test complet, `StaticModel.Hotel01` (aperçu de modèle) et un matériau de `Hotel01` (aperçu de matériau) s'ouvrent avec le code 0. Les captures lues montrent le modèle et la sphère ;
>   - chaque ouverture n'a que les deux avertissements connus (assemblée de scripts encore chargée).
> - **Contrôle manuel de l'auteur**, que l'automatisation ne sait pas faire :
>   1. ouvrir `Worlds/Test/TrackBeginnerGenerators.world` ;
>   2. reculer à la molette jusqu'à voir toute la piste : rien ne doit être coupé au loin ;
>   3. double-cliquer une entité pour la cadrer ;
>   4. survoler et déplacer les axes du gizmo, près de l'origine puis loin d'elle ;
>   5. passer en mode Play et revenir : la caméra d'édition reste sans plan lointain.

### ✅ T1.3 — ADR, documentation et pointeur RacingGame

- Fichiers :
  - `docs/decisions/0074-editor-cameras-have-an-infinite-far-plane.md` (P5) et l'index `docs/decisions/README.md` ;
  - `docs/engine/rendering-2d-3d-spaces.md` : le lointain `+∞` par `FarPlane`, ses limites, et le calcul des rayons de sélection ;
  - ce plan et `ai-agent/README.md` ; le plan passe dans `ai-agent/tasks/archive/`.
- Validation :
  - **RacingGame**, sur `remaster` :
    - le commit `chore(casaengine): give the editor cameras an infinite view distance` porte le pointeur ;
    - `dotnet build RacingGame.slnx` à 0 erreur ;
    - l'export de scène est identique à `artifacts/o7-track-helpers/after/scene-a.json` ;
    - `--smoke-frontend` sort à 0 ;
  - vérificateur indépendant : CONFIRMED.
- Commit : `docs(adr): record the infinite far plane of the editor cameras`

> Validation (2026-10-09) :
> - **Docs** : ADR-0074 (`docs/decisions/0074-editor-cameras-have-an-infinite-far-plane.md`) et son entrée d'index. Section « Caméra 3D sans plan lointain (éditeur) » de `docs/engine/rendering-2d-3d-spaces.md`, en français comme le reste du document. Le plan passe dans `ai-agent/tasks/archive/`.
> - **RacingGame**, sur le nouveau moteur :
>   - `dotnet build RacingGame.slnx` : 0 erreur ;
>   - export de scène identique à `artifacts/o7-track-helpers/after/scene-a.json` (1 372, 953, 2 829 enregistrements appariés) ;
>   - `--smoke-frontend` : code 0 ;
>   - réglages de l'auteur restaurés à l'identique.
>   Le commit du pointeur, sur `remaster`, enregistre aussi le commit de l'auteur `ba7b4e41f` (vignettes), antérieur à ce chantier sur la branche.
> - **Vérification indépendante, en workflow à deux angles : CONFIRMED pour les deux**, sans constat P0 à P2 (sorties dans `verify-infcam/` du scratchpad) :
>   - **angle qui exécute** :
>     - builds à 0 erreur, 4 357 tests sur 4 357 ;
>     - dans un worktree à `221e63065` avec l'ancien `RayHelper` et l'ancien `TileMapComponent`, 13 tests échouent : les 12 rayons, y compris au lointain 1 000, et la tilemap ;
>     - sa propre sonde : 200 caméras tirées au hasard par cas, à 10, 3 000 et 100 000 unités, au lointain 1 000 et infini. Erreur de direction au plus 2e-7 contre une référence en double précision, `GizmoRay` égal à `RayHelper`, et l'orthographique exact ;
>     - culling juste à l'origine comme à (90 000 ; 50 ; −90 000) ;
>     - ciel : écart de 1,4e-7 avec l'ancienne formule au lointain fini, et formule définie à l'infini ;
>     - dans l'éditeur, l'hôtel à 5 000 unités est absent avant et présent après, et le ciel est le même (1 pixel sur 125 125). Les aperçus de modèle, de matériau, de sprite et de particules s'ouvrent ;
>     - export RacingGame identique et smoke à 0 ;
>   - **relecture adverse**, en lecture seule : elle confirme la fabrique, la réinitialisation au changement de monde, le mode Play intact, les appelants de `RayHelper` (`ViewManager.ViewToWorldRay`, `WorldSpaceUIDemo`), l'absence d'allocation, et que l'ADR et la doc concordent avec le code.
> - **Avis reportés**, sans retouche du candidat confirmé :
>   - P3 : les caméras des vignettes ne passent pas par la fabrique. C'est voulu (« Hors périmètre »), et l'ADR le dit ;
>   - P3 : le commentaire de `Gizmo.ActiveViewport` (`GizmoTool/Gizmo.cs:202`) parle encore d'`Unproject` ;
>   - P4 : avec le plan lointain NaN, `BoundingFrustum.Contains` ne rend jamais `Contains`. La requête de l'octree teste donc chaque objet au lieu de prendre un nœud entier : le résultat est juste, pour un léger coût dans l'éditeur ;
>   - P4 : la sélection du runtime (`ViewToWorldRay`) change aussi, en mieux. L'ancien rayon lisait la profondeur 1..1000 du viewport et était faux même au lointain fini. L'ADR le dit (« Picking is more precise for every camera »).
> - **Non exécuté** :
>   - l'aperçu de clip d'animation, faute d'asset `.skeletonAnim` dans les projets ; son changement est le même appel de fabrique que les quatre autres ;
>   - le changement de monde dans l'éditeur (entrée et sortie du mode Play), qui reste dans le contrôle manuel de l'auteur (T1.2).

---

## Points ouverts

| Réf | Sujet | Tâche concernée |
|---|---|---|
| — | Aucun à l'écriture du plan. | — |

## Hors périmètre

- **Vignettes du navigateur de contenu** (particules, sprites) : leurs caméras ne changent pas.
- **Caméras de jeu**, en mode Play et au runtime : plan lointain inchangé.
- **Caméras de scène réglées à `+∞`** (inspecteur) : l'enregistrement d'une valeur non finie n'est pas pris en charge par ce chantier (P5).
- **Caméra 2D** de la vue du monde : inchangée.
- **Culling tuile par tuile** d'une tilemap alignée, vue par une caméra de l'éditeur à lointain infini : il laisse la place à toute la plage (P2). Le retrouver demanderait un frustum fini pour ce seul calcul.
- **Plan proche** de la caméra d'édition (1) et précision de profondeur : inchangés. La précision d'un tampon de 24 bits dépend du plan proche, et reste celle d'aujourd'hui dans les 1 000 premières unités.
- **Taille de la grille** de l'éditeur (±50 unités) : inchangée.
- **Distance de cadrage des aperçus** (par exemple la borne de 1 000 de l'aperçu de sprite, `SpritePreviewViewport.cs:405`) : inchangée.
- Push, fusion, pull request.
