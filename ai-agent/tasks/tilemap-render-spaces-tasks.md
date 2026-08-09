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

### ⏳ B2 — Indicateur `DebugOverlay`

Dans `DebugOverlay` : si la caméra de la vue est une `Camera2dComponent`, afficher une ligne `PixelPerfect: OK` / `PixelPerfect: degraded (raison)` en réutilisant le prédicat de B1. Aucune allocation par frame (précomposer les chaînes).

Done : build vert. 🧪 restant : vérification visuelle de l'overlay sur une démo.

---

## Phase C — TileMap objet 3D complet

### ⏳ C1 — Matrice monde complète (rotation) dans les deux chemins de rendu

Dans `TileMapComponent.Draw` ([TileMapComponent.cs:218](../../CasaEngine/Framework/Scene/Entities/Components/TileMapComponent.cs)) :

- **Fast path préservé** : si la rotation monde est l'identité (à epsilon près), garder exactement le chemin actuel (positions x/y + scale). Zéro régression perf : pas d'allocation ni de produit matriciel supplémentaire par tile.
- **Chemin statique** : construire la matrice monde avec la rotation (la géométrie des chunks est en espace local — remplacer `CreateScale * CreateTranslation` par la matrice monde complète du composant).
- **Chemin dynamique** : quand rotation ≠ identité, transformer les quads par la matrice monde. `SpriteDisplayData` porte déjà une `WorldMatrix` par sprite — ajouter si nécessaire une surcharge `DrawSprite` prenant une matrice monde, sans casser les surcharges existantes.
- La bounding box (`GetBoundingBox`) utilise déjà `WorldMatrixWithScale` : vérifier la cohérence rendu/bounds avec rotation.

Done : build + tests TileMap existants verts ; nouveaux tests sur la construction de matrice si extractible.

### ⏳ C2 — Culling fallback par chunk quand rotation ≠ identité

`TryGetVisibleTileRange` (unproject vers plan Z) n'est valide que sans rotation :

