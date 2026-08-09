# Analyse — Espaces de rendu des TileMaps (2D pixel-perfect vs monde 3D)

## Contexte

Cette analyse fait suite à [analysis_tilemaps_casaengine.md](analysis_tilemaps_casaengine.md) (modernisation TileMap : culling, chunks, batching — largement implémentés depuis) et traite un sujet distinct : **dans quel espace une tilemap est-elle rendue, et qui décide de la projection ?**

Principe directeur retenu (aligné sur les moteurs modernes) :

```text
Tilemap        = données
Camera         = projection
Renderer       = interprétation de la tilemap dans un espace donné
RenderPipeline = ordre et combinaison des passes
```

Quatre modes de rendu à distinguer :

1. **Screen-space 2D** — coordonnées écran/pixels, sans caméra de scène.
2. **World-space 2D pixel-perfect** — caméra orthographique, 1 texel = 1 pixel écran (ou zoom entier).
3. **World-space 3D** — la tilemap est un objet du monde, soumis à une caméra perspective.
4. **Render-to-texture** — la tilemap est rendue pixel-perfect dans un RenderTarget, réutilisé comme texture dans la scène 3D. Mode robuste pour mixer 2D pixel-perfect et 3D.

L'analyse est basée sur une lecture statique du code (commit `878458a8`). Le moteur n'a pas été exécuté.

---

## Verdict global

L'architecture actuelle est **déjà à 70 % alignée** sur le modèle cible :

- la tilemap est **déjà rendue en world-space** et ne contient aucune logique de projection ;
- l'infrastructure multi-vues (`RenderView` + `RenderFrame`) sépare déjà caméra, surface et pipeline ;
- le pattern render-to-texture → quad 3D existe déjà (`WorldUIComponent`).

Les manques sont ciblés :

| Élément | État |
|---|---|
| Tilemap = données pures, rendu world-space | ✅ en place |
| Caméra orthographique de scène | ❌ absente — seul le hack perspective `Camera3dIn2dAxisComponent` existe |
| Garanties pixel-perfect (snap, zoom entier) | ❌ absentes |
| Tilemap librement orientable en 3D | ⚠️ rotation ignorée par le rendu |
| Composition RT → scène 3D pour tilemap | ⚠️ infrastructure prête, composant manquant |
| Mode viewport 2D dans l'éditeur | ❌ absent (viewport scène = ArcBall perspective uniquement) |

---

## État des lieux (faits vérifiés dans le code)

### 1. La tilemap est déjà un objet world-space sans logique de projection

`TileMapComponent.Draw` ([TileMapComponent.cs:218](../../CasaEngine/Framework/Scene/Entities/Components/TileMapComponent.cs)) produit des quads en **unités monde** (convention 1 unité = 1 pixel de tile, lignes vers le bas = −Y, `zOffset` par layer) :

- chemin dynamique : `Tile.Draw` → `SpriteRendererComponent.DrawSprite` ;
- chemin statique : buffers par chunk soumis via `SpriteRendererComponent.DrawStaticBatch`.

La projection est appliquée **uniquement au flush**, à partir de la vue :

- `SpriteRendererComponent.Flush` utilise `frame.View * frame.Projection` (SpriteRendererComponent.cs:124, 142) ;
- `DrawStaticBatch` utilise `frame.ViewProjection` (SpriteRendererComponent.cs:237).

Le principe « Renderer = interprétation dans l'espace donné par la caméra » est donc **déjà respecté**. Aucune modification de `TileMapComponent` n'est nécessaire pour changer d'espace de rendu : il suffit de changer la caméra de la `RenderView`.

### 2. Caméras : uniquement de la perspective

- `CameraComponent` (abstraite) → `Camera3dComponent` : `Matrix.CreatePerspectiveFieldOfView` ([Camera3dComponent.cs:38](../../CasaEngine/Framework/Scene/Entities/Components/Camera3dComponent.cs)). Le FOV est **recalculé automatiquement au resize** à partir de l'aspect ratio (`ComputeFieldOfView`, lignes 53–57).
- Les seules projections orthographiques du dépôt sont hors scène : UI (`CasaDrawTransaction`, MGUI, NvgSharp) et `ShadowPass`.
- `Camera3dIn2dAxisComponent` ([Camera3dIn2dAxisComponent.cs:36-47](../../CasaEngine/Framework/Scene/Entities/Components/Camera3dIn2dAxisComponent.cs)) implémente le mode « pixel-perfect à distance calculée » : `z = (ScreenSizeHeight/2) / tan(FOV/2)`. C'est exactement l'approche identifiée comme **fragile** :
  - dépend de `Game.ScreenSizeHeight` (taille globale du jeu, pas de la vue) ;
  - dépend d'un FOV lui-même recalculé à chaque resize ;
  - aucun zoom propre, précision float, profondeur perspective non uniforme sur les layers à `zOffset` ≠ 0.
