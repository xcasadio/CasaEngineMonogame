# Character Controller - analyse et plan CasaEngine

## Objectif

Ce document affine le besoin pour ajouter un `CharacterController` moderne dans CasaEngine sans presenter comme existant ce qui n'a pas ete verifie dans le code.

Le but n'est pas de remplacer la physique, l'animation, l'input ou les cutscenes. Le `CharacterController` doit devenir la couche gameplay qui transforme une intention de mouvement en deplacement resolu dans le monde.

Le document cible d'abord un controleur 3D a capsule, car le moteur contient deja un `CapsuleCollisionComponent` base sur `BulletSharp.CapsuleShape`. Un controleur 2D devra etre traite separement.

## Critique du brouillon initial

- Le brouillon melangeait l'analyse, la vision long terme, les details d'API, les modes cutscene et les fonctionnalites avancees sans distinguer l'existant du travail a faire.
- Plusieurs noms etaient presentes comme naturels alors qu'ils ne sont pas constates dans le code actuel : `LayerMask`, `AnimatorComponent`, `DebugRenderer`, `SweepCapsule`, `ComputePenetration`, `FixedUpdatePhysics`, `Animator.SetBool`, `Animator.SetFloat`.
- L'exemple `CharacterControllerComponent : Component` ne correspond pas aux bases existantes du moteur. Les composants observes derivent de `EntityComponent` ou `SceneComponent`.
- La V1 etait trop large : moving platforms, root motion, IK, dash, crouch, navmesh, network prediction et cutscene bridge complet ne doivent pas bloquer le premier controleur jouable.
- La partie physique etait formulee comme si les sweeps etaient deja exposes par l'API publique, alors que `IPhysicsWorldContext` n'expose actuellement que des ajouts/retraits d'objets, des ghost objects, des rigid bodies et deux raycasts historiques.
- La partie animation supposait une API d'Animator a parametres. Le moteur expose un `AnimationController`, un `SkinnedMeshComponent`, des graphes d'animation, des evenements et du root motion, mais pas d'API type `SetFloat` / `SetBool`.

## Etat verifie dans le moteur

### Gameplay

- `Pawn`, `Controller` et `PlayerController` existent dans `CasaEngine.Framework.Gameplay`.
- `Controller` possede une reference vers un `Pawn`.
- `PlayerController` gere un `Player`, un etat d'input, une affectation de vue et une integration UI.
- Aucun `CharacterControllerComponent` n'a ete trouve dans le code actuel.

### Composants et transforms

- `EntityComponent` est la base des comportements sans transform propre.
- `SceneComponent` ajoute `Coordinates`, position, orientation, scale, hierarchie parent/enfant et matrices monde.
- Les composants de collision existants derivent de `PhysicsBaseComponent`, lui-meme derive de `SceneComponent`.
- Un controleur de personnage ne devrait donc pas heriter de `PhysicsBaseComponent` par defaut. Il devrait coordonner un transform existant et un composant de collision existant.

### Physique

- Chaque `World` possede un `PhysicsWorldContext` via `World.PhysicsWorldContext`.
- `IPhysicsWorldContext` expose :
  - creation de ghost objects ;
  - ajout/retrait de `CollisionObject` ;
  - ajout/retrait de `RigidBody` ;
  - nettoyage des collisions d'un `ICollideableComponent` ;
  - `WorldRayCast` ;
  - `NearBodyWorldRayCast`.
- `PhysicsWorldContext.PhysicsEngine` donne acces au `PhysicsEngine`.
- `PhysicsEngine` expose des raycasts modernes : `Raycast`, `RaycastPenetrating`, `HitResult`, `CollisionFilterGroups` et filtrage `hitTriggers`.
- Les methodes `ShapeSweep` et `ShapeSweepPenetrating` sont presentes seulement en code commente. Elles ne sont pas utilisables en l'etat et referencent un style d'API a adapter avant reutilisation.
- Les callbacks internes de sweep convexe existent dans `PhysicsEngine`, mais aucun sweep public n'est actuellement expose via `IPhysicsWorldContext`.
- `CapsuleCollisionComponent` existe et cree un `CapsuleShape`.
- `PhysicsType.Kinetic` existe, mais `PhysicsBaseComponent.AdvanceKinematic` applique directement `Position += Velocity * elapsedTime` puis synchronise l'objet physique. Ce comportement ne suffit pas pour un character controller moderne, car il ne resout pas les sweeps, le slide, les pentes ou la depenetration.