- rotation identité → chemin actuel inchangé ;
- rotation ≠ identité → ne pas utiliser la plage plane ; culler chunk par chunk via leur `BoundingBox` monde (transformée par la matrice complète) contre `BoundingFrustum(frame.ViewProjection)`. Réutiliser/étendre `chunk.UpdateWorldBounds` ; le `BoundingFrustum` doit être mis en cache par draw (pas d'allocation par chunk).

Done : build + tests verts ; test unitaire de la logique de sélection (identité → plage plane, rotation → frustum).

### ⏳ C3 — Démo `TileMap3dDemo`

Nouvelle démo dans `CasaEngine.Demos` (suivre le modèle des démos existantes, l'enregistrer là où les démos sont listées) : une tilemap au sol (rotation −90° sur X), une tilemap murale verticale, caméra perspective libre (`ArcBallCameraComponent` ou équivalent utilisé par les démos 3D).

Done : build vert. 🧪 restant : validation visuelle (culling correct en orbitant, pas de tiles manquantes/fantômes).

### ⏳ C4 — Vérification perf du fast path

Vérifier par test (compteurs `LastVisitedChunkCount` / `LastDrawnTileCount` / `LastStaticBatchCount` sur une map de test, rotation identité) que le comportement de culling/batching est inchangé par rapport à avant C1/C2.

Done : test vert prouvant l'iso-comportement du chemin identité.

---

## Phase D — `TileMapSurfaceComponent` (RT → quad 3D)

### ⏳ D1 — Composant de rendu offscreen

Nouveau `CasaEngine/Framework/.../TileMapSurfaceComponent.cs` sur le **modèle exact de `WorldUIComponent`** ([WorldUIComponent.cs](../../CasaEngine/Framework/UI/WorldUIComponent.cs)) :

- possède un `RenderTargetSurface` (taille par défaut = taille de la map en pixels, redimensionnable) ;
- paramètres ortho internes (zoom entier, snap actif) — réutiliser `Camera2dComponent` ou des matrices ortho directes ;
- passe offscreen exécutée **avant la passe monde**, au même endroit que `World.DrawWorldUIToTextures` (ajouter un appel analogue à côté, même mécanique de liste au niveau `World`) ;
- séquence : `GraphicsStateGuard` → apply surface → clear → construire une `RenderFrame` ortho → dessiner la tilemap via les chemins existants (`TileMapComponent.Draw` + flush du `SpriteRendererComponent` avec cette frame) → restaurer. Attention : `TileMapComponent.Draw` lit `Owner.World.CurrentRenderFrame` pour le culling/batches — fournir la frame ortho pendant la passe puis restaurer la valeur précédente ;
- expose `Texture` et `BindToMaterial(UnlitTextureMaterial/LitDiffuseMaterial)` comme `WorldUIComponent` ;
- ne pas dessiner la tilemap dans la passe monde principale quand elle est routée vers la surface (flag explicite sur le composant tilemap visé, opt-in).

Done : build + tests verts.

### ⏳ D2 — Invalidation à la demande

La surface ne se re-rend que si : mutation de tiles (`SetTileReference` et compagnie), tiles animées présentes (tick → re-rendu), autotiles dirty, ou `Invalidate()` explicite. Map statique = zéro re-rendu par frame.

Done : test unitaire du dirty-tracking (mutation → re-rendu au frame suivant ; rien → pas de re-rendu).

### ⏳ D3 — Démo écran/minimap

Démo : un quad 3D dans une scène perspective (type écran d'arcade ou minimap) affichant la texture de la surface tilemap, avec `SamplerState.PointClamp` sur le matériau.

Done : build vert. 🧪 restant : validation visuelle (netteté du RT quelle que soit la caméra 3D).

---

## Phase E — Éditeur : viewport 2D

Build de référence pour cette phase : `dotnet build CasaEngine.Editor.MonoGame.sln`.

### ⏳ E1 — `EditorViewport2dCameraController`

Nouveau contrôleur dans `CasaEngine.Editor/Runtime/` (parallèle à `EditorViewportCameraController`, **sans le modifier**) :

- crée/pilote une `Camera2dComponent` ;
- pan : drag clic milieu ; zoom : molette par crans entiers (×1, ×2, ×3, … et fractions ½, ¼ si simple) centré sur le curseur ;
- état capturable/restaurable (`CaptureState`/`RestoreState`) comme le contrôleur ArcBall.

Done : build éditeur vert ; tests unitaires du contrôleur sur le modèle d'`EditorViewportCameraControllerTests`.

### ⏳ E2 — Bascule 2D/3D par viewport

Dans le viewport scène de l'éditeur (repérer `EditorViewContext` et la création des vues) : une bascule 2D/3D qui échange caméra + contrôleur, en préservant l'état de l'autre mode. UI minimale (bouton/raccourci dans la barre du viewport, suivre les patterns existants).

Done : build éditeur vert. 🧪 restant : bascule aller-retour sans perte d'état, rendu 2D correct.

### ⏳ E3 — Grille 2D et contrainte gizmo XY

- En mode 2D : grille graduée en tiles/pixels (adapter le rendu de grille existant, version minimale acceptable) ;
- gizmos de translation contraints au plan XY en mode 2D.

Version minimale acceptable ; si le système de grille/gizmo résiste, marquer ⚠️ avec le point de blocage précis et ne pas forcer un refactor.

Done : build éditeur vert. 🧪 restant : vérification visuelle.

### ⏳ E4 — Persistance de l'état de vue 2D

Sauvegarder mode (2D/3D) + état caméra 2D avec le layout éditeur, comme l'état ArcBall existant.

Done : build éditeur vert. 🧪 restant : fermer/rouvrir l'éditeur conserve le mode et le cadrage.

---

## Phase F — Documentation

### ⏳ F1 — Page « Espaces de rendu 2D/3D »

Dans `docs/engine/` (suivre le nommage des pages existantes) : les 4 modes de rendu, la règle de projection (« l'espace d'affichage est une propriété de la vue, jamais de la donnée tilemap »), la checklist pixel-perfect (ortho, zoom entier, snap, PointClamp, ResolutionScale = 1), `Camera3dIn2dAxisComponent` documentée comme legacy, snippets d'usage (`Camera2dComponent`, `TileMapSurfaceComponent`). Mettre à jour l'index `docs/README.md`.

Done : doc écrite, index à jour, commit.

---

## Suivi

| Phase | Statut | Notes |
| --- | --- | --- |
| A — Camera2dComponent | 🧪 | A1 + A2 ✅. A3 : code en place, reste la validation visuelle de `TileMapDemo`. |
| B — Politique pixel-perfect | 🚧 | B1 ✅ (`PixelPerfectDiagnostics` + avertissement une fois par vue + tests). B2 à faire. |
| C — TileMap 3D | ⏳ | |
| D — TileMapSurfaceComponent | ⏳ | |
| E — Viewport 2D éditeur | ⏳ | |
| F — Documentation | ⏳ | |
