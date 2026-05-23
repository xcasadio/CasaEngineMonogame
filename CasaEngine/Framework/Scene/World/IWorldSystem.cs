using CasaEngine.Core.Time;

namespace CasaEngine.Framework.Scene.World;

public interface IWorldSystem
{
    void Update(FrameTime frameTime);

    void Clear();
}