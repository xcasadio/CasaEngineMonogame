# [ARCHIVE] Système de Materials — Architecture & Liste de tâches

## Etat du document

Ce document capture un etat pre-migration du moteur et n'est plus une source de verite fiable pour le backlog.

Il contient notamment des hypotheses qui ne sont plus vraies dans le repo courant : `basicEffect.fx` comme shader lit principal, absence de `LightComponent`, absence de pipeline `MaterialAsset` -> `CompiledMaterial`, et draw path encore hardcode autour du fallback legacy.

Backlog et references fiables a utiliser a la place :

- `ai-agent/material-system-modernization-tasks.md` pour la migration materials cloturee
- `ai-agent/material-shader-deep-audit-tasks.md` pour l'audit et les refactors deja realises
- `ai-agent/rendering/materials-workflow.md` pour l'etat final authoring/runtime
- `ai-agent/shader-source-hot-reload-tasks.md` pour le principal manque fonctionnel restant cote shaders

Consequence : ne pas reutiliser cette liste comme plan de travail actif.

## Analyse de l'existant

### Problème actuel
- La classe `Material` existe (`Framework/Materials/Material.cs`) mais n'est **pas connectée au pipeline de rendu**.
- `StaticModelMesh` possède un `MaterialIndex` (int) qui n'est **jamais utilisé** pour chercher un matériau.
- Les meshes stockent directement un `TextureAssetId` et les renderers appliquent les paramètres shader en dur.
- `StaticMeshRendererComponent` utilise un **unique Effect** (`basicEffect`) partagé pour tous les meshes avec des valeurs de lighting **hardcodées** (3 directional lights, couleurs, specular).
- `SkinnedMeshRendererComponent` fait de même avec `skinEffect` et des valeurs hardcodées.
- Il n'y a **aucun système de lumières** (pas de `LightComponent`, pas de `DirectionalLightComponent`).
- Les render states (BlendState, DepthStencilState, RasterizerState) sont fixés **par renderer**, pas par matériau.
- `Material` ne contient que des slots de textures (8 textures par Guid) — **aucune propriété scalaire** (couleur diffuse, specular power, emissive, alpha, roughness…).
- `Material` **n'est pas enregistré** dans `AssetLoaderRegistry` → pas chargeable comme un asset.
- Il n'y a **aucun tri** des draw calls (pas de batching, pas de queue opaque/transparente, pas de tri par shader/material).

### Ce qui existe déjà

| Classe | Rôle |
|---|---|
| `Material` | 8 slots de textures (Guid → Texture2D), sérialisable JSON, **non connecté au pipeline** |
| `StaticModelMesh` | GPU mesh (VB/IB) + `TextureAssetId` + `MaterialIndex` (inutilisé) |
| `StaticModel` | Asset composé : hiérarchie de `StaticModelNode` + `List<StaticModelMesh>` |
| `StaticModelSubMeshComponent` | SceneComponent qui référence un `StaticModelMesh`, appelle `StaticMeshRendererComponent.AddMesh()` |
| `StaticMeshRendererComponent` | Collecte `MeshInfo` → `Flush()` → dessine tout avec `basicEffect` et params hardcodés |
| `SkinnedMeshRendererComponent` | Idem pour les modèles skinned avec `skinEffect` |
| `SpriteRendererComponent` | Rendu 2D/3D de sprites avec `spritebatch` shader |
| `RenderPipeline` | Orchestrateur top-level : itère les `RenderView`, délègue à `IViewRenderPipeline` |
| `DefaultViewPipeline` | `World.Draw()` → `Flush` renderers → UI |
| `RenderFrame` | Struct readonly : View/Projection/ViewProjection + CameraPosition + ViewportRect |
| `IViewFlushableRenderer` | Interface : `Flush(in RenderFrame frame)` |
| `GraphicsStateSnapshot` / `GraphicsStateGuard` | Capture/restauration de l'état GPU |
| `AssetContentManager` | Chargement d'assets par type via `IAssetLoader` |
| `AssetLoaderRegistry` | Enregistrement statique des loaders par type |
| `AssetCatalog` | Registre global Guid → AssetInfo |
| `EffectLoader` | Charge les `.mgfxc` via `new Effect(device, bytes)` |
| `basicEffect.fx` | 16 techniques (Basic/VertexLighting/PixelLighting ± Texture ± VertexColor) |
| `skinEffect.fx` | Technique `RiggedModelDraw` + debug |
| `Lighting.fxh` | `ComputeLights()` pour 3 directional lights (diffuse + specular) |
| `Structures.fxh` / `Common.fxh` / `Macros.fxh` | Headers shader partagés |
| `GeometricPrimitive` | Primitives 3D (Box, Sphere, Cylinder…) avec `VertexPositionNormalTexture` |

### Conventions pattern du moteur
- Sérialisation : `ISerializable` avec `Load(JObject)` / `Save(JObject)`
- Assets identifiés par `Guid`, enregistrés dans `AssetCatalog`
- Les loaders implémentent `IAssetLoader` → enregistrés dans `AssetLoaderRegistry`
- Blocs `#if EDITOR` pour la sauvegarde et les événements éditeur
- Pattern deferred rendering : components appellent `renderer.AddXxx()` pendant `Draw()`, puis `RenderPipeline` appelle `renderer.Flush(frame)` une fois par vue

---

## Architecture cible

```
StaticModelSubMeshComponent.Draw()
    │
    ▼
StaticMeshRendererComponent.AddMesh(MeshInfo)
    │  MeshInfo contient maintenant : StaticModelMesh + WorldMatrix + Material
    │
    ▼
StaticMeshRendererComponent.Flush(RenderFrame)
    │
    ├── Construire List<RenderItem> depuis les MeshInfo
    ├── Générer SortKey par RenderItem (queue + shader hash + material hash + distance)
    ├── Trier : Opaque (shader→material→mesh) | Transparent (distance back-to-front)
    │
    ├── foreach RenderItem trié :
    │     ├── RenderStateCache.Apply(material.BlendState, material.DepthStencilState, material.RasterizerState)
    │     ├── ShaderBindCache.BindGlobals(frame)   // View/Proj si shader différent
    │     ├── material.Bind(RenderContext)          // WVP, textures, params scalaires
    │     └── mesh.Draw(GraphicsDevice)
    │
    └── Stats: drawCalls, effectBinds, textureBinds, stateChanges
```

### Hiérarchie des classes Material

