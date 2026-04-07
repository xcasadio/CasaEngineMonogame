# Completer la voie materials legacy existante — Liste de taches IA

## Objectif

Completer la voie actuelle sans refonte lourde pour obtenir un pipeline exploitable de bout en bout sur les points suivants :

- importer la reflection jusqu'au runtime et au draw path,
- consommer l'ambient de facon propre dans le rendu statique,
- sortir les heuristiques de naming du chemin moteur generique.

Le but de ce plan n'est pas de finaliser la grande refonte d'architecture, mais de terminer proprement la voie existante pour qu'elle soit coherente, testee et demonstrable.

## Regles obligatoires pour l'agent IA

1. Une seule tache a la fois.
2. Avant de commencer une tache, remplacer son icone `⏳` par `🚧`.
3. Ne jamais commencer la tache suivante avant d'avoir termine la tache courante.
4. A la fin de chaque tache, mettre a jour ce fichier et remplacer l'icone par `✅`, `🧪` ou `⚠️` selon le resultat.
5. Commiter entre chaque tache, avec le code, les tests/demos et la mise a jour du statut dans le meme commit.
6. Si une tache touche le moteur CasaEngine (`CasaEngine/CasaEngine/**`, `CasaEngine/CasaEngine.Editor/**`, `CasaEngine/CasaEngine.EditorServices/**`, `CasaEngine/CasaEngine.Shaders/**`), l'agent doit obligatoirement :
   - mettre a jour ou ajouter une verification dans `CasaEngine.Demos`,
   - mettre a jour ou ajouter des tests unitaires dans `CasaEngine.Tests`.
7. Ne pas laisser de logique RacingGame hardcodee dans le moteur pendant ce plan ; utiliser des metadonnees, des flags explicites ou des points d'extension neutres.
8. Statuts autorises :
   - `⏳ Todo`
   - `🚧 In progress`
   - `✅ Done`
   - `🧪 Needs testing`
   - `⚠️ Blocked`

## Validation minimale par tache

- Build moteur : `dotnet build CasaEngine.Editor.MonoGame.sln -c Debug --no-restore`
- Build demos : `dotnet build CasaEngine.Demos/CasaEngine.Demos.csproj -c Debug --no-restore`
- Tests bornes : `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj -c Debug --filter <FiltreCible> --no-restore`
- Si la tache touche aussi RacingGameCasaEngine : `dotnet build RacingGameCasaEngine/RacingGameCasaEngine.csproj -c Debug --no-restore`

## Criteres d'acceptation finaux

- `reflectionCubeTexture` est importee, materialisee et rendue jusqu'au draw path statique.
- `AmbientColor` n'est plus seulement repliée aveuglement en emissive dans le chemin moteur generique.
- Les heuristiques de naming ne pilotent plus le coeur du pipeline moteur.
- Chaque evolution moteur est couverte par une demo cible et des tests unitaires cibles.

## Etat d'execution

- Builds valides : `CasaEngine.Editor.MonoGame.sln`, `CasaEngine.Demos.csproj`, `RacingGameCasaEngine.csproj`.
- Couverture ajoutee : demos et tests cibles pour reflection, ambient et hints d'import.
- Tests cibles valides : `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj -c Debug --no-restore --filter "FullyQualifiedName~StaticModelImporterTests|FullyQualifiedName~EditorAssetImportServiceTests|FullyQualifiedName~MaterialCompilerTests|FullyQualifiedName~RenderFeatureResolverTests|FullyQualifiedName~EffectiveShaderResolverTests|FullyQualifiedName~MaterialRuntimeResolverTests|FullyQualifiedName~MaterialAssetJsonSerializerTests|FullyQualifiedName~MaterialDefinitionRegistryTests|FullyQualifiedName~MaterialDefinitionEditorRegistryTests"` -> 59/59.
- Validation visuelle bornee executee : capture propre de `MaterialDemo` et audit screenshots `RacingGameCasaEngine --capture-track-audit` completes.
- Reserve visuelle : les captures automatiques `RacingGameCasaEngine` montrent un cadrage d'audit encore peu exploitable pour juger les assets legacy fins; elles confirment surtout que la voie automation tourne jusqu'au bout.

