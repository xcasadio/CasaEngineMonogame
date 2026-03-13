# Editor / Runtime Separation Refactor Tasks

Ce fichier découpe les actions du rapport d'audit en petites tâches committables une par une par un agent IA.

## Règles d'exécution pour l'agent

- Traiter une seule tâche à la fois.
- Créer exactement un commit par tâche terminée.
- Mettre à jour l'icône de statut de la tâche avant de passer à la suivante.
- Ne pas regrouper plusieurs tâches dans le même commit.
- Si une tâche est bloquée, laisser son contenu intact, passer son statut à `⛔` et expliquer le blocage directement dans la section de la tâche.
- Après chaque tâche, vérifier au minimum les fichiers touchés et confirmer qu'aucune régression évidente n'a été introduite.

## Légende des statuts

- ⬜ À faire
- 🟠 En cours
- ✅ Terminé
- ⛔ Bloqué

## Ordre recommandé

Commencer par les dépendances structurelles, poursuivre avec les services d'authoring, puis finir par le découplage du host éditeur et le nettoyage des projets.

## Tâches

### ✅ CASA-SEP-001 — Introduire une abstraction runtime de transform manipulable
**Objectif**  
Créer une abstraction appartenant à CasaEngine pour représenter les opérations de transformation nécessaires aux outils d'édition, sans dépendre de `GizmoTools`.

**Fichiers / classes concernés**  
- `CasaEngine/Framework/Entities/Components/SceneComponent.cs`
- nouvelle abstraction runtime dans `CasaEngine/Framework` ou `CasaEngine/Core`

**Critères d’acceptation**  
- le contrat de transformation utilisé par le runtime n'est plus importé depuis `GizmoTools`
- le nouveau contrat est défini dans une assembly runtime-safe

**Commit suggéré**  
`Introduce runtime transform contract for editor tools`

---

### ⬜ CASA-SEP-002 — Retirer `GizmoTools` de `SceneComponent`
**Objectif**  
Faire en sorte que `SceneComponent` implémente uniquement le contrat runtime introduit à la tâche précédente.

**Fichiers / classes concernés**  
- `CasaEngine/Framework/Entities/Components/SceneComponent.cs`

**Critères d’acceptation**  
- `SceneComponent` ne référence plus `GizmoTools`
- le comportement de transformation côté runtime reste inchangé

**Dépendances éventuelles**  
- CASA-SEP-001

**Commit suggéré**  
`Remove GizmoTools dependency from SceneComponent`

---

### ⬜ CASA-SEP-003 — Retirer `GizmoTools` de `World` et isoler la sélection editor
**Objectif**  
Supprimer la fuite des concepts de sélection editor dans `World` et déplacer ce qui est propre à l'éditeur derrière un adapter ou un service dédié.

**Fichiers / classes concernés**  
- `CasaEngine/Framework/World/World.cs`
- classes editor consommant les éléments sélectionnables

**Critères d’acceptation**  
- `World` ne référence plus `GizmoTools`
- la sélection d'objets reste possible depuis l'éditeur via une couche dédiée

**Dépendances éventuelles**  
- CASA-SEP-001

**Commit suggéré**  
`Move world selection integration out of runtime layer`

---

### ⬜ CASA-SEP-004 — Ajouter une façade editor pour la manipulation gizmo
**Objectif**  
Créer côté éditeur un point d'entrée clair qui adapte les objets runtime au système de gizmo, au lieu de brancher `GizmoTools` directement sur les types runtime.

**Fichiers / classes concernés**  
- `CasaEngine.EditorUI`
- `CasaEngine.Editor`
- `CasaEngine/Framework/Game/Components/Editor/GizmoComponent.cs`

**Critères d’acceptation**  
- l'intégration gizmo passe par une couche editor dédiée
- les types runtime ne dépendent plus directement de l'API gizmo

**Dépendances éventuelles**  
- CASA-SEP-002
- CASA-SEP-003

**Commit suggéré**  
`Add editor gizmo adapter layer`

---

### ⬜ CASA-SEP-005 — Scinder `AssetCatalog` en lecture runtime et écriture editor
**Objectif**  
Conserver en runtime un catalogue de lookup en lecture seule et sortir les opérations de mutation dans une façade editor.

**Fichiers / classes concernés**  
- `CasaEngine/Framework/Assets/AssetCatalog.cs`
- nouveaux services côté éditeur

**Critères d’acceptation**  
- les méthodes de lecture restent accessibles au runtime
- les opérations `Add`, `Remove`, `Rename`, `Save`, `Clear` ne sont plus portées par le même point d'entrée runtime

**Commit suggéré**  
`Split runtime asset catalog from editor mutations`

---

