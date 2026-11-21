# Plan agent IA — Sprites orientés par leur transformation

Les décisions D1 → D6 ci-dessous ont été arbitrées avec l'auteur le 2026-11-21 : **ce plan les applique, il ne les rediscute pas**.

Ce fichier doit être mis à jour pendant le travail : l'icône au début de chaque tâche indique son statut courant.

**Soumis à l'auteur : aucun code avant son approbation.**

Relecture de clôture de l'époque 2 (`17cf8eb2`) : REVISE, deux points ; les trois points de l'époque 1 sont résolus. Disposition : les deux sont **corrigés** dans cette version (époque 3), et la tâche de visibilité est **réduite** : elle reprend la règle que `TransformComponent` applique déjà (descendants qui se dessinent, composants physiques exclus) au lieu de compter tous les enfants, et elle ne traite plus les entités enfants. Points repris : la boîte comptait les composants physiques, que le moteur exclut exprès de la boîte de rendu et qu'un test existant vérifie ; l'invariant « une entité sans enfant garde sa boîte » contredisait le déplacement voulu de la boîte des caméras. Relecture de clôture de l'époque 3 (`cd72f5dc`) : **READY**.

Relu une fois par un relecteur neuf (`22217208`) : REVISE, trois points, tous repris dans cette version (époque 2) : la preuve d'équivalence lisait des sommets que le rendu ne transforme pas (la transformation est dans la matrice de chaque entrée, appliquée par le shader) ; aucune tolérance n'était fixée alors que l'équivalence n'est pas exacte au bit près ; un sprite enfant d'une caméra n'aurait été dessiné que si l'origine du monde était dans le champ, la boîte englobante d'une entité ignorant ses enfants. Sur ce dernier point l'auteur a demandé comment font les moteurs modernes : la boîte compte les enfants, sans attribut « toujours visible » (D7).

## Objectif

Un sprite du monde est dessiné avec la transformation de monde complète de son composant : position, orientation et échelle, parents compris. Un sprite tourné se voit tourné ; un sprite enfant d'une caméra suit cette caméra et lui fait face, sans composant dédié.

Besoin d'origine, en termes génériques : afficher une image fixe plus grande que le cadre sous une caméra perspective orientée librement (roulis compris), en plusieurs couches à des profondeurs différentes, entre lesquelles passent des objets 3D, et faire défiler le tout par le décalage de l'image de la caméra (ADR-0147).

## État vérifié du dépôt (2026-11-21, `main` `51ea3857`)

Relevé par trois lectures séparées (rendu des sprites ; composants de couche ; vue, caméra et données), puis recoupé par la session principale sur les faits qui portent une décision.

### Le sprite d'aujourd'hui

- `StaticSpriteComponent.Submit` passe au rendu la position (`Position.X`, `Position.Y`), une rotation de `0.0f` écrite en dur, l'échelle (`Scale.X`, `Scale.Y`) et `Position.Z` comme profondeur (`CasaEngine/Framework/Scene/Entities/Components/StaticSpriteComponent.cs:320-364`). Il ne lit pas `Orientation` pour dessiner.
- `Position` additionne les positions des parents, `Scale` multiplie leurs échelles, `Orientation` additionne leurs quaternions (`SceneComponent.cs:112-218`). Aucun des trois ne compose les transformations comme `WorldMatrixWithScale` le fait (`SceneComponent.cs:32-50`).
- `GetBoundingBox` du sprite utilise déjà `WorldMatrixWithScale` (`StaticSpriteComponent.cs:295-304`) : la boîte englobante suit la transformation complète, le dessin non.
- `AnimatedSpriteComponent` dessine ses parties avec la même position et la même échelle, plus une rotation propre à chaque partie, et un chemin de quad libre (`AnimatedSpriteComponent.cs:522-593`).

### Le rendu

- `SpriteRendererComponent` (dans `CasaEngine/Framework/Application/Components/`) garde par sprite une matrice de monde (`SpriteDisplayData.WorldMatrix`, `:24`). Le quad unitaire est mis à l'échelle de la source, placé d'après l'origine du sprite, tourné autour de z, puis multiplié par une transformation de monde si l'appelant en donne une (`:1145-1155`).
- **Une surcharge publique prend déjà cette transformation** : `DrawSprite(texture, source, origin, position, rotation, scale, color, z, effects, in Matrix worldTransform)` (`:1099-1106`). Les tuiles des tilemaps s'en servent (`CasaEngine/Framework/Assets/TileMap/Tile.cs:48`, `:78-96`). Elle ne prend ni mode de mélange, ni clé de tri, ni ordre de dessin, ni rectangle de découpe, ni dessin de mise au point ; la méthode privée qu'elle appelle les prend tous (`:1108-1112`).
- Le dessin passe par `frame.View` et `frame.Projection` de l'image en cours (`:196-218`, `:258`). Le décalage de l'image d'une caméra perspective est dans sa projection (`PerspectiveCameraComponent.cs:92-123`) : il s'applique donc aux sprites.
- Profondeur : test et écriture, `LessEqual` (`:144-149`). Le shader rejette un texel dont l'alpha, après produit par la couleur, vaut au plus 0,01 (`CasaEngine/Content/Shaders/SpriteBatch.fx:59-63`) : un texel transparent n'écrit pas la profondeur.
- Tri sans clé : z de monde décroissant, puis ordre de dessin, puis ordre de soumission (`:574-597`). Le z est lu dans la translation de la matrice de monde.
- `FillVertices` ne fait que recopier les quatre coins du quad unitaire (±0,5 ; ±0,5 ; 0) (`:161-164`, `:556-569`) : la transformation de monde n'est appliquée que par le shader, avec la matrice de l'entrée (`:322`, `SpriteBatch.fx:46`). Les tests existants lisent la file du rendu par réflexion (`CasaEngine.Tests/Scene/SpriteComponentDrawOrderTests.cs:326-401`, `Harness` ; `CasaEngine.Tests/Rendering/ScrollingLayers/ScrollingLayerComponentSubmissionTests.cs:58` lit `WorldMatrix`).
- Faces : `CullCounterClockwise`, sauf pour une entrée marquée `NoCull`, que seul le chemin des quads libres pose (`:250`, `:293-296`, `:868`, `:1164`). Un sprite vu de dos n'est pas dessiné.

### Les données

