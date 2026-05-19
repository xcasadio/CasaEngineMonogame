# Taches IA - lumiere forward, shadows et binding shader

Ce fichier est un plan d'execution pour un agent IA peu autonome. Il doit etre mis a jour pendant le travail : l'icone au debut de chaque tache indique son statut courant.

## Legende des statuts

- ⏳ Todo : pas encore commence.
- 🚧 In progress : en cours de modification locale.
- 🧪 Needs testing : code ecrit, validation incomplete ou en attente.
- ✅ Done : code valide, build/tests OK, commit effectue.
- ⚠️ Blocked : bloque par une erreur non resolue ou une decision manquante.

Regle stricte : changer l'icone dans le titre de la tache quand son statut change. Exemple : `### ⏳ Tache 03` devient `### 🚧 Tache 03`, puis `### ✅ Tache 03` apres validation et commit.

## Regles obligatoires pour l'agent

- Lire les instructions applicables avant toute modification C# : `.github/copilot-instructions.md`, `AGENTS.md`, `.github/instructions/csharp-monogame.instructions.md`, et `.github/instructions/rendering.instructions.md` pour les fichiers rendering/shaders.
- Ne jamais commencer une tache suivante tant que la tache courante n'a pas ete validee et commitee.
- Faire exactement un commit par tache atomique terminee. Ne pas grouper deux taches dans le meme commit.
- Inclure la mise a jour de statut de ce fichier dans le commit de la tache.
- Avant chaque commit, verifier `rtk git status` ou `git status` et ne commiter que les fichiers lies a la tache.
- Ne pas utiliser `git reset --hard`, `git checkout --` ou autre commande destructive.
- Ne pas introduire de LINQ, closures ou allocations evitables dans `Update`, `Draw`, `RenderPass.Execute` ou les boucles de rendu.
- Restaurer tout etat `GraphicsDevice` modifie par un nouveau pass : render targets, viewport, rasterizer, blend, depth/stencil, sampler si touche.
- Si une validation demandee ne peut pas etre lancee, mettre la tache en `🧪 Needs testing` ou `⚠️ Blocked`, expliquer pourquoi dans ce fichier, puis demander une decision.

Commandes de base a utiliser entre les taches :

```powershell
rtk git status
dotnet test .\CasaEngine.Tests\CasaEngine.Tests.csproj -v minimal --filter "FullyQualifiedName~<NomDuTest>"
dotnet build .\CasaEngine.MonoGame.sln --no-restore -v minimal
git add <fichiers de la tache> docs\forward-lighting-shadows-plan.md
git commit -m "<message de commit de la tache>"
```

## Contexte technique actuel

- `LightComponent` existe deja et alimente `LightingContext` via `IRenderLightSource` puis `WorldLightCollector`.
- `MaterialBase`, `MaterialAsset` et `CompiledMaterial` ont deja `CastShadows` et `ReceiveShadows` au niveau material.
- `StaticModelComponent` et `SkinnedMeshComponent` n'ont pas encore de flags d'instance pour les shadows.
- `ForwardRenderPipeline` execute `SkyPass`, `OpaquePass`, puis `TransparentPass`.
- `RenderPassType.ShadowPass` existe deja, mais aucun `ShadowPass` concret n'est branche.
- `ShaderBindCache.BindGlobals` appelle encore `context.Lighting?.Bind(shader)`.
- Objectif architectural : `LightingContext` doit devenir un simple conteneur de lumieres visibles; le binding GPU doit passer par `ForwardLightBinder`.

## Decisions de conception a respecter

- `LightComponent.CastShadows` doit etre `false` par defaut pour garder les scenes existantes compatibles et eviter un cout GPU surprise.
- Les composants rendus doivent avoir `CastShadows = true` et `ReceiveShadows = true` par defaut, car le rendu actuel suppose que la geometrie est visible et eclairee.
- La regle effective d'une draw call est :

```text
effectiveCastShadows = component.CastShadows && material.CastShadows
effectiveReceiveShadows = component.ReceiveShadows && material.ReceiveShadows
```

- V1 shadows : commencer par directional shadow map 2D. Spot shadows ensuite. Point shadows plus tard.
- L'ambient et l'environnement ne sont pas shadowes en V1. Seule la lumiere directe est attenuee.

