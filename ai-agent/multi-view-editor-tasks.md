# Multi-View Editor — Migration vers une architecture "un runtime, N viewports"

## Analyse de l'existant

### Architecture actuelle (multi-instances)

Chaque onglet éditeur (World, Entity, Sprite, Animation2d, TileMap) crée un contrôle `GameEditor` (hérite de `WpfGame` → `D3D11Host`) qui instancie un `CasaEngineGame` complet avec :

| Ressource dupliquée | Classe | Impact |
|---|---|---|
| GraphicsDevice | `D3D11Host` crée le sien | 5 devices GPU = lourd, fragile, "device lost" |
| SpriteBatch | `CasaEngineGame.Initialize()` | Mémoire GPU |
| Tous les renderers | `StaticMeshRenderer`, `SkinnedMeshRenderer`, `SpriteRenderer`, `Line3dRenderer`, `Renderer2D` | Mémoire + draw calls non partagés |
| RenderPipeline | `RenderPipeline` par instance | Pipeline dupliqué |
| InputComponent | `InputComponent` par instance | OK en soi, mais couplé au game |
| PhysicsEngine | `PhysicsEngineComponent` + `PhysicsDebugView` | Duplications inutiles |
| FontSystem | FontStashSharp `FontSystem` | Polices rechargées N fois |
| AssetContentManager | Instance par game, cache indépendant | Textures/modèles GPU dupliqués |
| ViewManager | Instance par game | Chaque game a ses propres RenderViews |
| RenderTargetPool.Shared | Static, écrasé par le dernier init | Bug potentiel |
| GameManager | Instance par game | World, camera, etc. séparés |

### Ce qui est déjà prêt (acquis du ViewManager v2)

Le moteur dispose **déjà** d'une infrastructure multi-view au sein d'un seul `CasaEngineGame` :

- `ViewManager` avec `ViewId`, `CreateView()`, `Add()`, `Remove()`, événements (`ViewAdded`, `ViewRemoved`, `ViewResized`, `ViewInvalidated`)
- `RenderView` avec `World`, `Camera`, `IRenderSurface`, `UpdateMode`, `Pipeline`, `Presenter`, `Host`, `UIRoot`
- `IViewHost` interface (Resized, Closed, IsVisible)
- `RenderPipeline` multi-view (itère les views, applique surfaces, flush renderers par vue)
- `RenderTargetSurface` + `BackBufferSurface` + `RenderTargetPool`
- `InputRouter` pour le routage input par vue
- `GraphicsStateGuard` pour l'isolation GPU entre vues
- `IViewPresenter` / `IViewRenderPipeline` pour customiser le rendu/présentation par vue
- `DebugOverlay` par vue

### Direction cible

```
┌─────────────────────────────────────────────────────┐
│  WPF Application (MainWindow)                       │
│  ┌───────────────────────────────────────────────┐  │
│  │  EngineHost (unique)                          │  │
│  │  ┌─ GraphicsDevice (1 seul, partagé)          │  │
│  │  ├─ CasaEngineGame (1 seule instance)         │  │
│  │  │   ├─ SpriteBatch, FontSystem               │  │
│  │  │   ├─ Renderers (Mesh, Sprite, Line, 2D)    │  │
│  │  │   ├─ RenderPipeline                        │  │
│  │  │   ├─ AssetContentManager (1 seul cache)    │  │
│  │  │   ├─ InputComponent + InputRouter          │  │
│  │  │   └─ ViewManager                           │  │
│  │  │       ├─ RenderView "World"                │  │
│  │  │       │   ├─ Camera3d, RenderTargetSurface │  │
│  │  │       │   ├─ World (scene courante)        │  │
│  │  │       │   ├─ GizmoComponent, Grid, Axis    │  │
│  │  │       │   └─ EditorViewContext (per-view)  │  │
│  │  │       ├─ RenderView "Entity"               │  │
│  │  │       │   ├─ Camera3d, RenderTargetSurface │  │
│  │  │       │   ├─ World (entity preview)        │  │
│  │  │       │   └─ EditorViewContext (per-view)  │  │
│  │  │       ├─ RenderView "Sprite" ...           │  │
│  │  │       └─ ...                               │  │
│  │  └────────────────────────────────────────────│  │
│  └───────────────────────────────────────────────┘  │
│  ┌──────────┐ ┌──────────┐ ┌──────────┐             │
│  │ WPF Tab  │ │ WPF Tab  │ │ WPF Tab  │             │
│  │ Viewport │ │ Viewport │ │ Viewport │             │
│  │ (Image)  │ │ (Image)  │ │ (Image)  │             │
│  │ affiche  │ │ affiche  │ │ affiche  │             │
│  │ RT.Tex   │ │ RT.Tex   │ │ RT.Tex   │             │
│  └──────────┘ └──────────┘ └──────────┘             │
└─────────────────────────────────────────────────────┘
```

