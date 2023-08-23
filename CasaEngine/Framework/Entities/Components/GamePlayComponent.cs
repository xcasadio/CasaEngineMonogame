using Newtonsoft.Json.Linq;
using System.ComponentModel;
using System.Text.Json;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.Scripting;

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
        entity.OnHit += OnHit;
        entity.OnHitEnded += OnHitEnded;
    }

    public override void Initialize(CasaEngineGame game)
    {
        ExternalComponent?.Initialize(game);
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

    public override Component Clone(Entity owner)
    {
        var component = new GamePlayComponent(owner);

        component._externalComponent = _externalComponent;

        return component;
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