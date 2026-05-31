# Éditeur d’animations 2D composées pour CasaEngine

## Objectif

L’objectif est de développer dans CasaEngine un éditeur d’animations 2D capable de reconstruire, visualiser, corriger et exporter des animations issues du jeu **Alundra**.

Les animations d’Alundra ne doivent pas être vues uniquement comme une succession d’images complètes. Elles peuvent être composées de plusieurs sprites, chacun ayant sa propre position, visibilité, ordre d’affichage et parfois ses propres changements de frame.

L’éditeur doit donc être pensé comme un éditeur d’**animations 2D composées**, inspiré de plusieurs outils existants :

- **Godot AnimationPlayer** pour le principe de pistes animant des propriétés.
- **Spine** pour les notions de slots, attachments et draw order.
- **Unity Animation Window** pour l’interface timeline, keyframes et preview.
- **Tiled** pour la philosophie de formats ouverts, JSON lisibles et propriétés personnalisées.
- **PaperZD / Unreal Animation Blueprint** pour les notifies et, plus tard, les graphes d’animation.

L’outil ne doit pas être conçu uniquement pour Alundra.  
Il doit être un éditeur générique d’animations 2D pour CasaEngine, avec un importer/exporter spécifique aux données Alundra.

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
- les données spécifiques à Alundra ;
- les données génériques utilisables par CasaEngine.

---

# 2. Animation2DAsset

`Animation2DAsset` est le fichier racine de l’animation.

Il représente un personnage, un objet animé, un effet visuel ou une entité 2D composée.

Exemples :

```text id="dt7orw"
Alundra_Player
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
- conserver les métadonnées nécessaires au round-trip avec Alundra si possible.

---

# 3. SpriteLibrary

## 3.1 Rôle

`SpriteLibrary` contient toutes les images ou régions graphiques utilisables par l’animation.

Elle peut référencer :

- une texture complète ;
- une spritesheet ;
- des régions dans une texture ;
- des sprites extraits depuis les données Alundra ;
- des palettes ou CLUT si nécessaire ;
- des informations PSX spécifiques.

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

    public AlundraSpriteMetadata AlundraMetadata { get; set; }
}
```

Exemples de propriétés :

```text id="r0boin"
Id              = "sprite_0123"
Name            = "Alundra_Body_Walk_01"
TexturePath     = "Sprites/Alundra/Player.png"
SourceRectangle = x, y, width, height
Pivot           = 0, 0
DefaultOffset   = -8, -24
```

Pour Alundra, cet objet peut aussi conserver :

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

Dans Alundra, certaines animations peuvent être composées de plusieurs primitives ou morceaux de sprites affichés ensemble.

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

    public AlundraAnimationMetadata AlundraMetadata { get; set; }
}
```

Pour Alundra, il est conseillé de travailler avec un modèle **frame-based** plutôt que uniquement time-based.

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

Les types de pistes nécessaires pour Alundra sont principalement :

```text id="vjbnnc"
AttachmentTrack     -> changement de sprite/image
PositionTrack       -> changement de position X/Y
VisibilityTrack     -> affichage ou masquage
DrawOrderTrack      -> ordre d’affichage
FlipTrack           -> flip horizontal/vertical
PaletteTrack        -> changement de palette/CLUT si nécessaire
EventTrack          -> son, hitbox, effet, script
```

Pour une V1, les plus importantes sont :

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

Une keyframe représente une valeur à une frame donnée.

Exemple :

```csharp id="6ghxy3"
public sealed class Keyframe2D
{
    public int Frame { get; set; }

    public object Value { get; set; }

    public AnimationInterpolationMode Interpolation { get; set; }

    public Dictionary<string, string> CustomProperties { get; set; }
}
```

Exemple :

```json id="b0nqne"
{
  "frame": 4,
  "value": {
    "x": 2,
    "y": -1
  },
  "interpolation": "Step"
}
```

---

## 7.2 Interpolation

Pour Alundra, l’interpolation par défaut doit être `Step`.

Cela signifie que la valeur change brutalement à la frame indiquée.

```text id="3xh98g"
Frame 0 -> sprite_0100
Frame 4 -> sprite_0101
Frame 8 -> sprite_0102
```

Il ne faut pas interpoler automatiquement les positions ou les sprites, car les animations PSX sont souvent pensées comme des changements discrets.

Modes possibles :

```csharp id="05irji"
public enum AnimationInterpolationMode
{
    Step,
    Linear,
    Cubic
}
```

Pour la V1 :

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
Frame 0:
 ├── body
 └── weapon

Frame 6:
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
    public int Frame { get; set; }

    public List<string> OrderedPartIds { get; set; }
}
```

