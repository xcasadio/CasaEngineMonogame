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
        CharacterMotion.Update(frameTime);
        CoroutineManager.Update(new CoroutineUpdateContext(frameTime));
    }

    public void Clear()
    {
        CharacterMotion.Clear();
        CutsceneDirector.Stop();
        CoroutineManager.StopAllCoroutines();
    }
}