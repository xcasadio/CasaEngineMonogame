using System.Text.Json;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Entities.Components;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.Game.Components;
using CasaEngine.Framework.Graphics;
using CasaEngine.Framework.Materials;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.SceneManagement.Components;

public class StaticMeshComponent : PrimitiveComponent
{
    public const int ComponentId = (int)ComponentIds.Mesh;

    private StaticMeshRendererComponent? _meshRendererComponent;
    private StaticMesh? _mesh;
    private Material? _material;

    public StaticMesh? Mesh
    {
        get { return _mesh; }
        set
        {
            _mesh = value;
        }
    }

    public Material? Material
    {
        get { return _material; }
        set
        {
            _material = value;
        }
    }

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
        Mesh?.Initialize(world.Game.GraphicsDevice, world.Game.GameManager.AssetContentManager);
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

        var camera = World.Game.GameManager.ActiveCamera;
        var worldViewProj = WorldMatrix * camera.ViewMatrix * camera.ProjectionMatrix;
        _meshRendererComponent.AddMesh(Mesh, Material, WorldMatrix, worldViewProj, camera.Position);
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
                var position = Vector3.Transform(vertex.Position, WorldMatrix);
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

    public override void Load(JsonElement element)
    {
        base.Load(element);

        var meshElement = element.GetProperty("mesh");

        if (meshElement.ToString() != "null")
        {
            _mesh = new StaticMesh();
            _mesh.Load(meshElement);
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
            _mesh.Save(newJObject, SaveOption.Editor);
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