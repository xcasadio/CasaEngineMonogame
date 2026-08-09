# Plan agent IA — Rendu PBR (metallic-roughness) dans CasaEngine

Ce fichier est un plan d'execution pour un agent IA peu autonome. Il doit etre mis a jour pendant le travail : l'icone au debut de chaque tache indique son statut courant.

## Objectif

Ajouter un modele d'eclairage PBR metallic-roughness dans CasaEngine **en complement** du modele Blinn-Phong existant, choisi **par material** (nouvelle definition `lit-pbr` a cote de `lit-diffuse` et `unlit-texture`). Le plan couvre : workflow lineaire + HDR + tonemapping (prerequis), BRDF GGX en eclairage direct, IBL (irradiance + specular prefiltre + LUT BRDF), import glTF sans degradation, demos et documentation.

## Etat verifie du depot (2026-08-09)

- Le modele d'eclairage actuel est Blinn-Phong : `SpecularPower` et `pow(dot(H,N), SpecularPower)` dans `CasaEngine/Content/Shaders/Lighting.fxh` (partage par `LitForward.fx` et `skinEffect.fx` via `#include`).
- Aucun workflow lineaire, sRGB, HDR ou tonemapping n'existe (verifie par recherche dans `Content/Shaders` et `Framework/Rendering`).
- Deux definitions de material built-in : `lit-diffuse` et `unlit-texture` dans `CasaEngine/Framework/Materials/Definitions/BuiltInMaterialDefinitions.cs`. Le registre est extensible : `MaterialDefinitionRegistry.Register(definition, runtimeMaterialFactory, overrideMapper)`.
- Chaine material : `MaterialAsset` → `MaterialCompiler` → `CompiledMaterial` → `MaterialRuntimeResolver` / `MaterialRenderStateResolver` ; overrides par instance via `MaterialPropertyBlock` et par slot via `MaterialSlotOverride`.
- Shaders built-in enregistres dans `CasaEngine/Framework/Rendering/Shaders/BuiltInShaderCatalog.cs` (source `.fx` + content name), hot-reloadables.
- Flags `ShaderFeature` existants : `BasColorTexture`, `VertexColor`, `AlphaTest`, `Skinned`, `Instanced`, `NormalMap`, `Emissive`, `Transparent`, `Reflection`.
- `WorldEnvironmentSettings` expose deja : `BackgroundCubemap`, `SpecularEnvironmentCubemap` (+ asset ids), `AmbientColor`, `AmbientIntensity`, `SpecularIntensity`, `ProceduralSky`, `PhysicalAtmosphere`, `Shadows`, suivi de version (`IsDirty`/`Version`).
- Un generateur de cubemap existe deja comme precedent : `PanoramaEnvironmentGenerator`.
- Presentation par vue : `IViewPresenter`, `BackBufferPresenter`, `TexturePresenter` ; `RenderPipeline` sait deja rendre la scene dans un `sceneRt` intermediaire (contournement D3D11 shadows) avant blit.
- Import glTF : `GltfStaticModelReader` lit le canal `MetallicRoughness` mais le **degrade** en Blinn-Phong via `ConvertRoughnessToSpecularPower` (`GltfStaticModelReader.cs`, ~ligne 204).
- Demos utiles : `CasaEngine.Demos/Demos/MaterialDemo.cs`, `EnvironmentShowcaseDemo.cs`. Le harnais de demos supporte `CASAENGINE_START_DEMO` et `CASAENGINE_CAPTURE_SCREENSHOT_PATH` pour la validation par screenshot.

## Decisions verrouillees

- **Choix par material, pas par jeu ni par level** : `lit-pbr` est une definition supplementaire ; `lit-diffuse` reste intact et le contenu existant ne doit subir aucune regression visuelle.
- **Workflow metallic-roughness** (aligne glTF), pas specular-glossiness.
- **Forward only** : on reste dans `ForwardRenderPipeline` (Opaque/Transparent passes). Pas de deferred, pas de clustered.
- Les plafonds de lumieres existants (8 directionnelles + 8 ponctuelles + 8 spots, contrainte mgfxc) ne changent pas.
- `LitPbr.fx` est **pixel lighting uniquement** (pas de variante vertex lighting).
- Le pipeline couleur (lineaire/HDR/tonemap) est un reglage **par World/vue** (via `WorldEnvironmentSettings`), pas par material. Valeur par defaut : `Legacy` (comportement actuel inchange).
- Respecter les conventions de techniques/macros existantes (`Macros.fxh`) et les cibles de compilation deja utilisees par `LitForward.fx` (compatibilite mgfxc).
- Le skinning PBR (`skinEffect` en GGX) est **hors perimetre V1** — note en extension future.

