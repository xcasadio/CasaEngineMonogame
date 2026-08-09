# CasaEngine — Spécification fonctionnelle d’un système de particules

**Cible :** CasaEngine / MonoGame / C#  
**But :** définir les fonctionnalités attendues d’un système de particules, depuis une V1 simple et robuste jusqu’à une architecture avancée comparable aux moteurs modernes.  
**Périmètre :** runtime, rendu, édition, preview, sérialisation, performances, intégration moteur.

---

## 1. Objectifs généraux

Un système de particules doit permettre de créer rapidement des effets visuels dynamiques : fumée, feu, explosion, étincelles, poussière, magie, pluie, neige, impact, traînées, débris, effets UI, aura, téléportation, etc.

Pour CasaEngine, l’objectif n’est pas de copier directement Unity, Unreal Niagara ou Godot, mais de reprendre leurs concepts utiles :

- un **asset de particules** réutilisable ;
- un ou plusieurs **émetteurs** par effet ;
- des paramètres de naissance, de vie, de mouvement et de rendu ;
- des courbes et gradients pour faire évoluer les particules dans le temps ;
- une séparation claire entre **données d’édition**, **données runtime** et **rendu** ;
- une intégration propre avec l’éditeur CasaEngine : visualisation, édition, preview, sauvegarde, hot reload.

---

## 2. Définitions

### ParticleEffectAsset

Asset sérialisé qui décrit un effet complet. Exemple : `FireSmall`, `ExplosionBarrel`, `MagicPortal`, `RainArea`.

Il contient :

- la liste des émetteurs ;
- les paramètres globaux de simulation ;
- les ressources utilisées : textures, matériaux, shaders, flipbooks ;
- les paramètres exposés à l’éditeur ou au gameplay ;
- les métadonnées : nom, GUID, version, tags, catégorie.

### ParticleSystemComponent

Composant placé sur une entité CasaEngine.

Il référence un `ParticleEffectAsset` et contrôle l’instance runtime :

- play / pause / stop / restart ;
- looping ou one-shot ;
- simulation en espace local ou monde ;
- overrides locaux ;
- activation / désactivation ;
- interaction avec la transform de l’entité.

### ParticleEmitterDefinition

Décrit une source d’émission dans un effet.

Un effet peut avoir plusieurs émetteurs :

- fumée + étincelles + lumière temporaire ;
- explosion principale + débris + onde de choc ;
- pluie + splash au sol ;
- feu + braises + chaleur/distorsion.

### ParticleRuntimeInstance

Objet runtime non sérialisé qui contient :

- les particules vivantes ;
- les timers ;
- l’état de lecture ;
- les buffers CPU/GPU ;
- les données temporaires de rendu.

### ParticleModifier / Module

Bloc de comportement appliqué aux particules.

Exemples :

- `GravityModifier` ;
- `DragModifier` ;
- `ColorOverLifetimeModifier` ;
- `SizeOverLifetimeModifier` ;
- `VelocityOverLifetimeModifier` ;
- `RotationOverLifetimeModifier` ;
- `CollisionModifier`.

Pour la V1, les modifiers peuvent être codés sous forme de propriétés simples. Pour les versions avancées, ils deviennent des modules empilables comme dans les moteurs modernes.

---

## 3. Architecture recommandée

### 3.1 Séparer authoring, runtime et rendu

Le système doit être pensé en trois couches :

```text
ParticleEffectAsset
  Données éditables et sérialisées
        ↓ instanciation
ParticleRuntimeInstance
  Etat vivant, timers, particules actives, buffers temporaires
        ↓ extraction render data
ParticleRenderer
  Tri, batching, draw calls, shaders, blend states
```

Cette séparation évite de polluer l’asset avec des données runtime et permet :

- plusieurs instances du même effet dans la scène ;
- des overrides par entité ;
- une preview éditeur indépendante du jeu ;
- du hot reload ;
- des optimisations de rendu centralisées.

### 3.2 Un asset, plusieurs instances

Un `ParticleEffectAsset` ne doit jamais contenir l’état vivant des particules.

Exemple :

```csharp
public sealed class ParticleSystemComponent : Component
{
    public AssetReference<ParticleEffectAsset> Effect { get; set; }
    public bool PlayOnStart { get; set; }
    public bool LoopingOverride { get; set; }
    public float SimulationSpeed { get; set; } = 1.0f;

    private ParticleRuntimeInstance? _runtime;
}
```

### 3.3 CPU d’abord, GPU ensuite

Pour CasaEngine / MonoGame, la V1 doit être **CPU based**.

Raisons :

- plus simple à debugger ;
- plus simple à sérialiser ;
- plus simple à prévisualiser dans l’éditeur ;
- suffisant pour une V1 2D/3D billboard ;
- compatible avec tous les backends MonoGame.

Ensuite, l’évolution moderne peut ajouter :

- rendu instancié GPU ;
- simulation GPU optionnelle ;
- compute shaders seulement si le backend choisi le supporte réellement ;
- fallback CPU obligatoire.

---

## 4. Version V1 — Système de base robuste

La V1 doit être volontairement limitée mais complète pour produire déjà des effets utiles.

### 4.1 Fonctionnalités V1 prioritaires

#### Lecture de l’effet

- Play.
- Pause.
- Stop.
- Restart.
- Looping.
- One-shot.
- Duration.
- Start delay.
- Simulation speed.
- Play on start.
- Stop action : `None`, `DisableComponent`, `DestroyEntity`, `NotifyOnly`.

#### Émission

