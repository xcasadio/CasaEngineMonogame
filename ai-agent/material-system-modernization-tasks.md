# Modernisation Materials + Rendu Mesh — Plan d'action IA

## Objectif

Faire evoluer CasaEngine vers une architecture de materials plus moderne, sans casser le pipeline actuel pendant la migration.

Le resultat cible doit couvrir ces points :

- `ShaderVariantLibrary` branchee dans le chemin normal de rendu
- calcul complet des features runtime (`NormalMap`, `AlphaTest`, `Transparent`, `Skinned`, `Instanced`, `VertexColor` si present)
- vraies `RenderStats` visibles dans l'overlay debug par vue
- separation nette entre asset editable, representation runtime compilee et overrides par objet
- base saine pour l'editeur de materials et le hot reload

## Regles obligatoires pour l'agent IA

1. Une seule tache a la fois.
2. Avant de commencer une tache, remplacer son icone `⏳` par `🚧`.
3. Ne jamais commencer la tache suivante avant d'avoir termine la tache courante.
4. Une tache terminee doit laisser le repo dans un etat compilable.
5. A la fin de chaque tache :
   - lancer un build borne de la solution
   - lancer des tests filtres si la tache touche de la logique pure
   - mettre a jour le statut de la tache dans ce fichier
   - committer le code + la mise a jour du plan dans le meme commit
6. Statuts autorises :
   - `⏳ Todo`
   - `🚧 In progress`
   - `✅ Done`
   - `🧪 Needs testing`
   - `⚠️ Blocked`
7. Si une tache est bloquee, ajouter une note courte juste sous la tache avant de committer ou d'arreter.

## Validation minimale par tache

- Build principal : `dotnet build CasaEngine.Editor.MonoGame.sln -c Debug --no-restore`
- Si la tache ajoute ou modifie des calculs purs : ajouter un test cible dans `CasaEngine.Tests` puis lancer un filtre cible.
- Si la tache change un workflow visible : lancer le sample ou la demo la plus proche de la zone touchee.

## Risques principaux

- Casser le rendu existant en branchant trop tot les shader variants
- Melanger feature flags, valeurs artistiques et render states
- Faire entrer l'editeur trop tot dans la migration, avant d'avoir stabilise les types authoring/runtime
- Perdre la compatibilite avec les `.material` existants
- Faire fuiter des objets runtime/GPU dans les assets editoriaux

## Criteres d'acceptation finaux

- Le draw path normal n'utilise plus systematiquement le shader legacy unique
- Les features de rendu proviennent d'un resolver unique et coherent
- Les `RenderStats` affichees sont reelles et exploitables dans l'overlay debug
- Le moteur dispose d'une vraie separation `MaterialAsset` / `CompiledMaterial` / overrides runtime
- Les assets legacy restent lisibles pendant la migration
- Une demo permet de verifier visuellement variants, transparence, stats et overrides

---

## Phase 1 — Brancher les shader variants dans le chemin normal

- ✅ **T01.01 — Introduire un `EffectiveShaderResolver`**
  Objectif:
  - Determiner le shader effectif d'un material meme si `ShaderAssetId` est vide.
  - Fournir des fallbacks stables pour `LitDiffuseMaterial` et `UnlitTextureMaterial`.
  Validation:
  - Build solution.
  Commit conseille:
  - `feat(rendering): resolve effective shader for runtime materials`

- ✅ **T01.02 — Brancher la selection de shader dans le draw path normal**
  Objectif:
  - Faire choisir le shader/variant dans le chemin opaque/transparent normal.
  - Arreter d'utiliser `legacyShader` comme chemin principal pour tous les draws.
  Validation:
  - Build solution.
  - Smoke test sur `MaterialDemo` et `StaticModelDemo`.
  Commit conseille:
  - `feat(rendering): use shader selection in regular draw path`

