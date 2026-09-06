# Navigation des personnages dans un moteur moderne

## Objectif

Ce document décrit comment les moteurs modernes gèrent la navigation des personnages dans :

- un monde 3D ;
- un monde 2D top-down ;
- un monde 2D basé sur une TileMap ;
- un monde 2D platformer.

L’objectif est aussi de définir une progression d’implémentation en trois niveaux :

- **V1 : noyau simple, testable et utilisable rapidement** ;
- **V2 : système moteur moderne exploitable en jeu réel** ;
- **V3 : système avancé proche des moteurs professionnels**.

---

# 0. Audit CasaEngine avant plan d'implémentation

Cette section doit être traitée avant de transformer ce document en plan agent. Elle distingue le panorama théorique du système réellement présent dans CasaEngine.

## 0.1 Conclusion de l'audit

### Critiques bloquantes

Ces critiques décrivent l'état initial du document avant correction. Les sections suivantes appliquent les décisions retenues pour rendre la V1 compatible avec CasaEngine.

1. Le document initial supposait un branchement direct du `CharacterController` qui ne correspondait pas au contrat CasaEngine actuel.

    Le pseudo-code V1 appelait `controller.Move(nav.DesiredVelocity, deltaTime)`. Dans CasaEngine, le contrat principal du contrôleur reste l'intention via `SetMoveIntent(Vector2)`, puis le déplacement est appliqué dans `Update`. Le contrôleur expose aussi maintenant `Move(Vector3 requestedDisplacement)` pour un déplacement externe bas niveau, notamment root motion, mais ce n'est pas un `MoveTo`, ni un `Move(desiredVelocity, deltaTime)`.

    Conséquence : `DesiredVelocity` en `Vector3` ne doit pas être branché tel quel sur le controller sans adaptateur. Le chemin correct retenu est un driver haut niveau qui convertit destination, waypoint ou vitesse souhaitée en intention compatible avec `SetMoveIntent(Vector2)`.

2. Le `CharacterControlMode` proposé ne correspond pas à l'existant.

    Le document proposait `Player`, `AI`, `Navigation`, `Cutscene`, `Disabled`. Le code actuel contient `Player`, `AI`, `Script`, `Cutscene`, `Disabled` dans `CharacterControlMode`.

    Conséquence : ajouter un mode `Navigation` serait un changement d'API à justifier. Par défaut, la navigation doit utiliser `AI` ou un driver dédié au-dessus du controller.

3. Le document initial ignorait des briques IA/navigation déjà présentes.

    Le document proposait une V1 autour de nouveaux types `NavigationGrid2D`, `NavigationSystem`, `NavigationAgentComponent`. Le dépôt contient déjà `SteeringAgentComponent`, `FollowPathSteeringBehaviorRuntime`, des comportements de steering/avoidance et `WorldSpatialServices` branché au `World`.

    Conséquence : le plan agent doit commencer par auditer, réutiliser ou isoler ces briques. Sans cette étape, il risque de créer un second système parallèle.

4. Le pathfinding existant doit être audité avant d'être repris.

    Le dépôt contient déjà `AStarSearch`. Mais `PathPlanner` contient des points suspects constatés dans le code : deux méthodes initialisent la recherche avec le même noeud source/cible, et `NodesToPositions` lit `graph.GetNode(i)` au lieu du noeud listé.

    Conséquence : le document ne doit pas simplement dire "A* V1". Il faut choisir explicitement entre réparer/tester le pathfinding existant ou créer un nouveau noyau A* dédié à la navigation grille.

5. Le pseudo-code V1 initial était incohérent avec sa propre classe proposée.

    Le pseudo-code appelait `agent.Query`, mais la classe `NavigationAgentComponent` proposée ne déclarait pas `Query`. Autre souci : après avoir atteint un waypoint, le code incrémentait l'index puis normalisait encore l'ancien `toTarget`. Si `toTarget` valait zéro, c'était une source possible de vecteur invalide.

    Conséquence : le pseudo-code V1 a été corrigé pour déclarer la requête et déléguer le suivi de chemin au driver existant.

### Points corrigés avant plan

1. Le passage "V1 pour CasaEngine" remplace `DebugRenderer` par les APIs réelles : `Renderer2DComponent` et `Line3dRendererComponent`.

2. La partie TileMap est compatible dans l'idée, mais était trop vague pour une implémentation. Les données existantes ont `TileMapData`, `TileMapLayerData.CustomProperties`, `TileData.CollisionType` et `TileData.CustomProperties`. Le stockage V1 est maintenant fixé sur une couche TileMap explicite et des propriétés `navigation.*`.

### Ce qui est OK

- Le principe de séparation navigation / déplacement / animation est sain.
- La priorité V1 sur TileMap + grille + tests de pathfinding est raisonnable.
- L'exclusion de NavMesh 3D, crowd simulation, streaming et async jobs de la V1 est cohérente.

Conclusion : les écarts ci-dessus étaient factuels. Le document référence maintenant les APIs existantes pour la V1 ; il peut servir de base à un plan agent avec statuts `⏳` / `🚧` / `✅` / `🧪` / `⚠️` et obligation de commit à chaque tâche.

## 0.2 Analyse des prérequis CasaEngine

### Résultat de vérification disponible

Commande lancée après audit :

```text
dotnet test .\CasaEngine.Tests\CasaEngine.Tests.csproj --filter "CharacterController|Navigation|TileMap"
```

Résultat constaté : 84 tests, 0 échec. Les avertissements de build restants concernent surtout des annotations nullable existantes et des warnings projet hors de cette tranche.

### Prérequis existants à réutiliser

| Prérequis | État dans CasaEngine | Vérification actuelle | Décision avant plan |
|---|---|---|---|
| CharacterController bas niveau | Existe avec `SetMoveIntent(Vector2)`, saut, dash, step, moving ground, root motion bridge, snapshots replay et bridge ragdoll. | Couvert par les tests `CharacterController` inclus dans la commande ci-dessus. | Réutiliser. Ne pas ajouter `MoveTo` au controller. |
| Driver navigation vers controller | Existe via `CharacterControllerNavigationDriverComponent` et `CharacterControllerSteeringBridgeComponent`. | Couvert par les tests `CharacterController|Navigation`. | Réutiliser comme point d'intégration IA/navigation. |
| Steering / avoidance local | Existe via `SteeringAgentComponent`, comportements de steering, `FollowPathSteeringBehaviorRuntime` et services spatiaux. | Des tests existent pour le bridge controller/navigation ; pas de validation complète de tous les comportements de steering dans cet audit. | Auditer avant de dupliquer. Ajouter tests ciblés si une tâche V1/V2 s'appuie dessus. |
| Services spatiaux monde | Existe via `WorldSpatialServices`, avec index monde et index steering. | Pas de test spécifique lancé ici hors intégration navigation/controller. | Réutiliser pour requêtes agents/obstacles si les besoins correspondent. |
| TileMap runtime | Existe via `TileMapData`, `TileMapLayerData`, `TileData`, `TileMapComponent`. | Tests `TileMap` inclus dans la commande ci-dessus. | Réutiliser les données existantes, mais définir explicitement le stockage nav. |
| Debug draw 2D/3D | Existe via `Renderer2DComponent` et `Line3dRendererComponent`. | Build et tests passent, mais pas de test visuel navigation. | Remplacer `DebugRenderer` par ces APIs dans tout plan. |

