# Éditeur d’animations 2D pour CasaEngine

## Objectif

L’objectif est de développer dans CasaEngine un éditeur générique d’animations 2D pour les assets `.anim2d` déjà utilisés par le moteur et l’éditeur.

Une animation 2D CasaEngine ne doit pas être vue uniquement comme une succession d’images complètes. Elle peut être composée de plusieurs sprites, chacun ayant sa propre position, visibilité, ordre d’affichage et ses propres changements dans le temps.

Ce document décrit une direction produit et des contraintes verrouillées pour le format composé actuel. Les anciennes mentions d’un sous-ensemble V1 mono-sprite/event-only doivent être lues comme de l’historique de conception, pas comme une cible encore supportée dans le dépôt : c'est le cas des sections rédigées au présent qui décrivent une V1 « un seul sprite courant » ou « sans composition » (notamment 15, 16.1, 23.2 et 28) ; en cas de conflit avec les décisions verrouillées ci-dessus, ces décisions priment.

Décisions verrouillées :

- conserver l’extension `.anim2d` et le contrat chargé par `Animation2dData` ;
- conserver le modèle time-based déjà en place (`time_seconds`) ;
- cibler directement une animation 2D composée avec `parts`, `tracks` et `events` ;
- garder une timeline read-only graduée en secondes, scrollable horizontalement, zoomable et centrée sur les événements ;
- rester centré sur l’éditeur générique CasaEngine ;
- séparer strictement runtime et éditeur.

Les noms conceptuels utilisés plus bas (`Animation2DAsset`, `AnimationPlayer2D`, etc.) restent des repères de conception. Toute implémentation doit rester alignée sur les types réellement présents dans le dépôt.

L’éditeur doit donc être pensé comme un éditeur d’**animations 2D composées**, inspiré de plusieurs outils existants :

- **Godot AnimationPlayer** pour le principe de pistes animant des propriétés.
- **Spine** pour les notions de slots, attachments et draw order.
- **Unity Animation Window** pour l’interface timeline, keyframes et preview.
- **Tiled** pour la philosophie de formats ouverts, JSON lisibles et propriétés personnalisées.
- **PaperZD / Unreal Animation Blueprint** pour les notifies et, plus tard, les graphes d’animation.

---

# 1. Structure de données

## 1.1 Principe général

Une animation 2D composée est constituée de :

```text
Animation2DAsset
 ├── SpriteLibrary
 ├── SpriteComposition
 ├── AnimationClips
 ├── Events / Notifies
 ├── Metadata
 └── ImportSourceData
```

Le modèle doit séparer clairement :

- les images disponibles ;
- les parties visibles du personnage ou de l’objet ;
- les animations ;
- les keyframes ;
- les données source optionnelles ;
- les données génériques utilisables par CasaEngine.

---

# 2. Animation2DAsset

`Animation2DAsset` est le fichier racine de l’animation.

Il représente un personnage, un objet animé, un effet visuel ou une entité 2D composée.

Exemples :

```text id="dt7orw"
Hero_Player
Enemy_Bat
NPC_Villager
Chest_Open
Magic_Effect
```

Structure conceptuelle :

```csharp id="n8oi3l"
public sealed class Animation2DAsset
{
    public string Name { get; set; }

    public SpriteLibrary SpriteLibrary { get; set; }

    public SpriteComposition Composition { get; set; }

    public List<AnimationClip2D> Clips { get; set; }

    public Dictionary<string, string> CustomProperties { get; set; }

    public AnimationImportMetadata ImportMetadata { get; set; }
}
```

Responsabilités :

- regrouper toutes les animations d’une entité ;
- conserver la bibliothèque de sprites utilisée ;
- définir les parties animables ;
- stocker les clips d’animation ;
- conserver les métadonnées nécessaires au round-trip avec la source si besoin.

---

# 3. SpriteLibrary

## 3.1 Rôle

`SpriteLibrary` contient toutes les images ou régions graphiques utilisables par l’animation.

Elle peut référencer :

