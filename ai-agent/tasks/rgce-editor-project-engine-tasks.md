# Plan agent IA — Fondations moteur et éditeur pour le projet éditeur de RacingGameCasaEngine

Tranche S2 du programme RacingGame `ai-agent/tasks/rgce-editor-project-tasks.md` (dépôt superprojet RacingGame). Ce programme fait de `RacingGameCasaEngine/Content` un projet CasaEngine complet, dont tous les assets et les mondes se chargent et s'éditent dans l'éditeur.

Les décisions D1 → D8 et les propositions P1 → P10 de ce programme ont été arbitrées ou validées avec l'auteur le 2026-10-08. Le 2026-10-08, l'auteur a aussi demandé d'exécuter toutes les tranches (« fait toutes les tâches ») : **ce plan les applique, il ne les rediscute pas**.

Ce fichier doit être mis à jour pendant le travail : l'icône au début de chaque tâche indique son statut courant.

> **Quand écrire un plan** : dès que le travail demande plus d'un commit. En dessous, exécution directe avec le rapport de fin de tâche d'`AGENTS.md`.
> **Avant d'écrire le plan** : poser toutes les questions en une seule fois ; ne rien inventer, ne rien supposer.
> **Après approbation** : exécution autonome, tâche par tâche ; arrêt uniquement sur ⚠️ Blocked.

## Objectif

Corriger dans CasaEngine les manques qui bloquent la suite du programme :
- **E1, sauvegarde des composants de jeu.** Un composant défini par un jeu garde ses champs à l'enregistrement d'un monde, d'une entité ou d'une copie de Play.
- **E1b, liste « Add Component ».** Elle propose les composants de la DLL de gameplay courante, y compris après un rechargement des scripts.
- **E2, types inconnus.** Un type inconnu dans un monde ne fait plus planter l'éditeur et ne perd plus de données.
- **E3, documents.** Les types d'assets du jeu sans document s'ouvrent dans l'éditeur, au niveau P1 du programme :
  - `.staticModel` et `.texture` : ouverts, édités et enregistrés ;
  - images, `.wav`, `.fnt`, glTF : ouverts en aperçu ;
  - données brutes : ouvertes en lecture seule.
- **E4, réflexion.** Les facteurs de réflexion de `lit-diffuse` deviennent des propriétés de `.material`.
- **E5, automatisation.** Elle devient un contrôle fiable : journal du moteur dans les diagnostics, code de sortie, délai maximal.

Ce que la tranche ne livre pas est dans « Hors périmètre ».

## État vérifié du dépôt (2026-10-08)

Découverte en lecture seule du programme (rapports `editor-disc2/` du scratchpad de la session, contre-vérifiés), et code relu directement.

- **Dépôt** : CasaEngine, branche `chantier/rgce-editor-project` créée depuis `main` (`2435f8ab9`). Elle porte déjà `c0f3343` (panneau Logs lisible, pointeur MGUI `4e94d7e8`) et `4ce5ea0` (option `--logs-filter`). MGUI est sur sa propre branche `chantier/rgce-editor-project`.
- **Chargement d'un composant** : `ElementFactory.Load<T>` lit `type`, crée l'instance par `Activator.CreateInstance` et appelle `Load(JObject)` (`CasaEngine/Framework/Assets/ElementFactory.cs:22-34`).
  - Un nom de type inconnu donne `FindTypeByName` = null, puis `CreateInstance(null)`, sans message utile.
  - `ObjectBase.Load` ne lit que `id` et `name` (`Framework/Common/ObjectBase.cs:58-62`).
  - `EntityComponent` n'a pas de `Load` (`Framework/Scene/Entities/Components/EntityComponent.cs`). `SceneComponent.Load` lit `local_transform` et `children_component`, puis crée chaque enfant par `ElementFactory.Load<SceneComponent>` (`SceneComponent.cs:416-444`).
