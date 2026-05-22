# Gestion moderne de la profondeur des TileMaps

## Objectif

Ce document definit une architecture cible pour gerer la profondeur visuelle dans les mondes 2D, top-down, 2.5D ou isometriques de CasaEngine.

Le but n'est pas seulement d'afficher correctement une TileMap. Le but est de pouvoir faire cohabiter facilement :

- les layers de TileMap fixes ;
- les chunks statiques performants ;
- les personnages ;
- les PNJ et ennemis ;
- les props issus de la TileMap ;
- les objets interactifs ;
- les effets ;
- les foregrounds comme toits, feuillages, arches et plafonds.

La regle centrale est la suivante :

```text
La TileMap fournit le decor, les zones, les objets places et les contraintes.
Les entities restent des entities du monde.
Le rendu 2D fournit l'ordre visuel commun entre TileMap et entities.
```

Un personnage ne doit pas etre "integre" dans `TileMapComponent` au sens ownership ou rendu special. Il doit pouvoir etre rendu dans le meme espace visuel que la TileMap, avec les memes regles de profondeur.

---

## Etat actuel observe dans le code

Cette section decrit uniquement ce qui est observable dans le depot au moment de la redaction.

### TileMap

Le rendu TileMap actuel passe par `TileMapComponent.Draw`.

Comportement observe :

- les layers sont parcourus dans l'ordre de `TileMapData.Layers` ;
- chaque layer possede un `z_offset` ;
- `worldZ = TileMapComponent.Position.Z + layer.zOffset` est transmis au rendu ;
- les tiles statiques peuvent etre regroupees en chunks ;
- les tiles dynamiques ou non statiques repassent par un rendu tile par tile ;
- les chunks statiques sont envoyes a `SpriteRendererComponent.DrawStaticBatch` ;
- les tiles dynamiques utilisent le chemin de rendu sprite existant.

Le modele actuel sait donc dessiner des TileMaps avec un ordre par layer et par `z_offset`, mais il ne fournit pas encore un systeme explicite et unifie de profondeur 2D entre TileMap et entities.

### Donnees disponibles

Les donnees TileMap disposent deja de plusieurs points d'extension utiles :

- `TileMapData.CustomProperties` ;
- `TileMapLayerData.CustomProperties` ;
- `TileMapObjectLayerData.CustomProperties` ;
- `TileMapObjectData.CustomProperties` ;
- `TileData.CustomProperties` ;
- `TileMapLayerData.zOffset` ;
- `TileMapObjectLayerData.zOffset`.

Ces champs permettent de stocker des intentions de profondeur, mais le runtime ne les interprete pas encore comme un systeme complet de tri 2D.

### Pipeline de rendu

Le monde dessine avec un `RenderFrame`. Pendant `World.Draw(in RenderFrame frame)`, le monde expose temporairement `CurrentRenderFrame` pour que les composants puissent dessiner dans la vue courante.

Plusieurs renderers implementent `IViewFlushableRenderer` et sont flushes par vue. Cette architecture est importante : la gestion moderne de profondeur doit s'integrer au pipeline par vue, pas creer un second pipeline parallele sans relation avec `RenderFrame`.

---

## Probleme a resoudre

Dans un RPG 2D ou 2.5D, une entity doit pouvoir passer naturellement devant ou derriere des elements de decor.

Exemples :

```text
- le joueur passe derriere un arbre puis devant son tronc ;
- un PNJ passe sous une arche ;
- un coffre haut masque partiellement le joueur quand le joueur est derriere ;
- un personnage marche sur un pont pendant qu'un autre passe dessous ;
- un toit ou une canopee reste toujours au premier plan ;
- un effet de poussiere doit etre devant les pieds mais derriere le corps.
```

Un simple `z_offset` par layer ne suffit pas pour ces cas. A l'inverse, trier chaque tile individuellement avec chaque entity serait trop couteux et rendrait les chunks statiques beaucoup moins utiles.

Il faut donc separer deux familles de rendu :

```text
Rendu fixe / chunkable
    sol, details plats, foregrounds massifs, grandes zones statiques

Rendu sortable
    personnages, PNJ, ennemis, props hauts, objets interactifs, effets lies aux entities
```

---

## Principe cible

Le modele cible est un tri 2D explicite, commun a la TileMap et aux entities.

Chaque element rendu recoit une cle de profondeur composee de plusieurs champs compares dans l'ordre :

```text
RenderPass2D
SortingLayer
OrderInLayer
Elevation
SortCoordinate
LocalSortOffset
StableId
```