- une texture complète ;
- une spritesheet ;
- des régions dans une texture ;
- des sprites importés depuis une source externe ;
- des palettes ou CLUT si nécessaire ;
- des informations de source optionnelles.

Exemple :

```csharp id="oerbh7"
public sealed class SpriteLibrary
{
    public List<SpriteAttachment> Attachments { get; set; }
}
```

---

## 3.2 SpriteAttachment

Le terme `Attachment` est inspiré de Spine.

Un attachment représente une image pouvant être assignée à une partie de l’animation.

Exemple :

```csharp id="p48ev3"
public sealed class SpriteAttachment
{
    public string Id { get; set; }

    public string Name { get; set; }

    public string TexturePath { get; set; }

    public Rectangle SourceRectangle { get; set; }

    public Vector2 Pivot { get; set; }

    public Vector2 DefaultOffset { get; set; }

    public Dictionary<string, string> CustomProperties { get; set; }

    public SourceSpriteMetadata SourceMetadata { get; set; }
}
```

Exemples de propriétés :

```text id="r0boin"
Id              = "sprite_0123"
Name            = "Hero_Body_Walk_01"
TexturePath     = "Sprites/Characters/Hero.png"
SourceRectangle = x, y, width, height
Pivot           = 0, 0
DefaultOffset   = -8, -24
```

Selon la source, cet objet peut aussi conserver :

```text id="1kr64u"
originalSpriteId
originalTileIndex
originalClut
originalTPage
originalVramX
originalVramY
originalWidth
originalHeight
```

---

# 4. SpriteComposition

## 4.1 Rôle

`SpriteComposition` décrit les parties qui composent une entité animée.

C’est l’équivalent simplifié du système `Slot` de Spine.

Une composition contient plusieurs `SpritePart`.

Exemple :

```text id="r0fkmi"
SpriteComposition
 ├── Part: shadow
 ├── Part: body
 ├── Part: head
 ├── Part: weapon
 └── Part: effect
```

Selon la source, certaines animations peuvent etre composees de plusieurs primitives ou morceaux de sprites affiches ensemble.

---

## 4.2 SpritePart

Un `SpritePart` est une partie animable.

Il peut changer :

- de sprite ;
- de position ;
- de visibilité ;
- d’ordre d’affichage ;
- de flip horizontal ou vertical ;
- de palette ;
- de couleur ;
- d’opacité ;
- éventuellement de rotation ou scale pour un usage moderne.

Exemple :

```csharp id="4ueyg1"
public sealed class SpritePart
{
    public string Id { get; set; }

    public string Name { get; set; }

    public string DefaultAttachmentId { get; set; }

    public Vector2 DefaultPosition { get; set; }

    public int DefaultDrawOrder { get; set; }

    public bool DefaultVisible { get; set; }

    public bool DefaultFlipX { get; set; }

    public bool DefaultFlipY { get; set; }

    public Dictionary<string, string> CustomProperties { get; set; }
}
```

Exemple :

```text id="n7aqqf"
Part body
 ├── DefaultAttachment = sprite_0123
 ├── Position = 0, 0
 ├── DrawOrder = 10
 └── Visible = true
```

---

# 5. AnimationClip2D

## 5.1 Rôle

Un `AnimationClip2D` représente une animation précise.

Exemples :

```text id="x7glv4"
Idle_Down
Walk_Down
Walk_Up
Attack_Left
Hurt
Sleep
Warp_In
Warp_Out
```

Structure :

```csharp id="pvxh7h"
public sealed class AnimationClip2D
{
    public string Name { get; set; }

    public int FrameRate { get; set; }

    public int FrameCount { get; set; }

    public bool Loop { get; set; }

    public List<AnimationTrack2D> Tracks { get; set; }

    public List<AnimationEvent2D> Events { get; set; }

    public Dictionary<string, string> CustomProperties { get; set; }

    public SourceAnimationMetadata SourceMetadata { get; set; }
}
```

  Pour la V1, il faut rester aligné sur le modèle **time-based** déjà en place.

