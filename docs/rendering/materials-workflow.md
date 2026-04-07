# Material Workflow

Ce document decrit le workflow cible des materials dans CasaEngineMonogame apres la migration authoring/runtime.

## Pieces du pipeline

- `MaterialDefinition` / `MaterialPropertyDefinition`
  - definissent le schema d'un type de material: cles, types, valeurs par defaut, metadata editoriales et flags.
- `MaterialAsset`
  - represente l'asset editable `.material`.
  - stocke `DefinitionId`, l'heritage (`ParentMaterialAssetId`), les valeurs persistantes et les hints de render state (`Queue`, blend/depth/rasterizer/sampler, transparence).
- `CompiledMaterial`
  - snapshot runtime compile contenant le shader effectif, les features materials, les valeurs compilees, les textures resolues et les etats GPU prets a l'emploi.
  - utile pour le cache, l'inspection et le hot reload.
- `MaterialBase` et ses derives (`LitDiffuseMaterial`, `UnlitTextureMaterial`, `Material` legacy)
  - objets runtime bindables par le renderer.
  - ils poussent les parametres shader dans `Bind(...)` et, si necessaire, choisissent une technique dans `SelectTechnique(...)`.
- `MaterialInstanceData`
  - overrides authoring par objet / par slot.
  - persiste des `MaterialValue` types sans dupliquer l'asset material.
- `MaterialPropertyBlock`
  - overrides runtime appliques juste avant le draw.
  - reserve aux modifications locales qui ne doivent pas reconfigurer le pipeline.

## Flux authoring vers rendu

1. Un `MaterialAsset` est charge depuis le content system.
2. `MaterialCompiler` resolve:
   - la definition,
   - les valeurs locales + heritage parent,
   - les textures,
   - les valeurs non-texture compilees.
3. `MaterialRenderStateResolver` centralise la traduction authoring -> etats runtime:
   - queue opaque / alpha-test / transparent,
   - blend state,
   - depth state,
   - transparence derivee des proprietes (`alpha`, alpha du tint, alpha du diffuse, etc.).
4. `MaterialCompiler` produit:
   - un `CompiledMaterial`,
   - un `MaterialBase` runtime concret.
5. `MaterialRuntimeResolver` charge le runtime material via `MaterialCache` quand il est disponible.
6. Au draw:
   - `RenderFeatureResolver` combine structure du material et capacites du mesh,
   - `EffectiveShaderResolver` choisit le shader effectif,
  - `ShaderVariantLibrary` route vers la permutation canonique,
  - la policy canonique couvre explicitement les dimensions draw-path stables (`BasColorTexture`, `AlphaTest`, `Transparent`, `Skinned`, `VertexColor`, `Instanced`),
  - les variantes material-specifiques (`NormalMap`, `Reflection`, specialisation `OneLight`) restent sous le controle explicite du material quand elles ne sont pas partagees par toutes les familles de shaders,
   - le renderer applique les render states,
   - `MaterialBase.Bind(...)` pousse les parametres,
   - le `MaterialPropertyBlock` est applique en dernier pour les overrides par instance.

## Overrides par objet

- `StaticModelComponent` persiste les `MaterialSlotOverride` par slot.
- Un slot override peut:
  - remplacer le material asset du slot,
  - ajouter un `MaterialInstanceData` pour des variations locales.
- `StaticModelMaterialResolver` et `StaticModelComponent` resolvent ces overrides vers:
  - un material runtime par slot,
  - un `MaterialPropertyBlock` par slot.
- `MaterialInstancePropertyBlockMapper` ne mappe que les overrides sûrs:
  - `diffuse_color`, `specular_color`, `specular_power` pour `lit-diffuse`,
  - `tint_color` et `alpha` quand le chemin unlit le permet.
- Regle importante:
  - un override par instance ne doit pas muter les defaults de l'asset ni changer implicitement la queue, le blend state ou la permutation shader du material source.

## Editeur, preview et hot reload

- `MaterialAssetInspectorPanel` genere l'UI de proprietes depuis `MaterialDefinitionEditorRegistry`.
- `MaterialPreviewViewport` compile directement le `MaterialAsset` courant avec `MaterialCompiler` et l'affiche sur sphere / cube / plane.
- Dans le runtime principal, `CasaEngineGame.ReloadMaterialAsset(Guid)` fait trois choses:
  - invalide `MaterialCache` pour l'asset modifie et ses enfants qui heritent de lui,
  - appelle `RefreshResolvedMaterials(...)` sur les `StaticModelComponent` deja charges,
  - invalide toutes les `RenderView` pour forcer un redraw.
- Consequence pratique:
  - la preview editeur est isolee et immediate,
  - les vues runtime repassent par le cache et le refresh de modeles charges.

## Notes de compatibilite pipeline

- Transparence:
  - la queue pilote le routage opaque / transparent.
  - les proprietes marquees `AffectsTransparency` peuvent faire basculer automatiquement vers le pipeline transparent quand l'asset reste sur les defaults.
- Alpha-test:
  - `alpha_cutoff` est une propriete material explicite.
  - le clipping n'est actif que quand le material est route dans `RenderQueue.AlphaTest`.
- Normal map:
  - la simple presence d'une normal map dans le material ne suffit pas.
  - le mesh doit fournir un layout tangent-compatible pour que `RenderFeatureResolver` conserve `ShaderFeature.NormalMap`.

## Validation manuelle de reference

Build minimal:

```powershell
dotnet build CasaEngine.Editor.MonoGame.sln -c Debug --no-restore
dotnet build CasaEngine.Demos/CasaEngine.Demos.csproj -c Debug --no-restore
```

Lancer directement `MaterialDemo`:

```powershell
Set-Location CasaEngine.Demos
$env:CASAENGINE_START_DEMO = 'Material system demo'
dotnet run --project CasaEngine.Demos.csproj -c Debug --no-build
```

Points a verifier dans `MaterialDemo`:

1. Les spheres partagent bien le meme material de base et changent de tint via `MaterialPropertyBlock`.
2. Le panneau `AlphaTest` a une silhouette decoupee et n'apparait pas comme un simple rectangle transparent.
3. Le cube `Glass` passe dans la queue transparente et reste visible.
4. Le `NormalMapBox` montre une reponse lumineuse differente du simple checker albedo.

Nettoyage optionnel:

```powershell
Remove-Item Env:CASAENGINE_START_DEMO
```