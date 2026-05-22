# Character Controller - plan agent IA

## Regles d'execution

- Mettre a jour ce fichier a chaque changement de statut.
- Faire un commit apres chaque tache terminee et validee.
- Garder chaque tache atomique et compilable.
- Ne pas inventer d'API : chaque dependance doit etre constatee dans le code ou creee par une tache explicite.
- Statuts autorises : ✅ Done, 🚧 In progress, ⏳ Todo, 🧪 Needs testing, ⚠️ Blocked.

## Decisions d'architecture

### ✅ Done - A1. Positionner `MoveTo` hors du noyau Character Controller

Decision : `MoveTo` est une commande de haut niveau et ne doit pas etre ajoutee directement a l'API noyau de `CharacterControllerComponent`.

Details :

- le noyau `CharacterControllerComponent` reste responsable de la locomotion et de la collision : `SetMoveIntent`, `RequestJump`, `Stop`, `Teleport`, vitesse, sol, pente, slide ;
- `MoveTo(destination, ...)` appartient a une couche au-dessus : IA, navigation, script ou cutscene ;
- cette couche convertit une destination ou un waypoint en intention compatible avec le controller, notamment via `SetMoveIntent(Vector2)` ;
- si une API plus directe devient necessaire, preferer une commande bas niveau d'intention ou de vitesse souhaitee plutot qu'un `MoveTo` portant destination, arret, echec, timeout et callbacks ;
- toute tache future mentionnant `MoveTo` doit preciser le driver qui le porte et ne pas l'ajouter au controller sans decision explicite.

## Prerequis / points bloquants

### ✅ Done - P0. Creer le plan de travail agent

Objectif : creer ce fichier avec un decoupage exploitable par un agent IA.

Livrables :

- sections prerequis, V1, V2, V3 ;
- statut avec icone en face de chaque tache ;
- regle de commit apres chaque tache.

Validation :

- le fichier existe ;
- le markdown n'a pas de diagnostic ;
- un commit documente la creation du plan.

### ✅ Done - P1. Exposer les requetes physiques necessaires

Objectif : rendre utilisables les sweeps de formes convexes depuis la couche physique CasaEngine.

Livrables :

- `PhysicsEngine.ShapeSweep(...)` public base sur `CollisionWorld.ConvexSweepTest` ;
- `PhysicsEngine.ShapeSweepPenetrating(...)` public pour collecter les hits ;
- equivalents exposes par `IPhysicsWorldContext`, `PhysicsWorldContext` et `PhysicsEngineComponent` ;
- filtrage par `CollisionFilterGroups` et `hitTriggers` coherent avec les raycasts existants ;
- tests physiques ciblant hit, no-hit et filtrage trigger basique si possible.

Validation :

- tests `CasaEngine.Tests` de la zone physics ;
- build solution.

### ✅ Done - P2. Ajouter les types de contrat Character Controller

Objectif : definir les types stables avant le solveur.

Livrables :

- `CharacterControlMode` ;
- `CharacterMovementState` ;
- `CharacterControllerSettings` ;
- `CharacterControllerGroundInfo` ou equivalent ;
- valeurs par defaut coherentes et validation simple.

Validation :

- tests unitaires de reglages/validation ;
- build solution.

## V1 - controleur cinematique 3D minimal

### ✅ Done - V1.1. Ajouter le composant et son contrat public

Objectif : ajouter `CharacterControllerComponent : EntityComponent` sans solveur complet.

Livrables :

- validation des dependances `Owner`, `Owner.RootComponent`, `CapsuleCollisionComponent`, `World.PhysicsWorldContext` ;
- proprietes publiques `Settings`, `ControlMode`, `MovementState`, `Velocity`, `IsGrounded`, `GroundNormal`, `GroundCollider`, `LastCollisionHit` ;
- commandes `SetMoveIntent`, `RequestJump`, `Stop`, `Teleport`, `SetControlMode` ;
- events minimaux `JumpStarted`, `Landed`, `GroundChanged`.

Validation :

- tests du contrat public et de la validation ;
- build solution.

### ✅ Done - V1.2. Implementer le moteur de locomotion sans collision

Objectif : calculer intention, acceleration, deceleration, gravite et saut sans encore resoudre les obstacles.

Livrables :

- vitesse horizontale clampee par `MaxHorizontalSpeed` ;
- gravite appliquee quand le personnage n'est pas grounded ;
- saut via `RequestJump()` ;
- transitions `Grounded`, `Jumping`, `Falling`, `Disabled` ;
- aucun LINQ/closure/allocation evitable dans `Update`.

Validation :

- tests de vitesse, stop, saut et disabled ;
- build solution.

### ✅ Done - V1.3. Ajouter sweep, slide, sol, pente et snap

Objectif : resoudre le mouvement principal avec la physique.

Livrables :