- 20 sprites (`StaticSpriteComponent`, `AnimatedSpriteComponent`, `VideoSpriteComponent`) dans 12 fichiers de `Projects/` et de `CasaEngine.Demos/Content/`. **Aucun n'a de rotation.** Aucun n'est l'enfant d'un autre composant ; quatre ont pour enfant un `CollisionComponent`. Relevé par `jq` sur chaque fichier.
- Un composant enfant se déclare dans `children_component` de son parent (`SceneComponent.cs:416-444`) ; `SceneComponent.Draw` descend dans les enfants (`:388-394`). `WorldMatrixWithScale` compose le parent composant, puis le composant racine de l'entité parente.

### La visibilité

- `World.Draw` ne dessine que les entités que l'index spatial rend pour le frustum de l'image (`CasaEngine/Framework/Scene/World/World.cs:955-970`). Une entité enfant n'est pas dans l'index : elle est dessinée par son parent (`Entity.cs:544-563`).
- La boîte d'une entité est celle de son composant racine, étendue par celles de ses composants d'entité (`Entity.cs:396-409`). **Elle ne compte ni les composants enfants ni les entités enfants.**
- **Un précédent existe** : `TransformComponent.GetBoundingBox` rend l'union des boîtes de ses descendants qui se dessinent (`IComponentDrawable`), à toute profondeur, en sautant tout sous-arbre `PhysicsBaseComponent`, parce que les composants physiques vivent dans l'espace logique et non dans l'espace de rendu (`TransformComponent.cs:39-74`). Le test `TransformComponentTests.GetBoundingBox_AfterUpdate_FollowsTheRenderPositionAndExcludesPhysics` le vérifie jusque sur `entity.GetBoundingBox()` (`CasaEngine.Tests/Physics/TransformComponentTests.cs:80-100`).
- Utilisateurs de la boîte d'une entité hors de l'index : le cadrage de l'éditeur (`CasaEngine.Editor/Controls/WorldViewportPanel.cs:822`, `CutsceneAssetInspectorPanel.cs:1723`) et les requêtes de voisinage (`SteeringAgentComponent.cs:329`, par `World.QueryEntities`).
- `CameraComponent.GetBoundingBox` rend une boîte fixe de −1 à 1, à l'origine du monde, sans la transformer (`CameraComponent.cs:122-125`). Une caméra posée loin de l'origine a donc sa boîte ailleurs qu'elle-même.
- Conséquence : un sprite enfant d'une caméra n'est dessiné que si le frustum contient l'origine du monde.
- L'index est remis à jour pour une entité quand son composant racine ou un de ses composants d'entité est marqué (`World.cs:670-673`, `:705-720`) ; les composants enfants ne sont pas consultés. `SceneComponent.Update` marque un composant dont la matrice de monde a changé, puis met à jour ses enfants (`SceneComponent.cs:371-386`).
- `Entity.Draw` dessine le composant racine (qui descend dans ses enfants) et, parmi les autres composants, seulement les `PrimitiveComponent` (`Entity.cs:551-559`) : une caméra qui n'est pas le composant racine de son entité ne dessine pas ses enfants.

### Ce que le moteur n'a pas

- Aucun mécanisme existant ne fait le besoin : `ScrollingLayerComponent` et `CellularLayerComponent` sont des composants de jeu créés par code, à qui le jeu pousse une position de caméra ; `FrozenFrameComponent` et `ScreenEffectComponent` couvrent la vue entière sans profondeur ; `VideoSpriteComponent` ignore la profondeur ; `TileMapSurfaceComponent` rend une tilemap dans une texture. Aucun mode de fond de monde ne prend une image (`EnvironmentBackgroundMode` : `LegacyClearColor`, `SolidColor`, `Environment`).
- `Billboard` n'existe que pour les particules.

### Autres

- Dernière ADR : 0150. `CasaEngine.Tests` : 8473 tests verts sur `51ea3857`.
- Arbre de travail : deux dossiers non suivis, `Projects/PixelUiSample/.casaeditor/` et `Projects/SpriteCutsceneSample/.casaeditor/`. Ne jamais les indexer.

## Décisions verrouillées

| Réf | Décision |
|---|---|
| D1 | **Le sprite devient orientable** : il est dessiné avec la transformation de monde complète de son composant. Pas de composant dédié « image attachée à la caméra ». |
| D2 | **L'attache à une caméra se fait par la hiérarchie des transformations** : un sprite enfant de la caméra la suit. |
| D3 | **Sample dédié, nouveau** : une caméra inclinée avec du roulis, deux couches d'image plus grandes que le cadre, une boîte entre les deux, les flèches qui décalent l'image. |
| D4 | Le dimensionnement d'un sprite pour qu'un pixel de son image fasse un pixel du cadre est **calculé par les données** : aucune aide dans le moteur dans ce plan. |
| D5 | Périmètre : `StaticSpriteComponent`. `AnimatedSpriteComponent` et `VideoSpriteComponent` ne changent pas (O3). |
| D6 | Mode AUTO pour l'exécution, après approbation du plan. Ni push ni fusion dans `main` sans demande. |
| D7 | **La boîte englobante d'une entité compte ses composants enfants qui se dessinent**, et celle d'une caméra suit sa position. Pas d'attribut « toujours visible ». Les composants physiques restent exclus, comme aujourd'hui ; les entités enfants ne sont pas traitées dans ce plan (O6). |

## Conception

