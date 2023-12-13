using Newtonsoft.Json.Linq;
using System.ComponentModel;
using System.Text.Json;
using CasaEngine.Core.Design;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.Scripting;

namespace CasaEngine.Framework.Entities.Components;

[DisplayName("GamePlay")]
public class GamePlayComponent : Component
{
    public override int ComponentId => (int)ComponentIds.GamePlay;

    public ExternalComponent? ExternalComponent { get; set; }

    public override void Initialize(Entity entity)
    {
        base.Initialize(entity);

        entity.OnHit += OnHit;
        entity.OnHitEnded += OnHitEnded;

        ExternalComponent?.Initialize(Owner);
    }

    public override void Update(float elapsedTime)
    {
#if EDITOR
        if (!Owner.Game.GameManager.IsRunningInGameEditorMode && Owner.Game.GameManager.ActiveCamera.Owner != Owner)
        {
            return;
        }
#endif

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
        return new GamePlayComponent { ExternalComponent = ExternalComponent };
    }

    public override void Load(JsonElement element, SaveOption option)
    {
        //base.Load(element);
        if (element.TryGetProperty("external_component", out var externalComponentNode))
        {
            //var type = GameSettings.ScriptLoader.GetTypeFromId(externalComponentId);
            ExternalComponent = GameSettings.ScriptLoader.Load(externalComponentNode);
        }
    }

#if EDITOR

    public override void Save(JObject jObject, SaveOption option)
    {
        base.Save(jObject, option);

        var externalComponentNode = new JObject();
        externalComponentNode.Add("type", ExternalComponent?.ExternalComponentId ?? -1);

        jObject.Add("external_component", externalComponentNode);
    }
#endif
}