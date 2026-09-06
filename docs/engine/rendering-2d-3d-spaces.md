# Espaces de rendu 2D / 3D

Ce document décrit comment une donnée 2D (tilemap, sprite) arrive à l'écran dans CasaEngine, et
quel mode choisir selon le rendu voulu.

Decisions: see [ADR-0011](../decisions/0011-tilemap-render-spaces.md).

## Règle de projection

> Une tilemap est un objet world-space. Son espace d'affichage est décidé exclusivement par la
> caméra de la vue (ortho = 2D pixel-perfect, perspective = objet 3D) ou par un render target
> intermédiaire. Aucune logique d'espace ou de projection ne doit entrer dans `TileMapComponent`
> ni dans les assets TileMap.

Conséquence pratique : on ne « met pas une tilemap en 2D », on assigne une caméra orthographique à
la `RenderView`. Le même asset, le même composant et le même renderer servent les quatre modes
ci-dessous.

```text
TileMapData / TileSetData / TileMapComponent   données         (indépendantes de l'espace)
CameraComponent                                projection      (décide l'espace d'affichage)
SpriteRendererComponent                        interprétation  (consomme RenderFrame)
RenderPipeline / RenderView                    ordre des passes
```

---

## Mode 1 — Screen-space 2D (UI / HUD)

`Renderer2DComponent` et MGUI dessinent en coordonnées écran (pixels, `ZOrder` de 0 à 1), sans
matrice vue/projection de scène.

Réservé à l'UI et au HUD. **Ne pas y router une tilemap** : la map perdrait le culling, les chunks
statiques, la profondeur par layer et la cohabitation avec les entities du monde.

---

## Mode 2 — World-space 2D pixel-perfect

Mode nominal d'un jeu 2D. La vue utilise `Camera2dComponent`
([Camera2dComponent.cs](../../CasaEngine/Framework/Scene/Entities/Components/Camera2dComponent.cs)) :
les unités monde sont des pixels, +Y vers le haut, la caméra regarde le long de −Z.

Propriétés :

- `Target` — point du monde au centre de la vue ;
- `Zoom` — facteur de grossissement, clampé à `MinimumZoom` (`0.0001`). Le volume orthographique
  vaut `viewport / Zoom` ;
- `PixelSnap` — arrondit la caméra sur la grille texel (pas `1 / Zoom`) **au calcul de la view
  matrix uniquement**. `Target` stocké n'est jamais modifié.

Projection : `Matrix.CreateOrthographic(viewport.Width / Zoom, viewport.Height / Zoom,
viewport.MinDepth, viewport.MaxDepth)`, recalculée au resize. Avec les valeurs par défaut de
`CameraComponent.InitializeWithWorld` (near `1`, far `1000`) et la distance caméra interne fixe de
500, la fenêtre de profondeur utile est **`[Target.Z − 500, Target.Z + 499]`**. Les `zOffset` de
layers d'une map en pixels y tiennent largement ; du contenu hors de cette plage est clippé.

À `Zoom = 1`, un point du plan `Z = Target.Z` se projette au même pixel qu'avec l'ancienne astuce
perspective (caméra reculée à `z = (hauteur de vue / 2) / tan(FOV / 2)`) — mais **sans déformation
perspective** sur les layers placés à un autre Z, ce qui est l'intérêt décisif du mode.

```csharp
// CasaEngine.Demos/Demos/TileMapDemo.cs
var entity = new Entity();
var camera = new Camera2dComponent();
camera.Target = new Vector3(game.Window.ClientBounds.Size.X / 2f,
                            game.Window.ClientBounds.Size.Y / 2f,
                            0.0f);
entity.AddComponent(camera);
entity.Initialize();
game.GameManager.CurrentWorld.AddEntity(entity);
```

---

## Mode 3 — World-space 3D (tilemap orientable)

Sous caméra perspective, la tilemap est un objet 3D de plein droit : elle peut être posée au sol,
dressée en mur, tournée librement. `TileMapComponent.Draw` choisit son chemin **une fois par draw**
selon `IsAxisAlignedWorldMatrix(WorldMatrixWithScale)` (6 comparaisons sur les termes hors-diagonale).

- **Fast path axis-aligned** — rotation identité : chemin historique inchangé (arithmétique
  position/échelle, culling par plage de tiles via `TryGetVisibleTileRange`). Aucun coût ajouté.
- **Chemin rotation** — la géométrie locale des chunks est transformée par la matrice monde
  complète ; le culling se fait chunk par chunk, `BoundingBox` monde contre un `BoundingFrustum`
  réutilisé (une seule instance, réaffectée par draw).

### Sémantique du `zOffset` de layer

Les deux chemins ne l'appliquent pas dans le même espace :

| Chemin | Application du `zOffset` |
| --- | --- |
| axis-aligned | `worldZ = position.Z + layer.zOffset` — **Z monde, non scalé** |
| rotation | `Matrix.CreateTranslation(0, 0, layer.zOffset) * worldMatrix` — **espace local, donc scalé** |

