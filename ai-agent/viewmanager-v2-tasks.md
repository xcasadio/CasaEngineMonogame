# ViewManager v2 — Tâches d'évolution (éditeur + split screen)

## Analyse de l'existant

Le refactor multi-view initial (PR 1–6) est terminé. On dispose de :

| Élément | État actuel | Lacune identifiée |
|---|---|---|
| `RenderView` | ✅ Existe (World, Camera, Surface, Enabled, ClearColor) | Pas de ViewId, pas d'UpdateMode, pas de ResolutionScale |
| `ViewManager` | ✅ Add/Remove/Clear/ActiveView | Pas d'événements, pas de ScreenToView, API minimale |
| `IRenderSurface` | ✅ BackBufferSurface + RenderTargetSurface | Pas de PresentMode, pas d'aspect fit, pas de letterboxing |
| `RenderPipeline` | ✅ Boucle multi-view, flush par vue | Pipeline unique pour toutes les vues, pas de pipeline par vue |
| RT resize | ✅ `EnsureSize()` (recrée si dimension change) | Pas de debounce temporel, pas de pooling |
| Dispose | ✅ `RenderTargetSurface : IDisposable` | Ownership pas formalisé (ViewManager ne dispose rien) |
| State isolation | ❌ Aucun GraphicsStateGuard | Capture/restore RT pour WPF seulement |
| Picking | ❌ Raycasting CPU uniquement (`RayHelper`) | Pas de color-picking / ID buffer |
| Host/Presenter | ❌ Pas d'abstraction | `D3D11Host` (WPF) existe mais couplé |
| Events ViewManager | ❌ Aucun | Pas de ViewAdded/Removed/Resized/Invalidated |
| Input mapping par vue | ❌ Aucun | Pas de ScreenToView, pas de capture souris |
| Performance (throttle) | ❌ Seulement `Enabled` (bool) | Pas de OnDemand/Throttled/ResolutionScale |
| Demos avancées | ✅ SplitScreenDemo + RenderToTextureDemo | Pas de sandbox complète (création/destruction dynamique, leak check) |

---

## Priorités

Les tâches sont ordonnées par priorité décroissante. Chaque PR doit compiler en Debug + DebugEditor et ne pas casser l'existant.

---

## PR 7 — Séparation View / Host / Presenter

**Objectif :** Découpler la logique de rendu (View), le conteneur UI (Host), et la présentation à l'écran (Presenter).

### 7.1 — Refactor RenderView : ajout ViewId + métadonnées

