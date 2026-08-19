using CasaEngine.Framework.Gameplay;
using CasaEngine.Framework.Physics;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Scene.World;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.Gameplay;

public class LocalMultiplayerTests
{
    private static JObject BuildPlayerStartJson(int? playerIndex = null)
    {
        var json = JObject.Parse("""
        {
            "id": "8f1b1e0a-1d0a-4b8a-9f0a-1d0a4b8a9f0a",
            "name": "PlayerStart",
            "local_transform": {
                "position": { "x": 0, "y": 0, "z": 0 },
                "scale": { "x": 1, "y": 1, "z": 1 },
                "rotation": { "x": 0, "y": 0, "z": 0, "w": 1 }
            },
            "children_component": []
        }
        """);

        if (playerIndex.HasValue)
        {
            json.Add("player_index", playerIndex.Value);
        }

        return json;
    }

    [Fact]
    public void Load_WithoutPlayerIndex_DefaultsToPlayerOne()
    {
        var component = new PlayerStartComponent();

        component.Load(BuildPlayerStartJson());

        Assert.Equal(PlayerIndex.One, component.PlayerIndex);
    }

    [Fact]
    public void Load_WithPlayerIndex_ReadsValue()
    {
        var component = new PlayerStartComponent();

        component.Load(BuildPlayerStartJson((int)PlayerIndex.Two));

        Assert.Equal(PlayerIndex.Two, component.PlayerIndex);
    }

    [Fact]
    public void Clone_CopiesPlayerIndex()
    {
        var component = new PlayerStartComponent { PlayerIndex = PlayerIndex.Two };

        var clone = component.Clone();

        Assert.Equal(PlayerIndex.Two, clone.PlayerIndex);
    }

    [Fact]
    public void CollectLocalPlayerIndices_NoPlayerStarts_ReturnsPlayerOne()
    {
        var entities = new List<Entity> { new Entity(), new Entity() };

        var result = World.CollectLocalPlayerIndices(entities);

        Assert.Equal(new[] { PlayerIndex.One }, result);
    }

    [Fact]
    public void CollectLocalPlayerIndices_DuplicateIndices_AreDeduplicated()
    {
        var entityA = new Entity();
        entityA.AddComponent(new PlayerStartComponent { PlayerIndex = PlayerIndex.Two });
        var entityB = new Entity();
        entityB.AddComponent(new PlayerStartComponent { PlayerIndex = PlayerIndex.Two });
        var entities = new List<Entity> { entityA, entityB };

        var result = World.CollectLocalPlayerIndices(entities);

        Assert.Equal(new[] { PlayerIndex.Two }, result);
    }

    [Fact]
    public void CollectLocalPlayerIndices_MultipleIndices_AreSortedAscending()
    {
        var entityA = new Entity();
        entityA.AddComponent(new PlayerStartComponent { PlayerIndex = PlayerIndex.Two });
        var entityB = new Entity();
        entityB.AddComponent(new PlayerStartComponent { PlayerIndex = PlayerIndex.One });
        var entities = new List<Entity> { entityA, entityB };

        var result = World.CollectLocalPlayerIndices(entities);

        Assert.Equal(new[] { PlayerIndex.One, PlayerIndex.Two }, result);
    }

    [Fact]
    public void FindPlayerStart_FindsMatchingIndexAmongSeveral()
    {
        var entityOne = new Entity();
        entityOne.AddComponent(new PlayerStartComponent { PlayerIndex = PlayerIndex.One });
        var entityTwo = new Entity();
        var expectedComponent = new PlayerStartComponent { PlayerIndex = PlayerIndex.Two };
        entityTwo.AddComponent(expectedComponent);
        var entities = new List<Entity> { entityOne, entityTwo };

        var result = World.FindPlayerStart(entities, PlayerIndex.Two);

        Assert.Same(expectedComponent, result);
    }

    [Fact]
    public void FindPlayerStart_NoMatchingIndex_ReturnsNull()
    {
        var entityOne = new Entity();
        entityOne.AddComponent(new PlayerStartComponent { PlayerIndex = PlayerIndex.One });
        var entities = new List<Entity> { entityOne };

        var result = World.FindPlayerStart(entities, PlayerIndex.Three);

        Assert.Null(result);
    }

    [Fact]
    public void Clone_Entity_GetsFreshId()
    {
        var pawn = new Entity();

        Assert.NotEqual(pawn.Id, pawn.Clone().Id);
    }

    private static Entity CreateEntityWithRoot()
    {
        return new Entity
        {
            RootComponent = new TestSceneComponent(),
        };
    }

    [Fact]
    public void CreateLocalPlayerController_WithCharacterController_ConfiguresControllerAndMovesPawn()
    {
        var world = new World();
        var pawn = CreateEntityWithRoot();
        var characterController = new CharacterControllerComponent();
        pawn.AddComponent(characterController);

        var playerStart = new PlayerStartComponent();
        playerStart.LocalTransform.Position = new Vector3(1f, 2f, 3f);

        var controller = world.CreateLocalPlayerController(PlayerIndex.Two, pawn, playerStart);

        Assert.Contains(controller, world.PlayerControllers);
        Assert.Equal(PlayerIndex.Two, controller.PlayerIndex);
        Assert.Equal(pawn, controller.Pawn);
        Assert.Equal(CharacterControlMode.Player, characterController.ControlMode);
        Assert.Null(controller.Input);
        Assert.Equal(playerStart.LocalTransform.Position, pawn.RootComponent.LocalTransform.Position);
    }

    [Fact]
    public void CreateLocalPlayerController_NullPlayerStart_DoesNotThrowAndLeavesTransformUntouched()
    {
        var world = new World();
        var pawn = CreateEntityWithRoot();
        var originalPosition = new Vector3(5f, 6f, 7f);
        pawn.RootComponent.LocalTransform.Position = originalPosition;

        var controller = world.CreateLocalPlayerController(PlayerIndex.One, pawn, null);

        Assert.Contains(controller, world.PlayerControllers);
        Assert.Equal(originalPosition, pawn.RootComponent.LocalTransform.Position);
    }

    private sealed class TestSceneComponent : SceneComponent
    {
        public override TestSceneComponent Clone() => new();
    }
}