- Rate over time : nombre de particules par seconde.
- Burst : émission ponctuelle à un temps donné.
- Max particles par émetteur.
- Seed aléatoire optionnelle.
- Random seed automatique ou fixe.
- Prewarm simple pour les effets continus, comme feu ou fumée.

#### Formes d’émission V1

Commencer avec des formes simples :

- Point.
- Circle / Disc.
- Rectangle / Box 2D.
- Sphere simple pour les scènes 3D.
- Cone simple pour flammes, jets, projectiles, spot-like effects.

Chaque forme doit pouvoir générer :

- position initiale ;
- direction initiale ;
- variation aléatoire ;
- émission depuis le volume ou depuis la surface.

#### Propriétés initiales de particules

Pour chaque particule :

- lifetime min/max ;
- position ;
- velocity ;
- acceleration ;
- size min/max ;
- rotation min/max ;
- angular velocity min/max ;
- start color ;
- start alpha ;
- texture index ou frame de départ ;
- custom data V1 optionnelle : `float Custom0`, `float Custom1`.

#### Simulation V1

- Intégration simple : position += velocity * dt.
- Accélération constante.
- Gravity scale.
- Drag / damping.
- Rotation.
- Scale over lifetime.
- Color over lifetime.
- Alpha over lifetime.
- Velocity over lifetime simple.
- Local space / world space.
- Pause quand l’entité ou la scène est désactivée.

#### Courbes et gradients V1

La V1 doit absolument inclure :

- `FloatCurve` pour taille, alpha, vitesse, rotation ;
- `ColorGradient` pour couleur ;
- possibilité d’évaluer une courbe à partir de l’âge normalisé : `age / lifetime` ;
- édition basique dans l’éditeur.

Exemples :

```text
SizeOverLifetime:
0.0 → 0.2
0.3 → 1.0
1.0 → 0.0

AlphaOverLifetime:
0.0 → 0.0
0.1 → 1.0
0.8 → 1.0
1.0 → 0.0
```

#### Rendu V1

Pour la V1, il faut viser des particules billboard :

- rendu sprite 2D ;
- rendu billboard 3D face caméra ;
- texture unique par émetteur ;
- tint color ;
- alpha blending ;
- additive blending ;
- multiply blending optionnel ;
- sorting simple : none, by distance, by layer/depth ;
- render layer / render queue ;
- bounds pour culling.

##### Choix de rendu MonoGame conseillé

V1 minimale :

- `SpriteBatch` pour particules 2D écran / UI / world 2D ;
- batch CPU interne pour particules 3D billboard via `DynamicVertexBuffer` dès que possible.

Ne pas dépendre uniquement de `SpriteBatch` pour toutes les particules à long terme. Il est pratique pour commencer, mais un système moderne doit pouvoir construire ses propres quads, trier, batcher, gérer des shaders spécifiques et préparer une évolution vers l’instancing.

#### Matériaux V1

Créer un `ParticleMaterial` simple :

- texture principale ;
- blend mode ;
- shader/effect optionnel ;
- soft particles désactivé en V1 ;
- lighting désactivé en V1 ;
- depth write off par défaut ;
- depth test configurable.

#### Culling V1

- Bounding box/sphere par émetteur.
- Mise à jour simple des bounds à partir des particules vivantes.
- Option `AlwaysVisible` pour les effets importants.
- Culling caméra.
- Pause simulation when culled optionnelle, à utiliser prudemment.

#### Pooling V1

Indispensable dès la V1 :

- tableau préalloué de particules ;
- freelist d’indices libres ;
- pas d’allocation par frame ;
- max particles fixe par émetteur ;
- réutilisation des `ParticleRuntimeInstance` pour les effets fréquents.

### 4.2 Structure de données V1 proposée

```csharp
public sealed class ParticleEffectAsset : Asset
{
    public string Name { get; set; } = string.Empty;
    public Guid Guid { get; set; }
    public int Version { get; set; } = 1;
    public List<ParticleEmitterDefinition> Emitters { get; set; } = new();
}
```

```csharp
public sealed class ParticleEmitterDefinition
{
    public string Name { get; set; } = "Emitter";
    public bool Enabled { get; set; } = true;

    public float Duration { get; set; } = 5.0f;
    public bool Looping { get; set; } = true;
    public float StartDelay { get; set; } = 0.0f;
    public int MaxParticles { get; set; } = 1000;

    public ParticleEmissionModule Emission { get; set; } = new();
    public ParticleShapeModule Shape { get; set; } = new();
    public ParticleInitialModule Initial { get; set; } = new();
    public ParticleSimulationModule Simulation { get; set; } = new();
    public ParticleRendererModule Renderer { get; set; } = new();
}
```

```csharp
public struct Particle
{
    public bool Alive;
    public float Age;
    public float Lifetime;

    public Vector3 Position;
    public Vector3 Velocity;
    public Vector3 Acceleration;

    public float Rotation;
    public float AngularVelocity;
    public Vector2 Size;

    public Color Color;
    public float Alpha;

    public int TextureFrame;
    public uint RandomSeed;
}
```

### 4.3 Modules V1

#### ParticleEmissionModule

```csharp
public sealed class ParticleEmissionModule
{
    public float RateOverTime { get; set; } = 10.0f;
    public List<ParticleBurst> Bursts { get; set; } = new();
}
```

Fonctionnalités :

- émission continue ;
- bursts ;
- compteur fractionnaire pour éviter les pertes de particules à bas framerate ;
- limitation par max particles.

#### ParticleShapeModule