- ✅ **T01.03 — Declarer les alias de techniques pour les shaders existants**
  Objectif:
  - Enregistrer les alias necessaires pour `basicEffect` et `UnlitTexture`.
  - Verifier les fallbacks de technique si une permutation n'existe pas.
  Validation:
  - Build solution.
  - Smoke test sur `MaterialDemo`.
  Commit conseille:
  - `feat(rendering): register shader technique aliases for built-in materials`

---

## Phase 2 — Completer le calcul des features runtime

- ✅ **T02.01 — Etendre `ShaderFeature` avec les flags manquants**
  Objectif:
  - Ajouter explicitement `Transparent` si necessaire.
  - Verifier que les flags couvrent bien `NormalMap`, `AlphaTest`, `Skinned`, `Instanced` et `VertexColor`.
  Validation:
  - Build solution.
  Commit conseille:
  - `refactor(rendering): complete shader feature flags`

- ✅ **T02.02 — Creer un `RenderFeatureResolver` unique**
  Objectif:
  - Centraliser le calcul des features depuis material + mesh + chemin de draw.
  - Eviter la logique eparpillee dans plusieurs renderers.
  Validation:
  - Build solution.
  Commit conseille:
  - `feat(rendering): add unified render feature resolver`

- ✅ **T02.03 — Completer les features derivees du material**
  Objectif:
  - Deriver correctement `BasColorTexture`, `NormalMap`, `Emissive`, `AlphaTest`, `Transparent`.
  - Faire remonter les informations structurelles sans y melanger les valeurs artistiques.
  Validation:
  - Build solution.
  - Tests filtres sur le resolver si introduit.
  Commit conseille:
  - `feat(materials): expose material-driven shader features`

- ✅ **T02.04 — Completer les features derivees du mesh et du renderer**
  Objectif:
  - Remonter `Skinned`, `Instanced` et `VertexColor` depuis les donnees de draw reelles.
  - Eviter qu'un material declare seul des features qui appartiennent au mesh ou au pass.
  Validation:
  - Build solution.
  - Smoke test `SkinnedMeshDemo` si touche.
  Commit conseille:
  - `feat(rendering): derive mesh and draw-path shader features`

- ✅ **T02.05 — Brancher `RenderFeatureResolver` dans les renderers**
  Objectif:
  - Utiliser le resolver dans `StaticMeshRendererComponent` et la voie skinned si applicable.
  - Supprimer les calculs ad hoc restants.
  Validation:
  - Build solution.
  - Smoke test `MaterialDemo` + `SkinnedMeshDemo`.
  Commit conseille:
  - `refactor(rendering): route render item features through resolver`

- ✅ **T02.06 — Ajouter des tests cibles pour les features et la selection de variant**
  Objectif:
  - Cadrer le comportement attendu de `RenderFeatureResolver`.
  - Cadrer le mapping variant -> technique.
  Validation:
  - `dotnet test CasaEngine.Tests\\CasaEngine.Tests.csproj -c Debug --filter FullyQualifiedName~RenderFeature --no-restore`
  Commit conseille:
  - `test(rendering): cover feature resolution and shader variant selection`

---

## Phase 3 — Rendre `RenderStats` reelles et visibles

- ✅ **T03.01 — Introduire une collecte de stats par vue**
  Objectif:
  - Faire survivre les stats d'un renderer au-dela du `Flush()` local.
  - Rendre ces stats accessibles au pipeline de vue.
  Validation:
  - Build solution.
  Commit conseille:
  - `feat(rendering): expose per-view render stats`

- ✅ **T03.02 — Compter les items opaques/transparents et les binds texture**
  Objectif:
  - Alimenter `OpaqueItems`, `TransparentItems` et `TextureBinds` avec de vraies donnees.
  - Ne pas se limiter aux `EffectBinds` et `StateChanges` deja presents.
  Validation:
  - Build solution.
  - Tests filtres si un cache de binding est ajoute.
  Commit conseille:
  - `feat(rendering): count texture binds and item buckets`