Exemple :

```text id="8td3e0"
FrameRate = 30
FrameCount = 12
Loop = true
```

---

# 6. AnimationTrack2D

## 6.1 Principe

Une piste anime une propriété précise d’un objet précis.

Ce modèle est inspiré de Godot et Unity.

Exemples :

```text id="iqtf0d"
body.attachment
body.position
body.visible
body.flipX
body.drawOrder
shadow.position
weapon.visible
```

Structure :

```csharp id="8y6gox"
public sealed class AnimationTrack2D
{
    public string TargetPartId { get; set; }

    public string PropertyName { get; set; }

    public AnimationTrackValueType ValueType { get; set; }

    public AnimationInterpolationMode Interpolation { get; set; }

    public List<Keyframe2D> Keyframes { get; set; }
}
```

---

## 6.2 Types de pistes

Les types de pistes utiles à plus long terme sont principalement :

```text id="vjbnnc"
AttachmentTrack     -> changement de sprite/image
PositionTrack       -> changement de position X/Y
VisibilityTrack     -> affichage ou masquage
DrawOrderTrack      -> ordre d’affichage
FlipTrack           -> flip horizontal/vertical
PaletteTrack        -> changement de palette/CLUT si nécessaire
EventTrack          -> son, hitbox, effet, script
```

Ces pistes ne sont pas dans le périmètre de la V1 actuelle. Elles deviennent pertinentes à partir du moment où l’éditeur gère une vraie composition multi-sprite.

Pour les versions suivantes, les plus importantes sont :

```text id="tfnck9"
attachment
position
visible
drawOrder
flipX
flipY
```

---

# 7. Keyframe2D

## 7.1 Rôle

Une keyframe représente une valeur à un instant donné.

Exemple :

```csharp id="6ghxy3"
public sealed class Keyframe2D
{
  public float TimeSeconds { get; set; }

    public object Value { get; set; }

    public AnimationInterpolationMode Interpolation { get; set; }

    public Dictionary<string, string> CustomProperties { get; set; }
}
```

Exemple :

```json id="b0nqne"
{
  "time_seconds": 0.133,
  "value": {
    "x": 2,
    "y": -1
  },
  "interpolation": "Step"
}
```

---

## 7.2 Interpolation

Quand des tracks de propriétés existeront, l’interpolation par défaut devra être `Step`.

Cela signifie que la valeur change brutalement au temps indiqué.

```text id="3xh98g"
0.0s   -> sprite_0100
0.133s -> sprite_0101
0.266s -> sprite_0102
```

Il ne faut pas interpoler automatiquement les positions ou les sprites tant que le systeme reste centre sur des changements discrets.

Modes possibles :

```csharp id="05irji"
public enum AnimationInterpolationMode
{
    Step,
    Linear,
    Cubic
}
```

Pour une premiere version de tracks de proprietes :

```text id="20ll47"
Step uniquement
```

Pour les versions futures :

```text id="85vqt0"
Linear pour position/scale/rotation
Cubic pour animations modernes
```

---

# 8. DrawOrder

## 8.1 Rôle

L’ordre d’affichage est essentiel pour les animations composées.

Deux approches sont possibles :

## Approche simple

Chaque `SpritePart` possède un `DrawOrder`.

```text id="lrckrx"
shadow = 0
body   = 10
head   = 20
weapon = 30
```

## Approche animable

Le draw order peut changer pendant l’animation.

Exemple :

```text id="rmilni"
Time 0.0s:
 ├── body
 └── weapon

Time 0.2s:
 ├── weapon
 └── body
```

Cela peut être utile pour :

- une arme qui passe devant ou derrière le corps ;
- un effet visuel qui change de profondeur ;
- une entité qui se retourne ;
- une animation importée depuis un moteur PSX.

Structure possible :

```csharp id="q47kj2"
public sealed class DrawOrderKeyframe
{
  public float TimeSeconds { get; set; }

    public List<string> OrderedPartIds { get; set; }
}
```

