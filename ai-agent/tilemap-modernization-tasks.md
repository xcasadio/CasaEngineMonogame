# Plan agent IA - Modernisation TileMap et import Tiled

## Contexte

Ce plan transforme l'analyse de `docs/analyse_tilemaps_casaengine.md` en feuille de route actionnable pour un agent IA.

Objectif final : faire évoluer le système TileMap de CasaEngineMonogame vers un modèle plus robuste et plus moderne, tout en conservant la compatibilité avec les assets existants et en ajoutant l'import de maps créées avec l'éditeur Tiled.

## Avis technique synthétique

Le système actuel est une bonne base côté modèle d'asset : `TileMapData`, `TileMapLayerData`, `TileSetData`, `TileData` et les variantes de tiles sont déjà séparés du composant runtime.

Le point faible n'est pas le format de données de départ, mais le runtime : la map est parcourue entièrement dans `TileMapComponent.Draw`, toutes les tiles reçoivent `Update`, les collisions sont créées tuile par tuile, `RemoveTile` ne met pas à jour l'asset ni la physique, et le constructeur de copie partage des layers runtime. Pour une petite démo, c'est acceptable ; pour un éditeur, de grosses maps ou des imports Tiled, c'est trop fragile.

La bonne stratégie est progressive : fiabiliser d'abord les données et les mutations, ajouter le culling caméra, puis introduire chunking, cache de rendu et collisions fusionnées. L'import Tiled doit être traité comme un import éditeur/offline vers les assets CasaEngine, pas comme un format runtime direct.

## Contraintes pour l'agent

- Garder la compatibilité des fichiers `.tileMap` et `.tileset` existants.
- Ne pas casser `TileMapDemo` ni les maps de `Projects/RPGDemo/Maps` et `CasaEngine.Demos/Content/Maps`.
- Pas de LINQ ni d'allocations évitables dans `Update`/`Draw`.
- Centraliser les mutations de tiles : aucune modification directe dispersée de `TileMapLayerData.tiles` ou `TileMapLayer.Tiles`.
- Restaurer proprement les ressources physiques et GPU créées par chunks.
- Préférer un import Tiled qui convertit en assets CasaEngine plutôt qu'une dépendance runtime forte à Tiled.
- Toujours ajouter des tests ciblés ou, si impossible, un sample de validation minimal.
- Chaque tâche atomique terminée doit être validée puis commitée avant de passer à la tâche suivante.
- Chaque tâche doit commencer par son icône de statut, directement devant son libellé : `✅ Done`, `🚧 In progress`, `⏳ Todo`, `🧪 Needs testing`, `⚠️ Blocked`.

## Etat observé

### ✅ Done - Analyse initiale

Faits confirmés dans le code :

- `TileMapComponent.InitializeWithWorld` instancie un objet runtime par cellule.
- `TileMapComponent.Draw` parcourt toutes les layers et toutes les cellules à chaque frame.
- `TileMapComponent.Update` parcourt toutes les tiles même si seules les auto/animated tiles ont besoin d'un update.
- Les tiles animées existent dans le modèle, mais le chargement runtime est commenté et `AnimatedTile.Draw` est incomplet.
- Les collisions utilisent un `BoxShape` par tuile bloquante ou trigger-like.
- `TileData.CollisionShape` existe, mais n'est pas exploité par le runtime TileMap.
- `RemoveTile` remplace seulement la tile runtime par `EmptyTile` et laisse un TODO pour la physique.
- Le constructeur de copie de `TileMapComponent` fait `Layers.AddRange(other.Layers)`, donc il partage des états runtime.
- `GetBoundingBox` mélange un min à `0` et un max avec `-height`, ce qui peut produire une box invalide selon les conventions attendues.
- `Constants.FileNameExtensions` expose `.tileMap`, mais pas `.tileset`, alors que des fichiers `.tileset` existent.
- Il n'y a pas de support explicite `.tmx`, `.tsx` ou Tiled JSON dans les sources principales.

## Architecture cible

