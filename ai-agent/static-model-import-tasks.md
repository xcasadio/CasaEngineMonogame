# Import 3D Static Model — Architecture & Task List

## Analyse de l'existant

### Problème actuel
- `StaticMesh` ne gère qu'**un seul mesh** (un seul VertexBuffer/IndexBuffer).
- `StaticMeshComponent` référence un seul `StaticMesh` + un seul `Material`.
- L'import 3D (`ContentBrowserControl.ImportAssetFile`) crée toujours un `RiggedModel` → `SkinnedMesh`, même pour un modèle sans squelette.
- Il n'existe pas de concept d'**asset de modèle statique** (équivalent d'un `.fbx` importé en tant qu'asset réutilisable composé de plusieurs meshes avec hiérarchie).

### Ce qui existe déjà
| Classe | Rôle |
|---|---|
| `ObjectBase` | Base de tous les objets sérialisables (Id, Name, FileName, Save/Load) |
| `AssetInfo` | Entrée catalogue (Id, Name, FileName) |
| `AssetCatalog` | Registre global des assets |
| `AssetContentManager` | Chargement d'assets par type (IAssetLoader) |
| `AssetSaver` | Sauvegarde JSON d'un ISerializable |
| `StaticMesh` | Un seul mesh (vertices + indices + texture) — hérite d'ObjectBase |
| `StaticMeshComponent` | Component ECS qui affiche un StaticMesh unique |
| `SceneComponent` | Base avec hiérarchie Parent/Children + Coordinates (position/rotation/scale) |
| `PrimitiveComponent` | SceneComponent avec représentation géométrique |
| `RiggedModel` | Modèle chargé via Assimp avec squelette, meshes, animations |
| `RiggedModelLoader` | Charge un fichier 3D (FBX, etc.) via Assimp → RiggedModel |
| `ModelLoader` | IAssetLoader qui utilise RiggedModelLoader |
| `ElementFactory` | Crée des objets par nom de type (reflection) + Load JSON |
| `Constants.FileNameExtensions` | Extensions d'assets (`.model`, `.texture`, etc.) |

---

## Architecture proposée

L'objectif est d'avoir un workflow d'import similaire aux moteurs modernes (Unreal, Unity, Godot) :

```
Fichier 3D (.fbx/.obj/.gltf)
        │
        ▼
   [Import dans l'éditeur]
        │
        ▼
  StaticModel (Asset)           ← Nouvel asset : contient la hiérarchie + tous les meshes
   ├── StaticModelNode "Root"
   │    ├── Coordinates (position, rotation, scale)
   │    ├── MeshIndex? (optionnel, ref vers un mesh)
   │    └── Children[]
   │         ├── StaticModelNode "Chassis"
   │         │    ├── MeshIndex = 0
   │         │    └── Children[]
   │         └── StaticModelNode "Wheel_FL"
   │              ├── MeshIndex = 1
   │              └── Children[]
   └── StaticModelMesh[] (tableau de meshes)
        ├── [0] vertices, indices, materialIndex
        └── [1] vertices, indices, materialIndex
```

### Nouvelles classes (Engine — `CasaEngine/`)

#### 1. `StaticModelMesh` (dans `Framework/Graphics/`)
Un sous-mesh du modèle. Remplace l'usage direct de `StaticMesh` pour les modèles importés.

```csharp
public class StaticModelMesh
{
    public string Name { get; set; }
    public VertexPositionNormalTexture[] Vertices { get; set; }
    public uint[] Indices { get; set; }
    public PrimitiveType PrimitiveType { get; set; } = PrimitiveType.TriangleList;
    
    // Données GPU (créées à l'initialisation)
    public VertexBuffer? VertexBuffer { get; private set; }
    public IndexBuffer? IndexBuffer { get; private set; }
    
    // Référence matériau
    public int MaterialIndex { get; set; } = -1;
    public Guid TextureAssetId { get; set; } = Guid.Empty;
    
    public void Initialize(GraphicsDevice device) { ... }
    public void Load(JObject element) { ... }
    public void Save(JObject jObject) { ... }  // #if EDITOR
}
```

#### 2. `StaticModelNode` (dans `Framework/Graphics/`)
Un nœud de la hiérarchie du modèle (transform + ref optionnelle vers un mesh).

