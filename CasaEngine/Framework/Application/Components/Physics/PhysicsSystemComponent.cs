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

        context = new PhysicsWorld(_casaEngineGame?.ExecutionPolicy.UseExternalViewManagement == true);
        _physicsWorldContexts.Add(world, context);
        return context;
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

        float elapsedTime = GameTimeHelper.ConvertElapsedTimeToSeconds(gameTime);
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
            GetOrCreateContext(_worldsToUpdate[i]).Update(elapsedTime);
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