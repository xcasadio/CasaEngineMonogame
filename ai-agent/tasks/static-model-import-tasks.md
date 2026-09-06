# Import 3D Static Model — Architecture & Task List

> **Réconciliation avec le code (2026-09-06, chantier ai-guidelines T5.5)** : 25 tâches vérifiées livrées, 1 restante (Task 6.2 : tester l'import via l'éditeur, aucun test d'import FBX réel par l'éditeur trouvé). Références périmées annotées : `CasaEngine.EditorUI/` (projet WPF supprimé), `RiggedModelLoader` et `StaticModelImporter` (supprimés par la migration glTF, ADR-0019), `ContentBrowserControl.ImportAssetFile` et `Import3dFileOptionsWindow` (ont existé, supprimés avec les projets WPF : commit `dc2f4d25` « remove unused projects »), `Framework/Graphics/` (réel : `Framework/Rendering/Models/`). Le plan reste actif tant que la Task 6.2 n'est pas faite.

## Analyse de l'existant

### Problème actuel
- `StaticMesh` ne gère qu'**un seul mesh** (un seul VertexBuffer/IndexBuffer).
- `StaticMeshComponent` référence un seul `StaticMesh` + un seul `Material`.
- L'import 3D (`ContentBrowserControl.ImportAssetFile`) crée toujours un `RiggedModel` → `SkinnedMesh`, même pour un modèle sans squelette. *(Référence périmée — `ContentBrowserControl` est introuvable dans le code actuel ; l'import passe aujourd'hui par `EditorAssetImportService.ImportFile`, `CasaEngine.EditorServices/EditorAssetImportService.cs:23`, appelé depuis `FileOperationService`, `CasaEngine.Editor/ContentBrowser/Services/FileOperationService.cs:423`. Ce constat décrit l'état d'avant ce plan, avant migration glTF.)*
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
| ~~`RiggedModelLoader`~~ | **Supprimé par la migration glTF** (`ai-agent/tasks/gltf-import-migration-tasks.md`, ADR `docs/decisions/0019-gltf-import-migration.md`) — introuvable dans le code (recherche `class RiggedModelLoader` négative). Le runtime charge désormais les modèles riggés via `GltfRiggedModelReader` (`CasaEngine/Framework/Assets/Animations/GltfRiggedModelReader.cs`) |
| `ModelLoader` | IAssetLoader qui utilise `GltfRiggedModelReader` (`CasaEngine/Framework/Assets/Loaders/ModelLoader.cs`) — utilisait `RiggedModelLoader`/Assimp avant la migration glTF |
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

#### 1. `StaticModelMesh` (dans `Framework/Graphics/`) *(chemin réel : `CasaEngine/Framework/Rendering/Models/StaticModelMesh.cs`)*
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

#### 2. `StaticModelNode` (dans `Framework/Graphics/`) *(chemin réel : `CasaEngine/Framework/Rendering/Models/StaticModelNode.cs`)*
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

#### 3. `StaticModel` (dans `Framework/Graphics/`) *(chemin réel : `CasaEngine/Framework/Rendering/Models/StaticModel.cs`)*
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

#### 5. `StaticModelImporter` (dans `Framework/Assets/Loaders/`) — ⚠️ **Supprimé par la migration glTF** : cette classe a existé (Task 3.1, implémentée sous `#if EDITOR`) puis a été supprimée lors de la migration décrite dans `ai-agent/tasks/gltf-import-migration-tasks.md` et `docs/decisions/0019-gltf-import-migration.md` (« Deleted `StaticModelImporter.cs`, `RiggedModelLoader.cs`, `AssimpConverter.cs` »). Recherche `class StaticModelImporter` dans le code : négative. Le rôle décrit ici est aujourd'hui tenu par `GltfStaticModelReader` (`CasaEngine/Framework/Assets/Loaders/GltfStaticModelReader.cs:22`) + `AssimpToGltfConverter` (`CasaEngine.EditorServices/Import/AssimpToGltfConverter.cs:18`), orchestrés par `EditorAssetImportService.ImportFile` (`CasaEngine.EditorServices/EditorAssetImportService.cs:23`).
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

