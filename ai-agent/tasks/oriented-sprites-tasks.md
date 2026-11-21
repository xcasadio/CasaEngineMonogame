# Plan agent IA — Sprites orientés par leur transformation

Les décisions D1 → D6 ci-dessous ont été arbitrées avec l'auteur le 2026-11-21 : **ce plan les applique, il ne les rediscute pas**.

Ce fichier doit être mis à jour pendant le travail : l'icône au début de chaque tâche indique son statut courant.

**Soumis à l'auteur : aucun code avant son approbation.**

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
- Faces : `CullCounterClockwise`, sauf pour une entrée marquée `NoCull`, que seul le chemin des quads libres pose (`:250`, `:293-296`, `:868`, `:1164`). Un sprite vu de dos n'est pas dessiné.

### Les données

- 20 sprites (`StaticSpriteComponent`, `AnimatedSpriteComponent`, `VideoSpriteComponent`) dans 12 fichiers de `Projects/` et de `CasaEngine.Demos/Content/`. **Aucun n'a de rotation.** Aucun n'est l'enfant d'un autre composant ; quatre ont pour enfant un `CollisionComponent`. Relevé par `jq` sur chaque fichier.
- Un composant enfant se déclare dans `children_component` de son parent (`SceneComponent.cs:416-444`) ; `SceneComponent.Draw` descend dans les enfants (`:388-394`). `WorldMatrixWithScale` compose le parent composant, puis le composant racine de l'entité parente.

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

## Conception

- **Dessin.** `StaticSpriteComponent.Submit` passe au rendu le quad local du sprite (position nulle, rotation nulle, échelle 1, z nul) et `WorldMatrixWithScale` comme transformation de monde. Le rendu applique déjà cette transformation après la mise à l'échelle de la source et le placement de l'origine.
- **Équivalence pour l'existant.** Pour un sprite sans rotation et sans parent, ou sous des parents sans rotation ni échelle, la matrice obtenue est celle d'aujourd'hui : même échelle, même translation, même z. C'est le cas de tous les sprites des données du dépôt.
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
| Périmètre | `StaticSpriteComponent.Submit`, une surcharge de `SpriteRendererComponent.DrawSprite`, un sample, un ADR, la doc |
| Hors périmètre | `AnimatedSpriteComponent`, `VideoSpriteComponent`, le shader, le tri, les faces arrière, une aide au dimensionnement |
| Acceptation | Pour tout sprite sans rotation et sans parent tourné ni mis à l'échelle, les sommets soumis au rendu sont ceux d'avant, prouvé par un test écrit **avant** le changement et non modifié ensuite ; un sprite tourné ou enfant d'une caméra tournée a les sommets que donne sa matrice de monde ; les deux solutions à 0 erreur ; `CasaEngine.Tests` vert |
| Retour arrière | Annuler les commits de la branche |
| Arrêts | Un sommet qui diffère pour un sprite sans rotation ; un sprite du dépôt, créé par code, sous un parent tourné ou mis à l'échelle (O1) ; un test existant qui casse |
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

### ⏳ T0.1 — Recenser les sprites créés par code sous un parent

- Objectif : savoir si un sprite du dépôt changerait d'aspect.
- Sources : `CasaEngine/`, `CasaEngine.Demos/`, `Projects/**/*.cs`, `CasaEngine.Editor/`, `CasaEngine.EditorServices/`.
- Étapes :
  1. Chercher chaque création ou usage d'un `StaticSpriteComponent` par code : `new StaticSpriteComponent`, `AddChildComponent`, `AddChild`, écriture de `LocalOrientation`, `Orientation`, `LocalScale` ou `Scale` sur un sprite ou sur un de ses parents.
  2. Pour chacun : le sprite a-t-il une rotation, ou un parent (composant ou entité) qui a une rotation ou une échelle différente de 1 ?
- Validation : la note de la tâche liste chaque cas trouvé, ou dit qu'il n'y en a aucun. Un cas trouvé : ⚠️ Blocked, O1.
- Commit : `docs(plan): record the survey of the sprites placed under a parent`

### ⏳ T0.2 — Figer les sommets des sprites d'aujourd'hui

Tests seulement.

- Objectif : une preuve d'équivalence écrite sur le code d'avant.
- Fichiers : `CasaEngine.Tests/Scene/StaticSpriteWorldTransformTests.cs` (nouveau).
- Étapes :
  1. Écrire un montage sans carte graphique qui soumet un `StaticSpriteComponent` et lit les sommets que le rendu calcule (`SpriteRendererComponent.FillVertices`, interne, visible des tests), d'après les tests existants de `CasaEngine.Tests/Scene/SpriteComponentDrawOrderTests.cs`. Si ce montage n'existe pas pour `StaticSpriteComponent`, le dire dans la note et s'appuyer sur ce que ces tests font.
  2. Test d'équivalence : pour une grille de positions (z compris), d'échelles, d'origines de sprite et de rectangles source, sans rotation et sans parent, puis sous un parent composant et sous une entité parente sans rotation ni échelle, les quatre sommets valent ce que donne la formule d'aujourd'hui, écrite dans le test : centre (x − ox·sx + w·sx ÷ 2, y + oy·sy − h·sy ÷ 2, z), demi-tailles (w·sx ÷ 2, h·sy ÷ 2).
  3. Chaque mode de soumission est couvert : opaque, mélangé, avec ordre de dessin, avec clé de tri.