Pour la V1, un simple `drawOrder` par partie peut suffire.

---

# 9. AnimationEvent2D

## 9.1 Rôle

Les événements d’animation sont inspirés de PaperZD, Unreal et Unity Animation Events.

Ils permettent de déclencher quelque chose à une frame précise.

Exemples :

```text id="wvvn95"
Frame 3  -> PlaySound("step")
Frame 6  -> EnableHitbox("sword")
Frame 8  -> SpawnEffect("slash")
Frame 10 -> DisableHitbox("sword")
```

Structure :

```csharp id="ypu4ka"
public sealed class AnimationEvent2D
{
    public int Frame { get; set; }

    public string EventType { get; set; }

    public string EventName { get; set; }

    public Dictionary<string, string> Parameters { get; set; }
}
```

Pour Alundra, cela peut servir plus tard à représenter :

```text id="ogru3u"
sons
effets visuels
hitboxes
flags gameplay
appels de scripts
moments de collision
```

Pour la V1, les événements peuvent être simplement affichés mais pas forcément exécutés.

---

# 10. Métadonnées Alundra

## 10.1 Pourquoi conserver les données originales ?

L’importer Alundra doit préserver le maximum d’informations brutes.

Même si CasaEngine utilise une structure propre, il est important de conserver :

- les ids originaux ;
- les offsets d’origine ;
- les indices de sprites ;
- les adresses ou offsets dans les fichiers ;
- les palettes ;
- les durées ;
- les flags inconnus ;
- les valeurs non encore comprises.

Cela évite de perdre des informations pendant l’analyse.

---

## 10.2 Exemple

```csharp id="00mjuv"
public sealed class AlundraAnimationMetadata
{
    public int OriginalAnimationId { get; set; }

    public int OriginalDataOffset { get; set; }

    public string SourceFile { get; set; }

    public Dictionary<string, int> RawFlags { get; set; }

    public List<UnknownAlundraField> UnknownFields { get; set; }
}
```

```csharp id="69qoxu"
public sealed class UnknownAlundraField
{
    public string Name { get; set; }

    public int Offset { get; set; }

    public int RawValue { get; set; }

    public string Comment { get; set; }
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
  "name": "Alundra_Player",
  "spriteLibrary": {
    "attachments": [
      {
        "id": "sprite_0123",
        "name": "body_walk_01",
        "texture": "Sprites/Alundra/player.png",
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

# 14.1 Objectif de l’éditeur

L’éditeur doit permettre de :

- importer une animation Alundra ;
- afficher les sprites composant l’animation ;
- lire l’animation frame par frame ;
- visualiser et modifier les keyframes ;
- inspecter les propriétés des parties ;
- corriger les offsets ;
- changer les sprites associés aux frames ;
- modifier l’ordre d’affichage ;
- exporter vers un format CasaEngine ;
- conserver les métadonnées d’origine.

L’éditeur doit être intégré à CasaEngine et utiliser MGUI pour l’interface.

---

# 15. Organisation générale de l’éditeur

Interface proposée :

```text id="wjy4n3"
Animation2DEditor
 ├── Animation Browser
 ├── Sprite Library Panel
 ├── Part / Slot Hierarchy
 ├── Preview Viewport
 ├── Timeline
 ├── Track List
 ├── Property Grid
 ├── Event / Notify Panel
 ├── Import / Export Panel
 └── Validation / Debug Panel
```

---

# 16. Animation Browser

## 16.1 Rôle

Le panneau `Animation Browser` permet de voir toutes les animations disponibles pour un asset.

Exemple :

```text id="1tr74l"
Alundra_Player
 ├── Idle_Down
 ├── Idle_Up
 ├── Idle_Left
 ├── Idle_Right
 ├── Walk_Down
 ├── Walk_Up
 ├── Walk_Left
 ├── Walk_Right
 ├── Attack_Down
 └── Hurt
