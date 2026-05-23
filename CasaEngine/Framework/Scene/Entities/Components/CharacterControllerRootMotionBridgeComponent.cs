using System.ComponentModel;
using CasaEngine.Framework.Animations;
using CasaEngine.Framework.Scene.Entities;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Scene.Entities.Components;

[DisplayName("Character controller root motion bridge")]
public sealed class CharacterControllerRootMotionBridgeComponent : EntityComponent
{
    private CharacterControllerComponent _controller;
    private IRootMotionDeltaSource _rootMotionSource;

    public bool ForceApplyRootMotionMode { get; set; } = true;

    public bool ApplyRotation { get; set; }

    public CharacterControllerComponent Controller => _controller;

    public IRootMotionDeltaSource RootMotionSource => _rootMotionSource;

    public RootMotionDelta LastConsumedRootMotionDelta { get; private set; } = RootMotionDelta.Identity;

    public Vector3 LastAppliedDisplacement { get; private set; }

    public override int UpdateOrder => (int)EntityComponentUpdateOrder.BeforeDefault;

    public override void Attach(Entity actor)
    {
        base.Attach(actor);
        ResolveDependencies();
    }

    public override void InitializeWithWorld(Scene.World.World world)
    {
        base.InitializeWithWorld(world);
        ResolveDependencies();
    }

    public override void Update(float elapsedTime)
    {
        base.Update(elapsedTime);

        ResolveDependencies();
        if (_controller == null || _rootMotionSource == null)
        {
            LastConsumedRootMotionDelta = RootMotionDelta.Identity;
            LastAppliedDisplacement = Vector3.Zero;
            return;
        }

        if (ForceApplyRootMotionMode)
        {
            _rootMotionSource.RootMotionMode = RootMotionMode.Apply;
        }

        ApplyRootMotionDelta(_rootMotionSource.ConsumeRootMotionDelta());
    }

    public Vector3 ApplyRootMotionDelta(RootMotionDelta rootMotionDelta)
    {
        LastConsumedRootMotionDelta = rootMotionDelta;
        LastAppliedDisplacement = Vector3.Zero;

        if (_controller == null)
        {
            ResolveDependencies();
        }

        if (_controller == null)
        {
            return Vector3.Zero;
        }

        if (rootMotionDelta.Translation.LengthSquared() > 0f)
        {
            LastAppliedDisplacement = _controller.Move(rootMotionDelta.Translation);
        }

        if (ApplyRotation && rootMotionDelta.Rotation != Quaternion.Identity && _controller.Owner?.RootComponent != null)
        {
            _controller.Owner.RootComponent.Orientation = Quaternion.Normalize(rootMotionDelta.Rotation * _controller.Owner.RootComponent.Orientation);
        }

        return LastAppliedDisplacement;
    }

    public override CharacterControllerRootMotionBridgeComponent Clone()
    {
        return new CharacterControllerRootMotionBridgeComponent
        {
            ForceApplyRootMotionMode = ForceApplyRootMotionMode,
            ApplyRotation = ApplyRotation,
        };
    }

    private void ResolveDependencies()
    {
        _controller = Owner?.GetComponent<CharacterControllerComponent>();
        _rootMotionSource = Owner?.GetComponent<IRootMotionDeltaSource>();
    }
}