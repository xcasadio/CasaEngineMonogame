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
| P1 | **FXAA et SSAO** n'agissent que pour une vue qui rend réellement par la cible HDR (`LinearHdr`) ; en `Legacy` leurs réglages sont ignorés. Le **clear coat** est une propriété du matériau : il suit `lit-pbr` dans les deux pipelines, `Legacy` compris. Avec les trois fonctions coupées (vernis à 0), le rendu reste identique au bit près dans les deux pipelines. |
| P2 | Tout est coupé par défaut ; les réglages sont des clés additives de `ColorPipelineSettings` (FXAA, SSAO) et de la définition `lit-pbr` (clear coat). Un fichier existant se relit sans changement. |
| P3 | FXAA : le tonemapping écrit dans une cible LDR `Color` mise en commun, puis la passe FXAA écrit dans la destination, **avant** `FlushPostTonemapRenderers` et l'interface, qui ne sont donc pas filtrés. La passe ne s'exécute **que si** le pipeline de vue sépare la scène de l'interface (`composeInterfaceAfter` vrai dans `RenderHdrView`) : une vue dont le pipeline rend tout d'un bloc (les vues de l'éditeur, `OverlayViewPipeline`) n'est pas filtrée, limite écrite. Sans FXAA, le chemin actuel est inchangé (pas de cible intermédiaire). |
| P4 | Clear coat : deux scalaires, `clear_coat` (0 à 1, défaut 0) et `clear_coat_roughness` (défaut 0,1). **Direct** : second lobe spéculaire GGX, F0 = 0,04, normale géométrique (pas de normal map de vernis), visibilité de Kelemen, Fresnel de Schlick `F_d` en VoH ; la base (diffus et spéculaire) est multipliée par `1 − clear_coat × F_d`. **Ambiant avec IBL** : `E = 0,04 × A + B`, A et B lus dans la table BRDF en (NoV, `clear_coat_roughness`) ; reflet du vernis = atlas préfiltré **global** lu à `clear_coat_roughness` × `E` × `clear_coat` × `EnvironmentSpecularIntensity` × occlusion ; la base ambiante (diffus et spéculaire) est multipliée par `1 − clear_coat × E`. Le reflet du vernis ne lit pas les sondes locales (limite écrite). **Ambiant plat** (`HasIbl = 0`) : pas de reflet de vernis ; la base ambiante est multipliée par `1 − clear_coat × Schlick(0,04, NoV)`. Pas de texture de vernis : `LitPbr.fx` reste à 14 échantillonneurs. |
| P5 | Clear coat absent du PBR skinné (`skinEffect.fx`) : limite écrite. |
| P6 | SSAO : pré-passe de profondeur de vue (en unités du monde, non normalisée) dans une cible flottante mise en commun, pour la géométrie opaque et à découpe alpha des rendus statique et skinné ; normales reconstruites à partir de la profondeur ; bruit calculé (pas de texture) ; flou séparable sensible à la profondeur ; puis **multiplication de la cible HDR** par l'occlusion **entre la scène et le bloom** (`RenderHdrView`, entre `RenderScene` et `ApplyBloom`). Le rendu statique dessine son transparent avant que le rendu skinné ne dessine son opaque (`StaticMeshRendererComponent.cs:459`, `SkinnedMeshRendererComponent.cs:161-223`) : il n'existe donc pas de point « après tout l'opaque, avant tout le transparent ». Limites écrites : la géométrie transparente et les autres dessins 3D (particules, lignes, sprites, tuiles) ne sont pas dans la profondeur mais sont multipliés par l'occlusion de ce qui est derrière eux ; une vue à projection non perspective n'a pas de SSAO. |
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
- **Dépendances** : T0.1 avant tout. T1.1 → T1.2 ; T2.1 → T2.2 ; T3.1 → T3.2 → T3.3 ; T4.1 demande T1.2, T2.2 et T3.3 ; T4.2 demande T4.1. Les phases 1, 2 et 3 sont indépendantes entre elles et s'exécutent dans cet ordre.
- **Tâche ⚠️** : elle arrête **sa phase** (les tâches suivantes de la phase restent ⏳). Les autres phases continuent. T4.1, T4.2 et la validation globale se **resserrent** alors : elles ne couvrent que les fonctions livrées, et nomment dans leur note les combinaisons, mesures et ADR abandonnés. Si les trois phases sont bloquées, la phase 4 ne s'exécute pas.
- **Abandon propre** : avant de commencer une tâche, l'arbre de travail doit être égal au dernier commit, hors ` M MGUI`. Une tâche qui finit ⚠️ remet à l'état du dernier commit tous les fichiers qu'elle a modifiés (`git restore` fichier par fichier) et retire les fichiers qu'elle a elle-même créés dans cette tâche ; seul le plan (statut ⚠️, note, point ouvert) est commité.
- **Protocole de capture** : lancer les captures sans autre processus lourd (jamais pendant ni juste après `dotnet test` ou un build). Une empreinte différente est recapturée jusqu'à trois fois ; seule une différence présente aux trois recaptures bloque la tâche et compte comme une passe de correction. Les recaptures sont notées.
- **Cibles mises en commun** : toute cible acquise dans `RenderTargetPool.Shared` par une nouvelle passe voit sa clé horodatée dans `_hdrLastUsedRender` avec `_renderIndex`, comme le fait le bloom (`BloomRenderer.cs:204-212`, `RenderPipeline.cs:636`), afin que `ReleaseUnusedHdrTargets` la libère après un changement de taille. Les compteurs de création et de libération du bloom ne doivent pas compter ces cibles : chaque passe a les siens.

## Légende des statuts

- ⏳ Todo
- 🚧 In progress
- 🧪 Needs testing
- ✅ Done
- ⚠️ Blocked

## Validation globale

- Les deux solutions compilent ; la suite complète est verte.
- Fonctions coupées : empreintes `Legacy`, référence `LinearHdr` et captures de validation de la démo PBR (`LinearHdr` et `Legacy`) identiques à T0.1.
- Chaque fonction activée tient les mesures de sa tâche, notées avec leurs valeurs.
- ADR-0112 à ADR-0115 écrits et indexés ; `docs/engine/` à jour.

---

## Phase 0 — Base et périmètre

### ✅ T0.1 — Base de référence et amendement du périmètre

- Objectif : figer la base de ce plan et enregistrer l'élargissement du périmètre.
- Fichiers : ce plan ; `docs/decisions/0112-…md` (nouveau), `docs/decisions/README.md`, `docs/decisions/0102-…md` (ligne de statut « amended by ADR-0112 » seulement), `ai-agent/README.md` (ligne de ce plan).
- Étapes :
  1. Lire les scripts `capture-v2.sh`, `lin-v2.sh`, `pbrdemo.sh`, `framestats.sh` ; noter ici leur usage.
  2. Construire (`Debug`), lancer `capture-v2.sh`, `lin-v2.sh` et deux fois la capture de validation de `pbrdemo.sh`. Les empreintes `Legacy` et `LinearHdr` doivent être celles de « État vérifié » ; sinon ⚠️ Blocked. Noter l'empreinte de la capture de validation PBR (les deux passages doivent être identiques).
  3. ADR-0112 : « PBR scope extends to clear coat, screen-space ambient occlusion and FXAA », qui amende l'ADR-0102 et reprend P1 et P2.
  4. **Précision de revue** : la base comprend aussi la démo PBR en `Legacy`, que rien ne couvrait : `pbrdemo.sh <dossier> <nom> CASAENGINE_DEMO_COLOR_PIPELINE=Legacy`, deux passages identiques. Cette empreinte fait partie de la « non-régression, fonctions coupées » de toutes les tâches et de la validation globale. Si la variable n'a pas d'effet sur cette démo (la démo force son pipeline), le noter, ne pas modifier la démo dans cette tâche, et ouvrir un point : T2.1 ajoutera alors la variable de démo nécessaire avant de toucher aux shaders. L'ADR-0112 reprend P1 tel qu'il est écrit (vernis dans les deux pipelines).
- Validation : empreintes notées dans un tableau sous cette tâche ; ADR indexé ; aucun fichier de code modifié.
- **Note de validation (2026-11-15).** Builds `Debug` des deux solutions : 0 erreur (188 et 204 avertissements). Aucun fichier de code modifié. Suite de tests non relancée (aucun code touché). Les captures ont tourné sans autre processus lourd, sans recapture nécessaire.
  - Usage des scripts (sous `CasaEngine/artifacts/pbr-baseline/`, ignorés par git ; `BIN` par défaut = `CasaEngine.Demos/bin/Debug/net9.0-windows`, sauf `framestats.sh` = `bin/Release`). Tous font `cd "$BIN"` : le dossier de sortie doit être **absolu** (sinon les images tombent dans `$BIN`).
    - `capture-v2.sh <dossier> [env…]` : onze démos par titre exact, chacune deux fois (`CASAENGINE_CAPTURE_SCREENSHOT_PATH`), imprime `slug sha1 sha2 notfound exceptions files`.
    - `lin-v2.sh <dossier> <nom> [env…]` : « Colour pipeline demo » en `LinearHdr`, `AcesApprox`, exposition 1, intensité 1.
    - `pbrdemo.sh <dossier> <nom> [env…]` : « PBR material demo » avec `CASAENGINE_PBR_DEMO_VALIDATION=1`, délai de capture 12000 ms (`DELAY`).
    - `framestats.sh <dossier> <nom> <titre> [env…]` : lance avec `CASAENGINE_DEMO_FRAME_STATS=1` (mesure des mises à jour 101 à 700, puis sortie), imprime la ligne `[FrameStats]`. Ne prend aucune capture.
  - Empreintes (SHA-256, 16 premiers caractères sauf mention), `Legacy` via `capture-v2.sh` (deux passages chacun) :

    | Capture | Empreinte | Deux passages |
    | --- | --- | --- |
    | material | `b73dfdd2684a8615` | identiques |
    | environment | `0cf97b7e08cc0871` | identiques |
    | static-model | `312dda61e950f6b9` | identiques |
    | static-shadow | `add5db24fbd43814` | identiques |
    | render-to-texture | `d45bc1b2d6630553` | identiques |
    | ui-overlay | `307d01b87c81303d` | identiques |
    | frozen-frame | `eb25f3917cfa6a0c` | identiques |
    | tile-map | `355772772016e47f` | identiques |
    | skinned, particles, split-screen | non déterministes (deux passages différents : `d2eaab0dfff587d6`/`9384efdd102a7b22`, `31f5c240eb8830fa`/`05c3e412df2e4824`, `bf137ae5a2fa30d0`/`5d970c0fc495c75c`) | non comparés |
    | `lin-v2.sh` `LinearHdr` | `0ba208b099b28b67ebf7cfbf4293c6fac4a45f9241698fbfcaae8a3bef05c39b` | un passage, égal à « État vérifié » |
    | `pbrdemo.sh` validation, pipeline par défaut (`LinearHdr`) | `00847b5d4e85ef3b13002fd0bcb2f475ae06fb19b0bfa8fee33d24cf38427161` | identiques |
    | `pbrdemo.sh` validation, `CASAENGINE_DEMO_COLOR_PIPELINE=Legacy` | `a844109b0a2bfd5fd1b000a3c6e676e5bbdcb7c50b1e09b306bc5a702eb85f35` | identiques |

  - Les huit empreintes `Legacy` déterministes et l'empreinte `LinearHdr` égalent celles de « État vérifié » : pas de blocage. 0 exception, 0 « was not found » dans tous les journaux.
  - Précision de revue 4 : la variable `CASAENGINE_DEMO_COLOR_PIPELINE=Legacy` **a un effet** sur la démo PBR (journal : « Colour pipeline harness: Legacy, AcesApprox, exposure 1. » ; empreinte différente de celle par défaut, dont le journal crée la cible HDR `1024x768 HalfVector4`). Aucun point ouvert, T2.1 n'a pas à ajouter de variable.
  - ADR-0112 écrit (`docs/decisions/0112-pbr-scope-extends-to-clear-coat-screen-space-ambient-occlusion-and-fxaa.md`, `Proposed`, P1 et P2 repris), indexé dans `docs/decisions/README.md` ; ligne de statut de l'ADR-0102 (et de l'index) « amended by ADR-0112 » ; ligne de ce plan dans `ai-agent/README.md`.
