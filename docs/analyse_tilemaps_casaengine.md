# Analyse de la gestion des TileMaps dans CasaEngineMonogame

## Contexte

Ce rapport analyse la gestion actuelle des TileMaps dans le moteur **CasaEngineMonogame**.

Objectif :

- évaluer si l’architecture actuelle est saine ;
- identifier les problèmes de performance et de conception ;
- comparer avec une approche plus moderne de moteur de jeu ;
- proposer un plan d’amélioration progressif.

L’analyse est basée sur une lecture statique du code. Le moteur n’a pas été exécuté.

---

## Verdict global

La base est **bonne pour un moteur/editor 2D maison** : la séparation entre données d’asset, tileset, layers, tiles runtime et composant de scène est déjà présente.

Cependant, la partie runtime est encore assez **naïve** pour un moteur moderne :

- rendu de toutes les tuiles à chaque frame ;
- objets C# créés pour chaque tuile ;
- collisions physiques créées tuile par tuile ;
- animation de tiles incomplète ;
- peu de validation des données ;
- pas encore de chunking, culling ou batching spécialisé TileMap.

### Évaluation rapide

| Domaine | Note | Avis |
|---|---:|---|
| Modèle d’asset TileMap / TileSet | 7/10 | Structure claire et extensible |
| Intégration scène / composant | 6/10 | Simple et cohérente |
| Performance runtime | 3/10 | Pas encore adaptée aux grosses maps |
| Collisions TileMap | 3/10 | Fonctionnel mais trop coûteux |
| Éditeur / sérialisation | 6/10 | Bonne base, mais mutation runtime à fiabiliser |
| Niveau “moteur moderne” | 4/10 | Il manque culling, chunks, batching réel, rebuild partiel |

---

## Ce qui est bien conçu

### Séparation des données TileMap

Le dossier `Assets/TileMap` est bien découpé :

- `TileMapData`
- `TileMapLayerData`
- `TileSetData`
- `TileData`
- `StaticTile`
- `AnimatedTile`
- `AutoTile`
- `EmptyTile`
- données de collision

Cette séparation est saine : les données d’asset sont séparées du composant runtime.

### Modèle d’asset clair

`TileMapData` contient :

- la taille de la map ;
- la référence au tileset ;
- la liste des layers.

`TileMapLayerData` contient :

- le nom du layer ;
- la liste des ids de tiles ;
- le `zOffset`.

`TileSetData` contient :

- les définitions des tiles ;
- un dictionnaire `id -> TileData`.

C’est une bonne base pour charger, éditer, sauvegarder et inspecter des TileMaps dans un éditeur.

### Plusieurs types de tuiles prévus

Le design prévoit déjà plusieurs types :

- `Static`
- `Animated`
- `Auto`
- `Empty`

C’est une bonne direction. Dans un moteur moderne, toutes les tuiles ne doivent pas avoir le même coût ni la même logique runtime.

### Données de collision dans les tiles

`TileData` contient déjà des informations de collision :

- type de collision ;
- forme ;
- comportement ;
- cassabilité.

C’est une bonne base pour faire des maps interactives, destructibles, ou semi-physiques.

---

## Problème principal : le rendu parcourt toute la map

Le principal problème se trouve dans le comportement de rendu du composant TileMap.

Le composant parcourt :

```text
for each layer
    for each y
        for each x
            draw tile
```

Cela signifie que le coût de rendu est :

```text
O(nombre_layers * largeur_map * hauteur_map)
```

à chaque frame.

Même si seulement une petite partie de la map est visible à la caméra, toute la TileMap est parcourue et potentiellement dessinée.

### Conséquences

Pour une petite map, ce n’est pas forcément bloquant.

Mais pour :

- de grandes maps ;
- un éditeur de monde ;
- plusieurs caméras ;
- du split-screen ;
- un outil de preview ;
- des maps importées depuis Tiled ;
- un monde découpé en zones ;

