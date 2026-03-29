using System.ComponentModel;
using CasaEngine.Core.Serialization;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.Game.Components;
using CasaEngine.Framework.Graphics;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Entities.Components;

/// <summary>
/// Renders a <see cref="StaticModel"/> asset in the world.
/// On <see cref="InitializeWithWorld"/>, the model hierarchy is expanded into
/// child <see cref="StaticModelSubMeshComponent"/> instances (one per
/// <see cref="StaticModelNode"/> referencing a mesh).  Each child is marked
/// <see cref="StaticModelSubMeshComponent.IsGeneratedFromModel"/> so that
/// <see cref="Save"/> skips them — they are always rebuilt from the asset.
/// </summary>
[DisplayName("Static Model")]
public class StaticModelComponent : PrimitiveComponent
{
    /// <summary>Asset ID of the <see cref="StaticModel"/> to render.</summary>
    public Guid StaticModelAssetId { get; set; } = Guid.Empty;

    /// <summary>Runtime reference to the loaded model.</summary>
    public StaticModel? StaticModel { get; set; }

    public StaticModelComponent() { }

    public StaticModelComponent(StaticModelComponent other) : base(other)
    {
        StaticModelAssetId = other.StaticModelAssetId;
    }

    public override StaticModelComponent Clone() => new(this);

    public override void InitializeWithWorld(World.World world)
    {
        base.InitializeWithWorld(world);

        if (StaticModelAssetId != Guid.Empty && StaticModel == null)
        {
            StaticModel = world.Game.AssetContentManager.Load<StaticModel>(StaticModelAssetId);
        }

        StaticModel?.Initialize(world.Game.AssetContentManager);

        if (StaticModel?.RootNode != null)
        {
            // Remove any previously generated children (e.g. re-initialize).
            var old = Children
                .OfType<StaticModelSubMeshComponent>()
                .Where(c => c.IsGeneratedFromModel)
                .ToList();
            foreach (var c in old)
            {
                RemoveChildComponent(c);
            }

            // Build the component hierarchy from the model node tree.
            BuildHierarchy(StaticModel.RootNode, this, world);
        }
    }

    private void BuildHierarchy(StaticModelNode node, SceneComponent parent, World.World world)
    {
        var sub = new StaticModelSubMeshComponent
        {
            Name = node.Name,
            IsGeneratedFromModel = true,
        };

        // Apply the node's local transform.
        sub.Coordinates.Position    = node.Position;
        sub.Coordinates.Orientation = node.Rotation;
        sub.Coordinates.Scale       = node.Scale;

        // Wire up the mesh if this node has one.
        if (node.MeshIndex >= 0 && node.MeshIndex < StaticModel!.Meshes.Count)
        {
            sub.ModelMesh = StaticModel.Meshes[node.MeshIndex];
        }

        parent.AddChildComponent(sub);
        sub.InitializeWithWorld(world);

        foreach (var child in node.Children)
        {
            BuildHierarchy(child, sub, world);
        }
    }

    // Draw and BoundingBox are fully delegated to the child StaticModelSubMeshComponents
    // via the SceneComponent.Draw() / GetBoundingBox() propagation chain.

    public override void Load(JObject element)
    {
        base.Load(element);

        if (element.ContainsKey("static_model_asset_id"))
        {
            StaticModelAssetId = element["static_model_asset_id"]!.GetGuid();
        }
    }

}