```text
TileMapData
    - MapSize
    - TileSize ou référence tileset compatible
    - Layers persistantes
    - Références tileset

TileSetData
    - Texture(s)
    - Tile definitions
    - Collision definitions
    - Animation definitions
    - AutoTile / Wang / terrain metadata

TileMapComponent
    - Référence asset
    - Runtime state par instance
    - API SetTile/GetTile/RemoveTile
    - Dirty flags visuel/collision

TileMapRenderer
    - Culling caméra
    - Chunks visibles
    - Buffers statiques par chunk
    - Chemin séparé pour tiles animées/dynamiques

TileMapCollisionSystem
    - Chunks collision
    - Fusion de rectangles
    - Triggers, plateformes one-way, slopes/custom polygon à terme

TiledImporter
    - Lecture .tmx/.tsx et/ou JSON Tiled
    - Conversion vers .tileMap/.tileset/.texture
    - Copie des images source
    - Inscription AssetCatalog
```

## 🧪 Phase 1 - Fiabiliser les assets et les mutations

Priorité : très haute.

Fichiers probables :

- `CasaEngine/Framework/Assets/TileMap/TileMapData.cs`
- `CasaEngine/Framework/Assets/TileMap/TileMapLayerData.cs`
- `CasaEngine/Framework/Assets/TileMap/TileSetData.cs`
- `CasaEngine/Framework/Scene/Entities/Components/TileMapComponent.cs`
- `CasaEngine.EditorServices/EditorAssetJsonSerializer.cs`

Tâches :

- ✅ Ajouter une validation de `MapSize` et `layer.tiles.Count == width * height` au chargement.
- ✅ Ajouter `IsInside(x, y)`, `GetTileIndex(x, y)`, `GetTileId(layerIndex, x, y)` et `SetTileId(layerIndex, x, y, tileId)`.
- ✅ Définir une constante claire pour la tile vide CasaEngine (`-1`) et documenter la conversion depuis Tiled (`0` vers `-1`).
- ✅ Faire échouer proprement les IDs inconnus : log + tile vide ou exception contrôlée selon le contexte.
- ✅ Corriger `RemoveTile` pour passer par `SetTile`.
- ✅ Mettre à jour à la fois l'asset data et l'état runtime lors d'une mutation.
- ✅ Marquer les états runtime dirty : visual, collision, auto-tile neighborhood.
- ✅ Supprimer le partage des `TileMapLayer` runtime dans le constructeur de copie.
- ✅ Ajouter `Constants.FileNameExtensions.TileSet = ".tileset"`.
- ✅ Sauvegarder/restaurer le nom de layer si le modèle garde `TileMapLayerData.Name`.

Critères d'acceptation :

- Une suppression ou modification de tile est visible au rendu, sauvegardable, et ne laisse pas de collider orphelin.
- Deux composants clonés ne partagent pas la même liste runtime de tiles.
- Une map invalide produit un message explicite, pas une exception cryptique d'index ou de dictionnaire.
- Les assets existants `.tileMap`/`.tileset` continuent à se charger.

Vérifications :

- ✅ Ajouter des tests unitaires de validation `TileMapData` si le projet de tests peut référencer ces types sans GraphicsDevice.
- ✅ Lancer `dotnet build CasaEngine.MonoGame.sln`.
- 🧪 Lancer ou vérifier `TileMapDemo` si possible.

## 🧪 Phase 2 - Réduire le coût runtime immédiat

Priorité : très haute.

Fichiers probables :

- `CasaEngine/Framework/Scene/Entities/Components/TileMapComponent.cs`
- `CasaEngine/Framework/Assets/TileMap/TileMapLayer.cs`
- classes caméra/renderer 2D utiles pour obtenir le rectangle visible

Tâches :