## Regles obligatoires pour l'agent

- Lire avant toute modification : `.github/copilot-instructions.md`, `AGENTS.md`, et s'ils existent `.github/instructions/csharp-monogame.instructions.md` et `.github/instructions/rendering.instructions.md`.
- Une seule tache a la fois. Ne jamais commencer la tache suivante tant que la tache courante n'est pas validee et commitee.
- Avant de commencer une tache, remplacer son icone `⏳` par `🚧`.
- A la fin de chaque tache : lancer la validation indiquee, remplacer l'icone par `✅`, `🧪` ou `⚠️`, ajouter une courte note de validation sous la tache, puis **creer un commit dedie**.
- Si le code est ecrit mais qu'une verification visuelle ou un test manque, utiliser `🧪 Needs testing` et noter precisement ce qui manque.
- Si une tache est bloquee, utiliser `⚠️ Blocked`, expliquer pourquoi dans ce fichier, et demander une decision.
- Ne jamais laisser une tache en `🚧` a la fin d'une session.
- Aucune allocation dans les chemins chauds (`Update`/`Draw`/flush des renderers) : pas de LINQ, pas de closures, buffers reutilises.
- Restaurer tout etat GPU modifie (render targets, blend/depth/rasterizer/sampler states).
- Ne pas casser la serialisation des `.material` existants : changements additifs uniquement.

## Legende des statuts

- ⏳ Todo : pas encore commence.
- 🚧 In progress : en cours de modification locale.
- 🧪 Needs testing : code ecrit, validation incomplete ou en attente.
- ✅ Done : code valide, build/tests OK, commit effectue.
- ⚠️ Blocked : bloque par une erreur non resolue ou une decision manquante.

---

## Phase 0 — Baseline et garde-fous

### ⏳ PBR-000 — Captures de reference des demos existantes

- Objectif : disposer d'une baseline visuelle avant tout changement du pipeline couleur.
- Etapes :
  1. Lancer `CasaEngine.Demos` avec `CASAENGINE_START_DEMO` + `CASAENGINE_CAPTURE_SCREENSHOT_PATH` pour capturer au minimum : `MaterialDemo`, `EnvironmentShowcaseDemo`, `SkinnedMeshDemo`, une demo shadows.
  2. Stocker les captures dans `artifacts/pbr-baseline/` (hors assets du moteur) et lister les fichiers dans la note de validation ci-dessous.
- Validation : captures presentes et nettes ; noter la resolution et la demo de chaque capture.
- Commit : `chore(pbr): capture baseline screenshots before color pipeline work`

---

## Phase 1 — Workflow lineaire, HDR et tonemapping (prerequis)

### ⏳ PBR-101 — Reglages du pipeline couleur dans WorldEnvironmentSettings

- Objectif : exposer un reglage de pipeline couleur par World, serialise, sans changer le rendu par defaut.
- Fichiers : `CasaEngine/Framework/Rendering/Environment/WorldEnvironmentSettings.cs`, serialisation associee (chercher ou `WorldEnvironmentSettings` est charge/sauve), `RenderFrame`/`RenderFrameFactory` pour l'exposition au rendu.
- Etapes :
  1. Ajouter un type `ColorPipelineSettings` : `Mode` (`Legacy` par defaut, `LinearHdr`), `Tonemapper` (`Reinhard`, `AcesApprox`), `Exposure` (float, defaut 1.0).
  2. L'ajouter a `WorldEnvironmentSettings` en incrementant la version dirty comme les autres proprietes.
  3. Le propager dans `RenderFrame` (immutable) via `RenderFrameFactory`.
  4. Serialisation additive : les mondes existants chargent en `Legacy`.
- Validation : build solution ; charger un world existant → aucun changement de champ requis, rendu identique.
- Commit : `engine(pbr): add ColorPipelineSettings (legacy default) to world environment`

### ⏳ PBR-102 — Render target de scene HDR par vue

