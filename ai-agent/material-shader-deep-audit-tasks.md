# Audit approfondi materials + shaders + effects - Liste de taches IA

## Objectif

Faire une analyse fine puis une remise a niveau progressive de l'architecture materials/shaders/effects apres les changements recents.

Le travail de l'agent doit couvrir trois axes :

- verifier si l'architecture materials/shaders est moderne, modulaire et decouplee du runtime shader concret ;
- verifier chaque classe de l'architecture material/shader pour identifier bugs, code mort, zones de transition inachevees et problemes d'optimisation ;
- analyser les fichiers `.fx` / `.fxh` et le tooling de compilation pour verifier la qualite du code, les duplications et les optimisations pertinentes.
- traiter le hot reload materials de l'editeur comme une contrainte de premier rang : une modification authoring doit continuer a se propager proprement aux materials, vues et bindings shader deja charges.

## Regles obligatoires pour l'agent IA

1. Une seule tache a la fois.
2. Avant de commencer une tache, remplacer son icone `⏳` par `🚧`.
3. A la fin de chaque tache, remplacer l'icone par `✅`, `🧪` ou `⚠️`.
4. Commiter entre chaque tache, avec la mise a jour du statut dans le meme commit.
5. Si une hypothese de ce document s'avere fausse apres relecture du code, documenter la conclusion puis cloturer la tache plutot que forcer un refactor inutile.
6. Le hot reload material de l'editeur (`EditorAssetWriterService.AssetSaved` -> `Game1.OnEditorAssetSaved` -> `CasaEngineGame.ReloadMaterialAsset`) ne doit pas etre degrade par la refonte ; il doit rester mesurable et fiable.
7. Le resultat final ne doit plus conserver de chemin legacy materials/shaders actif ; une compatibilite transitoire n'est acceptable que si une tache suivante la supprime explicitement.
8. Aucune nouvelle fonctionnalite de rendu ne doit utiliser `Microsoft.Xna.Framework.Graphics.BasicEffect` ; utiliser uniquement des fichiers effect CasaEngine.
9. Si une tache touche du rendu visible, verifier au minimum la demo ou le renderer le plus proche de la zone touchee.
10. Si une tache touche de la logique pure, ajouter ou etendre des tests cibles dans `CasaEngine.Tests`.
11. Statuts autorises :
   - `⏳ Todo`
   - `🚧 In progress`
   - `✅ Done`
   - `🧪 Needs testing`
   - `⚠️ Blocked`

## Validation minimale par tache

- Build principal borne : `dotnet build CasaEngine.Editor.MonoGame.sln -c Debug --no-restore`
- Tests bornes selon la zone :
  - `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj -c Debug --filter FullyQualifiedName~MaterialCompiler --no-restore`
  - `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj -c Debug --filter FullyQualifiedName~MaterialRuntimeResolver --no-restore`
  - `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj -c Debug --filter FullyQualifiedName~RenderFeatureResolver --no-restore`
  - `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj -c Debug --filter FullyQualifiedName~EffectiveShaderResolver --no-restore`
- Validation visuelle cible si necessaire : `MaterialDemo`, renderer statique, renderer skinned, preview material editeur.
- Validation hot reload cible si la tache touche les materials/shaders runtime : modifier un parametre material dans l'editeur, verifier la preview, verifier le runtime recharge via `ReloadMaterialAsset`, puis verifier que les vues invalidees affichent la nouvelle valeur sans rechargement manuel du projet.

## Resume par categorie - points a ameliorer

### 1. Architecture et decouplage

