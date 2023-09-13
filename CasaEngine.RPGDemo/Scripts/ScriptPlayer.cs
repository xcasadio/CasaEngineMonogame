using System.Text.Json;
using CasaEngine.Core.Design;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.AI.Messaging;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Entities.Components;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.Scripting;
using CasaEngine.RPGDemo.Controllers;
using Microsoft.Xna.Framework;

namespace CasaEngine.RPGDemo.Scripts;

public class ScriptPlayer : ExternalComponent, IScriptCharacter
{
    public override int ExternalComponentId => (int)RpgDemoScriptIds.Player;

    private Entity _entity;
    public Character Character { get; private set; }
    public Controller Controller { get; private set; }

    public override void Initialize(Entity entity, CasaEngineGame game)
    {
        _entity = entity;
        Character = new Character(_entity);
        Controller = new HumanPlayerController(Character, PlayerIndex.One);

        Character.Initialize(game);
        Controller.Initialize(game);
        Character.AnimatedSpriteComponent.AnimationFinished += OnAnimationFinished;
    }

    public override void Update(float elapsedTime)
    {
        Controller.Update(elapsedTime);
        Character.Update(elapsedTime);
    }

    public override void Draw()
    {
    }

    public override void OnHit(Collision collision)
    {
        //Controller.OnHit(collision);
    }

    public override void OnHitEnded(Collision collision)
    {
        //Controller.OnHitEnded(collision);
    }

    private void OnAnimationFinished(object sender, CasaEngine.Framework.Assets.Animations.Animation2d animation2d)
    {
        if (sender is Component component)
        {
            Controller.StateMachine.HandleMessage(new Message(component.Owner.Id, component.Owner.Id,
                (int)MessageType.AnimationChanged, 0.0f, animation2d));
        }
    }

    public override void Load(JsonElement element, SaveOption option)
    {

    }

#if EDITOR

    public override void Save(JObject jObject, SaveOption option)
    {
        base.Save(jObject, option);
    }

#endif
}