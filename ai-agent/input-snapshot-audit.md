# Audit — Flux d'input brut et consommateurs

## Resume

Le codebase dispose deja de trois briques importantes :

1. un type de snapshot brut `WindowInputSnapshot`
2. un routage par vue via `InputRouter` -> `ViewInputContext`
3. un contrat UI runtime via `IUIViewRuntime` -> `UIViewInputState`

Le probleme actuel n'est donc pas l'absence totale d'architecture, mais le fait que plusieurs chemins lisent encore l'input brut en parallele au lieu de partager une source canonique par frame.

## Acquisition brute actuelle

### Source fenetre partagee

- `CasaEngine/Framework/Input/IWindowInputSource.cs`
  - definit `WindowInputSnapshot`
- `CasaEngine/Framework/Input/Win32WindowInputSource.cs`
  - produit un snapshot via `GetSnapshot()`
  - expose aussi `IRawInputSource`, `IKeyboardStateProvider`, `IMouseStateProvider`
  - lit encore `Mouse.GetState()` en fallback dans `GetWindowMouseState()`
  - reconstruit l'etat clavier et souris via Win32 + hook molette

### Sources brutes alternatives encore presentes

- `CasaEngine/Engine/Input/InputDeviceStateProviders/KeyboardStateProvider.cs`
  - lit `Keyboard.GetState()`
- `CasaEngine/Engine/Input/InputDeviceStateProviders/MouseStateProvider.cs`
  - lit `Mouse.GetState()`
- `CasaEngine/Engine/Input/InputDeviceStateProviders/GamePadStateProvider.cs`
  - lit `GamePad.GetState(...)`
- `CasaEngine/Engine/Input/InputSequence/InputManager.cs`
  - lit aussi directement les devices MonoGame

## Routage moteur actuel

### InputComponent

- `CasaEngine/Framework/Input/InputComponent.cs`
  - met a jour `KeyboardManager`, `MouseManager`, `GamePadManager`
  - si `InputRouter` existe, consomme `TryDispatchContext(out ViewInputContext)`
  - sinon retombe sur ses providers globaux

### InputRouter

- `CasaEngine/Framework/Input/InputRouter.cs`
  - connait la vue cible, la vue sous le pointeur, le focus clavier, la capture et la modalite
  - produit `ViewInputContext`
  - calcule deja : `ScreenPosition`, `LocalPosition`, `VerticalWheelDelta`, `HorizontalWheelDelta`
  - stocke un etat precedent de souris par vue pour calculer les deltas

### Donnees routees

- `CasaEngine/Framework/Input/ViewInputContext.cs`
  - contient deja l'essentiel des donnees utiles aux controleurs runtime
  - le contexte est donc deja assez proche de la cible architecturale

## Chemins UI / MGUI actuels

### UIRoot par vue

- `CasaEngine/Framework/GUI/MGUI/UIRoot.cs`
  - cree un `MainRenderer` MGUI par vue
  - expose `IsPointerOverUI`, `IsKeyboardCaptured`, `HasModalInput`
  - ne porte pas encore explicitement la capture pointeur dans `UIViewInputState`

### Host de rendu MGUI par vue

- `CasaEngine/Framework/GUI/MGUI/ViewRenderHost.cs`
  - implemente `IRenderHost` et `IRawInputSource`
  - relit l'input brut via `_keyboardStateProvider?.GetState() ?? Keyboard.GetState()`
  - relit l'input brut via `_mouseStateProvider?.GetState() ?? Mouse.GetState()`
  - convertit ensuite la souris vers l'espace local du viewport

### MainRenderer MGUI

- `MGUI/MGUI.Shared/Rendering/MainRenderer.cs`
  - recoit un `IRawInputSource`
  - construit `UpdateArgs` a partir de `GetMouseState()` et `GetKeyboardState()` a chaque `PreviewUpdate`
  - maintient son propre `InputTracker`

## Consommateurs editor/runtime

### Viewport editeur

- `CasaEngine.Editor/Controls/WorldViewportPanel.cs`
  - utilise `ViewportRelativeMouseStateProvider` pour fournir un `MouseState` local a la vue routee
  - active/focalise la vue mais ne doit pas devenir l'orchestrateur d'input

### Controleurs runtime editeur

- `CasaEngine.Editor/Runtime/EditorViewportCameraController.cs`
- `CasaEngine.Editor/Runtime/EditorViewportGizmoController.cs`
  - consomment le `ViewInputContext`
  - la direction architecturale est deja correcte : comportements derives du contexte route

## Doublons et divergences identifies

### 1. Acquisition brute non centralisee par frame

- `Win32WindowInputSource` sait produire un snapshot mais il n'y a pas encore de cache frame-level partage.
- MGUI et le moteur peuvent relire la source brute separement au cours de la meme frame.

### 2. Fallback directs aux APIs MonoGame

- `ViewRenderHost` retombe encore sur `Keyboard.GetState()` / `Mouse.GetState()`.
- `InputComponent` conserve des providers globaux qui peuvent ne pas correspondre au meme instant logique que MGUI.

### 3. Conversion locale du pointeur faite a plusieurs endroits

- `InputRouter` calcule deja `ScreenPosition` et `LocalPosition`.
- `ViewRenderHost` reconvertit aussi la souris en espace viewport-local pour MGUI.
- `ViewportRelativeMouseStateProvider` rederive lui aussi la souris locale.

### 4. Contrat UI incomplet pour le routage

- `UIViewInputState` expose hover clavier/modalite.
- il n'expose pas explicitement la capture pointeur UI, alors que la capture est un etat de priorite important.

## Direction de migration recommande

### Etape 1

Centraliser l'acquisition brute dans une source unique par frame, idealement via un cache partage au niveau `IWindowInputSource` ou d'un provider adjacent.

### Etape 2

Faire lire MGUI et le moteur a partir de ce snapshot partage, sans fallback a `Keyboard.GetState()` / `Mouse.GetState()` dans les hosts de haut niveau.

### Etape 3

Conserver `InputRouter` comme endroit canonique pour :

- la selection de la vue cible
- les coordonnees locales
- les deltas de molette
- la decision capture/focus/modalite

### Etape 4

Etendre le contrat UI pour exposer explicitement la capture pointeur et fiabiliser la politique de blocage modal/capture au niveau central.

## Impact sur les taches suivantes

- Tache 2 : consolider `WindowInputSnapshot` en vraie source canonique de frame
- Tache 3 : brancher `ViewRenderHost` et le moteur sur cette source
- Tache 4 : eviter les conversions locales redondantes la ou `ViewInputContext` suffit
- Tache 5 : garder `WorldViewportPanel` passif
- Tache 6 : enrichir `UIViewInputState`
- Tache 7 : ajouter une couverture de non-regression sur modalite/capture/routage