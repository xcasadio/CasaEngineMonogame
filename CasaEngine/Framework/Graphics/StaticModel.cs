using CasaEngine.Core.Log;
using CasaEngine.Core.Serialization;
using CasaEngine.Engine.Primitives3D;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Materials;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Graphics;

/// <summary>
/// A static (non-animated) 3-D model asset.
/// Contains a node hierarchy (<see cref="RootNode"/>) and a flat list of
/// <see cref="StaticModelMesh"/> objects referenced by the nodes.
/// Load/Save use the engine JSON format so it integrates with
/// <see cref="AssetLoader{T}"/> and <see cref="Assets.AssetSaver"/>.
/// </summary>
public class StaticModel : ObjectBase
{
    public StaticModelNode? RootNode { get; set; }

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
    /// Upload vertex/index buffers for every mesh and load bound textures.
    /// Call once after the asset has been loaded.
    /// </summary>
    public void Initialize(AssetContentManager assetContentManager)
    {
        if (_isInitialized)
        {
            return;
        }

        foreach (var mesh in Meshes)
        {
            mesh.Initialize(assetContentManager.GraphicsDevice);

            // Resolve material asset — takes priority over legacy texture slot
            if (mesh.MaterialAssetId != Guid.Empty)
            {
                try
                {
                    mesh.Material = assetContentManager.Load<MaterialBase>(mesh.MaterialAssetId);
                }
                catch (Exception ex)
                {
                    Logs.WriteException(ex);
                }
            }
            else if (mesh.TextureAssetId != Guid.Empty)
            {
                mesh.LoadTexture(mesh.TextureAssetId, assetContentManager);
            }

            // Resolve per-submesh materials
            foreach (var sub in mesh.SubMeshes)
            {
                if (sub.MaterialAssetId == Guid.Empty) continue;
                try
                {
                    sub.Material = assetContentManager.Load<MaterialBase>(sub.MaterialAssetId);
                }
                catch (Exception ex)
                {
                    Logs.WriteException(ex);
                }
            }
        }

        _isInitialized = true;
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
        if (element.ContainsKey("meshes"))
        {
            foreach (JObject meshObj in element["meshes"]!)
            {
                var mesh = new StaticModelMesh();
                mesh.Load(meshObj);
                Meshes.Add(mesh);
            }
        }
    }

    public override void Save(JObject jObject)
    {
        throw new NotSupportedException("StaticModel authoring serialization lives in CasaEngine.EditorServices.");
    }
}