**Principes :**
1. **Un seul `CasaEngineGame`** avec un seul `GraphicsDevice`, un seul jeu de renderers, un seul `AssetContentManager`.
2. **N `RenderView`** dans le `ViewManager`, chacune avec sa propre `RenderTargetSurface`, `Camera`, `World`.
3. **Chaque onglet WPF** est un contrôle léger qui affiche la texture du `RenderTarget2D` de sa vue, et envoie les inputs au `InputRouter`.
4. **Données par vue** (camera, gizmo, grid, tools) portées par un `EditorViewContext` attaché à chaque `RenderView`.

---

## Priorités et règles

- Chaque PR doit compiler en `Debug` + `DebugEditor` et ne pas casser l'existant.
- On procède **du moteur vers le WPF** : d'abord les abstractions côté moteur, puis l'intégration WPF.
- L'éditeur existant (multi-instances) continue de fonctionner tant que la migration n'est pas complète (stratégie "feature flag" ou "#if" temporaire).
- Les demos standalone (`CasaEngine.Demos`) ne doivent pas être impactées.

---

## PR 1 — Shared GraphicsDevice dans D3D11Host

**Objectif :** Permettre à tous les contrôles WPF de partager un seul `GraphicsDevice`, pré-requis pour partager les ressources GPU (textures, modèles, render targets).

### 1.1 — Activer et stabiliser `UseASingleSharedGraphicsDevice`

- [x] Dans `D3D11Host`, le mode `UseASingleSharedGraphicsDevice` existe mais n'est pas activé. Le tester et corriger les bugs éventuels.
- [x] Vérifier que `_staticGraphicsDevice` est créé une seule fois et partagé entre toutes les instances de `D3D11Host`.
- [x] S'assurer que le Dispose du dernier `D3D11Host` dispose le `_staticGraphicsDevice` (reference counting via `_referenceCount`).
- [x] Vérifier la compatibilité threading : le rendering WPF utilise `CompositionTarget.Rendering` (thread UI) — s'assurer qu'un seul `D3D11Host` rend à la fois (pas de concurrence sur le device).
- [ ] Ajouter un test : ouvrir 2 contrôles `D3D11Host` côte à côte, vérifier qu'ils partagent le même `GraphicsDevice.Handle`.

### 1.2 — Activer le mode partagé dans l'éditeur

- [x] Dans `App.xaml.cs` ou au démarrage de l'éditeur, appeler `D3D11Host.UseASingleSharedGraphicsDevice = true` avant la création des contrôles.
- [ ] Vérifier que tous les `GameEditor` existants fonctionnent avec un GraphicsDevice partagé (même si chacun a encore son propre `CasaEngineGame`).
- [x] Traiter le cas de `PresentationParameters` / BackBuffer size : chaque `D3D11Host` a son propre `_cachedRenderTarget` de taille différente — le device partagé ne doit pas imposer une taille unique. Fix : `OnRenderSizeChanged` ne modifie plus les `PresentationParameters` partagés + `CasaEngineGame.ScreenSizeWidth/Height` stocke sa propre taille via `_screenSizeWidth/_screenSizeHeight` mis à jour par `OnScreenResized`.

✅ Critère : L'éditeur démarre avec un seul `GraphicsDevice`, tous les onglets affichent correctement, pas de device lost.

---

## PR 2 — EditorViewContext : données par vue

**Objectif :** Extraire les données spécifiques à chaque vue éditeur (caméra, gizmo, grid, axis, tools) dans un objet dédié, indépendant de `CasaEngineGame`.

### 2.1 — Créer la classe `EditorViewContext`

