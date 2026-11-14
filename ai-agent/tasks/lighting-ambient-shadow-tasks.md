# Plan agent IA — Lumière ambiante et ombres (chantier RacingGame S2)

Plan d'exécution des tâches moteur de la tranche S2 « lumière et ombres » du chantier de fidélité 3D de RacingGame, décrite dans `RacingGame/ai-agent/tasks/rgce-3d-lighting-shadows-tasks.md` (révision 5, amendements A1 à A8). Ce plan en recopie les tâches moteur E0 → E6 et E5b ; pour les ombres (E3 → E6) il en donne la synthèse du contrat effectif (amendements du plan de RacingGame), voir la phase 2.
Les décisions D1 → D3, D5 → D8 ci-dessous ont été arbitrées avec l'auteur : **ce plan les applique, il ne les rediscute pas**.

Ce fichier doit être mis à jour pendant le travail : l'icône au début de chaque tâche indique son statut courant.

> **Périmètre.** E0, E1, E2 (ambiant) et E7, E8 (matériaux) sont terminées. E3, E4, E5, E5b et E6 (ombres, phase 2) sont exécutées sur la demande de l'auteur du 2026-11-14 d'exécuter les tâches qui suivent le plan des matériaux pendant son absence ; elles n'ont pas passé de revue de plan indépendante concluante (voir la phase 2).

## Objectif

Le moteur calcule l'ambiant comme `texture × DiffuseColor × (N·L × lumière + ambiant du monde × ambiant du matériau) + émissif` (E1), peut garder un ambiant plat quand un cube de réflexion est lié au monde (E2) et sait appliquer l'ombre comme l'original de RacingGame : couleur éclairée multipliée par une teinte, bord doux, fondu à distance, profondeur de carte indépendante de son étendue (E3 → E5b, documentation E6).

Hors périmètre : réajustement des scènes existantes du moteur après E1, spéculaire, réflexions, ciel, post-traitement, push, merge.

## État vérifié du dépôt (2026-11-14)

