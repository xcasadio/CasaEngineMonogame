# Architecture Editor Refacto Tasks

Etat observe le 2026-03-20.

## Scope

- Inclure `CasaEngine`, `CasaEngine.Editor`, `CasaEngine.EditorServices`, `CasaEngine.Launcher`, `CasaEngine.Demos`, `Projects/SandBoxGame`, `Editor` si necessaire pour compatibilite.
- Exclure explicitement `CasaEngine.EditorUI` de cette refacto.
- Ne pas faire de grand rewrite horizontal. Garder les formats de fichiers et les comportements utilisateur existants quand c'est possible.

## Constat court

- `CasaEngine/CasaEngine.csproj` reference encore `GizmoTools`, donc le runtime partage depend encore d'un outil editeur.
- `CasaEngine/Framework/Assets/AssetCatalog.cs` melange lecture runtime, mutations editor, suppression de fichiers, evenements UI et sauvegarde disque.
- `CasaEngine/Framework/Project/ProjectSettingsHelper.cs` et `CasaEngine/Framework/Project/ProjectSettings.cs` portent encore de l'etat d'edition (`ProjectFileOpened`).
- `CasaEngine.EditorServices` existe deja, mais une partie de ses services restent de simples facades sur des APIs runtime qui gardent encore la responsabilite editor.
- `CasaEngine/Framework/World/World.cs`, `CasaEngine/Framework/Entities/Entity.cs`, `CasaEngine/Framework/Entities/Components/SceneComponent.cs`, `CasaEngine/Framework/Materials/*` et beaucoup d'assets partages exposent encore `Save(JObject)` pour de l'authoring.
- `CasaEngine/Framework/Scripting/IGameplayProxy.cs` garde encore une branche `#if EDITOR` pour `Save`.
- `CasaEngine/Framework/Game/GameplayExecutionPolicy.cs` est deja une bonne direction: il faut privilegier les policies/services runtime plutot que des branches compile-time.

## Architecture cible simple

### 1. CasaEngine = runtime only

Responsabilites autorisees:

- chargement et lecture des assets
- boucle de jeu runtime
- rendu, input, gameplay, physics
- contrats runtime neutres utilises aussi par l'editeur via adaptation
- persistance runtime legitime seulement si elle sert vraiment le jeu en execution

Interdits:

- reference directe a `GizmoTools`
- services de creation/suppression/renommage d'assets
- sauvegarde authoring de `World`, `Entity`, `Material`, `AssetInfo`, etc.
- etat de session editeur (`ProjectFileOpened`, selection editeur, layout, gizmo state)

### 2. CasaEngine.EditorServices = couche authoring/editor

Responsabilites autorisees:

- project authoring
- asset catalog mutable
- writers et serializers editor
- adaptation des objets runtime pour gizmos/selection
- etat de session editor utile a `CasaEngine.Editor`

Regle cle:

- si une fonctionnalite sert a creer, sauver, renommer, supprimer, reimporter, selectionner ou manipuler pour l'edition, elle doit vivre ici et non dans `CasaEngine`.

### 3. CasaEngine.Editor = composition root editor

Responsabilites autorisees:

- orchestration UI et panels
- binding avec `CasaEngine.EditorServices`
- integration `GizmoTools`
- commandes utilisateur editor

## Regles d'execution pour l'agent

- Traiter une seule tache a la fois.
- Faire exactement un commit par tache terminee.
- Mettre a jour l'icone de statut avant et apres la tache.
- Ne pas elargir le scope vers `CasaEngine.EditorUI`.
- Preferer des validations ciblees et bornees apres chaque tache.
- Si une tache bloque, passer son statut a `⛔` et decrire le blocage sous la tache.

## Legende des statuts

- ⬜ A faire
- 🟠 En cours
- ✅ Termine
- ⛔ Bloque

## Ordre recommande

Commencer par la direction des dependances, poursuivre avec l'authoring, puis supprimer les derniers `#if EDITOR` du code partage.

---

### ⬜ ARCH-EDIT-001 - Sortir les composants editor hors de CasaEngine

**Objectif**