- [x] Créer `CasaEngine/Framework/Game/Components/Editor/EditorViewContext.cs` avec `ViewId`, `RenderView`, `World`, `Camera`, `RenderTargetSurface`, gizmo/grid/axis, providers input, name, type.
- [x] Créer `enum EditorViewType { World, Entity, Sprite, Animation2d, TileMap, Custom }` dans `CasaEngine/Framework/Game/Components/Editor/EditorViewType.cs`.
- [x] Le `EditorViewContext` possède et gère le cycle de vie de sa `RenderTargetSurface`, dispose la surface dans `Dispose()`. Gizmo/Grid/Axis seront extraits en PR 5.

### 2.2 — Rendre les composants éditeur instanciables par vue

- [ ] Vérifier que `GizmoComponent` peut fonctionner sans être un `GameComponent` global. (sera fait en PR 5)
- [ ] Même analyse pour `GridComponent` et `AxisComponent` : vérifier qu'ils peuvent être scopés à une vue. (sera fait en PR 5)
- [ ] Si ces composants font `GraphicsDevice.Clear()` ou manipulent des états globaux, les refactorer pour qu'ils s'appuient sur le `RenderFrame` de leur vue. (sera fait en PR 5)

### 2.3 — Stocker le contexte dans RenderView

- [x] Ajout de la propriété `object? Tag` sur `RenderView` pour porter le contexte éditeur.
- [x] Le constructeur de `EditorViewContext` place automatiquement l'instance dans `renderView.Tag`.

✅ Critère : `EditorViewContext` compile, les composants éditeur peuvent être instanciés N fois sans conflit.

---

## PR 3 — EngineHost : un seul CasaEngineGame pour l'éditeur

**Objectif :** Créer un composant central qui héberge l'unique instance de `CasaEngineGame` et pilote la boucle de jeu pour tout l'éditeur.

### 3.1 — Créer la classe `EngineHost`

- [x] `EngineHost.cs` créé dans `CasaEngine.EditorUI/Controls/`. Hérite de `WpfGame`. Propriété statique `Instance`. Possède l'unique `CasaEngineGame`.  Configure input et physics en mode éditeur. Événements `Started` et `FrameReady`.
- [x] Possède l'unique `CasaEngineGame`, délègue `Initialize/LoadContent/Update/Draw`.
- [x] À placer en tant que contrôle invisible dans `MainWindow` (fait en PR 7).
- [x] La boucle de mise à jour passe par `WpfGame` (qui utilise `CompositionTarget.Rendering` via `D3D11Host`).

### 3.2 — Créer `EditorViewDefinition`

- [x] `EditorViewDefinition.cs` créé dans `CasaEngine.EditorUI/Controls/`. Record avec `Name`, `ViewType`, `InitialWidth`, `InitialHeight`, `World?`, `ClearColor`, `UpdateMode?`, `ShowGizmo`, `ShowGrid`, `ShowAxis`.

### 3.3 — `RegisterEditorView` : création d'une vue complète

- [x] `RegisterEditorView(EditorViewDefinition)` : crée monde (vide si null), entité caméra (ArcBall 3D ou Camera3dIn2dAxis 2D), `RenderTargetSurface`, `ViewDefinition`, enregistre dans `ViewManager`, crée `EditorViewContext` (Gizmo/Grid/Axis en option), retourne `ViewId`.
- [x] `UnregisterEditorView(ViewId)` : nettoie composants, retire la vue du `ViewManager`, dispose le contexte.

### 3.4 — Boucle de jeu centralisée

- [x] La boucle est dans `WpfGame.Render()` → `Update()` + `Draw()` → `FrameReady` event pour les `ViewportControl`.
- [x] `EngineHost.CanRender` bloque le rendu tant que non initialisé.

✅ Critère : L'`EngineHost` compile, peut créer/détruire des vues, et une boucle de jeu unique tourne.

---

## PR 4 — ViewportControl WPF : contrôle léger d'affichage

**Objectif :** Remplacer les `GameEditor` (qui hébergent chacun un `CasaEngineGame` entier) par un contrôle WPF léger qui affiche simplement la texture `RenderTarget2D` d'une vue.

### 4.1 — Créer `ViewportControl`