---

## Phase 1 — Reflection de bout en bout

- ✅ **T01.01 — Auditer la voie reflection existante**
  Objectif :
  - Verifier le flux complet `StaticModelImporter -> EditorAssetImportService -> MaterialAsset -> MaterialCompiler -> renderer statique`.
  - Documenter precisement ou la reflection est perdue aujourd'hui.
  Validation :
  - Build moteur borne.
  Commit conseille :
  - `docs(materials): audit legacy reflection pipeline gaps`

- ✅ **T01.02 — Importer les textures de reflection cote editor**
  Objectif :
  - Etendre `StaticModelImporter.GetTextureFilePaths()` et `EditorAssetImportService.ImportTextureAssets()` pour inclure `ReflectionTextureFilePath`.
  - Etendre la structure `ImportedTextureAssets` pour transporter la reflection par index de material.
  Validation :
  - Build moteur borne.
  - Test unitaire cible sur l'import de textures legacy.
  Commit conseille :
  - `feat(import): import legacy reflection textures`

- ✅ **T01.03 — Materialiser la reflection dans les materials importes**
  Objectif :
  - Faire en sorte que les materials importes exposent aussi `reflection_texture` quand une cubemap legacy est presente.
  - Choisir une voie transitoire coherente : reutiliser `legacy-multi-texture` si cela suffit, sinon introduire un material runtime minimal dedie.
  Validation :
  - Build moteur + demos.
  - Test unitaire cible sur `MaterialCompiler`.
  Commit conseille :
  - `feat(materials): thread reflection texture into imported materials`

- ✅ **T01.04 — Declarer les features runtime de reflection**
  Objectif :
  - Ajouter le flag de feature ou la semantique minimale necessaire pour qu'un draw path sache qu'une reflection est requise.
  - Etendre `RenderFeatureResolver` sans casser les features existantes.
  Validation :
  - Build moteur + demos.
  - Tests unitaire cibles sur `RenderFeatureResolver`.
  Commit conseille :
  - `feat(rendering): expose reflection feature in runtime materials`

- ✅ **T01.05 — Ajouter un shader built-in reflection-aware**
  Objectif :
  - Ajouter un shader ou une variante built-in pour surfaces statiques reflechissantes.
  - Binder la cubemap de reflection au shader et declarer les parametres associes.
  Validation :
  - Build moteur + demos.
  - Demo visuelle minimale dans `CasaEngine.Demos`.
  Commit conseille :
  - `feat(shaders): add built-in static reflection shader path`

- ✅ **T01.06 — Brancher la resolution shader reflection dans le renderer statique**
  Objectif :
  - Etendre `EffectiveShaderResolver`, `StaticMeshRendererComponent` et la librairie de variantes pour enregistrer la voie reflection.
  - Garder les fallbacks actuels pour les materials non reflechissants.
  Validation :
  - Build moteur + demos.
  - Test unitaire cible sur la resolution de shader effectif.
  Commit conseille :
  - `feat(rendering): route reflective static materials to dedicated shader`

- ✅ **T01.07 — Ajouter une demo de reference pour la reflection**
  Objectif :
  - Ajouter ou etendre une demo dans `CasaEngine.Demos` montrant une surface simple avec reflection active.
  - Permettre une verification visuelle rapide apres chaque regression future.
  Validation :
  - Build demos borne.
  Commit conseille :
  - `feat(demos): add reflection coverage for imported static materials`

- ✅ **T01.08 — Ajouter des tests de non-regression reflection**
  Objectif :
  - Couvrir l'import, la compilation material et la resolution shader reflection.
  - Garder des tests bornes, sans run ouvert sur toute la suite.
  Validation :
  - `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj -c Debug --filter FullyQualifiedName~Reflection --no-restore`
  Commit conseille :
  - `test(materials): cover reflection import and runtime routing`

---

## Phase 2 — Consommer l'ambient de facon propre

