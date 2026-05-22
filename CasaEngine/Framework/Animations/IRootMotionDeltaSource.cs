namespace CasaEngine.Framework.Animations;

public interface IRootMotionDeltaSource
{
    RootMotionMode RootMotionMode { get; set; }

    RootMotionDelta ConsumeRootMotionDelta();
}