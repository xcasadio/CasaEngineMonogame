# Plan agent IA — Faire agir `TileMapDepthSettings` au rendu (étapes 4 et 5)

Chantier ouvert le 2026-09-07. Origine : découvert en creusant une étape du portage Alundra, mais
**c'est une dette du moteur**, utile à tous ses projets et sans rapport avec ce portage.

Les décisions D1 → D6 ci-dessous ont été arbitrées avec l'auteur le 2026-09-07 — « exécute le plan »,
lu comme l'approbation des recommandations portées par P1 à P5 : **ce plan les applique, il ne les
rediscute pas.**

Ce fichier doit être mis à jour pendant le travail : l'icône au début de chaque tâche indique son
statut courant.

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
- **`Projects/CasaEngine.RPGDemo/Maps/map_1_1.tileMap` est le SEUL contenu du dépôt qui exerce
  réellement** `renderPass`, `sortingLayer`, `orderInLayer` et `elevation` — sur ses quatre couches,
  avec des valeurs cohérentes entre elles.

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
| D1 | **Couches fixes : le Z porte la passe, la séquence porte le reste.** Une couche portant des métadonnées `depth.*` et restant chunkée (`KeepsStaticChunking`) est dessinée à `translation.Z + DeriveDepthOffset(RenderPass)`, fonction pure, monotone dans l'ordre des passes, valant **exactement 0 pour `YSortedWorld`** — le plan coplanaire des sprites triés. `SortingLayer`, `OrderInLayer` et `Elevation` **n'entrent pas dans le Z** : ils ordonnent les couches d'une même passe par **séquence de dessin**, puisque sous `LessEqual` avec écriture le dernier dessiné gagne. Aucun pas de Z fractionnaire : le tampon de profondeur ne les résoudrait pas. |
| D2 | **Couches en tri dynamique (`UsesDynamicSort`, rôle `YSortedSource`) : la file triée, tuile par tuile.** Elles quittent le chemin chunké et chaque tuile est soumise avec une `RenderSortKey2D` construite depuis la couche (passe, calque, ordre, élévation, décalage local) et une `SortCoordinate` par tuile selon `SortMode`, **au Z coplanaire `translation.Z`** — le mécanisme exact d'`AddSortedOverlayTile`, rendu automatique. C'est la seule catégorie triée par tuile, et le document l'autorise pour elle seule. |
| D3 | **L'étape 6 reste hors périmètre.** `SpawnAsEntity` et `EmitsSortableObjects` restent morts ; le document le dira. |
| D4 | **Le résidu de l'étape 1 entre dans le chantier** : valeur `depth.*` inconnue → avertissement de chargement ; valeur invalide → erreur de chargement, comme le document l'exige (lignes 673-680). |
| D5 | **`Projects/CasaEngine.RPGDemo/Maps/map_1_1.tileMap` est la fixture d'acceptation**, seule carte du dépôt qui exerce ces réglages. Capture avant/après. |
| D6 | **L'absence de métadonnée reproduit exactement l'ordre actuel** — ordre du tableau, `zOffset` — et un test l'épingle. Le chantier ne rend indéterminé aucun rendu aujourd'hui stable. |

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

### ⏳ T1.1 — `DeriveDepthOffset` et l'ordre des couches fixes

- Objectif : la passe de rendu d'une couche fixe décide de son Z ; ses autres champs décident de sa
  place dans la séquence de dessin ; l'absence de métadonnée ne change rien.
- Fichiers : `CasaEngine/Framework/Scene/Entities/Components/TileMapComponent.cs` ; un nouveau type
  pur sous `CasaEngine/Framework/Rendering/Depth/` ; `CasaEngine.Tests/TileMap/` et
  `CasaEngine.Tests/Rendering/`.