- ✅ Corriger `GetBoundingBox` avec min/max explicites malgré l'axe Y négatif.
- ✅ Éviter `Layers.Min`/`Layers.Max` dans `GetBoundingBox` si cette méthode peut être appelée fréquemment.
- ✅ Remplacer l'update global de toutes les tiles par une liste spécialisée d'auto-tiles dirty.
- ⚠️ Ajouter une liste spécialisée d'animated tiles quand le runtime `AnimatedTile` sera finalisé.
- ✅ Calculer le rectangle visible caméra en coordonnées monde 2D depuis le `RenderFrame` courant.
- ✅ Convertir ce rectangle en plage de tiles visibles.
- ✅ Dessiner uniquement `minTileX..maxTileX` et `minTileY..maxTileY`.
- ✅ Ignorer les empty tiles avant tout appel `Draw`.
- ✅ Ajouter une marge de sécurité optionnelle pour éviter le pop-in aux bords.
- ✅ Exposer des compteurs debug `LastVisitedTileCount` et `LastDrawnTileCount` pour mesurer le parcours effectif.

Critères d'acceptation :

- Le coût de `Draw` dépend de la zone visible, pas de la taille totale de la map.
- Le rendu reste correct avec zoom caméra, caméra partiellement hors map, position de map non nulle et scale non uniforme.
- Les auto-tiles ne recalculent pas toute la map après chaque frame.

Vérifications :

- ✅ Lancer `dotnet build CasaEngine.MonoGame.sln`.
- ✅ Relancer les tests unitaires TileMapData existants.
- 🧪 Tester avec la map démo existante.
- 🧪 Ajouter une map de test plus grande ou générée temporairement pour observer que seule la zone visible est parcourue.
- ✅ Mesurer au moins le nombre de tiles visitées avant/après en debug via `LastVisitedTileCount`.

## 🧪 Phase 3 - Import Tiled v1 : conversion editor/offline

Priorité : haute.

But : importer une map Tiled simple et produire des assets CasaEngine natifs.

Portée v1 recommandée :

- Maps Tiled orthogonales finies.
- Format `.tmx` + `.tsx` et/ou export JSON Tiled.
- Tile layers classiques.
- Un tileset image par map pour la première livraison, ou erreur claire si plusieurs tilesets sont détectés.
- GIDs Tiled sans flips pour la première validation, ou stockage minimal des flags si le modèle est étendu dans la même phase.
- Copie/import de l'image du tileset vers le dossier projet.
- Génération de `.texture`, `.tileset` et `.tileMap` CasaEngine.

Fichiers probables :

- `CasaEngine.EditorServices/EditorAssetImportService.cs`
- nouveau `CasaEngine.EditorServices/Tiled/TiledMapImporter.cs`
- nouveau `CasaEngine.EditorServices/Tiled/TiledMapImportResult.cs`
- `CasaEngine/Framework/Configuration/Constants.cs`
- `CasaEngine.EditorServices/EditorAssetJsonSerializer.cs`
- éventuellement `Directory.Packages.props` si une dépendance dédiée est retenue

Décision de dépendance :

- Option recommandée : parser minimal avec `System.Xml.Linq` pour `.tmx/.tsx` et `Newtonsoft.Json.Linq` pour JSON Tiled, car Newtonsoft est déjà présent.
- Option acceptable : ajouter une dépendance Tiled dédiée seulement si elle réduit vraiment le risque et reste dans `EditorServices` ou dans le pipeline d'import, pas dans le runtime TileMap.
- Ne pas utiliser le format Tiled comme modèle runtime principal.

Tâches :

- ✅ Ajouter la détection `.tmx` dans l'import éditeur.
- ✅ Résoudre les tilesets externes `.tsx` référencés par une map `.tmx`.
- ⏳ Ajouter l'import direct `.tsx` et la détection `.json` Tiled si nécessaire.
- ✅ Créer un importeur qui lit les champs Tiled essentiels : `orientation`, `width`, `height`, `tilewidth`, `tileheight`, `layers`, `tilesets`.
- ✅ Rejeter explicitement les orientations non supportées en v1 : isometric, staggered, hexagonal.
- ✅ Convertir `gid == 0` en tile vide CasaEngine `-1`.
- ✅ Masquer les flags Tiled avant de résoudre l'ID de tile : horizontal, vertical, diagonal/anti-diagonal selon le format Tiled.
- ✅ Mapper `firstgid + local tile id` vers des IDs CasaEngine stables.
- ✅ Générer `TileSetData` depuis les rectangles de l'image tileset.
- ✅ Créer/importer le wrapper `.texture` de l'image tileset en réutilisant la logique existante d'import texture.
- ✅ Générer `TileMapData` avec une layer CasaEngine par tile layer Tiled.
- ✅ Conserver le nom des layers Tiled dans `TileMapLayerData.Name`.
- ✅ Convertir l'ordre des layers et `zOffset` de façon déterministe.
- ✅ Inscrire les nouveaux assets dans `EditorAssetCatalogService`.
- ✅ Sauvegarder le catalogue après import.
- ✅ Retourner un résultat d'import affichable dans l'éditeur : assets créés, warnings, limitations.