### Temps et update

- `FrameTime` expose `DeltaTime`, `UnscaledDeltaTime`, `TotalTime`, `UnscaledTotalTime`, `TimeScale` et `FrameIndex`.
- `World.Update(FrameTime)` utilise `frameTime.DeltaTime` pour mettre a jour le gameplay.
- `EntityComponent.Update` recoit actuellement un `float elapsedTime`, pas un `FrameTime` complet.
- `PhysicsEngine` possede `FixedTimeStep` et `MaxSubSteps` et appelle `StepSimulation(deltaTime, MaxSubSteps, FixedTimeStep)`.

Implication : une V1 peut utiliser `elapsedTime`. Si le controller a besoin de `FrameIndex`, temps non scale ou phase fixed dediee, il faudra etendre la phase d'update au lieu de supposer qu'elle existe deja.

### Animation

- `SkinnedMeshComponent` existe.
- `AnimationController` expose lecture de clips, graphes, layers, cross-fade, evenements d'animation et root motion.
- `SkinnedMeshComponent` expose `RootMotionMode` et `ConsumeRootMotionDelta()`.
- Aucun `AnimatorComponent` ni API de parametres `SetFloat` / `SetBool` n'a ete constate.

Implication : la V1 doit exposer un etat de locomotion exploitable par l'animation, sans appeler une API d'Animator inexistante. Un bridge animation peut etre ajoute ensuite vers les pieces deja presentes.

### Cutscenes et coroutines

- `World.CoroutineManager` existe.
- `CutsceneDirector` existe.
- Les entites et composants peuvent demarrer/arretter des coroutines via leur `World`.

Implication : les cutscenes pourront piloter le controleur via une API publique de commandes, mais le document ne doit pas pretendre que des actions dediees au personnage existent deja.

### Debug

- `PhysicsDebugViewRendererComponent` existe pour le debug physique Bullet.
- Aucun debug draw dedie au character controller n'a ete constate.

Implication : la V1 doit au minimum exposer des donnees de debug. Le rendu specifique de la capsule, des sweeps, des normales et du sol peut etre une tache separee.

## Decision d'architecture proposee

La V1 doit etre un controleur cinematique gameplay.

Le personnage n'est pas pilote par des forces physiques. Le controller calcule un deplacement souhaite, interroge la physique, resout les collisions, puis applique une position finale au transform de l'entite.

Cette decision correspond mieux au besoin d'un personnage jouable moderne : precision, stabilite, controle des pentes, saut reproductible et synchronisation claire avec l'animation.

Le controller dynamique base sur rigid body ne doit pas etre la V1. Il peut rester utile plus tard pour ragdoll, objets physiques, knockback violent ou vehicules, mais il ne donne pas le niveau de controle attendu pour la locomotion principale.

## Forme recommandee pour CasaEngine

### Type principal a creer

`CharacterControllerComponent` doit etre cree comme composant gameplay.

Base recommandee pour la V1 : `EntityComponent`.

Raison : le commentaire de `EntityComponent` indique que cette base convient aux comportements abstraits comme le mouvement et ne possede pas de transform propre. Le controller doit piloter le `RootComponent` de l'entite ou un `SceneComponent` explicitement configure, pas devenir lui-meme un collider physique.

Conditions a valider au demarrage :

- `Owner` non null ;
- `Owner.RootComponent` non null ;
- un composant de collision compatible existe sur l'entite, par exemple `CapsuleCollisionComponent` pour la V1 3D ;
- `Owner.World.PhysicsWorldContext` disponible ;
- les requetes physiques necessaires au controller sont disponibles.

### Dependances physiques a ajouter avant la V1

Le point bloquant principal est l'API de requetes physiques.

Avant d'ecrire le solveur du character controller, il faut exposer proprement :

1. un sweep de forme convexe ;
2. un sweep penetrant ou une variante qui renvoie tous les hits utiles ;
3. une requete overlap ou depenetration pour sortir d'un etat initial deja en collision ;
4. le filtrage par `CollisionFilterGroups` ou une abstraction CasaEngine equivalente ;
5. l'exclusion explicite du collider du personnage pendant ses propres requetes.