- ✅ **T02.01 — Auditer le contrat ambient runtime et shader**
  Objectif :
  - Verifier la coherence entre `LightingContext.AmbientColor`, les materials runtime et les shaders statiques.
  - Documenter la difference entre ambient global, ambient legacy par material et emissive.
  Validation :
  - Build moteur borne.
  Commit conseille :
  - `docs(rendering): audit ambient contract across runtime and shaders`

- ✅ **T02.02 — Consommer `AmbientColor` dans le shader statique principal**
  Objectif :
  - Utiliser effectivement `AmbientColor` dans le shader statique au lieu de l'ignorer.
  - Garder un rendu stable pour les cas non legacy.
  Validation :
  - Build moteur + demos.
  - Demo de rendu simple mise a jour.
  Commit conseille :
  - `feat(shaders): consume ambient lighting in static forward shader`

- ✅ **T02.03 — Clarifier la traduction de l'ambient legacy par material**
  Objectif :
  - Introduire une representation explicite de l'ambient legacy par material si necessaire.
  - Arreter la conversion implicite et systematique `AmbientColor -> EmissiveColor` dans le chemin moteur generique.
  Validation :
  - Build moteur + demos.
  - Tests cibles sur compilation/import material.
  Commit conseille :
  - `refactor(materials): separate legacy ambient intent from emissive fallback`

- ✅ **T02.04 — Ajouter une demo et des tests ambient**
  Objectif :
  - Ajouter une verification visuelle et des tests cibles montrant la difference entre ambient, emissive et specular.
  Validation :
  - `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj -c Debug --filter FullyQualifiedName~Ambient --no-restore`
  Commit conseille :
  - `test(rendering): cover ambient lighting behavior for static materials`

---

## Phase 3 — Sortir les heuristiques de naming du chemin moteur

- ✅ **T03.01 — Introduire des metadonnees d'import explicites pour alpha-cutout**
  Objectif :
  - Ajouter un flag ou une intention explicite dans les metadonnees importees pour piloter l'alpha-cutout.
  - Ne plus depender directement des noms `Alpha*`, `Palm`, `Leave`, `Ast`, `plants` dans le moteur.
  Validation :
  - Build moteur + demos.
  - Test cible sur l'importer ou le service d'import editor.
  Commit conseille :
  - `feat(import): persist explicit alpha-cutout import intent`

- ✅ **T03.02 — Introduire des metadonnees explicites pour bright ambient legacy**
  Objectif :
  - Deplacer l'intention "bright ambient" dans une metadata d'import ou un hint explicite.
  - Prepararer la suppression des checks `Sign/Banner/Windmill` du moteur.
  Validation :
  - Build moteur + demos.
  - Test cible sur material import/compile.
  Commit conseille :
  - `feat(import): persist explicit bright ambient import hint`

- ✅ **T03.03 — Remplacer les heuristiques dans `EditorAssetImportService`**
  Objectif :
  - Consommer les nouvelles metadonnees explicites et supprimer les decisions basees sur le naming dans le service moteur.
  Validation :
  - Build moteur + demos.
  - Tests cibles sur le service d'import editor.
  Commit conseille :
  - `refactor(import): remove naming heuristics from engine import service`

- ✅ **T03.04 — Remplacer les heuristiques dans le runtime de compatibilite**
  Objectif :
  - Faire en sorte que la voie runtime de compatibilite ne reintroduise pas les memes heuristiques si la metadata explicite est disponible.
  - Laisser au pire un fallback transitoire court et documente.
  Validation :
  - Build moteur + RacingGameCasaEngine.
  - Verification visuelle bornee sur un asset alpha-cutout et un asset signage.
  Commit conseille :
  - `refactor(runtime): consume imported material hints instead of naming heuristics`

- ✅ **T03.05 — Ajouter les demos et tests de non-regression heuristiques**
  Objectif :
  - Couvrir le nouveau chemin metadata-driven dans `CasaEngine.Demos` et `CasaEngine.Tests`.
  - Garder une verification bornee sur l'import et la compilation.
  Validation :
  - `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj -c Debug --filter FullyQualifiedName~Import --no-restore`
  Commit conseille :
  - `test(import): cover explicit material import hints`
