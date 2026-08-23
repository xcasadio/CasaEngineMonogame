# Architecture collision 2D / 2.5D / 3D

Ce document définit une architecture cible pour la collision dans CasaEngine, couvrant d'un seul
tenant les jeux 2D purs, les jeux 2.5D top-down ou isométriques et les jeux 3D. Il répond à la
question fondatrice « faut-il des shapes 3D pour un jeu 2D ? » en la reformulant : **la
dimensionnalité d'un jeu est une configuration du monde, pas une architecture**.

Méthode : état vérifié dans le code d'abord, comparaison avec les moteurs modernes ensuite,
décisions et migration à la fin. Les noms de types introduits ici sont des **propositions**, pas
des API existantes.

**Posture de compatibilité (décision projet, 2026-08)** : aucun projet n'utilise CasaEngine en
dehors des démos, de l'éditeur, des tests et du convertisseur Alundra (qui ne référence aucun type
physique — vérifié). La rétrocompatibilité des API et des assets sérialisés n'est **pas** un
objectif : on remplace, on ne double pas ; chaque phase supprime ce qu'elle remplace. Deux
invariants seulement : le dépôt compile et les démos/tests passent à chaque phase ; les assets
régénérables (export Alundra) sont revalidés par un export complet.

## Règle centrale

> Une seule simulation physique, en 3D, pour tous les jeux. La « 2D » d'un jeu est une politique du
> monde — plan de simulation, abaissement des formes d'authoring, mapping simulation → rendu —
> jamais une seconde pile physique, et jamais une hypothèse cachée dans les composants ou les assets.

C'est le miroir exact de la règle de projection du rendu
([rendering-2d-3d-spaces.md](rendering-2d-3d-spaces.md)) : « on ne met pas une tilemap en 2D, on
assigne une caméra orthographique à la vue ». Côté physique : **on ne crée pas une "collision 2D",
on assigne une politique d'espace au monde.**

---

## 1. État vérifié dans le code

Cette section décrit uniquement ce qui est observable dans le dépôt au moment de la rédaction.

> **Backend Bepu (2026-08).** Les mentions de Bullet ci-dessous décrivent l'état du dépôt au moment
> de la rédaction de ce document ; le backend a depuis été remplacé par bepuphysics2 (voir
> [analysis-bepuphysics2-migration.md](../../ai-agent/audits/analysis-bepuphysics2-migration.md) et
> [bepu-physics-migration-tasks.md](../../ai-agent/tasks/bepu-physics-migration-tasks.md)). Ce qui a
> changé pour les points touchés par ce document : Bepu n'a pas d'échelle de forme, l'échelle locale
> est cuite dans les dimensions et les offsets de compound à la création ; le capteur n'est plus un
> flag de corps mais une décision prise dans le callback de narrow-phase
> (`ConfigureContactManifold` retourne `false`, pas de contrainte) ; les contacts portent
> l'index d'enfant des deux côtés d'une paire, y compris pour les compounds — une amélioration par
> rapport au BulletSharp vendorisé qui ne le donnait que côté sweep/raycast, jamais côté contact ;
> `LinearFactor` n'a pas d'équivalent natif et s'implémente par annulation de vitesse dans
> `IPoseIntegratorCallbacks.IntegrateVelocity` ; les champs `PhysicsDefinition` propres à Bullet
> (`AdditionalDamping*`, `RollingFriction`, `LocalInertia`, les deux seuils de sommeil séparés) sont
> supprimés, remplacés par un unique `SleepThreshold`.
>
> `PhysicsDefinition.Load` tolère désormais un nœud sans `physics_type` (le type courant est conservé).

### Trois vocabulaires de formes, aucun pivot

- [Shape2d](../../CasaEngine/Engine/Geometry/Shape2d.cs) (Compound, Polygone,
  Rectangle, Circle, Line) portait une pose (`Position` Vector2 + `Rotation`) — **supprimée en
  phase C** : la pose vit désormais sur `Collision2d` (`LocalPosition` + `Rotation`), l'attache
  d'authoring commune aux sprites et aux tuiles (`TileData.CollisionShape`).
- [Shape3d](../../CasaEngine/Engine/Geometry/Shape3d.cs) (Compound, Box, Capsule,
  Cylinder, Sphere, Cone) ne porte **pas** de pose. Consommé par `BoxCollisionComponent`.
- `PhysicsShape` (Box, Sphere, Capsule, Cylinder) était un troisième vocabulaire, public, avec un
  handle backend interne. **Supprimé en phase B** :
  [IPhysicsWorld](../../CasaEngine/Framework/Application/Components/Physics/IPhysicsWorld.cs)
  consomme désormais `Shape3d` directement, et les sweeps prennent un handle
  [PhysicsQueryShape](../../CasaEngine/Engine/Physics/PhysicsQueryShape.cs) créé par le monde.

Les deux premiers vivaient sous `Framework/Rendering/Geometry` alors qu'ils décrivent de la
collision — habitat erroné, corrigé en phase B : ils vivent maintenant sous
`CasaEngine/Engine/Geometry` (namespace `CasaEngine.Engine.Geometry`).

### L'abaissement 2D → physique est codé en dur

