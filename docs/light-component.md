# LightComponent

`LightComponent` est le composant d'auteuring des lumieres de scene pour le rendu forward.

## Authoring

- Ajouter `Light` depuis `Add Component` dans l'editeur.
- `Type` expose les trois modes supportes: `Directional`, `Point`, `Spot`.
- `Directional` et `Spot` utilisent l'orientation du composant pour leur direction.
- `Point` et `Spot` utilisent la position du composant.
- Regler `Color`, `SpecularColor` et `Intensity` pour tous les types.
- Regler `Range` pour `Point` et `Spot`.
- Regler `InnerConeAngleDegrees` et `OuterConeAngleDegrees` pour `Spot`.

## Serialisation et runtime

- Le composant est sauvegarde par `EditorEntityJsonSerializer` dans les worlds / entities.
- Le chargement runtime passe par `LightComponent.Load(JObject)`.
- Le `RenderPipeline` ne depend pas directement de `LightComponent`.
- La collecte runtime passe par `IRenderLightSource` puis `WorldLightCollector`.
- `LightingContext` transporte les directional, point et spot lights vers les shaders forward et skinned.

## Validation rapide

- Build runtime: `dotnet build .\CasaEngine.MonoGame.sln -nologo -p:WarningLevel=0`
- Build editor: `dotnet build .\CasaEngine.Editor\CasaEngine.Editor.csproj -t:Compile -nologo -p:WarningLevel=0`
- Build demos: `dotnet build .\CasaEngine.Demos\CasaEngine.Demos.csproj -nologo -p:WarningLevel=0`

Smoke demo visible:

```powershell
Push-Location .\CasaEngine.Demos
try {
    $env:CASAENGINE_START_DEMO = 'Material system demo'
    $env:CASAENGINE_CAPTURE_SCREENSHOT_PATH = '..\ai-agent\material-demo-lightcomponent-smoke.png'
    $env:CASAENGINE_CAPTURE_SCREENSHOT_DELAY_MS = '1500'
    dotnet run --no-build
}
finally {
    Pop-Location
}
```

La capture automatisee produite par cette commande est stockee dans `ai-agent/material-demo-lightcomponent-smoke.png`.