- `TileMapDemo` utilise cette caméra avec une cible au centre de la fenêtre en pixels (TileMapDemo.cs:91-100) — confirmation de la convention « monde = pixels » pour la 2D.

### 3. Screen-space 2D : existe, mais séparé (et c'est bien)

- `Renderer2DComponent` : SpriteBatch en coordonnées écran, ignore totalement les matrices de la frame (Renderer2DComponent.cs:127) — HUD/debug/screen-space pur.
- UI MGUI : composée par vue via `IUICompositionService` **après** le flush world (`DefaultViewPipeline` étape 3), avec sa propre ortho off-center.

Ces chemins sont sains pour du screen-space. La tilemap ne doit **pas** y passer.

### 4. Render-to-texture : l'infrastructure existe déjà en double

- **Multi-vues** : `RenderView` (World + Camera + Surface) + `RenderTargetSurface` + `IViewPresenter` + modes `RealTime` / `OnDemand` / `Throttled`. Point clé : `RenderPipeline.Render` traite **les vues RT avant les vues backbuffer** (RenderPipeline.cs:117-119) — l'ordonnancement nécessaire à la composition « RT d'abord, scène ensuite » est déjà garanti.
- **`WorldUIComponent`** ([WorldUIComponent.cs](../../CasaEngine/Framework/UI/WorldUIComponent.cs)) : rend une UI dans un `RenderTargetSurface` et binde la texture sur un matériau de quad 3D (`UnlitTextureMaterial` / `LitDiffuseMaterial`), avec `GraphicsStateGuard` et rendu avant la passe monde (`World.DrawWorldUIToTextures`, appelé en tête de `RenderPipeline.Render`). **C'est le précédent architectural exact du mode 4 pour la tilemap.**

### 5. Modèle de profondeur 2D : déjà riche

`RenderPass2D`, `TileMapDepthRole`, sorting layers, Y-sort, `RenderSortKey2D` ([TileMapDepthSettings.cs](../../CasaEngine/Framework/Assets/TileMap/TileMapDepthSettings.cs)) : l'interprétation « 2D » de la profondeur est déjà bien développée et n'est pas remise en cause par cette analyse.

### 6. Éditeur

- **`TileMapEditorPanel.RenderTileMap`** (TileMapEditorPanel.cs:542-587) : SpriteBatch direct dans un RT privé avec matrice zoom/offset maison. Entièrement **hors pipeline moteur** : pas d'autotiles/tiles animées runtime, pas de depth roles, culling dupliqué. Acceptable pour un éditeur d'asset, mais chemin visuel divergent (risque WYSIWYG).
- **Viewport scène** : `ArcBallCameraComponent` (perspective) piloté par `EditorViewportCameraController` (type concret câblé en dur), overlays via `OverlayViewPipeline`. Aucun mode 2D orthographique.

---

## Constats critiques

### C1 — Pas de caméra orthographique de scène

C'est le manque principal. Sans elle, le seul moyen d'afficher une tilemap « à l'échelle » est le hack perspective `Camera3dIn2dAxisComponent` (fragile, cf. §2).

### C2 — Le rendu tilemap ignore la rotation du composant

- chemin dynamique : utilise `Position` et `Scale.ToVector2()` seulement (TileMapComponent.cs:231-232) ;
- chemin statique : `world = CreateScale(sx, sy, 1) * CreateTranslation(...)` (TileMapComponent.cs:907).

La bounding box utilise pourtant `WorldMatrixWithScale` (rotation incluse). Conséquence : la tilemap **n'est pas encore un vrai objet 3D librement orientable** (mur, sol, panneau incliné impossibles), et rendu/bounds peuvent diverger si une rotation est posée.

### C3 — Le culling suppose une map axis-alignée à Z fixe