`Physics2dHelper.CreateCollisionsFromSprite` convertissait `ShapeRectangle → PhysicsShape.CreateBox(w/2, h/2, 0.5f)`
— demi-profondeur `0.5f` en dur — et jetait `ArgumentOutOfRangeException` pour Circle, Line,
Polygone, Compound. Le fait important : **le moteur crée déjà des volumes 3D pour ses jeux 2D**.
**Supprimé en phase C** : l'abaissement appartient à
[SimulationSpacePolicy.Lower](../../CasaEngine/Engine/Physics/SimulationSpacePolicy.cs), avec une
extrusion paramétrée, et
[SpriteCollisionHelper](../../CasaEngine/Framework/Scene/Entities/Components/SpriteCollisionHelper.cs)
crée les corps des volumes de sprite.

### Un corps = une forme

Aucun usage de `CompoundShape` dans le moteur. Un `PhysicsBody` porte exactement une forme, sans
pose locale : un offset de forme par rapport à l'entité n'a aujourd'hui **nulle part où vivre**.

### Le filtrage est du Bullet brut

`PhysicsCollisionFilterGroups` (supprimé depuis, phase A — voir
[CollisionProfiles.cs](../../CasaEngine/Engine/Physics/CollisionProfiles.cs))
recopiait les groupes par défaut de Bullet (`DefaultFilter`, `StaticFilter`, `DebrisFilter`…), sans
signification gameplay, et fuit dans la signature publique des sweeps d'`IPhysicsWorld`. La seule
sémantique gameplay existante, `CollisionHitType` (`Unknown`/`Attack`/`Defense`), vivait dans un
asset sprite et ne pilotait… qu'une couleur de debug draw. **Supprimé en phase C** : un volume de
sprite nomme un profil (`Collision2d.ProfileName`, profils réservés `AttackVolume` et
`DamageableVolume`), qui pilote à la fois le filtrage et la couleur de debug.

### Les événements ignorent la forme

[Collision](../../CasaEngine/Framework/Physics/Collision.cs) et
[ContactPoint](../../CasaEngine/Framework/Physics/ContactPoint.cs) identifient une **paire de
composants**. Impossible d'attribuer « quelle hitbox a touché quelle hurtbox » — l'information de
la forme en contact est perdue.

### La pose physique est asservie à la pose de rendu

[PhysicsBaseComponent](../../CasaEngine/Framework/Scene/Entities/Components/PhysicsBaseComponent.cs)
pousse `WorldMatrixNoScale` dans le corps (événements `PositionChanged`/`OrientationChanged`).
Correct tant que rendu = simulation ; faux dès qu'un genre projette (top-down à élévation : le Y
écran mélange profondeur au sol et hauteur — deux entités à des élévations différentes qui se
chevauchent à l'écran collisionneraient).

### La collision animée existe, à la mauvaise granularité

