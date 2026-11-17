# Plan agent IA — Ordre de dessin déterministe et émissive ombrée

Deux évolutions du moteur autorisées par l'auteur le 2026-11-17 pour le chantier RacingGame (plan du jeu `ai-agent/tasks/rgce-render-gaps-tasks.md`, points O16 et O17 de `rgce-pbr-handover-tasks.md`), plus deux corrections de documentation. Mode autonome accordé par l'auteur le 2026-11-17 (« fait toutes les tâches suivantes de manière autonome »).

Ce fichier doit être mis à jour pendant le travail : l'icône au début de chaque tâche indique son statut courant.

## Objectif

1. L'ordre de dessin de `StaticMeshRendererComponent` est entièrement déterminé par ce qui est soumis et par l'ordre de soumission : il ne dépend plus d'empreintes d'identité d'objets ni de la stabilité du tri.
2. Un matériau `lit-pbr` peut demander que son émissive reçoive l'ombre (`emissive_shadow_factor`, 0 par défaut : aucune image existante ne change).
3. Le commentaire XML de `GeneratedAssetHandleCache` et deux lignes de `ai-agent/README.md` disent ce qui est vrai.

## État vérifié du dépôt (2026-11-17, `main` = `8a73c5329`)

Relevé en lecture seule par un enquêteur, à revérifier par l'exécutant avant de modifier (`rg`, lecture des lignes citées) :