### Modifications éditeur (`CasaEngine.EditorUI/`) — ⚠️ **Dossier disparu**. Il n'existe pas de projet `CasaEngine.EditorUI` dans le dépôt actuel (recherche `fd -t d "EditorUI"` négative). L'éditeur vit dans `CasaEngine.Editor/` (contrôles/panels, ex. `CasaEngine.Editor/Controls/ContentBrowserPanel.cs`, `CasaEngine.Editor/ContentBrowser/Services/FileOperationService.cs`) et `CasaEngine.EditorServices/` (logique d'import, ex. `EditorAssetImportService.cs`). Les classes `ContentBrowserControl` et `Import3dFileOptionsWindow` citées ci-dessous sont également introuvables telles quelles.

#### 7. Modifier `ContentBrowserControl.ImportAssetFile` *(introuvable — voir note ci-dessus ; l'entrée réelle est `EditorAssetImportService.ImportFile`, `CasaEngine.EditorServices/EditorAssetImportService.cs:23`)*
Ajouter le choix "Static Model" vs "Skinned Model" à l'import. Si static → utiliser `StaticModelImporter` *(supprimée par la migration glTF — voir §5)*.

#### 8. Modifier `Import3dFileOptionsWindow` *(introuvable — aucun fichier de ce nom dans le dépôt ; recherche `fd "Import3dFileOptionsWindow"` négative)*
Ajouter un choix : type d'import (Static Model / Skinned Model).

#### 9. `Constants.FileNameExtensions`
Ajouter `.staticModel` pour le nouveau type d'asset.

#### 10. Enregistrer le `StaticModelLoader` dans le setup
Enregistrer l'`IAssetLoader` pour le type `StaticModel` dans l'initialisation du jeu.

---

## Diagramme de flux d'import

> ⚠️ **Diagramme périmé** : `Import3dFileOptionsWindow` et `StaticModelImporter` n'existent plus dans le code (voir §5, §7, §8 ci-dessus). Le flux réel (post-migration glTF) n'a plus de choix manuel Static/Skinned : `EditorAssetImportService.ImportFile` (`CasaEngine.EditorServices/EditorAssetImportService.cs:23`) détecte automatiquement un modèle riggé (squelette/animations séparés) via `TryImportSeparatedAnimationAssets`, sinon convertit la source en `.glb` si nécessaire (`AssimpToGltfConverter`) et la lit via `GltfStaticModelReader` pour produire un `.staticModel`.

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

- [x] ✅ **Task 1.1** : Créer `StaticModelMesh.cs` dans `CasaEngine/Framework/Graphics/` — ⚠️ chemin périmé, fichier réel : `CasaEngine/Framework/Rendering/Models/StaticModelMesh.cs`
  - ✅ Propriétés : Name, Vertices (VertexPositionNormalTexture[]), Indices (uint[]), PrimitiveType, MaterialIndex, TextureAssetId
  - ✅ VertexBuffer / IndexBuffer (créés dans Initialize)
  - ✅ Méthode `Initialize(GraphicsDevice device)`
  - ✅ Méthodes `Load(JObject)` / `Save(JObject)` (Save sous `#if EDITOR`)
  - ✅ Méthode `GetVertices()` pour le calcul de bounding box

- [x] ✅ **Task 1.2** : Créer `StaticModelNode.cs` dans `CasaEngine/Framework/Graphics/` — ⚠️ chemin périmé, fichier réel : `CasaEngine/Framework/Rendering/Models/StaticModelNode.cs`
  - ✅ Propriétés : Name, Position (Vector3), Rotation (Quaternion), Scale (Vector3), MeshIndex (int, -1 si pas de mesh), Children (List\<StaticModelNode\>)
  - ✅ Propriété calculée `LocalTransform` (Matrix)
  - ✅ Méthodes `Load(JObject)` / `Save(JObject)` (Save sous `#if EDITOR`)

- [x] ✅ **Task 1.3** : Créer `StaticModel.cs` dans `CasaEngine/Framework/Graphics/` — ⚠️ chemin périmé, fichier réel : `CasaEngine/Framework/Rendering/Models/StaticModel.cs`
  - ✅ Hérite de `ObjectBase`
  - ✅ Propriétés : RootNode (StaticModelNode), Meshes (List\<StaticModelMesh\>)
  - ✅ Méthode `Initialize(AssetContentManager)` qui initialise chaque mesh
  - ✅ Load/Save qui sérialise la hiérarchie de nodes + les meshes
  - ✅ Méthode `LoadTextures(AssetContentManager)` pour charger les textures référencées

- [x] ✅ **Task 1.4** : Ajouter `StaticModel = ".staticModel"` dans `Constants.FileNameExtensions`

### Phase 2 — Asset Loader