```csharp
public sealed class ParticleShapeModule
{
    public ParticleShapeType ShapeType { get; set; } = ParticleShapeType.Point;
    public Vector3 Size { get; set; } = Vector3.One;
    public float Radius { get; set; } = 1.0f;
    public float Angle { get; set; } = 25.0f;
    public bool EmitFromShell { get; set; }
}
```

#### ParticleInitialModule

```csharp
public sealed class ParticleInitialModule
{
    public FloatRange Lifetime { get; set; } = new(1.0f, 2.0f);
    public FloatRange Speed { get; set; } = new(1.0f, 3.0f);
    public FloatRange Rotation { get; set; } = new(0.0f, 360.0f);
    public FloatRange AngularVelocity { get; set; } = new(-90.0f, 90.0f);
    public Vector2Range Size { get; set; } = new(Vector2.One, Vector2.One);
    public ColorGradient StartColor { get; set; } = ColorGradient.White;
}
```

#### ParticleSimulationModule

```csharp
public sealed class ParticleSimulationModule
{
    public ParticleSimulationSpace SimulationSpace { get; set; } = ParticleSimulationSpace.Local;
    public Vector3 Gravity { get; set; } = new(0, -9.81f, 0);
    public float GravityScale { get; set; } = 0.0f;
    public float Drag { get; set; } = 0.0f;

    public FloatCurve SizeOverLifetime { get; set; } = FloatCurve.Constant(1.0f);
    public FloatCurve AlphaOverLifetime { get; set; } = FloatCurve.Constant(1.0f);
    public ColorGradient ColorOverLifetime { get; set; } = ColorGradient.White;
}
```

#### ParticleRendererModule

```csharp
public sealed class ParticleRendererModule
{
    public ParticleRenderMode RenderMode { get; set; } = ParticleRenderMode.Billboard;
    public AssetReference<Texture2D> Texture { get; set; }
    public ParticleBlendMode BlendMode { get; set; } = ParticleBlendMode.Alpha;
    public ParticleSortMode SortMode { get; set; } = ParticleSortMode.None;
    public bool DepthTest { get; set; } = true;
    public bool DepthWrite { get; set; } = false;
    public int RenderQueue { get; set; } = 3000;
}
```

---

## 5. Éditeur V1

L’éditeur est aussi important que le runtime. Sans éditeur, le système sera trop lent à utiliser.

### 5.1 Asset Particle Effect

Créer un type d’asset :

```text
Assets/Particles/Explosion_Barrel.particle.json
Assets/Particles/Fire_Torch.particle.json
Assets/Particles/Magic_Hit.particle.json
```

Fonctionnalités :

- création d’un nouvel asset depuis le content browser ;
- duplication ;
- renommage ;
- preview thumbnail ;
- drag & drop sur une entité ;
- référence via `ParticleSystemComponent`.

### 5.2 Fenêtre d’édition Particle Editor

Une fenêtre dédiée doit contenir :

```text
+---------------------------------------------------------------+
| Toolbar: Play Pause Stop Restart Loop SimSpeed Camera         |
+-------------------------+-------------------------------------+
| Emitters list           | Preview viewport                    |
| - Smoke                 |                                     |
| - Sparks                |        effet en temps réel           |
| - Flash                 |                                     |
+-------------------------+-------------------------------------+
| Properties / Modules                                          |
| Emission / Shape / Initial / Simulation / Renderer             |
+---------------------------------------------------------------+
| Timeline / Bursts / Events / Debug stats                       |
+---------------------------------------------------------------+
```

### 5.3 Preview viewport V1

Fonctionnalités :

- afficher l’effet en temps réel ;
- play / pause / stop / restart ;
- simulation speed ;
- loop toggle ;
- fond transparent, noir, gris ou checkerboard ;
- afficher/masquer la grille ;
- afficher/masquer les gizmos d’émission ;
- caméra 2D et/ou orbit caméra 3D ;
- reset camera ;
- zoom ;
- affichage des bounds ;
- affichage du nombre de particules vivantes ;
- affichage du nombre de draw calls estimé ;
- affichage CPU time simulation/render.

### 5.4 Propriétés éditables V1

L’éditeur doit permettre d’éditer :

- nom de l’effet ;
- nom de chaque émetteur ;
- enabled/disabled par émetteur ;
- duration ;
- looping ;
- start delay ;
- max particles ;
- rate over time ;
- bursts ;
- shape ;
- lifetime ;
- speed ;
- size ;
- rotation ;
- angular velocity ;
- gravity ;
- drag ;
- curves ;
- gradients ;
- texture ;
- blend mode ;
- render mode ;
- sorting ;
- depth settings.

### 5.5 Édition des courbes

Le système de courbes est une brique importante pour CasaEngine au-delà des particules.

Fonctionnalités V1 :

- ajout/suppression de points ;
- déplacement de points ;
- interpolation linéaire ;
- clamp entre 0 et 1 pour le temps normalisé ;
- presets : constant, fade in, fade out, bell, pulse ;
- reset ;
- copie/collage de courbe.

Fonctionnalités avancées :

- tangentes ;
- interpolation smooth ;
- bezier ;
- édition multi-courbes ;
- zoom/pan dans le graphe ;
- snapping ;
- preview de valeur sous le curseur.

### 5.6 Édition des gradients

Fonctionnalités V1 :

- points de couleur ;
- points d’alpha ;
- interpolation linéaire ;
- presets : white, fire, smoke, magic blue, electric, fade transparent ;
- copie/collage.

### 5.7 Gizmos d’émission

