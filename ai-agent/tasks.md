# Plan de refactor — Multi-View Rendering (Editor MGUI + Split Screen)

## 0) Contexte

### État actuel
- Le moteur repose sur **une caméra globale** (`GameManager.ActiveCamera`) ; le rendu du monde fait `CurrentWorld.Draw(ActiveCamera.ViewMatrix * ActiveCamera.ProjectionMatrix)`.
- Plusieurs renderers (Sprite/Line/SkinnedMesh) récupèrent **directement `ActiveCamera`** dans leur `Draw()`.
- `World.Draw(Matrix viewProjection)` construit la liste des entités visibles via frustum, puis appelle `entity.Draw()` (ce qui alimente les buffers/queues des renderers).

### Problèmes
1) Impossible de rendre **plusieurs vues** la même frame (éditeur multi-panneaux / split screen), car :
- on ne sait pas “quelle caméra” est utilisée au flush des renderers (ils lisent `ActiveCamera`)
- le système de draw “queue puis flush” n’est pas organisé **par vue**

2) L’éditeur MGUI a besoin de rendre dans un **RenderTarget2D** (texture) affiché dans l’UI, pas uniquement sur le backbuffer.

---

## 1) Objectifs / non-objectifs

### Objectifs (MVP)
- Introduire un concept de **RenderView** : `(World, Camera, SurfaceOutput, ClearOptions)`
- Permettre de rendre :
  - soit vers le **backbuffer** dans un `Rectangle` (split screen)
  - soit vers un **RenderTarget2D** (éditeur)
- Refactor minimal : conserver le modèle actuel “World.Draw() enregistre, renderers flush”.

### Non-objectifs
- Pas de refonte complète ECS / scène / pipeline shader.
- Pas de migration WPF → MGUI dans ce ticket (on prépare juste l’infra côté moteur).
- Pas de “render graph” complexe ; on veut un pipeline simple et robuste.

---

## 2) Stratégie de livraison (PRs petites, build toujours OK)

### Règle
Chaque PR doit :
- compiler en Debug + DebugEditor
- ne pas casser les demos existantes
- ajouter une petite preuve (mini test manuel / sample / note “How to test”)

---

## 3) Backlog détaillé (petites tâches)

> Convention : “✅ Done = compile + comportement identique ou démonstration claire”

---

### PR 1 — Fondation : types multi-vues (sans changer le rendu)
**Objectif :** ajouter les nouveaux types sans les brancher au moteur.

Tâches :
- [x] Créer dossier `CasaEngine/Framework/Rendering/`
- [x] Ajouter `RenderFrame` (struct) :
  - [x] `Matrix View`, `Matrix Projection`, `Matrix ViewProjection`
  - [x] `Vector3 CameraPosition`
  - [x] `Rectangle ViewportRect`
- [x] Ajouter interface `IRenderSurface`
  - [x] `bool IsBackBuffer`
  - [x] `Rectangle ViewportRect { get; }`
  - [x] `RenderTarget2D? RenderTarget { get; }`
  - [x] `void Apply(GraphicsDevice gd)` → set render target + set viewport
  - [x] `void Restore(GraphicsDevice gd)` → revenir à backbuffer + viewport plein écran (si nécessaire)
- [x] Implémenter `BackBufferSurface(Rectangle rect)`
- [x] Implémenter `RenderTargetSurface(GraphicsDevice gd, int w, int h, SurfaceFormat fmt = Color, DepthFormat depth=Depth24)`
  - [x] `EnsureSize(w,h)` : recrée la texture si taille change
  - [x] `Dispose()`
- [x] Ajouter classe `RenderView`
  - [x] `World World`
  - [x] `CameraComponent Camera`
  - [x] `IRenderSurface Surface`
  - [x] options : `ClearColor`, `ClearDepth`, `Enabled`, `Name`
- [x] Ajouter classe `ViewManager`
  - [x] `List<RenderView> Views`
  - [x] helpers : `Clear()`, `Add()`, `Remove()`

✅ Critère : build OK, aucune utilisation encore.

---

### PR 2 — API “Flush par vue” sur les renderers (sans changer le rendu)
**Objectif :** permettre d’exécuter le rendu d’un renderer pour une vue donnée.

Tâches :
- [x] Ajouter interface `IViewFlushableRenderer`
  - [x] `void Flush(in RenderFrame frame);`
- [x] Modifier `StaticMeshRendererComponent`
  - [x] extraire le contenu de `Draw()` dans `Flush(frame)` (frame pas forcément utilisé)
  - [x] `Draw()` devient : `Flush(frameFromActiveCamera)` (fallback)
  - [x] ne plus dépendre d’ActiveCamera (sauf fallback)
- [x] Modifier `Line3dRendererComponent`
  - [x] `Flush(frame)` utilise `frame.View` et `frame.Projection` au lieu de `ActiveCamera`
  - [x] `Draw()` appelle `Flush(frameFromActiveCamera)` en fallback
- [x] Modifier `SpriteRendererComponent`
  - [x] `Flush(frame)` utilise `frame.ViewProjection` au lieu de `ActiveCamera`
  - [x] `Draw()` appelle `Flush(frameFromActiveCamera)` en fallback
- [x] Modifier `SkinnedMeshRendererComponent`
  - [x] enlever la lecture `ActiveCamera` dans `Draw()` et la remplacer par `Flush(frame)`
  - [x] `Draw()` appelle `Flush(frameFromActiveCamera)` en fallback
