# Plan agent IA - Navigation Engine V1

Source : `docs/navigation-engine-features.md`.

## Règles obligatoires

- Mettre à jour l'icone de statut devant chaque tâche au fil de l'exécution.
- Faire un commit après chaque tâche atomique validée.
- Ne pas intégrer `PathPlanner<T>` dans la V1 TileMap ; il reste legacy à réparer séparément.
- Ne pas appeler directement `CharacterControllerComponent.Move(...)` depuis la navigation V1.
- Utiliser `CharacterControllerNavigationDriverComponent` ou `CharacterControllerSteeringBridgeComponent` pour convertir la navigation en `SetMoveIntent(Vector2)`.
- Lancer au minimum les tests ciblés de la tâche avant chaque commit ; lancer un build solution avant la fin.

## Légende

- ✅ Done
- 🚧 In progress
- ⏳ Todo
- 🧪 Needs testing
- ⚠️ Blocked

## Tâches

### ✅ T0 - Créer le plan Navigation V1

Objectif : créer ce fichier de plan à partir de l'audit `navigation-engine-features.md`.

Validation :

- `get_errors docs/navigation-engine-tasks.md`
- Commit : `Add navigation engine V1 task plan`

### ✅ T1 - Ajouter les primitives Navigation V1

Objectif : ajouter les types purs nécessaires au noyau TileMap V1.

Livrables :

- `NavigationLayerMask`
- `NavigationAgentSettings`
- `NavigationQuery`
- `NavigationPath`
- types cellule/grille nécessaires à `NavigationGrid2D`

Validation :

- tests ciblés de compilation ou tests unitaires associés
- Commit : `Add navigation V1 primitives`

### ✅ T2 - Construire `NavigationGrid2D` depuis TileMap

Objectif : générer une grille de navigation depuis `TileMapData` et une couche `navigation.role=grid`.

Décisions :

- `TileMapLayerData.CustomProperties["navigation.role"] == "grid"` identifie la couche navigation.
- `TileMapLayerData.CustomProperties["navigation.defaultWalkable"]` définit le fallback walkable.
- `TileMapLayerData.CustomProperties["navigation.defaultCost"]` définit le coût fallback.
- `TileData.CustomProperties["navigation.walkable"]`, `navigation.cost`, `navigation.layers` priment sur `TileData.CollisionType`.

Validation :

- `NavigationGrid2D_BuildsWalkabilityFromNavigationLayer`
- `NavigationGrid2D_UsesTileNavigationPropertiesBeforeCollisionFallback`
- Commit : `Build navigation grid from tile maps`

### ✅ T3 - Implémenter `GridPathfinder2D`

Objectif : ajouter un pathfinder A* dédié grille, sans utiliser `PathPlanner<T>`.

Règles :

- supporter 4 directions et 8 directions selon `NavigationQuery` ;
- interdire le corner cutting diagonal ;
- prendre en compte les coûts de cellule ;
- retourner `false` si le départ ou l'arrivée est invalide ou inaccessible.

Validation :

- `GridPathfinder2D_PrefersLowerTotalCostOverShortestCellCount`
- `GridPathfinder2D_BlocksDiagonalCornerCutting`
- `GridPathfinder2D_ReturnsFalseWhenGoalIsUnreachable`
- Commit : `Add grid pathfinder for navigation V1`

### ✅ T4 - Intégrer la grille avec le driver CharacterController

Objectif : fournir un composant/système V1 qui calcule un chemin et l'envoie à `CharacterControllerNavigationDriverComponent.SetPath(...)`.

Contraintes :

- ne pas ajouter `MoveTo` au `CharacterControllerComponent` ;
- ne pas ajouter `CharacterControlMode.Navigation` ;
- convertir les waypoints vers le driver existant ;
- laisser `CharacterControllerSteeringBridgeComponent` pour le steering local existant.

Validation :

- `NavigationDriverIntegration_ConvertsPathToMoveIntent`
- tests `CharacterController|Navigation`
- Commit : `Integrate navigation paths with character controller driver`

### 🚧 T5 - Ajouter le debug draw Navigation V1

Objectif : ajouter un adaptateur debug navigation léger basé sur les renderers existants.

Livrables :

- debug draw 2D via `Renderer2DComponent` pour grille, cellules, chemin ;
- debug draw 3D via `Line3dRendererComponent` pour chemin/liens simples ;
- budget de primitives et culling minimal par rectangle visible pour la grille ;
- correction du clamp de `Line3dRendererComponent` si nécessaire pour éviter de dessiner plus de lignes qu'uploadées.

Validation :

- tests unitaires possibles sur le budget/culling sans GraphicsDevice ;
- build ciblé si tests graphiques non automatisables ;
- Commit : `Add navigation debug draw helpers`

### ⏳ T6 - Validation finale Navigation V1

Objectif : valider l'ensemble des tâches Navigation V1 et laisser le dépôt dans un état buildable.

Validation :

- `dotnet test .\CasaEngine.Tests\CasaEngine.Tests.csproj --filter "Navigation|TileMap|CharacterController"`
- `dotnet build .\CasaEngine.MonoGame.sln`
- mettre tous les statuts restants à jour
- Commit : `Finalize navigation engine V1 tasks`