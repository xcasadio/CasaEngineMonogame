using System.ComponentModel;
using CasaEngine.Core.Serialization;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Application.Components;
using CasaEngine.Framework.Rendering.Models;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Scene.Entities.Components;

[DisplayName("Skinned Mesh")]
public class SkinnedMeshComponent : PrimitiveComponent
{
    private SkinnedMeshRendererComponent? _skinnedMeshRendererComponent;

    public Guid SkinnedMeshAssetId { get; set; } = Guid.Empty;
    public SkinnedMesh? SkinnedMesh { get; set; }

    public SkinnedMeshComponent()
    {

    }

    public SkinnedMeshComponent(SkinnedMeshComponent other) : base(other)
    {
        SkinnedMesh = other.SkinnedMesh;
    }

    public override void InitializeWithWorld(CasaEngine.Framework.Scene.World.World world)
    {
        base.InitializeWithWorld(world);

        _skinnedMeshRendererComponent = Owner.World.Game.GetGameComponent<SkinnedMeshRendererComponent>();

        if (SkinnedMeshAssetId != Guid.Empty)
        {
            SkinnedMesh = world.Game.AssetContentManager.Load<SkinnedMesh>(SkinnedMeshAssetId);
            SkinnedMesh?.Initialize(Owner.World.Game.AssetContentManager);
        }
    }

    public override SkinnedMeshComponent Clone()
    {
        return new SkinnedMeshComponent(this);
    }

    public override void Update(float elapsedTime)
    {
        SkinnedMesh?.RiggedModel?.Update(elapsedTime);

        base.Update(elapsedTime);
    }

    public override void Draw(float elapsedTime)
    {
        if (SkinnedMesh?.RiggedModel == null || _skinnedMeshRendererComponent == null)
        {
            return;
        }

        _skinnedMeshRendererComponent.AddMesh(
            SkinnedMesh.RiggedModel,
            WorldMatrixWithScale);
    }

    public override BoundingBox GetBoundingBox()
    {
        if (SkinnedMesh?.RiggedModel != null)
        {
            bool hasBounds = false;
            BoundingBox bounds = default;

            foreach (var mesh in SkinnedMesh.RiggedModel.Meshes)
            {
                var meshWorld = WorldMatrixWithScale * mesh.NodeRefContainingAnimatedTransform.CombinedTransformMg;
                var meshBounds = new BoundingBox(mesh.Min, mesh.Max).Transform(meshWorld);

                if (!hasBounds)
                {
                    bounds = meshBounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.ExpandBy(meshBounds);
                }
            }

            if (hasBounds)
            {
                return bounds;
            }
        }

        return base.GetBoundingBox();
    }

    public override void Load(JObject element)
    {
        base.Load(element);

        if (element.ContainsKey("skinned_mesh_id"))
        {
            SkinnedMeshAssetId = element["skinned_mesh_id"].GetGuid();
        }
    }

}