Critères d'acceptation :

- Un fichier `.tmx` orthogonal simple importé depuis Tiled crée au minimum un `.tileMap`, un `.tileset` et un `.texture` utilisables par CasaEngine.
- La map importée s'ouvre dans le runtime ou dans `TileMapDemo` après adaptation minimale de l'asset chargé.
- Les tiles vides, noms de layers, taille de map et UV de tileset sont corrects.
- Les flips Tiled ne polluent pas les IDs de tile, même si le rendu des flips est repoussé.
- Les erreurs d'import expliquent précisément la fonctionnalité Tiled non supportée.

Vérifications :

- ✅ Ajouter un petit fixture Tiled généré par test, avec une image très petite.
- ✅ Tester import `.tmx` avec tileset externe `.tsx`.
- ✅ Tester map avec cellules vides.
- ✅ Tester map avec plusieurs layers.
- ✅ Tester comportement sur une map isométrique : l'import doit refuser clairement.
- ✅ Lancer `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj --filter FullyQualifiedName~TiledTmx`.
- ✅ Lancer `dotnet build CasaEngine.MonoGame.sln`.

## 🧪 Phase 4 - Import Tiled v2 : fidélité des données

Priorité : moyenne à haute.

But : couvrir les usages Tiled courants au-delà d'une map simple.

Tâches :

- ✅ Supporter plusieurs tilesets dans une même map avec un modèle runtime multi-tileset/multi-texture.
- ✅ Décision prise : étendre `TileMapData` avec plusieurs références tileset et une source par cellule ; ne pas aplatir en atlas à l'import.
- ✅ Ajouter `TileMapData.TileSetDataAssetIds` en conservant `TileSetDataAssetId` comme compat legacy/shortcut mono-tileset.
- ✅ Ajouter un stockage par cellule pour la source tileset, compatible avec les maps existantes sans `tile_sources`.
- ✅ Ajouter des helpers centraux `GetTileReference`/`SetTileReference` et validation des indices de source.
- ✅ Étendre la sérialisation `.tileMap` pour écrire/lire plusieurs tilesets et `tile_sources` seulement si nécessaire.
- ✅ Étendre l'import Tiled pour accepter plusieurs `<tileset>`/`tilesets[]`, créer plusieurs `.tileset` CasaEngine et mapper chaque GID vers `{tilesetSourceIndex, tileIdLocal}`.
- ✅ Étendre `TiledMapImportResult` pour retourner l'ensemble des assets `.tileset` créés.
- ✅ Charger plusieurs `TileSetData`/textures dans `TileMapComponent` et résoudre la tile runtime à partir de la source par cellule.
- ✅ Adapter le rendu chunké statique pour grouper la géométrie par texture/tileset au lieu d'un seul batch global de layer.
- ✅ Adapter collisions et mutations (`SetTile`, `RemoveTile`, flags) pour préserver la source tileset par cellule.
- 🧪 Ajouter des tests de compatibilité : map legacy mono-tileset inchangée, import Tiled multi-tileset et sérialisation multi-tileset couverts ; validation visuelle runtime minimale encore à faire.
- 🧪 Supporter les flip flags Tiled dans le modèle runtime : `TileCellFlags` optionnels sont importés, horizontal/vertical sont rendus pour les tiles statiques, diagonal reste limité.
- ✅ Ajouter une structure de flags compatible : `TileMapLayerData.tileFlags` optionnel conserve la compatibilité avec `List<int>`.
- ✅ Ajouter une API centrale `GetTileFlags`/`SetTileFlags`/`SetTile(..., flags)` qui marque seulement le rendu dirty quand seule l'orientation change.
- ⏳ Importer les animations Tiled (`tile.animation`) vers `AnimatedTileData` ou vers des métadonnées convertibles.
- ✅ Importer les collisions rectangles Tiled depuis `objectgroup` des tiles vers `TileData.CollisionShape`.
- ✅ Importer les object layers Tiled comme données editor : objets rectangulaires, type/name, coordonnées, propriétés.
- ✅ Importer les custom properties Tiled de map, layer et tile dans `custom_properties` metadata string.
- ✅ Importer les custom properties Tiled d'object layers via le modèle `TileMapObjectLayerData`.
- ✅ Supporter tilesets embedded dans `.tmx`, pas seulement `.tsx` externe.
- ✅ Supporter les chemins relatifs Windows/Unix et les chemins contenant des espaces.
- ✅ Supporter les maps Tiled JSON `.tmj` avec tileset image embedded ou `.tsj` externe.

