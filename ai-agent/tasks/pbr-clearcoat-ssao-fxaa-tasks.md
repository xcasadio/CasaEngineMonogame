# Plan agent IA — Clear coat, occlusion ambiante d'écran et anti-crénelage FXAA

> Suite du plan [pbr-rendering-tasks.md](pbr-rendering-tasks.md) (exécuté, non archivé : quatre tâches 🧪 attendent l'auteur).
> Demande de l'auteur du 2026-11-15 : « établis le plan et exécute-le de manière autonome ».
> La partie jeu (brancher le PBR dans RacingGame) est hors de ce plan : l'auteur tranche d'abord, cas par cas, les comportements accidentels de l'original.

## Objectif

Ajouter au moteur trois fonctions de rendu, toutes **désactivées par défaut** et sans effet sur le chemin `Legacy` :

1. **FXAA** : anti-crénelage d'écran après le tonemapping, avant l'interface.
2. **Clear coat** : une couche de vernis sur le matériau `lit-pbr` (peinture de voiture).
3. **SSAO** : occlusion ambiante d'écran, à partir d'une pré-passe de profondeur.

Preuve de fin : avec les trois fonctions coupées, toutes les empreintes de référence sont identiques ; chaque fonction activée tient ses mesures chiffrées ; les deux solutions compilent ; la suite de tests est verte.

## État vérifié du dépôt (2026-11-15)

Relevé en lecture seule par quatre agents (`scout`), commit `674dd64c`, branche `chantier/pbr-clearcoat-ssao-fxaa`.

- **Chaîne d'image `LinearHdr`** (`CasaEngine/Framework/Rendering/RenderPipeline.cs`, `RenderHdrView` :651-704) : scène → `ApplyBloom` (:679-682) → `BindHdrDestination` (:685) → `DrawTonemap` (:686, `SpriteBatch` + `Tonemap.fx`, `PointClamp`) → `FlushPostTonemapRenderers` (:690) → composition de l'interface (:694-695). Le tonemapping écrit directement dans la destination (tampon d'affichage, surface de vue, `sceneRt` ou cible retenue ; `ColorPipelinePlanner.cs:49-73`). Il n'existe pas de cible LDR intermédiaire.
- **`Legacy`** : pas de cible HDR ni de tonemapping (`ColorPipelinePlanner.cs:51-53`) ; les vues dessinent directement dans la destination.
- **Cibles** : HDR `HalfVector4` + `Depth24Stencil8` (`RenderPipeline.cs:61, :625`), repli `HdrBlendable` puis `Color` (:628-632). Mise en commun : `RenderTargetPool.Shared`, clé (largeur, hauteur, format, profondeur). Aucun multi-échantillonnage (`CasaEngineGame.cs:191`).
- **Profondeur** : la profondeur de scène n'est échantillonnable nulle part ; aucune cible de profondeur linéaire ni de normales ; aucun shader ne lit la position d'écran. La profondeur d'ombre est écrite en couleur par `ShadowDepth.fx` (`ShadowDepth_Solid`, `ShadowDepth_Textured` avec découpe alpha), choisie dans `Draw/ShadowPass.cs:112-114` ; les maillages skinnés ont `RiggedModelShadowDepth` et sa variante dual quaternion (`SkinnedMeshRendererComponent.cs:501-503`).
- **Passes 3D** : `ForwardRenderPipeline` = Shadow, Sky, Opaque, Transparent (`ForwardRenderPipeline.cs:27-30`), `InsertPass` (:51-62), rappel après l'ombre utilisé par le rendu skinné (:101-104). La liste est possédée par `StaticMeshRendererComponent` (:459).
- **Bloom** (modèle à suivre) : `PostProcess/BloomRenderer.cs`, `Bloom.fx`, réglages dans `ColorPipelineSettings.cs` (clés lues une à une, absentes = défaut ; enums par nom), copiés par `EnvironmentResolver.cs:130-132` dans `ResolvedEnvironmentSettings`, édités dans `EntityDetailsPanel.cs:761-836`, pilotés en démo par `CASAENGINE_DEMO_BLOOM*` (`DemosGame.cs:846-897`).
- **`lit-pbr`** : `Pbr.fxh` (`ComputePbrSurface` :50-70, `AccumulatePbrLight` :101-121, `EvaluatePbrLights` :220-254, 24 emplacements de lumière), `LitPbr.fx` (`ShadePbr` :449-496, somme :486-490, six techniques, 14 échantillonneurs sur 16, `LitPbr.xnb` 888 091 octets), `LitPbrMaterial.cs`, définition à 12 clés dans `BuiltInMaterialDefinitions.cs`, `MaterialCompiler.cs:332-348`. `PbrShaderCoverageTests` épingle les six noms de technique, les 14 textures, quatre `DecodeColor`, deux `ApplyAlphaTest`.
- **Chaîne OpenGL** : `SAMPLE_TEXTURE_LEVEL` ignore le niveau ; `texCUBElod` est ignoré (mesuré, ADR-0109) ; `tex2Dlod` n'est utilisé nulle part ; `ddx`/`ddy` compilent (`PsxQuad.fx:76`). `mgfxc` ne donne pas de nombre d'instructions.
- **glTF** : SharpGLTF.Core 1.0.6 a le type `MaterialClearCoat` et `ClearCoatFactor` ; le nom exact du canal n'est pas établi. `GltfStaticModelReader.ReadPbrChannels` (:174-207) et `EditorAssetImportService.CreateLitPbrMaterial` (:797-842) ne lisent pas de clear coat. L'ADR-0102 et `docs/engine/pbr-rendering.md` déclarent le clear coat hors périmètre.
- **Base de référence** (ré-épinglée après la troisième fusion, journal du plan PBR) : `capture-v2.sh` `Legacy` — material `b73dfdd2684a8615`, environment `0cf97b7e08cc0871`, static-model `312dda61e950f6b9`, static-shadow `add5db24fbd43814`, render-to-texture `d45bc1b2d6630553`, ui-overlay `307d01b87c81303d`, frozen-frame `eb25f3917cfa6a0c`, tile-map `355772772016e47f` (skinned, particles, split-screen non déterministes) ; `lin-v2.sh` `LinearHdr` `0ba208b099b28b67ebf7cfbf4293c6fac4a45f9241698fbfcaae8a3bef05c39b`. Scripts sous `CasaEngine/artifacts/pbr-baseline/` (ignorés par git) : `capture-v2.sh`, `lin-v2.sh`, `pbrdemo.sh`, `framestats.sh`.
- **Tests** : 5941 verts. `CasaEngine.Tests` n'est dans aucune solution : toujours lancer `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj` sans `--no-build`. Tests GPU : `GpuDeviceHost.Instance`, collection `GpuDeviceCollection`. L'hôte de test s'interrompt parfois sans test en échec (point ouvert O14 du plan PBR) : relancer jusqu'à un passage complet.
- **Prochain ADR libre** : 0112.

## Décisions verrouillées

| Réf | Décision |
| --- | --- |
| D1 | Anti-crénelage : **FXAA** (réponse de l'auteur, 2026-11-15). Pas de TAA ni de MSAA dans ce plan. |
| D2 | Peinture de voiture : **clear coat** sur un diélectrique (réponse de l'auteur). Le clear coat entre donc dans le périmètre PBR, ce qui amende l'ADR-0102. |
| D3 | Exécution autonome demandée par l'auteur ; la partie jeu attend son arbitrage et reste hors de ce plan. |

Points arbitrés par l'agent, à valider par l'auteur :

| Réf | Arbitrage |
| --- | --- |
| P1 | Les trois fonctions n'agissent que pour une vue qui rend réellement par la cible HDR (`LinearHdr`). En `Legacy` les réglages sont ignorés : le chemin `Legacy` reste identique au bit près. |
| P2 | Tout est coupé par défaut ; les réglages sont des clés additives de `ColorPipelineSettings` (FXAA, SSAO) et de la définition `lit-pbr` (clear coat). Un fichier existant se relit sans changement. |
| P3 | FXAA : le tonemapping écrit dans une cible LDR `Color` mise en commun, puis la passe FXAA écrit dans la destination, **avant** `FlushPostTonemapRenderers` et l'interface, qui ne sont donc pas filtrés. Sans FXAA, le chemin actuel est inchangé (pas de cible intermédiaire). |
| P4 | Clear coat : deux scalaires, `clear_coat` (0 à 1, défaut 0) et `clear_coat_roughness` (défaut 0,1). Second lobe spéculaire GGX, F0 = 0,04, normale géométrique (pas de normal map de vernis), visibilité de Kelemen ; la couche de base est atténuée par `1 − clear_coat × F`. Pas de texture de vernis : `LitPbr.fx` reste à 14 échantillonneurs. Le lobe s'applique à l'éclairage direct et à l'IBL (atlas préfiltré lu à la rugosité du vernis). |
| P5 | Clear coat absent du PBR skinné (`skinEffect.fx`) : limite écrite. |
| P6 | SSAO : pré-passe de profondeur linéaire de vue dans une cible flottante mise en commun (géométrie opaque et à découpe alpha, statique et skinnée), normales reconstruites à partir de la profondeur, bruit calculé (pas de texture), flou séparable sensible à la profondeur, puis **multiplication de la cible HDR** par l'occlusion après la géométrie opaque et avant la géométrie transparente. Si ce point d'insertion n'existe pas pour toute la géométrie opaque, la multiplication se fait entre la scène et le bloom, et la limite (le transparent est assombri aussi) est écrite. |
| P7 | L'occlusion multiplie toute la couleur opaque (direct compris), comme le fait un SSAO d'écran classique en rendu forward. Une occlusion limitée au terme ambiant demanderait un échantillonneur d'écran dans chaque shader éclairé : hors de ce plan. |

## Règles d'exécution pour l'agent

- **Branche dédiée `chantier/pbr-clearcoat-ssao-fxaa`**, créée depuis `main` (`674dd64c`). Ne jamais committer sur `main`.
- **Une seule tâche à la fois.** Avant de commencer une tâche, remplacer son icône `⏳` par `🚧`. À la fin, lancer la validation indiquée, remplacer l'icône par `✅`, `🧪` ou `⚠️`, ajouter une courte note de validation sous la tâche (valeurs mesurées), puis **créer un commit dédié** qui inclut la mise à jour de ce fichier.
- **Un commit par tâche**, atomique et compilable, message en anglais au format `type(area): summary`.
- **Ne jamais pousser.** Le merge sur `main` reste une décision humaine.
- **Ne rien inventer** : toute API, tout fichier, toute règle utilisée existe dans le dépôt, vient d'une réponse de l'auteur, ou d'une doc officielle citée (URL). Sinon : passer la tâche en ⚠️ Blocked, écrire la question dans « Points ouverts », et **s'arrêter**. Les numéros de ligne de ce plan datent du 2026-11-15 : les revérifier avant usage.
- **Build obligatoire** avant ✅ : `dotnet build CasaEngine.MonoGame.sln` et `dotnet build CasaEngine.Editor.MonoGame.sln`. **Tests** : `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj --blame-hang-timeout 5m` (jamais `--no-build`).
- Si une vérification visuelle ou manuelle manque, utiliser `🧪 Needs testing` et noter précisément ce qui manque.
- **Ne jamais laisser une tâche en 🚧** à la fin d'une session.
- **Ne jamais indexer** les modifications préexistantes de l'auteur (` M MGUI`) : `git add` fichier par fichier.
- **Langue** : ce plan en français ; code, messages de commit, docs de `docs/` et ADR en anglais.
- **ADR** : chaque décision d'architecture, de format ou d'API est enregistrée dans `docs/decisions/` (skill `adr`), statut `Proposed`, dans le commit de la tâche. Si `origin/main` a pris un numéro entre-temps, prendre le suivant libre.
- **Non-régression, fonctions coupées** : toute tâche qui touche un shader ou le chemin d'image relance `capture-v2.sh`, `lin-v2.sh` et la capture de validation de `pbrdemo.sh`, et compare aux empreintes de T0.1. Une différence bloque la tâche.
- Rappel moteur : pas d'allocation, de LINQ, de closure ni de journalisation par image ; restaurer tout état GPU modifié (cibles, viewport, états, échantillonneurs touchés) ; le runtime ne dépend pas de l'éditeur ; sérialisation additive.
- **Budget** : par tâche, deux passes de correction au plus sur une différence d'empreinte, un test rouge ou une mesure manquée ; à la troisième, ⚠️ Blocked et arrêt de la phase. Les phases indépendantes peuvent continuer.
- **Revue** : chaque phase est relue avant exécution (lecture seule) et vérifiée après par un agent sans le contexte de l'auteur du code.
- Une mesure qui ne peut pas échouer quand sa fonction est coupée est un défaut de la mesure, à corriger avant de valider.

## Légende des statuts

- ⏳ Todo
- 🚧 In progress
- 🧪 Needs testing
- ✅ Done
- ⚠️ Blocked

## Validation globale

- Les deux solutions compilent ; la suite complète est verte.
- Fonctions coupées : empreintes `Legacy`, référence `LinearHdr` et capture de validation de la démo PBR identiques à T0.1.
- Chaque fonction activée tient les mesures de sa tâche, notées avec leurs valeurs.
- ADR-0112 à ADR-0115 écrits et indexés ; `docs/engine/` à jour.

---

## Phase 0 — Base et périmètre

### ⏳ T0.1 — Base de référence et amendement du périmètre

- Objectif : figer la base de ce plan et enregistrer l'élargissement du périmètre.
- Fichiers : ce plan ; `docs/decisions/0112-…md` (nouveau), `docs/decisions/README.md`, `docs/decisions/0102-…md` (ligne de statut « amended by ADR-0112 » seulement), `ai-agent/README.md` (ligne de ce plan).
- Étapes :
  1. Lire les scripts `capture-v2.sh`, `lin-v2.sh`, `pbrdemo.sh`, `framestats.sh` ; noter ici leur usage.
  2. Construire (`Debug`), lancer `capture-v2.sh`, `lin-v2.sh` et deux fois la capture de validation de `pbrdemo.sh`. Les empreintes `Legacy` et `LinearHdr` doivent être celles de « État vérifié » ; sinon ⚠️ Blocked. Noter l'empreinte de la capture de validation PBR (les deux passages doivent être identiques).
  3. ADR-0112 : « PBR scope extends to clear coat, screen-space ambient occlusion and FXAA », qui amende l'ADR-0102 et reprend P1 et P2.
- Validation : empreintes notées dans un tableau sous cette tâche ; ADR indexé ; aucun fichier de code modifié.
- Commit : `docs(decisions): extend the PBR scope to clear coat, SSAO and FXAA (ADR-0112)`

---

## Phase 1 — FXAA

### ⏳ T1.1 — Réglage d'anti-crénelage

- Objectif : un réglage additif, sans effet tant que la passe n'existe pas.
- Fichiers : nouveau `CasaEngine/Framework/Rendering/Environment/AntiAliasingMode.cs` ; `ColorPipelineSettings.cs`, `ResolvedEnvironmentSettings.cs`, `EnvironmentResolver.cs`, `CasaEngine.Editor/Controls/EntityDetailsPanel.cs`, `CasaEngine.Demos/DemosGame.cs`, `ColorPipelineSettingsTests.cs`.
- Étapes :
  1. `enum AntiAliasingMode { None, Fxaa }` ; propriété `ColorPipelineSettings.AntiAliasing` (défaut `None`), clé `anti_aliasing` écrite par nom comme `tonemapper` ; `IsDefault`, `ResetToDefaults`, `Load`, `Save`, `CopyFrom` complétés.
  2. Copie dans `ResolvedEnvironmentSettings` par `EnvironmentResolver`.
  3. Éditeur : liste déroulante à la suite du bloom, sur le modèle de `Tonemapper`.
  4. Démo : `CASAENGINE_DEMO_AA=Fxaa|None`, lue là où le sont les variables de bloom.
- Validation : builds ; tests : valeur par défaut, aller-retour, un fichier sans la clé se relit `None`, `color_pipeline` reste omis quand tout est par défaut ; non-régression fonctions coupées.
- Commit : `feat(rendering): add the anti-aliasing setting of the colour pipeline`

### ⏳ T1.2 — Passe FXAA

- Objectif : lisser les bords de la scène après le tonemapping, sans toucher à l'interface.
- Fichiers : nouveaux `CasaEngine/Content/Shaders/Fxaa.fx`, `CasaEngine/Framework/Rendering/PostProcess/FxaaRenderer.cs` ; `Content.mgcb`, `BuiltInShaderCatalog.cs`, `ShaderParameterNames.cs`, `CasaEngineGame.cs` (chargement, rechargement à chaud), `RenderPipeline.cs`, `docs/engine/effect-file-inventory.md`, `docs/engine/color-pipeline.md` ; tests ; `docs/decisions/0113-…md`.
- Étapes :
  1. `Fxaa.fx` : passe de pixel seule sur le quad de `SpriteBatch` (comme `Tonemap.fx`), luminance calculée à partir du RVB encodé, détection de contraste local, direction du bord, mélange le long du bord ; lectures par `tex2D` uniquement, filtrage `LinearClamp`, paramètre de taille de texel. Écrire l'algorithme dans l'en-tête du fichier ; ne copier aucun code sous licence.
  2. `RenderHdrView` : si `AntiAliasing == Fxaa` et le shader est chargé, acquérir dans `RenderTargetPool.Shared` une cible `Color` sans profondeur à la taille du rectangle de la vue, y tonemapper, puis lier la destination comme aujourd'hui (`BindHdrDestination`) et dessiner la passe FXAA dans le rectangle de la vue ; relâcher la cible. Sinon, chemin actuel inchangé. `FlushPostTonemapRenderers` et l'interface restent après.
  3. États GPU sauvegardés et restaurés comme le fait `BloomRenderer`.
  4. ADR-0113 (place de la passe, cible intermédiaire, `LinearHdr` seulement).
- Validation :
  - builds ; tests de couverture du shader (technique, paramètres, entrée `Content.mgcb`, catalogue) ; test d'ordre : tonemapping < FXAA < `FlushPostTonemapRenderers` < interface ;
  - test GPU : une image à bord oblique net (noir/blanc) passée par la passe : le nombre de pixels de niveau intermédiaire (entre 32 et 223) le long du bord augmente d'au moins 50 % ; une zone unie reste identique à ± 1 niveau ; l'image de sortie a la taille de l'entrée, sans décalage (le centre d'un carré blanc reste au même pixel) ;
  - capture `lin-v2.sh` avec `CASAENGINE_DEMO_AA=Fxaa` : sur un segment traversant le bord d'un objet, le nombre de pixels intermédiaires augmente ; les pixels du texte d'interface (rectangle nommé) sont identiques avec et sans FXAA ; deux captures identiques ;
  - écran partagé et rendu vers texture en `LinearHdr` + FXAA : pas d'exception, chaque vue reste dans son rectangle (mesure d'un pixel de chaque vue) ;
  - non-régression fonctions coupées.
- Commit : `feat(rendering): add FXAA after the tonemapping (ADR-0113)`

---

## Phase 2 — Clear coat

### ⏳ T2.1 — Lobe de vernis dans le shader et le matériau

- Objectif : `lit-pbr` rend une couche de vernis.
- Fichiers : `Pbr.fxh`, `LitPbr.fx`, `ShaderParameterNames.cs`, `LitPbrMaterial.cs`, `BuiltInMaterialDefinitions.cs`, `MaterialCompiler.cs`, registre de l'éditeur si une clé y est listée à la main, `PbrShaderCoverageTests.cs`, tests du matériau et de la définition, nouveau test GPU ; `docs/decisions/0114-…md`, `docs/engine/pbr-rendering.md`, `docs/engine/materials-workflow.md`.
- Étapes :
  1. Constantes `ClearCoat` et `ClearCoatRoughness` ; lobe selon P4 dans l'éclairage direct (`Pbr.fxh`) et dans l'ambiant IBL (`LitPbr.fx`, atlas préfiltré lu à la rugosité du vernis, table BRDF à F0 0,04) ; base (diffus et spéculaire, direct et ambiant) multipliée par `1 − ClearCoat × F_vernis`. En transparence, le lobe de vernis suit la règle du spéculaire (non multiplié par l'alpha).
  2. Avec `ClearCoat = 0` le résultat doit être **identique au bit près** à l'actuel : si la formule ne le garantit pas numériquement, garder le calcul actuel derrière un test `ClearCoat > 0`.
  3. Si `LitPbr.fx` ne compile plus (taille du shader) : réduire le lobe direct du vernis aux lumières directionnelles et à l'IBL, l'écrire comme limite dans l'ADR ; si cela ne suffit pas, ⚠️ Blocked.
  4. `LitPbrMaterial.ClearCoat`, `ClearCoatRoughness` (défauts 0 et 0,1), liés dans `Bind` ; clés `clear_coat` et `clear_coat_roughness` dans la définition (scalaires, surchargeables par instance), lues par le compilateur ; un matériau sans ces clés se compile avec les défauts.
  5. ADR-0114.
- Validation :
  - builds ; tests de couverture mis à jour (constantes, comptes épinglés justifiés un par un) ; définition à 14 clés ; aller-retour d'un `.material` avec et sans les clés ;
  - test GPU (cible `HalfVector4`, sur le modèle de `LitPbrTransparentGpuTests`) : (a) `ClearCoat = 0` : pixel égal à celui d'un matériau sans vernis ; (b) base noire non métallique, rugosité de base 1, `ClearCoat = 1`, rugosité de vernis 0,1, lumière dans l'axe du reflet : le pixel dépasse d'au moins un facteur 5 celui de `ClearCoat = 0` ; (c) même montage, `ClearCoat = 0,5` : valeur entre (a) et (b), croissante ; (d) base diffuse blanche vue de face, `ClearCoat = 1` : le diffus baisse d'environ 4 % (entre 2 % et 6 %) par rapport à `ClearCoat = 0`, hors reflet ;
  - non-régression fonctions coupées (`pbrdemo.sh` compris : empreinte de T0.1).
- Commit : `feat(materials): add a clear coat layer to lit-pbr (ADR-0114)`

### ⏳ T2.2 — Clear coat à l'import glTF et dans la démo

- Objectif : lire le vernis d'un glTF et le montrer.
- Fichiers : `StaticModelImportResult.cs`, `GltfStaticModelReader.cs`, `EditorAssetImportService.cs`, leurs tests ; `CasaEngine.Demos/Demos/PbrMaterialDemo.cs`, `docs/engine/pbr-rendering.md`, ADR-0111 (une ligne) et ADR-0114 (import).
- Étapes :
  1. **Preuve d'abord** : un test construit un glTF avec un clear coat (facteur 0,8, rugosité 0,2) par l'API de SharpGLTF 1.0.6 et le relit. Si l'API ne permet ni de l'écrire ni de le lire de façon établie, ne pas inventer : noter le fait, laisser l'import sans vernis, ouvrir un point, et passer à l'étape 3.
  2. Champs additifs `ClearCoatFactor`, `ClearCoatRoughnessFactor` sur `StaticModelImportedMaterial` (défaut 0) ; en import `LitPbr`, écrire `clear_coat` et `clear_coat_roughness` seulement quand le facteur est > 0. Chemin `LitDiffuse` inchangé (référence versionnée de T9.2 du plan PBR toujours identique).
  3. Démo : derrière `CASAENGINE_PBR_DEMO_CLEARCOAT=1`, une rangée de cinq sphères (base rouge diélectrique, rugosité 0,6, `clear_coat` 0 ; 0,25 ; 0,5 ; 0,75 ; 1). Sans la variable, la démo est inchangée.
- Validation : builds ; tests ; mesure sur la capture de validation avec la variable : la valeur maximale du reflet croît strictement de la première à la dernière sphère ; sans la variable, empreinte de T0.1 ; non-régression fonctions coupées.
- Commit : `feat(assets): read the glTF clear coat and show it in the PBR demo`

---

## Phase 3 — SSAO

### ⏳ T3.1 — Relevé et preuves préalables

- Objectif : établir, par le code et par un essai, ce dont la pré-passe et la composition ont besoin.
- Sources : `ForwardRenderPipeline.cs`, `Draw/ShadowPass.cs`, `Draw/OpaqueAndTransparentPasses.cs`, `StaticMeshRendererComponent.cs`, `SkinnedMeshRendererComponent.cs`, `DefaultViewPipeline.cs`, `RenderPipeline.cs`, `ShadowDepth.fx`, `skinEffect.fx`, les autres composants qui dessinent de la géométrie 3D opaque (terrain, particules, lignes : les lister).
- Étapes :
  1. Écrire sous cette tâche : l'ordre réel de dessin de toute la géométrie opaque et transparente d'une vue (statique, skinnée, autres) ; le point où une passe peut s'insérer après tout l'opaque et avant tout le transparent, ou la raison pour laquelle il n'existe pas ; comment `ShadowPass` parcourt la liste de dessin et choisit sa technique (modèle de la pré-passe).
  2. Essai GPU jetable (non commité, ou test conservé s'il est utile) : une cible `SurfaceFormat.Single` est-elle créée et lue en `tex2D` avec la précision attendue sur cette machine ; sinon `HalfVector4` ou `Vector4`. Noter le format retenu et son repli.
  3. Choisir et écrire le point de composition (P6, premier ou second cas).
- Validation : relevé écrit avec fichiers et lignes ; résultat de l'essai noté.
- Commit : `docs(ai-agent): record how the scene depth pre-pass and the SSAO composite fit the pipeline`

### ⏳ T3.2 — Pré-passe de profondeur de scène

- Objectif : une texture de profondeur linéaire de vue, par vue, seulement quand le SSAO est actif.
- Fichiers : nouveaux `CasaEngine/Content/Shaders/SceneDepth.fx`, `CasaEngine/Framework/Rendering/PostProcess/SceneDepthPrepass.cs` (ou sous `Draw/` selon T3.1) ; `skinEffect.fx` (techniques `RiggedModelSceneDepth` et dual quaternion), `SkinnedMeshRendererComponent.cs`, `Content.mgcb`, `BuiltInShaderCatalog.cs`, `ShaderParameterNames.cs`, `CasaEngineGame.cs`, `ColorPipelineSettings.cs` et sa chaîne (`ssao_enabled`, `ssao_radius`, `ssao_intensity`, `ssao_bias`, défauts : coupé ; 0,5 ; 1 ; 0,02), `EntityDetailsPanel.cs`, `DemosGame.cs` (`CASAENGINE_DEMO_SSAO`, `_RADIUS`, `_INTENSITY`), tests ; `docs/decisions/0115-…md`.
- Étapes :
  1. Réglages additifs, sur le modèle de T1.1.
  2. `SceneDepth.fx` : techniques `SceneDepth_Solid` et `SceneDepth_Textured` (découpe alpha), sortie = profondeur de vue divisée par le plan lointain ; fond effacé à 1.
  3. Pré-passe : quand le SSAO est actif pour la vue et que la vue rend par la cible HDR, dessiner la géométrie opaque et à découpe alpha (pas la transparente, pas le ciel) dans une cible mise en commun au format de T3.1, avec son propre tampon de profondeur ; maillages skinnés par leurs techniques dédiées. Aucune allocation par image ; états restaurés.
  4. ADR-0115 (pré-passe, format, coût : la géométrie opaque est dessinée une fois de plus).
- Validation : builds ; tests de couverture ; test GPU : un plan à distance connue donne la profondeur attendue à ± 1 % ; une découpe alpha laisse le fond à 1 dans ses trous ; SSAO coupé : la pré-passe ne s'exécute pas (compteur ou test du planificateur) et aucune cible n'est acquise ; non-régression fonctions coupées.
- Commit : `feat(rendering): add a linear scene depth pre-pass for screen-space effects (ADR-0115)`

### ⏳ T3.3 — Occlusion, flou et composition

- Objectif : assombrir les creux et les contacts.
- Fichiers : nouveaux `CasaEngine/Content/Shaders/Ssao.fx`, `CasaEngine/Framework/Rendering/PostProcess/SsaoRenderer.cs` ; `Content.mgcb`, `BuiltInShaderCatalog.cs`, `ShaderParameterNames.cs`, `CasaEngineGame.cs`, `RenderPipeline.cs` ou la liste de passes (selon T3.1), une scène de mesure dans `ColorPipelineDemo.cs` derrière `CASAENGINE_COLOR_DEMO_SSAO=1` (un cube posé sur un sol, une sphère de verre devant un fond), tests, ADR-0115 complété, `docs/engine/color-pipeline.md`, `effect-file-inventory.md`.
- Étapes :
  1. `Ssao.fx` : position de vue reconstruite à partir de la profondeur et des paramètres de projection ; normale par différences de positions voisines ; N échantillons dans l'hémisphère (N entre 8 et 16, noyau constant dans le shader), rotation par un bruit calculé à partir de la position de pixel ; test de portée ; sortie = facteur d'occlusion. Lectures par `tex2D`.
  2. Flou séparable en deux passes qui ne traverse pas les ruptures de profondeur.
  3. Composition selon T3.1 : multiplication de la cible HDR par `lerp(1, occlusion, intensité)`, état de mélange multiplicatif, tampon de profondeur non modifié ; le ciel (profondeur 1) n'est pas assombri.
  4. Cibles mises en commun ; repli : si le format flottant manque, le SSAO se coupe comme le fait le bloom.
- Validation :
  - builds ; tests de couverture et d'ordre des passes ;
  - test GPU : profondeur plane uniforme → occlusion ≥ 0,98 partout ; marche de profondeur → occlusion < 0,8 du côté creux près de l'arête ;
  - capture de la scène de mesure, constantes nommées : (1) le pixel du sol au pied du cube est plus sombre d'au moins 15 niveaux avec SSAO que sans ; (2) un pixel du sol loin de tout objet change de 3 niveaux au plus ; (3) un pixel de ciel est identique ; (4) premier cas de P6 seulement : un pixel vu à travers la sphère de verre, hors de tout creux, change de 3 niveaux au plus ; (5) deux captures identiques ; (6) `CASAENGINE_DEMO_SSAO_INTENSITY=0` rend l'image sans SSAO au niveau près ;
  - écran partagé et rendu vers texture en `LinearHdr` + SSAO : pas d'exception, pas de débordement d'une vue sur l'autre (un pixel par vue) ;
  - non-régression fonctions coupées.
- Commit : `feat(rendering): add screen-space ambient occlusion (ADR-0115)`

---

## Phase 4 — Démo, coût, documentation

### ⏳ T4.1 — Démo de référence et coût

- Objectif : tout montrer ensemble et chiffrer.
- Fichiers : `CasaEngine.Demos/Demos/PbrMaterialDemo.cs`, `CasaEngine.Demos/Content/demos-input.buttonsMapping`, `docs/engine/demos-main-menu.md`, ce plan.
- Étapes :
  1. Touches de la démo PBR : FXAA, SSAO (affichées sur la fiche) ; la rangée vernie de T2.2 visible par défaut **hors** mode de validation seulement (le mode de validation garde l'empreinte de T0.1 sans variable).
  2. Coût : `framestats.sh` en `Release`, démo PBR en validation, dans les combinaisons FXAA × SSAO × rangée vernie ; noter les temps moyens et les compteurs.
  3. Passe finale : builds, suite complète, non-régression fonctions coupées.
- Validation : une ligne de journal par configuration notée ici ; empreintes identiques à T0.1 ; basculer chaque touche dans la démo est un contrôle manuel (🧪 s'il n'est pas observé).
- Commit : `feat(demos): show clear coat, SSAO and FXAA in the PBR demo and record their cost`

### ⏳ T4.2 — Documentation et rapport

- Objectif : documenter et rendre le plan.
- Fichiers : `docs/engine/pbr-rendering.md`, `docs/engine/color-pipeline.md`, `docs/README.md`, `ai-agent/README.md`, ce plan.
- Étapes : sections clear coat, SSAO, FXAA (réglages, clés, ordre des passes, limites : `LinearHdr` seulement, vernis absent du skinné, SSAO sur toute la couleur opaque, FXAA sans effet sur l'interface) ; retirer le clear coat de la liste « hors périmètre » ; renvois aux ADR-0112 à 0115 ; rapport de fin (`AGENTS.md` §12) ; ligne du tableau de `ai-agent/README.md`. Le plan n'est archivé que si aucune tâche ne reste 🧪 ou ⚠️.
- Validation : chaque lien ajouté ou modifié pointe sur un fichier existant ; les limites sont trouvées par `rg`.
- Commit : `docs(engine): document clear coat, SSAO and FXAA`

---

## Points ouverts

| Réf | Sujet | Tâche concernée |
| --- | --- | --- |
| O1 | Le nom du canal clear coat de SharpGLTF 1.0.6 n'est pas établi ; T2.2 le prouve par un test avant tout code d'import. | T2.2 |
| O2 | Point d'insertion de la composition du SSAO entre l'opaque et le transparent : à établir par T3.1. | T3.1, T3.3 |
| O3 | Format de la cible de profondeur lisible sur cette chaîne OpenGL : à établir par T3.1. | T3.1, T3.2 |
| O4 | `LitPbr.fx` compile-t-il encore avec le lobe de vernis sur les 24 emplacements de lumière ? Repli écrit en T2.1. | T2.1 |

## Hors périmètre

- Brancher le PBR dans RacingGame (plan séparé dans le dépôt du jeu, après arbitrage de l'auteur).
- TAA, MSAA, flou de mouvement, profondeur de champ, exposition automatique.
- Réflexions d'écran, illumination globale, rendu différé ou clustered.
- Normal map et textures de vernis, anisotropie, sheen, transmission.
- Clear coat sur les maillages skinnés ; SSAO limité au terme ambiant ; SSAO et FXAA en `Legacy`.
- Les quatre tâches 🧪 et les défauts P3 reportés du plan PBR.

## Journal des revues

| Époque | Objet | Verdict | Suite |
| --- | --- | --- | --- |
