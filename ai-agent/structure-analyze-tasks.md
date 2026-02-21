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

### Résultats — Tâche 4 : Analyse structurelle de la couche Framework

---

### ✅ Bien implémenté

#### 4.1 — Inventaire des modules Framework

| Module | Rôle | Catégorie |
|---|---|---|
| `AI/` | BehaviorTree, FSM, NeuralNets, Steering, Pathfinding, Messaging | Feature — IA |
| `Assets/` | Animation, fonts, loaders (Assimp), sprites, textures, tiles | Feature — Assets |
| `Audio/` | Sons, musiques | Feature — Audio |
| `Constants.cs` | Extensions de fichiers (.entity, .world, .material…) | Utilitaire |
| `Debugger/` | Debug visuel, DebugManager | Utilitaire |
| `Entities/` | ECS : `Entity`, composants (Camera, Physics, Mesh, Input…) | Cœur — ECS |
| `Game/` | `CasaEngineGame`, `GameManager`, GameComponents | Cœur — Runtime |
| `GameFramework/` | `DemoGame`, `SceneManagement`, `GameMode`, `Player`, `Controller` | Feature — Gameplay |
| `Graphics/` | `RiggedModel`, shapes 3D | Feature — Rendu 3D |
| `Graphics2D/` | Sprites, tilemaps, rendu 2D | Feature — Rendu 2D |
| `GUI/` | Interface MGUI, screens | Feature — UI |
| `Input/` | `InputComponent`, `InputManager` (wrapper MonoGame) | Système |
| `Materials/` | `Material`, `MaterialAsset`, `ShaderWriter` | Feature — Matériaux |
| `Objects/` | `ObjectBase` (Id, Name, AssetId, Initialize/Load/Save) | Utilitaire — Base |
| `Physics/` | Types de collision, `Collision`, `EventCollisionArgs` | Feature — Physique |
| `Project/` | `ProjectSettings` | Utilitaire |
| `Rendering/` | `RenderFrame`, `IRenderSurface`, `RenderPipeline`, `ViewManager` | Système — Multi-vues |
| `Scripting/` | `GameplayProxy` (base), `ScriptArcBallCamera` (concret) | Feature — Scripts |
| `SpacePartitioning/` | `Octree`, queries spatiales | Système |
| `World/` | `World` : container d'entités, gestion de scène | Feature — Monde |

#### 4.3 — ObjectBase

- Classe de base minimale et propre : `Id` (Guid), `Name`, `FileName`, `AssetId` + `Initialize()` / `Load()` / `Save()`.
- N'entre **pas** en conflit avec l'ECS : ce n'est pas une entité, c'est une base identitaire pour tous les assets/objets persistables. La hiérarchie `ObjectBase → Entity` est cohérente.
- `Save()` correctement protégé par `#if EDITOR`.

#### 4.4 — Scripting/

- Seulement 2 fichiers — pas un "dumping ground".
- `GameplayProxy` : pattern gameplay clair (comparable aux Blueprints Unreal en plus léger) — Owner, lifecycle callbacks (Update, Draw, OnHit, OnBeginPlay…).
- `ScriptArcBallCamera` : script concret, bien ciblé, dépendances légitimes (`Entities.Components`, `Game`, `Input`).

---

### ❌ Erreurs identifiées

#### `Entities → Scripting` — dépendance cyclique (majeur)

`Entity.cs` possède une propriété `GameplayProxy?` (type `Scripting.GameplayProxy`) et l'instancie via `ElementFactory.Create<GameplayProxy>()`. Or `Scripting.GameplayProxy` dépend de `Entities.Entity`. Cela crée un cycle :

```
Entities → Scripting → Entities
```

Le compilateur l'accepte (même assembly), mais ce cycle empêche de séparer proprement les deux namespaces et complique les tests unitaires.

**Correction suggérée** : extraire l'interface `IGameplayProxy` dans `Entities/` — `Entity` dépend de l'interface, `Scripting.GameplayProxy` l'implémente.

#### `Entities → GUI` — couplage discutable (mineur)

`ScreenWidgetComponent.cs` importe `using CasaEngine.Framework.GUI`. Sémantiquement acceptable (composant qui embarque un widget UI dans l'espace 3D), mais `Entities` dépend désormais de `GUI`, ce qui inverse le sens naturel (GUI devrait dépendre d'Entities pour accéder aux données à afficher).

#### `SpacePartitioning → Game` — couplage trop haut (mineur)

Un module de structure spatiale pure (Octree) ne devrait pas dépendre de la couche `Game`. Probablement pour accéder à `GameManager` ou `GraphicsDevice`. Idéalement, `SpacePartitioning` ne devrait dépendre que d'`Entities` (pour requêter des entités) et de Core/MonoGame (pour les types géométriques).

#### `Objects/` — namespace trop fin (mineur)

`Framework/Objects/` ne contient qu'un seul fichier (`ObjectBase.cs`). Ce dossier/namespace est peut-être trop fin pour justifier son existence. `ObjectBase` pourrait vivre à la racine de `Framework/` ou dans `Entities/`.

---

### 💡 Pistes d'amélioration