Critères d'acceptation :

- Une map Tiled avec deux tilesets importe sans perte majeure.
- Les collisions dessinées dans Tiled deviennent visibles dans l'overlay/debug collision CasaEngine.
- Les animations Tiled simples se retrouvent comme animated tiles CasaEngine.
- Les propriétés personnalisées importantes restent disponibles pour gameplay/editor.
- Une map Tiled avec plusieurs tilesets génère plusieurs assets `.tileset` CasaEngine et une `TileMapData` qui référence explicitement la bonne source pour chaque cellule.

Vérifications :

- ✅ Étendre les tests Tiled pour collisions rectangles de tiles, tilesets embedded et chemins avec espaces.
- ✅ Étendre les tests Tiled pour l'import JSON `.tmj`.
- ✅ Étendre les tests TileMap/Tiled pour les flip flags importés.
- ✅ Relancer les tests TileMap et le build après ajout de l'API de mutation des flags.
- ✅ Étendre les tests TileMap/Tiled pour les custom properties map/layer/tile.
- ✅ Étendre les tests TileMap/Tiled pour les object layers et leurs propriétés.
- ✅ Lancer `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj --filter FullyQualifiedName~TiledTmx`.
- ✅ Lancer `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj --filter "FullyQualifiedName~TiledTmx|FullyQualifiedName~TiledJson"`.
- ✅ Lancer `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj --filter "FullyQualifiedName~CasaEngine.Tests.TileMap.TileMapDataTests|FullyQualifiedName~TiledTmx|FullyQualifiedName~TiledJson"`.
- ✅ Lancer `dotnet build CasaEngine.MonoGame.sln`.

## 🧪 Phase 5 - Chunking visuel

Priorité : haute après culling.

Fichiers probables :

- nouveau `CasaEngine/Framework/Assets/TileMap/TileMapChunk.cs` ou namespace runtime plus approprié
- nouveau `TileMapRenderer` si l'architecture est séparée du composant
- `TileMapComponent.cs`

Structure cible indicative :

```csharp
public sealed class TileMapChunk
{
    public Point ChunkIndex;
    public Rectangle TileBounds;
    public BoundingBox WorldBounds;
    public bool DirtyVisual;
    public bool DirtyCollision;
    public bool ContainsAnimatedTiles;
}
```

Tâches :

- ✅ Choisir une taille de chunk par défaut : 16x16 tiles.
- ✅ Créer les chunks au chargement.
- ✅ Associer chaque tile à un chunk sans allocation par frame.
- ✅ Ajouter `MarkChunkDirty(layerIndex, x, y)` et marquer les voisins utiles aux auto-tiles.
- ✅ Dessiner seulement les chunks visibles, puis les cellules visibles dans ces chunks.
- 🧪 Ajouter un overlay debug chunks/visible chunks ; compteur `LastVisitedChunkCount` disponible pour validation.
- ✅ Garder le fallback cellule par cellule à l'intérieur des chunks pendant la transition vers buffers.