- **Dessin.** `StaticSpriteComponent.Submit` passe au rendu le quad local du sprite (position nulle, rotation nulle, échelle 1, z nul) et `WorldMatrixWithScale` comme transformation de monde. Le rendu applique déjà cette transformation après la mise à l'échelle de la source et le placement de l'origine.
- **Équivalence pour l'existant.** Pour un sprite sans rotation et sans parent, ou sous des parents sans rotation ni échelle, les quatre coins du quad transformés par la matrice de l'entrée sont ceux d'aujourd'hui **à l'arrondi près en x et y, et exactement en z**. La matrice elle-même n'est pas identique au bit près : l'ordre des opérations change (aujourd'hui `(x − ox·sx) + (w ÷ 2)·sx`, demain `((0 − ox) + w ÷ 2)·sx + x`), et son terme d'échelle en z vaut l'échelle z du composant au lieu de 1, sans effet sur un quad de z local nul. Le z de la translation reste exactement `Position.Z` : le tri ne change pas. C'est le cas de tous les sprites des données du dépôt.
- **Règle de comparaison**, la même dans T0.2, dans les arrêts et dans la vérification : sur x et y, un écart d'au plus `1e-4 × max(1, |valeur attendue|)` ; sur z, l'égalité exacte.
- **Visibilité (D7).** La règle de `TransformComponent` est appliquée à toute entité : `Entity.GetBoundingBox` étend la boîte du composant racine et de chaque composant d'entité par celles de leurs descendants qui se dessinent (`IComponentDrawable`), à toute profondeur, **en sautant tout sous-arbre `PhysicsBaseComponent`**. `World.IsBoundingBoxDirty` consulte les mêmes descendants, avec la même exclusion, pour savoir si une entité doit être réindexée. `CameraComponent.GetBoundingBox` rend sa boîte de −1 à 1 transformée par la matrice de monde de la caméra : **la boîte de toute entité caméra passe de l'origine du monde à la caméra, c'est voulu**.
- **Invariant de la visibilité.** Une entité **sans caméra** et sans descendant qui se dessine garde exactement sa boîte. Une entité dont la racine est un `TransformComponent` aussi : elle compte déjà ces descendants. Une autre entité avec des descendants qui se dessinent a une boîte plus grande ou égale : elle est visible plus souvent, jamais moins. Limite gardée et écrite : une caméra doit être le composant racine de son entité pour que ses enfants soient dessinés.
- **Ce qui change, voulu.** Un sprite tourné se voit tourné. Un sprite sous un parent tourné ou mis à l'échelle suit ce parent comme sa boîte englobante le fait déjà. T0.1 recense les sprites créés par code qui seraient dans ce cas ; s'il y en a, la tâche s'arrête et la liste va à l'auteur (O1).
- **Rendu.** Une surcharge publique de `DrawSprite` est ajoutée : celle d'aujourd'hui avec transformation de monde, plus le mode de mélange, la clé de tri optionnelle, l'ordre de dessin, le rectangle de découpe et le dessin de mise au point. Additive : aucune signature existante ne change.
- **Tri.** Inchangé : z de monde, lu dans la translation de la matrice. La clé d'un `DepthSortable2DComponent` reste calculée sur `Position`.
- **Ce que le plan ne change pas** : le shader, les états GPU, les collisions du sprite, sa boîte englobante, ses clés de données.
- **Sprite enfant d'une caméra.** Position locale (x, y, −d) : le plan z = −d du repère de la caméra, qui regarde le long de son −z local. Le quad du sprite est dans le plan xy local, face au +z local : il fait face à la caméra. À la distance d, une unité du monde couvre `hauteur du viewport ÷ (2 · d · tan(champ de vision ÷ 2))` pixels : l'échelle qui donne un pixel d'image par pixel du cadre s'en déduit.
- **Limites écrites dans l'ADR et la doc.** Un sprite vu de dos n'est pas dessiné (O2). Le tri des sprites semi-transparents se fait sur le z du monde : sous une caméra tournée, deux sprites mélangés enfants de la caméra peuvent être triés à l'envers ; les sprites opaques ou à découpe ne dépendent pas du tri (O4).

## Règles d'exécution pour l'agent

- **Branche dédiée `chantier/oriented-sprites`**, créée depuis `main` `51ea3857`. Ne jamais committer sur `main`.
- **Une seule tâche à la fois.** Avant de commencer une tâche, remplacer son icône `⏳` par `🚧`. À la fin, lancer la validation indiquée, remplacer l'icône par `✅`, `🧪` ou `⚠️`, ajouter une courte note de validation sous la tâche, puis **créer un commit dédié** qui inclut la mise à jour de ce fichier.
- **Un commit par tâche**, atomique et compilable, message en anglais au format `type(area): summary`.
- **Ne jamais pousser.** Le merge sur `main` reste une décision humaine.
- **Ne rien inventer** : toute API, tout fichier, toute règle utilisée existe dans le dépôt, vient d'une réponse de l'auteur, ou d'une doc officielle citée. Sinon : ⚠️ Blocked, question dans « Points ouverts », et **arrêt**.
- **Build obligatoire** avant ✅ dès que du code est touché (`dotnet build CasaEngine.MonoGame.sln` et `dotnet build CasaEngine.Editor.MonoGame.sln`) ; **tests** `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj`.
- **Ne jamais laisser une tâche en 🚧** à la fin d'une session. **Ne jamais indexer** les modifications préexistantes de l'auteur.
- **Langue** : ce plan en français ; code, messages de commit, docs de `docs/` et ADR en anglais.
- Rappel moteur : pas d'allocation, de LINQ ni de closure dans `Draw` et `Submit` ; restaurer tout état GPU modifié ; le runtime ne dépend pas de l'éditeur ; sérialisation additive.
- Un sous-agent qui écrit dans ce dépôt ne nomme aucun projet extérieur, dans aucun fichier ni message.

## Légende des statuts

- ⏳ Todo · 🚧 In progress · 🧪 Needs testing · ✅ Done · ⚠️ Blocked

## Enveloppe

| | |
|---|---|
| Résultat | `StaticSpriteComponent` est dessiné avec sa transformation de monde complète ; un sample le montre sous une caméra inclinée |
| Périmètre | `StaticSpriteComponent.Submit`, une surcharge de `SpriteRendererComponent.DrawSprite`, la boîte englobante d'une entité et d'une caméra (D7), un sample, deux ADR, la doc |
| Hors périmètre | `AnimatedSpriteComponent`, `VideoSpriteComponent`, le shader, le tri, les faces arrière, une aide au dimensionnement |
| Acceptation | Pour tout sprite sans rotation et sans parent tourné ni mis à l'échelle, les coins du quad transformés par la matrice de l'entrée du rendu sont ceux d'avant, selon la règle de comparaison de la conception, prouvé par un test écrit **avant** le changement et non modifié ensuite ; un sprite tourné ou enfant d'une caméra tournée a les coins que donne sa matrice de monde ; un sprite enfant d'une caméra posée de façon que l'origine du monde soit hors de son champ est rendu par la requête de visibilité du monde ; une entité sans caméra et sans descendant qui se dessine garde sa boîte ; un composant physique enfant ne change pas la boîte ; les deux solutions à 0 erreur ; `CasaEngine.Tests` vert |
| Retour arrière | Annuler les commits de la branche |
| Arrêts | Un coin qui diffère, au-delà de la règle de comparaison, pour un sprite sans rotation ; une entité sans caméra et sans descendant qui se dessine dont la boîte change ; un sprite du dépôt, créé par code, sous un parent tourné ou mis à l'échelle (O1) ; un test existant qui casse |
| Risque | **Élevé : changement de rendu de tous les sprites du monde.** Relecture indépendante du plan avant approbation ; vérification indépendante du résultat avant de dire le chantier fait |
| Budget | Un commit par tâche ; au plus cinq passes de correction après vérification |

## Validation globale