`TryGetVisibleTileRange` unprojette les coins du viewport vers le plan `Z = Position.Z` (TileMapComponent.cs:1056-1170). Correct pour les modes 1–2 et pour une perspective face à la map ; **invalide dès qu'une rotation libre est appliquée**. Un support 3D complet exige un fallback (culling par chunk `BoundingBox` contre frustum — les chunks ont déjà `UpdateWorldBounds`).

### C4 — Pixel-perfect non garanti

Points acquis : `SamplerState.PointClamp` (sprites et static batches). Points manquants :

- pas de snapping de la caméra sur la grille texel ;
- pas de notion de zoom entier ;
- `RenderView.ResolutionScale` ≠ 1 casse silencieusement le 1:1 texel/pixel ;
- aucune règle documentée (demi-texel, tailles impaires de viewport).

### C5 — Éditeur sans mode 2D

Éditer une scène 2D dans un viewport ArcBall perspective est pénible et ne montre jamais le rendu pixel-perfect réel.

---

## Architecture cible dans CasaEngine

Aucun bouleversement : chaque responsabilité du modèle cible a déjà son emplacement.

```text
Tilemap = données          → TileMapData / TileSetData / TileMapComponent   (inchangé)
Camera = projection        → nouveau Camera2dComponent (ortho)             (ajout)
Renderer = interprétation  → SpriteRendererComponent consomme RenderFrame  (inchangé)
RenderPipeline = ordre     → RenderPipeline / RenderView                   (inchangé ; RT déjà avant BB)
```

### Mode 1 — Screen-space 2D

Réservé à l'UI/HUD (`Renderer2DComponent`, MGUI). **Ne pas y router la tilemap.** Rien à faire.

### Mode 2 — World-space 2D pixel-perfect (mode nominal jeu 2D)

Nouveau `Camera2dComponent` (`CameraComponent` → projection `Matrix.CreateOrthographicOffCenter`) :

- position/cible 2D en unités monde, `Zoom` (avec option « entier seulement ») ;
- option `PixelSnap` : arrondi de la position caméra à la grille texel **au calcul de la view matrix uniquement** (jamais dans les données gameplay) ;
- near/far explicites (ex. −1000/+1000) englobant les `zOffset` de layers **sans** déformation perspective — les layers gardent leur ordre de profondeur mais tous à l'échelle 1:1 (c'est l'avantage décisif sur `Camera3dIn2dAxisComponent`) ;
- `OnScreenResized` : recalcul de l'ortho à la taille de la vue (pas de FOV).

Sérialisation : `ElementFactory` résout les composants par nom de type via réflexion — l'ajout est purement additif, aucun registre à modifier. `Camera3dIn2dAxisComponent` est conservé tel quel pour compatibilité (à documenter comme legacy).

La tilemap et le pipeline ne changent pas : on assigne simplement cette caméra à la `RenderView`.

### Mode 3 — Tilemap dans le monde 3D

La tilemap sous caméra perspective fonctionne déjà « de fait » (quads monde). Pour en faire un objet 3D de plein droit :

1. utiliser la **matrice monde complète** (rotation incluse) dans les deux chemins de rendu (C2) — le chemin statique prend déjà une matrice, extension triviale ; le chemin dynamique nécessite une surcharge `DrawSprite` acceptant une world matrix ou une transformation des positions ;
2. **fast path préservé** : si la rotation est l'identité, garder le chemin actuel (culling plan + arithmétique simple) pour ne rien régresser en 2D ;
3. si rotation ≠ identité : désactiver `TryGetVisibleTileRange` et cull par `BoundingBox` de chunk contre le frustum (C3).

Le « pixel-perfect à distance calculée dans une perspective » reste possible (Camera3dIn2dAxis) mais documenté comme fragile, jamais comme voie recommandée.

### Mode 4 — Render-to-texture (mixage robuste 2D pixel-perfect + 3D)

Nouveau `TileMapSurfaceComponent` calqué sur `WorldUIComponent` :

```text
TileMapSurfaceComponent
    - RenderTargetSurface (taille = map en pixels, ou fenêtre de vue)
    - caméra ortho interne (Camera2dComponent, PixelSnap actif)
    - rend la tilemap (chemins de rendu existants) dans le RT
    - binde la texture sur UnlitTextureMaterial / LitDiffuseMaterial d'un quad 3D
    - invalidation : SetTile / autotile / tile animée → re-rendu ; sinon OnDemand
```