```
MaterialBase (abstract)
 ├── BlendState, DepthStencilState, RasterizerState, SamplerState
 ├── IsTransparent, RenderQueue
 ├── ShaderAssetId (Guid → Effect)
 ├── abstract void Bind(RenderContext, Matrix world)
 │
 ├── UnlitTextureMaterial
 │    ├── Texture2D? Albedo, AlbedoAssetId
 │    ├── Color Tint
 │    └── float Alpha
 │
 ├── LitDiffuseMaterial
 │    ├── Texture2D? Albedo, AlbedoAssetId
 │    ├── Color DiffuseColor, EmissiveColor, SpecularColor
 │    ├── float SpecularPower
 │    └── (utilise les lumières du RenderContext)
 │
 └── (futurs : PBRMaterial, SkinMaterial…)
```

### Shader Wrapper

```
ShaderWrapper
 ├── Effect Effect
 ├── Dictionary<string, EffectParameter> _paramCache   // lookup 1 fois
 ├── void SetParameter(string name, T value)            // générique
 ├── void TrySetParameter(string name, T value)         // silencieux si absent
 ├── void SelectTechnique(string techniqueName)
 └── void ApplyCurrentTechnique()
```

### RenderContext

```
RenderContext (struct)
 ├── GraphicsDevice Device
 ├── GameTime GameTime
 ├── RenderFrame Frame          // View, Projection, CameraPosition
 ├── LightingContext Lighting   // DirectionalLight[], AmbientColor
 └── RenderStats Stats          // compteurs draw calls, binds
```

---

## Phase 0 — Fondations & conventions

### Tâche 0.1 — Créer la structure de dossiers
- [x] Vérifier que `CasaEngine/Framework/Materials/` existe (il existe déjà)
- [ ] Créer `CasaEngine/Framework/Rendering/Shaders/` pour les wrappers shader
- [ ] Créer `CasaEngine/Framework/Rendering/Draw/` pour le système de tri/batching

**Fichiers :** Créer un fichier vide `_namespace.md` ou `.gitkeep` dans chaque nouveau dossier.

### Tâche 0.2 — Constantes de noms de paramètres shader
Créer `CasaEngine/Framework/Rendering/Shaders/ShaderParameterNames.cs`

```csharp
namespace CasaEngine.Framework.Rendering.Shaders;

public static class ShaderParameterNames
{
    // Transforms
    public const string World = "World";
    public const string WorldInverseTranspose = "WorldInverseTranspose";
    public const string WorldViewProj = "WorldViewProj";
    public const string View = "View";
    public const string Projection = "Projection";
    public const string ViewProjection = "ViewProjection";
    public const string EyePosition = "EyePosition";

    // Material
    public const string DiffuseColor = "DiffuseColor";
    public const string EmissiveColor = "EmissiveColor";
    public const string SpecularColor = "SpecularColor";
    public const string SpecularPower = "SpecularPower";
    public const string AlbedoTexture = "Texture";
    public const string TintColor = "TintColor";
    public const string Alpha = "Alpha";
    public const string OpacityTexture = "OpacityTexture";
    public const string NormalTexture = "NormalTexture";

    // Lighting
    public const string AmbientColor = "AmbientColor";
    public const string DirLight0Direction = "DirLight0Direction";
    public const string DirLight0DiffuseColor = "DirLight0DiffuseColor";
    public const string DirLight0SpecularColor = "DirLight0SpecularColor";
    public const string DirLight1Direction = "DirLight1Direction";
    public const string DirLight1DiffuseColor = "DirLight1DiffuseColor";
    public const string DirLight1SpecularColor = "DirLight1SpecularColor";
    public const string DirLight2Direction = "DirLight2Direction";
    public const string DirLight2DiffuseColor = "DirLight2DiffuseColor";
    public const string DirLight2SpecularColor = "DirLight2SpecularColor";

    // Skinning
    public const string Bones = "Bones";
}
```

### Tâche 0.3 — Créer RenderContext
Créer `CasaEngine/Framework/Rendering/RenderContext.cs`

```csharp
namespace CasaEngine.Framework.Rendering;

public struct RenderContext
{
    public GraphicsDevice Device;
    public GameTime GameTime;
    public RenderFrame Frame;               // View, Projection, CameraPosition, Viewport
    public LightingContext Lighting;        // voir Phase 5
    public RenderStats Stats;               // compteurs
}
```

### Tâche 0.4 — Créer RenderStats
Créer `CasaEngine/Framework/Rendering/RenderStats.cs`

Un compteur simple, reset chaque frame :

```csharp
namespace CasaEngine.Framework.Rendering;

public class RenderStats
{
    public int DrawCalls { get; set; }
    public int EffectBinds { get; set; }
    public int TextureBinds { get; set; }
    public int StateChanges { get; set; }
    public int OpaqueItems { get; set; }
    public int TransparentItems { get; set; }

    public void Reset()
    {
        DrawCalls = 0;
        EffectBinds = 0;
        TextureBinds = 0;
        StateChanges = 0;
        OpaqueItems = 0;
        TransparentItems = 0;
    }

    public override string ToString() =>
        $"Draws:{DrawCalls} FX:{EffectBinds} Tex:{TextureBinds} States:{StateChanges} Opaque:{OpaqueItems} Trans:{TransparentItems}";
}
```

**Validation Phase 0 :** Le projet compile. Les nouveaux fichiers sont référencés. `RenderContext` est utilisable dans les tests.

---

## Phase 1 — Material minimal "UnlitTexture"

### Tâche 1.1 — Créer MaterialBase
Créer `CasaEngine/Framework/Materials/MaterialBase.cs`

```csharp
namespace CasaEngine.Framework.Materials;

public enum RenderQueue
{
    Opaque = 2000,
    AlphaTest = 2500,
    Transparent = 3000,
    Overlay = 4000
}

public abstract class MaterialBase : ISerializable
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;

    // Render states (null = utiliser les defaults du renderer)
    public BlendState? BlendState { get; set; }
    public DepthStencilState? DepthStencilState { get; set; }
    public RasterizerState? RasterizerState { get; set; }
    public SamplerState? SamplerState { get; set; }

    // Tri
    public bool IsTransparent { get; set; }
    public RenderQueue Queue { get; set; } = RenderQueue.Opaque;

    // Shader
    public Guid ShaderAssetId { get; set; } = Guid.Empty;

    // Méthode principale : applique les states + set les params shader
    public abstract void Bind(ShaderWrapper shader, in RenderContext context, Matrix world);

    // Sérialisation
    public abstract void Load(JObject element);
#if EDITOR
    public abstract void Save(JObject jObject);
#endif
}
```

### Tâche 1.2 — Créer ShaderWrapper
Créer `CasaEngine/Framework/Rendering/Shaders/ShaderWrapper.cs`

