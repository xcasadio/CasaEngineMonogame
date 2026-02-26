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

- [ ] Créer `CasaEngine.EditorUI/Controls/EngineHost.cs` :
  ```csharp
  public class EngineHost : IDisposable
  {
      public CasaEngineGame Game { get; }
      public GraphicsDevice GraphicsDevice { get; }
      public ViewManager ViewManager => Game.GameManager.ViewManager;
      
      // Registry des vues éditeur
      private readonly Dictionary<ViewId, EditorViewContext> _viewContexts = new();
      
      public ViewId RegisterEditorView(EditorViewDefinition def);
      public void UnregisterEditorView(ViewId viewId);
      public EditorViewContext GetViewContext(ViewId viewId);
      
      public void Initialize();
      public void Update(GameTime gameTime);
      public void Draw(GameTime gameTime);
  }
  ```
- [ ] L'`EngineHost` possède l'unique `CasaEngineGame` et appelle `Initialize`, `Update`, `Draw` via les méthodes `WithEditor`.
- [ ] L'`EngineHost` est créé une seule fois au démarrage de l'éditeur (dans `MainWindow` ou `App`).
- [ ] Il pilote la boucle de mise à jour via `CompositionTarget.Rendering` (comme le faisait `D3D11Host` mais centralisé).

### 3.2 — Créer `EditorViewDefinition`

- [ ] Record/struct contenant les paramètres pour créer une vue éditeur :
  ```csharp
  public record EditorViewDefinition
  {
      public string Name { get; init; }
      public EditorViewType ViewType { get; init; }
      public int Width { get; init; }
      public int Height { get; init; }
      public World? World { get; init; }
      public Color ClearColor { get; init; } = Color.CornflowerBlue;
      public bool ShowGizmo { get; init; }
      public bool ShowGrid { get; init; }
      public bool ShowAxis { get; init; }
  }
  ```

### 3.3 — `RegisterEditorView` : création d'une vue complète

- [ ] `RegisterEditorView` fait :
  1. Créer une `RenderTargetSurface` de la taille demandée.
  2. Créer la caméra appropriée (3D ou 2D selon `ViewType`).
  3. Créer un `EditorViewContext` avec les composants éditeur demandés (gizmo, grid, axis).
  4. Appeler `ViewManager.CreateView(...)` pour enregistrer la `RenderView`.
  5. Retourner le `ViewId`.
- [ ] `UnregisterEditorView` fait le cleanup inverse : `ViewManager.Remove()`, dispose surface, dispose composants éditeur.

### 3.4 — Boucle de jeu centralisée

- [ ] L'`EngineHost` s'abonne à `CompositionTarget.Rendering` du WPF.
- [ ] À chaque frame :
  1. Calculer le `GameTime` (delta depuis le dernier render).
  2. Appeler `Game.UpdateWithEditor(gameTime)`.
  3. Appeler `Game.DrawWithEditor(gameTime)` — le `RenderPipeline` rend toutes les vues dans leurs `RenderTargetSurface` respectives.
  4. Signaler aux contrôles WPF que les textures sont prêtes (via événement ou dirty flag).
- [ ] Gérer l'ordre : la boucle centrale doit tourner même si certains onglets sont masqués (le `RenderView.IsVisible` gère ça dans le pipeline).

✅ Critère : L'`EngineHost` compile, peut créer/détruire des vues, et une boucle de jeu unique tourne.

---

## PR 4 — ViewportControl WPF : contrôle léger d'affichage

**Objectif :** Remplacer les `GameEditor` (qui hébergent chacun un `CasaEngineGame` entier) par un contrôle WPF léger qui affiche simplement la texture `RenderTarget2D` d'une vue.

### 4.1 — Créer `ViewportControl`