## Taches atomiques

### ✅ Tache 01 - Ajouter `CastShadows` aux lumieres

But : permettre a une lumiere d'indiquer si elle doit produire une shadow map, sans encore changer le rendu.

Fichiers a modifier :

- `CasaEngine/Framework/Scene/Entities/Components/LightComponent.cs`
- `CasaEngine/Framework/Rendering/DirectionalLight.cs`
- `CasaEngine/Framework/Rendering/PointLight.cs`
- `CasaEngine/Framework/Rendering/SpotLight.cs`
- `CasaEngine.EditorServices/EditorEntityJsonSerializer.cs`
- `CasaEngine.Editor/Controls/ComponentEditors/LightComponentEditor.cs`
- Tests pertinents dans `CasaEngine.Tests/Rendering` ou `CasaEngine.Tests/EditorServices`.

Etapes detaillees :

1. Dans `LightComponent`, ajouter `public bool CastShadows { get; set; } = false;`.
2. Copier `CastShadows` dans le constructeur de copie de `LightComponent`.
3. Charger la cle JSON `cast_shadows` dans `LightComponent.Load(JObject)`, avec fallback `false` si absente.
4. Sauver `cast_shadows` dans `EditorEntityJsonSerializer.SaveLightComponent`.
5. Ajouter un checkbox `Cast Shadows` dans `LightComponentEditor` avec undo/redo via `ApplyValueChange`.
6. Ajouter un champ `bool CastShadows` dans `DirectionalLight`, `PointLight` et `SpotLight`.
7. Ajouter un parametre optionnel `bool castShadows = false` aux constructeurs de ces structs pour garder la compatibilite des appels existants.
8. Passer `LightComponent.CastShadows` dans `AppendDirectionalLight`, `AppendPointLight` et `AppendSpotLight`.
9. Ajouter ou adapter les tests clone/load/save pour verifier que `cast_shadows` est conserve.

Validation obligatoire :

```powershell
dotnet test .\CasaEngine.Tests\CasaEngine.Tests.csproj -v minimal --filter "FullyQualifiedName~LightComponent"
dotnet build .\CasaEngine.MonoGame.sln --no-restore -v minimal
```

Commit obligatoire :

```powershell
git commit -m "Add light shadow casting authoring"
```

### ✅ Tache 02 - Ajouter `CastShadows` / `ReceiveShadows` aux composants rendus

But : permettre a une instance rendue d'autoriser ou non casting/reception de shadows, independamment du material.

Fichiers a modifier :

- `CasaEngine/Framework/Scene/Entities/Components/PrimitiveComponent.cs`
- `CasaEngine/Framework/Scene/Entities/Components/StaticModelComponent.cs`
- `CasaEngine/Framework/Scene/Entities/Components/StaticModelSubMeshComponent.cs`
- `CasaEngine/Framework/Scene/Entities/Components/SkinnedMeshComponent.cs`
- `CasaEngine.EditorServices/EditorEntityJsonSerializer.cs`
- Editeurs de composants sous `CasaEngine.Editor/Controls/ComponentEditors` si l'affichage generique n'est pas suffisant.
- Tests de serialisation composants.

Etapes detaillees :

1. Ajouter dans `PrimitiveComponent` : `CastShadows = true` et `ReceiveShadows = true`.
2. Copier ces deux proprietes dans le constructeur de copie de `PrimitiveComponent`.
3. Charger `cast_shadows` et `receive_shadows` dans `PrimitiveComponent.Load(JObject)`, avec fallback `true` si absent.
4. Sauver les deux flags pour `StaticModelComponent` et `SkinnedMeshComponent` dans `EditorEntityJsonSerializer`.
5. Pour `StaticModelSubMeshComponent` genere, ne pas serialiser separement; il doit heriter des flags du `StaticModelComponent` parent.
6. Verifier que les proprietes apparaissent dans l'editeur via generic section. Si elles n'apparaissent pas clairement, ajouter une section `Rendering` explicite dans `StaticModelComponentEditor`, `StaticModelSubMeshComponentEditor` et l'editeur/fallback de `SkinnedMeshComponent`.
7. Ajouter des tests load/save avec absence de cle pour verifier la compatibilite anciennes scenes.