- `dotnet build CasaEngine.MonoGame.sln` et `dotnet build CasaEngine.Editor.MonoGame.sln` : 0 erreur.
- `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj` : 0 échec ; aucun test existant modifié.
- Le test d'équivalence de T0.2, vert du début à la fin, sans modification.
- Captures : les samples à sprites du dépôt (`SpriteDrawOrderSample`, `PixelUiSample`, `BufferedResolutionSample`, `PerspectiveCameraSample`, `RPGDemo`), lancés avant et après par le script de capture du dépôt hôte, donnent des images identiques octet pour octet aux instants où l'image est fixe. Indice, pas preuve.
- Recette de l'auteur : le nouveau sample, et les démos et samples à sprites de son choix.

---

## Phase 0 — Avant le changement

### ✅ T0.1 — Recenser les sprites créés par code sous un parent

- Objectif : savoir si un sprite du dépôt changerait d'aspect.
- Sources : `CasaEngine/`, `CasaEngine.Demos/`, `Projects/**/*.cs`, `CasaEngine.Editor/`, `CasaEngine.EditorServices/`.
- Étapes :
  1. Chercher chaque création ou usage d'un `StaticSpriteComponent` par code : `new StaticSpriteComponent`, `AddChildComponent`, `AddChild`, écriture de `LocalOrientation`, `Orientation`, `LocalScale` ou `Scale` sur un sprite ou sur un de ses parents.
  2. Pour chacun : le sprite a-t-il une rotation, ou un parent (composant ou entité) qui a une rotation ou une échelle différente de 1 ?
- Validation : la note de la tâche liste chaque cas trouvé, ou dit qu'il n'y en a aucun. Un cas trouvé : ⚠️ Blocked, O1.
- Commit : `docs(plan): record the survey of the sprites placed under a parent`
- **Fait (2026-11-21).** Plan approuvé par l'auteur le 2026-11-21 (« Approuvé, lance »), en mode AUTO. Recherche de `new StaticSpriteComponent`, `AddChildComponent` et `AddChild` dans le code hors tests : **un seul sprite statique créé par code sous un parent**, dans `CasaEngine.Demos/Demos/TopDownElevationDemo.cs:105-117`. Il a une échelle locale de 0,15, pas de rotation, et ses deux parents (un `RenderProjectionComponent`, sous un `CollisionComponent` racine) n'ont qu'une position : c'est le cas équivalent de la conception, pas un arrêt. Aucun sprite statique n'est placé sous un parent tourné ou mis à l'échelle. `Projects/CasaEngine.RPGDemo/Weapons/MeleeWeapon.cs:17` met une entité sous une autre, mais son sprite est un sprite animé, hors périmètre. O1 est sans objet.

### ✅ T0.2 — Figer les sommets des sprites d'aujourd'hui

Tests seulement.

- Objectif : une preuve d'équivalence écrite sur le code d'avant.
- Fichiers : `CasaEngine.Tests/Scene/StaticSpriteWorldTransformTests.cs` (nouveau).
- Étapes :
  1. Reprendre le montage sans carte graphique de `SpriteComponentDrawOrderTests` (`Harness`, lecture de la file `_spriteDatas` par réflexion), en le réutilisant ou en le recopiant. **Grandeur comparée** : pour chaque entrée de la file, les quatre coins locaux (±0,5 ; ±0,5 ; 0) transformés par `WorldMatrix` de l'entrée. `FillVertices` n'est pas la source : il ne transforme rien.
  2. Test d'équivalence : pour une grille de positions (z compris), d'échelles, d'origines de sprite et de rectangles source, sans rotation et sans parent, puis sous un parent composant et sous une entité parente sans rotation ni échelle, les quatre coins valent ce que donne la formule d'aujourd'hui, écrite dans le test dans l'ordre du code : centre ((x − ox·sx) + (w ÷ 2)·sx, (y + oy·sy) − (h ÷ 2)·sy, z), demi-tailles (w·sx ÷ 2, h·sy ÷ 2). Comparaison selon la règle de la conception : tolérance relative de 1e-4 sur x et y, égalité exacte sur z. La grille contient des positions non entières avec une origine non nulle et une échelle différente de 1, et une échelle négative.
  3. Chaque mode de soumission est couvert : opaque, mélangé, avec ordre de dessin, avec clé de tri.
  4. Contrôle que le test mord : une mutation volontaire et provisoire de `Submit` (l'échelle passée à 1) le fait échouer ; la mutation est retirée avant le commit et la note le dit.
- Validation : tests verts sur le code d'aujourd'hui ; la note donne le compte de cas et le résultat de la mutation.
- Commit : `test(sprite): record the vertices of the unrotated static sprite`
- **Fait (2026-11-21).** `StaticSpriteWorldTransformTests`, sur le montage recopié de `SpriteComponentDrawOrderTests`. Grandeur comparée : les quatre coins locaux transformés par `WorldMatrix` de l'entrée de la file. 14 lignes de théorie : trois placements (racine, composant enfant d'une racine inerte, entité enfant), quatre modes pour chacun (opaque, mélangé, avec ordre de dessin, les deux), plus la clé de tri en opaque et en mélangé pour le sprite racine. Grille par ligne : 3 sprites (rectangle source et origine) × 5 positions × 5 échelles, × 2 positions de parent pour les sprites enfants, soit 75 cas pour une racine et 150 pour un enfant ; 1650 cas en tout, dont des positions non entières avec origine non nulle et échelle différente de 1, et des échelles négatives. Règle de comparaison de la conception. Vert sur le code d'aujourd'hui. **Mutation** : l'échelle de `Submit` passée à 1, les 14 lignes échouent ; mutation retirée, fichier rétabli à l'identique.

---

## Phase 1 — Le sprite orientable

### ✅ T1.0 — La boîte d'une entité compte ses composants enfants qui se dessinent