- **Enregistrement** : `EditorEntityJsonSerializer.SaveComponent` est un `switch` fermé (`CasaEngine.EditorServices/EditorEntityJsonSerializer.cs:187-267`). Le cas par défaut n'écrit que `id`, `name` et `type` ; un `SceneComponent` inconnu n'écrit que son transform et ses enfants. Play passe par le même code.
- **Liste « Add Component »** : un `Lazy` statique parcourt `AppDomain.GetAssemblies()`, contextes collectibles compris, et n'est jamais recalculé (`CasaEngine.Editor/Controls/EntityDetailsPanel.cs:29,1232-1267`). `ElementFactory` exclut au contraire les contextes collectibles et ajoute les assemblies de script enregistrées (`ElementFactory.cs:70-86`).
- **Matériaux** : la définition `lit-diffuse` (`Framework/Materials/Definitions/BuiltInMaterialDefinitions.cs:14-110`) n'a ni `reflection_add_amount`, ni `reflection_multiply_base`, ni `reflection_multiply_factor`, ni `use_scene_reflection_cube`. `LitDiffuseMaterial` les a pourtant et les envoie au shader (`Framework/Materials/Runtime/LitDiffuseMaterial.cs:23,32-34,123-129`). `MaterialCompiler.CreateLitDiffuseMaterial` ne les copie pas (`Framework/Materials/Compilation/MaterialCompiler.cs:285-310`). `MaterialPropertyType` a `Boolean` et `Float`.
- **Documents** : `GameEditor.GetAssetDocumentRoutes` ouvre 13 extensions (`GameEditor.cs:4478-4497`). Sans route, `TryOpenEditorAsset` renvoie false.
  - Sur le catalogue de RacingGameCasaEngine, 28 entrées sur 94 ne s'ouvrent pas : 10 png, 8 texture, 6 wav, 3 track, 1 fnt (référence T0.1 du programme).
  - Patrons existants : `SoundAssetInspectorPanel` (document avec enregistrement), `MaterialPreviewViewport` (aperçu 3D par `PreviewWorldDriver`, `RenderTargetSurface`, `RenderView`, caméra).
  - `EditorAssetJsonSerializer.TrySerialize` sait écrire `StaticModel` et `Texture`.
- **Automatisation** :
  - `--diagnostics-out` ne contient que `EditorDiagnosticsBuffer`, sans les `Logs` du moteur.
  - Le code de sortie vaut 0 même si l'asset ne s'ouvre pas.
  - Un monde de départ sans entité ou un projet qui ne charge pas fait attendre l'éditeur sans fin.