[AnimatedSpriteComponent](../../CasaEngine/Framework/Scene/Entities/Components/AnimatedSpriteComponent.cs)
crée des corps par **sprite id** (`_collisionObjectsBySpriteId`), choisis via
`GetPrimarySpriteId()` (premier part visible), sous l'opt-in `CreatePhysicsForEachFrame`. Or une
hitbox de gameplay est une donnée de **frame d'animation** (elle change au cours d'une attaque),
pas une donnée de sprite (un sprite est partageable entre animations).
**Supprimé en phase E** : les volumes d'un sprite animé viennent désormais de la timeline de
fixtures de l'asset d'animation ; `CreatePhysicsForEachFrame` garde son sens (opt-in des volumes
pilotés par l'animation).

### Divers

- `IsPhysics2dActivated` enregistrait le pipeline `Convex2DShape`/`Box2DShape` de Bullet, mais aucun
  code ne créait jamais ces shapes : vestige mort, **supprimé en phase C**.
- Le verrouillage de plan existait **par branche de classes** : `Physics2dComponent` n'existait que
  pour poser `LinearFactor = (1,1,0)` et `AngularFactor = (0,0,1)` dans son constructeur —
  exactement la future politique `Planar2d`, codée en héritage. Composant supprimé depuis ; les
  défauts de verrouillage sont fournis par `Planar2dSimulationSpacePolicy.ApplyDefaultConstraints`
  depuis la phase D.
- L'abaissement est incohérent entre composants : `Box2dCollisionComponent` extrude son rectangle
  à une profondeur de 1 (`0.5f` en demi-étendue), `CircleCollisionComponent` abaisse son cercle en
  **sphère** (profondeur `2r`). Deux colliders 2D d'une même scène n'ont pas la même épaisseur.
- Le chantier character controller est engagé au-delà de son doc
  ([character-controller-features.md](character-controller-features.md)) : `CharacterControllerComponent`
  et `CharacterControllerSettings` existent, avec des sweeps publics sur `IPhysicsWorld` et des
  tests — et `CharacterControllerSettings` expose `CollisionGroup`/`CollisionMask` en groupes
  Bullet bruts. C'est le premier client du filtrage propre.

### Synthèse des manques

```text
- trois vocabulaires de formes, aucun pivot commun ;
- pas de notion de fixture (forme + pose locale + sémantique) ;
- un corps = une forme : pas de compound ;
- filtrage = groupes Bullet bruts, sans signification gameplay ;
- sémantique de hit enfouie dans un asset sprite, convertie en couleur ;
- événements sans identité de forme ;
- pose physique asservie à la pose de rendu ;
- terrain : rien de prévu — et le baker en corps statiques serait le mauvais chemin.
```

---

## 2. Ce que font les moteurs modernes

| Moteur | Modèle | À retenir | À éviter |
|---|---|---|---|
| Unity | Deux piles parallèles (PhysX 3D / Box2D 2D), matrice de layers | Simplicité de la matrice layer × layer | Le fork 2D/3D intégral : API doublée, 2.5D orphelin |
| Unreal | Une seule sim 3D ; canaux + réponses (`Ignore`/`Overlap`/`Block`) + **profils nommés** ; corps = agrégat de formes posées | Le modèle sémantique le plus complet ; profils nommés = sérialisation stable et édition centrale | La matrice de réponses complète peut être surdimensionnée |
| Godot | Serveurs 2D/3D séparés ; corps + nœuds `CollisionShape` (ressource de forme + pose) ; `layer`/`mask` asymétriques | La séparation **ressource de forme / nœud d'attachement** ; layer (ce que je suis) vs mask (ce que je scanne) | Encore un fork 2D/3D |
| Stride | Descripteurs de formes (liste → compound) ; groupes + flags | L'ascendant direct du code actuel : sa couche *descripteurs* est exactement le chaînon manquant ici | — |
| Box2D | body / **fixture** (forme + matériau + filtre + sensor) | Le concept de fixture | — |
| Moteurs de genre (CrossCode, ARPG classiques) | Espace logique `(x, y_sol, z_élévation)` ≠ espace écran ; terrain = heightfield de tuiles interrogé analytiquement ; entités = AABB | Simulation ≠ rendu comme donnée de premier ordre ; séparation **volumes / champs** | Figer la projection en dur dans le gameplay |
| Jeux de combat (frame data) | Boîtes typées keyframées sur l'animation ; overlap pur, hors solveur ; attribution par boîte | Timelines de fixtures ; identité de la boîte dans l'événement | Faire passer les hitboxes par le solveur |

Trois leçons transversales :

1. **Tous convergent vers shape / fixture / body / world.** CasaEngine a shape, body et world —
   mais pas fixture. C'est ce trou qui produit les helpers codés en dur et les offsets sans domicile.
2. **Le fork 2D/3D est l'erreur historique la plus chère** (Unity la paie encore). La simulation
   unique + contraintes de plan est le chemin moderne — et c'est de fait le pari déjà pris par
   CasaEngine, qui n'a que Bullet.
3. **La sémantique de collision doit être des données de projet nommées** (canaux, profils), pas
   des enums backend ni des booléens épars.

---

## 3. Les six décisions

### D1 — Une seule simulation, en 3D ; la dimensionnalité est une politique

Le moteur ne crée jamais de seconde pile « 2D ». Un jeu 2D utilise la simulation 3D avec un
verrouillage de plan ; un jeu 2.5D y ajoute un mapping d'espace ; un jeu 3D est le cas identité.

- Le coût de la 3D pour un jeu 2D est marginal : même broadphase, narrowphase box-box triviale.
- Les leviers existent déjà : `LinearFactor = (1,1,0)` + `AngularFactor = (0,0,1)` verrouillent un
  jeu au plan XY. La politique ne fait que fournir ces défauts.
- Le pipeline `Convex2D` de Bullet peut revenir un jour comme **optimisation interne** de la
  politique planaire — jamais comme API. `IsPhysics2dActivated` est absorbé ou retiré.

### D2 — Quatre couches : Shape, Fixture, Body, World

```text
Shape    géométrie pure, immuable, partageable, SANS pose        (Shape3d, déjà là)
Fixture  forme + pose locale + sémantique (profil, sensor, tag)  (MANQUANT — le trou central)
Body     type de mouvement + pose de simulation + N fixtures     (PhysicsBody, mono-forme aujourd'hui)
World    broadphase, dispatch, requêtes, événements, politiques  (IPhysicsWorld / BulletPhysicsEngine)
```

Décisions de vocabulaire :

- **`Shape3d` devient l'unique vocabulaire public des volumes.** Il est déjà sérialisé
  (`SaveShape3d` gère Box/Capsule/Cylinder/Sphere), déjà consommé par des composants.
- **`PhysicsShape` est supprimé.** Le backend abaisse directement `Shape3d` → formes Bullet dans
  `BulletPhysicsEngine` ; les requêtes publiques (sweeps) prennent `Shape3d` + pose. Aucun
  intermédiaire public entre le vocabulaire de formes et le backend.
- **`Shape2d` reste l'authoring 2D** (sprites, tiles) et *s'abaisse* vers `Shape3d` via la
  politique d'espace du monde (D4) : `Rectangle → Box(w, h, profondeur d'extrusion)`,
  `Circle → Cylinder` sur l'axe normal au plan, `Polygone → hull extrudé` (v2). Le `0.5f` de
  `Physics2dHelper` devient un paramètre nommé de la politique.
- **Aucune forme ne porte de pose** : la pose appartient à la fixture. `Shape3d` est déjà propre ;
  `Shape2d` perd `Position`/`Rotation` (son défaut historique), qui migrent vers l'attachement
  d'authoring — `Collision2d` devient la fixture d'authoring 2D : forme pure + pose + profil + tag,
  le miroir exact de `ColliderFixture`.

Forme conceptuelle de la fixture :

```csharp
// nom possible : ColliderFixture
public sealed class ColliderFixture
{
    public Shape3d Shape;             // géométrie pure, partageable entre fixtures
    public Vector3 LocalPosition;     // pose locale dans le corps
    public Quaternion LocalRotation;
    public string ProfileName;        // profil de collision, résolu en index au chargement
    public bool IsSensor;             // overlap pur, aucune réponse de contact
    public string Tag;                // identité gameplay : "sword", "hurt_head", "push"…
}
```

Contrainte backend assumée à la rédaction (Bullet filtre **par corps**, pas par forme) : le runtime
regroupe donc les fixtures par profil — une entité qui porte à la fois une hurtbox et une hitbox
d'attaque possède plusieurs corps. C'est un détail d'implémentation du backend, pas du modèle de
données ; toujours vrai avec Bepu, qui filtre aussi par collidable (corps ou static) via
`AllowContactGeneration`, pas par enfant de compound.

### D3 — Canaux, réponses, profils nommés

Modèle Unreal réduit à l'essentiel :

```csharp
public enum CollisionResponse { Ignore, Overlap, Block }