- sweep capsule sur le deplacement souhaite ;
- slide sur murs ;
- detection sol via sweep/raycast court ;
- rejet ou projection des pentes selon `MaxSlopeAngle` ;
- snap au sol via `GroundSnapDistance` ;
- `LastCollisionHit`, `GroundNormal`, `GroundCollider` mis a jour.

Validation :

- tests sol plat, mur, pente autorisee, pente refusee, saut, depart en penetration si l'API le permet ;
- build solution.

### ✅ Done - V1.4. Ajouter donnees de debug et documentation V1

Objectif : rendre le comportement inspectable sans ajouter un rendu debug complet non verifie.

Livrables :

- snapshot de debug ou proprietes publiques suffisantes ;
- documentation courte dans `character-controller-features.md` si le contrat final differe du plan ;
- limites V1 clairement notees.

Validation :

- tests existants ;
- build solution.

## V2 - enrichissement gameplay

### ✅ Done - V2.1. Step offset

Objectif : permettre de monter de petites marches sans saut.

Livrables : sweep up, forward, down ; validation de sol final ; tests de marche et obstacle trop haut.

Validation :

- tests `CharacterController` OK ;
- build `CasaEngine.Tests` OK via `dotnet test --filter CharacterController`.

### ✅ Done - V2.2. Plateformes mobiles simples

Objectif : heriter une translation simple du sol courant.

Livrables : vitesse de sol, changement de sol, saut depuis plateforme ; tests de translation horizontale/verticale.

Validation :

- tests `CharacterController` OK ;
- build `CasaEngine.Tests` OK via `dotnet test --filter CharacterController`.

### ✅ Done - V2.3. Bridge animation

Objectif : exposer les etats V1 vers `SkinnedMeshComponent` / `AnimationController` sans API Animator inventee.

Livrables : adaptateur optionnel, donnees de locomotion, tests de mapping pur.

Validation :

- tests de mapping locomotion OK ;
- tests `CharacterController` OK ;
- build `CasaEngine.Tests` OK via `dotnet test --filter CharacterController`.

### ✅ Done - V2.4. Bridge cutscene minimal

Objectif : permettre aux cutscenes de prendre et rendre l'autorite.

Livrables : helper script/cutscene, commande `MoveTo` portee par la couche cutscene/script si le solveur V1 est stable, conversion en intention controller, tests de mode de controle.

Implementation :

- `CharacterControllerMoveToDriverComponent` : driver haut niveau, prend le `CharacterControlMode.Cutscene`, convertit destination XZ en `SetMoveIntent(Vector2)`, restaure le mode precedent a l'arrivee, au timeout ou a l'annulation ;
- `MoveToCutsceneActionData` + serialization/validation/runtime coroutine : action cutscene `MoveTo` ciblee par nom d'entite, sans ajouter `MoveTo` au coeur du controller ;
- affichage read-only editor du detail `MoveTo`.

Validation :

- tests driver authority/timeout OK ;
- tests serialization/validation `MoveTo` OK ;
- test runtime `CutsceneDirector` -> `MoveTo` -> restore mode OK ;
- `dotnet test .\CasaEngine.Tests\CasaEngine.Tests.csproj --filter "Cutscene|CharacterController"` OK : 49 tests, 0 echec.

### ✅ Done - V2.5. Options gameplay usuelles

Objectif : ajouter uniquement les options demandant peu de dependances externes.

Livrables : coyote time, jump buffer, dash simple, crouch si la capsule peut changer de taille proprement.

Implementation :

- `CoyoteTimeSeconds` et `JumpBufferSeconds` dans `CharacterControllerSettings` ;
- buffer de saut conserve une demande jusqu'a l'atterrissage ou expiration ;
- coyote time autorise un saut juste apres la perte du sol ;
- `RequestDash(Vector2)` applique un dash horizontal simple avec duree/cooldown (`DashSpeed`, `DashDurationSeconds`, `DashCooldownSeconds`) ;
- crouch non implemente dans cette tranche : `CapsuleCollisionComponent` expose une capsule mutable mais pas d'API publique sure pour resize + recreation physique + validation headroom.

Validation :

- tests settings load/validation OK ;
- tests coyote time, jump buffer, dash/cooldown OK ;
- `dotnet test .\CasaEngine.Tests\CasaEngine.Tests.csproj --filter "CharacterController"` OK : 39 tests, 0 echec.

## V3 - systemes avances

### ✅ Done - V3.1. Root motion avec collision

Objectif : consommer `SkinnedMeshComponent.ConsumeRootMotionDelta()` via le solveur du controller.

Livrables : mode root motion, validation collision, tests de delta applique/consume.

Implementation :

