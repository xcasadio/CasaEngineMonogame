using CasaEngine.Framework.AI.Navigation;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;
using Xunit;

namespace CasaEngine.Tests.AI.Navigation;

public sealed class CharacterControllerNavigationBridgeTests
{
    [Fact]
    public void SteeringBridge_ConvertsAgentDesiredVelocityToControllerIntent()
    {
        Entity entity = CreateControlledEntity(out CharacterControllerComponent controller);
        var agent = new SteeringAgentComponent();
        agent.Settings.MaxForce = 10f;
        agent.Settings.MaxSpeed = 10f;
        agent.Settings.OutputMode = SteeringOutputMode.DesiredVelocity;
        agent.RegisterBehavior(new ConstantSteeringBehavior(new Vector3(0.5f, 0.25f, 0f)));
        var bridge = new CharacterControllerSteeringBridgeComponent();
        entity.AddComponent(agent);
        entity.AddComponent(bridge);

        agent.Update(0.1f);
        bridge.Update(0.1f);

        Assert.Equal(CharacterControlMode.AI, controller.ControlMode);
        Assert.Equal(new Vector2(0.5f, -0.25f), controller.MoveIntent);
        Assert.Equal(controller.MoveIntent, bridge.LastMoveIntent);
    }

    [Fact]
    public void NavigationDriver_MoveToTakesAiAuthorityAndRestoresWhenReached()
    {
        Entity entity = CreateControlledEntity(out CharacterControllerComponent controller);
        var driver = new CharacterControllerNavigationDriverComponent();
        entity.AddComponent(driver);

        driver.MoveTo(new Vector3(1f, 0f, 0f), stoppingDistance: 0.05f);

        Assert.True(driver.IsMoving);
        Assert.Equal(CharacterControlMode.AI, controller.ControlMode);

        AdvanceNavigation(controller, driver, 8);

        Assert.False(driver.IsMoving);
        Assert.True(driver.HasReachedDestination);
        Assert.Equal(CharacterControlMode.Player, controller.ControlMode);
        Assert.Equal(Vector2.Zero, controller.MoveIntent);
        Assert.InRange(entity.RootComponent!.Position.X, 0.95f, 1.05f);
    }

    [Fact]
    public void NavigationDriver_SetPathAdvancesWaypoints()
    {
        Entity entity = CreateControlledEntity(out CharacterControllerComponent controller);
        var driver = new CharacterControllerNavigationDriverComponent();
        entity.AddComponent(driver);
        var waypoints = new List<Vector3>
        {
            new(0.2f, 0f, 0f),
            new(1f, 0f, 0f),
        };

        driver.SetPath(waypoints, stoppingDistance: 0.25f);
        driver.Update(0.1f);

        Assert.True(driver.IsMoving);
        Assert.Equal(1, driver.CurrentWaypointIndex);

        AdvanceNavigation(controller, driver, 8);

        Assert.True(driver.HasReachedDestination);
        Assert.Equal(CharacterControlMode.Player, controller.ControlMode);
    }

    [Fact]
    public void NavigationDriverIntegration_ConvertsPathToMoveIntent()
    {
        Entity entity = CreateControlledEntity(out CharacterControllerComponent controller);
        entity.RootComponent!.Position = new Vector3(0.5f, 0f, 0.5f);
        var driver = new CharacterControllerNavigationDriverComponent();
        var navigationAgent = new NavigationAgentComponent
        {
            NavigationMap = CreateGroundGrid(3, 1),
            Query = new NavigationQuery { LayerMask = NavigationLayerMask.Ground },
            StoppingDistance = 0.05f,
        };
        entity.AddComponent(driver);
        entity.AddComponent(navigationAgent);

        navigationAgent.MoveTo(new Vector3(2.5f, 0f, 0.5f));
        bool pathRequested = navigationAgent.RequestPath();
        driver.Update(0.1f);
        driver.Update(0.1f);

        Assert.True(pathRequested);
        Assert.True(navigationAgent.HasPath);
        Assert.Equal(CharacterControlMode.AI, controller.ControlMode);
        Assert.Equal(new Vector2(1f, 0f), controller.MoveIntent);
        Assert.Equal(controller.MoveIntent, driver.LastMoveIntent);
    }

    [Fact]
    public void NavigationDriver_FollowTargetUpdatesIntentTowardTargetPosition()
    {
        CreateControlledEntity(out CharacterControllerComponent controller, out CharacterControllerNavigationDriverComponent driver);
        Entity target = CreateEntityWithRoot();
        target.RootComponent!.Position = new Vector3(0f, 0f, 1f);

        driver.FollowTarget(target, stoppingDistance: 0.05f);
        driver.Update(0.1f);

        Assert.True(driver.IsMoving);
        Assert.Equal(new Vector2(0f, -1f), controller.MoveIntent);
    }

    private static Entity CreateControlledEntity(out CharacterControllerComponent controller)
    {
        var entity = CreateEntityWithRoot();
        controller = new CharacterControllerComponent
        {
            Settings = new CharacterControllerSettings
            {
                MaxHorizontalSpeed = 10f,
                Acceleration = 100f,
                Deceleration = 100f,
                Gravity = 0f,
            }
        };
        entity.AddComponent(controller);
        return entity;
    }

    private static Entity CreateControlledEntity(out CharacterControllerComponent controller, out CharacterControllerNavigationDriverComponent driver)
    {
        Entity entity = CreateControlledEntity(out controller);
        driver = new CharacterControllerNavigationDriverComponent();
        entity.AddComponent(driver);
        return entity;
    }

    private static Entity CreateEntityWithRoot()
    {
        return new Entity
        {
            RootComponent = new TestSceneComponent(),
        };
    }

    private static NavigationGrid2D CreateGroundGrid(int width, int height)
    {
        var grid = new NavigationGrid2D(width, height, 1f);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                grid.SetCell(x, y, new NavigationGridCell(true, 1f, NavigationLayerMask.Ground));
            }
        }

        return grid;
    }

    private static void AdvanceNavigation(CharacterControllerComponent controller, CharacterControllerNavigationDriverComponent driver, int frameCount)
    {
        for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
        {
            driver.Update(0.1f);
            controller.Update(0.1f);
            if (!driver.IsMoving)
            {
                return;
            }
        }
    }

    private sealed class ConstantSteeringBehavior : SteeringBehaviorRuntime
    {
        private readonly Vector3 _force;

        public ConstantSteeringBehavior(Vector3 force)
            : base("constant")
        {
            _force = force;
        }

        protected override Vector3 Calculate(SteeringAgentKinematics kinematics, SteeringAgentComponent agent, float elapsedTime)
        {
            return _force;
        }

        public override SteeringBehaviorRuntime Clone()
        {
            return new ConstantSteeringBehavior(_force)
            {
                IsEnabled = IsEnabled,
                Weight = Weight,
            };
        }
    }

    private sealed class TestSceneComponent : SceneComponent
    {
        public override EntityComponent Clone()
        {
            return new TestSceneComponent();
        }
    }
}