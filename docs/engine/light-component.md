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
- Regler `CastShadows` pour indiquer si la lumiere doit produire une shadow map runtime.
- `CastShadows` est `false` par defaut sur `LightComponent` pour preserver la compatibilite des scenes existantes et eviter un cout GPU surprise.

## Flags shadows des composants rendus

- Les composants derives de `PrimitiveComponent` exposent `CastShadows` et `ReceiveShadows`.
- `StaticModelComponent` et `SkinnedMeshComponent` sauvegardent et restaurent ces deux flags via la serialisation editeur/runtime.
- Les deux flags valent `true` par defaut cote composants rendus, ce qui conserve le comportement historique si aucune scene ne les surcharge.

## Regle effective composant + material

Le rendu applique la regle suivante avant chaque draw call :

```text
effectiveCastShadows = component.CastShadows && material.CastShadows
effectiveReceiveShadows = component.ReceiveShadows && material.ReceiveShadows
```

Cela permet de desactiver le casting ou la reception soit au niveau instance, soit au niveau material, sans casser le reste du pipeline.

## Serialisation et runtime

- Le composant est sauvegarde par `EditorEntityJsonSerializer` dans les worlds / entities.
- Le chargement runtime passe par `LightComponent.Load(JObject)`.
- Quand le world est selectionne dans l'editeur, le panneau `World settings` expose aussi les reglages de scene `Shadows.Enabled`, `Resolution`, `DepthBias`, `NormalBias` et `MaxDistance` pour la shadow map directional V1.
- Le `RenderPipeline` ne depend pas directement de `LightComponent`.
- La collecte runtime passe par `IRenderLightSource` puis `WorldLightCollector`.
- `LightingContext` transporte uniquement les lumieres visibles; le binding GPU forward passe par `ForwardLightBinder`.

## Limites V1 du workflow shadows

- V1 supporte une shadow map directional dans le pipeline forward.
- La shadow map stocke la profondeur en `SurfaceFormat.Single` pour eviter la quantification 8 bits visible sous forme de bandes d'auto-ombrage; le fallback `Color` ne doit servir qu'aux materiels qui ne supportent pas ce format.
- Les materials lit forward et les meshes skinnes peuvent recevoir cette shadow map si leurs flags effectifs l'autorisent.
- L'ambient global et l'environnement ne sont pas shadowes en V1; seule la lumiere directe directional est attenuee.
- Les point shadows et spot shadows restent a faire dans une iteration suivante.

## Validation rapide

- Build runtime: `dotnet build .\CasaEngine.MonoGame.sln -nologo -p:WarningLevel=0`
- Build editor: `dotnet build .\CasaEngine.Editor\CasaEngine.Editor.csproj -t:Compile -nologo -p:WarningLevel=0`
- Build demos: `dotnet build .\CasaEngine.Demos\CasaEngine.Demos.csproj -nologo -p:WarningLevel=0`

Smoke shadows visible :

Validation statique dediee :

```powershell
Push-Location .\CasaEngine.Demos
try {
    $env:CASAENGINE_START_DEMO = 'Static shadow validation demo'
    $env:CASAENGINE_CAPTURE_SCREENSHOT_PATH = 'artifacts/validation/static-shadow-validation-demo.png'
    $env:CASAENGINE_CAPTURE_SCREENSHOT_DELAY_MS = '2000'
    dotnet run --project .\CasaEngine.Demos.csproj --no-build
}
finally {
    Pop-Location
}
```

Validation skinned existante :

```powershell
Push-Location .\CasaEngine.Demos
try {
    $env:CASAENGINE_START_DEMO = 'Skinned mesh demo'
    $env:CASAENGINE_CAPTURE_SCREENSHOT_PATH = 'artifacts/validation/skinned-shadow-demo.png'
    $env:CASAENGINE_CAPTURE_SCREENSHOT_DELAY_MS = '2000'
    dotnet run --project .\CasaEngine.Demos.csproj --no-build
}
finally {
    Pop-Location
}
```

Les captures automatisees produites par ces commandes sont stockees dans `CasaEngine.Demos/artifacts/validation/static-shadow-validation-demo.png` et `CasaEngine.Demos/artifacts/validation/skinned-shadow-demo.png`.