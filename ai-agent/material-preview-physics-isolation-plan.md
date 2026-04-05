# Material Preview Physics Isolation — Plan d'action IA

## Règles obligatoires pour l'agent

1. Traiter une seule tâche à la fois.
2. Avant de commencer une tâche, remplacer son icône `⏳` par `🚧`.
3. À la fin de chaque tâche terminée, remplacer l'icône par `✅`.
4. Si une tâche nécessite une validation complémentaire non faite, utiliser `🧪`.
5. Si une tâche est bloquée, utiliser `⚠️` et ajouter une note courte juste dessous.
6. Mettre à jour ce fichier dans le même commit que le code de la tâche.
7. Faire un commit compilable après chaque tâche atomique.

## Validation minimale par tâche

- `dotnet build CasaEngine.Editor.MonoGame.sln -c Debug --no-restore`
- smoke test d'ouverture d'un material dans l'éditeur

## Tâches

- ✅ Cartographier les dépendances world -> physics runtime
  Objectif: identifier tous les composants qui résolvent `PhysicsEngineComponent` depuis `world.Game` et vérifier les hypothèses d'isolation multi-world.
  Fichiers ciblés:
  - `CasaEngine/Framework/Entities/Components/PhysicsBaseComponent.cs`
  - `CasaEngine/Framework/Game/Components/Physics/PhysicsEngineComponent.cs`
  - `CasaEngine/Framework/Game/CasaEngineGame.cs`
  Cartographie validée:
  - `CasaEngineGame.Initialize()` instancie un unique `PhysicsEngineComponent` par runtime, exposé aussi via `CasaEngineGame.PhysicsEngineComponent`.
  - `World` ne porte aujourd'hui aucun contexte physics propre: il ne stocke que `Game`, donc les composants world-scoped retombent sur le singleton du jeu.
  - Les résolutions directes depuis `world.Game` sont présentes dans `PhysicsBaseComponent`, `AnimatedSpriteComponent`, `StaticSpriteComponent`, `TileMapComponent` et `MovingObject`.
  - `PhysicsDebugViewRendererComponent` branche `DebugDrawer` sur le `PhysicsEngine.World` partagé puis dessine ce même monde après le pipeline multi-view, ce qui explique les fuites visuelles entre vues.
  - En mode éditeur, `HostedEditorGameAdapter` héberge simultanément le world principal, des `MaterialPreviewWorld` et un éventuel `EditorPreviewWorld`; sans contexte physics par world, ils partagent tous la même `DynamicsWorld`.
  Commit conseillé: `docs(physics): map editor multi-world physics dependencies`

- ⏳ Découpler la simulation physique du singleton de jeu
  Objectif: rattacher le contexte physics au world ou introduire une abstraction stable par world pour supprimer le partage de `DynamicsWorld` entre mondes de l'éditeur.
  Fichiers ciblés:
  - `CasaEngine/Framework/World/World.cs`
  - `CasaEngine/Framework/Game/Components/Physics/PhysicsEngineComponent.cs`
  - `CasaEngine/Framework/Entities/Components/PhysicsBaseComponent.cs`
  Commit conseillé: `refactor(physics): isolate physics runtime per world`

- ⏳ Rendre le debug physics scope par view ou par world
  Objectif: empêcher qu'une file globale de lignes debug soit flushée dans la mauvaise vue quand seul l'onglet material est visible.
  Fichiers ciblés:
  - `CasaEngine/Framework/Game/Components/Physics/PhysicsDebugViewRendererComponent.cs`
  - `CasaEngine/Framework/Game/Components/Physics/PhysicsDebugDrawComponent.cs`
  - `CasaEngine/Framework/Game/Components/Line3dRendererComponent.cs`
  - `CasaEngine/Framework/Rendering/RenderPipeline.cs`
  Commit conseillé: `fix(rendering): scope physics debug rendering to the owning view`

- ⏳ Valider l'isolation world principal / world material preview
  Objectif: vérifier qu'aucune forme debug physics du world principal n'apparaît dans l'onglet material preview.
  Fichiers ciblés:
  - `CasaEngine.Editor/Controls/WorldViewportPanel.cs`
  - `CasaEngine.Editor/Controls/MaterialPreviewViewport.cs`
  Commit conseillé: `test(editor): validate physics debug isolation for material preview`