### Prérequis existants mais à auditer avant usage

| Prérequis | État dans CasaEngine | Risque constaté | Action requise |
|---|---|---|---|
| A* générique | `AStarSearch<T, TK>` existe. | Aucun test pathfinding dédié trouvé dans `CasaEngine.Tests`. | Ajouter tests A* minimaux avant réutilisation. |
| `PathPlanner<T>` | Existe. | Points suspects : initialisation source/cible identique dans deux méthodes ; conversion nodes -> positions utilisant l'index de boucle au lieu du noeud listé. | Ne pas utiliser pour la V1 ; réparer dans une tâche legacy séparée. |
| `PathManager<T>` | Existe. | Gestion globale statique par type, à vérifier pour multi-world/editor/runtime. | Tester isolation et budget update avant usage runtime. |

### Prérequis absents ou incomplets

| Prérequis | État actuel | Décision |
|---|---|---|
| `NavigationGrid2D` | Non constaté comme type moteur existant. | À créer pour la V1 depuis une couche TileMap explicite `navigation.role=grid`. |
| `NavigationSystem` dédié | Non constaté comme système moteur existant. | À créer en cohérence avec `World.Update`, `WorldSpatialServices` et les drivers controller existants. |
| `NavigationAgentComponent` générique | Non constaté comme type distinct ; il existe déjà `SteeringAgentComponent` et un driver controller/navigation. | Ne pas créer avant audit de réutilisation ou décision d'isolation. |
| Données TileMap de navigation | Pas de schéma moteur historique constaté pour walkable/cost/layers. | Schéma V1 retenu : couche dédiée via `TileMapLayerData.CustomProperties` + propriétés `navigation.*` sur `TileData.CustomProperties`. |
| Off-mesh links | Pas de type moteur navigation dédié constaté. | À reporter après V1 grille testée. |
| NavMesh 3D | Pas de système NavMesh runtime constaté. | Hors V1 ; V2/V3 seulement après noyau 2D stable. |
| Crowd simulation | Pas de système crowd constaté. | Hors V1/V2 initiale. |

### Préconditions minimales avant un plan agent Navigation V1

1. ✅ Corriger ce document pour remplacer les exemples incompatibles avec le contrat actuel du controller.
2. ✅ Décider le stockage des données de navigation TileMap.
3. ✅ Auditer ou remplacer explicitement `AStarSearch` / `PathPlanner`.
4. ✅ Définir l'intégration avec `CharacterControllerNavigationDriverComponent` au lieu d'un appel direct au controller.
5. ✅ Prévoir des tests unitaires pour grille, coûts, diagonales, chemin impossible, et conversion chemin -> intention controller.
6. ✅ Prévoir un debug draw basé sur `Renderer2DComponent` / `Line3dRendererComponent` et analyser leurs performances.

### Décisions appliquées pour les préconditions

#### Stockage des données Navigation TileMap

Décision V1 : ne pas créer immédiatement un nouveau format d'asset navigation. La grille de navigation est construite depuis `TileMapData` en utilisant les mécanismes existants :

- `TileMapLayerData.CustomProperties` marque une couche comme couche de navigation explicite ;
- les cellules de cette couche donnent le tile de navigation ;
- `TileData.CustomProperties` porte les propriétés par type de tile ;
- `TileData.CollisionType` sert seulement de fallback quand aucune propriété navigation explicite n'existe.

Schéma de propriétés retenu pour la V1 :

| Niveau | Propriété | Rôle |
|---|---|---|
| `TileMapLayerData.CustomProperties` | `navigation.role=grid` | Identifie la couche utilisée pour générer `NavigationGrid2D`. |
| `TileMapLayerData.CustomProperties` | `navigation.defaultWalkable=false` | Valeur par défaut pour les cellules vides ou sans metadata. |
| `TileMapLayerData.CustomProperties` | `navigation.defaultCost=1` | Coût par défaut si le tile ne définit pas de coût. |
| `TileData.CustomProperties` | `navigation.walkable=true/false` | Autorise ou interdit le passage sur ce tile. |
| `TileData.CustomProperties` | `navigation.cost=1` | Coût de traversée du tile. |
| `TileData.CustomProperties` | `navigation.layers=Ground,Water` | Masques d'agents autorisés. |

Règle importante : la V1 ne déduit pas la navigation depuis le nom ou l'apparence des tiles. La couche de navigation est explicite. Si aucune couche `navigation.role=grid` n'est trouvée, la génération de grille doit échouer proprement ou demander un fallback explicite au caller.

#### Audit `AStarSearch` / `PathPlanner`

Décision V1 : ne pas utiliser `PathPlanner<T>` pour `NavigationGrid2D`. La V1 doit créer un pathfinder de grille dédié, par exemple `GridPathfinder2D`, avec tests unitaires dès l'introduction. `AStarSearch<T, TK>` peut rester comme brique legacy générique, mais il ne doit pas être le coeur de la V1 TileMap sans tests dédiés.

Constats factuels :

- `AStarSearch<T, TK>` implémente bien une recherche A* sur `Graph<T, TK>`, mais alloue des listes à chaque `Initialize`, reconstruit le chemin avec `Insert(0, ...)`, et n'a pas de test pathfinding dédié trouvé dans `CasaEngine.Tests`.
- `PathPlanner<T>.GetNowPathOfPositionsToPosition` et `GetNowPathOfEdgesToPosition` initialisent la recherche avec `closestNodeToEntity` comme source et cible, au lieu d'utiliser `closestNodeTodestination`.
- `PathPlanner<T>.NodesToPositions` parcourt les noeuds trouvés mais lit `graph.GetNode(i)` au lieu de `graph.GetNode(nodes[i])`.
- `PathPlanner<T>.ClosestNodeToPosition` retourne l'index local dans la liste des voisins, pas l'index réel du noeud. Aujourd'hui `NavigationNode.IsNeighbour` retourne toujours `true`, ce qui masque en partie le problème, mais le code devient faux dès qu'un filtrage réel est ajouté.
- `PathManager<T>.UpdateWithTime` utilise `DateTime.Today.Ticks`, qui ne mesure pas le temps écoulé dans la frame. Un budget temps basé sur cette valeur n'est pas fiable.