```

Fonctionnalités :

- sélectionner un clip ;
- créer un nouveau clip ;
- dupliquer un clip ;
- renommer un clip ;
- supprimer un clip ;
- filtrer les clips ;
- afficher les métadonnées Alundra associées.

---

# 17. Sprite Library Panel

## 17.1 Rôle

Le panneau `Sprite Library` affiche tous les sprites disponibles.

Il doit permettre de :

- voir les sprites extraits ;
- afficher leur id ;
- afficher leur nom ;
- afficher leur rectangle source ;
- afficher leur pivot ;
- afficher leur palette éventuelle ;
- glisser-déposer un sprite sur une partie ;
- rechercher un sprite par id ou nom ;
- filtrer les sprites utilisés/non utilisés.

Pour Alundra, ce panneau est essentiel pour vérifier si l’extraction est correcte.

---

# 18. Part / Slot Hierarchy

## 18.1 Rôle

Ce panneau affiche les parties de l’animation.

Inspiré des slots de Spine.

Exemple :

```text id="tguzn1"
Parts
 ├── shadow
 ├── body
 ├── head
 ├── weapon
 └── effect
```

Fonctionnalités :

- sélectionner une partie ;
- renommer une partie ;
- activer/désactiver une partie ;
- changer son ordre d’affichage ;
- verrouiller une partie ;
- afficher le sprite courant ;
- créer une nouvelle partie ;
- supprimer une partie ;
- grouper des parties si nécessaire.

Pour Alundra, les parties peuvent correspondre à des primitives ou morceaux de sprites utilisés par une animation.

---

# 19. Preview Viewport

## 19.1 Rôle

Le `Preview Viewport` affiche l’animation en temps réel.

Il doit permettre de visualiser l’animation comme elle apparaîtra dans CasaEngine.

Fonctionnalités minimales :

- Play ;
- Pause ;
- Stop ;
- frame suivante ;
- frame précédente ;
- zoom ;
- déplacement de la vue ;
- grille ;
- axes X/Y ;
- origine de l’entité ;
- bounding box ;
- affichage des pivots ;
- affichage des rectangles de sprites ;
- affichage du draw order ;
- fond transparent, noir, blanc ou custom.

---

## 19.2 Options spécifiques Alundra

Pour reconstruire fidèlement les animations Alundra, le viewport devrait proposer :

```text id="c7bqkf"
Mode CasaEngine
Mode PSX approximatif
Affichage des offsets originaux
Affichage des sprites par primitive
Affichage des palettes/CLUT
Affichage des coordonnées sources
Affichage des flags inconnus
```

---

# 20. Timeline

## 20.1 Rôle

La timeline est inspirée de Unity Animation Window et Godot AnimationPlayer.

Elle affiche :

- les frames ;
- les pistes ;
- les keyframes ;
- la position courante du playhead ;
- la durée du clip ;
- les événements.

Structure :

```text id="kog7qy"
Timeline
 ├── Frame ruler
 ├── Playhead
 ├── Tracks
 │    ├── body.attachment
 │    ├── body.position
 │    ├── body.visible
 │    ├── weapon.attachment
 │    └── weapon.position
 └── Events
```

---

## 20.2 Fonctionnalités

La timeline doit permettre de :

- déplacer le playhead ;
- sélectionner une keyframe ;
- déplacer une keyframe ;
- ajouter une keyframe ;
- supprimer une keyframe ;
- copier/coller une keyframe ;
- dupliquer une plage de frames ;
- zoomer horizontalement ;
- passer en mode frame-by-frame ;
- afficher les frames vides ;
- afficher les changements de sprites ;
- afficher les changements de position.

---

# 21. Track List

## 21.1 Rôle

La `Track List` affiche les propriétés animées.

Exemple :

```text id="t9e0jj"
body
 ├── attachment
 ├── position
 ├── visible
 └── drawOrder

weapon
 ├── attachment
 ├── position
 └── visible
```

Fonctionnalités :

- ajouter une piste ;
- supprimer une piste ;
- masquer une piste ;
- verrouiller une piste ;
- filtrer les pistes ;
- regrouper les pistes par partie ;
- mettre en évidence les pistes modifiées à la frame courante.

---

# 22. Property Grid

## 22.1 Rôle

La `Property Grid` permet d’éditer précisément la valeur sélectionnée.

Elle doit pouvoir éditer :

- les propriétés de l’asset ;
- les propriétés d’un clip ;
- les propriétés d’une partie ;
- les propriétés d’un sprite ;
- les propriétés d’une keyframe ;
- les propriétés d’un événement.

Exemple pour une keyframe de position :

```text id="5qk2rp"
Selected Keyframe
 ├── Frame: 4
 ├── Property: body.position
 ├── X: 2
 ├── Y: -1
 └── Interpolation: Step
