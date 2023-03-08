using System.ComponentModel;
using System.Text.Json;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.Game.Components;

namespace CasaEngine.Framework.Entities.Components;

[DisplayName("Mesh")]
public class MeshComponent : Component
{
    public static readonly int ComponentId = (int)ComponentIds.Mesh;
    private MeshRendererComponent? _meshRendererComponent;

    //private Effect _effect;
    public Mesh Mesh { get; set; }

    public MeshComponent(Entity entity) : base(entity, ComponentId)
    {
    }

    public override void Initialize()
    {
        //load shader in byte
        //_effect = new Effect(Game.Engine.Instance.Game.GraphicsDevice, );
        Mesh.Initialize(Game.Engine.Instance.Game.GraphicsDevice);
        _meshRendererComponent = Game.Engine.Instance.Game.GetGameComponent<MeshRendererComponent>();
    }

    public override void Update(float elapsedTime)
    {
        var camera = GameInfo.Instance.ActiveCamera;
        var worldViewProj = Owner.Coordinates.WorldMatrix * camera.ViewMatrix * camera.ProjectionMatrix;
        _meshRendererComponent.AddMesh(Mesh, worldViewProj);
    }

    public override void Load(JsonElement element)
    {
        throw new NotImplementedException();
    }
}