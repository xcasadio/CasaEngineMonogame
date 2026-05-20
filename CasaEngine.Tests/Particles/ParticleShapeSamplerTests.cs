using CasaEngine.Framework.Particles;
using CasaEngine.Framework.Particles.Authoring;
using CasaEngine.Framework.Particles.Runtime;
using Microsoft.Xna.Framework;
using Xunit;

namespace CasaEngine.Tests.Particles;

public class ParticleShapeSamplerTests
{
    [Fact]
    public void Point_ReturnsOriginAndUpDirection()
    {
        var random = new ParticleRandom(1u);
        ParticleShapeSampler.Sample(new ParticleShapeModule
        {
            ShapeType = ParticleShapeType.Point,
        }, ref random, out Vector3 position, out Vector3 direction);

        Assert.Equal(Vector3.Zero, position);
        Assert.Equal(Vector3.UnitY, direction);
    }

    [Fact]
    public void CircleVolume_StaysInsideRadiusWithNormalizedDirection()
    {
        var random = new ParticleRandom(2u);
        var shape = new ParticleShapeModule
        {
            ShapeType = ParticleShapeType.Circle,
            Radius = 3.0f,
        };

        for (int index = 0; index < 64; index++)
        {
            ParticleShapeSampler.Sample(shape, ref random, out Vector3 position, out Vector3 direction);

            Assert.InRange(position.Length(), 0.0f, shape.Radius + 0.0001f);
            Assert.Equal(0.0f, position.Z);
            AssertNormalized(direction);
        }
    }

    [Fact]
    public void CircleShell_StaysOnRadius()
    {
        var random = new ParticleRandom(3u);
        var shape = new ParticleShapeModule
        {
            ShapeType = ParticleShapeType.Circle,
            Radius = 2.0f,
            EmitFromShell = true,
        };

        ParticleShapeSampler.Sample(shape, ref random, out Vector3 position, out Vector3 direction);

        Assert.Equal(shape.Radius, position.Length(), 4);
        AssertNormalized(direction);
    }

    [Fact]
    public void BoxVolume_StaysInsideHalfExtents()
    {
        var random = new ParticleRandom(4u);
        var shape = new ParticleShapeModule
        {
            ShapeType = ParticleShapeType.Box,
            Size = new Vector3(2.0f, 4.0f, 6.0f),
        };

        for (int index = 0; index < 64; index++)
        {
            ParticleShapeSampler.Sample(shape, ref random, out Vector3 position, out Vector3 direction);

            Assert.InRange(position.X, -1.0f, 1.0f);
            Assert.InRange(position.Y, -2.0f, 2.0f);
            Assert.InRange(position.Z, -3.0f, 3.0f);
            AssertNormalized(direction);
        }
    }

    [Fact]
    public void BoxShell_PlacesParticleOnOneFace()
    {
        var random = new ParticleRandom(5u);
        var shape = new ParticleShapeModule
        {
            ShapeType = ParticleShapeType.Box,
            Size = new Vector3(2.0f, 4.0f, 6.0f),
            EmitFromShell = true,
        };

        ParticleShapeSampler.Sample(shape, ref random, out Vector3 position, out Vector3 direction);

        bool onShell = MathF.Abs(MathF.Abs(position.X) - 1.0f) < 0.0001f
            || MathF.Abs(MathF.Abs(position.Y) - 2.0f) < 0.0001f
            || MathF.Abs(MathF.Abs(position.Z) - 3.0f) < 0.0001f;

        Assert.True(onShell);
        AssertNormalized(direction);
    }

    [Fact]
    public void SphereVolume_StaysInsideRadiusWithNormalizedDirection()
    {
        var random = new ParticleRandom(6u);
        var shape = new ParticleShapeModule
        {
            ShapeType = ParticleShapeType.Sphere,
            Radius = 5.0f,
        };

        for (int index = 0; index < 64; index++)
        {
            ParticleShapeSampler.Sample(shape, ref random, out Vector3 position, out Vector3 direction);

            Assert.InRange(position.Length(), 0.0f, shape.Radius + 0.0001f);
            AssertNormalized(direction);
        }
    }

    [Fact]
    public void SphereShell_StaysOnRadius()
    {
        var random = new ParticleRandom(7u);
        var shape = new ParticleShapeModule
        {
            ShapeType = ParticleShapeType.Sphere,
            Radius = 4.0f,
            EmitFromShell = true,
        };

        ParticleShapeSampler.Sample(shape, ref random, out Vector3 position, out Vector3 direction);

        Assert.Equal(shape.Radius, position.Length(), 4);
        AssertNormalized(direction);
    }

    [Fact]
    public void ConeDirection_StaysInsideConfiguredAngle()
    {
        var random = new ParticleRandom(8u);
        var shape = new ParticleShapeModule
        {
            ShapeType = ParticleShapeType.Cone,
            AngleDegrees = 25.0f,
        };
        float cosMax = MathF.Cos(MathHelper.ToRadians(shape.AngleDegrees));

        for (int index = 0; index < 64; index++)
        {
            ParticleShapeSampler.Sample(shape, ref random, out Vector3 position, out Vector3 direction);

            Assert.Equal(Vector3.Zero, position);
            Assert.True(Vector3.Dot(direction, Vector3.UnitY) >= cosMax - 0.0001f);
            AssertNormalized(direction);
        }
    }

    [Fact]
    public void ConeShell_UsesConfiguredAngle()
    {
        var random = new ParticleRandom(9u);
        var shape = new ParticleShapeModule
        {
            ShapeType = ParticleShapeType.Cone,
            AngleDegrees = 30.0f,
            EmitFromShell = true,
        };

        ParticleShapeSampler.Sample(shape, ref random, out _, out Vector3 direction);

        Assert.Equal(MathF.Cos(MathHelper.ToRadians(shape.AngleDegrees)), Vector3.Dot(direction, Vector3.UnitY), 4);
        AssertNormalized(direction);
    }

    private static void AssertNormalized(Vector3 value)
    {
        Assert.Equal(1.0f, value.Length(), 4);
    }
}