- **[Refactoring ciblé — cycle Scripting/Entities]** Extraire `IGameplayProxy` dans `Entities/` — interface légère avec `Initialize(Entity)`, `Update(float)`, `Draw()`, `OnHit(Collision)`, `Clone()`. Quick win si `Entity.cs` ne garde que la ref à l'interface.
- **[Quick win — SpacePartitioning]** Supprimer la dépendance vers `Game` dans `SpacePartitioning/` — vérifier quel type précis est importé et le remplacer par un type plus bas niveau si possible.
- **[Réflexion — Objects/]** Si `ObjectBase` est la seule classe du namespace, envisager de la déplacer dans le namespace `CasaEngine.Framework` directement (fichier racine) ou dans `Entities/`.

---

## 5. Entity-Component System analysis

The ECS is inspired by Unreal Engine (entity + components, not pure ECS with archetypes).

- [ ] **5.1** Review `Entity.cs` — what does it hold? Is it a lean container for components, or a monolithic class with too many responsibilities?
- [ ] **5.2** Review `EntityComponent.cs` — is the base component well-defined (lifecycle: Init, Update, Draw, Destroy)?
- [ ] **5.3** Catalog all components in `Framework/Entities/Components/` — are they single-responsibility? Are there components trying to do too much?
- [ ] **5.4** Check `SceneComponent.cs` vs `EntityComponent.cs` — is there a clear hierarchy (SceneComponent = spatial, EntityComponent = abstract)? Like Unreal's ActorComponent vs SceneComponent?
- [ ] **5.5** Review `PrimitiveComponent.cs` — does it properly extend SceneComponent for renderable primitives?
- [ ] **5.6** Check collision components (Box, Sphere, Capsule, Circle, Cylinder, Box2d) — are they consistent, do they share a common base? Is there code duplication?
- [ ] **5.7** Review camera components (CameraComponent, Camera3dComponent, ArcBallCameraComponent, CopyTargeted2d, CameraLookAtComponent) — is the camera hierarchy clean or over-engineered?
- [ ] **5.8** Check `ChildActorComponent.cs` — is the parent/child entity relationship well implemented?
- [ ] **5.9** Evaluate `ICollideableComponent.cs` and `IComponentDrawable.cs` — are interfaces used properly to decouple systems?
- [ ] **5.10** Check `EntityReference.cs` — how are cross-entity references handled? Is it safe (weak refs, IDs) or fragile (direct pointers)?

---

## 6. GameFramework analysis (Unreal-style gameplay layer)

- [ ] **6.1** Review `GameMode.cs`, `Player.cs`, `LocalPlayer.cs`, `Pawn.cs`, `Controller.cs`, `PlayerController.cs`, `AIController.cs` — does this follow the Unreal pattern properly?
- [ ] **6.2** Check if GameMode is properly separated from World management.
- [ ] **6.3** Check if Controller/Pawn possession model works cleanly.
- [ ] **6.4** Check if there is tight coupling between GameFramework and specific component types.

---

## 7. Rendering system analysis

- [ ] **7.1** Review renderer components (`SpriteRendererComponent`, `Line3dRendererComponent`, `SkinnedMeshRendererComponent`, `StaticMeshRendererComponent`, `Renderer2DComponent`) — do they follow a consistent pattern?
- [ ] **7.2** Evaluate `IViewFlushableRenderer` — is the flush interface well-defined and consistently implemented across all renderers?
- [ ] **7.3** Review the `Framework/Rendering/` folder — is the multi-view rendering pipeline (RenderView, RenderPipeline, ViewManager, IRenderSurface) clean and well-abstracted?
- [ ] **7.4** Check if rendering code in `Framework/Graphics/` (StaticMesh, SkinnedMesh, RiggedModel) is properly separated from the component system.
- [ ] **7.5** Evaluate `Framework/Graphics2D/` — is 2D rendering well-separated from 3D? Is `Renderer2DComponent` redundant with `SpriteRendererComponent`?
- [ ] **7.6** Check `Framework/Materials/` — is the material system (Material, MaterialAsset, ShaderWriter) well-designed? Is `ShaderWriter2` a code smell (copy for v2)?

---

## 8. Game management & lifecycle

- [ ] **8.1** Review `GameManager.cs` — is it a clean orchestrator or a god object with too many static/global responsibilities?
- [ ] **8.2** Review `CasaEngineGame.cs` — is the game loop well structured (Initialize, LoadContent, Update, Draw)?
- [ ] **8.3** Check `ComponentOrder.cs` — is draw/update ordering explicit and manageable?
- [ ] **8.4** Check `GameSettings.cs` / `GraphicsSettings.cs` — clean separation of concerns?
- [ ] **8.5** Check `GameExtension.cs` — what extension points exist, are they well-defined?

---

## 9. Asset system analysis

- [ ] **9.1** Review `AssetContentManager.cs` / `AssetContentManagerAdapter.cs` — is the asset loading pipeline clean?
- [ ] **9.2** Review `IAssetable`, `IAssetLoader`, `AssetLoader`, `AssetSaver` — are assets loaded/saved uniformly?
- [ ] **9.3** Check `AssetCatalog.cs` / `AssetInfo.cs` — is there a proper manifest system?
- [ ] **9.4** Evaluate specialized asset folders (`Animations/`, `Fonts/`, `Sprites/`, `Textures/`, `TileMap/`) — are they consistent in structure?
- [ ] **9.5** Check `ElementLoader.cs` — is this a generic loader or a misplaced utility?

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
