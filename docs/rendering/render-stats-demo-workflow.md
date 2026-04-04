# Render Stats Demo Workflow

Ce document decrit le workflow de validation de reference pour les `RenderStats` affichees dans `DebugOverlay`.

## Demos de reference

- `SplitScreenDemo` : verification rapide de l'overlay par vue sur deux viewports backbuffer.
- `ViewManagerSandbox` : verification des overlays avec ajout/retrait de vues et changement de `UpdateMode`.

## Build minimal

```powershell
dotnet build CasaEngine.Editor.MonoGame.sln -c Debug --no-restore
dotnet build CasaEngine.Demos/CasaEngine.Demos.csproj -c Debug --no-restore
```

Le premier build valide le moteur principal. Le second est necessaire quand le harness de demos evolue.

## Lancer directement une demo cible

La variable d'environnement `CASAENGINE_START_DEMO` accepte soit :

- un index numerique dans la liste `_demos` de `DemosGame`
- un fragment de titre insensible a la casse

Exemple PowerShell pour lancer directement `SplitScreenDemo` :

```powershell
Set-Location CasaEngine.Demos
$env:CASAENGINE_START_DEMO = '6'
dotnet run --project CasaEngine.Demos.csproj -c Debug --no-build
```

Exemple equivalent avec un fragment de titre :

```powershell
Set-Location CasaEngine.Demos
$env:CASAENGINE_START_DEMO = 'Split-screen'
dotnet run --project CasaEngine.Demos.csproj -c Debug --no-build
```

Nettoyage optionnel apres fermeture :

```powershell
Remove-Item Env:CASAENGINE_START_DEMO
```

## Checklist SplitScreenDemo

Attendus minimaux :

1. Le titre de fenetre devient `Split-screen demo (2 views)`.
2. Les deux moities de l'ecran affichent chacune un `DebugOverlay` en haut a gauche de leur viewport.
3. Chaque overlay affiche les lignes `Draws`, `FX`, `Tex`, `State`, `O`, `T`.
4. Dans la scene actuelle, les compteurs opaques sont strictement positifs et `T` reste a `0`.
5. Les positions camera affichees dans `Cam:` different entre la vue gauche et la vue droite.

## Checklist ViewManagerSandbox

Lancement direct :

```powershell
Set-Location CasaEngine.Demos
$env:CASAENGINE_START_DEMO = '9'
dotnet run --project CasaEngine.Demos.csproj -c Debug --no-build
```

Controles utiles :

- `F5` : toggle du debug overlay sur la vue 1
- `F6..F9` : cycle `UpdateMode` des vues 1 a 4
- `Tab` : ajoute une vue
- `Backspace` : retire la derniere vue
- `Space` : invalide les vues `OnDemand`

Points a verifier :

1. `F5` affiche ou masque l'overlay sans decalage hors viewport.
2. `Tab` et `Backspace` changent le nombre de vues sans fuite evidente du `RenderTargetPool`.
3. `F6..F9` modifie les modes affiches et permet de verifier que les stats restent bien scopees par vue.
4. `Space` force le rafraichissement des vues `OnDemand` et les overlays se mettent a jour ensuite.

## Notes d'interpretation

- Les stats sont stockees par `RenderView` et remises a zero au debut de chaque rendu de vue.
- `Draws` compte les draws executes par la vue, y compris le chemin instancie quand il est utilise.
- `Tex` compte les affectations de textures envoyees aux shaders par les materials et les `MaterialPropertyBlock`.
- `O` et `T` comptent les items opaques et transparents routes vers la vue courante.