ce modèle devient rapidement trop coûteux.

### Amélioration immédiate : culling caméra

Il faut calculer la plage visible de tiles à partir de la caméra :

```csharp
int minTileX = Math.Max(0, (int)Math.Floor((view.Left - mapX) / tileWidth));
int maxTileX = Math.Min(mapWidth - 1, (int)Math.Ceiling((view.Right - mapX) / tileWidth));

int minTileY = Math.Max(0, (int)Math.Floor((view.Top - mapY) / tileHeight));
int maxTileY = Math.Min(mapHeight - 1, (int)Math.Ceiling((view.Bottom - mapY) / tileHeight));
```

Puis ne dessiner que cette plage :

```csharp
for (int layerIndex = 0; layerIndex < layerCount; layerIndex++)
{
    for (int y = minTileY; y <= maxTileY; y++)
    {
        for (int x = minTileX; x <= maxTileX; x++)
        {
            DrawTile(layerIndex, x, y);
        }
    }
}
```

Même sans chunking, ce changement donnera un gain immédiat.

---

## Deuxième problème : pas de vrai batching GPU spécialisé TileMap

Le rendu passe par `SpriteRendererComponent`, ce qui est logique pour mutualiser le rendu de sprites.

Mais pour une TileMap, cela pose un problème : une TileMap statique ne devrait pas être traitée comme une grande quantité de sprites dynamiques indépendants.

### Limite actuelle

Le chemin actuel semble fonctionner ainsi :

```text
Tile.Draw()
    -> SpriteRendererComponent.DrawSprite()
```

Cela garde une abstraction simple, mais le coût peut rester élevé si chaque tuile produit une opération de rendu individuelle ou quasi individuelle.

Même avec un buffer de vertices, si le flush final conserve un dessin sprite par sprite ou ne regroupe pas efficacement par texture/material, le gain reste limité.

### Architecture plus moderne

Pour une TileMap, le pipeline devrait être spécialisé :

```text
TileMapRenderer
    -> visible chunks
    -> static vertex buffers
    -> one draw call per chunk/material
```

Les tiles statiques devraient être précompilées dans des buffers de géométrie.

Les tiles dynamiques ou animées devraient être traitées séparément.

### Recommandation

Conserver `SpriteRendererComponent` pour :

- sprites dynamiques ;
- personnages ;
- objets ;
- particules simples ;
- effets 2D ;
- UI/world sprites.

Créer un pipeline spécifique pour :

- TileMaps statiques ;
- layers massifs ;
- chunks ;
- collisions de map ;
- debug overlays TileMap.

---

## Troisième problème : objet runtime pour chaque tuile

Le runtime crée actuellement une instance de tuile pour chaque cellule :

- `EmptyTile`
- `StaticTile`
- `AutoTile`
- potentiellement `AnimatedTile`

C’est pratique et lisible, mais ce n’est pas optimal pour une grande map.

### Problèmes

Un objet par tuile implique :

- beaucoup d’allocations ;
- mauvaise localité mémoire ;
- pression sur le garbage collector ;
- overhead C# important ;
- parcours moins efficace que des tableaux denses.

Pour une TileMap moderne, les tiles statiques devraient surtout être des données, pas des objets.

### Approche recommandée

Stocker les données principales dans des tableaux denses :

```csharp
int[] tileIds;
byte[] flags;
ushort[] variantIds;
```

Puis créer des objets seulement pour les cas particuliers :

```text
AnimatedTileInstance
InteractiveTileInstance
BreakableTileInstance
ScriptedTileInstance
```

Cela permettrait de garder un runtime rapide tout en conservant de l’extensibilité.

---

## Collisions : actuellement trop coûteuses

La collision semble être créée tuile par tuile.

Pour chaque tile avec collision, le runtime crée une forme physique, typiquement une boîte.

### Problème

Une grande zone solide comme ceci :

```text
[X][X][X][X][X][X][X][X]
```