- [x] ✅ **Task 2.1** : Créer un loader pour `StaticModel`
  - ✅ Implémenter `IAssetLoader`
  - ✅ `IsFileSupported` : vérifie extension `.staticModel`
  - ✅ `LoadAsset` : lit le JSON, désérialise un `StaticModel`, retourne l'objet
  - ℹ️ *Implémenté via le générique `AssetLoader<StaticModel>` au lieu d'un `StaticModelLoader` dédié*

- [x] ✅ **Task 2.2** : Enregistrer le loader pour le type `StaticModel` dans le setup du jeu
  - ✅ Chercher où les autres loaders sont enregistrés (`AssetContentManager.RegisterAssetLoader`)
  - ✅ Enregistré via `new AssetLoader<StaticModel>()` dans `AssetLoaderRegistry`

### Phase 3 — Importer (Éditeur)

- [x] ✅ **Task 3.1** : Créer `StaticModelImporter.cs` dans `CasaEngine/Framework/Assets/Loaders/` (sous `#if EDITOR`) — ⚠️ **Référence périmée** : cette classe a bien été livrée telle que décrite (Assimp direct), puis **supprimée par la migration glTF** (`ai-agent/tasks/gltf-import-migration-tasks.md`, ADR `docs/decisions/0019-gltf-import-migration.md` : « Deleted `StaticModelImporter.cs` »). Recherche `class StaticModelImporter` : négative. Rôle repris par `GltfStaticModelReader` (`CasaEngine/Framework/Assets/Loaders/GltfStaticModelReader.cs:22`) + `AssimpToGltfConverter` (`CasaEngine.EditorServices/Import/AssimpToGltfConverter.cs:18`).
  - ✅ Utilise `Assimp.AssimpContext` pour charger le fichier 3D
  - ✅ Parcourt `scene.RootNode` récursivement pour construire les `StaticModelNode`
  - ✅ Parcourt `scene.Meshes` pour construire les `StaticModelMesh` (vertices, indices, textures)
  - ✅ Associe les meshes aux nodes (via MeshIndex)
  - ✅ Gère l'extraction des chemins de textures pour l'import
  - ✅ Retourne un `StaticModel` prêt à être sérialisé
  - ✅ PostProcessSteps recommandés : Triangulate, FlipUVs, GenerateNormals, JoinIdenticalVertices

- [x] ✅ **Task 3.2** : Modifier `Import3dFileOptionsWindow.xaml` et `.xaml.cs` — ⚠️ **Référence périmée** : `Import3dFileOptionsWindow` introuvable dans le code actuel (recherche `fd "Import3dFileOptionsWindow"` négative). L'import ne propose plus de choix manuel Static/Skinned : `EditorAssetImportService.ImportFile` (`CasaEngine.EditorServices/EditorAssetImportService.cs:23`) décide automatiquement selon le contenu du fichier source. Statut réel (livré à l'origine puis remplacé, ou jamais construit sous ce nom) non vérifiable sans historique git — laissé tel quel par prudence.
  - ✅ Ajouter un RadioButton ou ComboBox : "Import as Static Model" / "Import as Skinned Model"
  - ✅ Exposer une propriété `ImportAsStaticModel` (bool)
  - Par défaut, sélectionner "Static" si le modèle n'a pas de squelette (peut être déterminé après)

- [x] ✅ **Task 3.3** : Modifier `ContentBrowserControl.ImportAssetFile` — ⚠️ **Référence périmée** : `ContentBrowserControl` introuvable ; l'entrée réelle est `FileOperationService` (`CasaEngine.Editor/ContentBrowser/Services/FileOperationService.cs:423`) qui appelle `EditorAssetImportService.ImportFile`, sans branche `ImportAsStaticModel` (voir Task 3.2).
  - ✅ Si `Import3dFileOptionsWindow.ImportAsStaticModel == true` :
    - ✅ Utiliser `StaticModelImporter.Import()`
    - ✅ Sauvegarder le `StaticModel` avec `AssetSaver.SaveAsset()` et extension `.staticModel`
    - ✅ Gérer l'import des textures associées (similaire à `ImportTexturesFromModel`)
  - ✅ Sinon : conserver le workflow existant (RiggedModel → SkinnedMesh)

### Phase 4 — Component & Rendering

