# [ARCHIVE] Refactor d'architecture materials CasaEngine — Liste de taches IA

## Etat du document

Ce plan exploratoire n'est plus la source de verite du backlog.

Une partie importante de son contenu a deja ete livree via :

- `ai-agent/tasks/archive/material-shader-deep-audit-tasks.md` pour le contrat de capacites, l'extensibilite du registre, la compilation extensible et le draw path compile
- `ai-agent/tasks/archive/material-system-modernization-tasks.md` pour la migration authoring/runtime et l'integration editor/runtime
- `docs/engine/materials-workflow.md` pour l'etat final documente

Le laisser entierement ouvert en l'etat est trompeur. Le conserver uniquement comme note d'orientation long terme ; toute nouvelle tache d'architecture doit repartir de l'etat final documente, pas de cette liste.

## Objectif

Faire une vraie evolution d'architecture pour que CasaEngine reste generique, modulable et moderne :

- remplacer les checks de type par un contrat de capacites et de semantiques material,
- rendre le registre de definitions de materials extensible,
- introduire un descripteur runtime stable entre l'authoring et le draw path.

Ce plan doit conserver la compatibilite avec le pipeline actuel pendant la migration.

## Regles obligatoires pour l'agent IA

1. Une seule tache a la fois.
2. Avant de commencer une tache, remplacer son icone `⏳` par `🚧`.
3. Ne jamais commencer la tache suivante avant d'avoir termine la tache courante.
4. A la fin de chaque tache, mettre a jour ce fichier et remplacer l'icone par `✅`, `🧪` ou `⚠️`.
5. Commiter entre chaque tache, avec la mise a jour du statut dans le meme commit.
6. Toute modification du moteur CasaEngine doit obligatoirement etre accompagnee de :
   - mise a jour ou ajout d'une demo dans `CasaEngine.Demos`,
   - mise a jour ou ajout de tests unitaires dans `CasaEngine.Tests`.
7. Ne pas casser l'API existante brutalement ; introduire des adaptateurs, fallbacks ou chemins de compatibilite quand c'est raisonnable.
8. Les nouvelles abstractions doivent etre neutres vis-a-vis de RacingGame et ne pas encoder d'indices ou heuristiques de contenu specifique.
9. Statuts autorises :
   - `⏳ Todo`
   - `🚧 In progress`
   - `✅ Done`
   - `🧪 Needs testing`
   - `⚠️ Blocked`

## Validation minimale par tache

- Build moteur : `dotnet build CasaEngine.Editor.MonoGame.sln -c Debug --no-restore`
- Build demos : `dotnet build CasaEngine.Demos/CasaEngine.Demos.csproj -c Debug --no-restore`
- Tests bornes : `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj -c Debug --filter <FiltreCible> --no-restore`

## Criteres d'acceptation finaux

- Le renderer peut resoudre les shaders et features via un contrat de semantiques/capacites, pas seulement via des checks de type.
- Le registre de definitions de materials peut etre etendu sans modifier une liste statique centrale.
- Un descripteur runtime stable existe entre `MaterialAsset` et le draw path.
- Les demos et tests couvrent explicitement cette nouvelle architecture.

---

## Phase 1 — Introduire un contrat de capacites / semantiques material

- ⏳ **T01.01 — Definir le vocabulaire de semantiques material**
  Objectif :
  - Introduire les enums, flags ou records necessaires pour exprimer les semantiques de surface, de textures et de capacites runtime.
  - Garder ce vocabulaire generique et independant des shaders concrets.
  Validation :
  - Build moteur borne.
  - Tests unitaires cibles si conversion/flags non triviaux.
  Commit conseille :
  - `feat(materials): define generic material semantics vocabulary`

- ⏳ **T01.02 — Introduire un contrat runtime de capacites material**
  Objectif :
  - Ajouter une interface ou un descripteur lisible par le renderer pour exposer les capacites d'un material.
  - Eviter d'obliger le renderer a connaitre chaque classe concrete.
  Validation :
  - Build moteur + demos.
  - Test unitaire cible sur le contrat.
  Commit conseille :
  - `feat(materials): add runtime capability contract for materials`

- ⏳ **T01.03 — Adapter les materials runtime existants a ce contrat**
  Objectif :
  - Faire exposer les semantiques/capacites par `LitDiffuseMaterial`, `UnlitTextureMaterial` et `Material`.
  - Garder les APIs existantes utilisables pendant la transition.
  Validation :
  - Build moteur + demos.
  - Tests cibles sur les adapters runtime.
  Commit conseille :
  - `refactor(materials): expose capabilities from existing runtime materials`

- ⏳ **T01.04 — Mettre a jour une demo de reference semantique**
  Objectif :
  - Ajouter dans `CasaEngine.Demos` une demo ou un affichage debug montrant les semantiques/capacites resolues pour quelques materials.
  Validation :
  - Build demos borne.
  Commit conseille :
  - `feat(demos): expose material capability inspection`

---

## Phase 2 — Remplacer progressivement les checks de type dans le renderer

- ⏳ **T02.01 — Faire consulter le contrat par `RenderFeatureResolver`**
  Objectif :
  - Faire en sorte que le resolver de features interroge d'abord le contrat de capacites/semantiques.
  - Garder les checks de type actuels en fallback transitoire.
  Validation :
  - Build moteur + demos.
  - Tests bornes sur `RenderFeatureResolver`.
  Commit conseille :
  - `refactor(rendering): derive features from material capability contract`

