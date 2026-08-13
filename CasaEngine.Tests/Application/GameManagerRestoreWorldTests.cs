using CasaEngine.EditorServices;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Scene.World;
using Xunit;

namespace CasaEngine.Tests.Application;

public class GameManagerRestoreWorldTests
{
    private static World CreateLoadedWorld()
    {
        var world = new World { Name = "EditWorld" };

        EditorWorldEditingService.AddEntityReference(world, new EntityReference
        {
            AssetId = Guid.Empty,
            Entity = new Entity
            {
                Name = "Hero",
                RootComponent = new LightComponent(),
            },
        });

        return world;
    }

    [Fact]
    public void RestoreWorld_ReinstallsWorldAndNotifiesWorldChanged()
    {
        var gameManager = new GameManager(null);
        var world = CreateLoadedWorld();
        bool worldChangedRaised = false;
        gameManager.WorldChanged += (_, _) => worldChangedRaised = true;

        gameManager.RestoreWorld(world);

        Assert.Same(world, gameManager.CurrentWorld);
        Assert.True(worldChangedRaised);
    }

    [Fact]
    public void RestoreWorld_DoesNotTouchWorldEntities()
    {
        var gameManager = new GameManager(null);
        var world = CreateLoadedWorld();
        var entityBefore = Assert.Single(world.Entities);

        gameManager.RestoreWorld(world);

        var entityAfter = Assert.Single(world.Entities);
        Assert.Same(entityBefore, entityAfter);
    }

    [Fact]
    public void RestoreWorld_CancelsPendingWorldLoad()
    {
        var gameManager = new GameManager(null);
        var playWorld = new World { Name = "PlayWorld" };
        var editWorld = CreateLoadedWorld();

        gameManager.SetWorldToLoad(playWorld);
        Assert.True(gameManager.HasPendingWorldLoad);

        gameManager.RestoreWorld(editWorld);

        Assert.False(gameManager.HasPendingWorldLoad);
        Assert.Same(editWorld, gameManager.CurrentWorld);
    }

    [Fact]
    public void RestoreWorld_NullWorld_Throws()
    {
        var gameManager = new GameManager(null);

        Assert.Throws<ArgumentNullException>(() => gameManager.RestoreWorld(null));
    }
}