Deplacer les composants purement editor hors du projet `CasaEngine` afin que le runtime partage ne porte plus de rendu/editor tooling.

**Fichiers / zones probables**

- `CasaEngine/Framework/Game/Components/Editor/GizmoComponent.cs`
- `CasaEngine/Framework/Game/Components/Editor/AxisComponent.cs`
- `CasaEngine/Framework/Game/Components/Editor/GridComponent.cs`
- tout helper directement lie a ces composants

**Resultat attendu**

- ces types vivent dans `CasaEngine.EditorServices` ou `CasaEngine.Editor`
- `CasaEngine` ne contient plus de namespace `Framework.Game.Components.Editor`
- `CasaEngine.Editor` continue a afficher gizmo/grille/axes

**Validation minimale**

- `dotnet build CasaEngine.Editor/CasaEngine.Editor.csproj -c Debug`

**Commit suggere**

- `Move editor render components out of CasaEngine`

---

### ✅ ARCH-EDIT-002 - Retirer GizmoTools du runtime partage

**Objectif**

Supprimer la reference directe a `GizmoTools` depuis `CasaEngine/CasaEngine.csproj` et faire reposer l'integration editor sur des adapters runtime-neutres.

**Fichiers / zones probables**

- `CasaEngine/CasaEngine.csproj`
- `CasaEngine/Framework/Transform/ITransformableObject.cs`
- references editor qui consomment encore directement des types runtime supposant `GizmoTools`

**Resultat attendu**

- `CasaEngine.csproj` ne reference plus `GizmoTools`
- le contrat runtime `ITransformableObject` reste la seule frontiere necessaire cote moteur
- `CasaEngine.Editor` garde la manipulation gizmo via adaptation cote editor

**Validation minimale**

- `dotnet build CasaEngine/CasaEngine.csproj -c Debug`
- `dotnet build CasaEngine.Editor/CasaEngine.Editor.csproj -c Debug`

**Commit suggere**

- `Remove GizmoTools dependency from shared runtime`

---

### ✅ ARCH-EDIT-003 - Extraire la selection et les mutations editor de World

**Objectif**

Sortir de `World` les APIs qui n'ont de sens qu'en edition: selection des objets manipulables et mutations speciales editor.

**Fichiers / zones probables**

- `CasaEngine/Framework/World/World.cs`
- `CasaEngine.Editor` classes qui appellent `GetSelectableComponents()`
- `CasaEngine.Editor` classes qui appellent `AddEntityWithEditor()` / `RemoveEntityWithEditor()`

**Resultat attendu**

- `World` garde seulement des operations runtime generiques
- une facade editor dediee gere selection, ajout/suppression authoring et sync des evenements
- le runtime ne connait plus le vocabulaire "WithEditor"

**Validation minimale**

- `dotnet build CasaEngine.Editor/CasaEngine.Editor.csproj -c Debug`

**Commit suggere**

- `Extract editor world facade from runtime world`

---

### ✅ ARCH-EDIT-004 - Scinder AssetCatalog entre lecture runtime et ecriture editor

**Objectif**

Faire de `AssetCatalog` un registre runtime en lecture, et deplacer les operations de mutation/sauvegarde dans `CasaEngine.EditorServices`.

**Fichiers / zones probables**

- `CasaEngine/Framework/Assets/AssetCatalog.cs`
- `CasaEngine.EditorServices/EditorAssetCatalogService.cs`
- `CasaEngine.Editor/*` consommateurs de renommage/suppression/save

**Resultat attendu**

- `AssetCatalog` expose seulement `Load`, `Get`, `GetByFileName`, `AssetInfos`, et eventuellement `IsLoaded`
- `EditorAssetCatalogService` devient le vrai owner de `Add`, `Remove`, `Rename`, `Save`, `Clear`, suppression disque et evenements editor
- le runtime ne declenche plus d'evenements de mutation authoring

**Validation minimale**

- `dotnet build CasaEngine.EditorServices/CasaEngine.EditorServices.csproj -c Debug`
- `dotnet build CasaEngine.Editor/CasaEngine.Editor.csproj -c Debug`