- La separation `MaterialAsset` -> `CompiledMaterial` -> `MaterialBase` existe, mais le draw path principal reste pilote par `MaterialBase` plutot que par une description runtime compilee unique.
- Le workflow editeur + hot reload existe deja (`EditorAssetWriterService.AssetSaved`, `Game1.OnEditorAssetSaved`, `CasaEngineGame.ReloadMaterialAsset`, `MaterialCache`, `MaterialAuthoringAssetCache`, invalidation des vues) et doit etre traite comme un contrat architectural, pas comme un detail annexe.
- `MaterialDefinitionRegistry` reste un registre statique ferme : aucune API d'enregistrement extensible pour de nouvelles definitions, policies editor ou compilateurs par definition.
- `MaterialCompiler` reste centralise autour d'un `switch` sur `definition.Id`, ce qui couple toute extension material au noyau du moteur.
- `MaterialInstancePropertyBlockMapper` est lui aussi pilote par `definition.Id` et ne gere explicitement que `lit-diffuse` et `unlit-texture`.
- `EffectiveShaderResolver` et `RenderFeatureResolver` derivent encore une partie importante du comportement a partir des types runtime concrets et de champs specifiques (`LitDiffuseMaterial`, `UnlitTextureMaterial`, `Material`).
- Le chemin skinned reste a part : `SkinnedMeshRendererComponent` hardcode `skinEffect`, ses etats de rendu et ses defaults material, au lieu de suivre la meme resolution material/shader que le rendu statique.

### 2. Bugs, fragilites et zones de transition dans les classes C#

- `ShaderVariantLibrary` met en cache des `ShaderWrapper` partages par variante, mais la selection de technique n'est appliquee que sur le chemin de resolution a froid. Si le meme `ShaderWrapper` est partage par plusieurs cles, un cache hit peut reutiliser une technique selectionnee par une autre variante precedente.
- `ShaderVariantLibrary.BuildTechniqueName(...)` ne modelise pas toutes les dimensions deja presentes dans le moteur (`NormalMap`, `Reflection`, `VertexColor`, `Instanced`). La resolution de variantes est donc partielle et repose encore sur `MaterialBase.SelectTechnique(...)` pour certains cas.
- `MaterialCompiler.BuildResolvedTextures(...)` traite `reflection_texture` comme un cas special en stockant `null` dans la map compilee, alors que le runtime charge ensuite une cubemap separement. Le descripteur compile ne represente donc pas completement l'etat runtime reel.
- `CompiledMaterial` et `MaterialCache` existent et sont testes, mais le renderer principal ne consomme presque pas cette representation compilee. La separation est donc reelle dans les types, mais encore partielle dans le comportement.
- `MaterialBase.GetFeatures(...)` et ses overrides sur les materials concrets ne sont pas utilises par le renderer actuel. Cela ressemble a une API de transition ou a du code mort a requalifier.
- `MaterialDefinitionRegistry.TryGetByRuntimeType(...)` apparait non consomme dans le moteur actuel. Il faut verifier s'il s'agit encore d'un chemin utile ou d'une relique de migration.
- `CasaEngine.Shaders/ShaderCompiler.ProcessErrorsAndWarnings(...)` utilise une regex/generation de groupes incoherente avec les index lus ensuite. Le parsing des diagnostics `mgfxc` est donc fragile et peut masquer ou degrader les messages de compilation shader.
- `MaterialLoader` et `MaterialAssetLoader` coexistent pour la meme extension `.material`. La compatibilite est utile, mais le role exact de chaque voie doit etre clarifie pour eviter de prolonger une ambiguite d'architecture.

### 3. Overrides, extensibilite et maintenance

- Les overrides par instance sont bien encadres, mais la logique reste specifique a quelques definitions et peu extensible pour de nouveaux types de materials.
- Les donnees compilees, les property blocks et les runtime materials portent encore des morceaux recouvrants de l'etat material. Il y a plusieurs sources de verite a clarifier.
- Le resultat cible ne doit plus conserver de chemin legacy actif : `Material` multi-texture, adaptation legacy, doubles chargeurs `.material` et chemins runtime historiques doivent etre migres puis supprimes, pas seulement documentes.
- Toute compatibilite temporaire doit etre une etape de migration courte avec suppression explicite planifiee dans ce document ; ce n'est pas un etat final acceptable.

