# CasaEngine — Structure & Architecture Analysis Tasks

## Objective
Analyze the CasaEngine project to verify it is well structured, properly layered, and that features are cleanly separated. Identify dependency violations, misplaced code, design smells, and areas for improvement.

---

## Format de réponse attendu

Toutes les réponses doivent être rédigées **en français**. Chaque tâche analysée doit produire une réponse structurée selon le modèle suivant :

```markdown
### ✅ Bien implémenté
- Point positif 1
- Point positif 2

### ❌ Erreurs identifiées
- Violation ou problème concret 1 (fichier/classe concerné(e))
- Violation ou problème concret 2

### 💡 Pistes d'amélioration
- Suggestion concrète 1
- Suggestion concrète 2
```

> **Règles de rédaction :**
> - Être factuel et précis : toujours citer le fichier, la classe ou le namespace concerné.
> - **Bien implémenté** : ne pas inventer de points positifs s'il n'y en a pas.
> - **Erreurs identifiées** : distinguer les violations de couche (critique), les incohérences de design (majeur) et les problèmes de nommage/organisation (mineur). Indiquer la sévérité entre parenthèses : `(critique)`, `(majeur)`, `(mineur)`.
> - **Pistes d'amélioration** : proposer des actions concrètes et réalistes, en précisant si c'est un quick win ou un refactoring lourd.
>
> **Règle fondamentale — MonoGame/XNA :**
> Ce moteur est construit **sur** MonoGame (`Microsoft.Xna.Framework`). Les références à MonoGame dans `Core/` et `Engine/` sont **normales et attendues** (ex: `Vector3`, `Matrix`, `GameTime`, `GraphicsDevice`…). Ne pas les signaler comme des erreurs. La seule règle de couche à contrôler est : **Core ne doit pas référencer Engine ni Framework**, et **Engine ne doit pas référencer Framework**.

---

## 1. Layer dependency analysis (Core ← Engine ← Framework)

The expected dependency rule: **Core** has zero inward dependencies, **Engine** depends only on Core, **Framework** depends on Engine + Core. No reverse dependencies allowed.

- [x] **1.1** Scan all `using` / references in `Core/` — verify it never references `Engine/` or `Framework/`.
- [x] **1.2** Scan all `using` / references in `Engine/` — verify it never references `Framework/`.
- [x] **1.3** Scan all `using` / references in `Framework/` — catalog all references to `Core/` and `Engine/` (these are allowed), flag any circular references.
- [x] **1.4** ~~Check if `Core/` has any dependency on MonoGame types~~ — **Non applicable** : MonoGame est le framework de base, les références XNA dans Core et Engine sont normales et attendues.
- [x] **1.5** ~~Check if `Engine/` references MonoGame types directly~~ — **Non applicable** : même raison.
- [x] **1.6** Produce a dependency matrix (Core → Engine → Framework) showing actual cross-layer references by namespace.

---

### Résultats — Tâche 1 : Analyse des dépendances de couche _(état final)_

---

### ✅ Bien implémenté

- `Framework/` référence correctement `Core/` et `Engine/` sans dépendances inversées — conforme à la règle de couche.
- `Engine/` ne référence plus `Framework` — **zéro violation**.
- `Core/` ne référence plus `Framework` ni `Engine` — **zéro violation**.
- Aucun import circulaire entre les couches ni à l'intérieur de `Framework/`.
- Les références MonoGame (`Microsoft.Xna.Framework`) dans `Core/` et `Engine/` sont normales et attendues : le moteur est construit sur MonoGame.

---

### ❌ Erreurs identifiées

Aucune violation de couche détectée.

---

### 💡 Pistes d'amélioration

- **[Quick win — documentation]** Ajouter un fichier `LAYER_RULES.md` à la racine de `CasaEngine/` documentant explicitement les règles : Core ne dépend de rien (sauf MonoGame), Engine dépend de Core, Framework dépend de Engine + Core.

- **[Tooling — prévention des régressions]** Mettre en place un outil d'analyse statique des dépendances entre namespaces pour détecter automatiquement toute nouvelle violation de couche avant qu'elle entre dans le dépôt. Deux options :

  - **`dotnet-depends`** : outil CLI (`dotnet tool install dotnet-depends`) qui analyse les dépendances entre projets et assemblies. Il génère un graphe de dépendances et peut être utilisé dans la CI pour vérifier qu'aucun projet de couche basse ne référence une couche haute. Usage : `dotnet depends CasaEngine.csproj` pour visualiser le graphe, puis définir des règles d'exclusion dans le pipeline CI.

  - **`ArchUnitNET`** : bibliothèque de test (NuGet `TngTech.ArchUnitNET`) qui permet d'écrire des règles architecturales sous forme de tests xUnit/NUnit. Exemple concret pour ce projet :
    ```csharp
    // Dans un projet CasaEngine.ArchTests
    Architecture architecture = new ArchLoader().LoadAssemblies(
        typeof(Core.Log.Logger).Assembly,       // CasaEngine (Core)
        typeof(Engine.PhysicsEngine).Assembly,  // CasaEngine (Engine)
        typeof(Framework.Game.GameManager).Assembly // CasaEngine (Framework)
    ).Build();

    IObjectProvider<IType> coreLayer     = Types().That().ResideInNamespace("CasaEngine.Core.*", true);
    IObjectProvider<IType> engineLayer   = Types().That().ResideInNamespace("CasaEngine.Engine.*", true);
    IObjectProvider<IType> frameworkLayer = Types().That().ResideInNamespace("CasaEngine.Framework.*", true);

    // Core ne doit pas dépendre de Engine ni Framework
    IArchRule coreRule = Types().That().Are(coreLayer)
        .Should().NotDependOnAny(engineLayer)
        .AndShould().NotDependOnAny(frameworkLayer);

    // Engine ne doit pas dépendre de Framework
    IArchRule engineRule = Types().That().Are(engineLayer)
        .Should().NotDependOnAny(frameworkLayer);

    coreRule.Check(architecture);
    engineRule.Check(architecture);
    ```
    Ces tests s'exécutent avec `dotnet test` et font échouer le build CI à la première violation introduite.

---