Critères d'acceptation :

- Modifier une tile ne reconstruit que son chunk et les voisins nécessaires pour auto-tiles.
- Une grande map ne parcourt pas les chunks hors caméra.
- Le debug overlay permet de vérifier les bounds de chunks.

Vérifications :

- ✅ Ajouter des tests unitaires `TileMapChunk` pour intersections, bounds monde et dirty flags.
- ✅ Lancer `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj --filter FullyQualifiedName~CasaEngine.Tests.TileMap.TileMapChunkTests`.
- ✅ Lancer `dotnet build CasaEngine.MonoGame.sln`.

## 🧪 Phase 6 - Buffers statiques par chunk

Priorité : haute.

Tâches :

- ✅ Générer les quads statiques du chunk dans des tableaux réutilisés.
- ✅ Créer ou mettre à jour `VertexBuffer`/`IndexBuffer` par chunk pour le tileset courant.
- ✅ Séparer tiles statiques et tiles dynamiques : auto/animated restent sur le chemin tile runtime.
- ✅ Grouper par texture/tileset pour réduire les draw calls dans la portée mono-tileset actuelle.
- ✅ Rebuilder uniquement les chunks dirty ou les buffers manquants après changement de device.
- ✅ Libérer les buffers GPU lors du detach/réinitialisation du composant.
- 🧪 Gérer device reset/reload content : les buffers sont recréés si le `GraphicsDevice` ne correspond plus, validation runtime à faire.

Critères d'acceptation :

- Une layer statique visible se rend par chunks, pas par appel `DrawSprite` par tile.
- Les ressources GPU sont libérées quand la TileMap ou le monde est déchargé.
- Le nombre de draw calls est mesurable en debug.

Vérifications :

- ✅ Étendre les tests `TileMapChunk` pour le bookkeeping des buffers statiques réutilisables.
- ✅ Lancer `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj --filter FullyQualifiedName~CasaEngine.Tests.TileMap.TileMapChunkTests`.
- ✅ Lancer `dotnet build CasaEngine.MonoGame.sln`.

## 🧪 Phase 7 - Collisions par chunks

Priorité : haute.

Tâches :

- ✅ Créer un modèle `TileMapCollisionChunk`.
- ✅ Extraire les tiles solides par chunk.
- ✅ Fusionner les rectangles adjacents de même type.
- ✅ Supprimer les anciens colliders lors d'un rebuild de chunk.
- ✅ Distinguer solides `Blocked` et `NoContactResponse`.
- ✅ Exploiter les `ShapeRectangle` de `TileData.CollisionShape` au lieu de tout convertir en full box.
- ⏳ Préparer le support one-way/slopes/custom polygon.
- 🧪 Ajouter debug draw collision ; les rectangles fusionnés sont disponibles dans `MergedTileRectangles` pour instrumentation.

Critères d'acceptation :

- Une zone solide continue génère beaucoup moins de colliders que le nombre de tiles.
- `RemoveTile` ou `SetTile` reconstruit seulement la collision du chunk concerné.
- Les colliders orphelins ne restent pas dans le monde physique.

Vérifications :

- ✅ Ajouter des tests unitaires `TileMapCollisionChunk` pour la fusion de rectangles adjacents.
- ✅ Lancer `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj --filter FullyQualifiedName~CasaEngine.Tests.TileMap.TileMapCollisionChunkTests`.
- ✅ Lancer `dotnet build CasaEngine.MonoGame.sln`.
- ✅ Relancer les tests collision chunk et le build après support runtime des shapes rectangle.

## 🧪 Phase 8 - Animated tiles et auto-tiles modernes

Priorité : moyenne.

Décision animated tiles : option 3 retenue, renderer hybride. Les tiles statiques restent dans les buffers de chunks groupés par tileset/texture ; les tiles animées sont exclues de ces buffers et rendues dans le passage dynamique existant, avec une liste runtime dédiée mise à jour seulement quand nécessaire.

