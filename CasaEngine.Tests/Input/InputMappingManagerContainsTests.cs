using CasaEngine.Framework.Input;
using Xunit;

namespace CasaEngine.Tests.Input;

public class InputMappingManagerContainsTests
{
    [Fact]
    public void Contains_ReturnsTrue_WhenMappingIsRegistered()
    {
        var manager = new InputMappingManager();
        var mapping = new InputMapping { Name = "Jump" };
        manager.AddInputMapping(mapping);

        Assert.True(manager.Contains("Jump"));
    }

    [Fact]
    public void Contains_ReturnsFalse_WhenMappingIsNotRegistered()
    {
        var manager = new InputMappingManager();

        Assert.False(manager.Contains("Jump"));
    }

    [Fact]
    public void Contains_IsCaseSensitive_LikeExistingLookups()
    {
        var manager = new InputMappingManager();
        var mapping = new InputMapping { Name = "Jump" };
        manager.AddInputMapping(mapping);

        Assert.False(manager.Contains("jump"));
    }

    [Fact]
    public void TryGet_ReturnsMapping_WhenRegistered()
    {
        var manager = new InputMappingManager();
        var mapping = new InputMapping { Name = "Jump" };
        manager.AddInputMapping(mapping);

        Assert.True(manager.TryGet("Jump", out var found));
        Assert.Same(mapping, found);
    }

    [Fact]
    public void TryGet_ReturnsFalseAndNull_WhenNotRegistered()
    {
        var manager = new InputMappingManager();

        Assert.False(manager.TryGet("Jump", out var found));
        Assert.Null(found);
    }
}