## 2. Core layer analysis

Core should contain only low-level utilities with no domain logic.

- [x] **2.1** Review `Core/Design/` — is `IBoundingBoxable` truly a core design pattern or a Framework/Engine concept?
- [x] **2.2** Review `Core/Helpers/` — check if helpers like `GameTimeHelper`, `BoundingBoxHelper`, `RayHelper` belong in Core or instead in Engine (they use MonoGame types).
- [x] **2.3** Review `Core/Maths/` — verify these are pure math utilities with no engine coupling.
- [x] **2.4** Review `Core/Shapes/` — are shapes (Box, Capsule, Sphere…) pure geometry or do they reference engine/framework types?
- [x] **2.5** Review `Core/Serialization/` — check if `ISerializable` and `JsonHelper` introduce unwanted dependencies.
- [x] **2.6** Review `Core/Collections/`, `Core/Log/`, `Core/MultiThreading/`, `Core/Packing/`, `Core/Parser/` — do they stay self-contained?

---

### Résultats — Tâche 2 : Analyse de la couche Core

---

### ✅ Bien implémenté

- **`Core/Design/`** : les patterns de base (`Disposable`, `IObservable<T>`, `IObserver<T>`, `EventArgs`) sont purs, sans aucune dépendance externe. `IBoundingBoxable` utilise `BoundingBox` de MonoGame — acceptable pour un moteur MonoGame.
- **`Core/Helpers/`** : tous les helpers sont des classes `static` à responsabilité unique (extensions de méthodes sur les types MonoGame). Aucun import d'autres namespaces CasaEngine.
- **`Core/Maths/`** : `Coordinates` centralise position/rotation/scale + matrice locale — concept cohérent. Les courbes (`BSpline`, `Bezier`) et utilitaires (`RectangleF`, `PolygonSimplifyTools`) sont bien à leur place.
- **`Core/Serialization/`** : `ISerializable` est une interface simple et propre (Newtonsoft.Json uniquement). `JsonHelper` étend les types MonoGame pour leur sérialisation JSON — logique attendue dans ce contexte.
- **`Core/Collections/`** : structures de données génériques pures (`PriorityQueue`, `Deque`, `Pool`, etc.) — zéro dépendance externe ni MonoGame. Parfaitement autonome.
- **`Core/Log/`** : interface `ILogger` + implémentations (`DebugLogger`, `FileLogger`) — uniquement `System.Diagnostics`, aucune dépendance moteur. Propre.
- **`Core/MultiThreading/`** : aucune dépendance externe. Autonome.
- **`Core/Packing/`** : algorithmes de packing de rectangles purs (Arevalo, Cygon, Simple) — aucune dépendance externe. Propre.
- **`Core/Parser/`** : dépend uniquement de `CasaEngine.Core.Serialization` (interne à Core) — cohérent, les tokens du calculateur sont sérialisables.

---

### ❌ Erreurs identifiées

#### `Core/Helpers/BoundingBoxHelper.cs` et `RayHelper.cs` — import `Microsoft.Xna.Framework.Graphics` (acceptable)

Ces deux helpers importent `Microsoft.Xna.Framework.Graphics` pour `Viewport` et `GraphicsDevice`. Le cas `RayHelper.CalculateRayFromScreenCoordinate` utilise `Viewport.Unproject()` — c'est un concept graphique (GPU), mais dans le contexte d'un moteur MonoGame intégré, cela reste acceptable.

---

### 💡 Pistes d'amélioration

- **[✅ Fait]** `Core/Shapes/` supprimé.
- **[✅ Fait]** Sérialisation de `Coordinates` externalisée dans `JsonHelper` — `Coordinates` ne dépend plus que de `Microsoft.Xna.Framework`.
- **[Réflexion]** Décider si `IBoundingBoxable` (dans `Core/Design/`) est vraiment un pattern générique ou un contrat métier spatial qui appartiendrait mieux à `Engine/`. Pas de violation de couche, mais sémantiquement discutable.
- **[Quick win]** Évaluer si `RayHelper` a vraiment sa place dans `Core/` : `Viewport.Unproject()` est un concept graphique GPU. Le déplacer vers un sous-dossier `Engine/Helpers/` serait plus précis sémantiquement.

---

## 3. Engine layer analysis

Engine should provide mid-level systems (input, physics, animations, primitives) that sit on top of Core.

- [x] **3.1** Review `Engine/Input/` — does input system depend only on Core + MonoGame? Does it leak into Framework?
- [x] **3.2** Review `Engine/Physics/` — does the physics engine depend only on Core? Does it reference entity/component types from Framework?
- [x] **3.3** Review `Engine/Animations/` — check if animation loading (AssimpConverter, RiggedModelLoader) depends on Framework types.
- [x] **3.4** Review `Engine/Primitives2D/` and `Engine/Primitives3D/` — are they independent of Framework?
- [x] **3.5** Review `Engine/Plugins/` — does the plugin system introduce reverse dependencies?
- [x] **3.6** Evaluate `Engine/EngineEnvironment.cs` and `Engine/Constants.cs` — are they well-scoped or do they act as a global god-config?

---

### Résultats — Tâche 3 : Analyse de la couche Engine _(état final après corrections)_

---

### ✅ Bien implémenté

- **`Engine/Input/`** : dépend uniquement de `Core.Design`, `Core.Serialization` et MonoGame. Zéro référence Framework. Propre.
- **`Engine/Input/InputDeviceStateProviders/`** : bonne abstraction — interfaces `IKeyboardStateProvider`, `IMouseStateProvider`, `IGamePadStateProvider` avec leurs implémentations. Permet de mocker les périphériques.
- **`Engine/Animations/AssimpConverter.cs`** : conversions Assimp→MonoGame uniquement. Après nettoyage du dead import (`using CasaEngine.Framework.Assets.Animations`), zéro dépendance Framework.
- **`Engine/Primitives2D/` et `Engine/Primitives3D/`** : aucune référence CasaEngine. Uniquement MonoGame. Parfaitement autonomes.
- **`Engine/Plugins/`** : uniquement `System.Reflection` + `EngineEnvironment` interne. Aucune dépendance Framework.
- **`Engine/Physics/`** : ne contient plus que des définitions pures — enums, settings, `PhysicsDefinition`. Zéro référence BulletSharp, zéro référence Framework.
- **`EngineEnvironment.cs`** : classe minimale, une seule propriété statique `ProjectPath`. Rôle clair.

