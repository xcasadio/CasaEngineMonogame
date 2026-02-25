using CasaEngine.Core.Serialization;
using CasaEngine.Framework.Assets;
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

#if EDITOR
    private bool _isInitialized;
#endif

    // ------------------------------------------------------------------
    //  Runtime initialisation
    // ------------------------------------------------------------------

    /// <summary>
    /// Upload vertex/index buffers for every mesh and load bound textures.
    /// Call once after the asset has been loaded.
    /// </summary>
    public void Initialize(AssetContentManager assetContentManager)
    {
#if EDITOR
        if (_isInitialized)
        {
            return;
        }
#endif

        foreach (var mesh in Meshes)
        {
            mesh.Initialize(assetContentManager.GraphicsDevice);

            if (mesh.TextureAssetId != Guid.Empty)
            {
                mesh.LoadTexture(mesh.TextureAssetId, assetContentManager);
            }
        }

#if EDITOR
        _isInitialized = true;
#endif
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

#if EDITOR
    public override void Save(JObject jObject)
    {
        base.Save(jObject);

        // Root node
        if (RootNode != null)
        {
            var nodeObj = new JObject();
            RootNode.Save(nodeObj);
            jObject.Add("root_node", nodeObj);
        }
        else
        {
            jObject.Add("root_node", JValue.CreateNull());
        }

        // Meshes
        var meshesArray = new JArray();
        foreach (var mesh in Meshes)
        {
            var meshObj = new JObject();
            mesh.Save(meshObj);
            meshesArray.Add(meshObj);
        }
        jObject.Add("meshes", meshesArray);
    }
#endif
}