- **Règles du dépôt** : §9.7 (formats stables, ajouts additifs, validation au chargement avec contexte) ; §9.9 (l'écriture des assets reste dans `CasaEngine.EditorServices` et `CasaEngine.Editor`, le runtime ne fait que charger) ; §6 (build, tests, sample pour une feature visible) ; §10 (ADR).

## Décisions verrouillées

| Réf | Décision |
|---|---|
| D1 | Programme RacingGame, D3 : les manques du moteur et de l'éditeur se corrigent dans CasaEngine, sur une branche dédiée (`chantier/rgce-editor-project`). |
| D2 | Programme RacingGame, P7 (validée) : la sauvegarde des composants de jeu passe par un attribut sur les propriétés sérialisées. Le runtime les lit avec une aide générique ; l'éditeur les écrit avec un écrivain générique. |
| D3 | Programme RacingGame, P1 (validée) : niveaux d'ouverture par type d'asset.<br>• **Ouvrir, éditer, enregistrer** : `.world`, `.entity`, `.staticModel`, `.material`, `.texture`, `.sound`, `.uiscreen`, `.sprite`.<br>• **Aperçu** : images, `.wav`, `.fnt`.<br>• **Lecture seule** : données de jeu (`.Track`, carte de hauteurs, `.CombiModel`). |
| D4 | L'auteur (2026-10-08) : exécuter toutes les tranches, en enchaînant, sans nouvelle approbation tant que le travail reste dans les décisions déjà prises. |

## Points à valider avec ce plan

| Réf | Proposition |
|---|---|
| P1 | **Attribut** `[SerializedProperty("json_key")]` (`CasaEngine.Core.Serialization`), sur des propriétés publiques en lecture et écriture.<br>• Types pris en charge : `bool`, `int`, `float`, `double`, `string`, `Guid`, énumérations, `Vector2`, `Vector3`, `Vector4`, `Quaternion`, `Color`, aux formats de `JsonHelper`.<br>• Le runtime (`SerializedProperties.Load`) les lit dans `ElementFactory.Load<T>`, après le `Load` du type. Une clé absente garde la valeur par défaut ; une valeur illisible est journalisée avec le type et la clé, sans lever d'exception.<br>• Le cache par type est un `ConditionalWeakTable`, qui ne retient pas un contexte de script collectible.<br>• `SerializedProperties.Copy` aide les `Clone()`. Contrat, écrit dans l'ADR-0071 : le `Clone()` d'un composant de jeu appelle `Copy` (ou copie lui-même chaque propriété annotée), sinon une entité modèle (`LoadCopy(...).Clone()`) ou une duplication dans l'éditeur perd ses valeurs.<br>• L'éditeur écrit ces propriétés après l'écriture propre au type, pour tous les composants. |
| P2 | **Type inconnu** : `ElementFactory.LoadComponent<T>` remplace un composant de type inconnu par un `UnresolvedComponent` (`SceneComponent` concret, tolérant), qui garde le nœud JSON d'origine. L'erreur est journalisée avec le nom du type, l'id et le nom du composant. L'éditeur réécrit le nœud tel quel : rien n'est perdu à l'enregistrement. Entité, composants et composant racine passent par ce chemin. |
| P3 | **Liste « Add Component »** recalculée à chaque ouverture de la boîte, à partir de `ElementFactory.GetDerivedTypesFrom<EntityComponent>()` (mêmes assemblies que le chargement), avec les filtres actuels : constructeur public sans paramètre, `[Browsable]`, `[DisplayName]`. |
| P4 | **Réflexion de `lit-diffuse`** : `reflection_add_amount` (1), `reflection_multiply_base` (1), `reflection_multiply_factor` (0) et `use_scene_reflection_cube` (false), aux valeurs par défaut actuelles du runtime. Un `.material` existant garde donc son rendu ; le compilateur les copie. |
| P5 | **Automatisation** :<br>• les avertissements et erreurs du journal de l'éditeur sont ajoutés aux diagnostics (section `Logs:`) ;<br>• code de sortie 1 si un asset ne s'ouvre pas, si un contrôle de `--play-smoke` échoue ou en cas d'échec de l'automatisation ;<br>• `--automation-timeout <s>` (300 par défaut quand l'automatisation est active) écrit les diagnostics puis sort avec le code 2. |
| P6 | **Un document d'aperçu générique**, en lecture seule, ouvre tout fichier catalogué sans autre document : images (`png`, `jpg`, `jpeg`, `bmp`, `gif`, `tga`), cubemaps `dds` (6 faces), audio `wav` et `ogg` (informations, lecture et arrêt), `.fnt` (police, pages, texte d'exemple), glTF et glb (modèle statique en aperçu 3D), et toute autre donnée.<br>**Route de repli** : elle ne s'applique qu'aux extensions qui ne sont pas des formats d'asset du moteur (`Constants.FileNameExtensions`) et n'ont pas de document. Un format du moteur sans document (`.texture` jusqu'à T7, `.staticModel` jusqu'à T8, `.model`, `.skeleton`, `.environment`, `.gameMode`, `.gameplayMode`, `.tileset`, `.buttonsMapping`, `.dialogue`, `.shader`) reste non ouvert jusqu'à son propre document.<br>Pour ces données : type, taille, aperçu texte pour un fichier texte, aperçu en niveaux de gris pour un fichier binaire dont la taille est un carré d'octets (carte de hauteurs). Les parties sans interface (classement, lecture des en-têtes) sont dans `CasaEngine.EditorServices` et testées. |
| P7 | **Document `.texture`** : aperçu de l'image liée, édition de l'échantillonneur (filtre, adressage U, V et W, anisotropie maximale) et enregistrement par `EditorAssetWriterService`.<br>• Intégration : état modifié par `EditorHistoryContextKind.Texture`, enregistrement dans `SaveCurrentProject` (`SaveDirtyTextureDocuments`).<br>• Option d'automatisation `--set-texture-sampler <clé>=<valeur>`, sur le patron de `--set-material-property` : instantané, modification, enregistrement. Les diagnostics donnent la valeur relue dans le fichier enregistré, et le fichier est restauré en fin de lancement. |
| P8 | **Document `.staticModel`** : aperçu 3D cadré sur le modèle (patron de `MaterialPreviewViewport`), liste des maillages et sous-maillages avec leur matériau, choix d'un matériau parmi les assets `material`, et enregistrement par `EditorAssetWriterService`.<br>• Intégration : `EditorHistoryContextKind.StaticModel`, `SaveDirtyStaticModelDocuments`.<br>• Option d'automatisation `--set-static-model-material <indice de maillage>=<id de matériau>`, même patron : les diagnostics donnent l'id relu dans le fichier enregistré. |
| P9 | **ADR** : 0071 (propriétés sérialisées des composants de jeu), 0072 (composants de type inconnu conservés), 0073 (documents d'aperçu et d'édition des assets sans document). Docs : `docs/engine/component-persistence.md`, `docs/editor/asset-documents.md`, plus les index. |

## Règles d'exécution pour l'agent

- **Branche `chantier/rgce-editor-project`** de CasaEngine (et de MGUI si MGUI doit changer). Ne jamais committer sur `main` ni sur `develop`.
- **Une seule tâche à la fois.** Avant de commencer une tâche, remplacer son icône `⏳` par `🚧`. À la fin, lancer la validation indiquée, remplacer l'icône par `✅`, `🧪` ou `⚠️`, ajouter une courte note de validation sous la tâche, puis **créer un commit dédié** qui inclut la mise à jour de ce fichier.
- **Un commit par tâche**, atomique et compilable, message en anglais au format `type(area): summary`, avec la ligne `Co-Authored-By` de la session. Les commandes de commit commencent par `cd D:/development/repo/RacingGame/CasaEngine &&`.
- **Ne jamais pousser.** Le merge sur `main` reste une décision humaine.
- **Ne rien inventer** : toute API, tout fichier, toute règle utilisée existe dans le dépôt, vient d'une réponse de l'auteur, ou d'une doc officielle citée (URL). Sinon : passer la tâche en ⚠️ Blocked, écrire la question dans « Points ouverts », et **s'arrêter**.
- **Build obligatoire** avant ✅ dès que du code est touché (`dotnet build CasaEngine.Editor.MonoGame.sln`, et `dotnet build CasaEngine.MonoGame.sln` pour le runtime) ; **tests** `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj` à chaque tâche de code. Si le build est impossible, la tâche reste 🧪 avec la raison écrite.
- **Vérification visuelle** des documents par l'automatisation de l'éditeur (`--open-asset`, `--screenshot-out`, `--diagnostics-out`) sur `Projects/SampleProject` et sur le projet RacingGameCasaEngine ; lecture des captures.
- Si le code est écrit mais qu'une vérification visuelle ou manuelle manque, utiliser `🧪 Needs testing` et noter précisément ce qui manque.
- **Ne jamais laisser une tâche en 🚧** à la fin d'une session.
- **Ne jamais indexer** les modifications préexistantes de l'auteur : `git add` fichier par fichier, jamais `git add -A` ni `git add .`.
- **Langue** : ce plan en français ; code, messages de commit, docs de `docs/` et ADR en anglais.
- Rappel moteur : pas d'allocation, de LINQ ni de closure dans les chemins chauds ; restaurer tout état GPU modifié ; le runtime ne dépend pas de l'éditeur ; sérialisation additive (détail dans `AGENTS.md` et les règles par chemin).
- **Au plus 3 cycles de correction par tâche** ; au-delà, ⚠️ Blocked.
- **Vérificateur indépendant** de la tranche avant de la déclarer faite.

## Légende des statuts

- ⏳ Todo : pas encore commencé.
- 🚧 In progress : en cours de modification locale.
- 🧪 Needs testing : code écrit, validation incomplète ou en attente.
- ✅ Done : code validé, build/tests OK, commit effectué.
- ⚠️ Blocked : bloqué par une erreur non résolue ou une décision manquante.

## Validation globale

- `dotnet build CasaEngine.Editor.MonoGame.sln` et `dotnet build CasaEngine.MonoGame.sln` : 0 erreur.
- `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj` : tout vert, nouveaux tests compris.
- **Automatisation sur le projet RacingGameCasaEngine** (superprojet) :
  - les 94 entrées du catalogue s'ouvrent, soit 28 de plus que la référence T0.1 ;
  - un asset qui ne s'ouvre pas donne le code de sortie 1, et ses diagnostics contiennent `Logs:`.
- **Captures lues** des nouveaux documents : `.staticModel` et `.texture` de `Projects/SampleProject`, png, wav, fnt, dds et données de RacingGameCasaEngine.
- **Vérificateur indépendant** : CONFIRMED.

---

## Phase 1 — Sauvegarde et chargement des composants

### ✅ T1 — Propriétés sérialisées des composants de jeu (E1)

- **Objectif** : un composant de jeu garde ses champs annotés à l'enregistrement et au rechargement (P1).
- **Fichiers** :
  - `CasaEngine/Core/Serialization/SerializedPropertyAttribute.cs` et `SerializedProperties.cs` (runtime : lecture, copie, cache) ;
  - `CasaEngine/Framework/Assets/ElementFactory.cs` (appel après `Load`) ;
  - `CasaEngine.EditorServices/EditorSerializedPropertyWriter.cs` et `EditorEntityJsonSerializer.cs` (écriture après le `switch`) ;
  - tests `CasaEngine.Tests/Serialization/SerializedPropertiesTests.cs` et `CasaEngine.Tests/EditorServices/EditorSerializedPropertyWriterTests.cs` ;
  - test de déchargement dans `CasaEngine.Tests/Scripting/ElementFactoryReloadTests.cs`, avec une surcharge de `ScriptAssemblyCompiler` qui ajoute un composant annoté à l'assembly compilée ;
  - **sample** (règle 6) : `Projects/CasaEngine.RPGDemo/Components/SampleAnnotatedComponent.cs` (étiquette, poids, couleur, id d'asset), posé sur une nouvelle entité de `Projects/RPGDemo/DefaultWorld.world` avec des valeurs autres que celles par défaut ;
  - `docs/decisions/0071-serialized-properties-of-game-components.md`, `docs/decisions/README.md`, `docs/engine/component-persistence.md`, `docs/README.md`.
- **Étapes** :
  1. Écrire l'attribut, la lecture, la copie et le cache.
  2. Brancher la lecture dans `ElementFactory.Load<T>`.
  3. Écrire l'écrivain éditeur et l'appeler pour tout composant, après l'écriture propre au type.
  4. Écrire les tests :
     - aller-retour de chaque type pris en charge ;
     - clé absente qui garde la valeur par défaut ;
     - valeur illisible journalisée, sans exception ;
     - entité avec un composant annoté : `SaveEntity` puis `Load` donnent le même JSON ;
     - `Copy`, et clonage d'une entité qui porte un composant annoté : le clone garde les valeurs ;
     - assembly de script collectible : un composant annoté qui en vient est chargé (ce qui remplit le cache), puis l'assembly est retirée de l'`ElementFactory` et déchargée. Un `WeakReference` sur son contexte doit être libéré. Ce test échoue avec un cache `Dictionary<Type, …>` statique.
  5. Sample RPGDemo, ADR et doc.
- **Validation** :
  - build des deux solutions ; tous les tests verts, nouveaux compris ;
  - **sample** : copie de `Projects/RPGDemo` et `Projects/CasaEngine.RPGDemo`, avec sa DLL compilée, dans le scratchpad. Puis `CasaEngine.Editor.exe --project <copie>\RPGDemo.json --open-asset DefaultWorld.world --entity-index 0 --save-project --diagnostics-out <f> --screenshot-out <png>`. Ensuite `jq` sur le `DefaultWorld.world` réenregistré : le composant `SampleAnnotatedComponent` garde ses valeurs. Avant T1, l'éditeur les perdait (cas par défaut du `switch`).
- **Commit** : `feat(serialization): persist annotated properties of game components`

> Validation (2026-10-08) :
> - **Code** :
>   - `SerializedPropertyAttribute` et `SerializedProperties` (`CasaEngine/Core/Serialization/`) : lecture, copie, cache `ConditionalWeakTable`. Le message d'une valeur illisible prend le nom et l'id dans le nœud JSON, ce qui évite à `Core` de dépendre de `Framework` ;
>   - appel dans `ElementFactory.Load<T>` ;
>   - `EditorSerializedPropertyWriter`, appelé par `SaveComponent` après le `switch`, renommé `SaveComponentCore`.
> - **Tests** : les tests du runtime et de l'écrivain sont regroupés dans `CasaEngine.Tests/Serialization/SerializedPropertiesTests.cs` (8 tests). Ils couvrent les 12 types, la clé absente, la valeur illisible journalisée, `Copy` et `Clone`, le clone d'entité, l'aller-retour `SaveEntity` → `Load` → `SaveEntity` égal, les déclarations invalides et l'ordre des clés.
> - **Déchargement** : `UnregisteredScriptAssembly_WithAnnotatedComponent_DoesNotPinTheLoadContext` (`ElementFactoryReloadTests`, avec `ScriptAssemblyCompiler` étendu) passe. Contrôle négatif : avec un `Dictionary<Type, …>` statique à la place du cache, ce test échoue (`Assert.True() Failure`) ; le fichier a été restauré.
> - **Build et tests** :
>   - `dotnet build CasaEngine.Editor.MonoGame.sln` : 0 erreur ;
>   - `dotnet build CasaEngine.MonoGame.sln` : 0 erreur, après l'ajout de `using System;` au sample, car RPGDemo n'a pas les usings implicites ;
>   - `CasaEngine.Tests` : 4 168 sur 4 168.
> - **Sample**, sur une copie de `Projects/RPGDemo` et `Projects/CasaEngine.RPGDemo` dans le scratchpad :
>   - l'éditeur, avec `--open-asset DefaultWorld.world --entity-index 0 --save-project`, sort avec le code 0 ;
>   - le monde est réécrit, puisque l'entité gagne les clés de politique que l'éditeur écrit, et `SampleAnnotatedComponent` garde `label`, `weight`, `tint` et `linked_asset_id` ;
>   - lancé par `CasaEngine.Launcher` sur `DefaultWorld.world` (réglé dans la copie seulement), RPGDemo tourne 12 s : aucun avertissement ni erreur dans `log.txt`, et les ennemis sont actifs.

### ✅ T2 — Composants de type inconnu conservés (E2)

- **Objectif** : un monde ou une entité qui cite un type inconnu se charge, journalise une erreur avec son contexte et se réenregistre sans perte (P2).
- **Fichiers** :
  - `CasaEngine/Framework/Scene/Entities/Components/UnresolvedComponent.cs` ;
  - `ElementFactory.cs` (`LoadComponent<T>`, message explicite pour un type inconnu) ;
  - `Entity.cs` et `SceneComponent.cs` (chargement des composants par `LoadComponent`) ;
  - `EditorEntityJsonSerializer.cs` (réécriture du nœud d'origine) ;
  - tests `CasaEngine.Tests/Scene/UnresolvedComponentTests.cs` ;
  - `docs/decisions/0072-unresolved-components-are-kept.md`, `docs/decisions/README.md`, `docs/engine/component-persistence.md`.
- **Validation** :
  - tests : composant inconnu, composant racine inconnu et enfant inconnu se chargent ; `SaveEntity` rend un JSON égal à l'original (`JToken.DeepEquals`) ; l'erreur est journalisée ;
  - tests de clonage, pour la racine, un composant et un enfant : `Entity.Clone()` d'une entité qui porte un `UnresolvedComponent`, puis `SaveEntity` du clone, rendent le nœud d'origine gardé par le composant. Un `Clone` qui ne copierait pas le nœud fait échouer ces tests ;
  - build ; tous les tests ;
  - dans l'éditeur, un monde copié dans le scratchpad avec un type inconnu s'ouvre, sans plantage.
- **Commit** : `feat(scene): keep components of unknown type instead of failing the load`

> Validation (2026-10-08) :
> - **Code** :
>   - `UnresolvedComponent` garde une copie du nœud, son transform et le nom du type ; ses enfants restent dans le nœud ;
>   - `ElementFactory.LoadComponent<T>`, appelé par `Entity.Load` (racine et composants) et `SceneComponent.Load` (enfants), journalise l'erreur avec le type, le nom et l'id, puis garde le nœud ;
>   - `Create<T>` lève une `ArgumentException` qui nomme le type ;
>   - l'éditeur réécrit le nœud gardé, avec l'id et le transform courants (`SaveUnresolvedComponent`), et exclut `UnresolvedComponent` de la liste « Add Component ».
> - **Tests** : `CasaEngine.Tests/Scene/UnresolvedComponentTests.cs`, 7 tests : composant, racine et enfant inconnus réécrits à l'identique ; racine déplacée ; clones de racine, de composant et d'enfant, dont le nœud est gardé, id renouvelé à part ; type existant mais qui n'est pas un composant.
>   - Contrôle négatif : avec un `Clone` qui ne copie pas le nœud, les 2 tests de clonage échouent ; le fichier a été restauré.
>   - Le test existant `UnregisteredScriptAssembly_IsNoLongerResolvable`, qui attend une `ArgumentException`, passe toujours.
> - **Build et tests** : les deux solutions compilent avec 0 erreur ; `CasaEngine.Tests` : 4 175 sur 4 175.
> - **Éditeur**, sur la copie de RPGDemo du scratchpad, avec un composant `ComponentThatNoAssemblyDeclares` ajouté à `DefaultWorld.world` :
>   - `--open-asset DefaultWorld.world --save-project` ouvre le monde (« Opened asset ») et sort avec le code 0 ;
>   - le monde est réécrit, puisque le fichier écrit avec une indentation de 4 revient à celle de 2 de l'éditeur, et le nœud inconnu y est intact, `nested` compris.

### ✅ T3 — Liste « Add Component » à jour (E1b)

- **Objectif** : la boîte « Add Component » propose les composants des assemblies que le chargement résout, recalculés à chaque ouverture (P3).
- **Fichiers** : `CasaEngine.Editor/Controls/EntityDetailsPanel.cs` : le `Lazy` statique disparaît, et une méthode interne de construction de la liste est appelée par la boîte à chaque ouverture ; test `CasaEngine.Tests/Editor/AddComponentListTests.cs`.
- **Validation** :
  - test, avec le banc de `ElementFactoryReloadTests` : on compile une assembly de script avec un composant, on l'enregistre, et la méthode le liste ; on la retire, et un nouvel appel ne le liste plus. Avec le `Lazy` actuel, le deuxième appel garderait l'ancien résultat et le test échouerait ;
  - test : un composant `[Browsable(false)]` n'y figure pas ;
  - build ; tous les tests.
- **Commit** : `fix(editor): rebuild the Add Component list from the loadable assemblies`

> Validation (2026-10-08) :
> - **Code** :
>   - le `Lazy` statique est retiré. `EntityDetailsPanel.BuildAddableComponentTypes()` part de `ElementFactory.GetDerivedTypesFrom<EntityComponent>()`, avec les filtres d'avant : constructeur public sans paramètre, `[Browsable]`, `[DisplayName]`, et sans `UnresolvedComponent`. La boîte l'appelle à chaque ouverture et garde ce dictionnaire pour son bouton Add ;
>   - `ElementFactory.BuildDerivedTypes` ignore désormais les types qui ne se chargent pas d'une assembly (`ReflectionTypeLoadException`), comme le faisait l'ancienne liste.
> - **Tests** : `CasaEngine.Tests/Editor/AddComponentListTests.cs`, 2 tests ; le composant visible est listé, ni `[Browsable(false)]` ni `UnresolvedComponent` ne le sont.
>   - Assembly de script compilée, enregistrée puis retirée : son composant est listé, puis ne l'est plus.
>   - Contrôle négatif : avec un résultat mis en cache dans la méthode, ce test échoue ; le fichier a été restauré.
> - **Build et tests** : les deux solutions compilent avec 0 erreur ; `CasaEngine.Tests` : 4 177 sur 4 177.

## Phase 2 — Matériaux et automatisation

### ⏳ T4 — Facteurs de réflexion de `lit-diffuse` (E4)

- **Objectif** : P4.
- **Fichiers** : `BuiltInMaterialDefinitions.cs`, `MaterialCompiler.cs` ; tests dans `CasaEngine.Tests` (compilation d'un `MaterialAsset`) ; `docs/engine/` (doc des matériaux qui liste les propriétés de `lit-diffuse`, si elle existe).
- **Validation** :
  - test : sans ces propriétés, les valeurs restent celles d'avant (1, 1, 0, false) ; avec elles, le `LitDiffuseMaterial` compilé les porte ;
  - build ; tous les tests ;
  - l'inspecteur de matériau les affiche (capture de `--open-asset` d'un `.material` de `Projects/SampleProject`).
- **Commit** : `feat(materials): author the reflection factors of lit-diffuse materials`

### ⏳ T5 — Automatisation fiable (E5)

- **Objectif** : P5.
- **Fichiers** : `EditorAutomationOptions.cs` (`--automation-timeout`), `GameEditor.cs` (section `Logs:`, code de sortie, délai) ; tests `EditorAutomationOptionsTests` et une aide pure de calcul du code de sortie.
- **Validation** :
  - tests ;
  - build ;
  - sur le projet RacingGameCasaEngine, un `.wav` encore sans document donne le code 1 et des diagnostics avec `Logs:`, tandis qu'un `.uiscreen` donne 0 ;
  - un délai de 5 s sur un projet sans fin d'attente donne le code 2.
- **Commit** : `feat(editor): make editor automation outcomes checkable`

## Phase 3 — Documents des assets

### ⏳ T6 — Document d'aperçu en lecture seule (E3a)

- **Objectif** : P6.
- **Fichiers** :
  - `CasaEngine.EditorServices/AssetPreview/` (classement d'un fichier, lecture des en-têtes d'image, de DDS, d'audio, de police, de glTF, de texte et de données ; sans interface) ;
  - `CasaEngine.Editor/Controls/AssetPreviewPanel.cs` et `ModelPreviewViewport.cs`, ce dernier partagé avec T8 ;
  - routes dans `GameEditor.cs`, avec une route de repli pour toute extension sans document ;
  - tests `CasaEngine.Tests/EditorServices/AssetPreviewTests.cs`.
- **Validation** :
  - tests (classement, en-têtes, carré d'octets) ;
  - build ;
  - captures lues, sur RacingGameCasaEngine : un png, un wav, `GameFont.fnt`, un `.Track` ; sur des fichiers de test du scratchpad : un dds et un glTF ;
  - catalogue de RacingGameCasaEngine ouvert entrée par entrée : 86 entrées sur 94 s'ouvrent. Les 8 `.texture` restent fermées, conformément à la règle de repli de P6.
- **Commit** : `feat(editor): preview assets that have no editing document`

### ⏳ T7 — Document `.texture` (E3b)

- **Objectif** : P7.
- **Fichiers** :
  - `CasaEngine.Editor/Controls/TextureAssetDocumentPanel.cs` et la route dans `GameEditor.cs` ;
  - `EditorHistoryContextKind.Texture` et `SaveDirtyTextureDocuments` dans `SaveCurrentProject` ;
  - option `--set-texture-sampler` (`EditorAutomationOptions.cs`, instantané et restauration dans `GameEditor.cs`) ;
  - tests de l'aller-retour d'un `Texture` modifié et de l'option (`CasaEngine.Tests`).
- **Validation** :
  - tests ; build ;
  - capture lue d'un `.texture` de RacingGameCasaEngine ;
  - `--open-asset UI/Sprites/Ui.Menu.ButtonsTexture.texture --set-texture-sampler address_u=Wrap --diagnostics-out <f>` : les diagnostics donnent `address_u=Wrap`, relu dans le fichier enregistré. Le fichier est restauré à l'identique en fin de lancement (SHA-1 avant et après) ;
  - catalogue de RacingGameCasaEngine : 94 entrées sur 94 s'ouvrent.
- **Commit** : `feat(editor): edit texture assets`

### ⏳ T8 — Document `.staticModel` (E3c)

- **Objectif** : P8.
- **Fichiers** :
  - `CasaEngine.Editor/Controls/StaticModelAssetDocumentPanel.cs`, avec `ModelPreviewViewport`, et la route ;
  - `EditorHistoryContextKind.StaticModel` et `SaveDirtyStaticModelDocuments` ;
  - option `--set-static-model-material` ;
  - tests de l'aller-retour d'un `StaticModel` dont le matériau d'un maillage change, géométrie inchangée.
- **Validation** :
  - tests ; build ;
  - capture lue de `Projects/SampleProject/Entities/Box.staticModel` ;
  - `--open-asset Entities/Box.staticModel --set-static-model-material 0=<id de NewLitDiffuseMaterial>` sur `Projects/SampleProject` : les diagnostics donnent cet id, relu dans le fichier enregistré. Le fichier est restauré à l'identique ensuite (SHA-1).
- **Commit** : `feat(editor): preview static models and assign their materials`

### ⏳ T9 — Clôture de la tranche

- **Objectif** :
  - validation globale, doc des documents (`docs/editor/asset-documents.md`), ADR-0073 et index ;
  - vérificateur indépendant ;
  - mise à jour de `ai-agent/README.md` et archivage de ce plan ;
  - côté RacingGame, enregistrement du pointeur.
- **Validation** : la validation globale ci-dessus ; vérificateur CONFIRMED.
- **Commit** : `docs(editor): close the editor project foundations plan`

---

## Points ouverts

| Réf | Sujet | Tâche concernée |
|---|---|---|
| O1 | `ai-agent/tasks/racinggame-car-profile-parity-plan.md` est un plan RacingGame égaré dans ce dépôt. Il reste tel quel. | — |

## Hors périmètre

- **Édition dans l'éditeur** : éditeur de tracé de piste ; édition des pixels d'une image ou des échantillons d'un son.
- **Play du parcours complet d'un jeu** dans l'éditeur.
- **Entités enfants** ajoutées par « Add Entity » sur une sélection, perdues à l'enregistrement : défaut connu de l'éditeur, signalé, non corrigé ici.
- **Sélecteur d'asset typé** pour les propriétés `*AssetId` des composants, qui gardent le sélecteur actuel.
- **Commande « cataloguer un fichier existant »**.
- **Le jeu RacingGameCasaEngine** : il relève des tranches S3 à S7 du programme RacingGame.
