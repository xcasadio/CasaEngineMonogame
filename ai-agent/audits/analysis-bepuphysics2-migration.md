# Analyse — Migration du backend physique vers bepuphysics2 2.5.0-beta.29

Date : 2026-08-22. Analyse en lecture seule, aucun code modifié. Sources vérifiées : le dépôt
CasaEngine (HEAD `e18b2282`) et les sources de bepuphysics2 au tag
[`2.5.0-beta.29`](https://github.com/bepu/bepuphysics2/releases/tag/2.5.0-beta.29)
(commit `f73164b`, publié le 2026-04-18 sur NuGet).

## Résumé

- **Le backend actuel est BulletSharp**, pas Bepu : une DLL vendorisée
  ([ThirdParties/BulletSharp/BulletSharp.dll](../../ThirdParties/BulletSharp/BulletSharp.dll),
  2,9 Mo, référencée par `HintPath` dans [CasaEngine.csproj:35](../../CasaEngine/CasaEngine.csproj)
  et [CasaEngine.Tests.csproj:35](../../CasaEngine.Tests/CasaEngine.Tests.csproj)).
  Le package `BepuPhysics 2.4.0` déclaré dans
  [Directory.Packages.props:9](../../Directory.Packages.props) et référencé par `CasaEngine.csproj`
  n'est **utilisé par aucun fichier** (`rg "Bepu"` sur `*.cs` : zéro résultat).
- **Le backend est déjà isolé** : seuls deux fichiers touchent BulletSharp —
  [BulletPhysicsEngine.cs](../../CasaEngine/Framework/Physics/BulletPhysicsEngine.cs) (1 714 lignes,
  dont ~300 de code mort commenté) et un commentaire dans
  [CollisionComponentTests.cs:199](../../CasaEngine.Tests/Physics/CollisionComponentTests.cs).
  Tout le reste du moteur passe par
  [IPhysicsWorld](../../CasaEngine/Framework/Application/Components/Physics/IPhysicsWorld.cs),
  [PhysicsBody](../../CasaEngine/Engine/Physics/PhysicsBody.cs) et
  [PhysicsQueryShape](../../CasaEngine/Engine/Physics/PhysicsQueryShape.cs), dont les backends sont
  des interfaces `internal`. **La migration est un remplacement de fichier, pas une refonte.**
- **Les tests existants servent de critère d'acceptation** : 161 tests physique verts sur HEAD
  (`dotnet test --filter FullyQualifiedName~CasaEngine.Tests.Physics`), dont 8 fichiers qui
  instancient le vrai `PhysicsWorld` (33 constructions) et exercent sweeps, contacts, tags de
  fixtures, profils, AABB, politiques d'espace.
- **Effort estimé** : un backend Bepu complet (~1 000 à 1 200 lignes) + un debug draw (Bepu n'en a
  pas) + adaptation de `PhysicsDefinition`. Découpé en 6 tranches ci-dessous. Le risque n'est pas
  l'API mais les **différences sémantiques** (contacts spéculatifs, sommeil des cinématiques, ordre
  des paires, échelle des formes, facteurs linéaires/angulaires) détaillées en §4.

## 1. État vérifié dans le dépôt

### Surface consommée par le moteur

`IPhysicsWorld` (71 lignes) expose exactement ce qui suit, et rien d'autre n'est utilisé en dehors
du backend :