Conséquence : `PathPlanner<T>` doit être traité comme legacy à réparer dans une tâche séparée. La V1 navigation TileMap doit partir sur un pathfinder grille isolé et testé.

#### Intégration CharacterController retenue

Décision V1 : la navigation ne pilote pas `CharacterControllerComponent` par `Move(...)`. Elle produit un chemin, puis l'envoie à `CharacterControllerNavigationDriverComponent`.

Chaîne retenue :

```text
NavigationGrid2D
    -> GridPathfinder2D
    -> NavigationPath
    -> CharacterControllerNavigationDriverComponent.SetPath(...)
    -> CharacterControllerComponent.SetMoveIntent(Vector2)
    -> CharacterControllerComponent.Update(...)
```

`CharacterControllerSteeringBridgeComponent` reste l'intégration à utiliser quand la source du mouvement est `SteeringAgentComponent` plutôt qu'un chemin V1 grille.

#### Tests unitaires V1 à prévoir

Les tests à créer avec l'implémentation V1 doivent couvrir au minimum :

| Test | Attendu |
|---|---|
| `NavigationGrid2D_BuildsWalkabilityFromNavigationLayer` | Une couche `navigation.role=grid` produit les bonnes cellules walkable/bloquées. |
| `NavigationGrid2D_UsesTileNavigationPropertiesBeforeCollisionFallback` | `navigation.walkable` et `navigation.cost` priment sur `CollisionType`. |
| `GridPathfinder2D_PrefersLowerTotalCostOverShortestCellCount` | Le coût influence réellement le chemin. |
| `GridPathfinder2D_BlocksDiagonalCornerCutting` | Une diagonale entre deux obstacles orthogonaux est refusée. |
| `GridPathfinder2D_ReturnsFalseWhenGoalIsUnreachable` | Une destination isolée retourne un échec propre, sans chemin partiel ambigu. |
| `NavigationDriverIntegration_ConvertsPathToMoveIntent` | Un chemin calculé alimente le driver et produit une intention `Vector2` cohérente pour le controller. |

#### Debug draw et audit renderer

Décision V1 : le debug draw navigation doit être un adaptateur fin au-dessus des renderers existants, pas un nouveau renderer généraliste.

```text
NavigationDebugDraw2D
    -> Renderer2DComponent.DrawLine / DrawRectangle / DrawText limité

NavigationDebugDraw3D
    -> Line3dRendererComponent.AddLine / DrawRectangle limité
```

Analyse `Renderer2DComponent` :

- Points solides : accumulation par listes, flush par frame, un `SpriteBatch.Begin/End`, données de sprites/textes/lignes majoritairement en structs, donc adapté à du debug 2D léger.
- Points à surveiller : capacité initiale faible pour une grille complète, scissor stocké mais non appliqué dans les chemins actuels, rotation de sprite/texte passée aux méthodes mais non utilisée dans le draw final, lignes 2D dessinées via texture 1x1 avec `atan2` et longueur par segment.
- Décision perf : utilisable pour chemins, rectangles de cellules visibles et overlays légers. Pour une TileMap dense, le debug doit culler par viewport, limiter le texte, et éviter de redessiner toute la grille si ce n'est pas demandé.

Analyse `Line3dRendererComponent` :

- Points solides : vertex buffer réutilisé, batch unique en `LineList`, statistiques de flush, pool de lignes après échauffement.
- Points à surveiller : limite interne de 5000 lignes, mais le draw utilise le nombre demandé plutôt que le nombre clampé uploadé ; au-delà de la capacité, le comportement doit être corrigé avant un debug 3D dense. Le composant change aussi `DepthStencilState`, `RasterizerState`, `BlendState` sans restauration locale.
- Décision perf : suffisant pour chemins, volumes simples, liens et quelques contours de NavMesh. Pas suffisant tel quel pour afficher une NavMesh 3D dense ou des milliers de cellules sans budget, culling et correction du clamp.

---

# 1. Principe général

Dans un moteur moderne, il faut séparer clairement :

```text
Navigation
    -> Trouver par où passer

Déplacement / CharacterController
    -> Appliquer le mouvement réel avec collision, pente, gravité, step, etc.

Animation
    -> Jouer les animations adaptées au mouvement
```

La navigation ne devrait pas déplacer directement le personnage.

Elle devrait produire une intention :

```csharp
Vector3 DesiredVelocity;
Vector3 NextWaypoint;
bool HasPath;
bool ReachedDestination;
bool IsUsingSpecialLink;
```

Puis le `CharacterController` applique réellement le déplacement :

```csharp
MoveWithCollision(desiredVelocity);
ApplyGravity();
HandleSlope();
HandleStep();
UpdateGroundedState();
```

Enfin, le système d’animation réagit :

```csharp
SetFloat("Speed", currentSpeed);
SetFloat("Direction", movementDirection);
SetBool("IsMoving", isMoving);
SetBool("IsJumping", isJumping);
```

---

# 2. Architecture générale d’un système de navigation

Une architecture propre peut être représentée comme ceci :

```text
AI / Gameplay
    ↓
NavigationAgentComponent
    ↓
NavigationSystem
    ↓
NavigationMap / NavMesh / NavigationGrid
    ↓
Pathfinding
    ↓
Path smoothing / Corridor
    ↓
Local avoidance / Steering
    ↓
DesiredVelocity
    ↓
CharacterController
    ↓
AnimationController
```

## Responsabilités

| Système | Responsabilité |
|---|---|
| AI / Gameplay | Décide où aller |
| NavigationAgentComponent | Stocke la destination, le chemin courant et l’état de navigation |
| NavigationSystem | Calcule et met à jour les chemins |
| NavigationMap | Représente les zones navigables |
| Pathfinding | Trouve un chemin entre deux points |
| Steering | Convertit un chemin en direction/vitesse souhaitée |
| Local avoidance | Corrige la trajectoire pour éviter agents et obstacles dynamiques |
| CharacterController | Applique le déplacement réel |
| AnimationController | Synchronise l’animation avec le mouvement |