Deux options d'implémentation, par ordre de préférence :

- **Option A (recommandée)** : `RenderView` dédiée avec `RenderTargetSurface` + `Camera2dComponent` + `UpdateMode.OnDemand`, `Invalidate()` déclenché par les mutations de tiles. Réutilise clear/state-guard/stats du pipeline, et l'ordre RT-avant-backbuffer existant. Nécessite de pouvoir restreindre la vue au rendu de la tilemap seule (monde de preview dédié, ou filtre de rendu — point de design à trancher en implémentation).
- **Option B** : passe offscreen autonome dans le composant (comme `WorldUIComponent.DrawToTexture`), appelée avant la passe monde. Plus simple mais duplique la gestion clear/état.

Bénéfice : la tilemap est **toujours** rendue dans son espace naturel (ortho pixel-perfect) ; c'est la *texture résultat* qui vit en 3D. Aucune règle de projection hybride à inventer.

### Règle de projection (à documenter dans `docs/`)

> Une tilemap est un objet world-space. Son espace d'affichage est décidé exclusivement par la caméra de la vue (ortho = 2D pixel-perfect, perspective = objet 3D) ou par un RT intermédiaire (mixage). Aucune logique d'espace/projection ne doit entrer dans `TileMapComponent` ni dans les assets TileMap.

---

## Intégration éditeur

### E1 — Mode viewport 2D (priorité éditeur)

- Bascule 2D/3D par viewport : en mode 2D, la vue utilise `Camera2dComponent` + un contrôleur pan (drag molette/clic milieu) / zoom (molette, crans entiers en mode pixel-perfect).
- `EditorViewportCameraController` est câblé sur `ArcBallCameraComponent` (type concret) : ajouter un contrôleur parallèle `EditorViewport2dCameraController` plutôt que refactorer l'existant ; la bascule choisit le contrôleur et la caméra.
- Grille : graduations en tiles/pixels en mode 2D (la grille actuelle est pensée monde 3D).
- Gizmos contraints à XY (+ `zOffset` éventuellement via panneau, pas via gizmo).
- Persistance de l'état de vue (mode + position/zoom) avec le layout éditeur, comme l'état ArcBall actuel.

### E2 — TileMapEditorPanel : court terme inchangé, moyen terme WYSIWYG

- **Court terme** : conserver le SpriteBatch maison (rapide, isolé, suffisant pour peindre).
- **Moyen terme** : remplacer la préview par une `RenderView` (RT surface + `Camera2dComponent` + monde de preview dédié + `OnDemand`), comme les thumbnails de particules/sprites utilisent déjà des scènes de preview. Gains : autotiles, tiles animées, depth roles et rendu réel visibles dans l'éditeur ; suppression du culling dupliqué. C'est la continuation naturelle de la phase 9 de [tilemap-modernization-tasks.md](../tasks/tilemap-modernization-tasks.md).

### E3 — Choix du mode par la donnée de scène (pas par le code)

Le mode de rendu d'une tilemap placée en scène découle de la caméra de la vue — donc du **composant caméra choisi dans la scène**, pas d'un flag sur la tilemap. Seul le mode 4 introduit un composant dédié (`TileMapSurfaceComponent`), qui est un choix d'auteur de scène explicite.

---

## Plan de phases proposé

### Phase A — `Camera2dComponent` (runtime) — priorité haute, faible risque

- [ ] Créer `Camera2dComponent` : ortho off-center, Zoom, PixelSnap, near/far explicites, resize correct.
- [ ] Tests unitaires : matrices attendues (viewport donné → projection), zoom, snap, resize.
- [ ] Adapter `TileMapDemo` (ou variante) pour utiliser `Camera2dComponent`.
- **Done** : la démo tilemap rend à l'identique du mode actuel, puis reste stable en zoom ×1/×2/×3 et au resize (ce que le hack perspective ne garantit pas).

### Phase B — Politique pixel-perfect — priorité haute

- [ ] Snapping caméra dans la view matrix uniquement ; documenter la règle demi-texel.
- [ ] Interaction avec `ResolutionScale` : en mode pixel-perfect, forcer scale 1 (ou zoom entier équivalent) et le documenter.
- [ ] Debug overlay optionnel : indicateur « pixel-perfect OK / dégradé » sur la vue.
- **Done** : checklist pixel-perfect documentée dans `docs/`, vérifiée sur la démo.

