using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;
using Xunit;

namespace CasaEngine.Tests.Scene;

/// <summary>
/// The motion and the tint of a sprite component, advanced in Update by the received time. No GPU, no world: the
/// component is used on its own.
/// </summary>
public sealed class StaticSpriteComponentMotionTintTests
{
    private static StaticSpriteComponent CreateAt(float x, float y, float z = 0.0f)
    {
        var sprite = new StaticSpriteComponent();
        sprite.Position = new Vector3(x, y, z);
        return sprite;
    }

    // ---- Motion ----

    [Fact]
    public void Move_AdvancesAtConstantSpeedWithTheReceivedTime()
    {
        var sprite = CreateAt(0, 0, 5);
        sprite.StartMove(new Vector2(100, 0), 50.0f);

        sprite.Update(0.5f);

        Assert.True(sprite.IsMoving);
        Assert.Equal(25.0f, sprite.Position.X, 3);
        Assert.Equal(0.0f, sprite.Position.Y, 3);
        Assert.Equal(5.0f, sprite.Position.Z);
    }

    [Fact]
    public void Move_AlongADiagonal_FollowsTheStraightLine()
    {
        var sprite = CreateAt(10, 10);
        sprite.StartMove(new Vector2(40, 50), 10.0f);

        sprite.Update(2.0f);

        Assert.Equal(10.0f + 20.0f * 0.6f, sprite.Position.X, 3);
        Assert.Equal(10.0f + 20.0f * 0.8f, sprite.Position.Y, 3);
    }

    [Fact]
    public void Move_ArrivesExactlyOnTheTargetAndStops()
    {
        var sprite = CreateAt(0, 0, 3);
        sprite.StartMove(new Vector2(33.3f, -17.7f), 40.0f);

        for (var i = 0; i < 10; i++)
        {
            sprite.Update(0.25f);
        }

        Assert.False(sprite.IsMoving);
        Assert.Equal(33.3f, sprite.Position.X);
        Assert.Equal(-17.7f, sprite.Position.Y);
        Assert.Equal(3.0f, sprite.Position.Z);

        sprite.Update(1.0f);
        Assert.Equal(33.3f, sprite.Position.X);
    }

    [Fact]
    public void Move_WithAStepLargerThanTheDistance_DoesNotOvershoot()
    {
        var sprite = CreateAt(0, 0);
        sprite.StartMove(new Vector2(10, 0), 5.0f);

        sprite.Update(100.0f);

        Assert.Equal(10.0f, sprite.Position.X);
        Assert.False(sprite.IsMoving);
    }

    [Fact]
    public void Move_ReplacedByANewMove_StartsFromTheCurrentPosition()
    {
        var sprite = CreateAt(0, 0);
        sprite.StartMove(new Vector2(100, 0), 10.0f);
        sprite.Update(2.0f);

        // From (20, 0) toward (20, 30): 10 units up after one second.
        sprite.StartMove(new Vector2(20, 30), 10.0f);
        sprite.Update(1.0f);

        Assert.Equal(20.0f, sprite.Position.X, 3);
        Assert.Equal(10.0f, sprite.Position.Y, 3);
    }

    [Fact]
    public void Move_ToTheCurrentPosition_IsSetAtOnce()
    {
        var sprite = CreateAt(7, 8, 9);

        sprite.StartMove(new Vector2(7, 8), 1.0f);

        Assert.False(sprite.IsMoving);
        Assert.Equal(new Vector3(7, 8, 9), sprite.Position);
    }

    [Fact]
    public void Move_ScaledTime_MovesLessForTheSameFrames()
    {
        var normal = CreateAt(0, 0);
        var slow = CreateAt(0, 0);
        normal.StartMove(new Vector2(1000, 0), 100.0f);
        slow.StartMove(new Vector2(1000, 0), 100.0f);

        for (var i = 0; i < 10; i++)
        {
            normal.Update(0.1f);
            slow.Update(0.1f * 0.5f);
        }

        Assert.Equal(100.0f, normal.Position.X, 2);
        Assert.Equal(50.0f, slow.Position.X, 2);
    }

    [Fact]
    public void Move_WithAZeroStep_DoesNotMove()
    {
        var sprite = CreateAt(0, 0);
        sprite.StartMove(new Vector2(10, 0), 5.0f);

        sprite.Update(0.0f);

        Assert.Equal(0.0f, sprite.Position.X);
        Assert.True(sprite.IsMoving);
    }

