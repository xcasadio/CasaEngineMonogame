# Plan agent IA — Lumière ambiante et ombres (chantier RacingGame S2)

Plan d'exécution des tâches moteur de la tranche S2 « lumière et ombres » du chantier de fidélité 3D de RacingGame, décrite dans `RacingGame/ai-agent/tasks/rgce-3d-lighting-shadows-tasks.md` (révision 5, amendements A1 à A8). Ce plan en recopie les tâches moteur E0 → E6, sans en changer le contenu.
Les décisions D1 → D3, D5 → D8 ci-dessous ont été arbitrées avec l'auteur : **ce plan les applique, il ne les rediscute pas**.

Ce fichier doit être mis à jour pendant le travail : l'icône au début de chaque tâche indique son statut courant.

> **Périmètre approuvé : E0, E1 et E2 uniquement (ambiant).** E3 → E6 (ombres) ne sont **pas approuvées** : elles repassent en revue indépendante puis devant l'auteur avec les mesures de S2a. **Ne pas les exécuter.**

## Objectif

Le moteur calcule l'ambiant comme `texture × DiffuseColor × (N·L × lumière + ambiant du monde × ambiant du matériau) + émissif` (E1), peut garder un ambiant plat quand un cube de réflexion est lié au monde (E2) et, après approbation de S2b, sait appliquer l'ombre comme l'original de RacingGame : couleur éclairée multipliée par une teinte, bord doux, fondu à distance (E3 → E5, documentation E6).

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
| D1 | Ombre : `ShadowSettings.Mode`, `OccludeDirectLight` (défaut, comportement actuel) ou `MultiplyLitColor` (la couleur éclairée entière est multipliée par `lerp(1, ShadowColor, ombre)`), plus `ShadowColor` (défaut noir). **S2b, non approuvée.** |
| D2 | Douceur : `ShadowSettings.FilterRadius` en texels (0 = les 4 échantillons actuels ; au-delà, disque de Poisson fixe). **S2b, non approuvée.** |
| D3 | Fondu : `ShadowSettings.FadeDistance` (0 = coupure nette actuelle). **S2b, non approuvée.** |
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
| `CASAENGINE_SHADOW_DEMO_MODE`, `…_COLOR` | Mode d'ombre et teinte | E3 (non approuvée) |
| `CASAENGINE_SHADOW_DEMO_FILTER_RADIUS` | Rayon de filtre | E4 (non approuvée) |
| `CASAENGINE_SHADOW_DEMO_FADE_DISTANCE` | Distance de fondu | E5 (non approuvée) |

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

---

## Phase 2 — Ombres (S2b) — NON APPROUVÉE, NE PAS EXÉCUTER

Exigences déjà retenues : en `MultiplyLitColor`, le spéculaire de la lumière qui projette l'ombre et la contribution de réflexion sont multipliés par la même teinte que la couleur éclairée (l'original multiplie l'image entière) ; E3, E4 et E5 prouvent chacun leur comportement non par défaut par une capture de démo pilotée par variable d'environnement, valeurs de pixel notées.

### ⏳ E3 — Mode d'application et teinte de l'ombre (D1)

- Objectif : `ShadowSettings.Mode` (`OccludeDirectLight` par défaut, `MultiplyLitColor`) et `ShadowSettings.ShadowColor` ; lus et écrits de façon optionnelle (`mode`, `shadow_color`), copiés par `CopyFrom`, remis par `ResetToDefaults` ; liés par `ForwardLightBinder` ; déclarés dans `LitForward.fx` et `skinEffect.fx` (registres sans collision) ; appliqués dans `Lighting.fxh`. En `MultiplyLitColor`, la lumière directe n'est plus occultée et la couleur éclairée (direct + ambiant, hors émissif) est multipliée par `lerp(1, ShadowColor, ombre)`. La démo gagne `CASAENGINE_SHADOW_DEMO_MODE` et `CASAENGINE_SHADOW_DEMO_COLOR`.
- Un seul ADR couvre D1, D2 et D3.
- Tests : chargement, sauvegarde, copie et défauts de `ShadowSettings` ; `LightingShaderCoverageTests` : nouveaux uniformes dans les deux shaders, assertion « l'ambiant n'est pas ombré » restreinte au mode par défaut.
- Validation : builds, tests ; démo identique à celle de E2 avec les défauts ; avec `MultiplyLitColor` et une teinte grise, l'ombre n'est plus noire.
- Commit : `feat(rendering): add a shadow mode that multiplies the lit colour by a shadow colour`

### ⏳ E4 — Bord doux (D2)

- Objectif : `ShadowSettings.FilterRadius` (texels, 0 = filtrage actuel) ; au-delà, disque de Poisson fixe de 12 points mis à l'échelle par le rayon, dans `Lighting.fxh`, uniforme déclaré dans les deux shaders ; valeur négative ou non finie ramenée à 0 ; démo `CASAENGINE_SHADOW_DEMO_FILTER_RADIUS`.
- Validation : démo identique à E3 avec le défaut ; avec un rayon de 3, la transition s'étale sur plusieurs pixels (largeur mesurée).
- Commit : `feat(rendering): add a shadow filter radius for soft shadow edges`

### ⏳ E5 — Fondu à la distance maximale (D3)

- Objectif : `ShadowSettings.FadeDistance` (0 par défaut) ; l'ombre passe linéairement à « éclairé » entre `MaxDistance − FadeDistance` et `MaxDistance` ; uniforme dans les deux shaders ; démo `CASAENGINE_SHADOW_DEMO_FADE_DISTANCE` (deux distances).
- Validation : démo identique à E4 avec le défaut.
- Commit : `feat(rendering): fade shadows out before the shadow distance`

### ⏳ E6 — Documentation

- Objectif : `docs/engine/` décrit les réglages d'ombre et la source d'ambiant ; index `docs/README.md` à jour ; plan clos.
- Commit : `docs(engine): document the shadow and ambient lighting settings`

---

## Points ouverts

| Réf | Sujet | Tâche concernée |
|---|---|---|
| O1 | E1 éclaircit l'ambiant de toutes les scènes du moteur (démos, jeux d'exemple, éditeur). Leur réajustement n'est pas dans ce plan : il est listé dans l'ADR comme suite. | E1 |
| O2 | Approbation de S2b (E3 → E6) par l'auteur, après revue indépendante. | E3 → E6 |

## Hors périmètre

- Réajustement des scènes existantes du moteur après E1.
- Post-traitement (flou d'ombre en espace écran, glow), ciel, matériaux de RacingGame.
- Push, merge, pointeur du sous-module dans RacingGame.