Quand la composition multi-sprite arrivera, un simple `drawOrder` par partie pourra suffire pour une premiere iteration.

---

# 9. AnimationEvent2D

## 9.1 Rôle

Les événements d’animation sont inspirés de PaperZD, Unreal et Unity Animation Events.

Ils permettent de déclencher quelque chose à un temps précis.

Exemples génériques :

```text id="wvvn95"
0.10s -> PlaySound("step")
0.20s -> EnableHitbox("sword")
0.26s -> SpawnEffect("slash")
0.33s -> DisableHitbox("sword")
```

Structure :

```csharp id="ypu4ka"
public sealed class AnimationEvent2D
{
  public float TimeSeconds { get; set; }

    public string EventType { get; set; }

    public string EventName { get; set; }

    public Dictionary<string, string> Parameters { get; set; }
}
```

Cela peut servir plus tard à représenter :

```text id="ogru3u"
sons
effets visuels
hitboxes
flags gameplay
appels de scripts
moments de collision
```

Pour la V1, seuls deux événements sont nécessaires : `changeSprite` et `restart`.

---

# 10. Données source optionnelles

## 10.1 Pourquoi conserver des données source ?

Une source d’import spécifique peut conserver des informations brutes si cela aide au round-trip ou au diagnostic.

Pour la V1 générique CasaEngine, ces données restent optionnelles et ne doivent pas structurer le runtime ni l’éditeur. Si elles existent, il est utile de pouvoir conserver :

- les identifiants de source ;
- le chemin ou le nom du fichier source ;
- les propriétés brutes non mappées ;
- les valeurs utiles au debug ou au round-trip.

Il vaut mieux garder ces données dans un bloc optionnel que les mélanger au contrat runtime principal.

---

## 10.2 Exemple conceptuel

```csharp id="00mjuv"
public sealed class SourceAnimationMetadata
{
  public string SourceId { get; set; }

  public string SourceFile { get; set; }

  public Dictionary<string, string> RawProperties { get; set; }
}
```

---

# 11. Format JSON ouvert

## 11.1 Philosophie

Le format doit être inspiré de Tiled :

- lisible ;
- stable ;
- versionné ;
- extensible ;
- compatible avec des outils externes ;
- sans données cachées uniquement dans l’éditeur.

Exemple simplifié :

```json id="iz04oj"
{
  "formatVersion": 1,
  "name": "Hero_Player",
  "spriteLibrary": {
    "attachments": [
      {
        "id": "sprite_0123",
        "name": "body_walk_01",
        "texture": "Sprites/Characters/Hero.png",
        "sourceRectangle": {
          "x": 0,
          "y": 0,
          "width": 24,
          "height": 32
        },
        "pivot": {
          "x": 0,
          "y": 0
        }
      }
    ]
  },
  "composition": {
    "parts": [
      {
        "id": "body",
        "name": "Body",
        "defaultAttachmentId": "sprite_0123",
        "defaultPosition": {
          "x": 0,
          "y": 0
        },
        "defaultDrawOrder": 10,
        "defaultVisible": true
      }
    ]
  },
  "clips": [
    {
      "name": "Walk_Down",
      "frameRate": 30,
      "frameCount": 12,
      "loop": true,
      "tracks": [
        {
          "targetPartId": "body",
          "propertyName": "attachment",
          "valueType": "String",
          "interpolation": "Step",
          "keyframes": [
            {
              "frame": 0,
              "value": "sprite_0123"
            },
            {
              "frame": 4,
              "value": "sprite_0124"
            },
            {
              "frame": 8,
              "value": "sprite_0125"
            }
          ]
        },
        {
          "targetPartId": "body",
          "propertyName": "position",
          "valueType": "Vector2",
          "interpolation": "Step",
          "keyframes": [
            {
              "frame": 0,
              "value": {
                "x": 0,
                "y": 0
              }
            },
            {
              "frame": 4,
              "value": {
                "x": 1,
                "y": 0
              }
            }
          ]
        }
      ]
    }
  ]
}
```

