# Plan agent IA — Décalage d'ombre le long de la normale géométrique

Correction d'un défaut du moteur trouvé par l'analyse du chantier RacingGame le 2026-11-18 (plan du jeu `ai-agent/tasks/rgce-road-tunnel-shadow-tasks.md` ; point O9 de `ai-agent/tasks/rgce-render-gaps-tasks.md` du jeu). L'auteur a demandé ce plan le 2026-11-18 (« Oui, écris le plan ») ; les corrections de défauts prouvés du moteur sont autorisées par sa réponse du 2026-11-17. Approuvé par l'auteur le 2026-11-18 (« Oui, en autonomie ») : exécution en mode autonome, sans push ni avance de `main`.

Ce fichier doit être mis à jour pendant le travail : l'icône au début de chaque tâche indique son statut courant.

## Objectif

Le décalage `NormalBias` du point de lecture de l'ombre directionnelle se fait le long de la normale géométrique du récepteur (la normale de sommet interpolée), dans toutes les techniques éclairées, et plus le long de la normale perturbée par la normal map. Une normal map ne déplace plus le bord d'une ombre.

## État vérifié du dépôt (2026-11-18, branche `chantier/draw-order-emissive-shadow` = `4e867fec3`, `main` = `8a73c5329`)

Lu par la session principale ; à revérifier par l'exécutant avant de modifier :