- [x] ✅ **Task 4.1** : Supporter `StaticModel` dans un component
  - ✅ `Guid StaticModelAssetId` et `StaticModel? StaticModel`
  - ✅ Dans `InitializeWithWorld` : charger le `StaticModel` via `AssetContentManager`
  - ⚠️ La hiérarchie de meshes est dessinée en monolithique via `DrawNode()` récursif — pas de composants enfants créés
  - ✅ `Draw()` supporte le dessin d'un `StaticModelMesh`
  - ✅ `GetBoundingBox()` prend en compte tous les enfants
  - ✅ `Load`/`Save` sérialisent le `StaticModelAssetId`
  - ℹ️ *Implémenté via un nouveau `StaticModelComponent` dédié (approche alternative plus propre) au lieu de modifier `StaticMeshComponent`*

- [x] ✅ **Task 4.2** : Adapter `StaticMeshRendererComponent` si nécessaire
  - ✅ Vérifier que `AddMesh` / `Flush` supporte `StaticModelMesh` (même structure VertexBuffer/IndexBuffer → devrait fonctionner)
  - ✅ Surcharge `AddMesh(StaticModelMesh, ...)` ajoutée

### Phase 4b — Hiérarchie de composants pour sous-meshes

> **Problème** : `StaticModelComponent` dessine toute la géométrie dans un seul component monolithique via
> `DrawNode()` récursif. L'éditeur ne voit qu'un seul component — il n'y a pas de hiérarchie de 
> sous-composants. L'utilisateur ne peut pas sélectionner, masquer ou paramétrer (matériau, visibilité)
> chaque partie du modèle indépendamment.
>
> **Solution** : Créer un composant dédié `StaticModelSubMeshComponent` (léger, une seule responsabilité : rendre
> un `StaticModelMesh`). Au moment de `InitializeWithWorld()`, `StaticModelComponent` crée un sous-composant
> pour chaque nœud ayant un mesh, via `SceneComponent.AddChildComponent()`. L'éditeur (`ComponentListViewModel`)
> affichera automatiquement la hiérarchie car il parcourt déjà les `Children` récursivement.
>
> On ne réutilise **pas** `StaticMeshComponent` pour les sous-meshes car :
> - `StaticMesh` ≠ `StaticModelMesh` (types différents, propriétés différentes)
> - La sérialisation est opposée : `StaticMeshComponent` sérialise le mesh inline, un sous-mesh ne doit **pas** être sérialisé
> - Mélanger les deux responsabilités dans un seul component violerait SRP
> - `StaticMeshComponent` sera retiré à terme (Phase 7)