```csharp
public class StaticModelNode : ISerializable
{
    public string Name { get; set; }
    public Vector3 Position { get; set; }
    public Quaternion Rotation { get; set; }
    public Vector3 Scale { get; set; } = Vector3.One;
    public int MeshIndex { get; set; } = -1;  // -1 = pas de mesh (nœud structurel)
    public List<StaticModelNode> Children { get; } = new();
    
    public Matrix LocalTransform => Matrix.CreateScale(Scale)
        * Matrix.CreateFromQuaternion(Rotation)
        * Matrix.CreateTranslation(Position);
    
    public void Load(JObject element) { ... }
    public void Save(JObject jObject) { ... }  // #if EDITOR
}
```

#### 3. `StaticModel` (dans `Framework/Graphics/`)
L'asset principal, hérite de `ObjectBase`. Contient la hiérarchie complète + tous les meshes.

```csharp
public class StaticModel : ObjectBase
{
    public StaticModelNode RootNode { get; set; }
    public List<StaticModelMesh> Meshes { get; } = new();
    
    public void Initialize(AssetContentManager assetContentManager) { ... }
    public override void Load(JObject element) { ... }
    public override void Save(JObject jObject) { ... }  // #if EDITOR
}
```

#### 4. `StaticModelLoader` (dans `Framework/Assets/Loaders/`)
`IAssetLoader` pour charger un `StaticModel` depuis un fichier JSON (asset sérialisé).

```csharp
public class StaticModelLoader : IAssetLoader
{
    public bool IsFileSupported(string fileName) => 
        Path.GetExtension(fileName) == Constants.FileNameExtensions.StaticModel;
    
    public object LoadAsset(string fileName, AssetContentManager assetContentManager) { ... }
}
```

#### 5. `StaticModelImporter` (dans `Framework/Assets/Loaders/`)
Utilise Assimp pour convertir un fichier 3D en `StaticModel`. Utilisé **uniquement à l'import** dans l'éditeur.

```csharp
#if EDITOR
public class StaticModelImporter
{
    public StaticModel Import(string filePath, AssetContentManager assetContentManager) { ... }
    public bool IsFileSupported(string fileName) { ... }
}
#endif
```

### Modifications de classes existantes

#### 6. Modifier `StaticMeshComponent`
Le component doit référencer un `StaticModel` par son `AssetId` et reconstruire la hiérarchie de `SceneComponent` au moment de l'initialisation.

```csharp
[DisplayName("Static Mesh")]
public class StaticMeshComponent : PrimitiveComponent
{
    // Référence asset
    public Guid StaticModelAssetId { get; set; } = Guid.Empty;
    public StaticModel? StaticModel { get; set; }
    
    // Compatibilité : un mesh unique inline (primitives, etc.)
    public StaticMesh? Mesh { get; set; }
    public Material? Material { get; set; }
    
    public override void InitializeWithWorld(World.World world)
    {
        // Charger le StaticModel si un AssetId est défini
        // Créer des enfants StaticMeshComponent pour chaque noeud avec mesh
    }
}
```

**Alternative** (plus propre) : créer un **nouveau** `StaticModelComponent` qui gère la hiérarchie, et laisser `StaticMeshComponent` pour un mesh unique. L'import crée alors une Entity avec un `StaticModelComponent` comme root, et celui-ci crée des enfants `StaticMeshComponent` via la hiérarchie de `SceneComponent`.

> **Recommandation** : Garder `StaticMeshComponent` inchangé (1 mesh = 1 component), et **ajouter** un processus d'import qui crée une **Entity avec une hiérarchie de SceneComponents** à partir d'un `StaticModel`.

### Modifications éditeur (`CasaEngine.EditorUI/`)

#### 7. Modifier `ContentBrowserControl.ImportAssetFile`
Ajouter le choix "Static Model" vs "Skinned Model" à l'import. Si static → utiliser `StaticModelImporter`.

#### 8. Modifier `Import3dFileOptionsWindow`
Ajouter un choix : type d'import (Static Model / Skinned Model).

#### 9. `Constants.FileNameExtensions`
Ajouter `.staticModel` pour le nouveau type d'asset.

#### 10. Enregistrer le `StaticModelLoader` dans le setup
Enregistrer l'`IAssetLoader` pour le type `StaticModel` dans l'initialisation du jeu.

---

## Diagramme de flux d'import

```
Utilisateur drag & drop un .fbx dans le Content Browser
    │
    ▼
Import3dFileOptionsWindow s'ouvre
    │ Nouveau choix : [Static Model] / [Skinned Model]
    │
    ├─ Static Model sélectionné
    │   │
    │   ▼
    │  StaticModelImporter.Import(filePath)
    │   │  - Charge via Assimp
    │   │  - Parcourt la hiérarchie de nodes
    │   │  - Extrait les meshes (vertices, indices)
    │   │  - Construit StaticModel (nodes + meshes)
    │   │
    │   ▼
    │  AssetSaver.SaveAsset() → fichier .staticModel (JSON)
    │  AssetCatalog.Add() → 2 entrées (fichier source + asset)
    │   │
    │   ▼
    │  (Optionnel) Import textures associées
    │
    └─ Skinned Model sélectionné → workflow existant (RiggedModel → SkinnedMesh)
```