---

# 3. Navigation en monde 3D

## 3.1 Représentation principale : la NavMesh

En 3D, les moteurs modernes utilisent majoritairement une **Navigation Mesh**, ou **NavMesh**.

Une NavMesh est une version simplifiée de la géométrie du monde qui ne contient que les surfaces où les personnages peuvent marcher.

```text
Géométrie 3D complète
    ↓ bake
NavMesh simplifiée
    ↓ pathfinding
Chemin utilisable par les agents
```

Exemple :

```text
Monde 3D réel :

    mur
+-----------+
|           |
|           |
+-----------+

sol, pente, escalier, obstacles, props

NavMesh :

+----------------------+
| zone navigable       |
|     obstacle retiré  |
| zone non navigable   |
+----------------------+
```

La NavMesh ignore les détails inutiles au déplacement et conserve uniquement les zones utiles à la navigation.

---

## 3.2 Paramètres d’agent

Une NavMesh dépend du type de personnage.

Un petit monstre, un humain et un boss n’ont pas les mêmes contraintes.

Exemple :

```csharp
public sealed class NavigationAgentSettings
{
    public float Radius;
    public float Height;
    public float MaxSlopeAngle;
    public float StepHeight;
    public float MaxSpeed;
    public float Acceleration;
    public NavigationLayerMask LayerMask;
}
```

## Exemples

| Agent | Radius | Height | MaxSlope | StepHeight |
|---|---:|---:|---:|---:|
| Rat | 0.15 | 0.20 | 50° | 0.05 |
| Humain | 0.35 | 1.80 | 45° | 0.30 |
| Boss | 1.20 | 3.00 | 35° | 0.40 |

Un couloir peut donc être navigable pour un rat mais non navigable pour un boss.

---

## 3.3 Coûts de navigation

Une zone navigable peut avoir un coût.

Le chemin choisi n’est pas forcément le plus court, mais souvent le moins coûteux.

```text
Sol normal          coût 1
Herbe dense         coût 2
Boue                coût 4
Eau peu profonde    coût 6
Lave                interdit ou coût très élevé
```

Exemple :

```csharp
public enum NavigationAreaType
{
    Walkable,
    Mud,
    Water,
    Dangerous,
    Forbidden
}

public sealed class NavigationAreaCost
{
    public NavigationAreaType AreaType;
    public float Cost;
}
```

Cela permet de créer des comportements différents selon les agents.

Exemple :

```text
Humain :
- évite l’eau
- préfère la route

Monstre aquatique :
- préfère l’eau
- peut ignorer certaines zones dangereuses

Robot lourd :
- évite les ponts fragiles
```

---

## 3.4 Off-mesh links

Une NavMesh représente surtout la marche continue.

Mais certains déplacements sont spéciaux :

- sauter par-dessus un trou ;
- monter une échelle ;
- franchir une porte ;
- prendre un ascenseur ;
- sauter depuis une corniche ;
- se téléporter ;
- grimper ;
- utiliser une tyrolienne.

Pour cela, on utilise des **off-mesh links**.

```text
NavMesh A ----[JumpLink]---- NavMesh B
```

Un lien de navigation indique :

```csharp
public sealed class NavigationLink
{
    public Vector3 Start;
    public Vector3 End;
    public NavigationLinkType Type;
    public float Cost;
    public bool Bidirectional;
}
```

Exemple :

```csharp
public enum NavigationLinkType
{
    Jump,
    DropDown,
    Ladder,
    Door,
    Elevator,
    Teleport,
    Custom
}
```

Le pathfinding sait que le lien existe.

Mais l’exécution du lien appartient au gameplay :

```text
Navigation:
    "Il faut utiliser le JumpLink"

Gameplay:
    "Je joue l’animation de saut"

CharacterController:
    "Je déplace réellement le personnage"

Animation:
    "Je synchronise le saut"
```

---

## 3.5 Pathfinding 3D

Le pathfinding 3D sur NavMesh se fait généralement en plusieurs étapes :

```text
1. Trouver le polygone de départ
2. Trouver le polygone d’arrivée
3. Lancer A* sur les polygones
4. Obtenir une séquence de polygones
5. Simplifier le chemin
6. Produire des waypoints utilisables
```

Chemin brut :

```text
Poly 12 -> Poly 18 -> Poly 19 -> Poly 25 -> Poly 31
```

Chemin final :

```text
(10, 0, 4)
(16, 0, 8)
(24, 0, 12)
```

---

## 3.6 Path smoothing

Le chemin brut n’est pas toujours naturel.

Il faut souvent le lisser.

Sans smoothing :

```text
x -> x -> x -> x
```

Avec smoothing :

```text
x ---------> x -----> x
```

Le moteur peut utiliser :

- funnel algorithm ;
- string pulling ;
- simplification de waypoints ;
- steering progressif vers le prochain point ;
- anticipation des virages.

---

## 3.7 Évitement local

Le pathfinding global ne suffit pas.

Exemples :

- deux PNJ veulent passer dans le même couloir ;
- le joueur bloque temporairement un chemin ;
- un obstacle dynamique apparaît ;
- plusieurs ennemis encerclent une cible ;
- une foule se déplace.

On ajoute donc une couche d’évitement local.

```text
Path global
    ↓
Direction souhaitée
    ↓
Correction selon les autres agents
    ↓
Correction selon les obstacles dynamiques
    ↓
DesiredVelocity finale
```

Important :

```text
Local avoidance ≠ collision physique
```

L’évitement local propose une vitesse corrigée.

Le CharacterController vérifie ensuite si le mouvement est réellement possible.

---

## 3.8 Navigation dynamique

Un monde moderne peut changer :

- porte qui s’ouvre ;
- porte qui se ferme ;
- pont qui s’effondre ;
- obstacle déplacé ;
- bâtiment détruit ;
- ascenseur qui change d’étage ;
- personnage qui bloque un passage.

Il faut donc prévoir :

```text
Obstacle dynamique
    -> modifie temporairement la navigation

Rebuild partiel
    -> recalcule une portion de NavMesh

Repath
    -> recalcule le chemin de certains agents
```

---

# 4. Navigation en monde 2D

La navigation 2D dépend fortement du type de jeu.

Il y a trois cas principaux :

```text
2D TileMap
2D top-down libre
2D platformer
```

---

# 5. Navigation 2D TileMap

## 5.1 Grille de navigation

Dans un jeu 2D basé sur une TileMap, la solution la plus naturelle est souvent une grille A*.

Chaque tile devient un nœud.