peut générer 8 colliders.

Alors qu’elle devrait générer 1 seul collider fusionné :

```text
[XXXXXXXXXXXXXXXX]
```

### Conséquences

Créer un collider par tuile provoque :

- trop d’objets physiques ;
- une broad phase plus coûteuse ;
- plus de mémoire utilisée ;
- plus de complexité pour supprimer/modifier des tiles ;
- des performances faibles sur les grandes maps.

### Architecture recommandée

Introduire des chunks de collision :

```text
TileMapCollisionChunk
    - Bounds
    - Solid rectangles merged
    - Slopes
    - One-way platforms
    - Custom shapes
    - Dirty flag
```

Quand une tuile change :

```text
SetTile(x, y)
    -> mark visual chunk dirty
    -> mark collision chunk dirty
    -> rebuild only affected chunks
```

---

## CollisionShape : donnée existante mais sous-exploitée

`TileData` semble charger ou stocker une `CollisionShape`.

Cependant, le runtime semble encore construire des collisions simples de type `BoxShape`, basées sur la taille complète de la tuile.

Cela signifie que la donnée de collision existe côté asset, mais n’est pas encore pleinement exploitée côté runtime.

### Types de collision à supporter

À terme, le moteur devrait supporter :

```text
None
FullBox
HalfBox
SlopeLeft
SlopeRight
OneWayPlatform
CustomPolygon
Trigger
Ladder
Water
Damage
```

Il faudrait éviter que toutes les collisions soient ramenées à une simple boîte pleine.

---

## Les tiles animées semblent incomplètes

Le modèle `TileSetData` prévoit des tiles animées.

Cependant, dans le flux runtime, le traitement des `AnimatedTile` semble incomplet ou commenté.

### Ce qui devrait exister

Le pipeline complet devrait être :

```text
AnimatedTileData
    -> AnimatedTile runtime instance
    -> register in animatedTiles list
    -> update only animated tiles
    -> update UV/frame
    -> mark visual chunk dirty if needed
```

### Important

Il ne faut pas appeler `Update` sur toutes les tuiles pour animer seulement quelques tiles d’eau, de feu ou de lave.

Il faut une liste spécialisée :

```csharp
List<AnimatedTileInstance> _animatedTiles;
```

Puis :

```csharp
foreach (var animatedTile in _animatedTiles)
{
    animatedTile.Update(deltaTime);
}
```

---

## `RemoveTile` est dangereux actuellement

La suppression d’une tuile semble remplacer la tuile runtime par une `EmptyTile`.

Mais plusieurs aspects doivent être garantis.

### Risques

Si `RemoveTile` ne met pas tout à jour, on peut obtenir :

- tuile disparue visuellement mais collision encore présente ;
- données sauvegardables non mises à jour ;
- chunk visuel non reconstruit ;
- chunk collision non reconstruit ;
- auto-tiles voisines non recalculées ;
- divergence entre éditeur et runtime.

### Comportement attendu

Une suppression devrait passer par une API centrale :

```csharp
SetTile(layerIndex, x, y, EmptyTileId);
```

Et cette API devrait faire :

```text
- modifier les données du layer ;
- mettre à jour l’état runtime ;
- marquer le chunk visuel comme dirty ;
- marquer le chunk collision comme dirty ;
- supprimer ou reconstruire les colliders ;
- recalculer les auto-tiles voisines ;
- marquer l’asset dirty si on est dans l’éditeur.
```

Exemple :

```csharp
public void RemoveTile(int layerIndex, int x, int y)
{
    SetTile(layerIndex, x, y, EmptyTileId);
}
```

---

## Risque de bug : copie superficielle des layers

Le constructeur de copie du composant TileMap semble copier certaines références directement.

Si les layers runtime sont partagés entre deux composants, cela peut provoquer des effets de bord.

### Exemple de risque