Dans la scène et dans le Particle Editor :

- point : petite croix ;
- circle/sphere : cercle/sphère wireframe ;
- box : rectangle/box wireframe ;
- cone : cône wireframe ;
- direction initiale ;
- bounds runtime.

### 5.8 Intégration PropertyGrid

Le système doit être compatible avec la future `PropertyGrid` CasaEngine/MGUI.

Contraintes :

- édition temps réel ;
- pas d’allocation excessive à chaque frame ;
- binding propre des propriétés ;
- undo/redo ;
- reset valeur par défaut ;
- indication des overrides ;
- validation des ranges.

### 5.9 Undo / Redo V1

Chaque modification d’un paramètre doit être undoable :

- changement numérique ;
- ajout/suppression d’émetteur ;
- ajout/suppression de burst ;
- modification de courbe ;
- modification de gradient ;
- changement de texture ;
- changement de blend mode.

### 5.10 Sauvegarde et hot reload

- Auto dirty flag quand un paramètre change.
- Save asset.
- Reimport texture/material si modifié.
- Mise à jour immédiate de toutes les previews ouvertes.
- Mise à jour des instances en scène si l’asset change.
- Possibilité de conserver ou reset l’état runtime lors du hot reload.

---

## 6. Sérialisation

### 6.1 Format recommandé V1

Pour CasaEngine, commencer par un format texte lisible : JSON ou YAML.

Exemple :

```json
{
  "type": "ParticleEffectAsset",
  "version": 1,
  "guid": "2f9c3e33-28d8-4cc8-89a4-9d68c35f2b0a",
  "name": "Explosion_Barrel",
  "emitters": [
    {
      "name": "Smoke",
      "enabled": true,
      "duration": 2.5,
      "looping": false,
      "maxParticles": 300,
      "emission": {
        "rateOverTime": 0,
        "bursts": [
          { "time": 0.0, "countMin": 80, "countMax": 120 }
        ]
      },
      "shape": {
        "type": "Sphere",
        "radius": 0.5,
        "emitFromShell": false
      },
      "initial": {
        "lifetime": { "min": 1.0, "max": 2.0 },
        "speed": { "min": 1.0, "max": 3.0 },
        "size": {
          "min": { "x": 0.3, "y": 0.3 },
          "max": { "x": 1.2, "y": 1.2 }
        }
      },
      "renderer": {
        "texture": "Assets/Textures/Particles/smoke.png",
        "blendMode": "Alpha",
        "renderMode": "Billboard"
      }
    }
  ]
}
```

### 6.2 Versioning

Chaque asset doit contenir :

- `version` ;
- `guid` ;
- `type` ;
- éventuellement `schemaVersion` si CasaEngine a déjà une convention d’assets.

Prévoir des migrations :

```csharp
public interface IAssetMigration
{
    int FromVersion { get; }
    int ToVersion { get; }
    void Migrate(JsonObject assetJson);
}
```

### 6.3 Références d’assets

Ne pas sérialiser directement des objets MonoGame (`Texture2D`, `Effect`, etc.).

Sérialiser uniquement :

- GUID d’asset ;
- chemin logique ;
- nom de ressource ;
- type attendu.

Exemple :

```json
"texture": {
  "guid": "a4fcf343-2f8b-4d2c-9bb3-e4adf41a1b20",
  "path": "Assets/Textures/Particles/smoke.png"
}
```

### 6.4 Overrides

Dès la V1 ou V1.5, prévoir la notion d’override :

```json
"overrides": {
  "SimulationSpeed": 0.8,
  "Emitters[Smoke].MaxParticles": 100
}
```

Cela permettra :

- d’utiliser le même effet avec des variations ;
- de modifier un effet par instance ;
- de préparer un système de prefab.

---

## 7. Roadmap vers un système moderne

## 7.1 V1 — CPU billboard particles

Objectif : produire rapidement des effets utilisables.

Fonctionnalités :

- asset particule ;
- composant particule ;
- un ou plusieurs émetteurs ;
- émission continue + bursts ;
- formes simples ;
- particules CPU ;
- courbes simples ;
- gradients ;
- rendu billboard/sprite ;
- blend modes de base ;
- editor preview ;
- property editing ;
- sérialisation JSON ;
- pooling ;
- culling simple ;
- debug stats.

## 7.2 V1.5 — Qualité de production

Objectif : rendre le système confortable et fiable.

Fonctionnalités :

- undo/redo complet ;
- presets d’effets ;
- thumbnails ;
- duplication d’émetteurs ;
- drag & drop de textures ;
- hot reload ;
- gizmos scène ;
- édition avancée des courbes ;
- édition avancée des gradients ;
- flipbook animation ;
- texture sheet ;
- sub UV random ;
- billboards orientés velocity ;
- alignement caméra / monde / velocity ;
- world/local simulation plus fiable ;
- préchauffage stable ;
- deterministic mode avec seed ;
- profiling détaillé.

## 7.3 V2 — Système modulaire

Objectif : passer d’un système à propriétés fixes vers un système extensible.

Fonctionnalités :

- modules empilables ;
- catégories de modules : Spawn, Initialize, Update, Render ;
- ordre de modules visible dans l’éditeur ;
- activation/désactivation par module ;
- presets de modules ;
- paramètres exposés ;
- système de blackboard/parameters ;
- bindings vers gameplay : intensité, couleur, taille, direction ;
- modules custom C# ;
- API d’extension editor/runtime.

Exemple de stack :