// nom possible : CollisionProfile — données de projet, pas du code
public sealed class CollisionProfile
{
    public string Name;                    // "WorldStatic", "Character", "Trigger", "AttackVolume"…
    public int Channel;                    // ce que l'objet EST (table de canaux du projet, ≤ 32)
    public CollisionResponse[] Responses;  // réponse à chaque canal
    public Color? DebugColor;
}
```

- La **table de canaux** est définie par le projet ; le moteur en réserve quelques-uns
  (`WorldStatic`, `WorldDynamic`, `Pawn`, `Trigger`) avec des profils par défaut qui reproduisent
  le comportement actuel.
- Les assets et composants référencent un profil **par nom** — sérialisation stable, édition
  centrale, et c'est exactement ce que
  [tilemaps-gestion-profondeur.md](tilemaps-gestion-profondeur.md) réclame déjà avec
  `collision.profile = TreeTrunk`.
- Mapping backend : canal → bit de groupe ; masque broadphase = canaux dont la réponse ≠ `Ignore`.
  Règle v1 honnête à la rédaction : Bullet ne savait pas faire `Block` et `Overlap` par paire sur un
  même corps — un corps dont le profil ne bloque rien est un capteur (`NoContactResponse`) ; le cas
  mixte se résout en scindant les fixtures en deux corps (cf. D2). Bepu réalise ce « plus tard, par
  callback de contact » nativement : le capteur n'est plus un flag de corps mais une décision prise
  dans `ConfigureContactManifold`, qui retourne `false` (pas de contrainte) quand l'un des deux
  collidables est capteur — le corps continue d'enregistrer ses contacts et de recevoir ses
  événements `OnHit`/`OnHitEnded`.
- **`CollisionHitType` est supprimé** : `Attack`/`Defense` deviennent des canaux de projet
  (`AttackVolume`/`DamageableVolume`) + un `Tag` de fixture. Les couleurs de debug viennent du
  profil. `Collision2d` porte `ProfileName` + `Tag` à la place de l'enum.

### D4 — L'espace de simulation est une politique du monde

Le pendant physique de la règle de rendu. Un monde déclare sa politique ; composants et assets n'en
savent rien.

```csharp
// nom possible : ISimulationSpacePolicy — forme conceptuelle
public interface ISimulationSpacePolicy
{
    // défauts de verrouillage des corps (Planar2d : LinearFactor=(1,1,0), AngularFactor=(0,0,1))
    void ApplyDefaultConstraints(PhysicsDefinition definition);

    // abaissement des formes d'authoring 2D (remplace Physics2dHelper)
    Shape3d Lower(Shape2d shape);

    // pose de rendu dérivée de la pose logique (identité par défaut)
    Matrix DeriveRenderTransform(in Vector3 logicalPosition, in Quaternion logicalRotation);
}
```

Trois instances canoniques :

| Politique | Simulation | Rendu | Genre |
|---|---|---|---|
| `Identity3d` | = rendu | identité | 3D, et défaut absolu (zéro changement de comportement) |
| `Planar2d(plan, extrusion)` | plan verrouillé, formes extrudées | identité | platformer / puzzle 2D |
| `TopDownElevation` | `X = est, Y = profondeur sol, Z = élévation` | `X = X, Y = -(Y - Z)`, tri via la clé 2D | ARPG / zelda-like 2.5D |

Conséquence structurelle — la plus profonde du document : sous une politique non-identité, **le
corps physique lit la pose logique, plus jamais `WorldMatrixNoScale`**. L'entité possède une pose
logique canonique ; la pose de rendu en est dérivée. V1 pragmatique : un système de projection
côté gameplay écrit les transforms de rendu des composants sprite à partir de la pose logique ;
l'intégration au pipeline est le long terme. Le tri visuel, lui, appartient au chantier
[tilemaps-gestion-profondeur.md](tilemaps-gestion-profondeur.md) (`DepthSortable2DComponent`) — la
politique fournit l'élévation, elle ne trie pas.

### D5 — Deux familles de colliders : volumes et champs

```text
Volumes   fixtures discrètes dans le broadphase          entités, props, triggers, hitboxes
Champs    données denses d'environnement, interrogées    heightfield de tuiles, walkability,
          analytiquement, JAMAIS bakées en corps          pentes, murs
```

Tout moteur spécialise déjà ses heightfields ; les jeux à tuiles (CrossCode, les ARPG classiques)
résolvent le mouvement contre une grille par lookup O(1), pas contre des milliers de boîtes
statiques. Baker une map de 3 000+ cellules en corps Bullet serait à la fois lent et **faux** (les
pentes et le step-up ne sont pas de la géométrie, ce sont des règles).

Forme livrée (phase F, 2026-08) :

```csharp
public interface ICollisionField
{
    bool TrySampleGround(in Vector3 worldPosition, float maxDropDistance, out GroundSample sample);
}