- ✅ **T03.03 — Afficher les `RenderStats` dans `DebugOverlay`**
  Objectif:
  - Ajouter draw calls, effect binds, texture binds, state changes et compteurs opaques/transparents dans l'overlay.
  - Garder un rendu lisible en split-screen.
  Validation:
  - Build solution.
  - Smoke test `SplitScreenDemo`.
  Commit conseille:
  - `feat(rendering): show render stats in debug overlay`

- ✅ **T03.04 — Ajouter une validation demo pour les stats**
  Objectif:
  - Verifier visuellement que les stats changent quand on change vue, materiaux ou transparence.
  - Documenter la demo de reference pour les futures regressions.
  Validation:
  - Build solution.
  - Run `SplitScreenDemo` ou `ViewManagerSandbox`.
  Commit conseille:
  - `docs(rendering): document render stats demo workflow`

---

## Phase 4 — Poser la separation authoring / runtime

- ✅ **T04.01 — Introduire `MaterialDefinition` et `MaterialPropertyDefinition`**
  Objectif:
  - Declarer les types de materials et leurs proprietes exposees.
  - Definir groupes, types, flags, valeurs par defaut et metadata editoriales.
  Validation:
  - Build solution.
  Commit conseille:
  - `feat(materials): add authoring material definitions`

- ✅ **T04.02 — Introduire `MaterialValue` typee**
  Objectif:
  - Representer proprement les valeurs `Float`, `Int`, `Bool`, `Color`, `Vector`, `Texture`, `Enum`.
  - Eviter les dictionnaires runtime de type `string -> object` pour les assets authoring.
  Validation:
  - Build solution.
  - Ajouter tests purs si la logique de conversion est non triviale.
  Commit conseille:
  - `feat(materials): add typed material property values`

- ✅ **T04.03 — Introduire `MaterialAsset` authoring**
  Objectif:
  - Stocker `DefinitionId`, valeurs persistantes, parent eventuel et options structurelles.
  - Garder les render states encadres, pas libres partout.
  Validation:
  - Build solution.
  Commit conseille:
  - `feat(materials): add authoring material asset model`

- ✅ **T04.04 — Introduire `CompiledMaterial` runtime**
  Objectif:
  - Isoler la representation runtime compilee des assets editoriaux.
  - Y stocker shader effectif, permutation, textures resolues et etats prepares.
  Validation:
  - Build solution.
  Commit conseille:
  - `feat(materials): add compiled material runtime representation`

- ✅ **T04.05 — Introduire `MaterialCompiler` minimal**
  Objectif:
  - Compiler au minimum `LitDiffuse` et `UnlitTexture` vers `CompiledMaterial`.
  - Garder la compatibilite avec le pipeline existant pendant la transition.
  Validation:
  - Build solution.
  - Tests filtres sur compilation de material si possible.
  Commit conseille:
  - `feat(materials): compile authoring materials to runtime representation`

- ✅ **T04.06 — Introduire `MaterialCache` et invalidation**
  Objectif:
  - Cacher les `CompiledMaterial` et pouvoir les invalider.
  - Preparer la base du hot reload.
  Validation:
  - Build solution.
  Commit conseille:
  - `feat(materials): add compiled material cache and invalidation`

---

## Phase 5 — Migrer le chargement sans casser la compatibilite

- ⏳ **T05.01 — Ajouter le format de serialisation de `MaterialAsset`**
  Objectif:
  - Pouvoir sauver/charger le nouvel asset authoring dans `.material`.
  - Garder le format lisible et stable pour l'editeur.
  Validation:
  - Build solution.
  Commit conseille:
  - `feat(materials): add material asset serialization`

- ⏳ **T05.02 — Ajouter un adaptateur legacy `.material` -> `MaterialAsset`**
  Objectif:
  - Continuer a charger les anciens JSON `MaterialBase` existants.
  - Les convertir vers le nouveau modele authoring pendant la lecture.
  Validation:
  - Build solution.
  - Tests filtres de compatibilite.
  Commit conseille:
  - `feat(materials): bridge legacy material files to authoring assets`

