using CasaEngine.Engine.Physics;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Application.Components.Physics;

public class PhysicsSystemComponent : GameComponent
{
    private readonly CasaEngineGame _casaEngineGame;
    private readonly Dictionary<Scene.World.World, PhysicsWorld> _physicsWorldContexts = [];
    private readonly List<Scene.World.World> _worldsToUpdate = [];

    public PhysicsSystemComponent(CasaEngineGame game) : base(game)
    {
        _casaEngineGame = Game as CasaEngineGame;
        game.Components.Add(this);
        UpdateOrder = (int)ComponentUpdateOrder.Physics;
    }

    public PhysicsWorld GetOrCreateContext(Scene.World.World world)
    {
        ArgumentNullException.ThrowIfNull(world);

        if (_physicsWorldContexts.TryGetValue(world, out var context))
        {
            return context;
        }

        context = new PhysicsWorld(
            world.ExecutionPolicy?.UseExternalViewManagement == true,
            ResolveSpacePolicy(world));
        _physicsWorldContexts.Add(world, context);
        return context;
    }

    /// <summary>
    /// Simulation space of a world: the one it names, or the project default when it names none.
    /// This is the single place a physics context is built, so it is the single place the policy is resolved.
    /// </summary>
    private static SimulationSpacePolicy ResolveSpacePolicy(Scene.World.World world)
    {
        return string.IsNullOrEmpty(world.SpacePolicyName)
            ? GameSettings.PhysicsEngineSettings.SpacePolicy
            : SimulationSpacePolicy.CreateByName(world.SpacePolicyName);
    }

    public void ReleaseContext(Scene.World.World world)
    {
        ArgumentNullException.ThrowIfNull(world);

        if (_physicsWorldContexts.Remove(world, out var context))
        {
            context.Dispose();
        }
    }

    public override void Update(GameTime gameTime)
    {
        if (_casaEngineGame?.ExecutionPolicy.UpdatePhysicsEngine == false)
        {
            return;
        }

        // Physics follows the gameplay time scale so pause (TimeScale = 0) and
        // slow motion stay coherent with scripts and animations.
        float timeScale = _casaEngineGame?.GameManager.TimeScale ?? 1f;
        float elapsedTime = GameTimeHelper.ConvertElapsedTimeToSeconds(gameTime) * timeScale;
        if (elapsedTime <= 0f)
        {
            return;
        }

        _worldsToUpdate.Clear();

        if (_casaEngineGame != null)
        {
            var views = _casaEngineGame.GameManager.ViewManager.Views;
            for (int i = 0; i < views.Count; i++)
            {
                AddWorldToUpdate(views[i].World);
            }

            var currentWorld = _casaEngineGame.GameManager.CurrentWorld;
            if (currentWorld != null)
            {
                AddWorldToUpdate(currentWorld);
            }
        }

        for (int i = 0; i < _worldsToUpdate.Count; i++)
        {
            var world = _worldsToUpdate[i];

            // A world whose own policy says the game does not step it is driven by something else:
            // skip it before its physics context is even requested.
            if (world.ExecutionPolicy?.UpdatePhysicsEngine == false)
            {
                continue;
            }

            GetOrCreateContext(world).Update(elapsedTime);
        }
    }

    private void AddWorldToUpdate(Scene.World.World world)
    {
        if (!_worldsToUpdate.Contains(world))
        {
            _worldsToUpdate.Add(world);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            foreach (var context in _physicsWorldContexts.Values)
            {
                context.Dispose();
            }

            _physicsWorldContexts.Clear();
            _worldsToUpdate.Clear();
        }

        base.Dispose(disposing);
    }
}