```text
[ ][ ][X][ ][ ]
[ ][ ][X][ ][ ]
[ ][ ][ ][ ][ ]
[ ][X][X][X][ ]
[ ][ ][ ][ ][ ]
```

`X` représente une tile bloquée.

Chaque tile peut contenir :

```csharp
public sealed class NavigationTile
{
    public bool Walkable;
    public float Cost;
    public NavigationLayerMask Layers;
}
```

---

## 5.2 Propriétés par tile

Exemple de données :

```text
Sol normal      walkable    coût 1
Herbe           walkable    coût 2
Boue            walkable    coût 4
Eau             variable    coût 6
Mur             bloqué
Porte fermée    bloqué
Porte ouverte   walkable
```

Exemple :

```csharp
public enum TileNavigationType
{
    Walkable,
    Blocked,
    Slow,
    Water,
    Door,
    Ladder,
    Damage
}
```

---

## 5.3 Mouvements autorisés

Selon le jeu, on autorise :

```text
4 directions :
haut, bas, gauche, droite

8 directions :
haut, bas, gauche, droite, diagonales

mouvement libre :
grille seulement utilisée pour calculer un chemin approximatif
```

En 4 directions :

```text
  N
W + E
  S
```

En 8 directions :

```text
NW N NE
 W + E
SW S SE
```

Attention aux diagonales.

Si deux tiles orthogonales sont bloquées, il faut souvent interdire la diagonale pour éviter que l’agent traverse un coin.

```text
[X][ ]
[ ][A]

A ne devrait pas forcément pouvoir aller en diagonale vers le haut-gauche.
```

---

## 5.4 Pathfinding A*

A* est le choix classique pour une grille.

Il utilise :

```text
G = coût depuis le départ
H = estimation vers l’arrivée
F = G + H
```

Pour une TileMap :

```csharp
public sealed class GridPathfinder
{
    public bool TryFindPath(
        Point start,
        Point goal,
        NavigationQuery query,
        out NavigationPath path)
    {
        // A*
    }
}
```

---

## 5.5 TileMap et couches

Dans un moteur avec TileMaps, toutes les couches ne doivent pas forcément participer à la navigation.

Exemple :

```text
GroundLayer
    -> sol visuel

CollisionLayer
    -> murs, obstacles

DecorationLayer
    -> arbres, buissons, détails

NavigationLayer
    -> données de navigation explicites
```

Approche recommandée :

```text
Ne pas déduire toute la navigation uniquement du rendu visuel.

Créer une couche de navigation ou collision claire.
```

Cela évite de dépendre du nom des tiles ou de leur apparence.

---

## 5.6 Agents différents

Un même monde peut être traversé différemment selon l’agent.

Exemple :

```text
Humain :
- marche sur sol
- ne traverse pas l’eau

Poisson :
- traverse l’eau
- ne traverse pas la terre

Fantôme :
- traverse certains murs

Volant :
- ignore les obstacles au sol
```

Il faut donc prévoir un masque :

```csharp
[Flags]
public enum NavigationLayer
{
    Ground = 1 << 0,
    Water = 1 << 1,
    Flying = 1 << 2,
    Door = 1 << 3,
    Dangerous = 1 << 4
}
```

---

# 6. Navigation 2D top-down libre

## 6.1 Navigation polygons

Pour un jeu 2D top-down avec mouvement fluide, une grille peut être trop rigide.

On peut utiliser des polygones de navigation.

```text
+------------------------+
| zone navigable         |
|                        |
|    +------------+      |
|    | obstacle   |      |
|    +------------+      |
|                        |
+------------------------+
```

Le monde est représenté par des surfaces 2D navigables.

L’agent peut se déplacer librement à l’intérieur.

---

## 6.2 Avantages

Les navigation polygons sont utiles pour :

- les jeux top-down sans grille visible ;
- les RPG d’action ;
- les jeux avec trajectoires fluides ;
- les environnements organiques ;
- les zones non alignées sur des tiles.

---

## 6.3 Inconvénients

Ils sont plus complexes à générer et éditer.

Pour un moteur maison, il est souvent préférable de commencer par :

```text
V1 : grille A*
V2 : navigation polygons manuels
V3 : génération automatique
```

---

# 7. Navigation 2D platformer

## 7.1 Problème spécifique

Un platformer n’est pas seulement un monde 2D.

La gravité change tout.

Le chemin n’est pas seulement :

```text
aller de A à B
```

Mais plutôt :

```text
quelle séquence d’actions permet d’atteindre B ?
```

Exemples d’actions :

- marcher ;
- sauter ;
- tomber ;
- descendre d’une plateforme ;
- grimper ;
- utiliser une échelle ;
- wall jump ;
- dash ;
- utiliser un ressort ;
- prendre une plateforme mobile.

---

## 7.2 Graphe de plateformes

On représente souvent le niveau comme un graphe.

```text
Platform A ----walk---- Platform A end
     |                       |
   jump                    fall
     ↓                       ↓
Platform B ------------walk------------
```

Chaque lien représente une action.

```csharp
public enum PlatformerNavAction
{
    Walk,
    Jump,
    Fall,
    Climb,
    DropThrough,
    Dash,
    WallJump,
    UseMovingPlatform
}
```

Un lien contient :

```csharp
public sealed class PlatformerNavigationLink
{
    public Vector2 Start;
    public Vector2 End;
    public PlatformerNavAction Action;
    public float Cost;
    public float RequiredJumpHeight;
    public float RequiredHorizontalSpeed;
}
```

---

## 7.3 Pathfinding platformer

Le pathfinding doit savoir si l’action est possible.

Exemple :

```text
La plateforme B est-elle atteignable depuis A avec :
- la hauteur de saut du personnage ?
- sa vitesse horizontale ?
- sa gravité ?
- son dash ?
- son wall jump ?
```

Donc le système de navigation platformer dépend fortement du CharacterController.

---

# 8. Interaction avec le CharacterController

La navigation ne doit pas remplacer le CharacterController.

Le bon découpage est :

```text
NavigationAgent
    -> calcule destination, requête et chemin

CharacterControllerNavigationDriverComponent
    -> suit les waypoints et convertit en intention Vector2

CharacterController
    -> applique le mouvement réel via SetMoveIntent + Update

AnimationController
    -> joue l’animation adaptée
```

## Exemple

```csharp
public sealed class NavigationAgentComponent : Component
{
    public Vector3 Destination;
    public NavigationPath CurrentPath;
    public NavigationQuery Query;
    public float StoppingDistance;
    public bool HasDestination;
    public bool HasPath;
    public bool ReachedDestination;
}
```

