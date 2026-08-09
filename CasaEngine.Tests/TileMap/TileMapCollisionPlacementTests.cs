using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;
using Xunit;

namespace CasaEngine.Tests.TileMap;

/// <summary>
/// Verifies that tile collision bodies are placed through the full world transform.
/// The tile offset is expressed in map local pixels, so it must be rotated and scaled like the rendered map;
/// the historical identity path (axis aligned map, scale 1) must keep its exact former positions.
/// </summary>
public class TileMapCollisionPlacementTests
{
    private const int TilePixelSize = 16;

    [Theory]
    // left, top, width, height, expected x, expected y (historical formula: left + width / 2, -top - height / 2, 0)
    [InlineData(0f, 0f, 16f, 16f, 8f, -8f)]
    [InlineData(48f, 32f, 16f, 16f, 56f, -40f)]
    [InlineData(32f, 48f, 32f, 16f, 48f, -56f)]
    [InlineData(20f, 12f, 8f, 4f, 24f, -14f)]
    public void ComputeCollisionWorldPosition_WithIdentityTransform_KeepsHistoricalPositions(
        float left, float top, float width, float height, float expectedX, float expectedY)
    {
        var worldMatrix = Matrix.Identity;

        var position = TileMapComponent.ComputeCollisionWorldPosition(ref worldMatrix, left, top, width, height);

        Assert.Equal(expectedX, position.X);
        Assert.Equal(expectedY, position.Y);
        Assert.Equal(0f, position.Z);
    }

    [Fact]
    public void ComputeCollisionWorldPosition_WithTranslationOnly_KeepsHistoricalPositions()
    {
        var worldMatrix = Matrix.CreateTranslation(100f, -50f, 25f);

        var position = TileMapComponent.ComputeCollisionWorldPosition(ref worldMatrix, 48f, 32f, 16f, 16f);

        // Historical path added the local offset directly to the world translation.
        Assert.Equal(156f, position.X);
        Assert.Equal(-90f, position.Y);
        Assert.Equal(25f, position.Z);
    }

    [Fact]
    public void ComputeCollisionWorldPosition_WithUniformScale_ScalesTheTileOffset()
    {
        const float scale = 0.05f;
        var worldMatrix = Matrix.CreateScale(scale) * Matrix.CreateTranslation(10f, 2f, 0f);

        var position = TileMapComponent.ComputeCollisionWorldPosition(ref worldMatrix, 48f, 32f, 16f, 16f);

        Assert.Equal(10f + 56f * scale, position.X, 4);
        Assert.Equal(2f + -40f * scale, position.Y, 4);
        Assert.Equal(0f, position.Z, 4);
    }

    [Fact]
    public void ComputeCollisionWorldPosition_WithGroundRotation_PlacesBodiesInTheXzPlane()
    {
        // Ground orientation used by the 3d tile map demo: -90 degrees around X maps map local Y onto world -Z.
        var worldMatrix = Matrix.CreateFromQuaternion(
            Quaternion.CreateFromAxisAngle(Vector3.UnitX, -MathHelper.PiOver2));

        var firstRowLeft = TileMapComponent.ComputeCollisionWorldPosition(ref worldMatrix, 0f, 0f, TilePixelSize, TilePixelSize);
        var firstRowRight = TileMapComponent.ComputeCollisionWorldPosition(ref worldMatrix, 3f * TilePixelSize, 0f, TilePixelSize, TilePixelSize);
        var secondRow = TileMapComponent.ComputeCollisionWorldPosition(ref worldMatrix, 0f, TilePixelSize, TilePixelSize, TilePixelSize);
        var thirdRow = TileMapComponent.ComputeCollisionWorldPosition(ref worldMatrix, 0f, 2f * TilePixelSize, TilePixelSize, TilePixelSize);

        // Whole first row stays at a constant world height.
        Assert.Equal(0f, firstRowLeft.Y, 4);
        Assert.Equal(firstRowLeft.Y, firstRowRight.Y, 4);
        Assert.Equal(firstRowLeft.Z, firstRowRight.Z, 4);

        // Columns still spread along world X.
        Assert.Equal(8f, firstRowLeft.X, 4);
        Assert.Equal(56f, firstRowRight.X, 4);

        // Rows now spread along world Z instead of world Y.
        Assert.Equal(0f, secondRow.Y, 4);
        Assert.Equal(0f, thirdRow.Y, 4);
        Assert.Equal(8f, firstRowLeft.Z, 4);
        Assert.Equal(24f, secondRow.Z, 4);
        Assert.Equal(40f, thirdRow.Z, 4);
    }

    [Fact]
    public void ComputeCollisionWorldPosition_WithScaledGroundRotation_CombinesRotationAndScale()
    {
        const float scale = 0.05f;
        var worldMatrix = Matrix.CreateScale(scale)
            * Matrix.CreateFromQuaternion(Quaternion.CreateFromAxisAngle(Vector3.UnitX, -MathHelper.PiOver2))
            * Matrix.CreateTranslation(0f, 1f, 0f);

        var position = TileMapComponent.ComputeCollisionWorldPosition(ref worldMatrix, 3f * TilePixelSize, 2f * TilePixelSize, TilePixelSize, TilePixelSize);

        Assert.Equal(56f * scale, position.X, 4);
        Assert.Equal(1f, position.Y, 4);
        Assert.Equal(40f * scale, position.Z, 4);
    }
}