Ce modele est volontairement lexicographique. Il evite de cacher des plages implicites dans une seule valeur numerique magique.

### RenderPass2D

`RenderPass2D` represente les grandes phases de rendu.

Exemple d'ordre cible :

```text
Background
Ground
GroundDetails
YSortedWorld
Foreground
Effects
UI
```

Les passes fixes restent rapides et peuvent contenir des chunks. La passe `YSortedWorld` contient les entities et les props qui doivent etre tries dynamiquement.

### SortingLayer

`SortingLayer` represente une famille de rendu a l'interieur d'une passe.

Exemples :

```text
Ground
Road
LowWater
Characters
Props
ForegroundRoof
ForegroundCanopy
EffectsBehindCharacters
EffectsInFrontOfCharacters
```

Le nom exact des layers doit etre defini par CasaEngine ou par le projet, mais le moteur doit fournir un format stable pour les comparer.

### OrderInLayer

`OrderInLayer` sert aux ordres locaux stables.

Exemples :

```text
Ground / 0      eau basse
Ground / 10     herbe
Ground / 20     route

Foreground / 0  feuillage
Foreground / 10 toits
Foreground / 20 brume
```

### Elevation

`Elevation` represente le niveau logique, pas la position verticale a l'ecran.

Exemples :

```text
-1  sous-sol
 0  sol normal
 1  pont / plateforme
 2  toit / etage superieur
```

Cette information est necessaire pour les ponts, escaliers, plateformes et zones superposees. Le Y-sort seul ne sait pas distinguer un personnage au sol sous un pont d'un personnage sur le pont.

### SortCoordinate

`SortCoordinate` est la coordonnee utilisee pour le tri dynamique.

Elle ne doit pas etre supposee identique a `Entity.Position.Y`.

Le moteur doit accepter un point de tri explicite, appele `SortAnchor` :

```text
personnage      pieds
PNJ             pieds
arbre           base du tronc
poteau          base du poteau
coffre haut     bas du coffre
arche basse     base de la partie basse
```

Pour eviter une hypothese globale fausse, la coordonnee de tri doit etre calculee a partir d'un profil de profondeur :

```text
TopDownYDown       plus Y ecran est grand, plus l'objet est devant
TopDownYUp         plus -Y monde est grand, plus l'objet est devant
IsometricAxis      projection sur un axe de tri configure
ScreenProjected    projection du SortAnchor dans la vue courante
```

Pour CasaEngine, `ScreenProjected` est le plus robuste a long terme parce que le rendu passe deja par un `RenderFrame`. Un profil plus simple peut etre utilise pour les maps strictement 2D si le projet veut eviter le cout de projection.

### LocalSortOffset

`LocalSortOffset` permet de regler des details visuels sans changer la position logique.

Exemples :

```text
ombre sous personnage      -10
corps personnage             0
arme tenue devant            5
poussiere devant les pieds   3
```

### StableId

`StableId` garantit un ordre stable lorsque deux items ont la meme cle.

Il doit venir d'une source stable : id d'entity, id d'objet TileMap, id de chunk ou id genere lors du chargement de la map.

Sans tie-breaker stable, deux elements au meme tri peuvent changer d'ordre d'une frame a l'autre.

---

## Cle de tri recommandee

La representation recommandee est une structure comparable, pas un `long` packe des le depart.

Exemple de forme cible :

```csharp
public readonly struct RenderSortKey2D : IComparable<RenderSortKey2D>
{
    public readonly int RenderPass;
    public readonly int SortingLayer;
    public readonly int OrderInLayer;
    public readonly int Elevation;
    public readonly int SortCoordinate;
    public readonly int LocalSortOffset;
    public readonly int StableId;

    public int CompareTo(RenderSortKey2D other)
    {
        var result = RenderPass.CompareTo(other.RenderPass);
        if (result != 0) return result;

        result = SortingLayer.CompareTo(other.SortingLayer);
        if (result != 0) return result;

        result = OrderInLayer.CompareTo(other.OrderInLayer);
        if (result != 0) return result;

        result = Elevation.CompareTo(other.Elevation);
        if (result != 0) return result;

        result = SortCoordinate.CompareTo(other.SortCoordinate);
        if (result != 0) return result;

        result = LocalSortOffset.CompareTo(other.LocalSortOffset);
        if (result != 0) return result;

        return StableId.CompareTo(other.StableId);
    }
}
```

Un packing en `long` peut etre ajoute plus tard si un profiling le justifie. Dans ce cas, les plages de chaque champ devront etre documentees et testees.