---

# 12. Runtime CasaEngine

## 12.1 AnimationPlayer2D

Le runtime doit pouvoir lire un `AnimationClip2D` et appliquer les valeurs aux `SpritePart`.

Structure possible :

```csharp id="mwud57"
public sealed class AnimationPlayer2D
{
    public Animation2DAsset Asset { get; set; }

    public AnimationClip2D CurrentClip { get; set; }

    public int CurrentFrame { get; set; }

    public float CurrentTime { get; set; }

    public bool IsPlaying { get; set; }

    public void Play(string clipName);

    public void Stop();

    public void Update(float elapsedTime);

    public void ApplyFrame(int frame);
}
```

---

## 12.2 SpriteCompositionRenderer

Le renderer doit dessiner l’état courant de la composition.

```csharp id="xzhy4h"
public sealed class SpriteCompositionRenderer
{
    public void Draw(SpriteCompositionInstance instance);
}
```

Il doit respecter :

```text id="hcjc6j"
attachment courant
position courante
visible
drawOrder
flipX
flipY
palette éventuelle
couleur
opacité
```

---

# 13. Instance runtime

L’asset décrit les données.  
L’instance décrit l’état courant en jeu.

```csharp id="sgtlm0"
public sealed class SpriteCompositionInstance
{
    public Animation2DAsset Asset { get; set; }

    public List<SpritePartInstance> Parts { get; set; }
}
```

```csharp id="m468q5"
public sealed class SpritePartInstance
{
    public string PartId { get; set; }

    public string CurrentAttachmentId { get; set; }

    public Vector2 Position { get; set; }

    public int DrawOrder { get; set; }

    public bool Visible { get; set; }

    public bool FlipX { get; set; }

    public bool FlipY { get; set; }
}
```

---

# 14. Éditeur d’animation

## 14.1 Objectif de l’éditeur

La V1 de l’éditeur doit rester volontairement étroite.

Elle doit permettre de :

- ouvrir un asset `.anim2d` existant ;
- prévisualiser une animation 2D qui n’affiche qu’un seul sprite à la fois ;
- lire l’animation dans le temps ;
- afficher une timeline read-only graduée en secondes ;
- permettre le scroll horizontal et le zoom de cette timeline ;
- afficher les événements sous forme de marqueurs sur la piste ;
- sélectionner un événement dans la timeline ;
- visualiser les propriétés de l’événement sélectionné dans l’inspector ;
- afficher les validations utiles à l’asset courant.

L’éditeur doit être intégré à CasaEngine et utiliser MGUI pour l’interface.

---

# 15. Organisation générale de l’éditeur V1

Interface proposée pour la V1 :

```text id="wjy4n3"
Animation2DEditor
 ├── Preview Viewport
 ├── Event Timeline (read-only)
 ├── Inspector
 └── Validation / Debug Panel
```

Les surfaces de composition, de bibliothèque de sprites, de hiérarchie de parties et de tracks de propriétés sont hors V1.

---

# 16. Preview Viewport

## 16.1 Rôle

Le `Preview Viewport` affiche l’animation en temps réel.

En V1, il n’affiche qu’un seul sprite courant. Ce sprite peut changer dans le temps uniquement via les événements supportés.

Fonctionnalités minimales :

- Play ;
- Pause ;
- Stop ;
- scrubbing temporel ;
- zoom ;
- déplacement de la vue ;
- grille ;
- axes X/Y ;
- affichage du sprite courant ;
- affichage des avertissements de validation.

---

# 17. Event Timeline

## 17.1 Rôle

La timeline V1 est une timeline read-only.

Elle affiche :

- l’échelle de temps graduée en secondes ;
- la position courante du playhead ;
- une unique piste logique d’événements ;
- les événements triés par temps, matérialisés par des losanges.

Structure :

```text id="kog7qy"
Timeline
 ├── Time ruler
 ├── Playhead
 └── Event Track
  ├── changeSprite
  ├── changeSprite
  └── restart
```