- `CasaEngine/Content/Shaders/Lighting.fxh:219-262`, `ComputeDirectionalShadowFactor(worldPosition, worldNormal)` : `biasedWorldPosition = worldPosition + worldNormal * ShadowNormalBias` (`:226`).
- `CasaEngine/Content/Shaders/Pbr.fxh:329`, dans `EvaluatePbrLights(surface, worldPosition, numDirectionalLights)` : `ComputeDirectionalShadowFactor(worldPosition, surface.Normal)`. `surface.Normal` est la normale donnée à `ComputePbrSurface` (`Pbr.fxh:93`), c'est-à-dire la normale perturbée dans les techniques à normal map de `LitPbr.fx` (`PSLitPbrNormalMap`, `PSLitPbrNormalMapAlphaTest`, `PSLitPbrNormalMapTransparent` : `ComputePerturbedNormal`, puis `ShadePbr(..., worldNormal, normalize(pin.NormalWS), ...)`). `ShadePbr` (`LitPbr.fx:504`) reçoit déjà la normale géométrique (`geometricNormal`) et ne s'en sert que pour le vernis. Dans les techniques sans normal map, les deux normales passées à `ShadePbr` sont la même expression, `normalize(pin.NormalWS)`.
- `CasaEngine/Content/Shaders/Lighting.fxh:502`, dans `ComputeLights(eyeVector, worldPosition, worldNormal, numDirectionalLights)` : même appel avec `worldNormal`. `LitForward.fx` lui passe une normale perturbée dans ses deux techniques à normal map (`:673-676`, `:727-730`, `worldNormal = normalize(mul(normalMap, TBN))`) et `normalize(pin.NormalWS)` ailleurs (`:525`, `:561`, `:597`, `:622`, `:697`, `:710`).
- `skinEffect.fx` (`:392-394`, `:451-455`, `:571-574`) ne passe que la normale de sommet : son image ne doit pas changer.
- Mesure du jeu (`artifacts/pbr-handover/s21/` du dépôt du jeu, planche `s21/judge/2-tunnel-ombre-essai.jpg`) : sur une paroi de tunnel à normal map de briques, `NormalBias` 0,1 donne un bord d'ombre déchiqueté et moucheté ; 0 le rend net ; 0,3 l'aggrave selon le relief des briques ; le rayon du filtre et le biais de profondeur n'y changent rien.
- `docs/engine/shadow-settings.md:12` décrit `NormalBias` comme « Offset of the receiver position along its normal, in world units. », sans dire laquelle.
- `NormalBias` vaut 0 par défaut (`Framework/Rendering/Shadows/ShadowSettings.cs`). Des démos le règlent : `CasaEngine.Demos/Demos/PbrMaterialDemo.cs`, `StaticShadowValidationDemo.cs`, `SkinnedMeshDemo.cs` (valeurs à relever par l'exécutant).
- Modèle de test GPU : `CasaEngine.Tests/Rendering/LitPbrEmissiveShadowGpuTests.cs` (un rendu d'un pixel de `LitPbr.fx` sur un vrai périphérique, sommets `VertexPositionNormalTexture`, lumière directionnelle noire à l'indice ombré, carte d'ombre fournie par le test, émissive ombrée comme seule source de couleur). Le harnais GPU charge aussi `LitForward` (`CasaEngine.Tests/Rendering/IblCacheTests.cs:72`).
- Des tests épinglent le texte des shaders : `PbrShaderCoverageTests.cs:182-183`, `LightingShaderCoverageTests.cs:32-38` et `:262` (détail en T1).
- Dans `LitForward.fx`, l'émissive ne reçoit pas l'ombre et l'éclairage suit la normale perturbée (`:673-685`).
- Références relevées sur `4e867fec3` : 6441 tests, 0 échec ; `pbrdemo.sh` par défaut `54e406a1dde8c171` ; `lin-v2.sh` `39f8355d905a14a8` (scripts dans `CasaEngine/artifacts/pbr-baseline/`). `capture-v2.sh` a deux démos dont l'empreinte varie d'un lancement à l'autre pour une raison connue et sans rapport (`ui-overlay`, `environment` : entrée réelle et horloge, O3 du plan `draw-order-emissive-shadow-tasks.md`).
- Dernier ADR : 0126.

## Décisions verrouillées

| Réf | Décision |
|---|---|
| D1 | La normale donnée à `ComputeDirectionalShadowFactor` est, dans chaque technique de `LitPbr.fx`, `LitForward.fx` et `skinEffect.fx`, la normale de sommet interpolée et normalisée, celle que la technique calcule déjà (`normalize(pin.NormalWS)`, ou son équivalent dans `skinEffect.fx`). L'éclairage garde la normale perturbée. |
| D2 | Aucun nouveau réglage, aucune clé de matériau, aucun uniforme, aucune permutation. `ComputeDirectionalShadowFactor` garde sa signature ; ce sont ses appelants (`EvaluatePbrLights`, `ComputeLights`) qui reçoivent la normale géométrique, par un paramètre ajouté ou par un champ de `PbrSurface`, au choix de l'exécutant, sans dupliquer de fonction. |
| D3 | Aucun traitement nouveau des faces arrière : la normale utilisée est exactement celle que la technique a déjà. |
| D4 | Pas d'ADR : c'est la correction d'un défaut, sans décision d'architecture, de format ni d'API publique. `docs/engine/shadow-settings.md` précise que le décalage suit la normale géométrique et qu'une normal map ne le déplace pas. |
| D5 | Branche `chantier/shadow-normal-offset`, créée depuis `chantier/draw-order-emissive-shadow` (`4e867fec3`) et non depuis `main` : le jeu a besoin des deux chantiers ensemble et `main` (`8a73c5329`) n'a pas encore le premier. Écart à la règle « branche depuis `main` », à confirmer par l'auteur à l'approbation (O1). `main` n'est pas avancé et rien n'est poussé par ce plan. |

## Règles d'exécution pour l'agent

- Branche dédiée, jamais de commit sur `main`, jamais de push ; `git add` fichier par fichier ; aucune commande git dans `MGUI` ni `NvgSharp` ; jamais de `git submodule update`, `checkout`, `switch` ou `reset`.
- Builds `dotnet build CasaEngine.MonoGame.sln` et `dotnet build CasaEngine.Editor.MonoGame.sln` (0 erreur) ; tests `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj --blame-hang-timeout 5m`, toujours avec build, 0 échec.
- Un commit par tâche, ce plan mis à jour dans le même commit ; message en anglais `type(area): summary` ; code, commentaires et docs en anglais ; ce plan en français. Fins de ligne des fichiers existants conservées.
- Le changement de comportement commence par un test qui échoue sur le code d'avant, puis passe ; la sortie du test en échec est gardée dans la note de la tâche.
- Une démo ou un test GPU à la fois, jamais pendant un build. Le jeu n'est ni construit ni lancé par ce plan.
- Arrêt (tâche en ⚠️, question dans « Points ouverts ») : un test en échec qui n'est ni celui que la tâche vise ni une des assertions de texte nommées en T1 ; une empreinte qui change sans que le changement soit attribué à un récepteur à normal map sous un `NormalBias` non nul (voir T1) ; une empreinte déterministe qui diffère entre deux lancements du même code, hors des deux démos nommées plus haut.

## Légende des statuts

⏳ Todo · 🚧 In progress · 🧪 Needs testing · ✅ Done · ⚠️ Blocked

## Validation globale

- Les deux solutions à 0 erreur ; tous les tests à 0 échec, les nouveaux compris.
- Empreintes `pbrdemo.sh` par défaut, `lin-v2.sh`, `capture-v2.sh` : inchangées, ou changement nommé, regardé et attribué (T1).
- Vérification indépendante par un `verifier` frais (T2).

---

## Tâches

### ✅ T0 — Branche et inscription du plan

- Créer `chantier/shadow-normal-offset` depuis `4e867fec3` (D5) ; ajouter la ligne de ce plan en tête du tableau de `ai-agent/README.md`.
- Validation : `git status` ne montre que ces deux fichiers ; aucun code touché.
- Commit : `docs(ai-agent): plan the geometric normal of the shadow offset`

> Validation (2026-11-18) : plan approuvé par l'auteur le 2026-11-18 (« Oui, en autonomie »), mode autonome ; sans avis contraire de sa part, la branche part de `chantier/draw-order-emissive-shadow` (D5, O1). Branche `chantier/shadow-normal-offset` créée sur `4e867fec3` ; `git status` ne montre que ce plan et `ai-agent/README.md` ; aucun code touché.

### ✅ T1 — Le décalage d'ombre suit la normale géométrique (D1 à D4)

- Fichiers : `CasaEngine/Content/Shaders/Pbr.fxh`, `Lighting.fxh`, `LitPbr.fx`, `LitForward.fx`, `skinEffect.fx` (seulement si une signature partagée l'exige) ; nouveau `CasaEngine.Tests/Rendering/ShadowNormalOffsetGpuTests.cs` ; `CasaEngine.Tests/Rendering/PbrShaderCoverageTests.cs` et `LightingShaderCoverageTests.cs` (assertions de texte nommées ci-dessous) ; `docs/engine/shadow-settings.md` ; ce plan.
- Assertions de texte existantes que la correction casse forcément, à mettre à jour et non à supprimer (elles épinglent le texte exact des appels que D2 fait changer) : `PbrShaderCoverageTests.cs:183` (`ComputeDirectionalShadowFactor(worldPosition, surface.Normal);` dans `EvaluatePbrLights`) ; `LightingShaderCoverageTests.cs:262` (une seule occurrence de `ComputeDirectionalShadowFactor(worldPosition, worldNormal);` dans `Lighting.fxh`) ; `LightingShaderCoverageTests.cs:32`, `:35`, `:36` (et leurs négations `:33`, `:37`, `:38`), texte exact des appels de `ComputeLights` dans `LitForward.fx` et `skinEffect.fx`, si `ComputeLights` change de signature. Après la correction, chacune épingle le nouveau texte exact et continue de prouver ce qu'elle prouvait : le facteur d'ombre est calculé une seule fois par pixel, hors de la boucle des lumières (`PbrShaderCoverageTests.cs:182` garde son compte de 1), et la normale passée à `ComputeDirectionalShadowFactor` est la normale géométrique. Ces assertions, et elles seules, sont exemptées de l'arrêt « autre test en échec ».
- Étapes :
  1. Relever avant toute modification, sur `4e867fec3` : le nombre de tests, et les empreintes `pbrdemo.sh` par défaut, `lin-v2.sh` et `capture-v2.sh`, chacun lancé deux fois dans deux processus. Relever dans les démos quelles scènes ont à la fois un `NormalBias` non nul, des ombres actives et un récepteur à normal map : ce sont les seules dont l'empreinte peut changer.
  2. Écrire le test GPU. `LitPbrEmissiveShadowGpuTests` sert de modèle pour le périphérique, la cible de rendu et la carte d'ombre fournie par le test ; la scène elle-même est nouvelle (image de plusieurs pixels, sommets avec tangentes, technique à normal map). Scène commune aux deux shaders : un quad face à la caméra, une carte d'ombre fournie par le test avec un bord droit et franc (une moitié occultée, l'autre non), filtre de rayon 0, `ShadowFadeWidth` 0, une lumière directionnelle oblique placée de sorte que la normale géométrique du quad ait une composante perpendiculaire au bord dans l'espace de la carte d'ombre.
     - Grandeur observée, la même pour les deux shaders : la **colonne du bord**. Une image rendue avec une normal map uniforme a deux paliers, celui des pixels à l'ombre et celui des pixels hors de l'ombre, séparés par une bande de pixels intermédiaires : même au rayon 0, le filtre moyenne quatre lectures à ± 0,5 texel (`Lighting.fxh:205-212`), donc la visibilité y vaut 0,25, 0,5 ou 0,75. Ces pixels sont attendus et n'appartiennent à aucun palier. Le test fixe lui-même l'état d'échantillonnage de la carte d'ombre, en point (`SamplerState.PointClamp`), pour que la bande ne dépende pas d'un filtrage par défaut. Chaque rendu est normalisé entre ses deux propres paliers (0 = palier à l'ombre, 1 = palier hors de l'ombre) ; la colonne du bord d'une ligne est la première colonne, en partant du côté à l'ombre, dont le niveau normalisé dépasse 0,375, seuil qui ne peut égaler aucun niveau du filtre (0 ; 0,25 ; 0,5 ; 0,75 ; 1) et que l'arrondi ne peut donc pas faire basculer. Elle ne dépend pas de la différence d'éclairage entre deux normal maps. Chaque palier doit couvrir au moins un quart de l'image, sinon le test échoue : un rendu tout éclairé ou tout à l'ombre ne prouve rien.
     - Cas 1, la normal map ne déplace pas le bord : avec un `ShadowNormalBias` fixé par le test, deux rendus, l'un avec une normal map plate, l'autre avec une normal map uniforme fortement inclinée dans la direction qui traverse le bord. Le test calcule, à partir de l'inclinaison, du biais et de l'échelle de la projection de la carte d'ombre, le déplacement que la normale perturbée donnerait, et choisit le biais pour qu'il vaille au moins 4 pixels (calcul écrit en commentaire). Assertions : la colonne du bord du rendu plat est à la colonne attendue à 1 pixel près ; la colonne du bord du rendu incliné est la même que celle du rendu plat, sur chaque ligne. Sur `4e867fec3`, ce cas échoue (bord déplacé d'au moins 4 pixels) ; la sortie est gardée.
     - Cas 2, le décalage agit toujours : avec la normal map plate, `ShadowNormalBias` 0 puis la valeur du test ; la colonne du bord se déplace du nombre de pixels calculé à partir de la normale géométrique, à 1 pixel près, et d'au moins 2 pixels. Ce cas échoue si le décalage est supprimé au lieu d'être corrigé.
     - `LitPbr.fx` (technique `LitPbr_NormalMap`) : la couleur vient de l'émissive ombrée (`emissive_shadow_factor` 1, lumière noire, pas d'ambiant ni d'IBL), comme dans le modèle.
  3. Les deux mêmes cas pour `LitForward.fx` (technique à normal map), que le harnais de test GPU sait déjà charger (`IblCacheTests.cs:72`). L'émissive n'y reçoit pas l'ombre et l'éclairage y suit la normale perturbée : la configuration qui rend l'ombre visible (lumière directionnelle colorée en mode par défaut, ou mode `MultiplyLitColor` avec une teinte) est au choix de l'exécutant, puisque la grandeur observée est la colonne du bord et non le niveau des pixels. Si l'exécutant n'arrive pas à rendre cette technique avec une carte d'ombre fournie : tâche en ⚠️, question dans « Points ouverts », pas de repli silencieux.
  4. Corriger (D1 à D3). Dans `LitPbr.fx`, `ShadePbr` transmet `geometricNormal` jusqu'à l'appel de `ComputeDirectionalShadowFactor`. Dans `LitForward.fx`, les deux techniques à normal map transmettent `N` (la normale de sommet déjà calculée, `:669`, `:723`). Les techniques sans normal map passent la même valeur qu'aujourd'hui. Commentaires des fonctions touchées mis à jour.
  5. `docs/engine/shadow-settings.md` : la ligne de `NormalBias` et, si la doc a une section sur les biais, une phrase : le décalage suit la normale de sommet interpolée, la normal map ne le modifie pas.
- Validation :
  - builds à 0 erreur ; tests à 0 échec ; le nouveau test échoue sur `4e867fec3` et passe après ;
  - empreintes relancées deux fois : identiques entre les deux lancements (hors `ui-overlay` et `environment`). Comparées à celles de l'étape 1 : une empreinte ne peut changer que pour une scène relevée à l'étape 1 (normal map, ombres, `NormalBias` non nul) ; chaque changement est nommé, les images d'avant et d'après sont regardées, et la note dit ce qui a changé à l'œil (attendu : seuls des bords d'ombre sur des surfaces à normal map bougent). Tout autre changement : arrêt ;
  - contrôle statique, cité ligne par ligne dans la note : les deux appels de `ComputeDirectionalShadowFactor` (`Lighting.fxh`, `Pbr.fxh`) et chacun des dix sites où la normale est choisie, avec la normale d'éclairage et la normale d'ombre que chacun passe : `ComputeLights` dans `LitForward.fx` (`:527`, `:563`, `:599`, `:624`, `:676`, `:730`) et `skinEffect.fx` (`:394`, `:574`) ; `EvaluatePbrLights` dans `skinEffect.fx` (`:455`) et `LitPbr.fx` (`:534`, par `ShadePbr` et ses six techniques). Dans chacun, la normale d'ombre est la normale de sommet.
- Retour arrière : `git revert` du commit.
- Commit : `fix(rendering): offset the shadow lookup along the geometric normal`

> Validation (2026-11-18) : conception retenue : un paramètre ajouté, `float3 shadowNormal`, à `ComputeLights(eyeVector, worldPosition, worldNormal, shadowNormal, numDirectionalLights)` (`Lighting.fxh`) et à `EvaluatePbrLights(surface, worldPosition, shadowNormal, numDirectionalLights)` (`Pbr.fxh`) ; `ComputeDirectionalShadowFactor` garde sa signature, aucune fonction dupliquée, aucun réglage, uniforme ni permutation. `ShadePbr` passe `geometricNormal` (déjà reçue) ; `LitForward.fx` passe `N` dans les deux techniques à normal map ; ailleurs, la même normale deux fois.
>
> - Avant (4e867fec3) : 6441 tests, 0 échec. Empreintes (deux processus chacune) : `pbrdemo.sh` par défaut `54e406a1dde8c171` ×2 ; `lin-v2.sh` `39f8355d905a14a8` ×2 ; `capture-v2.sh` : `material` 40e2df3c20090e1f, `static-model` efae412c45225453, `static-shadow` 536a8183f8100d2e, `render-to-texture` e62b6b73a06f0939, `frozen-frame` eb25f3917cfa6a0c, `tile-map` 625f42300bcbd4ff, identiques entre les deux lancements ; `environment` (d1ff7d4a258b96ea / 4423dd1487a939b6) et `ui-overlay` (08df0ac0282ae9c5 / 9d6c39bc6a2398b5) varient (cause connue). Les images de `skinned`, `particles` et `split-screen` varient aussi d'un lancement à l'autre sur ce code (trois lancements supplémentaires chacun, trois valeurs différentes) : le délai de capture est une durée, pas un compte d'images (`DemosGame.ResolveAutomationScreenshotDelay`), donc l'animation, les particules et l'étiquette du haut de `split-screen` (diff localisé en y 33 à 77) en dépendent ; ce sont des empreintes non déterministes dans cet environnement, hors du critère « déterministe ».
> - Scènes à la fois `NormalBias` non nul, ombres actives et récepteur à normal map : aucune. `NormalBias` est réglé à 0,002 par `PbrMaterialDemo` (sphères et sol `LitPbrMaterial` sans normal map ; personnage skinné via `skinEffect.fx`), `StaticShadowValidationDemo` (récepteurs texturés, sans normal map) et `SkinnedMeshDemo` (`skinEffect.fx`, normale de sommet seule) ; aucun fichier de monde ne le règle (défaut 0). Aucune empreinte ne doit donc changer.
> - Test GPU (`ShadowNormalOffsetGpuTests`, 4 tests : cas 1 et cas 2 pour `LitPbr_NormalMap` et pour `LitForward_PixelLighting_Texture_NormalMap`) : image de 64 × 8 pixels, carte d'ombre 100 × 100 fournie par le test (moitié gauche occultée), échantillonneur en point, biais 0,15, matrice de lumière oblique (cisaillement 1). Colonne du bord attendue (calcul en commentaire) : 27 avec la normale de sommet, 31 sans biais, 33 avec la normale inclinée de 60 degrés, soit 6 pixels d'écart (au moins 4 exigés) et 4 pixels de déplacement du biais (au moins 2 exigés). Avant la correction (sortie gardée, `artifacts/pbr-handover/s22/engine/before/new-test-unfixed.txt`), cas 1 en échec pour les deux shaders : `row 0: edge at column 33 with the tilted normal map, 27 with the flat one` (colonnes 27 avec la carte plate, 33 avec la carte inclinée, sur les 8 lignes) ; cas 2 réussi (31 puis 27 pour `LitPbr`, 32 puis 27 pour `LitForward`, à 1 pixel de l'attendu). Après : cas 1 réussi (27 et 27 sur toutes les lignes), cas 2 réussi.
> - Après : builds `CasaEngine.MonoGame.sln` et `CasaEngine.Editor.MonoGame.sln` à 0 erreur ; `dotnet test` (avec build) : 6445 tests, 0 échec (6441 + les 4 nouveaux). Assertions de texte mises à jour (même preuve, nouveau texte exact) : `PbrShaderCoverageTests.cs:185` (`ComputeDirectionalShadowFactor(worldPosition, shadowNormal);`, compte de 1 conservé, plus la signature de `EvaluatePbrLights` et l'appel de `ShadePbr` avec `geometricNormal`), `LightingShaderCoverageTests.cs:32-43` (les appels de `ComputeLights` de `LitForward.fx` et `skinEffect.fx`, avec leurs négations) et `:268` (une occurrence de `ComputeDirectionalShadowFactor(worldPosition, shadowNormal);` dans `Lighting.fxh`). Aucun autre test touché.
> - Empreintes après, deux lancements chacune : `pbrdemo.sh` `54e406a1dde8c171` ×2, `lin-v2.sh` `39f8355d905a14a8` ×2, `capture-v2.sh` `material`, `static-model`, `static-shadow`, `render-to-texture`, `frozen-frame`, `tile-map` : les mêmes valeurs qu'avant. Aucun changement d'empreinte, donc rien à attribuer ni à regarder (attendu : aucune scène des démos ne combine les trois conditions). `environment` 4423dd1487a939b6 ×2 et `ui-overlay` 9d6c39bc6a2398b5 ×2 (valeurs déjà vues avant). `skinned`, `particles`, `split-screen` : non déterministes comme avant, donc non comparables par empreinte ; pour `skinned` la preuve est plus forte que l'empreinte : `skinEffect.xnb` compilé est identique octet pour octet avant et après (SHA-256 `689a1aaf4b8c464d...`), l'image du personnage ne peut pas changer ; `particles` et `split-screen` ne règlent pas `NormalBias` (0 : le décalage est nul quelle que soit la normale).
> - Contrôle statique (`CasaEngine/Content/Shaders/`, normale d'éclairage / normale d'ombre) : les deux appels de `ComputeDirectionalShadowFactor` : `Lighting.fxh:505` (dans `ComputeLights`, avec `shadowNormal`) et `Pbr.fxh:331` (dans `EvaluatePbrLights`, avec `shadowNormal`). Les dix sites : `LitForward.fx:527` `worldNormal` / `worldNormal` (`normalize(pin.NormalWS)`) ; `:563` idem ; `:599` idem ; `:624` idem ; `:677` `worldNormal` (carte) / `N` (`normalize(pin.NormalWS)`, `:669`) ; `:732` `worldNormal` (carte) / `N` (`:724`) ; `skinEffect.fx:394` `worldNormal` / `worldNormal` (`normalize(input.Normal3D)`) ; `:455` `surface.Normal` (de `worldNormal`) / `worldNormal` ; `:574` `N` / `N` (`normalize(input.Normal3D)`) ; `LitPbr.fx:535` (`ShadePbr`) `surface.Normal` / `geometricNormal`, que les six techniques passent en 4e argument (`:614`, `:622`, `:630`, `:639`, `:646`, `:654`) sous la forme `normalize(pin.NormalWS)`. Dans chacun, la normale d'ombre est la normale de sommet.
> - Remarque : sur `LitForward` sans biais, l'image donne la colonne 32 au lieu de 31 pour `LitPbr` (écart de moins d'un pixel entre les deux shaders, dans la tolérance de 1 pixel du plan) ; cause non recherchée, sans effet sur les deux cas.

### ⏳ T2 — Vérification indépendante et clôture

- Un `verifier` frais reçoit l'affirmation exacte (« dans toutes les techniques éclairées, le point de lecture de l'ombre est décalé le long de la normale de sommet ; une normal map ne change plus le bord de l'ombre ; les images sans normal map ou à `NormalBias` 0 sont inchangées »), le diff `4e867fec3..HEAD` et les commandes de la validation de T1. Il refait builds, tests et empreintes, vérifie que le test échoue sur le code d'avant, et rend CONFIRMED, REFUTED ou INCONCLUSIVE.
- Constats traités selon leur priorité (P0 et P1 : correction dans le périmètre ou arrêt ; P2 introduit : bloquant ; le reste noté).
- Mettre à jour la ligne de ce plan dans `ai-agent/README.md` (état, ce qui reste à l'auteur).
- Commit : `docs(ai-agent): record the verification of the shadow normal offset`

---

## Hors périmètre

- Les réglages d'ombre du jeu et des démos ; le filtrage, la résolution et la projection de la carte d'ombre ; un flou de l'ombre comme celui du jeu original.
- Le traitement des faces arrière des matériaux à deux faces.
- L'avance de `main`, le push, la fusion de `chantier/draw-order-emissive-shadow`.

## Points ouverts

- O1 (auteur, à l'approbation) : la branche part de `chantier/draw-order-emissive-shadow` et non de `main` (D5). L'autre voie : avancer d'abord `main` sur ce chantier, puis partir de `main`.
- O2 (auteur, après T2) : avance de `main` et push de la branche.