Validation obligatoire :

```powershell
dotnet test .\CasaEngine.Tests\CasaEngine.Tests.csproj -v minimal --filter "FullyQualifiedName~StaticModelComponent|FullyQualifiedName~SkinnedMeshComponent|FullyQualifiedName~PrimitiveComponent"
dotnet build .\CasaEngine.MonoGame.sln --no-restore -v minimal
```

Commit obligatoire :

```powershell
git commit -m "Add render component shadow flags"
```

### ✅ Tache 03 - Propager les flags shadows aux draw calls

But : faire voyager les valeurs effectives jusqu'au pipeline sans encore rendre de shadow map.

Fichiers a modifier :

- `CasaEngine/Framework/Rendering/Draw/RenderItem.cs`
- `CasaEngine/Framework/Application/Components/StaticMeshRendererComponent.cs`
- `CasaEngine/Framework/Application/Components/SkinnedMeshRendererComponent.cs`
- `CasaEngine/Framework/Scene/Entities/Components/StaticModelSubMeshComponent.cs`
- `CasaEngine/Framework/Scene/Entities/Components/SkinnedMeshComponent.cs`
- Tests de rendering si existants.

Etapes detaillees :

1. Ajouter `public bool ComponentCastShadows;` et `public bool ComponentReceiveShadows;` dans `RenderItem`, ou directement `public bool CastShadows;` et `public bool ReceiveShadows;` si le calcul effectif est fait avant.
2. Ajouter des proprietes readonly sur `RenderItem` si utile : `EffectiveCastShadows` et `EffectiveReceiveShadows`.
3. Etendre les overloads `StaticMeshRendererComponent.AddMesh(...)` pour recevoir `castShadows` et `receiveShadows` avec valeurs par defaut `true`.
4. Dans la construction de chaque `RenderItem`, appliquer la regle : composant && material.
5. Dans `StaticModelSubMeshComponent.Draw`, passer les flags herites du parent/instance au renderer.
6. Etendre `SkinnedMeshRendererComponent.AddMesh(...)` et `SkinnedMeshInfo` pour recevoir les flags composant.
7. Dans `SkinnedMeshComponent.Draw`, passer `CastShadows` et `ReceiveShadows` au renderer skinned.
8. Ne pas changer le rendu visible dans cette tache.

Validation obligatoire :

```powershell
dotnet test .\CasaEngine.Tests\CasaEngine.Tests.csproj -v minimal --filter "FullyQualifiedName~RenderItem|FullyQualifiedName~RenderFeature|FullyQualifiedName~SkinnedMesh"
dotnet build .\CasaEngine.MonoGame.sln --no-restore -v minimal
```

Commit obligatoire :

```powershell
git commit -m "Propagate shadow flags to render items"
```

### ✅ Tache 04 - Extraire `ForwardLightBinder`

But : sortir tout binding shader de `LightingContext` et garder le rendu identique.

Fichiers a modifier :

- `CasaEngine/Framework/Rendering/LightingContext.cs`
- `CasaEngine/Framework/Rendering/Shaders/ForwardLightBinder.cs` nouveau fichier.
- `CasaEngine/Framework/Rendering/Shaders/ShaderBindCache.cs`
- Tests `LightingContextTests` et nouveaux tests `ForwardLightBinderTests`.

Etapes detaillees :

1. Creer `ForwardLightBinder` dans `CasaEngine.Framework.Rendering.Shaders`.
2. Deplacer dans `ForwardLightBinder` les tableaux temporaires de binding actuellement presents dans `LightingContext`.
3. Deplacer la logique de `LightingContext.Bind(ShaderWrapper)` vers `ForwardLightBinder.Bind(ShaderWrapper shader, LightingContext? lighting, RenderStats? stats)` ou signature equivalente.
4. Laisser `LightingContext` gerer seulement : arrays de lights visibles, counts, ambient, scoring local, add/copy/clear.
5. Retirer de `LightingContext` les using et references a `ShaderWrapper` et `ShaderParameterNames`.
6. Modifier `ShaderBindCache` pour posseder un `ForwardLightBinder` et l'appeler dans `BindGlobals`.
7. Garder `EnvironmentShaderBinder.Bind(...)` separe et appele apres le binding des lumieres.
8. Verifier que les slots inactifs sont toujours zeroes par le nouveau binder.