Même asset, séparation de layers différente selon le chemin. À `LocalScale = 0.05` (cas de
`TileMap3dDemo`), un `zOffset` de 1 ne sépare plus les layers que de 0.05 unité monde : risque de
z-fighting. Contourner en augmentant les `zOffset` de l'asset ou l'échelle du composant.

### Tri des tiles dynamiques

`SpriteRendererComponent` trie les sprites dynamiques par `WorldMatrix.Translation.Z` monde, ce qui
n'est **pas corrélé à la profondeur caméra dès que la map est tournée** (une map au sol a tous ses
quads à Z monde quasi constant). Sans conséquence en opaque — le depth buffer tranche — mais des
artefacts d'ordre sont possibles sur des tiles réellement alpha-blended. Le chemin statique (batches
indexés) n'est pas concerné.

### Collisions

Les corps de collision suivent la transformation monde complète : le centre de la tuile, exprimé en
pixels locaux, est transformé par `WorldMatrixWithScale`, l'orientation venant de
`WorldMatrixNoScale`. Le debug physique coïncide donc avec le rendu, map tournée ou mise à l'échelle
comprise.

```csharp
// CasaEngine.Demos/Demos/TileMap3dDemo.cs — la même map posée au sol puis dressée en mur
var groundTileMap = new TileMapComponent();
groundEntity.RootComponent = groundTileMap;
groundTileMap.TileMapData = tileMapData;
groundTileMap.LocalScale = new Vector3(MapScale);
groundTileMap.LocalOrientation = Quaternion.CreateFromAxisAngle(Vector3.UnitX, -MathHelper.PiOver2);

var wallTileMap = new TileMapComponent();
wallEntity.RootComponent = wallTileMap;
wallTileMap.TileMapData = tileMapData;
wallTileMap.LocalScale = new Vector3(MapScale);
// Les quads de tiles sont simple face (normale vers +Z local, CullCounterClockwise) :
// +90° autour de Y tourne cette normale vers +X monde, face à la caméra par défaut.
wallTileMap.LocalOrientation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathHelper.PiOver2);
```

---

## Mode 4 — Render-to-texture (`TileMapSurfaceComponent`)

Pour mixer une map 2D pixel-perfect avec une scène 3D (écran d'arcade, minimap, affichage
in-game), la map est peinte offscreen dans son espace ortho naturel, et c'est la *texture résultat*
qui vit en 3D.

`TileMapSurfaceComponent`
([TileMapSurfaceComponent.cs](../../CasaEngine/Framework/Rendering/TileMapSurfaceComponent.cs))
suit le modèle exact de `WorldUIComponent` : ce n'est **pas** un `SceneComponent`, il n'est ni
attaché à une entity ni sérialisé. C'est un objet `IDisposable` enregistré sur le monde via
`World.RegisterTileMapSurface` / `UnregisterTileMapSurface` ; sa passe est déclenchée par
`World.DrawTileMapSurfacesToTextures()`, appelée dans `RenderPipeline.Render` juste à côté de
`DrawWorldUIToTextures()`, donc avant la passe monde. `World.Clear()` dispose les surfaces.

Déroulé de la passe : `GraphicsStateGuard` → `Surface.Apply` → clear → installation d'une
`RenderFrame` ortho cadrant la map (`World.SetCurrentRenderFrame`, restaurée en `finally`) →
`TileMapComponent.DrawTileMap()` → `SpriteRendererComponent.Flush(in frame)` (le flush vide aussi
les files pour ne pas empoisonner la passe monde).

Taille du render target : taille de la map en pixels × `Zoom` (entier, ≥ 1), réduite uniformément
au-delà de `MaximumSurfaceSize` (4096).

### Opt-in et compteurs

`TileMapComponent.SkipMainPassDraw` (bool runtime, non sérialisé, recopié par le constructeur de
copie) fait sortir `Draw` immédiatement : la map n'est plus dessinée dans la passe monde. La surface
appelle `DrawTileMap()`, qui ignore le flag.

Attention aux compteurs : quand `SkipMainPassDraw` est actif, `Draw` sort **avant** la remise à zéro
des compteurs, donc `LastVisitedTileCount`, `LastDrawnTileCount` et `LastStaticBatch*` décrivent la
dernière passe offscreen, pas la passe monde (valeurs figées sur une map statique).

### Invalidation

La surface ne repeint que si nécessaire. Prédicat `ShouldRedraw` : jamais rendue, `Invalidate()`
explicite, tiles animées présentes, autotiles sales, ou `TileMapComponent.TileRevision` différente
de la dernière rendue. Une map statique déjà rendue coûte **une comparaison d'entiers par frame et
zéro travail GPU**.

`TileRevision` n'est incrémentée que par les mutations passant par le composant (`SetTileReference`
et compagnie, refresh d'autotiles dans `Update`, `InitializeWithWorld`).

> **Limitation** — une écriture directe sur l'asset (`TileMapData.SetTile*`) contourne le composant :
> les chunks ne sont pas invalidés et la surface reste sur son dernier rendu. Appeler
> `TileMapSurfaceComponent.Invalidate()` explicitement dans ce cas.