```text
TileMapComponent A
    -> LayerRuntime instance 1

TileMapComponent B
    -> same LayerRuntime instance 1
```

Si A modifie une tuile, B peut être modifié aussi sans le vouloir.

### Règle recommandée

Clarifier la séparation :

```text
TileMapData
    -> asset partagé, lecture seule autant que possible

TileMapRuntimeState
    -> instance propre au composant

TileMapLayerRuntime
    -> propre à chaque TileMapComponent

TileSetData
    -> asset partagé
```

Les assets peuvent être partagés.

Les données runtime modifiables ne doivent pas l’être par défaut.

---

## Bounding box : point à vérifier

Le calcul de bounding box doit être vérifié avec la convention d’axe Y du moteur.

Si le rendu utilise une coordonnée Y négative pour descendre dans la map, il faut garantir que :

```text
min.Y <= max.Y
```

Certaines APIs ou systèmes de culling considèrent une bounding box invalide si min et max sont inversés.

### Recommandation

Créer une méthode robuste :

```csharp
public BoundingBox GetBoundingBox()
{
    var p0 = new Vector3(0, 0, 0);
    var p1 = new Vector3(width * tileWidth, -height * tileHeight, 0);

    return BoundingBox.CreateFromPoints(new[]
    {
        p0,
        p1
    });
}
```

Ou calculer explicitement les min/max :

```csharp
float minX = Math.Min(p0.X, p1.X);
float maxX = Math.Max(p0.X, p1.X);
float minY = Math.Min(p0.Y, p1.Y);
float maxY = Math.Max(p0.Y, p1.Y);
```

---

## Architecture cible recommandée

À terme, la gestion TileMap pourrait être structurée ainsi :

```text
TileMapAsset
    - MapSize
    - TileSize
    - Layers
    - TileSet reference

TileSetAsset
    - Texture
    - Tile definitions
    - Collision definitions
    - Animation definitions
    - AutoTile rules

TileMapComponent
    - Runtime instance
    - Transform
    - Visible layers
    - Runtime modifications

TileMapRenderer
    - Visible chunk selection
    - Static chunk mesh cache
    - Animated tile update
    - Debug draw

TileMapCollisionSystem
    - Chunk collision rebuild
    - Merged boxes
    - Slopes
    - Custom shapes

TileMapEditorTool
    - Paint
    - Erase
    - Fill
    - Selection
    - Brush
    - AutoTile preview
    - Collision overlay
```

---

## Plan d’amélioration recommandé

## Phase 1 — Fiabiliser les données et les mutations

Priorité haute.

### Tâches

- [ ] Valider que `layer.tiles.Count == mapWidth * mapHeight`.
- [ ] Ajouter `IsInside(x, y)`.
- [ ] Ajouter `GetTileId(layerIndex, x, y)`.
- [ ] Ajouter `SetTileId(layerIndex, x, y, tileId)`.
- [ ] Centraliser toutes les modifications de tiles dans `SetTile`.
- [ ] Corriger `RemoveTile`.
- [ ] Supprimer ou reconstruire la physique quand une tuile change.
- [ ] Mettre à jour `TileMapLayerData` quand une tuile change.
- [ ] Finaliser le support des `AnimatedTile`.
- [ ] Éviter les shallow copies des layers runtime.
- [ ] Gérer les tile ids inconnus proprement.
- [ ] Ajouter des logs ou assertions de validation en mode debug.

### Objectif

Avoir une TileMap fiable, modifiable et cohérente entre :

- asset ;
- runtime ;
- éditeur ;
- rendu ;
- physique.

---

## Phase 2 — Ajouter le culling caméra

Priorité très haute.

### Tâches

