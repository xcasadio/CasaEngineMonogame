namespace CasaEngine.Framework.Assets.Animations;

public abstract class AnimationEvent
{
    public uint Id { get; private set; }
    public float Time { get; set; }

    public abstract void Activate(Animation anim);
}