### 4. Fichiers effects et includes shader

- Les effets materials principaux (`basicEffect.fx`, `UnlitTexture.fx`, `skinEffect.fx`) sont globalement coherents avec le runtime actuel, mais seule une partie de leurs permutations est formalisee dans `ShaderVariantLibrary`.
- Il existe une confusion de nommage entre le fichier CasaEngine `basicEffect.fx` et la classe `Microsoft.Xna.Framework.Graphics.BasicEffect`. Dans le code actuel, `basicEffect.fx` est bien le shader material lit principal du moteur, tandis que les usages debug simples passent encore par la classe MonoGame `BasicEffect`.
- Renommer le shader material principal vers un nom semantique (`LitForward.fx`, `SurfaceLit.fx` ou equivalent) aiderait a sortir de cette ambiguite. Ce renommage ne suffit pas a lui seul pour moderniser l'architecture, mais il clarifie nettement les analyses et la maintenance.
- `LightingContext` et `Lighting.fxh` restent structures autour de `MaxDirectionalLights = 3` et de slots nommes `DirLight0..2`. C'est trop limite pour une architecture moderne et trop loin d'un modele de light list scalable.
- Sans deferred rendering, la cible moderne cote moteur forward est un systeme de lumieres scalable : nombre actif dynamique, plafond configurable plus grand, et a terme light culling par objet, par vue ou par cluster plutot qu'un triplet code en dur.
- `Lighting.fxh` utilise encore une construction `float3x3(...)` temporaire pour indexer les lumieres. Cette implementation doit disparaitre au profit d'une representation de lumieres scalable et lisible.
- `skinEffect.fx` est mieux aligne qu'avant sur les helpers partages, mais son pilotage C# reste hors du pipeline material moderne.
- `basicEffect.fx` est deja un fichier effect CasaEngine, mais plusieurs composants runtime utilisent encore la classe `BasicEffect` de MonoGame (`DebugGridComponent`, `Line3dRendererComponent`, `PrimitiveBatch`, `Primitive2D`). Ces usages doivent etre supprimes.
- `axisComponent.fx` n'est pas reellement specifique a l'axe : il dessine simplement des primitives colorees via `WorldViewProj` + `VertexPositionColor`. Il constitue un meilleur point de depart pour un shader utilitaire partage de debug que le shader material principal `basicEffect.fx`.
- Les shaders utilitaires `simple.fx`, `spritebatch.fx` et `axisComponent.fx` restent dans un style plus ancien, avec des conventions distinctes des shaders materials. Avant toute refonte, il faut auditer leur usage reel et decider s'ils doivent etre modernises ou simplement classes comme utilitaires debug/outils hors analyse material.

### 5. Conclusion de l'audit actuel

- L'architecture materials/shaders est plus moderne qu'avant, mais elle n'est pas encore completement decouplee ni pilotee par un contrat runtime unique.
- La priorite n'est pas de re-ecrire tous les shaders au hasard : il faut d'abord fermer les trous de conception entre `CompiledMaterial`, resolvers, variantes, overrides, hot reload et draw path.
- La cible finale doit etre explicite : plus de chemin legacy actif, plus d'usage de `BasicEffect` MonoGame, et plus de pipeline de lumieres borne a trois slots en dur.

---

## Liste de taches pour l'agent IA

### Phase 1 - Audit de reference et carte des dependances

- ✅ **T01.01 - Cartographier les sources de verite material/shader**
  Objectif :
  - Dresser une matrice claire des responsabilites de `MaterialAsset`, `CompiledMaterial`, `MaterialBase`, `RenderItem`, `MaterialPropertyBlock`, `ShaderVariantLibrary`, `ShaderWrapper`, `MaterialCache` et `MaterialAuthoringAssetCache`.
  - Identifier pour chaque information critique (shader effectif, features, textures, render states, overrides, queue, invalidation hot reload) sa source de verite reelle aujourd'hui.
  Livrable :
  - Une note d'audit courte dans `ai-agent/` ou `docs/rendering/` avec un tableau `type -> role -> consommateurs -> statut`.
  Validation :
  - Pas de code si inutile.
  Commit conseille :
  - `docs(materials): map current sources of truth for materials and shaders`