```text
Emitter: Sparks
  Spawn
    - Rate Over Time
    - Burst At Time
  Initialize Particle
    - Set Lifetime
    - Set Position From Cone
    - Set Velocity Random
    - Set Color Random Gradient
  Update Particle
    - Gravity
    - Drag
    - Color Over Lifetime
    - Size Over Lifetime
    - Kill When Below Plane
  Render
    - Sprite Billboard Renderer
```

## 7.4 V2.5 — Effets composés et événements

Objectif : créer des effets complexes sans code gameplay spécifique.

Fonctionnalités :

- sub emitters ;
- event on spawn ;
- event on death ;
- event on collision ;
- spawn child particles ;
- trails simples ;
- ribbons ;
- particules attachées à bones/sockets ;
- émission depuis mesh ;
- émission depuis texture/mask ;
- collisions simples avec sol/plan ;
- collisions avec primitives ;
- callbacks gameplay optionnels.

Exemples :

- une particule d’étincelle meurt → spawn mini flash ;
- une goutte touche le sol → spawn splash ;
- un projectile laisse une ribbon trail ;
- un mesh brûle → émission depuis ses vertices.

## 7.5 V3 — Rendu avancé

Objectif : obtenir un rendu proche des moteurs modernes.

Fonctionnalités :

- soft particles avec depth buffer ;
- distortion / refraction ;
- normal mapped particles ;
- lit particles ;
- shadow receiving optionnel ;
- particles affected by lights ;
- emissive particles ;
- bloom-friendly HDR color ;
- depth fading ;
- camera fading ;
- GPU instancing ;
- sorting par matériau/render queue ;
- atlas packing ;
- ribbon renderer robuste ;
- mesh particles ;
- decal particles ;
- volumetric-like particles via slicing ou impostors ;
- interaction avec la pipeline forward/deferred.

## 7.6 V4 — Simulation avancée / GPU

Objectif : supporter des volumes de particules élevés.

Fonctionnalités :

- simulation GPU optionnelle ;
- compute shader backend si disponible ;
- fallback CPU ;
- millions de particules pour effets non gameplay ;
- simulation en texture/buffer ;
- collisions depth buffer ;
- vector fields ;
- turbulence/noise fields ;
- attractors/repulsors ;
- curl noise ;
- signed distance fields optionnels ;
- LOD automatique ;
- fixed timestep optionnel ;
- simulation cache pour cinématiques.

---

## 8. Fonctionnalités avancées à prévoir

### 8.1 Modules modernes

#### Spawn modules

- Rate over time.
- Rate over distance.
- Burst.
- Spawn from event.
- Spawn from mesh.
- Spawn from texture mask.
- Spawn from spline/path.
- Spawn from gameplay trigger.

#### Initialize modules

- Lifetime.
- Position.
- Velocity.
- Acceleration.
- Color.
- Alpha.
- Size.
- Rotation.
- Mass.
- Custom data.
- Random frame.
- Random material variant.

#### Update modules

- Gravity.
- Drag.
- Force.
- Vortex.
- Attractor.
- Repulsor.
- Turbulence.
- Curl noise.
- Collision.
- Kill by age.
- Kill by distance.
- Kill by bounds.
- Color over lifetime.
- Size over lifetime.
- Velocity over lifetime.
- Rotation over lifetime.
- Texture frame over lifetime.
- Custom curve over lifetime.

#### Render modules

- Sprite billboard.
- Stretched billboard.
- Velocity aligned billboard.
- Mesh renderer.
- Ribbon renderer.
- Trail renderer.
- Decal renderer.
- Light renderer.
- UI renderer.

### 8.2 Champs de force

Types de forces :

- directionnelle ;
- point attractor ;
- point repulsor ;
- vortex ;
- turbulence ;
- wind zone ;
- noise field ;
- vector field importé ;
- gravity well.

Ces forces peuvent être :

- globales à la scène ;
- locales à un effet ;
- attachées à une entité ;
- limitées par volume.

### 8.3 Collisions

Niveaux possibles :

#### V1

- pas de collision ou collision plan simple.

#### V2

- collision avec plan ;
- collision avec AABB/sphere ;
- kill on collision ;
- bounce ;
- dampening ;
- spawn event on collision.

#### V3

- collision avec physics world ;
- collision depth buffer ;
- collision heightmap/tilemap ;
- collision signed distance field.

### 8.4 Trails et ribbons

Fonctionnalités :

- trail par particule ;
- ribbon entre particules ;
- width over lifetime ;
- color over lifetime ;
- texture tiling ;
- UV scrolling ;
- smoothing ;
- max segments ;
- lifetime des segments ;
- alignement caméra ;
- mode projectile/tracer.

### 8.5 Flipbooks et texture sheets

Fonctionnalités :

- atlas en grille ;
- frame count ;
- start frame random ;
- frame over lifetime ;
- FPS ;
- loop animation ;
- random row ;
- blending entre frames optionnel ;
- sub-UV pour explosions, fumée, feu.

### 8.6 Particules éclairées

Fonctionnalités avancées :

- particules unlit ;
- particules lit ;
- normal map ;
- roughness/metallic optionnels ;
- contribution aux light buffers ;
- particules emissive ;
- lien avec bloom ;
- light spawn temporaire, par exemple flash d’explosion.

### 8.7 Interaction avec le gameplay

Le système doit exposer une API claire :

```csharp
particleSystem.Play();
particleSystem.Stop();
particleSystem.Restart();
particleSystem.SetFloat("Intensity", 2.0f);
particleSystem.SetColor("MainColor", Color.OrangeRed);
particleSystem.Emit(25);
particleSystem.EmitAt(position, count: 10);
```