- Tri : `CasaEngine/Framework/Application/Components/StaticMeshRendererComponent.cs:404` trie `_renderItems` par `List<T>.Sort(Comparison)` (tri non stable) sur `SortKey` seul. Des clés égales sont courantes (même maillage et même matériau à plusieurs transformations, sous-maillages d'un maillage, collisions de la troncature à 16 bits de `SortKeyGenerator.cs:57-66`, objets transparents au même pas de distance de 0,1). La partie « maillage » de la clé est `vb.GetHashCode()` (`:358`, `:396`), empreinte d'identité du CLR si MonoGame ne la redéfinit pas (non vérifié : pas de source locale). `OpaqueAndTransparentPasses.LowerBound` suppose `SortKey` croissant. `SceneDepthPrepassGpuTests.cs:175` a sa propre comparaison.
- Émissive : dans `Content/Shaders/LitPbr.fx` (`ShadePbr`, vers `:521-557`), l'émissive est ajoutée après l'éclairage et passée à `ApplyShadowTint` comme terme exclu (`Lighting.fxh:276-290`) ; en mode d'ombre 0, la visibilité ne module que la lumière directionnelle ombrée (`Pbr.fxh:330,348`). Aucun paramètre de matériau n'y change rien. Modèle à suivre pour un paramètre scalaire de `lit-pbr` sans permutation : ADR-0115 et ADR-0117 (définition dans `Framework/Materials/Definitions/BuiltInMaterialDefinitions.cs`, `ShaderParameterNames`, `LitPbrMaterial`, `MaterialCompiler.CreateLitPbrMaterial`, tests `LitPbrMaterialDefinitionTests`, `PbrShaderCoverageTests`, `LitPbrMaterialTests`).
- `Framework/Rendering/Environment/GeneratedAssetHandleCache.cs:18-20` dit qu'une source corrigée est reprise « when the manager is recreated, for example when the project is reloaded » ; or l'éditeur crée son gestionnaire une fois et recharger un projet vide le catalogue, pas le gestionnaire (plan archivé `known-defects-gltf-alpha-probe-tasks.md`, O2).
- `ai-agent/README.md`, lignes des plans `texture-asset-mipmaps-tasks.md` et `static-model-tangents-tasks.md` : disent que la remontée dans `main` et le push restent à faire ; les deux sont dans `main` (`b9cc2ef23`, `76d87d455`) et poussés. Les ADR-0123 et 0124 restent `Proposed`.
- Dernier ADR : 0124. Références : 6422 tests, 0 échec ; `pbrdemo.sh` par défaut `54e406a1dde8c171`.

## Décisions verrouillées

| Réf | Décision |
|---|---|
| D1 | Ordre total : `RenderItem` reçoit un champ interne `SubmissionIndex` (rang d'ajout dans la liste de l'image) ; la comparaison compare `SortKey`, puis `SubmissionIndex`. La comparaison est un délégué statique mis en cache : aucune allocation par image. `SortKey` reste croissant. |
| D2 | La partie « maillage » de la clé ne vient plus de `GetHashCode()` : elle vient d'un identifiant attribué par ordre de première apparition du tampon de sommets dans l'image (table réutilisée et vidée à chaque image, sans allocation une fois chaude). Deux lancements qui soumettent les mêmes maillages dans le même ordre donnent les mêmes clés. La partie « matériau » suit la même règle : elle vient aujourd'hui de `mat.Id.GetHashCode()` (`:357`, `:395`), et `MaterialBase.Id` vaut par défaut `Guid.NewGuid()` (`MaterialBase.cs:29`), que les matériaux générés ne remplacent pas (`StaticModelMaterialResolver.cs:61`, `:76` ; seuls les matériaux compilés d'un asset copient l'identifiant de l'asset, `MaterialCompiler.cs:389`) : cette partie diffère donc d'un lancement à l'autre dès qu'une scène a un matériau généré. Elle est remplacée par un identifiant de première apparition du matériau dans l'image, par la même table réutilisée. `MaterialBase.Id` lui-même ne change pas. |
| D3 | `SceneDepthPrepassGpuTests` utilise la même comparaison si c'est faisable sans changer ce qu'il teste ; sinon il n'est pas touché et c'est noté. |
| D4 | Émissive ombrée : clé `emissive_shadow_factor` de `lit-pbr` (flottant, 0 à 1, défaut 0, groupe Lighting, pas de surcharge par instance, comme `normal_scale`), uniforme `EmissiveShadowFactor` sans emplacement réservé, pas de permutation. Dans `ShadePbr` : l'émissive ajoutée est `emissive × lerp(1, facteur d'ombre du pixel, EmissiveShadowFactor)`, où le facteur d'ombre du pixel est celui que le reste du pixel reçoit dans le mode d'ombre actif (visibilité en mode 0 ; teinte d'ombre appliquée comme au reste en mode `MultiplyLitColor`). À 0, le résultat est celui d'aujourd'hui, bit pour bit attendu. L'exécutant lit le shader et choisit l'écriture exacte ; il la décrit dans l'ADR. `lit-diffuse` et les autres matériaux ne changent pas. |
| D5 | Deux ADR `Proposed` : 0125 (ordre de dessin déterministe), 0126 (`emissive_shadow_factor`). Docs : `docs/engine/pbr-rendering.md`, `docs/engine/materials-workflow.md`, et la doc du pipeline de rendu si elle décrit le tri. |
| D6 | Branche `chantier/draw-order-emissive-shadow` depuis `main` (`8a73c5329`). `main` n'est pas avancé et rien n'est poussé par ce plan : décision de l'auteur. |

## Règles d'exécution pour l'agent

- Branche dédiée, jamais de commit sur `main`, jamais de push ; `git add` fichier par fichier ; aucune commande git dans `MGUI` ni `NvgSharp` ; jamais de `git submodule update`, `checkout`, `switch` ou `reset`.
- Builds `dotnet build CasaEngine.MonoGame.sln` et `dotnet build CasaEngine.Editor.MonoGame.sln` (0 erreur) ; tests `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj --blame-hang-timeout 5m`, toujours avec build, 0 échec.
- Un commit par tâche, ce plan mis à jour dans le même commit ; message en anglais `type(area): summary` ; code, commentaires, docs et ADR en anglais ; ce plan en français. Fins de ligne des fichiers existants conservées.
- Chaque changement de comportement commence par un test qui échoue sur le code d'avant, puis passe ; la sortie du test en échec est gardée dans la note de la tâche.
- Une démo ou un test GPU à la fois, jamais pendant un build. Le jeu n'est ni construit ni lancé par ce plan.
- Arrêt (tâche en ⚠️, question dans « Points ouverts ») : un test en échec qui n'est pas celui que la tâche vise ; l'empreinte par défaut de `pbrdemo.sh` change à T2 (défaut 0) ; une empreinte déterministe de `capture-v2.sh` ou `lin-v2.sh` diffère entre deux lancements du même code.

## Légende des statuts

⏳ Todo · 🚧 In progress · 🧪 Needs testing · ✅ Done · ⚠️ Blocked

## Tâches

### ✅ T0 — Branche, inscription du plan, corrections de documentation

- Créer la branche ; ajouter la ligne de ce plan en tête du tableau de `ai-agent/README.md` ; corriger dans ce tableau les deux cellules des plans des mipmaps et des tangentes (fusionnés dans `main` et poussés le 2026-11-17 ; reste la validation de l'ADR par l'auteur) ; corriger le commentaire XML de `GeneratedAssetHandleCache.cs:18-20` (une source corrigée sur disque n'est reprise qu'au redémarrage du jeu ou de l'éditeur : chacun crée son gestionnaire une fois, et charger un autre projet vide le catalogue, pas le gestionnaire).
- Validation : build à 0 erreur ; `rg "when the project is reloaded" CasaEngine` ne rend rien ; le diff de code ne touche que des lignes de commentaire.
- Commit : `docs(ai-agent): plan the deterministic draw order and the shadowed emissive`

> Validation (2026-11-17) : branche `chantier/draw-order-emissive-shadow` créée depuis `8a73c5329` ; `dotnet build CasaEngine.MonoGame.sln` et `CasaEngine.Editor.MonoGame.sln` : 0 erreur ; `rg "when the project is reloaded" CasaEngine` ne rend rien ; le diff de code est limité aux deux lignes de commentaire XML de `GeneratedAssetHandleCache.cs`. Le README corrige aussi les cellules des plans des mipmaps et des tangentes.

### ⏳ T1 — Ordre de dessin déterministe (D1 à D3, ADR-0125)

- Fichiers : `Framework/Rendering/Draw/RenderItem.cs`, `Framework/Application/Components/StaticMeshRendererComponent.cs`, nouveau `CasaEngine.Tests/Rendering/StaticMeshDrawOrderTests.cs`, `SceneDepthPrepassGpuTests.cs` (D3), docs, ADR-0125 et index.
- Validation :
  - tests : des éléments de clé égale soumis dans un ordre donné ressortent dans cet ordre, quel que soit le reste du tableau (ajout d'un élément d'une autre clé) ; des clés différentes trient par clé ; l'identifiant de maillage est le même pour deux listes qui soumettent les mêmes tampons dans le même ordre ; deux listes bâties avec deux jeux distincts de matériaux générés (identifiants `Guid.NewGuid()` neufs à chaque fois), soumis dans le même ordre, donnent la même suite de `SortKey` ; 0 octet alloué sur 1000 tris d'une liste chaude (`GC.GetAllocatedBytesForCurrentThread`, comme les tests à 0 octet existants) ;
  - builds à 0 erreur, tests à 0 échec ;
  - empreintes : `pbrdemo.sh` par défaut, `capture-v2.sh` et `lin-v2.sh`, chacun lancé deux fois dans deux processus : identiques entre les deux lancements. Les valeurs sont comparées à celles d'avant la tâche (relevées sur `8a73c5329` au début de T1) : tout changement est nommé, regardé (image avant et après) et attribué à l'ordre d'éléments de clé égale ; aucun changement n'est attendu sur `pbrdemo` par défaut, un changement n'y est pas un arrêt à cette tâche mais doit être expliqué.
- Commit : `feat(rendering): make the static mesh draw order fully deterministic`

### ⏳ T2 — `emissive_shadow_factor` (D4, ADR-0126)

- Fichiers : `Framework/Materials/Definitions/BuiltInMaterialDefinitions.cs`, `Framework/Materials/Runtime/LitPbrMaterial.cs`, `Framework/Materials/Compilation/MaterialCompiler.cs`, `Framework/Rendering/Shaders/ShaderParameterNames.cs`, `Content/Shaders/LitPbr.fx`, tests (`LitPbrMaterialDefinitionTests`, `PbrShaderCoverageTests`, `LitPbrMaterialTests`, un test GPU nouveau), docs, ADR-0126 et index.
- Validation :
  - tests de définition : la clé existe, défaut 0, un fichier sans la clé charge 0, un fichier avec la clé fait l'aller-retour ; `Bind` écrit l'uniforme ;
  - test GPU (banc 1×1 des tests GPU `lit-pbr` existants) : montage précis, parce que sans lumière directionnelle à l'indice ombré le facteur d'ombre vaut toujours 1 (`Pbr.fxh:319`, `:336`) : une lumière directionnelle active à `ShadowedDirectionalLightIndex`, de couleurs diffuse et spéculaire nulles ; une carte d'ombre qui occulte tout le quad (visibilité 0, `ShadowFadeWidth` 0) ; ambiant nul, pas d'IBL ; `ReceiveShadows` actif ; émissive 0,5. En mode `MultiplyLitColor` avec teinte (0,25 ; 0,26 ; 0,27) : à 0 la sortie est l'émissive ; à 1 elle est l'émissive × la teinte, et diffère de l'émissive ; à 0,5 elle est entre les deux ; un quad hors de l'ombre rend l'émissive entière pour toute valeur. Dans le mode d'ombre par défaut : à 1 et visibilité 0, la sortie est 0. La note de la tâche montre le montage (`ActiveDirectionalLightCount` ≥ 1, indice ombré dedans) et les valeurs mesurées ;
  - builds à 0 erreur, tests à 0 échec ; `pbrdemo.sh` par défaut inchangé par rapport à la valeur relevée après T1 (arrêt sinon) ; `capture-v2.sh` et `lin-v2.sh` inchangés par rapport à T1.
- Commit : `feat(materials): let a lit-pbr material shadow its emissive`

### ⏳ T3 — Vérification indépendante

- Un `verifier` frais confirme ou réfute T0 à T2 par ses propres builds, tests et empreintes, et relit les deux ADR contre le code.
- Commit : `docs(ai-agent): record the verification of the draw order and shadowed emissive`

## Points ouverts

- O1 : avance de `main` sur cette branche et push : à l'auteur.
- O2 : validation des ADR-0125 et 0126 par l'auteur.