- ⏳ **T02.02 — Faire consulter le contrat par `EffectiveShaderResolver`**
  Objectif :
  - Resoudre le shader effectif a partir des semantiques/capacites avant de retomber sur les switches de type.
  Validation :
  - Build moteur + demos.
  - Tests bornes sur `EffectiveShaderResolver`.
  Commit conseille :
  - `refactor(rendering): resolve shaders from material semantics first`

- ⏳ **T02.03 — Introduire une policy de selection de technique cote shader**
  Objectif :
  - Deplacer progressivement la selection de technique hors des classes material et vers une policy liee au shader ou a la variante.
  - Garder le comportement de `LitDiffuseMaterial` en compatibilite pendant la transition.
  Validation :
  - Build moteur + demos.
  - Tests cibles sur la selection de techniques.
  Commit conseille :
  - `feat(rendering): add shader-side technique selection policy`

- ⏳ **T02.04 — Ajouter une demo de verification renderer**
  Objectif :
  - Verifier visuellement que la resolution features/shaders continue de fonctionner apres la reduction des checks de type.
  Validation :
  - Build demos borne.
  Commit conseille :
  - `feat(demos): validate renderer behavior after semantics-based routing`

---

## Phase 3 — Rendre le registre de definitions extensible

- ⏳ **T03.01 — Extraire le bootstrap built-in du registre statique**
  Objectif :
  - Separer les definitions built-in de la mecanique d'enregistrement.
  - Garder les definitions actuelles disponibles par defaut.
  Validation :
  - Build moteur borne.
  - Tests cibles sur l'initialisation du registre.
  Commit conseille :
  - `refactor(materials): split built-in definitions from registry mechanics`

- ⏳ **T03.02 — Introduire une API d'enregistrement extensible**
  Objectif :
  - Permettre d'enregistrer des `MaterialDefinition` et leurs services associes sans modifier une liste statique centrale.
  Validation :
  - Build moteur + demos.
  - Tests bornes sur l'API de registre.
  Commit conseille :
  - `feat(materials): add extensible material definition registry`

- ⏳ **T03.03 — Rendre `MaterialCompiler` extensible par definition**
  Objectif :
  - Remplacer progressivement le `switch` sur `definition.Id` par une mecanique de compilateurs/enregistrements par definition.
  Validation :
  - Build moteur + demos.
  - Tests cibles sur la compilation authoring/runtime.
  Commit conseille :
  - `refactor(materials): make material compilation pluggable by definition`

- ⏳ **T03.04 — Faire consommer le registre extensible par l'editeur**
  Objectif :
  - Faire en sorte que l'editeur de materials et les inspecteurs s'appuient sur le registre extensible, pas sur une liste figee.
  Validation :
  - Build moteur + demos.
  - Verification visuelle ou smoke test editeur cible.
  Commit conseille :
  - `refactor(editor): use extensible material definition registry`

- ⏳ **T03.05 — Ajouter une demo et des tests d'extensibilite**
  Objectif :
  - Ajouter un cas demonstratif minimal d'enregistrement d'une definition supplementaire.
  - Couvrir le chargement et la compilation via tests unitaires cibles.
  Validation :
  - `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj -c Debug --filter FullyQualifiedName~MaterialDefinition --no-restore`
  Commit conseille :
  - `test(materials): cover extensible material definition registration`

---

## Phase 4 — Introduire un descripteur runtime stable entre authoring et draw path

- ⏳ **T04.01 — Definir le descripteur runtime stable**
  Objectif :
  - Introduire un objet runtime stable portant shader effectif, features, textures par semantique, constantes material et render states.
  - Eviter qu'il soit trop couple a un shader unique ou a une classe material concrete.
  Validation :
  - Build moteur borne.
  - Tests cibles sur le modele de descripteur.
  Commit conseille :
  - `feat(materials): add stable runtime render material descriptor`

- ⏳ **T04.02 — Faire produire ce descripteur par `MaterialCompiler`**
  Objectif :
  - Etendre `MaterialCompiler` pour qu'il produise ce descripteur en plus des objets actuels.
  - Garder `CompiledMaterial` et `MaterialBase` pendant la migration si necessaire.
  Validation :
  - Build moteur + demos.
  - Tests bornes sur `MaterialCompiler`.
  Commit conseille :
  - `feat(materials): compile stable render descriptors from material assets`

- ⏳ **T04.03 — Faire consommer le descripteur par la resolution shader/features**
  Objectif :
  - Brancher `EffectiveShaderResolver` et `RenderFeatureResolver` sur ce descripteur avant les fallbacks legacy.
  Validation :
  - Build moteur + demos.
  - Tests bornes sur les resolvers.
  Commit conseille :
  - `refactor(rendering): resolve shaders and features from compiled render descriptor`

- ⏳ **T04.04 — Faire consommer le descripteur par le draw path statique**
  Objectif :
  - Utiliser le descripteur runtime dans `StaticMeshRendererComponent` et le draw path associe sans supprimer immediatement la compatibilite `MaterialBase`.
  Validation :
  - Build moteur + demos.
  - Demo de rendu cible mise a jour.
  Commit conseille :
  - `refactor(rendering): thread compiled render descriptor into static draw path`

- ⏳ **T04.05 — Ajouter une demo et des tests de non-regression sur le descripteur**
  Objectif :
  - Verifier que la compilation et la consommation du descripteur restent stables pour les materials existants.
  Validation :
  - `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj -c Debug --filter FullyQualifiedName~CompiledMaterial --no-restore`
  Commit conseille :
  - `test(rendering): cover compiled render descriptor end-to-end`
