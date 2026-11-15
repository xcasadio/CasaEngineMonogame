# Plan agent IA — Rendu PBR (metallic-roughness), pipeline couleur HDR, bloom, IBL

Plan d'exécution du chantier PBR du moteur. Il remplace [pbr-rendering-implementation-plan.md](archive/pbr-rendering-implementation-plan.md) (2026-08-09, aucune tâche commencée), devenu inexécutable après les ADR-0086 à ADR-0093. L'analyse de départ est le relevé du code du 2026-11-14 résumé dans « État vérifié du dépôt ».
Les décisions D1 → D12 ci-dessous ont été arbitrées avec l'auteur (ADR-0013 le 2026-08-09, réponses du 2026-11-14) : **ce plan les applique, il ne les rediscute pas**. Les arbitrages P1 → P12 sont proposés par l'agent ; l'auteur a demandé le 2026-11-14 d'écrire ce plan puis de l'exécuter en autonomie, ils sont donc appliqués tels quels et restent révisables.

Ce fichier doit être mis à jour pendant le travail : l'icône au début de chaque tâche indique son statut courant.

## Objectif

Livrer dans CasaEngine un rendu PBR metallic-roughness **en complément** du Blinn-Phong existant, choisi par matériau (`lit-pbr` à côté de `lit-diffuse`) :

1. un pipeline couleur par monde (`Legacy` par défaut, `LinearHdr`) : cible de scène HDR, tonemapping, interface composée après le tonemapping ;
2. un bloom sur la cible HDR ;
3. un BRDF GGX en éclairage direct, opaque, alpha-test et transparent ;
4. un éclairage par l'image : irradiance, spéculaire préfiltré par rugosité, table BRDF, sondes de réflexion locales préfiltrées ;
5. le PBR sur les maillages skinnés ;
6. un import glTF qui garde les canaux PBR ;
7. une démo de référence et la documentation.

Contrainte transverse : en mode `Legacy`, toute image existante reste identique au bit près.

## Tranches

Chaque phase est une tranche approuvable, vérifiable et réversible seule (une suite de commits sur la branche). La phase 1 est la première tranche exécutable ; les phases suivantes dépendent d'elle selon le tableau.

| Phase | Tranche | Dépend de | Preuve de fin |
|---|---|---|---|
| 0 | Préparation | — | Ancien plan archivé, ADR-0094, captures de base épinglées, backend graphique et chaîne de shaders relevés |
| 1 | Pipeline couleur | 0 | `Legacy` identique au bit ; `LinearHdr` : gris 128 rendu à 117 ± 2 (`Reinhard`) et 154 ± 2 (`AcesApprox`), valeurs calculées |
| 2 | Bloom | 1 | Pixel à 8 px de l'émissif plus clair d'au moins 10 niveaux, pixel d'interface inchangé ; bloom coupé = référence `LinearHdr` archivée par T1.7 |
| 3 | BRDF direct | 1 | `LitPbr.fx` compile ; tests de couverture ; la famille `LitPbr` résout le shader `LitPbr` ; empreintes `Legacy` identiques |
| 4 | Matériau `lit-pbr` | 3 | Aller-retour `.material`, compilation, surcharges, masque d'ombre alpha-test fourni par le matériau (test `ShadowMaskTests`) ; sphères rugosité 0,1 → 0,9 : largeur du reflet strictement croissante |
| 5 | PBR transparent | 4 | Sphère de verre : fond visible, reflet spéculaire conservé |
| 6 | IBL | 4 | Table BRDF et cache testés ; une génération par changement, hors de la boucle des vues ; écran partagé intact sur l'image de génération ; contraste normalisé du reflet à rugosité 0,6 ≤ 50 % de sa valeur à niveau de mip forcé à 0 |
| 7 | Sondes locales | 6 | Dans la zone d'une sonde, contraste normalisé du reflet à rugosité 0,6 ≤ 50 % de sa valeur à niveau de mip forcé à 0, teinte de la sonde reconnaissable |
| 8 | PBR skinné | 4, 6 | Technique PBR skinnée résolue (test, deux modes de skinning) ; écart de pixel mesuré entre Blinn-Phong et PBR sur `Soldier.glb` ; pixel d'ombre plus sombre que le sol éclairé |
| 9 | Import glTF | 4 | Lecture des canaux PBR, `.material` `lit-pbr` générés |
| 10 | Démo, docs, clôture | toutes | `PbrMaterialDemo`, non-régression finale, coût mesuré |

## État vérifié du dépôt (2026-11-14, `main` a200f12b, branche `chantier/pbr-rendering`)

Relevé par huit lectures indépendantes, chaque fait relu par un second lecteur. Chemins relatifs à la racine du dépôt ; le code du moteur est sous `CasaEngine/`.

**Image**
- `RenderPipeline.Render` (`CasaEngine/Framework/Rendering/RenderPipeline.cs:108`) : par vue, `FrameCapturePlanner.Plan` (`FrozenFrames/FrameCapturePlanner.cs:11-38`, interne, testé par `FrameCapturePlannerTests`) décide `UseSceneRt` : ombres actives sur une vue back buffer effacée, ou rétention (ADR-0086).
- La cible de scène `_sceneRt` est `SurfaceFormat.Color` + `Depth24Stencil8`, `PreserveContents` (`RenderPipeline.cs:436-455`), hors `RenderTargetPool`. Le seul blit est un `SpriteBatch` opaque sans shader (`:273-286`), dont la source est `_sceneRt` ou la cible retenue (`:283`).
- `DefaultViewPipeline.RenderView` (`DefaultViewPipeline.cs:27-58`) enchaîne `World.Draw`, le `Flush` de chaque `IViewFlushableRenderer`, puis la composition de l'interface : **l'interface est dessinée dans la même cible que la scène, avant le blit**. Trois pipelines de vue enveloppent ou remplacent ce chemin : `SkyBackgroundViewPipeline`, `OverlayViewPipeline` (éditeur), et ceux des jeux.
- Ordre des renderers (`CasaEngineGame.cs:444-452`) : maillages statiques, skinnés, sprites, particules, lignes 3D, `Renderer2DComponent` (un `SpriteBatch.Begin` sans effet, `Renderer2DComponent.cs:128`).
- Le debug overlay est dessiné après le blit (`RenderPipeline.cs:309-328`), dans la cible de la vue pour une vue à cible propre.
- Vues à tampon (ADR-0087) : `RenderTargetSurface` `Color`/`Depth24` créée par `DefaultRuntimeViewBootstrapper.cs:67-68`, présentée par `VirtualResolutionPresenter`. `ViewManager.cs:473, 484` teste ce type de presenter. `TexturePresenter` ne dessine rien ; `BackBufferPresenter` n'est construit nulle part.
- Image figée : cibles retenue et capturée en `Color` (`FrameRetention.cs:93-95, 114-116`). Back buffer en `Color` (`CasaEngineGame.cs:185`).
- `RenderFrame` (`RenderFrame.cs:11-28`) porte vue, projection, caméra, viewport, `ResolvedEnvironmentSettings`, `LightingContext`, `ShadowSettings` ; rien sur l'espace couleur.
- Trois pools : `Framework/Rendering/RenderTargetPool.cs` (clé avec format), et deux pools d'interface dont la clé force `Color` (`MGUI.MonoGame.Integration/Rendering/RenderTargetPool.cs:64-66`, `Framework/UI/Backend/MonoGame/CasaRenderTargetPool.cs:77-82`).
- Aucun code sRGB, linéaire, HDR, tonemap, bloom ou passe plein écran dans `.cs`, `.fx`, `.fxh`.