- [ ] Convertir le rectangle visible de la caméra en coordonnées TileMap.
- [ ] Calculer `minTileX`, `maxTileX`, `minTileY`, `maxTileY`.
- [ ] Dessiner seulement les tiles visibles.
- [ ] Ignorer explicitement les `EmptyTile`.
- [ ] Ajouter une marge optionnelle de sécurité autour de la caméra.
- [ ] Ajouter un debug draw des bornes visibles.
- [ ] Tester avec plusieurs tailles de maps.
- [ ] Tester avec zoom caméra.
- [ ] Tester avec coordonnées négatives.
- [ ] Tester avec caméra partiellement hors map.

### Objectif

Réduire immédiatement le coût de rendu des grandes maps.

---

## Phase 3 — Introduire le chunking

Priorité haute après le culling.

### Structure proposée

```csharp
public sealed class TileMapChunk
{
    public Point ChunkIndex;
    public Rectangle TileBounds;
    public BoundingBox WorldBounds;

    public bool DirtyVisual;
    public bool DirtyCollision;
    public bool ContainsAnimatedTiles;

    public VertexBuffer StaticVertexBuffer;
    public IndexBuffer StaticIndexBuffer;
}
```

### Tâches

- [ ] Définir une taille de chunk : 16x16 ou 32x32 tiles.
- [ ] Générer les chunks au chargement.
- [ ] Calculer les bounds monde de chaque chunk.
- [ ] Associer chaque tile à un chunk.
- [ ] Ajouter `MarkChunkDirty(layerIndex, x, y)`.
- [ ] Rebuilder uniquement les chunks dirty.
- [ ] Dessiner uniquement les chunks visibles.
- [ ] Ajouter un debug draw des chunks.
- [ ] Mesurer le nombre de chunks visibles.
- [ ] Mesurer le nombre de draw calls.

### Objectif

Passer d’un rendu cellule par cellule à un rendu par blocs optimisés.

---

## Phase 4 — Générer des buffers statiques par chunk

Priorité haute.

### Pipeline proposé

```text
TileMapLayer
    -> chunks
        -> static vertex buffer
        -> index buffer
        -> material / texture reference
```

### Tâches

- [ ] Générer les quads visibles du chunk dans un tableau de vertices.
- [ ] Générer les indices associés.
- [ ] Créer ou mettre à jour le `VertexBuffer`.
- [ ] Créer ou mettre à jour le `IndexBuffer`.
- [ ] Séparer les tiles statiques des tiles animées.
- [ ] Grouper par texture ou tileset.
- [ ] Réduire le nombre de draw calls.
- [ ] Rebuilder seulement les chunks dirty.
- [ ] Libérer correctement les buffers GPU.
- [ ] Gérer le reload device / content.

### Objectif

Faire en sorte qu’un chunk statique coûte un draw call ou quelques draw calls, pas un draw par tuile.

---

## Phase 5 — Optimiser les collisions TileMap

Priorité haute pour les grandes maps.

### Tâches

- [ ] Créer `TileMapCollisionChunk`.
- [ ] Extraire les tiles solides par chunk.
- [ ] Fusionner les rectangles adjacents.
- [ ] Générer moins de colliders.
- [ ] Supporter `NoContactResponse` séparément des solides bloquants.
- [ ] Supporter les triggers.
- [ ] Supporter les plateformes one-way.
- [ ] Supporter les slopes si nécessaire.
- [ ] Rebuilder seulement les chunks collision dirty.
- [ ] Supprimer proprement les anciens colliders.
- [ ] Ajouter un debug draw collision.

### Objectif

Éviter un objet physique par tuile.

---

## Phase 6 — Finaliser les animated tiles

Priorité moyenne à haute.

### Tâches

- [ ] Finaliser `AnimatedTileData`.
- [ ] Créer une structure runtime `AnimatedTileInstance`.
- [ ] Maintenir une liste `_animatedTiles`.
- [ ] Mettre à jour uniquement les animated tiles.
- [ ] Mettre à jour les UV ou frame index.
- [ ] Marquer le chunk visuel dirty si nécessaire.
- [ ] Permettre des animations synchronisées ou indépendantes.
- [ ] Supporter vitesse, boucle, ping-pong.
- [ ] Supporter animation basée sur temps global.
- [ ] Supporter animation basée sur instance locale.

