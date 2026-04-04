# StaticModel Always-Material Pipeline — Plan d'implementation

## Objectif

Faire converger le pipeline StaticModel vers une regle unique : chaque mesh et chaque slot rendu doivent toujours consommer un `MaterialBase` resolu avant d'entrer dans le renderer.

## Cible d'architecture

- le renderer ne doit plus contenir de branche legacy texture-only
- `StaticModel.Initialize()` doit resoudre un `MaterialBase` pour chaque mesh
- un submesh sans materiau propre doit reutiliser le materiau resolu du mesh parent
- un slot avec seulement une texture doit utiliser un `LitDiffuseMaterial` genere a partir de cette texture
- un slot sans materiau ni texture doit utiliser un materiau explicite de type missing

## Taches

- ✅ T1 - Introduire un helper de resolution de materiaux StaticModel
- ✅ T2 - Resoudre les meshes texture-only vers un `LitDiffuseMaterial`
- ✅ T3 - Resoudre les slots sans materiau ni texture vers un materiau missing explicite
- ✅ T4 - Faire de `StaticModel.Initialize()` le point unique de resolution des materiaux runtime
- ✅ T5 - Supprimer le fallback texture-only de `StaticMeshRendererComponent`
- ✅ T6 - Mettre a jour l'inspector pour refléter le comportement par defaut
- ✅ T7 - Ajouter des tests cibles sur les materiaux generes
- ✅ T8 - Valider par build et tests filtres

## Validation realisee

- `dotnet build CasaEngine.Editor.MonoGame.sln -c Debug --no-restore`
- `dotnet test CasaEngine.Tests\\CasaEngine.Tests.csproj -c Debug --filter FullyQualifiedName~StaticModelMaterial --no-restore`

## Notes d'implementation

- Le renderer continue d'accepter des overrides d'instance, mais il ne doit plus inventer de fallback si la resolution runtime a ete correctement faite en amont.
- Le materiau missing est volontairement visible via un `LitDiffuseMaterial` magenta.
- Les materiaux generes a partir d'une texture sont construits une fois a l'initialisation de l'asset, jamais en hot path.