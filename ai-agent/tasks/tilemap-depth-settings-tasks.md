# Plan agent IA — Faire agir `TileMapDepthSettings` au rendu (étapes 4 et 5)

Chantier ouvert le 2026-09-07. Origine : découvert en creusant une étape du portage Alundra, mais
**c'est une dette du moteur**, utile à tous ses projets et sans rapport avec ce portage.

Les décisions D1 → D6 ci-dessous ont été arbitrées avec l'auteur le 2026-09-07 — « exécute le plan »,
lu comme l'approbation des recommandations portées par P1 à P5 : **ce plan les applique, il ne les
rediscute pas.**

Ce fichier doit être mis à jour pendant le travail : l'icône au début de chaque tâche indique son
statut courant.

**Révision 2 (2026-09-07)** — la révision 1 a été relue et a reçu **sept blocages, tous retenus** :
(1) D1 supposait qu'à Z égal la séquence de dessin ordonne ; faux pour les tuiles non statiques d'une
couche chunkée, mises en file et vidées après tous les lots — D1 garde donc `zOffset` comme séparateur
et renonce à consommer les champs fins pour les couches chunkées ; (2) « porte des métadonnées »
n'était défini nulle part et `TileMapDepthSettings` n'a pas d'indicateur de présence — D7 le définit
sur la clé brute ; (3) réordonner `Layers` aurait cassé l'adressage par index du portage — plus aucun
réordonnancement ; (4) la plage de culling dérivait du seul `zOffset` — T1.1 l'aligne ; (5)
`UsesDynamicSort` et « rôle `YSortedSource` » se recouvraient — D2 tranche, le tri explicite prime ;
(6) T2.1 oubliait les `TileCellFlags` et le chemin tourné — ajoutés ; (7) **la fixture d'acceptation
n'existait pas au chemin indiqué** — corrigé, `Projects/RPGDemo/…`.

**Révision 3 (2026-09-07) — relecture de clôture, trois blocages de plus, tous dispositionnés.**
(1) **CORRIGÉ** — `HasDepthMetadata` calculé au chargement aurait été **faux sur toute couche
rendue** : le composant dessine une copie de travail (`CreateWorldWorkingCopy`) qui n'appelle jamais
`Load` ; la tranche aurait été inerte en jeu avec des tests verts. Le drapeau traverse la copie et le
test de Z passe par elle. (2) **CORRIGÉ** — un second site de culling, `GetBoundingBox`, dérive l'étendue
Z des `zOffset` bruts et alimente l'index spatial du monde ; avec un pas ≥ 1 l'entité entière pouvait
être éliminée. T1.1 l'aligne. (3) **RÉTRÉCI** — T3.1 n'était pas exécutable : rien n'est invalide
aujourd'hui, aucun mécanisme d'échec n'était nommé, et le nom de couche n'était pas atteignable. Elle
ne livre plus que les avertissements, énumérés cas par cas ; la moitié « erreur » de D4 est reportée
avec sa raison. **C'est le seul point qui touche une décision verrouillée : à confirmer par l'auteur.**

**Plafond atteint** : deux verdicts REVISE consécutifs. Conformément à la règle, chaque blocage est
dispositionné ci-dessus et le plan **n'est pas resoumis** ; il passe à l'exécution sur T1.1 et T2.1,
qui ne dépendent pas du point à confirmer.

## Objectif

`CasaEngine/Framework/Assets/TileMap/TileMapDepthSettings.cs` analyse onze propriétés `depth.*` par
couche de tilemap, les valide, et les couvre de tests unitaires. **Le rendu n'en lit qu'une.** Ce
chantier fait agir les autres, c'est-à-dire réalise les **étapes 4 et 5** de la migration que le
moteur s'est écrite dans `docs/engine/tilemaps-gestion-profondeur.md`.

**Hors périmètre** : l'étape 6 (conversion des couches d'objets en entités), donc `SpawnAsEntity` et
`EmitsSortableObjects` restent morts ; l'étape 7 (élévations de ponts et escaliers) ; l'étape 8
(scène de démonstration complète).

## État vérifié du dépôt (2026-09-07)