- [x] Ajouter un type `ViewId` (struct wrapper autour d'un `int` ou `Guid`, `IEquatable<ViewId>`)
- [x] Chaque `RenderView` reçoit un `ViewId` unique à la création
- [x] Le `ViewManager` expose `TryGetView(ViewId id, out RenderView view)`
- [x] Le `ViewManager` expose `CreateView(ViewDefinition def)` qui retourne un `ViewId`
  - `ViewDefinition` : record/struct contenant `World`, `CameraComponent`, `IRenderSurface`, `ClearColor`, `Name`, etc.
- [x] Conserver rétro-compat : `Add(RenderView)` / `Remove(RenderView)` restent fonctionnels

### 7.2 — Abstraction IViewHost

- [x] Créer interface `IViewHost` dans `CasaEngine/Framework/Rendering/`
  - `ViewId ViewId { get; }`
  - `int Width { get; }`
  - `int Height { get; }`
  - `bool IsVisible { get; }`
  - `event Action<IViewHost, int, int>? Resized`
  - `event Action<IViewHost>? Closed`
- [x] Implémenter `BackBufferHost : IViewHost` (plein écran ou zone du backbuffer)
- [x] Implémenter `RenderTargetHost : IViewHost` (pour onglet éditeur / panel MGUI)
- [x] Le host est responsable du resize de la `IRenderSurface` associée
- [x] Documenter le contrat : le Host possède la surface, le ViewManager possède la View

### 7.3 — Abstraction IViewPresenter

- [x] Créer interface `IViewPresenter` dans `CasaEngine/Framework/Rendering/`
  - `void Present(GraphicsDevice gd, RenderView view)` — affiche la texture finale
- [x] Implémenter `BackBufferPresenter : IViewPresenter`
  - Dessine la texture RT dans un rectangle du backbuffer (ou BackBufferSurface via Viewport)
  - Supporte `PresentMode` : `Stretch`, `Fit`, `Fill`, `PixelPerfect`
- [x] Implémenter `TexturePresenter : IViewPresenter`
  - Expose simplement `Texture2D` (pour UI MGUI/WPF qui gère l'affichage)
- [x] Ajouter `enum PresentMode { Stretch, Fit, Fill, PixelPerfect }`
- [x] Letterboxing géré dans `BackBufferPresenter` (pas dans la caméra)

### 7.4 — Mise à jour du ViewManager

- [x] Le `ViewManager` maintient un dictionnaire `ViewId → RenderView`
- [x] Ajout d'un lien optionnel `RenderView.Host` et `RenderView.Presenter`
- [x] Le `RenderPipeline` utilise le `Presenter` pour la phase d'affichage finale (si présent)

✅ Critère : les demos existantes fonctionnent toujours, nouvelles abstractions utilisables.

---

## PR 8 — Pooling RT + resize debounced + dispose ownership

**Objectif :** Éviter les fuites GPU et les allocations excessives lors du resize (docking = resize en boucle).

### 8.1 — RenderTarget pooling

- [x] Créer `RenderTargetPool` dans `CasaEngine/Framework/Rendering/`
  - `RenderTarget2D Acquire(int width, int height, SurfaceFormat fmt, DepthFormat depth)`
  - `void Release(RenderTarget2D rt)` — remet dans le pool (ne dispose pas)
  - `void Trim()` — dispose les RT inutilisés depuis > N secondes
  - `void DisposeAll()` — vide le pool
  - Stratégie : bucket par (width, height, format) ou "closest fit"
- [x] Intégrer le pool dans `RenderTargetSurface` :
  - `EnsureSize()` utilise `RenderTargetPool.Acquire()` au lieu de `new RenderTarget2D()`
  - L'ancien RT est retourné au pool via `Release()` (pas disposé)
- [x] Le pool est possédé par le `GameManager` ou le `RenderPipeline` (cycle de vie clair)

### 8.2 — Resize debounced (temporel)

- [x] Dans `RenderTargetSurface` (ou `IViewHost`), ajouter un mécanisme de debounce :
  - Quand `RequestResize(w, h)` est appelé, marquer `_pendingWidth`/`_pendingHeight` + `_resizeDirtyTime`
  - Le resize effectif (recréation du RT) se fait dans `EnsureSize()` seulement si :
    - dimensions différentes ET
    - au moins N ms depuis le dernier changement (ex: 100–200 ms) OU on est dans un `Draw()`
  - Pendant le debounce, on continue à rendre dans l'ancien RT (stretché si besoin)
- [x] Alternative simple : flag `_resizeDirty` mis à `true` sur resize, consommé au prochain `Draw()`
  - Plus simple et suffisant si le Draw est le seul consommateur

### 8.3 — Dispose ownership formalisé

- [x] Définir clairement dans la doc et le code :
  - `ViewManager.Remove(view)` ne dispose PAS la surface (le Host en est responsable)
  - `ViewManager.Clear()` ne dispose PAS les surfaces
  - `IViewHost.Dispose()` dispose sa surface (et retourne les RT au pool)
  - `RenderTargetPool.DisposeAll()` est appelé dans `Game.UnloadContent()` ou `Dispose()`
- [x] Ajouter des `Debug.Assert` / logs si un RT est GC'd sans avoir été retourné au pool
- [x] Ajouter un compteur de RT actifs accessible (pour la demo de validation)

✅ Critère : resize en boucle d'un host ne fait pas exploser la mémoire GPU, compteur RT stable.

---

## PR 9 — Isolation de l'état GraphicsDevice entre views

**Objectif :** Empêcher qu'une view corrompe l'état GPU de la suivante.

### 9.1 — GraphicsStateGuard

- [x] Créer `GraphicsStateSnapshot` (struct) dans `CasaEngine/Framework/Rendering/`
  - Capture : `RenderTargetBinding[]`, `Viewport`, `ScissorRectangle`, `BlendState`, `DepthStencilState`, `RasterizerState`, `SamplerState[0..3]`
- [x] Créer `GraphicsStateGuard` (struct `IDisposable`, pattern `using`) :
  - Constructeur : capture le snapshot courant
  - `Dispose()` : restaure le snapshot
- [x] Alternative : méthode statique `GraphicsStateGuard.Capture(gd)` → `GraphicsStateSnapshot` + `Restore(gd, snapshot)`

### 9.2 — Intégration dans RenderPipeline

- [x] Dans `RenderPipeline.Render()`, encapsuler chaque vue dans un `GraphicsStateGuard` :
  ```
  using var guard = new GraphicsStateGuard(gd);
  // ... render view ...
  ```
- [x] Après chaque Flush de renderer, optionnellement vérifier que le device n'est pas dans un état sale (mode Debug uniquement)
- [x] Ajouter une option `RenderPipeline.ValidateStatePerView` (debug flag) qui logue les différences d'état

### 9.3 — Convention de "propreté"

- [x] Documenter la convention : chaque `IViewFlushableRenderer.Flush()` doit remettre le device dans un état propre (ou le guard s'en charge)
- [x] Ajouter des assertions en mode Debug si un renderer laisse un RT bindu ou un BlendState non-default

✅ Critère : 2+ views avec pipelines différents (sprites + 3D) n'ont pas de "cross bleed" d'état.

---

## PR 10 — Pipeline de rendu par view (éditeur-ready)

**Objectif :** Permettre à chaque view d'avoir son propre pipeline de rendu (game, scene editor, preview…).

### 10.1 — Abstraction IRenderPipeline / IViewRenderer

- [x] Créer interface `IViewRenderPipeline` dans `CasaEngine/Framework/Rendering/`
  - `void RenderView(GraphicsDevice gd, RenderView view, in RenderFrame frame, IReadOnlyList<IViewFlushableRenderer> renderers)`
- [x] Ajouter `RenderView.Pipeline` (propriété `IViewRenderPipeline?`, nullable = utilise le pipeline par défaut)

### 10.2 — DefaultViewPipeline

- [x] Extraire la logique actuelle per-view de `RenderPipeline.Render()` dans `DefaultViewPipeline : IViewRenderPipeline`
  - Clear → World.Draw → Flush renderers
- [x] `RenderPipeline.Render()` délègue à `view.Pipeline ?? _defaultPipeline`

### 10.3 — EditorViewPipeline (placeholder)

- [x] Créer `EditorViewPipeline : IViewRenderPipeline` (dans `CasaEngine.EditorUI` ou `CasaEngine` sous `#if EDITOR`)
  - Étapes : Clear → World.Draw → Flush renderers → RenderGrid → RenderGizmos → RenderSelectionOutline → RenderUIOverlay
  - Pour l'instant, les étapes gizmo/grid/outline appellent les composants existants (`GizmoComponent`, `GridComponent`, `AxisComponent`)
- [x] Préparer des "slots" extensibles :
  - `void RenderOpaque(...)`, `void RenderTransparent(...)`, `void RenderOverlays(...)`, `void PostProcess(...)`

### 10.4 — InspectorPreviewPipeline (stub)

- [x] Créer un stub `PreviewPipeline : IViewRenderPipeline` pour aperçu mesh/material dans l'inspector
  - Rendu simplifié : un seul mesh, éclairage basique, fond uni
  - Pas d'implémentation complète — juste la structure

✅ Critère : la SceneView éditeur utilise `EditorViewPipeline`, la GameView utilise `DefaultViewPipeline`, pas de régression.

---

## PR 11 — Support éditeur : picking buffer + overlays

**Objectif :** Ajouter les bases du picking et des overlays de debug par vue.

### 11.1 — Picking buffer (color ID)

- [x] Créer `PickingBuffer` dans `CasaEngine/Framework/Rendering/`
  - Possède un `RenderTarget2D` auxiliaire (même taille que la view, format `Color`)
  - `void Render(GraphicsDevice gd, RenderView view, in RenderFrame frame)` :
    - Rend les entités avec un shader "ID" (chaque entité = couleur unique dérivée de son ID)
  - `Entity? Pick(int screenX, int screenY)` :
    - Lit le pixel, retrouve l'entité par son ID-couleur
  - `void EnsureSize(int w, int h)` — resize du RT picking (utilise le pool)
- [x] Créer un `Effect` / shader basique "EntityId" :
  - Vertex shader standard (MVP)
  - Pixel shader : retourne `float4(r, g, b, 1)` encodant l'entity ID
- [x] Intégrer dans `EditorViewPipeline` : le picking buffer est rendu après la scène (ou en parallèle via un second pass)

### 11.2 — Depth buffer lisible (stratégie)

- [x] Documenter la stratégie choisie pour accéder au depth buffer :
  - Option A : `PreserveContents` sur le depth buffer + lecture GPU→CPU (lent)
  - Option B : Render depth dans un RT séparé via un depth-only pass (préféré)
- [x] Créer un stub `DepthPass` dans le pipeline éditeur (pas d'implémentation complète)
  - Sera utilisé pour : selection outline, gizmo occlusion

### 11.3 — Debug overlay par view

- [x] Créer `DebugOverlay` dans `CasaEngine/Framework/Rendering/`
  - `void Draw(SpriteBatch sb, RenderView view, Rectangle rect)` — affiche les stats
  - Stats : FPS, draw calls (si accessible), nombre de RT actifs, résolution de la view
  - Activable par view via `RenderView.ShowDebugOverlay`
- [x] Intégrer dans `RenderPipeline` après le rendu de chaque vue (si activé)

✅ Critère : click dans la scene view → entité sélectionnée via picking, overlay FPS visible.

---

## PR 12 — Performance : UpdateMode + ResolutionScale

**Objectif :** Permettre de throttler les vues non-critiques pour garder le framerate.

### 12.1 — UpdateMode par view

- [x] Ajouter `enum ViewUpdateMode { RealTime, OnDemand, Throttled }`
- [x] Ajouter `RenderView.UpdateMode` (default: `RealTime`)
- [x] Ajouter `RenderView.TargetFrameRate` (pour `Throttled`, ex: 10 fps)
- [x] Ajouter `RenderView.Invalidate()` (pour `OnDemand` : force un re-render au prochain frame)
- [x] Dans `RenderPipeline.Render()` :
  - `RealTime` : rendu à chaque frame (comportement actuel)
  - `OnDemand` : rendu seulement si `_isDirty` est true, puis reset le flag
  - `Throttled` : rendu seulement si `elapsed >= 1/TargetFrameRate`
  - Les vues non rendues conservent leur dernier RT (pas de clear)

### 12.2 — ResolutionScale par view

- [x] Ajouter `RenderView.ResolutionScale` (float, default: 1.0, range 0.25–2.0)
- [x] Le RT interne est créé à `(Width * Scale, Height * Scale)`
- [x] Le Presenter upscale/downscale à la taille finale
- [x] Utile pour : preview basse résolution, mini-map, perf mode

### 12.3 — IsVisible vs Enabled

- [x] Clarifier la sémantique :
  - `Enabled = false` : la view n'existe logiquement pas (pas de rendu, pas d'update)
  - `IsVisible = false` : la view existe mais est cachée (onglet non visible) → pas de rendu mais state conservé
  - `IsActive` : la view reçoit les inputs (une seule à la fois)
- [x] `RenderPipeline` skip les vues `!Enabled` et `!IsVisible`

✅ Critère : 5 vues simultanées, seule la vue active est en RealTime, les previews en Throttled(10fps), le framerate reste stable.

---

## PR 13 — API ergonomique : événements + input mapping

**Objectif :** Fournir une API complète pour l'intégration éditeur/jeu.

### 13.1 — Événements ViewManager

- [x] Ajouter événements sur `ViewManager` :
  - `event Action<RenderView>? ViewAdded`
  - `event Action<RenderView>? ViewRemoved`
  - `event Action<RenderView, int, int>? ViewResized` (largeur, hauteur)
  - `event Action<RenderView>? ViewInvalidated`
- [x] Fire les événements dans `Add()`, `Remove()`, et quand le Host signale un resize

### 13.2 — Input mapping par view

- [x] Ajouter `ViewManager.ScreenToView(Point screenPoint)` → `(RenderView? view, Vector2 localPoint)`
  - Teste les vues dans l'ordre inverse (dernière = au-dessus)
  - Retourne la première vue dont le rect contient le point
- [x] Ajouter `ViewManager.ViewToWorldRay(RenderView view, Vector2 localPoint)` → `Ray`
  - Utilise `RayHelper.CalculateRayFromScreenCoordinate()` avec la caméra et le viewport de la vue
- [x] Ajouter un mécanisme de "capture" :
  - `ViewManager.CaptureInput(RenderView view)` — la vue reçoit tous les events souris jusqu'au release
  - `ViewManager.ReleaseInput()` — fin de capture
  - Utile pour le drag de gizmo

### 13.3 — Intégration avec l'éditeur existant

- [x] Refactorer `GameEditorWorld.cs` pour utiliser `ScreenToView` + `ViewToWorldRay` au lieu d'accéder directement à `ActiveView.Camera`
- [x] Refactorer `GizmoComponent` pour utiliser le système de capture input

✅ Critère : drag gizmo fonctionne proprement sans "échapper" à la vue, ScreenToView retourne la bonne vue en split screen.

---

## PR 14 — Demo sandbox complète

**Objectif :** Valider l'ensemble des fonctionnalités avec une demo interactive.

### 14.1 — ViewManagerSandbox demo

- [x] Créer `ViewManagerSandbox` dans `CasaEngine.Demos/Demos/`
- [x] Scénarios couverts :
  - [x] 4 caméras en split screen (Grid4)
  - [x] 2 vues avec render targets qui resizent dynamiquement (simulation docking)
  - [x] Création/suppression dynamique de vues (touche clavier pour add/remove)
  - [x] Validation mémoire : afficher le compteur de RT actifs dans le pool
  - [x] Démonstration UpdateMode : une vue en RealTime, une en Throttled(5fps), une en OnDemand
  - [x] Picking : click sur une entité dans une vue → sélection affichée
  - [x] Debug overlay activable par touche
- [x] Afficher un HUD avec :
  - Nombre de vues actives
  - Nombre de RT dans le pool (actifs / libres)
  - FPS global
  - Mode de chaque vue

✅ Critère : pas de fuite mémoire (RT count stable après cycles add/remove), toutes les fonctionnalités démontrées.

---

## Résumé des priorités

| Priorité | PR | Fonctionnalité |
|---|---|---|
| 🔴 Haute | PR 7 | View / Host / Presenter séparés |
| 🔴 Haute | PR 8 | Pooling RT + resize debounced + dispose |
| 🔴 Haute | PR 9 | Isolation GraphicsDevice state |
| 🟠 Moyenne | PR 10 | Pipeline par view (éditeur-ready) |
| 🟠 Moyenne | PR 11 | Picking buffer + overlays |
| 🟡 Standard | PR 12 | Performance (UpdateMode / ResolutionScale) |
| 🟡 Standard | PR 13 | API ergonomique (événements + input) |
| 🟢 Validation | PR 14 | Demo sandbox complète |

---

## Checklist d'acceptation (manuelle)

- [x] Debug build OK (`CasaEngine.MonoGame.sln --configuration Debug`)
- [x] Editor Debug build OK (`CasaEngine.Editor.MonoGame.sln --configuration Debug`)
- [x] Pas de régression : demos existantes (SplitScreenDemo, RenderToTextureDemo) non cassées
- [x] `ViewId` assigné automatiquement sur `Add()` et `CreateView()`
- [x] `ViewManager.TryGetView(id)` retourne la bonne view
- [x] `ViewManager.ScreenToView(point)` retourne la vue et le point local
- [x] `ViewManager.ViewToWorldRay(view, point)` produit un ray valide
- [x] `IViewHost` + `BackBufferHost` + `RenderTargetHost` compilent et gèrent resize
- [x] `RenderTargetPool.Shared` initialisé — RT retournés au pool sur `EnsureSize()`
- [x] `RenderTargetSurface.RequestResize()` debounce les resizes multiples par frame
- [x] `GraphicsStateGuard` restaure l'état GPU après chaque vue (pas de cross-bleed)
- [x] `IViewRenderPipeline` + `DefaultViewPipeline` + `EditorViewPipeline` + `PreviewPipeline` compilent
- [x] `RenderView.Pipeline` surchargeable par vue
- [x] `PickingBuffer` s'initialise et appelle correctement le pool
- [x] `DebugOverlay` s'affiche quand `RenderView.ShowDebugOverlay = true`
- [x] `ViewUpdateMode.RealTime` : rendu à chaque frame
- [x] `ViewUpdateMode.Throttled` : rendu limité au `TargetFrameRate`
- [x] `ViewUpdateMode.OnDemand` : rendu seulement après `Invalidate()`
- [x] `ResolutionScale` clamped 0.25..2.0
- [x] `ViewManager` fire `ViewAdded` / `ViewRemoved` sur add/remove
- [x] Demo sandbox `ViewManagerSandbox` présente : split 4 vues, modes, overlay, add/remove dynamique