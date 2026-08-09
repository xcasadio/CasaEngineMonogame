# Tâches — Espaces de rendu TileMap (2D pixel-perfect / monde 3D)

Plan d'exécution issu de l'analyse [analysis-tilemap-render-spaces.md](../audits/analysis-tilemap-render-spaces.md).
Lire l'analyse avant de commencer : elle contient l'état des lieux, les références de fichiers et les décisions de design.

Légende : ⏳ Todo · 🚧 In progress · 🧪 Needs testing (validation visuelle/manuelle restante) · ✅ Done · ⚠️ Blocked

---

## Règles d'exécution pour l'agent

1. **Une tâche = un commit.** Ne jamais regrouper plusieurs tâches dans un commit. Chaque commit doit compiler.
2. **Avant chaque commit** :
   - build : `dotnet build CasaEngine.MonoGame.sln` (phases A–D, F) et/ou `dotnet build CasaEngine.Editor.MonoGame.sln` (phase E, et toute tâche touchant `CasaEngine.Editor`) ;
   - tests : `rtk dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj` (le projet de tests n'est dans aucune `.sln`) ;
   - mettre à jour l'icône de statut de la tâche **dans ce fichier** et inclure cette mise à jour dans le même commit.
3. **Message de commit** : impératif court, suffixé de l'id de tâche. Exemple : `Add Camera2dComponent orthographic scene camera (A1)`.
4. **Statuts** : ✅ si la validation est entièrement automatisée (build + tests verts). 🧪 si le done-criteria exige une validation visuelle/manuelle (démos, éditeur) — noter alors ce qui reste à valider sous la tâche. ⚠️ si bloqué — noter la cause précise et passer à la tâche suivante indépendante.
5. **Jamais de push. Jamais de rebase.** Travailler sur la branche `tilemap-render-spaces`.
6. **Périmètre strict** : ne pas refactorer l'existant. Ne pas modifier `Camera3dIn2dAxisComponent`, `EditorViewportCameraController`, ni les formats d'assets sérialisés. Tous les ajouts sont additifs.
7. **Hot paths** (`Update`, `Draw`, flush, culling) : pas de LINQ, pas d'allocations par frame, pas de closures — cf. règles du dépôt.
8. Suivre le style des fichiers voisins (nommage, XML doc sur les API publiques, pas de commentaires superflus).
9. **Baseline tests (constatée le 2026-08-09, avant ce chantier ; corrigée après vérification : 18 échecs uniques, pas 19)** : 18 échecs préexistants sur 783 (`CasaMguiBackendOwnershipTests` ×5, `EditorControlTemplateAssetLoadingTests` ×8, `CutsceneDirectorTests.Play_MoveToActionAdvancesPositionInRuntimeUpdateOrder`, `LightOverlayTests.LightOverlayIcons_AreExposedAndLoadedByEditorIcons`, `EditorAssetWriterServiceTests.SaveAsset_WithEntitySceneTransforms_PersistsRootAndChildCoordinates`, `MaterialDefinitionEditorRegistryTests.GetDescriptors_LitDiffuseDefinition_UsesSemanticGroupsAndControlHints`, `MonoGameBasicEffectUsageTests.RuntimeAndToolingSources_DoNotReferenceMonoGameBasicEffect`, `EditorControlTemplateAssetLoadingTests` = 7 échecs et non 8). **Ne pas les corriger, ne pas les compter comme régressions.** Critère : aucun échec *nouveau* par rapport à cette liste. Test flaky connu (échoue parfois en suite complète, passe isolé) : `ParticleEffectAssetJsonSerializerTests.SampleProjectParticleAssets_LoadThroughAssetContentManager` — relancer avant de conclure à une régression.

---

## Phase A — `Camera2dComponent` (caméra orthographique de scène)

### ✅ A1 — Créer `Camera2dComponent`

Nouveau fichier `CasaEngine/Framework/Scene/Entities/Components/Camera2dComponent.cs`.

- Hérite de `CameraComponent` (pas de `Camera3dComponent` : pas de FOV).
- Projection : `Matrix.CreateOrthographic(viewportWidth / Zoom, viewportHeight / Zoom, near, far)` avec near/far issus du viewport (`MinDepth`/`MaxDepth`) — les défauts de `CameraComponent.InitializeWithWorld` conviennent.
- Vue : `Matrix.CreateLookAt(position, Target, Vector3.Up)` avec `position = (Target.X, Target.Y, Target.Z + distance fixe)` — même convention d'axes que `Camera3dIn2dAxisComponent` (monde = pixels, +Y vers le haut).
- Propriétés : `Target` (Vector3), `Zoom` (float, défaut 1, clampé > 0), `PixelSnap` (bool, défaut false).
- `PixelSnap` : arrondir la position/cible à la grille texel (pas de 1/Zoom) **au calcul de la view matrix uniquement** — ne jamais modifier `Target` stocké.
- **Exigence d'équivalence** : à `Zoom = 1`, un point du plan `Z = Target.Z` doit se projeter à l'écran au même pixel qu'avec `Camera3dIn2dAxisComponent` (même cible, mêmes dimensions de viewport). C'est le contrat qui garantit A3.
- `OnScreenResized` : recalculer la projection (pas de FOV impliqué).
- `Clone()`, constructeur de copie, `Load(JObject)`/sauvegarde sur le modèle des composants caméra voisins. `ElementFactory` résout par nom de type : aucun enregistrement nécessaire.
- `SetPositionAndTarget` : aligner sur le comportement de `Camera3dIn2dAxisComponent` (déplace `Target`).

Done : build vert ; le composant est créable, clonable, sérialisable.

### ✅ A2 — Tests unitaires `Camera2dComponent`

Nouveau fichier dans `CasaEngine.Tests` (suivre l'arborescence existante, ex. `CasaEngine.Tests/Scene/Camera2dComponentTests.cs` ou dossier équivalent existant).

Cas à couvrir :
- matrice de projection attendue pour viewport/zoom donnés (dimensions de l'ortho = viewport/zoom) ;
- `Zoom` clampé (0 et négatif rejetés) ;
- `PixelSnap` : la view matrix change par crans texel, `Target` stocké inchangé ;
- resize : la projection suit les nouvelles dimensions ;
- **équivalence** : projection d'un point du plan cible identique (à epsilon près) entre `Camera2dComponent` (Zoom 1) et `Camera3dIn2dAxisComponent` — si l'instanciation de `Camera3dIn2dAxisComponent` hors monde est impraticable en test, valider contre la formule perspective calculée à la main et le documenter dans le test.

Done : `rtk dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj` vert (nouveaux tests inclus).

### 🧪 A3 — Basculer `TileMapDemo` sur `Camera2dComponent`

Dans `CasaEngine.Demos/Demos/TileMapDemo.cs` : remplacer `Camera3dIn2dAxisComponent` par `Camera2dComponent` (même cible). Ne toucher à aucune autre démo.

Done : build `CasaEngine.MonoGame.sln` vert. 🧪 restant : lancer la démo et vérifier un rendu identique à l'ancien mode, stable en resize.
Point de vigilance (verifier) : le cadrage X/Y est équivalent mais la fenêtre de profondeur change — ortho = [Target.Z−500, Target.Z+499] (distance interne fixe 500) vs la fenêtre perspective legacy. Vérifier aussi qu'aucun contenu en Z n'est clippé ; un `FarPlane` < 500 clipperait le plan cible.

---

## Phase B — Politique pixel-perfect

### ✅ B1 — Diagnostic de dégradation pixel-perfect

Quand une `RenderView` utilise une `Camera2dComponent` avec `PixelSnap` actif **et** que `ResolutionScale != 1` ou que `Zoom` n'est pas entier : logger un avertissement **une seule fois par vue** (pas par frame — garder un flag sur la vue ou un HashSet côté diagnostic). Emplacement suggéré : `RenderPipeline` au moment d'`ApplyResolutionScale`, ou `RenderFrameFactory`.

Done : build + tests verts ; test unitaire du prédicat de dégradation si extractible en logique pure.

### 🧪 B2 — Indicateur `DebugOverlay`

Dans `DebugOverlay` : si la caméra de la vue est une `Camera2dComponent`, afficher une ligne `PixelPerfect: OK` / `PixelPerfect: degraded (raison)` en réutilisant le prédicat de B1. Aucune allocation par frame (précomposer les chaînes).

Done : build vert. 🧪 restant : vérification visuelle de l'overlay sur une démo (ligne affichée uniquement quand la caméra de la vue est une `Camera2dComponent`, chaînes issues de `PixelPerfectDiagnostics.DescribeOverlayLine`).

---

## Phase C — TileMap objet 3D complet

### ✅ C1 — Matrice monde complète (rotation) dans les deux chemins de rendu

Dans `TileMapComponent.Draw` ([TileMapComponent.cs:218](../../CasaEngine/Framework/Scene/Entities/Components/TileMapComponent.cs)) :

- **Fast path préservé** : si la rotation monde est l'identité (à epsilon près), garder exactement le chemin actuel (positions x/y + scale). Zéro régression perf : pas d'allocation ni de produit matriciel supplémentaire par tile.
- **Chemin statique** : construire la matrice monde avec la rotation (la géométrie des chunks est en espace local — remplacer `CreateScale * CreateTranslation` par la matrice monde complète du composant).
- **Chemin dynamique** : quand rotation ≠ identité, transformer les quads par la matrice monde. `SpriteDisplayData` porte déjà une `WorldMatrix` par sprite — ajouter si nécessaire une surcharge `DrawSprite` prenant une matrice monde, sans casser les surcharges existantes.
- La bounding box (`GetBoundingBox`) utilise déjà `WorldMatrixWithScale` : vérifier la cohérence rendu/bounds avec rotation.

Done : build + tests TileMap existants verts ; nouveaux tests sur la construction de matrice si extractible.

**Notes de sémantique (différences assumées entre les deux chemins)** :

- **`zOffset` de layer** : le chemin identité l'ajoute en **Z monde non scalé** (`worldZ = translation.Z + layerZ`), alors que le chemin rotation l'applique en **espace local**, donc multiplié par l'échelle du composant. Même asset, séparation de layers différente selon le chemin : à `LocalScale = 0.05` (cas de `TileMap3dDemo`), un `zOffset` de 1 ne sépare plus les layers que de 0.05 unité monde — risque de z-fighting entre layers sur une map fortement réduite. Contourner en augmentant les `zOffset` de l'asset ou l'échelle, si le cas se présente.
- **Tri des sprites dynamiques** : `SpriteRendererComponent.CompareSpriteDisplayData` trie par `WorldMatrix.Translation.Z` monde, ce qui n'est pas corrélé à la profondeur caméra dès que la map est tournée (une map au sol a tous ses quads à Z monde quasi constant, une map murale trie selon un axe orthogonal à la vue). Sans conséquence en opaque — le depth buffer tranche (`DepthBufferWriteEnable = true`, `ColorDestinationBlend = Zero`) — mais des artefacts d'ordre sont possibles sur des tiles dynamiques réellement alpha-blended. Le chemin statique (batches indexés) n'est pas concerné.

Réalisé : `TileMapComponent.Draw` teste **une fois par draw** si `WorldMatrixWithScale` est une pure échelle+translation (`IsAxisAlignedWorldMatrix`, 6 comparaisons sur les termes hors-diagonale) ; si oui le chemin existant est exécuté à l'identique. Sinon `DrawWithWorldMatrix` dessine les quads en espace local transformés par la matrice monde complète (`Tile.Draw(..., in Matrix)` + surcharge additive `SpriteRendererComponent.DrawSprite(..., in Matrix worldTransform)`). Le chemin statique reçoit désormais sa matrice monde du dispatcher (calculée une fois par layer au lieu d'une fois par chunk).

### ✅ C2 — Culling fallback par chunk quand rotation ≠ identité

`TryGetVisibleTileRange` (unproject vers plan Z) n'est valide que sans rotation :

- rotation identité → chemin actuel inchangé ;
- rotation ≠ identité → ne pas utiliser la plage plane ; culler chunk par chunk via leur `BoundingBox` monde (transformée par la matrice complète) contre `BoundingFrustum(frame.ViewProjection)`. Réutiliser/étendre `chunk.UpdateWorldBounds` ; le `BoundingFrustum` doit être mis en cache par draw (pas d'allocation par chunk).

Done : build + tests verts ; test unitaire de la logique de sélection (identité → plage plane, rotation → frustum).

Réalisé : `TileMapChunk.UpdateWorldBounds(..., in Matrix world)` (surcharge additive, bounds locales transformées par les 8 coins, sans allocation) ; `TileMapComponent` garde une instance `BoundingFrustum` réutilisée dont la `Matrix` est réaffectée une fois par draw. Tests : `CasaEngine.Tests/TileMap/TileMapCullingSelectionTests.cs`.

### 🧪 C3 — Démo `TileMap3dDemo`

Nouvelle démo dans `CasaEngine.Demos` (suivre le modèle des démos existantes, l'enregistrer là où les démos sont listées) : une tilemap au sol (rotation −90° sur X), une tilemap murale verticale, caméra perspective libre (`ArcBallCameraComponent` ou équivalent utilisé par les démos 3D).

Done : build vert. 🧪 restant : validation visuelle (culling correct en orbitant, pas de tiles manquantes/fantômes).

Réalisé : `CasaEngine.Demos/Demos/TileMap3dDemo.cs` (`map_1_1` posée deux fois : sol rotation −90° X, mur rotation −90° Y, échelle 0.05 pour rester à l'échelle métrique des démos 3D), enregistrée dans `DemosGame.LoadContentPrivate`, caméra `ArcBallCameraComponent` par défaut. La validation visuelle n'a pas pu être faite dans l'environnement de l'agent : le lancement de `CasaEngine.Demos` échoue avant tout rendu sur `FileNotFoundException: FontStashSharp.MonoGame, Version=1.5.6.0` (problème d'environnement préexistant, indépendant de ce chantier). À vérifier en orbitant : sol et mur correctement orientés, pas de tiles manquantes en bordure de frustum. Note : les collisions tilemap sont toujours générées en XY sans tenir compte de la rotation (comportement préexistant, hors périmètre) — le debug physique de la démo peut donc afficher des boîtes non alignées avec le rendu.

### ✅ C4 — Vérification perf du fast path

Vérifier par test (compteurs `LastVisitedChunkCount` / `LastDrawnTileCount` / `LastStaticBatchCount` sur une map de test, rotation identité) que le comportement de culling/batching est inchangé par rapport à avant C1/C2.

Done : test vert prouvant l'iso-comportement du chemin identité.

Réalisé : `CasaEngine.Tests/TileMap/TileMapComponentDrawCountersTests.cs` construit un `TileMapComponent` sans GraphicsDevice (world/game non initialisés par réflexion, tiles remplacées par des stubs, `BuildChunks` appelé par réflexion) et vérifie sur une map 8×8 en chunks de 2 : rotation identité sans `RenderFrame` → tous les chunks/tiles visités ; rotation identité avec `RenderFrame` ortho → la plage plane restreint bien le rendu (marge d'une tile) ; rotation ≠ identité → mêmes compteurs quand rien n'est cullé, et culling effectif par frustum quand la map devient vue par la tranche. Limite documentée : sans GraphicsDevice aucun batch statique ne peut être émis, `LastStaticBatchCount` reste donc à 0 dans ces tests.

---

## Phase D — `TileMapSurfaceComponent` (RT → quad 3D)

### ✅ D1 — Composant de rendu offscreen

Nouveau `CasaEngine/Framework/.../TileMapSurfaceComponent.cs` sur le **modèle exact de `WorldUIComponent`** ([WorldUIComponent.cs](../../CasaEngine/Framework/UI/WorldUIComponent.cs)) :

- possède un `RenderTargetSurface` (taille par défaut = taille de la map en pixels, redimensionnable) ;
- paramètres ortho internes (zoom entier, snap actif) — réutiliser `Camera2dComponent` ou des matrices ortho directes ;
- passe offscreen exécutée **avant la passe monde**, au même endroit que `World.DrawWorldUIToTextures` (ajouter un appel analogue à côté, même mécanique de liste au niveau `World`) ;
- séquence : `GraphicsStateGuard` → apply surface → clear → construire une `RenderFrame` ortho → dessiner la tilemap via les chemins existants (`TileMapComponent.Draw` + flush du `SpriteRendererComponent` avec cette frame) → restaurer. Attention : `TileMapComponent.Draw` lit `Owner.World.CurrentRenderFrame` pour le culling/batches — fournir la frame ortho pendant la passe puis restaurer la valeur précédente ;
- expose `Texture` et `BindToMaterial(UnlitTextureMaterial/LitDiffuseMaterial)` comme `WorldUIComponent` ;
- ne pas dessiner la tilemap dans la passe monde principale quand elle est routée vers la surface (flag explicite sur le composant tilemap visé, opt-in).

Done : build + tests verts.

Réalisé : `CasaEngine/Framework/Rendering/TileMapSurfaceComponent.cs`, objet possédé `IDisposable` (pas un `SceneComponent`) exactement comme `WorldUIComponent` — il n'est ni attaché à une entité ni sérialisé, il est enregistré sur le monde (`World.RegisterTileMapSurface` / `UnregisterTileMapSurface`) et sa passe est déclenchée par `World.DrawTileMapSurfacesToTextures()`, appelée dans `RenderPipeline.Render` juste à côté de `DrawWorldUIToTextures()`. Placé dans `Framework/Rendering` (c'est un aide de passe de rendu, pas un composant de scène). La frame ortho est construite directement (`CreateLookAt` + `CreateOrthographic`, mêmes conventions d'axes que `Camera2dComponent`, distance caméra 500, near/far 1/1000) et cadre exactement la map en espace monde ; la taille du RT vaut la taille de la map en pixels × `Zoom`, réduite uniformément au-delà de 4096. Pendant la passe : `GraphicsStateGuard` (qui restaure aussi render target et viewport) + `World.SetCurrentRenderFrame` (nouvelle méthode `internal` renvoyant la frame précédente) + `TileMapComponent.DrawTileMap()` + `SpriteRendererComponent.Flush(in frame)` qui vide les files pour ne pas empoisonner la passe monde. Opt-in via `TileMapComponent.SkipMainPassDraw` (bool additif, runtime only, non sérialisé, recopié par le constructeur de copie) : `Draw` sort immédiatement, la surface appelle `DrawTileMap()` qui ignore le flag.

Limites et points de vigilance D1 :

- **Rotation non supportée** : `BuildRenderFrame` cadre la map à partir de sa position et de son échelle uniquement. La tilemap routée vers une surface doit être axis-aligned (rotation identité) ; une map tournée serait cadrée de travers et rendrait de biais ou vide. Pour un affichage 3D orienté, tourner le quad qui consomme la texture, pas la map. Documenté dans la XML doc de `TileMapSurfaceComponent`.
- **Compteurs de draw** : quand `SkipMainPassDraw` est actif, `Draw` sort avant la remise à zéro des compteurs, donc `LastVisitedTileCount` / `LastDrawnTileCount` / `LastStaticBatch*` décrivent la dernière passe offscreen et non la passe monde (valeurs figées sur une map statique). Comportement volontairement inchangé (les tests C4 épinglent les valeurs du chemin normal), documenté dans la XML doc de `SkipMainPassDraw`.
- **Cycle de vie** : `World.Clear()` dispose et vide `_tileMapSurfaces` exactement comme `_worldUiComponents`, pour que les render targets repartent au pool et qu'aucune surface ne survive à un reset de monde.
- **Perf** : la référence au `SpriteRendererComponent` est mise en cache (clé = instance de `CasaEngineGame`) au lieu d'être relookée à chaque redraw — une map animée redessine chaque frame.

### ✅ D2 — Invalidation à la demande

La surface ne se re-rend que si : mutation de tiles (`SetTileReference` et compagnie), tiles animées présentes (tick → re-rendu), autotiles dirty, ou `Invalidate()` explicite. Map statique = zéro re-rendu par frame.

Done : test unitaire du dirty-tracking (mutation → re-rendu au frame suivant ; rien → pas de re-rendu).

Réalisé : `TileMapComponent.TileRevision` (compteur `uint` additif) est incrémenté par `SetTileReference` (uniquement quand la tuile ou ses flags changent réellement), par le rafraîchissement des autotiles dans `Update` et par `InitializeWithWorld`. `TileMapComponent.HasAnimatedTiles` / `HasDirtyAutoTiles` exposent l'état des tuiles animées et des autotiles sales. Le prédicat `TileMapSurfaceComponent.ShouldRedraw(neverRendered, invalidated, hasAnimatedTiles, hasDirtyAutoTiles, tileRevision, lastRenderedTileRevision)` est `internal static` et sans dépendance graphique ; une map statique déjà rendue coûte une comparaison d'entiers par frame et zéro travail GPU. Tests : `CasaEngine.Tests/TileMap/TileMapSurfaceInvalidationTests.cs` (6 cas sur le prédicat, mutation vs no-op sur `TileRevision`, `Draw` ne bouge pas la révision, `SkipMainPassDraw`, calcul de la taille de surface).

Note d'historique : le code de D2 (`TileRevision`, `HasAnimatedTiles`, `HasDirtyAutoTiles`, `ShouldRedraw`) a été livré dans le commit D1 `Add TileMapSurfaceComponent offscreen tilemap rendering (D1)` — la passe offscreen ne pouvait pas compiler sans son prédicat de redraw. Le commit D2 n'apporte donc que les tests.

Limitation : seules les mutations passant par `TileMapComponent.SetTileReference` / `SetTileFlags` / `RemoveTile` bumpent `TileRevision`. Une écriture directe sur l'asset (`TileMapData.SetTile*`) contourne le composant : les chunks ne sont pas invalidés et la surface reste sur son dernier rendu. Appeler `TileMapSurfaceComponent.Invalidate()` explicitement dans ce cas.

Note : `Update` vide `_dirtyAutoTiles` avant la phase de dessin, donc `HasDirtyAutoTiles` est presque toujours faux au moment de la passe offscreen — c'est l'incrément de `TileRevision` fait dans `Update` qui déclenche réellement le re-rendu après un refresh d'autotiles. Le paramètre est conservé dans le prédicat pour couvrir un appel de passe avant `Update`.

### 🧪 D3 — Démo écran/minimap

Démo : un quad 3D dans une scène perspective (type écran d'arcade ou minimap) affichant la texture de la surface tilemap, avec `SamplerState.PointClamp` sur le matériau.

Done : build vert. 🧪 restant : validation visuelle (netteté du RT quelle que soit la caméra 3D).

Réalisé : `CasaEngine.Demos/Demos/TileMapSurfaceScreenDemo.cs`, enregistrée dans `DemosGame.LoadContentPrivate` juste après `TileMap3dDemo`. `map_1_1` (30 × 11 tuiles de 32 px) est posée dans le monde avec `SkipMainPassDraw = true`, rendue offscreen par un `TileMapSurfaceComponent` (clear noir, zoom 1 → RT de 960 × 352), et sa texture est affichée sur un quad `PlanePrimitive` de ratio identique porté par un `UnlitTextureMaterial` avec `SamplerState = SamplerState.PointClamp`. La validation visuelle n'a pas pu être faite dans l'environnement de l'agent (même blocage que C3 : `FileNotFoundException: FontStashSharp.MonoGame` au lancement de `CasaEngine.Demos`). À vérifier : la map apparaît nette et à l'endroit sur l'écran, aucune tuile n'est dessinée dans la scène 3D elle-même, et l'orientation UV du quad ne retourne pas l'image.

Note : l'entité tilemap est placée en `(0, -5000, 0)`. Elle reste un objet de scène à l'échelle pixel et génère ses corps de collision ; à l'origine elle remplissait la zone jouable de 960 × 352 unités de colliders invisibles. Le cadrage de la passe offscreen étant relatif à la position de la map, le rendu du RT est identique. Un mode « tilemap purement offscreen » sans génération de collisions serait la vraie solution — hors périmètre de cette phase.

---

## Phase E — Éditeur : viewport 2D

Build de référence pour cette phase : `dotnet build CasaEngine.Editor.MonoGame.sln`.

### ✅ E1 — `EditorViewport2dCameraController`

Nouveau contrôleur dans `CasaEngine.Editor/Runtime/` (parallèle à `EditorViewportCameraController`, **sans le modifier**) :

- crée/pilote une `Camera2dComponent` ;
- pan : drag clic milieu ; zoom : molette par crans entiers (×1, ×2, ×3, … et fractions ½, ¼ si simple) centré sur le curseur ;
- état capturable/restaurable (`CaptureState`/`RestoreState`) comme le contrôleur ArcBall.

Done : build éditeur vert ; tests unitaires du contrôleur sur le modèle d'`EditorViewportCameraControllerTests`.

Réalisé : `CasaEngine.Editor/Runtime/EditorViewport2dCameraController.cs` (+ `EditorViewport2dCameraState`), strictement parallèle au contrôleur ArcBall qui n'est pas touché. Zoom par crans entiers : `ZoomFromStep(step)` → `step >= 0` donne ×1, ×2, ×3… et `step < 0` donne ½, ⅓, ¼… ; crans bornés à `[-7, 31]` (1/8 → ×32). `ZoomAtCursor` conserve le point monde sous le curseur (`Target += offsetVue * (1/zoomAvant − 1/zoomAprès)`, Y écran inversé). Pan au drag clic milieu (`Target -= delta / Zoom`, Y inversé) ; le delta n'est appliqué qu'à partir de la deuxième frame du drag pour éviter un saut au clic. `CaptureState`/`RestoreState`/`SetState`/`Focus` comme l'ArcBall. Tests : `CasaEngine.Tests/Editor/EditorViewport2dCameraControllerTests.cs` (table des crans, bornes, invariance du point sous le curseur, pan, aller-retour capture/restore, conversion delta molette → crans), sans GraphicsDevice.

Correctif post-vérification : `PixelSnap` du contrôleur vaut désormais **`false` par défaut**. Avec le snap actif, la caméra est quantifiée sur la grille texel et le zoom centré curseur dérive (mesuré : cible (123.5, −47.25), viewport 640×480, curseur (500, 100), crans 0 → 2 → reprojection à (501.999, 98.999)). La dérive est inhérente au snap (bornée par ~0.5 × zoomAprès / zoomAvant + 0.5 pixel écran par changement) : c'est acceptable pour prévisualiser le rendu pixel-perfect, pas pour naviguer en édition. Le snap reste opt-in. Le défaut correspondant de `pixel_snap` dans `EditorViewportViewStateSerializer` a été aligné sur `false`.

L'invariance du point sous le curseur n'est plus vérifiée contre une ré-implémentation de la formule du contrôleur (test qui ne pouvait pas échouer) mais contre les **vraies matrices** de `Camera2dComponent` : `Viewport.Unproject` du curseur sur le plan cible avant le zoom, puis `Viewport.Project` du point monde obtenu après le zoom — écart < 0.01 px, sur 4 crans (±1, +2, −3). Un test séparé documente le comportement avec `PixelSnap = true` (dérive bornée, assertion large : comportement connu, pas un bug).

### 🧪 E2 — Bascule 2D/3D par viewport

Dans le viewport scène de l'éditeur (repérer `EditorViewContext` et la création des vues) : une bascule 2D/3D qui échange caméra + contrôleur, en préservant l'état de l'autre mode. UI minimale (bouton/raccourci dans la barre du viewport, suivre les patterns existants).

Done : build éditeur vert. 🧪 restant : bascule aller-retour sans perte d'état, rendu 2D correct.

Réalisé : `WorldViewportPanel` porte désormais deux caméras et deux contrôleurs indépendants — `ArcBallCameraComponent` + `EditorViewportCameraController` (inchangés) et `Camera2dComponent` + `EditorViewport2dCameraController`. La bascule passe par `WorldViewportPanel.Is2dViewMode` / `SetViewMode(bool)` : elle crée la caméra 2D à la demande (entité cachée `EditorViewport2dCamera`, parallèle à `EditorViewportCamera`), réaffecte `RenderView.Camera` et invalide la vue. **La préservation de l'état de l'autre mode est structurelle** : chaque contrôleur garde son propre état, il n'y a donc rien à capturer/restaurer au moment de la bascule (`CaptureState`/`RestoreState` restent utilisés pour la persistance E4 et le mode preview existant). UI : bouton bascule « 2D » ajouté à la barre d'outils de viewport existante (`BuildGizmoToolbar`, après un séparateur), donc visible uniquement là où `ShowGizmoToolbar` est actif, c'est-à-dire le viewport monde de l'éditeur.

Correctif post-vérification (préview) : `SetWorldOverride` recadrait inconditionnellement le contrôleur 3D sans consulter le mode du panneau, si bien qu'entrer en préview depuis le mode 2D laissait la caméra ortho liée à la vue et perdait le recadrage préview. Le mode effectif est désormais porté par un seul prédicat, `WorldViewportPanel.UsesCamera2d` = `_is2dViewMode && !HasWorldOverride` : **une préview est une vue de type runtime et rend toujours par la caméra perspective**, quel que soit le mode du panneau ; à la sortie de l'override la vue revient à la caméra du mode courant (et la caméra 2D est créée à ce moment si besoin). Toutes les bascules (input, `SynchronizeCamera`, `FocusBounds`, grille, contrainte gizmo XY, création de la vue) passent par ce prédicat, donc plus aucun chemin ne peut diverger. Basculer le mode pendant une préview est mémorisé et appliqué à la sortie.

Points intégrés au passage : `EditorViewportGizmoController.EnsureInitialized`/`Synchronize`/`Update` acceptent maintenant `CameraComponent` au lieu du type concret `ArcBallCameraComponent` (élargissement, aucun appelant cassé — le gizmo ne lisait déjà que `ViewMatrix`/`ProjectionMatrix`/`Position`) ; le resize, le changement de monde et `FocusBounds` pilotent la caméra du mode actif ; le délégué de relâchement d'input est mis en cache dans un champ au lieu d'être alloué à chaque frame.

### 🧪 E3 — Grille 2D et contrainte gizmo XY

- En mode 2D : grille graduée en tiles/pixels (adapter le rendu de grille existant, version minimale acceptable) ;
- gizmos de translation contraints au plan XY en mode 2D.

Version minimale acceptable ; si le système de grille/gizmo résiste, marquer ⚠️ avec le point de blocage précis et ne pas forcer un refactor.

Done : build éditeur vert. 🧪 restant : vérification visuelle.

Réalisé :

- **Grille** : `GridComponent.DrawForView2d(gd, in frame, tileSizeInPixels = 32)` (ajout additif, la grille 3D `DrawForView` est inchangée — les deux passent par le même `DrawLines` privé). Grille dans le plan XY graduée en tuiles (32 px par défaut, une ligne accentuée toutes les 8 tuiles, axes X rouge / Y vert), même nombre de lignes que la grille 3D (±50 tuiles, soit ±1600 px). Le tableau de sommets 2D est construit une seule fois (reconstruit uniquement si la taille de tuile change), aucune allocation par frame. `WorldViewportPanel.RenderEditorGrid` (groupe de méthodes, plus de closure) choisit la grille selon le mode du viewport.
- **Gizmo XY** : `Gizmo.ConstrainToXYPlane` (nouvelle propriété opt-in, défaut `false` → comportement 3D strictement inchangé) fait ignorer à `SelectAxis` l'axe Z, la sphère Z et les plans ZX/YZ ; seules les poignées X, Y et XY restent sélectionnables, donc toute manipulation reste dans le plan XY. Exposée via `EditorViewportGizmoController.ConstrainToXYPlane`, activée par `WorldViewportPanel.SetViewMode`.

À valider visuellement : lisibilité de la grille en tuiles aux différents crans de zoom, et poignée Z effectivement inaccessible en mode 2D. Limite connue : le gizmo se dimensionne sur la distance caméra (`_screenScale = |cameraPos − gizmoPos| / SCREEN_SCALE_FACTOR`) ; en ortho cette distance est constante (500), le gizmo a donc une taille monde fixe et sa taille écran varie avec le zoom au lieu de rester constante. Corriger cela demanderait de modifier la logique d'échelle de `GizmoTool` — hors périmètre, non entrepris.

### 🧪 E4 — Persistance de l'état de vue 2D

Sauvegarder mode (2D/3D) + état caméra 2D avec le layout éditeur, comme l'état ArcBall existant.

Done : build éditeur vert. 🧪 restant : fermer/rouvrir l'éditeur conserve le mode et le cadrage.

Découverte d'architecture : **l'état ArcBall n'est en fait persisté nulle part**. `MGDockHostExtensions.SaveLayoutToJson` ne sérialise que l'arbre de docking (`DockLayoutModel` n'a aucun emplacement d'état par panneau), et `_savedPrimaryWorldCameraState` de `WorldViewportPanel` est purement en mémoire (bascule preview ↔ monde). Il n'y avait donc aucun mécanisme existant à réutiliser pour E4.

Réalisé : fichier compagnon `viewport.editor.json`, écrit dans le même dossier `.casaeditor/` que `layout.editor.json` et piloté aux mêmes moments (écrit par `SavePersistedDockLayout`, relu par `TryLoadPersistedDockLayout`, donc par les commandes « Save/Load layout » et au chargement de projet). Contenu : mode 2D/3D + cible, cran de zoom et `PixelSnap` de la caméra 2D. `EditorViewportViewStateSerializer` (`CasaEngine.Editor/Runtime/EditorViewportViewStateSerializer.cs`) est une sérialisation JSON pure, sans dépendance fichier ni graphique, donc testable : `CasaEngine.Tests/Editor/EditorViewportViewStateSerializerTests.cs` (aller-retour, passage par le texte, json étranger, champs manquants → valeurs par défaut). `WorldViewportPanel.CaptureViewState`/`RestoreViewState` font le pont ; comme le panneau de viewport est créé paresseusement, un état lu avant sa création est mémorisé (`_pendingWorldViewportViewState`) puis appliqué à la création. Les erreurs d'E/S ou de parsing sont journalisées en warning et n'empêchent jamais le chargement du layout.

Correctif post-vérification (sauvegarde automatique) : le fichier compagnon n'était écrit que par la commande « Save Layout », ce qui rendait le critère « fermer/rouvrir conserve le mode » inatteignable. `GameEditor.Dispose(bool disposing)` est le point de sortie propre déjà utilisé pour le travail d'arrêt (`RestoreAutomationEditedFilesIfNeeded`, disposal des panneaux) : `SavePersistedViewportViewState()` y est appelé en premier. **Seul le fichier compagnon est écrit** — le layout de docking garde sa sauvegarde explicite par commande, son comportement est inchangé. Sans projet chargé le chemin est nul et l'appel est un no-op ; toute erreur d'E/S reste un simple warning.

L'état ArcBall 3D n'est volontairement pas persisté : hors périmètre de la tâche (« mode + état caméra 2D ») et il faudrait décider du comportement au changement de projet/scène.

---

## Phase F — Documentation

### ✅ F1 — Page « Espaces de rendu 2D/3D »

Dans `docs/engine/` (suivre le nommage des pages existantes) : les 4 modes de rendu, la règle de projection (« l'espace d'affichage est une propriété de la vue, jamais de la donnée tilemap »), la checklist pixel-perfect (ortho, zoom entier, snap, PointClamp, ResolutionScale = 1), `Camera3dIn2dAxisComponent` documentée comme legacy, snippets d'usage (`Camera2dComponent`, `TileMapSurfaceComponent`). Mettre à jour l'index `docs/README.md`.

Done : doc écrite, index à jour, commit.

Réalisé : [docs/engine/rendering-2d-3d-spaces.md](../../docs/engine/rendering-2d-3d-spaces.md)
(règle de projection, les 4 modes avec snippets tirés des démos, sémantique du `zOffset` par chemin
de rendu, tri des tiles dynamiques, `SkipMainPassDraw` et compteurs, invalidation par `TileRevision`,
checklist pixel-perfect + diagnostics, `Camera3dIn2dAxisComponent` documentée comme legacy sans
dépréciation dans le code) et [docs/editor/editor-2d-viewport.md](../../docs/editor/editor-2d-viewport.md)
(bascule 2D/3D, pan/zoom, compromis `PixelSnap`, grille 2D, gizmo XY et sa limite d'échelle en ortho,
persistance `viewport.editor.json`). Index `docs/README.md` mis à jour dans les deux sections.

Écarts constatés entre les notes des phases A–E et le code (le code fait foi, la doc suit le code) :

- `TileMap3dDemo` : le mur est tourné de **+90°** autour de Y (et non −90° comme noté en C3) — les
  quads de tiles sont simple face, l'autre sens laisserait le mur back-face culled.
- `Camera2dComponent` utilise `Matrix.CreateOrthographic` (l'analyse mentionnait
  `CreateOrthographicOffCenter`) ; le cadrage est centré sur `Target`, le résultat est équivalent.
- `DebugOverlay` affiche la ligne `PixelPerfect:` dès que la caméra est une `Camera2dComponent`,
  mais `PixelPerfectDiagnostics.Evaluate` ne renvoie une dégradation que si `PixelSnap` est actif :
  une caméra 2D sans snap affiche donc `PixelPerfect: OK`. Documenté tel quel.
- `PixelSnap` du viewport éditeur n'a aucun contrôle d'UI : il se règle par code ou via le champ
  `pixel_snap` de `viewport.editor.json`.

---

## Suivi

| Phase | Statut | Notes |
| --- | --- | --- |
| A — Camera2dComponent | ✅ | A1 + A2 ✅. A3 🧪 : code en place, reste la validation visuelle de `TileMapDemo`. |
| B — Politique pixel-perfect | ✅ | B1 ✅ (`PixelPerfectDiagnostics` + avertissement une fois par vue + tests). B2 🧪 : ligne overlay en place, reste la vérification visuelle. |
| C — TileMap 3D | ✅ | C1, C2, C4 ✅. C3 🧪 : démo `TileMap3dDemo` en place et enregistrée, reste la validation visuelle (lancement des démos impossible dans l'environnement de l'agent). |
| D — TileMapSurfaceComponent | ✅ | D1, D2 ✅. D3 🧪 : démo `TileMapSurfaceScreenDemo` en place et enregistrée, reste la validation visuelle (lancement des démos impossible dans l'environnement de l'agent). |
| E — Viewport 2D éditeur | ✅ | E1 ✅. E2, E3, E4 🧪 : bascule 2D/3D (bouton « 2D » de la barre de viewport), grille en tuiles, gizmo contraint XY et persistance du mode + cadrage 2D (`viewport.editor.json`) en place ; reste la validation visuelle dans l'éditeur. |
| F — Documentation | ✅ | F1 ✅ : `docs/engine/rendering-2d-3d-spaces.md`, `docs/editor/editor-2d-viewport.md`, index `docs/README.md`. |

Toutes les phases sont livrées : code, tests et documentation. Il ne reste que la validation
visuelle ci-dessous, non automatisable.

---

## Validation visuelle restante

Six points 🧪 à vérifier à la main. Aucun n'est un blocage de livraison : le code est en place,
buildé et couvert par les tests automatisables.

| # | Tâche | À vérifier |
| --- | --- | --- |
| 1 | A3 | `TileMapDemo` : rendu identique à l'ancien mode `Camera3dIn2dAxisComponent`, stable au resize, aucun contenu clippé en Z (fenêtre ortho `[Target.Z − 500, Target.Z + 499]`). |
| 2 | B2 | Overlay debug : la ligne `PixelPerfect: …` n'apparaît que sur une vue à `Camera2dComponent`, et le texte suit la dégradation (zoom non entier, `ResolutionScale != 1`). |
| 3 | C3 | `TileMap3dDemo` : sol et mur correctement orientés, culling correct en orbitant (pas de tiles manquantes ni fantômes en bordure de frustum). Rappel : les collisions tilemap restent générées en XY, le debug physique peut donc ne pas coïncider avec le rendu. |
| 4 | D3 | `TileMapSurfaceScreenDemo` : map nette (PointClamp) et à l'endroit sur le quad quelle que soit la caméra 3D, aucune tuile dessinée dans la scène elle-même, UV du quad non retournées. |
| 5 | E2 | Éditeur : bouton « 2D » de la barre de viewport, bascule aller-retour sans perte de cadrage dans les deux modes, préview toujours en perspective. |
| 6 | E3 / E4 | Éditeur : lisibilité de la grille en tuiles aux différents crans de zoom, poignée Z inaccessible en mode 2D, et fermer/rouvrir l'éditeur conserve le mode et le cadrage (`viewport.editor.json`). |

**Blocage environnement (préexistant, hors périmètre)** : le lancement de `CasaEngine.Demos` échoue
avant tout rendu sur `FileNotFoundException: FontStashSharp.MonoGame, Version=1.5.6.0`. Les points 1
à 4 n'ont donc pas pu être validés dans l'environnement de l'agent. Résoudre cette dépendance est un
préalable à la validation visuelle des démos.