## 17.2 Fonctionnalités

La timeline V1 doit permettre de :

- déplacer le playhead ;
- faire défiler horizontalement la vue de timeline ;
- zoomer pour modifier l’espacement entre les graduations ;
- afficher les événements sur une piste unique ;
- sélectionner un événement ;
- synchroniser la sélection vers l’inspector ;
- montrer clairement le temps de chaque événement.

La timeline V1 ne doit pas permettre de :

- créer des événements ;
- déplacer des événements ;
- supprimer des événements ;
- éditer une valeur directement sur la timeline ;
- afficher des pistes de composition ou de propriétés.

---

# 18. Inspector

## 18.1 Rôle

L’inspector V1 sert principalement à visualiser les propriétés de l’événement sélectionné dans la timeline.

Exemple pour un événement `changeSprite` :

```text id="5qk2rp"
Selected Event
 ├── Time: 0.133s
 ├── Type: changeSprite
 └── Sprite: sprite_0123
```

Exemple pour un événement `restart` :

```text id="eph22v"
Selected Event
 ├── Time: 0.400s
 └── Type: restart
```

En V1, l’inspector n’a pas besoin d’exposer une édition complète de l’asset.

---

# 19. Événements V1

## 19.1 Rôle

La V1 repose uniquement sur les événements.

Il n’y a pas encore de composition, pas de parties animées, pas de tracks de propriétés et pas de keyframes de position/visibilité/draw order.

Une animation V1 contient :

- un seul sprite courant visible à un instant donné ;
- une unique piste logique d’événements ;
- des événements triés dans le temps.

## 19.2 Types d’événements supportés en V1

Deux événements sont requis en V1 :

```text id="ll4l2f"
changeSprite
restart
```

### `changeSprite`

Cet événement change le sprite courant affiché par la preview/runtime.

Il doit exposer une propriété de référence vers le sprite cible afin que l’inspector puisse l’afficher.

Dans le contrat `.anim2d` V1, cette référence est persistée via `sprite_asset_id`.

### `restart`

Cet événement redémarre l’animation depuis le début.

Il permet de créer une boucle sans introduire pour l’instant un système de composition ou de state machine.

---

# 20. Ouverture / Sauvegarde / Export

## 20.1 Ouvrir un asset `.anim2d`

La V1 doit d’abord ouvrir les assets `.anim2d` existants via le pipeline déjà présent dans CasaEngine.

Entrée attendue :

```text id="7o7c1e"
.anim2d
```

Le chargement doit préserver le contrat `.anim2d` existant.

La V1 se limite cependant à un sous-ensemble fonctionnel :

- un seul sprite courant ;
- une seule piste logique d’événements ;
- aucune composition éditable ;
- aucun track de propriété éditable.

## 20.2 Sauvegarder un asset `.anim2d`

La V1 ne doit pas introduire une nouvelle extension ni un nouveau contrat de fichier.

Sortie attendue :

```text id="vuuyzt"
.anim2d
```

---

# 21. Validation / Debug Panel

## 21.1 Rôle

Ce panneau est important pour l’authoring et le diagnostic.

En V1, il doit détecter au minimum :

- animation vide ;
- événement avec temps négatif ;
- événement non trié ;
- type d’événement inconnu ;
- événement `changeSprite` sans sprite cible ;
- référence de sprite introuvable.

Exemple :

```text id="xwq5n6"
Warnings
 ├── Time 0.000s: changeSprite has no sprite target
 ├── Time 0.233s: sprite reference was not found
 ├── Time 0.400s: unknown event type
 └── Timeline: events are not sorted by time
```

---

# 22. Workflow d’utilisation

## 22.1 Ouvrir une animation `.anim2d`

```text id="rgivxl"
1. L’utilisateur ouvre un asset `.anim2d` depuis le Content Browser.
2. L’éditeur charge l’asset.
3. L’éditeur affiche la preview et la piste d’événements.
```

## 22.2 Vérifier l’animation