**Commit suggere**

- `Split runtime asset catalog from editor mutations`

---

### ✅ ARCH-EDIT-005 - Supprimer les hooks editor de AssetContentManager

**Objectif**

Faire de `AssetContentManager` un chargeur/cache runtime pur, sans abonnement aux mutations de l'asset catalog editor.

**Fichiers / zones probables**

- `CasaEngine/Framework/Assets/AssetContentManager.cs`
- eventuelle logique editor de refresh cache cote `CasaEngine.EditorServices`

**Resultat attendu**

- plus de `#if EDITOR` dans `AssetContentManager`
- plus d'abonnement runtime a `AssetCatalog.AssetRenamed`
- la logique de cache/refresh en edition est geree cote editor

**Validation minimale**

- `dotnet build CasaEngine/CasaEngine.csproj -c Debug`
- `dotnet build CasaEngine.Editor/CasaEngine.Editor.csproj -c Debug`

**Commit suggere**

- `Make asset content manager runtime-only`

---

### ✅ ARCH-EDIT-006 - Rendre ProjectSettings runtime-only et sortir la session editor

**Objectif**

Retirer l'etat et les branches editor de `ProjectSettingsHelper` et `ProjectSettings`.

**Fichiers / zones probables**

- `CasaEngine/Framework/Project/ProjectSettingsHelper.cs`
- `CasaEngine/Framework/Project/ProjectSettings.cs`
- `CasaEngine.EditorServices/EditorProjectAuthoringService.cs`
- nouveau type possible: `EditorProjectSession` ou equivalent

**Resultat attendu**

- `ProjectSettingsHelper` sert uniquement au chargement runtime d'un projet
- `ProjectFileOpened` quitte `ProjectSettings`
- `EditorProjectAuthoringService` ou une session editor dediee porte l'etat courant du projet ouvert
- plus de `#if EDITOR` dans `ProjectSettingsHelper`

**Validation minimale**

- `dotnet build CasaEngine.EditorServices/CasaEngine.EditorServices.csproj -c Debug`
- `dotnet build CasaEngine.Editor/CasaEngine.Editor.csproj -c Debug`

**Commit suggere**

- `Extract editor project session from runtime settings`

---

### ✅ ARCH-EDIT-007 - Rendre EditorAssetWriterService autonome

**Objectif**

Supprimer les fallbacks qui deleguent encore a `objectBase.Save(...)` et `materialBase.Save(...)` dans le runtime partage.

**Fichiers / zones probables**

- `CasaEngine.EditorServices/EditorAssetWriterService.cs`
- `CasaEngine.EditorServices/EditorAssetJsonSerializer.cs`
- serializers manquants pour materials/assets encore non couverts

**Resultat attendu**

- `EditorAssetWriterService` ne depend plus des methodes `Save` des types runtime
- les serializers editor couvrent explicitement les types encore utilises par l'editeur
- la direction de dependance devient claire: editor serialize, runtime charge

**Validation minimale**

- `dotnet build CasaEngine.EditorServices/CasaEngine.EditorServices.csproj -c Debug`

**Commit suggere**

- `Remove runtime save fallback from editor asset writer`

---

### ✅ ARCH-EDIT-008 - Sortir la persistance World/Entity/Component de CasaEngine

**Objectif**

Deplacer l'ecriture authoring de `World`, `Entity`, `EntityReference`, `EntityComponent`, `SceneComponent` et des composants derives dans `CasaEngine.EditorServices`.

**Fichiers / zones probables**

- `CasaEngine/Framework/World/World.cs`
- `CasaEngine/Framework/Entities/Entity.cs`
- `CasaEngine/Framework/Entities/EntityReference.cs`
- `CasaEngine/Framework/Entities/Components/**/*.cs`
- `CasaEngine.EditorServices/EditorWorldWriter.cs`
- `CasaEngine.EditorServices/EditorEntityWriter.cs`
- `CasaEngine.EditorServices/EditorEntityJsonSerializer.cs`

**Resultat attendu**