Les methodes commentees `ShapeSweep` dans `PhysicsEngine` peuvent servir d'indice, mais elles ne doivent pas etre decommentee sans adaptation. Elles referencent un type `ColliderShape` et une variable `collisionWorld` qui ne correspondent pas directement au code actif actuel.

### Donnees minimales du controller

| Donnee | Statut | Note |
| --- | --- | --- |
| `CharacterControllerSettings` | A creer | Conteneur serialisable des reglages. |
| `CharacterControlMode` | A creer | Source d'autorite : player, IA, script/cutscene, disabled. |
| `CharacterMovementState` | A creer | Etat de mouvement : grounded, falling, jumping, sliding, disabled. |
| `Velocity` | A creer | Vitesse resolue par le controller. |
| `IsGrounded` | A creer | Etat calcule par requetes physiques, pas simple flag manuel. |
| `GroundNormal` | A creer | Normale du sol courant. |
| `GroundCollider` | A creer | `PhysicsBaseComponent?` touche comme sol si disponible via `HitResult`. |
| `LastCollisionHit` | A creer | Dernier `HitResult` utile pour debug et diagnostics. |

### Reglages V1

Les reglages doivent rester petits et testables :

| Reglage | Role |
| --- | --- |
| `Radius` | Rayon de capsule. |
| `Height` | Hauteur totale de capsule. |
| `SkinWidth` | Marge de securite pour limiter les tremblements. |
| `MaxHorizontalSpeed` | Vitesse horizontale maximale. |
| `Acceleration` | Vitesse de convergence vers l'intention de mouvement. |
| `Deceleration` | Ralentissement sans input. |
| `Gravity` | Gravite appliquee par le controller. |
| `JumpSpeed` | Vitesse verticale initiale du saut. |
| `MaxSlopeAngle` | Angle maximum considere marchable. |
| `GroundSnapDistance` | Distance maximale de raccrochage au sol. |
| `CollisionGroup` | Groupe Bullet/CasaEngine du personnage. |
| `CollisionMask` | Masque des objets solides testes par le personnage. |
| `HitTriggers` | Indique si les triggers doivent etre inclus dans les requetes. |

Ne pas utiliser `LayerMask` dans cette V1 : aucun type portant ce nom n'a ete constate. Si une abstraction de masque CasaEngine est creee plus tard, elle devra mapper proprement vers le filtrage physique existant.

### Commandes publiques V1

Ces commandes sont a creer sur le controller. Elles decrivent le contrat attendu, pas une API deja presente.

| Commande | Role |
| --- | --- |
| `SetMoveIntent(Vector2 direction)` | Definit l'intention horizontale normalisee ou clampee. |
| `RequestJump()` | Demande un saut pour la prochaine resolution possible. |
| `Stop()` | Annule l'intention et remet la vitesse horizontale a zero. |
| `Teleport(Vector3 position)` | Place le personnage sans sweep, puis force une resynchronisation physique. |
| `SetControlMode(CharacterControlMode mode)` | Change la source autorisee a commander le personnage. |

Le controller ne doit pas lire directement clavier, souris, gamepad, IA ou cutscene. Ces systemes produisent des intentions ; le controller les resout.

## Pipeline de mouvement V1

1. Lire l'intention courante deja fournie au controller.
2. Calculer la vitesse horizontale par acceleration/deceleration.
3. Appliquer gravite et saut a la vitesse verticale.
4. Construire le deplacement souhaite pour `elapsedTime`.
5. Lancer un sweep de capsule dans la direction du deplacement.
6. Avancer jusqu'au hit autorise, avec `SkinWidth`.
7. Si un mur est touche, projeter le reste du mouvement sur le plan de collision pour glisser.
8. Detecter le sol avec une requete courte vers le bas.
9. Rejeter ou projeter le mouvement sur les pentes selon `MaxSlopeAngle`.
10. Appliquer `GroundSnapDistance` seulement si le personnage descend ou reste au sol.
11. Ecrire la position finale sur le `SceneComponent` pilote.
12. Synchroniser le composant de collision si necessaire.
13. Publier `Velocity`, `IsGrounded`, `GroundNormal`, `MovementState` et les evenements de transition.

Contraintes moteur : pas de LINQ, pas de closures et pas d'allocations evitables dans `Update` ou dans la resolution de mouvement.