### ⬜ CASA-SEP-006 — Migrer les événements de mutation d'assets vers le service editor
**Objectif**  
Déplacer les événements `AssetAdded`, `AssetRemoved`, `AssetRenamed`, `AssetCleared` dans la couche editor pour que le runtime ne porte plus d'orchestration d'authoring.

**Fichiers / classes concernés**  
- `CasaEngine/Framework/Assets/AssetCatalog.cs`
- contrôles du content browser dans `CasaEngine.EditorUI`

**Critères d’acceptation**  
- les événements editor ne sont plus exposés par le catalogue runtime
- le content browser reste synchronisé lors d'ajout, suppression et renommage

**Dépendances éventuelles**  
- CASA-SEP-005

**Commit suggéré**  
`Move asset catalog mutation events to editor service`

---

### ⬜ CASA-SEP-007 — Extraire un service editor d'écriture d'assets
**Objectif**  
Déplacer `AssetSaver` et les helpers d'écriture associés hors de l'assembly runtime partagée.

**Fichiers / classes concernés**  
- `CasaEngine/Framework/Assets/AssetSaver.cs`
- nouvelle classe de service côté éditeur

**Critères d’acceptation**  
- le runtime partagé ne contient plus le service d'écriture d'assets
- l'éditeur peut toujours sauvegarder un asset simple via le nouveau service

**Commit suggéré**  
`Extract editor asset writer service`

---

### ⬜ CASA-SEP-008 — Migrer les écrans editor qui sauvegardent des assets vers le nouveau service
**Objectif**  
Remplacer les appels directs à `AssetSaver` dans les contrôles editor par le service d'écriture introduit précédemment.

**Fichiers / classes concernés**  
- `CasaEngine.EditorUI/Controls/ContentBrowser/ContentBrowserControl.xaml.cs`
- `CasaEngine.EditorUI/Controls/WorldControls/WorldEditorControl.xaml.cs`
- autres contrôles editor appelant `AssetSaver`

**Critères d’acceptation**  
- les contrôles editor n'appellent plus directement le writer runtime historique
- les opérations de sauvegarde principales fonctionnent toujours

**Dépendances éventuelles**  
- CASA-SEP-007

**Commit suggéré**  
`Migrate editor save flows to asset writer service`

---

### ⬜ CASA-SEP-009 — Extraire un service d'authoring de projet hors de `ProjectSettingsHelper`
**Objectif**  
Laisser `ProjectSettingsHelper` gérer uniquement le chargement runtime d'un projet existant et déplacer `CreateProject`, `Save`, `Clear` et les événements editor vers un service dédié.

**Fichiers / classes concernés**  
- `CasaEngine/Framework/Project/ProjectSettingsHelper.cs`
- `CasaEngine.EditorUI/MainWindow.xaml.cs`
- `CasaEngine.EditorUI/ProjectLauncherWindow.xaml.cs`
- `CasaEngine.Editor/ProjectLauncher/ProjectLauncherWindow.cs`

**Critères d’acceptation**  
- `ProjectSettingsHelper` ne porte plus les flux d'authoring editor
- l'éditeur peut toujours créer et ouvrir un projet

**Commit suggéré**  
`Extract editor project authoring service`

---

### ⬜ CASA-SEP-010 — Introduire des writers editor pour `World` et `Entity`
**Objectif**  
Commencer la séparation de la persistance editor en déplaçant l'écriture de `World` et `Entity` vers des writers dédiés.

**Fichiers / classes concernés**  
- `CasaEngine/Framework/World/World.cs`
- `CasaEngine/Framework/Entities/Entity.cs`
- nouvelles classes writer côté éditeur

**Critères d’acceptation**  
- `World` et `Entity` ne sont plus la source principale de l'écriture editor
- un writer editor dédié produit toujours le format attendu

**Commit suggéré**  
`Add editor writers for world and entity persistence`

---

### ⬜ CASA-SEP-011 — Retirer `Save` de `ISerializable`
**Objectif**  
Faire de `ISerializable` un contrat de chargement runtime seulement, sans branche `#if EDITOR`.

**Fichiers / classes concernés**  
- `CasaEngine/Core/Serialization/ISerializable.cs`
- appelants dépendant encore de `Save` sur l'interface

**Critères d’acceptation**  
- `ISerializable` n'expose plus `Save`
- les chemins editor compilent en s'appuyant sur des writers dédiés

**Dépendances éventuelles**  
- CASA-SEP-010

**Commit suggéré**  
`Remove editor save contract from ISerializable`

---

### ⬜ CASA-SEP-012 — Déplacer les helpers `JsonHelper.Save` dans la couche editor
**Objectif**  
Sortir les helpers d'écriture JSON de l'assembly runtime pour finaliser la séparation des utilitaires de persistance.

