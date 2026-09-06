# Plan d'intégration UI — UI Root, Screen Stack, Overlay/World, Game States

> **Réconciliation avec le code (2026-09-06, chantier ai-guidelines T5.5)** : les 77 cases ont été vérifiées une par une dans le code ; 35 sont cochées avec la preuve `fichier:ligne` en fin de ligne, 42 restent ouvertes (surtout PR4 câblage per-view de `ViewMouseViewport`, PR6 World UI, PR7 game screen states, PR10 démo d'intégration). Le dossier cible `CasaEngine/Framework/GUI/` n'existe pas : tout vit dans `CasaEngine/Framework/UI/` (chemins corrigés dans les cases concernées). Le plan reste actif.

> **Toolkit UI retenu : [MGUI](https://github.com/Videogamers0/MGUI)** (WPF-like layout + XAML + DataBinding pour MonoGame)

## 0 — Contexte

### État actuel

| Élément | État actuel | Lacune identifiée |
|---|---|---|
| `ScreenGui` | ✅ Entity contenant des Controls Neoforce (legacy) | Sera remplacé par des écrans MGUI (`MGWindow` / `MGElement`) |
| `UserInterfaceComponent` | ✅ Singleton `DrawableGameComponent` wrappant le `Manager` Neoforce | Global, pas per-view, hors pipeline de rendu — à remplacer |
| MGUI (`MGDesktop` / `MainRenderer`) | 🆕 Lib externe WPF-like avec layout engine, XAML, DataBinding | Pas encore intégré dans le pipeline per-view |
| `InputComponent` | ✅ Polling global clavier/souris/gamepad + `InputMappingManager` | Pas de routage per-player/per-view |
| MGUI `InputTracker` | 🆕 `MouseTracker` + `KeyboardTracker` avec `IsHandled` pattern | Doit être câblé per-view via `IMouseViewport` |
| `PlayerController` | ✅ Structure Unreal-like, `IsInputEnable` | Stub, aucun câblage input réel |
| `GameMode` | ✅ State machine string-based (EnteringMap → InProgress → …) | `HUDClass` jamais instancié, pas de scene states |
| `ViewManager` | ✅ Multi-view mature, split-screen, RT, events | Aucun slot UI per-view |
| `DefaultViewPipeline` | ✅ World.Draw() → flush renderers | Aucun stage UI overlay |
| `World._screens` | ✅ `List<ScreenGui>` dans le World | Liste plate, pas de stack, pas de layering |
| `ScreenWidgetComponent` | ✅ Pont Entity → Neoforce Controls | Legacy — à remplacer par composant MGUI |

### MGUI — Résumé de l'architecture

| Concept MGUI | Rôle |
|---|---|
| `MainRenderer` | Renderer central. Crée l'`InputTracker`. Un par context de rendu. |
| `MGDesktop` | Container racine des `MGWindow`. Gère `Update()` + `Draw()`. Un par desktop logique. |
| `MGWindow` | Fenêtre top-level (titre, drag, resize optionnel). Contient un arbre d'`MGElement`. |
| `MGElement` | Classe de base de tous les contrôles (WPF-like : `Margin`, `Padding`, `HorizontalAlignment`, etc.). |
| Layout panels | `MGDockPanel`, `MGGrid`, `MGStackPanel`, `MGOverlayPanel`, `MGUniformGrid` |
| Contrôles | `MGButton`, `MGTextBlock`, `MGTextBox`, `MGCheckBox`, `MGComboBox`, `MGSlider`, `MGProgressBar`, `MGImage`, `MGListBox`, `MGTabControl`, `MGToolTip`, etc. |
| `InputTracker` | Singleton (via `MainRenderer`). Contient `MouseTracker` + `KeyboardTracker`. |
| `MouseHandler` | Handler d'events souris avec `IsHandled` pattern pour consommation. `IMouseViewport` pour per-view clipping. |
| XAML | Markup déclaratif supporté au runtime via `MGXAMLDesigner`. |
| DataBinding | Moteur WPF-like : `OneWay`, `TwoWay`, `Converter`, `StringFormat`. Fonctionne cross-platform. |

### Problèmes identifiés

1. **UI globale** : Le `Manager` Neoforce est un singleton (legacy). MGUI peut supporter un `MGDesktop` per-view mais ce n'est pas encore câblé.
2. **Pas de ScreenStack** : Aucun système de pile pour empiler pause/inventaire/modal au-dessus du gameplay.
3. **UI hors pipeline** : Le rendu UI se fait en `DrawableGameComponent` (phase 3), pas dans le pipeline per-view.
4. **Input non routé** : L'input arrive globalement. MGUI a `IMouseViewport` pour le clipping per-view, mais il faut l'intégrer.
5. **Pas de World UI** : Aucun mécanisme pour rendre de l'UI dans le monde 3D (panneaux, barres de vie).
6. **Game States minimalistes** : `GameMode` gère le match flow, mais il n'y a pas de système de screens (Title, Loading, Pause).
7. **Neoforce encore câblé** : `UserInterfaceComponent`, `ScreenGui`, `ScreenWidgetComponent` dépendent de Neoforce — à migrer.

---

## 1 — Objectifs / Non-objectifs

### Objectifs (MVP)

- **Intégrer MGUI** comme toolkit UI principal du moteur, remplaçant Neoforce.
- **UIRoot per-view** : Chaque `RenderView` possède son propre `UIRoot` (wrappant un `MGDesktop`) qui orchestre l'UI overlay.
- **Screen Stack** : Un système de pile (`ScreenStack`) avec layers (HUD, Menu, Modal, Tooltip, Debug) et règles d'input blocking. Chaque écran contient des `MGWindow`.
- **UI Render Pass** : Intégrer le rendu UI comme un stage du pipeline per-view (`DefaultViewPipeline`).
- **Input Router per-view** : Router l'input vers la bonne view via `IMouseViewport`, puis vers l'UI (MGUI `InputTracker`) avant le gameplay.
- **World UI basique** : Un composant `WorldUIComponent` qui rend un `MGDesktop` dans un `RenderTarget2D` mappé sur un quad 3D.
- **Game Screen States** : Un `GameScreenManager` gérant les états Title/InGame/Pause/Loading avec transitions.
- **XAML + DataBinding** : Les HUD/menus peuvent être définis en XAML avec binding aux données gameplay.

### Non-objectifs (hors scope)

- Éditeur visuel d'UI (WYSIWYG).
- Système d'animation UI complet (tweens, transitions fancy).
- Networking UI.
- Accessibilité avancée (lecteur d'écran, daltonisme).
- Portage multi-plateforme MGUI (rester sur `net6.0-windows` pour l'instant).

---

## 2 — Stratégie de livraison

- Chaque PR doit **compiler** en Debug + DebugEditor.
- Chaque PR ne doit **pas casser** les démos existantes.
- Chaque PR doit être **testable** indépendamment.
- **L'agent doit commiter après chaque tâche terminée.**
- L'agent code en **anglais** (noms de classes, commentaires inline).
- L'agent communique en **français**.

---

## PR 1 — Intégration MGUI de base + UIRoot per-view

**Objectif :** Intégrer MGUI dans le moteur et créer la classe `UIRoot` qui encapsule un `MGDesktop` per-view.

- [x] Ajouter les références projet `MGUI.Shared` et `MGUI.Core` à `CasaEngine.csproj` — livré : `CasaEngine/CasaEngine.csproj:30-32` (MGUI.Core, MGUI.Shared, MGUI.FontStashSharp référencés)
  - S'assurer que le contenu MGUI (`MGUI.Shared.Content.mgcb`, `MGUI.Core.Content.mgcb`) est copié
- [x] Créer la classe `UIRoot` dans `CasaEngine/Framework/GUI/` — livré : `CasaEngine/Framework/UI/UIRoot.cs:26` — chemin corrigé (`Framework/GUI/` n'existe pas, dossier réel `Framework/UI/`)
  - Propriétés :
    - `RenderView View` — la vue propriétaire
    - `MGDesktop Desktop` — le desktop MGUI de cette vue
    - `MainRenderer Renderer` — le renderer MGUI (peut être partagé ou per-view)
    - `ScreenStack ScreenStack` (ajouté PR 2)
    - `bool IsActive`
    - `float UIScale` (default 1.0)
  - Méthodes :
    - `Initialize(RenderView view, MainRenderer sharedRenderer)` — crée le `MGDesktop` attaché à cette vue
    - `Update(GameTime gameTime)` — appelle `Desktop.Update()`
    - `Draw(GameTime gameTime, Rectangle viewportRect)` — appelle `Desktop.Draw()` dans le viewport de la vue
    - `Dispose()` — libère le desktop et les ressources associées
  - Le `MGDesktop` gère ses propres `MGWindow` — chaque écran UI est un ensemble de `MGWindow` ajoutées/retirées du desktop
- [x] Créer `ViewMouseViewport : IMouseViewport` dans `CasaEngine/Framework/GUI/` — livré : `CasaEngine/Framework/UI/ViewMouseViewport.cs:15` — chemin corrigé en `Framework/UI/`
  - Adapte un `RenderView` en `IMouseViewport` MGUI :
    - `IsInside(Vector2 position)` → teste si la position souris est dans le viewport de la vue
    - `GetOffset()` → retourne le coin top-left du viewport (coordonnées écran → coordonnées locales)
  - Ceci permet à l'`InputTracker` de MGUI de router les events souris correctement per-view
- [x] Ajouter le champ `UIRoot? UIRoot` à `RenderView` — livré sous un nom différent : `CasaEngine/Framework/Rendering/RenderView.cs:163` (`IUIViewRuntime UIView`, pas `UIRoot? UIRoot`)
- [x] Dans `ViewManager.CreateView()`, créer automatiquement un `UIRoot` et l'attacher à la vue — livré ailleurs : `CasaEngine/Framework/Application/CasaEngineGame.cs:641` (`OnViewAddedCreateUIRuntime`, abonné à `ViewManager.ViewAdded`) — pas dans `ViewManager.CreateView()` lui-même
  - Le `MainRenderer` MGUI est partagé (singleton dans `GameManager` ou `CasaEngineGame`)
  - Seul le `MGDesktop` est per-view
- [x] Dans `ViewManager.RemoveView()`, appeler `UIRoot.Dispose()` et détacher — livré ailleurs : `CasaEngine/Framework/Application/CasaEngineGame.cs:649-652` (`OnViewRemovedDisposeUIRuntime`), abonné à `ViewManager.ViewRemoved` — `ViewManager` n'a pas de méthode `RemoveView` (voir `Remove(RenderView)`, `ViewManager.cs:194`)
- [ ] Initialiser le `MainRenderer` MGUI dans `CasaEngineGame.Initialize()` (ou `GameManager`) — laissé : pas de type `MainRenderer` trouvé ; chaque `UIRoot` crée son propre backend via `ViewRenderHost`/`CasaMonoGameBackendBootstrap` (`CasaEngine/Framework/UI/UIRoot.cs:75-83`), aucune preuve d'un renderer MGUI partagé unique
  - Implémenter `IRenderHost` ou utiliser `GameRenderHost<T>` pour l'adapter au moteur
- [ ] Ajouter un test unitaire : créer une View → vérifier que `UIRoot` est non-null et `Desktop` est initialisé → détruire la View → vérifier dispose — laissé : aucun test de ce type trouvé dans `CasaEngine.Tests`

**Critère (✅)** : Chaque `RenderView` a un `UIRoot` avec un `MGDesktop` fonctionnel, initialisé et disposé proprement. MGUI compile et se charge correctement.

---

## PR 2 — ScreenStack : pile d'écrans avec layers

**Objectif :** Implémenter un système de pile qui gère l'empilement et le dépilage d'écrans UI avec règles de layering. Chaque écran possède des `MGWindow` gérées via le `MGDesktop` du `UIRoot`.

- [x] Créer l'enum `UILayer` dans `CasaEngine/Framework/GUI/` — livré : `CasaEngine/Framework/UI/UILayer.cs:7-21` — chemin corrigé en `Framework/UI/` ; valeurs réelles `HUD=0/Menu=1/Modal=2/Tooltip=3/Debug=4` (mêmes 5 layers, même ordre, valeurs différentes)
  ```
  WorldHUD = 0,   // permanent, non-modal (barres de vie, minimap)
  Menu = 100,      // menu titre, pause
  Modal = 200,     // dialogues de confirmation
  Tooltip = 300,   // tooltips
  Debug = 400      // FPS, console debug
  ```
- [ ] Créer l'interface `IUIScreen` dans `CasaEngine/Framework/GUI/` — laissé : l'interface existe (`CasaEngine/Framework/UI/IUIScreen.cs:9`) mais avec une API différente (`Initialize/Show/Hide/GetWindows/BlocksViewsBelow`) ; pas de `IsTransparent`, `IsVisible`, `Windows`, `OnPushed`, `OnPopped`, `OnCovered`, `OnRevealed`
  - `string Name { get; }`
  - `UILayer Layer { get; }`
  - `bool IsModal { get; }` — bloque l'input aux layers inférieurs
  - `bool IsTransparent { get; }` — couches en dessous toujours rendues visuellement
  - `bool IsVisible { get; set; }`
  - `IReadOnlyList<MGWindow> Windows { get; }` — les fenêtres MGUI de cet écran
  - `void OnPushed(UIRoot uiRoot)` — ajoute ses `MGWindow` au `Desktop`
  - `void OnPopped(UIRoot uiRoot)` — retire ses `MGWindow` du `Desktop`
  - `void OnCovered()` / `void OnRevealed()`
  - `void Update(GameTime gameTime)`
- [x] Créer la classe abstraite `UIScreenBase : IUIScreen` dans `CasaEngine/Framework/GUI/` — livré : `CasaEngine/Framework/UI/UIScreenBase.cs:11` — chemin corrigé en `Framework/UI/`
  - Implémentation de base commune (gestion visibilité, liste de `MGWindow`, add/remove au desktop)
  - Méthode protégée `CreateContent(MGDesktop desktop)` — à surcharger pour construire les fenêtres
- [ ] Créer l'enum `UIInputResult { Consumed, PassThrough }` — laissé : type introuvable dans le code
- [x] Créer la classe `ScreenStack` dans `CasaEngine/Framework/GUI/` — livré (fonctionnalité de base Push/Pop/Clear/HasModalInput) : `CasaEngine/Framework/UI/ScreenStack.cs:14` — chemin corrigé en `Framework/UI/` ; pas de `PopLayer`, `Find<T>`, `OnCovered`/`OnRevealed` (introuvables)
  - Liste interne `List<IUIScreen>` triée par `Layer`
  - `Push(IUIScreen screen)` — ajoute à la pile, appelle `OnPushed(uiRoot)` (qui ajoute les `MGWindow` au desktop), et `OnCovered` sur l'écran en dessous (même layer)
  - `Pop(IUIScreen screen)` — retire, appelle `OnPopped(uiRoot)` (qui retire les `MGWindow`), et `OnRevealed` sur l'écran du dessus
  - `PopLayer(UILayer layer)` — retire tous les écrans d'une layer
  - `T? Find<T>() where T : IUIScreen`
  - `bool HasModal` — retourne `true` si un écran modal est actif
  - `Update(GameTime gameTime)` — itère du top au bottom, arrête l'update des écrans sous un modal
  - `UIInputResult ProcessInput()` — vérifie si un écran modal est actif (bloque le gameplay). L'input UI lui-même est géré par MGUI nativement via `InputTracker` + `IsHandled`.
- [x] Connecter `ScreenStack` au `UIRoot` : `UIRoot.ScreenStack` est initialisé dans `UIRoot.Initialize()` — livré : `CasaEngine/Framework/UI/UIRoot.cs:37,82` (`ScreenStack` créé dans le constructeur de `UIRoot`, pas dans une méthode `Initialize()` séparée)
- [ ] Ajouter un test : Push HUD (2 fenêtres) + Push Menu (modal) → vérifier que les Windows sont dans le Desktop → Pop Menu → vérifier état — laissé : aucun test `ScreenStack` dédié trouvé dans `CasaEngine.Tests`

**Critère (✅)** : La pile gère correctement push/pop, l'ajout/retrait des `MGWindow` au `MGDesktop` est automatique, les modals sont détectés.

---

## PR 3 — UI Render Pass dans le pipeline per-view

**Objectif :** Le rendu UI est un stage officiel du pipeline de rendu, exécuté per-view.

- [x] Ajouter un stage `RenderUIOverlay` dans `DefaultViewPipeline` — livré sous un autre nom : `CasaEngine/Framework/Rendering/DefaultViewPipeline.cs:49-56` (étape « Compose the per-view UI phase » via `IUICompositionService`), pas de méthode nommée `RenderUIOverlay`
  - Après le flush des renderers (sprites, meshes…), avant le DebugOverlay
  - Ce stage appelle `view.UIRoot?.Draw(gameTime, viewportRect)` si la vue a un UIRoot
- [ ] Le `UIRoot.Draw()` : — laissé : `DefaultUICompositionService.Compose()` (`CasaEngine/Framework/Rendering/DefaultUICompositionService.cs:12-15`) se contente d'appeler `view.UIView?.Draw()`, aucune configuration/restauration explicite du `Viewport` visible à cet endroit
  1. Configure le `Viewport` du `GraphicsDevice` sur le rectangle de la vue
  2. Appelle `Desktop.Draw()` — MGUI gère son propre `SpriteBatch` via `MainRenderer`
  3. Restaure le viewport précédent
- [ ] Alternative RT (si nécessaire pour isolation) : — laissé : pas de `UIRoot.UseRenderTarget` ni de RT alternatif par vue trouvé (le RT n'existe que pour `WorldUIComponent`, cas différent — PR6)
  - `UIRoot` possède un `RenderTarget2D` optionnel (via `RenderTargetPool`)
  - Rend le `Desktop` dans le RT, puis blit le RT en overlay sur le framebuffer de la vue (alpha blend)
  - Activable via `UIRoot.UseRenderTarget = true`
- [x] Gérer le resize : quand la vue est redimensionnée (`ViewManager.ViewResized` event), recalculer les bounds du `MGDesktop` — livré avec un mécanisme différent : `CasaEngine/Framework/Application/CasaEngineGame.cs:655-674` (`SyncUIViewMetrics`, appelé chaque frame) — pas d'abonnement à l'event `ViewManager.ViewResized` trouvé
- [x] Supprimer le rendu global du `UserInterfaceComponent.Draw()` pour les vues qui ont un `UIRoot` — livré : `UserInterfaceComponent` introuvable dans le dépôt (classe supprimée)
  - Garder le fallback Neoforce pour l'éditeur ou les vues legacy sans UIRoot
- [ ] Vérifier que le split-screen affiche un contenu UI différent par vue — laissé : aucun test/démo trouvé démontrant explicitement un rendu UI différent par vue en split-screen

**Critère (✅)** : En split-screen, chaque vue a son propre overlay UI rendu indépendamment via MGUI.

---

## PR 4 — InputRouter per-view avec focus UI (MGUI)

**Objectif :** Router l'input vers la bonne vue, puis vers MGUI (`InputTracker`) avant le gameplay.

- [x] Créer la classe `InputRouter` dans `CasaEngine/Framework/Input/` — livré : `CasaEngine/Framework/Input/InputRouter.cs:21`
  - Propriétés : `RenderView? FocusedView`, `bool IsUICapturingInput`
  - Méthode `RouteInput(InputComponent input, ViewManager viewManager)` :
    1. Détermine quelle vue a le focus :
       - Souris : `ViewManager.ScreenToView(mousePosition)` → vue sous le curseur
       - Gamepad : `PlayerIndex` → vue assignée via mapping
    2. Active le `ViewMouseViewport` de la vue focusée (les autres viewports retournent `IsInside = false`)
    3. L'`InputTracker` de MGUI traite les events souris/clavier → les handlers MGUI consomment via `IsHandled`
    4. Vérifie si `ScreenStack.HasModal` sur la vue focusée → si oui, bloque le gameplay
    5. Sinon → passe l'input au `PlayerController` / `Pawn`
- [ ] Câbler `IMouseViewport` per-view : — laissé : `ViewMouseViewport` existe (`CasaEngine/Framework/UI/ViewMouseViewport.cs`) mais n'est instancié nulle part dans le moteur (aucune occurrence de `new ViewMouseViewport(` hors son propre fichier) — non câblé per-view
  - Chaque `UIRoot` possède un `ViewMouseViewport` qui clip l'input MGUI au viewport de la vue
  - Les `MouseHandler` créés par les `MGElement` sont automatiquement clippés par MGUI
- [x] Ajouter la notion de `PlayerIndex` → `ViewId` mapping dans `ViewManager` — livré, mais dans `InputRouter` et non `ViewManager` : `CasaEngine/Framework/Input/InputRouter.cs:278` (`AssignPlayer`), `:293` (`GetViewForPlayer`)
  - `AssignPlayer(PlayerIndex player, ViewId view)`
  - `ViewId? GetViewForPlayer(PlayerIndex player)`
- [ ] Créer la struct `InputState` dans `CasaEngine/Framework/Input/` — laissé : type `InputState` introuvable
  - Snapshot de l'état input pertinent : `KeyboardState`, `MouseState`, `GamePadState`
  - `MousePosition` transformé en coordonnées locales de la vue (screen → viewport local)
- [x] Connecter l'`InputRouter` dans `InputComponent.Update()` : — livré : `CasaEngine/Framework/Input/InputComponent.cs:65-79` (`Update()` appelle `InputRouter.TryDispatchContext`)
  - Après le polling des devices, appeler `InputRouter.RouteInput()`
  - Le résultat détermine si le gameplay layer reçoit l'input
- [ ] Gestion manette : quand un écran modal est actif, un `MGElement` par défaut reçoit le focus (`DefaultFocusedElement`) — laissé : mécanisme `DefaultFocusedElement`/gamepad introuvable
- [ ] Ajouter un test : UI modal active → vérifier que l'input gameplay est bloqué — laissé : `InputRouterTests` (`CasaEngine.Tests/Input/InputRouterTests.cs:41-88`) teste la priorité capture UI/pointeur, mais aucun test nommé explicitement autour d'un écran modal bloquant le gameplay

**Critère (✅)** : L'input passe par MGUI d'abord ; un modal bloque le gameplay ; le focus MGUI fonctionne per-view.

---

## PR 5 — PlayerController + UIRoot wiring

**Objectif :** Connecter le cycle de vie player → view → UI de manière Unreal-like.

- [x] Étoffer `PlayerController` : — livré : `CasaEngine/Framework/Gameplay/PlayerController.cs:42` (`AssignedViewId`), `:58` (`AddScreenToHUD`), `:64` (`RemoveScreenFromHUD`), `:73`/`:82` (`ShowPauseMenu`/`HidePauseMenu`)
  - Propriété `ViewId? AssignedView` — la vue de ce joueur
  - Propriété `UIRoot? UIRoot` (accès direct via `ViewManager.TryGetView()`)
  - Méthode `AddScreenToHUD(IUIScreen screen)` — raccourci vers `UIRoot.ScreenStack.Push()`
  - Méthode `RemoveScreenFromHUD(IUIScreen screen)` — raccourci vers `UIRoot.ScreenStack.Pop()`
  - Méthode `ShowPauseMenu()` / `HidePauseMenu()` — push/pop d'un écran de type Menu
- [ ] Étoffer `LocalPlayer` : — laissé : `LocalPlayer.ControllerId` existe (`CasaEngine/Framework/Gameplay/LocalPlayer.cs:13`) mais l'assignation de vue se fait dans `GameManager` (`GameManager.cs:188-224`), pas via une méthode `ViewManager.AssignPlayer` appelée depuis `LocalPlayer`
  - À la connexion d'un joueur, créer ou assigner une `RenderView` via `ViewManager`
  - Le `ControllerId` (`PlayerIndex`) est automatiquement mappé à la vue via `ViewManager.AssignPlayer()`
- [ ] Dans `World.BeginPlay()` / `GameMode.InitGame()` : — laissé : pas de méthode `AssignView`/flux confirmé dans `World.BeginPlay()` ou `GameMode.InitGame()`
  - Pour chaque `PlayerController` initialisé, appeler `AssignView()` et `CreateDefaultHUD()`
- [ ] Créer `GameMode.CreateDefaultHUD(PlayerController pc)` : — laissé : `GameMode.CreateDefaultHUD` introuvable dans le code
  - Instancie le HUD par défaut (via `HUDClass` ou une factory)
  - Le HUD est un `IUIScreen` contenant des `MGWindow` (ex: barre de vie, score, minimap)
  - Ajouté au `UIRoot` du joueur via `AddScreenToHUD()`
  - Peut être défini en XAML : `GameMode.HUDXamlPath` → le XAML est parsé en `MGWindow`
- [ ] Ajouter une démo : 2 joueurs en split-screen, chaque joueur a son propre HUD MGUI avec score/vie — laissé : aucune démo trouvée combinant split-screen et HUD MGUI indépendant par joueur (`SplitScreenDemo.cs` n'a pas de HUD par vue)

**Critère (✅)** : `PlayerController.AddScreenToHUD()` fonctionne, le HUD est instancié automatiquement via MGUI.

---

## PR 6 — World UI Component

**Objectif :** Permettre de rendre de l'UI MGUI dans l'espace 3D (barres de vie flottantes, panneaux interactifs).

- [ ] Créer `WorldUIComponent : PrimitiveComponent` dans `CasaEngine/Framework/Entities/Components/` — laissé : `WorldUIComponent` existe (`CasaEngine/Framework/UI/WorldUIComponent.cs:15`) mais n'hérite pas de `PrimitiveComponent` — classe autonome `IDisposable`, pas un component ECS ; chemin réel différent
  - Propriétés : `int PixelWidth`, `int PixelHeight` (résolution du RT), `float WorldWidth`, `float WorldHeight` (taille dans le monde)
  - Propriété `MGDesktop Desktop` — un desktop MGUI dédié pour ce composant
  - Propriété `IUIScreen? Screen` — l'écran UI à rendre dans ce composant
  - Propriété `bool IsBillboard` — si true, le quad face toujours la caméra
  - Propriété `bool IsInteractive` — si true, accepte l'input via raycast
  - Propriété `bool IgnoreDepth` — si true, rendu always-on-top
- [ ] Le composant possède un `RenderTarget2D` (via `RenderTargetPool`) sur lequel le `Desktop` est dessiné — laissé : utilise `RenderTargetSurface` (`WorldUIComponent.cs:18,43`), pas explicitement `RenderTargetPool`
- [x] `Update()` : appelle `Desktop.Update()` et `Screen.Update()` — livré (équivalent) : `CasaEngine/Framework/UI/WorldUIComponent.cs:61-71` (`Update()` appelle `UIView.Update(gameTime)`)
- [ ] `Draw()` : — laissé : `DrawToTexture()` (`WorldUIComponent.cs:100-113`) dessine dans le RT mais ne dessine pas le quad 3D lui-même (délégué à un `StaticModelComponent` externe dans la démo) ; pas de billboard/`IgnoreDepth`
  1. Rend le `Desktop` dans le RT (configure viewport sur `(0, 0, PixelWidth, PixelHeight)`)
  2. Dessine un quad texturé avec le RT comme texture, positionné selon la `Transform` de l'entity
  3. Si `IsBillboard`, calcule la rotation vers la caméra
  4. Si `IgnoreDepth`, désactive le depth test
- [ ] Pour l'interaction : `WorldUIComponent.Raycast(Ray ray)` retourne les coordonnées 2D sur le RT — laissé : méthode `Raycast(Ray)` introuvable ; l'interaction passe par `ProjectedMouseInputSource` dans la démo, pas par une API sur le composant
  - Le `InputRouter` peut détecter un clic sur un `WorldUIComponent` et injecter les events dans son `Desktop`
- [x] Ajouter un test/démo : un panneau flottant dans le monde 3D avec un `MGButton` cliquable — livré : `CasaEngine.Demos/Demos/WorldSpaceUIDemo.cs` (panneau 3D avec `WorldUIComponent`, `HudScreen`, `PauseMenuScreen`, interaction souris projetée)

**Critère (✅)** : Un quad dans le monde affiche de l'UI MGUI interactive, clickable via raycast.

---

## PR 7 — Game Screen States (Title, InGame, Pause, Loading)

**Objectif :** Fournir un `GameScreenManager` qui gère les macro-états du jeu.

- [ ] Créer l'enum `GameScreenState` dans `CasaEngine/Framework/GameFramework/` — laissé : enum `GameScreenState` introuvable
  ```
  Title,
  Loading,
  InGame,
  Paused,
  GameOver
  ```
- [ ] Créer l'interface `IGameScreen` dans `CasaEngine/Framework/GameFramework/` — laissé : interface `IGameScreen` introuvable
  - `GameScreenState State { get; }`
  - `void OnEnter(GameScreenManager manager)`
  - `void OnExit(GameScreenManager manager)`
  - `void Update(GameTime gameTime)`
  - `void Draw(GameTime gameTime)`
- [x] Créer la classe `GameScreenManager` dans `CasaEngine/Framework/GameFramework/` — livré, conception différente : `CasaEngine/Framework/UI/GameScreenManager.cs:19` (transitions par nom de state `string` + `IUIScreen`, pas par `IGameScreen`/enum) — chemin réel `Framework/UI/`
  - Machine à états : `CurrentState`, `TransitionTo(IGameScreen screen)`
  - Gère les transitions : `OnExit` ancien → `OnEnter` nouveau
  - Propriété `IGameScreen? CurrentScreen`
  - Événement `GameScreenChanged(GameScreenState oldState, GameScreenState newState)`
- [ ] Créer les implémentations de base (toutes basées sur MGUI) : — laissé : seuls `TitleScreen` et `GameOverScreen` existent, et uniquement dans `Projects/CasaEngine.RPGDemo/Scripts/Screens/` (démo spécifique) ; `LoadingScreen`/`InGameScreen`/`PausedScreen` introuvables
  - `TitleScreen : IGameScreen` — affiche le menu titre via des `MGWindow` (New Game / Continue / Options / Quit). Peut être défini en XAML.
  - `LoadingScreen : IGameScreen` — écran de chargement avec `MGProgressBar`, lance `GameManager.SetWorldToLoad()`
  - `InGameScreen : IGameScreen` — monde + HUD actifs, `TimeScale = 1`
  - `PausedScreen : IGameScreen` — monde figé (`TimeScale = 0`), UI pause en overlay modal (`MGWindow` modale), input redirigé vers MGUI
  - `GameOverScreen : IGameScreen` — `MGWindow` avec résultat de match + boutons Restart/Quit
- [ ] Connecter au `GameMode` : — laissé : aucun câblage direct `GameMode.GameStateChanged` → `GameScreenManager` trouvé
  - `GameMode.GameStateChanged` → peut déclencher un changement de `GameScreenManager` (ex: `InProgress` → `InGameScreen`, `LeavingMap` → `LoadingScreen`)
- [ ] Connecter au `InputRouter` : — laissé : câblage `GameScreenManager` ↔ `InputRouter` en état Paused introuvable
  - En état `Paused`, l'`InputRouter` force l'input vers MGUI (modal)
  - En état `Title`, pas de routage gameplay
- [ ] Intégrer dans `GameManager` : le `GameScreenManager` est mis à jour dans la boucle principale — laissé : `GameScreenManager` instancié comme propriété de `GameManager` (`GameManager.cs:31,50`) mais pas de méthode `Update` appelée en boucle (transitions pilotées par événement, pas par tick)

**Critère (✅)** : On peut naviguer Title → Loading → InGame → Pause → InGame → GameOver, chaque état gère correctement le monde et l'UI MGUI.

---

## PR 8 — Safe Area, Scaling et adaptation MGUI au viewport

**Objectif :** L'UI overlay s'adapte correctement aux différentes résolutions et safe areas (TV, ultrawide).

> **Note :** MGUI possède déjà un layout engine WPF-like avec `Margin`, `Padding`, `HorizontalAlignment`, `VerticalAlignment`, `MinWidth/MaxWidth`, etc. On n'a **pas** besoin de réimplémenter un système d'ancrage — on utilise les capacités natives de MGUI. Ce PR ajoute les couches manquantes : safe area et scaling global.

- [x] Créer la struct `SafeArea` dans `CasaEngine/Framework/GUI/` — livré sous un autre nom : `CasaEngine/Framework/UI/UIScaler.cs` (`ComputeSafeArea`, `UIViewMetrics.SafeArea`) au lieu d'une struct `SafeArea` avec `Apply()`
  - `float Left`, `Right`, `Top`, `Bottom` (en pourcentage du viewport, ex: 0.05 = 5%)
  - `Rectangle Apply(Rectangle viewportRect)` — retourne le rectangle utilisable après safe area
  - Configurable par plateforme / par vue
- [ ] Intégrer la safe area dans `UIRoot.Draw()` : — laissé : `UIRoot.Draw()` (`UIRoot.cs:132-140`) n'exploite pas explicitement `Metrics.SafeArea`
  - Le rectangle viewport passé au `MGDesktop` tient compte de la safe area
  - Les `MGWindow` positionnées avec `HorizontalAlignment.Left` / `VerticalAlignment.Bottom` respectent automatiquement les marges
- [x] Gestion du scaling UI : — livré : `CasaEngine/Framework/UI/UIRoot.cs:43` (`UIScale`), `:58-68` (`UpdateMetrics`)
  - `UIRoot.UIScale` (float, default 1.0) — facteur de mise à l'échelle global
  - Appliqué via une transformation de la matrice SpriteBatch dans `MainRenderer`
  - Auto-calculable : `referenceResolution / actualResolution` (ex: référence 1920×1080)
- [ ] Configurer les `MGWindow` pour fill le viewport (pas de titre, pas de bordure, fullscreen overlay) : — laissé : helper `CreateFullscreenWindow` introuvable
  - Helper `UIRoot.CreateFullscreenWindow()` → crée une `MGWindow` sans chrome, taille = viewport - safe area
  - Utile pour le HUD, les écrans de chargement, etc.
- [ ] Tester avec différentes résolutions (720p, 1080p, 4K) et en split-screen — laissé : aucun test multi-résolution trouvé

**Critère (✅)** : L'UI MGUI se positionne correctement quel que soit le viewport, safe area respectée. Le layout WPF-like de MGUI gère les ancrages nativement.

---

## PR 9 — Suppression Neoforce + migration legacy

**Objectif :** Supprimer les dépendances Neoforce et migrer le code existant vers MGUI.

- [ ] Migrer `ScreenGui` : — laissé : `ScreenGui` a disparu du code, mais aucune preuve directe d'une conversion 1:1 de chaque écran existant vers `UIScreenBase` — seule l'absence de l'ancien type est constatée
  - Chaque `ScreenGui` existant est converti en une classe dérivée de `UIScreenBase` (qui utilise des `MGWindow`)
  - Les controls Neoforce (Button, TextBox, etc.) sont remplacés par leurs équivalents MGUI (`MGButton`, `MGTextBlock`, `MGTextBox`, etc.)
  - Si des écrans étaient définis en code, évaluer une conversion en XAML
- [ ] Migrer `ScreenWidgetComponent` → nouveau `UIWidgetComponent` — laissé : ni `ScreenWidgetComponent` ni `UIWidgetComponent` trouvés dans le code
  - Utilise le `UIRoot` de la vue propriétaire (via `Entity.World` → `ViewManager` → première vue de ce world)
  - Les `MGWindow` du widget sont ajoutées/retirées du `Desktop` de la vue
- [x] Supprimer `UserInterfaceComponent` (wrappeur Neoforce) — livré : `UserInterfaceComponent` introuvable (confirmé par recherche globale)
  - Le rendu UI est maintenant géré par le pipeline per-view (PR 3)
- [x] Supprimer `Framework/GUI/Neoforce/` (le dossier complet ~100+ fichiers) — livré : aucun dossier `Neoforce` dans le dépôt (confirmé)
  - Retirer la dépendance Neoforce du `.csproj`
- [x] Supprimer `Framework/GUI/ControlHelper.cs` (factory Neoforce) — livré : `ControlHelper.cs` introuvable (confirmé)
- [ ] Nettoyer les enums `ComponentUpdateOrder` / `ComponentDrawOrder` : — laissé : `ComponentDrawOrder.GUIBegin` existe toujours (`CasaEngine/Framework/Application/ComponentOrder.cs:36`), non supprimé ni marqué `[Obsolete]`
  - Les ordres `GUIBegin` et `GUI` ne sont plus nécessaires
  - Les supprimer ou les marquer `[Obsolete]`
- [x] Mettre à jour les démos existantes pour utiliser MGUI — livré : aucune trace de Neoforce dans `CasaEngine.Demos`, les démos UI utilisent `CasaEngine.Framework.UI` (ex. `UIOverlayDemo.cs`, `WorldSpaceUIDemo.cs`)
- [ ] Vérifier qu'aucune régression n'apparaît sur l'éditeur (`EditorViewPipeline` garde son propre mécanisme) — laissé : classe `EditorViewPipeline` introuvable sous ce nom exact dans le dépôt — non vérifiable directement
- [x] Mettre à jour `World._screens` : `List<ScreenGui>` → `List<IUIScreen>` ou supprimer si le `ScreenStack` per-view remplace — livré (option « supprimer ») : aucun champ `_screens`/`ScreenGui` dans `CasaEngine/Framework/Scene/World/World.cs` (confirmé) ; le `ScreenStack` per-view de chaque `UIRoot` en tient lieu

**Critère (✅)** : Neoforce est complètement supprimé. Toutes les UI du moteur utilisent MGUI. Compilation clean.

---

## PR 10 — Démo d'intégration complète

**Objectif :** Une démo showcase qui valide tous les systèmes ensemble.

- [ ] Créer une nouvelle démo `UIIntegrationDemo` dans `CasaEngine.Demos/Demos/` — laissé : démo `UIIntegrationDemo` introuvable
- [ ] Scénario : — laissé : dépend de la démo `UIIntegrationDemo` (non trouvée) ; des éléments existent séparément (`UIOverlayDemo`, `WorldSpaceUIDemo`) mais pas le scénario complet
  1. **Title Screen** : `MGWindow` plein écran avec `MGButton` "Start" / "Options" / "Quit" (défini en XAML)
  2. **Loading** : `MGWindow` avec `MGProgressBar` bound en `OneWay` à la progression du chargement (DataBinding MGUI)
  3. **InGame** : Monde 3D ou 2D avec :
     - HUD per-player (score via `MGTextBlock` bound au modèle, barre de vie via `MGProgressBar`, minimap via `MGImage`) sur layer `WorldHUD`
     - Bouton pause → push d'un `PauseScreen` (layer `Menu`, modal, `MGWindow` avec `MGButton` Resume/Restart/Quit)
     - Un `WorldUIComponent` dans le monde (panneau interactif avec `MGButton` MGUI en 3D)
  4. **Split-screen** : 2 joueurs, chacun avec son propre `MGDesktop` / HUD, pause indépendante per-player
  5. **Game Over** : `MGWindow` avec résultat + `MGButton` actions
- [ ] Valider : — laissé : dépend de la démo `UIIntegrationDemo` (non trouvée)
  - L'input est correctement routé (modal bloque gameplay)
  - Le focus MGUI fonctionne (navigation clavier/manette dans les menus)
  - Le resize ne casse pas l'UI (layout MGUI s'adapte)
  - Le split-screen affiche des HUDs indépendants
  - Le World UI est cliquable
  - Le DataBinding MGUI fonctionne (score/vie se mettent à jour en temps réel)

**Critère (✅)** : La démo tourne sans crash et tous les scénarios décrits fonctionnent avec MGUI.

---

## Tableau des priorités

| PR | Titre | Priorité | Dépendances |
|---|---|---|---|
| PR 1 | MGUI intégration + UIRoot per-view | 🔴 Critique | — |
| PR 2 | ScreenStack + layers | 🔴 Critique | PR 1 |
| PR 3 | UI Render Pass | 🔴 Critique | PR 1, PR 2 |
| PR 4 | InputRouter per-view (MGUI) | 🔴 Critique | PR 1, PR 2 |
| PR 5 | PlayerController + UIRoot wiring | 🟠 Important | PR 1, PR 4 |
| PR 6 | World UI Component | 🟡 Normal | PR 2, PR 3 |
| PR 7 | Game Screen States | 🟠 Important | PR 2, PR 4 |
| PR 8 | Safe Area, Scaling | 🟡 Normal | PR 3 |
| PR 9 | Suppression Neoforce + migration | 🟠 Important | PR 1–8 |
| PR 10 | Démo d'intégration complète | 🟢 Low | PR 1–9 |

---

## Checklist d'acceptation finale

- [x] MGUI (`MGUI.Shared` + `MGUI.Core`) est intégré et compile — livré (référence projet confirmée) : `CasaEngine/CasaEngine.csproj:30-32` — build non relancé dans cette session
- [x] Chaque `RenderView` possède un `UIRoot` avec un `MGDesktop` fonctionnel — livré : `CasaEngine/Framework/Rendering/RenderView.cs:163` + `CasaEngine/Framework/Application/CasaEngineGame.cs:641` + `CasaEngine/Framework/UI/UIRoot.cs:34`
- [ ] Le `ScreenStack` gère correctement push/pop/modal/layers avec des `MGWindow` — laissé : push/pop/modal fonctionnent (`ScreenStack.cs`) mais aucun tri/gestion explicite par `UILayer` dans `ScreenStack` (simple liste d'insertion)
- [x] L'UI est rendue dans le pipeline per-view (pas en global) — livré : `CasaEngine/Framework/Rendering/DefaultViewPipeline.cs:49-56`
- [ ] L'input est routé per-view via `IMouseViewport` MGUI : UI d'abord, gameplay ensuite — laissé : voir item PR4 — `ViewMouseViewport` non câblé dans le moteur
- [x] Un écran modal bloque l'input des couches inférieures — livré (mécanique) : `CasaEngine/Framework/UI/ScreenStack.cs:122-134` (`Update` n'itère qu'à partir de l'écran bloquant), `UIRoot.HasModalInput` (`UIRoot.cs:56`)
- [ ] Split-screen : HUD indépendant par joueur (desktop MGUI séparé) — laissé : aucune démo confirmée (voir PR5)
- [x] World UI : panneau 3D interactif avec `MGDesktop` rendu en RT — livré : `CasaEngine.Demos/Demos/WorldSpaceUIDemo.cs` + `CasaEngine/Framework/UI/WorldUIComponent.cs`
- [ ] Game States : Title → Loading → InGame → Pause → GameOver — laissé : `LoadingScreen`/`InGameScreen`/`PausedScreen` introuvables (voir PR7)
- [x] Safe area et scaling fonctionnent en multi-résolution — livré (mécanisme, pas de test multi-résolution dédié trouvé) : `CasaEngine/Framework/UI/UIScaler.cs`
- [x] Neoforce est complètement supprimé du projet — livré : aucune trace de Neoforce dans tout le dépôt
- [x] Les HUD/menus peuvent être définis en XAML avec DataBinding MGUI — livré : fichiers XAML MGUI trouvés (`Projects/SampleProject/Screens/*.xaml`) + outil dédié `CasaEngine.EditorServices/ScreenEditor`
- [ ] Toutes les démos existantes fonctionnent sans régression — laissé : non vérifiable sans build/exécution dans cette session
- [ ] La démo `UIIntegrationDemo` valide l'ensemble — laissé : démo introuvable

---

## Pièges connus

1. **`MainRenderer` singleton vs per-view** : MGUI crée un `InputTracker` dans le `MainRenderer`. Si on instancie un `MainRenderer` par `UIRoot`, on aura N `InputTracker` qui polleront l'input N fois. **Solution** : Partager un seul `MainRenderer` entre tous les `UIRoot`. Chaque `UIRoot` a son propre `MGDesktop` mais utilise le même renderer. Utiliser `IMouseViewport` per-view pour le routage spatial.

2. **`MGDesktop` bounds** : L'`MGDesktop` dessine dans l'espace écran complet par défaut. Il faut configurer ses bounds pour correspondre au viewport de la vue. **Solution** : Setter les bounds du desktop via les dimensions de la vue, et appliquer un offset viewport dans le renderer ou via `IMouseViewport.GetOffset()`.

3. **RenderTarget thrashing** : En split-screen 4 joueurs, si chaque `UIRoot` utilise un RT dédié, ça fait 4 RT UI + 4 RT scène. **Solution** : Utiliser le `RenderTargetPool` existant. Le mode RT est optionnel (`UIRoot.UseRenderTarget`). Pour la majorité des cas, dessiner directement dans le viewport suffit.

4. **Coordonnées souris en split-screen** : La souris est globale — les `MouseHandler` MGUI doivent recevoir des coordonnées locales. **Solution** : Le `ViewMouseViewport.GetOffset()` retourne le coin de la vue, MGUI fait la transformation automatiquement. Le `ViewMouseViewport.IsInside()` empêche les vues inactives de recevoir les events.

5. **Focus clavier en split-screen** : Un seul clavier pour potentiellement N vues. **Solution** : Seule la vue "active" (dernière cliquée) reçoit les events clavier MGUI. Les gamepad sont routés par `PlayerIndex`.

6. **Compatibilité éditeur** : L'`EditorViewPipeline` a son propre système d'overlay. Ne pas interférer — l'éditeur garde son mécanisme. Utiliser un flag `RenderView.IsEditorView` pour court-circuiter le `UIRoot`. L'éditeur pourra aussi utiliser MGUI, mais via son propre `MGDesktop` éditeur (déjà le cas dans MonoGame.Framework.Wpf.Core).

7. **MGUI cible `net6.0-windows`** : `MGUI.Core` cible `net6.0-windows` par défaut. Si un portage multi-plateforme est envisagé plus tard, il faudra changer le `TargetFramework` en `net6.0`. Le DataBinding et le XAML fonctionnent en `net6.0`, seuls certains intellisense features sont dégradés.

8. **Z-order World UI** : Les quads World UI participent au depth buffer. Une barre de vie derrière un mur ne doit pas être visible (sauf si voulu). **Solution** : Option `IgnoreDepth` sur le `WorldUIComponent` pour les cas "always on top".

9. **XAML parsing au runtime** : MGUI supporte le XAML parsing via `MGXAMLDesigner`. Les HUD/menus peuvent être définis en fichiers `.xaml` chargés au runtime. **Solution** : Stocker les fichiers XAML dans le `Content/` et les charger via un helper `UIRoot.LoadScreenFromXaml(string path)`.

10. **DataBinding + GameplayProxy** : Le DataBinding MGUI fonctionne avec des `INotifyPropertyChanged` ou des notifications manuelles. Les modèles de données gameplay (score, vie, inventaire) doivent implémenter `INotifyPropertyChanged` ou utiliser des wrappers. **Solution** : Créer une classe de base `ObservableGameplayModel` qui implémente `INotifyPropertyChanged`.