```csharp
public sealed class CharacterControllerNavigationDriverComponent : Component
{
    public void SetPath(IReadOnlyList<Vector3> waypoints, float stoppingDistance = 0.1f)
    {
        // suit les waypoints puis appelle CharacterControllerComponent.SetMoveIntent(Vector2)
    }

    public void MoveTo(Vector3 destination, float stoppingDistance = 0.1f)
    {
        // raccourci haut niveau pour une destination directe
    }
}
```

Dans CasaEngine, le système de navigation donne donc un chemin au driver. Il n'appelle pas directement `CharacterControllerComponent.Move(...)`.

---

# 9. Interaction avec l’animation

Le système de navigation fournit des informations.

Le système d’animation décide quoi jouer.

```csharp
public sealed class NavigationAnimationData
{
    public float Speed;
    public Vector3 Direction;
    public bool IsMoving;
    public bool IsStopping;
    public bool IsUsingNavigationLink;
}
```

Exemples d’animations :

```text
Idle
Walk
Run
Turn
Stop
Jump
Climb
OpenDoor
DropDown
```

---

# 10. Interaction avec les cutscenes

Une cutscene peut vouloir contrôler un personnage.

Il faut donc prévoir des modes de contrôle :

```csharp
public enum CharacterControlMode
{
    Player,
    AI,
    Script,
    Cutscene,
    Disabled
}
```

Exemple :

```text
Mode AI :
    l’IA ou la navigation choisit une destination et pilote un driver

Mode Script :
    un script gameplay pilote temporairement le personnage

Mode Cutscene :
    un script impose position, vitesse ou animation

Mode Player :
    l’input joueur contrôle le mouvement
```

Le système de navigation doit pouvoir être suspendu :

```csharp
navigationDriver.Cancel();
```

Ou contrôlé par une cutscene :

```csharp
navigationDriver.MoveTo(targetPosition);
```

---

# 11. Débogage et visualisation éditeur

Un système de navigation doit être visible dans l’éditeur.

À afficher :

```text
NavMesh / grille navigable
zones bloquées
coûts de navigation
destination de l’agent
chemin courant
waypoints
desired velocity
local avoidance radius
off-mesh links
obstacles dynamiques
```

Exemple de debug draw :

```csharp
navigationDebug.DrawNavigationMap();
navigationDebug.DrawAgentPath(agent);
navigationDebug.DrawAgentDesiredVelocity(agent);
navigationDebug.DrawNavigationLinks();
```

Pour CasaEngine, ce debug draw doit utiliser les backends existants :

```text
Renderer2DComponent
    -> grille TileMap, cellules visibles, coûts, chemin 2D

Line3dRendererComponent
    -> chemins 3D, liens, contours simples de NavMesh
```

Le debug navigation doit limiter le nombre de primitives par frame et culler par vue. Les renderers existants sont adaptés à un overlay de debug léger, mais pas à l'affichage permanent d'une grille complète ou d'une NavMesh dense sans budget.

---

# 12. V1 - Noyau simple et testable

## Objectif

La V1 doit être petite, robuste et utilisable rapidement.

Elle doit permettre :

- de faire se déplacer un personnage vers une destination ;
- de calculer un chemin en 2D TileMap ;
- de séparer navigation et CharacterController ;
- de visualiser le chemin en debug ;
- de tester facilement le système.

---

## Périmètre V1

### Inclus

```text
NavigationAgentComponent
NavigationSystem
NavigationGrid2D
GridPathfinder2D
NavigationPath
NavigationQuery
Navigation debug draw via Renderer2DComponent / Line3dRendererComponent
Intégration CharacterController via CharacterControllerNavigationDriverComponent
```

### Non inclus

```text
NavMesh 3D
Avoidance complexe
Crowd simulation
Rebuild dynamique avancé
Platformer navigation avancée
Streaming
Multithreading
```

---

## Classes V1 proposées

```csharp
public interface INavigationMap
{
    bool TryFindPath(
        Vector3 start,
        Vector3 end,
        NavigationQuery query,
        out NavigationPath path);

    bool IsWalkable(Vector3 position, NavigationAgentSettings agent);

    Vector3 ProjectToNavigation(Vector3 position);
}
```

```csharp
public sealed class NavigationGrid2D : INavigationMap
{
    public int Width;
    public int Height;
    public float CellSize;

    public bool IsCellWalkable(int x, int y, NavigationQuery query)
    {
        // check tile
    }

    public bool TryFindPath(
        Vector3 start,
        Vector3 end,
        NavigationQuery query,
        out NavigationPath path)
    {
        // GridPathfinder2D dédié à la TileMap, pas PathPlanner<T>
    }

    public Vector3 ProjectToNavigation(Vector3 position)
    {
        // snap or clamp to grid
    }
}
```

```csharp
public sealed class NavigationAgentComponent : Component
{
    public Vector3 Destination;
    public NavigationPath CurrentPath;
    public NavigationQuery Query;

    public float StoppingDistance = 0.1f;

    public bool HasDestination;
    public bool HasPath;
    public bool ReachedDestination;
}
```

```csharp
public sealed class NavigationPath
{
    public readonly List<Vector3> Points = new();
    public int CurrentPointIndex;

    public bool IsFinished => CurrentPointIndex >= Points.Count;
}
```

```csharp
public sealed class NavigationQuery
{
    public NavigationAgentSettings AgentSettings;
    public NavigationLayerMask LayerMask;
}
```

---

## Update V1

```text
1. Si l’agent a une destination et pas de chemin :
       calculer un chemin

2. Si un chemin est trouvé :
    stocker le chemin dans l’agent

3. Envoyer les waypoints au CharacterControllerNavigationDriverComponent

4. Le driver suit les waypoints et produit SetMoveIntent(Vector2)

5. Le CharacterController applique le mouvement dans son Update
```

Pseudo-code :

```csharp
public void UpdateAgent(NavigationAgentComponent agent)
{
    if (agent.HasDestination && !agent.HasPath)
    {
        if (_navigationMap.TryFindPath(
            agent.Owner.Transform.Position,
            agent.Destination,
            agent.Query,
            out var path))
        {
            agent.CurrentPath = path;
            agent.HasPath = true;

            var driver = agent.Owner.GetComponent<CharacterControllerNavigationDriverComponent>();
            if (driver != null)
            {
                driver.SetPath(path.Points, agent.StoppingDistance);
            }
        }
        else
        {
            agent.HasPath = false;
            agent.ReachedDestination = false;
        }
    }
}
```

