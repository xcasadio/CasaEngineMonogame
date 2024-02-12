using System.ComponentModel;

using CasaEngine.Framework.Game;
using CasaEngine.Framework.Game.Components;
using CasaEngine.Framework.Graphics;
using CasaEngine.Framework.Materials;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.SceneManagement.Components;

[DisplayName("Static Mesh")]
public class StaticMeshComponent : PrimitiveComponent
{
    private StaticMeshRendererComponent? _meshRendererComponent;

    public StaticMesh? Mesh { get; set; }

    public Material? Material { get; set; }

    public StaticMeshComponent()
    {

    }

    public StaticMeshComponent(StaticMeshComponent other) : base(other)
    {
        Mesh = other.Mesh;
        Material = other.Material;
    }

    protected override void InitializePrivate()
    {
        base.InitializePrivate();
    }

    public override void InitializeWithWorld(World.World world)
    {
        base.InitializeWithWorld(world);

        _meshRendererComponent = world.Game.GetGameComponent<StaticMeshRendererComponent>();
        Mesh?.Initialize(world.Game.GraphicsDevice, world.Game.AssetContentManager);
    }

    public override StaticMeshComponent Clone()
    {
        return new StaticMeshComponent(this);
    }

    public override void Draw(float elapsedTime)
    {
        base.Draw(elapsedTime);

        if (Mesh == null)
        {
            return;
        }

        var camera = Owner.World.Game.GameManager.ActiveCamera;
        var worldViewProj = WorldMatrixWithScale * camera.ViewMatrix * camera.ProjectionMatrix;
        _meshRendererComponent.AddMesh(Mesh, Material, WorldMatrixWithScale, worldViewProj, camera.Position);
    }

    public override BoundingBox GetBoundingBox()
    {
        var min = Vector3.One * int.MaxValue;
        var max = Vector3.One * int.MinValue;

        if (Mesh != null)
        {
            var vertices = Mesh.GetVertices();

            foreach (var vertex in vertices)
            {
                var position = Vector3.Transform(vertex.Position, WorldMatrixWithScale);
                min = Vector3.Min(min, position);
                max = Vector3.Max(max, position);
            }
        }
        else // default box
        {
            const float length = 0.5f;
            min = Vector3.One * -length;
            max = Vector3.One * length;
        }

        return new BoundingBox(min, max);
    }

    public override void Load(JObject element)
    {
        base.Load(element);

        var meshElement = element["mesh"];

        if (meshElement.ToString() != "null")
        {
            Mesh = new StaticMesh();
            Mesh.Load((JObject)meshElement);
        }

        //material
    }

#if EDITOR

    public override void Save(JObject jObject)
    {
        base.Save(jObject);

        JObject newJObject = new();
        if (Mesh != null)
        {
            Mesh.Save(newJObject);
            jObject.Add("mesh", newJObject);
        }
        else
        {
            jObject.Add("mesh", "null");
        }

        //material
    }

#endif
}