- [x] ✅ **Task 4.3** : Créer `StaticModelSubMeshComponent.cs` dans `CasaEngine/Framework/Entities/Components/`
  - Hérite de `PrimitiveComponent`
  - Propriété `StaticModelMesh? ModelMesh` (runtime-only, assignée par le parent `StaticModelComponent`)
  - `Draw()` : appelle `_meshRendererComponent.AddMesh(ModelMesh, world, worldInvT)` (surcharge existante)
  - `GetBoundingBox()` : calcule la bounding box à partir des vertices de `ModelMesh`
  - Référence au `StaticMeshRendererComponent` (récupérée dans `InitializeWithWorld`)
  - `DisplayName` : "Sub Mesh" (ou le nom du nœud assigné par le parent)
  - Pas de sérialisation du mesh (les données viennent de l'asset parent)
  - Override possible : `Material`, `IsVisible` (sérialisés si modifiés par l'utilisateur)

- [x] ✅ **Task 4.4** : Modifier `StaticModelComponent.InitializeWithWorld()` pour créer la hiérarchie
  - Parcourir récursivement `StaticModel.RootNode`
  - Pour chaque `StaticModelNode` ayant un `MeshIndex >= 0`, créer un `StaticModelSubMeshComponent` enfant via `AddChildComponent()`
  - Nommer le composant enfant avec `StaticModelNode.Name` (ex: "Chassis", "Wheel_FL")
  - Assigner les Coordinates (Position, Rotation, Scale) depuis le `StaticModelNode`
  - Assigner `subMeshComponent.ModelMesh = StaticModel.Meshes[node.MeshIndex]`
  - Pour les nœuds structurels (pas de mesh, juste un transform), créer un `StaticModelSubMeshComponent` sans mesh pour préserver la hiérarchie de transforms
  - Initialiser chaque enfant créé (`InitializePrivate()` + `InitializeWithWorld()`)

- [x] ✅ **Task 4.5** : Supprimer le rendu monolithique de `StaticModelComponent.Draw()`
  - Retirer `DrawNode()` et la méthode récursive `DrawNode(StaticModelNode, Matrix)`
  - Le rendu est maintenant délégué aux enfants `StaticModelSubMeshComponent` via la propagation `SceneComponent.Draw()` → `Children[i].Draw()`
  - Retirer `AccumulateBounds()` dans `GetBoundingBox()` — le calcul est maintenant fait par les enfants

- [x] ✅ **Task 4.6** : Créer `StaticModelSubMeshComponentViewModel` + UIéditeur — ⚠️ chemin/nom périmés : `CasaEngine.EditorUI/` n'existe pas ; l'éditeur réel utilise `CasaEngine.Editor/Controls/ComponentEditors/StaticModelSubMeshComponentEditor.cs` (et `StaticModelComponentEditor.cs`), une architecture `ComponentEditor` MGUI, pas un `ViewModel` WPF
  - Créer le ViewModel dans `CasaEngine.EditorUI/Controls/EntityControls/ViewModels/` *(chemin disparu — voir note ci-dessus)*
  - Enregistrer dans `ComponentViewModelFactory` (case `StaticModelSubMeshComponent`)
  - Afficher le nom du sous-mesh, le nombre de vertices/indices
  - *(Optionnel)* Exposer la visibilité par sous-mesh (toggle enable/disable)
  - *(Optionnel)* Permettre l'override du material sur un sous-mesh individuel
  - Vérifier que `StaticModelComponentViewModel` affiche correctement la hiérarchie des sous-composants dans l'arbre

- [x] ✅ **Task 4.7** : Gérer la sérialisation/désérialisation de la hiérarchie
  - Lors du `Save` d'une Entity avec un `StaticModelComponent`, les enfants `StaticModelSubMeshComponent` ne doivent **pas** être sérialisés (ils seront recréés à partir du `StaticModel` asset)
  - Ajouter un flag `IsGeneratedFromModel` (ou similaire) sur `StaticModelSubMeshComponent` pour les distinguer
  - Lors du `Load`, vérifier que les enfants générés ne sont pas dupliqués au rechargement
  - Permettre à l'utilisateur d'override des propriétés (matériau, visibilité) sur un enfant → ces overrides doivent être sérialisés

### Phase 5 — Éditeur UI

- [x] ✅ **Task 5.1** : Mettre à jour `ContentBrowserControl` pour ouvrir/prévisualiser les `.staticModel` — ⚠️ classe périmée : `ContentBrowserControl` introuvable ; l'équivalent réel est `CasaEngine.Editor/Controls/ContentBrowserPanel.cs`
  - ✅ Ajouter le case `.staticModel` dans le switch d'ouverture d'asset
  - Créer un contrôle de preview si nécessaire (ou réutiliser l'existant)

- [x] ✅ **Task 5.2** : Mettre à jour le drag & drop d'asset sur une Entity dans l'éditeur
  - ✅ Permettre de drag & drop un `.staticModel` pour créer automatiquement un `StaticModelComponent` avec le bon `StaticModelAssetId`
  - ℹ️ *Implémenté via `StaticModelAssetDropHandler` enregistré dans `DragAndDropConfiguration`*

- [x] ✅ **Task 5.3** : Créer ou mettre à jour le ViewModel pour `StaticModel`
  - ✅ Afficher / éditer le `StaticModelAssetId` (sélecteur d'asset)
  - ✅ Afficher les propriétés du `StaticModel` chargé (nombre de meshes, hiérarchie)
  - ℹ️ *Implémenté via un nouveau `StaticModelComponentViewModel` dédié*

### Phase 6 — Tests & Validation

- [x] ✅ **Task 6.1** : Créer une démo `StaticModelDemo` dans `CasaEngine.Demos/`
  - ✅ Démo programmatique (box + sphere primitives) avec hiérarchie multi-mesh
  - ✅ Vérification des transforms (position/rotation/scale de chaque nœud)
  - ✅ Enregistrée dans DemosGame

- [ ] ❌ **Task 6.2** : Tester l'import via l'éditeur — statut confirmé toujours non fait : aucun test d'import de bout en bout (FBX réel → `.staticModel` via l'éditeur) trouvé. Les tests existants (`CasaEngine.Tests/Graphics/GltfStaticModelReaderTests.cs`, `CasaEngine.Tests/EditorServices/AssimpToGltfConverterTests.cs`) sont des tests unitaires du lecteur/convertisseur sur des fichiers générés en mémoire, pas un test d'import piloté par l'éditeur avec un FBX réel multi-mesh + textures + rechargement projet. `CasaEngine.Tests/EditorServices/EditorAssetImportServiceTests.cs` ne couvre que l'import Tiled (TMX/TMJ), aucun cas `.fbx`/`.staticModel`.
  - ❌ Import d'un FBX simple (1 mesh) — confirmé non fait, aucune preuve trouvée
  - ❌ Import d'un FBX multi-mesh avec hiérarchie — confirmé non fait, aucune preuve trouvée
  - ❌ Vérifier la sérialisation/désérialisation JSON — confirmé non fait au sens "test d'import éditeur" (existe uniquement en unitaire isolé sur `GltfStaticModelReader`)
  - ❌ Vérifier que les textures associées sont importées — confirmé non fait, aucune preuve trouvée
  - ❌ Vérifier le rechargement après fermeture/ouverture du projet — confirmé non fait, aucune preuve trouvée

### Phase 7 — Dépréciation et suppression de `StaticMeshComponent`

> **Contexte** : `StaticMeshComponent` gère un seul `StaticMesh` inline (primitives). Ce cas d'usage est
> entièrement couvert par un `StaticModel` à 1 mesh + `StaticModelComponent`. Après la Phase 4b,
> `StaticMeshComponent` n'a plus de raison d'exister.
>
> **Usages actuels** :
> - Primitives procédurales (cubes, plans) dans les Demos
> - `ArrowComponent` (hérite de `StaticMeshComponent`)
> - Entities sérialisées (`.entity`, `.world`) dans `SampleProject`
> - Éditeur : "Select Mesh" dans le panel component

- [x] ✅ **Task 7.1** : Ajouter `StaticModel.CreateFromPrimitive(GeometricPrimitive, string)` factory method

- [x] ✅ **Task 7.2** : Migré `ArrowComponent` pour hériter de `StaticModelComponent`
  - ✅ Hérite de `StaticModelComponent` (anciennement `StaticMeshComponent`)
  - ✅ Dans `InitializePrivate` : construit cylinder+cone mesh en `StaticModelMesh`, crée `StaticModel`

- [x] ✅ **Task 7.3** : Migré toutes les Demos vers `StaticModelComponent`
  - ✅ `SplitScreenDemo`, `Collision3dBasicDemo`, `Collision2dBasicDemo`, `SceneManagementDemo`, `UIOverlayDemo`, `RenderToTextureDemo`, `ViewManagerSandbox`
  - ✅ `SandBoxGame` dans `Projects/`

- [x] ✅ **Task 7.4** : Migré les fichiers sérialisés existants
  - ✅ `Box.entity` → type=`StaticModelComponent` + asset `Entities/Box.staticModel`
  - ✅ `DefaultWorld.world` → 2 entités ground migrées + assets `Models/ground_ground*.staticModel`
  - ✅ Nouveaux assets enregistrés dans `AssetInfos.json`

- [x] ✅ **Task 7.5** : Supprimé `StaticMeshComponent` et le code associé
  - ✅ Supprimé `StaticMeshComponent.cs`
  - ✅ Supprimé `StaticMeshComponentViewModel.cs`
  - ✅ Supprimé `StaticMeshComponentControl.xaml` / `.xaml.cs`
  - ✅ Retiré de `ComponentViewModelFactory`, `EntityComponentTemplateSelector`, `EntityComponentControl.xaml`
  - ✅ Supprimé `StaticMesh.cs`
  - ✅ Retiré la surcharge `AddMesh(StaticMesh, Material, ...)` de `StaticMeshRendererComponent`
  - ✅ Build solution : 0 erreurs

---

## Notes importantes

1. **Rétrocompatibilité** : le `StaticMesh` / `StaticMeshComponent` existant continue de fonctionner jusqu'à la Phase 7. Le `StaticModel` est un **ajout** dans un premier temps, puis un **remplacement** complet en Phase 7.

2. **Assimp PostProcessFlags** pour l'import static :
   - `PostProcessSteps.Triangulate` — convertir en triangles
   - `PostProcessSteps.FlipUVs` — convention UV MonoGame
   - `PostProcessSteps.GenerateNormals` — si absentes
   - `PostProcessSteps.JoinIdenticalVertices` — optimiser
   - `PostProcessSteps.PreTransformVertices` — **NE PAS** utiliser si on veut garder la hiérarchie

3. **Format de vertex** : `VertexPositionNormalTexture` est suffisant pour le static. Si on veut supporter les tangentes (normal mapping), il faudra un format custom plus tard.

4. **Matériaux** : pour cette première passe, on peut juste stocker le `TextureAssetId` par mesh. Le système de `Material` complet pourra être connecté ensuite.