---

## V1 pour CasaEngine

Pour CasaEngine, la V1 recommandée serait :

```text
TileMapData + couche navigation explicite -> NavigationGrid2D
NavigationGrid2D -> GridPathfinder2D -> NavigationPath
World -> NavigationSystem
NavigationSystem -> CharacterControllerNavigationDriverComponent.SetPath(...)
CharacterControllerNavigationDriverComponent -> CharacterControllerComponent.SetMoveIntent(Vector2)
Renderer2DComponent / Line3dRendererComponent -> affiche chemin, grille et liens
```

### Pourquoi commencer par la 2D ?

Parce que :

- c’est plus simple à tester ;
- ça s’intègre bien avec les TileMaps ;
- A* est stable et facile à valider ;
- cela impose déjà la bonne architecture ;
- la séparation Navigation / CharacterController sera réutilisable en 3D.

---

# 13. V2 - Système moteur moderne

## Objectif

La V2 transforme le noyau V1 en vrai système utilisable dans un jeu plus complexe.

Elle ajoute :

- agents multiples ;
- coûts de navigation ;
- couches de navigation ;
- obstacles dynamiques simples ;
- recalcul de chemin ;
- path smoothing ;
- off-mesh links simples ;
- debug éditeur plus complet ;
- premières bases 3D.

---

## Périmètre V2

### Inclus

```text
Navigation layers
Area costs
Multiple agent types
Path smoothing
Dynamic obstacles simples
Repath automatique
Off-mesh links
Navigation polygons 2D optionnels
Début NavMesh 3D manuel ou importé
Debug avancé
```

### Non inclus

```text
Crowd simulation avancée
Streaming NavMesh
Async jobs
Génération Recast complète
Navigation hiérarchique
```

---

## Navigation layers

Chaque agent peut avoir un masque de navigation.

```csharp
[Flags]
public enum NavigationLayerMask
{
    None = 0,
    Ground = 1 << 0,
    Water = 1 << 1,
    Flying = 1 << 2,
    Door = 1 << 3,
    Dangerous = 1 << 4,
    All = ~0
}
```

Exemple :

```text
Humain :
    Ground, Door

Poisson :
    Water

Fantôme :
    Ground, Door, Dangerous

Volant :
    Flying
```

---

## Area costs

Chaque zone peut avoir un coût.

```csharp
public sealed class NavigationArea
{
    public NavigationLayerMask Layer;
    public float Cost;
}
```

Le pathfinding doit intégrer ce coût dans A*.

```text
cost = movementCost * areaCost
```

---

## Obstacles dynamiques simples

Un obstacle dynamique peut temporairement bloquer des cellules.

Exemples :

- caisse poussée ;
- porte fermée ;
- personnage bloquant un passage ;
- élément destructible ;
- pont activé/désactivé.

```csharp
public sealed class DynamicNavigationObstacleComponent : Component
{
    public BoundingBox Bounds;
    public bool BlocksNavigation;
    public float AdditionalCost;
}
```

En V2, on peut rester simple :

```text
TileMap/Grid :
    marquer des cellules comme bloquées ou coûteuses

NavMesh :
    marquer une zone comme temporairement interdite
```

---

## Repath automatique

Un agent doit pouvoir recalculer son chemin si :

```text
sa destination change
son chemin est bloqué
il est poussé hors du chemin
un obstacle dynamique apparaît
il n’avance plus depuis un certain temps
```

Exemple :

```csharp
public sealed class NavigationAgentComponent : Component
{
    public float RepathInterval = 0.25f;
    public float RepathTimer;
    public bool NeedsRepath;
}
```

---

## Path smoothing

En V2, il faut éviter que les agents suivent mécaniquement chaque cellule.

Pour une grille :

```text
A* produit :
[0,0] [1,0] [2,0] [3,1] [4,2]

Smoothing produit :
[0,0] [2,0] [4,2]
```

Avec line-of-sight :

```csharp
if (HasLineOfSight(pointA, pointC))
{
    remove pointB;
}
```

---

## Off-mesh links V2

En V2, on peut ajouter des liens simples :

```csharp
public sealed class NavigationLinkComponent : Component
{
    public Vector3 Start;
    public Vector3 End;
    public NavigationLinkType Type;
    public bool Bidirectional;
    public float Cost;
}
```

Exemples :

```text
DoorLink
JumpLink
LadderLink
TeleportLink
```

Le chemin peut contenir des segments spéciaux :

```csharp
public enum NavigationPathSegmentType
{
    Move,
    Link
}
```

---

## Navigation polygons 2D

Pour la V2, tu peux ajouter un système de polygones 2D manuel.

```text
L’éditeur permet de dessiner une région navigable.
Le pathfinding utilise un graphe de polygones.
```

Cela permet de supporter les jeux 2D top-down non basés sur une grille.

---

## Début de NavMesh 3D

Pour la V2, deux options :

### Option A : NavMesh manuelle

L’éditeur permet de placer des polygones de navigation.

Avantages :

- simple ;
- contrôlable ;
- pas besoin de génération automatique ;
- suffisant pour prototypes.

Inconvénients :

- pas adapté aux grands mondes ;
- fastidieux à maintenir.

### Option B : importer une NavMesh

Importer une NavMesh depuis un outil externe.

Avantages :

- évite d’écrire tout de suite un générateur ;
- permet de tester l’architecture 3D.

Inconvénients :

- dépend d’un pipeline externe ;
- moins intégré à l’éditeur.

---

## Debug V2

À afficher :

```text
Grid / NavMesh
coûts de navigation
layers
chemins calculés
waypoints après smoothing
destination
vitesse souhaitée
obstacles dynamiques
links
état de chaque agent
```

---

# 14. V3 - Système avancé proche moteur professionnel

## Objectif

La V3 vise un système proche des moteurs modernes avancés.

Elle ajoute :

- NavMesh 3D générée automatiquement ;
- tuilage de NavMesh ;
- streaming ;
- rebuild partiel ;
- crowd simulation ;
- pathfinding asynchrone ;
- navigation hiérarchique ;
- smart objects ;
- intégration avancée cutscene/gameplay/animation.

---

## Périmètre V3

### Inclus

```text
Recast/Detour ou équivalent
NavMesh tiled
Rebuild partiel
Streaming de navigation
Crowd simulation
Local avoidance avancée
Async pathfinding
Hierarchical pathfinding
Smart navigation links
Smart objects
Navigation editor avancé
```

---

## Génération automatique de NavMesh