Fonctionnalités :

- paramètres exposés ;
- overrides runtime ;
- trigger burst depuis code ;
- callbacks optionnels ;
- pas de dépendance gameplay dans le coeur du système.

---

## 9. Intégration avec CasaEngine

### 9.1 Composant ECS / Entity Component

Ajouter un composant :

```csharp
public sealed class ParticleSystemComponent : Component
{
    public AssetReference<ParticleEffectAsset> Effect { get; set; }
    public bool PlayOnStart { get; set; } = true;
    public bool Looping { get; set; } = true;
    public bool SimulateInEditor { get; set; } = true;
    public ParticleInstanceOverrides Overrides { get; set; } = new();
}
```

### 9.2 Systèmes runtime

Créer des systèmes dédiés :

```text
ParticleSystemManager
  - crée/détruit les instances runtime
  - update simulation
  - gère pooling
  - expose API globale

ParticleRenderSystem
  - collecte les render packets
  - trie par caméra/render queue/material
  - remplit les buffers
  - exécute les draw calls

ParticleEditorService
  - preview isolée
  - gizmos
  - thumbnails
  - hot reload
```

### 9.3 Pipeline de rendu

Intégration souhaitée :

```text
Opaque geometry
Skybox
Transparent geometry
Particles alpha blended
Particles additive
Distortion particles
Post-processing / bloom
UI particles optionnelles
```

À prévoir selon le moteur :

- render queues ;
- blend states partagés ;
- depth buffer accessible ;
- camera-specific particle collection ;
- tri par distance pour transparence ;
- mode UI particles rendu dans MGUI ou overlay.

### 9.4 Multi-caméras et éditeur

Le système doit marcher avec :

- caméra jeu ;
- caméra éditeur ;
- plusieurs viewports ;
- preview asset isolée ;
- split screen ;
- rendu dans texture pour l’éditeur.

Une instance runtime ne doit pas être liée à une seule caméra. Le rendu doit produire des données consommables par chaque caméra.

---

## 10. Performance

### 10.1 Règles de base

- Aucune allocation par frame en simulation.
- Tableaux préalloués.
- Freelist pour particules mortes.
- Max particles strict.
- Batch par texture/material/blend mode.
- Culling avant rendu.
- Update seulement les instances actives.
- Éviter LINQ dans les boucles runtime.
- Éviter les delegates/events par particule.
- Utiliser des structs simples pour les particules.
- Mesurer simulation time et render time.

### 10.2 Budgets recommandés

Définir des budgets par qualité :

```text
Low quality:
  max global particles: 5 000
  max per effect: 500

Medium quality:
  max global particles: 20 000
  max per effect: 2 000

High quality:
  max global particles: 100 000+
  max per effect: 10 000+
```

Ces valeurs dépendent fortement du rendu, du tri, du backend et du hardware.

### 10.3 Profiling éditeur

Afficher dans le Particle Editor :

- particules vivantes ;
- particules émises par seconde ;
- particules mortes par seconde ;
- max particles atteint ;
- simulation CPU time ;
- render CPU time ;
- draw calls ;
- nombre de matériaux/textures ;
- mémoire estimée ;
- overdraw approximatif si possible.

### 10.4 LOD

Fonctionnalités avancées :

- réduire rate over time avec la distance ;
- réduire max particles ;
- désactiver certains émetteurs ;
- changer texture/material ;
- désactiver collisions ;
- désactiver lighting ;
- remplacer par sprite impostor ;
- cull complet au-delà d’une distance.

---

## 11. UX éditeur moderne

### 11.1 Organisation des propriétés

L’éditeur doit éviter un énorme panneau plat.

Organisation recommandée :

```text
Particle Effect
  General
  Parameters
  Emitters
    Emitter: Smoke
      General
      Emission
      Shape
      Initial
      Simulation
      Renderer
      Debug
```

### 11.2 Recherche et filtres

Fonctionnalités utiles :

- recherche de propriété ;
- masquer propriétés avancées ;
- afficher seulement les valeurs modifiées ;
- afficher seulement les modules actifs ;
- favoris ;
- reset section.

### 11.3 Presets

Presets d’effets :

- Fire small.
- Smoke puff.
- Explosion.
- Spark burst.
- Magic hit.
- Rain.
- Snow.
- Dust footstep.
- Projectile trail.
- UI sparkle.

Presets de modules :

- fade out alpha ;
- grow then shrink ;
- gravity sparks ;
- smoke upward drift ;
- fire gradient ;
- additive magic glow.

### 11.4 Preview thumbnails

Créer des miniatures d’assets particules :

- rendu offscreen ;
- frame à temps donné ;
- fond configurable ;
- mise à jour automatique à la sauvegarde ;
- cache des thumbnails.
- contrainte : un thumbnail `.particle` doit provenir du meme rendu de scene/runtime que la preview editor ; pas d'icone synthétique spécifique a l'asset.
- le placeholder n'est autorisé que pendant le chargement asynchrone ; l'image finale doit etre issue du rendu de la scene.

### 11.5 Timeline

Même en V1, une timeline simple est utile :

- duration ;
- start delay ;
- bursts ;
- curseur de temps ;
- loop region ;
- markers d’événements.

Version avancée :

- scrub temporel ;
- simulation cache ;
- visualisation des bursts ;
- events ;
- synchronisation audio/cutscene.

---

## 12. API minimale recommandée

### 12.1 Runtime API