Validation obligatoire :

```powershell
dotnet test .\CasaEngine.Tests\CasaEngine.Tests.csproj -v minimal --filter "FullyQualifiedName~LightingContextTests|FullyQualifiedName~ForwardLightBinder"
dotnet build .\CasaEngine.MonoGame.sln --no-restore -v minimal
```

Commit obligatoire :

```powershell
git commit -m "Extract forward light shader binder"
```

### ✅ Tache 05 - Ajouter le modele runtime des shadow resources

But : preparer les donnees shadows sans encore modifier les shaders lit.

Fichiers a creer ou modifier :

- `CasaEngine/Framework/Rendering/Shadows/ShadowSettings.cs`
- `CasaEngine/Framework/Rendering/Shadows/ShadowLight.cs`
- `CasaEngine/Framework/Rendering/Shadows/ForwardShadowResources.cs`
- `CasaEngine/Framework/Rendering/RenderContext.cs`
- `CasaEngine/Framework/Rendering/RenderFrame.cs` si les donnees doivent etre par view/frame.
- Tests rendering shadows.

Etapes detaillees :

1. Creer un dossier/namespace `CasaEngine.Framework.Rendering.Shadows`.
2. Ajouter `ShadowSettings` avec au minimum : resolution, depth bias, normal bias, max distance, enabled.
3. Ajouter une structure `ShadowLight` avec : type de lumiere, index dans `LightingContext`, matrix light view-projection, viewport atlas, bias.
4. Ajouter `ForwardShadowResources` pour porter la shadow map/atlas et la liste des shadow lights visibles.
5. Ajouter un champ optionnel dans `RenderContext`, par exemple `public ForwardShadowResources? Shadows;`.
6. Ne pas allouer de listes par frame dans les hot paths; prevoir reuse/clear.
7. Ajouter des tests simples de valeurs par defaut et clear/reuse.

Validation obligatoire :

```powershell
dotnet test .\CasaEngine.Tests\CasaEngine.Tests.csproj -v minimal --filter "FullyQualifiedName~Shadow"
dotnet build .\CasaEngine.MonoGame.sln --no-restore -v minimal
```

Commit obligatoire :

```powershell
git commit -m "Add forward shadow runtime resources"
```

### ✅ Tache 06 - Ajouter `ShadowPass` static directional depth-only

But : rendre une premiere shadow map depth-only pour les static meshes et directional lights.

Fichiers a creer ou modifier :

- `CasaEngine/Framework/Rendering/Draw/ShadowPass.cs`
- `CasaEngine/Framework/Rendering/ForwardRenderPipeline.cs`
- `CasaEngine/Content/Shaders/ShadowDepth.fx` ou shader equivalent.
- `CasaEngine/Content/Content.mgcb`
- Tests pipeline/source.

Etapes detaillees :

1. Creer `ShadowPass : RenderPass` avec `RenderPassType.ShadowPass`.
2. Inserer `ShadowPass` avant `SkyPass` dans `ForwardRenderPipeline`.
3. En V1, traiter seulement les directional lights avec `CastShadows == true`.
4. Filtrer les `RenderItem` avec `EffectiveCastShadows == true`.
5. Exclure les transparent queues en V1.
6. Creer ou utiliser un shader depth-only static.
7. Sauvegarder l'etat GPU avant le pass et le restaurer apres : render target, viewport, rasterizer, blend, depth/stencil.
8. Mettre a jour `ForwardShadowResources` avec la texture produite et la matrix light view-projection.
9. Ajouter un test source/pipeline qui verifie que `ShadowPass` est avant `SkyPass`.
10. Documenter clairement que les skinned casters ne sont pas encore couverts si non implementes dans cette tache.

Validation obligatoire :

```powershell
dotnet mgcb /@:"CasaEngine\Content\Content.mgcb" /platform:Windows /outputDir:"CasaEngine\Content\bin\Windows\Content" /intermediateDir:"CasaEngine\Content\obj\Windows\net9.0-windows\Content" /workingDir:"CasaEngine\Content"
dotnet test .\CasaEngine.Tests\CasaEngine.Tests.csproj -v minimal --filter "FullyQualifiedName~ShadowPass|FullyQualifiedName~ForwardRenderPipeline"
dotnet build .\CasaEngine.MonoGame.sln --no-restore -v minimal
```