- Validation : tests verts sur le code d'aujourd'hui ; la note donne le compte de cas.
- Commit : `test(sprite): record the vertices of the unrotated static sprite`

---

## Phase 1 — Le sprite orientable

### ⏳ T1.1 — ADR

- Objectif : enregistrer la décision.
- Fichiers : `docs/decisions/0151-a-sprite-is-drawn-with-the-world-transform-of-its-component.md` (nouveau), `docs/decisions/README.md`. Numéro à confirmer sur l'index au moment de l'écrire.
- Étapes : avec le skill `adr`, en anglais : le contexte (rotation ignorée, boîte englobante qui la suit déjà, surcharge du rendu existante) ; la décision (D1, D2, D4, D5) ; les conséquences et les limites (faces arrière, tri des sprites mélangés, sprite animé et sprite vidéo inchangés, dimensionnement par les données) ; ce qui change pour un sprite sous un parent tourné ou mis à l'échelle.
- Validation : relecture ; aucun nom de projet extérieur.
- Commit : `docs(adr): record that a sprite is drawn with the world transform of its component`

### ⏳ T1.2 — Surcharge du rendu

- Objectif : pouvoir soumettre un sprite avec une transformation de monde et tous ses réglages.
- Fichiers : `CasaEngine/Framework/Application/Components/SpriteRendererComponent.cs`, `CasaEngine.Tests/` (tests de la surcharge).
- Étapes :
  1. Ajouter une surcharge publique `DrawSprite(Sprite sprite, in Matrix worldTransform, Color color, SpriteBlendMode blendMode, int drawOrder, Rectangle scissorRectangle, bool drawDebug)` et sa jumelle avec `in RenderSortKey2D sortKey`. Elles appellent la méthode privée existante avec une position nulle, une rotation nulle, une échelle de 1 et un z nul. Le dessin des formes de collision de mise au point garde son comportement actuel pour les autres surcharges ; pour celles-ci, il est fait à la position de la translation de la matrice.
  2. Tests : la matrice gardée par le rendu égale `échelle de la source × placement de l'origine × transformation de monde` ; le mode de mélange, l'ordre de dessin et la clé de tri arrivent dans l'entrée.
- Validation : builds ; tests verts ; aucun appel existant modifié.
- Commit : `feat(sprite): let a sprite be submitted with a world transform and its settings`

### ⏳ T1.3 — `StaticSpriteComponent` dessine avec sa transformation de monde

- Objectif : le changement lui-même.
- Fichiers : `CasaEngine/Framework/Scene/Entities/Components/StaticSpriteComponent.cs`, `CasaEngine.Tests/Scene/StaticSpriteWorldTransformTests.cs`.
- Étapes :
  1. `Submit` appelle les surcharges de T1.2 avec `WorldMatrixWithScale`, dans chacun de ses quatre cas (clé de tri ou non, mélange ou non, ordre de dessin ou non). La clé de tri reste calculée sur `Position`.
  2. Tests ajoutés : un sprite tourné autour de z, de x et de y a les sommets de sa matrice de monde ; un sprite enfant composant d'une `PerspectiveCameraComponent` inclinée avec du roulis, en (0, 0, −d), a ses quatre sommets dans le plan z = −d du repère de la caméra et ses bords parallèles aux axes x et y de la caméra ; à l'échelle `2 · d · tan(champ ÷ 2) ÷ hauteur du viewport`, une image de la hauteur du viewport couvre exactement la hauteur du cadre (projection des sommets par la vue et la projection de la caméra) ; avec un décalage de l'image, les sommets projetés se déplacent du décalage en pixels ; un sprite sous une entité parente tournée suit la rotation.
  3. Test : `Submit` n'alloue pas (mesure par `GC.GetAllocatedBytesForCurrentThread`, comme `OrbitCameraComponentTests`).
- Validation : le test de T0.2 vert **sans modification** ; builds ; tests verts ; aucun test existant modifié.
- Commit : `feat(sprite): draw the static sprite with the world transform of its component`

### ⏳ T1.4 — Preuve à l'image

- Objectif : voir le résultat sur une carte graphique, sans fenêtre.
- Fichiers : un test GPU dans `CasaEngine.Tests/Rendering/` (nouveau), sur le montage des tests GPU existants (`GpuDeviceHost`).
- Étapes : rendre dans une cible une caméra perspective inclinée avec du roulis et deux sprites enfants à deux distances, l'un avec une découpe transparente, plus un objet opaque entre les deux ; lire les pixels : le sprite proche couvre l'objet, l'objet couvre le sprite lointain, le sprite lointain se voit par la découpe du sprite proche ; un décalage de l'image déplace le tout du même nombre de pixels.
- Validation : test vert. Si le montage GPU ne permet pas un objet opaque dans la même image, la note le dit et le test se limite aux deux sprites et à la découpe.
- Commit : `test(sprite): render sprites that follow a tilted camera and read the pixels back`