---

### ❌ Erreurs identifiées

Aucune violation détectée. Toutes les erreurs précédentes ont été corrigées :

| Problème (état initial) | Correction appliquée |
|---|---|
| `PhysicsDefinition.cs` — propriétés `CollisionShape`/`MotionState` BulletSharp (majeur) | ✅ Propriétés supprimées, `using BulletSharp` retiré (commit `ea1290fd`) |
| `Constants.cs` — constantes avec concepts Framework dans Engine (mineur) | ✅ Fichier déplacé vers `Framework/Constants.cs` (commit `9b725f40`) |
| `InputManager.Test.cs` — fichier de test en production (mineur) | ✅ Supprimé (commit `9b725f40`) |
| `Extensions.cs` — conversions numériques sans rapport avec les animations (mineur) | ✅ Supprimé, contenu déplacé vers `Core/Helpers/NumericFormatExtensions.cs` (commit `5c42545c`) |
| `AssimpConverter.cs` — dead import `using CasaEngine.Framework.Assets.Animations` | ✅ Import supprimé (commit `5c42545c`) |

---

### 💡 Pistes d'amélioration

- **[Aucune action requise]** La couche Engine est désormais propre : zéro référence Framework, zéro dépendance BulletSharp. La règle de couche est respectée.

---

## 4. Framework layer analysis — general structure

- [x] **4.1** Enumerate all sub-namespaces under `Framework/` and classify them (system, feature module, utility).
- [x] **4.2** Check for cross-feature dependencies inside Framework (e.g. does `AI/` depend on `GUI/`? does `Audio/` depend on `Graphics2D/`?). Each feature should ideally only depend on Entities/Game + Engine + Core.
- [x] **4.3** Evaluate the `Framework/Objects/ObjectBase.cs` — is it a base class for entities? Does it create an inheritance hierarchy that competes with the component system?
- [x] **4.4** Check if `Framework/Scripting/` (`GameplayProxy`, `ScriptArcBallCamera`) is well-separated or a dumping ground.

---

### Résultats — Tâche 4 : Analyse structurelle de la couche Framework _(état final)_

---

### ✅ Bien implémenté

#### 4.1 — Inventaire des modules Framework (état actuel)

| Module | Rôle | Catégorie |
|---|---|---|
| `AI/` | BehaviorTree, FSM, NeuralNets, Steering, Pathfinding, Messaging | Feature — IA |
| `Assets/` | Animation, fonts, loaders (Assimp), sprites, textures, tiles | Feature — Assets |
| `Audio/` | Sons, musiques | Feature — Audio |
| `Constants.cs` | Extensions de fichiers (.entity, .world, .material…) | Utilitaire |
| `Debugger/` | Debug visuel, DebugManager, **OctreeVisualizer** | Utilitaire |
| `Entities/` | ECS : `Entity`, composants (Camera, Physics, Mesh, Input…) | Cœur — ECS |
| `Game/` | `CasaEngineGame`, `GameManager`, GameComponents | Cœur — Runtime |
| `GameFramework/` | `DemoGame`, `SceneManagement`, `GameMode`, `Player`, `Controller` | Feature — Gameplay |
| `Graphics/` | `RiggedModel`, shapes 3D | Feature — Rendu 3D |
| `Graphics2D/` | Sprites, tilemaps, rendu 2D | Feature — Rendu 2D |
| `GUI/` | Interface MGUI, screens | Feature — UI |
| `Input/` | `InputComponent`, `InputManager` (wrapper MonoGame) | Système |
| `Materials/` | `Material`, `MaterialAsset`, `ShaderWriter` | Feature — Matériaux |
| `ObjectBase.cs` | `ObjectBase` (Id, Name, AssetId, Initialize/Load/Save) — **à la racine Framework** | Utilitaire — Base |
| `Physics/` | Types de collision, `Collision`, `EventCollisionArgs` | Feature — Physique |
| `Project/` | `ProjectSettings` | Utilitaire |
| `Rendering/` | `RenderFrame`, `IRenderSurface`, `RenderPipeline`, `ViewManager` | Système — Multi-vues |
| `Scripting/` | `IGameplayProxy`, `GameplayProxy` (base), `ScriptArcBallCamera` | Feature — Scripts |
| `SpacePartitioning/` | `Octree`, queries spatiales — **Core uniquement** | Système |
| `World/` | `World` : container d'entités, gestion de scène | Feature — Monde |

#### 4.3 — ObjectBase

- Classe de base minimale et propre : `Id` (Guid), `Name`, `FileName`, `AssetId` + `Initialize()` / `Load()` / `Save()`.
- **[✅ Fait]** Déplacée du dossier `Objects/` vers la racine de `Framework/` (`namespace CasaEngine.Framework`) — plus aucun module ne dépend du namespace `Objects`.
- `Save()` correctement protégé par `#if EDITOR`.

#### 4.4 — Scripting/

- `IGameplayProxy` introduite — `Entity` dépend de l'interface et non de la classe concrète.
- `ScriptArcBallCamera` : script concret, bien ciblé.

#### Corrections appliquées depuis l'analyse initiale

| Problème (état initial) | Correction |
|---|---|
| `SpacePartitioning → Game` (mineur) | ✅ `OctreeVisualizer` déplacé dans `Debugger/` (commit `4aea6732`). `SpacePartitioning` est maintenant Core-only. |
| `Objects/` namespace trop fin (mineur) | ✅ `ObjectBase.cs` déplacé à la racine `Framework/`.  `Objects/` supprimé. |

---

### ❌ Erreurs identifiées _(restantes)_

#### `Entities → Scripting` — cycle résiduel (majeur)

`IGameplayProxy` est définie dans `Scripting/IGameplayProxy.cs` et dépend elle-même de `Entities` (`void Initialize(Entity owner)`) et retourne le type concret `GameplayProxy Clone()`. Le cycle reste :