- **Précisions de clôture.** Revue de clôture de la tranche (blocage P2, tous **FIX**) : le texte de T4.1 est revu dans ce commit, sans code. T4.1 exige un `dotnet build CasaEngine.MonoGame.sln -c Release` juste avant la mesure de coût et, pour chaque configuration FXAA × SSAO × rangée vernie, la ligne `[FrameStats]` accompagnée de ses compteurs d'activation (passe FXAA, pré-passe SSAO statique et skinnée, ≥ 1 si active, 0 sinon) et d'une ligne `[DemoConfig]` de la même exécution ; `DemoFrameStats.cs` et `DemosGame.cs` entrent dans ses fichiers pour les écrire sans capture. Voir T4.1.
- Commit : `docs(decisions): extend the PBR scope to clear coat, SSAO and FXAA (ADR-0112)`

---

## Phase 1 — FXAA

### ✅ T1.1 — Réglage d'anti-crénelage

- Objectif : un réglage additif, sans effet tant que la passe n'existe pas.
- Fichiers : nouveau `CasaEngine/Framework/Rendering/Environment/AntiAliasingMode.cs` ; `ColorPipelineSettings.cs`, `ResolvedEnvironmentSettings.cs`, `EnvironmentResolver.cs`, `CasaEngine.Editor/Controls/EntityDetailsPanel.cs`, `CasaEngine.Demos/DemosGame.cs`, `ColorPipelineSettingsTests.cs`.
- Étapes :
  1. `enum AntiAliasingMode { None, Fxaa }` ; propriété `ColorPipelineSettings.AntiAliasing` (défaut `None`), clé `anti_aliasing` écrite par nom comme `tonemapper` ; `IsDefault`, `ResetToDefaults`, `Load`, `Save`, `CopyFrom` complétés.
  2. Copie dans `ResolvedEnvironmentSettings` par `EnvironmentResolver`.
  3. Éditeur : liste déroulante à la suite du bloom, sur le modèle de `Tonemapper`.
  4. Démo : `CASAENGINE_DEMO_AA=Fxaa|None`, lue là où le sont les variables de bloom.