Tâches animated tiles :

- ✅ Remplacer l'ancien pointeur `animation_2d_id` seul par une donnée runtime locale `AnimatedTileFrameData` : `tile_id`, `duration_ms`, et compat legacy `animation_2d_id` optionnelle.
- ✅ Étendre le chargement/sauvegarde `.tileset` pour sérialiser `animation_frames` sur `AnimatedTileData`.
- ✅ Étendre l'import Tiled TMX/TMJ pour lire `tile.animation` / `animation.frames[]` et convertir la tile concernée en `AnimatedTileData` avec frames locales du même tileset.
- ✅ Finaliser `AnimatedTile.Draw` pour dessiner la frame courante depuis la texture du tileset, en respectant les flags horizontal/vertical déjà supportés.
- ✅ Créer une liste runtime `_animatedTiles` ou équivalent dans `TileMapComponent`, alimentée au chargement et lors de `SetTileReference`.
- ✅ Mettre à jour uniquement les tiles animées dans `Update`, et demander une conditional update seulement quand il existe des animated tiles ou des auto-tiles dirty.
- ✅ Garder les animated tiles hors des buffers statiques : le chunk statique reste valide, `ContainsDynamicTiles` force le sous-passage dynamique uniquement pour les cellules animées/auto.
- ✅ Marquer le chunk visuel dirty uniquement quand une mutation transforme une cellule statique en dynamique ou inversement ; le changement de frame ne doit pas reconstruire toute la map.
- 🧪 Ajouter des tests de data/import/runtime : sérialisation frames, import Tiled animation TMX/TMJ et avancement runtime couverts ; validation visuelle/compteur intégré à faire via sample ou test harness graphique.
- ⚠️ Reporter ping-pong et temps global/local avancé après le loop Tiled simple ; Tiled encode déjà une boucle par défaut avec durées par frame.

Tâches auto-tiles :

- ✅ Remplacer le recalcul global par un recalcul des cellules modifiées et de leurs voisins via file dirty.
- ⏳ Clarifier les règles supportées : 4-bit, 8-bit, Wang tiles ou règle CasaEngine existante.
- ⏳ Importer à terme les Wang sets/Terrain sets Tiled si la donnée est compatible.
- ⏳ Ajouter un debug mask de voisinage.

Critères d'acceptation :

- Une map avec quelques tiles animées ne déclenche pas `Update` sur toutes les cellules.
- Les chunks statiques continuent à batcher les cellules statiques même si le même chunk contient des animated tiles.
- Les animations Tiled simples deviennent des `AnimatedTileData` CasaEngine et rendent leur frame courante dans le passage dynamique.
- Peindre/effacer une auto-tile met à jour la cellule et ses voisins visibles sans recalcul global.

Vérifications :

- ✅ Lancer `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj --filter FullyQualifiedName~CasaEngine.Tests.TileMap`.
- ✅ Lancer `dotnet build CasaEngine.MonoGame.sln`.

## 🧪 Phase 9 - Intégration éditeur TileMap

Priorité : moyenne.

Tâches :

- ⏳ Ajouter ou finaliser la vue TileMap editor côté MGUI/editor actuel.
- ⏳ Ajouter palette de tileset.
- ⏳ Ajouter outils paint, erase, fill, pipette, sélection rectangle et brush multi-tile.
- ⏳ Ajouter overlays : collision, tile ids, chunks, zones visibles.
- ⏳ Brancher undo/redo sur l'API centrale `SetTile`.
- ⏳ Ajouter sauvegarde propre via `EditorAssetWriterService`.
- ✅ Ajouter commande/import UI pour `.tmx`/`.tmj` dans le Content Browser.
- ✅ Afficher les warnings d'import Tiled dans l'éditeur.

Critères d'acceptation :

- L'utilisateur peut importer une map Tiled depuis le Content Browser.
- L'utilisateur peut ouvrir/inspecter la TileMap générée.
- Les modifications éditeur passent par undo/redo et sont sauvegardées.

Vérifications :