## Scope V1 corrige

### Inclus

- `CharacterControllerComponent` 3D cinematique.
- Reglages serialisables.
- Capsule comme forme principale.
- Mouvement horizontal.
- Acceleration/deceleration.
- Gravite.
- Saut simple.
- Detection du sol.
- Pentes marchables et non marchables.
- Sweep + slide contre les obstacles.
- Snap au sol.
- Teleport et stop.
- Etats publics pour gameplay/animation.
- Evenements minimaux : jump started, landed, ground changed.
- Donnees de debug consultables.

### Exclu de la V1

- Dash.
- Crouch/prone.
- Coyote time.
- Jump buffer.
- Moving platforms completes.
- Root motion avec collision.
- IK pieds.
- Navmesh agent.
- Ladders, ledge grab, wall jump, swimming, flying.
- Prediction reseau.
- Replay deterministe.
- Ragdoll transition.

Ces sujets peuvent etre ajoutes en V2/V3 une fois les requetes physiques et le solveur de base valides.

## Animation

La V1 ne doit pas appeler une API d'Animator inexistante.

Elle doit seulement exposer des donnees :

- `MovementState` ;
- `IsGrounded` ;
- `Velocity` ;
- vitesse horizontale ;
- vitesse verticale ;
- direction de mouvement ;
- normale du sol ;
- angle de pente.

Un bridge animation pourra ensuite traduire ces donnees vers `SkinnedMeshComponent`, `AnimationController`, graphes d'animation ou blend spaces.

Pour le root motion, la regle est stricte : un delta venant de `SkinnedMeshComponent.ConsumeRootMotionDelta()` ne doit pas etre applique directement au transform. Il devra passer par le meme solveur de collision que le mouvement gameplay. Cette integration est hors V1.

## Cutscenes

Les cutscenes ne doivent pas contourner le controller quand elles deplacent un personnage.

La V1 doit seulement fournir un mode de controle explicite :

- `Player` ;
- `AI` ;
- `Script` ou `Cutscene` ;
- `Disabled`.

Une cutscene peut ensuite prendre l'autorite, envoyer des intentions ou teleporter le personnage via l'API publique, puis rendre l'autorite. Les actions de cutscene dediees au personnage restent a creer plus tard ; elles ne sont pas considerees comme existantes dans ce document.

## Ordre d'implementation recommande

1. Exposer et tester les requetes physiques manquantes : sweep convexe, sweep multi-hit ou overlap/depenetration, filtrage, ignore self.
2. Ajouter `CharacterControllerSettings` avec serialization selon les patterns existants `Load(JObject)`.
3. Ajouter `CharacterControllerComponent : EntityComponent` avec validation des dependances, etat public et commandes d'intention.
4. Implementer le solveur capsule : sweep, slide, sol, pente, snap.
5. Ajouter des tests ciblant les cas physiques : sol plat, mur, pente autorisee, pente refusee, saut, depart en penetration.
6. Ajouter les donnees de debug et, si necessaire, un rendu debug dedie.
7. Ajouter ensuite seulement les bridges animation et cutscene.

## Criteres d'acceptation

- Le controller ne deplace pas le personnage par simple `Position += Velocity * deltaTime` hors teleport explicite.
- Le mouvement principal passe par des requetes physiques.
- Le controller ne depend pas d'une API d'input concrete.
- Le controller ne depend pas d'une API d'Animator non existante.
- Les noms de types nouveaux sont clairement ajoutes dans l'implementation, pas supposes presents.
- La V1 compile sans casser `Pawn`, `Controller`, `PlayerController`, `PhysicsBaseComponent` ou `SkinnedMeshComponent`.
- Les tests couvrent au moins sol, mur, pente, saut et penetration initiale.
- La resolution de mouvement respecte les regles de performance du repo : pas de LINQ, pas de closures, pas d'allocations evitables dans l'update.

## Resume

Le bon point de depart pour CasaEngine est un `CharacterControllerComponent` cinematique, base sur les composants existants, pilote par intentions et resolu par la physique.

Le prealable technique est l'exposition propre des requetes de shape sweep/depenetration dans la couche physique. Sans cela, le controller ne pourra pas fournir les garanties attendues d'un moteur moderne : collision stable, slide, pentes, snap au sol et etat fiable pour l'animation et les cutscenes.