- les types runtime ci-dessus n'exposent plus de `Save(JObject)` pour l'authoring editor
- `EditorWorldWriter` / `EditorEntityWriter` deviennent la seule porte d'entree de sauvegarde editor pour ces objets
- les formats de fichiers existants restent compatibles

**Etat 2026-03-20**

- `World`, `Entity`, `EntityReference`, `EntityComponent` et les composants derives ne portent plus l'ecriture authoring runtime
- les surfaces publiques restantes jettent une exception explicite quand elles sont appelees hors de `CasaEngine.EditorServices`
- la sauvegarde editor passe par `EditorWorldWriter`, `EditorEntityWriter` et `EditorEntityJsonSerializer`

**Validation minimale**

- `dotnet build CasaEngine.EditorServices/CasaEngine.EditorServices.csproj -c Debug`
- `dotnet build CasaEngine.Editor/CasaEngine.Editor.csproj -c Debug`

**Commit suggere**

- `Move world and entity authoring persistence to editor services`

---

### ✅ ARCH-EDIT-009 - Sortir la persistance des assets et materials authoring de CasaEngine

**Objectif**

Deplacer hors du runtime partage les `Save(JObject)` utilises pour l'authoring des assets, des materials et des donnees graphiques, tout en laissant le chargement runtime en place.

**Fichiers / zones probables**

- `CasaEngine/Framework/Assets/**/*.cs`
- `CasaEngine/Framework/Materials/**/*.cs`
- `CasaEngine/Framework/Graphics/**/*.cs`
- `CasaEngine/Core/Serialization/JsonSaveExtensions.Editor.cs`
- `CasaEngine.EditorServices/EditorAssetJsonSerializer.cs`
- `CasaEngine.EditorServices/EditorJsonSaveHelper.cs`

**Resultat attendu**

- les types runtime ne portent plus les serializers editor authoring
- `JsonSaveExtensions.Editor.cs` n'est plus compile dans `CasaEngine`
- `EditorJsonSaveHelper` devient l'outil unique de save JSON cote editor

**Etat 2026-03-20**

- les `Save(JObject)` authoring de `Assets`, `Materials`, `PhysicsDefinition`, `Graphics` et `Shapes` ont ete retires du runtime partage
- `JsonSaveExtensions.Editor.cs` est exclu de `CasaEngine.csproj`
- `EditorJsonSaveHelper` et les serializers de `CasaEngine.EditorServices` portent desormais la sauvegarde JSON cote editor

**Validation minimale**

- `dotnet build CasaEngine/CasaEngine.csproj -c Debug`
- `dotnet build CasaEngine.EditorServices/CasaEngine.EditorServices.csproj -c Debug`

**Commit suggere**

- `Move asset and material authoring serializers out of runtime`

---

### ✅ ARCH-EDIT-010 - Nettoyer les contrats partages encore pollues par l'authoring editor

**Objectif**

Supprimer les derniers contrats editor caches dans le code partage, en particulier `IGameplayProxy.Save` et les APIs similaires qui n'ont pas de role runtime legitime.

**Fichiers / zones probables**

- `CasaEngine/Framework/Scripting/IGameplayProxy.cs`
- `CasaEngine/Framework/Scripting/GameplayProxy.cs`
- autres interfaces/modeles partagees encore conditionnees par `#if EDITOR`

**Resultat attendu**

- plus de branche `#if EDITOR` dans les contrats partages critiques
- si une persistance editor reste necessaire, elle passe par un serializer/editor adapter externe
- les contrats runtime restent stables et minimaux

**Etat 2026-03-20**

- `IGameplayProxy.Save` a ete retire du contrat partage
- les surfaces d'authoring restantes dans `Parser`, `Input`, `GameMode` et autres contrats partages ont ete retirees du runtime
- les contrats runtime restants n'ont plus besoin du symbole `EDITOR`

**Validation minimale**

- `dotnet build CasaEngine/CasaEngine.csproj -c Debug`
- `dotnet build CasaEngine.Editor/CasaEngine.Editor.csproj -c Debug`

**Commit suggere**

- `Remove editor persistence from shared runtime contracts`

