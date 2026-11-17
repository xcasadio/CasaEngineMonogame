# Plan agent IA — Deux défauts connus : alpha d'un matériau glTF opaque, sonde de réflexion qui échoue à chaque image

Plan de deux corrections du moteur listées comme « défauts connus, non corrigés » dans la note de passation du chantier RacingGame (`ai-agent/HANDOVER.md` du dépôt du jeu, §4), tranche S5 de `ai-agent/tasks/rgce-pbr-handover-tasks.md` (Phase 17). Mode autonome accordé par l'auteur le 2026-11-17 (« enchaîne les autres tâches »).

Ce fichier doit être mis à jour pendant le travail : l'icône au début de chaque tâche indique son statut courant.

## Objectif

1. Un matériau glTF de mode alpha `OPAQUE` est importé opaque, quel que soit l'alpha de son facteur de couleur de base.
2. Une sonde de réflexion référencée par identifiant d'asset n'alloue rien à chaque image une fois résolue, et un asset qui ne se charge pas (cube, environnement ou panorama) n'est tenté et journalisé qu'une fois.

## État vérifié du dépôt (2026-11-17, `8a3933b36`)

- Défaut 1, tel que la note le décrit : « un matériau `OPAQUE` dont le facteur de couleur de base a un alpha inférieur à 1 est compilé en transparent (`EditorAssetImportService.cs`) ».
  - `CasaEngine.EditorServices/EditorAssetImportService.cs`, `CreateLitPbrMaterial` : la file vient du mode alpha (`Mask` → `AlphaTest`, `Blend` → `Transparent`, sinon `Opaque`), mais `base_color` est écrit avec l'alpha du facteur (`linearBaseColor.A / 255.0f`).
  - Au rendu, `CasaEngine/Framework/Materials/Runtime/LitPbrMaterial.cs:181` passe `isTransparent: BaseColor.A < byte.MaxValue`, et `MaterialRenderStateResolver.cs:104` rend vrai pour une couleur dont l'alpha est sous 255.
  - glTF 2.0, mode `OPAQUE` : la valeur d'alpha est ignorée et le rendu est entièrement opaque (spécification Khronos, « Alpha Coverage »). À citer dans le commentaire avec le nom de la section ; l'exécutant ne cite pas d'URL qu'il n'a pas lue.
- Défaut 2, tel que la note le décrit : « une sonde de réflexion référencée par identifiant d'asset alloue et journalise à chaque image (`ReflectionProbeResolver`) ».
  - `CasaEngine/Framework/Rendering/Environment/ReflectionProbeResolver.cs`, `ResolveCubemap` : appelle `EnvironmentAssetLookup.TryLoadTextureCube` ou `TryLoadEnvironmentAsset` à chaque résolution.
  - `EnvironmentAssetLookup.TryLoadAsset` : rend l'asset du cache `AcquiredAssetHandleCache` s'il y est ; sinon, si le catalogue connaît l'identifiant, appelle `Acquire` dans un `try` et, en cas d'exception, `Logs.WriteException` puis rend `null`. Un échec n'est pas retenu : il se reproduit, avec son exception et sa ligne de journal, à chaque résolution.
  - Second chemin, par le panorama (`ReflectionProbeResolver.cs:110-112`) : `PanoramaEnvironmentGenerator.GetOrCreateCubemap` est appelé à chaque résolution quand l'environnement de la sonde a un panorama. `CreateGeneratedCubemapAssetId` y est appelé avant la recherche dans le cache (`PanoramaEnvironmentGenerator.cs:38`) et alloue deux tableaux à chaque appel (`ToByteArray`, `BitConverter.GetBytes`, `:104-106`), même quand le cube est déjà en cache. En cas d'échec, `GeneratedAssetHandleCache` ne retient rien : chaque image refait `ResolvePanoramaPath` et `File.Exists` (fichier absent, sans journal), ou construit la lambda, lève, et journalise par `Logs.WriteException` (`:53-67`).
  - `EnvironmentResolver.Resolve` atteint `ReflectionProbeResolver.Resolve` à chaque appel dès que le monde a une sonde (`EnvironmentResolver.cs:23-25`).
  - Test existant : `CasaEngine.Tests/Rendering/EnvironmentAssetLookupHandleTests.cs`.
