using CasaEngine.Framework.Scene.World;
using CasaEngine.Framework.Scripting.Coroutines;

namespace CasaEngine.Framework.Gameplay;

public sealed class GameplayContext
{
    public GameplayContext(World world)
        : this(world, new GameplayEventBus())
    {
    }

    public GameplayContext(World world, GameplayEventBus events)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(events);

        World = world;
        Events = events;
    }

    public World World { get; }

    public GameplayEventBus Events { get; }

    public CoroutineManager CoroutineManager => World.RuntimeSystems.CoroutineManager;
}