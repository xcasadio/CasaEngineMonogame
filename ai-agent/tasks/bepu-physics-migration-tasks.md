# Migration physique BulletSharp → bepuphysics2 2.5.0-beta.29 — plan d'exécution

Légende : ⏳ Todo · 🚧 In progress · 🧪 Needs testing · ✅ Done · ⚠️ Blocked

Source : [analysis-bepuphysics2-migration.md](../audits/analysis-bepuphysics2-migration.md) (lire
§1, §3 et §4 avant toute tranche). Branche : `bepu-physics`. Décisions utilisateur (2026-08-22) :
préversion `2.5.0-beta.29` acceptée ; `LinearFactor` par annulation de vitesse dans l'intégrateur ;
suppression des champs Bullet de `PhysicsDefinition` ; tuiles trigger en static capteur dans cette
migration.

Invariants à chaque commit : le dépôt compile (`dotnet build CasaEngine.Tests/CasaEngine.Tests.csproj`),
aucun fichier hors périmètre de la tranche n'est modifié, un commit par sous-étape cohérente,
jamais de push. Les modifications préexistantes de l'arbre de travail
(`CasaEngine.Launcher/Program.cs`, `Projects/SampleProject/.casaeditor/viewport.editor.json`)
appartiennent à l'utilisateur : ne pas les inclure dans un commit, ne pas les reverter.

Baseline HEAD `e18b2282` : `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj --filter "FullyQualifiedName~CasaEngine.Tests.Physics"`
→ 161 réussis, 0 échec.

---

## Tranche 1 — Backend Bepu complet derrière `IPhysicsWorld` (tranches 1–3 de l'analyse) ⏳

**Objectif** : `PhysicsWorld` instancie un backend Bepu et la suite `CasaEngine.Tests.Physics`
est intégralement verte (161 tests), Bullet restant dans le dépôt (non utilisé) pour l'A/B.

