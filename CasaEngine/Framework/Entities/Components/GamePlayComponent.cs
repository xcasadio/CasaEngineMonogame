using Newtonsoft.Json.Linq;
using System.ComponentModel;
using System.Text.Json;
using CasaEngine.Framework.Game;

namespace CasaEngine.Framework.Entities.Components;

[DisplayName("GamePlay")]
public class GamePlayComponent : Component
{
    public static readonly int ComponentId = (int)ComponentIds.GamePlay;
    private IExternalComponent? _externalComponent;

    public IExternalComponent? ExternalComponent
    {
        get => _externalComponent;
        set => _externalComponent = value;
    }

    public GamePlayComponent(Entity entity) : base(entity, ComponentId)
    {
    }

    public override void Initialize(CasaEngineGame game)
    {
        ExternalComponent?.Initialize();
    }

    public override void Update(float elapsedTime)
    {
        ExternalComponent?.Update(elapsedTime);
    }

    public override void Draw()
    {
        ExternalComponent?.Draw();
    }

    public override void Load(JsonElement element)
    {
        throw new NotImplementedException();
    }

#if EDITOR

    public override void Save(JObject jObject)
    {
        base.Save(jObject);

        throw new NotImplementedException();
    }
#endif
}