```

Exemple pour une partie :

```text id="eph22v"
Part: body
 ├── Default Attachment: sprite_0123
 ├── Default Position X: 0
 ├── Default Position Y: 0
 ├── Default Draw Order: 10
 ├── Visible: true
 ├── Flip X: false
 └── Flip Y: false
```

---

# 23. Event / Notify Panel

## 23.1 Rôle

Ce panneau affiche les événements associés à l’animation.

Exemples :

```text id="ll4l2f"
Frame 3  PlaySound footstep
Frame 6  EnableHitbox sword
Frame 8  SpawnEffect slash
```

Pour la V1, il peut être en lecture seule.

Pour les versions suivantes, il pourra permettre :

- ajouter un événement ;
- supprimer un événement ;
- éditer les paramètres ;
- tester l’événement dans le viewport ;
- connecter l’événement au gameplay CasaEngine.

---

# 24. Import / Export Panel

## 24.1 Import Alundra

Le panneau d’import doit permettre de charger les données extraites par les outils Alundra.

Entrées possibles :

```text id="7o7c1e"
animations.json
sprites.json
spritesheet.png
metadata.json
```

L’importer doit créer :

```text id="te2v3c"
Animation2DAsset
SpriteLibrary
SpriteComposition
AnimationClips
Tracks
Keyframes
Metadata
```

---

## 24.2 Export CasaEngine

L’export CasaEngine doit générer un fichier propre, sans dépendance directe aux structures brutes d’Alundra.

Sorties possibles :

```text id="vuuyzt"
.alundra.animation2d.json
.animation2d.json
.animation2d.casa
```

Le format exporté doit pouvoir être chargé par le runtime CasaEngine.

---

## 24.3 Export debug

L’éditeur peut aussi exporter :

```text id="ax82wr"
spritesheet de preview
GIF de debug
PNG par frame
JSON détaillé avec métadonnées
rapport de validation
```

Cela permet de comparer l’animation reconstruite avec l’animation originale.

---

# 25. Validation / Debug Panel

## 25.1 Rôle

Ce panneau est important pour le reverse engineering.

Il doit détecter :

- sprite manquant ;
- attachment non trouvé ;
- keyframe invalide ;
- frame hors limites ;
- partie sans sprite ;
- draw order incohérent ;
- offset suspect ;
- palette manquante ;
- champ Alundra inconnu ;
- animation vide ;
- durée invalide.

Exemple :

```text id="xwq5n6"
Warnings
 ├── Frame 4: sprite_0188 not found
 ├── Frame 7: body.position has invalid value
 ├── Clip Walk_Down: unknown flag 0x04 is not mapped
 └── Part weapon: no default attachment
```

---

# 26. Workflow d’utilisation

## 26.1 Importer une animation Alundra

```text id="rgivxl"
1. L’utilisateur sélectionne un fichier d’animation extrait.
2. L’importer charge les sprites.
3. L’importer crée les attachments.
4. L’importer crée les parts.
5. L’importer crée les clips.
6. L’importer crée les pistes.
7. L’importer crée les keyframes.
8. L’éditeur affiche l’animation dans le viewport.
```

---

## 26.2 Vérifier l’animation

```text id="fach4d"
1. L’utilisateur lance la preview.
2. Il compare visuellement l’animation.
3. Il active l’affichage des offsets.
4. Il vérifie les sprites utilisés.
5. Il inspecte les keyframes.
6. Il corrige les positions si nécessaire.
```

---

## 26.3 Corriger une frame

```text id="sn6y32"
1. L’utilisateur sélectionne une frame.
2. Il sélectionne une partie.
3. Il modifie le sprite ou la position.
4. Une keyframe est créée ou mise à jour.
5. La preview se met à jour immédiatement.
```

---

## 26.4 Exporter

```text id="hypcgc"
1. L’utilisateur valide l’animation.
2. L’éditeur lance les validations.
3. Les erreurs bloquantes sont affichées.
4. Le fichier CasaEngine est généré.
5. Le runtime peut charger l’animation.
```

---

# 27. Règles importantes pour la V1

## 27.1 Animation frame-based

La V1 doit être basée sur des frames entières.

```text id="0x0ayp"
Frame 0
Frame 1
Frame 2
Frame 3
```

Cela correspond mieux aux données Alundra et évite les erreurs de timing liées aux floats.

---

## 27.2 Interpolation désactivée par défaut

Toutes les pistes doivent utiliser `Step` par défaut.

```text id="1a62pq"
spriteId  -> Step
position  -> Step
visible   -> Step
drawOrder -> Step
flip      -> Step
```

L’interpolation moderne pourra être ajoutée plus tard.

---

## 27.3 Ne pas perdre les données inconnues

Toute donnée Alundra non comprise doit être conservée.

```text id="vcaxld"
UnknownField_00
UnknownFlag_01
RawValue_02
OriginalOffset
OriginalAnimationId
```

Il vaut mieux stocker une donnée inutile que la perdre définitivement.

---

## 27.4 Séparer runtime et éditeur

Le runtime CasaEngine ne doit pas dépendre de l’éditeur.

Structure recommandée :

```text id="vk5vnh"
CasaEngine.Framework
 └── Animations2D runtime

