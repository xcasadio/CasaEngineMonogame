using CasaEngine.Framework.Gameplay;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Xunit;

namespace CasaEngine.Tests.Gameplay;

public class ControllerPossessionTests
{
    private static Entity CreateEntityWithCharacterController(CharacterControlMode initialMode, out CharacterControllerComponent component)
    {
        var entity = new Entity();
        component = new CharacterControllerComponent();
        entity.AddComponent(component);
        component.SetControlMode(initialMode);
        return entity;
    }

    [Fact]
    public void Possess_PlayerController_AppliesPlayerControlMode()
    {
        var entity = CreateEntityWithCharacterController(CharacterControlMode.AI, out var component);
        var controller = new PlayerController();

        controller.Possess(entity);

        Assert.Equal(entity, controller.Pawn);
        Assert.Equal(CharacterControlMode.Player, component.ControlMode);
    }

    [Fact]
    public void UnPossess_PlayerController_RestoresPreviousControlMode()
    {
        var entity = CreateEntityWithCharacterController(CharacterControlMode.AI, out var component);
        var controller = new PlayerController();
        controller.Possess(entity);

        controller.UnPossess();

        Assert.Equal(CharacterControlMode.AI, component.ControlMode);
        Assert.Null(controller.Pawn);
    }

    [Fact]
    public void Possess_NewEntity_ReleasesPreviousEntityFirst()
    {
        var entityA = CreateEntityWithCharacterController(CharacterControlMode.AI, out var componentA);
        var entityB = CreateEntityWithCharacterController(CharacterControlMode.Script, out var componentB);
        var controller = new PlayerController();
        controller.Possess(entityA);

        controller.Possess(entityB);

        Assert.Equal(CharacterControlMode.AI, componentA.ControlMode);
        Assert.Equal(CharacterControlMode.Player, componentB.ControlMode);
        Assert.Equal(entityB, controller.Pawn);
    }

    [Fact]
    public void Possess_EntityWithoutCharacterControllerComponent_SetsAndClearsPawnWithoutException()
    {
        var entity = new Entity();
        var controller = new PlayerController();

        controller.Possess(entity);
        Assert.Equal(entity, controller.Pawn);

        controller.UnPossess();
        Assert.Null(controller.Pawn);
    }

    [Fact]
    public void Possess_AIController_AppliesAIControlMode()
    {
        var entity = CreateEntityWithCharacterController(CharacterControlMode.Player, out var component);
        var controller = new AIController();

        controller.Possess(entity);
        Assert.Equal(CharacterControlMode.AI, component.ControlMode);

        controller.UnPossess();
        Assert.Equal(CharacterControlMode.Player, component.ControlMode);
    }

    [Fact]
    public void Possess_SameEntityTwice_IsNoOpAndDoesNotRecaptureMode()
    {
        var entity = CreateEntityWithCharacterController(CharacterControlMode.AI, out var component);
        var controller = new PlayerController();
        controller.Possess(entity);
        Assert.Equal(CharacterControlMode.Player, component.ControlMode);

        controller.Possess(entity);

        controller.UnPossess();

        Assert.Equal(CharacterControlMode.AI, component.ControlMode);
    }

    [Fact]
    public void Possess_BaseController_LeavesControlModeUntouched()
    {
        var entity = CreateEntityWithCharacterController(CharacterControlMode.Script, out var component);
        var controller = new Controller();

        controller.Possess(entity);

        Assert.Equal(entity, controller.Pawn);
        Assert.Equal(CharacterControlMode.Script, component.ControlMode);

        controller.UnPossess();

        Assert.Equal(CharacterControlMode.Script, component.ControlMode);
        Assert.Null(controller.Pawn);
    }

    [Fact]
    public void Possess_NullEntity_ThrowsArgumentNullException()
    {
        var controller = new PlayerController();

        Assert.Throws<ArgumentNullException>(() => controller.Possess(null!));
    }
}