- Validation : builds ; tests : valeur par défaut, aller-retour, un fichier sans la clé se relit `None`, `color_pipeline` reste omis quand tout est par défaut ; non-régression fonctions coupées.
- **Note de validation (2026-11-15).** Builds `Debug` des deux solutions : 0 erreur (188 et 204 avertissements). Tests : 5945 verts (5941 + 4 nouveaux) ; l'hôte s'est interrompu sans test en échec aux deux premiers lancements (dernier test en cours : `EntityDetailsScriptRowTests`), passage complet au troisième lancement.
  - Code : `AntiAliasingMode { None, Fxaa }` ; `ColorPipelineSettings.AntiAliasing` (clé `anti_aliasing`, écrite par nom, lue sans tenir compte de la casse ; `IsDefault`, `ResetToDefaults`, `Load`, `Save`, `CopyFrom` complétés) ; `ResolvedEnvironmentSettings.AntiAliasing` copié par `EnvironmentResolver` (`None` dans `CreateLegacy`) ; liste déroulante « Anti-Aliasing » après le bloom dans `EntityDetailsPanel` ; `CASAENGINE_DEMO_AA=Fxaa|None` dans `DemosGame` (valeur illisible : avertissement, ignorée). Aucun effet de rendu tant que T1.2 n'existe pas. Pas d'ADR dans cette tâche : l'ADR-0112 couvre le réglage, l'ADR-0113 vient avec la passe.
  - Tests ajoutés (`ColorPipelineSettingsTests`, 4) : défaut `None` et `IsDefault` ; clé écrite par nom et relue (casse libre) ; un `color_pipeline` écrit avant la clé se relit `None` ; seul ce champ différent écrit `color_pipeline`, et le retour à `None` l'omet à nouveau. L'aller-retour, `Clone`, `CopyFrom` et le résolveur (vue, surcharge de vue, `CreateLegacy`) couvrent le champ via le jeu de valeurs non défaut.
  - Non-régression, fonctions coupées (sans recapture, 0 exception, 0 « was not found ») : `capture-v2.sh` `Legacy` égal à T0.1 pour les huit captures déterministes (material `b73dfdd2684a8615`, environment `0cf97b7e08cc0871`, static-model `312dda61e950f6b9`, static-shadow `add5db24fbd43814`, render-to-texture `d45bc1b2d6630553`, ui-overlay `307d01b87c81303d`, frozen-frame `eb25f3917cfa6a0c`, tile-map `355772772016e47f`, deux passages identiques) ; `lin-v2.sh` `LinearHdr` `0ba208b099b28b67` (16 premiers caractères, égal à T0.1) ; `pbrdemo.sh` validation `LinearHdr` `00847b5d4e85ef3b` (deux passages) et `Legacy` `a844109b0a2bfd5f` (deux passages).
  - Contrôle de la variable : `CASAENGINE_DEMO_AA=Fxaa` journalise « Anti-aliasing harness: Fxaa » et laisse les empreintes inchangées (`lin-v2.sh` `0ba208b099b28b67`, `pbrdemo.sh` `00847b5d4e85ef3b`), ce qui est attendu avant T1.2.
  - Non vérifié : l'éditeur (liste déroulante) a été compilé mais pas lancé.
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
- **Précisions de revue (priment sur les étapes et la ligne Validation là où elles les contredisent).**
  - **Interface** : la passe est appelée dans la branche `composeInterfaceAfter` de `RenderHdrView` seulement (P3). Test : avec un pipeline de vue qui rend tout d'un bloc (le double de test `WholeViewPipeline` existant sert de modèle), la passe ne s'exécute pas ; un test d'ordre épingle l'appel à l'intérieur de la condition. L'ADR-0113 et `color-pipeline.md` écrivent la limite.
  - **Éviction** : la clé de la cible LDR est horodatée (règle « Cibles mises en commun ») ; compteurs propres `FxaaTargetCreationCount` et `FxaaTargetDisposalCount` ; la boucle de libération de `RenderPipeline.cs:597-600`, qui attribue aujourd'hui au bloom toute clé sans profondeur, distingue les deux. Test : après un rendu à la taille A avec FXAA puis plus de `HdrTargetEviction.MaxIdleRenders` rendus à la taille B seulement, plus aucune cible libre (A, `Color`, sans profondeur) dans le pool ; les compteurs du bloom ne bougent pas quand le bloom est coupé.
  - **Preuve que la passe a tourné** : `FxaaRenderer` compte ses dessins de l'image ; le banc de démo écrit ce compte une fois au journal au moment de la capture. Toute mesure de capture ci-dessous exige un compte ≥ 1 par vue concernée, et 0 sans FXAA.
  - **Rectangles nommés** (constantes dans `ColorPipelineDemo.cs`) : `FxaaTextBorderRect` chevauche le bord extérieur du bloc de texte sur la scène ; `FxaaPanelBorderRect` chevauche le bord du panneau d'interface sur la scène. Critère : égalité exacte de ces deux rectangles avec et sans FXAA. Contrôle de la mesure, fait une fois puis annulé : en déplaçant l'appel FXAA après la composition de l'interface, l'un des deux rectangles diffère.
  - **Bord de scène** : `FxaaEdgeSegment`, segment nommé traversant le bord d'un objet ; le nombre de pixels de niveau strictement compris entre ceux des deux côtés augmente d'au moins 1 avec FXAA.
  - **Destinations exercées** : `InitialTarget` (`lin-v2.sh`), `ViewSurface` (rendu vers texture ou résolution virtuelle tamponnée, `CASAENGINE_COLOR_DEMO_BUFFERED=1` si la variable existe encore, sinon la démo de rendu vers texture en `LinearHdr`), `SceneRt` (une vue avec ombres). Pour chacune : compte de passe ≥ 1, pas d'exception, et un pixel nommé d'une zone unie de chaque vue égal à ± 2 niveaux à celui de la même exécution sans FXAA. L'écran partagé : même critère sur un pixel uni par vue. `RetainedTarget` et la texture hôte de l'éditeur ne sont pas exercés : notés 🧪.
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
- **Précisions de revue (priment sur les étapes et la ligne Validation là où elles les contredisent).**
  - **Formules** : celles de P4, sans variante (direct, ambiant avec IBL, ambiant plat ; atlas global seulement pour le reflet du vernis).
  - **Preuve de l'ambiant** : (e) test GPU avec l'IBL liée sur un environnement uniforme de radiance connue (le montage des tests GPU d'IBL existants sert de modèle), base noire non métallique de rugosité 1, aucune lumière directe : le pixel avec `ClearCoat = 1` dépasse celui de `ClearCoat = 0` de `radiance × E` à ± 10 %, E calculé côté test à partir de la table BRDF ; ce test devient rouge si le reflet du vernis est retiré de l'ambiant. (f) test de couverture : l'appel du reflet du vernis lit l'atlas et la table avec `ClearCoatRoughness`, pas avec la rugosité de base. (g) ambiant plat : base diffuse blanche, `HasIbl = 0`, `ClearCoat = 1`, vue de face : l'ambiant baisse de 4 % (entre 2 % et 6 %).
  - **Surcharges par instance** : fichier ajouté `MaterialInstancePropertyBlockMapper.cs` ; `clear_coat` et `clear_coat_roughness` y sont projetés sur `ShaderParameterNames.ClearCoat` et `ClearCoatRoughness`, comme `metallic` et `roughness`. Test : un bloc de surcharge portant les deux clés contient les deux constantes ; le test existant qui épingle le nombre de propriétés projetées (`LitPbrMaterialDefinitionTests.cs:299-313`) est mis à jour et justifié.
  - **`Legacy`** : la non-régression comprend l'empreinte PBR `Legacy` de T0.1 (ou, si T0.1 a ouvert le point, la variable de démo est ajoutée ici d'abord, dans un état où les shaders ne sont pas encore modifiés, et l'empreinte prise avant toute modification de shader).
- Commit : `feat(materials): add a clear coat layer to lit-pbr (ADR-0114)`

### ⏳ T2.2 — Clear coat à l'import glTF et dans la démo

- Objectif : lire le vernis d'un glTF et le montrer.
- Fichiers : `StaticModelImportResult.cs`, `GltfStaticModelReader.cs`, `EditorAssetImportService.cs`, leurs tests ; `CasaEngine.Demos/Demos/PbrMaterialDemo.cs`, `docs/engine/pbr-rendering.md`, ADR-0111 (une ligne) et ADR-0114 (import).
- Étapes :
  1. **Preuve d'abord** : un test construit un glTF avec un clear coat (facteur 0,8, rugosité 0,2) par l'API de SharpGLTF 1.0.6 et le relit. Si l'API ne permet ni de l'écrire ni de le lire de façon établie, ne pas inventer : noter le fait, laisser l'import sans vernis, ouvrir un point, et passer à l'étape 3.
  2. Champs additifs `ClearCoatFactor`, `ClearCoatRoughnessFactor` sur `StaticModelImportedMaterial` (défaut 0) ; en import `LitPbr`, écrire `clear_coat` et `clear_coat_roughness` seulement quand le facteur est > 0. Chemin `LitDiffuse` inchangé (référence versionnée de T9.2 du plan PBR toujours identique).
  3. Démo : derrière `CASAENGINE_PBR_DEMO_CLEARCOAT=1`, une rangée de cinq sphères (base rouge diélectrique, rugosité 0,6, `clear_coat` 0 ; 0,25 ; 0,5 ; 0,75 ; 1). Sans la variable, la démo est inchangée.
- Validation : builds ; tests ; mesure sur la capture de validation avec la variable : la valeur maximale du reflet croît strictement de la première à la dernière sphère ; sans la variable, empreinte de T0.1 ; non-régression fonctions coupées.
- **Précisions de revue (priment sur la mesure de la ligne Validation).** La croissance avec `clear_coat` est prouvée par le test GPU (c) de T2.1 ; la démo ne prouve que la visibilité. Constantes nommées dans `PbrMaterialDemo.Measurement` : un disque par sphère de la rangée. Configuration : validation, `CASAENGINE_PBR_DEMO_BLOOM=0`. Critère : le maximum de luminance dans le disque de la sphère `clear_coat = 1` dépasse d'au moins 20 niveaux celui de la sphère `clear_coat = 0`. Contrôle : avec `CASAENGINE_PBR_DEMO_CLEARCOAT=flat` (les cinq sphères à `clear_coat = 0`), l'écart entre ces deux disques est de 3 niveaux au plus. Si le reflet de la sphère sans vernis sature déjà, baisser l'intensité du soleil de la rangée n'est pas permis (cela changerait l'empreinte) : choisir une base plus rugueuse pour la rangée et le noter.
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
- **Précisions de revue : décisions d'architecture prises par le plan, que T3.1 vérifie contre le code.** Si le code contredit l'une d'elles, T3.1 passe ⚠️ avec le fait et la question ; T3.2 et T3.3 ne s'exécutent qu'après une relecture indépendante de leur tranche à la lumière de la note de T3.1.
  1. **Hôte** : un objet `SceneDepthPrepass` possédé par `RenderPipeline` et exposé par `RenderContext` (comme `IblCache`). `RenderHdrView`, avant `RenderScene`, l'active pour la vue si le SSAO est actif, si la vue rend par la cible HDR et si la projection est perspective (`frame.Projection.M44 == 0`) : il acquiert la cible (clé horodatée), l'efface à la valeur de fond, puis relie la cible HDR. Il la relâche dans le `finally` de `RenderHdrView`, avec la cible HDR. En T3.2 rien ne la consomme encore : elle est acquise, remplie et relâchée.
  2. **Dessin** : chaque rendu dessine sa propre profondeur pendant son propre `Flush`, en liant la cible de la pré-passe puis en reliant la cible précédente. Les cibles du pool sont en `PreserveContents` (`RenderTargetPool.cs:84`), donc ce va-et-vient ne les efface pas — à confirmer par l'essai GPU de T3.1 (dessiner, changer de cible, revenir : le contenu est conservé). Statique : une passe `SceneDepthPass` insérée dans `ForwardRenderPipeline` après `ShadowPass`, qui parcourt la liste opaque comme `ShadowPass` (éléments instanciés dessinés un par un, technique à découpe alpha quand le matériau en a une). Skinné : dans son `Flush`, avant ses dessins de couleur, par `RiggedModelSceneDepth` et sa variante ; le rappel `PostShadowPassCallback` existant n'est pas réutilisé.
  3. **Sortie** : profondeur de vue en unités du monde (distance le long de l'axe de la caméra), non normalisée : aucun plan proche ou lointain n'est requis. Fond : une constante nommée, grande et représentable dans le format retenu ; le shader de SSAO traite toute profondeur au-delà d'un seuil comme du ciel (occlusion 1). Position de vue reconstruite avec `M11` et `M22` de `frame.Projection`.
  4. **Composition** : second cas de P6, entre `RenderScene` et `ApplyBloom`.
  5. **Autres dessins 3D** : hors de la pré-passe, limite écrite (P6).
  6. **Compteurs** : `SceneDepthPrepass` compte par image ses dessins statiques et skinnés ; le banc de démo les écrit une fois au journal à la capture.
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
- **Précisions de revue (priment sur les étapes 2 et 3 et complètent les lignes Fichiers et Validation).**
  - Architecture : celle des précisions de T3.1 (hôte, dessin par chaque rendu, sortie non normalisée, compteurs). L'étape 2 « divisée par le plan lointain » et « fond effacé à 1 » est remplacée par la sortie de la décision 3.
  - **Fichiers ajoutés** : `ForwardRenderPipeline.cs`, `StaticMeshRendererComponent.cs`, `RenderPipeline.cs`, `RenderContext.cs`, et le fichier de la nouvelle passe sous `Draw/`.
  - **Éviction** : clé de la cible horodatée (avec son format de profondeur) ; compteurs propres. Test : après un changement de taille de vue et plus de `HdrTargetEviction.MaxIdleRenders` rendus, la cible de l'ancienne taille n'est plus dans le pool ; rouge si l'horodatage est retiré.
  - **Preuve du chemin skinné et instancié** : démo de maillages skinnés figée (`CASAENGINE_SKINNED_DEMO_FREEZE=1`) en `LinearHdr` avec `CASAENGINE_DEMO_SSAO=1` : le compte de dessins skinnés de la pré-passe est > 0, et 0 sans SSAO ; même chose pour le compte statique sur une démo statique. Test GPU : un maillage skinné (les deux modes de skinning) à distance connue écrit la profondeur attendue à ± 1 % et laisse le fond à sa constante. Si une démo dessine des groupes instanciés, le compte statique doit égaler le nombre d'instances opaques visibles (noter la valeur).
  - **Projection non perspective** : test : une vue orthographique avec SSAO actif n'active pas la pré-passe.
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
- **Précisions de revue (priment sur l'étape 3 et sur la mesure (4)).**
  - Composition entre `RenderScene` et `ApplyBloom` (P6) : fichier `RenderPipeline.cs`. La mesure (4) est remplacée par : un pixel vu à travers la sphère de verre, placé dans un creux, **est** assombri comme le fond qu'il recouvre (limite P6, notée avec sa valeur) ; ce n'est pas un critère de réussite.
  - Les cibles d'occlusion et de flou sont horodatées (règle « Cibles mises en commun »), avec le même test d'éviction que T3.2.
  - Le teinté d'ombre, le bloom et le tonemapping s'appliquent après la multiplication : un test d'ordre épingle scène < SSAO < bloom < tonemapping.
  - Mesure ajoutée : (7) dans la démo de maillages skinnés figée, un pixel du sol au pied d'un personnage est plus sombre d'au moins 5 niveaux avec SSAO que sans ; si la disposition ne le permet pas, noter la mesure de remplacement et pourquoi.
- Commit : `feat(rendering): add screen-space ambient occlusion (ADR-0115)`

---

## Phase 4 — Démo, coût, documentation

### ⏳ T4.1 — Démo de référence et coût

- Objectif : tout montrer ensemble et chiffrer.
- Fichiers : `CasaEngine.Demos/Demos/PbrMaterialDemo.cs`, `CasaEngine.Demos/DemoFrameStats.cs`, `CasaEngine.Demos/DemosGame.cs`, `CasaEngine.Demos/Content/demos-input.buttonsMapping`, `docs/engine/demos-main-menu.md`, ce plan.
- Étapes :
  1. Touches de la démo PBR : FXAA, SSAO (affichées sur la fiche) ; la rangée vernie de T2.2 visible par défaut **hors** mode de validation seulement (le mode de validation garde l'empreinte de T0.1 sans variable).
  2. Coût : `framestats.sh` en `Release`, démo PBR en validation, dans les combinaisons FXAA × SSAO × rangée vernie ; noter les temps moyens et les compteurs.
  3. Passe finale : builds, suite complète, non-régression fonctions coupées.
- Validation : une ligne de journal par configuration notée ici ; empreintes identiques à T0.1 ; basculer chaque touche dans la démo est un contrôle manuel (🧪 s'il n'est pas observé).
- **Précisions de clôture (revue de clôture, priment sur l'étape 2 et la ligne Validation).**
  - **Binaire mesuré** : `dotnet build CasaEngine.MonoGame.sln -c Release` est lancé juste avant l'étape 2 (le `Release` existant date du plan précédent et n'a ni FXAA ni SSAO). La note de T4.1 écrit l'horodatage du binaire `CasaEngine.Demos/bin/Release/net9.0-windows/CasaEngine.Demos.dll` et celui du dernier build.
  - **Preuve d'activation, dans le journal de la même exécution** (sans capture) : `framestats.sh` n'en prend pas, donc les compteurs de T1.2 (`FxaaRenderer`) et de T3.1 décision 6 (`SceneDepthPrepass`, dessins statiques et skinnés) ne seraient pas écrits « au moment de la capture ». T4.1 ajoute donc `CasaEngine.Demos/DemoFrameStats.cs` et `CasaEngine.Demos/DemosGame.cs` à ses fichiers : le rapport `[FrameStats]` de la dernière image mesurée (`DemoFrameStats.Report`) ajoute à la même ligne `fxaaDraws=`, `ssaoStaticDraws=`, `ssaoSkinnedDraws=` (lus sur le dernier rendu de la vue 0), et le banc de démo écrit, une fois, une ligne `[DemoConfig]` portant les variables effectives : `aa`, `ssao`, `clearCoatRow` (état de la rangée vernie lu dans `PbrMaterialDemo`). Le code n'écrit rien par image : lecture et écriture une seule fois, à la fin de la mesure.
  - **Règle de validité** : chaque configuration FXAA × SSAO × rangée vernie est notée avec sa ligne `[FrameStats]` et sa ligne `[DemoConfig]` de la même exécution. Les compteurs doivent valoir ≥ 1 quand la fonction est active (FXAA : `fxaaDraws`; SSAO : `ssaoStaticDraws` et `ssaoSkinnedDraws` pour la part de la démo qui dessine cette géométrie ; si la démo PBR ne dessine pas de maillage skinné, `ssaoSkinnedDraws` attendu 0 est noté et le skinné est couvert par T3.2) et 0 quand elle est coupée ; `[DemoConfig]` doit afficher la rangée vernie dans l'état de la configuration. **Une configuration dont les compteurs ou la ligne `[DemoConfig]` ne correspondent pas à ses variables invalide sa ligne** : elle est relancée après correction, jamais notée.
- Commit : `feat(demos): show clear coat, SSAO and FXAA in the PBR demo and record their cost`

### ⏳ T4.2 — Documentation et rapport

- Objectif : documenter et rendre le plan.
- Fichiers : `docs/engine/pbr-rendering.md`, `docs/engine/color-pipeline.md`, `docs/README.md`, `ai-agent/README.md`, ce plan.
- Étapes : sections clear coat, SSAO, FXAA (réglages, clés, ordre des passes, limites : FXAA et SSAO en `LinearHdr` seulement, FXAA absent des vues qui rendent scène et interface d'un bloc (éditeur), vernis absent du skinné et sans sondes locales, SSAO appliqué aussi au transparent et aux autres dessins 3D, SSAO sur toute la couleur opaque, FXAA sans effet sur l'interface) ; retirer le clear coat de la liste « hors périmètre » ; renvois aux ADR-0112 à 0115 ; rapport de fin (`AGENTS.md` §12) ; ligne du tableau de `ai-agent/README.md`. Le plan n'est archivé que si aucune tâche ne reste 🧪 ou ⚠️.
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
| 1 | Enveloppe, phases 1, 2, 3 | REVISE × 4 | Tous P2, tous **FIX**. Enveloppe : P1 restreint à FXAA et SSAO (le vernis suit le matériau dans les deux pipelines) et empreinte PBR `Legacy` ajoutée à la base ; dépendances et règle de resserrement ; abandon propre ; protocole de recapture. FXAA : horodatage de la cible pour l'éviction ; passe limitée aux vues qui séparent scène et interface ; rectangles, pixels et preuve d'exécution nommés. Clear coat : formules de l'ambiant (IBL, plat, sans sondes) et leur preuve ; mesure de démo indépendante de la position sous-pixel ; surcharges projetées par le mappeur. SSAO : architecture décidée dans le plan (hôte, dessin par chaque rendu, sortie non normalisée, composition entre scène et bloom), T3.1 devient un point de contrôle suivi d'une relecture ; preuve du chemin skinné ; éviction. |