CasaEngine.Editor
 └── Animation2DEditor

CasaEngine.Tools.Alundra
 └── Importer / Exporter Alundra
```

---

# 28. V1 recommandée

La V1 doit rester simple et testable.

Fonctionnalités V1 :

```text id="ohmp6b"
- Charger un Animation2DAsset
- Importer des animations Alundra
- Afficher les sprites extraits
- Afficher une composition de sprites
- Lire un clip en preview
- Avancer frame par frame
- Afficher une timeline
- Afficher les pistes attachment et position
- Éditer spriteId
- Éditer position X/Y
- Éditer visible
- Éditer drawOrder
- Exporter en JSON CasaEngine
- Afficher les erreurs de validation
```

À éviter en V1 :

```text id="lvff7x"
- bones
- IK
- blending
- animation graph
- transitions complexes
- interpolation avancée
- skinning
- mesh deform
- éditeur de state machine
```

---

# 29. V2 possible

La V2 peut ajouter :

```text id="001rxd"
- Animation events
- Notifies
- Hitboxes
- Hurtboxes
- Sons
- Effets visuels
- Comparaison avec capture originale
- Export GIF ou PNG sequence
- Onion skinning
- Outils de duplication de keyframes
- Multi-selection de keyframes
- Édition avancée de draw order
```

---

# 30. V3 possible

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

# 31. Résumé des inspirations

## Godot

À reprendre :

```text id="o88udj"
- Animation par pistes
- Animation de propriétés
- Preview directe
- Structure flexible
```

## Spine

À reprendre :

```text id="8czjbh"
- Slots
- Attachments
- Draw order
- Composition de sprites
```

## Unity

À reprendre :

```text id="66vwap"
- Timeline claire
- Keyframes visibles
- Property editing
- Scrubbing
- Mode record plus tard
```

## Tiled

À reprendre :

```text id="3mlpad"
- Format ouvert
- JSON lisible
- Propriétés personnalisées
- Import/export simple
```

## PaperZD / Unreal

À reprendre plus tard :

```text id="itrivo"
- Notifies
- Animation graph
- State machine
- Transitions gameplay
```

---

# 32. Conclusion

L’éditeur d’animation 2D de CasaEngine doit être pensé comme un outil générique d’animation de sprites composés.

Pour Alundra, il doit d’abord permettre de reconstruire fidèlement les animations extraites :

```text id="wqoqmx"
sprites
parts
offsets
keyframes
ordre d’affichage
durées
métadonnées originales
```

Mais l’architecture doit rester suffisamment propre pour être réutilisée ensuite dans CasaEngine pour :

```text id="cofb50"
personnages 2D
effets visuels
UI animée
cutscenes simples
objets interactifs
animations de gameplay
```

La V1 doit rester très simple :

```text id="el9rho"
Animation2DAsset
SpriteLibrary
SpriteComposition
AnimationClip2D
Tracks
Keyframes
Preview
Timeline
Import Alundra
Export CasaEngine
```

Le plus important est de ne pas créer un outil trop spécialisé Alundra dès le départ.

La bonne approche est :

```text id="zp14sw"
Créer un éditeur CasaEngine d’animations 2D composées
+
Ajouter un importer/exporter Alundra
```