### Objectif

Avoir des tiles animées efficaces et isolées du coût des tiles statiques.

---

## Phase 7 — Améliorer les auto-tiles

Priorité moyenne.

### Tâches

- [ ] Définir clairement les règles d’auto-tiling.
- [ ] Recalculer une auto-tile quand ses voisins changent.
- [ ] Recalculer aussi les voisins directs.
- [ ] Supporter les règles 4-bit, 8-bit ou Wang tiles.
- [ ] Ajouter un preview dans l’éditeur.
- [ ] Ajouter un mode debug affichant le masque de voisinage.
- [ ] Marquer les chunks affectés dirty.

### Objectif

Pouvoir peindre efficacement des terrains connectés sans recalculer toute la map.

---

## Phase 8 — Intégration éditeur

Priorité moyenne.

### Tâches

- [ ] Ajouter outils Paint / Erase / Fill.
- [ ] Ajouter sélection rectangle.
- [ ] Ajouter pipette.
- [ ] Ajouter brush multi-tile.
- [ ] Ajouter palette de tileset.
- [ ] Ajouter overlay collision.
- [ ] Ajouter overlay tile id.
- [ ] Ajouter overlay chunks.
- [ ] Ajouter undo / redo.
- [ ] Ajouter sauvegarde propre des modifications.
- [ ] Ajouter validation avant sauvegarde.

### Objectif

Faire de la TileMap un vrai outil editor-friendly, pas seulement un composant runtime.

---

## Phase 9 — Streaming et grosses maps

Priorité plus tardive.

### Tâches

- [ ] Permettre de charger seulement certains chunks.
- [ ] Décharger les chunks loin de la caméra.
- [ ] Précharger autour de la caméra.
- [ ] Gérer des worlds composés de plusieurs TileMaps.
- [ ] Gérer des zones ou scènes adjacentes.
- [ ] Éviter les gros assets monolithiques.
- [ ] Ajouter un système de dépendances assets par chunk.

### Objectif

Préparer le moteur à des mondes 2D plus grands.

---

## Synthèse des priorités

### Priorité immédiate

1. Corriger les mutations de tiles.
2. Ajouter le culling caméra.
3. Ne plus dessiner toute la map.
4. Finaliser `RemoveTile`.
5. Vérifier les bounding boxes.

### Priorité court terme

1. Ajouter le chunking.
2. Générer des buffers statiques par chunk.
3. Séparer TileMapRenderer de SpriteRenderer.
4. Rebuilder seulement les chunks dirty.
5. Ajouter debug draw chunks / visible tiles.

### Priorité moyen terme

1. Fusionner les collisions.
2. Finaliser les animated tiles.
3. Améliorer les auto-tiles.
4. Ajouter les outils éditeur.
5. Ajouter undo / redo.

### Priorité long terme

1. Streaming de chunks.
2. Support grosses maps.
3. Layers avancés.
4. TileMap 2D/3D mixte.
5. Intégration plus poussée avec matériaux et lighting.

---

## Conclusion

Le système TileMap de CasaEngineMonogame est **bien parti côté modèle d’asset**, mais il est encore trop orienté :

```text
objet par tuile
+ draw complet de la map
+ collider par tuile
```

Pour devenir plus moderne, les trois changements les plus importants sont :

1. **ajouter le culling caméra immédiatement** ;
2. **passer à des chunks avec cache de rendu** ;
3. **fusionner les collisions au lieu de créer un collider par tuile**.

Après ces améliorations, le système pourra évoluer proprement vers un workflow proche de moteurs comme Unity, Godot ou Tiled :

- grosses maps ;
- édition temps réel ;
- collisions optimisées ;
- auto-tiles ;
- layers multiples ;
- debug views ;
- rendu performant ;
- intégration propre avec les caméras et le renderer.