### Phase C — Tilemap objet 3D complet — priorité moyenne

- [ ] World matrix complète (rotation) sur les deux chemins de rendu ; fast path identité préservé.
- [ ] Culling fallback par chunk bounds vs frustum quand rotation ≠ identité.
- [ ] Vérifier tri/alpha : le blend state sprite actuel (`ColorDestinationBlend = Zero`, depth write on) est de fait opaque — évaluer le comportement des bords transparents de tiles sous perspective avec z variables.
- **Done** : démo avec tilemap au sol + tilemap murale (rotation 90°) sous caméra perspective, culling correct, pas de régression perf sur le chemin 2D (compteurs `LastVisitedChunkCount`/`LastDrawnTileCount` inchangés en mode identité).

### Phase D — `TileMapSurfaceComponent` (RT → quad 3D) — priorité moyenne

- [ ] Implémenter selon l'option A (RenderView OnDemand) ; trancher le point « restreindre la vue à la tilemap » (monde de preview vs filtre).
- [ ] Invalidation branchée sur `SetTileReference`/autotiles/tiles animées (tick animé → `Invalidate` throttlé).
- [ ] Démo : écran d'arcade / minimap affichant une tilemap pixel-perfect dans une scène 3D perspective.
- **Done** : la tilemap RT reste nette (point sampling, zoom entier interne) quelle que soit la caméra 3D ; map statique = zéro re-rendu par frame.

### Phase E — Éditeur : viewport 2D — priorité moyenne (après A/B)

- [ ] `EditorViewport2dCameraController` (pan/zoom crans entiers) + bascule 2D/3D par viewport.
- [ ] Grille 2D en tiles/pixels ; gizmos contraints XY.
- [ ] Persistance de l'état de vue 2D dans le layout.
- **Done** : ouvrir une scène 2D, basculer le viewport en 2D, peindre/placer avec un rendu identique au runtime.

### Phase F — Documentation — courte, en continu

- [ ] Page `docs/` « Espaces de rendu 2D/3D » : la règle de projection, les 4 modes, quand utiliser quoi, checklist pixel-perfect.
- [ ] Marquer `Camera3dIn2dAxisComponent` comme legacy dans la doc (sans le déprécier dans le code).

---

## Risques et points de vigilance

- **Compatibilité** : ne pas modifier `Camera3dIn2dAxisComponent` (comportement resize/FOV utilisé par les démos). Tous les ajouts sont additifs ; `ElementFactory` résout par nom de type (insensible à la casse, premier trouvé) → choisir des noms uniques (`Camera2dComponent` est libre à ce jour).
- **Perf chemin 2D** : la Phase C ne doit introduire ni allocation ni coût matrice supplémentaire quand la rotation est l'identité (fast path explicite ; les chemins `Draw` sont des hot paths).
- **Blend/depth des sprites** : le blend state effectivement opaque masque aujourd'hui les problèmes de tri alpha ; la 3D libre (Phase C) peut les révéler. À traiter comme point de design rendu, pas comme un bug tilemap.
- **`ResolutionScale`** et surfaces RT : le pixel-perfect impose un contrat (scale 1, tailles entières) à documenter et vérifier, sinon les utilisateurs verront du blur inexpliqué.
- **Éditeur** : ne pas refactorer `EditorViewportCameraController` (stabilisé, testé) ; contrôleur 2D parallèle.

## Ce qu'il ne faut pas faire

- Mettre un flag « screen-space » ou une logique de projection dans `TileMapComponent` ou les assets TileMap — l'espace est une propriété de la **vue**, pas de la donnée.
- Router la tilemap par `Renderer2DComponent`/SpriteBatch écran — chemin divergent, perte des chunks statiques et du depth model.
- Créer un « TileMapRenderer3D » séparé — les deux chemins existants (dynamique + static batch) couvrent tous les modes une fois la world matrix complète supportée.
- Déprécier `Camera3dIn2dAxisComponent` dans le code tant que les démos/projets l'utilisent.

## Prochaine étape utile

Phase A (`Camera2dComponent`) : petite, additive, à fort levier — elle débloque les modes 2 et 4 et le viewport 2D éditeur, et fournit immédiatement la solution propre au pixel-perfect que le hack perspective n'offre pas.