- Branche : `chantier/known-defects-gltf-alpha-probe`, créée depuis `chantier/pbr-ggx-back-facing` (`8a3933b36`), sur laquelle le dépôt du jeu pointe. Raison de ne pas partir de `main` : le sous-module du jeu est extrait sur cette branche, et changer de base ferait bouger tout l'arbre de travail du jeu ; les trois branches du chantier RacingGame sont déjà empilées de cette façon. Le pointeur du jeu n'est pas avancé par ce plan : le jeu n'importe pas de glTF au lancement et n'a pas de sonde de réflexion référencée par identifiant.

## Décisions verrouillées

| Réf | Décision |
|---|---|
| D1 | Défaut 1 corrigé à l'import, pas au rendu : pour un matériau de mode `Opaque`, l'importeur écrit `base_color` avec un alpha de 1. La règle de rendu « alpha de couleur sous 255 = transparent » ne change pas : des matériaux écrits à la main peuvent en dépendre. Les modes `Mask` et `Blend` gardent l'alpha du facteur. |
| D2 | D1 s'applique à `CreateLitPbrMaterial` et à `CreateLitDiffuseMaterial`, qui écrit lui aussi `DiffuseColor` avec son alpha (`EditorAssetImportService.cs:769`). La condition est `importedMaterial.AlphaMode == StaticModelAlphaMode.Opaque`, jamais la file de présentation : la file du chemin `lit-diffuse` vient d'indices hérités (`LegacyImportedMaterialPresentation.cs:31-33`), et s'y fier rendrait opaques des imports `BLEND`. |
| D3 | Défaut 2 : un identifiant dont l'acquisition a levé une exception est retenu, par gestionnaire d'assets, à côté du cache des poignées ; les résolutions suivantes rendent `null` sans réessayer ni journaliser. L'échec est journalisé une fois. Un identifiant absent du catalogue n'est pas retenu (il peut être catalogué plus tard), et ce chemin ne journalise déjà rien. |
| D3b | Chemin du panorama : l'identifiant du cube généré est calculé sans allocation (tampon sur la pile), et reste le même identifiant qu'aujourd'hui, octet pour octet. Un échec (fichier absent, ou exception à la création) est retenu par gestionnaire d'assets pour cet identifiant généré : les résolutions suivantes rendent `null` sans refaire le chemin ni journaliser ; l'exception est journalisée une fois, le fichier absent n'est pas journalisé (comme aujourd'hui). Limite écrite dans la doc : un panorama corrigé sur disque n'est repris qu'à la recréation du gestionnaire d'assets (rechargement du projet). |
| D4 | Pas de nouvel ADR : deux corrections de défauts, sans changement de format ni d'API publique. La doc est mise à jour là où le comportement est décrit. |

## Règles d'exécution pour l'agent

- Branche dédiée, jamais de commit sur `main`, jamais de push ; `git add` fichier par fichier ; aucune commande git dans `MGUI` ni `NvgSharp` ; jamais de `git submodule update`.
- Builds `dotnet build CasaEngine.MonoGame.sln` et `dotnet build CasaEngine.Editor.MonoGame.sln` ; tests `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj --blame-hang-timeout 5m`. Échecs connus sur ce PC : 8 tests GPU (`IblCubeGeneratorGpuTests` × 1, `DrawTransactionRearmGpuTests` × 1, `DesktopFrameGpuAllocationTests` × 6) ; tout autre échec est un arrêt.
- Un commit par tâche, le plan mis à jour dans le même commit ; message en anglais ; code et commentaires en anglais ; ce plan en français.
- Chaque correction commence par un test qui échoue sur le code d'avant (preuve du défaut), puis passe ; la sortie du test en échec est gardée dans la note de la tâche. Aucun des tests n'a besoin d'un GPU. Le journal s'observe par `Logs.AddLogger` et `Logs.Close`, comme dans les tests existants de `ProjectEnvironmentCollection`.
- L'exécutant laisse le dépôt du moteur sur la branche de ce plan : la vérification indépendante s'y fait. Le retour sur `chantier/pbr-ggx-back-facing` (T3) est fait ensuite par la session principale. Le jeu n'est ni construit ni lancé tant que le sous-module est sur cette branche.

## Légende des statuts

⏳ Todo · 🚧 In progress · 🧪 Needs testing · ✅ Done · ⚠️ Blocked

## Tâches

### ✅ T0 — Branche et inscription du plan

- Créer la branche ; ajouter la ligne de ce plan au tableau de `ai-agent/README.md`.
- Commit : `docs(ai-agent): plan two known defect fixes (glTF opaque alpha, failing reflection probe)`

> Validation (2026-11-17) : branche `chantier/known-defects-gltf-alpha-probe` créée depuis `8a3933b36` ; ligne ajoutée en tête du tableau de `ai-agent/README.md`. Documentation seule, pas de build.

### ✅ T1 — Matériau glTF opaque importé opaque

- Fichiers : `CasaEngine.EditorServices/EditorAssetImportService.cs` ; un test dans `CasaEngine.Tests` à côté des tests existants de l'import de matériaux (l'exécutant les trouve par `rg`) ; la doc de l'import glTF ou du PBR qui décrit l'alpha, si elle existe (`rg -n "alpha" docs/engine docs/editor`).
- Validation :
  - test nouveau, qui modifie le glTF à l'intérieur du test comme le fait `AddClearCoatToTheOpaqueMaterial` (le fichier partagé `GltfImportProbe` et sa référence dorée ne changent pas) : dans les deux chemins (`lit-pbr` et `lit-diffuse`), un matériau importé de mode `Opaque` avec un facteur de couleur de base d'alpha 0,5 donne un `MaterialAsset` de file `Opaque` dont `base_color` a un alpha de 255, et le matériau de rendu construit à partir de lui n'est pas transparent ; le test échoue sur le code d'avant (sortie notée) et passe après ;
  - tests nouveaux ou existants : les modes `Mask` et `Blend` gardent l'alpha du facteur ;
  - les deux solutions à 0 erreur ; tests : les 8 échecs connus et eux seuls.
