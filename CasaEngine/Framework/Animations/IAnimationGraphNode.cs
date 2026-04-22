namespace CasaEngine.Framework.Animations;

public interface IAnimationGraphNode
{
    SkeletonDefinition Skeleton { get; }

    void Evaluate(SkeletonPoseLocal outputPose);
}