## Diagramme de flux d'utilisation runtime

```
Entity avec StaticMeshComponent(s) en hiérarchie
    │
    ▼
InitializeWithWorld()
    │  Charge StaticModel via AssetContentManager
    │  OU utilise le Mesh inline (cas primitives)
    │
    ▼
Draw()
    │  Pour chaque StaticMeshComponent dans la hiérarchie :
    │    → _meshRendererComponent.AddMesh(mesh, material, worldMatrix)
    │
    ▼
StaticMeshRendererComponent.Flush()
    │  Dessine tous les meshes accumulés
```

---

## Task List pour Agent IA

### Phase 1 — Core Engine (classes de données)

- [ ] **Task 1.1** : Créer `StaticModelMesh.cs` dans `CasaEngine/Framework/Graphics/`
  - Propriétés : Name, Vertices (VertexPositionNormalTexture[]), Indices (uint[]), PrimitiveType, MaterialIndex, TextureAssetId
  - VertexBuffer / IndexBuffer (créés dans Initialize)
  - Méthode `Initialize(GraphicsDevice device)`
  - Méthodes `Load(JObject)` / `Save(JObject)` (Save sous `#if EDITOR`)
  - Méthode `GetVertices()` pour le calcul de bounding box

- [ ] **Task 1.2** : Créer `StaticModelNode.cs` dans `CasaEngine/Framework/Graphics/`
  - Propriétés : Name, Position (Vector3), Rotation (Quaternion), Scale (Vector3), MeshIndex (int, -1 si pas de mesh), Children (List\<StaticModelNode\>)
  - Propriété calculée `LocalTransform` (Matrix)
  - Méthodes `Load(JObject)` / `Save(JObject)` (Save sous `#if EDITOR`)

- [ ] **Task 1.3** : Créer `StaticModel.cs` dans `CasaEngine/Framework/Graphics/`
  - Hérite de `ObjectBase`
  - Propriétés : RootNode (StaticModelNode), Meshes (List\<StaticModelMesh\>)
  - Méthode `Initialize(AssetContentManager)` qui initialise chaque mesh
  - Load/Save qui sérialise la hiérarchie de nodes + les meshes
  - Méthode `LoadTextures(AssetContentManager)` pour charger les textures référencées

- [ ] **Task 1.4** : Ajouter `StaticModel = ".staticModel"` dans `Constants.FileNameExtensions`

### Phase 2 — Asset Loader

- [ ] **Task 2.1** : Créer `StaticModelLoader.cs` dans `CasaEngine/Framework/Assets/Loaders/`
  - Implémenter `IAssetLoader`
  - `IsFileSupported` : vérifie extension `.staticModel`
  - `LoadAsset` : lit le JSON, désérialise un `StaticModel`, retourne l'objet

- [ ] **Task 2.2** : Enregistrer `StaticModelLoader` pour le type `StaticModel` dans le setup du jeu
  - Chercher où les autres loaders sont enregistrés (`AssetContentManager.RegisterAssetLoader`)
  - Ajouter `assetContentManager.RegisterAssetLoader(typeof(StaticModel), new StaticModelLoader())`

### Phase 3 — Importer (Éditeur)

- [ ] **Task 3.1** : Créer `StaticModelImporter.cs` dans `CasaEngine/Framework/Assets/Loaders/` (sous `#if EDITOR`)
  - Utilise `Assimp.AssimpContext` pour charger le fichier 3D
  - Parcourt `scene.RootNode` récursivement pour construire les `StaticModelNode`
  - Parcourt `scene.Meshes` pour construire les `StaticModelMesh` (vertices, indices, textures)
  - Associe les meshes aux nodes (via MeshIndex)
  - Gère l'extraction des chemins de textures pour l'import
  - Retourne un `StaticModel` prêt à être sérialisé
  - PostProcessSteps recommandés : Triangulate, FlipUVs, GenerateNormals, JoinIdenticalVertices

- [ ] **Task 3.2** : Modifier `Import3dFileOptionsWindow.xaml` et `.xaml.cs`
  - Ajouter un RadioButton ou ComboBox : "Import as Static Model" / "Import as Skinned Model"
  - Exposer une propriété `ImportAsStaticModel` (bool)
  - Par défaut, sélectionner "Static" si le modèle n'a pas de squelette (peut être déterminé après)

