namespace CasaEngine.Framework.Assets.Animations;

public class Animation2d
{
    public AnimationData AnimationData => Animation2dData;

    public Animation2dData Animation2dData { get; }

    public Animation2d(Animation2dData animation2dData)
    {
        ArgumentNullException.ThrowIfNull(animation2dData);
        Animation2dData = animation2dData;
    }

    public void Initialize()
    {
    }

    public void Reset()
    {
    }
}