- [ ] Créer `CasaEngine.EditorUI/Controls/ViewportControl.cs` (hérite de `System.Windows.Controls.Image`) :
  ```csharp
  public class ViewportControl : Image, IViewHost, IDisposable
  {
      private EngineHost _engineHost;
      private ViewId _viewId;
      private D3D11Image _d3dImage;  // interop DX11 → WPF
      
      public ViewId ViewId => _viewId;
      public int Width => (int)ActualWidth;
      public int Height => (int)ActualHeight;
      
      public void Attach(EngineHost host, ViewId viewId);
      public void Detach();
  }
  ```
- [ ] Le `ViewportControl` implémente `IViewHost` :
  - `Resized` est déclenché sur `OnRenderSizeChanged`.
  - `Closed` est déclenché sur `Unloaded` ou `Dispose`.
  - `IsVisible` reflète `UIElement.IsVisible`.
- [ ] Le contrôle lit la texture `RenderTarget2D` de sa vue (`RenderTargetSurface.Texture`) et l'affiche via `D3D11Image` (interop Direct3D → WPF `ImageSource`).

### 4.2 — Input bridging par ViewportControl

- [ ] Chaque `ViewportControl` crée ses propres `WpfKeyboard` + `WpfMouse` scopés à ce contrôle.
- [ ] Les événements input WPF (MouseMove, MouseDown, KeyDown, etc.) sont capturés par le contrôle et transmis au `InputRouter` de l'`EngineHost` avec le `ViewId` de cette vue.
- [ ] Quand la souris est au-dessus d'un `ViewportControl`, cette vue devient la vue active pour l'input (`InputRouter.SetActiveView(viewId)`).
- [ ] Support du focus clavier : `Focusable = true`, le focus clavier WPF détermine quelle vue reçoit les événements clavier.

### 4.3 — Synchronisation texture → WPF

- [ ] Après chaque `RenderPipeline.Render()`, les `RenderTargetSurface` contiennent les images rendues.
- [ ] Le `ViewportControl` doit récupérer la texture D3D11 et l'afficher dans le `D3D11Image` WPF.
- [ ] Utiliser le même mécanisme que `D3D11Host` (copie vers un `_sharedRenderTarget` via `SpriteBatch`, puis `D3D11Image.SetBackBuffer()`).
- [ ] Optimisation : si la vue est en mode `OnDemand` et n'a pas été re-rendue, ne pas copier la texture.

### 4.4 — Gestion du resize

- [ ] Sur `OnRenderSizeChanged`, le `ViewportControl` :
  1. Notifie l'`EngineHost` du changement de taille.
  2. L'`EngineHost` resize la `RenderTargetSurface` de la vue (via `EnsureSize()` avec debounce).
  3. La caméra de la vue est mise à jour (`OnScreenResized`).
- [ ] Le debounce existant dans `RenderTargetSurface` protège contre les resize rapides (docking).

✅ Critère : Un `ViewportControl` affiche le rendu d'une `RenderView`, gère le resize et les inputs de base.

---

## PR 5 — Migration des GameComponents éditeur vers le mode par-vue

**Objectif :** Transformer `GizmoComponent`, `GridComponent`, `AxisComponent` pour qu'ils soient par-vue plutôt que globaux.

### 5.1 — Refactorer GizmoComponent

- [ ] Actuellement `GizmoComponent` hérite de `DrawableGameComponent` et est ajouté à `Game.Components`. Il accède au `Game`, `GraphicsDevice`, `ViewManager.ActiveView.Camera`.
- [ ] Refactorer pour que `GizmoComponent` puisse être associé à un `EditorViewContext` spécifique :
  - Recevoir la caméra et le monde de son `EditorViewContext` plutôt que du `ViewManager.ActiveView`.
  - Dessiner dans la `RenderTargetSurface` de sa vue (via le pipeline de rendu par vue).