- Arrêts : le défaut ne se reproduit pas par le test ; le test doré `ImportFile_DefaultOptionsMatchTheReferenceCapturedBeforeTheMaterialTemplateExisted` change ; la correction change un fichier d'asset déjà importé dans le dépôt (elle ne doit toucher que les imports futurs).
- Commit : `fix(import): import an opaque glTF material as opaque whatever its base colour alpha`

> Validation (2026-11-17) : tests écrits d'abord, dans `CasaEngine.Tests/EditorServices/EditorAssetImportMaterialTemplateTests.cs`. Le glTF est modifié dans le test (`SetBaseColorFactorAlpha`, sur le matériau `OpaqueDoubleSided` ou `Masked` de la sonde, le fichier partagé et la référence dorée ne changent pas).
> - Sur le code d'avant, `ImportFile_AnOpaqueMaterialIsImportedOpaqueWhateverTheAlphaOfItsBaseColorFactor` échoue pour les deux gabarits : `(template: LitPbr, colorKey: "base_color")` et `(template: LitDiffuse, colorKey: "diffuse_color")` donnent `Assert.Equal() Failure: Values differ / Expected: 255 / Actual: 127` (alpha du facteur 0,5 écrit tel quel ; la file, elle, est bien `Opaque`). Échoué : 2, réussite : 10, total : 12 (classe `EditorAssetImportMaterialTemplateTests`).
> - Après la correction (`ResolveImportedBaseColor`, condition `importedMaterial.AlphaMode == StaticModelAlphaMode.Opaque`, utilisée par `CreateLitPbrMaterial` et `CreateLitDiffuseMaterial`) : la classe passe, 12/12, golden `ImportFile_DefaultOptionsMatchTheReferenceCapturedBeforeTheMaterialTemplateExisted` inchangé. Le même test compile le `MaterialAsset` relu par `MaterialCompiler` et vérifie `IsTransparent == false` et la file `Opaque`.
> - `ImportFile_AMaskedAndABlendedMaterialKeepTheAlphaOfTheirBaseColorFactor` (deux gabarits) : `Masked` et `Blended` gardent un alpha de 127 ou 128 ; il passe déjà sur le code d'avant, comme attendu (non-régression, pas preuve du défaut).
> - `dotnet build CasaEngine.MonoGame.sln` : 0 erreur ; `dotnet build CasaEngine.Editor.MonoGame.sln` : 0 erreur. `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj --blame-hang-timeout 5m` : échec 8, réussite 6387, total 6395 ; les 8 échecs sont les 8 connus (`IblCubeGeneratorGpuTests` × 1, `DrawTransactionRearmGpuTests` × 1, `DesktopFrameGpuAllocationTests` × 6).
> - Doc : `docs/engine/pbr-rendering.md`, section « glTF import ». Aucun fichier d'asset du dépôt n'est touché : la correction ne s'exécute qu'à l'import.