```text id="fach4d"
1. L’utilisateur lance la preview.
2. Il scrube la timeline time-based.
3. Il sélectionne un événement.
4. Il visualise ses propriétés dans l’inspector.
5. Il contrôle que la séquence `changeSprite` / `restart` correspond au résultat attendu.
```

## 22.3 Valider

```text id="hypcgc"
1. L’utilisateur consulte les avertissements.
2. Il vérifie les références de sprite.
3. Il vérifie l’ordre temporel des événements.
4. Le runtime peut ensuite charger l’animation.
```

---

# 23. Règles importantes pour la V1

## 23.1 Animation time-based

La V1 reste basée sur le temps.

```text id="0x0ayp"
0.0s
0.1s
0.2s
0.3s
```

## 23.2 Mono-sprite uniquement

La V1 n’affiche qu’un seul sprite à la fois.

Il n’y a pas encore de composition de plusieurs sprites visibles simultanément.

## 23.3 Une seule piste logique d’événements

La V1 n’utilise qu’une seule piste logique d’événements.

Cette piste contient uniquement des événements `changeSprite` et `restart`.

## 23.4 Timeline read-only

La timeline V1 sert à visualiser et sélectionner les événements, pas à les éditer. Elle reste graduée en secondes, scrollable horizontalement et zoomable.

## 23.5 Conserver le contrat `.anim2d`

La V1 ne doit pas casser le contrat `.anim2d` existant.

## 23.6 Séparer runtime et éditeur

Le runtime CasaEngine ne doit pas dépendre de l’éditeur.

---

# 24. V1 recommandée

La V1 doit rester simple et testable.

Fonctionnalités V1 :

```text id="ohmp6b"
- Ouvrir un `.anim2d` existant
- Prévisualiser une animation mono-sprite
- Afficher une timeline read-only graduée d’événements
- Supporter le scroll horizontal et le zoom de timeline
- Afficher les événements comme marqueurs sélectionnables
- Sélectionner un événement sur la timeline
- Visualiser ses propriétés dans l’inspector
- Supporter `changeSprite`
- Supporter `restart`
- Afficher les validations de base
```

À éviter en V1 :

```text id="lvff7x"
- composition multi-sprite
- parties / slots
- tracks de propriétés
- édition de timeline
- keyframes de position
- draw order animé
- state machine
- animation graph
```

---

# 25. V2 possible

La V2 peut ajouter :

```text id="001rxd"
- composition de plusieurs sprites
- parties / slots
- tracks de propriétés
- changement de visibilité
- draw order animé
- timeline authoring
- édition des événements dans l’inspector
- validation plus détaillée
```

---

# 26. V3 possible

La V3 peut évoluer vers un vrai système moderne d’animation 2D :

```text id="4gqb5l"
- AnimationGraph2D
- State machine
- Blend entre animations
- Conditions de transition
- Parameters
- Preview gameplay
- Intégration CharacterController
- Intégration dialogue/cutscene
- Animation UI MGUI
- Animation d’effets 2D modernes
```

---

# 27. Résumé des inspirations

## Godot

À reprendre :

```text id="o88udj"
- timeline claire
- preview directe
- scrubbing
```

## Unity

À reprendre :

```text id="66vwap"
- lecture temporelle
- sélection visuelle d’éléments sur la timeline
- inspector synchronisé avec la sélection
```

## PaperZD / Unreal

À reprendre plus tard :

```text id="itrivo"
- notifies typés
- animation graph
- state machine
- transitions gameplay
```

---

# 28. Conclusion

La V1 de l’éditeur Animation2D CasaEngine ne doit pas partir tout de suite sur la composition.

Elle doit d’abord livrer un sous-ensemble simple :

```text id="wqoqmx"
.anim2d
mono-sprite
event track unique
changeSprite
restart
timeline read-only
inspector de visualisation
```

La composition, les tracks de propriétés et l’authoring complet de timeline sont reportés aux versions suivantes.

Decisions: see [ADR-0015](../decisions/0015-animation2d-editor-v1.md).
