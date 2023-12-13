using System.ComponentModel;
using System.Text.Json;
using CasaEngine.Core.Design;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.Game.Components;
using CasaEngine.Framework.Graphics;
using CasaEngine.Framework.Materials;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Entities.Components;

[DisplayName("Static Mesh")]
public class StaticMeshComponent : Component, IBoundingBoxable
{
    public override int ComponentId => (int)ComponentIds.Mesh;
    private StaticMeshRendererComponent? _meshRendererComponent;
    private StaticMesh? _mesh;
    private Material? _material;

    public StaticMesh? Mesh
    {
        get { return _mesh; }
        set
        {
            _mesh = value;
#if EDITOR
            OnPropertyChanged();
#endif
        }
    }

    public Material Material
    {
        get { return _material; }
        set
        {
            _material = value;
#if EDITOR
            OnPropertyChanged();
#endif
        }
    }

    public override void Initialize(Entity entity)
    {
        base.Initialize(entity);

        _meshRendererComponent = Owner.Game.GetGameComponent<StaticMeshRendererComponent>();
        Mesh?.Initialize(Owner.Game.GraphicsDevice, Owner.Game.GameManager.AssetContentManager);
    }

    public override void Update(float elapsedTime)
    {
        if (Mesh == null)
        {
            return;
        }

        var camera = Owner.Game.GameManager.ActiveCamera;
        var worldViewProj = Owner.Coordinates.WorldMatrix * camera.ViewMatrix * camera.ProjectionMatrix;
        _meshRendererComponent.AddMesh(Mesh, Material, Owner.Coordinates.WorldMatrix, worldViewProj, camera.Position);
    }

    public override Component Clone()
    {
        var component = new StaticMeshComponent();

        component._mesh = _mesh;

        return component;
    }

    public override void Load(JsonElement element, SaveOption option)
    {
        var meshElement = element.GetProperty("mesh");

        if (meshElement.ToString() != "null")
        {
            _mesh = new StaticMesh();
            _mesh.Load(meshElement);
        }
    }

#if EDITOR

    public override void Save(JObject jObject, SaveOption option)
    {
        base.Save(jObject, option);

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
    }

    public BoundingBox BoundingBox
    {
        get
        {
            var min = Vector3.One * int.MaxValue;
            var max = Vector3.One * int.MinValue;

            if (Mesh != null)
            {
                var vertices = Mesh.GetVertices();

                foreach (var vertex in vertices)
                {
                    var position = Vector3.Transform(vertex.Position, Owner.Coordinates.WorldMatrix);
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
    }
#endif
}