- Étapes :
  1. Une fonction pure statique `DeriveDepthOffset(RenderPass2D)` : monotone dans l'ordre de
     `RenderPass2D`, **0 pour `YSortedWorld`**, négatif avant, positif après. Tests : monotonie sur
     toute l'énumération, zéro exact pour `YSortedWorld`.
  2. Dans les deux chemins de dessin (`DrawTileMap` axis-aligned et `DrawWithWorldMatrix`), pour une
     couche qui **porte** des métadonnées et garde le chunking : `worldZ = translation.Z +
     DeriveDepthOffset(layer.Depth.RenderPass)`. Pour une couche **sans** métadonnées : `worldZ =
     translation.Z + zOffset`, **inchangé au caractère près**.
  3. Ordonner les couches fixes d'une même passe par `(SortingLayer, OrderInLayer, Elevation)` avant
     de les dessiner, **sans allocation par frame** : l'ordre est calculé une fois au chargement ou à
     l'invalidation, jamais dans `Draw`.
  4. Test D6 : une tilemap dont aucune couche n'a de métadonnée produit la même séquence de
     `(worldZ, couche)` qu'avant — épinglé sur les valeurs, pas sur la forme.
- Validation : `dotnet test CasaEngine.Tests` zéro échec ; `Alundra.Tests` 815 inchangé.
- Commit : `feat(tilemap): fixed layers take their depth from the render pass`

## Phase 2 — Les couches en tri dynamique (D2)

### ⏳ T2.1 — `YSortedSource` par la file triée

- Objectif : une couche `UsesDynamicSort` quitte le chunking et soumet chaque tuile avec sa clé.
- Fichiers : `TileMapComponent.cs`, tests.
- Étapes :
  1. Pour une telle couche, ne pas appeler `TryDrawStaticChunkBatch` ; itérer les tuiles visibles
     (réutiliser la plage de culling existante, comme l'overlay) et soumettre chacune par
     `SpriteRendererComponent.DrawSprite(..., in RenderSortKey2D, ...)` avec la **surcharge à ciseaux
     explicite** — jamais `GraphicsDevice.ScissorRectangle` au moment de la mise en file.
  2. La clé : passe, calque, ordre, élévation, décalage local depuis la couche ; `SortCoordinate` depuis
     la tuile selon `SortMode` (`TopDownYUp` : sa ligne écran) ; `StableId` depuis l'index de tuile.
     Z = `translation.Z`, coplanaire.
  3. Tests : une couche `YSortedSource` avec deux tuiles et un sprite Y-trié entre elles produit
     l'ordre attendu ; la même couche sans le rôle reste chunkée (le lot statique est bien appelé).
- Validation : suite moteur zéro échec.
- Commit : `feat(tilemap): y-sorted source layers join the sorted sprite queue per tile`

## Phase 3 — Le résidu de l'étape 1 (D4)

### ⏳ T3.1 — Diagnostics de chargement

- Objectif : une valeur `depth.*` inconnue avertit, une valeur invalide échoue, au chargement.
- Fichiers : `CasaEngine/Framework/Assets/TileMap/TileMapDepthSettings.cs`, tests.
- Étapes : distinguer « clé connue, valeur hors énumération » (avertissement `Logs.WriteWarning`,
  **une fois par couche**, jamais par frame) de « valeur inutilisable » (erreur de chargement avec le
  nom de la couche et la clé). Tests sur les deux.
- Validation : suite moteur zéro échec.
- Commit : `feat(tilemap): diagnose unknown and invalid depth properties at load`

## Phase 4 — Preuve et documentation (D5)

### ⏳ T4.1 — Fixture RPGDemo et mise à jour du document

- Objectif : montrer que la seule carte concernée rend comme attendu, et mettre le document d'accord
  avec le code.
- Fichiers : `docs/engine/tilemaps-gestion-profondeur.md`, `docs/README.md` si besoin.
- Étapes :
  1. Lancer `Projects/CasaEngine.RPGDemo` sur `map_1_1` avant T1 (capture) et après T2 (capture) ;
     consigner ce qui a changé et pourquoi c'est attendu d'après ses métadonnées.
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
