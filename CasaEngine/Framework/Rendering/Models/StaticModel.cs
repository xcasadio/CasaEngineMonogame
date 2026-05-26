using CasaEngine.Engine.Primitives.ThreeD;
using CasaEngine.Framework.Assets;

using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Rendering.Models;

/// <summary>
/// A static (non-animated) 3-D model asset.
/// Contains a node hierarchy (<see cref="RootNode"/>) and a flat list of
/// <see cref="StaticModelMesh"/> objects referenced by the nodes.
/// Load/Save use the engine JSON format so it integrates with
/// <see cref="AssetLoader{T}"/> and <see cref="Assets.AssetSaver"/>.
/// </summary>
public class StaticModel : ObjectBase
{
    public StaticModelNode RootNode { get; set; }

    public List<StaticModelMesh> Meshes { get; } = new();

    private bool _isInitialized;

    // ------------------------------------------------------------------
    //  Factory helpers
    // ------------------------------------------------------------------

    /// <summary>
    /// Creates a <see cref="StaticModel"/> containing a single mesh built from
    /// the supplied <paramref name="primitive"/> geometry.
    /// The model has a root node pointing to mesh index 0.
    /// <para>
    /// Call <c>model.Meshes[0].Initialize(graphicsDevice)</c> (or
    /// <c>model.Initialize(assetContentManager)</c>) before adding the
    /// owning entity to a world.
    /// </para>
    /// </summary>
    public static StaticModel CreateFromPrimitive(GeometricPrimitive primitive, string name = "Mesh")
    {
        var mesh = new StaticModelMesh { Name = name };
        mesh.SetData(primitive.Vertices.ToArray(), primitive.Indices.ToArray());

        var root = new StaticModelNode
        {
            Name      = "Root",
            MeshIndex = 0,
            Position  = Vector3.Zero,
            Rotation  = Quaternion.Identity,
            Scale     = Vector3.One
        };

        var model = new StaticModel { Name = name };
        model.Meshes.Add(mesh);
        model.RootNode = root;
        return model;
    }

    // ------------------------------------------------------------------
    //  Runtime initialisation
    // ------------------------------------------------------------------

    /// <summary>
    /// Upload vertex/index buffers for every mesh and resolve the runtime materials.
    /// Call once after the asset has been loaded.
    /// </summary>
    public void Initialize(AssetContentManager assetContentManager)
    {
        if (_isInitialized)
        {
            return;
        }

        StaticModelMaterialSlots.EnsureMetadata(this);

        foreach (var mesh in Meshes)
        {
            mesh.Initialize(assetContentManager.GraphicsDevice);

            mesh.Material = StaticModelMaterialResolver.ResolveMeshMaterial(mesh, assetContentManager);

            // Resolve per-submesh materials
            foreach (var sub in mesh.SubMeshes)
            {
                sub.Material = StaticModelMaterialResolver.ResolveSubMeshMaterial(mesh, sub, assetContentManager, mesh.Material);
            }
        }

        _isInitialized = true;
    }

    public bool ReferencesAnyMaterialAsset(ISet<Guid> materialAssetIds)
    {
        ArgumentNullException.ThrowIfNull(materialAssetIds);

        if (materialAssetIds.Count == 0)
        {
            return false;
        }

        foreach (var mesh in Meshes)
        {
            if (mesh.MaterialAssetId != Guid.Empty && materialAssetIds.Contains(mesh.MaterialAssetId))
            {
                return true;
            }

            foreach (var subMesh in mesh.SubMeshes)
            {
                Guid referencedMaterialId = subMesh.MaterialAssetId != Guid.Empty
                    ? subMesh.MaterialAssetId
                    : mesh.MaterialAssetId;
                if (referencedMaterialId != Guid.Empty && materialAssetIds.Contains(referencedMaterialId))
                {
                    return true;
                }
            }
        }

        return false;
    }

    public bool RefreshResolvedMaterials(AssetContentManager assetContentManager, ISet<Guid> affectedMaterialAssetIds = null)
    {
        ArgumentNullException.ThrowIfNull(assetContentManager);

        StaticModelMaterialSlots.EnsureMetadata(this);

        bool refreshAllMaterials = affectedMaterialAssetIds == null || affectedMaterialAssetIds.Count == 0;
        bool refreshedAnyMaterial = false;

        foreach (var mesh in Meshes)
        {
            bool refreshMeshMaterial = refreshAllMaterials
                || (mesh.MaterialAssetId != Guid.Empty && affectedMaterialAssetIds!.Contains(mesh.MaterialAssetId));
            if (refreshMeshMaterial)
            {
                mesh.Material = StaticModelMaterialResolver.ResolveMeshMaterial(mesh, assetContentManager);
                refreshedAnyMaterial = true;
            }

            foreach (var subMesh in mesh.SubMeshes)
            {
                bool refreshSubMeshMaterial = refreshAllMaterials;
                if (!refreshSubMeshMaterial)
                {
                    Guid referencedMaterialId = subMesh.MaterialAssetId != Guid.Empty
                        ? subMesh.MaterialAssetId
                        : mesh.MaterialAssetId;
                    refreshSubMeshMaterial = referencedMaterialId != Guid.Empty && affectedMaterialAssetIds!.Contains(referencedMaterialId);
                }

                if (!refreshSubMeshMaterial)
                {
                    continue;
                }

                subMesh.Material = StaticModelMaterialResolver.ResolveSubMeshMaterial(mesh, subMesh, assetContentManager, mesh.Material);
                refreshedAnyMaterial = true;
            }
        }

        if (refreshedAnyMaterial)
        {
            _isInitialized = true;
        }

        return refreshedAnyMaterial;
    }

    // ------------------------------------------------------------------
    //  Serialization
    // ------------------------------------------------------------------

    public override void Load(JObject element)
    {
        base.Load(element);

        // Root node
        if (element.ContainsKey("root_node") && element["root_node"]!.Type != JTokenType.Null)
        {
            RootNode = new StaticModelNode();
            RootNode.Load((JObject)element["root_node"]!);
        }

        // Meshes
        Meshes.Clear();
        if (element.TryGetValue("meshes", out var value))
        {
            foreach (JObject meshObj in value!)
            {
                var mesh = new StaticModelMesh();
                mesh.Load(meshObj);
                Meshes.Add(mesh);
            }
        }

        StaticModelMaterialSlots.EnsureMetadata(this);
    }
}
