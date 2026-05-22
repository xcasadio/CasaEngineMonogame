using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;
using Xunit;

namespace CasaEngine.Tests.Physics;

public sealed class CharacterControllerMoveToDriverTests
{
    [Fact]
    public void MoveTo_TakesCutsceneAuthorityAndRestoresPreviousModeOnArrival()
    {
        Entity entity = CreateControlledEntity(out CharacterControllerComponent controller, out CharacterControllerMoveToDriverComponent driver);
        controller.SetControlMode(CharacterControlMode.Script);

        driver.MoveTo(new Vector3(1f, 0f, 0f), stoppingDistance: 0.05f);

        Assert.True(driver.IsMoving);
        Assert.Equal(CharacterControlMode.Cutscene, controller.ControlMode);

        AdvanceMoveTo(controller, driver, 8);

        Assert.False(driver.IsMoving);
        Assert.True(driver.HasReachedDestination);
        Assert.False(driver.HasTimedOut);
        Assert.Equal(CharacterControlMode.Script, controller.ControlMode);
        Assert.Equal(Vector2.Zero, controller.MoveIntent);
        Assert.InRange(entity.RootComponent!.Position.X, 0.95f, 1.05f);
    }

    [Fact]
    public void MoveTo_RestoresPreviousModeWhenItTimesOut()
    {
        CreateControlledEntity(out CharacterControllerComponent controller, out CharacterControllerMoveToDriverComponent driver);

        driver.MoveTo(new Vector3(10f, 0f, 0f), stoppingDistance: 0.05f, timeoutSeconds: 0.05f);
        driver.Update(0.1f);

        Assert.False(driver.IsMoving);
        Assert.False(driver.HasReachedDestination);
        Assert.True(driver.HasTimedOut);
        Assert.Equal(CharacterControlMode.Player, controller.ControlMode);
        Assert.Equal(Vector2.Zero, controller.MoveIntent);
    }

    private static Entity CreateControlledEntity(out CharacterControllerComponent controller, out CharacterControllerMoveToDriverComponent driver)
    {
        var entity = new Entity
        {
            RootComponent = new TestSceneComponent(),
        };

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
        driver = new CharacterControllerMoveToDriverComponent();
        entity.AddComponent(controller);
        entity.AddComponent(driver);
        return entity;
    }

    private static void AdvanceMoveTo(CharacterControllerComponent controller, CharacterControllerMoveToDriverComponent driver, int frameCount)
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

    private sealed class TestSceneComponent : SceneComponent
    {
        public override EntityComponent Clone()
        {
            return new TestSceneComponent();
        }
    }
}