- [ ] **Task 3.3** : Modifier `ContentBrowserControl.ImportAssetFile`
  - Si `Import3dFileOptionsWindow.ImportAsStaticModel == true` :
    - Utiliser `StaticModelImporter.Import()`
    - Sauvegarder le `StaticModel` avec `AssetSaver.SaveAsset()` et extension `.staticModel`
    - Gérer l'import des textures associées (similaire à `ImportTexturesFromModel`)
  - Sinon : conserver le workflow existant (RiggedModel → SkinnedMesh)

### Phase 4 — Component & Rendering

- [ ] **Task 4.1** : Modifier `StaticMeshComponent` pour supporter `StaticModel`
  - Ajouter `Guid StaticModelAssetId` et `StaticModel? StaticModel`
  - Dans `InitializeWithWorld` : si `StaticModelAssetId != Guid.Empty`, charger le `StaticModel` via `AssetContentManager.Load<StaticModel>(id)`
  - Si le `StaticModel` a une hiérarchie, créer des `StaticMeshComponent` enfants via `AddChildComponent()` pour chaque noeud avec mesh
  - Chaque enfant reçoit ses Coordinates depuis le `StaticModelNode` correspondant
  - Chaque enfant reçoit son mesh depuis `StaticModel.Meshes[node.MeshIndex]`
  - Mettre à jour `Draw()` pour aussi supporter le dessin d'un `StaticModelMesh`
  - Mettre à jour `GetBoundingBox()` pour prendre en compte tous les enfants
  - Mettre à jour `Load`/`Save` pour sérialiser le `StaticModelAssetId`

- [ ] **Task 4.2** : Adapter `StaticMeshRendererComponent` si nécessaire
  - Vérifier que `AddMesh` / `Flush` supporte `StaticModelMesh` (même structure VertexBuffer/IndexBuffer → devrait fonctionner)
  - Ajouter une surcharge `AddMesh(StaticModelMesh, ...)` si le type est différent

### Phase 5 — Éditeur UI

- [ ] **Task 5.1** : Mettre à jour `ContentBrowserControl` pour ouvrir/prévisualiser les `.staticModel`
  - Ajouter le case `.staticModel` dans le switch d'ouverture d'asset
  - Créer un contrôle de preview si nécessaire (ou réutiliser l'existant)

- [ ] **Task 5.2** : Mettre à jour le drag & drop d'asset sur une Entity dans l'éditeur
  - Permettre de drag & drop un `.staticModel` pour créer automatiquement un `StaticMeshComponent` avec le bon `StaticModelAssetId`

- [ ] **Task 5.3** : Créer ou mettre à jour le ViewModel `StaticMeshComponentViewModel`
  - Afficher / éditer le `StaticModelAssetId` (sélecteur d'asset)
  - Afficher les propriétés du `StaticModel` chargé (nombre de meshes, hiérarchie)

### Phase 6 — Tests & Validation

- [ ] **Task 6.1** : Créer une démo `StaticModelDemo` dans `CasaEngine.Demos/`
  - Charger un fichier FBX multi-mesh
  - Vérifier l'affichage avec hiérarchie correcte
  - Vérifier les transforms (position/rotation/scale de chaque noeud)

- [ ] **Task 6.2** : Tester l'import via l'éditeur
  - Import d'un FBX simple (1 mesh)
  - Import d'un FBX multi-mesh avec hiérarchie
  - Vérifier la sérialisation/désérialisation JSON
  - Vérifier que les textures associées sont importées
  - Vérifier le rechargement après fermeture/ouverture du projet

---

## Notes importantes

1. **Rétrocompatibilité** : le `StaticMesh` existant et son workflow (primitives géométriques, mesh inline dans le component) doivent continuer à fonctionner. Le `StaticModel` est un **ajout**, pas un remplacement.

2. **Assimp PostProcessFlags** pour l'import static :
   - `PostProcessSteps.Triangulate` — convertir en triangles
   - `PostProcessSteps.FlipUVs` — convention UV MonoGame
   - `PostProcessSteps.GenerateNormals` — si absentes
   - `PostProcessSteps.JoinIdenticalVertices` — optimiser
   - `PostProcessSteps.PreTransformVertices` — **NE PAS** utiliser si on veut garder la hiérarchie

3. **Format de vertex** : `VertexPositionNormalTexture` est suffisant pour le static. Si on veut supporter les tangentes (normal mapping), il faudra un format custom plus tard.

4. **Matériaux** : pour cette première passe, on peut juste stocker le `TextureAssetId` par mesh. Le système de `Material` complet pourra être connecté ensuite.