- [x] Créer `CasaEngine.EditorUI/Controls/ViewportControl.cs` — hérite de `D3D11Host` (pas `Image`) pour réutiliser le mécanisme D3D11Image/D3D9 WPF déjà opérationnel :
  ```csharp
  public sealed class ViewportControl : D3D11Host, IViewHost
  {
      public ViewId ViewId { get; }
      int IViewHost.Width  => (int)ActualWidth;
      int IViewHost.Height => (int)ActualHeight;
      public void Attach(EngineHost host, ViewId viewId);
      public void Detach();
  }
  ```
  Note : `D3D11Image` est `internal` donc `ViewportControl` étend `D3D11Host` directement plutôt que `Image`.
- [x] Le `ViewportControl` implémente `IViewHost` :
  - `Resized` déclenché sur `OnRenderSizeChanged` → `NotifyResized()`.
  - `Closed` déclenché dans `Dispose(true)` avant de appeler `EngineHost.UnregisterEditorView`.
  - `IsVisible` reflète `Visibility == Visible && _contentLoaded`.
- [x] `Render(GameTime)` (override de `D3D11Host`) blit la texture `RenderTargetSurface.Texture` via `SpriteBatch` dans le back-buffer géré par `D3D11Host`.
- [x] `Attach(host, viewId)` pose `view.Host = this` puis appelle `host.ViewManager.HookViewHost(view)` pour que le `ViewManager` s'abonne aux événements `Resized`/`Closed` même si la vue a été créée avant le contrôle.
- [x] Ajout de `ViewManager.HookViewHost(RenderView)` et `ViewManager.UnhookViewHost(RenderView)` (méthodes publiques) pour permettre un câblage différé du host.

### 4.2 — Input bridging par ViewportControl

- [ ] ⚠️ **Différé à PR 6** — `WpfKeyboard` et `WpfMouse` n'acceptent qu'un `WpfGame` en paramètre. Le refactoring pour accepter `D3D11Host` / `FrameworkElement` est prévu en PR 6.
- [ ] Quand la souris est au-dessus d'un `ViewportControl`, cette vue devient la vue active (`InputRouter.SetActiveView(viewId)`).
- [ ] Support du focus clavier : `Focusable = true` posé dans `Initialize()`.

### 4.3 — Synchronisation texture → WPF

- [x] `Render()` récupère `ctx.Surface.Texture` et le blit full-screen via `SpriteBatch` dans le RT géré par `D3D11Host.OnRendering` — le mécanisme `_cachedRenderTarget` → `D3D11Image` → WPF est entièrement réutilisé.
- [ ] Optimisation OnDemand (skip blit si non re-rendu) — non bloquant pour PR4, à évaluer lors de PR9.

### 4.4 — Gestion du resize

- [x] Sur `OnRenderSizeChanged`, `ViewportControl.NotifyResized()` :
  1. Déclenche l'événement `IViewHost.Resized` → `ViewManager.OnHostResized` → `ViewManager.ViewResized`.
  2. Appelle `ctx.Surface.RequestResize(w, h)` (debounce dans `RenderTargetSurface`).
  3. Appelle `ctx.Camera.OnScreenResized(w, h)` pour mettre à jour les matrices de projection.
  4. Invalide la vue (`view.Invalidate()`) pour forcer un re-render.

✅ Critère : Un `ViewportControl` affiche le rendu d'une `RenderView`, gère le resize et les inputs de base (inputs différés à PR6).

---

## PR 5 — Migration des GameComponents éditeur vers le mode par-vue ✅

**Objectif :** Transformer `GizmoComponent`, `GridComponent`, `AxisComponent` pour qu'ils soient par-vue plutôt que globaux.

### 5.1 — Refactorer GizmoComponent

- [x] Actuellement `GizmoComponent` hérite de `DrawableGameComponent` et est ajouté à `Game.Components`. Il accède au `Game`, `GraphicsDevice`, `ViewManager.ActiveView.Camera`.
- [x] Refactorer pour que `GizmoComponent` puisse être associé à un `EditorViewContext` spécifique :
  - Ajout de la propriété `ActiveCamera` — utilisée à la place de `ViewManager.ActiveView.Camera` dans `Update()`.
  - Ajout de `DrawForView(in RenderFrame)` qui passe les matrices View/Projection/CameraPosition au Gizmo.
  - `Visible = false` dans `Initialize()` pour inhiber le rendu Phase 3 de `DrawWithEditor`.
