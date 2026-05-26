namespace CasaEngine.Framework.Animations;

public sealed class AnimationLayer
{
    public AnimationLayer(int index, SkeletonDefinition skeleton)
    {
        Index = index;
        Mask = BoneMask.CreateFullBody(skeleton);
        Weight = 1f;
        BlendMode = AnimationLayerBlendMode.Override;
    }

    public int Index { get; }

    public AnimationState State { get; private set; }

    public BoneMask Mask { get; private set; }

    public float Weight { get; private set; }

    public AnimationLayerBlendMode BlendMode { get; private set; }

    public bool Enabled { get; private set; }

    public void Configure(AnimationClip clip, BoneMask mask, float weight, AnimationLayerBlendMode blendMode, bool loop, float speed)
    {
        State = new AnimationState(clip, loop, speed);
        Mask = mask ?? BoneMask.CreateFullBody(clip.Skeleton);
        Weight = Math.Clamp(weight, 0f, 1f);
        BlendMode = blendMode;
        Enabled = true;
    }

    public void Clear()
    {
        State = null;
        Weight = 1f;
        BlendMode = AnimationLayerBlendMode.Override;
        Enabled = false;
    }

    public void SetWeight(float weight)
    {
        Weight = Math.Clamp(weight, 0f, 1f);
    }

    public void Pause()
    {
        State?.Pause();
    }

    public void Resume()
    {
        State?.Resume();
    }

    public void Update(float elapsedSeconds)
    {
        State?.Update(elapsedSeconds);
    }
}