---

## Integration des entities

L'integration facile des personnages dans une TileMap doit se faire par contrat de rendu, pas par dependance speciale a `TileMapComponent`.

### Composant de profondeur 2D

Une entity qui participe au rendu 2D sortable devrait exposer une information de profondeur via un composant dedie.

Nom possible : `DepthSortable2DComponent`.

Responsabilites :

```text
- fournir le SortAnchor local ;
- fournir le SortingLayer ;
- fournir l'OrderInLayer ;
- fournir l'Elevation courante ;
- fournir le LocalSortOffset ;
- fournir un StableId ;
- permettre un override manuel par entity ou prefab.
```

Exemple conceptuel :

```text
Player Entity
    Transform
    AnimatedSpriteComponent
    DepthSortable2DComponent
        SortAnchorLocal = pieds
        RenderPass = YSortedWorld
        SortingLayer = Characters
        Elevation = resolue depuis le monde
```

La TileMap n'a pas besoin de connaitre le type `Player`. Elle fournit seulement des informations de zone, d'elevation ou d'objet sortable que le systeme de profondeur peut consulter.

### Resolution de l'elevation

Pour les personnages, l'elevation peut venir de plusieurs sources :

```text
- valeur explicite sur l'entity ;
- volume ou zone de la map ;
- object layer TileMap ;
- custom property de tile ou layer ;
- script de gameplay lors d'une transition escalier / pont.
```

Nom possible : `TileMapDepthResolver`.

Responsabilites :

```text
- convertir un SortAnchor monde en cellule TileMap si necessaire ;
- lire les zones de profondeur pertinentes ;
- retourner une elevation et eventuellement une pass/layer conseillee ;
- ne pas gerer la collision a la place du systeme physique.
```

Cette resolution doit etre optionnelle. Une entity hors TileMap ou dans une scene non tilemap doit rester rendable.

---

## Integration des TileMaps

La TileMap ne doit pas tout trier tile par tile. Elle doit classer ses layers et objets selon leur role.

### Roles de layer

Chaque layer TileMap devrait pouvoir declarer un role.

Noms possibles :

```text
Background
Ground
GroundDetails
YSortedSource
Foreground
CollisionOnly
ObjectSource
Debug
```

Ces roles peuvent etre stockes dans `custom_properties` des layers, tout en conservant `z_offset` pour compatibilite avec les assets existants.

Exemples de proprietes :

```text
depth.role = Ground
depth.sortingLayer = Ground
depth.orderInLayer = 0
depth.elevation = 0
depth.ySort = false
```

```text
depth.role = Foreground
depth.sortingLayer = ForegroundCanopy
depth.orderInLayer = 10
depth.elevation = 0
depth.ySort = false
```

```text
depth.role = ObjectSource
depth.spawnAsEntities = true
```

### Layers fixes et chunking

Les roles suivants restent chunkables :

```text
Background
Ground
GroundDetails
Foreground
```

Ils doivent continuer a utiliser des chunks statiques quand les tiles sont statiques. Leur ordre est controle par `RenderPass`, `SortingLayer`, `OrderInLayer` et `Elevation`, pas par un tri individuel de chaque tile.

### Objets sortables issus de la TileMap

Les objets hauts ne doivent pas rester une collection de tiles independantes si un personnage doit passer devant ou derriere eux.

Ils doivent devenir des objets sortables ou des entities.

Exemples :

```text
TreeProp
LampPostProp
ColumnProp
ChestEntity
BridgeEntity
DoorEntity
```

Sources possibles :

```text
- object layer Tiled ;
- object layer CasaEngine ;
- tile avec custom properties indiquant un prefab ;
- layer marque ObjectSource ;
- conversion editor depuis une selection de tiles vers un prop.
```

Exemples de proprietes sur un objet :

```text
entity.prefab = Props/Tree01
depth.renderPass = YSortedWorld
depth.sortingLayer = Props
depth.sortAnchor = 16,48
depth.elevation = 0
depth.localSortOffset = 0
collision.profile = TreeTrunk
```

Le runtime peut alors creer une entity ou un renderable sortable a partir de cet objet.

---

## Objets composes et SortingGroup

Un objet compose ne doit pas etre melange avec le joueur tile par tile.

Exemple d'arbre compose :

```text
Tree01
    tronc gauche
    tronc droite
    feuillage gauche
    feuillage droite
```

Le groupe complet doit avoir un seul `SortAnchor`, generalement la base du tronc.

Nom possible : `SortingGroup2D`.