- Branche `chantier/racinggame-lighting`, créée depuis `chantier/gltf-static-tangents` (un commit d'avance sur `main` : lecture de `TANGENT` des glTF statiques, dont la conversion de RacingGame dépend). Exception approuvée à `AGENTS.md` §4 (« branche créée depuis `main` »), amendement A6 du plan de RacingGame.
- Modification préexistante de l'auteur dans l'arbre de travail : le pointeur du sous-module `MGUI` (` M MGUI`). Elle n'est jamais indexée ni committée.
- **Ambiant multiplié deux fois par la couleur de surface.** `ComputeBaseAmbientTerm` (`CasaEngine/Content/Shaders/LitForward.fx`) rend `couleur × DiffuseColor × ambiant`, puis `ComposeLitSurfaceColor` (`Lighting.fxh`) multiplie `(direct + ambiant)` par la couleur : l'ambiant à l'écran vaut `texture² × DiffuseColor × ambiant`. Même chose dans `skinEffect.fx` (`ComputeSkinnedAmbientContribution`). Six appels dans `LitForward.fx` (`PSBasicPixelLighting`, `…Tx`, `…OneLight`, `…TxOneLight`, `…TxNorm`, `…TxNormReflection`), un dans `skinEffect.fx` (`PixelShaderRiggedModelDraw`).
- Quand un cube de réflexion est lié au monde, `HasEnvironmentCubeTexture = 1` (`EnvironmentShaderBinder`) et l'ambiant suit la voie « environnement » : `cube(normale) × EnvironmentAmbientColor × ambiant du matériau`, non `AmbientColor × ambiant du matériau`.
- `ShadowSettings` : `Enabled`, `Resolution`, `DepthBias`, `NormalBias`, `MaxDistance`. Receveur : 4 échantillons ponctuels à ±0,5 texel ; le facteur d'ombre ne multiplie que la lumière directe.
- Les shaders `LitForward.fx` et `skinEffect.fx` consomment tous deux `Lighting.fxh` : tout nouvel uniforme lu par ce fichier est déclaré dans les deux.
- Les effets sont compilés par le pipeline de contenu (`CasaEngine/Content/Content.mgcb`, entrées `Shaders/LitForward.fx` et `Shaders/skinEffect.fx`), reconstruit par le build du projet `CasaEngine`.

## Décisions verrouillées

| Réf | Décision |
|---|---|
| D1 | Ombre : `ShadowSettings.Mode`, `OccludeDirectLight` (défaut, comportement actuel) ou `MultiplyLitColor` (la couleur éclairée entière est multipliée par `lerp(1, ShadowColor, ombre)`), plus `ShadowColor` (défaut noir). |
| D2 | Douceur : `ShadowSettings.FilterRadius` en texels (0 = les 4 échantillons actuels ; au-delà, disque de Poisson fixe). |
| D3 | Fondu : `ShadowSettings.FadeDistance` (0 = coupure nette actuelle). |
| D5 | Branche dédiée `chantier/racinggame-lighting` depuis `chantier/gltf-static-tangents`, un commit par tâche, aucun push, aucun merge. Le pointeur du sous-module dans RacingGame reste à l'auteur. |
| D6 | Ces tâches sont recopiées ici (règle du moteur : plan dans son dépôt), sans changer leur contenu. |
| D7 | Ambiant : correction pour tous les appelants. `ComputeBaseAmbientTerm` et `ComputeSkinnedAmbientContribution` ne multiplient plus par la texture ; `DiffuseColor` reste dans l'ambiant (teinte de base du matériau) ; `ComposeLitSurfaceColor` applique seule la couleur de surface. Toute scène existante voit son ambiant s'éclaircir (facteur 1 / texture) : traité comme un bug, avec ADR et note de migration. |
| D8 | `WorldEnvironmentSettings.AmbientSource` : `Auto` (défaut, comportement actuel) ou `Flat` (toujours `AmbientColor × ambiant du matériau`, même avec un cube de réflexion lié). Champ sérialisé optionnel. |

## Règles d'exécution pour l'agent

- **Branche `chantier/racinggame-lighting`.** Ne jamais committer sur `main`.
- **Une seule tâche à la fois**, un commit par tâche, atomique et compilable, mise à jour de ce fichier dans le même commit.
- **Ne jamais pousser, ne jamais fusionner.** `git add` fichier par fichier ; ne jamais indexer `MGUI`.
- Build des deux solutions et tests avant ✅. Les captures de démo restent **hors dépôt** (dossier temporaire de session).
- Un champ sérialisé nouveau est optionnel à la lecture ; son défaut garde le comportement actuel (sauf D7, correction assumée).
- Règle d'arrêt : une validation qui échoue sans correction possible dans le périmètre de la tâche passe la tâche en ⚠️ avec les faits mesurés ; on ne commite pas un état cassé et on ne desserre ni test ni tolérance.
- Langue : plan en français ; code, commits, ADR et docs en anglais.

## Légende des statuts

- ⏳ Todo · 🚧 In progress · 🧪 Needs testing · ✅ Done · ⚠️ Blocked

## Validation globale

- `dotnet build CasaEngine.MonoGame.sln` et `dotnet build CasaEngine.Editor.MonoGame.sln` : 0 erreur.
- `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj` : aucun échec.
- Démo `StaticShadowValidationDemo`, capturée par `CASAENGINE_START_DEMO="Static shadow validation demo"` et `CASAENGINE_CAPTURE_SCREENSHOT_PATH` (voir `CasaEngine.Demos/DemosGame.cs`), sans variable puis en configuration de mesure.

### Démo de mesure (variables d'environnement de `StaticShadowValidationDemo`)

Sans variable, la démo est inchangée (capture identique octet pour octet, vérifiée en E0).

| Variable | Effet | Ajoutée en |
|---|---|---|
| `CASAENGINE_SHADOW_DEMO_VALIDATION=1` | Configuration de mesure (amendement A1) : ambiant du monde `(0.5, 0.5, 0.5)`, intensité 1 ; lumière d'appoint éteinte ; les trois sols deviennent le receveur texturé (texture unie 128 / 255 générée en mémoire, `DiffuseColor` blanc, ambiant du matériau 1, spéculaire 0) ; une sphère réfléchissante (`UseSceneReflectionCube`, ambiant du matériau 0, devant la colonne centrale) | E0 |
| `CASAENGINE_SHADOW_DEMO_REFLECTION_CUBE=1` | (avec la configuration de mesure) cube uni `(0.2, 0.6, 1.0)` généré par la démo et lié comme cube d'environnement spéculaire du monde | E0 |
| `CASAENGINE_SHADOW_DEMO_AMBIENT_SOURCE=Flat\|Auto` | (avec la configuration de mesure) valeur de `WorldEnvironmentSettings.AmbientSource` | E2 |
| `CASAENGINE_SHADOW_DEMO_DETAIL=1` | (avec la configuration de mesure) lie au receveur une texture de détail unie de valeur 64 / 255 (générée en mémoire), pavage 4 | E7 |
| `CASAENGINE_SHADOW_DEMO_MIPMAP_CHECK=1` | (indépendante de la configuration de mesure) vérifie `Texture2DLoader.GenerateMipmaps` sur la carte graphique et journalise le verdict | E8 |
| `CASAENGINE_SHADOW_DEMO_MODE=OccludeDirectLight|MultiplyLitColor`, `CASAENGINE_SHADOW_DEMO_COLOR=r,g,b` | (avec la configuration de mesure) mode d'ombre et teinte (composantes entre 0 et 1, point décimal) | E3 |
| `CASAENGINE_SHADOW_DEMO_FILTER_RADIUS` | Rayon de filtre | E4 |
| `CASAENGINE_SHADOW_DEMO_FADE_DISTANCE`, `CASAENGINE_SHADOW_DEMO_MAX_DISTANCE` | Distance de fondu ; distance maximale de la carte (défaut 48) | E5 |
| `CASAENGINE_SHADOW_DEMO_DEPTH_RANGE` | Profondeur de la carte (`DepthRange`) | E5b |

### Pixels mesurés (capture 1024 × 768)

| Nom | Position (x, y) | Surface |
|---|---|---|
| `ombre` | (310, 420) | receveur texturé de la colonne gauche, dans l'ombre de la lumière principale (ambiant seul) |
| `eclaire` | (240, 435) | même receveur, en pleine lumière |
| `reflet` | (520, 490) | sphère réfléchissante |

Les trois pixels sont stables d'une exécution à l'autre (deux captures de la démo sans variable : fichiers identiques).

---

## Phase 1 — Ambiant

### ✅ E0 — Branche, plan, configuration de mesure, état initial

- Objectif : créer la branche (D5) ; ce plan ; la configuration de mesure `CASAENGINE_SHADOW_DEMO_VALIDATION=1` de la démo ; l'état initial.
- Fichiers : `ai-agent/tasks/lighting-ambient-shadow-tasks.md`, `ai-agent/README.md`, `CasaEngine.Demos/Demos/StaticShadowValidationDemo.cs`.
- Validation et état initial (2026-11-14) :
  - `dotnet build CasaEngine.MonoGame.sln` et `CasaEngine.Editor.MonoGame.sln` : 0 erreur. `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj` : 5080 réussis, 0 échec, 0 ignoré.
  - Démo sans variable : capture identique octet pour octet avant et après la modification de la démo (`cmp`).
  - Valeurs mesurées (par canal R, G, B, sur 255) :

    | Capture | `ombre` | `eclaire` | `reflet` |
    |---|---|---|---|
    | configuration de mesure | 32, 32, 32 | 191, 183, 166 | 165, 157, 139 |
    | configuration de mesure + cube lié (`Auto`, seul comportement avant E2) | 6, 19, 32 | 166, 170, 166 | 168, 166, 155 |

  - **Prédiction de A1 confirmée, aucune courbe de sortie.** `ombre` = `128/255 × 128/255 × 0,5 × 255 = 32,1` : mesuré 32. Avec le cube : `0,252 × (0,2 ; 0,6 ; 1,0) × 0,5 × 255 = (6,4 ; 19,3 ; 32,1)` : mesuré (6, 19, 32). `eclaire` : `tex × (N·L × 1,55 × couleur de la lumière + tex × 0,5)` avec `N·L = 0,8027` (dessus du sol, lumière `(0,42 ; -0,8 ; -0,42)`) et couleur `(1 ; 0,949 ; 0,839)` donne `(191,4 ; 183,3 ; 165,8)` : mesuré `(191, 183, 166)`. La sortie est donc linéaire (ni gamma, ni sRGB, ni tonemapping, aucune autre lumière, couleur de sommet 1) et la règle de prédiction de A1 s'applique telle quelle à E1 et E2.
  - Valeurs attendues pour E1 (`ombre` 64 ; `eclaire` : `tex × (direct + 0,5)` = (223,3 ; 215,2 ; 197,6), soit +32 par canal, la part d'ambiant `tex × (1 − tex) × 0,5 × 255 = 31,9`).
- Commit : `chore(demos): add a measurement configuration to the static shadow demo`

### ✅ E1 — L'ambiant n'est multiplié qu'une fois par la couleur de surface (D7)

- Objectif : dans `LitForward.fx` (`ComputeBaseAmbientTerm`) et `skinEffect.fx` (`ComputeSkinnedAmbientContribution`), le terme ambiant ne contient plus la couleur de surface ; `ComposeLitSurfaceColor` reste seule à l'appliquer. Résultat : `texture × DiffuseColor × (N·L × lumière + ambiant du monde × ambiant du matériau) + émissif`. Vérifier les autres appelants et chemins.
- ADR dans `docs/decisions/` (numéro suivant, index mis à jour) : la correction, son impact visible sur les scènes existantes, la note de migration, la suite (réajustement des scènes existantes hors de ce plan).
- Tests : `LightingShaderCoverageTests` mis à jour, avec une assertion de source sur les deux shaders qui échoue si le terme ambiant reprend la couleur de surface.
- Validation : builds des deux solutions, tests ; démo sans variable : capture identique à `e0-default` (matériaux sans texture) ; configuration de mesure : `ombre` passe de 32 à 64 (±3), `eclaire` ne change que de la part d'ambiant (+32).
- Fichiers : `CasaEngine/Content/Shaders/LitForward.fx` (`ComputeBaseAmbientTerm(worldNormal)`, six appels mis à jour), `CasaEngine/Content/Shaders/skinEffect.fx` (`ComputeSkinnedAmbientContribution(worldNormal)`, un appel), `CasaEngine.Tests/Rendering/LightingShaderCoverageTests.cs`, `docs/decisions/0089-the-ambient-term-applies-the-surface-colour-once.md`, `docs/decisions/README.md`.
- Autres chemins vérifiés : `Lighting.fxh` ne contient que `ComputeAmbientTerm` et `ComputeEnvironmentDiffuseTerm` (couleur fois couleur) et `ComposeLitSurfaceColor` ; les techniques à éclairage par sommet de `LitForward.fx` ne calculent aucun ambiant ; `LitDiffuseMaterial` ne lie que `MaterialAmbientColor`, `DiffuseColor`, `EmissiveColor` ; aucun autre shader ni chemin CPU du dépôt ne compose ce terme. Les effets sont reconstruits par le build du projet `CasaEngine` (pipeline de contenu) : le `LitForward.xnb` de la démo est régénéré.
- Résultats (2026-11-14) : builds des deux solutions 0 erreur ; `dotnet test` : 5082 réussis (5080 + 2 cas de la nouvelle assertion), 0 échec.
  - Démo sans variable : capture identique octet pour octet à `e0-default` (`cmp`) ; ses matériaux n'ont pas de texture.
  - Configuration de mesure (par canal R, G, B) :

    | Pixel | E0 | prédit E1 | mesuré E1 |
    |---|---|---|---|
    | `ombre` | 32, 32, 32 | 64 | 64, 64, 64 |
    | `eclaire` | 191, 183, 166 | 223,3 ; 215,2 ; 197,6 (+32, part d'ambiant) | 223, 215, 198 |
    | `reflet` | 165, 157, 139 | inchangé (ambiant du matériau nul) | 165, 157, 139 |

  - Avec le cube lié (`Auto`) : `ombre` 6, 19, 32 → 13, 38, 64 (prédit `0,502 × (0,2 ; 0,6 ; 1,0) × 0,5 × 255 = 12,8 ; 38,4 ; 64`) ; `reflet` inchangé (168, 166, 155).
- Commit : `fix(rendering): apply the surface colour to the ambient term only once`

### ✅ E2 — Source d'ambiant plate même avec un cube de réflexion (D8)

- Objectif : `WorldEnvironmentSettings.AmbientSource` (`Auto` par défaut, `Flat`), sérialisé de façon optionnelle (absent = `Auto`), géré par `Clone`, `CopyFrom`, `ResetToDefaults`, `Load`, `Save`, résolu par `EnvironmentResolver`, lié aux shaders par un uniforme distinct de `HasEnvironmentCubeTexture`, déclaré dans `LitForward.fx` et `skinEffect.fx` avec des registres sans collision. En `Flat`, l'ambiant vaut `AmbientColor × ambiant du matériau` ; les réflexions du cube sont inchangées.
- ADR ; documentation courte dans `docs/engine/` (index à jour).
- Tests : sérialisation, défaut, `Clone`, `CopyFrom`, `ResetToDefaults` de `AmbientSource` ; résolution (`Flat` avec cube lié donne la voie plate, `Auto` garde le comportement actuel) ; assertions de source dans `LightingShaderCoverageTests`.
- Démo : `CASAENGINE_SHADOW_DEMO_AMBIENT_SOURCE=Flat|Auto` dans la configuration de mesure.
- Validation : builds, tests ; démo sans variable : capture identique à celle de E1 ; configuration de mesure avec le cube lié, deux captures (`Auto`, `Flat`) : `Flat` donne `ombre` = 64 (±3) ; `Auto` en diffère de plus de 12 sur au moins un canal et vaut `64 × (0,2 ; 0,6 ; 1,0)` (soit (12,8 ; 38,4 ; 64)) ; `reflet` identique dans les deux.
- Fichiers : `CasaEngine/Framework/Rendering/Environment/AmbientSource.cs` (nouveau), `WorldEnvironmentSettings.cs`, `ResolvedEnvironmentSettings.cs`, `EnvironmentResolver.cs`, `EnvironmentShaderBinder.cs`, `CasaEngine/Framework/Rendering/Shaders/ShaderParameterNames.cs`, `CasaEngine/Content/Shaders/LitForward.fx`, `skinEffect.fx`, `CasaEngine.Editor/Controls/EntityDetailsPanel.cs` (liste déroulante « Ambient Source » annulable, à côté des autres champs d'environnement), `CasaEngine.Demos/Demos/StaticShadowValidationDemo.cs`, `CasaEngine.Tests/Rendering/AmbientSourceTests.cs` (nouveau), `LightingShaderCoverageTests.cs`, `docs/engine/lit-ambient-and-ambient-source.md`, `docs/README.md`, `docs/decisions/0090-a-world-may-use-a-flat-ambient-term-with-a-reflection-cube.md`, `docs/decisions/README.md`.
- Choix : le champ est écrit sous la clé `ambient_source` seulement quand il n'est pas `Auto` (les mondes existants gardent leur format octet pour octet) ; absent = `Auto` ; `Load` remet `Auto` avant de lire. Uniforme `UseFlatAmbientSource`, déclaré dans les deux shaders au même emplacement `_ps(c36)` / `_cb(c32.z)` (c36 ne contient que `HasEnvironmentCubeTexture` x et `HasMaterialReflectionCube` y dans `LitForward.fx`, seulement `HasEnvironmentCubeTexture` dans `skinEffect.fx` ; `c32.z` est libre dans les deux). Les annotations `_vs/_ps/_cb` se développent en rien (`Macros.fxh:24-26`) : ce sont des indications, la collision réelle est impossible, l'emplacement est tout de même laissé sans doublon (assertion de source).
- Autres lieux qui énumèrent les réglages d'environnement : `EnvironmentAsset` et son sérialiseur (asset séparé, hors champ), `PreviewEnvironmentFactory` (copie nommée, défaut `Auto`), `ResolvedEnvironmentCache` (clé par version, pas par champ) : inchangés.
- Résultats (2026-11-14) : builds des deux solutions 0 erreur ; `dotnet test` : 5097 réussis (5082 + 13 tests d'`AmbientSourceTests` + 2 cas de source), 0 échec.
  - Démo sans variable, configuration de mesure sans cube et configuration de mesure avec cube en `Auto` : captures identiques octet pour octet à celles de E1 (`cmp`).
  - Configuration de mesure + cube lié :

    | Pixel | `Auto` | `Flat` | prédit |
    |---|---|---|---|
    | `ombre` | 13, 38, 64 | 64, 64, 64 | `Flat` : 64 (±3, valeur de E1) ; `Auto` : `64 × (0,2 ; 0,6 ; 1,0) = (12,8 ; 38,4 ; 64)`, écart à 64 de 51 et 26 sur R et G (> 12) |
    | `eclaire` | 172, 190, 198 | 223, 215, 198 | `Flat` = valeur de E1 sans cube |
    | `reflet` | 168, 166, 155 | 168, 166, 155 | identique dans les deux (ambiant du matériau nul) ; les réflexions suivent toujours le cube (165, 157, 139 sans cube) |
- Commit : `feat(rendering): let a world use a flat ambient term while keeping a reflection cube`

## Phase 1b — Matériaux et chargeur de textures (tranche S3 de RacingGame, `ai-agent/tasks/rgce-3d-materials-base-tasks.md`, décisions D1, D3, D5, amendements A1, A2, A3, A8)

> E7 et E8 sont approuvées par l'auteur avec le plan de la tranche S3 ; elles ne dépendent pas de E3 → E6.

### ✅ E7 — Texture de détail sur `LitDiffuseMaterial` (D1)

- Objectif : `LitDiffuseMaterial.DetailTexture` (`Texture2D`), `DetailTextureAssetId` (`Guid`), `DetailTiling` (`float`, défaut 1 ; valeur non finie ou ≤ 0 ramenée à 1). `SampleBaseTexture` de `LitForward.fx` rend `texture.rgb × 2 × détail(coordonnées × DetailTiling).rgb` (alpha inchangé) quand une texture de détail est liée ; uniformes `HasDetailTexture` et `DetailTiling`, échantillonneur `DetailTexture` sur l'emplacement 7 ; aucune nouvelle technique ; sans texture de détail, sortie identique. `skinEffect.fx` et la passe d'ombres ne sont pas modifiés.
- Sérialisation : définition `lit-diffuse` : `detail_texture` (texture, défaut vide, alias `texture_detail_asset_id`, amendement A8) et `detail_tiling` (flottant, défaut 1, minimum 0), ajoutées en fin de liste (les indices des propriétés existantes ne bougent pas). Clés JSON du matériau d'exécution : `texture_detail_asset_id`, `detail_tiling`. Un asset sans ces clés se charge comme avant.
- ADR `docs/decisions/0091-a-lit-diffuse-material-may-carry-a-detail-texture.md` (formule, neutre = gris moyen, multiplication avant l'éclairage, maillages animés non pris en charge, impact éditeur, échantillonneur du périphérique par défaut en DesktopGL : amendement A3) ; page `docs/engine/lit-detail-texture.md` ; index mis à jour.
- Fichiers : `CasaEngine/Content/Shaders/LitForward.fx`, `CasaEngine/Framework/Materials/Runtime/LitDiffuseMaterial.cs`, `CasaEngine/Framework/Rendering/Shaders/ShaderParameterNames.cs`, `CasaEngine/Framework/Materials/Definitions/BuiltInMaterialDefinitions.cs`, `CasaEngine/Framework/Materials/Compilation/MaterialCompiler.cs`, `CasaEngine.EditorServices/EditorAssetJsonSerializer.cs`, `CasaEngine.Demos/Demos/StaticShadowValidationDemo.cs`, `CasaEngine.Tests/Rendering/LitDiffuseDetailTextureTests.cs` (nouveau), `CasaEngine.Tests/Rendering/LightingShaderCoverageTests.cs`, `CasaEngine.Tests/EditorServices/MaterialDefinitionEditorRegistryTests.cs`, `docs/decisions/0091-…`, `docs/decisions/README.md`, `docs/engine/lit-detail-texture.md`, `docs/README.md`, `ai-agent/README.md`.
- Lieux où la gestion de la normal map a été reprise : `LitDiffuseMaterial` (propriétés, `Bind` — liée seulement avec une texture de base —, `Load`), définition `lit-diffuse`, `MaterialCompiler.CreateLitDiffuseMaterial`, `EditorAssetJsonSerializer.SaveLitDiffuseMaterial`, `ShaderParameterNames`. Volontairement non repris : `GetShaderCapabilities`, `RenderFeatureResolver`, `GetTechniqueName` (le détail ne change ni technique ni variante de shader) ; `EditorAssetImportService` (l'import glTF ne produit pas de texture de détail) ; `MaterialInstancePropertyBlockMapper` (ne traite aucune texture) ; `StaticModelMaterialResolver`, `GameEditor` (description de diagnostic).
- Résultats (2026-11-14) : builds des deux solutions 0 erreur (l'effet `LitForward.fx` compile sous DesktopGL avec le sampler supplémentaire, aucun message du compilateur d'effets) ; `dotnet test` : 5117 réussis (5097 + 20 : 18 cas de `LitDiffuseDetailTextureTests`, 2 de source), 0 échec. Le test de l'éditeur qui comptait les 9 propriétés de `lit-diffuse` passe à 11.
  - Captures prises dans cette session (hors dépôt, `engine-captures/e7-*`) : sans variable et en configuration de mesure sans détail, identiques octet pour octet avant (au `HEAD` précédent) et après (`cmp`), et à `e2-default` / `e2-validation`.
  - Configuration de mesure + détail (par canal R, G, B) :

    | Pixel | sans détail | prédit | mesuré |
    |---|---|---|---|
    | `ombre` | 64, 64, 64 | `64 × 2 × 64 / 255` = 32 (±3) | 32, 32, 32 |
    | `eclaire` | 223, 215, 198 | × 0,502 = 112,0 ; 107,9 ; 99,4 (±3) | 112, 108, 99 |
    | `reflet` | 165, 157, 139 | inchangé | 165, 157, 139 |
- Commit : `feat(materials): add a detail texture to the lit diffuse material`

### ✅ E8 — Le chargeur de textures peut construire une chaîne de mipmaps (D5)

- Objectif : `Texture2DLoader.GenerateMipmaps` (propriété d'instance publique, défaut `false` = comportement actuel). Vrai : `LoadAsset` rend une texture `Color` avec la chaîne complète (`floor(log2(max(l, h))) + 1` niveaux de taille `max(1, dim >> i)`, convention de MonoGame), niveau 0 identique au bit près à `Texture2D.FromStream`, chaque niveau suivant étant la moyenne 2 × 2 du précédent (arrondi au plus proche, moitiés vers le haut ; alpha droit, moyenné comme les canaux de couleur ; sur une dimension impaire la dernière ligne ou colonne n'entre dans aucun bloc ; sur un axe de longueur 1 le bloc fait 1 pixel).
- Fonction publique (amendement A1) : type statique pur `CasaEngine.Framework.Assets.Loaders.MipmapChain` plutôt qu'une méthode du chargeur, pour qu'un appelant sans fichier (texture peinte à l'exécution) puisse l'utiliser : `GetLevelCount`, `GetLevelSize`, `ComputeNextLevel` (tableau et `Span`), `ComputeLevels`, et `UploadChain(Texture2D, Color[])` qui écrit le niveau 0 et tous les niveaux calculés dans une texture `Color` créée avec `mipMap: true` (à rappeler après une repeinte).
- ADR `docs/decisions/0092-the-texture-loader-can-build-a-mipmap-chain.md` (API publique, défaut désactivé et pourquoi, comportement de l'alpha, coût : temps de chargement et +33 % de mémoire) ; page `docs/engine/texture-mipmaps.md` ; index mis à jour.
- Fichiers : `CasaEngine/Framework/Assets/Loaders/Texture2DLoader.cs`, `CasaEngine/Framework/Assets/Loaders/MipmapChain.cs` (nouveau), `CasaEngine.Tests/Assets/MipmapChainTests.cs` (nouveau), `CasaEngine.Demos/Demos/StaticShadowValidationDemo.cs`, `docs/decisions/0092-…`, `docs/decisions/README.md`, `docs/engine/texture-mipmaps.md`, `docs/README.md`, ce plan.
- Résultats (2026-11-14) : builds des deux solutions 0 erreur ; `dotnet test` : 5153 réussis (5117 + 36 : tailles de niveaux pour dimensions paires, impaires, non carrées et 1 × N, jusqu'à 1 × 1 ; valeurs moyennées et arrondi ; alpha droit ; niveau 0 inchangé ; arguments invalides), 0 échec.
  - Démo sans variable : capture identique octet pour octet avant et après (aucun appelant du moteur n'active l'option) ; configuration de mesure : identique à celle de E7 (`cmp`).
  - Preuve sur carte graphique (amendement A2), `CASAENGINE_SHADOW_DEMO_MIPMAP_CHECK=1`, journal de la démo (`log.txt`), fichiers temporaires générés puis supprimés (aucun reste dans le dossier temporaire) :

    ```
    [MipmapCheck] file=odd.png size=37x19 LevelCount=6 expected=6 levels=37x19,18x9,9x4,4x2,2x1,1x1 level0IdenticalToFromStream=True levelsMatchCpuChain=True defaultLoaderLevelCount=1 passed=True
    [MipmapCheck] file=even.tga size=64x32 LevelCount=7 expected=7 levels=64x32,32x16,16x8,8x4,4x2,2x1,1x1 level0IdenticalToFromStream=True levelsMatchCpuChain=True defaultLoaderLevelCount=1 passed=True
    [MipmapCheck] verdict=PASS
    ```

    Le niveau attendu est recalculé dans la démo sans `MipmapChain` ; chaque niveau est relu par `GetData` (qui refuse une taille de niveau fausse) et comparé à la chaîne calculée sur le processeur ; le chargeur par défaut rend 1 niveau. Le dépôt ne contient aucun fichier TGA : les deux images sont générées (PNG 37 × 19 impair et non carré, TGA 64 × 32 non compressé 32 bits).
- Commit : `feat(assets): let the texture loader build a mipmap chain`

---

## Phase 2 — Ombres (S2b)

Exécutée sur la demande de l'auteur du 2026-11-14 (exécuter les tâches qui suivent le plan des matériaux pendant son absence), sans nouvelle revue de plan : la revue de clôture de S2b du plan de RacingGame (`ai-agent/tasks/rgce-3d-lighting-shadows-tasks.md`, révision 4) n'a pas rendu READY (un point de priorité 2 sur l'étendue de la carte, resserré par l'amendement F1, côté jeu). La vérification indépendante de fin de tranche (T4, côté jeu) reste obligatoire.

**Contrat effectif.** Précédence dans le plan de RacingGame : amendements F (révision 4) > D (révision 3) > C (révision 2) > B > notes de la phase 4 > textes d'origine E3 → E6 > décisions D1 → D3. Les tâches ci-dessous en sont la synthèse (amendements C10 et F9) ; elles remplacent les textes d'origine. Écarts entre couches, tranchés selon cette précédence :

- La couleur multipliée comprend le spéculaire et la contribution de réflexion (B2, C6, F7), pas seulement « direct + ambiant » (texte d'origine de E3).
- Largeur de transition de E4 : au moins 2 fois (D4), pas 3 fois (B4). Disque de 12 points d'origine, réductible à 8 puis 5 par la limite de shader (C7).
- Preuve de E5b : test unitaire de matrice et affirmation de démo réduite (D3), pas la validation de C4.
- Biais de profondeur de E5b : `2 × DepthBias × (2 × DepthRange − 0,1)` sous DesktopGL (F6) ; la formule de D2 (sans le facteur 2) est corrigée. Vérifiée dans les shaders : `ShadowDepth.fx` (`EncodeShadowDepth`) et `Lighting.fxh` (`ComputeDirectionalShadowFactor`) appliquent sous `#if OPENGL` la même transformation affine `× 0,5 + 0,5` à la profondeur écrite et à la profondeur comparée, toutes deux dans [0, 1] côté HLSL : un biais `b` sur la profondeur comparée vaut un écart `2 b` de profondeur normalisée, soit `2 b × (plan lointain − plan proche)` = `2 × DepthBias × (2 × DepthRange − 0,1)` mètres. Sous DirectX le facteur 2 disparaît.
- E6 documente les réglages d'ombre ; la source d'ambiant l'est déjà (E2). Message de commit de E6 : `docs(engine): document the shadow settings`.

Champs sérialisés (tous optionnels, écrits seulement s'ils diffèrent du défaut, copiés par `CopyFrom`, remis par `ResetToDefaults`, présents dans le panneau de monde de l'éditeur) : `mode`, `shadow_color` (E3), `filter_radius` (E4), `fade_distance` (E5), `depth_range` (E5b). Un seul ADR (ADR-0093) couvre E3, E4, E5 et E5b : écrit en E3, complété à chaque tâche.

### ✅ E3 — Mode d'application et teinte de l'ombre (D1)

- Objectif : `ShadowSettings.Mode` (`ShadowMode.OccludeDirectLight` par défaut, `MultiplyLitColor`) et `ShadowSettings.ShadowColor` (`Vector3`, défaut noir, composantes bornées à [0, 1], non fini → 0) ; clés optionnelles `mode` et `shadow_color` ; `Load`, `Save`, `CopyFrom`, `ResetToDefaults` ; uniformes `ShadowApplyMode` et `ShadowTintColor` liés par `ForwardLightBinder`, déclarés dans `LitForward.fx` et `skinEffect.fx` ; appliqués dans `Lighting.fxh` et les pixel shaders. En `MultiplyLitColor`, la lumière directe (diffus et spéculaire) n'est pas occultée ; la couleur finale hors émissif (couleur éclairée, spéculaire, contribution de réflexion) est multipliée par `lerp(1, ShadowColor, 1 − facteur d'ombre)`, le facteur étant calculé une fois, hors de la boucle des lumières (`ColorPair.ShadowFactor`) ; alpha intact ; dans les trois pixel shaders à réflexion la teinte suit `ApplyReflectionContribution` et ne porte que sur la part non émissive de sa forme linéaire (`ApplyReflectionContributionWithShadowTint`). Aucune teinte en éclairage par sommet, avec `ReceiveShadows = false` ni sans lumière ombrée ; avec plusieurs lumières la teinte multiplie toutes les contributions (ADR, documentation).
- Fichiers : `CasaEngine/Framework/Rendering/Shadows/ShadowMode.cs` (nouveau), `ShadowSettings.cs`, `CasaEngine/Framework/Rendering/Shaders/ForwardLightBinder.cs`, `ShaderParameterNames.cs`, `CasaEngine/Content/Shaders/Lighting.fxh`, `LitForward.fx`, `skinEffect.fx`, `CasaEngine.Editor/Controls/EntityDetailsPanel.cs` (liste « Mode » et sélecteur « Shadow Color », annulables), `CasaEngine.Demos/Demos/StaticShadowValidationDemo.cs`, `CasaEngine.Tests/Rendering/ShadowSettingsTests.cs` (nouveau), `LightingShaderCoverageTests.cs`, `ForwardLightBinderTests.cs`, `docs/decisions/0093-shadow-settings-mode-colour-filter-fade-and-depth-range.md`, `docs/decisions/README.md`, `ai-agent/README.md`, ce plan.
- Démo : `CASAENGINE_SHADOW_DEMO_MODE=MultiplyLitColor` et `CASAENGINE_SHADOW_DEMO_COLOR=0.25,0.26,0.27` (point décimal), lues en configuration de mesure.
- Autres lieux qui énumèrent les réglages d'ombre : `StaticMeshRendererComponent.ApplyShadowSettings` (passe par `CopyFrom`) et `WorldEnvironmentSettings` (`Save`, `Load`, `Clone`, `CopyFrom` passent par `ShadowSettings`) : inchangés. `ShadowLight` (biais par lumière) n'est pas étendu : mode et teinte sont lus dans `ForwardShadowResources.Settings` par le binder, seulement quand une lumière ombrée est visible.
- Résultats (2026-11-14) : builds des deux solutions 0 erreur ; `dotnet test` : 5166 réussis (5153 + 13 : 10 de `ShadowSettingsTests`, 3 de source), 0 échec.
  - Captures prises dans cette session avant la modification (au `HEAD` précédent) et après, hors dépôt (`engine-captures/e3-*`) : démo sans variable, configuration de mesure, configuration de mesure avec cube lié : identiques octet pour octet (`cmp`) avant et après.
  - Configuration de mesure, `MultiplyLitColor`, teinte (0,25 ; 0,26 ; 0,27) (par canal R, G, B) :

    | Pixel | défaut (`OccludeDirectLight`) | prédit | mesuré |
    |---|---|---|---|
    | `eclaire` (240, 435) | 223, 215, 198 | inchangé | 223, 215, 198 |
    | `ombre` (310, 420) | 64, 64, 64 | valeur `eclaire` du même receveur × teinte = 55,8 ; 55,9 ; 53,5 (±3) | 56, 56, 53 |
    | `reflet` (520, 490), hors ombre | 165, 157, 139 | inchangé | 165, 157, 139 |

    Teinte noire (défaut) : `ombre` = 0, 0, 0. Avec le cube lié : `eclaire` 172, 190, 198 ; `ombre` prédit 43,0 ; 49,4 ; 53,5, mesuré 43, 49, 53.
  - Spéculaire à l'ombre : la démo n'a aucune surface réfléchissante ni brillante dans l'ombre (le receveur est sans spéculaire, la sphère réfléchissante est hors de l'ombre, `reflet` ne change pas). Contrôle fait à part, par une modification temporaire non commitée de la démo (spéculaire du receveur 0,2, puissance 2) : le pixel `ombre` vaut 64 en `OccludeDirectLight` (spéculaire occulté) ; en `MultiplyLitColor` avec une teinte blanche, il vaut le pixel éclairé (R saturé à 255, G 249, B 232) ; avec la teinte (0,25 ; 0,26 ; 0,27) il vaut 64, 65, 63, soit `249 × 0,26 = 64,7` et `232 × 0,27 = 62,6` : le reflet du soleil est teinté comme le reste (un reflet non teinté donnerait environ 90 en G). Non vérifié sur capture : les pixel shaders à réflexion avec texture ou normal map (la démo n'en utilise pas) et le shader d'os (`skinEffect.fx`) : couverts par assertion de source et compilation seulement.
- Commit : `feat(rendering): add a shadow mode that multiplies the lit colour by a shadow colour`

### ✅ E4 — Bord doux (D2)

- Objectif : `ShadowSettings.FilterRadius` (texels, 0 par défaut = les 4 échantillons actuels, valeur négative ou non finie ramenée à 0, clé `filter_radius`) ; au-delà de 0, le receveur échantillonne un disque de Poisson fixe de 12 points mis à l'échelle par le rayon, dans `Lighting.fxh` (`SampleDirectionalShadowPcf`), uniforme `ShadowFilterRadius` déclaré dans les deux shaders (`_ps(c106)` / `_cb(c96.w)`), champ « Filter Radius » dans l'éditeur. Aucune allocation, aucun état GPU changé. Limite de shader (C7) : le contenu DesktopGL doit compiler toutes les techniques de `LitForward.fx` (ps_3_0) avec la texture de détail ; si la limite d'instructions est atteinte, 8 puis 5 points (choix noté) ; en dessous de 5 : ⚠️.
- Fichiers : `CasaEngine/Content/Shaders/Lighting.fxh`, `LitForward.fx`, `skinEffect.fx`, `CasaEngine/Framework/Rendering/Shadows/ShadowSettings.cs`, `ForwardLightBinder.cs`, `ShaderParameterNames.cs`, `CasaEngine.Editor/Controls/EntityDetailsPanel.cs`, `CasaEngine.Demos/Demos/StaticShadowValidationDemo.cs`, `CasaEngine.Tests/Rendering/ShadowSettingsTests.cs`, `LightingShaderCoverageTests.cs`, `docs/decisions/0093-…`, ce plan.
- Démo : `CASAENGINE_SHADOW_DEMO_FILTER_RADIUS` (configuration de mesure).
- **Choix C7 : 12 points conservés.** Le contenu (`LitForward.fx`, 24 techniques, et `skinEffect.fx`) compile sous DesktopGL avec le disque de 12 points et la texture de détail, sans erreur ni avertissement de limite d'instructions ; `LitForward.xnb` passe de 1 172 279 à 1 216 891 octets. Le disque est un tableau fixe de 12 points dans le disque unité (rayons de 0,51 à 0,99, distance minimale entre deux points 0,50).
- Résultats (2026-11-14) : builds des deux solutions 0 erreur ; `dotnet test` : 5175 réussis (5166 + 9 : cas de `ShadowSettingsTests` et 2 cas de source), 0 échec.
  - Rayon 0 (défaut) : démo sans variable, configuration de mesure, avec cube lié, et `MultiplyLitColor` : captures identiques octet pour octet à celles de E3 (`cmp`).
  - Mesure de transition (D4), ligne définie avant la capture du rayon 3, d'après la capture du rayon 0 : colonne de pixels x = 310, y de 421 à 436, canal R, configuration de mesure (ombre 64, lumière 223 : seuils de 10 % et 90 % à 79,9 et 207,1), qui traverse le bord inférieur de l'ombre de la caisse au sol. Position interpolée linéairement entre pixels voisins :

    | Rayon | valeurs y = 427 … 430 | y à 10 % | y à 90 % | largeur 10 % – 90 % |
    |---|---|---|---|---|
    | 0 | 64, 64, 223, 223 (y = 428 à 429) | 428,10 | 428,90 | 0,80 px |
    | 3 | 64, 117, 157, 223 | 427,30 | 429,76 | 2,46 px |
    | 6 (donnée en plus) | — | 426,40 | 430,89 | 4,50 px |

    Critère : largeur(3) ≥ 2 × largeur(0) : 2,46 ≥ 1,60, rapport 3,1 : atteint. Même mesure en `MultiplyLitColor` (56 / 223) : 0,80 et 2,46 px.
  - Non vérifié : le rendu du disque sur le shader d'os et sur les techniques à texture ou normal map (la démo n'en utilise pas) : compilation et assertions de source seulement.
- Commit : `feat(rendering): add a shadow filter radius for soft shadow edges`

### ⏳ E5 — Fondu à la distance maximale (D3)

- Objectif : `ShadowSettings.FadeDistance` (0 par défaut, borné à `MaxDistance`, clé `fade_distance`) ; l'ombre passe linéairement à « éclairé » entre `MaxDistance − FadeDistance` et `MaxDistance` du centre de la carte, distance mesurée dans le plan de la carte, en carré (même forme que la carte, C8) ; uniforme dans les deux shaders ; champ dans l'éditeur.
- Démo : `CASAENGINE_SHADOW_DEMO_FADE_DISTANCE` et `CASAENGINE_SHADOW_DEMO_MAX_DISTANCE`.
- Validation (C8, D5) : captures identiques avec le défaut ; `MAX_DISTANCE` autour de 28 (l'ombre du sol de la colonne de gauche est à environ 26 m de l'axe dans le plan de la lumière) et fondu de quelques mètres ; fraction de fondu attendue calculée et notée avant la capture pour deux pixels du même sol à l'ombre (un hors de la bande, un dedans) ; mesure à ±3 ; avec fondu 0 les deux pixels sont égaux.
- Commit : `feat(rendering): fade shadows out before the shadow distance`

### ⏳ E5b — Profondeur de la carte séparée de son étendue latérale (C4, D3)

- Objectif : `ShadowSettings.DepthRange` (0 par défaut = la profondeur suit `MaxDistance` ; clé `depth_range` ; borné ≥ 0) : positif, la lumière est placée à `DepthRange` en amont et le plan lointain à 2 × `DepthRange`, l'étendue latérale restant `MaxDistance`. Alimente seulement la matrice de `ShadowPass` : aucun uniforme de shader. Champ dans l'éditeur.
- Test obligatoire sur `ShadowPass.BuildDirectionalShadowViewProjection` (surcharge ; `ShadowPassTexelSnappingTests` doit continuer à compiler) : (1) `DepthRange` 0 : matrice identique au bit près à l'actuelle ; (2) `DepthRange` > `MaxDistance` : un point en amont de la caméra d'une distance d, `MaxDistance` < d < `DepthRange`, a une profondeur dans ]0, 1[ alors qu'il était coupé ; (3) coordonnées latérales et alignement sur les texels inchangés, à une tolérance près (F8).
- Démo : `CASAENGINE_SHADOW_DEMO_DEPTH_RANGE` ; affirmation réduite : capture identique octet pour octet sans la variable ; avec elle, l'ombre de la colonne de gauche est toujours présente (valeurs de pixel notées).
- ADR et documentation : relation entre le biais de profondeur et `DepthRange`, facteur DesktopGL compris (F6, voir plus haut).
- Commit : `feat(rendering): separate the shadow depth range from its lateral extent`

### ⏳ E6 — Documentation

- Objectif : `docs/engine/` décrit les réglages d'ombre (mode, teinte, rayon de filtre, fondu, profondeur : résumé, extrait d'usage, limites) ; index `docs/README.md` à jour ; plan clos pour E3 → E6.
- Commit : `docs(engine): document the shadow settings`

---

## Points ouverts

| Réf | Sujet | Tâche concernée |
|---|---|---|
| O1 | E1 éclaircit l'ambiant de toutes les scènes du moteur (démos, jeux d'exemple, éditeur). Leur réajustement n'est pas dans ce plan : il est listé dans l'ADR comme suite. | E1 |
| O2 | S2b (E3 → E6) n'a pas passé de revue de plan indépendante concluante (la revue de clôture n'a pas rendu READY) : exécutée sur la demande de l'auteur du 2026-11-14 ; la vérification indépendante de fin de tranche (T4, côté jeu) reste à faire. | E3 → E6 |

## Hors périmètre

- Réajustement des scènes existantes du moteur après E1.
- Post-traitement (flou d'ombre en espace écran, glow), ciel, matériaux de RacingGame.
- Push, merge, pointeur du sous-module dans RacingGame.