Commit obligatoire :

```powershell
git commit -m "Add forward shadow depth pass"
```

### ✅ Tache 07 - Binder et consommer les shadows dans `LitForward.fx`

But : faire recevoir les shadows aux static/material renderers.

Fichiers a modifier :

- `CasaEngine/Content/Shaders/LitForward.fx`
- `CasaEngine/Content/Shaders/Lighting.fxh` si un helper commun est necessaire.
- `CasaEngine/Framework/Rendering/Shaders/ShaderParameterNames.cs`
- `CasaEngine/Framework/Rendering/Shaders/ForwardLightBinder.cs`
- `CasaEngine/Framework/Rendering/Draw/RenderPass.cs` ou material bind path si `ReceiveShadows` doit devenir parametre shader par draw call.
- Tests shader coverage.

Etapes detaillees :

1. Ajouter les noms de parametres shader pour shadow map, matrix, count, bias, texel size, receive flag.
2. Etendre `ForwardLightBinder` pour binder les ressources shadows si presentes dans `RenderContext.Shadows`.
3. Dans le draw path static, binder un flag par draw call : `ReceiveShadows` effectif.
4. Dans `LitForward.fx`, sampler la shadow map pour les contributions directes directional V1.
5. Ne pas appliquer les shadows a l'ambient ni a l'environnement.
6. Ajouter une PCF simple et stable, sans boucle couteuse non bornee.
7. Garder un fallback exact si aucune shadow map n'est disponible.

Validation obligatoire :

```powershell
dotnet mgcb /@:"CasaEngine\Content\Content.mgcb" /platform:Windows /outputDir:"CasaEngine\Content\bin\Windows\Content" /intermediateDir:"CasaEngine\Content\obj\Windows\net9.0-windows\Content" /workingDir:"CasaEngine\Content"
dotnet test .\CasaEngine.Tests\CasaEngine.Tests.csproj -v minimal --filter "FullyQualifiedName~LightingShaderCoverageTests|FullyQualifiedName~ForwardLightBinder"
dotnet build .\CasaEngine.MonoGame.sln --no-restore -v minimal
```

Commit obligatoire :

```powershell
git commit -m "Apply forward shadows to lit materials"
```

### ✅ Tache 08 - Ajouter support shadows pour skinned meshes

But : eviter que les personnages/meshes animes ignorent le systeme de shadows.

Fichiers a modifier :

- `CasaEngine/Framework/Application/Components/SkinnedMeshRendererComponent.cs`
- `CasaEngine/Content/Shaders/skinEffect.fx`
- Nouveau shader depth-only skinned si necessaire.
- `CasaEngine/Content/Content.mgcb`
- Tests shader/source skinned.

Etapes detaillees :

1. Faire voyager `EffectiveCastShadows` et `EffectiveReceiveShadows` dans le chemin skinned.
2. Pour casting, ajouter une technique depth-only skinned ou un shader dedie qui applique les bones avant d'ecrire la profondeur.
3. Pour reception, ajouter les memes bindings shadow que `LitForward.fx` dans `skinEffect.fx`.
4. Ne pas casser le support LinearBlend/DualQuaternion.
5. Verifier que les palettes `Bones` et `BonesDualQuaternion` sont bindees dans le pass shadow skinned.
6. Ajouter des tests source qui verifient les techniques et parametres attendus.

Validation obligatoire :

```powershell
dotnet mgcb /@:"CasaEngine\Content\Content.mgcb" /platform:Windows /outputDir:"CasaEngine\Content\bin\Windows\Content" /intermediateDir:"CasaEngine\Content\obj\Windows\net9.0-windows\Content" /workingDir:"CasaEngine\Content"
dotnet test .\CasaEngine.Tests\CasaEngine.Tests.csproj -v minimal --filter "FullyQualifiedName~LightingShaderCoverageTests|FullyQualifiedName~Skinned"
dotnet build .\CasaEngine.MonoGame.sln --no-restore -v minimal
```

Commit obligatoire :

```powershell
git commit -m "Add skinned mesh shadow support"
```