- ⏳ **T05.03 — Faire compiler `MaterialAsset` vers les materials runtime actuels**
  Objectif:
  - Garder le pipeline fonctionnel pendant la transition.
  - Permettre au renderer actuel de consommer les materials compiles sans tout reecrire d'un coup.
  Validation:
  - Build solution.
  - Smoke test `MaterialDemo`.
  Commit conseille:
  - `feat(materials): bridge compiled materials to current renderer runtime`

- ⏳ **T05.04 — Faire produire des `MaterialAsset` a l'import static model**
  Objectif:
  - Remplacer la creation directe de `LitDiffuseMaterial` dans l'importeur par la creation d'assets authoring.
  - Conserver les GUIDs et le workflow du content browser.
  Validation:
  - Build solution.
  - Smoke test d'import d'un static model.
  Commit conseille:
  - `refactor(import): author materials as material assets`

- ⏳ **T05.05 — Ajouter des tests legacy + nouveau format**
  Objectif:
  - Garantir que le nouveau loader ne casse pas les `.material` existants.
  - Cadrer la compilation depuis le format authoring.
  Validation:
  - `dotnet test CasaEngine.Tests\\CasaEngine.Tests.csproj -c Debug --filter FullyQualifiedName~Material --no-restore`
  Commit conseille:
  - `test(materials): cover legacy and authoring material loading`

---

## Phase 6 — Brancher l'editeur et le hot reload

- ⏳ **T06.01 — Ajouter un registre editor de definitions de properties**
  Objectif:
  - Permettre a l'editeur de generer une UI depuis `MaterialDefinition`.
  - Organiser les properties par groupes semantiques (`Surface`, `Normals`, `PBR`, `Emission`, `UV`, `Advanced`).
  Validation:
  - Build solution.
  Commit conseille:
  - `feat(editor): add material definition registry for inspector generation`

- ⏳ **T06.02 — Creer un panneau/inspecteur MGUI de material genere automatiquement**
  Objectif:
  - Editer les `MaterialAsset` sans coder un inspecteur specifique par classe runtime.
  - Afficher les bons controles selon le type de property.
  Validation:
  - Build solution.
  - Smoke test sur un `.material` dans l'editeur.
  Commit conseille:
  - `feat(editor): generate material inspector from definitions`

- ⏳ **T06.03 — Ajouter les marqueurs d'override et le reset par property**
  Objectif:
  - Preparer l'heritage parent/enfant de materials.
  - Rendre visibles les valeurs locales vs heritees.
  Validation:
  - Build solution.
  - Smoke test editeur sur un material parent/enfant minimal.
  Commit conseille:
  - `feat(editor): show material property overrides and reset actions`

- ⏳ **T06.04 — Brancher le hot reload des materials**
  Objectif:
  - Invalider le `MaterialCache` a la sauvegarde ou au rechargement d'un asset material.
  - Faire refleter la modification dans les vues sans redemarrage du moteur.
  Validation:
  - Build solution.
  - Smoke test dans l'editeur sur un material applique a une scene.
  Commit conseille:
  - `feat(editor): hot reload compiled materials after asset save`

- ⏳ **T06.05 — Ajouter un preview material minimal**
  Objectif:
  - Offrir un preview sphere/cube/plane dans l'editeur material.
  - Donner un preset de lumiere simple pour valider les changements rapidement.
  Validation:
  - Build solution.
  - Smoke test editeur.
  Commit conseille:
  - `feat(editor): add material preview viewport`

---

## Phase 7 — Aligner les overrides par objet avec la nouvelle architecture

- ⏳ **T07.01 — Introduire `MaterialInstanceData`**
  Objectif:
  - Representer les overrides par objet de facon distincte de l'asset material.
  - Garder le systeme leger et compatible avec le rendu par instance.
  Validation:
  - Build solution.
  Commit conseille:
  - `feat(materials): add per-object material instance data`