// GroundSample : HasGround, GroundHeight (Y monde), Normal, IsWalkable, SurfaceTag.
// Axes : Y up, X/Z horizontaux — la convention du mover. L'appelant choisit la position
// d'échantillonnage et possède tout décalage pied/centre.
```

Implémentation concrète livrée : `HeightGridCollisionField`, une grille régulière sur le plan XZ
dont toutes les données (hauteurs, marchabilité, tags de surface) sont fournies par l'appelant.
Un monde porte au plus un champ (`World.CollisionField`, nullable, non sérialisé).

Le consommateur naturel est le character mover du chantier
[character-controller-features.md](character-controller-features.md) : champs pour le terrain,
sweeps pour les volumes. `TileCollisionManager` devient un détail d'implémentation d'un champ
adossé à la TileMap.

**État de D5** : la famille « champs » existe et est close ; son premier consommateur — la
résolution du sol du mover — est reporté au chantier character-controller, avec les trois prérequis
relevés consignés dans son doc.

### D6 — Les fixtures sont animables par la timeline

Généralisation du frame data des jeux de combat, valable pour tout genre à mêlée :

- Un asset d'animation peut porter une **timeline de sets de fixtures** (keyframes `Step`,
  émises uniquement au changement — le cas « hitbox constante » coûte un seul keyframe).
- Un composant runtime (nom possible : `AnimatedColliderComponent`) échange le set actif au
  changement de keyframe : corps préconstruits et poolés par set distinct (groupés par profil,
  cf. D2), zéro allocation en régime permanent.
- Les événements portent le `Tag` de la fixture en contact → le gameplay attribue « l'épée a
  touché la tête », pas « l'entité A a touché l'entité B ».
- Ce chemin **remplace** la collision par sprite id d'`AnimatedSpriteComponent`, qui est supprimée :
  granularité fausse (le sprite est partagé entre animations), sélection fragile
  (`GetPrimarySpriteId`).

Implémentation de l'attribution à la rédaction : les manifolds Bullet exposaient l'index de child
shape des compounds (`ManifoldPoint.Index0/Index1`) — c'est le crochet pour remonter à la fixture.
Bepu fait aussi bien pour les contacts : `ConfigureContactManifold` reçoit `childIndexA/childIndexB`
des deux côtés de la paire, y compris quand les deux collidables sont des compounds (`BepuBodyBackend.
ResolveFixtureTag`) — une amélioration par rapport au BulletSharp vendorisé, qui ne remplissait
`LocalShapeInfo` que côté sweep/raycast, jamais côté contact.

---

## 4. Paramétrage des mondes : presets

La réponse directe à « comment paramétrer le physics engine ». Un monde choisit un preset — des
données, pas du code :

| Preset | Politique d'espace | Flags | Gravité monde | Notes |
|---|---|---|---|---|
| `Full3d` | `Identity3d` | dynamique complète, CCD | `(0, -9.81, 0)` | comportement actuel |
| `Planar2d` | `Planar2d(XY, extrusion 1)` | dynamique complète | `(0, -g, 0)` | platformer physiqué ; corps verrouillés au plan par défaut |
| `TopDownElevation` | `TopDownElevation` | `CollisionsOnly` | `Vector3.Zero` | ARPG 2.5D ; gravité/frottement gérés par le gameplay sur l'axe d'élévation ; tout en ghost objects + champs |

`PhysicsEngineSettings` s'étend (additivement) : politique d'espace, table de canaux, profils.
`IsPhysics2dActivated` disparaît dans `Planar2d`.

---

## 5. Correspondance existant → cible

| Existant | Rôle actuel | Devenir |
|---|---|---|
| `Shape2d` + dérivés | authoring 2D, avec pose | authoring 2D **sans pose** (la pose migre dans `Collision2d`) ; abaissé par la politique ; plus aucun rôle physique direct |
| `Shape3d` + dérivés | volume sans pose | **vocabulaire public unique des volumes** ; la pose vit dans la fixture ; déménagé hors de `Rendering/Geometry` |
| `PhysicsShape` | 3ᵉ vocabulaire public | **supprimé** — le backend consomme `Shape3d` directement |
| `Physics2dHelper` | `Rectangle → Box(0.5f)` en dur | **supprimé** — remplacé par `ISimulationSpacePolicy.Lower` |
| `PhysicsCollisionFilterGroups` | copie Bullet | **supprimé** — remplacé par canaux/profils |
| `CollisionHitType` | Attack/Defense → couleur | **supprimé** — canal + `Tag` de fixture |
| `PhysicsDefinition` | params rigid body | + `ProfileName` ; `LinearFactor`/`AngularFactor` deviennent les leviers officiels du verrouillage de plan |
| `PhysicsBaseComponent.ConvertToCollisionShape()` | une forme | remplacé par une liste de fixtures |
| Composants typés (`BoxCollisionComponent`, `CapsuleCollisionComponent`, `SphereCollisionComponent`, `CylinderCollisionComponent`) | un composant par forme | **fusionnés** dans un composant générique à liste de fixtures ; l'éditeur expose l'ajout de fixtures typées |
| Branche `Physics2dComponent` (`Box2dCollisionComponent`, `CircleCollisionComponent`) | verrouillage de plan par héritage + abaissement en dur | **supprimée** — politique `Planar2d` (défauts par monde, override par composant via `PhysicsDefinition`) + composant générique ; `Collision2dBasicDemo` réécrite sur la nouvelle API |
| `CharacterControllerSettings.CollisionGroup/Mask` | groupes Bullet bruts sérialisés | remplacés par un `ProfileName` / masque de canaux |
| `Collision` / `ContactPoint` / `HitResult` | paire de composants | + identité de fixture (`Tag`) via child shape index |
| `AnimatedSpriteComponent` (corps par sprite id) | granularité sprite | **supprimé** (phase E) → timeline de fixtures (D6) |
| `TileCollisionManager` | `ICollideableComponent` par tile | implémentation d'un `ICollisionField` |
| `IsPhysics2dActivated` | pipeline Convex2D mort | **supprimé** — absorbé par `Planar2d` |

---

## 6. Migration en phases additives

La migration est une **coupe franche phasée** : les phases existent pour garder le dépôt compilable
et les démos/tests verts à chaque étape, pas pour préserver l'ancien code — **chaque phase supprime
ce qu'elle remplace**, aucun double chemin. Consommateurs à mettre à jour en parallèle : démos,
éditeur, tests. Le convertisseur Alundra ne référence aucun type physique (vérifié) ; il n'est
concerné qu'à partir de la phase E (schéma des assets d'animation), validé par un export complet.

```text
A  Canaux et profils      table de canaux + profils nommés dans les settings ; mapping interne
                          vers groupes/masques Bullet ; règle capteur (profil qui ne bloque rien
                          → NoContactResponse) ; sweeps et CharacterControllerSettings migrés ;
                          PhysicsCollisionFilterGroups SUPPRIMÉ.
                          FAIT (2026-08). Changements assumés : profils par PhysicsType — statiques
                          éditeur/runtime unifiés ; un sweep/raycast à paramètres par défaut touche
                          désormais les corps statiques (l'ancien défaut DefaultFilter les ratait) ;
                          les ghosts restent NoContactResponse quel que soit le profil.
B  Fixtures et compounds  ColliderFixture ; corps multi-formes (btCompoundShape) ; composant de
                          collision générique remplaçant les six composants typés ; identité de
                          fixture dans les contacts ; formes déménagées hors de Rendering/Geometry ;
                          PhysicsShape SUPPRIMÉ (le backend consomme Shape3d).
                          FAIT (2026-08). Changements assumés : un composant crée un corps par
                          profil de collision (un Dynamic n'a droit qu'à un seul profil, erreur de
                          validation sinon) ; seul un corps Dynamic réécrit la transformation de
                          l'entité (Static et Kinetic ne la touchent plus) ; les fixtures sont
                          sérialisées dans un tableau `fixtures` sur le nœud du composant et les
                          assets livrés ont été migrés à la main.
                          Autres limites connues : le LocalScaling d'un compound (échelle non
                          unitaire) n'est exercé par aucune scène réelle — appliqué au compound
                          après AddChildShape, enfants non scalés ; et un composant 2D créé à la
                          main doit poser LinearFactor/AngularFactor explicitement tant que la
                          politique Planar2d (phase D) ne fournit pas les défauts de monde.
                          Limite connue à la rédaction : le BulletSharp vendorisé ne remplissait pas
                          LocalShapeInfo pour les enfants d'un compound lors d'un sweep/raycast —
                          HitResult.Tag n'était donc renseigné que pour les corps à fixture unique ;
                          les contacts, eux, portaient bien l'enfant touché (ManifoldPoint.Index0/
                          Index1). Bepu reproduit exactement cette asymétrie (même limite côté
                          bibliothèque, pas seulement côté binding) : `ISweepHitHandler.AllowTest`
                          reçoit un `childIndex` mais `OnHit` ne le reporte pas, donc `HitResult.Tag`
                          reste `null` pour un compound touché par un sweep
                          (`ShapeSweep_ReportsNoTag_WhenItHitsACompoundBody`), alors que
                          `ConfigureContactManifold` porte bien `childIndexA/childIndexB` des deux
                          côtés d'un contact. LocalScaling n'existe plus dans Bepu : l'échelle locale
                          est cuite dans les dimensions et les offsets de compound à la création
                          (`BepuShapeCache`), plus dans une propriété de forme posée après coup.