Reconnaissance à cinq surfaces disjointes, puis **trois relecteurs adverses : les trois ont réfuté**,
et leurs corrections sont intégrées ci-dessous.

### Ce qui est mort, et c'est le cœur du problème

- **`ShouldRenderTiles` est le seul champ lu en production**, à exactement trois sites, tous comme
  garde binaire avant de dessiner une couche : `TileMapComponent.cs:428`, `:589`, `:1655`.
- **Tout le reste** — `Role`, `RenderPass`, `SortingLayer`, `OrderInLayer`, `Elevation`, `SortAnchor`,
  `LocalSortOffset`, `SortMode`, `SpawnAsEntity`, et les dérivés `KeepsStaticChunking`,
  `EmitsSortableObjects`, `UsesDynamicSort` — n'est référencé **que dans les tests**
  (`CasaEngine.Tests/TileMap/TileMapDataTests.cs:171-187`).
- Les couches sont dessinées **dans l'ordre du tableau**, avec pour seule profondeur
  `translation.Z + layer.zOffset` (`TileMapComponent.cs:425-435`).

### Ce qui existe déjà, et qu'il ne faut pas réécrire

- Les **étapes 1 à 3 sont réellement câblées** : `RenderSortKey2D` (7 champs, comparaison
  lexicographique, sans empaquetage), `DepthSortable2DComponent`, `RenderPass2D`. Consommés de bout en
  bout par `AnimatedSpriteComponent`, `StaticSpriteComponent`, et les composants récents de couches
  défilantes et cellulaires.
- `SpriteRendererComponent` accumule, trie par `RenderSortKey2D` et est vidé **une fois par vue** par
  le pipeline. Un premier lecteur en a conclu que l'étape 4 était déjà faite. **Réfuté, voir ci-dessous.**
- L'**overlay runtime de tuiles triées** (`AddSortedOverlayTile`) et le **mode de fusion par sprite**
  sont livrés, documentés et testés.

### Les trois réfutations, qui redéfinissent le chantier

1. **L'étape 4 n'est PAS déjà faite.** Le chemin par lequel les couches de tuiles dessinent
   réellement — `TryDrawStaticChunkBatch` → `SpriteRendererComponent.DrawStaticBatch`
   (`SpriteRendererComponent.cs:266-323`) — **n'entre jamais dans la liste triée** : il émet son
   propre `DrawIndexedPrimitives` immédiat. La file triée n'existe que pour les sprites.
2. **Et il n'existe AUCUN code qui convertit une `RenderSortKey2D` en un Z.** `RenderSortKey2D.cs` ne
   définit qu'une comparaison lexicographique ; les composants sprites passent le Z de leur entité
   (`AnimatedSpriteComponent.cs:312`, `StaticSpriteComponent.cs:115`). Or la correction de la file
   triée **repose sur un invariant de coplanarité** : un unique `DepthStencilState` partagé
   (`:92-97`, `LessEqual`, écriture activée) sert au chemin trié **et** au chemin par lots, et les
   participants triés doivent partager un même Z pour que la clé décide, et non le tampon de
   profondeur. **C'est là que se joue la conception, et c'est P1.**
3. **L'étape 1 n'est pas complète non plus**, selon la définition du document lui-même (lignes
   673-680) : une valeur `depth.*` inconnue doit produire un avertissement d'import ou un diagnostic
   éditeur, une valeur invalide une erreur d'import. `TileMapDepthSettings` ne le fait pas. Résidu
   mineur, mais il appartient à ce chantier ou à aucun.

### Le rayon d'explosion, mesuré deux fois

**485 fichiers `.tileMap`** en tout : 483 sous `alundra-project`, 2 dans ce dépôt.

- Les **483 cartes Alundra** portent **exactement une** propriété `depth.*` chacune :
  `depth.role = CollisionOnly` sur une couche de navigation synthétique. Aucune autre, jamais. Et
  comme `Role` referme la garde avant que les autres champs ne soient lus, **honorer ces réglages ne
  change rien pour le portage**.