```csharp
namespace CasaEngine.Framework.Rendering.Shaders;

public class ShaderWrapper
{
    private readonly Effect _effect;
    private readonly Dictionary<string, EffectParameter?> _paramCache = new();

    public Effect Effect => _effect;

    public ShaderWrapper(Effect effect)
    {
        _effect = effect;
    }

    public EffectParameter? GetParameter(string name)
    {
        if (!_paramCache.TryGetValue(name, out var param))
        {
            param = _effect.Parameters[name]; // null si absent
            _paramCache[name] = param;
        }
        return param;
    }

    public void SetParameter(string name, float value) => GetParameter(name)?.SetValue(value);
    public void SetParameter(string name, Vector2 value) => GetParameter(name)?.SetValue(value);
    public void SetParameter(string name, Vector3 value) => GetParameter(name)?.SetValue(value);
    public void SetParameter(string name, Vector4 value) => GetParameter(name)?.SetValue(value);
    public void SetParameter(string name, Color value) => GetParameter(name)?.SetValue(value.ToVector4());
    public void SetParameter(string name, Matrix value) => GetParameter(name)?.SetValue(value);
    public void SetParameter(string name, Texture2D? value) => GetParameter(name)?.SetValue(value);
    public void SetParameter(string name, Matrix[] value) => GetParameter(name)?.SetValue(value);

    public bool HasParameter(string name) => GetParameter(name) != null;

    public void SelectTechnique(string techniqueName)
    {
        var technique = _effect.Techniques[techniqueName];
        if (technique != null)
        {
            _effect.CurrentTechnique = technique;
        }
        else
        {
            // Fallback : première technique
            Log.Warning($"Technique '{techniqueName}' not found, using default");
        }
    }

    public void ApplyPass(int passIndex = 0)
    {
        _effect.CurrentTechnique.Passes[passIndex].Apply();
    }
}
```

### Tâche 1.3 — Créer UnlitTextureMaterial
Créer `CasaEngine/Framework/Materials/UnlitTextureMaterial.cs`

```csharp
namespace CasaEngine.Framework.Materials;

public class UnlitTextureMaterial : MaterialBase
{
    public Texture2D? Albedo { get; set; }
    public Guid AlbedoAssetId { get; set; } = Guid.Empty;
    public Color Tint { get; set; } = Color.White;
    public float Alpha { get; set; } = 1.0f;

    public override void Bind(ShaderWrapper shader, in RenderContext context, Matrix world)
    {
        // WVP
        var worldViewProj = world * context.Frame.ViewProjectionMatrix;
        shader.SetParameter(ShaderParameterNames.WorldViewProj, worldViewProj);
        shader.SetParameter(ShaderParameterNames.World, world);

        // Material params
        shader.SetParameter(ShaderParameterNames.AlbedoTexture, Albedo);
        shader.SetParameter(ShaderParameterNames.TintColor, Tint);
        shader.SetParameter(ShaderParameterNames.Alpha, Alpha);
    }

    // Sérialisation Load/Save...
}
```

### Tâche 1.4 — Créer/adapter un shader .fx "UnlitTexture"
Créer `CasaEngine/Content/Shaders/UnlitTexture.fx`

Shader minimal avec :
- VS : transforme position par WorldViewProj, passe UV
- PS : sample texture × TintColor × Alpha
- Technique `Unlit_Textured` et `Unlit_Colored` (sans texture)

Compiler le shader en `.mgfxc` via le pipeline existant (`ShaderCompiler` ou MGFXC).

### Tâche 1.5 — Connecter Material à StaticModelMesh
Modifier `StaticModelMesh` :
- Ajouter `public MaterialBase? Material { get; set; }` (runtime, pas sérialisé)
- Ajouter `public Guid MaterialAssetId { get; set; }` (sérialisé, pour résoudre le matériau au chargement)
- Conserver `TextureAssetId` comme fallback si `MaterialAssetId == Guid.Empty`

### Tâche 1.6 — Modifier MeshInfo pour transporter le Material
Modifier la struct `MeshInfo` dans `StaticMeshRendererComponent` :
- Ajouter un champ `MaterialBase? Material`
- `StaticModelSubMeshComponent.Draw()` passe le material du mesh

### Tâche 1.7 — Modifier StaticMeshRendererComponent.Flush()
Premier niveau d'intégration :
- Si `meshInfo.Material != null` :
  - Appliquer les render states du material (BlendState, DepthStencilState, RasterizerState)
  - Créer ou récupérer un `ShaderWrapper` pour l'Effect du material
  - Appeler `material.Bind(shader, context, world)`
  - `shader.ApplyPass()`
  - Dessiner le mesh
- Sinon : conserver le comportement actuel (basicEffect hardcodé) → **rétrocompatibilité**

**Validation Phase 1 :**
- Créer un test unitaire ou une scène de test avec 1 cube + `UnlitTextureMaterial` (texture + tint rouge).
- Le cube s'affiche correctement avec la couleur tintée.
- Les meshes sans material continuent de fonctionner avec le rendu hardcodé.

---

## Phase 2 — SubMeshes & multi-matériaux

### Tâche 2.1 — Créer SubMesh
Créer `CasaEngine/Framework/Graphics/SubMesh.cs`

```csharp
namespace CasaEngine.Framework.Graphics;

public class SubMesh
{
    public int IndexStart { get; set; }
    public int PrimitiveCount { get; set; }
    public int VertexOffset { get; set; }
    public MaterialBase? Material { get; set; }
    public Guid MaterialAssetId { get; set; } = Guid.Empty;
}
```

### Tâche 2.2 — Modifier StaticModelMesh pour supporter les SubMeshes
- Ajouter `public List<SubMesh> SubMeshes { get; }` à `StaticModelMesh`
- Si `SubMeshes` est vide, le mesh entier est un seul submesh (comportement actuel)
- Sérialiser/désérialiser la liste de submeshes

### Tâche 2.3 — Modifier StaticModelImporter pour créer des SubMeshes
Lors de l'import Assimp, si un mesh source a plusieurs matériaux, créer un `SubMesh` par groupe de faces partageant le même matériau.

### Tâche 2.4 — Modifier le flush pour itérer les SubMeshes
Dans `StaticMeshRendererComponent.Flush()` :
- Pour chaque `MeshInfo`, itérer `mesh.SubMeshes` (ou le mesh entier si pas de submesh)
- Chaque submesh a son propre material → bind + draw séparés
- Créer un `RenderItem` par (mesh, submesh, material, world)

### Tâche 2.5 — Créer RenderItem
Créer `CasaEngine/Framework/Rendering/Draw/RenderItem.cs`