### ✅ Tache 09 - Ajouter demo et validation visuelle

But : prouver le comportement dans une scene simple et automatisable.

Fichiers a modifier :

- `CasaEngine.Demos/**` selon la demo choisie.
- Documentation sous `docs/` si une commande de smoke est ajoutee.
- Eventuellement assets demo existants, sans ajouter de dependance lourde.

Etapes detaillees :

1. Ajouter ou adapter une demo avec un sol, un mesh opaque, une directional light avec `CastShadows = true`.
2. Ajouter au moins un objet avec `CastShadows = false` pour verifier qu'il ne projette pas.
3. Ajouter au moins un objet ou material avec `ReceiveShadows = false` pour verifier qu'il ne recoit pas.
4. Ajouter une capture automatique via les variables d'environnement de `CasaEngine.Demos` si le framework existant le permet.
5. Documenter la commande exacte dans ce fichier ou dans une doc dediee.

Validation obligatoire :

```powershell
Push-Location .\CasaEngine.Demos
try {
    $env:CASAENGINE_START_DEMO = '<nom exact de la demo>'
    $env:CASAENGINE_CAPTURE_SCREENSHOT_PATH = '..\ai-agent\forward-shadows-smoke.png'
    $env:CASAENGINE_CAPTURE_SCREENSHOT_DELAY_MS = '1500'
    dotnet run --no-build
}
finally {
    Pop-Location
}
dotnet build .\CasaEngine.MonoGame.sln --no-restore -v minimal
```

Validation executee pour cette tache :

```powershell
dotnet build .\CasaEngine.Demos\CasaEngine.Demos.csproj --no-restore -clp:ErrorsOnly
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

Notes d'implementation :

- La validation visuelle s'appuie sur `SkinnedMeshDemo` plutot qu'une nouvelle scene asset-heavy.
- La demo met en place trois colonnes de personnages skinnes : reception normale, `ReceiveShadows = false`, puis `CastShadows = false` cote caster.
- La capture automatisee valide le chemin runtime sans rebuild supplementaire et ecrit l'image dans `CasaEngine.Demos/artifacts/validation/skinned-shadow-demo.png`.

Commit obligatoire :

```powershell
git commit -m "Add forward shadows demo coverage"
```

### ⏳ Tache 10 - Nettoyage final et documentation

But : fermer la fonctionnalite avec une documentation claire et des tests coherents.

Fichiers a modifier :

- `docs/light-component.md`
- `docs/forward-lighting-shadows-plan.md`
- Tests si un manque de couverture est identifie.

Etapes detaillees :

1. Mettre a jour `docs/light-component.md` pour expliquer `CastShadows` sur lights.
2. Documenter les flags `CastShadows` / `ReceiveShadows` des composants rendus.
3. Documenter la regle effective composant && material.
4. Documenter les limites V1 : directional shadows, static/skinned selon l'etat final, point shadows deferrees.
5. Marquer toutes les taches terminees en `✅ Done` dans ce fichier.
6. Lancer une derniere validation solution.

Validation obligatoire :

```powershell
dotnet test .\CasaEngine.Tests\CasaEngine.Tests.csproj -v minimal --filter "FullyQualifiedName~Rendering|FullyQualifiedName~LightComponent|FullyQualifiedName~Shadow"
dotnet build .\CasaEngine.MonoGame.sln --no-restore -v minimal
```

Commit obligatoire :

```powershell
git commit -m "Document forward shadow lighting workflow"
```

## Definition finale du succes

- `LightComponent.CastShadows` existe, est edite, serialise, clone, et propage aux lights visibles.
- Les composants rendus portent `CastShadows` et `ReceiveShadows`.
- Les materials et composants participent ensemble a la decision effective.
- `LightingContext` ne contient plus de binding shader.
- `ForwardLightBinder` est le seul binder des lumieres directes forward.
- `ShadowPass` est execute avant le rendu visible et restaure l'etat GPU.
- `LitForward.fx` recoit les shadows.
- `skinEffect.fx` recoit les shadows si la tache skinned est terminee.
- Une demo ou smoke test montre au moins un objet qui projette une shadow et un objet qui ne la recoit pas.
- Chaque tache terminee a son propre commit.