```csharp
public interface IParticleSystem
{
    bool IsPlaying { get; }
    bool IsPaused { get; }
    bool IsAlive { get; }

    void Play();
    void Pause();
    void Stop(bool clearParticles = true);
    void Restart();
    void Emit(int count);

    void SetFloat(string parameterName, float value);
    void SetVector3(string parameterName, Vector3 value);
    void SetColor(string parameterName, Color value);
}
```

### 12.2 Editor API

```csharp
public interface IParticlePreviewService
{
    ParticlePreviewSession CreateSession(ParticleEffectAsset asset);
    void DestroySession(ParticlePreviewSession session);
    void RenderPreview(ParticlePreviewSession session, RenderTarget2D target, float deltaTime);
}
```

### 12.3 Asset API

```csharp
public interface IParticleEffectSerializer
{
    ParticleEffectAsset Load(string path);
    void Save(ParticleEffectAsset asset, string path);
    bool CanMigrate(int version);
    ParticleEffectAsset Migrate(ParticleEffectAsset asset);
}
```

---

## 13. Backlog d’implémentation suggéré

### Phase 1 — Fondations runtime

- ⬜ Créer `ParticleEffectAsset`.
- ⬜ Créer `ParticleEmitterDefinition`.
- ⬜ Créer `ParticleSystemComponent`.
- ⬜ Créer `ParticleRuntimeInstance`.
- ⬜ Créer structure `Particle`.
- ⬜ Implémenter pooling/freelist.
- ⬜ Implémenter play/pause/stop/restart.
- ⬜ Implémenter duration/looping/start delay.
- ⬜ Implémenter max particles.

### Phase 2 — Émission et simulation V1

- ⬜ Implémenter rate over time.
- ⬜ Implémenter bursts.
- ⬜ Implémenter formes point/circle/box/sphere/cone.
- ⬜ Implémenter lifetime random.
- ⬜ Implémenter speed random.
- ⬜ Implémenter size random.
- ⬜ Implémenter rotation/angular velocity.
- ⬜ Implémenter gravity/drag.
- ⬜ Implémenter local/world space.

### Phase 3 — Courbes et gradients

- ⬜ Créer `FloatCurve`.
- ⬜ Créer `ColorGradient`.
- ⬜ Implémenter size over lifetime.
- ⬜ Implémenter alpha over lifetime.
- ⬜ Implémenter color over lifetime.
- ⬜ Ajouter presets de courbes.
- ⬜ Ajouter presets de gradients.

### Phase 4 — Rendu V1

- ⬜ Créer `ParticleRendererModule`.
- ⬜ Supporter texture par émetteur.
- ⬜ Supporter alpha blend.
- ⬜ Supporter additive blend.
- ⬜ Supporter billboard 2D.
- ⬜ Supporter billboard 3D face caméra.
- ⬜ Ajouter sorting simple.
- ⬜ Ajouter render queue.
- ⬜ Ajouter culling bounds.
- ⬜ Ajouter debug draw bounds.

### Phase 5 — Sérialisation

- ⬜ Définir format `.particle.json`.
- ⬜ Sérialiser asset + emitters + modules.
- ⬜ Sérialiser références d’assets.
- ⬜ Ajouter `version` et `guid`.
- ⬜ Ajouter migration V1.
- ⬜ Ajouter validation des assets.
- ⬜ Ajouter tests de load/save.

### Phase 6 — Éditeur V1

- ⬜ Créer Particle Editor window.
- ⬜ Ajouter preview viewport.
- ⬜ Ajouter toolbar play/pause/stop/restart.
- ⬜ Ajouter liste d’émetteurs.
- ⬜ Ajouter édition des propriétés générales.
- ⬜ Ajouter édition émission/shape/initial/simulation/renderer.
- ⬜ Ajouter édition des bursts.
- ⬜ Ajouter édition des courbes.
- ⬜ Ajouter édition des gradients.
- ⬜ Ajouter gizmos d’émission.
- ⬜ Ajouter stats runtime.
- ⬜ Ajouter sauvegarde asset.
- ⬜ Ajouter hot reload simple.

### Phase 7 — Qualité éditeur

- ⬜ Ajouter undo/redo.
- ⬜ Ajouter presets.
- ⬜ Ajouter thumbnails.
- ⬜ Ajouter drag & drop texture.
- ⬜ Ajouter duplication d’émetteurs.
- ⬜ Ajouter copier/coller module.
- ⬜ Ajouter reset default values.
- ⬜ Ajouter recherche de propriétés.

### Phase 8 — V2 modulaire

- ⬜ Introduire interface `IParticleModule`.
- ⬜ Séparer modules Spawn/Initialize/Update/Render.
- ⬜ Ajouter ordre de modules éditable.
- ⬜ Ajouter activation/désactivation de modules.
- ⬜ Ajouter paramètres exposés.
- ⬜ Ajouter bindings runtime.
- ⬜ Ajouter modules custom C#.

### Phase 9 — Rendu avancé

- ⬜ Ajouter flipbook texture sheet.
- ⬜ Ajouter stretched billboard.
- ⬜ Ajouter velocity alignment.
- ⬜ Ajouter soft particles.
- ⬜ Ajouter distortion.
- ⬜ Ajouter trails.
- ⬜ Ajouter ribbons.
- ⬜ Ajouter mesh particles.
- ⬜ Ajouter particles lit/emissive.

### Phase 10 — Simulation avancée