- Objectif : quand `Mode == LinearHdr`, rendre la scene 3D dans un RT `HalfVector4` (fallback `HdrBlendable` puis `Color` si non supporte) avant presentation.
- Fichiers : `CasaEngine/Framework/Rendering/RenderPipeline.cs` (reutiliser/etendre le mecanisme `sceneRt` existant du contournement D3D11), `RenderTargetPool` si necessaire.
- Etapes :
  1. Etendre la logique existante de rendu intermediaire : en `LinearHdr`, la scene est toujours rendue dans un `sceneRt` au format HDR (le contournement shadows devient un cas particulier du meme chemin).
  2. Verifier l'interaction avec `ResolutionScale` et les vues `RenderTargetSurface` (render-to-texture).
  3. La composition UI (MGUI) doit rester **apres** le tonemapping : reperer dans `DefaultViewPipeline` ou l'UI est composee et documenter le point d'insertion pour PBR-103.
- Validation : build ; demos en `Legacy` inchangees (comparer aux captures PBR-000) ; en `LinearHdr` sans tonemap encore branche, l'image peut etre delavee — c'est attendu, le noter.
- Commit : `engine(pbr): render scene into HDR target when LinearHdr mode is active`

### ⏳ PBR-103 — Passe de tonemapping a la presentation

- Objectif : tonemapper le RT HDR vers la surface de sortie (ACES approx + exposure + encodage gamma), UI composee apres.
- Fichiers : nouveau `CasaEngine/Content/Shaders/Tonemap.fx` (fullscreen), `BuiltInShaderCatalog.cs` (enregistrement + hot reload), `BackBufferPresenter.cs`, `TexturePresenter.cs` (ou point de blit equivalent identifie en PBR-102).
- Etapes :
  1. Ecrire `Tonemap.fx` : techniques `Tonemap_Reinhard` et `Tonemap_AcesApprox` (approximation ACES de Narkowicz), parametre `Exposure`, encodage lineaire→sRGB en sortie (`pow(c, 1/2.2)` acceptable en V1).
  2. L'enregistrer dans `BuiltInShaderCatalog.RuntimeShaderDescriptors`.
  3. Brancher la passe au moment du blit scene→sortie quand `Mode == LinearHdr` ; en `Legacy`, blit inchange.
  4. Verifier que l'UI (MGUI) et les overlays debug sont dessines apres le tonemap et ne sont pas alteres.
- Validation : build ; `MaterialDemo` en `LinearHdr` + ACES rend une image plausible (pas delavee, pas doublement gamma-corrigee) ; demos en `Legacy` strictement identiques aux captures PBR-000.
- Commit : `engine(pbr): add tonemap pass (Reinhard/ACES) at view presentation`

### ⏳ PBR-104 — Decodage sRGB des textures couleur en mode lineaire

- Objectif : en `LinearHdr`, les textures base color et les couleurs de material sont converties en lineaire avant eclairage.
- Fichiers : `Lighting.fxh` ou `Macros.fxh` (helper `DecodeSrgb`), `LitForward.fx`, `skinEffect.fx`, bind des parametres dans `LitDiffuseMaterial`/`ShaderBindCache` (nouveau parametre global `ColorPipelineMode`).
- Etapes :
  1. Ajouter un uniform global (ex: `float ColorPipelineIsLinear`) pousse via le bind des globals par frame (meme mecanisme que camera/lumieres dans `ShaderBindCache`).
  2. Decoder base color texture + couleurs de material (`pow(c, 2.2)` approx) quand le mode est lineaire ; les normal maps, metallic-roughness et donnees non-couleur ne sont **jamais** decodees.
  3. Verifier le clear color et le fog/ambient : toute couleur authoree doit etre decodee de facon coherente.
- Validation : build ; en `Legacy` rendu identique (le flag vaut 0) ; en `LinearHdr` + ACES, `MaterialDemo` conserve des couleurs proches de l'authoring (pas de double correction).
- Commit : `engine(pbr): decode sRGB color inputs when linear pipeline is active`

### ⏳ PBR-105 — Validation visuelle de la phase 1

- Objectif : verrouiller la non-regression avant d'ecrire le moindre shader PBR.
- Etapes :
  1. Recapturer les demos de PBR-000 en `Legacy` et comparer (aucune difference attendue).
  2. Capturer `MaterialDemo` et `EnvironmentShowcaseDemo` en `LinearHdr`/ACES et archiver dans `artifacts/pbr-baseline/linear/`.
  3. Verifier `RenderStats` : pas de draw calls ni de changements d'etat supplementaires en `Legacy`.