- [x] `EditorViewPipeline.RenderGizmosAction` est câblé dans `EngineHost.RegisterEditorView`.

### 5.2 — Refactorer GridComponent

- [x] Même principe : `DrawForView(GraphicsDevice, in RenderFrame)` ajouté, `Visible = false` dans `LoadContent()`.
- [x] Correction du bug dans `Dispose()` : `Game.RemoveGameComponent<GridComponent>()` retirait TOUTES les instances ; remplacé par `base.Dispose(disposing)` uniquement.
- [x] `EditorViewPipeline.RenderGridAction` câblé dans `EngineHost.RegisterEditorView`.

### 5.3 — Refactorer AxisComponent

- [x] Même principe : `DrawForView(GraphicsDevice, in RenderFrame)` ajouté utilisant `frame.ViewportRect.Width/Height`, `Visible = false` dans `LoadContent()`.
- [x] `EditorViewPipeline.RenderAxisAction` câblé dans `EngineHost.RegisterEditorView`.

### 5.4 — Adapter PhysicsDebugViewRendererComponent

- [ ] Le debug view physics doit aussi être scopé par vue (on ne l'active que dans la vue World).
- [ ] Vérifier qu'il utilise le `RenderFrame` pour ses matrices.

✅ Critère : Chaque composant éditeur peut exister en N instances indépendantes, associées chacune à une vue.

---

## PR 6 — Input multi-vue dans l'éditeur ✅ (partiel)

**Objectif :** Faire en sorte que chaque vue éditeur reçoive ses propres événements d'entrée (souris, clavier, molette).

### 6.1 — InputRouter : routage par ViewId

- [x] `WpfKeyboard` et `WpfMouse` élargis pour accepter `D3D11Host` (au lieu de `WpfGame`) : les constructeurs utilisent maintenant `D3D11Host focusElement`.
- [x] `FocusOnMouseOver` déplacé de `WpfGame` vers `D3D11Host` (héritage → rétro-compatible).
- [x] `ViewportControl` crée ses propres instances `WpfKeyboard` et `WpfMouse` dans `Initialize()`.
- [x] `ViewportControl.GetKeyboardState()` et `GetMouseState()` exposent les états par viewport.
- [ ] Adapter l'`InputRouter` pour accepter des états injectés par `ViewId` :
  ```csharp
  public void InjectMouseState(ViewId viewId, MouseState state);
  public void InjectKeyboardState(ViewId viewId, KeyboardState state);
  ```
  (sera consommé par les systèmes de navigation caméra et gizmo en PR 9)

### 6.2 — Focus et capture input

- [x] `ViewportControl` souscrit à `MouseEnter` → appelle `ViewManager.SetActive(view)` pour activer la vue survolée.
- [ ] `InputRouter.CaptureInput(viewId)` pour les opérations drag (gizmo, pan camera).
- [ ] Gestion du focus clavier : seule la vue focusée reçoit les raccourcis clavier.

### 6.3 — Camera navigation par vue

- [ ] Les composants de navigation caméra doivent utiliser l'input de leur vue spécifique.
- [ ] Le code de navigation caméra dans `GameEditor2d.Update()` doit être migré dans le `EditorViewContext` de type 2D.
- [ ] Le code de navigation 3D doit être migré pareillement.

✅ Critère (partiel) : Chaque viewport possède ses propres providers keyboard/mouse ; MouseEnter active la vue dans le ViewManager.

---

## PR 7 — Migration des onglets éditeur existants

**Objectif :** Remplacer les 5 `GameEditor*` par des contrôles WPF utilisant `ViewportControl` + `EngineHost`.

### 7.1 — Migrer GameEditorWorld → WorldViewport

- [ ] Créer `WorldViewportControl` qui contient un `ViewportControl`.
- [ ] Au `Loaded`, demander à l'`EngineHost` de créer une vue de type `World` :
  - `EditorViewType.World`, gizmo ON, grid ON, axis ON.
  - Caméra 3D (ArcBallCamera ou FlyCamera).
  - World = la scène courante du projet.
- [ ] Migrer la logique spécifique : drag & drop d'entités, sélection via gizmo, chargement du monde.
- [ ] Les ViewModel existants (`WorldEditorViewModel`) restent côté WPF et communiquent avec le moteur via l'`EditorViewContext`.

### 7.2 — Migrer GameEditorEntity → EntityViewport

- [ ] Créer `EntityViewportControl` avec un `ViewportControl`.
- [ ] Vue de type `Entity` : gizmo ON, grid ON, axis ON, caméra 3D.
- [ ] World séparé (monde vide + entité prévisualisée).
- [ ] Migrer `OnDataContextChanged` → chargement de l'entité dans le monde de la vue.

### 7.3 — Migrer GameEditorSprite → SpriteViewport

- [ ] Créer `SpriteViewportControl` avec un `ViewportControl`.
- [ ] Vue de type `Sprite` : pas de gizmo, pas de grid, caméra 2D (`Camera3dIn2dAxisComponent`).
- [ ] Migrer le pan/zoom 2D depuis `GameEditor2d.Update()`.
- [ ] Migrer la logique de création d'entité sprite et affichage.

### 7.4 — Migrer GameEditorAnimation2d → Animation2dViewport

- [ ] Créer `Animation2dViewportControl` avec un `ViewportControl`.
- [ ] Vue de type `Animation2d` : même base que Sprite, avec playback d'animation.

### 7.5 — Migrer GameEditorTileMap → TileMapViewport

- [ ] Créer `TileMapViewportControl` avec un `ViewportControl`.
- [ ] Vue de type `TileMap` : caméra 2D, outils de peinture tile.

### 7.6 — Supprimer l'ancienne hiérarchie

- [ ] Une fois tous les onglets migrés et validés, supprimer `GameEditor`, `GameEditor2d`, `GameEditorWorld`, `GameEditorEntity`, `GameEditorSprite`, `GameEditorAnimation2d`, `GameEditorTileMap`.
- [ ] Supprimer `WpfGame` si il n'est plus utilisé (ou le garder pour la compatibilité standalone).
- [ ] Nettoyer les `#if EDITOR` dans `CasaEngineGame` qui ne sont plus nécessaires.

✅ Critère : Tous les onglets éditeur fonctionnent avec l'architecture uni-instance. L'ancienne hiérarchie est supprimée.

---

## PR 8 — Nettoyage des singletons / état statique

**Objectif :** S'assurer que l'état global ne pose plus de problème dans l'architecture uni-instance.

### 8.1 — Audit `RenderTargetPool.Shared`

- [ ] `RenderTargetPool.Shared` est un static écrasé par chaque `CasaEngineGame.Initialize()`. Avec une seule instance, ce n'est plus un problème, mais formaliser : le pool est possédé par l'unique `CasaEngineGame`.
- [ ] Ajouter une assertion : si `RenderTargetPool.Shared` est déjà défini et qu'on tente de le remplacer, c'est une erreur (double init).

### 8.2 — Vérifier `GameSettings`

- [ ] `GameSettings` est un `static class`. En mode uni-instance, c'est acceptable (un seul projet ouvert à la fois).
- [ ] Documenter explicitement que `GameSettings` est voué à devenir un `ProjectContext` si on veut supporter le multi-projets à l'avenir.

### 8.3 — Vérifier `AssetCatalog`

- [ ] `AssetCatalog` est statique et c'est correct pour un seul projet ouvert. Documenter cette contrainte.
- [ ] Vérifier qu'il n'y a pas de course aux données si plusieurs vues chargent des assets en parallèle (normalement non car tout est sur le thread UI/render).

### 8.4 — Vérifier `EngineEnvironment`

- [ ] `EngineEnvironment.ProjectPath` est statique. Correct pour un projet ouvert. Documenter.

✅ Critère : Tous les singletons sont audités, documentés, et ne causent pas de bugs avec l'architecture uni-instance.

---

## PR 9 — Performance et optimisation

**Objectif :** Optimiser le rendu multi-vue pour l'éditeur.

### 9.1 — UpdateMode par vue éditeur

- [ ] La vue World est en `RealTime` (elle doit être fluide pour la navigation).
- [ ] Les vues Entity, Sprite, Animation2d : `OnDemand` (re-rendu seulement quand le contenu change ou quand on interagit).
- [ ] La vue TileMap : `OnDemand` ou `Throttled` (selon les besoins de l'outil de peinture).
- [ ] Configurer les `UpdateMode` dans `EngineHost.RegisterEditorView()`.

### 9.2 — Visibilité des onglets

- [ ] Quand un onglet WPF est masqué (dans AvalonDock ou un TabControl), `ViewportControl` doit mettre `RenderView.IsVisible = false` pour que le pipeline le saute.
- [ ] Détecter la visibilité via `IsVisible` du contrôle WPF + événement `IsVisibleChanged`.

### 9.3 — Asset sharing

- [ ] Avec un seul `AssetContentManager` et un seul `GraphicsDevice`, les textures et modèles ne sont chargés qu'une fois en GPU.
- [ ] Vérifier qu'aucune vue ne dispose des assets partagés quand elle est fermée.
- [ ] Le `AssetContentManager` doit garder les assets tant que le projet est ouvert (pas de ref counting par vue).

### 9.4 — Mesurer les gains

- [ ] Mesurer la consommation mémoire GPU avant/après la migration (nombre de textures, taille des RT).
- [ ] Mesurer le framerate de l'éditeur avec 3+ onglets ouverts avant/après.

✅ Critère : L'éditeur avec 5 onglets consomme nettement moins de mémoire GPU et tourne plus fluide.

---

## PR 10 — Tests et validation

**Objectif :** S'assurer que la migration est solide.

### 10.1 — Tests manuels

- [ ] Ouvrir tous les onglets éditeur simultanément → pas de crash, pas de device lost.
- [ ] Naviguer la caméra dans la vue World → les autres vues ne bougent pas.
- [ ] Drag & drop d'entités dans World → fonctionne.
- [ ] Modifier une entité dans Entity → la preview se met à jour.
- [ ] Éditer un sprite / animation / tilemap → fonctionne.
- [ ] Redimensionner les onglets (docking) → les render targets se redimensionnent sans fuite mémoire.
- [ ] Masquer/afficher un onglet → le rendu s'arrête/reprend.
- [ ] Le mode standalone (`CasaEngine.Demos`, `CasaEngine.Launcher`) n'est pas affecté.

### 10.2 — Tests de régression

- [ ] Les demos existantes (`SplitScreenDemo`, `RenderToTextureDemo`) fonctionnent toujours.
- [ ] Le chargement/sauvegarde de mondes fonctionne.
- [ ] Le gizmo (translate, rotate, scale) fonctionne dans la vue World et Entity.
- [ ] Les raccourcis clavier fonctionnent dans la vue focusée.
- [ ] L'Undo/Redo fonctionne.

### 10.3 — Tests de stress

- [ ] Ouvrir/fermer un onglet 50 fois → pas de fuite mémoire GPU (`RenderTargetPool` compteur stable).
- [ ] Resize rapide (drag docking) pendant 10 secondes → pas de freeze, mémoire stable.
- [ ] Charger un monde lourd (beaucoup de meshs/textures) → un seul chargement pour toutes les vues.

✅ Critère : Tous les tests passent, l'éditeur est stable.

---

## Résumé de l'ordre d'exécution

| PR | Module | Dépendance | Objectif |
|---|---|---|---|
| **1** | `MonoGame.Framework.Wpf.Core` | — | GraphicsDevice partagé |
| **2** | `CasaEngine` (moteur) | — | `EditorViewContext` (données par vue) |
| **3** | `CasaEngine.EditorUI` | PR 1, 2 | `EngineHost` (runtime unique) |
| **4** | `CasaEngine.EditorUI` | PR 1, 3 | `ViewportControl` (WPF léger) |
| **5** | `CasaEngine` (moteur) | PR 2 | Composants éditeur par-vue |
| **6** | `CasaEngine` + `EditorUI` | PR 4, 5 | Input multi-vue |
| **7** | `CasaEngine.EditorUI` | PR 3–6 | Migration des onglets |
| **8** | `CasaEngine` (moteur) | PR 7 | Nettoyage singletons |
| **9** | Tous | PR 7 | Optimisation performance |
| **10** | Tous | PR 7–9 | Tests et validation |