---

## Phase 2 — Sample et documentation

### ⏳ T2.1 — Sample

- Objectif : montrer la fonctionnalité dans le Launcher.
- Fichiers : `Projects/OrientedSpriteSample/` (fichier de projet, `AssetInfos.json`, un monde, `Images/`, `Models/`, `README.md`) ; le script de clavier existant `PictureOffsetKeys` de `Projects/CasaEngine.PerspectiveCameraSample/` est réutilisé si sa DLL peut être copiée dans le dossier du nouveau sample par une seconde ligne de son `PostBuild`, sinon une DLL de jeu minimale est ajoutée à `CasaEngine.MonoGame.sln` ; `.gitignore` pour les binaires copiés ; un générateur d'images et un test de données dans `CasaEngine.Tests/`, sur le modèle de `PerspectiveCameraSampleImages` et `PerspectiveCameraSampleDataTests`.
- Étapes :
  1. Le monde : une caméra perspective inclinée avec du roulis ; deux sprites enfants de la caméra, plus larges et plus hauts que le cadre, l'un lointain, l'autre proche avec une découpe ; une boîte posée dans le monde entre les deux distances ; un sprite tourné posé dans le monde, sans lien avec la caméra, pour montrer le cas général.
  2. Les flèches décalent l'image de la caméra.
  3. README : objectif, contrôles, comment lancer, comment l'échelle des sprites a été calculée.
  4. Test de données : les images committées sont celles du générateur ; les échelles enregistrées sont celles de la formule pour le champ de vision et la hauteur du monde ; chaque référence se résout dans le catalogue du sample.
- Validation : builds ; tests ; le sample lancé par le script de capture du dépôt hôte, une capture par décalage (au moins deux), jointes au rapport. 🧪 jusqu'à la recette de l'auteur.
- Commit : `feat(samples): add a sample of sprites that follow a tilted camera`

### ⏳ T2.2 — Documentation

- Fichiers : `docs/engine/` (un document court sur le sprite orienté : ce qu'il suit, comment l'attacher à une caméra, la formule d'échelle, les limites), `docs/engine/perspective-camera-component.md` et `Projects/PerspectiveCameraSample/README.md` là où ils disent que les sprites restent parallèles à XY, `docs/README.md`, `ai-agent/README.md`.
- Validation : relecture ; liens vérifiés.
- Commit : `docs(sprite): describe the sprite drawn with its world transform`

### ⏳ T2.3 — Vérification indépendante et recette

- Un `verifier` neuf reçoit les affirmations de la « Validation globale » et de l'« Acceptation », le diff de la branche et les notes des tâches ; il relance builds et tests et cherche à réfuter. Un point P0 à P2 est corrigé puis revérifié.
- Captures avant et après des samples à sprites, comparées octet pour octet et, si elles diffèrent, pixel par pixel avec une seconde passe sur le même moteur pour mesurer la variation d'un lancement à l'autre.
- Recette de l'auteur (🧪 jusque-là).
- Commit : `docs(plan): record the verification of the oriented sprites`

---

## Points ouverts

| Réf | Sujet | Tâche concernée |
|---|---|---|
| O1 | Si T0.1 trouve un sprite créé par code sous un parent tourné ou mis à l'échelle, son aspect changerait. Proposition : arrêt, liste à l'auteur, qui décide cas par cas | T0.1 |
| O2 | Un sprite vu de dos n'est pas dessiné (faces arrière éliminées). Proposition : ne pas changer dans ce plan ; le dire dans l'ADR et la doc. Un sprite à deux faces serait un réglage à part | T1.1, T2.2 |
| O3 | `AnimatedSpriteComponent` a des parties, une rotation par partie et un chemin de quad libre ; `VideoSpriteComponent` ignore la profondeur. Proposition : hors de ce plan ; les aligner demande son propre recensement | — |
| O4 | Tri des sprites semi-transparents sur le z du monde : faux sous une caméra tournée pour des sprites mélangés enfants de la caméra. Proposition : hors de ce plan ; le dire dans l'ADR et la doc | T1.1, T2.2 |
| O5 | Si le champ de vision change en jeu, l'échelle calculée par les données ne suit pas (D4). Proposition : accepté ; une aide dans le moteur viendra si le besoin se confirme | — |

## Hors périmètre

- Un composant « image attachée à la caméra » qui se dimensionne seul (D1, D4).
- `AnimatedSpriteComponent` et `VideoSpriteComponent` (O3).
- Le shader des sprites, le tri par profondeur de vue, les sprites à deux faces.
- Un mode « toujours face à la caméra » (billboard) pour les sprites.
- Tout changement de `SceneComponent.Position`, `Orientation` ou `Scale`.