- Validation : comparaison faite et notee ici (differences = tache ⚠️).
- Commit : `test(pbr): validate legacy visual parity after color pipeline phase`

---

## Phase 2 — BRDF GGX en eclairage direct

### ⏳ PBR-201 — Pbr.fxh : fonctions BRDF partagees

- Objectif : isoler le BRDF dans un include reutilisable, branche sur les memes donnees de lumieres que `Lighting.fxh`.
- Fichiers : nouveau `CasaEngine/Content/Shaders/Pbr.fxh`.
- Etapes :
  1. Implementer : distribution GGX (Trowbridge-Reitz), visibilite Smith height-correlated (ou Schlick-GGX), Fresnel Schlick, `F0` = lerp(0.04, baseColor, metallic).
  2. Fournir `EvaluatePbrDirectLight(...)` consommant les structures/uniforms de lumieres existants (directionnelles + ponctuelles + spots avec attenuation existante) sans dupliquer leurs declarations — reutiliser `Lighting.fxh` ou factoriser ce qui doit l'etre sans changer le comportement de `LitForward.fx`.
  3. Prevoir les entrees : `baseColor`, `metallic`, `roughness` (perceptual, remap `alpha = r*r`), `N`, `V`, `occlusion`, `emissive`.
- Validation : `LitForward.fx` et `skinEffect.fx` compilent toujours (mgfxc) ; aucun changement de rendu existant.
- Commit : `shaders(pbr): add Pbr.fxh with GGX/Smith/Schlick BRDF helpers`

### ⏳ PBR-202 — LitPbr.fx : effect PBR statique

