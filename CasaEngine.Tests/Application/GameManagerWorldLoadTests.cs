using System;
using CasaEngine.Framework.Application;
using Microsoft.Xna.Framework;
using Xunit;

namespace CasaEngine.Tests.Application;

/// <summary>
/// Pins the failure mode of a world load requested by name. Gameplay code can ask for a world with
/// <see cref="GameManager.SetWorldToLoad(string)"/>, which defers to the next
/// <see cref="GameManager.UpdateWorld"/>; a path missing from the asset catalog used to surface one
/// frame later as a NullReferenceException that named nothing.
/// </summary>
public class GameManagerWorldLoadTests
{
    [Fact]
    public void UpdateWorld_WithWorldPathMissingFromTheCatalog_ThrowsAndNamesThePath()
    {
        var gameManager = new GameManager(null);
        const string missingWorldPath = @"Maps\No Such Zone\No Such Map-4242\No Such Map-4242.world";
        gameManager.SetWorldToLoad(missingWorldPath);

        var exception = Assert.Throws<InvalidOperationException>(() => gameManager.UpdateWorld(new GameTime()));

        Assert.Contains(missingWorldPath, exception.Message);
    }

    [Fact]
    public void UpdateWorld_WithNoPendingWorldLoad_DoesNotThrow()
    {
        var gameManager = new GameManager(null);

        gameManager.UpdateWorld(new GameTime());

        Assert.Null(gameManager.CurrentWorld);
    }
}