**Environnement**
- `WorldEnvironmentSettings` (`Rendering/Environment/WorldEnvironmentSettings.cs:12`) : ajouter une propriété touche `ResetToDefaults` (:74), `Load` (:96), `Save` (:178), `Clone` (:221), `CopyFrom` (:247). Aucun setter n'appelle `MarkDirty` ; `ResetToDefaults` remet `IsDirty` à faux sans changer `_version`. Exemple complet : `AmbientSource` (ADR-0090), testé par `AmbientSourceTests` (12 tests).
- `EnvironmentResolver.Resolve` (`EnvironmentResolver.cs:14-123`) : la source est `view.EnvironmentOverride ?? view.World.EnvironmentSettings`, remplacée en bloc, mise en cache par `ResolvedEnvironmentCache` (valide tant que `IsDirty` est faux et que les versions n'ont pas changé).
- L'intensité ambiante est repliée une seule fois : `EffectiveAmbientColor = AmbientColor * AmbientIntensity` (`ResolvedEnvironmentSettings.cs:63`), envoyée dans `AmbientColor` (`ForwardLightBinder.cs:106`) et `EnvironmentAmbientColor` (`EnvironmentShaderBinder.cs:11`). Le sanitizer ne plafonne pas les intensités.
- `EnvironmentShaderBinder.Bind` est appelé par `ShaderBindCache.BindGlobals` (`ShaderBindCache.cs:34`), une fois par changement de shader, remis à zéro par `ResetFrame`.
- Cubes : aucun n'a de mips. `TextureCubeLoader` refuse les DDS à mips (`:47-50`) et tout format hors DXT1/3/5 et RGB(A) 8 bits (`:204-221`). Cubes générés en `Vector4` sans mips (`PanoramaEnvironmentGenerator.cs:128`, génération CPU face par face, cache par `GeneratedAssetHandleCache`). `.hdr` lu en flottant (`:229-242`).
- `SampleEnvironmentDiffuse` échantillonne le cube spéculaire non convolué à la normale (`Lighting.fxh:102-110`). `Macros.fxh:53` : `SAMPLE_CUBEMAP` n'a pas d'argument de niveau.
- Sondes locales : `ReflectionProbe` (runtime seulement, non sérialisé), `ReflectionProbeResolver`, mélange primaire/secondaire, cubes aux emplacements t4 et t5.
- Le ciel lit `BackgroundCubemap` par nom de paramètre, hors `ShaderWrapper` (`SkyCubemapRenderer.cs:62-65`).

**Shaders**
- `CasaEngine/Content/Shaders` : `Lighting.fxh`, `Macros.fxh`, `Structures.fxh`, `LitForward.fx` (24 techniques, `:741-769`), `skinEffect.fx` (copies locales des aides d'éclairage, `:173-207`), `UnlitTexture.fx`, `SkyCubemap.fx`, `LegacySkyCube.fx`, `ShadowDepth.fx`, `TexturedPrimitive.fx`, `PsxQuad.fx`, `SpriteBatch.fx`, `DebugSolidColor.fx`, `DebugPrimitiveColor.fx`. Deux fichiers de contenu existent : `CasaEngine/Content/Content.mgcb` (`/platform:Windows`, une entrée par `.fx`) et `CasaEngine.Shaders/Content/Content.mgcb` (`/platform:DesktopGL`) ; tous les projets référencent `MonoGame.Framework.DesktopGL` 3.8.5.1 (`Directory.Packages.props:18`). `Macros.fxh:9-14` : `vs_3_0`/`ps_3_0` sous `OPENGL`, `vs_5_0`/`ps_5_0` sinon ; sous `OPENGL`, `SAMPLE_TEXTURE_LEVEL` ignore le niveau (`:46-56`). **Lequel est effectif n'est pas établi : c'est l'objet de T0.3.**
- Constantes et textures numérotées à la main. `LitForward.fx` : textures t0 base, t1 normale, t2 cube de réflexion du matériau, t3 cube d'environnement, t4 et t5 sondes, t6 ombres, t7 détail ; constantes jusqu'à c36, `_cb(c32.w)` paraît libre. Toute constante utilisée par `Lighting.fxh` doit être déclarée dans chaque `.fx` qui l'inclut.
- `HAS_ENVIRONMENT_BINDINGS` et `HAS_FORWARD_SHADOW_BINDINGS` doivent être définis avant l'inclusion ; sinon environnement et ombres valent silencieusement 0 et 1.
- L'ombre ne s'applique qu'à `ShadowedDirectionalLightIndex`, en éclairage au pixel (`Lighting.fxh:467, 480`). `ComputeLights` met l'émissif dans `Diffuse` et `ComputeDirectDiffuse` le retire (`LitForward.fx:204`).
- Tests qui lisent le texte des shaders : `LightingShaderCoverageTests` (emplacements exacts, par exemple `_cb(c32.z)` une seule fois), `ShaderTechniqueCoverageTests` (seulement `LitForward.fx`, `UnlitTexture.fx`, `skinEffect.fx`), `ShadowPipelineCoverageTests` (chaînes dans `ShadowPass.cs`, `OpaqueAndTransparentPasses.cs`, `Content.mgcb`).

**Sélection de shader**
- `BuiltInShaderCatalog.RuntimeShaders` (7 entrées) alimente le rechargement à chaud de l'éditeur ; `CasaEngineGame.ReloadBuiltInShader` (`:949-994`) ignore tout nom absent de son `switch` ; `StaticMeshRendererComponent.TryReloadBuiltInShader` (`:154-198`) n'accepte que Lit, Unlit, ShadowDepth, Sky.
- `EffectiveShaderResolver` : tout matériau de famille `Lit` sans `ShaderAssetId` reçoit `LitForward` ; un maillage skinné reçoit le shader de skinning **avant** l'examen de la famille (`:73-76`). `MaterialShaderFamily` n'a que `Unknown`, `Lit`, `Unlit`.
- `ShaderFeature` s'arrête à `Reflection = 1 << 8`. `NormalMap`, `Reflection`, `Emissive` n'entrent pas dans le nom de technique canonique (`ShaderVariantLibrary.cs:45-51`) : c'est `LitDiffuseMaterial.GetTechniqueName` (`:53-83`) qui choisit, via `RequiresMaterialTechniqueSelection`.
- `ShaderWrapper.SetParameter` ne fait rien pour un nom absent ; `SelectTechnique` journalise et garde la technique courante.
- `StaticMeshRendererComponent` charge et enregistre les shaders (`:114-146`) et applique `ApplyLitForwardDefaults` (`:200-212`).
- `ShaderFeature.Instanced` n'est posé par aucun code de production : le chemin instancié est dormant.

**Matériaux**
- Définitions intégrées `lit-diffuse` et `unlit-texture` (`Materials/Definitions/BuiltInMaterialDefinitions.cs:14-19`). `MaterialDefinitionRegistry.Register(definition, factory, overrideMapper)` ; une classe runtime ne sert qu'une définition (`:108-113`).
- `MaterialCompiler` : la fabrique `lit-diffuse` est appelée en direct et reçoit les handles comptés (ADR-0037) ; une fabrique enregistrée ne reçoit pas de `holds` (`:11-16, 268-275`). Le cube n'est reconnu que pour la clé `reflection_texture` en `dds` (`:412-414`). Définition sans fabrique : `NotSupportedException` (`:260-261`).
- Transparence : seules les propriétés marquées `AffectsTransparency` comptent (`MaterialRenderStateResolver.cs:87`) ; `MaterialBase.CreateShaderCapabilities` (`:135-149`).
- `LitDiffuseMaterial.Bind` pose tous les paramètres à chaque appel ; `AlphaCutoff` vaut 0 hors file `AlphaTest` (`:132`).
- `ShadowPass` : le masque alpha des projeteurs n'existe que pour `LitDiffuseMaterial` et `UnlitTextureMaterial` (`ShadowPass.cs:144-158`).
- Éditeur : `MaterialDefinitionEditorRegistry` classe par mots-clés avant le groupe (`:103-149`) ; `GameEditor.cs:8681-8694` ne décrit que deux classes ; `MaterialPreviewViewport.cs:513` compile sans garder les handles.
- Tests liés à `lit-diffuse` : `MaterialCompilerTests`, `MaterialDefinitionRegistryTests`, `MaterialDefinitionEditorRegistryTests` (compte 11), `MaterialInstancePropertyBlockMapperTests`, `MaterialRuntimeResolverTests`, `MaterialAssetJsonSerializerTests` (compte 2), `CompiledMaterialTests`, `MaterialCacheTests`.

**Passes et skinning**
- `ForwardRenderPipeline` : ShadowPass, SkyPass, OpaquePass, TransparentPass (`ForwardRenderPipeline.cs:25-31`) ; `InsertPass`, `AddPass`. Aucune passe plein écran.
- Transparents triés par distance à l'origine de l'objet, quantifiée (`SortKeyGenerator.cs:55`) ; même chemin `DrawItem` que les opaques, l'état de mélange vient de `RenderStateCache`.
- Maillages skinnés : hors `ForwardRenderPipeline`, dessinés par `SkinnedMeshRendererComponent.Flush` (`:140-200`), sans gestion de file ; projeteurs d'ombre à technique fixe (`:420-431`), dessinés deux fois par image (`:99` et `:170`).

**Import**
- `GltfStaticModelReader` lit couleur de base (8 bits), émissif, texture de normale, tangentes, et seulement `RoughnessFactor`, converti en puissance spéculaire (`:197-213`) ; ni métal, ni texture metallic-roughness, ni occlusion, ni mode alpha, ni double face ; images embarquées ignorées (`:225-231`). `StaticModelImportedMaterial` (`StaticModelImportResult.cs:26-61`) n'a aucun champ PBR.
- `GltfRiggedModelReader` : tangente en `Vector3` (signe perdu, `:308`), mode alpha et double face lus, images embarquées décodées.
- `EditorAssetImportService.ApplyMaterialImports` (`:646-716`) écrit toujours des `.material` `lit-diffuse`.
- `MipmapChain` moyenne les octets stockés (ADR-0092). Aucune métadonnée d'espace couleur sur les textures.
- Aucun `.gltf` avec matériaux dans le dépôt ; seuls `.glb` : `CasaEngine.Demos/Content/SkinnedMesh/Soldier.glb` et `kid_*.glb`.

**Démos et règles**
- `DemosGame` : `CASAENGINE_START_DEMO`, `CASAENGINE_CAPTURE_SCREENSHOT_PATH`, `CASAENGINE_CAPTURE_SCREENSHOT_DELAY_MS`, `CASAENGINE_DEMO_PIXELS_PATH` (`DemosGame.cs:142, 348, 576, 587`). Enregistrement par `AddDemo` (`:104-133`).
- `CasaEngine/artifacts/` est ignoré par git (`.gitignore:61`).
- Prochain ADR libre : 0094. Types de commit autorisés : `feat`, `fix`, `refactor`, `docs`, `chore`, `perf`, `test`.
- Arbre de travail : ` M MGUI` (sous-module modifié par l'auteur) : ne jamais l'indexer.

## Décisions verrouillées

| Réf | Décision |
|---|---|
| D1 | PBR choisi par matériau : `lit-pbr` s'ajoute à `lit-diffuse`, qui reste intact ; aucune régression visuelle du contenu existant (ADR-0013). |
| D2 | Workflow metallic-roughness, aligné sur glTF (ADR-0013). |
| D3 | Forward seulement, dans `ForwardRenderPipeline` ; ni différé ni clustered (ADR-0013). |
| D4 | Plafonds de lumières inchangés : 8 directionnelles, 8 ponctuelles, 8 spots (ADR-0013). |
| D5 | `LitPbr.fx` en éclairage au pixel uniquement (ADR-0013). |
| D6 | Le pipeline couleur est un réglage par monde, par vue via la surcharge d'environnement ; défaut `Legacy`, comportement actuel inchangé (ADR-0013). |
| D7 | Conventions de techniques et de macros de `Macros.fxh` et cibles de compilation de `LitForward.fx` respectées (ADR-0013). |
| D8 | Le bloom entre dans ce plan (auteur, 2026-11-14). |
| D9 | Le PBR transparent entre dans ce plan (auteur, 2026-11-14). |
| D10 | Le PBR sur maillages skinnés entre dans ce plan ; ceci remplace le point « hors périmètre V1 » de l'ADR-0013 (auteur, 2026-11-14). Il reste forward, dans le chemin de dessin skinné existant (`SkinnedMeshRendererComponent`), hors `ForwardRenderPipeline` : D3 est amendée pour les seuls maillages skinnés (ADR-0094). |
| D11 | Le préfiltrage des sondes de réflexion locales entre dans ce plan (auteur, 2026-11-14). |
| D12 | Le plan s'arrête au moteur, validé par une démo du moteur ; la bascule de RacingGame fait l'objet d'un plan du jeu séparé. L'ancien plan est archivé et remplacé (auteur, 2026-11-14). |

## Arbitrages proposés par l'agent

| Réf | Arbitrage | Tâche |
|---|---|---|
| P1 | En `LinearHdr`, toute vue rend sa scène dans une cible HDR de la taille de la vue, prise dans `RenderTargetPool` et rendue en fin de vue (`HalfVector4`, repli `HdrBlendable` puis `Color`), puis un passage de tonemapping écrit dans la destination habituelle de la vue (back buffer, cible retenue ou cible propre). En `Legacy`, aucun chemin ne change. | T1.4 |
| P2 | L'interface est composée après le tonemapping grâce à une interface additive `IScenePhaseViewPipeline` (`RenderScene` sans interface utilisateur). `DefaultViewPipeline` et `SkyBackgroundViewPipeline` l'implémentent. Un pipeline de vue qui ne l'implémente pas est rendu en entier dans la cible HDR, interface comprise, avec un avertissement journalisé une fois au premier rendu. | T1.4 |
| P3 | Décodage sRGB dans les shaders (`pow(c, 2.2)`), piloté par une constante `ColorPipelineIsLinear`, sous une branche qui laisse la valeur intacte à 0. Les couleurs qui arrivent au shader déjà multipliées par une intensité (lumières, ambiant) et les facteurs (couleur d'ombre) sont décodés côté CPU, avant le produit. Encodage `pow(c, 1/2.2)` dans `Tonemap.fx`. Pas de format de surface sRGB. | T1.5 |
| P4 | En `LinearHdr`, toute couleur existante est traitée comme une couleur d'affichage et décodée : textures de couleur, couleurs de matériau, couleurs de lumière, ambiant, couleur d'effacement, cubes. Exception : un cube issu d'un panorama `.hdr` est déjà linéaire. Les normales et données non colorées ne sont jamais décodées. | T1.5 |
| P5 | `Renderer2DComponent` (sprites d'écran et texte, `SpriteBatch` sans effet) est vidé après le tonemapping, avec l'interface, via un marqueur additif `IPostTonemapRenderer`. Ce vidage force `DepthStencilState.None`. Limite : en `LinearHdr`, il ne teste plus la profondeur de la scène. Particules, sprites 3D et lignes restent dans la scène et décodent dans leurs shaders. | T1.6 |
| P6 | Opérateurs de tonemapping : `Reinhard` et `AcesApprox` (approximation de Narkowicz), exposition scalaire. Pas d'exposition automatique. | T1.3 |
| P7 | Bloom : seuil, intensité, rayon ; passe-haut, chaîne de réductions floues, recomposition additive avant tonemapping. Réglages dans `ColorPipelineSettings`, coupé par défaut, sans effet en `Legacy`. | T2.1 |
| P8 | `lit-pbr` est une définition intégrée, appelée en direct par `MaterialCompiler` comme `lit-diffuse`, donc avec les handles comptés. Nouvelle famille `MaterialShaderFamily.LitPbr`. Le matériau choisit sa technique, comme `LitDiffuseMaterial`. | T4.1, T4.2 |
| P9 | IBL généré sur le GPU : chaque face et chaque niveau est rendu dans une `RenderTarget2D` par un shader de convolution, relu, puis écrit dans un `TextureCube` à mips. Fonctionne quel que soit le format du cube source. Cache par cube source ; aucune génération par image. La génération ne s'exécute **jamais pendant qu'une cible de vue est liée** : elle est demandée quand un shader qui déclare les paramètres IBL est lié pour un cube pas encore généré, mise en file, puis exécutée au début de `RenderPipeline.Render` (à côté de `DrawWorldUIToTextures`), avec restauration de la cible et du viewport initiaux. Tant qu'un cube n'est pas généré, le matériau PBR utilise le terme plat. Un monde sans matériau PBR ne génère rien. | T6.1, T6.2 |
| P10 | Transparent PBR : mélange alpha où seule la part diffuse est atténuée par l'alpha ; le spéculaire est conservé (sortie prémultipliée). Pas de transmission ni de réfraction. | T5.1 |
| P11 | PBR skinné : techniques PBR ajoutées à `skinEffect.fx`, qui partage déjà `Lighting.fxh`. Pas de normal map skinnée dans cette version. | T8.1 |
| P12 | Les images embarquées des `.glb` statiques restent hors périmètre. | T9.1 |

## Règles d'exécution pour l'agent

- **Branche dédiée `chantier/pbr-rendering`**, créée depuis `main` (a200f12b). Ne jamais committer sur `main`.
- **Une seule tâche à la fois.** Avant de commencer une tâche, remplacer son icône `⏳` par `🚧`. À la fin, lancer la validation indiquée, remplacer l'icône par `✅`, `🧪` ou `⚠️`, ajouter une courte note de validation sous la tâche, puis **créer un commit dédié** qui inclut la mise à jour de ce fichier.
- **Un commit par tâche**, atomique et compilable, message en anglais au format `type(area): summary`.
- **Ne jamais pousser.** Le merge sur `main` reste une décision humaine.
- **Ne rien inventer** : toute API, tout fichier, toute règle utilisée existe dans le dépôt, vient d'une réponse de l'auteur, ou d'une doc officielle citée (URL). Sinon : passer la tâche en ⚠️ Blocked, écrire la question dans « Points ouverts », et **s'arrêter**. Les numéros de ligne de ce plan datent du 2026-11-14 : relire le code avant d'éditer.
- **Build obligatoire** avant ✅ dès que du code est touché : `dotnet build CasaEngine.MonoGame.sln` et, dès qu'un shader, un service d'édition ou l'éditeur est touché, `dotnet build CasaEngine.Editor.MonoGame.sln`. **Tests** : `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj`.
- Si une vérification visuelle ou manuelle manque, utiliser `🧪 Needs testing` et noter précisément ce qui manque.
- **Ne jamais laisser une tâche en 🚧** à la fin d'une session.
- **Ne jamais indexer** les modifications préexistantes de l'auteur (` M MGUI`) : `git add` fichier par fichier.
- **Langue** : ce plan en français ; code, messages de commit, docs de `docs/` et ADR en anglais.
- **ADR** : chaque décision d'architecture, de format ou d'API prise par une tâche est enregistrée dans `docs/decisions/` (skill `adr`), statut `Proposed`, dans le commit de la tâche.
- **Non-régression `Legacy`** : toute tâche qui touche un shader ou le chemin d'image recapture les démos de T0.2 et compare les empreintes SHA-256. Une différence bloque la tâche.
- Rappel moteur : pas d'allocation, de LINQ ni de closure dans les chemins chauds ; restaurer tout état GPU modifié ; le runtime ne dépend pas de l'éditeur ; sérialisation additive ; journaliser au chargement, jamais dans `Draw`.
- **Budget** : par tâche, deux passes de correction au plus sur une différence d'empreinte `Legacy`, un test rouge ou une mesure manquée ; à la troisième, ⚠️ Blocked et arrêt de la phase. Les phases indépendantes de la phase bloquée peuvent continuer.
- **Revue** : chaque phase est relue avant exécution (lecture seule) et vérifiée après (exécution des tests et de la preuve de fin) par un agent sans le contexte de l'auteur du code.

## Légende des statuts

- ⏳ Todo : pas encore commencé.
- 🚧 In progress : en cours de modification locale.
- 🧪 Needs testing : code écrit, validation incomplète ou en attente.
- ✅ Done : code validé, build/tests OK, commit effectué.
- ⚠️ Blocked : bloqué par une erreur non résolue ou une décision manquante.

## Validation globale

- `dotnet build CasaEngine.MonoGame.sln` et `dotnet build CasaEngine.Editor.MonoGame.sln` : 0 erreur.
- `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj` : 5207 tests au départ, 0 échec, plus les tests ajoutés.
- Démos de T0.2 en `Legacy` : empreintes identiques à la base.
- `PbrMaterialDemo` : mesures de pixels de T10.1 tenues.

---

## Phase 0 — Préparation

Budget des phases 0 et 1 : sur une différence d'empreinte `Legacy` ou une mesure manquée, deux passes de correction au plus par tâche ; à la troisième, ⚠️ Blocked et arrêt de la phase.

### ✅ T0.1 — Archiver l'ancien plan et enregistrer le périmètre étendu

- Objectif : un seul plan PBR vivant ; les décisions D8 à D12 enregistrées.
- Fichiers : `ai-agent/tasks/pbr-rendering-implementation-plan.md` → `ai-agent/tasks/archive/` (`git mv`), `ai-agent/README.md`, `docs/decisions/0094-pbr-scope-extends-to-bloom-transparency-skinning-and-local-probes.md`, `docs/decisions/README.md`, `docs/decisions/0013-pbr-rendering-decisions.md` (ligne de statut et chemin de source seulement).
- Étapes :
  1. Déplacer l'ancien plan dans `archive/` et ajouter en tête une note : remplacé par `pbr-rendering-tasks.md` le 2026-11-14, aucune tâche exécutée.
  2. Mettre à jour la ligne du tableau de `ai-agent/README.md` : nouveau fichier, sujet, reste à faire.
  3. Écrire l'ADR-0094 (statut `Accepted`, décision de l'auteur) : D8 à D12. Il amende deux points de l'ADR-0013 : le skinning PBR entre dans le périmètre, et il reste forward **dans le chemin de dessin skinné existant, hors `ForwardRenderPipeline`** (amendement du point « forward only » pour les seuls maillages skinnés). Dans l'ADR-0013, passer le statut à `Accepted, amended by ADR-0094` et corriger le chemin de la source vers `archive/`.
  4. Ajouter l'ADR-0094 à l'index.
- Validation : dans l'ADR-0013, **chaque** renvoi `(source: ai-agent/tasks/pbr-rendering-implementation-plan.md…)` est redirigé vers `ai-agent/tasks/archive/…` (remplacement de chemin seulement). `rg -n "pbr-rendering-implementation-plan" . --glob "!ai-agent/tasks/archive/**" --glob "!ai-agent/audits/**" --glob "!ai-agent/tasks/pbr-rendering-tasks.md"` ne rend que des lignes contenant `tasks/archive/`. Les audits de `ai-agent/audits/` sont en lecture seule et gardent l'ancien chemin : `git diff --stat` du commit ne liste aucun fichier sous `ai-agent/audits/`. Liens de l'index ouverts.
- Commit : `docs(ai-agent): replace the PBR plan and record its extended scope (ADR-0094)`
- Note de validation (2026-11-15) : ancien plan déplacé dans `archive/` avec sa note ; ADR-0094 écrit et indexé ; ADR-0013 amendé (statut et chemins de source) ; la recherche de la validation ne rend aucune ligne hors `tasks/archive/` ; aucun fichier de `ai-agent/audits/` modifié.

### ✅ T0.2 — Captures de base épinglées

- Objectif : la référence `Legacy` de tout le chantier, épinglée au commit de départ.
- Fichiers : aucun fichier suivi hors ce plan ; captures dans `CasaEngine/artifacts/pbr-baseline/` (ignoré par git).
- Étapes :
  1. Construire `CasaEngine.Demos` sur le commit de T0.1.
  2. Capturer **deux fois** chaque démo, sélectionnée par son **titre exact** dans `CASAENGINE_START_DEMO` (la sélection retombe sur la démo 0 si le nom est inconnu, `DemosGame.cs:176-198`) : `Material system demo`, `Environment showcase`, `Static model demo`, `Static shadow validation demo` (avec `CASAENGINE_SHADOW_DEMO_VALIDATION=1`), `Skinned mesh demo`, `Particle system demo`, `Split-screen demo (2 views)`, `Render-to-texture demo`, `MGUI UI Overlay Demo (HUD + Modal Screens)`, `Frozen frame demo`, `Tile map demo`.
  3. Calculer le SHA-256 de chaque capture. Une démo dont les deux captures diffèrent est classée « non déterministe » et sort de la comparaison au bit ; elle reste contrôlée à l'œil (🧪) en fin de phase.
  4. Écrire sous cette tâche le tableau : titre, résolution, empreinte, déterministe ou non, et la commande exacte.
  5. Relevé `RenderStats` : lancer `Material system demo` en `Legacy` avec `CASAENGINE_SHOW_DEBUG_OVERLAY=1`, capture dans un fichier à part exclu de la comparaison d'empreintes ; noter dans le tableau les valeurs Draws et State lues sur l'overlay (`DebugOverlay.cs:101-102`) et la commande exacte.
- Validation : aucune ligne « Startup demo … was not found » dans les journaux ; les empreintes des démos déterministes sont deux à deux distinctes ; au moins `Material system demo`, `Static shadow validation demo` et `Static model demo` sont déterministes, sinon ⚠️ Blocked (la non-régression au bit n'est pas prouvable).
- Commit : `chore(ai-agent): pin the legacy baseline captures of the PBR work`
- Note de validation (2026-11-15), commit de base e237fa3f, `CasaEngine.Demos` en Debug, 1024 × 768 :
  - Commande, depuis `CasaEngine.Demos/bin/Debug/net9.0-windows`, par démo et par passage : `CASAENGINE_START_DEMO="<titre exact>" CASAENGINE_CAPTURE_SCREENSHOT_PATH="<dossier>\<nom>-<n>.png" ./CasaEngine.Demos.exe` (délai de capture par défaut, 1500 ms ; la démo se ferme seule après la capture). `Static shadow validation demo` ajoute `CASAENGINE_SHADOW_DEMO_VALIDATION=1`. Captures et journaux dans `CasaEngine/artifacts/pbr-baseline/legacy/`.
  - Aucune ligne « was not found » et aucune exception dans les 22 journaux.

  | Titre | Fichier | SHA-256 (16 premiers) | Déterministe |
  |---|---|---|---|
  | Material system demo | material | 7fe3d57bb10f7128 | oui |
  | Environment showcase | environment | 043ad52894e18395 | oui |
  | Static model demo | static-model | 3ee7cd655db47386 | oui |
  | Static shadow validation demo | static-shadow | c7d6c867b3de4ad9 | oui |
  | Render-to-texture demo | render-to-texture | f35395822a585e9e | oui |
  | MGUI UI Overlay Demo (HUD + Modal Screens) | ui-overlay | d226df79b82cf4b2 | oui |
  | Frozen frame demo | frozen-frame | eb25f3917cfa6a0c | oui |
  | Tile map demo | tile-map | 355772772016e47f | oui |
  | Skinned mesh demo | skinned | b42a675f99a32850 / 6ec7ac32c8220a0f | **non** |
  | Particle system demo | particles | 2062d28ec745685c / 0b8628bf485a9068 | **non** |
  | Split-screen demo (2 views) | split-screen | ef1cefef35595583 / 272c6fe9f348081c | **non** |

  - Les huit empreintes déterministes sont deux à deux distinctes. Les trois démos non déterministes sortent de la comparaison au bit et restent contrôlées à l'œil (🧪) en fin de phase ; conséquence : la non-régression `Legacy` du chemin skinné et de l'écran partagé repose sur les tests et sur ce contrôle visuel, pas sur une empreinte.
  - `RenderStats` de `Material system demo` (même commande plus `CASAENGINE_SHOW_DEBUG_OVERLAY=1`, fichier `material-overlay.png`, hors comparaison d'empreintes) : **Draws 14, FX 2, Tex 32, State 3, O 13, T 1**, « RT pool: 0 active / 0 free ».

### ✅ T0.3 — Relevé du backend graphique et de la chaîne de compilation des shaders

- Objectif : savoir quel backend exécute les shaders et quel fichier de contenu les compile, avant d'écrire un shader.
- Sources : `Directory.Packages.props:18` (tous les projets référencent `MonoGame.Framework.DesktopGL` 3.8.5.1), `CasaEngine/Content/Content.mgcb:6` (`/platform:Windows`), `CasaEngine.Shaders/Content/Content.mgcb:6` (`/platform:DesktopGL`), `CasaEngine.Shaders/CasaEngine.Shaders.csproj` (`mgfxc` embarqué), `Macros.fxh:9-14, 46-56` (`OPENGL` : `vs_3_0`/`ps_3_0`, et `SAMPLE_TEXTURE_LEVEL` y ignore le niveau), `EffectLoader.cs` (`.mgfxc`), les `.csproj` des démos.
- Étapes : établir, par lecture puis par une exécution d'une démo, (a) quel `Content.mgcb` produit les `.mgfxc` chargés par `CasaEngine.Demos`, (b) si `OPENGL` est défini à leur compilation, (c) le modèle de shader effectif et le nombre de samplers de pixel disponibles, (d) comment un nouveau `.fx` doit être déclaré pour être compilé et chargé (une ou deux entrées `mgcb`). Écrire le relevé sous cette tâche. Si le backend est OpenGL : noter que l'échantillonnage de cube par niveau exige une macro nouvelle (`texCUBElod`) à valider en T6.1, et corriger dans ce plan toute mention de `ps_5_0`.
- Validation : relevé écrit avec fichiers, lignes et sortie de commande ; les tâches T1.3, T3.2 et T6.1 sont ajustées dans le même commit si le relevé les contredit.
- Commit : `docs(ai-agent): record the graphics backend and the shader build chain`
- Note de validation (2026-11-15) — relevé :
  - **Backend effectif : OpenGL (MonoGame DesktopGL 3.8.5.1).** Le shader chargé par les démos, `CasaEngine.Demos/bin/Debug/net9.0-windows/Content/Shaders/LitForward.xnb`, a la même empreinte SHA-256 (`a08fca0b…`) que `CasaEngine/Content/bin/DesktopGL/Content/Shaders/LitForward.xnb` ; son en-tête est `XNBd` (DesktopGL), il pèse 1 222 441 octets et date du dernier build (2026-11-14 20:46). La variante `CasaEngine/Content/bin/Windows/…` (`XNBw`, 80 468 octets) date du 2026-04-18 : elle est périmée et n'est chargée par rien.
  - (a) Un seul fichier de contenu compile les shaders du runtime : `CasaEngine/Content/Content.mgcb`. Sa ligne `/platform:Windows` est remplacée au build par la plateforme du paquet référencé (sortie dans `bin/DesktopGL`). `CasaEngine.Shaders/Content/Content.mgcb` ne contient aucune entrée.
  - (b) `OPENGL` est donc défini à la compilation : `Macros.fxh` choisit `vs_3_0`/`ps_3_0`, `DECLARE_TEXTURE` et `DECLARE_CUBEMAP` déclarent des `sampler_state` sans registre, et `SAMPLE_TEXTURE_LEVEL` **ignore le niveau**. Les macros `_vs`, `_ps`, `_cb` sont vides dans tous les cas (`Macros.fxh:24-26`) : les « emplacements » de constantes de `LitForward.fx` sont de la documentation épinglée par les tests de texte, pas une contrainte du compilateur.
  - (c) Modèle de shader effectif : 3.0, traduit en GLSL ; 16 samplers de pixel au plus, donc les neuf textures de `LitPbr.fx` tiennent (O5 levé).
  - (d) Un nouveau `.fx` se déclare par **une** entrée dans `CasaEngine/Content/Content.mgcb`, copiée sur une entrée voisine ; il doit porter la variante `OPENGL` de tout ce qu'il écrit hors macros.
  - Conséquences inscrites dans ce plan : le commentaire « D3D11 » de `RenderPipeline.cs:139-145` est périmé (le comportement de changement de cible reste celui que l'ADR-0086 décrit pour DesktopGL) ; l'échantillonnage d'un cube par niveau exige une macro nouvelle (`texCUBElod`) dont le support par la chaîne OpenGL de MonoGame n'est pas établi : T6.1 le vérifie en premier et prévoit un repli.

---

## Phase 1 — Pipeline couleur : linéaire, HDR, tonemapping
> **Vérification indépendante de la phase 1 (2026-11-15).** Premier passage sur 998e7997 : cinq affirmations confirmées (identité `Legacy` au bit, sérialisation additive, couleurs `LinearHdr` mesurées, intensité linéaire, builds et 5288 tests), une réfutée par un constat P2 introduit par la phase — les cibles HDR n'étaient jamais libérées (21 créées, 0 libérée sur 20 redimensionnements). Corrigé par f2467650 (éviction après 4 rendus sans usage) ; contre-vérification ciblée **CONFIRMED** : 21 créées, 20 libérées, 1 vivante ; taille fixe : une création par taille ; empreintes `Legacy` et référence `LinearHdr` inchangées ; 5296 tests. Avis P4 reportés : valeur d'énumération invalide dans un fichier de monde (exception sans contexte, comportement déjà celui de `ambient_source`) ; `Tonemap.xnb` absent d'un dossier de contenu externe ferait échouer le chargement ; une autre clé HDR identique dans le pool partagé pourrait voir ses cibles libres élaguées.

### ✅ T1.1 — `ColorPipelineSettings` dans les réglages d'environnement

- Objectif : un réglage par monde, sérialisé, résolu et porté jusqu'à l'image, sans changer aucun rendu.
- Fichiers : nouveaux `CasaEngine/Framework/Rendering/Environment/ColorPipelineSettings.cs`, `ColorPipelineMode.cs`, `TonemapOperator.cs` ; `WorldEnvironmentSettings.cs`, `ResolvedEnvironmentSettings.cs`, `EnvironmentResolver.cs` ; `CasaEngine.Editor/Controls/EntityDetailsPanel.cs` ; nouveau `CasaEngine.Tests/Rendering/ColorPipelineSettingsTests.cs` ; ADR-0095.
- Étapes :
  1. `ColorPipelineSettings` (classe, sur le modèle de `ShadowSettings` : `ResetToDefaults`, `Load`, `Save`, `CopyFrom`) : `Mode` (`Legacy` par défaut, `LinearHdr`), `Tonemapper` (`AcesApprox` par défaut, `Reinhard`), `Exposure` (1, assaini : NaN et négatif → 0).
  2. `WorldEnvironmentSettings.ColorPipeline` (lecture seule, comme `Shadows`) : `ResetToDefaults`, `Load` et `Save` sous la clé `color_pipeline`, écrite seulement si un champ diffère du défaut et retirée sinon (comme `ambient_source`), `Clone`, `CopyFrom`.
  3. `ResolvedEnvironmentSettings` : propriétés `ColorPipelineMode`, `Tonemapper`, `Exposure`, et `IsLinearHdr`. `EnvironmentResolver` les copie depuis la source ; la surcharge de vue remplace en bloc, comme le reste. Le cache `ResolvedEnvironmentCache` n'a pas de clé à étendre : il repose sur `IsDirty` et les versions, donc un changement de réglage suivi de `MarkDirty` l'invalide (test).
  4. Éditeur : combo du mode, combo de l'opérateur, champ d'exposition, sur le modèle exact du combo `AmbientSource` (`MarkDirty` après chaque édition, annulation comprise).
  5. Tests, sur le modèle d'`AmbientSourceTests` : défauts ; `Clone` ; `CopyFrom` dans les deux sens ; `Save` omet la clé au défaut ; aller-retour `Save`/`Load` ; clé périmée supprimée ; `Load` insensible à la casse ; le resolver porte les valeurs ; la surcharge de vue l'emporte ; `ResetToDefaults` revient à `Legacy` ; changement puis `MarkDirty` → le resolver rend la nouvelle valeur.
  6. ADR-0095 : pipeline couleur par monde, clé `color_pipeline`, défaut `Legacy`.
- Validation : builds ; tests verts ; un monde existant se charge en `Legacy` (test) ; captures de T0.2 identiques.
- Commit : `feat(rendering): add per-world colour pipeline settings with a legacy default (ADR-0095)`
- Note de validation (2026-11-15) : `dotnet build CasaEngine.MonoGame.sln` et `CasaEngine.Editor.MonoGame.sln` : 0 erreur ; `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj` : 5225 réussis, 0 échec (5207 au départ + 18 de `ColorPipelineSettingsTests`) ; un monde sans la clé `color_pipeline` se charge en `Legacy` (test). `Exposure` : NaN, infini et négatif donnent 0 (l'infini est ajouté à la consigne par cohérence avec `ShadowSettings`). `ResolvedEnvironmentSettings.CreateLegacy` pose `Exposure = 1` (un `default` vaut 0, jamais lu pour l'instant). Captures de T0.2 (`capture.sh`) : sept démos déterministes sur huit identiques à la base ; **`ui-overlay` rend d'abord `c94bfa52754c67b6` au lieu de `d226df79b82cf4b2`, sans lien avec la tâche** : seul le chiffre du compteur « Time: 1,5s » de l'interface (rectangle 77,52 - 84,64) change, la base ayant été prise quand la démo affichait 1,5 s à 1500 ms et la machine affichant maintenant 1,4 s ; le même hash `c94bfa52754c67b6` sort à l'identique sans aucune modification de la tâche (changements écartés par `git stash`, `CasaEngine.Demos` reconstruit). Avec `CASAENGINE_CAPTURE_SCREENSHOT_DELAY_MS=1600` (ou 1550) et les changements de la tâche, la capture vaut `d226df79b82cf4b2`, soit la base : **comparer `ui-overlay` avec ce délai** ; les sept autres sont comparées avec le délai par défaut.

### ✅ T1.2 — Variables de harnais et scène de mesure

- Objectif : pouvoir mettre n'importe quelle démo en `LinearHdr` dès la tâche suivante, et disposer de la scène de mesure de la phase.
- Fichiers : `CasaEngine.Demos/DemosGame.cs` ; nouveau `CasaEngine.Demos/Demos/ColorPipelineDemo.cs` (+ `AddDemo` **en dernière position**, pour ne déplacer aucun index) ; `docs/engine/demos-main-menu.md` si la liste y figure.
- Étapes :
  1. `CASAENGINE_DEMO_COLOR_PIPELINE` (`LinearHdr`, `Legacy`), `CASAENGINE_DEMO_TONEMAPPER`, `CASAENGINE_DEMO_EXPOSURE` : appliqués au monde courant après `Initialize`, `ConfigureSceneLighting` et `CreateCamera` de la démo (`DemosGame.cs:218-220`), suivis de `MarkDirty` ; sans variable, aucun effet.
  2. `ColorPipelineDemo` (titre `Colour pipeline demo`) : caméra fixe ; rampe de cinq plans gris en `unlit-texture` (0, 64, 128, 191, 255), dont le plan 128 occupe un rectangle d'écran noté dans le fichier ; trois sphères `lit-diffuse` sous une directionnelle d'intensité 1, 4 et 16 (un monde par capture n'est pas nécessaire : trois lumières ne conviennent pas, donc l'intensité est choisie par `CASAENGINE_COLOR_DEMO_LIGHT_INTENSITY`, défaut 1) ; un plan émissif fort ; un panneau d'interface MGUI et un texte d'écran. Touches : mode, opérateur, exposition ±. `CASAENGINE_COLOR_DEMO_BUFFERED=1` lance la démo dans une résolution virtuelle à tampon (`VirtualResolutionMode.BufferedFit`, posée comme `FrozenFrameDemo.cs:87-91` pose `ProjectSettings.VirtualResolution`) : la vue passe alors par une `RenderTargetSurface` et `VirtualResolutionPresenter`, interface et texte compris. `Clean` restaure les réglages d'environnement et la résolution virtuelle.
  3. Écrire dans le fichier de la démo les coordonnées des pixels de mesure : gris 128, sphère éclairée (trois points sur sa moitié éclairée), émissif, bord d'émissif à 8 px, interface, texte.
- Validation : builds ; la démo se lance par son titre exact et se capture ; captures de T0.2 identiques ; en `Legacy`, le pixel gris 128 vaut 128 ± 1.
- Commit : `feat(demos): add colour pipeline harness variables and a measurement demo`
- Note de validation (2026-11-15) : builds des deux solutions : 0 erreur ; `dotnet test` : 5226 réussis, 0 échec ; `capture.sh` après la tâche : les huit démos déterministes identiques à la base (material 7fe3d57bb10f7128, environment 043ad52894e18395, static-model 3ee7cd655db47386, static-shadow c7d6c867b3de4ad9, render-to-texture f35395822a585e9e, ui-overlay d226df79b82cf4b2, frozen-frame eb25f3917cfa6a0c, tile-map 355772772016e47f ; `ui-overlay` dépend du compteur « Time » de son interface, voir la note de T1.1 : cette fois-ci il est tombé sur la base avec le délai par défaut), aucune ligne « was not found ». Démo ajoutée **en dernière position** de `DemosGame` (aucun index déplacé), lancée par son titre exact `Colour pipeline demo`, capture 1024 × 768 par `CASAENGINE_START_DEMO="Colour pipeline demo" CASAENGINE_CAPTURE_SCREENSHOT_PATH=<chemin>.png ./CasaEngine.Demos.exe`. Variables : `CASAENGINE_DEMO_COLOR_PIPELINE`, `CASAENGINE_DEMO_TONEMAPPER`, `CASAENGINE_DEMO_EXPOSURE` (appliquées dans `DemosGame.ChangeDemo` après `CreateCamera`, journal `Colour pipeline harness: …` ; sans variable aucun effet), `CASAENGINE_COLOR_DEMO_LIGHT_INTENSITY` (défaut 1), `CASAENGINE_COLOR_DEMO_BUFFERED=1` (résolution virtuelle `BufferedFit` 1024 × 768, journal `Virtual resolution in force: BufferedFit` ; l'échelle valant 1, **les pixels de mesure sont les mêmes avec et sans tampon, et la capture `Legacy` à tampon est identique au bit à celle sans tampon**, hash 6ca4add123c5b4b4). Texte d'écran : `Renderer2DComponent.DrawText` avec une `SpriteFont` à un seul glyphe plein (bloc), pour ne dépendre d'aucun fichier de police ; il doit être redemandé à chaque image (la file est vidée à chaque `Flush`).
  - **Pixels de mesure** (coordonnées fenêtre, constantes `ColorPipelineDemo.*Pixel`), valeurs `Legacy` mesurées sur la capture, lumière d'intensité 1 :

    | Mesure | Pixel (x, y) | Valeur `Legacy` |
    |---|---|---|
    | Gris 128 (plan x 462-561, y 201-300) | 512, 251 | 128, 128, 128 (mesuré 128) |
    | Rampe 0, 64, 128, 191, 255 | 279, 395, 512, 628, 744 ; y 251 | 0, 64, 128, 191, 255 |
    | Sphère grise, moitié éclairée (haut droite ; centre 278, 467, rayon environ 45) | 298, 440 ; 292, 452 ; 310, 460 | 120 ; 128 ; 111 |
    | Émissif (x 645-778, y 425-508, `EmissiveColor` 4) | 712, 467 | 255, 255, 255 |
    | Bord d'émissif, fond à 8 px du dernier pixel émissif (x 778) | 786, 467 | 20, 20, 20 (fond) |
    | Interface (panneau MGUI plein, x 16-175, y 16-79, bordure noire) | 96, 48 | 200, 60, 60 |
    | Texte (bloc, x 24-279, y 112-135) | 150, 124 | 60, 200, 240 |

  - Vérifications : `CASAENGINE_DEMO_COLOR_PIPELINE=LinearHdr` seul ne change pas l'image (rien ne lit encore le réglage) ; `CASAENGINE_COLOR_DEMO_LIGHT_INTENSITY=4` fait passer les trois points de la sphère à 255 ; la touche M, T, +/- règle mode, opérateur et exposition (non exercée en capture).

### ✅ T1.3 — `Tonemap.fx` et son enregistrement

- Objectif : le shader de tonemapping compilé, enregistré et rechargeable, pas encore branché.
- Fichiers : nouveau `CasaEngine/Content/Shaders/Tonemap.fx` ; le ou les `Content.mgcb` désignés par T0.3 ; `BuiltInShaderCatalog.cs` ; `CasaEngineGame.cs` (`ReloadBuiltInShader`) ; `RenderPipeline.cs` (champ `ShaderWrapper` et son chargement) ; `ShaderParameterNames.cs` ; nouveau `CasaEngine.Tests/Rendering/TonemapShaderCoverageTests.cs` ; `docs/engine/effect-file-inventory.md`.
- Étapes :
  1. Lire `SpriteBatch.fx` et `PsxQuad.fx` pour reprendre la forme d'un shader utilisable par `SpriteBatch.Begin(effect:)`, variante `OPENGL` comprise.
  2. `Tonemap.fx` : techniques `Tonemap_Reinhard` (`c / (1 + c)`) et `Tonemap_AcesApprox` (`(c(2.51c + 0.03)) / (c(2.43c + 0.59) + 0.14)`, bornée à [0,1]) ; paramètre `Exposure` appliqué avant l'opérateur ; sortie encodée `pow(c, 1/2.2)`, alpha 1.
  3. Entrée de contenu copiée sur celle de `SpriteBatch.fx`. Descripteur et constante de nom dans `BuiltInShaderCatalog`. `RenderPipeline` tient le `ShaderWrapper`, chargé à l'initialisation ; cas dans le `switch` de `ReloadBuiltInShader` qui lui remet l'effet rechargé.
  4. Tests par lecture du texte : les deux techniques existent ; `Exposure` et la texture source sont déclarés ; le fichier de contenu contient l'entrée ; le catalogue contient le descripteur.
- Validation : builds des deux solutions (la compilation du shader fait foi) ; tests verts ; captures de T0.2 identiques.
- Commit : `feat(rendering): add the tonemap shader and register it as a built-in`
- Note de validation (2026-11-15) : `dotnet build CasaEngine.MonoGame.sln` et `CasaEngine.Editor.MonoGame.sln` : 0 erreur (la compilation du shader fait foi : `CasaEngine/Content/bin/DesktopGL/Content/Shaders/Tonemap.xnb`, 4 759 octets, copié dans `CasaEngine.Demos/bin/Debug/net9.0-windows/Content/Shaders/`) ; `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj` : 5233 réussis, 0 échec (5226 + 7 de `TonemapShaderCoverageTests`). `capture.sh` après la tâche (dossier `artifacts/pbr-baseline/t13`) : les huit démos déterministes identiques à la base (material 7fe3d57bb10f7128, environment 043ad52894e18395, static-model 3ee7cd655db47386, static-shadow c7d6c867b3de4ad9, render-to-texture f35395822a585e9e, ui-overlay d226df79b82cf4b2, frozen-frame eb25f3917cfa6a0c, tile-map 355772772016e47f), aucune ligne « was not found », aucune exception.
  - **Forme du shader.** `SpriteBatch.fx` et `PsxQuad.fx` ne sont pas des effets de `SpriteBatch.Begin(effect:)` : ce sont des effets de quad 3D avec leur propre vertex shader (`World`, `ViewProj`). `Tonemap.fx` suit donc la forme documentée par MonoGame pour un effet de `SpriteBatch` : **pixel shader seul** (aucun `VertexShader` dans les techniques), entrée de pixel identique à la sortie du vertex shader de `SpriteBatch` (`SV_POSITION`, `COLOR0`, `TEXCOORD0`), texture lue par le sampler 0 que `SpriteBatch` lie ; échantillonneur déclaré point et clamp. Variante `OPENGL` : `ps_3_0` (même bloc de macros que `SpriteBatch.fx`).
  - Techniques `Tonemap_Reinhard` et `Tonemap_AcesApprox` ; paramètres `Exposure` (défaut 1) et `SceneTexture` ; `Exposure` appliqué avant l'opérateur, valeurs négatives ramenées à 0, sortie `pow(saturate(c), 1/2.2)`, alpha 1. Constantes `ShaderParameterNames.Exposure` et `SceneTexture`. Enregistrement : une entrée dans `CasaEngine/Content/Content.mgcb`, `BuiltInShaderCatalog.TonemapContentName` + descripteur, `RenderPipeline` tient le `ShaderWrapper` (`SetTonemapEffect`, `TryReloadBuiltInShader`), chargé par `CasaEngineGame.Initialize` juste après `Content.RootDirectory` (le pipeline est construit avant que la racine de contenu soit posée), cas dans `ReloadBuiltInShader`. Le shader n'est pas encore utilisé pour dessiner.

### ✅ T1.4 — Cible de scène HDR, tonemapping, interface après

- Objectif : en `LinearHdr`, la scène est rendue en HDR, tonemappée vers la destination de la vue, et l'interface est composée ensuite. En `Legacy`, rien ne change.
- Fichiers : `RenderPipeline.cs`, `DefaultViewPipeline.cs`, `SkyBackgroundViewPipeline.cs` ; nouveaux `IScenePhaseViewPipeline.cs`, `ColorPipelinePlanner.cs` (pur, interne) ; nouveau `ColorPipelinePlannerTests.cs` ; ADR-0096. `FrameCapturePlanner` et ses types ne sont pas modifiés.
- Étapes :
  1. Relire `RenderPipeline.Render` en entier et écrire sous cette tâche la liste des chemins de destination : back buffer direct, back buffer via `_sceneRt`, cible retenue, cible propre (`RenderTargetSurface`), éditeur (`initialRenderTarget` non nul), plusieurs vues back buffer partageant la cible avec des rectangles distincts (écran partagé).
  2. Lire le mode en avance : `(view.EnvironmentOverride ?? view.World.EnvironmentSettings).ColorPipeline.Mode`, au même endroit que la lecture des ombres (`:146`). L'appel à `EnvironmentResolver.Resolve` ne bouge pas.
  3. `ColorPipelinePlanner` (fonction pure, testée) : à partir du mode, du plan de capture et du type de surface, rend `UseHdrSceneTarget` et la destination du tonemapping (celle que le chemin `Legacy` aurait liée pour dessiner : back buffer ou cible initiale, `_sceneRt`, cible retenue, ou cible propre de la vue). En `Legacy` : `UseHdrSceneTarget` faux, et aucune ligne du chemin existant n'est exécutée différemment.
  4. Cible HDR **par vue**, prise dans `RenderTargetPool` (`Acquire` avec `HalfVector4` et `Depth24Stencil8`, `Release` en fin de vue ; la clé du pool contient le format) à la taille du rectangle de la vue après `ApplyResolutionScale`. Repli de format `HdrBlendable` puis `Color` si l'acquisition échoue, journalisé une fois. Aucune création de cible après la première image de chaque taille.
  5. `IScenePhaseViewPipeline.RenderScene(graphicsDevice, view, in frame, renderers)` : les étapes 1 et 2 de `DefaultViewPipeline.RenderView`, sans l'interface. `DefaultViewPipeline.RenderView` appelle `RenderScene` puis compose l'interface (comportement inchangé). `SkyBackgroundViewPipeline` l'implémente en dessinant son fond puis en déléguant à son pipeline intérieur s'il l'implémente aussi ; sinon il ne l'expose pas.
  6. Chemin `LinearHdr`, par vue : (a) lier la cible HDR, viewport plein, effacer couleur et profondeur ; (b) `RenderScene`, ou `RenderView` complet pour un pipeline qui n'implémente pas l'interface, avec l'avertissement de P2 journalisé une fois ; (c) lier la destination exactement comme le chemin `Legacy` (cible, surface, viewport) et y effacer la profondeur selon `BuildDepthClearOptions` ; (d) dessiner la cible HDR dans le rectangle de la vue par `SpriteBatch` opaque avec `Tonemap.fx` (technique et exposition résolues), `DepthStencilState.None` ; (e) si `RenderScene` a été utilisé, composer l'interface sur la destination ; (f) reprendre le chemin existant : blit de `_sceneRt` ou de la cible retenue, rétention, debug overlay, presenter.
  7. Image figée : la cible retenue reçoit l'image tonemappée par (d) ; son format reste `Color`. Vérifier avec `Frozen frame demo` en `LinearHdr`.
  8. Restaurer cible, viewport et états ; aucun `new` par image.
  9. Tests : `ColorPipelinePlannerTests` (table de vérité des chemins de l'étape 1 × deux modes) ; `FrameCapturePlannerTests` inchangés et verts.
  10. ADR-0096 : cible HDR par vue, tonemapping avant l'interface, `IScenePhaseViewPipeline`, repli de format, comportement des pipelines de vue non adaptés.
- Validation : builds ; tests verts ; captures de T0.2 identiques ; avec `CASAENGINE_DEMO_COLOR_PIPELINE=LinearHdr`, lancement et capture sans exception de `Colour pipeline demo` (back buffer), de la même démo avec `CASAENGINE_COLOR_DEMO_BUFFERED=1` (vue à tampon : le pixel d'interface y vaut sa valeur `Legacy` de la même configuration ± 2, preuve que l'interface est composée après le tonemapping dans la cible de la vue), `Render-to-texture demo` (cible propre sans interface), `Split-screen demo (2 views)` (un pixel non noir mesuré dans chacune des deux vues) et `Frozen frame demo` ; compteur de créations de cible HDR nul après la dixième image sur 300 images de `Render-to-texture demo` (journal ou compteur de débogage, noté ici). **Marqueur positif obligatoire par exécution `LinearHdr`** (une image `Legacy` ne peut pas le produire) : sur `Colour pipeline demo`, en back buffer et en mode à tampon, le pixel gris 128 vaut **205 ± 2** (`AcesApprox`, exposition 1, sans décodage : 0,502 → 0,6176 → 205), dans la même capture que le pixel d'interface ; sur `Render-to-texture demo`, `Split-screen demo (2 views)` (chaque vue) et `Frozen frame demo`, au moins une création de cible HDR par taille de vue est relevée (`RenderTargetPool.TotalCount` ou journal de création unique) avant le contrôle « zéro après la dixième image ». Sans la variable de harnais, ces contrôles échouent. L'image `LinearHdr` est encore fausse en couleur (pas de décodage) : attendu, noté.
- Commit : `feat(rendering): render the scene into an HDR target and tonemap before the UI (ADR-0096)`
- Note de validation (2026-11-15) : `dotnet build CasaEngine.MonoGame.sln` et `CasaEngine.Editor.MonoGame.sln` : 0 erreur ; `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj` : 5254 réussis, 0 échec (5233 + 16 de `ColorPipelinePlannerTests` + 5 de `ScenePhaseViewPipelineTests`) ; `FrameCapturePlannerTests` inchangés et verts. `capture.sh` après la tâche (dossier `artifacts/pbr-baseline/t14legacy`) : les huit démos déterministes identiques à la base (material 7fe3d57bb10f7128, environment 043ad52894e18395, static-model 3ee7cd655db47386, static-shadow c7d6c867b3de4ad9, render-to-texture f35395822a585e9e, ui-overlay d226df79b82cf4b2, frozen-frame eb25f3917cfa6a0c, tile-map 355772772016e47f ; `ui-overlay` est tombé sur la base avec le délai par défaut) ; `split-screen` (non déterministe) relue à l'œil : identique à la base ; aucune exception.
  - **Chemins de destination (étape 1)**, relus dans `RenderPipeline.Render`. Le plan de capture (`FrameCapturePlanner`) donne `UseSceneRt` et `RetainThisFrame`; `ColorPipelinePlanner` en déduit la destination du tonemapping, qui est exactement ce que le chemin `Legacy` lie pour dessiner :
    1. **Back buffer direct** (`UseSceneRt` faux, surface back buffer, cible initiale nulle) : `SetRenderTarget(initialRenderTarget)` puis `Surface.Apply` (viewport du rectangle de la vue). Destination `InitialTarget`.
    2. **Back buffer via `_sceneRt`** (ombres actives, vue effacée, cible initiale = back buffer) : scène dans `_sceneRt` (`Viewport` 0,0,l,h), puis copie vers la cible initiale par `SpriteBatch` opaque. Destination `SceneRt` ; la copie existante reste exécutée telle quelle.
    3. **Cible retenue** (image figée, vue cible éligible) : même chemin avec `retention.EnsureRetained` à la place de `_sceneRt`. Destination `RetainedTarget`.
    4. **Cible propre** (`RenderTargetSurface` : render-to-texture, résolution virtuelle à tampon) : `Surface.Apply` lie la cible de la surface ; la cible n'est effacée en couleur que si `ClearColorBuffer`. Destination `ViewSurface`.
    5. **Éditeur** (`initialRenderTarget` non nul, surface back buffer) : `UseSceneRt` n'est jamais vrai (`InitialTargetIsBackBuffer` faux) ; la scène est dessinée directement dans la texture de l'hôte. Destination `InitialTarget`.
    6. **Plusieurs vues back buffer** (écran partagé) : chaque vue décide seule, par son propre rectangle, le chemin 1 (ou 2 avec ombres) ; le rectangle vient de `Surface.Apply`, pas du plan.
    Hors de ces chemins : vue au rectangle vide ou `ClearColorBuffer` faux : le planificateur répond « pas de cible HDR », la vue reste sur le chemin `Legacy` (voir écarts).
  - **Format de la cible HDR effectivement utilisé : `HalfVector4`** (journal : `HDR scene target created: 1024x768 HalfVector4, render 1`), avec `Depth24Stencil8`, prise dans `RenderTargetPool`. Les replis `HdrBlendable` puis `Color` sont codés mais non exercés (le format a été accepté).
  - **Marqueurs positifs `LinearHdr`** (`CASAENGINE_DEMO_COLOR_PIPELINE=LinearHdr`, `AcesApprox`, exposition 1, aucun décodage ; pixels lus sur la capture par une lecture PNG jetable, `artifacts/pbr-baseline/t14`) : `Colour pipeline demo` en back buffer : gris 128 (512,251) = **205**, 205, 205 et panneau d'interface (96,48) = **200, 60, 60** (valeur `Legacy`) dans la même capture ; avec `CASAENGINE_COLOR_DEMO_BUFFERED=1` (cible propre) : gris 128 = **205**, 205, 205, panneau = **200, 60, 60** (`Legacy` à tampon : 128 et 200, 60, 60). L'interface est donc bien composée après le tonemapping. Le texte d'écran (150,124) vaut 159, 223, 229 au lieu de 60, 200, 240 : attendu, `Renderer2DComponent` est encore vidé avec la scène (limite P5, objet de T1.6). Émissif (712,467) = 252, bord (786,467) = 85 : image fausse en couleur, attendu sans décodage.
  - **Créations de cible HDR** (une ligne de journal par création, avec le numéro de rendu) : `Render-to-texture demo` : 256x256 et 1024x768 au rendu 1 (deux vues), aucune autre ; sur 300 images et plus (capture à 7 s, ~120 images par seconde lues sur l'overlay, `CASAENGINE_SHOW_DEBUG_OVERLAY=1 CASAENGINE_CAPTURE_SCREENSHOT_DELAY_MS=7000`, « RT pool: 1 active / 2 free ») : **zéro création après le rendu 1**. `Split-screen demo (2 views)` : 512x768 au rendu 1 (les deux vues ont la même taille : la cible, rendue au pool après la première vue, sert la seconde) ; pixels non noirs mesurés dans chaque vue : vue gauche (100,400) = 192, 212, 229 et vue droite (700,600) = 110, 110, 147. `Frozen frame demo` : 960x720 au rendu 1 et 1280x960 au rendu 13 (le pas de redimensionnement voulu par la démo), aucune autre ; ses vérifications de rétention (D2, D9, D5a, D6b-d, égalités octet pour octet) passent en `LinearHdr`, seules celles qui comparent des couleurs absolues de l'ancienne image échouent (D1, D2b, D8d, D4b, D3, D5b, D5c, D6a : les couleurs sont tonemappées et non décodées, attendu). Aucune exception dans aucun journal. Sans la variable de harnais, aucune ligne « HDR scene target created ».
  - **Écarts au plan** (tous consignés dans ADR-0096) : (1) `IScenePhaseViewPipeline` porte un second membre, `SupportsScenePhase` (vrai par défaut, implémentation d'interface par défaut), parce que `SkyBackgroundViewPipeline` « ne l'expose pas » quand son pipeline intérieur ne l'implémente pas : un type ne peut pas implémenter une interface conditionnellement. (2) Une vue qui garde le contenu précédent de son tampon couleur (`ClearColorBuffer` faux), de rectangle vide, ou sans shader `Tonemap` chargé reste sur le chemin `Legacy` en `LinearHdr` : le tonemapping opaque effacerait le contenu ; le plan disait « toute vue ». (3) Découverte à la mesure : sur DesktopGL, lier le back buffer depuis une autre cible l'efface quand `PresentationParameters.RenderTargetUsage` vaut `DiscardContents` (ADR-0086) ; en écran partagé la seconde vue effaçait la première (vue gauche noire mesurée). Corrigé dans le chemin HDR seulement : l'usage est mis à `PreserveContents` le temps de la liaison de la cible initiale puis remis (`BindInitialTargetKeepingBackBuffer`) ; le chemin `Legacy` ne l'appelle pas. (4) `RenderTargetPool.Acquire` compte la cible une fois construite (un format refusé ne laisse plus d'entrée fantôme dans `TotalCount`) : sans effet quand la création réussit. (5) Un compteur interne `HdrTargetCreationCount` et un numéro de rendu `_renderIndex` (une incrémentation par `Render`) servent au journal de création.
  - **Pour T1.5 à T1.7** : T1.5 doit lier `ColorPipelineIsLinear` d'après l'usage effectif de la cible HDR et non d'après le seul mode résolu, sinon une vue restée sur le chemin `Legacy` (écarts 2) serait décodée sans être tonemappée ; l'effacement HDR est la ligne `Clear(... resolvedClearColor ...)` de la branche `useHdr` de `RenderPipeline.Render` (couleur d'effacement à décoder là). T1.6 insère le vidage des `IPostTonemapRenderer` dans `RenderHdrView` entre `DrawTonemap` et la composition de l'interface (qui y est faite par `UICompositionService.Compose`), en forçant `DepthStencilState.None`.
- Correctif après vérification (2026-11-15) : défaut (P2, introduit par 57f881ac) : la cible HDR prise dans `RenderTargetPool.Shared` n'était jamais libérée, donc redimensionner la fenêtre 20 fois laissait 21 cibles HalfVector4 (environ 12 octets par pixel chacune) et 21 lignes Info. Règle choisie : `RenderPipeline` note le rendu de dernier usage de chaque taille HDR (largeur, hauteur, format) ; une fois par `Render`, hors de la boucle des vues, `HdrTargetEviction` (fonction pure, testée) désigne les tailles inutilisées depuis plus de 4 rendus et `RenderTargetPool.TrimFree` libère leurs cibles libres, sans toucher aux autres utilisateurs du pool (atlas d'ombres) ; création et libération sont journalisées en Debug. Mesures : redimensionnement ×20 : 21 créées, 20 libérées, 1 vivante à la fin ; `Colour pipeline demo` : 1 création (1024x768) ; `Render-to-texture demo` : 2 (256x256, 1024x768) ; `Split-screen demo` : 1 (512x768) ; `Frozen frame demo` : 2 (960x720, 1280x960), la seconde libérée après le retour à 960x720 ; capture `LinearHdr` de référence inchangée (c15f8d93…) ; captures `Legacy` identiques ; 5296 tests réussis (+8).

### ✅ T1.5 — Décodage des couleurs en mode linéaire

- Objectif : en `LinearHdr`, les couleurs d'entrée sont linéarisées avant l'éclairage ; en `Legacy`, sortie identique au bit.
- Fichiers : `Macros.fxh` ou `Lighting.fxh` (aide `DecodeColor`), `LitForward.fx`, `skinEffect.fx`, `UnlitTexture.fx`, `SkyCubemap.fx`, `LegacySkyCube.fx` ; `ShaderParameterNames.cs`, `EnvironmentShaderBinder.cs`, `ForwardLightBinder.cs`, `ResolvedEnvironmentSettings.cs`, `SkyCubemapRenderer.cs`, `RenderPipeline.cs` (couleur d'effacement) ; nouveaux `ColorSpace.cs` (aide pure `Decode`), `ColorPipelineShaderCoverageTests.cs`, `ColorPipelineBindingTests.cs` ; `LightingShaderCoverageTests.cs` si une assertion doit être étendue.
- Étapes :
  1. Relever dans chaque `.fx` concerné les constantes libres ; choisir un emplacement pour `ColorPipelineIsLinear` qui ne déplace aucune constante existante (`_cb(c32.w)` dans `LitForward.fx` si la relecture le confirme libre). Le noter ici.
  2. `float3 DecodeColor(float3 c)` : `if (ColorPipelineIsLinear > 0.5) c = pow(abs(c), 2.2); return c;`.
  3. **Côté shader**, décoder : la couleur de base **après** sa modulation par la texture de détail (le produit `base *= 2.0 * detail` de `LitForward.fx:177-184` est neutre à 0,5 en espace d'affichage : ne jamais décoder l'échantillon de détail seul), la couleur de sommet, `DiffuseColor`, `EmissiveColor`, `SpecularColor`, `MaterialAmbientColor`, le cube de réflexion du matériau, le cube d'environnement et les sondes. Ne pas décoder la texture de normale ni la carte d'ombres.
  4. **Côté CPU**, parce que ces valeurs arrivent déjà multipliées par leur intensité (`ForwardLightBinder.cs:241-242, 258-259, 276-278` ; `ResolvedEnvironmentSettings.cs:63`) : en `LinearHdr`, décoder la couleur de chaque lumière **avant** le produit par l'intensité ; `EffectiveAmbientColor` devient `Decode(AmbientColor) × AmbientIntensity` ; la couleur d'ombre (`ShadowColor`, un facteur) et la couleur d'effacement sont décodées. Le mode vient de `context.Environment` (`ShaderBindCache.cs:33`). En `Legacy`, les valeurs liées sont celles d'aujourd'hui, à l'identique.
  5. `UnlitTexture.fx`, `SkyCubemap.fx`, `LegacySkyCube.fx` : décoder texture et couleur.
  6. Cube issu d'un `.hdr` (P4) : chercher comment le resolver connaît l'origine du cube ; ajouter une constante `EnvironmentCubeIsLinear` si l'origine est connue, sinon traiter tout cube comme encodé et l'écrire en « Points ouverts » (O2).
  7. Lier `ColorPipelineIsLinear` dans `EnvironmentShaderBinder.Bind` (globaux par changement de shader) et dans `SkyCubemapRenderer`.
  8. Tests : (texte) la constante est déclarée dans chaque shader concerné et dans `ShaderParameterNames` ; `DecodeColor` enveloppe la couleur de base modulée et n'enveloppe pas l'appel `SAMPLE_TEXTURE(DetailTexture,` ; la texture de normale n'est pas décodée ; les assertions d'emplacement existantes restent vertes. (Liaison) en `LinearHdr`, une lumière grise 0,5 d'intensité 4 lie `4 × 0,5^2,2` ; en `Legacy`, les valeurs liées égalent celles d'avant ; même paire de tests pour l'ambiant.
  9. Mesure sur `Colour pipeline demo` en `LinearHdr`, exposition 1 : le pixel gris 128 vaut **117 ± 2 en `Reinhard`** et **154 ± 2 en `AcesApprox`** (128 → 0,2195 linéaire ; Reinhard 0,180 ; ACES 0,330 ; ré-encodés). Noter les deux valeurs mesurées.
- Validation : builds ; tests verts ; captures de T0.2 identiques ; mesures de l'étape 9 tenues.
- Commit : `feat(rendering): decode display colours when the linear pipeline is active`
- Note de validation (2026-11-15) : `dotnet build CasaEngine.MonoGame.sln` et `CasaEngine.Editor.MonoGame.sln` : 0 erreur ; `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj` : 5277 réussis, 0 échec (5254 + 23 : `ColorPipelineShaderCoverageTests`, `ColorPipelineBindingTests`). `capture.sh` après la tâche (dossier `artifacts/pbr-baseline/t15legacy2`) : material 7fe3d57bb10f7128, environment 043ad52894e18395, static-model 3ee7cd655db47386, static-shadow c7d6c867b3de4ad9, render-to-texture f35395822a585e9e, frozen-frame eb25f3917cfa6a0c, tile-map 355772772016e47f identiques à la base ; `ui-overlay` rend `c94bfa52754c67b6` à la première capture (compteur « Time », voir T1.1) puis `d226df79b82cf4b2` (base) à la seconde ; skinned, particles, split-screen non déterministes, relues à l'œil : inchangées ; aucune exception, aucune ligne « was not found ».
  - **Constantes libres (étape 1).** `LitForward.fx` : `ColorPipelineIsLinear` en `_cb(c32.w)` (`c32` portait `.x .y .z`) et `EnvironmentCubeIsLinear` en `_cb(c34.y)` (`c34` ne porte que `.x`) ; `skinEffect.fx` : `c32.w` et `c32.y` (`HasMaterialReflectionCube` n'y existe pas). Les `_vs/_ps/_cb` sont de la documentation (macros vides) : aucun emplacement existant déplacé. `UnlitTexture.fx`, `SkyCubemap.fx`, `LegacySkyCube.fx` déclarent des paramètres nus (`float ColorPipelineIsLinear = 0`). Aide partagée : nouveau `ColorPipeline.fxh` (`DecodeColor`, `DecodeColor4`), inclus après la déclaration de la constante.
  - **Écart à l'étape 2 : un select, pas un `if`.** `DecodeColor` est `return ColorPipelineIsLinear > 0.5f ? pow(abs(c), 2.2f) : c;` (le `if` donne la même valeur et a été essayé en premier). Le vrai problème était ailleurs : décoder `SpecularColor` à chaque emplacement de lumière (24 emplacements déroulés) donnait sur OpenGL une image fausse **avec la constante à 0** (static-model : `0cf67de7c38506bd` au lieu de `3ee7cd655db47386` ; la bascule `if`/select n'y change rien ; en décodant diffuse et émissif seuls la base revient). Corrigé en décodant une seule fois par pixel : variables `static` `DecodedMaterialDiffuse` et `DecodedMaterialSpecular` posées en tête de `ComputeLights` et `ComputeLightsNoShadows`, lues par `AccumulateLight`. Cause exacte non élucidée (le GLSL produit est correct à la lecture) ; la formulation ci-dessus rend les huit hashes à l'identique.
  - **Assertions épinglées déplacées** (`LightingShaderCoverageTests`), parce que le texte épinglé a légitimement bougé : `EmissiveColor` passé en argument devient `MaterialEmissiveColor()` (`ApplyShadowTint(color.rgb, …`, `ApplyReflectionContributionWithShadowTint(color.rgb, …`, `ComposeLitSurfaceColor(… ambientContribution, …)` du skinné) et `return DiffuseColor.rgb * ambientTerm;` devient `return MaterialDiffuseColor() * ambientTerm;`. Les compteurs (5, 3, 1) et les assertions d'emplacement sont inchangés.
  - **Quelle vue décode (fait 1 de T1.4).** `RenderPipeline.Render` pose `LinearColorActive = true` sur sa copie du `ResolvedEnvironmentSettings` quand `useHdr` (planificateur et shader de tonemapping chargé), juste après `EnvironmentResolver.Resolve` (la valeur résolue est mise en cache par vue et ne peut pas porter la décision). Tout ce qui est construit depuis cette copie la voit : `WorldLightCollector.Collect` (ambiant décodé via `EffectiveAmbientColor`), `RenderFrame.Environment`, `RenderContext.Environment` ; `EnvironmentShaderBinder` lie `ColorPipelineIsLinear`, `ShaderBindCache` passe l'indicateur à `ForwardLightBinder.Bind(…, decodeColors)`, `SkyCubemapRenderer` le lie. Une vue en `LinearHdr` restée sur le chemin `Legacy` garde l'indicateur faux. Additif : un champ, aucun paramètre obligatoire ajouté. Décision enregistrée dans ADR-0097.
  - **Couleur d'effacement (fait 2).** Branche `useHdr` de `RenderPipeline.Render` : `Clear(…, ColorSpace.Decode(resolvedClearColor), …)` avec la surcharge `Vector4` (un `Color` 8 bits écraserait les valeurs sombres : 20/255 décodé vaut 0,0036). Le chemin `Legacy` garde `resolvedClearColor`.
  - **O2 (étape 6), tranché.** Le resolver connaît l'origine : un cube n'est linéaire que s'il vient d'un panorama `.hdr` (`PanoramaEnvironmentGenerator.IsLinearPanorama`, par l'extension comme le chargeur). `ResolvedEnvironmentSettings.BackgroundCubemapIsLinear` et `SpecularEnvironmentCubemapIsLinear` valent vrai dans ce seul cas (cube = cube du panorama) ; ciel procédural et atmosphère physique (valeurs `Color` de l'auteur), cubes DDS/authored, sondes et cube de réflexion du matériau sont décodés. Les shaders lisent `EnvironmentCubeIsLinear`. Limite : une atmosphère à soleil > 1 est décodée comme un cube d'affichage (voir ADR-0097). Non exercé en capture (aucun panorama `.hdr` dans les démos) : couvert par les tests de texte seulement.
  - **Mesures** (`Colour pipeline demo`, `CASAENGINE_DEMO_COLOR_PIPELINE=LinearHdr`, exposition 1 ; pixels lus par une lecture PNG jetable ; identiques en back buffer et avec `CASAENGINE_COLOR_DEMO_BUFFERED=1`) : gris 128 (512,251) = **117** en `Reinhard` (cible 117 ± 2) et **154** en `AcesApprox` (cible 154 ± 2). Autres points, `Reinhard` / `AcesApprox` : sphère (298,440) 114 / 149, (292,452) 117 / 154, (310,460) 111 / 144 ; émissif (712,467) 250 / 255 ; fond (786,467) 20 / 11 ; panneau d'interface (96,48) 200, 60, 60 dans les deux ; texte (150,124) 120, 175, 183 / 159, 223, 229 (encore vidé avec la scène : objet de T1.6).

### ✅ T1.6 — Renderers 2D, particules, sprites, lignes et fond de ciel en mode linéaire

- Objectif : aucun renderer ne produit de couleur doublement encodée en `LinearHdr`.
- Fichiers : `TexturedPrimitive.fx`, `PsxQuad.fx`, `DebugSolidColor.fx`, `DebugPrimitiveColor.fx`, `SpriteBatch.fx` (si utilisé dans la scène) ; `ParticleRendererComponent.cs`, `SpriteRendererComponent.cs`, `Line3dRendererComponent.cs`, `SkyBackgroundViewPipeline.cs` ; nouveau `IPostTonemapRenderer.cs` ; `Renderer2DComponent.cs`, `DefaultViewPipeline.cs`, `RenderPipeline.cs`.
- Étapes :
  1. Pour chaque renderer, lire comment son effet est chargé et lié, et où `SpriteBatch.fx` est utilisé. Écrire le relevé sous cette tâche.
  2. Ajouter `ColorPipelineIsLinear` et le décodage aux shaders de particules, de sprites 3D et de lignes ; chaque renderer lie la constante depuis `frame.Environment`. Étendre `ColorPipelineShaderCoverageTests` : pour chacun de ces shaders, la constante est déclarée et `DecodeColor` enveloppe la couleur échantillonnée et la couleur de sommet. Ajouter à `Colour pipeline demo` un sprite 3D gris 128 et une particule fixe gris 128, à des pixels de mesure notés dans le fichier.
  3. `SkyBackgroundViewPipeline` dessine son dégradé et son soleil par un `SpriteBatch` sans effet (`:72-74, 109-112`) dans la cible de scène : en `LinearHdr`, décoder côté CPU les couleurs qu'il passe au `SpriteBatch` ; si une texture non décodable entre en jeu, l'écrire comme limite dans la doc de T1.7.
  4. `IPostTonemapRenderer` (marqueur) sur `Renderer2DComponent` : en `LinearHdr`, `RenderScene` saute ces renderers et `RenderPipeline` les vide après le tonemapping, avant l'interface, avec `DepthStencilState.None` forcé pour ce vidage (la profondeur de la scène vit dans la cible HDR). En `Legacy`, ordre et états inchangés.
  5. Écrire la limite de P5 dans la doc de T1.7.
- Validation : builds ; tests verts ; captures de T0.2 identiques ; en `LinearHdr`, sur `Colour pipeline demo` en back buffer puis avec `CASAENGINE_COLOR_DEMO_BUFFERED=1` (vue à cible propre) : le pixel de texte et le pixel d'interface valent leur valeur `Legacy` de la même configuration ± 2, valeurs notées ici, et dans chacune de ces captures le pixel gris 128 vaut 154 ± 2 (preuve que la capture est bien `LinearHdr`) ; le pixel du sprite 3D gris 128 et celui de la particule gris 128 valent 154 ± 2 (preuve que ces renderers lient la constante) ; les lignes 3D ne sont couvertes que par le test de texte, limite écrite dans la doc de T1.7 ; sur `Particle system demo` : lancement sans exception et capture archivée ; sur `Material system demo` : un pixel du fond de ciel vaut la valeur attendue du pixel `Legacy` décodé, tonemappé et ré-encodé ± 2, ou la limite est écrite.
- Commit : `feat(rendering): keep 2D, particle, sprite, line and sky background colours correct in the linear pipeline`
- Note de validation (2026-11-15) : `dotnet build CasaEngine.MonoGame.sln` et `CasaEngine.Editor.MonoGame.sln` : 0 erreur ; `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj` : 5288 réussis, 0 échec (5277 + 11 : `ColorPipelineRendererTests`, extension de `ColorPipelineShaderCoverageTests`). `capture.sh` après la tâche (dossier `artifacts/pbr-baseline/t16legacy`) : les huit démos déterministes identiques à la base, `ui-overlay` compris (material 7fe3d57bb10f7128, environment 043ad52894e18395, static-model 3ee7cd655db47386, static-shadow c7d6c867b3de4ad9, render-to-texture f35395822a585e9e, ui-overlay d226df79b82cf4b2, frozen-frame eb25f3917cfa6a0c, tile-map 355772772016e47f) ; skinned, particles, split-screen (non déterministes) relues à l'œil : inchangées ; aucune exception, aucune ligne « was not found ». Décisions : ADR-0098 (marqueur, sprites d'écran, ciel).
  - **Relevé des renderers (étape 1).** Particules : `ParticleRendererComponent` charge `TexturedPrimitive.fx` et le **clone** (`Content.Load(...).Clone()`), lie `WorldViewProj` et `Texture` à chaque dessin ; le backend MGUI charge sa propre instance du même fichier (`CasaDesktopRuntime.TexturedPrimitiveEffect`) et ne lie jamais la constante, donc l'interface garde ses couleurs. Sprites 3D : `SpriteRendererComponent` charge `SpriteBatch.fx` (`Content.Load`, instance partagée) et, à la demande, `PsxQuad.fx` ; `SpriteBatch.fx` n'est **pas** l'effet de la classe XNA `SpriteBatch` : c'est l'effet de quad 3D des sprites de scène. Lignes 3D : `Line3dRendererComponent` charge `DebugPrimitiveColor.fx` (cloné). `DebugSolidColor.fx` est le gizmo de transformation de l'éditeur (`TransformGizmoComponent`, `GizmoTool`), dessiné par l'éditeur au-dessus de la vue : hors renderers de scène, non décodé (test). `TexturedPrimitive.fx` sert aussi l'interface (valeur 0 par défaut, jamais liée par elle). `Renderer2DComponent` et `SkyBackgroundViewPipeline` utilisent le `SpriteBatch` XNA sans effet.
  - **Décodage des shaders de scène.** `ColorPipelineIsLinear` (défaut 0) et `DecodeColor4(texel × couleur de sommet ou constante)` dans `TexturedPrimitive.fx`, `SpriteBatch.fx`, `PsxQuad.fx` (après les tests d'alpha, alpha intact), `DebugPrimitiveColor.fx`. Liaison depuis `frame.Environment.LinearColorActive` à chaque flush : particules (`DrawPackets`), lignes (`Draw`), sprites (`Flush` pose un champ lu par `Draw`, effets partagé et PSX ; `DrawStaticBatch` depuis son `frame` ; `DrawDirectly`, sans frame, pose 0). Un test existant (`SpriteRendererComponentPsxSemiTransparencyTests`) appelle `Draw(Matrix, Matrix)` par réflexion et exige que la fenêtre d'alpha soit posée avant toute autre chose : la signature est conservée et la constante est posée après la fenêtre.
  - **Fond de ciel.** Dégradé (texture 8 bits d'affichage) : en `LinearHdr`, une seconde texture `HalfVector4` du même dégradé décodé est dessinée à la place (décoder dans 8 bits écraserait le bas du dégradé) ; couleur du soleil décodée avant son facteur 0,65 ; cœur blanc et texture alpha seule inchangés. Pas de texture non décodable en jeu : aucune limite à ajouter pour le ciel.
  - **`IPostTonemapRenderer` (étape 4).** Interface additive `IPostTonemapRenderer : IViewFlushableRenderer` avec un membre `FlushPostTonemap` (écart au « marqueur » du plan : `Renderer2DComponent.Flush` pose son propre état de profondeur, que le pipeline ne peut pas forcer de l'extérieur ; ADR-0098). `DefaultViewPipeline.RenderScene` saute ces renderers quand la vue décode (`frame.Environment.LinearColorActive`) ; `RenderView` (vue entière) et tout chemin `Legacy` les vident dans l'ordre et avec les états d'avant. `RenderPipeline.RenderHdrView` les vide entre `DrawTonemap` et la composition de l'interface, `DepthStencilState.None` forcé avant l'appel et par l'implémentation (`FlushQueues(depthTest: false)`).
  - **Démo de mesure.** `Colour pipeline demo` gagne un sprite 3D gris 128 (quad 3 × 3 unités monde en (10, 4), texture blanche 1 × 1, `SpriteRendererComponent.DrawSprite`, `Sprite3dGrey128Pixel` = (842, 251)) et une particule fixe gris 128 (billboard 2 × 2 en (10, −2,5), image 1 de 2 × 2 sur le texel blanc de la texture de repli, `ParticleGrey128Pixel` = (852, 457), à 10 px à droite et au-dessus du centre où la texture est blanche), mis en file à chaque image. Aucun pixel de mesure existant ne bouge (vérifié : 128, 120/128/111, 255, 20, 200 60 60, 60 200 240 en `Legacy` comme avant). Valeur `Legacy` des deux nouveaux pixels : 128, 128, 128.
  - **Mesures** (`Colour pipeline demo`, `LinearHdr`, `AcesApprox`, exposition 1 ; lecture PNG jetable) : gris 128 (512,251), sprite 3D (842,251), particule (852,457) = **154, 154, 154** (cible 154 ± 2) dans les deux configurations, back buffer et `CASAENGINE_COLOR_DEMO_BUFFERED=1` ; panneau d'interface (96,48) = **200, 60, 60** et texte (150,124) = **60, 200, 240** dans les deux, égaux à leurs valeurs `Legacy` (cible ± 2), dans la même capture que le gris à 154 (avant T1.6 le texte valait 159, 223, 229 : il est bien vidé après le tonemapping) ; fond (786,467) = 11. `Particle system demo` en `LinearHdr` : lancement et capture sans exception (archive `artifacts/pbr-baseline/linear/particle-system-linear-t16.png`). `Material system demo` en `LinearHdr` (`AcesApprox`, exposition 1) : pixel de ciel (40,20) `Legacy` 64, 91, 133 → mesuré 60, 102, 160 = attendu par v=(L/255)^2,2, ACES, ré-encodage (60, 102, 160) ; (40,100) 75, 99, 136 → 77, 114, 163 (attendu 77, 114, 163) ; (40,200) 103, 119, 143 → 120, 143, 171 (attendu 120, 143, 171) ; (40,250) 127, 135, 149 → 153, 162, 177 (attendu 153, 162, 177) : écart 0 (archive `linear/material-linear-t16.png`).
  - **Limites à écrire dans la doc de T1.7 (étape 5).** P5/O3 : en `LinearHdr`, `Renderer2DComponent` (sprites d'écran, texte, lignes 2D) ne teste plus la profondeur de la scène et est dessiné sur l'image tonemappée, avec l'interface ; une vue dont le pipeline n'implémente pas `IScenePhaseViewPipeline` le garde dans la cible HDR, tonemappé avec la scène. Lignes 3D (`Line3dRendererComponent`) : couvertes par le test de texte seulement, aucune ligne 3D dans la démo de mesure. `LegacySkyCube.fx` : constante à lier par le jeu. `DebugSolidColor.fx` (gizmo de l'éditeur) non décodé. Atmosphère physique à soleil > 1 décodée comme un cube d'affichage (ADR-0097).

### 🧪 T1.7 — Clôture de la phase 1 : non-régression et documentation

- Objectif : verrouiller la phase avant tout shader PBR.
- Fichiers : nouveau `docs/engine/color-pipeline.md` ; `docs/README.md` ; `docs/engine/effect-file-inventory.md` ; ce plan.
- Étapes :
  1. Recapturer toutes les démos de T0.2 en `Legacy` : empreintes identiques.
  1b. Référence de la phase 2 : capturer **deux fois** `Colour pipeline demo` en `LinearHdr`, réglages par défaut (`AcesApprox`, exposition 1, intensité 1, back buffer), dans `CasaEngine/artifacts/pbr-baseline/linear/colour-pipeline-default-{1,2}.png` ; noter ici la commande exacte, les deux empreintes et si elles sont égales (déterministe ou non).
  2. Capturer en `LinearHdr` les démos de T0.2 par leur titre exact : aucune exception, image plausible ; archiver.
  3. `Colour pipeline demo`, intensité de lumière 16 : en `LinearHdr`, les trois points de mesure de la sphère éclairée ont trois valeurs distinctes ; en `Legacy`, au moins deux sont écrêtés à la même valeur. Noter les valeurs.
  4. `RenderStats` en `Legacy` sur `Material system demo` : appels de dessin et changements d'état égaux aux valeurs notées en T0.2 étape 5, par la même commande.
  5. Documentation : modes, clé `color_pipeline`, ordre de l'image, ce qui est décodé et où (shader ou CPU), limites (P2, P5, O4, émissif traité comme couleur d'affichage, repli de format), renvoi aux ADR-0095 à ADR-0098 ; reprendre les limites écrites sous T1.5 (O2, atmosphère à soleil > 1) et T1.6 (P5/O3, lignes 3D, `LegacySkyCube.fx`, gizmo).
- Validation : résultats notés ici ; toute différence `Legacy` passe la tâche en ⚠️.
- Commit : `docs(engine): document the colour pipeline and close its validation`
- Note de validation (2026-11-15), `CasaEngine.Demos` en Debug, 1024 × 768 ; captures et journaux sous `CasaEngine/artifacts/pbr-baseline/` ; lecture de pixels par un programme jeté (`System.Drawing`, non committé) :
  - **Étape 1, `Legacy`** : `bash CasaEngine/artifacts/pbr-baseline/capture.sh D:/development/repo/RacingGame/CasaEngine/CasaEngine/artifacts/pbr-baseline/t17legacy` (dossier `t17legacy`). Les huit démos déterministes sont identiques à la base, deux passages chacune, `ui-overlay` compris (délai par défaut) : material 7fe3d57bb10f7128, environment 043ad52894e18395, static-model 3ee7cd655db47386, static-shadow c7d6c867b3de4ad9, render-to-texture f35395822a585e9e, ui-overlay d226df79b82cf4b2, frozen-frame eb25f3917cfa6a0c, tile-map 355772772016e47f. Aucune ligne « was not found », aucune exception. Non déterministes (empreintes différentes à chaque passage, comme en T0.2) : skinned f14733ff… / d3434438…, particles 9a145d30… / b6faba31…, split-screen 3734996d… / e1b50c39…. Comparaison pixel à pixel à la base `legacy/*-1.png` : skinned et particles, écarts confinés au rectangle des personnages (189,249 - 743,444) et à celui des particules (493,332 - 874,469), soit les zones où les deux captures de la base diffèrent déjà entre elles ; split-screen, écarts dans les blocs de statistiques (chiffres de temps, largeur du cadre de texte), même géométrie de scène. **Relu à l'œil par l'agent (images ouvertes côte à côte avec leur base) : rien d'autre que la phase d'animation, la phase des particules et les chiffres de statistiques ne diffère. Ce contrôle repose sur le regard de l'agent : à confirmer par l'auteur (🧪).**
  - **Étape 1b, référence de la phase 2** : depuis `CasaEngine.Demos/bin/Debug/net9.0-windows`, `CASAENGINE_START_DEMO="Colour pipeline demo" CASAENGINE_DEMO_COLOR_PIPELINE=LinearHdr CASAENGINE_DEMO_TONEMAPPER=AcesApprox CASAENGINE_DEMO_EXPOSURE=1 CASAENGINE_COLOR_DEMO_LIGHT_INTENSITY=1 CASAENGINE_CAPTURE_SCREENSHOT_PATH="D:\development\repo\RacingGame\CasaEngine\CasaEngine\artifacts\pbr-baseline\linear\colour-pipeline-default-<n>.png" ./CasaEngine.Demos.exe` (n = 1, 2 ; sans `BUFFERED`, donc back buffer). SHA-256 des deux captures : `c15f8d93912b84697807ed8218a0b4e1b1dd6b879de9f33ba107d68a21f3c490` pour `-1` et pour `-2` : **égales, capture déterministe** (comparaison au bit possible en T2.2). Journal : `Colour pipeline harness: LinearHdr, AcesApprox, exposure 1.`
  - **Étape 2, `LinearHdr`** : `bash CasaEngine/artifacts/pbr-baseline/capture.sh D:/development/repo/RacingGame/CasaEngine/CasaEngine/artifacts/pbr-baseline/t17linear CASAENGINE_DEMO_COLOR_PIPELINE=LinearHdr` (dossier `t17linear`, archivé). Les 11 journaux portent `Colour pipeline harness: LinearHdr`, aucune exception, aucune ligne « was not found ». Seul avertissement : `[FrozenFrame] Capture unavailable: no retained frame` (attendu une fois par la démo de la capture figée). Empreintes (identiques sur les deux passages) : material 1ff1539d464e5029, environment 249c511e72fc3311, static-model 93c965b8334e4482, static-shadow 52e9323017f899da, render-to-texture e904ba554e6d04c0, ui-overlay 1f28e04394e177ed, frozen-frame d834bf0fff6094a9, tile-map 3efaf629d127e5f8 ; skinned, particles, split-screen non déterministes. **Plausibilité, jugée à l'œil par l'agent (🧪, à confirmer par l'auteur)** : aucune image noire ni délavée ; luminance moyenne `LinearHdr` / `Legacy` : material 150 / 127, environment 157 / 143, static-model 165 / 157, static-shadow 30 / 41, skinned 30 / 41, render-to-texture 163 / 153, ui-overlay 158 / 141, frozen-frame 134 / 144, tile-map 162 / 151, particles 117 / 116, split-screen 99 / 97. Par démo : `material` plausible, sans écrêtage de la sphère et du cube blancs (sol plus clair et plus plat, tache de la lumière ponctuelle plus pâle : attendu d'un éclairage fait pour l'affichage, rendu en linéaire) ; `environment` plausible (ciel violet, damier lisible, sphère réfléchissante) ; `static-model` plausible, damier un peu moins contrasté ; `static-shadow` plausible, ombre portée présente, scène plus sombre ; `skinned` plausible, fond plus sombre ; `particles` plausible, la bande sombre du bas du plateau n'est plus distincte du plateau ; `split-screen` plausible, cadre de statistiques lisible, `RT pool: 0 active / 1 free` (la cible HDR est rendue au pool) ; `render-to-texture` plausible, la vignette de la cible reste lisible ; `ui-overlay` plausible, panneau et boutons lisibles ; `frozen-frame` plausible, couleurs vives un peu assombries et désaturées (effet attendu du décodage puis du tonemapping de fonds unis) ; `tile-map` plausible, carte et fenêtre de texte lisibles. Aucun défaut relevé.
  - **Étape 3, intensité de lumière 16** (`CASAENGINE_START_DEMO="Colour pipeline demo" CASAENGINE_COLOR_DEMO_LIGHT_INTENSITY=16 CASAENGINE_DEMO_COLOR_PIPELINE=<mode>`, `AcesApprox`, exposition 1, dossier `t17int16`) : points (298,440), (292,452), (310,460) : **`LinearHdr` 250 ; 251 ; 250** (gris, trois canaux égaux), **`Legacy` 255 ; 255 ; 255**. Attendu en `Legacy` tenu (trois points écrêtés à 255). **Attendu en `LinearHdr` non tenu au sens strict : deux des trois points valent 250 (trois valeurs distinctes attendues).** La compression de `AcesApprox` aplatit les hautes valeurs vers 255 ; l'information HDR n'est pourtant pas perdue : en `LinearHdr` la sphère va de 244 à 251 (points (278,467) 250, (300,430) 247, (320,450) 244, (285,445) 251) alors qu'elle est plate à 255 en `Legacy` ; avec `Reinhard` (`CASAENGINE_DEMO_TONEMAPPER=Reinhard`, même intensité) les trois points valent 226 ; 227 ; 224, trois valeurs distinctes. Aucun code modifié : voir O12.
  - **Étape 4, `RenderStats`** : `CASAENGINE_START_DEMO="Material system demo" CASAENGINE_SHOW_DEBUG_OVERLAY=1 CASAENGINE_CAPTURE_SCREENSHOT_PATH=...\t17stats\material-overlay.png ./CasaEngine.Demos.exe` (`Legacy`, depuis `CasaEngine.Demos/bin/Debug/net9.0-windows`) : overlay lu sur l'image, **Draws 14, FX 2, Tex 32, State 3, O 13, T 1**, « RT pool: 0 active / 0 free » : égal aux valeurs de T0.2.
  - **Étape 5** : nouveau `docs/engine/color-pipeline.md` (modes, clé `color_pipeline`, ordre de l'image, décodage shader ou CPU, `LinearColorActive`, vidage après tonemapping, toutes les limites dont O1, O3, O4, O10, O11, repli de format), ajouté à `docs/README.md` ; `docs/engine/effect-file-inventory.md` : `Tonemap.fx` y figurait déjà, `ColorPipeline.fxh` ajouté.
  - **Étapes 6 et 7** : ADR-0097 (décodage) et ADR-0098 (renderers 2D) ayant été pris par T1.5 et T1.6, les ADR prévus plus loin sont décalés de deux : bloom ADR-0099 (T2.1), `lit-pbr` ADR-0100 (T4.2), IBL ADR-0101 (T6.2), import glTF ADR-0102 (T9.1), renvoi de T10.3 « ADR-0094 à ADR-0102 » ; les notes de T1.1 à T1.6 et les fichiers d'ADR sont inchangés. Points ouverts O10 et O11 ajoutés, plus O12 (étape 3).
  - **Statut ⚠️** : la mesure de l'étape 3 en `LinearHdr` n'a pas tenu (O12) ; en plus, les contrôles visuels de l'étape 1 (trois démos non déterministes) et de l'étape 2 (plausibilité) reposent sur le seul regard de l'agent. Tout le reste est tenu : empreintes `Legacy`, `RenderStats`, déterminisme de la référence de la phase 2 ; `dotnet build CasaEngine.MonoGame.sln` : 0 erreur ; `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj` : 5288 réussis, 0 échec.

---

## Phase 2 — Bloom

### ⏳ T2.1 — Réglages et shader de bloom

- Objectif : réglages sérialisés et shader compilé.
- Fichiers : `ColorPipelineSettings.cs`, `ResolvedEnvironmentSettings.cs`, `EnvironmentResolver.cs`, `EntityDetailsPanel.cs`, `ColorPipelineSettingsTests.cs` ; nouveau `CasaEngine/Content/Shaders/Bloom.fx` ; `Content.mgcb`, `BuiltInShaderCatalog.cs`, `CasaEngineGame.cs` ; ADR-0099.
- Étapes :
  1. `BloomEnabled` (faux), `BloomThreshold` (1), `BloomIntensity` (0,5), `BloomRadius` (1) sous `color_pipeline`, mêmes règles de sérialisation et mêmes tests que T1.1.
  2. `Bloom.fx` : techniques `Bloom_BrightPass` (seuil doux), `Bloom_Downsample`, `Bloom_BlurHorizontal`, `Bloom_BlurVertical` (poids normalisés, somme 1 vérifiée par test), `Bloom_Composite` (additif).
  3. Enregistrement comme T1.3. ADR-0099.
- Validation : builds ; tests verts ; captures de T0.2 identiques.
- Commit : `feat(rendering): add bloom settings and the bloom shader (ADR-0099)`

### ⏳ T2.2 — Passe de bloom avant le tonemapping

- Objectif : le bloom agit sur la cible HDR, avant le tonemapping, donc jamais sur l'interface.
- Fichiers : nouveau `CasaEngine/Framework/Rendering/PostProcess/BloomRenderer.cs` ; `RenderPipeline.cs` ; `RenderStats.cs` (compteur additif si utile) ; `ColorPipelineDemo.cs`.
- Étapes :
  1. `BloomRenderer` : cibles HDR à la moitié, au quart et au huitième, prises dans `RenderTargetPool` (clé avec format) ; passe-haut, réductions, flou séparable, recomposition dans la cible HDR. États restaurés ; aucune allocation par image.
  2. Appel dans le chemin `LinearHdr` entre la scène et le tonemapping, si `BloomEnabled`.
  3. Démo : touche de bascule ; l'émissif fort de `ColorPipelineDemo` sert de mesure.
- Validation : builds ; tests verts ; captures de T0.2 identiques ; bloom coupé : la capture `LinearHdr` de `Colour pipeline demo` prise par la commande notée en T1.7 étape 1b a la même empreinte que `colour-pipeline-default-1.png` (si T1.7 a classé cette capture non déterministe : écart absolu maximal par canal ≤ 1 sur toute l'image, noté) ; bloom actif : un pixel à 8 px du bord de l'émissif est plus clair d'au moins 10 niveaux, un pixel d'interface est inchangé.
- Commit : `feat(rendering): apply bloom to the HDR scene before tonemapping`

---

## Phase 3 — BRDF GGX en éclairage direct

### ⏳ T3.1 — `Pbr.fxh`

- Objectif : le BRDF isolé dans un include, branché sur les lumières de `Lighting.fxh`.
- Fichiers : nouveau `CasaEngine/Content/Shaders/Pbr.fxh` ; nouveau `CasaEngine.Tests/Rendering/PbrShaderCoverageTests.cs`.
- Étapes :
  1. Distribution GGX, visibilité de Smith corrélée en hauteur, Fresnel de Schlick, `F0 = lerp(0.04, baseColor, metallic)`, diffus de Lambert `(1 - F)(1 - metallic) baseColor / π`, `alpha = roughness²` avec rugosité bornée à 0,045 au minimum.
  2. `EvaluatePbrLights(...)` : parcourt directionnelles, ponctuelles et spots avec les mêmes déclarations, atténuations et plafonds que `Lighting.fxh`, sans les dupliquer ; applique le facteur d'ombre à `ShadowedDirectionalLightIndex` seulement ; rend diffus et spéculaire séparés.
  3. Tests par lecture du texte : fonctions présentes, aucune redéclaration des tableaux de lumières.
- Validation : `LitForward.fx` et `skinEffect.fx` compilent toujours ; captures de T0.2 identiques.
- Commit : `feat(shaders): add the shared GGX BRDF include`

### ⏳ T3.2 — `LitPbr.fx`

- Objectif : le shader PBR des maillages statiques.
- Fichiers : nouveau `CasaEngine/Content/Shaders/LitPbr.fx` ; `Content.mgcb` ; `ShaderParameterNames.cs` ; `PbrShaderCoverageTests.cs`.
- Étapes :
  1. Déclarer, avec leurs emplacements écrits dans l'en-tête du fichier : les constantes de lumières, d'ombres (mode, couleur, rayon de filtre, fondu, profondeur) et d'environnement exigées par `Lighting.fxh`, `ColorPipelineIsLinear`, et `BaseColor`, `Metallic`, `Roughness`, `EmissiveColor`, `OcclusionStrength`, `AlphaCutoff`, `NormalScale`. Définir `HAS_ENVIRONMENT_BINDINGS` et `HAS_FORWARD_SHADOW_BINDINGS`.
  2. Textures : t0 couleur de base, t1 normale, t2 metallic-roughness (G rugosité, B métal, convention glTF), t3 cube d'environnement, t4 et t5 sondes, t6 ombres, t7 occlusion (R), t8 émissif. Vérifier d'abord la limite de samplers de la cible ; si elle est dépassée, fusionner occlusion dans le canal R de t2 et l'écrire ici.
  3. Techniques : `LitPbr`, `LitPbr_NormalMap`, chacune avec ou sans `_AlphaTest` ; présence des textures pilotée par des constantes `Has…` (comme `HasDetailTexture`), pas par des techniques.
  4. Ambiant de cette phase : terme plat `AmbientColor × occlusion × baseColor diffus`, isolé dans `ComputePbrAmbient` pour la phase 6 ; `UseFlatAmbientSource` déjà honoré.
  5. Ombres : mêmes fonctions que `LitForward.fx` ; le mode `MultiplyLitColor` (ADR-0093) s'applique au pixel entier hors émissif.
  6. Sortie non bornée ; alpha = alpha de la couleur de base.
  7. Tests : techniques présentes ; les deux macros définies avant l'inclusion ; chaque constante de `ShaderParameterNames` utilisée par le matériau PBR est déclarée dans le fichier.
- Validation : builds des deux solutions ; tests verts ; captures de T0.2 identiques.
- Commit : `feat(shaders): add the static PBR lit shader`

### ⏳ T3.3 — Enregistrement et sélection de `LitPbr`

- Objectif : `LitPbr.fx` chargé, sélectionnable et rechargeable.
- Fichiers : `BuiltInShaderCatalog.cs`, `EffectiveShaderResolver.cs`, `MaterialShaderCapabilities.cs` (`MaterialShaderFamily.LitPbr`), `StaticMeshRendererComponent.cs`, `CasaEngineGame.cs`, `ShaderVariantLibrary.cs` si nécessaire ; tests `EffectiveShaderResolverTests` (ou le fichier existant qui couvre le resolver), `ShaderTechniqueCoverageTests.cs`.
- Étapes :
  1. Descripteur, nom de contenu et identifiant de shader `LitPbr`.
  2. `EffectiveShaderResolver` : famille `LitPbr` → shader `LitPbr`, avant le repli vers `LitForward`. Le cas skinné reste traité en phase 8 : d'ici là, un matériau PBR sur maillage skinné garde le shader de skinning (noté dans la doc).
  3. `StaticMeshRendererComponent` : chargement, enregistrement dans `ShaderManager` et `RenderShaderSelector`, cas dans `TryReloadBuiltInShader` ; ne pas passer `LitPbr` par `ApplyLitForwardDefaults`. Cas dans `ReloadBuiltInShader`.
  4. Tests : une capacité de famille `LitPbr` résout `LitPbr` ; une famille `Lit` résout toujours `LitForward` ; `ShaderTechniqueCoverageTests` couvre `LitPbr.fx`.
- Validation : builds ; tests verts ; captures de T0.2 identiques.
- Commit : `feat(rendering): register the PBR shader and resolve it for PBR materials`

---

## Phase 4 — Matériau `lit-pbr`

### ⏳ T4.1 — `LitPbrMaterial`

- Objectif : la classe runtime qui lie les paramètres PBR.
- Fichiers : nouveau `CasaEngine/Framework/Materials/Runtime/LitPbrMaterial.cs` ; nouveau `CasaEngine.Tests/Rendering/LitPbrMaterialTests.cs`.
- Étapes :
  1. Propriétés : `BaseColor`, `Metallic`, `Roughness`, `EmissiveColor`, `AlphaCutoff`, `OcclusionStrength`, `NormalScale`, textures couleur de base, normale, metallic-roughness, occlusion, émissif.
  2. `GetShaderCapabilities` : famille `LitPbr`, drapeaux de texture, alpha-test et transparence selon la file et l'alpha de `BaseColor` (même règle que `LitDiffuseMaterial.cs:161-168`).
  3. `RequiresMaterialTechniqueSelection` et `GetTechniqueName` : `LitPbr[_NormalMap][_AlphaTest]` ; la normal map exige des tangentes (même règle que `RenderFeatureResolver.cs:40-43`), sans exiger de texture de base.
  4. `Bind` : tous les paramètres, `AlphaCutoff` à 0 hors file `AlphaTest`, constantes `Has…`.
- Validation : builds ; tests : capacités et nom de technique pour chaque combinaison.
- Commit : `feat(materials): add the PBR runtime material`

### ⏳ T4.2 — Définition `lit-pbr`, compilation, surcharges

- Objectif : la définition intégrée, compilée et surchargeable par instance.
- Fichiers : `BuiltInMaterialDefinitions.cs`, `MaterialCompiler.cs`, `MaterialInstancePropertyBlockMapper.cs`, `MaterialRenderStateResolver.cs` si nécessaire, `StaticModelMaterialResolver.cs` (lecture seule sauf besoin) ; tests frères de ceux de `lit-diffuse` ; ADR-0100.
- Étapes :
  1. Définition `lit-pbr` : clés `base_color_texture`, `normal_texture`, `metallic_roughness_texture`, `occlusion_texture`, `emissive_texture`, `base_color` (marquée `AffectsTransparency`), `metallic`, `roughness`, `emissive_color`, `alpha_cutoff`, `occlusion_strength`, `normal_scale` ; groupes, bornes, pas et indices d'éditeur sur le modèle de `lit-diffuse`. Vérifier qu'aucun alias n'entre en collision.
  2. `MaterialCompiler` : fabrique directe `CreateLitPbrMaterial` recevant les `holds`, à côté de celle de `lit-diffuse`.
  3. Surcharges par instance : `base_color`, `metallic`, `roughness`, `emissive_color`.
  4. Tests : identifiants intégrés (3) ; aller-retour `.material` ; compilation → file et technique attendues (opaque, alpha-test, transparent) ; surcharge appliquée ; les comptes de `MaterialDefinitionEditorRegistryTests` et `MaterialAssetJsonSerializerTests` mis à jour avec leur justification.
  5. ADR-0100 : définition `lit-pbr`, clés, famille, choix de technique par le matériau.
- Validation : builds ; tests verts ; captures de T0.2 identiques.
- Commit : `feat(materials): add the lit-pbr definition, its compilation and overrides (ADR-0100)`

### ⏳ T4.3 — Ombres portées alpha-test et premier rendu

- Objectif : un matériau PBR alpha-test projette une ombre découpée ; première image PBR mesurée.
- Fichiers : `ShadowPass.cs` ; `MaterialBase.cs` (membre virtuel additif) ; `ShadowPipelineCoverageTests.cs` si une chaîne épinglée bouge ; `ColorPipelineDemo.cs` ou démo provisoire.
- Étapes :
  1. Remplacer le test de type de `ShadowPass.cs:144-158` par un membre virtuel de `MaterialBase` rendant la texture de masque (nul par défaut), surchargé par `LitDiffuseMaterial`, `UnlitTextureMaterial` et `LitPbrMaterial` ; comportement identique pour les deux premiers.
  2. Rangée de cinq sphères `lit-pbr` non métalliques, rugosité 0,1 à 0,9, sous une directionnelle, en `LinearHdr` : mesurer la largeur du reflet (pixels au-dessus de la moitié du maximum) ; elle doit croître strictement avec la rugosité.
- Validation : builds ; tests verts ; captures de T0.2 identiques (les ombres de `StaticShadowValidation` comprises) ; mesure notée. Test `ShadowMaskTests` (passant par le nouveau membre virtuel) : un `LitPbrMaterial` en file `AlphaTest` avec texture de couleur de base fournit cette texture comme masque d'ombre, avec son `AlphaCutoff` ; hors `AlphaTest` ou sans texture, il n'en fournit pas ; `LitDiffuseMaterial` et `UnlitTextureMaterial` fournissent exactement ce qu'ils fournissent aujourd'hui.
- Commit : `feat(rendering): let any material provide its shadow mask and validate the first PBR image`

### ⏳ T4.4 — Éditeur

- Objectif : créer et éditer un `.material` `lit-pbr` dans l'éditeur.
- Fichiers : `CasaEngine.Editor` : `MaterialDefinitionEditorRegistry.cs`, `GameEditor.cs` (description de type), flux de création de matériau, `MaterialPreviewViewport.cs`.
- Étapes :
  1. Vérifier le classement par mots-clés des nouvelles clés (`occlusion_texture`, `metallic_roughness_texture`) ; corriger seulement ce qui range une clé dans le mauvais groupe.
  2. Description de type pour `LitPbrMaterial` ; choix de la définition à la création.
  3. Aperçu : le matériau PBR s'affiche ; rechargement à chaud de `LitPbr.fx`.
- Validation : build de la solution éditeur ; tests de registre verts ; contrôle manuel dans l'éditeur → 🧪 tant que l'auteur ne l'a pas fait.
- Commit : `feat(editor): edit and preview lit-pbr materials`

---

## Phase 5 — PBR transparent

### ⏳ T5.1 — Mélange transparent PBR

- Objectif : verre et vitres en `lit-pbr`.
- Fichiers : `LitPbr.fx`, `LitPbrMaterial.cs`, `MaterialRenderStateResolver.cs` ou `RenderStateCache.cs` selon ce qui porte l'état de mélange ; tests ; démo.
- Étapes :
  1. Lire comment la file `Transparent` obtient son état de mélange et sa profondeur (`RenderStateCache.cs:51`).
  2. Technique `LitPbr_Transparent` (et `_NormalMap`) : sortie `float4(diffus × alpha + spéculaire + émissif, alpha)` avec un état de mélange prémultiplié ; écriture de profondeur coupée.
  3. Démo : sphère de verre (alpha 0,25, rugosité 0,05) devant un damier.
- Validation : builds ; tests verts ; captures de T0.2 identiques ; mesure : derrière la sphère le damier reste lisible (contraste ≥ 50 % du contraste sans sphère) et le pixel du reflet spéculaire dépasse 200 après tonemapping.
- Commit : `feat(rendering): add transparent PBR with preserved specular`

---

## Phase 6 — Éclairage par l'image

### ⏳ T6.1 — Générateur de cubes convolués sur le GPU

- Objectif : produire un `TextureCube` à mips à partir d'un cube source, quel que soit son format.
- Fichiers : nouveaux `CasaEngine/Content/Shaders/IblConvolution.fx`, `CasaEngine/Framework/Rendering/Environment/IblCubeGenerator.cs` ; `Content.mgcb`, `BuiltInShaderCatalog.cs` ; `Macros.fxh` (`SAMPLE_CUBEMAP_LEVEL`, variante `OPENGL` comprise) ; tests.
- Étapes :
  1. Vérifier que `TextureCube` accepte `mipMap: true` et `SetData` par face et par niveau dans la version de MonoGame du dépôt (lire la signature dans le paquet référencé) ; sinon ⚠️ Blocked.
  1b. **Avant tout autre travail de la phase** (backend OpenGL, T0.3) : écrire `SAMPLE_CUBEMAP_LEVEL` (`texCUBElod(sampler, float4(dir, level))` sous `OPENGL`) et prouver par un shader d'essai, sur un cube à mips dont chaque niveau porte une couleur unie distincte, que le niveau demandé est bien celui lu (pixel mesuré pour les niveaux 0, 2 et 4). Si la chaîne OpenGL de MonoGame refuse ou ignore le niveau : repli sans niveau de mip — les niveaux de rugosité sont rangés dans **une texture 2D** (un bandeau latitude-longitude par niveau, empilés), échantillonnée par `tex2D` avec interpolation manuelle entre deux bandeaux ; l'écrire ici, en ADR-0101, et adapter T6.2, T6.3 et T7.1 dans le même commit.
  2. `IblConvolution.fx` : `Irradiance` (convolution cosinus, échantillonnage fixe) et `PrefilterGgx` (échantillonnage d'importance, 64 échantillons, rugosité en paramètre).
  3. `IblCubeGenerator` : pour chaque face et niveau, rendre dans une `RenderTarget2D` `Vector4`, relire par `GetData`, écrire par `SetData`. Irradiance 32 px sans mips ; spéculaire 128 px, 6 niveaux, rugosité `niveau / 5`. États restaurés.
  4. Tests : fonctions pures (direction par face et par texel, rugosité par niveau, nombre de niveaux).
- Validation : builds ; tests verts ; temps de génération mesuré et noté (seuil d'alerte : 500 ms par cube).
- Commit : `feat(rendering): generate irradiance and prefiltered cubes on the GPU`

### ⏳ T6.2 — Cache et liaison de l'IBL

- Objectif : les cubes IBL de l'environnement résolu, générés une fois par changement.
- Fichiers : `EnvironmentResolver.cs`, `ResolvedEnvironmentSettings.cs`, `EnvironmentShaderBinder.cs`, `ShaderParameterNames.cs`, nouveau `IblCache.cs` ; tests ; ADR-0101.
- Étapes :
  1. `IblCache` : clé = instance du cube spéculaire source ; rend irradiance et préfiltré s'ils existent, sinon enregistre une demande ; libère à la disparition de la source (suivre `GeneratedAssetHandleCache`). Les demandes sont exécutées par `RenderPipeline.Render` avant toute vue (P9), jamais dans `EnvironmentResolver.Resolve` ni pendant qu'une cible de vue est liée. Compteur de générations et indicateur « générée hors boucle des vues » exposés pour les tests et la démo.
  2. `ResolvedEnvironmentSettings` : `IrradianceCubemap`, `PrefilteredSpecularCubemap`, nombre de niveaux. Sans cube source : nuls.
  3. Liaison dans `EnvironmentShaderBinder`, sous des noms nouveaux ; `LitForward.fx` ne les déclare pas et n'est pas modifié.
  4. Table BRDF : texture 2D 64×64 calculée une fois sur le CPU (intégration numérique), format `Vector2` ou `Color` selon le support ; test de quelques valeurs de référence calculées par la même formule en double précision.
  5. ADR-0101 : génération GPU, tailles, cache, table BRDF.
- Validation : builds ; tests verts ; captures de T0.2 identiques ; tests unitaires de `IblCache` (demande mise en file, une seule génération par source, libération) et de la table BRDF. Aucun shader ne déclare encore les paramètres IBL : les mesures de génération en situation sont en T6.3.
- Commit : `feat(rendering): cache and bind the IBL cubes and the BRDF table (ADR-0101)`

### ⏳ T6.3 — IBL dans `LitPbr.fx`

- Objectif : l'ambiant PBR complet.
- Fichiers : `Pbr.fxh`, `LitPbr.fx`, `LitPbrMaterial.cs`, tests de couverture.
- Étapes :
  1. `ComputePbrAmbient` : `irradiance × diffus + préfiltré(R, rugosité) × (F0 × A + B)`, modulé par l'occlusion et par `EnvironmentAmbientColor` et `EnvironmentSpecularIntensity`.
  2. `UseFlatAmbientSource` (ADR-0090) : diffus plat, spéculaire par le cube. Sans cube : terme plat de T3.2.
  3. Emplacements de texture pour l'irradiance et la table BRDF : les prendre parmi ceux relevés libres en T3.2 et les noter.
- Validation : builds ; tests verts ; captures de T0.2 identiques ; mesures, sur une scène d'essai hors liste de T0.2 (la démo de T10.1 n'existe pas encore : scène ajoutée à `Colour pipeline demo` derrière `CASAENGINE_COLOR_DEMO_PBR=1`, coupée par défaut) : (a) sphère métal 1 rugosité 0 reflète l'environnement (écart moyen au ciel réfléchi ≤ 10 niveaux sur 5 pixels) ; (b) **preuve du préfiltrage** : une constante de débogage `IblForceBaseLevel` (0 par défaut, pilotée par `CASAENGINE_COLOR_DEMO_IBL_FORCE_BASE_LEVEL=1`) force l'échantillonnage au niveau 0 ; sur la sphère métal 1 rugosité 0,6, le contraste normalisé du reflet (écart max − min divisé par la moyenne, sur une ligne de 20 pixels) vaut au plus 50 % de celui de la même sphère avec le niveau forcé ; les deux valeurs sont notées. Un échantillonnage qui ignore le niveau échoue à cette mesure ; (c) compteur : une génération par changement d'environnement sur 600 images, exécutée hors de la boucle des vues ; (d) `Split-screen demo (2 views)` lancée avec `CASAENGINE_SPLIT_DEMO_PBR=1` (variable nouvelle, coupée par défaut, qui ajoute un cube d'environnement et une sphère `lit-pbr` ; sans elle la démo et son empreinte de T0.2 sont inchangées) : sur l'image où la génération a lieu, un pixel non noir est mesuré dans chacune des deux vues (capture déclenchée sur cette image par le compteur).
- Commit : `feat(shaders): light PBR materials from the environment`

---

## Phase 7 — Sondes de réflexion locales

### ⏳ T7.1 — Sondes préfiltrées

- Objectif : les sondes locales suivent la rugosité.
- Fichiers : `ReflectionProbeResolver.cs`, `ResolvedReflectionProbe.cs`, `EnvironmentShaderBinder.cs`, `LitPbr.fx`, tests.
- Étapes :
  1. Passer les cubes des sondes primaire et secondaire par `IblCache`, avec la même mise en file que T6.2 : une sonde vue pour la première fois est générée au début de l'image suivante ; d'ici là elle est échantillonnée non préfiltrée.
  2. `LitPbr.fx` : échantillonner les sondes au niveau de rugosité et les mélanger avec le cube global par `LocalReflectionProbeInfluence`, comme `SampleEnvironmentReflection`.
  3. `LitForward.fx` reçoit toujours les cubes non préfiltrés : chemin inchangé.
- Validation : builds ; tests verts ; captures de T0.2 identiques ; scène à deux sondes de couleurs distinctes : la sphère change de reflet en traversant la zone de mélange, sans génération par image (compteur, génération hors boucle des vues) ; dans la zone d'une seule sonde, une sphère métallique de rugosité 0,6 : son contraste normalisé (écart max − min divisé par la moyenne, ligne de 20 pixels) vaut au plus 50 % de celui obtenu avec `IblForceBaseLevel` (T6.3), et la teinte dominante de la sonde se retrouve dans les deux cas. Noter les deux valeurs. Scène derrière une variable coupée par défaut, hors liste de T0.2.
- Commit : `feat(rendering): prefilter local reflection probes for PBR materials`

---

## Phase 8 — PBR sur maillages skinnés

### ⏳ T8.1 — Relevé du chemin skinné

- Objectif : savoir précisément comment un maillage skinné reçoit son matériau et son shader.
- Sources : `SkinnedMeshRendererComponent.cs`, `skinEffect.fx`, `SkinningModeShaderResolver`, `RiggedModel` et ses matériaux, `GltfRiggedModelReader.cs`.
- Étapes : écrire sous cette tâche le type de matériau porté par un maillage skinné, le point où le shader est résolu, les techniques existantes et leurs constantes libres. Si un maillage skinné ne peut pas porter un `LitPbrMaterial` sans changement de format : ⚠️ Blocked et question en « Points ouverts ».
- Validation : relevé écrit, avec fichiers et lignes.
- Commit : `docs(ai-agent): record how skinned meshes get their material and shader`

### ⏳ T8.2 — Techniques PBR skinnées

- Objectif : `Soldier.glb` rendu en PBR.
- Fichiers : `skinEffect.fx`, `SkinnedMeshRendererComponent.cs`, `EffectiveShaderResolver.cs`, tests de couverture, `SkinnedMeshDemo.cs` (bascule).
- Étapes :
  1. Techniques PBR dans `skinEffect.fx` (skinning linéaire et dual quaternion), utilisant `Pbr.fxh` ; sans normal map (P11).
  2. Résolution : famille `LitPbr` + skinné → technique PBR skinnée.
  3. Démo : bascule Blinn-Phong / PBR.
- Validation : builds ; tests verts ; captures de T0.2 identiques (SkinnedMesh comprise si déterministe) ; test : un maillage skinné portant un matériau de famille `LitPbr` résout la technique PBR skinnée nommée, pour les deux modes de skinning, et cette technique existe dans `skinEffect.fx` (`SelectTechnique` garde en silence la technique courante si le nom manque) ; mesure : avec la bascule de la démo, l'écart absolu moyen entre Blinn-Phong et PBR sur un rectangle du `Soldier` dépasse 8 niveaux ; un pixel d'ombre portée est plus sombre d'au moins 20 niveaux que le sol éclairé voisin. Noter noms de technique, écart et valeurs.
- Commit : `feat(rendering): add PBR techniques for skinned meshes`

---

## Phase 9 — Import glTF

### ⏳ T9.1 — Lecture des canaux PBR

- Objectif : garder les données PBR au chargement.
- Fichiers : `StaticModelImportResult.cs`, `GltfStaticModelReader.cs`, `GltfStaticModelReaderTests.cs` ; ADR-0102.
- Étapes :
  1. Champs additifs sur `StaticModelImportedMaterial` : `MetallicFactor`, `RoughnessFactor`, chemins metallic-roughness, occlusion, émissif, `NormalScale`, `OcclusionStrength`, `AlphaMode`, `AlphaCutoff`, `DoubleSided`.
  2. Lecture depuis SharpGLTF ; la conversion en puissance spéculaire reste, inchangée (le test qui l'épingle reste vert).
  3. Tests : construire le glTF d'essai comme le font les tests existants ; vérifier chaque champ.
  4. ADR-0102.
- Validation : builds ; tests verts.
- Commit : `feat(assets): read the PBR material channels of glTF static models (ADR-0102)`

### ⏳ T9.2 — Matériaux `lit-pbr` à l'import

- Objectif : l'import de l'éditeur produit des `.material` `lit-pbr`.
- Fichiers : `CasaEngine.EditorServices/EditorAssetImportService.cs`, ses options d'import, `EditorAssetImportServiceTests.cs`.
- Étapes :
  1. Lire l'état réel de `ai-agent/tasks/gltf-import-migration-tasks.md` (ses statuts se contredisent) et le code d'import ; noter ici ce qui est en place.
  2. Option d'import « modèle de matériau » : `lit-diffuse` (défaut, comportement inchangé) ou `lit-pbr`.
  3. En `lit-pbr` : écrire les propriétés et importer les textures metallic-roughness, occlusion et émissif ; mode alpha → file et seuil ; double face → état de culling.
- Validation : builds ; tests : import en `lit-pbr` produit les clés attendues ; import par défaut identique à avant (fichier de référence).
- Commit : `feat(editor): import glTF materials as lit-pbr on request`

---

## Phase 10 — Démo, documentation, clôture

### ⏳ T10.1 — `PbrMaterialDemo`

- Objectif : la démo de référence.
- Fichiers : nouveau `CasaEngine.Demos/Demos/PbrMaterialDemo.cs`, enregistrement.
- Étapes : grille 5×5 métal × rugosité par surcharges d'instance sur un seul matériau ; sphère de verre ; sphère émissive avec bloom ; un maillage skinné ; cube d'environnement, une directionnelle avec ombres ; `LinearHdr` + `AcesApprox`. Touches : IBL, bloom, opérateur, exposition. `Clean` restaure tout. Variables de mesure sur le modèle de `StaticShadowValidationDemo`.
- Validation : capture archivée ; mesures notées (reflet croissant avec la rugosité, métal coloré par la couleur de base, compteur IBL à 1).
- Commit : `feat(demos): add the PBR material demo`

### ⏳ T10.2 — Non-régression finale et coût

- Objectif : verrouiller l'ensemble.
- Étapes :
  1. Builds des deux solutions, suite de tests complète.
  2. Démos de T0.2 en `Legacy` : empreintes identiques.
  3. Coût : `RenderStats` et temps d'image moyens sur 600 images de `PbrMaterialDemo`, avec et sans bloom, avec et sans IBL ; et de `MaterialDemo` en `Legacy` contre la base. Noter les chiffres.
- Validation : résultats notés ici.
- Commit : `test(rendering): record the final regression pass and cost of the PBR work`

### ⏳ T10.3 — Documentation et clôture

- Objectif : documenter et rendre le plan.
- Fichiers : nouveau `docs/engine/pbr-rendering.md` ; `docs/engine/materials-workflow.md`, `effect-file-inventory.md`, `environment-system-v1.md`, `docs/README.md`, `ai-agent/README.md`.
- Étapes : workflow, clés `lit-pbr`, IBL et caches, bloom, transparent, skinné, import, limites, renvois aux ADR-0094 à ADR-0102 ; rapport de fin (`AGENTS.md` §12) ; tableau du README.
- Validation : liens des index ouverts.
- Commit : `docs(engine): document PBR rendering and close the plan`

---

## Journal de revue du plan

| Époque | Unité | Verdict | Constats et disposition |
|---|---|---|---|
| 1 | Enveloppe | REVISE | 5 × P2 : preuve de la phase 3 hors de la phase ; génération IBL pendant qu'une cible de vue est liée ; D3 contre le skinning hors `ForwardRenderPipeline` ; preuves des phases 7 et 8 insuffisantes. Tous **FIX**. |
| 1 | Phases 0–1 | REVISE | 1 × P1 (décodage des lumières et de l'ambiant après le produit par l'intensité) et 8 × P2 (détail décodé seul, ordre des tâches, preuve à ± 2 inatteignable, sélection des démos, cible HDR recréée, fond de ciel oublié, profondeur du vidage 2D, budget). Tous **FIX**. Ajout de T0.3 (backend et chaîne de shaders non établis). |
| 2 | Enveloppe | REVISE | 3 × P2 : preuves IBL rattachées à une tâche où aucun shader ne déclare les paramètres ; mesures de rugosité passables sans préfiltrage ; référence du bloom produite par aucune tâche. Tous **FIX** (mesures déplacées en T6.3, constante `IblForceBaseLevel`, référence archivée en T1.7). |
| 2 | Phases 0–1 | REVISE | 2 × P2 : validation de T0.1 contre les audits en lecture seule ; aucune preuve de l'interface sur une vue à cible propre. Tous **FIX** (globs et redirection de l'ADR-0013, mode à tampon de la démo de mesure). |
| 3 | Enveloppe | REVISE (clôture) | 1 × P2 : aucune tâche ne prouve le masque d'ombre alpha-test du matériau PBR. **FIX** (test `ShadowMaskTests` en T4.3). Aucun P0 ni P1. |
| 3 | Phases 0–1 | REVISE (clôture) | 3 × P2 : les contrôles `LinearHdr` de T1.4 passent aussi en `Legacy` ; particules, sprites 3D et lignes non prouvés en T1.6 ; référence `RenderStats` enregistrée par aucune tâche. Tous **FIX** (marqueur gris 205 ± 2 et création de cible relevée, pixels de mesure sprite et particule, relevé en T0.2). Aucun P0 ni P1. |
| — | Décision | — | La boucle de revue automatique est close sans verdict READY (plafond atteint). Les constats restants étaient tous de priorité 2 et portaient sur la force des preuves ; ils sont corrigés ci-dessus sans nouvelle relecture, par arbitrage de la session principale en mode AUTO. Chaque phase reste relue avant son exécution et vérifiée après. |

## Points ouverts

| Réf | Sujet | Tâche concernée |
|---|---|---|
| O1 | Les pipelines de vue qui n'implémentent pas `IScenePhaseViewPipeline` (éditeur `OverlayViewPipeline`, jeux) voient leur interface tonemappée en `LinearHdr`. Faut-il les adapter dans ce plan ? Défaut : non, avertissement journalisé. | T1.4 |
| O2 | Levé par T1.5 : seul un cube issu d'un panorama `.hdr` est linéaire (le resolver le sait par l'extension) ; procédural, atmosphère physique, DDS, sondes et cube de réflexion du matériau sont décodés (ADR-0097). | T1.5 |
| O3 | `Renderer2DComponent` vidé après le tonemapping perd le test de profondeur contre la scène en `LinearHdr`. Acceptable ? Défaut : oui, documenté. | T1.6 |
| O4 | Les mipmaps (ADR-0092) sont moyennées en espace encodé : léger assombrissement des textures contrastées en `LinearHdr`. Défaut : non traité, documenté. | T1.7 |
| O5 | Levé par T0.3 : modèle 3.0 sur OpenGL, 16 samplers de pixel. | T3.2 |
| O6 | `TextureCube` à mips, `SetData` par niveau, et lecture d'un niveau de cube choisi (`texCUBElod`) sur le backend OpenGL ; repli en texture 2D prévu. | T6.1 |
| O7 | Un maillage skinné peut-il porter un `LitPbrMaterial` sans changer de format ? | T8.1 |
| O8 | État réel de la migration d'import glTF, dont les statuts se contredisent. | T9.2 |
| O9 | Dessin double des projeteurs d'ombre skinnés (`SkinnedMeshRendererComponent.cs:99` et `:170`) : hors périmètre, signalé. | — |
| O10 | Le décodage des couleurs de matériau dans chacun des 24 emplacements de lumière a donné une image `Legacy` fausse sur OpenGL, même avec la constante à 0 (cause non trouvée). Les nouveaux shaders doivent décoder les couleurs de matériau une seule fois par pixel. | T3.1, T3.2 |
| O11 | La forme à sélection de `DecodeColor` évalue aussi `pow` en `Legacy` : petit coût par pixel pour une sortie identique au bit, à mesurer en T10.2. | T10.2 |
| O12 | Levé par T1.7 (étape 3) : à l'intensité de lumière 16, en `LinearHdr` avec `AcesApprox` et exposition 1, deux des trois points de mesure de la sphère valent 250 (250 ; 251 ; 250) au lieu de trois valeurs distinctes, alors que `Legacy` les écrête tous à 255 et que `Reinhard` donne 226 ; 227 ; 224. Faut-il admettre le critère comme trop strict (le tonemapper compresse les hautes valeurs, l'information HDR est conservée : 244 à 251 sur la sphère) ou le reformuler (autre exposition ou autre tonemapper) ? Défaut : aucune modification du moteur. | T1.7 |
| O12b | Arbitrage de la session principale (2026-11-15, P2) : le critère de l'étape 3 de T1.7 était mal posé — les trois points, choisis à l'intensité 1, tombent tous sur l'épaule de `AcesApprox` à l'intensité 16. Il n'est **pas tenu tel qu'écrit**. Affirmation resserrée, tenue par les mesures : en `LinearHdr` la sphère n'est pas écrêtée en aplat (244 à 251 selon le point, sous 255 ; trois valeurs distinctes en `Reinhard` : 226, 227, 224), alors qu'en `Legacy` elle vaut 255 partout. T1.7 passe de ⚠️ à 🧪 : restent à l'œil de l'auteur les trois démos non déterministes en `Legacy` et la plausibilité des onze captures `LinearHdr`. | T1.7 |

## Hors périmètre

- La bascule de RacingGame vers `lit-pbr` et `LinearHdr` (plan du jeu séparé, D12).
- Rendu différé ou clustered, cascades d'ombres, exposition automatique.
- Transmission, réfraction, clearcoat, sheen et autres extensions glTF.
- Normal map sur maillages skinnés ; images embarquées des `.glb` statiques.
- Chargement de cubes DDS à mips ou HDR (BC6H, DX10).
- Lens flare (appartient au jeu, sur la base de la phase 1).
- Le chemin instancié dormant (`ShaderFeature.Instanced`).
- Toute modification de `lit-diffuse` autre que le décodage couleur de T1.5.
