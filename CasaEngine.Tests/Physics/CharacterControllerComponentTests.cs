using System.Reflection;
using CasaEngine.Framework.Application.Components.Physics;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Scene.World;
using Microsoft.Xna.Framework;
using Xunit;

namespace CasaEngine.Tests.Physics;

public class CharacterControllerComponentTests
{
    [Fact]
    public void ValidateDependencies_Throws_WhenComponentIsNotAttached()
    {
        var component = new CharacterControllerComponent();

        Assert.Throws<InvalidOperationException>(component.ValidateDependencies);
    }

    [Fact]
    public void ValidateDependencies_Throws_WhenRootComponentIsMissing()
    {
        var entity = new Entity();
        var component = new CharacterControllerComponent();
        entity.AddComponent(component);

        Assert.Throws<InvalidOperationException>(component.ValidateDependencies);
    }

    [Fact]
    public void ValidateDependencies_Throws_WhenCapsuleCollisionIsMissing()
    {
        var entity = CreateEntityWithRoot();
        var component = new CharacterControllerComponent();
        entity.AddComponent(component);

        Assert.Throws<InvalidOperationException>(component.ValidateDependencies);
    }

    [Fact]
    public void ValidateDependencies_Succeeds_WhenRequiredDependenciesExist()
    {
        using var physicsWorldContext = new PhysicsWorldContext(useExternalViewManagement: false);
        var entity = CreateEntityWithRoot();
        SetWorld(entity, CreateWorld(physicsWorldContext));
        entity.AddComponent(new CapsuleCollisionComponent());
        var component = new CharacterControllerComponent();
        entity.AddComponent(component);

        component.ValidateDependencies();
    }

    [Fact]
    public void SetMoveIntent_ClampsDirectionToUnitLength()
    {
        var component = new CharacterControllerComponent();

        component.SetMoveIntent(new Vector2(2f, 0f));

        Assert.Equal(1f, component.MoveIntent.Length(), precision: 5);
    }

    [Fact]
    public void SetControlMode_DisabledStopsMovement()
    {
        var component = new CharacterControllerComponent();
        component.SetMoveIntent(new Vector2(1f, 0f));

        component.SetControlMode(CharacterControlMode.Disabled);

        Assert.Equal(CharacterControlMode.Disabled, component.ControlMode);
        Assert.Equal(CharacterMovementState.Disabled, component.MovementState);
        Assert.Equal(Vector2.Zero, component.MoveIntent);
        Assert.Equal(Vector3.Zero, component.Velocity);
    }

    [Fact]
    public void Teleport_UpdatesRootPosition_AndClearsMovement()
    {
        var entity = CreateEntityWithRoot();
        var component = new CharacterControllerComponent();
        entity.AddComponent(component);
        component.SetMoveIntent(new Vector2(1f, 0f));

        component.Teleport(new Vector3(1f, 2f, 3f));

        Assert.Equal(new Vector3(1f, 2f, 3f), entity.RootComponent!.Position);
        Assert.Equal(Vector2.Zero, component.MoveIntent);
        Assert.Equal(Vector3.Zero, component.Velocity);
    }

    private static Entity CreateEntityWithRoot()
    {
        return new Entity
        {
            RootComponent = new TestSceneComponent(),
        };
    }

    private static World CreateWorld(IPhysicsWorldContext physicsWorldContext)
    {
        var world = new World();
        typeof(World)
            .GetProperty(nameof(World.PhysicsWorldContext), BindingFlags.Instance | BindingFlags.Public)!
            .SetValue(world, physicsWorldContext);
        return world;
    }

    private static void SetWorld(Entity entity, World world)
    {
        typeof(Entity)
            .GetProperty(nameof(Entity.World), BindingFlags.Instance | BindingFlags.Public)!
            .SetValue(entity, world);
    }

    private sealed class TestSceneComponent : SceneComponent
    {
        public override EntityComponent Clone()
        {
            return new TestSceneComponent();
        }
    }
}