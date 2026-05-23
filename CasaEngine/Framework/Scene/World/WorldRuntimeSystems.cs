using CasaEngine.Core.Time;
using CasaEngine.Framework.Scene.CharacterMotion;

namespace CasaEngine.Framework.Scene.World;

public sealed class WorldRuntimeSystems
{
    public WorldRuntimeSystems(World world)
    {
        CharacterMotion = new CharacterMotionSystem(world);
    }

    public CharacterMotionSystem CharacterMotion { get; }

    public void Update(FrameTime frameTime)
    {
        CharacterMotion.Update(frameTime);
    }

    public void Clear()
    {
        CharacterMotion.Clear();
    }
}