- ⏳ **T07.02 — Mapper `MaterialInstanceData` vers `MaterialPropertyBlock` au runtime**
  Objectif:
  - Conserver `MaterialPropertyBlock` comme outil runtime transitoire et performant.
  - Eviter de dupliquer des assets material pour des changements par entite.
  Validation:
  - Build solution.
  - Smoke test `MaterialDemo`.
  Commit conseille:
  - `feat(rendering): map material instance data to runtime property blocks`

- ⏳ **T07.03 — Adapter `StaticModelComponent` au nouveau modele de materials**
  Objectif:
  - Faire porter les overrides par slot sur l'asset/instance authoring plutot que sur des `MaterialBase` charges directement.
  - Garder la compatibilite reimport et les orphelins deja gerees.
  Validation:
  - Build solution.
  - Tests filtres `StaticModelMaterial`.
  Commit conseille:
  - `refactor(staticmodel): align slot overrides with material instance data`

- ⏳ **T07.04 — Verifier les overrides par slot apres reimport**
  Objectif:
  - Revalider le comportement sur les slots stables (`SlotName` / `SlotIndex`).
  - Garder les warnings utiles pour les overrides orphelins.
  Validation:
  - Build solution.
  - Tests filtres `StaticModelMaterial`.
  Commit conseille:
  - `test(staticmodel): validate slot overrides after material migration`

---

## Phase 8 — Demos, documentation et cloture

- ⏳ **T08.01 — Etendre `MaterialDemo` pour couvrir variants et transparence**
  Objectif:
  - Ajouter un cas visible `AlphaTest` / `Transparent` / `NormalMap`.
  - Faciliter la verification manuelle du pipeline modernise.
  Validation:
  - Build solution.
  - Run `MaterialDemo`.
  Commit conseille:
  - `feat(demos): extend material demo for variants and transparency`

- ⏳ **T08.02 — Ajouter un cas de validation overlay stats**
  Objectif:
  - Rendre evidente l'utilite des `RenderStats` en split-screen ou multi-view.
  - Donner un scenario de regression simple.
  Validation:
  - Build solution.
  - Run `SplitScreenDemo`.
  Commit conseille:
  - `feat(demos): add debug overlay stats validation scene`

- ⏳ **T08.03 — Documenter le workflow materials**
  Objectif:
  - Documenter la separation `MaterialAsset` / `CompiledMaterial` / overrides.
  - Documenter le workflow editeur + hot reload + preview.
  Validation:
  - Verification documentaire + build solution.
  Commit conseille:
  - `docs(materials): document authoring runtime and override workflow`

- ⏳ **T08.04 — Validation finale de la migration**
  Objectif:
  - Lancer un build solution final.
  - Rejouer les demos de reference et les tests filtres principaux.
  - Laisser le plan a jour a 100% ou bloquer proprement ce qui reste.
  Validation:
  - `dotnet build CasaEngine.Editor.MonoGame.sln -c Debug --no-restore`
  - tests filtres `Material`, `RenderFeature`, `StaticModelMaterial`.
  Commit conseille:
  - `chore(materials): final validation for material system modernization`

---

## Ordre recommande d'execution

1. Phase 1
2. Phase 2
3. Phase 3
4. Phase 4
5. Phase 5
6. Phase 6
7. Phase 7
8. Phase 8

## Notes importantes pour l'agent

- Ne pas reecrire tout le renderer d'un coup. La migration doit rester compatible a chaque tache.
- Ne pas brancher l'editeur sur des types authoring instables. Stabiliser d'abord `MaterialAsset` et `CompiledMaterial`.
- Ne pas confondre :
  - variant de material (asset parent/enfant)
  - variant de shader (permutation technique)
  - override par objet rendu
- Garder `MaterialPropertyBlock` comme outil runtime, meme si une couche authoring `MaterialInstanceData` arrive au-dessus.
- Toujours privilegier un chemin de migration qui conserve les assets et demos existants utilisables.