- ⏳ **T01.02 - Auditer chaque classe materials/shaders et la qualifier**
  Objectif :
  - Passer en revue toutes les classes de `CasaEngine/Framework/Materials`, `CasaEngine/Framework/Rendering/Shaders`, `CasaEngine.Shaders` et les renderers relies.
  - Pour chaque classe, la marquer comme `active`, `migration target`, `transition`, `suspect dead code` ou `a optimiser`.
  - Noter explicitement les APIs non consommees (`GetFeatures`, lookups de registre, chargeurs doubles, etc.) avant de planifier leur suppression.
  Validation :
  - Note d'audit versionnee.
  Commit conseille :
  - `docs(materials): classify material and shader classes by runtime role`

- 🧪 **T01.03 - Auditer tous les fichiers effects et leurs consommateurs**
  Objectif :
  - Faire un inventaire `fichier effect -> consumers C# -> type de shader -> utilitaire ou material-facing -> risque si suppression/refactor`.
  - Distinguer clairement `basicEffect.fx`, `UnlitTexture.fx`, `skinEffect.fx` des shaders utilitaires (`simple.fx`, `spritebatch.fx`, `axisComponent.fx`).
  Validation :
  - Note d'audit versionnee.
  Commit conseille :
  - `docs(shaders): inventory effect files and their runtime consumers`

- 🧪 **T01.04 - Cartographier le hot reload materials de l'editeur de bout en bout**
  Objectif :
  - Documenter precisement le chemin `editeur -> save -> asset event -> caches -> recompilation runtime -> invalidation des vues -> rendu final`.
  - Identifier les points critiques a ne pas casser lors des refactors materials/shaders.
  - Verifier si les changements de code shader eux-memes disposent d'un chemin de refresh coherent ou s'il manque une strategie separee.
  Validation :
  - Note d'audit versionnee.
  Commit conseille :
  - `docs(editor): map material hot reload and shader refresh flow`

### Phase 2 - Corrections structurelles prioritaires

- ⏳ **T02.01 - Ajouter un test de regression puis corriger le cache de variantes shader**
  Objectif :
  - Ajouter un test qui prouve qu'un meme `ShaderWrapper` partage entre plusieurs `ShaderVariantKey` ne reutilise pas une technique stale lors d'un cache hit.
  - Corriger `ShaderVariantLibrary` pour que la technique demandee soit reappliquee de maniere deterministe, y compris sur cache hit.
  Fichiers cibles :
  - `CasaEngine/Framework/Rendering/Shaders/ShaderVariantLibrary.cs`
  - `CasaEngine.Tests/Rendering/*`
  Validation :
  - Build principal.
  - Test filtre sur la zone rendering/shader variant.
  Commit conseille :
  - `fix(rendering): make shader variant technique selection deterministic`

- ⏳ **T02.02 - Introduire un contrat runtime stable de capacites material**
  Objectif :
  - Introduire un contrat ou descripteur lisible par le renderer pour exposer les capacites d'un material sans re-tester partout les types concrets.
  - Faire consulter ce contrat d'abord par `RenderFeatureResolver` et `EffectiveShaderResolver`, avec fallback legacy si necessaire.
  Fichiers cibles :
  - `CasaEngine/Framework/Materials/*`
  - `CasaEngine/Framework/Rendering/Shaders/RenderFeatureResolver.cs`
  - `CasaEngine/Framework/Rendering/Shaders/EffectiveShaderResolver.cs`
  Validation :
  - Build principal.
  - Tests filtres `RenderFeatureResolver` et `EffectiveShaderResolver`.
  Commit conseille :
  - `feat(materials): add stable capability contract for shader resolution`

