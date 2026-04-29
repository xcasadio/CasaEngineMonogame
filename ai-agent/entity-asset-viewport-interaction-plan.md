# Entity Asset Viewport Interaction — Plan d'action IA

## Règles obligatoires pour l'agent

1. Traiter une seule tâche à la fois.
2. Avant de commencer une tâche, remplacer son icône `⏳` par `🚧`.
3. À la fin de chaque tâche terminée, remplacer l'icône par `✅`.
4. Si une tâche nécessite une validation complémentaire non faite, utiliser `🧪`.
5. Si une tâche est bloquée, utiliser `⚠️` et ajouter une note courte juste dessous.
6. Mettre à jour ce fichier dans le même commit que le code de la tâche.
7. Faire un commit compilable après chaque tâche atomique.

## Objectif

Étendre le document `.entity` pour que le viewport de preview devienne une vraie surface d'édition document-scoped, sans réintroduire les couplages du world editor.

## Contraintes d'architecture

- La source de vérité de la sélection doit rester `EntityAssetEditorPanel`, pas `WorldViewportPanel`.
- `Hierarchy`, `Inspector` et le viewport doivent tous se synchroniser via l'état du document actif.
- Aucun changement ne doit modifier la sélection globale du world principal quand un document `.entity` est actif.
- Le preview world d'un document `.entity` doit rester isolé du world principal pour le rendu, la physique et le gizmo state.
- Les commandes d'édition doivent rester rattachées à `EditorHistoryContextKind.Entity`.

## Validation minimale par tâche

- `dotnet build .\CasaEngine.Editor\CasaEngine.Editor.csproj -t:Compile -nologo -p:WarningLevel=0`
- `dotnet test .\CasaEngine.Tests\CasaEngine.Tests.csproj --filter "FullyQualifiedName~EntityAssetDocumentTests|FullyQualifiedName~EditorAssetWriterServiceTests"`
- smoke manuel: ouvrir `Entities/Box.entity`, vérifier synchro `Viewport` / `Hierarchy` / `Inspector`

## Tâches

- ✅ Baseline document `.entity` contextualisé
  Objectif: faire du document `.entity` un viewport de preview avec `Hierarchy` et `Inspector` dédiés au document actif.
  État actuel:
  - l'onglet document ouvre un `WorldViewportPanel` branché sur un `EntityAssetPreviewWorld` isolé.
  - `Hierarchy` affiche l'arbre des composants de l'entity active.
  - `Inspector` réutilise `EntityDetailsPanel` en mode inspecteur seul.
  - l'historique, le dirty state et la sauvegarde utilisent `EditorHistoryContextKind.Entity`.

- ✅ Relayer la sélection depuis le viewport de preview vers le document `.entity`
  Objectif: quand le viewport sélectionne l'entity de preview, publier cette sélection dans `EntityAssetEditorPanel` au lieu de modifier la sélection du world editor.
  Fichiers ciblés:
  - `CasaEngine.Editor/Controls/EntityAssetEditorPanel.cs`
  - `CasaEngine.Editor/Controls/WorldViewportPanel.cs`
  - `CasaEngine.Editor/GameEditor.cs`
  Notes d'implémentation:
  - ajouter un relais document-scoped `SelectedEntityChanged`/`SelectedComponentChanged` côté panel `.entity`.
  - ignorer toute propagation vers `EditorSelection.Current` quand le document actif est `Entity`.

- ✅ Ajouter un picking viewport minimal sans réactiver tout le mode world
  Objectif: un clic dans le viewport doit au minimum sélectionner l'entity racine preview et resynchroniser `Hierarchy` + `Inspector`.
  Fichiers ciblés:
  - `CasaEngine.Editor/Controls/WorldViewportPanel.cs`
  - `CasaEngine.Editor/Runtime/EditorViewportGizmoController.cs`
  Risques:
  - ne pas réutiliser directement les hypothèses du world editor sur la sélection multi-entity.
  - garder le comportement actuel des autres documents (`World`, `Material`, `AnimationClip`).

- ⏳ Réintroduire le gizmo de façon contrôlée pour les composants transformables
  Objectif: permettre la manipulation de la racine ou des `SceneComponent` du document `.entity` via le gizmo, avec undo/redo document-scoped.
  Fichiers ciblés:
  - `CasaEngine.Editor/Controls/EntityAssetEditorPanel.cs`
  - `CasaEngine.Editor/Controls/EntityDetailsPanel.cs`
  - `CasaEngine.Editor/Runtime/EditorViewportGizmoController.cs`
  - `CasaEngine.Editor/History/*`
  Notes d'implémentation:
  - limiter la V1 au root component et aux `SceneComponent` explicitement sélectionnés dans `Hierarchy`.
  - encapsuler les modifications de transform dans des commandes `EditorDelegateCommand` du contexte `Entity`.

- ⏳ Ajouter une automatisation smoke spécifique aux documents `.entity`
  Objectif: prouver qu'ouvrir `Entities/Box.entity` ne pollue pas le world principal et que la sélection reste document-scoped.
  Fichiers ciblés:
  - `CasaEngine.Editor/GameEditor.cs`
  - `ai-agent/entity-asset-open-smoke.txt`
  Vérifications attendues:
  - le document actif est `panel_entity_asset_*`.
  - le viewport du document utilise `EntityAssetPreviewWorld`.
  - la sélection du world principal n'est pas modifiée.

- ⏳ Étendre les tests de non-régression autour du contrat `.entity`
  Objectif: couvrir les points stables sans dépendre d'un test UI fragile.
  Fichiers ciblés:
  - `CasaEngine.Tests/Editor/EntityAssetDocumentTests.cs`
  - `CasaEngine.Tests/EditorServices/EditorAssetWriterServiceTests.cs`
  - éventuels tests d'intégration supplémentaires si un harness d'éditeur léger devient disponible.
  Couverture minimale:
  - mapping `EditorDocumentKind.Entity -> EditorHistoryContextKind.Entity`
  - sauvegarde d'un asset `Entity` avec `EditorAssetSaveSource.EntityAssetEditorPanel`
  - futur: persistance des transforms manipulés par gizmo dans le document `.entity`

## Critères d'acceptation

- Cliquer dans le viewport d'un document `.entity` met à jour `Hierarchy` et `Inspector` du même document.
- Le world viewport principal et sa sélection restent inchangés pendant l'édition d'un `.entity`.
- Les modifications de transform via gizmo sont annulables/rétablissables via le contexte `Entity`.
- Les builds `CasaEngine.Editor` et `CasaEngine.Editor.MonoGame.sln` restent verts.
- Les tests ciblés `.entity` restent verts.