---

### ✅ ARCH-EDIT-011 - Remplacer les derniers #if EDITOR du runtime par des services ou policies

**Objectif**

Eliminer les branches compile-time restantes dans le code partage quand elles representent un mode d'execution et non une vraie frontiere de projet.

**Fichiers / zones probables**

- `CasaEngine/Framework/Game/CasaEngineGame.cs`
- `CasaEngine/Framework/Project/ProjectSettingsHelper.cs`
- `CasaEngine/Framework/Assets/AssetContentManager.cs`
- autres classes runtime encore trouvees par `rg "#if EDITOR" CasaEngine`

**Resultat attendu**

- les differences editor preview vs runtime passent par `GameplayExecutionPolicy`, `EngineRuntimeContext` ou des services explicites
- `CasaEngine` compile et se comporte sans dependre du symbole `EDITOR`
- les `#if EDITOR` restants sont confines a des projets editor-only

**Etat 2026-03-20**

- les branches runtime sur `Entity`, `PhysicsBaseComponent`, `PhysicsEngineComponent` et `CasaEngineGame` ont ete remplacees par `GameplayExecutionPolicy`
- les anciens blocs `#if EDITOR` inactifs restants dans `CasaEngine` ont ete supprimes
- `rg "#if EDITOR" CasaEngine` ne retourne plus de branche active dans le runtime partage

**Validation minimale**

- `rg "#if EDITOR" CasaEngine`
- `dotnet build CasaEngine/CasaEngine.csproj -c Debug`

**Commit suggere**

- `Replace remaining runtime editor branches with explicit services`

---

### ✅ ARCH-EDIT-012 - Nettoyer les executables runtime et finaliser la suppression du mode compile-time

**Objectif**

Verifier que les executables runtime n'ont plus de conditionnels editor parasites et finaliser la suppression du symbole `EDITOR` la ou il n'est plus utile.

**Fichiers / zones probables**

- `CasaEngine.EditorServices/CasaEngine.EditorServices.csproj`
- `CasaEngine.Editor/CasaEngine.Editor.csproj`
- `Projects/SandBoxGame/SandBoxGame.cs`
- `CasaEngine.Demos/**/*`
- `CasaEngine.Launcher/**/*`

**Resultat attendu**

- `SandBoxGame` et les demos runtime ne contiennent plus de branche editor evitables
- `CasaEngine.EditorServices` ne definit plus `EDITOR` si plus rien ne l'exige
- l'architecture finale est: runtime neutre + services editor + host editor

**Etat 2026-03-20**

- `CasaEngine.Demos` et `CasaEngine.Launcher` buildent sans erreur contre le runtime nettoye
- `CasaEngine.EditorServices`, `CasaEngine.Editor` et `CasaEngine.SimpleEditor` ne definissent plus `EDITOR`
- les executables runtime ne referencent plus de couches editor dans leur configuration de build

**Validation minimale**

- `dotnet build CasaEngine.Launcher/CasaEngine.Launcher.csproj -c Debug`
- `dotnet build CasaEngine.Demos/CasaEngine.Demos.csproj -c Debug`
- `dotnet build CasaEngine.Editor/CasaEngine.Editor.csproj -c Debug`

**Commit suggere**

- `Finalize runtime-editor split and remove remaining editor compile flags`

---

## Notes pour l'agent

- Reutiliser `CasaEngine.EditorServices` comme projet de destination en priorite. C'est la solution la plus simple et la plus pragmatique a ce stade.
- Ne creer un nouveau projet editor authoring que si `CasaEngine.EditorServices` devient objectivement trop large ou cree une dependance cyclique impossible a resoudre proprement.
- Garder la compatibilite des formats `.world`, `.entity`, `AssetInfos.json` et des assets existants.
- Tant que `CasaEngine.EditorUI` est hors scope, ne pas ouvrir une sous-tache juste pour corriger sa compilation.
- Si une API `Save` sert a une vraie persistance runtime et pas a l'authoring editor, la conserver mais la renommer/documenter clairement pour ne plus la confondre avec la sauvegarde d'assets.
