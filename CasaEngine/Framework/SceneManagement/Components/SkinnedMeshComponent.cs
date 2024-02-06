using System.ComponentModel;
using System.Text.Json;
using CasaEngine.Engine.Animations;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.Game.Components;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.SceneManagement.Components;

[DisplayName("Skinned Mesh")]
public class SkinnedMeshComponent : PrimitiveComponent
{
    private SkinnedMeshRendererComponent? _skinnedMeshRendererComponent;
    private RiggedModel? _skinnedMesh;

    public RiggedModel? SkinnedMesh
    {
        get { return _skinnedMesh; }
        set
        {
            _skinnedMesh = value;
        }
    }

    public SkinnedMeshComponent()
    {

    }

    public SkinnedMeshComponent(SkinnedMeshComponent other) : base(other)
    {
        _skinnedMesh = other._skinnedMesh;
    }

    public override void InitializeWithWorld(World.World world)
    {
        base.InitializeWithWorld(world);

        _skinnedMeshRendererComponent = Owner.World.Game.GetGameComponent<SkinnedMeshRendererComponent>();
        //Mesh?.LoadContent(game.GraphicsDevice);
        //Mesh?.Texture?.LoadContent(game.GraphicsDevice, game.AssetContentManager);
    }

    public override SkinnedMeshComponent Clone()
    {
        return new SkinnedMeshComponent(this);
    }

    public override void Update(float elapsedTime)
    {
        SkinnedMesh?.Update(elapsedTime);

        base.Update(elapsedTime);
    }

    public override void Draw(float elapsedTime)
    {
        if (SkinnedMesh == null)
        {
            return;
        }

        var camera = Owner.World.Game.GameManager.ActiveCamera;
        _skinnedMeshRendererComponent.AddMesh(
            SkinnedMesh,
            WorldMatrixWithScale,
            camera.ViewMatrix,
            camera.ProjectionMatrix,
            camera.Position);
    }

    public override BoundingBox GetBoundingBox()
    {
        var min = Vector3.One * int.MaxValue;
        var max = Vector3.One * int.MinValue;

        if (SkinnedMesh != null)
        {
            foreach (var mesh in SkinnedMesh.Meshes)
            {
                min = Vector3.Min(min, mesh.Min);
                max = Vector3.Max(max, mesh.Max);
            }
        }
        else // default box
        {
            const float length = 0.5f;
            min = Vector3.One * -length;
            max = Vector3.One * length;
        }

        min = Vector3.Transform(min, WorldMatrixWithScale);
        max = Vector3.Transform(max, WorldMatrixWithScale);

        return new BoundingBox(min, max);
    }

    public override void Load(JsonElement element)
    {
        base.Load(element);

        var meshElement = element.GetProperty("skinned_mesh");

        if (meshElement.ToString() != "null")
        {
            //_mesh = new SkinModel();
            //_mesh.Load(meshElement);
        }
    }

#if EDITOR

    public override void Save(JObject jObject)
    {
        base.Save(jObject);

        JObject newJObject = new();
        //if (SkinnedMesh != null)
        //{
        //    SkinnedMesh.Save(newJObject);
        //    jObject.Add("skinned_mesh", newJObject);
        //}
        //else
        //{
        //    jObject.Add("mesh", "null");
        //}
    }
#endif
}