```csharp
namespace CasaEngine.Framework.Rendering.Draw;

public struct RenderItem
{
    public StaticModelMesh Mesh;
    public SubMesh? SubMesh;            // null = mesh entier
    public MaterialBase Material;
    public Matrix World;
    public Matrix WorldInverseTranspose;
    public ulong SortKey;
    public float DistanceToCamera;      // pour tri transparent
}
```

**Validation Phase 2 :**
- Importer un modèle avec 2+ matériaux (ex : un personnage avec vêtements + peau).
- Chaque submesh affiche son propre material.

---

## Phase 3 — Material comme Asset à part entière

### Tâche 3.1 — Refactorer Material existant
Renommer/adapter `CasaEngine/Framework/Materials/Material.cs` :
- Faire hériter `Material` de `MaterialBase`
- Ou : créer une nouvelle classe `PBRMaterial : MaterialBase` et marquer l'ancien `Material` comme `[Obsolete]`
- Migrer les 8 slots de textures (BaseColor, Normal, Specular, etc.) vers la nouvelle hiérarchie

### Tâche 3.2 — Créer MaterialLoader
Créer `CasaEngine/Framework/Assets/Loaders/MaterialLoader.cs`

```csharp
public class MaterialLoader : IAssetLoader
{
    public bool IsFileSupported(string fileName) =>
        Path.GetExtension(fileName) == Constants.FileNameExtensions.Material;

    public object LoadAsset(string fileName, AssetContentManager assetContentManager)
    {
        // Lire le JSON
        // Déterminer le type de material (via un champ "type" dans le JSON)
        // Instancier le bon MaterialBase
        // Résoudre les textures via assetContentManager.Load<Texture2D>(assetId)
        // Résoudre le shader via assetContentManager.Load<Effect>(shaderAssetId)
        // Retourner le material
    }
}
```

### Tâche 3.3 — Enregistrer Material dans AssetLoaderRegistry
Ajouter dans `AssetLoaderRegistry.Initialize()` :
```csharp
Register(typeof(MaterialBase), new MaterialLoader());
```

### Tâche 3.4 — Ajouter l'extension de fichier
Ajouter dans `Constants.FileNameExtensions` :
```csharp
public const string Material = ".material";
```

### Tâche 3.5 — Créer ShaderManager
Créer `CasaEngine/Framework/Rendering/Shaders/ShaderManager.cs`

Responsabilités :
- Charger et cacher les `Effect` par `Guid` (via `AssetContentManager`)
- Fournir des `ShaderWrapper` pré-cachés
- Méthode `ShaderWrapper GetShader(Guid shaderAssetId)`

```csharp
namespace CasaEngine.Framework.Rendering.Shaders;

public class ShaderManager
{
    private readonly AssetContentManager _assetContentManager;
    private readonly Dictionary<Guid, ShaderWrapper> _cache = new();

    public ShaderManager(AssetContentManager assetContentManager)
    {
        _assetContentManager = assetContentManager;
    }

    public ShaderWrapper GetShader(Guid shaderAssetId)
    {
        if (!_cache.TryGetValue(shaderAssetId, out var wrapper))
        {
            var effect = _assetContentManager.Load<Effect>(shaderAssetId);
            wrapper = new ShaderWrapper(effect);
            _cache[shaderAssetId] = wrapper;
        }
        return wrapper;
    }
}
```

### Tâche 3.6 — Sérialisation MaterialBase en JSON
Définir le format JSON pour un material :

```json
{
    "type": "UnlitTextureMaterial",
    "name": "MyMaterial",
    "shader_asset_id": "00000000-0000-0000-0000-000000000000",
    "blend_state": "Opaque",
    "depth_stencil_state": "Default",
    "rasterizer_state": "CullCounterClockwise",
    "sampler_state": "AnisotropicClamp",
    "is_transparent": false,
    "queue": "Opaque",
    "albedo_asset_id": "11111111-1111-1111-1111-111111111111",
    "tint_color": { "r": 255, "g": 255, "b": 255, "a": 255 },
    "alpha": 1.0
}
```

Implémenter `Load(JObject)` et `Save(JObject)` dans `MaterialBase` (champs communs) et dans chaque sous-classe (champs spécifiques).

### Tâche 3.7 — Résolution Material au chargement du StaticModel
Dans `StaticModel.Initialize()` ou `StaticModelLoader` :
- Pour chaque `StaticModelMesh` / `SubMesh` ayant un `MaterialAssetId != Guid.Empty` :
  - `material = assetContentManager.Load<MaterialBase>(materialAssetId);`
  - Assigner au mesh/submesh
- Si `MaterialAssetId == Guid.Empty` et `TextureAssetId != Guid.Empty` :
  - Créer un `UnlitTextureMaterial` à la volée avec la texture

