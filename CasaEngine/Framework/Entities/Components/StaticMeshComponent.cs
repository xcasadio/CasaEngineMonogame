using System.ComponentModel;
using System.Text.Json;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.Game.Components;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Entities.Components;

[DisplayName("Static Mesh")]
public class StaticMeshComponent : Component
{
    public static readonly int ComponentId = (int)ComponentIds.Mesh;
    private StaticMeshRendererComponent? _meshRendererComponent;
    private StaticMesh? _mesh;

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

        var camera = GameInfo.Instance.ActiveCamera;
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
#endif
}