| Catégorie | Membres | Consommateurs |
| --- | --- | --- |
| Corps | `AddGhostObject` ×2, `CreateGhostObject` ×2, `AddStaticObject` ×2, `AddRigidBody` ×2 (+ surcharges `PhysicsBody`), `AddCollisionObject`, `RemoveCollisionObject`, `AddRigidBody(PhysicsBody)`, `RemoveRigidBody`, `RefreshBodyAabb` | [PhysicsBaseComponent.cs](../../CasaEngine/Framework/Scene/Entities/Components/PhysicsBaseComponent.cs), [AnimatedSpriteComponent.cs:470-520](../../CasaEngine/Framework/Scene/Entities/Components/AnimatedSpriteComponent.cs), [StaticSpriteComponent.cs:203](../../CasaEngine/Framework/Scene/Entities/Components/StaticSpriteComponent.cs), [SpriteCollisionHelper.cs](../../CasaEngine/Framework/Scene/Entities/Components/SpriteCollisionHelper.cs), [TileMapComponent.cs:972-1060](../../CasaEngine/Framework/Scene/Entities/Components/TileMapComponent.cs) |
| Événements | `Update` (step + `UpdateContacts` + `SendEvents`), `ClearCollisionDataFrom`, `GetContactPoints` | `PhysicsWorld.Update`, composants, gameplay via `ICollideableComponent.Collisions` + `GameplayProxy.OnHit/OnHitEnded` |
| Requêtes | `CreateQueryShape`, `ShapeSweep` ×4, `ShapeSweepPenetrating` ×2 | [CharacterControllerComponent.cs:879-915](../../CasaEngine/Framework/Scene/Entities/Components/CharacterControllerComponent.cs) (capsule mise en cache, masque de canaux, `hitTriggers`, `ignoredComponent`) |
| Requêtes non implémentées | `WorldRayCast`, `NearBodyWorldRayCast` | `throw NotImplementedException` dans Bullet ; appelés par [MovingObject.cs:97-102](../../CasaEngine/Framework/AI/Navigation/MovingObject.cs) (steering `WallAvoidance`) |
| Debug | `DrawDebugWorld(IPhysicsDebugDrawer)` | [PhysicsDebugDrawComponent.cs](../../CasaEngine/Framework/Application/Components/Physics/PhysicsDebugDrawComponent.cs) → `Line3dRendererComponent` |
| Divers | `CollisionObjectCount`, `SpacePolicy` | éditeur (`MaterialPreviewViewport` : comptage d'objets debug) |

`PhysicsBody` (`IPhysicsBodyBackend`) : `IsRigidBody`, `IsCompound`, `FixtureCount`,
`GetFixtureLocalTransform`, `GetFixtureTag`, `HasContactResponse`, `WorldTransform` (get/set),
`LinearVelocity` (get/set), `ApplyImpulse`, `RefreshAabb`, `Dispose`.

Membres **publics de `BulletPhysicsEngine` sans aucun appelant** (à ne pas porter) :
`AddConstraint/RemoveConstraint(TypedConstraint)`, `Raycast/RaycastPenetrating` (privés de fait :
`PhysicsWorld` ne les relaie pas), `ClearForces`, `SpeculativeContactRestitution`,
`SimulationBegin/SimulationEnd`, `CurrentCollisions`, `IncludeStaticAgainstStaticCollisions`,
`ContinuousCollisionDetection` (propriété), `CreateCollisionShape` (static public).
[ConstraintTypes.cs](../../CasaEngine/Engine/Physics/ConstraintTypes.cs) n'a aucun usage.

### Modèle de corps actuel (Bullet)

- **Static** (`PhysicsType.Static`) → `RigidBody` masse 0 + `CollisionFlags.StaticObject`, sauf en
  mode éditeur (`useExternalViewManagement`) où le flag est omis pour pouvoir déplacer le corps.
- **Kinetic** (`PhysicsType.Kinetic`) → `PairCachingGhostObject` + `NoContactResponse` : un capteur
  déplacé par le gameplay via `WorldTransform`, jamais par vitesse. C'est le cas des hitboxes de
  sprites, du `CharacterControllerComponent` (qui bouge par sweeps) et des tuiles trigger.
- **Dynamic** → `RigidBody` masse > 0, `DefaultMotionState`, gravité par corps, `LinearFactor` /
  `AngularFactor` (utilisés par `Planar2dSimulationSpacePolicy` pour verrouiller le plan XY).
- **Capteur** : un profil dont `BlockMask == 0` → `NoContactResponse` ; le filtrage est **par corps**
  (`group`/`mask` broadphase), d'où un corps par profil dans `PhysicsBaseComponent` (`FixtureGroup`).
- **Compound** : plusieurs fixtures → `CompoundShape` ; les tags sont stockés dans
  `CollisionShape.UserObject` (`string[]`) et retrouvés via `ManifoldPoint.Index0/Index1`
  (contacts) ou `LocalShapeInfo.TriangleIndex` (requêtes — non rempli pour les compounds par le
  BulletSharp vendorisé, cf. test `ShapeSweep_ReportsNoTag_WhenItHitsACompoundBody`).
- **Cycle de vie découplé du monde** : `CreateGhostObject` crée un objet hors monde, puis
  `AddCollisionObject` / `RemoveCollisionObject` l'insèrent et le retirent à volonté
  (`AnimatedSpriteComponent` poole ses corps par keyframe et les bascule à chaque changement).
- **Échelle** : `CollisionShape.LocalScaling = localScale` (y compris sur le compound, propagé aux
  enfants et à leurs offsets).
- **Pas de step**, la boucle est `World.StepSimulation(dt, MaxSubSteps, FixedTimeStep)` ; les
  transformations lues sont `CollisionObject.WorldTransform` (non interpolées).
- **Bug latent** : `Cylinder` est abaissé en `new CylinderShape(cylinder.Radius)` — la longueur est
  ignorée ([BulletPhysicsEngine.cs:376](../../CasaEngine/Framework/Physics/BulletPhysicsEngine.cs)).
  `Cone` n'est pas supporté. À corriger au passage avec Bepu (`Cylinder(radius, length)`).

### Paramètres sérialisés spécifiques à Bullet

[PhysicsDefinition.cs](../../CasaEngine/Engine/Physics/PhysicsDefinition.cs) porte 13 champs copiés
de `RigidBodyConstructionInfo` : `AdditionalDamping*` (5 champs), `RollingFriction`,
`LinearSleepingThreshold`/`AngularSleepingThreshold`, `LocalInertia`, `LinearFactor`/`AngularFactor`.
Ils sont lus par `Load(JObject)` (clés `additional_damping`, `rolling_friction`, …) et écrits par
[EditorEntityJsonSerializer.SavePhysicsDefinition](../../CasaEngine.EditorServices/EditorEntityJsonSerializer.cs)
(ligne 503). **Six assets de `Projects/` sérialisent `physics_definition`** avec toutes ces clés :
`SampleProject/Entities/Box.entity`, `SampleProject/DefaultWorld.world`,
`RPGDemo/Entities/{weapon_rock,character_octopus,character_link}.entity`, `RPGDemo/DefaultWorld.world`.
Les démos C# construisent la définition en code. Les démos et projets qui touchent la physique :
`Collision2dBasicDemo`, `Collision3dBasicDemo`, `TileMapDemo`, `TopDownElevationDemo`,
`CutsceneMoveToDemo`, `CutsceneNavigateToDemo`, `PlayerComponent`, et `CasaEngine.RPGDemo`
(`Character.cs`, `ScriptPlayer*.cs`, `ScriptEnemyWeapon.cs`, qui lisent `Collision.ContactPoint`).

### Posture de compatibilité

[collision-2d-3d-architecture.md](../../docs/engine/collision-2d-3d-architecture.md) fixe la
posture projet (2026-08) : pas de rétrocompatibilité d'API ni d'assets, « on remplace, on ne
double pas », deux invariants — le dépôt compile et démos/tests passent à chaque phase. Le même
document prévoit explicitement que l'attribution par fixture et le filtrage par corps sont des
« détails d'implémentation du backend » — Bepu les lève (§3).

## 2. La cible : bepuphysics2 2.5.0-beta.29

Faits vérifiés (NuGet + sources au tag) :

- **Package** `BepuPhysics 2.5.0-beta.29`, dépendance `BepuUtilities >= 2.5.0-beta.29`. Cible
  `net8.0` (compatible `net9.0-windows`, la cible du moteur via
  [Directory.Build.props](../../Directory.Build.props)). Apache-2.0. 100 % managé, AVX/SIMD via
  `System.Numerics` : **plus de DLL native, plus de contrainte x64**, build reproductible.
- **Statut** : dernière version stable `2.4.0` (2022-02) ; `2.5.0-beta.29` est la **dernière
  préversion publiée** et celle que Stride (`Stride.BepuPhysics`) consomme. La ligne 2.5 est en bêta
  depuis 2022 ; l'API des callbacks a changé entre 2.4.0 et 2.5 (`IPoseIntegratorCallbacks` en
  version *wide*/SIMD, `SolveDescription` avec substeps). Le changement unique de cette release :
  « MeshReduction for not-just-Mesh » (#403) — sans incidence pour nous (pas de mesh).
  **Il faut viser 2.5.0-beta.29 directement**, pas 2.4.0 : l'API 2.4 est morte et le package 2.4.0
  référencé aujourd'hui ne sert à rien.
- **Types mathématiques** : `System.Numerics.Vector3/Quaternion` et `RigidPose { Position,
  Orientation }`. MonoGame 3.8.4 fournit `implicit operator` Numerics → XNA et `ToNumerics()`
  XNA → Numerics pour `Vector3`, `Quaternion`, `Matrix` (vérifié dans `MonoGame.Framework.xml`).
  Le moteur passe des `Matrix` sans échelle (`WorldMatrixNoScale`) : conversion
  `Matrix → (Translation, Quaternion.CreateFromRotationMatrix)` sans `Decompose`.
- **Threading** : `Simulation.Timestep(dt, IThreadDispatcher = null)` ; `BepuUtilities.ThreadDispatcher`
  est dans la bibliothèque. Mono-thread par défaut = déterministe = même modèle que Bullet
  aujourd'hui. Le flag `PhysicsEngineFlags.MultiThreaded` existe déjà pour brancher un dispatcher
  plus tard.
- **Pas de debug draw, pas d'événements de contact** dans la bibliothèque : les deux se
  construisent côté moteur à partir des callbacks (le dépôt Bepu fournit `ContactEventsDemo.cs`
  comme modèle, ~500 lignes, à adapter).

## 3. Correspondance concept par concept

| CasaEngine / Bullet | Bepu 2.5 | Note |
| --- | --- | --- |
| `DiscreteDynamicsWorld` + `DbvtBroadphase` + `SequentialImpulseConstraintSolver` | `Simulation.Create(BufferPool, INarrowPhaseCallbacks, IPoseIntegratorCallbacks, SolveDescription)` | Un `BufferPool` par `PhysicsWorld` (un par `World`, cf. [PhysicsSystemComponent.cs](../../CasaEngine/Framework/Application/Components/Physics/PhysicsSystemComponent.cs)) ; `Dispose` = `Simulation.Dispose()` + `BufferPool.Clear()`. |
| `StepSimulation(dt, maxSubSteps, fixedStep)` | `Timestep(fixedStep)` dans un accumulateur écrit par nous | Bepu lève si `dt <= 0` (déjà garanti par `PhysicsSystemComponent.Update`). Reproduire la sémantique Bullet : au plus `MaxSubSteps` pas de `FixedTimeStep`, reste perdu. `SolveDescription(velocityIterationCount: 8, substepCount: 1)` au départ. |
| `RigidBody` masse 0 + `StaticObject` | `Simulation.Statics.Add(StaticDescription(pose, shapeIndex))` | Déplacement : `Statics.ApplyDescription(handle, desc)` — réveille les corps dynamiques en contact (filtre `StaticsShouldntAwakenKinematics` par défaut). Couvre le cas éditeur sans astuce « masse 0 mobile ». |
| `PairCachingGhostObject` + `NoContactResponse` | `BodyDescription.CreateKinematic(pose, collidable, activity)` + flag capteur côté moteur | Le capteur n'existe pas dans Bepu : il se programme dans `ConfigureContactManifold` en **retournant `false`** (pas de contrainte) après avoir enregistré les contacts. |
| `RigidBody` masse > 0 | `BodyDescription.CreateDynamic(pose, inertia, collidable, activity)` avec `shape.ComputeInertia(mass)` | `LocalInertia` de `PhysicsDefinition` devient inutile (Bepu le calcule). |
| `Gravity` par corps, `ApplyGravity` | `IPoseIntegratorCallbacks.IntegrateVelocity` (wide) + table par corps | `bodyIndices` sont des index du set actif → `Bodies.ActiveSet.IndexToHandle` → table de flags par handle (`CollidableProperty<T>` est fourni par la bibliothèque). |
| `LinearDamping` / `AngularDamping` | Même callback : `velocity *= (1 - damping)^dt` par lane | Sémantique proche de Bullet (amortissement exponentiel). |
| `Friction`, `Restitution` | `PairMaterialProperties { FrictionCoefficient, MaximumRecoveryVelocity, SpringSettings }` dans `ConfigureContactManifold` | Pas de restitution au sens Bullet : `MaximumRecoveryVelocity` + `SpringSettings` (rigidité/amortissement). Combinaison par paire à définir (produit/moyenne). |
| `AngularFactor` (ex. `(0,0,1)` en Planar2d) | Annuler les composantes de `BodyInertia.InverseInertiaTensor` des axes verrouillés | Exact : le solveur respecte une inertie infinie. |
| `LinearFactor` (ex. `(1,1,0)`) | Pas d'équivalent natif : annuler la composante de vitesse dans `IntegrateVelocity` (et re-projeter la pose) | Le solveur peut réinjecter de la vitesse sur l'axe verrouillé pendant un pas → dérive bornée par `dt`. À valider par un test d'empilement Planar2d (cf. §5, tranche 4). Alternative exacte : contrainte `OneBodyLinearServo`, plus coûteuse. |
| `CollisionFlags.NoContactResponse` → `HasContactResponse` | Flag capteur dans la table par corps | |
| `group`/`mask` broadphase (`ResolvedCollisionProfile.GroupBit/BroadphaseMask`) | `AllowContactGeneration(workerIndex, a, b, ref speculativeMargin)` : `(maskA & groupB) != 0 && (maskB & groupA) != 0` | Identique en sémantique ; la table par collidable est lue depuis `CollidableProperty<ResolvedCollisionProfile>` (handles corps **et** statics). |
| Static vs static jamais testé | Idem dans Bepu (arbre des statics non auto-testé) | `IncludeStaticAgainstStaticCollisions` disparaît sans perte. |
| Kinetic vs Kinetic / Kinetic vs Static | Bepu **pose la question** pour ces paires (`AllowContactGeneration` est appelé ; `CharacterNarrowphaseCallbacks.cs:22-25` montre que les démos les refusent par choix, pas par impossibilité) | Nécessaire pour hitbox vs hurtbox (deux Kinetic) : on accepte, on enregistre, on ne crée pas de contrainte. |
| `Dispatcher.NumManifolds` + `GetManifoldByIndexInternal` (scan par frame) | `ConfigureContactManifold` (générique : paires convexes ou manifold non-convexe final ; surcharge `childIndexA/childIndexB` + `ConvexContactManifold` : paires impliquant un compound) | On **pousse** les contacts dans des buffers par worker pendant le step, puis `UpdateContacts` diffère : pas de scan. Les index enfants donnent le tag de fixture **des deux côtés**, y compris pour les compounds (amélioration par rapport au BulletSharp vendorisé). |
| `ManifoldPoint.PositionWorldOnA/B`, `NormalWorldOnB`, `Distance1` | `manifold.GetOffset(i)` (depuis `poseA.Position`), `GetNormal(i)` (**pointe de B vers A**, comme Bullet), `GetDepth(i)` (= `-Distance`) | `PositionOnA = poseA.Position + offset`, `PositionOnB = PositionOnA - normal * depth`. |
| `Collision.ContactPoint = GetContactPoint(0).LocalPointA` | Premier contact enregistré, en monde | Bullet donnait un point **local à A** — consommé par `RPGDemo` (`AddHitEffect`) comme s'il était monde : passer en monde corrige un bug latent plus qu'il n'en crée. |
| `CompoundShape` + `LocalScaling` | `Compound(Buffer<CompoundChild>)` (enfants `{ LocalPosition, LocalOrientation, ShapeIndex }`) — ou `BigCompound` au-delà de ~8 enfants | **Pas d'échelle dans Bepu** : cuire `localScale` dans les dimensions (`Box.Size * scale`, `Capsule.Radius * max(sx,sz)`, `Length * sy`) et dans les offsets des enfants. Échelle négative (miroir) : à rejeter ou à refléter explicitement — vérifier que le moteur n'en passe jamais (les sprites se retournent par `SpriteEffects`, pas par l'échelle). |
| Formes par corps, `Dispose` par forme | `Simulation.Shapes.Add(shape)` → `TypedIndex` ; `RecursivelyRemoveAndDispose` pour les compounds | Les formes vivent dans un registre partagé : un **cache par dimensions** (`(type, dims) → TypedIndex` + refcount) évite une forme par tuile pour les tilemaps. |
| `CreateGhostObject` hors monde puis `Add/Remove` | Un handle Bepu n'existe que dans la simulation | Le backend garde une **description** (forme, pose, profil, mobilité, activité) et un `BodyHandle?` : `Add` = `Bodies.Add(desc)`, `Remove` = `Bodies.Remove(handle)` + handle nul ; `WorldTransform` set hors monde met à jour la description. |
| `RefreshAabb` → `UpdateSingleAabb` | `Bodies.UpdateBounds(handle)` (corps) / `Statics.UpdateBounds(handle)` | `UpdateBounds` **ne réveille pas** le corps : voir §4 sommeil. |
| `ConvexSweepTest(shape, from, to, callback)` | `Simulation.Sweep(shape, pose, velocity, maximumT: 1, pool, ref handler)` avec `velocity.Linear = to - from` | `ISweepHitHandler.OnHit(ref maximumT, t, hitLocation, hitNormal, collidable)` ; `HitFraction = t`. `AllowTest(collidable)` porte `hitTriggers`, `ignoredComponent` et le masque de canaux (même règle que `NeedsCollision`). Surcharge avec `minimumProgression/convergenceThreshold/maximumIterationCount` si la précision par défaut diverge des tests (`precision: 3`). |
| `LocalShapeInfo` (tag d'une requête) | `OnHit` **ne reporte pas** l'enfant touché ([ConvexCompoundSweepTask.cs:33-56](https://github.com/bepu/bepuphysics2/blob/2.5.0-beta.29/BepuPhysics/CollisionDetection/SweepTasks/ConvexCompoundSweepTask.cs)) ; `AllowTest(collidable, childIndex)` est appelé avant chaque enfant candidat, pas après le gagnant | Même limite que Bullet : `ShapeSweep_ReportsNoTag_WhenItHitsACompoundBody` reste vrai. Les **ray casts** reportent `childIndex` (`IRayHitHandler.OnRayHit`). |
| `RayTest` (jamais exposé) | `Simulation.RayCast(origin, direction, maximumT, pool, ref handler)` | Permet d'implémenter enfin `WorldRayCast` / `NearBodyWorldRayCast` (steering) — hors périmètre de la migration, bonus. |
| `ContinuousCollisionDetection` (dispatch `UseContinuous`) | `ContinuousDetection.Continuous(minimumSweepTimestep, sweepConvergenceThreshold)` par collidable, sinon `Passive` | Bepu est spéculatif par défaut (`Passive` suffit pour des vitesses modérées) ; `Continuous` sur les dynamiques si le flag est levé. |
| Seuils de sommeil (`Linear/AngularSleepingThreshold`) | `BodyActivityDescription(sleepThreshold: v², minimumTimestepCountUnderThreshold)` | Un seul seuil au carré (linéaire + angulaire). `-1` = jamais dormir. |
| `DebugDrawWorld()` | Aucun | Écrire un `BepuPhysicsDebugRenderer` : parcourir `Bodies.ActiveSet`/sets dormants + `Statics`, lire la forme par `TypedIndex`, émettre des lignes (box 12 arêtes, sphère 3 cercles, capsule, cylindre, AABB, contacts). `PhysicsDebugDrawModes` (copie de l'enum Bullet) se réduit à `DrawWireframe | DrawAabb | DrawContactPoints`. |

## 4. Différences sémantiques et risques

1. **Contacts spéculatifs.** Bepu appelle `ConfigureContactManifold` pour toute paire dont les AABB
   (élargies par la marge spéculative) se recouvrent ; un contact peut exister avec une
   profondeur négative (`ContactEventsDemo.cs`, note 5). Bullet ne gardait un point de manifold que
   sous ~0,02 de distance. Règle à fixer : **« en collision » = au moins un contact avec
   `depth >= -tolérance`** (tolérance ≈ 0,02 pour mimer Bullet, configurable). Sans cette règle,
   des triggers « touchent » à distance et `OnHit` se déclenche trop tôt.
2. **Sommeil des cinématiques.** Dans Bepu un corps cinématique à vitesse nulle **s'endort**, et une
   paire entre deux corps endormis n'est plus mise à jour (note 8 de la démo). Nos Kinetic sont
   téléportés par `WorldTransform` à vitesse nulle : hitbox vs hurtbox ou trigger vs trigger ne
   produiraient plus d'événements. Remèdes, cumulables : `SleepThreshold = -1` sur les corps capteurs
   et, dans le setter `WorldTransform`, `Awake = true` si nécessaire + `UpdateBounds`. Un dynamique
   qui tombe sur un trigger endormi fonctionne (la paire contient un corps éveillé).
3. **Ordre A/B d'une paire.** Bullet : `Body0/Body1` dans l'ordre d'insertion ; Bepu :
   `CollidablePair` ordonné par type/handle. `Collision` est symétrique, mais
   `Contacts_NameTheCompoundChildTheyTouched` asserte `FixtureTagA == "right"` et
   `FixtureTagB == "probe"` — le test devra devenir indifférent à l'ordre (ou le backend devra
   ordonner A/B comme `Collision` le fait, par hash du composant).
4. **Échelle.** `LocalScaling` disparaît. Le moteur passe `LocalScale` partout (tilemaps, sprites,
   composants) ; en pratique presque toujours `(1,1,1)`. Cuire l'échelle à la création est sans
   perte pour les boîtes ; sphères/capsules non uniformes deviennent approximatives (choix :
   `max` des composantes, documenté). Un corps dont l'échelle change doit être recréé
   (`ReCreatePhysicsObject` existe déjà dans `PhysicsBaseComponent`).
5. **`LinearFactor`.** Seul point sans équivalent exact (tableau §3). Le mode Planar2d en dépend.
   Risque : dérive en Z des dynamiques sur un empilement long. Test dédié à ajouter ; repli :
   contrainte servo.
6. **Restitution.** Bepu n'a pas de coefficient de restitution ; `MaximumRecoveryVelocity` +
   `SpringSettings` donnent un rebond différent. Les valeurs par défaut du moteur (`Restitution = 0`)
   rendent ce point indolore aujourd'hui.
7. **Champs Bullet de `PhysicsDefinition`.** `AdditionalDamping*`, `RollingFriction`, `LocalInertia`
   n'ont plus d'effet. Avec la posture projet (pas de compatibilité d'assets), les **supprimer**
   avec leurs clés JSON dans `Load` et dans `EditorEntityJsonSerializer.SavePhysicsDefinition`, et
   nettoyer les six assets de `Projects/` qui les portent (les clés inconnues sont ignorées au
   chargement, le nettoyage est cosmétique mais évite des assets trompeurs). `Linear/
   AngularSleepingThreshold` fusionnent en un seuil. C'est la seule rupture d'API publique.
8. **Précision des tests.** Les sweeps Bepu sont itératifs (avancement conservatif) ; les assertions
   `precision: 3` sur `result.Point` (`CollisionComponentTests`, `PhysicsShapeSweepTests`) peuvent
   nécessiter la surcharge à paramètres explicites ou `precision: 2`. Le `hitLocation` Bepu est sur
   la surface touchée, comme `HitPointLocal` de Bullet (malgré son nom, déjà traité comme monde).
9. **Tilemaps** : un static par tuile (`TileMapComponent.CreateCollisionObject`) reste viable
   (statics bon marché, formes partagées par le cache) ; les tuiles trigger créées en ghost
   cinématique jamais endormi pèsent dans l'arbre actif → les créer en **static capteur** (changement
   d'appelant, tranche 5).
10. **Threads et callbacks.** Sans dispatcher, tout est mono-thread ; si `MultiThreaded` est activé
    plus tard, les buffers de contacts doivent être par worker (`workerIndex`) — à prévoir dans la
    structure dès la tranche 2 pour ne pas la réécrire.
11. **Mémoire.** `BufferPool` doit être vidé à la destruction du monde ; les formes retirées doivent
    être `RemoveAndDispose` (compounds : `RecursivelyRemoveAndDispose`). Une fuite de pool est
    silencieuse en release et asserte en debug.

## 5. Plan de migration proposé

Branche `bepu-physics`. Invariants à chaque tranche : le dépôt compile, `CasaEngine.Tests.Physics`
est vert (161 tests), les démos `Collision2dBasicDemo`/`Collision3dBasicDemo`/`TileMapDemo`/
`TopDownElevationDemo` tournent. `BulletPhysicsEngine.cs` et la DLL ne sont supprimés qu'en
tranche 6, pour garder un A/B pendant le réglage.

| # | Tranche | Contenu | Acceptation |
| --- | --- | --- | --- |
| 1 | **Socle** | `Directory.Packages.props` : `BepuPhysics` → `2.5.0-beta.29`. Nouveau `CasaEngine/Framework/Physics/BepuPhysicsEngine.cs` : `Simulation`, `BufferPool`, accumulateur de pas, `BepuNarrowPhaseCallbacks` (filtrage par profils, capteurs, matériaux), `BepuPoseIntegratorCallbacks` (gravité/amortissement/facteurs par corps), cache de formes, `BepuPhysicsBodyBackend` (description + handle nullable, statics vs bodies), `BepuPhysicsQueryShapeBackend`, conversions `Matrix`↔`RigidPose`. `PhysicsWorld` bascule sur le nouveau backend. Cylindre corrigé. | Compile ; `PhysicsBroadphaseAabbTests`, `SimulationSpace*Tests`, `CollisionProfilesTests` (partie création) verts. |
| 2 | **Contacts et événements** | Buffers de contacts par worker remplis dans les deux surcharges de `ConfigureContactManifold` (tags par `childIndex`), règle de « toucher » (§4.1), `UpdateContacts`/`SendEvents`/`ClearCollisionDataOf`/`LatestContactPointsFor` réécrits sur ces buffers (pools conservés, zéro allocation en régime permanent). Sommeil des capteurs (§4.2). | `CollisionComponentTests`, `AnimatedSpriteCollisionTimelineTests` verts (test d'ordre A/B adapté, §4.3). |
| 3 | **Requêtes** | `Sweep` Bepu derrière `ShapeSweep`/`ShapeSweepPenetrating` avec handler réutilisable (`[ThreadStatic]` comme aujourd'hui), filtre `hitTriggers`/masque/`ignoredComponent`. | `PhysicsShapeSweepTests`, `CharacterControllerComponentTests` (partie réelle) verts ; démos cutscene `MoveTo`/`NavigateTo` OK. |
| 4 | **Dynamique et espaces** | Inertie (`AngularFactor` → tenseur), `LinearFactor` (§4.5) + **nouveau test** d'empilement Planar2d, CCD, seuils de sommeil. `PhysicsDefinition` nettoyé (§4.7) + `Load` tolérant, doc. | `Collision2dBasicDemo`/`Collision3dBasicDemo` visuellement équivalentes ; nouveau test vert. |
| 5 | **Debug draw et appelants** | `BepuPhysicsDebugRenderer` → `IPhysicsDebugDrawer` ; `PhysicsDebugDrawModes` réduit ; tuiles trigger en static capteur ; `MaterialPreviewViewport` (comptage) inchangé. | `PhysicsDebugDrawNullWorldTests` vert ; overlay physique visible dans l'éditeur et `TileMapDemo`. |
| 6 | **Retrait de Bullet** | Suppression de `BulletPhysicsEngine.cs`, de `ThirdParties/BulletSharp/`, des `<Reference>` dans les deux csproj ; mise à jour de [collision-2d-3d-architecture.md](../../docs/engine/collision-2d-3d-architecture.md) et [character-controller-features.md](../../docs/engine/character-controller-features.md) (références Bullet lignes 9, 81, 254, 331). | Suite complète `CasaEngine.Tests` au niveau de HEAD ; `rg -i bullet` sur `*.cs`/`*.csproj` : 0. |

Tranches 1–3 sont le cœur (~70 % de l'effort) et se valident entièrement par les tests existants.
Seule la tranche 4 introduit un comportement nouveau (facteur linéaire) qui demande un test neuf.
La tranche 2 touche des données sérialisées d'événements (aucune) et aucune migration d'asset ;
la tranche 4 supprime des champs sérialisés jamais écrits par un asset du dépôt.

## 6. Décisions à prendre avant de commencer

1. **Version** : `2.5.0-beta.29` (recommandé : c'est la seule ligne vivante, Stride l'utilise en
   production) — accepter d'être sur une préversion sans stable correspondante.
2. **`LinearFactor`** : approche « annulation de vitesse dans l'intégrateur » (simple, dérive
   bornée) ou contrainte servo (exacte, plus chère). Recommandation : annulation + test ; servo
   seulement si le test échoue.
3. **`PhysicsDefinition`** : supprimer les champs Bullet (posture projet) ou les garder inertes. Recommandation : supprimer.
4. **Capteurs statiques** pour les tuiles trigger (changement d'appelant dans `TileMapComponent`) :
   dans cette migration ou après. Recommandation : tranche 5, c'est trois lignes.
5. **Multithread** : hors périmètre (flag déjà présent, structure de buffers prête en tranche 2).

## 7. Hors périmètre, rendu possible par Bepu

- `WorldRayCast` / `NearBodyWorldRayCast` (steering `WallAvoidance`) via `Simulation.RayCast`, avec
  tag de fixture (les rays reportent `childIndex`).
- Formes `ConvexHull`, `Mesh` (statics de niveau importés glTF), `Triangle`.
- Contraintes (`BallSocket`, `Hinge`, ragdoll) — `CharacterControllerRagdollBridgeComponent` ne
  fait aujourd'hui que lister des composants, sans contrainte physique.
- Réponse `Block`/`Overlap` **par paire sur un même corps** (la « règle v1 honnête » de
  [collision-2d-3d-architecture.md](../../docs/engine/collision-2d-3d-architecture.md) §D3) : avec
  `ConfigureContactManifold` on décide par paire de créer ou non la contrainte, donc un composant
  pourrait porter hitbox et hurtbox dans un seul corps. À documenter comme évolution, pas à faire
  pendant la migration.
