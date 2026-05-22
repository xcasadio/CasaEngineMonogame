# Character Controller - plan agent IA

## Regles d'execution

- Mettre a jour ce fichier a chaque changement de statut.
- Faire un commit apres chaque tache terminee et validee.
- Garder chaque tache atomique et compilable.
- Ne pas inventer d'API : chaque dependance doit etre constatee dans le code ou creee par une tache explicite.
- Statuts autorises : ✅ Done, 🚧 In progress, ⏳ Todo, 🧪 Needs testing, ⚠️ Blocked.

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

### ⏳ Todo - V1.4. Ajouter donnees de debug et documentation V1

Objectif : rendre le comportement inspectable sans ajouter un rendu debug complet non verifie.

Livrables :

- snapshot de debug ou proprietes publiques suffisantes ;
- documentation courte dans `character-controller-features.md` si le contrat final differe du plan ;
- limites V1 clairement notees.

Validation :

- tests existants ;
- build solution.

## V2 - enrichissement gameplay

### ⏳ Todo - V2.1. Step offset

Objectif : permettre de monter de petites marches sans saut.

Livrables : sweep up, forward, down ; validation de sol final ; tests de marche et obstacle trop haut.

### ⏳ Todo - V2.2. Plateformes mobiles simples

Objectif : heriter une translation simple du sol courant.

Livrables : vitesse de sol, changement de sol, saut depuis plateforme ; tests de translation horizontale/verticale.

### ⏳ Todo - V2.3. Bridge animation

Objectif : exposer les etats V1 vers `SkinnedMeshComponent` / `AnimationController` sans API Animator inventee.

Livrables : adaptateur optionnel, donnees de locomotion, tests de mapping pur.

### ⏳ Todo - V2.4. Bridge cutscene minimal

Objectif : permettre aux cutscenes de prendre et rendre l'autorite.

Livrables : helper script/cutscene, commande move-to si le solveur V1 est stable, tests de mode de controle.

### ⏳ Todo - V2.5. Options gameplay usuelles

Objectif : ajouter uniquement les options demandant peu de dependances externes.

Livrables : coyote time, jump buffer, dash simple, crouch si la capsule peut changer de taille proprement.

## V3 - systemes avances

### ⏳ Todo - V3.1. Root motion avec collision

Objectif : consommer `SkinnedMeshComponent.ConsumeRootMotionDelta()` via le solveur du controller.

Livrables : mode root motion, validation collision, tests de delta applique/consume.

### ⏳ Todo - V3.2. Navigation et IA

Objectif : connecter un agent de navigation au controller par intentions.

Livrables : adaptateur navigation, suivi de cible, tests de commande sans input joueur.

### ⏳ Todo - V3.3. Locomotion avancee

Objectif : ajouter les mouvements specifiques projet.

Livrables : wall jump, wall slide, ledge grab, ladder, swimming/flying selon besoins valides.

### ⏳ Todo - V3.4. Prediction/replay deterministe

Objectif : rendre le controller compatible avec reseau ou replay.

Livrables : input snapshots, etat serialisable, correction, tests deterministes.

### ⏳ Todo - V3.5. Ragdoll transition

Objectif : basculer proprement entre controller cinematique et corps physiques.

Livrables : transfert transform/vitesse, desactivation controller, restauration controle.