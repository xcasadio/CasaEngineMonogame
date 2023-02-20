using System.Text.Json;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.Game.Components;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Entities.Components;

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
        _meshRendererComponent = CasaEngine.Framework.Game.Engine.Instance.Game.GetGameComponent<MeshRendererComponent>();
    }

    public override void Update(float elapsedTime)
    {
        var worldViewProj = Owner.Coordinates.WorldMatrix * GameInfo.Instance.ActiveCamera.ViewMatrix *
                            GameInfo.Instance.ActiveCamera.ProjectionMatrix;
        _meshRendererComponent.AddMesh(Mesh, worldViewProj);
    }

    public override void Draw()
    {
        //var worldViewProj = this.Owner.Coordinates.WorldMatrix * GameInfo.Instance.ActiveCamera.ViewMatrix * GameInfo.Instance.ActiveCamera.ProjectionMatrix;
        //_effect.Parameters["WorldViewProj"].SetValue(worldViewProj);
        //Mesh.Draw(_effect);
    }

    public override void Load(JsonElement element)
    {
        throw new NotImplementedException();
    }
}