- ⏳ **T02.03 - Remplacer les switches sur `definition.Id` par des services extensibles**
  Objectif :
  - Rendre `MaterialCompiler` extensible par definition au lieu de centraliser un `switch` global.
  - Faire de meme pour `MaterialInstancePropertyBlockMapper` afin que les overrides par definition soient enregistrables sans modifier le coeur du moteur.
  - Preserver explicitement la propagation hot reload quand un material edite force recompilation et re-resolution des overrides.
  Fichiers cibles :
  - `CasaEngine/Framework/Materials/MaterialCompiler.cs`
  - `CasaEngine/Framework/Materials/MaterialInstancePropertyBlockMapper.cs`
  - nouveaux services ou registres si necessaire
  Validation :
  - Build principal.
  - Tests filtres `MaterialCompiler`.
  Commit conseille :
  - `refactor(materials): make material compilation and override mapping extensible`

- ⏳ **T02.04 - Rendre `MaterialDefinitionRegistry` vraiment extensible**
  Objectif :
  - Extraire les definitions built-in du mecanisme de registre.
  - Ajouter une API d'enregistrement explicite pour de nouvelles definitions et leurs services associes.
  - Verifier si `TryGetByRuntimeType` doit etre conserve, remplace ou supprime.
  Fichiers cibles :
  - `CasaEngine/Framework/Materials/MaterialDefinitionRegistry.cs`
  - tests du registre
  Validation :
  - Build principal.
  - Tests filtres `MaterialDefinitionRegistry`.
  Commit conseille :
  - `refactor(materials): open material definition registry for extensions`

### Phase 3 - Fermer le trou entre compilation et draw path

- ⏳ **T03.01 - Faire consommer une description compilee unique par le draw path statique**
  Objectif :
  - Introduire dans `RenderItem` ou un objet associe une reference stable vers les donnees compilees utiles au draw (`shader`, `features`, `queue`, `textures`, `states`).
  - Arreter de dupliquer ces informations dans plusieurs champs derives quand elles existent deja cote compile.
  - Maintenir le hot reload de l'editeur pendant la migration : une invalidation de material doit continuer a faire converger cache compile, runtime material et draw path sans divergence.
  Fichiers cibles :
  - `CasaEngine/Framework/Materials/CompiledMaterial.cs`
  - `CasaEngine/Framework/Rendering/Draw/RenderItem.cs`
  - `CasaEngine/Framework/Game/Components/StaticMeshRendererComponent.cs`
  Validation :
  - Build principal.
  - Tests filtres `MaterialCompiler` / `MaterialCache` / rendering.
  Commit conseille :
  - `refactor(rendering): thread compiled material data into static draw path`

- ⏳ **T03.02 - Completer la representation compilee des textures et de la reflection**
  Objectif :
  - Corriger le fait que `CompiledMaterial` ne represente pas completement l'etat reflection/cubemap actuellement charge par le runtime.
  - Decider si la reflection doit etre normalisee par semantique ou via un type de ressource compilee plus riche.
  Fichiers cibles :
  - `CasaEngine/Framework/Materials/MaterialCompiler.cs`
  - `CasaEngine/Framework/Materials/CompiledMaterial.cs`
  Validation :
  - Build principal.
  - Tests filtres `MaterialCompiler`.
  Commit conseille :
  - `fix(materials): keep compiled reflection data aligned with runtime material state`

- ⏳ **T03.03 - Clarifier et reduire les sources de verite redondantes**
  Objectif :
  - Statuer sur le role de `MaterialBase.GetFeatures(...)`, `CompiledMaterial.Features`, `RenderFeatureResolver` et des property blocks.
  - Supprimer ou deprecier les APIs redondantes non consommees une fois la source de verite choisie.
  Validation :
  - Build principal.
  - Tests rendering cibles.
  Commit conseille :
  - `refactor(materials): remove redundant feature and state sources of truth`