**Périmètre (propriété exclusive de l'exécutant)** :
- `Directory.Packages.props` : `BepuPhysics` → `2.5.0-beta.29` (+ `BepuUtilities` si central
  package management l'exige — transitive sinon).
- Nouveau dossier `CasaEngine/Framework/Physics/Bepu/` : `BepuPhysicsEngine.cs` (même surface
  `internal`/`public` que `BulletPhysicsEngine` **pour les membres relayés par `PhysicsWorld`
  uniquement** — ne pas porter les membres sans appelant listés en §1 de l'analyse), et les
  fichiers annexes jugés utiles (`BepuNarrowPhaseCallbacks.cs`, `BepuPoseIntegratorCallbacks.cs`,
  `BepuShapeCache.cs`, `BepuBodyBackend.cs`…). Namespace `CasaEngine.Framework.Physics.Bepu`.
- `CasaEngine/Framework/Application/Components/Physics/PhysicsWorld.cs` : remplacer
  `BulletPhysicsEngine` par le backend Bepu (c'est le seul point de bascule).
- `CasaEngine.Tests/Physics/*` : adaptations **minimales et justifiées** uniquement :
  (a) `Contacts_NameTheCompoundChildTheyTouched` peut devenir indifférent à l'ordre A/B si le backend
  ne peut pas garantir l'ordre Bullet ; (b) une assertion `precision: 3` sur un point de sweep peut
  passer à `precision: 2` si, et seulement si, la surcharge `Sweep` à paramètres explicites ne
  suffit pas — documenter chaque changement dans le message de commit.
- Interdit : `PhysicsDefinition.cs` (tranche 4), `TileMapComponent.cs` (tranche 5), tout fichier
  d'`IPhysicsWorld`/`PhysicsBody`/`PhysicsQueryShape` (la surface publique ne change pas),
  suppression de Bullet (tranche 6).

**Design imposé** (détails et justifications en §3/§4 de l'analyse) :
1. `Simulation.Create(new BufferPool(), narrowPhaseCallbacks, poseIntegratorCallbacks, new SolveDescription(8, 1))`,
   sans `IThreadDispatcher` (mono-thread, déterministe). `Dispose` : `Simulation.Dispose()` puis
   `BufferPool.Clear()`.
2. Step : accumulateur reproduisant `StepSimulation(dt, MaxSubSteps, FixedTimeStep)` de Bullet (au
   plus `MaxSubSteps` pas de `FixedTimeStep`, reste perdu ; `MaxSubSteps == 0` → pas variable `dt`).
   `Simulation.Timestep` lève si `dt <= 0` : garder le garde.
3. Données par collidable : une `CollidableProperty<BepuCollidableData>` (struct non managée :
   `GroupBit`, `BroadphaseMask`, `IsSensor`, `ApplyGravity`, `LinearDamping`, `AngularDamping`,
   `LinearFactor`) pour corps **et** statics ; et une table managée (`List<BepuBodyBackend>`
   indexée par `BodyHandle.Value` / `StaticHandle.Value`) pour `UserObject` (l'`ICollideableComponent`
   ou l'objet passé par l'appelant) et les tags de fixtures `string[]`.
4. Mobilité : `PhysicsType.Static`/`AddStaticObject` → `Simulation.Statics` (y compris en
   `useExternalViewManagement` : le déplacement passe par `Statics.ApplyDescription`, qui réveille
   les dynamiques en contact) ; `AddGhostObject`/`CreateGhostObject` → `BodyDescription.CreateKinematic`
   avec `BodyActivityDescription(-1)` (jamais endormi) et `IsSensor = true` ; `AddRigidBody` masse > 0
   → `CreateDynamic` avec `shape.ComputeInertia(mass)` (compound : `CompoundBuilder`), masse 0 → static.
5. Cycle de vie : un `BepuBodyBackend` garde une **description** (forme, pose, profil, mobilité,
   activité) et un handle nullable ; `AddCollisionObject`/`AddRigidBody(PhysicsBody)` =
   `Bodies.Add`/`Statics.Add` si absent ; `RemoveCollisionObject`/`RemoveRigidBody` = retrait + handle
   nul (les deux méthodes acceptent n'importe quel corps, `PhysicsBaseComponent.DestroyPhysicsObject`
   choisit par `IsRigidBody`). `WorldTransform` set hors simulation met à jour la description ; en
   simulation : `Pose` + `Awake = true` (corps) ou `Statics.ApplyDescription` (static).
   `RefreshAabb` → `Bodies.UpdateBounds` / `Statics.UpdateBounds`. `Dispose` retire de la simulation
   et libère les formes. `IsRigidBody` = créé par `AddRigidBody`/`AddStaticObject` (comme Bullet).
   `HasContactResponse = !IsSensor`.
6. Formes : `Box(size * scale)`, `Sphere(radius * max(scale))`, `Capsule(radius * max(sx, sz), length * sy)`,
   `Cylinder(radius * max(sx, sz), length * sy)` (**corriger le bug Bullet qui ignorait la longueur**).
   Cache `(type, dimensions) → TypedIndex` avec refcount (`Shapes.Remove` à zéro). Compound : `Compound`
   (`CompoundChild { LocalPosition = fixture.LocalPosition * scale, LocalOrientation, ShapeIndex }`), ou
   `BigCompound` au-delà de 8 enfants ; `Shapes.RemoveAndDispose(compoundIndex, pool)` à la
   destruction (les enfants restent gérés par le cache). Une échelle négative ou nulle → `ArgumentException`.
   `GetFixtureLocalTransform(i)` et `GetFixtureTag(i)` lisent l'enfant / le tableau de tags ;
   corps à fixture unique : `IsCompound = false`, `FixtureCount = 1`.
7. `INarrowPhaseCallbacks` :
   - `AllowContactGeneration(a, b)` = `(maskA & groupB) != 0 && (maskB & groupA) != 0` (les paires
     cinématique-cinématique et cinématique-static sont **acceptées** : hitbox vs hurtbox).
   - `ConfigureContactManifold` (les deux surcharges) : enregistrer chaque contact dans un buffer
     **par `workerIndex`** (struct `{ CollidableReference A, B; int ChildA, ChildB; Vector3 Offset,
     Normal; float Depth }`, listes pré-allouées, zéro allocation en régime permanent) ; la
     surcharge enfant (`childIndexA/B`, `ConvexContactManifold`) est celle qui porte les index
     de fixture pour les paires avec compound — ne pas enregistrer deux fois la même paire (la
     surcharge générique reçoit un `NonconvexContactManifold` pour ces paires : s'en servir
     seulement pour le matériau et le retour). Retour : `pairMaterial = { FrictionCoefficient =
     fA * fB (produit, comme Bullet), MaximumRecoveryVelocity = 2f, SpringSettings = new(30, 1) }` ;
     retourner `false` (pas de contrainte) si l'un des deux est capteur ou si aucun n'est dynamique.
8. Événements (`UpdateContacts`/`SendEvents`/`ClearCollisionDataOf`/`LatestContactPointsFor`) :
   réécrits sur les buffers de contacts. Règle « en collision » : au moins un contact avec
   `Depth >= -ContactTolerance` (`const float ContactTolerance = 0.02f`). Ordonner A/B comme Bullet
   (`aFirst = a.UserObject.GetHashCode() > b.UserObject.GetHashCode()`). `Collision.ContactPoint`
   = premier contact en **monde** (`poseA.Position + offset`). `ContactPoint` : `Normal` (de B vers A,
   convention identique à Bullet), `PositionOnA = poseA.Position + offset`,
   `PositionOnB = PositionOnA - Normal * Depth`, `Distance = -Depth`, tags via `ChildA/ChildB`
   (corps à fixture unique : toujours attribuable). Conserver les pools `HashSet<ContactPoint>`.
   Ne pas émettre d'événement pour une paire static-static (jamais générée par Bepu de toute façon).
9. `IPoseIntegratorCallbacks` (wide) : `AngularIntegrationMode.Nonconserving`,
   `AllowSubstepsForUnconstrainedBodies = false`, `IntegrateVelocityForKinematics = false`.
   `IntegrateVelocity` : pour chaque lane active, lire la donnée du corps via
   `Bodies.ActiveSet.IndexToHandle[bodyIndex]` → gravité (`GameSettings.PhysicsEngineSettings.Gravity`
   si `ApplyGravity`), amortissement `v *= pow(1 - damping, dt)`, puis **`LinearFactor`** : mettre à
   zéro les composantes de vitesse linéaire dont le facteur vaut 0. `AngularFactor` : à la création,
   annuler les lignes/colonnes de `BodyInertia.InverseInertiaTensor` des axes dont le facteur vaut 0.
   `ApplyImpulse` applique aussi le masque `LinearFactor` à la vitesse résultante et réveille le corps.
   Test pilote : `SimulationSpacePolicyTests.Planar2dWorld_LocksTheBodiesItCreates`.
10. Requêtes : `PhysicsQueryShape` = forme convexe Bepu stockée par valeur (`Box`/`Sphere`/`Capsule`/
    `Cylinder`) dans le backend de requête (pas besoin du registre `Shapes`) ; `ShapeSweep` →
    `Simulation.Sweep(shape, pose(from), velocity { Linear = to.Translation - from.Translation },
    maximumT: 1f, pool, ref handler)` avec un handler réutilisable (`[ThreadStatic]` comme aujourd'hui)
    implémentant `AllowTest(collidable)` = masque de canaux ∧ `hitTriggers` ∧ `ignoredComponent`,
    `OnHit` = garder le plus petit `t` (closest) ou tout ajouter (penetrating), `OnHitAtZeroT` =
    hit avec `HitFraction = 0`, `Point = from.Translation`, `Normal = Vector3.Zero`. `HitResult.Tag`
    via `GetFixtureTag(collidable, childIndex = -1)` (un corps à fixture unique reste attribuable ;
    compound → `null`, même limite que Bullet, test `ShapeSweep_ReportsNoTag_WhenItHitsACompoundBody`).
    Si la précision par défaut ne satisfait pas `precision: 3`, utiliser la surcharge
    `(minimumProgression, convergenceThreshold, maximumIterationCount)`.
11. `WorldRayCast`/`NearBodyWorldRayCast` : conserver `throw new NotImplementedException()`
    (hors périmètre). `DrawDebugWorld` : no-op dans cette tranche (tranche 5).
12. Conversions : `Matrix` → `RigidPose` via `matrix.Translation.ToNumerics()` et
    `Quaternion.CreateFromRotationMatrix(matrix).ToNumerics()` (jamais `Decompose`) ; retour par
    `Matrix.CreateFromQuaternion(pose.Orientation)` + `Translation`. Helper statique interne.
13. Aucune allocation par frame dans `Update`/`UpdateContacts`/`SendEvents`/sweeps en régime
    permanent (listes réutilisées, structs, pas de LINQ ni de lambdas capturantes).

**Critères d'acceptation** :
- `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj --filter "FullyQualifiedName~CasaEngine.Tests.Physics"`
  → 161 réussis, 0 échec, 0 ignoré.
- `dotnet build CasaEngine.sln` (ou les projets `CasaEngine`, `CasaEngine.Editor`, `CasaEngine.Demos`,
  `CasaEngine.Tests`) sans erreur.
- `rg "BulletSharp|BulletPhysicsEngine" CasaEngine --glob "*.cs"` ne retourne plus que
  `Framework/Physics/BulletPhysicsEngine.cs` lui-même.
- Commits : au moins trois (socle+corps+formes ; requêtes ; contacts+événements), chacun buildable.

## Tranche 2 — `PhysicsDefinition` nettoyé, CCD, sommeil, test Planar2d ⏳

**Périmètre** : `CasaEngine/Engine/Physics/PhysicsDefinition.cs`,
`CasaEngine.EditorServices/EditorEntityJsonSerializer.cs` (`SavePhysicsDefinition`), les six assets
`Projects/SampleProject/Entities/Box.entity`, `Projects/SampleProject/DefaultWorld.world`,
`Projects/RPGDemo/Entities/{weapon_rock,character_octopus,character_link}.entity`,
`Projects/RPGDemo/DefaultWorld.world`, le backend Bepu (CCD, sommeil), un nouveau test
`CasaEngine.Tests/Physics/BepuPlanarStackingTests.cs` (ou ajout dans `SimulationSpacePolicyTests`),
`docs/engine/collision-2d-3d-architecture.md` (note de migration courte).

**Contenu** :
- Supprimer `AdditionalAngularDampingFactor`, `AdditionalAngularDampingThresholdSqr`,
  `AdditionalDamping`, `AdditionalDampingFactor`, `AdditionalLinearDampingThresholdSqr`,
  `RollingFriction`, `LocalInertia` ; remplacer `LinearSleepingThreshold`/`AngularSleepingThreshold`
  par un unique `SleepThreshold` (float, défaut `0.01f`, sémantique Bepu : seuil de vitesse au carré ;
  `< 0` = jamais dormir). `Load` : clés manquantes tolérées (valeurs par défaut), clés inconnues
  ignorées. `SavePhysicsDefinition` : n'écrit que les champs restants (`physics_type`,
  `collision_profile`, `angular_damping`, `angular_factor`, `friction`, `linear_damping`,
  `linear_factor`, `sleep_threshold`, `mass`, `restitution`, `apply_gravity`, `debug_color`).
  Nettoyer les six assets (retirer les clés mortes, ajouter `sleep_threshold` si utile).
- CCD : `PhysicsEngineFlags.ContinuousCollisionDetection` → `ContinuousDetection.Continuous()` sur
  les collidables dynamiques, sinon `Passive`.
- `SleepThreshold` → `BodyActivityDescription(sleepThreshold)` des dynamiques.
- Nouveau test : monde `Planar2d`, 5 boîtes dynamiques empilées sous gravité, 300 pas de 1/60 :
  `|Z| < 1e-3` pour chaque corps, et la pile reste debout (Y croissants).
- Doc : paragraphe « Backend Bepu (2026-08) » dans `collision-2d-3d-architecture.md` §1 : ce qui
  change (échelle cuite, capteur par callback, tags de compound en contact, `LinearFactor` par
  intégrateur) ; mettre à jour les mentions Bullet des lignes 217, 246, 343, 418.

**Acceptation** : suite `CasaEngine.Tests` (complète) au niveau de HEAD (les 18 échecs préexistants
hors physique, s'ils existent encore, sont documentés, pas introduits) ; `rg additional_damping Projects` → 0.

## Tranche 3 — Debug draw Bepu et tuiles trigger en static capteur ⏳

**Périmètre** : nouveau `CasaEngine/Framework/Physics/Bepu/BepuPhysicsDebugRenderer.cs`, méthode
`DrawDebugWorld` du backend, `CasaEngine/Engine/Physics/PhysicsDebugDrawModes.cs`,
`CasaEngine/Framework/Scene/Entities/Components/TileMapComponent.cs` (lignes 972–1060),
`CasaEngine/Framework/Application/Components/Physics/PhysicsDebugViewRendererComponent.cs` si le
mode par défaut change.

**Contenu** :
- `PhysicsDebugDrawModes` réduit à `NoDebug`, `DrawWireframe`, `DrawAabb`, `DrawContactPoints`
  (+ `MaxDebugDrawMode = DrawWireframe | DrawContactPoints`). Vérifier les consommateurs (un seul :
  `PhysicsDebugViewRendererComponent.cs:26`).
- Renderer : parcourir `Simulation.Bodies` (sets actif **et** dormants) et `Simulation.Statics`,
  lire la forme via `Simulation.Shapes` par `TypedIndex`, émettre des lignes via
  `IPhysicsDebugDrawer.DrawLine` : boîte (12 arêtes), sphère (3 cercles de 16 segments), capsule,
  cylindre, compound (récursif sur les enfants avec leur pose), AABB si demandé, contacts du
  dernier step si demandé (`DrawContactPoint`). Couleurs : `DebugColor` du corps si défini, sinon
  celle du profil, sinon vert (dynamique éveillé) / gris (statique ou dormant) / bleu (capteur).
  Aucune allocation par frame (tableaux de sommets de cercle pré-calculés).
- `TileMapComponent` : les tuiles trigger passent de `AddGhostObject(..., CollisionProfileIds.Trigger)`
  à `AddStaticObject(..., physicsDefinition Trigger)` (profil capteur → static capteur dans Bepu).
  Vérifier que `RemoveCollisionObject`/`Dispose` des chunks fonctionnent toujours (tranche 1, point 5).

**Acceptation** : `PhysicsDebugDrawNullWorldTests` vert ; `TileMapDemo` et l'éditeur affichent
l'overlay physique (smoke manuel documenté dans le commit) ; suite physique verte.

## Tranche 4 — Retrait de Bullet ⏳

**Périmètre** : suppression de `CasaEngine/Framework/Physics/BulletPhysicsEngine.cs`,
`ThirdParties/BulletSharp/`, des blocs `<Reference Include="BulletSharp">` dans
`CasaEngine/CasaEngine.csproj` et `CasaEngine.Tests/CasaEngine.Tests.csproj` ; `ConstraintTypes.cs`
(sans usage) ; mentions Bullet dans `docs/engine/character-controller-features.md` (lignes 9, 81,
254, 331) et `docs/editor/gameplay-csproj-scaffolding.md:214` ; commentaire de
`CollisionComponentTests.cs:199` ; mémoire/README `ai-agent` (statut de ce plan).

**Acceptation** : `rg -i "bullet" . --glob "*.cs" --glob "*.csproj" --glob "*.props"` → 0 ;
build complet ; suite complète `CasaEngine.Tests` au niveau de HEAD.

---

## Suivi

| Tranche | Statut | Commit(s) | Vérification |
| --- | --- | --- | --- |
| 1 — Backend Bepu | ⏳ | | |
| 2 — PhysicsDefinition / CCD / test Planar2d | ⏳ | | |
| 3 — Debug draw / tuiles trigger | ⏳ | | |
| 4 — Retrait de Bullet | ⏳ | | |
