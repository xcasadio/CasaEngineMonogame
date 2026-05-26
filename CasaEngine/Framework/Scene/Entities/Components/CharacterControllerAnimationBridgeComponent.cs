using System.ComponentModel;
using CasaEngine.Framework.Scene.Entities;

namespace CasaEngine.Framework.Scene.Entities.Components;

[DisplayName("Character controller animation bridge")]
public sealed class CharacterControllerAnimationBridgeComponent : EntityComponent, IWorldSystemDrivenComponent
{
    private CharacterControllerComponent _controller;

    public CharacterControllerLocomotionAnimationData LocomotionData { get; private set; } = CharacterControllerLocomotionAnimationData.Empty;

    public CharacterControllerComponent Controller => _controller;

    public override void Attach(Entity actor)
    {
        base.Attach(actor);
        ResolveController();
    }

    public override void InitializeWithWorld(Scene.World.World world)
    {
        base.InitializeWithWorld(world);
        ResolveController();
    }

    public override void Update(float elapsedTime)
    {
        base.Update(elapsedTime);

        if (_controller == null || _controller.Owner == null)
        {
            ResolveController();
        }

        LocomotionData = _controller == null
            ? CharacterControllerLocomotionAnimationData.Empty
            : CharacterControllerLocomotionAnimationData.From(_controller);
    }

    public override CharacterControllerAnimationBridgeComponent Clone()
    {
        return new CharacterControllerAnimationBridgeComponent();
    }

    private void ResolveController()
    {
        _controller = Owner?.GetComponent<CharacterControllerComponent>();
    }
}