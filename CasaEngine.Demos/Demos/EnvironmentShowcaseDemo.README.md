# EnvironmentShowcaseDemo

Objectif:
montrer qu'un world peut utiliser un cubemap d'environnement global pour l'ambient/specular tout en laissant le fond visuel du viewport basculer independamment.

Controles:
- `B` : bascule entre fond cubemap et fond couleur unie sans couper le cubemap d'environnement pour le lighting
- `E` : change de preset de cubemap d'environnement
- `L` : active ou coupe la contribution ambient/specular de l'environnement

Validation:
- lancer la demo `Environment showcase`
- verifier qu'avec `B` le fond change alors que les reflexions / la teinte ambient restent basees sur le cubemap courant
- verifier qu'avec `L` la contribution d'environnement varie alors que le fond reste stable

Commande de capture utilisee pour la validation:

```powershell
$env:CASAENGINE_START_DEMO = 'Environment showcase'
$env:CASAENGINE_CAPTURE_SCREENSHOT_PATH = 'artifacts/validation/environment-showcase-demo.png'
$env:CASAENGINE_CAPTURE_SCREENSHOT_DELAY_MS = '1500'
dotnet run --project CasaEngine.Demos/CasaEngine.Demos.csproj -c Debug --no-build
```