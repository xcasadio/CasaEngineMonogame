using CasaEngine.Framework.Scene.World;
using CasaEngine.Framework.Scripting.Coroutines;

namespace CasaEngine.Framework.Gameplay;

public sealed class GameplayContext
{
    public GameplayContext(World world)
    {
        ArgumentNullException.ThrowIfNull(world);

        World = world;
    }

    public World World { get; }

    public CoroutineManager CoroutineManager => World.RuntimeSystems.CoroutineManager;
}