C  Abaissement            ISimulationSpacePolicy.Lower ; Shape2d perd sa pose (migrée dans
                          Collision2d, qui gagne ProfileName + Tag) ; extrusion paramétrée ;
                          Physics2dHelper, branche Physics2dComponent, CollisionHitType et
                          IsPhysics2dActivated SUPPRIMÉS.
                          FAIT (2026-08). La politique vit dans SimulationSpacePolicy
                          (Identity3d par défaut dans PhysicsEngineSettings.SpacePolicy) et
                          n'expose que Lower : ApplyDefaultConstraints est reporté en phase D.
                          Abaissement retenu : rectangle → Box (w, h, ExtrusionDepth),
                          cercle → Sphere ; l'abaissement en disque est reporté. Ligne, polygone
                          et compound lèvent NotSupportedException, comme avant.
                          Limite levée en phase E : un volume cercle sur un sprite était créé
                          (Sphere) mais UpdateBodyTransformation ne gérait que les rectangles —
                          cast invalide au premier update. Le placement gère désormais le cercle
                          (même math d'origine, centrage par le rayon).
                          Changements assumés : deux profils réservés ajoutés, AttackVolume
                          (canal 4, debug rouge) et DamageableVolume (canal 5, debug vert), tous
                          deux capteurs — ils remplacent l'ancien CollisionHitType des sprites ;
                          la couleur de debug des quatre chemins de dessin vient désormais du
                          profil résolu (nom vide ou inconnu → Trigger). Les assets *.sprite
                          livrés ont été migrés par script ("collision_type" → "collision_profile").
                          Le TileCollisionType au niveau des tuiles et sa clé "collision_type"
                          sur les nœuds de tuile sont un concept distinct, laissés intacts.
D  Espace de simulation   politique complète par monde ; pose logique vs pose de rendu ;
                          Identity3d par défaut. Le chantier structurel.
                          FAIT (2026-08). Un monde nomme sa politique (World.SpacePolicyName,
                          clé "space_policy", vide = défaut projet) ; elle est résolue au seul
                          endroit où un contexte physique est construit,
                          PhysicsSystemComponent.GetOrCreateContext, et portée par
                          PhysicsWorld.SpacePolicy (SpriteCollisionHelper abaisse désormais via le
                          monde, plus via le global). SimulationSpacePolicy gagne Name,
                          ApplyDefaultConstraints, DeriveRenderPosition et CreateByName ;
                          TopDownElevation est ajoutée (X est, Y profondeur au sol, Z élévation ;
                          rendu = (X, -(Y - Z), 0)). PhysicsBaseComponent applique les contraintes
                          du monde à la PhysicsDefinition avant de créer le corps. Les visuels purs
                          d'un monde projeté s'attachent sous un RenderProjectionComponent, qui se
                          place chaque frame à la position de rendu dérivée de la position logique
                          de la racine de l'entité. Démo TopDownElevationDemo.
                          Heuristique assumée (Planar2d) : un facteur laissé à Vector3.One est un
                          facteur non exprimé, que le monde remplit (linéaire (1,1,0), angulaire
                          (0,0,1)) ; toute autre valeur est un override d'authoring conservé tel
                          quel. Limite : un corps volontairement non contraint dans un monde
                          planaire n'est pas exprimable, ses facteurs libres étant exactement ceux
                          que la politique lit comme non exprimés. Autre limite : les contraintes
                          sont écrites dans la PhysicsDefinition du composant, donc une entité
                          réinitialisée d'un monde Planar2d vers un monde Identity3d conserve ses
                          facteurs verrouillés (l'écriture n'est pas réversible).
                          Limite levée en phase E pour les sprites animés : un
                          AnimatedSpriteComponent place les corps de sa timeline sur la pose
                          logique de la racine de l'entité, il peut donc vivre sous un
                          RenderProjectionComponent. StaticSpriteComponent, lui, calcule toujours
                          la pose de ses volumes Collision2d depuis sa propre transformation
                          monde : un sprite statique à volumes authorés ne doit toujours pas être
                          placé sous une projection.
                          Ordre de mise à jour : aucune contrainte nouvelle. Les transformations
                          monde sont lues paresseusement en remontant les parents
                          (SceneComponent.WorldMatrixNoScale), et le composant projette avant de
                          mettre à jour ses enfants ; il publie donc toujours la position de la
                          racine de la même frame. Seule contrainte de placement : être un
                          descendant de la racine de l'entité, pas la racine elle-même.
E  Timelines de fixtures  keyframes de collision sur les assets d'animation ; composant runtime
                          poolé ; chemin par sprite id d'AnimatedSpriteComponent SUPPRIMÉ.
                          FAIT (2026-08). Un asset d'animation 2d porte une liste optionnelle
                          `collision_keyframes` (clé additive, triée au chargement) de sets de
                          ColliderFixture au schéma de la phase B ; elle entre dans
                          GetDurationSeconds, est recopiée dans le snapshot de composition et
                          échantillonnée en Step par Animation2dCompositionSampler
                          (CurrentCollisionKeyframeIndex, -1 avant le premier keyframe, mis à jour
                          en Update, Seek et au bouclage). AnimatedSpriteComponent crée les corps
                          d'un set à sa première activation — un ghost par profil résolu, fixture
                          sans profil = Trigger — les garde en cache par (animation, index de
                          keyframe) et se contente de retirer/rajouter au changement : régime
                          permanent sans allocation. Les corps sont posés sur
                          Owner.RootComponent.WorldMatrixNoScale, jamais sur la transformation du
                          composant sprite : c'est ce qui rend les volumes corrects sous une
                          projection de rendu. Désactivation et Detach détruisent les corps poolés.
                          Règle d'authoring : l'échantillonnage Step ne rend rien actif avant le
                          premier keyframe, y compris juste après un bouclage — un volume qui doit
                          être vivant dès le début d'une boucle a besoin d'un keyframe à t = 0.
                          Changements assumés : un sprite animé ne consomme plus
                          SpriteData.CollisionShapes (la timeline les remplace) — StaticSpriteComponent
                          continue de porter ses volumes Collision2d, inchangé ; ContactPoint.ColliderA/B
                          passent de PhysicsBaseComponent à ICollideableComponent, sans quoi les
                          contacts d'un corps porté par un composant non physique (le sprite animé)
                          ne remontaient aucun point de contact.
                          Non-objectif conservé : l'éditeur ne propose pas encore d'UI de timeline
                          pour ces keyframes (sérialisation seulement).
F  Champs et mover        ICollisionField ; intégration au character controller (doc dédié).
                          FAIT (2026-08) pour la famille de colliders, PAS pour le consommateur.
                          Forme livrée : le contrat ICollisionField
                          (TrySampleGround(in Vector3, float maxDropDistance, out GroundSample))
                          et son GroundSample (HasGround, GroundHeight, Normal, IsWalkable,
                          SurfaceTag), une implémentation concrète HeightGridCollisionField (grille
                          régulière sur le plan XZ, données fournies par l'appelant), et un slot par
                          monde (World.CollisionField, nullable, non sérialisé). Aucun câblage
                          consommateur.
                          Contrat d'axes assumé : ICollisionField et GroundSample sont définis
                          Y up, X/Z horizontaux — la convention du mover. GroundHeight est un Y
                          monde. Adapter un champ à une politique dont l'élévation est un autre axe
                          (TopDownElevation, élévation Z) est un non-objectif explicite de cette
                          version.
                          Intervalle d'acceptation pinné : delta = position.Y - hauteur de cellule ;
                          sol trouvé ssi 0 <= delta <= maxDropDistance. delta == 0 EST du sol ; un
                          sol strictement au-dessus du point d'échantillonnage (delta < 0) ne l'est
                          pas (pas de tolérance vers le haut dans cette version). Une cellule non
                          marchable dans l'intervalle rapporte quand même HasGround = true avec
                          IsWalkable = false : présence et marchabilité sont deux faits distincts.
                          Propriété du reset : World.Clear() remet le champ à null (démontage
                          complet), World.ClearEntities() n'y touche pas — charger un monde ne doit
                          pas jeter silencieusement un champ posé depuis le code.
                          Points d'extension documentés, non livrés : normales de pente par cellule
                          (toute normale vaut Vector3.Up), tolérance vers le haut dans l'intervalle.
                          Non dérivable, hors périmètre : un champ issu des tuiles. Les données de
                          tuiles ne portent AUCUNE hauteur de sol par cellule
                          (TileMapLayerData.zOffset est une profondeur de rendu dans le plan XY
                          d'authoring, non appliquée à la collision ; les corps de tuiles sont à
                          z = 0 local). Toutes les données d'un champ viennent de l'appelant.
                          RESTE À FAIRE — le premier consommateur : la résolution du sol du mover
                          est reportée au chantier character-controller, qui doit d'abord régler
                          trois prérequis relevés et consignés dans
                          character-controller-features.md (axes du mover vs politique projetée,
                          référence centre-de-capsule de rootComponent.Position, annulation du
                          SkinWidth dans le snap au sol).
```

D5 est donc clos côté famille de colliders : la seconde famille existe (champs à côté des volumes),
avec un contrat, une implémentation et un porteur par monde. Son premier consommateur — la
résolution du sol du character mover — est explicitement reporté au chantier
[character-controller-features.md](character-controller-features.md).

Dépendances : A et B sont fondatrices et indépendantes ; C dépend de B ; E dépend de B (et de D
pour les genres projetés) ; F rejoint le chantier character-controller. D peut avancer en parallèle.

Validation de généricité — trois scènes de démo, aucune spécifique à un jeu :

```text
- un platformer Planar2d (corps dynamiques verrouillés au plan) ;
- une scène top-down à élévation : un pont, une entité dessus, une entité dessous —
  l'écho collision du cas « pont » du doc de profondeur ;
- une scène 3D existante, strictement inchangée (politique Identity3d).
```

Un projet 2.5D réel (la conversion Alundra) consomme A+B+D+E+F tel quel : c'est un **test de
généricité**, pas une cible de conception — ses données (AABB par frame, terrain en grille avec
hauteurs et pentes, élévation soustraite du Y écran) sont représentatives du genre entier.

---

## 7. Contraintes de performance

```text
- corps préconstruits et poolés par set de fixtures ; le swap n'alloue rien ;
- pas de rebuild de compound par frame : échanger des corps préparés, pas reconstruire ;
- ProfileName résolu en index au chargement ; jamais de lookup string dans Update ;
- réutiliser les pools d'événements existants de BulletPhysicsEngine (déjà en place) ;
- ordre d'itération des fixtures stable (listes ordonnées) : déterminisme du fixed-step préservé ;
- les champs (ICollisionField) sont des lookups O(1) sans allocation.
```

---

## Ce qu'il ne faut pas faire

```text
- Ne pas créer une seconde pile physique « 2D » : API doublée, 2.5D orphelin (l'écueil Unity).
- Ne pas mettre de logique d'espace ou de projection dans les composants ou les assets
  (miroir de la règle de rendu).
- Ne pas donner de pose à Shape3d : la pose appartient à la fixture.
- Ne pas exposer les types Bullet dans les API gameplay.
- Ne pas garder de double chemin legacy : chaque phase supprime ce qu'elle remplace.
- Ne pas encoder la sémantique gameplay en booléens ou en couleurs : canaux, profils, tags.
- Ne pas asservir la pose physique à la pose de rendu sous une politique non-identité.
- Ne pas baker un terrain dense en corps statiques : c'est un champ, pas des volumes.
- Ne pas résoudre les hitboxes dans le solveur : overlap + événements, et rien d'autre.
- Ne pas attribuer les hits sans identité de fixture stable.
```

## Voir aussi

- [rendering-2d-3d-spaces.md](rendering-2d-3d-spaces.md) — la règle jumelle côté rendu : l'espace
  d'affichage est décidé par la caméra.
- [tilemaps-gestion-profondeur.md](tilemaps-gestion-profondeur.md) — le tri visuel 2D ; consomme
  l'élévation, ne la simule pas ; sépare déjà collision et ordre visuel.
- [character-controller-features.md](character-controller-features.md) — le consommateur des
  requêtes, champs et sweeps décrits ici.