### Phase 4 - Skinned path et suppression des chemins legacy

- ⏳ **T04.01 - Integrer le renderer skinned a la meme politique material/shader**
  Objectif :
  - Auditer puis reduire le contournement actuel de `SkinnedMeshRendererComponent`.
  - Reutiliser autant que possible les memes services de resolution shader/features/states que le rendu statique, ou formaliser clairement pourquoi le skinned reste une voie separee.
  Fichiers cibles :
  - `CasaEngine/Framework/Game/Components/SkinnedMeshRendererComponent.cs`
  - `CasaEngine/Content/Shaders/skinEffect.fx`
  - services de rendu relies
  Validation :
  - Build principal.
  - Validation visuelle du rendu skinned.
  Commit conseille :
  - `refactor(rendering): align skinned renderer with shared material and shader policies`

- ⏳ **T04.02 - Supprimer les chemins legacy materials/shaders du runtime cible**
  Objectif :
  - Migrer puis supprimer `Material` multi-texture, `MaterialLoader`, `LegacyMaterialAssetAdapter`, les doubles chargeurs `.material` et tout autre chemin runtime legacy encore actif.
  - Faire converger tout le pipeline vers `MaterialAsset` -> compilation -> runtime -> draw -> hot reload sans bifurcation historique.
  Validation :
  - Build principal.
  - Tests filtres `MaterialRuntimeResolver` / chargement materials.
  - Validation hot reload editeur sur sauvegarde et propagation runtime.
  Commit conseille :
  - `refactor(materials): remove legacy material runtime paths`

### Phase 5 - Audit et modernisation des effects

- ⏳ **T05.01 - Verifier systematiquement les permutations des shaders materials**
  Objectif :
  - Verifier que les techniques demandees par `LitDiffuseMaterial`, `UnlitTextureMaterial`, le resolver de variantes et les renderers existent bien dans `basicEffect.fx`, `UnlitTexture.fx` et `skinEffect.fx`.
  - Ajouter des tests ou une validation de chargement pour eviter les regressions silencieuses.
  Validation :
  - Build principal.
  - Tests cibles sur la disponibilite des techniques.
  Commit conseille :
  - `test(shaders): validate runtime technique coverage for material-facing effects`

- ⏳ **T05.01bis - Renommer le shader material principal pour supprimer l'ambiguite avec MonoGame `BasicEffect`**
  Objectif :
  - Renommer `basicEffect.fx` vers un nom semantique qui exprime sa vraie fonction material (`LitForward.fx`, `SurfaceLit.fx` ou equivalent).
  - Mettre a jour tous les points de chargement, ids, alias, docs et tests pour supprimer la collision de vocabulaire avec `Microsoft.Xna.Framework.Graphics.BasicEffect`.
  - S'assurer que ce renommage clarifie l'analyse : shader material principal d'un cote, shaders debug/outils de l'autre.
  Validation :
  - Build principal.
  - Verification de chargement du renderer statique et de `MaterialDemo`.
  Commit conseille :
  - `refactor(shaders): rename main lit effect for architectural clarity`

- ⏳ **T05.02 - Completer la politique canonique de variantes shader**
  Objectif :
  - Revoir `ShaderVariantLibrary.BuildTechniqueName(...)` et les alias pour couvrir explicitement les dimensions deja supportees par le moteur (`NormalMap`, `Reflection`, `VertexColor`, `Instanced`) ou documenter pourquoi certaines restent hors policy.
  - Reduire la dependance au `SelectTechnique(...)` imperatif dans les classes material quand c'est possible.
  Validation :
  - Build principal.
  - Tests rendering/shader variant cibles.
  Commit conseille :
  - `refactor(rendering): complete canonical shader variant policy`

