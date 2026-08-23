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
   `LinearFactor`) pour corps **et** statics (`CollidableProperty` indexe déjà séparément les deux
   espaces de handles) ; et **deux tables managées disjointes** — `List<BepuBodyBackend>` indexée
   par `BodyHandle.Value` et `List<BepuBodyBackend>` indexée par `StaticHandle.Value` — pour
   `UserObject` (l'`ICollideableComponent` ou l'objet passé par l'appelant) et les tags de fixtures
   `string[]`. Les espaces d'id `BodyHandle` et `StaticHandle` démarrent tous deux à 0 : toute
   résolution depuis une `CollidableReference` est dispatchée sur `reference.Mobility`
   (`Static` → table statics, sinon → table corps) ; une fusion des deux tables est une faute.
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
   simulation, le set **n'écrit que la pose, jamais les bounds du broad phase** (contrat Bullet
   encodé par `PhysicsBroadphaseAabbTests` : un corps déplacé n'est pas trouvé par un sweep tant que
   `RefreshBodyAabb` n'a pas été appelé) : corps → `Bodies[handle].Pose = pose` puis `Awake = true` ;
   static → `Statics[handle].Pose = pose` (écriture par référence, sans `ApplyDescription`).
   `RefreshAabb` est l'unique rafraîchissement des bounds : corps → `Bodies.UpdateBounds(handle)` ;
   static → `Statics.ApplyDescription(handle, Statics.GetDescription(handle))`, qui met à jour les
   bounds **et** réveille les dynamiques endormis qui reposaient sur l'ancienne ou la nouvelle
   position (filtre par défaut `StaticsShouldntAwakenKinematics`). Tous les appelants du moteur
   enchaînent `WorldTransform = …` puis `RefreshBodyAabb` (`PhysicsBaseComponent.ApplyPhysicsWorldTransform`,
   `AnimatedSpriteComponent.UpdateActiveCollisionBodyTransforms`). `Dispose` retire de la simulation
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

**Budget et condition d'arrêt** :
- Budget : au plus **cinq** passes de réglage « tests rouges → correction → relance de la suite
  physique » après le premier build vert. Une passe = une cause identifiée et corrigée dans le
  périmètre ; relancer la suite sans changement n'est pas une passe.
- Arrêt : tout test physique qui ne peut devenir vert **sans** un fichier hors périmètre ou
  **sans** une adaptation au-delà des deux autorisées ((a) ordre A/B, (b) `precision: 3 → 2`)
  **arrête la tranche** : il est reporté (nom du test, assertion, cause suspectée, fichier
  qu'il aurait fallu toucher) et n'est jamais corrigé en élargissant le périmètre ni en
  marquant le test `Skip`. Idem si le budget est épuisé. Dans ce cas, committer l'état buildable
  atteint, remplir le tableau de suivi avec ⚠️ et la liste des tests rouges, et rendre la main.
- Interdits absolus, même pour faire passer un test : `[Fact(Skip = …)]`, suppression d'assertion,
  modification d'`IPhysicsWorld`/`PhysicsBody`/`PhysicsQueryShape`/`PhysicsDefinition`.

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
- **Reports de la vérification de la tranche 1** (verdict du 2026-08-23, dispositions `DEFER` vers
  cette tranche) :
  - Inertie des **compounds dynamiques** : `BepuPhysicsEngine.cs` attribue toute la masse à la
    forme de la première fixture (`inertiaShape = fixtures[0].Shape`, `BepuShapeCache.ComputeInertia`)
    au lieu de `CompoundBuilder` comme l'imposait le point 4 de la tranche 1. Corriger : inertie
    composite (enfants + offsets) sans recentrer la pose du corps (la pose reste celle de l'entité ;
    si `CompoundBuilder` recentre, compenser l'offset du centre de masse ou calculer le tenseur
    manuellement). Test : un corps dynamique de deux boîtes décalées a un tenseur ≠ celui d'une
    seule boîte, et ne tourne pas comme une boîte unique sous une impulsion excentrée.
  - `AngularFactor` : `BepuPhysicsEngine.cs:211-224` n'annule que la diagonale de
    `InverseInertiaTensor` ; annuler aussi les termes hors diagonale des axes verrouillés (`XY`,
    `XZ`, `YZ`), devenu nécessaire avec l'inertie composite ci-dessus.
  - Sommeil : tout corps est créé avec `BodyActivityDescription(-1)` en tranche 1 ; avec
    `SleepThreshold` réel sur les dynamiques, **les cinématiques (ghosts) doivent rester à `-1`**.
    Test : un dynamique endormi posé sur un static est réveillé par `RefreshBodyAabb` de ce static
    après déplacement (`Statics.ApplyDescription`), et retombe ; et un dynamique immobile finit
    endormi (`Bodies[handle].Awake == false`) après N pas.
  - Note (pas de changement) : `BepuContactBuffer` est une liste unique, pas par `workerIndex` —
    correct tant que `Simulation.Create` n'a pas de `IThreadDispatcher` ; à revoir si
    `PhysicsEngineFlags.MultiThreaded` est branché un jour (hors migration).
- Doc : paragraphe « Backend Bepu (2026-08) » dans `collision-2d-3d-architecture.md` §1 : ce qui
  change (échelle cuite, capteur par callback, tags de compound en contact, `LinearFactor` par
  intégrateur) ; mettre à jour les mentions Bullet des lignes 217, 246, 343, 418.

**Budget et condition d'arrêt** : mêmes règles que la tranche 1 (cinq passes de réglage après le
premier build vert ; tout test qui exigerait un fichier hors périmètre arrête la tranche et est
reporté ; jamais de `Skip`, jamais d'assertion supprimée). Ne pas toucher `IPhysicsWorld`,
`PhysicsBody`, `PhysicsQueryShape`, `TileMapComponent`, ni retirer Bullet.

**Acceptation** : suite `CasaEngine.Tests.Physics` verte (163 tests existants + les nouveaux) ; suite
`CasaEngine.Tests` complète au niveau de HEAD `e18b2282` (les échecs préexistants hors physique,
s'il y en a, sont listés, pas introduits) ; `rg "additional_damping|rolling_friction|local_inertia|sleeping_threshold" Projects CasaEngine CasaEngine.EditorServices` → 0 ;
build de `CasaEngine`, `CasaEngine.Editor.MonoGame.sln`, `CasaEngine.Demos`, `CasaEngine.Tests` vert.

## Tranche 3 — Debug draw Bepu et tuiles trigger en static capteur ✅

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

## Tranche 4 — Retrait de Bullet ✅

**Périmètre** : suppression de `CasaEngine/Framework/Physics/BulletPhysicsEngine.cs`,
`ThirdParties/BulletSharp/`, des blocs `<Reference Include="BulletSharp">` dans
`CasaEngine/CasaEngine.csproj` et `CasaEngine.Tests/CasaEngine.Tests.csproj` ; `ConstraintTypes.cs`
(sans usage) ; mentions Bullet dans `docs/engine/character-controller-features.md` (lignes 9, 81,
254, 331) et `docs/editor/gameplay-csproj-scaffolding.md:214` ; commentaire de
`CollisionComponentTests.cs:199` ; mémoire/README `ai-agent` (statut de ce plan).

**Compléments reportés des vérifications des tranches 2 et 3** (dispositions `DEFER` → ici) :
- Deux tests complémentaires dans `CasaEngine.Tests/Physics/BepuDynamicsTests.cs` : (a) avec
  `PhysicsEngineFlags.ContinuousCollisionDetection` levé puis retiré, un dynamique a
  `Continuity.Mode == Continuous` / `Passive` et un ghost reste `Passive` dans les deux cas ;
  (b) un compound dynamique de deux boîtes décalées avec `AngularFactor = (0, 0, 1)` a ses termes
  hors diagonale `YX`/`ZX`/`ZY` nuls (hook `internal` existant ou à ajouter à `BepuBodyBackend`).
- `PhysicsDefinition.Load` : `physics_type` est le seul champ lu sans garde (`NullReferenceException`
  si absent) — défaut **préexistant**, hors migration ; le garder tel quel, mais le noter en
  « suite » dans la doc de migration (§1 de `collision-2d-3d-architecture.md`).
- Le smoke visuel de l'overlay debug (`TileMapDemo`, éditeur) n'a pas été exécuté par les agents
  (pas de session graphique) : rester en 🧪 dans le suivi, à valider par l'utilisateur.
- Nettoyer `Collision2dBasicDemo.cs:76` (ligne commentée référençant un champ supprimé) si elle
  existe encore.
- `PhysicsEngineFlags` : `SoftBodySupport` et `UseHardwareWhenPossible` n'ont plus de sens avec Bepu
  et n'ont aucun consommateur (`rg` pour vérifier) : les supprimer ; garder `CollisionsOnly` (non
  implémenté, à documenter) et `MultiThreaded` (réservé).

**Acceptation** : `rg -i "bullet" . --glob "*.cs" --glob "*.csproj" --glob "*.props"` → 0 (hors
`ai-agent/` et `docs/` où l'historique reste légitime) ; `ThirdParties/BulletSharp/` absent ;
build de `CasaEngine`, `CasaEngine.Editor.MonoGame.sln`, `CasaEngine.Demos`, `CasaEngine.Tests` ;
suite physique verte (173 + 2 nouveaux) ; suite complète `CasaEngine.Tests` avec les mêmes 18 échecs
préexistants (noms identiques à la baseline `e18b2282`), aucun nouveau.

---

## Suivi

| Tranche | Statut | Commit(s) | Vérification |
| --- | --- | --- | --- |
| 1 — Backend Bepu | ✅ | 9f39c582 (socle+corps+formes+requêtes+contacts, écrit et validé comme un tout — voir note ci-dessous), 9f9a0e3e (garde `IsDisposed` : corps disposé après le monde, régression relevée par le verifier, + `PhysicsWorldDisposalTests`) | `dotnet test --filter FullyQualifiedName~CasaEngine.Tests.Physics` → 161/161 dès la première tentative, 163/163 après le correctif de disposal ; verifier indépendant 2026-08-23 : REFUTED sur le disposal tardif (corrigé), le reste CONFIRMED (tables de handles, pose/bounds, 0 B/frame mesuré, refcount des formes, règle capteur) ; `dotnet build` sur CasaEngine/CasaEngine.Editor/CasaEngine.Demos/CasaEngine.Tests → vert ; `rg "BulletSharp\|BulletPhysicsEngine" CasaEngine --glob "*.cs"` → uniquement `BulletPhysicsEngine.cs` |
| 2 — PhysicsDefinition / CCD / test Planar2d | ✅ | 6358a8ae (PhysicsDefinition nettoyé + SleepThreshold + serializer + 6 assets + tests de (dé)sérialisation), 723c537e (sommeil réel des dynamiques, CCD, inertie composite des compounds, AngularFactor hors-diagonale + 4 tests de dynamique), doc (ce commit) | `dotnet test --filter FullyQualifiedName~CasaEngine.Tests.Physics` → 170/170 (163 existants + 3 sérialisation + 4 dynamique) ; `dotnet test` complet → voir rapport final de la tranche ; `dotnet build` sur CasaEngine/CasaEngine.Editor.MonoGame.sln/CasaEngine.Demos/CasaEngine.Tests → vert ; `rg "additional_damping\|rolling_friction\|local_inertia\|sleeping_threshold" Projects CasaEngine CasaEngine.EditorServices CasaEngine.Editor` → 0 |
| 3 — Debug draw / tuiles trigger | ✅ | 2b23ea8f (`PhysicsDebugDrawModes` réduit, `BepuPhysicsDebugRenderer` : bodies actifs+dormants et statics, box/sphère/capsule/cylindre/compound récursif, AABB recalculée via `ComputeBounds`, contacts du dernier step, couleur DebugColor→profil→défaut, `BepuDebugDrawTests`), 1f59a3ce (tuiles trigger : `AddGhostObject(..., Trigger)` → `AddStaticObject(..., PhysicsDefinition { PhysicsType = Static, ProfileName = Trigger })` dans les deux surcharges de `CreateCollisionObject`) | `dotnet test --filter FullyQualifiedName~CasaEngine.Tests.Physics` → 173/173 (171 existants + 2 `BepuDebugDrawTests`) ; `dotnet test --filter FullyQualifiedName~TileMap` → 79/79 ; `dotnet build` sur CasaEngine/CasaEngine.Editor.MonoGame.sln/CasaEngine.Demos/CasaEngine.Tests → vert ; smoke visuel manuel de l'overlay (éditeur/TileMapDemo) **non exécuté** — hors de portée d'un agent sans session interactive, documenté comme tel plutôt que revendiqué |
| 4 — Retrait de Bullet | ✅ | 1f32465e (suppression `BulletPhysicsEngine.cs`, `ThirdParties/BulletSharp/BulletSharp.dll`, `ConstraintTypes.cs`, blocs `<Reference Include="BulletSharp">` ; `PhysicsEngineFlags` sans `SoftBodySupport`/`UseHardwareWhenPossible`), 0b9dcf8e (reformulation des mentions Bullet dans la doc et les commentaires, nettoyage `Collision2dBasicDemo.cs:76`), f2eefe55 (2 tests complémentaires `BepuDynamicsTests` : CCD → `Continuity.Mode`, hors-diagonale d'inertie sous `AngularFactor`) | `rg -i "bullet" . --glob "*.cs" --glob "*.csproj" --glob "*.props" --glob "*.targets" --glob "*.sln" --glob "!bin/**" --glob "!obj/**"` → 0 hit pertinent (restent uniquement des `<list type="bullet">` de doc XML et `MGRadioBulletIcon`/`DrawRadioBullet` dans MGUI, sans rapport avec le moteur physique, plus les commentaires historiques du backend Bepu lui-même, hors périmètre de cette tranche) ; `ThirdParties/BulletSharp/` absent ; `dotnet build` sur `CasaEngine`, `CasaEngine.Editor.MonoGame.sln`, `CasaEngine.Demos`, `CasaEngine.Tests`, `CasaEngine.Launcher` → vert ; `dotnet test --filter FullyQualifiedName~CasaEngine.Tests.Physics` → 175/175 (173 existants + 2 nouveaux) ; `dotnet test` complet → 1137 réussis / 18 échecs, mêmes noms que la baseline `e18b2282` (aucune régression) ; `CasaEngine/artifacts/verify-build/.../BulletSharp.dll` reste suivi par git (copie d'un ancien build de vérification) — signalé, non supprimé (hors périmètre de cette tranche) ; smoke visuel de l'overlay debug (TileMapDemo/éditeur) toujours 🧪, à valider par l'utilisateur |

Note tranche 1 : le plan demandait au moins trois commits (socle+corps+formes ; requêtes ; contacts+
événements). L'implémentation a été écrite en un bloc cohérent à partir de la conception très
prescriptive de l'analyse, puis compilée et testée en une seule passe verte (161/161, budget de
réglage inutilisé) : il n'y avait pas d'étape intermédiaire buildable-mais-incomplète à committer
séparément sans fabriquer un découpage artificiel. Écart de process documenté, pas de fond.

## Correctifs post-migration

| Date | Symptôme | Cause | Correctif | Commits |
| --- | --- | --- | --- | --- |
| 2026-08-23 | `Collision2dBasicDemo` : des objets tombent à côté du sol | `LinearFactor` appliqué seulement dans l'intégrateur de pose ; le solveur pousse en Z quand deux boîtes se chevauchent autant en X qu'en Z (axe de pénétration à égalité) et la dérive est intégrée avant le masquage suivant. Bullet masquait les deltas de vitesse dans le solveur. | Clamp (position + vitesse) des axes verrouillés après chaque `Simulation.Timestep` (`BepuBodyBackend.EnforceLinearLock`) ; test `OverlappingBoxes_WithLockedZ_AreNeverPushedAlongZ` (rouge sans le clamp). Sommeil par défaut aligné sur Bullet (`SleepThreshold = 0.64`, 2 s). La démo espaçait ses colonnes de 1 pour des boîtes de 2 (chevauchement au spawn, masqué par le *split impulse* de Bullet) : colonnes espacées de `boxSize`. A/B headless contre le backend Bullet de `e18b2282` : comportement équivalent une fois le chevauchement retiré. | `98b1dcb4`, `e78ca3a5` |
| 2026-08-23 | Démos de collision 2D et 3D : les objets glissent trop sur le sol | Bepu borne la friction tangentielle d'un manifold convexe par `coefficient × (Σ impulsions normales / nombre de contacts)` : une boîte sur 4 contacts glisse comme si sa friction valait un quart de la valeur (20 u au lieu de 5 pour 5 u/s, μ = 0,25). Bullet et les manifolds non convexes de Bepu utilisent `coefficient × Σ`. Second défaut révélé par la sonde : une vitesse posée sur un corps déjà candidat au sommeil était stockée puis le corps endormi par le `Sleeper` (qui tourne en début de pas) — le corps ne bougeait pas. | `BepuNarrowPhaseCallbacks` : coefficient de paire × `manifold.Count` pour les manifolds convexes (Coulomb) ; `BepuBodyBackend.WakeAfterGameplayChange` : `Awake = true` + remise à zéro de `SleepCandidate`/`TimestepsUnderThresholdCount` sur `LinearVelocity`, `WorldTransform`, `ApplyImpulse`. Tests `BoxLaunchedOnGround_StopsAtTheCoulombDistance` (±15 % de v²/2μg) et `VelocitySetOnARestingBody_MovesIt`. A/B headless Bullet/Bepu : 4,94 / 5,02 u. | `320fe5d9` |