- La carte de `CasaEngine.Demos` **ne porte aucune** propriété `depth.*`.
- **`Projects/RPGDemo/Maps/map_1_1.tileMap` est le SEUL contenu du dépôt qui exerce réellement**
  `renderPass`, `sortingLayer`, `orderInLayer` et `elevation` — sur ses quatre couches, avec des
  valeurs cohérentes entre elles. (Révision 2 : la révision 1 écrivait `Projects/CasaEngine.RPGDemo/`,
  chemin qui **n'existe pas**.)

**Le chantier est donc additif pour tout le contenu existant sauf une carte**, qui est aussi sa
fixture d'acceptation naturelle.

### Deux corrections à des affirmations que j'avais reprises

- **Un chemin d'édition existe déjà.** J'avais dit qu'aucune surface éditeur n'exposait ces réglages.
  Faux : elles s'écrivent dans Tiled et arrivent par `Tiled/TiledMapImporter.ReadCustomProperties`,
  via `FileOperationService.cs:423-424` → `EditorAssetImportService.ImportFile`, et
  `TileMapInspectorPanel.cs` les affiche en lecture seule.
- **`ObjectLayers` n'est pas totalement inerte** : `EditorAssetJsonSerializer.cs:440` les parcourt
  pour les sérialiser. C'est le *rendu* qui les ignore.

### Modifications préexistantes de l'auteur

`CasaEngine.Launcher/Program.cs` est modifié et **non indexé** : ne jamais l'indexer, ne jamais
l'écraser.

## Décisions verrouillées

| Réf | Décision |
|---|---|
| D1 | **Couches fixes : la passe entre dans le Z, `zOffset` reste le séparateur fin, `Layers` n'est jamais réordonné.** (Révision 2.) Une couche portant des métadonnées `depth.*` (D7) et restant chunkée est dessinée à `translation.Z + DeriveDepthOffset(RenderPass) + zOffset`, avec `DeriveDepthOffset` pure, monotone dans l'ordre des passes, valant **exactement 0 pour `YSortedWorld`**. La passe est donc honorée ; `zOffset` garde son rôle actuel de séparation entre couches d'une même passe — il le tient déjà dans tout le contenu, par pas de 0,1 que le tampon de profondeur résout. **`SortingLayer`, `OrderInLayer` et `Elevation` ne sont PAS consommés pour les couches chunkées dans ce chantier**, et le document le dira. Raison, établie en relecture : les tuiles **non statiques** d'une couche chunkée ne sont pas dessinées en séquence, elles sont mises en file et vidées **après tous les lots** (`TileMapComponent.cs:474-481`, `DefaultViewPipeline.cs:35,42-45`) ; à Z égal elles gagneraient toujours, quelle que soit la séquence. Seul un Z distinct par couche ordonne correctement, et `zOffset` le fournit. `Layers` **n'est jamais réordonné** : l'index de couche est une clé d'adressage publique, utilisée par le portage (`WallPlacementOverlay.cs:264,321`). Aucun tableau d'ordre de dessin n'est nécessaire : le Z fait le travail, comme aujourd'hui. |
| D2 | **Une couche est triée tuile par tuile si et seulement si `UsesDynamicSort`** — un `depth.sortMode` posé, ou `depth.ySort = true` — **quel que soit son rôle** : la demande explicite de tri prime sur le défaut du rôle, qui ne fournit alors que la passe. (Révision 2 : `UsesDynamicSort` et « rôle `YSortedSource` » ne sont pas synonymes — `depth.role = Ground` avec `depth.ySort = true` satisfaisait les deux prédicats, `TileMapDepthSettings.cs:62,66-70,105-111`.) Une telle couche quitte le chemin chunké et chaque tuile est soumise avec une `RenderSortKey2D` construite depuis la couche et une `SortCoordinate` par tuile selon `SortMode`, **au Z coplanaire `translation.Z`** — le mécanisme d'`AddSortedOverlayTile`, rendu automatique, **plus le report des `TileCellFlags` en `SpriteEffects`** que l'overlay n'avait jamais eu à porter. **Sur le chemin tourné `DrawWithWorldMatrix`**, une telle couche est dessinée à plat par le chemin existant avec un avertissement **unique** : c'est la limite que l'overlay documente déjà, aucune surcharge à clé n'acceptant de transformée monde. |
| D3 | **L'étape 6 reste hors périmètre.** `SpawnAsEntity` et `EmitsSortableObjects` restent morts ; le document le dira. |
| D4 | **Le résidu de l'étape 1 entre dans le chantier, RÉTRÉCI à sa moitié sûre** (révision 3, sous plafond de relecture — **à confirmer par l'auteur**) : toute propriété `depth.*` non comprise → **avertissement** de chargement, une fois, avec le nom de la couche et la clé, et le défaut actuel s'applique. **La moitié « valeur invalide → erreur de chargement » que le document exige (lignes 673-680) est REPORTÉE**, avec sa raison : aujourd'hui aucune valeur ne peut être invalide — chaque lecteur retombe en silence sur son défaut —, et faire échouer un chemin de chargement partagé par les 483 cartes du portage et par l'import éditeur demande une décision sur le mécanisme (exception ou erreur journalisée avec repli) et sur l'effet sur le contenu existant, que ce chantier ne prend pas seul. |
| D5 | **`Projects/RPGDemo/Maps/map_1_1.tileMap` est la fixture d'acceptation**, seule carte du dépôt qui exerce ces réglages (métadonnées aux lignes 13-18, 356-361, 699-704, 1042-1047 ; chargée par `Projects/RPGDemo/DefaultWorld.world:12-18`, tilemap à Z = 0). Capture avant/après. (Révision 2 : le chemin `Projects/CasaEngine.RPGDemo/…` de la révision 1 **n'existe pas**.) **`CasaEngine.Demos/Content/Maps/map_1_1.tileMap`** — mêmes `zOffset` 0 / 0,1 / 0,2 / 0,8, **aucun** `depth.*` — est la fixture de non-régression de D6. |
| D6 | **L'absence de métadonnée reproduit exactement l'ordre actuel** — ordre du tableau, `zOffset` — et un test l'épingle sur les valeurs. Le chantier ne rend indéterminé aucun rendu aujourd'hui stable. |
| D7 | **Le prédicat « porte des métadonnées »** (révision 2 — il n'existait pas) : une couche porte des métadonnées **si et seulement si au moins une clé de `TileMapLayerData.CustomProperties` commence par `depth.`**, calculé **une fois au chargement** et mémorisé sur la couche. `TileMapDepthSettings` est une structure sans indicateur de présence (`:22-123`) et `Depth` est affecté inconditionnellement (`TileMapLayerData.cs:56`) : `depth.role = Ground` explicite et absence totale de clé donnent des réglages **identiques**. Seule la clé brute fait foi. Les 483 cartes Alundra en dépendent : leurs six couches `Render_N` n'ont **aucune** clé `depth.*` et ne tiennent que par leurs `zOffset` distincts. |

## Points à valider — tranchés le 2026-09-07 en D1 → D6, conservés pour l'historique

- **P1 — Comment les couches chunkées participent-elles à l'ordre global ?** C'est LA question, et la
  réfutation 2 l'impose. Trois voies : **(a)** dériver un Z scalaire depuis
  `(RenderPass, SortingLayer, OrderInLayer, Elevation)` et le donner aux lots statiques — les couches
  fixes restent chunkées, l'ordre devient explicite, mais il faut définir la dérivation et prouver
  qu'elle ne casse pas l'invariant de coplanarité du chemin trié ; **(b)** router les couches
  concernées dans la file triée — ordre parfaitement uniforme, mais **on perd le chunking**, ce que le
  document interdit explicitement pour les couches fixes (lignes 741-751) ; **(c)** ne rien changer
  aux couches fixes et ne router que les couches marquées en tri dynamique.
  **Je recommande (a) pour les couches fixes et (c) pour les autres** : c'est ce que le document
  décrit, et ça préserve le chunking.
- **P2 — L'étape 6 est-elle hors périmètre ?** `SpawnAsEntity` et `EmitsSortableObjects` resteraient
  morts après ce chantier. **Je recommande de les laisser hors périmètre** et de le dire dans le
  document, plutôt que de les câbler à moitié.
- **P3 — Le résidu de l'étape 1** (avertissement sur valeur inconnue, erreur sur valeur invalide)
  est-il dans ce chantier ? **Je recommande oui** : c'est petit, c'est la même zone, et sans lui une
  faute de frappe dans Tiled restera silencieuse alors que les réglages deviennent agissants.
- **P4 — La fixture d'acceptation.** `Projects/CasaEngine.RPGDemo/Maps/map_1_1.tileMap` est le seul
  contenu qui exerce ces réglages. **Je recommande de l'adopter comme fixture**, avec une capture
  avant/après, puisque c'est aussi le seul rendu que le chantier peut changer.
- **P5 — La règle de départage quand `depth.*` est absent.** La carte de `CasaEngine.Demos` n'a rien.
  **Je recommande que l'absence de métadonnée reproduise exactement l'ordre actuel** (ordre du
  tableau, `zOffset`), et qu'un test l'épingle — sinon le chantier rendrait indéterminé un rendu
  aujourd'hui stable.

## Règles d'exécution pour l'agent

- **Branche dédiée `chantier/profondeur-tilemap-etapes-4-5`**, créée depuis `main`. Jamais de commit
  sur `main`.
- **Une seule tâche à la fois.** Icône `⏳` → `🚧` avant de commencer ; `✅`, `🧪` ou `⚠️` à la fin,
  avec une courte note de validation, puis **un commit dédié** incluant la mise à jour de ce fichier.
- **Un commit par tâche**, atomique et compilable, message en anglais `type(area): summary`.
- **Ne jamais pousser.**
- **Ne rien inventer** : sinon `⚠️ Blocked`, question dans « Points ouverts », et **arrêt**.
- **Build obligatoire** avant tout `✅` ; `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj` dès
  qu'une tâche touche du code testé. **Le `.sln` n'inclut pas `CasaEngine.Tests`** : le builder
  explicitement avant tout `--no-build`.
- **Ne jamais indexer** `CasaEngine.Launcher/Program.cs`. `git add` fichier par fichier.
- Rappel moteur : pas d'allocation, de LINQ ni de closure dans `Update`/`Draw` ; restaurer tout état
  GPU modifié ; sérialisation additive.

## Légende des statuts

⏳ Todo · 🚧 In progress · 🧪 Needs testing · ✅ Done · ⚠️ Blocked

## Validation globale

- `dotnet build CasaEngine.MonoGame.sln` puis `dotnet build CasaEngine.Editor.MonoGame.sln` : 0 erreur.
- `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj` : **zéro échec** (1584 aujourd'hui).
- **Non-régression du portage** : `Alundra.Tests` inchangé à 815 — les 483 cartes Alundra ne portent
  que `depth.role = CollisionOnly`, donc rien ne doit bouger.
- **Smoke visuel** : `Projects/CasaEngine.RPGDemo`, carte `map_1_1`, capture avant/après (P4).

---

## Phase 1 — Les couches fixes (D1, D6)

### 🚧 T1.1 — `DeriveDepthOffset` et le Z des couches fixes (révision 2, amendée en révision 3)

- Objectif : la passe de rendu d'une couche fixe qui porte des métadonnées entre dans son Z ; le
  reste — `zOffset`, la plage de culling, l'adressage par index — ne bouge pas ; l'absence de
  métadonnée ne change rien.
- Fichiers : `CasaEngine/Framework/Scene/Entities/Components/TileMapComponent.cs` ;
  `CasaEngine/Framework/Assets/TileMap/TileMapLayerData.cs` (le prédicat D7) ; un nouveau type pur
  sous `CasaEngine/Framework/Rendering/Depth/` ; `CasaEngine.Tests/TileMap/` et
  `CasaEngine.Tests/Rendering/`.
- Étapes :
  1. **Le prédicat D7** : `TileMapLayerData` porte `HasDepthMetadata` = « au moins une clé de
     `CustomProperties` commence par `depth.` ». Calculé au chargement **et transporté (ou recalculé)
     par `CreateWorldWorkingCopy`** (`TileMapLayerData.cs:26-45`) — c'est cette copie de travail que le
     composant dessine (`TileMapComponent.cs:227-229`, `:425-435`), et elle n'appelle jamais `Load`.
     (Révision 3 : sans ça, le drapeau serait faux sur toute couche rendue et la tranche serait inerte
     en jeu avec des tests verts.) Jamais dans `Draw`. Tests : zéro clé → `false` ;
     `depth.role = Ground` seul → `true` ; **`template.CreateWorldWorkingCopy().Layers[i].HasDepthMetadata
     == template.Layers[i].HasDepthMetadata`** ; et le test de Z de l'étape 3 s'exécute **sur une copie
     de travail**, pas sur une couche fraîchement chargée.
  2. **`DeriveDepthOffset(RenderPass2D)`**, statique pure : monotone dans l'ordre de `RenderPass2D`,
     **0 pour `YSortedWorld`**, négatif avant, positif après, avec un pas ≥ 1 entre passes voisines
     pour dominer tout `zOffset` du contenu (tous < 1). Tests : monotonie sur toute l'énumération ;
     zéro exact pour `YSortedWorld` ; pas minimal.
  3. **Le Z**, dans les deux chemins (`DrawTileMap` et `DrawWithWorldMatrix`) : si `HasDepthMetadata` et
     couche chunkée, `worldZ = translation.Z + DeriveDepthOffset(layer.Depth.RenderPass) + zOffset` ;
     sinon `worldZ = translation.Z + zOffset`, **inchangé au caractère près**. **`Layers` n'est pas
     réordonné, aucun tableau d'ordre n'est créé** : le Z ordonne, comme aujourd'hui.
  4. **Les DEUX sites de culling suivent** (révision 3 : la révision 2 n'en nommait qu'un) :
     - `GetRenderedLayerWorldZRange` (`TileMapComponent.cs:1647-1671`), qui alimente
       `TryGetVisibleTileRange` et `TryGetWorldViewBounds`, applique **la même dérivation** — sinon,
       sous la caméra 2D perspective (`CameraTargeted2dComponent.cs:97-106`), la dalle ne couvrirait
       plus les plans dessinés.
     - **`GetBoundingBox`** (`TileMapComponent.cs:320-346`), qui construit l'étendue Z du composant
       depuis les `zOffset` bruts et alimente **l'index spatial du monde** (`World.cs:500,748`,
       requêté au dessin `:758-767`) : avec un pas ≥ 1 entre passes, la boîte ne contiendrait plus les
       plans dessinés et **l'entité tilemap pourrait être éliminée entière**.
     Tests : la dalle **et** `GetBoundingBox().Min.Z / .Max.Z` valent exactement le min/max des Z
     réellement utilisés au dessin sur une carte à métadonnées, et leurs valeurs d'aujourd'hui sur une
     carte sans clé `depth.*`.
  5. **Tests D6 et D7** : la fixture `CasaEngine.Demos/Content/Maps/map_1_1.tileMap` (aucun `depth.*`)
     produit la même séquence de `(worldZ, couche)` qu'avant, épinglée sur les valeurs 0 / 0,1 / 0,2 /
     0,8 ; une couche portant seulement `depth.role = Ground` prend le Z dérivé de la passe.
  6. **Test d'adressage** : `GetTileReference(layerIndex, x, y)` rend la même tuile avant et après, sur
     une carte dont les métadonnées changent les Z.
- Validation : `dotnet test CasaEngine.Tests` zéro échec ; `Alundra.Tests` 815 inchangé — ses 483
  cartes n'ont aucune clé `depth.*` sur leurs couches de rendu.
- Commit : `feat(tilemap): fixed layers fold their render pass into their depth`

## Phase 2 — Les couches en tri dynamique (D2)

### ⏳ T2.1 — Les couches `UsesDynamicSort` par la file triée (révision 2)

- Objectif : une couche en tri dynamique quitte le chunking et soumet chaque tuile avec sa clé, ses
  drapeaux et son Z coplanaire ; sur le chemin tourné elle se dégrade explicitement.
- Fichiers : `TileMapComponent.cs`, tests.
- Étapes :
  1. **Le prédicat, unique** : `layer.Depth.UsesDynamicSort`. Il **prime** sur le rôle : une couche
     `depth.role = Ground` avec `depth.ySort = true` prend ce chemin et pas le lot statique. Test :
     exactement cette couche → `TryDrawStaticChunkBatch` **n'est pas** appelé, les tuiles sont soumises
     à clé.
  2. Sur le chemin axis-aligned, pour une telle couche : itérer les tuiles visibles (la plage de culling
     existante, comme l'overlay) et soumettre chacune par
     `SpriteRendererComponent.DrawSprite(..., in RenderSortKey2D, ...)` avec la **surcharge à ciseaux
     explicite** — jamais `GraphicsDevice.ScissorRectangle` au moment de la mise en file.
  3. La clé : passe, calque, ordre, élévation, décalage local depuis la couche ; `SortCoordinate` depuis
     la tuile selon `SortMode` (`TopDownYUp` : sa ligne écran) ; `StableId` depuis l'index de tuile.
     Z = `translation.Z`, coplanaire.
  4. **Les drapeaux** : reporter les `TileCellFlags` de la tuile (miroir, rotation) en `SpriteEffects`,
     comme le chemin plat le fait (`:480-481`, `:636-637`) et comme l'overlay **ne le fait pas**
     (`:553`, `SpriteEffects.None` en dur). Test : une tuile en miroir d'une telle couche est soumise
     avec l'effet correspondant.
  5. **Le chemin tourné** (`DrawWithWorldMatrix`) : une telle couche y est dessinée **à plat par le
     chemin existant**, avec un avertissement `Logs.WriteWarning` émis **une fois par couche** — jamais
     par frame. Raison : aucune surcharge `DrawSprite` à clé n'accepte de transformée monde
     (`SpriteRendererComponent.cs:546-612`), et l'overlay documente déjà cette limite. Test : une
     tilemap tournée avec une telle couche dessine à plat et avertit une fois sur dix frames.
  6. Test d'ordre : deux tuiles d'une telle couche et un sprite Y-trié entre elles produisent l'ordre
     attendu.
- Validation : suite moteur zéro échec.
- Commit : `feat(tilemap): dynamically sorted layers join the sorted sprite queue per tile`

## Phase 3 — Le résidu de l'étape 1 (D4)

### ⏳ T3.1 — Diagnostics de chargement (révision 3 : **rétrécie**, voir D4 amendée)

- Objectif : une propriété `depth.*` que le chargeur ne comprend pas **avertit une fois**, avec le nom
  de la couche et la clé, et le défaut actuel s'applique. **Aucune erreur de chargement dans ce
  chantier.**
- Fichiers : `CasaEngine/Framework/Assets/TileMap/TileMapDepthSettings.cs` ;
  `CasaEngine/Framework/Assets/TileMap/TileMapLayerData.cs` (le seul appelant qui connaît le nom de la
  couche, `:49,56`) ; tests.
- Les cas, énumérés — aujourd'hui **rien** n'est invalide, chaque lecteur retombe en silence sur son
  défaut (`ReadEnum :233-251`, `ReadInt32 :207-211`, `ReadBoolean :213-231`, et `ReadSortingLayer
  :162-172` hache toute chaîne) :
  1. **clé `depth.*` non reconnue** — hors des onze clés lues → avertissement ;
  2. **clé d'énumération reconnue, valeur hors énumération** (`role`, `renderPass`, `sortMode`) →
     avertissement, défaut ;
  3. **clé entière reconnue, valeur non entière** (`orderInLayer`, `elevation`, `localSortOffset`)
     → avertissement, défaut ;
  4. **clé booléenne reconnue, valeur non booléenne** (`ySort`, `spawnAsEntity`) → avertissement, défaut.
- Mécanisme : `Logs.WriteWarning` **une fois par (couche, clé)**, au chargement seulement —
  `FromCustomProperties` n'est appelé qu'au chargement et par la copie de travail, jamais par frame ;
  le nom de la couche arrive par une **surcharge additive** de `FromCustomProperties` prenant le nom,
  appelée depuis `TileMapLayerData.cs:56`. L'ancienne signature reste.
- Tests : un cas de chaque catégorie avertit avec le nom de la couche et la clé, et produit le défaut ;
  une carte n'ayant que des clés valides charge **sans aucun avertissement** et à l'identique.
- Validation : suite moteur zéro échec.
- Commit : `feat(tilemap): warn once about depth properties the loader does not understand`

## Phase 4 — Preuve et documentation (D5)

### ⏳ T4.1 — Fixture RPGDemo et mise à jour du document

- Objectif : montrer que la seule carte concernée rend comme attendu, et mettre le document d'accord
  avec le code.
- Fichiers : `docs/engine/tilemaps-gestion-profondeur.md`, `docs/README.md` si besoin.
- Étapes :
  1. Lancer `Projects/RPGDemo` (monde `DefaultWorld.world`, carte `map_1_1`) avant T1 (capture) et
     après T2 (capture) ; consigner ce qui a changé et pourquoi c'est attendu d'après ses métadonnées.
     Sur `CasaEngine.Demos/Content/Maps/map_1_1.tileMap`, sans `depth.*` : **rien ne doit changer**.

     **Capture « avant » faite le 2026-09-07, avant toute modification** :
     `scratchpad/depth-before/map_1_1-before.png` (1024×768, non noire) et `layer-depth-note.md`.
     Technique : **lecture du back-buffer en processus** (`GraphicsDevice.GetBackBufferData` +
     `SaveAsPng`), celle de `GameEditor.CaptureAutomationScreenshot` — aucune capture d'écran ni de
     fenêtre, donc ni image noire ni risque de vie privée. `--play-smoke` ne pouvait pas atteindre la
     carte : `RPGDemo.json` démarre sur `TitleScreenWorld.world` et aucun drapeau ne choisit un autre
     monde ; un harnais jetable **hors dépôt** appelle le même `SetWorldToLoad("DefaultWorld.world")`
     que le bouton « Start Game ». Il est conservé pour la capture « après ».

     **Les quatre couches, mesurées dans le fichier** (tilemap à translation `(0, 700, 0)`, donc Z = 0) :

     | # | `z_offset` | rôle | passe | `orderInLayer` |
     |---|---|---|---|---|
     | 1 | 0,0 | Ground | Ground | 0 |
     | 2 | 0,1 | GroundDetails | GroundDetails | 10 |
     | 3 | 0,2 | GroundDetails | GroundDetails | 20 |
     | 4 | 0,8 | Foreground | Foreground | 0 |

     **Ce que D1 va en faire, prédit avant T1.1** : les passes sont ordonnées comme les `zOffset`
     (Ground < GroundDetails < Foreground), donc l'ordre **entre les quatre couches ne change pas**. Ce
     qui change est leur position **par rapport au plan des sprites à Z = 0** : Ground et
     GroundDetails passent en Z négatif — derrière les sprites —, Foreground reste positif — devant.
     Aujourd'hui les quatre sont à Z ≥ 0, c'est-à-dire au niveau ou devant les sprites. La capture
     « après » doit donc montrer le joueur **devant** le sol et ses détails, et **derrière** l'avant-plan.
     Si ce n'est pas ce qu'elle montre, c'est un arrêt.
  2. Dans le document : passer les étapes 4 et 5 à « fait » avec ce qui est réellement livré ; dire
     que l'étape 6 reste ouverte ; corriger les deux dérives constatées (`SpriteBlendMode` a gagné
     `Additive`/`Subtractive`, `RenderPass2D` a gagné `ScreenEffects`).
- Validation : capture après cohérente avec les métadonnées de la carte ; document relu.
- Commit : `docs(engine): tile-map depth migration steps 4 and 5 delivered`

---

## Points ouverts

- P1 à P5 ci-dessus.
- Le document `docs/engine/tilemaps-gestion-profondeur.md` devra être mis à jour en fin de chantier :
  ses étapes 4 et 5 changent d'état, et il porte deux dérives constatées — l'énumération
  `SpriteBlendMode` a grandi (`Additive`, `Subtractive`) et `RenderPass2D` a gagné `ScreenEffects`.