Responsabilites :

```text
- fournir une cle de tri externe unique ;
- conserver un ordre interne stable ;
- contenir plusieurs sprites ou sous-renderables ;
- permettre des parties foreground separees si necessaire.
```

Deux modeles sont utiles.

### Mode A : objet entier sortable

```text
Tree01
    RenderPass = YSortedWorld
    SortAnchor = base du tronc
    parties internes = tronc + feuillage
```

Simple et suffisant pour beaucoup de props.

### Mode B : objet splitte

```text
Tree01Trunk
    RenderPass = YSortedWorld
    SortAnchor = base du tronc

Tree01Canopy
    RenderPass = Foreground
    toujours devant
```

Plus propre pour les grands arbres, arches, toits et entrees de grotte.

---

## Rendu : integration recommandee

Le systeme cible doit collecter les items 2D visibles dans la vue courante, calculer leur `RenderSortKey2D`, puis les dessiner dans l'ordre.

Important : il faut eviter les delegates capturants et allocations par frame dans les chemins `Draw`.

Un `RenderItem2D` ne devrait donc pas etre un simple `Action Draw`. Il devrait plutot contenir une commande de rendu ou une reference vers un renderer existant avec des donnees preparees.

Forme conceptuelle :

```text
RenderItem2D
    SortKey
    Kind
    RendererId ou RenderQueue
    Texture / Buffer / SpriteId / Material selon le type
    WorldMatrix
    SourceRectangle
    Color
```

Le systeme doit rester compatible avec les renderers existants :

```text
SpriteRendererComponent
Renderer2DComponent
ParticleRendererComponent
Line3dRendererComponent
```

Approche recommandee :

```text
1. Les composants produisent des commandes de rendu 2D.
2. Les commandes portent une cle de tri.
3. Une queue 2D par vue trie les commandes.
4. Les commandes sont dispatch vers les renderers existants.
5. Les renderers flushent dans le contexte du RenderFrame courant.
```

Cette approche permet aux personnages et aux TileMaps de participer au meme ordre visuel sans que `TileMapComponent` connaisse les classes de gameplay.

---

## Cas pratiques

### Personnage et arbre

```text
Player
    RenderPass = YSortedWorld
    SortingLayer = Characters
    SortAnchor = pieds

TreeTrunk
    RenderPass = YSortedWorld
    SortingLayer = Props
    SortAnchor = base du tronc
```

Si le `SortCoordinate` du joueur est plus petit que celui de l'arbre, le joueur est dessine avant l'arbre et apparait derriere.

Si le `SortCoordinate` du joueur est plus grand, le joueur est dessine apres l'arbre et apparait devant.

### Feuillage toujours devant

```text
TreeCanopy
    RenderPass = Foreground
    SortingLayer = ForegroundCanopy
```

La canopee ne participe pas au Y-sort avec le joueur. Elle reste devant.

### Pont

```text
BridgeDeck
    RenderPass = YSortedWorld
    Elevation = 1

PlayerUnderBridge
    RenderPass = YSortedWorld
    Elevation = 0

PlayerOnBridge
    RenderPass = YSortedWorld
    Elevation = 1
```

Un joueur au sol ne passe pas devant le pont uniquement parce qu'il est plus bas a l'ecran. Un joueur sur le pont est trie dans la meme elevation que le pont.

### Ombre et personnage

```text
CharacterShadow
    meme SortAnchor que le personnage
    LocalSortOffset = -10

CharacterBody
    meme SortAnchor que le personnage
    LocalSortOffset = 0
```

L'ombre reste juste derriere le corps sans changer la position de l'entity.

---

## Donnees Tiled et CasaEngine

Les proprietes custom doivent etre nommees de maniere stable. Il est preferable d'utiliser un prefixe pour eviter les collisions.

Proprietes recommandees :

```text
depth.role
depth.renderPass
depth.sortingLayer
depth.orderInLayer
depth.elevation
depth.sortAnchorX
depth.sortAnchorY
depth.localSortOffset
depth.sortMode
depth.spawnAsEntity
entity.prefab
collision.profile
```

Les valeurs doivent etre parsees avec validation :

```text
- valeur absente -> valeur par defaut explicite ;
- valeur inconnue -> warning d'import ou diagnostic editor ;
- valeur invalide -> erreur d'import si elle rend l'objet inutilisable ;
- compatibilite -> `z_offset` reste lu tant que les anciens assets l'utilisent.
```

Exemple d'objet Tiled :

