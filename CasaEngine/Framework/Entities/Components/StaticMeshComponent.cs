using System.ComponentModel;
using System.Text.Json;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.Game.Components;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Entities.Components;

[DisplayName("Static Mesh")]
public class StaticMeshComponent : Component, IBoundingBoxComputable
{
    public static readonly int ComponentId = (int)ComponentIds.Mesh;
    private StaticMeshRendererComponent? _meshRendererComponent;
    private StaticMesh? _mesh;

    //TODO remove : use only in editor mode to retrieve the game. Very ugly....
    public CasaEngineGame Game { get; private set; }

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

    public StaticMeshComponent(Entity entity) : base(entity, ComponentId)
    {
    }

    public override void Initialize(CasaEngineGame game)
    {
        Game = game;
        _meshRendererComponent = game.GetGameComponent<StaticMeshRendererComponent>();
        Mesh?.Initialize(game.GraphicsDevice);
        Mesh?.Texture?.Initialize(game.GraphicsDevice, game.GameManager.AssetContentManager);
    }

    public override void Update(float elapsedTime)
    {
        if (Mesh == null)
        {
            return;
        }

        var camera = Game.GameManager.ActiveCamera;
        var worldViewProj = Owner.Coordinates.WorldMatrix * camera.ViewMatrix * camera.ProjectionMatrix;
        _meshRendererComponent.AddMesh(Mesh, worldViewProj);
    }

    public override void Load(JsonElement element)
    {
        var meshElement = element.GetProperty("mesh");

        if (meshElement.ToString() != "null")
        {
            _mesh = new StaticMesh();
            _mesh.Load(meshElement);
        }
    }

#if EDITOR

    public override void Save(JObject jObject)
    {
        base.Save(jObject);

        JObject newJObject = new();
        if (Mesh != null)
        {
            _mesh.Save(newJObject);
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
                    min = Vector3.Min(min, vertex.Position);
                    max = Vector3.Max(max, vertex.Position);
                }
            }
            else // default box
            {
                const float length = 0.5f;
                min = Vector3.One * -length;
                max = Vector3.One * length;
            }

            min = Vector3.Transform(min, Owner.Coordinates.WorldMatrix);
            max = Vector3.Transform(max, Owner.Coordinates.WorldMatrix);

            return new BoundingBox(min, max);
        }
    }
#endif
}