- ✅ Lancer `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj --filter FullyQualifiedName~FileOperationServiceTests`.
- ✅ Lancer `dotnet build CasaEngine.MonoGame.sln`.

## ⏳ Phase 10 - Streaming et grosses maps

Priorité : long terme.

Tâches :

- ⏳ Supporter les maps Tiled infinies si nécessaire.
- ⏳ Charger/décharger des chunks autour de la caméra.
- ⏳ Précharger une marge configurable.
- ⏳ Gérer worlds composés de plusieurs TileMaps.
- ⏳ Éviter les assets monolithiques pour les très grandes maps.
- ⏳ Ajouter dépendances assets par chunk.

Critères d'acceptation :

- Une grande map ne doit pas être entièrement présente en mémoire visuelle/physique si seules quelques zones sont actives.
- Les chunks chargés/déchargés restent cohérents avec la sauvegarde éditeur.

## Ordre recommandé de livraison

1. Phase 1 : fiabilisation data/mutation.
2. Phase 2 : culling caméra et réduction update.
3. Phase 3 : import Tiled v1 orthogonal simple.
4. Phase 5 : chunking visuel.
5. Phase 6 : buffers statiques par chunk.
6. Phase 7 : collisions fusionnées.
7. Phase 4 : fidélité Tiled avancée.
8. Phase 8 : animated/auto-tiles modernes.
9. Phase 9 : éditeur complet.
10. Phase 10 : streaming.

## Risques et décisions à prendre

- Multi-tileset Tiled : étendre `TileMapData` ou générer un tileset combiné à l'import.
- Flip flags Tiled : ajouter un modèle `TileCell` compatible ou refuser temporairement avec warning.
- Collisions Tiled : convertir en `Collision2d` existant ou créer un format TileMap collision dédié.
- Dépendance Tiled : parser minimal interne ou package spécialisé limité à l'import editor.
- Renderer TileMap : rester dans `SpriteRendererComponent` temporairement ou introduire vite un `TileMapRenderer` séparé.

## ⚠️ Non terminé en attente de choix d'architecture

Les points ci-dessous ne sont pas de simples tâches restantes. Ils demandent un cadrage d'architecture explicite avant de continuer, sinon on risque de figer un mauvais modèle de données ou un mauvais pipeline runtime/editor.

- ✅ Vrai support multi-tileset Tiled : décision prise pour un runtime `TileMapData` multi-tileset/multi-texture avec source explicite par cellule.
- ⚠️ Flip diagonal/rotations Tiled complets : choisir si le moteur stocke des flags par cellule avec rendu orienté au runtime, ou si l'import normalise les orientations en frames/UV transformées.
- ✅ Animated tiles runtime : décision prise pour l'option 3, renderer hybride avec chunks statiques inchangés et sous-passage dynamique pour animated tiles.
- ⚠️ Collisions avancées one-way/slopes/polygones : choisir un modèle de collision TileMap stable, soit aligné sur `Collision2d`, soit via un format dédié orienté physique/runtime.
- ⚠️ Auto-tiles/Wang/Terrain sets : choisir la règle canonique supportée par CasaEngine avant d'importer ou d'éditer ces données dans l'éditeur.
- ⚠️ Éditeur TileMap complet : choisir le document model editor, la granularité undo/redo, et si l'édition s'appuie directement sur `TileMapData` ou sur un modèle intermédiaire orienté outil.
- ⚠️ Streaming et grosses maps : choisir si les TileMaps restent des assets monolithiques, ou passent à un modèle chunké avec chargement/déchargement et dépendances d'assets par chunk.

## Definition of Done globale

- Les assets existants se chargent toujours.
- Une map Tiled orthogonale simple s'importe et se rend dans CasaEngine.
- Le rendu TileMap ne parcourt plus toute la map visible ou non visible.
- Les mutations de tiles sont centralisées, sauvegardables et synchronisées avec physique/rendu.
- Les collisions de grandes zones sont fusionnées ou au moins préparées par chunks.
- Les tests ou samples documentent les cas couverts et les limitations restantes.
