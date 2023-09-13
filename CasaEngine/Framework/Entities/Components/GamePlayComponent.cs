using Newtonsoft.Json.Linq;
using System.ComponentModel;
using System.Text.Json;
using CasaEngine.Core.Design;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.Scripting;
using static Assimp.Metadata;

namespace CasaEngine.Framework.Entities.Components;

[DisplayName("GamePlay")]
public class GamePlayComponent : Component
{
    public override int ComponentId => (int)ComponentIds.GamePlay;

    private ExternalComponent? _externalComponent;

    public ExternalComponent? ExternalComponent
    {
        get => _externalComponent;
        set => _externalComponent = value;
    }

    public override void Initialize(Entity entity, CasaEngineGame game)
    {
        base.Initialize(entity, game);

        entity.OnHit += OnHit;
        entity.OnHitEnded += OnHitEnded;

        ExternalComponent?.Initialize(Owner, game);
    }

    public override void Update(float elapsedTime)
    {
        ExternalComponent?.Update(elapsedTime);
    }

    public override void Draw()
    {
        ExternalComponent?.Draw();
    }

    private void OnHit(object? sender, EventCollisionArgs e)
    {
        ExternalComponent?.OnHit(e.Collision);
    }

    private void OnHitEnded(object? sender, EventCollisionArgs e)
    {
        ExternalComponent?.OnHitEnded(e.Collision);
    }

    public override Component Clone()
    {
        var component = new GamePlayComponent();

        component._externalComponent = _externalComponent;

        return component;
    }

    public override void Load(JsonElement element, SaveOption option)
    {
        //base.Load(element);
        var externalComponentId = element.GetProperty("external_component_id").GetInt32();

        if (externalComponentId != -1)
        {

        }
    }

#if EDITOR

    public override void Save(JObject jObject, SaveOption option)
    {
        base.Save(jObject, option);

        jObject.Add("external_component_id", ExternalComponent == null ? -1 : ExternalComponent.ExternalComponentId);
    }
#endif
}