- ⬜ Ajouter collisions simples.
- ⬜ Ajouter sub emitters.
- ⬜ Ajouter events on death/collision.
- ⬜ Ajouter forces locales.
- ⬜ Ajouter turbulence/noise.
- ⬜ Ajouter LOD.
- ⬜ Ajouter GPU instancing.
- ⬜ Étudier simulation GPU selon backend MonoGame.

---

## 14. Critères d’acceptation V1

La V1 est considérée terminée quand :

- un asset `.particle.json` peut être créé, édité, sauvegardé et rechargé ;
- un `ParticleSystemComponent` peut jouer cet asset dans une scène ;
- l’effet peut être visualisé dans une fenêtre d’éditeur ;
- au moins 3 effets de test existent : feu, explosion, fumée ;
- le système supporte au moins 2 émetteurs dans le même effet ;
- les particules ont lifetime, position, velocity, size, rotation, color, alpha ;
- les courbes size/alpha et gradient color fonctionnent ;
- l’émission continue et les bursts fonctionnent ;
- le rendu alpha et additive fonctionne ;
- aucune allocation majeure ne se produit par frame pendant la simulation ;
- le système affiche des statistiques de debug ;
- les bounds et le culling caméra fonctionnent ;
- les modifications dans l’éditeur sont visibles en temps réel.

---

## 15. Tests recommandés

### Tests unitaires

- Évaluation de `FloatCurve`.
- Évaluation de `ColorGradient`.
- Random range déterministe avec seed fixe.
- Émission rate over time stable selon delta time.
- Burst déclenché au bon temps.
- Pool/freelist sans doublons.
- Sérialisation load/save identique.
- Migration d’asset.

### Tests runtime

- 10 systèmes de feu en scène.
- 100 explosions one-shot successives.
- Effet looping pendant 10 minutes.
- Activation/désactivation d’entité.
- Changement de scène.
- Hot reload d’un asset utilisé par plusieurs instances.
- Caméra qui entre/sort des bounds.

### Tests éditeur

- Modification en live de chaque propriété.
- Undo/redo de valeurs simples.
- Undo/redo de courbes et gradients.
- Duplication d’émetteur.
- Suppression d’émetteur.
- Sauvegarde/reload.
- Preview thumbnail.

---

## 16. Points d’attention spécifiques à MonoGame

### 16.1 SpriteBatch

`SpriteBatch` est pratique pour commencer, surtout en 2D, mais il ne doit pas devenir la seule architecture de rendu des particules.

Limites à anticiper :

- moins de contrôle sur les données par particule ;
- tri et batching limités par les modes SpriteBatch ;
- shaders plus spécifiques plus difficiles à organiser ;
- transition vers ribbons/mesh particles moins naturelle.

Approche recommandée :

- V1 : SpriteBatch accepté pour 2D/simple ;
- V1/V2 : créer un renderer particule dédié avec vertex buffers dynamiques ;
- V2+ : envisager instancing pour les billboards ;
- V4 : étudier GPU simulation seulement si le backend CasaEngine le permet.

### 16.2 DynamicVertexBuffer

Pour les particules 3D billboard, générer des quads dynamiques est une bonne étape intermédiaire :

- un vertex buffer dynamique ;
- un index buffer partagé ;
- 4 vertices par particule ;
- 6 indices par particule ;
- tri côté CPU si nécessaire ;
- batch par material/blend mode.

### 16.3 Shaders MGFX

Prévoir plusieurs effets/shaders :

- particle unlit alpha ;
- particle additive ;
- particle flipbook ;
- particle soft ;
- particle distortion ;
- particle lit.

Ne pas tout faire dans un shader unique trop complexe en V1.

---

## 17. Recommandation de design final

Pour CasaEngine, la meilleure trajectoire est :

1. **V1 CPU simple mais propre**, avec asset, component, emitters, curves, gradients, preview editor et JSON.
2. **V1.5 production-ready**, avec undo/redo, presets, thumbnails, hot reload, flipbooks et profiling.
3. **V2 modulaire**, avec modules empilables et paramètres exposés.
4. **V3 rendu avancé**, avec trails, ribbons, soft particles, distortion, lit particles.
5. **V4 GPU/large scale**, uniquement après avoir une base CPU fiable et un renderer dédié.

La priorité doit être l’ergonomie éditeur : un système de particules sans preview, courbes, gradients, presets et hot reload sera techniquement utilisable, mais peu productif.

---

## 18. Références d’inspiration

Ces références servent à aligner la spec sur des concepts utilisés dans des moteurs actuels :

- Unity Particle System modules : https://docs.unity3d.com/6000.4/Documentation/Manual/ParticleSystemModules.html
- Unity Visual Effect Graph contexts : https://docs.unity3d.com/Packages/com.unity.visualeffectgraph%4017.0/manual/Contexts.html
- Unreal Engine Niagara overview : https://dev.epicgames.com/documentation/unreal-engine/overview-of-niagara-effects-for-unreal-engine
- Unreal Engine Niagara key concepts : https://dev.epicgames.com/documentation/unreal-engine/key-concepts-in-niagara-effects-for-unreal-engine
- Godot GPUParticles3D : https://docs.godotengine.org/en/stable/classes/class_gpuparticles3d.html
- Godot ParticleProcessMaterial : https://docs.godotengine.org/en/stable/classes/class_particleprocessmaterial.html
- MonoGame SpriteBatch API : https://docs.monogame.net/api/Microsoft.Xna.Framework.Graphics.SpriteBatch.html
- MonoGame DynamicVertexBuffer API : https://docs.monogame.net/api/Microsoft.Xna.Framework.Graphics.DynamicVertexBuffer.html