- `IRootMotionDeltaSource` expose `RootMotionMode` et `ConsumeRootMotionDelta()` ;
- `SkinnedMeshComponent` implemente cette source ;
- `CharacterControllerRootMotionBridgeComponent` consomme la root motion, force `RootMotionMode.Apply` par defaut, applique la translation via `CharacterControllerComponent.Move(Vector3)` et peut appliquer la rotation ;
- `CharacterControllerComponent.Move(Vector3)` reutilise le solveur collision/slide du controller pour les deplacements externes.

Validation :

- test consommation root motion + application par controller OK ;
- test rotation optionnelle OK ;
- test `Move(Vector3)` collisionne avec sweep/slide OK ;
- `dotnet test .\CasaEngine.Tests\CasaEngine.Tests.csproj --filter "CharacterController"` OK : 42 tests, 0 echec.

### ✅ Done - V3.2. Navigation et IA

Objectif : connecter un agent de navigation au controller par intentions.

Livrables : adaptateur navigation, commande `MoveTo` portee par l'agent/driver de navigation, suivi de cible ou waypoint, conversion en `SetMoveIntent`, tests de commande sans input joueur.

Implementation :

- `CharacterControllerSteeringBridgeComponent` convertit `SteeringAgentComponent.CurrentCommand.DesiredVelocity` en `CharacterControllerComponent.SetMoveIntent(Vector2)` ;
- `CharacterControllerNavigationDriverComponent` porte `MoveTo`, `SetPath(IReadOnlyList<Vector3>)` et `FollowTarget(Entity)` en `CharacterControlMode.AI` ;
- les waypoints sont consommes tels quels : pas de nouveau solveur A* ni duplication de `PathPlanner`/`AStarSearch` ;
- mapping explicite steering XY -> controller XZ dans le bridge, configurable via `SteeringUsesXYPlane`.

Validation :

- test SteeringAgent -> intent controller OK ;
- tests `MoveTo`, chemin multi-waypoints et follow target OK ;
- `dotnet test .\CasaEngine.Tests\CasaEngine.Tests.csproj --filter "CharacterController|Navigation"` OK : 46 tests, 0 echec.

### ⚠️ Blocked - V3.3. Locomotion avancee

Objectif : ajouter les mouvements specifiques projet.

Livrables : wall jump, wall slide, ledge grab, ladder, swimming/flying selon besoins valides.

Blocage : aucun composant, asset, tag, volume ou regle gameplay existant ne definit encore wall jump, wall slide, ledge grab, ladder, swimming ou flying. Ne pas inventer ces mouvements dans le controller tant que les besoins projet et les donnees d'environnement ne sont pas valides.

Validation :

- recherche code/doc effectuee ;
- tache a rouvrir quand les mouvements attendus et leurs donnees d'environnement sont specifies.

### ✅ Done - V3.4. Prediction/replay deterministe

Objectif : rendre le controller compatible avec reseau ou replay.

Livrables : input snapshots, etat serialisable, correction, tests deterministes.

Implementation :

- `CharacterControllerInputSnapshot` capture `MoveIntent`, jump et dash, avec round-trip JSON ;
- `CharacterControllerStateSnapshot` capture position/orientation root, mode de controle, etat moteur, vitesse, timers jump/dash, sol et derniers deplacements, avec round-trip JSON ;
- `CharacterControllerComponent.CaptureInputSnapshot()` / `ApplyInputSnapshot(...)` pour rejouer les commandes ;
- `CharacterControllerComponent.CaptureStateSnapshot()` / `RestoreStateSnapshot(...)` pour correction/prediction sans declencher de commandes haut niveau.

Validation :

- tests snapshot input/state, correction et replay deterministe OK ;
- `dotnet test .\CasaEngine.Tests\CasaEngine.Tests.csproj --filter "CharacterController"` OK : 50 tests, 0 echec.

### ✅ Done - V3.5. Ragdoll transition

Objectif : basculer proprement entre controller cinematique et corps physiques.

Livrables : transfert transform/vitesse, desactivation controller, restauration controle.

Implementation :

- `CharacterControllerRagdollBridgeComponent` enregistre les corps physiques ragdoll (`PhysicsBaseComponent`) sans creer de systeme de squelette invente ;
- `EnterRagdoll()` sauvegarde l'etat du controller, transfere la vitesse aux corps enregistres, active leur simulation et passe le controller en `CharacterControlMode.Disabled` ;
- `ExitRagdoll()` restaure le controller, peut recopier position/orientation depuis le corps de reference, et peut optionnellement restaurer la vitesse depuis ce corps ;
- le bridge reste generique : il suppose que les corps ragdoll sont fournis par une couche animation/physics future.

Validation :

- tests entree ragdoll, sortie/restauration, vitesse de reference et anti-duplication des corps OK ;
- `dotnet test .\CasaEngine.Tests\CasaEngine.Tests.csproj --filter "CharacterController"` OK : 54 tests, 0 echec.