    [Theory]
    [InlineData(0.0f)]
    [InlineData(-1.0f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void Move_WithAnInvalidSpeed_Throws(float speed)
    {
        var sprite = CreateAt(0, 0);

        Assert.Throws<ArgumentOutOfRangeException>(() => sprite.StartMove(new Vector2(1, 1), speed));
    }

    [Fact]
    public void StopMove_LeavesTheSpriteWhereItIs()
    {
        var sprite = CreateAt(0, 0);
        sprite.StartMove(new Vector2(100, 0), 10.0f);
        sprite.Update(1.0f);

        sprite.StopMove();
        sprite.Update(5.0f);

        Assert.False(sprite.IsMoving);
        Assert.Equal(10.0f, sprite.Position.X, 3);
    }

    // ---- Tint ----

    [Fact]
    public void Tint_IsLinearAndRoundedToTheNearestInteger()
    {
        var sprite = new StaticSpriteComponent { Color = new Color(0, 0, 0, 255) };
        sprite.StartTint(new Color(255, 100, 1, 255), 2.0f);

        sprite.Update(0.5f);

        Assert.True(sprite.IsTinting);
        // 255 * 0.25 = 63.75 -> 64 ; 100 * 0.25 = 25 ; 1 * 0.25 = 0.25 -> 0.
        Assert.Equal(new Color(64, 25, 0, 255), sprite.Color);
    }

    [Fact]
    public void Tint_EndsExactlyOnTheTargetAndStops()
    {
        var sprite = new StaticSpriteComponent { Color = Color.White };
        var target = new Color(12, 34, 56, 78);
        sprite.StartTint(target, 1.0f);

        sprite.Update(0.3f);
        sprite.Update(0.3f);
        sprite.Update(0.3f);
        Assert.True(sprite.IsTinting);
        sprite.Update(0.3f);

        Assert.False(sprite.IsTinting);
        Assert.Equal(target, sprite.Color);
    }

    [Fact]
    public void Tint_OfDurationZero_SetsTheTargetAtOnce()
    {
        var sprite = new StaticSpriteComponent();

        sprite.StartTint(new Color(1, 2, 3, 4), 0.0f);

        Assert.False(sprite.IsTinting);
        Assert.Equal(new Color(1, 2, 3, 4), sprite.Color);
    }

    [Fact]
    public void Tint_ReplacedByANewTint_StartsFromTheCurrentColour()
    {
        var sprite = new StaticSpriteComponent { Color = new Color(0, 0, 0, 255) };
        sprite.StartTint(new Color(200, 200, 200, 255), 1.0f);
        sprite.Update(0.5f);
        Assert.Equal(new Color(100, 100, 100, 255), sprite.Color);

        sprite.StartTint(new Color(0, 0, 0, 255), 1.0f);
        sprite.Update(0.5f);

        Assert.Equal(new Color(50, 50, 50, 255), sprite.Color);
    }

    [Fact]
    public void Tint_ScaledTime_TakesLongerForTheSameFrames()
    {
        var sprite = new StaticSpriteComponent { Color = new Color(0, 0, 0, 255) };
        sprite.StartTint(new Color(100, 0, 0, 255), 1.0f);

        for (var i = 0; i < 10; i++)
        {
            sprite.Update(0.1f * 0.5f);
        }

        Assert.True(sprite.IsTinting);
        Assert.Equal(50, sprite.Color.R);
    }

    [Theory]
    [InlineData(-0.1f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void Tint_WithAnInvalidDuration_Throws(float duration)
    {
        var sprite = new StaticSpriteComponent();

        Assert.Throws<ArgumentOutOfRangeException>(() => sprite.StartTint(Color.Black, duration));
    }

    [Fact]
    public void StopTint_KeepsTheCurrentColour()
    {
        var sprite = new StaticSpriteComponent { Color = new Color(0, 0, 0, 255) };
        sprite.StartTint(new Color(200, 0, 0, 255), 1.0f);
        sprite.Update(0.5f);

        sprite.StopTint();
        sprite.Update(5.0f);

        Assert.False(sprite.IsTinting);
        Assert.Equal(100, sprite.Color.R);
    }

    // ---- Both ----

    [Fact]
    public void StopMotionAndTint_StopsBoth()
    {
        var sprite = CreateAt(0, 0);
        sprite.StartMove(new Vector2(100, 0), 10.0f);
        sprite.StartTint(Color.Black, 1.0f);

        sprite.StopMotionAndTint();
        sprite.Update(1.0f);

        Assert.False(sprite.IsMoving);
        Assert.False(sprite.IsTinting);
        Assert.Equal(0.0f, sprite.Position.X);
        Assert.Equal(Color.White, sprite.Color);
    }

    [Fact]
    public void MoveAndTint_RunTogether()
    {
        var sprite = CreateAt(0, 0);
        sprite.Color = new Color(0, 0, 0, 255);
        sprite.StartMove(new Vector2(10, 0), 10.0f);
        sprite.StartTint(new Color(100, 0, 0, 255), 2.0f);

        sprite.Update(1.0f);

        Assert.False(sprite.IsMoving);
        Assert.Equal(10.0f, sprite.Position.X);
        Assert.True(sprite.IsTinting);
        Assert.Equal(50, sprite.Color.R);
    }

    // ---- Allocation ----

    [Fact]
    public void Update_WithAMotionAndATintInProgress_DoesNotAllocate()
    {
        var sprite = CreateAt(0, 0);
        sprite.StartMove(new Vector2(1_000_000, 500_000), 10.0f);
        sprite.StartTint(Color.Black, 1_000_000.0f);
        for (var i = 0; i < 10; i++)
        {
            sprite.Update(0.016f);
        }

        var before = AllocationWindow.Start();
        for (var i = 0; i < 1000; i++)
        {
            sprite.Update(0.016f);
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.True(sprite.IsMoving);
        Assert.True(sprite.IsTinting);
    }
}
