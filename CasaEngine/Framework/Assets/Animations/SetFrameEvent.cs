using System.Diagnostics;

namespace CasaEngine.Framework.Assets.Animations;

public class SetFrameEvent : AnimationEvent
{
    public Guid FrameId { get; set; }

    public override void Activate(Animation anim)
    {
        Debug.Assert(anim != null, "SetFrameEvent::Activate() : Animation is nullptr");

        if (anim is Animation2d pAnim2D)
        {
            pAnim2D.CurrentFrame = FrameId;
        }
        else
        {
            throw new InvalidOperationException($"SetFrameEvent.Activate() : Animation({anim.AnimationData.Name}) is not a Animation2D");
        }
    }
}