> **Limitation** — la frame ortho est construite à partir de la position et de l'échelle de la map
> uniquement : la map routée vers une surface doit être **axis-aligned** (rotation identité). Pour un
> affichage orienté, tourner le quad qui consomme la texture, pas la map.

Le consommateur doit poser `SamplerState.PointClamp` sur son matériau : la surface n'expose que la
texture, la politique d'échantillonnage appartient au matériau.

```csharp
// CasaEngine.Demos/Demos/TileMapSurfaceScreenDemo.cs
var tileMapComponent = new TileMapComponent
{
    TileMapData = tileMapData,
    SkipMainPassDraw = true,
};

var screenMaterial = new UnlitTextureMaterial
{
    Tint = Color.White,
    Alpha = 1.0f,
    SamplerState = SamplerState.PointClamp,
};

_tileMapSurface = new TileMapSurfaceComponent(game.GraphicsDevice, tileMapComponent)
{
    ClearColor = Color.Black,
};
_tileMapSurface.BindToMaterial(screenMaterial);
world.RegisterTileMapSurface(_tileMapSurface);
```

`Clean()` doit désenregistrer puis disposer la surface.

---

## Checklist pixel-perfect

Pour qu'un texel de tileset couvre exactement un pixel écran :

1. `Camera2dComponent` sur la vue (jamais une caméra perspective) ;
2. `PixelSnap = true` ;
3. `Zoom` entier (×1, ×2, ×3…) ;
4. `RenderView.ResolutionScale = 1` ;
5. `SamplerState.PointClamp` sur les matériaux affichant des textures de tiles.

### Diagnostic

`PixelPerfectDiagnostics`
([PixelPerfectDiagnostics.cs](../../CasaEngine/Framework/Rendering/PixelPerfectDiagnostics.cs))
évalue le contrat et renvoie un `PixelPerfectDegradation` (`ResolutionScale`, `NonIntegerZoom`).

- `RenderPipeline` appelle `WarnOnce(view)` : un avertissement est loggé **une seule fois par vue**
  (flag `RenderView.PixelPerfectWarningLogged`), jamais par frame.
- `DebugOverlay` ajoute une ligne `PixelPerfect: OK` / `PixelPerfect: degraded (raison)` quand la
  caméra de la vue est une `Camera2dComponent`. Les chaînes sont précomposées (aucune allocation).

> Note : le diagnostic ne s'applique qu'aux caméras avec `PixelSnap` actif. Une `Camera2dComponent`
> avec `PixelSnap = false` affiche donc `PixelPerfect: OK` dans l'overlay — la ligne signale « le
> contrat n'est pas rompu », pas « l'image est snappée ».

---

## `Camera3dIn2dAxisComponent` — supprimée

Cette classe plaçait une caméra **perspective** à la distance qui rend le plan cible à l'échelle 1:1
(`z = (Game.ScreenSizeHeight * 0.5f) / tan(FieldOfView * 0.5f)`). Elle a été **retirée du moteur** au
profit de `Camera2dComponent`, qui rend le même cadrage sans ses fragilités :

- son FOV était recalculé à chaque resize (`PiOver4 * 1.7777 / AspectRatio`), donc la distance caméra
  changeait avec la fenêtre ;
- cette distance dépendait de `Game.ScreenSizeHeight`, la **taille écran globale** et non celle de la
  vue : une vue à viewport partiel n'était pas cadrée correctement ;
- la projection restait perspective : seul le plan cible était à l'échelle 1:1, tout layer à un autre
  Z était mis à l'échelle et translaté ;
- ni zoom ni snap texel, donc aucun contrat pixel-perfect possible.

Si un monde sérialisé porte encore `"type": "Camera3dIn2dAxisComponent"`, il faut le migrer à la main
— `ElementFactory` résout les composants par nom de type et échouerait au chargement. Remplacer le
`type` et le `name` par `Camera2dComponent`, retirer `fieldOfView`, et ajouter les champs que lit
`Camera2dComponent.Load` :

```json
"target": { "x": 0.0, "y": 0.0, "z": 0.0 },
"zoom": 1.0,
"pixel_snap": false
```

Le cadrage X/Y est équivalent à `Zoom = 1` ; seule la fenêtre de profondeur change (voir mode 2).

---

## Ce qu'il ne faut pas faire

```text
- Ne pas ajouter de logique d'espace ou de projection dans TileMapComponent ou les assets TileMap.
- Ne pas router une tilemap vers Renderer2DComponent / MGUI (screen-space).
- Ne pas router une tilemap tournée vers TileMapSurfaceComponent.
- Ne pas compter sur les compteurs de draw d'une map en SkipMainPassDraw pour mesurer la passe monde.
- Ne pas attendre un rendu pixel-perfect d'une caméra perspective.
```

## Voir aussi

- [tilemaps-gestion-profondeur.md](tilemaps-gestion-profondeur.md) — modèle de profondeur 2D entre tilemap et entities.
- [../editor/editor-2d-viewport.md](../editor/editor-2d-viewport.md) — mode 2D du viewport de l'éditeur.