**Fichiers / classes concernés**  
- `CasaEngine/Core/Serialization/JsonHelper.cs`
- nouvelles classes utilitaires côté éditeur

**Critères d’acceptation**  
- les helpers `Save` ne vivent plus dans le helper runtime partagé
- le code editor qui écrit du JSON passe par les nouveaux helpers

**Dépendances éventuelles**  
- CASA-SEP-011

**Commit suggéré**  
`Move JSON save helpers out of runtime assembly`

---

### ⬜ CASA-SEP-013 — Remplacer les wrappers editor de `CasaEngineGame` par un adapter de host
**Objectif**  
Sortir `InitializeWithEditor`, `LoadContentWithEditor`, `UpdateWithEditor`, `DrawWithEditor` et la gestion directe du mode editor de l'API publique de `CasaEngineGame`.

**Fichiers / classes concernés**  
- `CasaEngine/Framework/Game/CasaEngineGame.cs`
- `CasaEngine.EditorUI/Controls/EngineHost.cs`

**Critères d’acceptation**  
- `CasaEngineGame` n'expose plus de wrappers spécifiques à l'éditeur
- `EngineHost` orchestre le cycle editor via un adapter ou service dédié

**Commit suggéré**  
`Move editor host lifecycle out of CasaEngineGame`

---

### ⬜ CASA-SEP-014 — Remplacer `IsRunningInGameEditorMode` par une policy explicite
**Objectif**  
Remplacer les branches de comportement gameplay dépendant du mode editor par une policy ou des hooks d'hébergement explicites.

**Fichiers / classes concernés**  
- `CasaEngine/Framework/World/World.cs`
- `CasaEngine/Framework/Entities/Entity.cs`
- composants dépendant du flag de mode editor

**Critères d’acceptation**  
- le cycle de vie runtime ne dépend plus d'un flag editor global dans les types métier principaux
- le preview editor reste possible via une policy dédiée

**Dépendances éventuelles**  
- CASA-SEP-013

**Commit suggéré**  
`Extract editor lifecycle policy from runtime domain`

---

### ⬜ CASA-SEP-015 — Déplacer les overlays et pipelines editor hors du namespace runtime partagé
**Objectif**  
Relocaliser `GridComponent`, `AxisComponent`, `GizmoComponent`, `EditorViewPipeline`, `PreviewPipeline` et les types associés vers une couche editor, ou reclassifier explicitement ce qui relève du debug runtime.

**Fichiers / classes concernés**  
- `CasaEngine/Framework/Game/Components/Editor/*`
- `CasaEngine/Framework/Rendering/EditorViewPipeline.cs`
- `CasaEngine/Framework/Rendering/PreviewPipeline.cs`
- démos runtime qui consomment ces types

**Critères d’acceptation**  
- les concepts nommés editor ne sont plus consommés directement depuis les apps runtime
- les démos runtime utilisent soit des overlays debug runtime, soit aucun type editor

**Commit suggéré**  
`Move editor overlays and pipelines out of runtime layer`

---

### ⬜ CASA-SEP-016 — Repointer les projets editor hors de `CasaEngine.WithEditor`
**Objectif**  
Faire dépendre les projets editor d'un noyau runtime propre et d'extensions editor explicites, au lieu d'une variante mixte du moteur.

**Fichiers / classes concernés**  
- `CasaEngine.Editor/CasaEngine.Editor.csproj`
- `CasaEngine.EditorUI/CasaEngine.EditorUI.csproj`
- `CasaEngine.WpfControls/CasaEngine.WpfControls.csproj`
- nouveaux projets d'extensions editor si nécessaires

**Critères d’acceptation**  
- les frontends editor ne référencent plus `CasaEngine.WithEditor.csproj`
- les dépendances editor sont explicites dans la graph de projets

**Dépendances éventuelles**  
- CASA-SEP-015

**Commit suggéré**  
`Point editor frontends to explicit editor extensions`

---

### ⬜ CASA-SEP-017 — Supprimer `CasaEngine.WithEditor` comme mécanisme de séparation
**Objectif**  
Finaliser le découplage en retirant la variante de build mixte comme solution architecturale de séparation runtime/editor.

**Fichiers / classes concernés**  
- `CasaEngine/CasaEngine.WithEditor.csproj`
- solution et références associées

**Critères d’acceptation**  
- la séparation runtime/editor est exprimée par les assemblies et services, pas par une double compilation du même cœur
- la suppression de `CasaEngine.WithEditor` ne casse pas les projets editor

**Dépendances éventuelles**  
- CASA-SEP-016

**Commit suggéré**  
`Retire mixed editor build variant`