- ⏳ **T05.03 - Remplacer le modele de lumieres fixe par un modele forward scalable**
  Objectif :
  - Faire disparaitre `MaxDirectionalLights = 3` et les slots nommes `DirLight0..2` comme architecture cible.
  - Introduire une representation de lumieres scalable cote `LightingContext`, effects et binding C# : nombre actif dynamique, plafond configurable plus grand, et strategie de culling forward raisonnable.
  - Si les contraintes MonoGame/mgfxc empechent un nombre vraiment libre, etablir un plafond propre et explicite superieur a 3 avec `activeCount` et une architecture extensible vers du forward+/clustered plus tard.
  Fichiers cibles :
  - `CasaEngine/Framework/Rendering/LightingContext.cs`
  - `CasaEngine/Content/Shaders/Lighting.fxh`
  - `CasaEngine/Content/Shaders/basicEffect.fx`
  - `CasaEngine/Content/Shaders/skinEffect.fx`
  - binders C# relies
  Validation :
  - Build principal.
  - Verification visuelle bornes sur rendu lit.
  - Tests cibles sur la nouvelle politique de binding des lumieres.
  Commit conseille :
  - `feat(rendering): replace fixed three-light pipeline with scalable forward lighting`

- 🧪 **T05.04 - Remplacer tous les usages restants de `BasicEffect` MonoGame**
  Objectif :
  - Migrer `DebugGridComponent`, `Line3dRendererComponent`, `PrimitiveBatch`, `Primitive2D` et toute autre zone restante vers des effects CasaEngine.
  - Evaluer si `axisComponent.fx` doit etre renomme et generalise en shader utilitaire partage de primitives colorees 3D, plutot que de reutiliser le shader material principal.
  - S'assurer qu'aucun renderer/runtime path CasaEngine n'instancie encore `Microsoft.Xna.Framework.Graphics.BasicEffect`.
  Validation :
  - Build principal.
  - Verification visuelle ou smoke des composants concernes.
  Commit conseille :
  - `refactor(rendering): replace MonoGame BasicEffect usages with CasaEngine effects`

- ⏳ **T05.05 - Auditer puis moderniser les shaders utilitaires CasaEngine**
  Objectif :
  - Examiner `simple.fx`, `spritebatch.fx` et `axisComponent.fx` avec leurs consommateurs reels.
  - Renommer les shaders utilitaires pour que leur role soit explicite, par exemple `DebugPrimitiveColor.fx`, `SpriteBatch.fx`, `DebugAxis.fx` si des variantes separentes restent necessaires.
  - Les conserver uniquement comme effects CasaEngine explicites, eventuellement les moderniser, mais sans retomber sur `BasicEffect` MonoGame.
  - Les sortir explicitement du perimetre d'analyse de l'architecture materials si leur role final est purement debug/outillage.
  Validation :
  - Build principal.
  - Note de decision versionnee si aucun changement de code n'est utile.
  Commit conseille :
  - `docs(shaders): classify and modernize CasaEngine utility effects`

- ⏳ **T05.06 - Formaliser une convention de nommage des shaders et includes**
  Objectif :
  - Definir une convention claire et stable pour distinguer les shaders materials, debug/outillage, 2D/blit et les includes partages.
  - Appliquer cette convention au moins aux fichiers les plus ambigus (`basicEffect.fx`, `axisComponent.fx`, `simple.fx`).
  Convention cible proposee :
  - shaders materials : noms semantiques en PascalCase (`LitForward.fx`, `UnlitTexture.fx`, `SkinnedLit.fx`)
  - shaders debug/outils : prefixe `Debug` (`DebugPrimitiveColor.fx`, `DebugGrid.fx`, `DebugAxis.fx`)
  - shaders 2D/blit/utilitaires : prefixe ou role explicite (`SpriteBatch.fx`, `BlitTexture.fx`)
  - includes partages : suffixe `Common` ou role explicite (`LightingCommon.fxh`, `VertexStructures.fxh`, `ShaderMacros.fxh`)
  Validation :
  - Note de convention versionnee.
  - Build principal si des renommages sont appliques.
  Commit conseille :
  - `docs(shaders): define naming convention for shader assets and includes`