- Objectif : qu'un composant enfant qui se dessine compte pour la visibilité de son entité, et que la boîte d'une caméra soit à la caméra (D7).
- Fichiers : `CasaEngine/Framework/Scene/Entities/Entity.cs` (`GetBoundingBox`), `CasaEngine/Framework/Scene/World/World.cs` (`IsBoundingBoxDirty`), `CasaEngine/Framework/Scene/Entities/Components/CameraComponent.cs` (`GetBoundingBox`), `CasaEngine/Framework/Scene/Entities/Components/TransformComponent.cs` seulement si la règle y est déplacée vers un endroit partagé sans en changer le résultat, `CasaEngine.Tests/Scene/EntityBoundsWithChildrenTests.cs` (nouveau).
- Étapes :
  1. Tests d'abord, sur le code d'avant : pour une grille d'entités **sans caméra** (composant racine de plusieurs types, avec et sans composants d'entité, sans descendant qui se dessine), la boîte est celle d'aujourd'hui ; de même pour une entité à racine `TransformComponent` avec des descendants qui se dessinent. Ces tests restent verts sans modification après l'étape 2.
  2. `Entity.GetBoundingBox` étend la boîte du composant racine et de chaque composant d'entité par celles de leurs descendants `IComponentDrawable`, à toute profondeur, en sautant tout sous-arbre `PhysicsBaseComponent` : la règle de `TransformComponent.AccumulateDrawableDescendantBounds`. `World.IsBoundingBoxDirty` consulte les mêmes descendants avec la même exclusion. Aucune allocation : boucles `for` sur les listes existantes.
  3. `CameraComponent.GetBoundingBox` rend la boîte de −1 à 1 transformée par `WorldMatrixNoScale`.
  4. Tests ajoutés : la boîte d'une entité contient celle d'un composant enfant éloigné qui se dessine ; **un composant physique enfant éloigné ne change pas la boîte** ; un composant enfant qui se dessine et qui bouge seul fait réindexer son entité ; par la requête de visibilité du monde (`World.Draw`, ou l'index et un frustum), une caméra racine posée de façon que l'origine du monde soit hors de son champ, avec un sprite enfant devant elle, est rendue ; la boîte d'une caméra sans enfant est à sa position (c'est le seul test sur la boîte d'une caméra).
- Validation : builds ; tests verts ; aucun test existant modifié, en particulier `TransformComponentTests.GetBoundingBox_AfterUpdate_FollowsTheRenderPositionAndExcludesPhysics` et `WorldIndex_ReturnsTheEntityAroundItsRenderPose_NotItsLogicalPose`. Si un test existant casse, arrêt et note.
- Commit : `fix(scene): count the children of an entity in its bounds and place the bounds of a camera on the camera`
- **Fait (2026-11-21).** `Entity.GetBoundingBox` étend la boîte du composant racine et de chaque composant d'entité par celles de leurs descendants `IComponentDrawable`, sous-arbres `PhysicsBaseComponent` sautés (`SceneComponent.ExpandByDrawableDescendants`, interne). `World.IsBoundingBoxDirty` consulte les mêmes descendants (`SceneComponent.HasBoundingBoxDirtyDescendant`). `CameraComponent.GetBoundingBox` rend sa boîte transformée par `WorldMatrixNoScale`. `TransformComponent` n'est pas touché.
  - **Fait établi pendant la tâche, qui précise le plan** : `SceneComponent` implémente lui-même `IComponentDrawable` (`SceneComponent.cs:16`). « Les descendants qui se dessinent » sont donc **tous les descendants hors physique**, pour `TransformComponent` aujourd'hui comme pour cette règle. La règle codée est celle du plan ; sa portée est plus large que le mot ne le laisse entendre : toute entité qui a un composant enfant hors physique a une boîte plus grande ou égale. L'invariant vérifié est donc « une entité sans caméra et sans descendant hors physique garde sa boîte ».
  - Tests, `EntityBoundsWithChildrenTests` : 13 invariants (trois types de racine, avec et sans composant d'entité, avec et sans enfant physique éloigné) et celui de la racine inerte, verts avant et après ; puis la boîte contient un enfant éloigné (sous la racine, sous un composant d'entité), un enfant physique éloigné et ce qui pend dessous ne la changent pas, la boîte d'une caméra est à la caméra, et par l'index du monde : l'enfant d'une caméra racine posée en (500, 0, 300) est rendu autour de (500, 0, 250), suit la caméra quand elle bouge, et un enfant qui bouge seul fait réindexer son entité. Avant la correction, les cinq tests du nouveau comportement échouaient ; une mutation (l'extension par les descendants de la racine retirée) en fait échouer trois.
  - Les deux solutions à 0 erreur ; 8506 tests, 0 échec (8487 + 19) ; aucun test existant modifié, dont les deux de `TransformComponentTests` nommés.

### ✅ T1.1 — ADR

- Objectif : enregistrer les deux décisions.
- Fichiers : `docs/decisions/0151-a-sprite-is-drawn-with-the-world-transform-of-its-component.md` et `docs/decisions/0152-the-bounds-of-an-entity-include-its-children.md` (nouveaux), `docs/decisions/README.md`. Numéros à confirmer sur l'index au moment de les écrire. Le second ADR (D7) dit ce que comptait la boîte avant, ce qu'elle compte maintenant (les descendants qui se dessinent, composants physiques exclus, comme `TransformComponent`), que la boîte d'une caméra suit la caméra, la limite de la caméra non racine, et que les entités enfants ne sont pas traitées.
- Étapes : avec le skill `adr`, en anglais : le contexte (rotation ignorée, boîte englobante qui la suit déjà, surcharge du rendu existante) ; la décision (D1, D2, D4, D5) ; les conséquences et les limites (faces arrière, tri des sprites mélangés, sprite animé et sprite vidéo inchangés, dimensionnement par les données) ; ce qui change pour un sprite sous un parent tourné ou mis à l'échelle.
- Validation : relecture ; aucun nom de projet extérieur.
- Commit : `docs(adr): record that a sprite is drawn with the world transform of its component`
- **Fait (2026-11-21).** ADR-0151 (le sprite dessiné avec la transformation de monde de son composant) et ADR-0152 (la boîte d'une entité compte ses composants enfants, celle d'une caméra suit la caméra), écrits et indexés ; les numéros 0151 et 0152 étaient libres. L'ADR-0152 dit que « les descendants qui se dessinent » sont tous les descendants hors physique. Documentation seule.

### ✅ T1.2 — Surcharge du rendu

- Objectif : pouvoir soumettre un sprite avec une transformation de monde et tous ses réglages.
- Fichiers : `CasaEngine/Framework/Application/Components/SpriteRendererComponent.cs`, `CasaEngine.Tests/` (tests de la surcharge).
- Étapes :
  1. Ajouter une surcharge publique `DrawSprite(Sprite sprite, in Matrix worldTransform, Color color, SpriteBlendMode blendMode, int drawOrder, Rectangle scissorRectangle, bool drawDebug)` et sa jumelle avec `in RenderSortKey2D sortKey`. Elles appellent la méthode privée existante avec une position nulle, une rotation nulle, une échelle de 1 et un z nul. Le dessin des formes de collision de mise au point garde son comportement actuel pour les autres surcharges ; pour celles-ci, il est fait à la position de la translation de la matrice.
  2. Tests : la matrice gardée par le rendu égale `échelle de la source × placement de l'origine × transformation de monde` ; le mode de mélange, l'ordre de dessin et la clé de tri arrivent dans l'entrée.
- Validation : builds ; tests verts ; aucun appel existant modifié.
- Commit : `feat(sprite): let a sprite be submitted with a world transform and its settings`
- **Fait (2026-11-21).** Deux surcharges publiques de `SpriteRendererComponent.DrawSprite`, placées par une transformation de monde seule : `(Sprite, in Matrix, Color, SpriteBlendMode, int drawOrder, Rectangle, bool drawDebug)` et sa jumelle avec `in RenderSortKey2D`. Elles appellent la méthode privée existante avec une position nulle, une rotation nulle, une échelle de 1 et un z nul. Dessin de mise au point : sous une transformation de monde, la taille des formes est celle que donnent les axes x et y de la transformation (longueur des axes, donc sans le signe d'une échelle négative), et les formes de collision sont à la translation de la transformation ; les appelants existants de la surcharge à transformation de monde passent `drawDebug: false`, rien ne change pour eux. Aucune signature existante modifiée. Tests : la matrice gardée par le rendu égale `échelle de la source × placement de l'origine × transformation de monde` ; couleur, mélange, ordre de dessin, découpe et clé de tri arrivent dans l'entrée. Les deux solutions à 0 erreur ; les 16 tests de `StaticSpriteWorldTransformTests` verts.

### ✅ T1.3 — `StaticSpriteComponent` dessine avec sa transformation de monde

- Objectif : le changement lui-même.
- Fichiers : `CasaEngine/Framework/Scene/Entities/Components/StaticSpriteComponent.cs`, `CasaEngine.Tests/Scene/StaticSpriteWorldTransformTests.cs`.
- Étapes :
  1. `Submit` appelle les surcharges de T1.2 avec `WorldMatrixWithScale`, dans chacun de ses quatre cas (clé de tri ou non, mélange ou non, ordre de dessin ou non). La clé de tri reste calculée sur `Position`.
  2. Tests ajoutés, sur la grandeur de T0.2 (coins transformés par la matrice de l'entrée) : un sprite tourné autour de z, de x et de y a les coins de sa matrice de monde ; un sprite enfant composant d'une `PerspectiveCameraComponent` inclinée avec du roulis, en (0, 0, −d), a ses quatre coins dans le plan z = −d du repère de la caméra et ses bords parallèles aux axes x et y de la caméra ; à l'échelle `2 · d · tan(champ ÷ 2) ÷ hauteur du viewport`, une image de la hauteur du viewport couvre exactement la hauteur du cadre (projection des coins par la vue et la projection de la caméra) ; avec un décalage de l'image, les coins projetés se déplacent du décalage en pixels ; un sprite sous une entité parente tournée suit la rotation ; un sprite enfant d'une caméra racine posée de façon que l'origine du monde soit hors de son champ arrive dans la file du rendu en passant par `World.Draw`.
  3. Test : `Submit` n'alloue pas (mesure par `GC.GetAllocatedBytesForCurrentThread`, comme `OrbitCameraComponentTests`).
- Validation : le test de T0.2 vert **sans modification** ; builds ; tests verts ; aucun test existant modifié.
- Commit : `feat(sprite): draw the static sprite with the world transform of its component`
- **Fait (2026-11-21).** `StaticSpriteComponent.Submit` passe `WorldMatrixWithScale` aux surcharges de T1.2 ; ses six branches deviennent deux (avec clé de tri, sans). La clé de tri reste calculée sur `Position`.
  - **Le test d'équivalence de T0.2 est vert après le changement** : sa théorie, sa formule et sa règle de comparaison ne sont pas modifiées. Le montage du fichier a reçu de quoi poser une caméra (politique d'éditeur et taille d'écran du jeu de test) et des orientations optionnelles ; ces ajouts ne touchent pas les cas d'équivalence.
  - Tests ajoutés (10) : un sprite tourné autour de z, de x, de y et d'un axe quelconque a les coins de son orientation, l'attendu étant écrit sans les matrices du moteur (échelle, rotation, position) ; un sprite sous une entité parente tournée d'un quart de tour suit la rotation ; un sprite enfant d'une caméra à lacet, tangage et roulis est dans le plan z = −200 du repère de la caméra, ses bords le long des axes x et y de la caméra, et il n'est pas vu de dos ; à l'échelle `2 · d · tan(champ ÷ 2) ÷ 720`, une image de 640 × 720 couvre les pixels (320 à 960, 0 à 720) d'un cadre de 1280 × 720, à 0,05 pixel près ; un décalage de l'image de (0,25 ; −0,1) déplace les coins projetés de (+320, −72) pixels ; un sprite enfant d'une caméra posée en (5000, 800, 3000), dont le champ ne contient pas l'origine du monde, est rendu par l'index pour le frustum de cette caméra, puis mis en file ; `Submit` n'alloue rien sur 200 appels.
  - **Écart au texte du plan** : le dernier test passe par l'index spatial et par `Submit`, pas par `World.Draw` : `StaticSpriteComponent.Draw` lit le rectangle de découpe du périphérique graphique, absent de ce montage. `World.Draw` est exercé avec un périphérique en T1.4.
  - Mise au point : sous une échelle négative, les formes de mise au point d'un sprite prennent la valeur absolue de l'échelle (T1.2) ; le dessin du sprite, lui, garde le signe.
  - Les deux solutions à 0 erreur ; 8518 tests, 0 échec (8508 + 10) ; aucun test existant modifié.

### ✅ T1.4 — Preuve à l'image

- Objectif : voir le résultat sur une carte graphique, sans fenêtre.
- Fichiers : un test GPU dans `CasaEngine.Tests/Rendering/` (nouveau), sur le montage des tests GPU existants (`GpuDeviceHost`).
- Étapes : en passant par le dessin du monde si le montage le permet (sinon la note le dit, la visibilité étant déjà prouvée en T1.0 et T1.3), rendre dans une cible une caméra perspective inclinée avec du roulis et deux sprites enfants à deux distances, l'un avec une découpe transparente, plus un objet opaque entre les deux ; lire les pixels : le sprite proche couvre l'objet, l'objet couvre le sprite lointain, le sprite lointain se voit par la découpe du sprite proche ; un décalage de l'image déplace le tout du même nombre de pixels.
- Validation : test vert. Si le montage GPU ne permet pas un objet opaque dans la même image, la note le dit et le test se limite aux deux sprites et à la découpe.
- Commit : `test(sprite): render sprites that follow a tilted camera and read the pixels back`
- **Fait (2026-11-21).** `OrientedSpriteGpuTests`, deux tests exécutés sur la carte graphique (non ignorés), sur le montage GPU existant (`WorldVideoHarness`, `GpuDeviceHost`). Une caméra à lacet, tangage et roulis, posée en (5000, 800, 3000), dont le champ ne contient pas l'origine du monde ; deux images enfants de la caméra, deux fois plus grandes que le cadre, à 200 et à 100 unités, la proche avec une découpe transparente au centre ; entre les deux, à 150 unités, un sprite du monde qui n'est pas enfant de la caméra, posé par sa propre transformation et tourné comme elle. **Le dessin passe par `World.Draw`** : les deux entités sont dans l'index, la requête de visibilité les rend, trois entrées arrivent dans la file. Pixels relus dans une cible de 64 × 64 : l'image proche hors de sa découpe et devant l'objet, l'image lointaine par la découpe, l'objet par la découpe devant l'image lointaine. Avec un décalage de l'image de (0,25 ; 0), images et objet se déplacent ensemble de 16 pixels et l'image proche couvre encore le bord gauche. L'« objet opaque » est un sprite, pas un maillage : le montage ne rend que les sprites.

---

## Phase 2 — Sample et documentation

### ✅ T2.1 — Sample

- Objectif : montrer la fonctionnalité dans le Launcher.
- Fichiers : `Projects/OrientedSpriteSample/` (fichier de projet, `AssetInfos.json`, un monde, `Images/`, `Models/`, `README.md`) ; le script de clavier existant `PictureOffsetKeys` de `Projects/CasaEngine.PerspectiveCameraSample/` est réutilisé si sa DLL peut être copiée dans le dossier du nouveau sample par une seconde ligne de son `PostBuild`, sinon une DLL de jeu minimale est ajoutée à `CasaEngine.MonoGame.sln` ; `.gitignore` pour les binaires copiés ; un générateur d'images et un test de données dans `CasaEngine.Tests/`, sur le modèle de `PerspectiveCameraSampleImages` et `PerspectiveCameraSampleDataTests`.
- Étapes :
  1. Le monde : une caméra perspective inclinée avec du roulis, composant racine de son entité, **posée de façon que l'origine du monde soit hors de son champ** ; deux sprites enfants de la caméra, plus larges et plus hauts que le cadre, l'un lointain, l'autre proche avec une découpe ; une boîte posée dans le monde entre les deux distances ; un sprite tourné posé dans le monde, sans lien avec la caméra, pour montrer le cas général.
  2. Les flèches décalent l'image de la caméra.
  3. README : objectif, contrôles, comment lancer, comment l'échelle des sprites a été calculée.
  4. Test de données : les images committées sont celles du générateur ; les échelles enregistrées sont celles de la formule pour le champ de vision et la hauteur du monde ; chaque référence se résout dans le catalogue du sample.
- Validation : builds ; tests ; le sample lancé par le script de capture du dépôt hôte, une capture par décalage (au moins deux), jointes au rapport. 🧪 jusqu'à la recette de l'auteur.
- Commit : `feat(samples): add a sample of sprites that follow a tilted camera`
- **Fait (2026-11-21), 🧪 jusqu'à la recette.** Construit par un `executor`, relu par la session principale. `Projects/OrientedSpriteSample/` : une caméra racine de son entité, champ de 45 degrés, posée en (800, 300, 650) avec lacet −120°, tangage −20° et roulis 25°, l'origine du monde à 142,7° de son axe de visée ; deux sprites enfants de la caméra, de 960 × 720 pour un cadre de 640 × 480, en (0, 0, −600) à l'échelle 1,035534 et en (0, 0, −240) à l'échelle 0,4142136, le proche avec une découpe ; une boîte de 60 unités à 400 unités sur l'axe de visée ; un sprite tourné posé dans le monde, vu de trois quarts. Le script `PictureOffsetKeys` est réutilisé : le `PostBuild` de `Projects/CasaEngine.PerspectiveCameraSample` copie aussi sa DLL dans le nouveau dossier, ignorée par git. Images produites par un générateur ; 13 tests de données (`OrientedSpriteSampleDataTests`). Un identifiant du catalogue a été remplacé après coup par la session principale.
  - **Vu à l'écran**, par le script de capture du dépôt hôte, sans entrée clavier : à décalage nul, les deux images sont droites dans le cadre, la boîte et le sprite tourné se voient par la découpe, devant l'image lointaine. Une copie du sample hors dépôt, avec un décalage de l'image de (0,2 ; −0,15) écrit dans le monde : tout a glissé ensemble de (+128, −72) pixels. Aucune erreur au journal.
  - **Non vu** : l'effet des flèches (le script de capture n'envoie aucune entrée). Recette de l'auteur.
  - Les deux solutions à 0 erreur ; 8533 tests, 0 échec (8520 + 13).
- **Recette de l'auteur : OK (2026-11-21)**, sur captures du Launcher : le sample sans décalage et avec un décalage de (0,2 ; −0,15). Les flèches du sample n'ont pas été essayées : aucune entrée n'est envoyée aux fenêtres.

### ✅ T2.2 — Documentation

- Fichiers : `docs/engine/` (un document court sur le sprite orienté : ce qu'il suit, comment l'attacher à une caméra, la formule d'échelle, les limites), `docs/engine/perspective-camera-component.md` et `Projects/PerspectiveCameraSample/README.md` là où ils disent que les sprites restent parallèles à XY, `docs/README.md`, `ai-agent/README.md`.
- Validation : relecture ; liens vérifiés.
- Commit : `docs(sprite): describe the sprite drawn with its world transform`
- **Fait (2026-11-21).** Nouveau `docs/engine/oriented-sprites.md` (ce que suit un sprite, l'attache à une caméra par la hiérarchie, la formule d'échelle au pixel, la visibilité, les limites, le sample et les tests). `perspective-camera-component.md` et le README de `PerspectiveCameraSample` ne disent plus que les sprites restent parallèles à XY. `docs/README.md` et `ai-agent/README.md` à jour. Documentation seule.

### ✅ T2.3 — Vérification indépendante et recette

- Un `verifier` neuf reçoit les affirmations de la « Validation globale » et de l'« Acceptation », le diff de la branche et les notes des tâches ; il relance builds et tests et cherche à réfuter. Un point P0 à P2 est corrigé puis revérifié.
- Captures avant et après des samples à sprites, comparées octet pour octet et, si elles diffèrent, pixel par pixel avec une seconde passe sur le même moteur pour mesurer la variation d'un lancement à l'autre.
- Recette de l'auteur (🧪 jusque-là).
- Commit : `docs(plan): record the verification of the oriented sprites`
- **Vérification indépendante (2026-11-21, `8b8caa53`) : CONFIRMED**, aucun point P0 à P2. Les neuf affirmations vérifiées : rien ne change pour un sprite existant (théorie, formule et règle de comparaison intactes depuis `7f0f77be` ; formule recoupée avec l'ancien code ; 20 sprites existants sans rotation ni parent composant) ; le sprite orienté ; la visibilité ; chemins chauds sans allocation (sondes à 0 octet sur 1000 appels) ; API additive ; deux tests exécutés sur la carte graphique, par `World.Draw` ; sample cohérent, recalculé à part ; builds à 0 erreur, 8533 tests verts, cinq fichiers de test tous ajoutés ; ADR et doc.
- **Captures avant et après** (2026-11-21), par le script de capture du dépôt hôte, sur `main` `51ea3857` puis sur cette branche : `SampleProject`, `PerspectiveCameraSample`, `SpriteDrawOrderSample`, `PixelUiSample`, `BufferedResolutionSample`, `RPGDemo` identiques octet pour octet à 3 s et à 8 s ; `TimedScreenSample` et `SpriteCutsceneSample` identiques à 8 s et différents à 3 s, pendant une animation (la recette précédente avait mesuré le même écart entre deux lancements d'un même moteur).
- **Remarques reportées sans changement du candidat** (P3 et P4), à trancher par l'auteur :
  - P3 : **l'exclusion de la physique ne vaut pas pour un composant racine physique.** `ExpandByDrawableDescendants` saute les enfants physiques, pas le composant d'où il part : sous une racine `CollisionComponent`, ce qui pend dessous est compté. La boîte grandit, donc l'entité est visible plus souvent, jamais moins ; c'est ce qu'il faut pour un visuel posé sous une racine logique. L'ADR-0152 et la doc disent « tout composant physique et ce qui pend dessous » : la formulation est à préciser.
  - P3 : **un sprite peut recevoir une rotation de la physique**, cas absent du recensement T0.1. Un corps dynamique écrit chaque image l'orientation de son parent (`PhysicsBaseComponent.cs:162-170`) ; dans `Projects/SpriteCutsceneSample/LiveWorld.world`, l'entité `FallingBox` a un sprite statique pour racine et un corps dynamique pour enfant. Avant, cette rotation était ignorée au dessin ; maintenant le sprite la suit et, s'il bascule, peut être vu de dos. Non simulé : l'effet n'est pas mesuré. L'ADR-0151 et la doc ne le disent pas.
  - P3 : le calcul de la boîte est fait deux fois pour une racine `StaticModelComponent` ou `TransformComponent`, qui comptent déjà leurs descendants ; coût non mesuré.
  - P4 : un `ChildActorComponent` sans acteur, posé en enfant, tire la boîte jusqu'à l'origine (composant utilisé nulle part) ; la mise au point d'un sprite en miroir est placée autrement qu'avant (drapeaux activés nulle part).
- **Restait 🧪 : la recette de l'auteur** (`Projects/OrientedSpriteSample`, flèches comprises ; les démos et samples à sprites de son choix). Ni l'éditeur ni `CasaEngine.Demos` n'ont été lancés.
- **Précisions de l'auteur demandées le 2026-11-21 (« Oui, précise »)**, documentation seule : l'ADR-0151 et `docs/engine/oriented-sprites.md` disent qu'un sprite porté par un corps dynamique suit sa rotation (dans un espace de simulation plan, le corps ne tourne qu'autour de z et le sprite tourne dans son plan ; ailleurs il peut basculer et être vu de dos ; un zéro dans `angular_factor` verrouille l'axe) ; l'ADR-0152 et la doc disent que l'exclusion de la physique porte sur les descendants, pas sur le composant de départ, et qu'une racine qui compte déjà ses descendants les voit comptés deux fois. Faits relus dans `PhysicsBaseComponent.cs:158-170`, `SimulationSpacePolicy.cs:129-142` et `BepuPhysicsEngine.cs:237`. L'effet sur l'entité concernée du dépôt n'est toujours pas mesuré.
- **Recette de l'auteur : OK (2026-11-21)**, sur sept captures du Launcher prises sur `d7b952ad` : `OrientedSpriteSample` avec et sans décalage, `SpriteCutsceneSample` sur son monde de départ, et `LiveWorld.world` à 2, 4 et 7 s depuis une copie de travail dont le monde de départ est changé. La boîte de `LiveWorld.world` tombe et se pose, son cadre de mise au point droit ; son image est un disque, une rotation dans son plan ne s'y verrait pas : l'effet de la physique sur ce sprite reste non mesuré. Non vérifié : les flèches du sample, l'éditeur, `CasaEngine.Demos`.

---

## Points ouverts

| Réf | Sujet | Tâche concernée |
|---|---|---|
| O1 | Si T0.1 trouve un sprite créé par code sous un parent tourné ou mis à l'échelle, son aspect changerait. Proposition : arrêt, liste à l'auteur, qui décide cas par cas | T0.1 |
| O2 | Un sprite vu de dos n'est pas dessiné (faces arrière éliminées). Proposition : ne pas changer dans ce plan ; le dire dans l'ADR et la doc. Un sprite à deux faces serait un réglage à part | T1.1, T2.2 |
| O3 | `AnimatedSpriteComponent` a des parties, une rotation par partie et un chemin de quad libre ; `VideoSpriteComponent` ignore la profondeur. Proposition : hors de ce plan ; les aligner demande son propre recensement | — |
| O4 | Tri des sprites semi-transparents sur le z du monde : faux sous une caméra tournée pour des sprites mélangés enfants de la caméra. Proposition : hors de ce plan ; le dire dans l'ADR et la doc | T1.1, T2.2 |
| O6 | Une entité enfant est dessinée par son parent et n'est pas dans l'index : sa boîte ne compte pas pour la visibilité du parent. Proposition : hors de ce plan, qui n'a besoin que des composants enfants ; à traiter à part si un besoin apparaît | — |
| O5 | Si le champ de vision change en jeu, l'échelle calculée par les données ne suit pas (D4). Proposition : accepté ; une aide dans le moteur viendra si le besoin se confirme | — |

## Hors périmètre

- Un composant « image attachée à la caméra » qui se dimensionne seul (D1, D4).
- `AnimatedSpriteComponent` et `VideoSpriteComponent` (O3).
- Le shader des sprites, le tri par profondeur de vue, les sprites à deux faces.
- Un mode « toujours face à la caméra » (billboard) pour les sprites.
- Tout changement de `SceneComponent.Position`, `Orientation` ou `Scale`.
- Un attribut « toujours visible », et une visibilité testée objet par objet plutôt que par entité (D7 corrige la boîte, pas le modèle).
- La boîte des entités enfants dans celle de leur parent (O6), et celle des composants physiques, qui restent exclus de la boîte de rendu.
- Faire dessiner les enfants d'un composant de scène qui n'est ni racine ni `PrimitiveComponent`.