- [ ] Option : créer un `IViewRenderPipeline` éditeur qui, après le `DefaultViewPipeline`, dessine le gizmo/grid/axis de la vue.
  ```csharp
  public class EditorViewPipeline : IViewRenderPipeline
  {
      public void RenderView(GraphicsDevice gd, RenderView view, in RenderFrame frame, IReadOnlyList<IViewFlushableRenderer> renderers)
      {
          DefaultViewPipeline.Instance.RenderView(gd, view, in frame, renderers);
          
          var ctx = view.Tag as EditorViewContext;
          ctx?.Gizmo?.Draw(gd, in frame);
          ctx?.Grid?.Draw(gd, in frame);
          ctx?.Axis?.Draw(gd, in frame);
      }
  }
  ```
- [ ] Le `GizmoComponent` ne doit plus être un `GameComponent` mais un objet standalone qui expose `Update(GameTime, RenderFrame)` et `Draw(GraphicsDevice, RenderFrame)`.

### 5.2 — Refactorer GridComponent

- [ ] Même principe : extraire de `GameComponent`, rendre instanciable par vue.
- [ ] Le grid se dessine dans le `RenderFrame` de sa vue associée (matrices View/Projection de la caméra de la vue).
- [ ] Paramètres (taille, espacement, couleur) portés par le `EditorViewContext` ou directement sur le `GridComponent`.

### 5.3 — Refactorer AxisComponent

- [ ] Même principe : extraire de `GameComponent`, rendre instanciable par vue.
- [ ] L'axis se dessine dans un coin du viewport de la vue.

### 5.4 — Adapter PhysicsDebugViewRendererComponent

- [ ] Le debug view physics doit aussi être scopé par vue (on ne l'active que dans la vue World).
- [ ] Vérifier qu'il utilise le `RenderFrame` pour ses matrices.

✅ Critère : Chaque composant éditeur peut exister en N instances indépendantes, associées chacune à une vue.

---

## PR 6 — Input multi-vue dans l'éditeur

**Objectif :** Faire en sorte que chaque vue éditeur reçoive ses propres événements d'entrée (souris, clavier, molette).

### 6.1 — InputRouter : routage par ViewId

- [ ] L'`InputRouter` existant utilise `ViewManager.ScreenToView()` pour déterminer quelle vue est sous la souris. En mode éditeur multi-vue, la source d'input est le `ViewportControl` WPF, pas une position écran absolue.
- [ ] Adapter le `InputRouter` pour accepter des événements input associés à un `ViewId` spécifique :
  ```csharp
  public void InjectMouseState(ViewId viewId, MouseState state);
  public void InjectKeyboardState(ViewId viewId, KeyboardState state);
  ```
- [ ] Les `WpfKeyboard` / `WpfMouse` de chaque `ViewportControl` alimentent le `InputRouter` avec leur `ViewId`.

### 6.2 — Focus et capture input

- [ ] Quand un `ViewportControl` a le focus WPF (clic ou MouseEnter), il notifie l'`EngineHost` pour activer la vue correspondante dans le `ViewManager` (`SetActive`).
- [ ] Le `InputRouter.CaptureInput(viewId)` déjà existant doit fonctionner pour les opérations drag (gizmo, pan camera, etc.).
- [ ] Gestion du focus clavier : seule la vue focusée reçoit les raccourcis clavier (Suppr, Ctrl+Z, etc.).

### 6.3 — Camera navigation par vue

- [ ] Les composants de navigation caméra (orbit, pan, zoom, fly) doivent utiliser l'input de leur vue spécifique et manipuler la caméra de leur `EditorViewContext`.
- [ ] Le code de navigation caméra dans `GameEditor2d.Update()` (pan 2D, zoom molette) doit être migré dans le `EditorViewContext` de type 2D.
- [ ] Le code de navigation 3D (si présent dans `GameEditorWorld` ou un `CameraController`) doit être migré pareillement.

✅ Critère : Chaque vue reçoit ses inputs indépendamment, naviguer la caméra dans une vue n'affecte pas les autres.

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
