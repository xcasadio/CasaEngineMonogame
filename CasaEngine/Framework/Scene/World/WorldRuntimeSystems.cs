using CasaEngine.Framework.Cutscenes;
using CasaEngine.Framework.Scene.CharacterMotion;
using CasaEngine.Framework.Scripting.Coroutines;

namespace CasaEngine.Framework.Scene.World;

public sealed class WorldRuntimeSystems
{
    public WorldRuntimeSystems(World world)
    {
        CharacterMotion = new CharacterMotionSystem(world);
        CutsceneDirector = new CutsceneDirector(world);
    }

    public CharacterMotionSystem CharacterMotion { get; }
    public CoroutineManager CoroutineManager { get; } = new();
    public CutsceneDirector CutsceneDirector { get; }

    public void Update(FrameTime frameTime)
    {
        // Coroutines issue the motion requests that CharacterMotion consumes, so they must run
        // first: in the reverse order a request issued on frame N only takes effect on frame N+1.
        CoroutineManager.Update(new CoroutineUpdateContext(frameTime));
        CharacterMotion.Update(frameTime);
    }

    public void Clear()
    {
        CharacterMotion.Clear();
        CutsceneDirector.Stop();
        CoroutineManager.StopAllCoroutines();
    }
}