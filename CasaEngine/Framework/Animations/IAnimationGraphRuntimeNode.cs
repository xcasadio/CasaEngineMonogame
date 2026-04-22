namespace CasaEngine.Framework.Animations;

public interface IAnimationGraphRuntimeNode : IAnimationGraphNode
{
    void Advance(float elapsedSeconds);
}