Le moteur analyse :

```text
géométrie statique
colliders
pentes
hauteurs de marche
obstacles
volumes d’exclusion
volumes de coût
types d’agents
```

Puis il génère automatiquement une NavMesh.

Pipeline :

```text
Scene geometry
    ↓
Rasterization
    ↓
Walkable filtering
    ↓
Region building
    ↓
Contour extraction
    ↓
Polygon mesh
    ↓
Detail mesh
    ↓
Runtime navigation data
```

---

## NavMesh tiled

Pour les grands mondes, il ne faut pas une seule grosse NavMesh.

Il faut découper la navigation en tuiles.

```text
+----+----+----+
| T1 | T2 | T3 |
+----+----+----+
| T4 | T5 | T6 |
+----+----+----+
| T7 | T8 | T9 |
+----+----+----+
```

Avantages :

- rebuild partiel ;
- streaming ;
- meilleure mémoire ;
- génération parallèle ;
- chargement/déchargement par zone.

---

## Rebuild partiel

Si une partie du monde change, il faut recalculer seulement les tuiles affectées.

Exemple :

```text
Un pont s’effondre
    -> seules les tuiles proches du pont sont rebakées
```

Pas tout le monde.

---

## Pathfinding asynchrone

Dans un jeu moderne, le pathfinding de nombreux agents ne doit pas bloquer la frame.

Il faut un système de requêtes :

```csharp
public sealed class NavigationPathRequest
{
    public Vector3 Start;
    public Vector3 End;
    public NavigationQuery Query;
    public Action<NavigationPathResult> Callback;
}
```

Pipeline :

```text
Game thread
    -> enqueue request

Navigation worker
    -> calculate path

Game thread
    -> apply result
```

---

## Navigation hiérarchique

Pour les grands mondes, A* direct peut être trop coûteux.

On peut utiliser une navigation hiérarchique :

```text
Niveau local :
    tiles / polys

Niveau régional :
    rooms / sectors

Niveau global :
    zones / chunks
```

Le moteur cherche d’abord un chemin global, puis détaille localement.

---

## Crowd simulation

La V3 peut intégrer une vraie simulation de foule.

Objectifs :

- éviter les collisions entre agents ;
- maintenir un flux naturel ;
- éviter les blocages ;
- gérer des groupes ;
- gérer des priorités.

Exemples :

```text
Agent rapide contourne agent lent
Garde prioritaire pousse foule
Groupe conserve une formation
PNJ évitent le joueur
```

---

## Smart Objects

Les moteurs modernes ne se limitent pas à une NavMesh.

Ils utilisent des objets interactifs de navigation.

Exemples :

```text
porte
échelle
couverture
chaise
bouton
véhicule
ascenseur
point de saut
```

Un Smart Object décrit :

```csharp
public sealed class SmartObjectNavigationEntry
{
    public Vector3 EntryPoint;
    public Vector3 ExitPoint;
    public string InteractionName;
    public float Cost;
    public bool IsAvailable;
}
```

Exemple :

```text
L’IA veut aller dans une pièce.
Le chemin contient une porte.
La porte est un Smart Object.
L’IA réserve la porte, joue l’animation, l’ouvre, passe, puis libère la porte.
```

---

## Intégration avancée animation

En V3, les animations peuvent contrôler le mouvement.

C’est le cas avec le root motion.

Dans ce cas :

```text
Navigation
    -> propose une trajectoire

Animation
    -> produit le déplacement réel

CharacterController
    -> valide collision et position

Navigation
    -> corrige progressivement
```

C’est utile pour :

- attaques ;
- sauts ;
- esquives ;
- interactions ;
- cinématiques ;
- déplacements réalistes.

---

## Intégration avancée cutscene

En V3, une cutscene peut demander :

```text
MoveTo
FaceTarget
UseSmartObject
PlayAnimationAndWait
FollowSpline
WaitUntilReached
```

Exemple :

```csharp
cutscene.Sequence(
    MoveTo(npc, door.EntryPoint),
    UseSmartObject(npc, door),
    MoveTo(npc, chair.Position),
    PlayAnimation(npc, "Sit")
);
```

---

# 15. Roadmap recommandée pour CasaEngine

## V1 recommandée

Commencer par :

```text
Couche TileMap navigation explicite
NavigationGrid2D
GridPathfinder2D
NavigationPath / NavigationQuery
NavigationSystem
CharacterControllerNavigationDriverComponent
Debug draw Renderer2DComponent / Line3dRendererComponent
```

Priorité :

```text
1. Stockage TileMap navigation explicite
2. Tests unitaires grille, coûts, diagonales et chemin impossible
3. Pathfinding grille dédié, sans PathPlanner<T>
4. Intégration CharacterController via driver
5. Debug visuel avec budget de primitives
```

---

## V2 recommandée

Ajouter ensuite :

```text
Navigation layers
Area costs
Obstacles dynamiques
Repath
Path smoothing
Off-mesh links
Navigation polygons 2D
NavMesh 3D manuelle ou importée
```

Priorité :

```text
1. Agents différents
2. Coûts et layers
3. Obstacles dynamiques simples
4. Repath robuste
5. Liens spéciaux
```

---

## V3 recommandée

Ajouter plus tard :

```text
Recast/Detour ou équivalent
NavMesh 3D automatique
Tiled NavMesh
Async pathfinding
Crowd simulation
Smart objects
Streaming
Navigation editor avancé
```

Priorité :

```text
1. Génération automatique NavMesh
2. Tuilage
3. Rebuild partiel
4. Jobs async
5. Crowd
6. Smart objects
```

---

# 16. Résumé final

## Monde 3D

```text
NavMesh
+ A*
+ path smoothing
+ off-mesh links
+ local avoidance
+ CharacterController
```

## Monde 2D TileMap

```text
NavigationGrid2D
+ A*
+ coûts par tile
+ layers
+ obstacles dynamiques
```

## Monde 2D top-down libre

```text
Navigation polygons
+ pathfinding polygonal
+ smoothing
+ avoidance
```

## Monde 2D platformer

```text
Graphe de plateformes
+ actions de navigation
+ validation selon les capacités du personnage
```

## Règle principale

```text
La navigation calcule une intention.
Le CharacterController applique le mouvement réel.
L’animation reflète ou pilote ce mouvement.
```

C’est cette séparation qui permet d’avoir un système robuste, extensible et compatible avec le gameplay, la physique, l’animation et les cutscenes.

Decisions: see [ADR-0023](../decisions/0023-navigation-v1-on-tilemap-grids.md).
