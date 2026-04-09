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
        var min = Vector3.One * int.MaxValue;
        var max = Vector3.One * int.MinValue;

        if (SkinnedMesh?.RiggedModel != null)
        {
            foreach (var mesh in SkinnedMesh.RiggedModel.Meshes)
            {
                min = Vector3.Min(min, Vector3.Transform(mesh.Min, WorldMatrixWithScale));
                max = Vector3.Max(max, Vector3.Transform(mesh.Max, WorldMatrixWithScale));
            }

            return new BoundingBox(min, max);
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
