using System.ComponentModel;
using System.Diagnostics;
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

    //private Effect _effect;
    public StaticMesh? Mesh
    {
        get { return _mesh; }
        set
        {
            _mesh = value;
            _mesh?.Initialize(EngineComponents.Game.GraphicsDevice);
#if EDITOR
            OnPropertyChanged();
#endif
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