```
Entities → Scripting.IGameplayProxy → Entities
```

Pour le briser complètement : déplacer `IGameplayProxy` dans `Entities/` (ou supprimer `GameplayProxy Clone()` du contrat — le Clone appartient à l'implémentation).

#### `Entities → GUI` — couplage discutable (mineur)

`ScreenWidgetComponent.cs` importe `using CasaEngine.Framework.GUI`. Sémantiquement acceptable mais crée une dépendance bidirectionnelle `Entities ↔ GUI`.

---

### 💡 Pistes d'amélioration

- **[Refactoring — cycle Scripting]** Déplacer `IGameplayProxy` dans `Entities/` et retirer `GameplayProxy Clone()` du contrat (remplacer par `IGameplayProxy Clone()`).
- **[Réflexion — Entities→GUI]** Évaluer si `ScreenWidgetComponent` doit rester dans `Entities/` ou migrer dans `GUI/`.

---

## 5. Entity-Component System analysis

The ECS is inspired by Unreal Engine (entity + components, not pure ECS with archetypes).

- [x] **5.1** Review `Entity.cs` — what does it hold? Is it a lean container for components, or a monolithic class with too many responsibilities?
- [x] **5.2** Review `EntityComponent.cs` — is the base component well-defined (lifecycle: Init, Update, Draw, Destroy)?
- [x] **5.3** Catalog all components in `Framework/Entities/Components/` — are they single-responsibility? Are there components trying to do too much?
- [x] **5.4** Check `SceneComponent.cs` vs `EntityComponent.cs` — is there a clear hierarchy (SceneComponent = spatial, EntityComponent = abstract)? Like Unreal's ActorComponent vs SceneComponent?
- [x] **5.5** Review `PrimitiveComponent.cs` — does it properly extend SceneComponent for renderable primitives?
- [x] **5.6** Check collision components (Box, Sphere, Capsule, Circle, Cylinder, Box2d) — are they consistent, do they share a common base? Is there code duplication?
- [x] **5.7** Review camera components (CameraComponent, Camera3dComponent, ArcBallCameraComponent, CopyTargeted2d, CameraLookAtComponent) — is the camera hierarchy clean or over-engineered?
- [x] **5.8** Check `ChildActorComponent.cs` — is the parent/child entity relationship well implemented?
- [x] **5.9** Evaluate `ICollideableComponent.cs` and `IComponentDrawable.cs` — are interfaces used properly to decouple systems?
- [x] **5.10** Check `EntityReference.cs` — how are cross-entity references handled? Is it safe (weak refs, IDs) or fragile (direct pointers)?

---

### Résultats — Tâche 5 : Analyse du système ECS

---

### ✅ Bien implémenté

#### Hiérarchie miroir de Unreal Engine

```
ObjectBase (namespace CasaEngine.Framework)
└── EntityComponent  (abstract — sans transform, comme UActorComponent)
    ├── ScreenWidgetComponent
    ├── PlayerStartComponent
    ├── Physics2dComponent
    └── SceneComponent  (abstract — avec transform, comme USceneComponent)
        ├── CameraComponent  (abstract — lazy ViewMatrix/ProjectionMatrix)
        │   ├── Camera3dComponent
        │   ├── Camera3dIn2dAxisComponent
        │   ├── ArcBallCameraComponent
        │   ├── CameraLookAtComponent
        │   └── CameraTargeted2dComponent
        ├── ChildActorComponent
        └── PrimitiveComponent  (abstract — représentation géométrique, comme UPrimitiveComponent)
            ├── StaticMeshComponent
            ├── SkinnedMeshComponent
            ├── StaticSpriteComponent
            ├── AnimatedSpriteComponent
            ├── TileMapComponent
            ├── ArrowComponent
            └── PhysicsBaseComponent  (abstract)
                ├── BoxCollisionComponent
                ├── SphereCollisionComponent
                ├── CapsuleCollisionComponent
                └── CylinderCollisionComponent
```

Les commentaires dans les fichiers citent explicitement les classes UE (`UActorComponent`, `USceneComponent`, `UPrimitiveComponent`) — l'intention architecturale est documentée.

#### EntityComponent

- Lifecycle clair : `Attach(Entity)` / `Detach()` / `InitializeWithWorld()` / `Update()` / `Clone()`.
- `Clone()` abstract : chaque composant est responsable de sa propre copie.

#### SceneComponent

- Hiérarchie parent/enfant via `Parent` + `List<SceneComponent> Children` — identique à UE.
- Calcul de `WorldMatrixWithScale` / `WorldMatrixNoScale` par chaînage jusqu'aux parents.

#### CameraComponent

- Lazy computation : `_needToComputeViewMatrix` / `_needToComputeProjectionMatrix` — recalcul uniquement quand nécessaire.

#### Composants de collision

- Tous héritent de `PhysicsBaseComponent : SceneComponent, ICollideableComponent`.
- Pattern uniforme : `ConvertToCollisionShape()` abstract + `ComputeBoundingBox()` abstract.
- `ICollideableComponent` : contrat minimal (`Owner`, `PhysicsType`, `Collisions`) — découple le système physique des composants concrets.

#### EntityReference

- Approche hybride explicite : `AssetId == Guid.Empty` → entité inline dans le world JSON ; sinon référence par GUID + `InitialCoordinates`. Design documenté par des commentaires.

#### IComponentDrawable

- Interface minimale : `GetBoundingBox()` + `Draw(float)`. Implémentée par `SceneComponent` — toute scène est dessinable et a une bounding box.

#### ChildActorComponent

- Pattern simple et direct : enveloppe une `Entity?` enfant dans un composant. Cohérent avec la hiérarchie.

---

### ❌ Erreurs identifiées

#### `IGameplayProxy` dans `Scripting/` — cycle résiduel (majeur)

`IGameplayProxy.cs` (dans `Scripting/`) déclare `GameplayProxy Clone()` — type concret. L'interface retourne sa propre implémentation concrète, ce qui viole le principe d'inversion des dépendances et maintient le cycle `Entities ↔ Scripting`.

```csharp
// Problème dans IGameplayProxy.cs :
GameplayProxy Clone();  // retourne le type concret → dependency circulaire
```

**Fix** : remplacer par `IGameplayProxy Clone()` dans l'interface.

#### `SceneComponent.cs` — classe volumineuse (mineur)

399 lignes gérant : transform + parent-child + WorldMatrix (3 variantes) + BoundingBox + Draw + editor `ITransformable`. Plusieurs responsabilités agrégées dans une seule classe. Acceptable pour une base ECS, mais tend vers un god object.

#### `PhysicsBaseComponent` — dépendance directe à BulletSharp (acceptable dans Framework)

`PhysicsBaseComponent` importe `using BulletSharp` directement (`RigidBody`, `CollisionObject`, `CollisionShape`). C'est **acceptable** — `PhysicsBaseComponent` est dans Framework qui peut utiliser BulletSharp. Ce n'est pas une violation de couche.

#### `StaticMeshComponent` résout son renderer via `world.Game.GetGameComponent<>()` (mineur)

Les composants mesh récupèrent leur renderer dans `InitializeWithWorld()` en appelant `world.Game.GetGameComponent<StaticMeshRendererComponent>()`. C'est un couplage au système de rendu concret — fonctionne, mais difficile à tester ou à remplacer le renderer.

---

### 💡 Pistes d'amélioration

- **[Quick win — cycle Scripting]** Dans `IGameplayProxy`, remplacer `GameplayProxy Clone()` par `IGameplayProxy Clone()`. Casse le dernier lien concret vers `Scripting` depuis l'interface.
- **[Refactoring moyen — Scripting]** Déplacer `IGameplayProxy` de `Scripting/` dans `Entities/` — `Scripting.GameplayProxy` l'implémente, `Entities.Entity` l'utilise, sans dépendance inverse.
- **[Réflexion — GetGameComponent dans InitializeWithWorld]** Envisager un système d'injection de dépendances léger (passer les services au constructeur ou via une interface `IWorldServices`) plutôt que de résoudre les GameComponents dynamiquement.

---

## 6. GameFramework analysis (Unreal-style gameplay layer)

- [x] **6.1** Review `GameMode.cs`, `Player.cs`, `LocalPlayer.cs`, `Pawn.cs`, `Controller.cs`, `PlayerController.cs`, `AIController.cs` — does this follow the Unreal pattern properly?
- [x] **6.2** Check if GameMode is properly separated from World management.
- [x] **6.3** Check if Controller/Pawn possession model works cleanly.
- [x] **6.4** Check if there is tight coupling between GameFramework and specific component types.

---

### Résultats — Tâche 6 : Analyse GameFramework (couche gameplay Unreal-style)

---

### ✅ Bien implémenté

#### Hiérarchie des classes — conforme au pattern Unreal

```
ObjectBase
├── GameMode           (machine à états de match : EnteringMap → WaitingToStart → InProgress → WaitingPostMatch → LeavingMap)
├── Player             (représente un joueur, physique ou réseau)
│   └── LocalPlayer    (joueur local, ControllerId: PlayerIndex)
└── Controller         (non-physique, pilote un Pawn)
    ├── PlayerController   (humain + Player ref)
    └── AIController       (IA)

Entity
└── Pawn               (représentation physique d'un joueur ou créature, InputEnabled, Controller ref)
```

Les classes sont correctement documentées (liens vers la doc UE dans les commentaires).

#### 6.2 — GameMode séparé de World ✅

`World.cs` ne contient **aucune référence** à `GameFramework`. `GameMode` stocke une ref `World.World World { get; private set; }` (sens normal : le mode de jeu connaît le monde où il s'exécute). La séparation est propre.

#### 6.4 — Couplage GameFramework → composants ✅

`GameFramework/` ne dépend que de `Entities` (via `Pawn : Entity`). Aucune dépendance vers des composants spécifiques (`Game.Components`, `Graphics`, etc.). Propre.

---

### ❌ Erreurs identifiées

#### `GameFramework` — couche entièrement non câblée (majeur)

**Aucun fichier hors de `GameFramework/` ne référence ce namespace.** Les 7 classes sont du code mort en production :

| Classe | État |
|---|---|
| `GameMode` | 442 lignes dont ~250 commentées (code C++ UE traduit). `ReadyToStartMatch()` retourne toujours `false` → le match ne commence jamais. |
| `Controller` | 1 propriété : `Pawn Pawn { get; set; }`. Pas de méthode `Possess()` / `UnPossess()`. |
| `PlayerController` | 2 propriétés : `Player`, `IsInputEnable`. Aucune logique. |
| `AIController` | Corps entièrement commenté (stubs C++). Classe vide. |
| `Player` | Classe vide. |
| `LocalPlayer` | 1 propriété : `ControllerId`. |
| `Pawn` | `InputEnabled` + `Controller` ref. Pas de méthode de possession. |

#### 6.3 — Modèle Possess/UnPossess non implémenté (majeur)

`Controller.Possess()` n'existe pas. Le lien Controller↔Pawn est une simple propriété assignée à la main. Aucun appel à `Possess` / `UnPossess` dans toute la codebase.

---

### 💡 Pistes d'amélioration

- **[Réflexion — code aspirationnel]** Décider si ce layer doit être activement développé ou rester un scaffold pour l'avenir. Si non prioritaire, ajouter un commentaire `// SCAFFOLD - not yet integrated` dans chaque fichier pour éviter la confusion.
- **[Quick win si activé]** Ajouter `Possess(Pawn pawn)` / `UnPossess()` sur `Controller` — 5 lignes pour rendre le modèle fonctionnel.
- **[Quick win si activé]** Câbler `GameMode` dans `World.InitGame()` et dans `CasaEngineGame` pour que la machine à états de match s'exécute réellement.
- **[Nettoyage]** Supprimer les 200+ lignes de code C++ commenté dans `GameMode.cs` — elles n'apportent rien et alourdissent la lisibilité.

---

## 7. Rendering system analysis

- [x] **7.1** Review renderer components (`SpriteRendererComponent`, `Line3dRendererComponent`, `SkinnedMeshRendererComponent`, `StaticMeshRendererComponent`, `Renderer2DComponent`) — do they follow a consistent pattern?
- [x] **7.2** Evaluate `IViewFlushableRenderer` — is the flush interface well-defined and consistently implemented across all renderers?
- [x] **7.3** Review the `Framework/Rendering/` folder — is the multi-view rendering pipeline (RenderView, RenderPipeline, ViewManager, IRenderSurface) clean and well-abstracted?
- [x] **7.4** Check if rendering code in `Framework/Graphics/` (StaticMesh, SkinnedMesh, RiggedModel) is properly separated from the component system.
- [x] **7.5** Evaluate `Framework/Graphics2D/` — is 2D rendering well-separated from 3D? Is `Renderer2DComponent` redundant with `SpriteRendererComponent`?
- [x] **7.6** Check `Framework/Materials/` — is the material system (Material, MaterialAsset, ShaderWriter) well-designed? Is `ShaderWriter2` a code smell (copy for v2)?

### Résultats de l'analyse — Task 7

**7.1 Renderer components**

✅ `SpriteRendererComponent`, `Line3dRendererComponent`, `StaticMeshRendererComponent`, `SkinnedMeshRendererComponent` : tous dans `Game/Components/`, tous `DrawableGameComponent + IViewFlushableRenderer`, pattern accumulate→`Flush(frame)` cohérent. Pools pré-alloués (`NbSprites=10000`, `NbLines=5000`).

⚠️ `StaticMeshRendererComponent` et `SkinnedMeshRendererComponent` : **valeurs d'éclairage hardcodées** en constantes magiques dans `LoadContent` / `Flush()` (3 lumières directionnelles RGB hardcodées, coefficients ambiants/diffus/speculaires fixes). Ces valeurs doivent passer dans un `LightingSettings` ou une propriété configurable.

❌ `Renderer2DComponent` : **n'implémente pas `IViewFlushableRenderer`**. Non-compatible avec le pipeline multi-vue. Localisé dans `Graphics2D/` alors que tous les autres renderers sont dans `Game/Components/`.

**7.2 IViewFlushableRenderer**

✅ Interface propre, méthode unique `void Flush(in RenderFrame frame)`. Bien définie.

❌ `Renderer2DComponent` manquant → impossible de l'intégrer dans `RenderPipeline` sans modification.

**7.3 Framework/Rendering/ (27 fichiers)**

✅ Architecture multi-vue solide et bien documentée :
- `RenderFrame` : struct readonly (View, Projection, ViewProjection, CameraPosition, ViewportRect) — transfert zéro-allocation des données caméra
- `IRenderSurface` / `BackBufferSurface` / `RenderTargetSurface` : abstraction propre backbuffer↔render target (Apply/Restore)
- `RenderView` : lifecycle complet (Enabled, IsVisible, UpdateMode, ResolutionScale, Id, hooks Pipeline/Presenter/Host)
- `RenderPipeline` : trie les vues RT avant backbuffer, gère throttled/on-demand/realtime, `GraphicsStateGuard`, `DebugOverlay` optionnel
- `ViewManager` : stable `ViewId`, events (ViewAdded/Removed/Resized/Invalidated), AutoLayoutMode, ScreenToView, CaptureInput

✅ Les commentaires XML sont détaillés et explicites — c'est du code de production bien pensé.

**7.4 Framework/Graphics/**

✅ `StaticMesh`, `SkinnedMesh`, `RiggedModel` : conteneurs de données mesh uniquement. Dépendances : `Core.Serialization`, `Engine.Primitives3D`, `Framework.Assets`. Aucun couplage avec `DrawableGameComponent` ou le système de composants. Séparation correcte modèle/renderer.

**7.5 Framework/Graphics2D/**

⚠️ `Renderer2DComponent` (469 lignes) gère sprites 2D + texte + lignes 2D — recouvrement partiel avec `SpriteRendererComponent` (399 lignes, sprites 3D). Ces deux classes ne sont pas strictement redondantes (la 2D gère `SpriteBatch` screen-space + texte + scissor) mais la séparation n'est pas formalisée.

❌ Problèmes identifiés :
1. `Renderer2DComponent` ne peut pas être utilisé dans le pipeline multi-vue (pas d'`IViewFlushableRenderer`)
2. Convention de nommage incohérente : `Renderer2dComponent` (d minuscule) vs `SpriteRendererComponent`
3. Localisation incohérente : devrait être dans `Game/Components/` comme les autres renderers

**7.6 Framework/Materials/**

✅ `ShaderWriter` : implémente `IMaterialAssetVisitor`, compile un `Material` (arbre d'assets) — design visitor correct.

⚠️ `ShaderWriter2` : **code smell de nommage**. N'implémente pas `IMaterialAssetVisitor`, compile un `MaterialGraph` (système de nœuds graphe). Fonctionnellement différent de `ShaderWriter`, mais le nom `ShaderWriter2` suggère une copie incrémentale alors qu'il s'agit d'un compilateur pour un système de matériaux différent. **Doit être renommé `ShaderGraphWriter` ou `MaterialGraphCompiler`**.

### Violations identifiées — Task 7

| # | Sévérité | Fichier | Problème |
|---|---|---|---|
| V7-1 | 🔴 | `Graphics2D/Renderer2DComponent.cs` | N'implémente pas `IViewFlushableRenderer` — incompatible multi-vue |
| V7-2 | 🟡 | `Graphics2D/Renderer2DComponent.cs` | Mauvais répertoire (`Graphics2D/` au lieu de `Game/Components/`) |
| V7-3 | 🟡 | `Graphics2D/Renderer2DComponent.cs` | Nommage incohérent (`Renderer2dComponent` vs `Renderer`) |
| V7-4 | 🟡 | `StaticMeshRendererComponent.cs` | Valeurs d'éclairage hardcodées (constantes magiques) |
| V7-5 | 🟡 | `SkinnedMeshRendererComponent.cs` | Valeurs d'éclairage hardcodées (constantes magiques) |
| V7-6 | 🟡 | `Materials/ShaderWriter2.cs` | Nommage trompeur — compiler GrapheMatériau ≠ ShaderWriter v2 |

### Corrections recommandées — Task 7

1. **V7-1 (prioritaire)** : Faire implémenter `IViewFlushableRenderer` à `Renderer2DComponent` — déplacer la logique de rendu dans `Flush(in RenderFrame frame)`, conserver `Draw()` comme fallback temporaire.
2. **V7-2** : Déplacer `Renderer2DComponent.cs` dans `Game/Components/` (renommer classe en `Renderer2DComponent` avec majuscule).
3. **V7-4/V7-5** : Extraire les constantes d'éclairage hardcodées vers une classe `DefaultLightingSettings` (const fields ou propriétés configurables).
4. **V7-6** : Renommer `ShaderWriter2` → `ShaderGraphWriter` (ou `MaterialGraphCompiler`).

---

## 8. Game management & lifecycle

- [x] **8.1** Review `GameManager.cs` — is it a clean orchestrator or a god object with too many static/global responsibilities?
- [x] **8.2** Review `CasaEngineGame.cs` — is the game loop well structured (Initialize, LoadContent, Update, Draw)?
- [x] **8.3** Check `ComponentOrder.cs` — is draw/update ordering explicit and manageable?
- [x] **8.4** Check `GameSettings.cs` / `GraphicsSettings.cs` — clean separation of concerns?
- [x] **8.5** Check `GameExtension.cs` — what extension points exist, are they well-defined?

### Résultats de l'analyse — Task 8

**8.1 GameManager.cs (154 lignes)**

✅ Pas un god object. Responsabilités uniques et cohérentes : cycle de vie du monde (chargement, `UpdateWorld`, `SetWorldToLoad`) + `ViewManager`. Aucun état statique — instance propre. Sections `#if EDITOR` bien délimitées (caméra éditeur, callback `CreateCameraComponentCallback`).

**8.2 CasaEngineGame.cs (463 lignes)**

✅ Game loop bien structuré (Initialize → LoadContent → Update → Draw). Pattern multi-vue intégré proprement dans `Draw()` :
- Phase 1 : composants DrawOrder < MeshComponent (UI setup)
- Phase 2 : `_renderPipeline.Render()` (pipeline 3D multi-vue)
- Phase 3 : composants DrawOrder ≥ MeshComponent (overlays, debug physics, etc.)
- Hook `AfterRenderPipeline()` pour les classes dérivées ✅
- `OnScreenResized()` proprement délégué à tous les composants + ViewManager ✅

❌ **Chemin de police hardcodé Windows-only** : `@"C:\\Windows\\Fonts\\Tahoma.ttf"` dans `Initialize()`. Violation cross-platform, doit utiliser une police livrée dans `Content/`.

⚠️ `RegisterLoaders()` contient 12+ enregistrements inline. Acceptable, mais pourrait être externalisé dans une classe dédiée `AssetLoaderRegistry` pour faciliter l'extension.

**8.3 ComponentOrder.cs**

✅ Deux enums explicites (`ComponentUpdateOrder`, `ComponentDrawOrder`) avec valeurs auto-incrémentées. Clair et maintenable. Extension `#if EDITOR` propre pour l'ordre éditeur.

**8.4 GameSettings.cs**

⚠️ Classe statique singleton avec 4 sous-objets : `ProjectSettings`, `AssemblyManager`, `GraphicsSettings`, `PhysicsEngineSettings`. Crée un état global mutable partagé. Acceptable pour un moteur de jeu mais introduit un couplage implicite (tout le code peut modifier les settings sans passer par injection). À documenter comme contrainte acceptée.

**8.5 GameExtension.cs**

✅ Extension methods utilitaires propres pour `Microsoft.Xna.Framework.Game` : `GetService<T>`, `GetGameComponent<T>`, `GetDrawableGameComponent<T>`, `RemoveGameComponent<T>`, `EnableAllGameComponent`, `SetVisibleAllDrawableGameComponent`. Bien conçues, sans couplage moteur.

### Violations identifiées — Task 8

| # | Sévérité | Fichier | Problème |
|---|---|---|---|
| V8-1 | 🔴 | `Game/CasaEngineGame.cs` | Chemin Windows absolu hardcodé : `@"C:\\Windows\\Fonts\\Tahoma.ttf"` |
| V8-2 | 🟡 | `Game/GameSettings.cs` | Singleton statique global — acceptable mais à documenter comme contrainte |

---

## 9. Asset system analysis

- [x] **9.1** Review `AssetContentManager.cs` / `AssetContentManagerAdapter.cs` — is the asset loading pipeline clean?
- [x] **9.2** Review `IAssetable`, `IAssetLoader`, `AssetLoader`, `AssetSaver` — are assets loaded/saved uniformly?
- [x] **9.3** Check `AssetCatalog.cs` / `AssetInfo.cs` — is there a proper manifest system?
- [x] **9.4** Evaluate specialized asset folders (`Animations/`, `Fonts/`, `Sprites/`, `Textures/`, `TileMap/`) — are they consistent in structure?
- [x] **9.5** Check `ElementLoader.cs` — is this a generic loader or a misplaced utility?

### Résultats de l'analyse — Task 9

**9.1 AssetContentManager.cs (265 lignes)**

✅ Cache d'assets par catégorie (double index Guid+Name via `AssetDictionary`). Chargeurs typés enregistrables (`RegisterAssetLoader`). `Unload(category)` + `UnloadAll()` avec `IDisposable`. Récupération de device via `IAssetable.OnDeviceReset`. Code clair et cohérent.

⚠️ Méthode `Load<T>` : commentaire `//TODO entity can be cache ?` non résolu — les `Entity` ne sont jamais cachées (contournement hardcodé par type `typeof(Entity)`).

**9.2 IAssetable / IAssetLoader / AssetLoader**

✅ `IAssetLoader` minimal (2 méthodes : `LoadAsset`, `IsFileSupported`). `AssetLoader<T>` générique: parse JSON + `ISerializable.Load(JObject)`. Extension propre par type spécialisé (`Texture2DLoader`, `EffectLoader`, etc.).

**9.3 AssetCatalog / AssetInfo**

✅ `AssetInfo` : `Id (Guid)`, `Name`, `FileName` — `ISerializable`, `IEquatable<AssetInfo>`. Manifest JSON bien structuré.

❌ `AssetCatalog` : classe **statique** (état global). `Get(string name)` et `GetByFileName(string)` font une **recherche linéaire O(N)** via `FirstOrDefault`. Avec des projets contenant >500 assets, c'est une régression de performance à chaque chargement.

**9.4 Dossiers d'assets spécialisés**

✅ Structure cohérente entre `Animations/`, `Sprites/`, `Textures/`, `TileMap/` : chaque sous-dossier a ses Data, Loader et types métier.

❌ `Animations/RiggedModelLoader.cs` : **1616 lignes** — God Class évidente. Un loader de 1600 lignes indique des responsabilités multiples (parsing, conversion, UV-unwrapping, bone extraction). Doit être décomposé.

**9.5 ElementLoader.cs**

❌ Nom de fichier `ElementLoader.cs` mais classe interne nommée `ElementFactory` — **mismatch de nommage**.

❌ `FindTypeByName()` : scan de **tous les assemblies chargés** (`AppDomain.CurrentDomain.GetAssemblies()`) à chaque appel. Un `TODO: cache types` est en commentaire depuis longtemps mais non implémenté. En production avec des centaines d'entités à charger, c'est une régression critique.

### Violations identifiées — Task 9

| # | Sévérité | Fichier | Problème |
|---|---|---|---|
| V9-1 | 🔴 | `Assets/ElementLoader.cs` | Nom de fichier ≠ nom de classe (`ElementFactory`) |
| V9-2 | 🔴 | `Assets/ElementLoader.cs` | `FindTypeByName()` non-caché — scan O(N×A) à chaque deserialisation |
| V9-3 | 🔴 | `Animations/RiggedModelLoader.cs` | God Class 1616 lignes — doit être décomposée |
| V9-4 | 🟡 | `Assets/AssetCatalog.cs` | `GetByFileName`/`Get(string name)` : recherche linéaire O(N) |
| V9-5 | 🟡 | `AssetContentManager.cs` | TODO `entity can be cache ?` non résolu — contournement hardcodé par type |

### Corrections recommandées — Task 9

1. **V9-1** : Renommer `ElementLoader.cs` → `ElementFactory.cs`.
2. **V9-2** : Ajouter un `Dictionary<string, Type> _typeCache` statique dans `ElementFactory.FindTypeByName()`.
3. **V9-3** : Décomposer `RiggedModelLoader.cs` en `BoneExtractor`, `MeshConverter`, `AnimationParser`, etc.
4. **V9-4** : Ajouter `Dictionary<string, AssetInfo> _byName` et `Dictionary<string, AssetInfo> _byFileName` dans `AssetCatalog`.

---

## 10. AI system analysis

- [ ] **10.1** Review overall AI structure — there are 12+ sub-modules (BehaviourTree, FuzzyLogic, Graphs, Pathfinding, StateMachines, NeuralNets, Reinforcement Learning, etc.). Are they all actually used or is this dead/aspirational code?
- [ ] **10.2** Check if AI modules depend on engine/framework types or stay self-contained.
- [ ] **10.3** Evaluate `Navigation/` and `Pathfinding/` — is there duplication between them?
- [ ] **10.4** Check if `Messaging/` and `Goals/` are properly integrated with the entity system.

---

## 11. Physics integration analysis

- [ ] **11.1** Review how `Engine/Physics/` (PhysicsEngine, PhysicsDefinition) connects with `Framework/Entities/Components/` collision components.
- [ ] **11.2** Check `Physics2dComponent` and `PhysicsBaseComponent` — is there a clean 2D/3D physics split?
- [ ] **11.3** Evaluate `Framework/Game/Components/Physics/` — what lives here vs in Engine/Physics?
- [ ] **11.4** Check if physics callbacks (EventCollisionArgs, ContactPoint) are well-typed and not using raw objects.

---

## 12. World & space partitioning analysis

- [ ] **12.1** Review `World.cs` — is it a clean container for entities or does it have rendering/update logic mixed in?
- [ ] **12.2** Review `SpacePartitioning/Octree/` — is it generic enough, does it depend on entity types?
- [ ] **12.3** Check how entities are added/removed from the world — is there a lifecycle (spawn, attach, detach, destroy)?

---

## 13. GUI system analysis

- [ ] **13.1** Review `Framework/GUI/` — is `Neoforce/` a third-party lib or custom code?
- [ ] **13.2** Review `ScreenGui.cs` — how does it integrate with the entity system?
- [ ] **13.3** Check `ScreenWidgetComponent` (in Entities/Components) — does the GUI properly use the component pattern?

---

## 14. Debugger & diagnostics analysis

- [ ] **14.1** Review `Framework/Debugger/` — is DebugSystem/DebugManager well-separated from production code?
- [ ] **14.2** Check if debug code uses `#if DEBUG` or can be stripped in Release builds.
- [ ] **14.3** Evaluate FpsCounter, TimeRuler — are they reusable or tightly coupled?

---

## 15. Cross-cutting concerns

- [ ] **15.1** Review how `static` / `singleton` patterns are used across the codebase (GameManager, AssetContentManager, etc.) — are they appropriately scoped or creating hidden global state?
- [ ] **15.2** Check for proper use of interfaces vs concrete types in public APIs.
- [ ] **15.3** Check for proper separation of editor-only code (`CasaEngine.WithEditor.csproj` vs `CasaEngine.csproj`) — are there `#if EDITOR` guards?
- [ ] **15.4** Check naming conventions consistency across layers.
- [ ] **15.5** Evaluate if the project could benefit from splitting into multiple assemblies (CasaEngine.Core.dll, CasaEngine.Engine.dll, CasaEngine.Framework.dll) to enforce layer boundaries at compile time.

---

## 16. Summary & recommendations

- [ ] **16.1** Create a dependency violation report listing every case where a lower layer references a higher layer.
- [ ] **16.2** Create a "god class" report for classes with too many responsibilities or too many lines.
- [ ] **16.3** Create a dead code / unused feature report (especially in AI/).
- [ ] **16.4** Propose a namespace restructuring plan if violations are found.
- [ ] **16.5** Propose concrete next steps prioritized by impact (quick wins vs large refactors).
