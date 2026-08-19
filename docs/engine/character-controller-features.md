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
- `Controller` possede une reference `Pawn` de type `Entity`, posee via `Possess`/`UnPossess` ; la possession pilote `CharacterControlMode` sur le `CharacterControllerComponent` de l'entite possedee, quand il existe.
- `PlayerController` porte le `PlayerInput` (facade d'input filtree), l'affectation de vue et l'integration UI.
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

Les reglages doivent rester petits et testables. Le tableau reflete le contenu reel de `CharacterControllerSettings` :

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
| `CoyoteTimeSeconds` | Fenetre de saut conservee apres avoir quitte le sol. Zero desactive l'assistance. |
| `JumpBufferSeconds` | Duree de memorisation d'une demande de saut avant le retour au sol. Zero desactive le buffer. |
| `DashSpeed` | Vitesse horizontale imposee pendant un dash. Zero desactive le dash. |
| `DashDurationSeconds` | Duree d'un dash. |
| `DashCooldownSeconds` | Delai avant de pouvoir relancer un dash. |
| `MaxSlopeAngle` | Angle maximum considere marchable. |
| `GroundSnapDistance` | Distance maximale de raccrochage au sol. |
| `StepHeight` | Hauteur maximale de marche franchissable au sol. Zero desactive le step offset. |
| `ProfileName` | Profil de collision du personnage, `CollisionProfileNames.Pawn` par defaut. Ses canaux bloques donnent le masque des sweeps via `GetSweepChannelMask()`. |
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

Exclusions d'origine implementees depuis : dash, coyote time, jump buffer, heritage de la vitesse lineaire du sol (plateformes mobiles simples), root motion avec collision (`CharacterControllerRootMotionBridgeComponent`), navigation par agent (`NavigationAgentComponent`, `CharacterControllerNavigationDriverComponent`) et transition ragdoll (`CharacterControllerRagdollBridgeComponent`). Voir « Etat V1 implemente ».

Restent exclus :

- Crouch/prone.
- Moving platforms completes : rotation et attache ne sont pas suivies, seule la vitesse lineaire du sol est heritee.
- IK pieds integre au controller ; un solveur IK deux os existe cote animation (`IkSolverTwoBone`).
- Ladders, ledge grab, wall jump, swimming, flying.
- Prediction reseau.
- Replay deterministe.

Ces sujets restants peuvent etre ajoutes ensuite ; les requetes physiques et le solveur de base sont en place.

## Etat V1 implemente

La V1 ajoute maintenant les pieces suivantes dans le code :

- `PhysicsEngine.ShapeSweep` et `ShapeSweepPenetrating`, exposes aussi via `IPhysicsWorldContext`, `PhysicsWorldContext` et `PhysicsEngineComponent` ;
- `CharacterControllerSettings`, `CharacterControlMode`, `CharacterMovementState`, `CharacterControllerGroundInfo` et `CharacterControllerDebugSnapshot` ;
- `CharacterControllerComponent : EntityComponent` avec validation de `Owner`, `RootComponent`, `CapsuleCollisionComponent` et `World.PhysicsWorldContext` ;
- commandes publiques `SetMoveIntent`, `RequestJump`, `RequestDash`, `Move`, `Stop`, `Teleport` et `SetControlMode` ;
- locomotion : acceleration, deceleration, gravite, saut, sweep capsule, slide, detection sol, limite de pente et snap au sol, completee depuis par step offset (`StepHeight`), coyote time, jump buffer, dash et heritage de la vitesse lineaire du sol ;
- donnees inspectables : `Velocity`, `IsGrounded`, `GroundNormal`, `GroundCollider`, `GroundVelocity`, `GroundSlopeAngle`, `LastCollisionHit`, `LastRequestedDisplacement`, `LastActualDisplacement`, `DebugSnapshot` et les timers restants de coyote time, jump buffer et dash ;
- composants compagnons livres depuis : bridge animation (`CharacterControllerAnimationBridgeComponent`, donnees de locomotion pour l'animation), root motion resolu par le solveur (`CharacterControllerRootMotionBridgeComponent`), bridge ragdoll (`CharacterControllerRagdollBridgeComponent`), drivers de deplacement et de navigation (`CharacterControllerMoveToDriverComponent`, `CharacterControllerNavigationDriverComponent`) et actions cutscene MoveTo/NavigateTo.

Limites connues de cette V1 :

- pas de depenetration initiale dediee au controller ; l'API multi-hit existe, mais la recuperation d'un depart deja en collision reste a traiter ;
- pas de crouch ; le portage par plateforme mobile reste limite a l'heritage de la vitesse lineaire du sol, sans suivi de rotation ni attache ;
- pas de rendu debug specifique au character controller ; les donnees sont exposees pour l'inspecteur, les tests ou un futur overlay ;
- les tests du solveur controller utilisent un `IPhysicsWorldContext` controle pour verifier les cas sol, mur et pente ; les tests Bullet natifs couvrent separement les sweeps convexes publics.

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
- Les tests couvrent au moins sol, mur, pente et saut. La penetration initiale reste une limite V1 documentee tant qu'une depenetration dediee n'est pas ajoutee.
- La resolution de mouvement respecte les regles de performance du repo : pas de LINQ, pas de closures, pas d'allocations evitables dans l'update.

## Champs de collision : prerequis releves avant integration au mover

La phase F du doc [collision-2d-3d-architecture.md](collision-2d-3d-architecture.md) a livre la
famille de colliders « champs » — `ICollisionField`, `GroundSample`, `HeightGridCollisionField` et
le slot `World.CollisionField` — mais **sans aucun cablage consommateur**. La resolution du sol par
champ appartient a ce chantier. Trois constats verifies sur `CharacterControllerComponent` doivent
etre traites avant, sinon l'integration sera fausse. Ils sont consignes ici pour que le suivi
demarre informe au lieu de les redecouvrir.

### 1. Prerequis d'axes

Le mover est Y up, X/Z horizontaux : la gravite (`CharacterControllerComponent` :580) et le snap au
sol (:815) travaillent sur `Vector3.Up`, et l'intention horizontale est mappee sur X/Z
(`GetDesiredHorizontalVelocity`, :946-949). La politique `TopDownElevation`, elle, definit Z comme
l'elevation (`SimulationSpacePolicy` :123-138). `ICollisionField` et `GroundSample` sont definis
dans la convention du mover (Y up). Un monde projete a donc besoin d'un **mover conscient de la
politique** avant de pouvoir consommer un champ ; adapter le champ n'est pas la bonne extremite.

### 2. `rootComponent.Position` est le CENTRE de la capsule

La forme de requete est translatee par la position brute de la racine (:903-913) et la
`CapsuleShape` Bullet est centree sur son origine (`BulletPhysicsEngine.cs`:375). Aucun helper
pied / demi-hauteur n'existe, ni sur le composant ni dans les settings. Un appelant de champ
choisit lui-meme sa position d'echantillonnage et possede ce decalage centre/pied : c'est
exactement la convention que l'integration doit fixer.

### 3. Le `- SkinWidth` du snap annule le retrecissement de la capsule

La capsule de requete est retrecie de `SkinWidth` sur les deux axes : rayon
`Radius - SkinWidth` (:881) et longueur de cylindre `Height - 2 * Radius` (:882). Sa demi-hauteur
vaut donc `Height / 2 - SkinWidth`. Le snap retire ensuite `SkinWidth` de la distance parcourue
(:829-833), ce qui **annule** ce retrecissement. Consequences a reproduire par tout placement base
sur un champ :

- Y d'equilibre de la racine au repos : `groundY + Height / 2` (exactement).
- Reference de sonde du sweep : `Position.Y - (Height / 2 - SkinWidth)`.

Mise en garde : ces deux egalites ne valent que **hors des clamps `MinSweepShapeSize`** appliques au
rayon et a la longueur de cylindre de la forme de requete. `CharacterControllerSettings.Validate`
(:125-138) verifie `Radius > 0`, `Height >= 2 * Radius` et `SkinWidth < Radius` ; aucune de ces
regles n'empeche une capsule assez
petite pour tomber dans ces clamps, auquel cas le calcul ci-dessus ne tient plus.

## Resume

Le bon point de depart pour CasaEngine est un `CharacterControllerComponent` cinematique, base sur les composants existants, pilote par intentions et resolu par la physique.

Le prealable technique est l'exposition propre des requetes de shape sweep/depenetration dans la couche physique. Sans cela, le controller ne pourra pas fournir les garanties attendues d'un moteur moderne : collision stable, slide, pentes, snap au sol et etat fiable pour l'animation et les cutscenes.