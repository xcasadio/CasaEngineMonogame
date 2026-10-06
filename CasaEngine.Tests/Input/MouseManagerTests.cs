using CasaEngine.Engine.Input;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace CasaEngine.Tests.Input;

public class MouseManagerTests
{
    [Theory]
    [InlineData(95, 100)]
    [InlineData(100, 95)]
    [InlineData(105, 100)]
    [InlineData(100, 105)]
    [InlineData(95, 95)]
    public void HasMoved_MoveOfTwoPixelsOrMoreInAnyDirection_ReturnsTrue(int x, int y)
    {
        var mouseManager = new MouseManager();
        mouseManager.Update(CreateMouseState(100, 100));

        mouseManager.Update(CreateMouseState(x, y));

        Assert.True(mouseManager.HasMoved);
    }

    [Theory]
    [InlineData(100, 100)]
    [InlineData(99, 100)]
    [InlineData(100, 99)]
    [InlineData(101, 101)]
    public void HasMoved_MoveOfOnePixelOrLess_ReturnsFalse(int x, int y)
    {
        var mouseManager = new MouseManager();
        mouseManager.Update(CreateMouseState(100, 100));

        mouseManager.Update(CreateMouseState(x, y));

        Assert.False(mouseManager.HasMoved);
    }

    private static MouseState CreateMouseState(int x, int y)
    {
        return new MouseState(
            x,
            y,
            0,
            Microsoft.Xna.Framework.Input.ButtonState.Released,
            Microsoft.Xna.Framework.Input.ButtonState.Released,
            Microsoft.Xna.Framework.Input.ButtonState.Released,
            Microsoft.Xna.Framework.Input.ButtonState.Released,
            Microsoft.Xna.Framework.Input.ButtonState.Released);
    }
}