### Phase 6 - Tooling, tests et documentation finale

- ⏳ **T06.01 - Corriger le parsing des diagnostics de compilation shader**
  Objectif :
  - Corriger `CasaEngine.Shaders/ShaderCompiler.cs` pour parser proprement les sorties `mgfxc`.
  - Ajouter des tests unitaires sur le parsing ou factoriser la logique pour qu'elle soit testable sans lancer `mgfxc` a chaque fois.
  Validation :
  - Build solution si necessaire.
  - Tests filtres sur `CasaEngine.Shaders` ou tests purs de parsing.
  Commit conseille :
  - `fix(shaders): parse mgfxc diagnostics reliably`

- ⏳ **T06.02 - Ajouter une couverture de tests ciblee sur l'architecture materials/shaders**
  Objectif :
  - Completer les tests sur `MaterialCompiler`, `MaterialRuntimeResolver`, `MaterialCache`, `RenderFeatureResolver`, `EffectiveShaderResolver`, la policy de variantes et le binding de lumieres scalable.
  - Ajouter des checks ou smoke tests bornes sur le hot reload editor/runtime pour les regressions les plus critiques.
  - Ajouter au moins un test de non-regression par bug ou fragilite corrigee dans les phases precedentes.
  Validation :
  - Suite de tests filtres par zone.
  Commit conseille :
  - `test(materials): extend focused coverage for material and shader architecture`

- ⏳ **T06.03 - Mettre a jour la documentation d'architecture finale**
  Objectif :
  - Mettre a jour `docs/rendering/materials-workflow.md` ou un document d'architecture associe pour refleter la source de verite finale, le contrat de hot reload, le nouveau modele de lumieres scalable et la disparition des chemins legacy et de `BasicEffect` MonoGame.
  Validation :
  - Relecture technique du document.
  Commit conseille :
  - `docs(materials): refresh architecture workflow after deep audit`

---

## Ordre recommande

1. Phase 1 complete : geler une cartographie fiable avant les refactors.
2. T02.01 en premier : c'est la correction structurelle la plus a risque cote rendu deterministe.
3. Ensuite T02.02 -> T02.04 pour poser un socle extensible.
4. Puis Phase 3 pour brancher la representation compilee dans le draw path.
5. Ensuite Phase 4 pour traiter skinned et supprimer les chemins legacy avec le nouveau cadre.
6. Puis Phase 5 pour normaliser les effets, sortir de `BasicEffect` MonoGame et passer a un modele de lumieres scalable.
7. Finir par Phase 6 pour renforcer outillage, tests, hot reload et documentation.

## Criteres d'acceptation finaux

- Le renderer peut raisonner sur des capacites ou une description compilee stable sans multiplier les tests sur les types concrets.
- Le registre de definitions et les mappers associes sont extensibles sans grossir un `switch` central.
- `CompiledMaterial` ou un descripteur equivalent participe reellement au draw path, pas seulement au cache et aux tests.
- Le cache de variantes shader est deterministe et couvre clairement les permutations prises en charge.
- Le hot reload material de l'editeur continue a propager les changements vers les materials et vues deja charges sans chemin parallele fragile.
- Le moteur ne garde plus de chemin legacy materials/shaders actif dans son architecture cible.
- Le moteur n'utilise plus `BasicEffect` MonoGame dans ses renderers et composants runtime.
- Le pipeline de lumieres n'est plus borne a trois slots en dur et suit un modele forward scalable.
- Les effets materials, les includes et le tooling shader sont verifies par des tests ou des checks cibles suffisants pour eviter les regressions silencieuses.