- [x] Ajouter utilitaire `RenderFrameFactory.From(CameraComponent cam, Rectangle viewportRect)`
  - [x] calcule View/Proj/ViewProj + position caméra

✅ Critère : rendu identique en l’état (car multi-view pas encore branché).

---

### PR 3 — RenderPipeline : boucle multi-views (feature flag)
**Objectif :** introduire la boucle de rendu multi-vues, activable sans casser l’existant.

Tâches :
- [x] Ajouter `RenderPipeline` (`CasaEngine/Framework/Rendering/RenderPipeline.cs`)
  - [x] constructor prend un accès au `GraphicsDevice` + aux renderers
  - [x] maintient la liste des renderers à flusher dans l’ordre (Mesh → Skinned → Sprite → Line, etc.)
  - [x] méthode `Render(IReadOnlyList<RenderView> views, GameTime gt)`
    - [x] pour chaque view :
      - [x] `view.Surface.Apply(gd)` (SetRenderTarget + Viewport)
      - [x] `gd.Clear(...)` (ClearColor + Depth si besoin)
      - [x] `frame = RenderFrameFactory.From(view.Camera, view.Surface.ViewportRect)`
      - [x] `view.World.Draw(frame.ViewProjection)`
      - [x] flush renderers via `Flush(frame)` (et clearing de leurs queues)
      - [x] `view.Surface.Restore(gd)` si nécessaire
    - [x] à la fin : remettre backbuffer + viewport plein écran
- [x] Ajouter dans `GameManager` un `ViewManager ViewManager {get;}`
- [x] Ajouter un flag runtime :
  - [x] `CasaEngineGame.UseRenderPipeline = true/false` (ou settings projet)
- [x] Modifier `CasaEngineGame.Draw()` :
  - [x] si flag OFF : comportement actuel (no change)
  - [x] si flag ON : appeler `_renderPipeline.Render(GameManager.ViewManager.Views, gameTime)`
  - [x] IMPORTANT : empêcher double-draw des renderers :
    - [x] option A (simple) : `Visible=false` pour les composants renderers quand pipeline actif
    - [x] option B : sortir les renderers du système `GameComponent` et les piloter uniquement via pipeline
- [x] Ajouter fallback si `Views` est vide :
  - [x] créer une view implicite avec `CurrentWorld + ActiveCamera + BackBuffer full screen`

✅ Critère : flag ON + views vides = rendu identique.

---

### PR 4 — Split screen (démo + helper de layout)
**Objectif :** preuve end-to-end : 2 caméras → 2 rectangles backbuffer.

Tâches :
- [x] Créer `SplitScreenLayout` helper :
  - [x] `static Rectangle[] Compute(int screenW, int screenH, int playerCount, SplitMode mode)`
  - [x] modes : 2 horizontal / 2 vertical / 4 grid
- [x] Ajouter une démo simple (dans `CasaEngine.Demos`) :
  - [x] crée 2 caméras (ou duplique la caméra) avec positions différentes
  - [x] configure `GameManager.ViewManager.Views` :
    - [x] View0 : `BackBufferSurface(rect0)`
    - [x] View1 : `BackBufferSurface(rect1)`
  - [x] active `UseRenderPipeline = true`
- [x] Vérifier que chaque vue flush bien ses propres draw queues :
  - [x] aucun “cross bleed” (sprites/lines) entre vues

✅ Critère : démo split-screen fonctionnelle.

---

### PR 5 — RenderTarget views (éditeur-ready, sans MGUI)
**Objectif :** preuve : une view rend dans RenderTarget2D.

Tâches :
- [x] Ajouter un sample “RenderToTextureDemo”
  - [x] crée `RenderTargetSurface` (ex 512x512)
  - [x] crée une `RenderView` vers cette surface
  - [x] rend une scène simple dedans
  - [x] affiche la texture dans le backbuffer via un sprite/quad
- [x] Ajouter une API “propre” pour exposer la texture :
  - [x] ex: `RenderTargetSurface.Texture`
- [x] Ajouter le resize :
  - [x] `surface.EnsureSize(w,h)` + `camera.OnScreenResized(w,h)`
  - [x] vérifier que projection s’update correctement

✅ Critère : texture rendue visible à l’écran.

---

### PR 6 — Nettoyage : réduire la dépendance à ActiveCamera
**Objectif :** faire du multi-view le “chemin normal”.

Tâches :
- [x] Chercher les usages `GameManager.ActiveCamera` dans renderers et systèmes
- [x] Conserver `ActiveCamera` uniquement comme fallback / compat
- [x] Ajouter commentaires + TODO de dépréciation
- [x] Mettre à jour documentation : “comment créer des RenderView”

✅ Critère : le moteur fonctionne avec `Views` comme source de vérité.

---

## 4) Checklist d’acceptation (manuelle)
- [x] Runtime : une vue plein écran (comportement identique)
- [x] Runtime : split screen 2 vues, chacune indépendante
- [x] Runtime : render target view visible (render to texture)
- [x] DebugEditor : compile OK (WPF editor pas cassé)
- [x] Pas de double draw (rendu dupliqué / overlays étranges)

---

## 5) Pièges connus
- Les queues des renderers doivent être **vidées par vue** (sinon artefacts).
- Ne pas laisser `base.Draw()` re-dessiner les renderers si pipeline actif (sinon rendu dupliqué).
- `CameraComponent.OnScreenResized(w,h)` doit être appelé quand un RenderTarget change de taille, sinon projection incorrecte.