**Validation Phase 3 :**
- Créer un fichier `.material` à la main.
- Le charger via `AssetContentManager`.
- L'assigner à un mesh et vérifier le rendu.
- La sauvegarde JSON fonctionne (#if EDITOR).

---

## Phase 4 — Tri, batching simple & queues de rendu

### Tâche 4.1 — Générer des SortKey
Créer `CasaEngine/Framework/Rendering/Draw/SortKeyGenerator.cs`

```csharp
namespace CasaEngine.Framework.Rendering.Draw;

public static class SortKeyGenerator
{
    // Layout 64 bits :
    // [63..60] queue (4 bits)
    // [59..44] shader hash (16 bits)
    // [43..28] material hash (16 bits)
    // [27..12] mesh hash (16 bits)
    // [11..0]  distance (12 bits, pour transparent)

    public static ulong Generate(RenderQueue queue, int shaderHash, int materialHash, int meshHash, float distance = 0f)
    {
        ulong key = 0;
        key |= ((ulong)queue & 0xF) << 60;
        key |= ((ulong)(shaderHash & 0xFFFF)) << 44;
        key |= ((ulong)(materialHash & 0xFFFF)) << 28;
        key |= ((ulong)(meshHash & 0xFFFF)) << 12;
        if (queue >= RenderQueue.Transparent)
        {
            // Distance inversée pour tri back-to-front
            var distBits = (uint)Math.Clamp((int)(distance * 10f), 0, 0xFFF);
            key |= (ulong)(0xFFF - distBits); // plus loin = plus petit → trié en premier
        }
        return key;
    }
}
```

### Tâche 4.2 — Implémenter le tri dans Flush()
Modifier `StaticMeshRendererComponent.Flush()` :
1. Construire `List<RenderItem>` depuis les `MeshInfo`
2. Calculer `SortKey` pour chaque `RenderItem`
3. Trier par `SortKey` croissant (opaque) / décroissant pour la partie transparente
4. Itérer les items triés et dessiner

### Tâche 4.3 — Créer RenderStateCache
Créer `CasaEngine/Framework/Rendering/Draw/RenderStateCache.cs`

```csharp
namespace CasaEngine.Framework.Rendering.Draw;

public class RenderStateCache
{
    private BlendState? _currentBlend;
    private DepthStencilState? _currentDepthStencil;
    private RasterizerState? _currentRasterizer;
    private SamplerState? _currentSampler;

    public bool Apply(GraphicsDevice device, MaterialBase material, RenderStats? stats = null)
    {
        bool changed = false;

        var blend = material.BlendState ?? BlendState.Opaque;
        if (blend != _currentBlend)
        {
            device.BlendState = blend;
            _currentBlend = blend;
            changed = true;
        }

        var depth = material.DepthStencilState ?? DepthStencilState.Default;
        if (depth != _currentDepthStencil)
        {
            device.DepthStencilState = depth;
            _currentDepthStencil = depth;
            changed = true;
        }

        var rasterizer = material.RasterizerState ?? RasterizerState.CullCounterClockwise;
        if (rasterizer != _currentRasterizer)
        {
            device.RasterizerState = rasterizer;
            _currentRasterizer = rasterizer;
            changed = true;
        }

        var sampler = material.SamplerState ?? SamplerState.AnisotropicClamp;
        if (sampler != _currentSampler)
        {
            device.SamplerStates[0] = sampler;
            _currentSampler = sampler;
            changed = true;
        }

        if (changed) stats?.StateChanges++;
        return changed;
    }

    public void Reset()
    {
        _currentBlend = null;
        _currentDepthStencil = null;
        _currentRasterizer = null;
        _currentSampler = null;
    }
}
```

### Tâche 4.4 — Créer ShaderBindCache
Créer `CasaEngine/Framework/Rendering/Shaders/ShaderBindCache.cs`

Responsabilités :
- Tracker le dernier `ShaderWrapper` utilisé
- Ne re-set les params globaux (View, Projection, EyePosition, lumières) que si le shader a changé
- Compter les `EffectBinds` dans `RenderStats`

```csharp
namespace CasaEngine.Framework.Rendering.Shaders;

public class ShaderBindCache
{
    private ShaderWrapper? _lastShader;

    public bool BindGlobals(ShaderWrapper shader, in RenderContext context)
    {
        if (shader == _lastShader) return false;

        _lastShader = shader;
        shader.SetParameter(ShaderParameterNames.EyePosition, context.Frame.CameraPosition);
        // Les lumières seront ajoutées en Phase 5
        context.Stats?.EffectBinds++;
        return true;
    }

    public void Reset() => _lastShader = null;
}
```

### Tâche 4.5 — Intégrer RenderStats dans le debug overlay
- Ajouter `RenderStats` dans `RenderPipeline` ou `RenderContext`
- Reset au début de chaque frame
- Afficher via le debug overlay existant (`EditorViewPipeline` ou un nouveau composant)

**Validation Phase 4 :**
- Scène avec 20 cubes identiques (même material) → vérifier que les state changes sont minimisés.
- Scène avec 2 materials différents → vérifier le tri (les objets du même material sont groupés).
- Scène avec objets opaques + transparents → les transparents sont dessinés après, en ordre back-to-front.
- Le debug overlay affiche les compteurs.

---

## Phase 5 — Material "LitDiffuse" & système de lumières

### Tâche 5.1 — Créer DirectionalLight
Créer `CasaEngine/Framework/Rendering/DirectionalLight.cs`

```csharp
namespace CasaEngine.Framework.Rendering;

public struct DirectionalLight
{
    public Vector3 Direction;
    public Vector3 DiffuseColor;
    public Vector3 SpecularColor;
    public float Intensity;

    public DirectionalLight(Vector3 direction, Vector3 diffuseColor, Vector3 specularColor, float intensity = 1.0f)
    {
        Direction = Vector3.Normalize(direction);
        DiffuseColor = diffuseColor;
        SpecularColor = specularColor;
        Intensity = intensity;
    }
}
```

### Tâche 5.2 — Créer LightingContext
Créer `CasaEngine/Framework/Rendering/LightingContext.cs`

```csharp
namespace CasaEngine.Framework.Rendering;

public class LightingContext
{
    public const int MaxDirectionalLights = 3;

    public DirectionalLight[] DirectionalLights { get; } = new DirectionalLight[MaxDirectionalLights];
    public int ActiveDirectionalLightCount { get; set; }
    public Vector3 AmbientColor { get; set; } = new Vector3(0.2f, 0.2f, 0.2f);

    // Méthode utilitaire pour bind les lumières sur un ShaderWrapper
    public void Bind(ShaderWrapper shader)
    {
        for (int i = 0; i < ActiveDirectionalLightCount && i < MaxDirectionalLights; i++)
        {
            var prefix = $"DirLight{i}";
            shader.SetParameter($"{prefix}Direction", DirectionalLights[i].Direction);
            shader.SetParameter($"{prefix}DiffuseColor", DirectionalLights[i].DiffuseColor * DirectionalLights[i].Intensity);
            shader.SetParameter($"{prefix}SpecularColor", DirectionalLights[i].SpecularColor);
        }
    }
}
```

### Tâche 5.3 — Connecter LightingContext à RenderContext
- Ajouter `LightingContext Lighting` dans `RenderContext`
- Initialiser avec les 3 directional lights actuellement hardcodées dans `StaticMeshRendererComponent`
- Rendre configurable (via la scène ou un composant `WorldLightingComponent`)

### Tâche 5.4 — Créer LitDiffuseMaterial
Créer `CasaEngine/Framework/Materials/LitDiffuseMaterial.cs`

```csharp
namespace CasaEngine.Framework.Materials;

public class LitDiffuseMaterial : MaterialBase
{
    public Texture2D? Albedo { get; set; }
    public Guid AlbedoAssetId { get; set; } = Guid.Empty;
    public Color DiffuseColor { get; set; } = Color.White;
    public Vector3 EmissiveColor { get; set; } = Vector3.Zero;
    public Vector3 SpecularColor { get; set; } = new Vector3(0.5f);
    public float SpecularPower { get; set; } = 16.0f;

    public override void Bind(ShaderWrapper shader, in RenderContext context, Matrix world)
    {
        // Transforms
        var worldViewProj = world * context.Frame.ViewProjectionMatrix;
        shader.SetParameter(ShaderParameterNames.WorldViewProj, worldViewProj);
        shader.SetParameter(ShaderParameterNames.World, world);
        var wit = Matrix.Transpose(Matrix.Invert(world));
        shader.SetParameter(ShaderParameterNames.WorldInverseTranspose, wit);
        shader.SetParameter(ShaderParameterNames.EyePosition, context.Frame.CameraPosition);

        // Material params
        shader.SetParameter(ShaderParameterNames.DiffuseColor, DiffuseColor);
        shader.SetParameter(ShaderParameterNames.EmissiveColor, EmissiveColor);
        shader.SetParameter(ShaderParameterNames.SpecularColor, SpecularColor);
        shader.SetParameter(ShaderParameterNames.SpecularPower, SpecularPower);
        shader.SetParameter(ShaderParameterNames.AlbedoTexture, Albedo);

        // Lighting
        context.Lighting?.Bind(shader);
    }

    // Sérialisation Load/Save...
}
```

### Tâche 5.5 — Migrer les valeurs hardcodées de StaticMeshRendererComponent
- Supprimer les `_effect.Parameters["DirLight0Direction"].SetValue(...)` hardcodés
- Remplacer par l'utilisation de `LightingContext` via `RenderContext`
- Le chemin legacy (sans material) utilise un `LitDiffuseMaterial` par défaut avec les anciennes valeurs

### Tâche 5.6 — Utiliser basicEffect.fx comme shader pour LitDiffuseMaterial
- Le shader `basicEffect.fx` existant contient déjà toutes les techniques nécessaires
- `LitDiffuseMaterial` sélectionne la technique `BasicEffect_PixelLighting_Texture` ou `BasicEffect_PixelLighting` selon la présence d'une texture Albedo

**Validation Phase 5 :**
- Scène avec des cubes éclairés par 1-3 directional lights.
- Modifier les couleurs/directions des lumières dynamiquement.
- Le rendu est identique au rendu hardcodé actuel (non-régression).

---

## Phase 6 — Paramètres génériques & overrides par instance

### Tâche 6.1 — Créer MaterialPropertyBlock
Créer `CasaEngine/Framework/Materials/MaterialPropertyBlock.cs`

Override de paramètres par instance sans dupliquer le material :

```csharp
namespace CasaEngine.Framework.Materials;

public class MaterialPropertyBlock
{
    private readonly Dictionary<string, object> _properties = new();

    public void SetFloat(string name, float value) => _properties[name] = value;
    public void SetVector2(string name, Vector2 value) => _properties[name] = value;
    public void SetVector3(string name, Vector3 value) => _properties[name] = value;
    public void SetVector4(string name, Vector4 value) => _properties[name] = value;
    public void SetColor(string name, Color value) => _properties[name] = value;
    public void SetTexture(string name, Texture2D value) => _properties[name] = value;
    public void SetMatrix(string name, Matrix value) => _properties[name] = value;

    public bool TryGetFloat(string name, out float value) { ... }
    // ... autres TryGet

    // Applique les overrides sur un ShaderWrapper (après le Bind du material)
    public void Apply(ShaderWrapper shader) { ... }

    public void Clear() => _properties.Clear();
    public bool IsEmpty => _properties.Count == 0;
}
```

### Tâche 6.2 — Intégrer MaterialPropertyBlock dans le pipeline
- Ajouter `MaterialPropertyBlock? PropertyOverrides` dans `MeshInfo` / `RenderItem`
- Dans le renderer, après `material.Bind()`, appeler `propertyOverrides?.Apply(shader)`
- Cas d'usage : couleur par entité, highlight de sélection, etc.

### Tâche 6.3 — Support dans SceneComponent
- Ajouter `MaterialPropertyBlock? MaterialOverrides` dans `StaticModelSubMeshComponent`
- Transmettre au renderer via `AddMesh()`

**Validation Phase 6 :**
- 10 cubes avec le même material, mais chacun avec une couleur Tint différente via `MaterialPropertyBlock`.
- Vérifier qu'un seul `MaterialBase` est instancié (pas de copies).

---

## Phase 7 — Shader Variants (niveau 1)

### Tâche 7.1 — Définir ShaderFeature flags
Créer `CasaEngine/Framework/Rendering/Shaders/ShaderFeature.cs`

```csharp
namespace CasaEngine.Framework.Rendering.Shaders;

[Flags]
public enum ShaderFeature : uint
{
    None            = 0,
    AlbedoTexture   = 1 << 0,
    VertexColor     = 1 << 1,
    AlphaTest       = 1 << 2,
    Skinned         = 1 << 3,
    Instanced       = 1 << 4,
    NormalMap       = 1 << 5,
    Emissive        = 1 << 6,
}
```

### Tâche 7.2 — Créer ShaderVariantKey
Créer `CasaEngine/Framework/Rendering/Shaders/ShaderVariantKey.cs`

```csharp
namespace CasaEngine.Framework.Rendering.Shaders;

public readonly struct ShaderVariantKey : IEquatable<ShaderVariantKey>
{
    public Guid ShaderBaseId { get; }
    public ShaderFeature Features { get; }

    public ShaderVariantKey(Guid shaderBaseId, ShaderFeature features)
    {
        ShaderBaseId = shaderBaseId;
        Features = features;
    }

    public bool Equals(ShaderVariantKey other) =>
        ShaderBaseId == other.ShaderBaseId && Features == other.Features;
    public override int GetHashCode() => HashCode.Combine(ShaderBaseId, Features);
    public override bool Equals(object? obj) => obj is ShaderVariantKey k && Equals(k);
}
```

### Tâche 7.3 — Créer ShaderVariantLibrary
Créer `CasaEngine/Framework/Rendering/Shaders/ShaderVariantLibrary.cs`

```csharp
namespace CasaEngine.Framework.Rendering.Shaders;

public class ShaderVariantLibrary
{
    private readonly Dictionary<ShaderVariantKey, ShaderWrapper> _variants = new();
    private readonly ShaderManager _shaderManager;

    public ShaderVariantLibrary(ShaderManager shaderManager) { ... }

    public ShaderWrapper Get(ShaderVariantKey key) { ... }

    // Stratégie simple : 1 variant = 1 fichier .fx compilé
    // Ex : "LitDiffuse" + ALPHA_TEST → charge "LitDiffuse_AlphaTest.mgfxc"
    // Ou utilise des techniques différentes dans le même .fx
}
```

### Tâche 7.4 — Détermination automatique des features
Dans `MaterialManager` ou au moment du bind :
- Si `material.Albedo != null` → `ShaderFeature.AlbedoTexture`
- Si `material.AlphaTestEnabled` → `ShaderFeature.AlphaTest`
- Si mesh a des vertex colors → `ShaderFeature.VertexColor`
- Si mesh est skinned → `ShaderFeature.Skinned`

### Tâche 7.5 — Intégrer dans le Renderer
Dans `StaticMeshRendererComponent.Flush()` :
- Déterminer les features pour le material/mesh
- Construire `ShaderVariantKey`
- Récupérer le bon `ShaderWrapper` via `ShaderVariantLibrary.Get(key)`
- Bind et draw

**Validation Phase 7 :**
- Un même material lit "LitDiffuse" sans texture → utilise la technique sans texture.
- Assigner une texture → bascule automatiquement sur la technique avec texture.
- Log si une variante est manquante → fallback.

---

## Phase 8 — Shader Variants (niveau 2 : techniques/passes)

### Tâche 8.1 — Conventions de techniques dans les .fx
Définir dans chaque .fx des techniques nommées selon les features :

```hlsl
technique Opaque { ... }
technique Opaque_Textured { ... }
technique AlphaTest { ... }
technique AlphaTest_Textured { ... }
technique Transparent { ... }
```

### Tâche 8.2 — Sélection automatique de technique
Dans `ShaderWrapper` ou `ShaderVariantLibrary` :
- Construire le nom de technique depuis les features + queue
- Appeler `shader.SelectTechnique(techniqueName)`
- Fallback si technique manquante : log warning + utiliser `Opaque`

### Tâche 8.3 — Adapter basicEffect.fx
Le shader existant a déjà 16 techniques. Mapper les noms existants aux noms du nouveau système :
- `BasicEffect_PixelLighting_Texture` → `Opaque_Textured` (alias ou renommage)
- `BasicEffect_PixelLighting` → `Opaque`
- Ou créer un mapping configurable dans `ShaderManager`

### Tâche 8.4 — Multi-pass support (préparation)
Dans `ShaderWrapper` :
- `int PassCount => _effect.CurrentTechnique.Passes.Count;`
- Boucle sur les passes dans le renderer si nécessaire

**Validation Phase 8 :**
- Basculer un material entre Opaque/Transparent → la bonne technique est sélectionnée.
- Si une technique est manquante → le log affiche un warning et le fallback fonctionne.

---

## Phase 9 — Instancing (optionnel)

### Tâche 9.1 — Créer InstanceData
Créer `CasaEngine/Framework/Rendering/Draw/InstanceData.cs`

```csharp
namespace CasaEngine.Framework.Rendering.Draw;

[StructLayout(LayoutKind.Sequential)]
public struct InstanceData : IVertexType
{
    public Matrix World;    // 64 bytes

    public static readonly VertexDeclaration VertexDeclaration = new VertexDeclaration(
        new VertexElement(0,  VertexElementFormat.Vector4, VertexElementUsage.BlendWeight, 0),
        new VertexElement(16, VertexElementFormat.Vector4, VertexElementUsage.BlendWeight, 1),
        new VertexElement(32, VertexElementFormat.Vector4, VertexElementUsage.BlendWeight, 2),
        new VertexElement(48, VertexElementFormat.Vector4, VertexElementUsage.BlendWeight, 3)
    );

    VertexDeclaration IVertexType.VertexDeclaration => VertexDeclaration;
}
```

### Tâche 9.2 — Créer InstanceBatcher
Créer `CasaEngine/Framework/Rendering/Draw/InstanceBatcher.cs`

Responsabilités :
- Grouper les `RenderItem` ayant le même mesh + même material variant
- Remplir un `VertexBuffer` d'instances (dynamic)
- Appeler `GraphicsDevice.DrawInstancedPrimitives()` pour le groupe

### Tâche 9.3 — Shader variant INSTANCED
Créer/adapter un .fx avec un vertex shader qui lit la matrice World depuis l'instance buffer :

```hlsl
float4x4 instanceWorld : BLENDWEIGHT;  // depuis le second vertex stream
float4x4 finalWorld = instanceWorld;
output.Position = mul(float4(input.Position, 1), mul(finalWorld, ViewProjection));
```

### Tâche 9.4 — Intégrer dans le Renderer
- Détecter les groupes instanciables (même mesh + même material + features contient `INSTANCED`)
- Si groupe > seuil (ex : 4 instances) → utiliser `InstanceBatcher` au lieu de draw individuels
- Sinon → draw individuel classique

**Validation Phase 9 :**
- 100 cubes identiques → vérifier que `DrawCalls` est réduit (1 draw call au lieu de 100).
- Comparer les performances avant/après instancing.

---

## Phase 10 — Pipeline avancé (préparation)

### Tâche 10.1 — Définir des RenderPass
Créer `CasaEngine/Framework/Rendering/Draw/RenderPass.cs`

```csharp
namespace CasaEngine.Framework.Rendering.Draw;

public enum RenderPassType
{
    DepthPrePass,
    OpaquePass,
    TransparentPass,
    OverlayPass,
    ShadowPass,
}

public abstract class RenderPass
{
    public RenderPassType Type { get; }
    public abstract void Execute(RenderContext context, List<RenderItem> items);
}
```

### Tâche 10.2 — Créer OpaquePass et TransparentPass
Extraire la logique de tri + dessin du renderer dans :
- `OpaquePass` : tri front-to-back (shader → material → mesh)
- `TransparentPass` : tri back-to-front (distance)

### Tâche 10.3 — Créer IRenderPipeline (3D)
Créer une interface pour le pipeline de rendu 3D (différent de `IViewRenderPipeline` qui gère les vues) :

```csharp
namespace CasaEngine.Framework.Rendering;

public interface IRenderPipeline3D
{
    void Initialize(GraphicsDevice device);
    void Render(RenderContext context, List<RenderItem> items);
}
```

### Tâche 10.4 — Implémenter ForwardRenderPipeline
```csharp
public class ForwardRenderPipeline : IRenderPipeline3D
{
    private readonly List<RenderPass> _passes = new();

    public ForwardRenderPipeline()
    {
        _passes.Add(new OpaquePass());
        _passes.Add(new TransparentPass());
    }

    public void Render(RenderContext context, List<RenderItem> items)
    {
        foreach (var pass in _passes)
        {
            pass.Execute(context, items);
        }
    }
}
```

### Tâche 10.5 — Pass tags côté material (placeholder)
Ajouter dans `MaterialBase` :
```csharp
public bool CastShadows { get; set; } = true;
public bool ReceiveShadows { get; set; } = true;
```

Ces flags ne sont pas encore utilisés mais préparent l'intégration future des shadow maps.

**Validation Phase 10 :**
- Le rendu passe par `ForwardRenderPipeline` avec `OpaquePass` + `TransparentPass`.
- Le résultat visuel est identique à la Phase 4.
- La structure est prête pour ajouter un `DepthPrePass` ou un `DeferredRenderPipeline`.

---

## Scène de test & validation continue

### Test à maintenir à chaque phase

| Test | Phase min | Description |
|---|---|---|
| 1 cube, 1 material Unlit | Phase 1 | Affiche un cube avec texture + tint |
| Cube sans material (legacy) | Phase 1 | Rétrocompatibilité avec le rendu hardcodé |
| 1 mesh, 2 submeshes, 2 materials | Phase 2 | Multi-matériaux sur un seul mesh |
| 20 cubes identiques | Phase 4 | Vérifier le batching (state changes minimisées) |
| Cubes opaques + transparents | Phase 4 | Tri par queue + distance |
| Cubes éclairés LitDiffuse | Phase 5 | Directional lights, specular |
| 10 cubes même material, couleurs différentes | Phase 6 | MaterialPropertyBlock overrides |
| Material avec/sans texture | Phase 7 | Shader variant automatique |
| 100 cubes instancés | Phase 9 | DrawCalls réduits |

### Debug overlay
Afficher en permanence :
- `Draw calls` : nombre de `DrawIndexedPrimitives` / `DrawInstancedPrimitives`
- `Opaque / Transparent` : nombre d'items dans chaque queue
- `Effect binds` : nombre de changements de shader
- `Texture binds` : nombre de changements de textures
- `State changes` : nombre de changements de render states (blend, depth, rasterizer)

---

## Résumé des fichiers à créer/modifier

### Nouveaux fichiers

| Fichier | Phase |
|---|---|
| `Framework/Rendering/Shaders/ShaderParameterNames.cs` | 0 |
| `Framework/Rendering/RenderContext.cs` | 0 |
| `Framework/Rendering/RenderStats.cs` | 0 |
| `Framework/Materials/MaterialBase.cs` | 1 |
| `Framework/Rendering/Shaders/ShaderWrapper.cs` | 1 |
| `Framework/Materials/UnlitTextureMaterial.cs` | 1 |
| `Content/Shaders/UnlitTexture.fx` | 1 |
| `Framework/Graphics/SubMesh.cs` | 2 |
| `Framework/Rendering/Draw/RenderItem.cs` | 2 |
| `Framework/Assets/Loaders/MaterialLoader.cs` | 3 |
| `Framework/Rendering/Shaders/ShaderManager.cs` | 3 |
| `Framework/Rendering/Draw/SortKeyGenerator.cs` | 4 |
| `Framework/Rendering/Draw/RenderStateCache.cs` | 4 |
| `Framework/Rendering/Shaders/ShaderBindCache.cs` | 4 |
| `Framework/Rendering/DirectionalLight.cs` | 5 |
| `Framework/Rendering/LightingContext.cs` | 5 |
| `Framework/Materials/LitDiffuseMaterial.cs` | 5 |
| `Framework/Materials/MaterialPropertyBlock.cs` | 6 |
| `Framework/Rendering/Shaders/ShaderFeature.cs` | 7 |
| `Framework/Rendering/Shaders/ShaderVariantKey.cs` | 7 |
| `Framework/Rendering/Shaders/ShaderVariantLibrary.cs` | 7 |
| `Framework/Rendering/Draw/InstanceData.cs` | 9 |
| `Framework/Rendering/Draw/InstanceBatcher.cs` | 9 |
| `Framework/Rendering/Draw/RenderPass.cs` | 10 |
| `Framework/Rendering/Draw/OpaquePass.cs` | 10 |
| `Framework/Rendering/Draw/TransparentPass.cs` | 10 |
| `Framework/Rendering/IRenderPipeline3D.cs` | 10 |
| `Framework/Rendering/ForwardRenderPipeline.cs` | 10 |

### Fichiers à modifier

| Fichier | Phase | Modification |
|---|---|---|
| `Framework/Materials/Material.cs` | 3 | Hériter de `MaterialBase` ou refactorer |
| `Framework/Graphics/StaticModelMesh.cs` | 1, 2 | Ajouter `MaterialBase`, `MaterialAssetId`, `List<SubMesh>` |
| `Framework/Game/Components/StaticMeshRendererComponent.cs` | 1, 2, 4, 5, 7, 9 | Intégrer materials, tri, batching, lighting, variants |
| `Framework/Game/Components/StaticModelSubMeshComponent.cs` | 1, 6 | Transmettre material + property block au renderer |
| `Framework/Assets/AssetLoaderRegistry.cs` | 3 | Enregistrer `MaterialLoader` |
| `Framework/Assets/Constants.cs` | 3 | Ajouter `.material` extension |
| `Framework/Rendering/RenderFrame.cs` | 0 | Vérifier que `CameraPosition` est accessible |
| `Framework/Graphics/StaticModel.cs` | 3 | Résolution des materials au chargement |
| `Framework/Assets/Loaders/StaticModelImporter.cs` | 2, 3 | Créer SubMeshes + MaterialAssetId |
| `Framework/Rendering/RenderPipeline.cs` | 4, 10 | Intégrer `RenderStats`, `IRenderPipeline3D` |
| `Framework/Game/CasaEngineGame.cs` | 3, 5 | Initialiser `ShaderManager`, `LightingContext` |
| `Content/Shaders/basicEffect.fx` | 8 | Ajouter aliases de techniques (optionnel) |

---

## Ordre d'exécution recommandé

```
Phase 0 (fondations)           ← ~2h
  ↓
Phase 1 (material minimal)     ← ~4h     ★ Premier résultat visible
  ↓
Phase 2 (submeshes)            ← ~3h
  ↓
Phase 3 (asset material)       ← ~4h     ★ Materials sérialisables
  ↓
Phase 4 (tri & batching)       ← ~4h     ★ Performances
  ↓
Phase 5 (lighting)             ← ~3h     ★ Éclairage dynamique
  ↓
Phase 6 (property overrides)   ← ~2h
  ↓
Phase 7 (shader variants L1)   ← ~3h
  ↓
Phase 8 (shader variants L2)   ← ~2h
  ↓
Phase 9 (instancing)           ← ~4h     ★ Performances avancées
  ↓
Phase 10 (pipeline structure)  ← ~3h     ★ Architecture future-proof
```

**Total estimé : ~34h de développement**

Chaque phase est autonome et produit un résultat testable. La rétrocompatibilité est maintenue à chaque étape (le rendu legacy fonctionne tant qu'un mesh n'a pas de material assigné).