```text
name = tree_01
type = Prop
entity.prefab = Props/Tree01
depth.renderPass = YSortedWorld
depth.sortingLayer = Props
depth.sortAnchorX = 16
depth.sortAnchorY = 48
depth.elevation = 0
collision.profile = TreeTrunk
```

---

## Separation avec collision, navigation et gameplay

La profondeur visuelle ne doit pas piloter directement la collision.

Un meme objet peut avoir :

```text
VisualBounds
    toute la taille visible

SortAnchor
    point de contact visuel avec le sol

CollisionShape
    zone physique reelle

NavigationCost
    information pour pathfinding

InteractionBounds
    zone d'interaction gameplay
```

Exemple arbre :

```text
VisualBounds = tronc + feuillage
SortAnchor = base du tronc
CollisionShape = petit rectangle autour du tronc
InteractionBounds = zone proche du tronc
```

Exemple pont :

```text
VisualElevation = 1
WalkableElevation = 1 sur le tablier
GroundPassageElevation = 0 sous le pont
Collision = depend de l'etat du joueur et de la zone
```

---

## Ce qu'il ne faut pas faire

```text
- Ne pas mettre les personnages dans TileMapComponent.
- Ne pas trier toute la TileMap tile par tile.
- Ne pas utiliser Entity.Position comme cle de tri universelle.
- Ne pas confondre z_offset, elevation logique et SortAnchor.
- Ne pas creer un renderer 2D parallele ignorant RenderFrame.
- Ne pas utiliser Action/closures par item de rendu dans Draw.
- Ne pas melanger collision et ordre visuel.
```

---

## Migration recommandee

### Etape 1 : formaliser les donnees de profondeur

Ajouter un modele de donnees pour les champs `depth.*` sans changer le rendu.

Resultat attendu : les assets peuvent exprimer les roles, layers, anchors et elevations de maniere validee.

### Etape 2 : introduire la cle de tri 2D

Ajouter `RenderSortKey2D` et ses tests unitaires.

Resultat attendu : ordre lexicographique stable, tie-breaker stable, pas de packing implicite.

### Etape 3 : ajouter le composant de profondeur des entities

Ajouter un composant de type `DepthSortable2DComponent` ou equivalent.

Resultat attendu : un personnage peut exposer ses pieds comme `SortAnchor` sans dependance a `TileMapComponent`.

### Etape 4 : connecter une queue de rendu 2D au RenderFrame

Ajouter une queue par vue qui collecte les commandes 2D, trie par `RenderSortKey2D`, puis dispatch vers les renderers existants.

Resultat attendu : TileMap et entities peuvent participer au meme ordre visuel.

### Etape 5 : classer les layers TileMap par role

Mapper les layers existants vers `Ground`, `GroundDetails`, `Foreground` ou `ObjectSource` via metadata.

Resultat attendu : les layers fixes restent chunkes, les objets sortables sortent du chemin chunk statique.

### Etape 6 : convertir les object layers en entities ou props sortables

Utiliser `TileMapObjectLayerData` et les proprietes `entity.*` / `depth.*` pour creer des entities ou renderables sortables.

Resultat attendu : arbres, coffres, poteaux et ponts peuvent etre places dans l'editeur TileMap et rendus avec les personnages.

### Etape 7 : gerer elevation et zones speciales

Ajouter une resolution d'elevation optionnelle pour ponts, escaliers et plateformes.

Resultat attendu : un personnage peut passer sous un pont ou dessus sans tricher avec un `z_offset` global.

### Etape 8 : ajouter samples et validations

Creer une scene de test contenant au minimum :

```text
- joueur devant / derriere arbre ;
- canopee foreground ;
- coffre sortable ;
- pont avec deux elevations ;
- deux entities au meme Y pour tester StableId ;
- TileMap ground chunked conservee.
```

---

## Synthese

La gestion moderne de profondeur pour CasaEngine doit etre un systeme de rendu 2D commun, pas une logique cachee dans la TileMap.

Le modele cible est :

```text
TileMap
    fournit layers fixes, chunks, object layers, zones et metadata

Entities
    fournissent SortAnchor, layer, elevation et offsets locaux

DepthResolver
    resout les informations de map utiles aux entities

RenderSortKey2D
    fournit un ordre stable et explicite

RenderQueue2D par vue
    trie TileMap, props, personnages et effets dans le meme espace visuel
```

Avec ce modele, les personnages s'integrent naturellement dans un monde TileMap sans devenir des tiles, sans casser le chunking, et sans multiplier les cas speciaux dans `TileMapComponent`.