### ⏳ T2 — Sonde de réflexion : pas d'allocation par image, échec tenté une seule fois

- Fichiers : `CasaEngine/Framework/Rendering/Environment/EnvironmentAssetLookup.cs`, `AcquiredAssetHandleCache.cs`, `PanoramaEnvironmentGenerator.cs`, `GeneratedAssetHandleCache.cs` ; `CasaEngine.Tests/Rendering/EnvironmentAssetLookupHandleTests.cs` ou un fichier voisin ; `docs/engine/` là où les sondes de réflexion par identifiant sont décrites, si un tel passage existe.
- Validation :
  - test nouveau : un identifiant catalogué dont l'acquisition lève une exception est résolu deux fois ; l'acquisition n'est tentée qu'une fois et une seule entrée est journalisée ; le test échoue sur le code d'avant (deux tentatives) et passe après ;
  - test nouveau (panorama) : `CreateGeneratedCubemapAssetId` rend, pour plusieurs couples (identifiant, taille) écrits dans le test, les mêmes identifiants que l'implémentation d'avant (valeurs attendues calculées avec le code d'avant et écrites en dur) ; mesurés par `GC.GetAllocatedBytesForCurrentThread`, mille appels n'allouent rien ; le test d'allocation échoue sur le code d'avant ;
  - test nouveau (panorama, création qui lève) : un panorama catalogué dont le fichier existe mais ne se décode pas (`PanoramaImageData.Load` lève avant tout usage du `GraphicsDevice`) est résolu deux fois ; une seule entrée de journal au total ; sur le code d'avant il y en a deux ;
  - test nouveau (panorama, fichier absent) : un panorama catalogué dont le fichier est absent est résolu une première fois (résultat `null`, aucune entrée de journal) ; un fichier indécodable est alors écrit au chemin résolu ; la seconde résolution rend `null` sans aucune entrée de journal, parce que l'échec est retenu ; sur le code d'avant, la seconde résolution charge le fichier, lève et journalise une entrée ;
  - les tests nouveaux n'utilisent que des membres qui existent déjà au commit parent, pour que la copie aux sources d'avant (vérification indépendante) compile et échoue à l'exécution, pas à la compilation ; si la correction demande un membre de test nouveau, il est ajouté de façon que les tests d'échec n'en dépendent pas ;
  - test : un identifiant absent du catalogue n'est pas retenu ; un identifiant qui se charge garde le comportement d'avant (tests existants verts) ;
  - pas d'allocation ajoutée sur le chemin d'une résolution réussie (lecture du code : une recherche de plus dans une table existante au plus, sur le seul chemin d'échec ou de premier chargement) ;
  - les deux solutions à 0 erreur ; tests : les 8 échecs connus et eux seuls ;
  - `pbrdemo.sh` par défaut : empreinte `54e406a1dde8c171`, inchangée.
- Arrêts : un des tests nouveaux passe déjà sur le code d'avant (le défaut n'est pas où le plan le dit : noter ce qui a été observé, ne rien corriger sur ce chemin) ; un identifiant généré change de valeur ; un troisième chemin par image est trouvé à la lecture de `ReflectionProbeResolver.Resolve` (le noter en point ouvert, ne pas l'ajouter à la tâche).
- Commit : `fix(rendering): try a failing reflection probe asset once instead of every frame`

### ⏳ T3 — Clôture

- Par la session principale, après la vérification indépendante faite sur la branche de ce plan (T17.3 du plan du jeu) : ce plan passe en ✅ et est archivé selon la règle du dépôt, dans un dernier commit de la branche (`docs(ai-agent): close the known defects plan`) ; puis `git checkout chantier/pbr-ggx-back-facing` dans `CasaEngine` (pas dans ses sous-modules) ; `git -C CasaEngine rev-parse HEAD` commence par `8a3933b36` ; `git status` du dépôt du jeu ne montre pas le sous-module modifié ; build du jeu à 0 erreur.

## Points ouverts

- O1 : remontée de cette branche dans `main` et push : décision de l'auteur, avec les trois autres branches empilées.