- Objectif : le shader material PBR pour meshes statiques, avec un jeu de techniques borne.
- Fichiers : nouveau `CasaEngine/Content/Shaders/LitPbr.fx` (+ `Structures.fxh`/`Macros.fxh` si extension necessaire).
- Etapes :
  1. Techniques V1 (pixel lighting uniquement), combinaisons : base color texture (on/off) × normal map (on/off) × metallic-roughness texture (on/off) × alpha test (on/off) × instancing (on/off). Limiter l'explosion : suivre le pattern de nommage/macros de `LitForward.fx` et ne generer que les combinaisons realistes (documenter la liste dans le fichier).
  2. Convention texture : metallic-roughness packee style glTF (G = roughness, B = metallic), occlusion optionnelle canal R (ou texture separee — suivre glTF).
  3. Ambient V1 : terme plat `AmbientColor * AmbientIntensity * occlusion` (sera remplace par l'IBL en phase 4 ; isoler ce calcul dans une fonction pour la substitution).
  4. Ombres : echantillonner la shadow map exactement comme `LitForward.fx` (memes parametres/biais).
  5. Emissive + sortie HDR non clampee (le tonemap de phase 1 s'en charge).
- Validation : compilation mgfxc de toutes les techniques ; pas encore branche au runtime.
- Commit : `shaders(pbr): add LitPbr.fx static-mesh PBR effect`

### ⏳ PBR-203 — Enregistrement runtime du shader LitPbr

- Objectif : rendre `LitPbr.fx` selectionnable et hot-reloadable comme les autres built-ins.
- Fichiers : `BuiltInShaderCatalog.cs`, `EffectiveShaderResolver` (content name), `RenderShaderSelector`/`ShaderVariantLibrary` (selection de technique par `ShaderFeature`).
- Etapes :
  1. Ajouter le descriptor `("Shaders/LitPbr.fx", LitPbrContentName)` et la constante de content name.
  2. Ajouter les nouveaux flags `ShaderFeature` necessaires (ex: `MetallicRoughnessTexture = 1 << 9`, `Occlusion = 1 << 10`) — additif, ne pas renumberoter les flags existants (ils participent aux clefs de tri).
  3. Etendre la resolution de technique pour mapper les combinaisons de features vers les techniques de PBR-202.
  4. Verifier le hot reload (`TryReloadBuiltInShader` ou mecanisme equivalent du catalogue).
- Validation : build ; test unitaire de resolution de technique (features → nom de technique attendu) dans `CasaEngine.Tests`.
- Commit : `engine(pbr): register LitPbr shader and feature-to-technique resolution`

---

## Phase 3 — Definition et material runtime `lit-pbr`

### ⏳ PBR-301 — LitPbrMaterial runtime

- Objectif : la classe runtime qui binde les parametres PBR sur `LitPbr.fx`.
- Fichiers : nouveau `CasaEngine/Framework/Materials/Runtime/LitPbrMaterial.cs` (modele : `LitDiffuseMaterial`).
- Etapes :
  1. Proprietes : `BaseColor` (Color), `Metallic` (float 0-1), `Roughness` (float 0-1), `EmissiveColor` (Vector3), `AlphaCutoff`, `OcclusionStrength`, textures : base color, normal, metallic-roughness, occlusion, emissive.
  2. Calcul des `ShaderFeature` selon les textures presentes (meme pattern que `LitDiffuseMaterial.GetFeatures`).
  3. Bind des parametres effect avec cache (pas de lookups par nom a chaque frame si le pattern existant les cache — suivre l'existant).
- Validation : build ; test unitaire : features attendues selon textures presentes.
- Commit : `engine(pbr): add LitPbrMaterial runtime material`

### ⏳ PBR-302 — Definition `lit-pbr` et compilation

- Objectif : la `MaterialDefinition` complete, integree au compilateur et aux resolvers.
- Fichiers : `BuiltInMaterialDefinitions.cs`, `MaterialCompiler.cs`, `MaterialRuntimeResolver.cs`, `MaterialInstancePropertyBlockMapper.cs`, `MaterialRenderStateResolver.cs` si necessaire.
- Etapes :
  1. Definition `id: "lit-pbr"`, `runtimeMaterialType: typeof(LitPbrMaterial)`, proprietes avec les memes conventions que `lit-diffuse` : keys snake_case (`base_color_texture`, `normal_texture`, `metallic_roughness_texture`, `occlusion_texture`, `emissive_texture`, `base_color`, `metallic`, `roughness`, `emissive_color`, `alpha_cutoff`, `occlusion_strength`), groupes (`Textures`, `Surface`, `Rendering`), flags (`AssetReference`, `SupportsOverrides`, `AffectsShaderCompilation`, `AffectsTransparency` sur `base_color`), min/max/step, `editorControlHint`.
  2. Brancher la compilation → `CompiledMaterial` (queue opaque/alpha-test/transparent selon `base_color.A` et `alpha_cutoff`, comme lit-diffuse).
  3. Supporter les overrides par instance (`MaterialPropertyBlock`) pour `base_color`, `metallic`, `roughness`, `emissive_color`.
- Validation : build ; tests unitaires : round-trip serialisation `.material` lit-pbr (`MaterialAssetJsonSerializer`), compilation → features/queue attendues, override par instance applique.
- Commit : `engine(pbr): add lit-pbr material definition, compilation and overrides`

### ⏳ PBR-303 — Inspector editeur et creation d'asset

- Objectif : verifier que l'editeur genere l'UI du material `lit-pbr` et permet d'en creer un.
- Fichiers : cote `CasaEngine.Editor` — l'inspector material est genere depuis `MaterialDefinition` ; verifier `MaterialAssetInspectorPanel` et le flux de creation d'asset material (content browser).
- Etapes :
  1. Creer un `.material` lit-pbr via l'editeur, verifier que tous les champs apparaissent avec les bons controles (sliders metallic/roughness, color pickers, pickers de textures).
  2. Verifier la preview material de l'editeur avec le nouveau shader (y compris hot reload de `LitPbr.fx` pendant l'edition).
  3. Corriger uniquement ce qui empeche la generation (pas de refonte de l'inspector).
- Validation : smoke manuel editeur note ici ; sauvegarde/rechargement de l'asset OK.
- Commit : `editor(pbr): verify and wire lit-pbr material inspector`

---

## Phase 4 — IBL (image-based lighting)

### ⏳ PBR-401 — Irradiance diffuse depuis le cubemap d'environnement

- Objectif : remplacer l'ambient plat du PBR par une irradiance issue de l'environnement.
- Fichiers : `CasaEngine/Framework/Rendering/Environment/` (modele : `PanoramaEnvironmentGenerator`), `WorldEnvironmentSettings`, `EnvironmentResolver`.
- Etapes :
  1. Generer une irradiance depuis `SpecularEnvironmentCubemap` (ou `BackgroundCubemap` en fallback) : convolution cosinus vers un petit cubemap (32px) — generation GPU au chargement/changement d'environnement (suivre le cycle dirty/`Version` de `WorldEnvironmentSettings`), avec cache.
  2. Exposer le resultat via `EnvironmentResolver`/`RenderFrame` pour le bind shader.
  3. Fallback : si aucun cubemap, conserver l'ambient plat actuel.
- Validation : build ; demo environnement : la teinte ambiante des objets PBR suit visiblement le cubemap ; pas de regeneration par frame (verifier via compteur/log une seule generation par changement).
- Commit : `engine(pbr): generate diffuse irradiance cubemap from environment`

### ⏳ PBR-402 — Cubemap specular prefiltre par roughness

- Objectif : reflexions floutees correctement selon la roughness (mips GGX).
- Fichiers : memes zones que PBR-401 + shader de prefiltrage (nouveau `.fx` utilitaire ou compute-like via draw fullscreen par face/mip).
- Etapes :
  1. Prefiltrer `SpecularEnvironmentCubemap` : chaine de mips ou chaque mip correspond a une roughness croissante (importance sampling GGX, nombre d'echantillons borne et documente).
  2. Cache + regeneration sur changement d'environnement uniquement.
  3. Mapping roughness→mip documente dans le shader (`roughness * (mipCount - 1)`).
- Validation : demo : une rangee de spheres roughness 0→1 montre des reflexions de plus en plus floues, sans banding grossier.
- Commit : `engine(pbr): prefilter specular environment cubemap by roughness`

### ⏳ PBR-403 — LUT BRDF et branchement IBL dans LitPbr.fx

- Objectif : terme ambiant specular complet (split-sum approximation).
- Fichiers : `Pbr.fxh`, `LitPbr.fx`, generation de la LUT (texture 2D generee une fois au demarrage ou embarquee dans le content).
- Etapes :
  1. Generer la LUT BRDF (NdotV × roughness → scale/bias de F0) une fois, format `HalfVector2`/`Color` selon support.
  2. Dans `LitPbr.fx` : ambient = irradiance × albedo diffus + prefiltered specular × (F0 × scale + bias), module par occlusion.
  3. `SpecularIntensity`/`AmbientIntensity` de `WorldEnvironmentSettings` restent les multiplicateurs artiste.
  4. Sans environnement : fallback ambient plat (deja prevu en PBR-202).
- Validation : sphere metallic=1/roughness=0 reflete nettement l'environnement ; metallic=0/roughness=1 quasi mat ; comparer visuellement a un rendu Blender du meme asset glTF si possible (noter l'ecart).
- Commit : `shaders(pbr): wire IBL (irradiance + prefiltered specular + BRDF LUT) into LitPbr`

---

## Phase 5 — Import glTF sans degradation

> Coordination : `ai-agent/tasks/gltf-import-migration-tasks.md` est en cours (phases C/E restantes). Ne pas entrer en conflit : cette phase modifie la **traduction des materials**, pas le cablage import editeur. Si la tache C2 de ce plan-la n'est pas faite, travailler au niveau de `GltfStaticModelReader` uniquement.

### ⏳ PBR-501 — Extraction fidele des canaux PBR au chargement glTF

- Objectif : conserver metallic/roughness/textures au lieu de les convertir en specular power.
- Fichiers : `CasaEngine/Framework/Assets/Loaders/GltfStaticModelReader.cs`.
- Etapes :
  1. Etendre les metadata material extraites : `BaseColorFactor`, `MetallicFactor`, `RoughnessFactor`, chemins des textures base color / metallic-roughness / normal / occlusion / emissive + `EmissiveFactor`, `AlphaMode`/`AlphaCutoff`, `DoubleSided`.
  2. Conserver `ConvertRoughnessToSpecularPower` comme chemin de compatibilite (utilise quand la cible est `lit-diffuse`).
  3. Ne pas changer le contrat public existant : ajout de champs uniquement.
- Validation : tests unitaires sur un `.glb` de test embarque : les facteurs et chemins extraits correspondent au fichier source.
- Commit : `engine(pbr): extract full metallic-roughness material data from glTF`

### ⏳ PBR-502 — Generation d'assets `.material` lit-pbr a l'import

- Objectif : l'import editeur d'un glTF produit des materials `lit-pbr` fideles.
- Fichiers : service d'import editeur (`EditorAssetImportService` cote `CasaEngine.EditorServices`) — respecter l'etat courant de la migration glTF.
- Etapes :
  1. Option d'import : `Target material model = Lit PBR (defaut pour glTF) | Lit Diffuse (compat)`.
  2. En mode PBR : creer les `.material` lit-pbr avec facteurs + references textures importees ; `AlphaMode` glTF → queue/cutoff.
  3. En mode compat : comportement actuel (conversion specular power) inchange.
- Validation : importer un asset glTF PBR connu (ex: DamagedHelmet) → materials lit-pbr crees, rendu correct dans la demo/editeur ; noter une capture.
- Commit : `editor(pbr): import glTF materials as lit-pbr assets`

---

## Phase 6 — Demos, validation finale et documentation

### ⏳ PBR-601 — PbrMaterialDemo

- Objectif : la demo de reference : grille de spheres metallic (lignes) × roughness (colonnes) + un asset glTF importe.
- Fichiers : nouveau `CasaEngine.Demos/Demos/PbrMaterialDemo.cs` (modele : `MaterialDemo.cs`), enregistrement dans `DemosGame`.
- Etapes :
  1. Grille 5×5 minimum via overrides par instance (`metallic`, `roughness`) sur un seul asset material — valide aussi le chemin d'overrides.
  2. Environnement avec cubemap actif + une directionnelle avec shadows ; `LinearHdr` + ACES actives.
  3. Toggles debug (clavier) : tonemapper on/off, exposure +/-, IBL on/off — pour diagnostic visuel.
  4. `Clean()` restaure l'etat global (environnement, pipeline couleur) comme les demos recentes.
- Validation : demo lancee, capture archivee ; `RenderStats` coherents (les spheres partagent shader/material → draw calls groupes).
- Commit : `demos(pbr): add PbrMaterialDemo metallic/roughness grid`

### ⏳ PBR-602 — Non-regression finale et perfs

- Objectif : verrouiller l'ensemble.
- Etapes :
  1. Build `CasaEngine.MonoGame.sln` + `CasaEngine.Editor.MonoGame.sln` ; `dotnet test CasaEngine.Tests` (noter les echecs preexistants hors perimetre s'il y en a).
  2. Recapturer toutes les demos de PBR-000 en `Legacy` et comparer a la baseline : identiques.
  3. `MaterialDemo` (Blinn-Phong) et `PbrMaterialDemo` cote a cote en `LinearHdr` : les deux plausibles.
  4. Verifier `RenderStats` sur `PbrMaterialDemo` : pas d'explosion de state changes ni d'allocations par frame (pas de generation IBL par frame).
- Validation : resultats notes ici, ecarts traites ou expliques.
- Commit : `test(pbr): final visual and perf non-regression pass`

### ⏳ PBR-603 — Documentation

- Objectif : documenter le systeme pour le wiki.
- Fichiers : nouveau `docs/engine/pbr-rendering.md` ; mise a jour de `docs/engine/materials-workflow.md`, `docs/engine/effect-file-inventory.md` (ajouter `LitPbr.fx`, `Pbr.fxh`, `Tonemap.fx`), `docs/README.md` (index), `ai-agent/README.md` (statut de ce plan).
- Contenu minimal de `pbr-rendering.md` : workflow metallic-roughness, choix par material, reglages du pipeline couleur, chaine IBL et ses caches, import glTF, limitations V1 (pas de skinning PBR, une seule shadow map, plafonds de lumieres), extensions futures (skinEffect GGX, probes locales prefilrees, clearcoat).
- Validation : liens des index verifies.
- Commit : `docs(pbr): document PBR rendering and update indexes`

---

## Extensions futures (hors perimetre de ce plan)

- Skinning PBR : porter `skinEffect.fx` sur `Pbr.fxh`.
- Probes de reflexion locales prefilrees (aujourd'hui seule la cubemap d'environnement globale est prefilree).
- Transparents PBR avances (transmission, clearcoat, sheen glTF extensions).
- Exposition automatique (histogramme) et bloom HDR.
- Cascades de shadow maps (dependance qualite, pas PBR a proprement parler).
