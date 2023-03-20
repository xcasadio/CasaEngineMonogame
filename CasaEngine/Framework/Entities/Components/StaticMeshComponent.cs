using System.ComponentModel;
using System.Text.Json;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.Game.Components;

namespace CasaEngine.Framework.Entities.Components;

[DisplayName("Static Mesh")]
public class StaticMeshComponent : Component
{
    public static readonly int ComponentId = (int)ComponentIds.Mesh;
    private StaticMeshRendererComponent? _meshRendererComponent;
    private StaticMesh? _mesh;

    //private Effect _effect;
    public StaticMesh? Mesh
    {
        get => _mesh;
        set
        {
            _mesh = value;
            _mesh?.Initialize(EngineComponents.Game.GraphicsDevice);
        }
    }

    public StaticMeshComponent(Entity entity) : base(entity, ComponentId)
    {
    }

    public override void Initialize()
    {
        //load shader in byte
        //_effect = new Effect(Game.EngineComponents.Game.GraphicsDevice, );
        Mesh?.Initialize(EngineComponents.Game.GraphicsDevice);
        _meshRendererComponent = EngineComponents.Game.GetGameComponent<StaticMeshRendererComponent>();
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
        throw new NotImplementedException();
    }
}