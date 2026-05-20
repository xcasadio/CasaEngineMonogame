using CasaEngine.Framework.Particles;
using CasaEngine.Framework.Particles.Authoring;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Particles.Runtime;

public static class ParticleShapeSampler
{
    private const float TwoPi = MathF.PI * 2.0f;
    private const float Epsilon = 0.000001f;

    public static void Sample(ParticleShapeModule shape, ref ParticleRandom random, out Vector3 position, out Vector3 direction)
    {
        ArgumentNullException.ThrowIfNull(shape);

        switch (shape.ShapeType)
        {
            case ParticleShapeType.Circle:
                SampleCircle(shape, ref random, out position, out direction);
                break;

            case ParticleShapeType.Box:
                SampleBox(shape, ref random, out position, out direction);
                break;

            case ParticleShapeType.Sphere:
                SampleSphere(shape, ref random, out position, out direction);
                break;

            case ParticleShapeType.Cone:
                SampleCone(shape, ref random, out position, out direction);
                break;

            case ParticleShapeType.Point:
            default:
                position = Vector3.Zero;
                direction = Vector3.UnitY;
                break;
        }
    }

    private static void SampleCircle(ParticleShapeModule shape, ref ParticleRandom random, out Vector3 position, out Vector3 direction)
    {
        float angle = random.NextFloat(0.0f, TwoPi);
        float radius = MathF.Max(0.0f, shape.Radius);
        float distance = shape.EmitFromShell ? radius : MathF.Sqrt(random.NextFloat01()) * radius;
        position = new Vector3(MathF.Cos(angle) * distance, MathF.Sin(angle) * distance, 0.0f);
        direction = NormalizeOrFallback(position, Vector3.UnitY);
    }

    private static void SampleBox(ParticleShapeModule shape, ref ParticleRandom random, out Vector3 position, out Vector3 direction)
    {
        Vector3 halfSize = new(MathF.Max(0.0f, shape.Size.X) * 0.5f, MathF.Max(0.0f, shape.Size.Y) * 0.5f, MathF.Max(0.0f, shape.Size.Z) * 0.5f);
        position = new Vector3(
            random.NextFloat(-halfSize.X, halfSize.X),
            random.NextFloat(-halfSize.Y, halfSize.Y),
            random.NextFloat(-halfSize.Z, halfSize.Z));

        if (!shape.EmitFromShell)
        {
            direction = NormalizeOrFallback(position, Vector3.UnitY);
            return;
        }

        int face = random.NextInt(0, 5);
        switch (face)
        {
            case 0:
                position.X = halfSize.X;
                direction = Vector3.Right;
                break;
            case 1:
                position.X = -halfSize.X;
                direction = Vector3.Left;
                break;
            case 2:
                position.Y = halfSize.Y;
                direction = Vector3.Up;
                break;
            case 3:
                position.Y = -halfSize.Y;
                direction = Vector3.Down;
                break;
            case 4:
                position.Z = halfSize.Z;
                direction = Vector3.Backward;
                break;
            default:
                position.Z = -halfSize.Z;
                direction = Vector3.Forward;
                break;
        }
    }

    private static void SampleSphere(ParticleShapeModule shape, ref ParticleRandom random, out Vector3 position, out Vector3 direction)
    {
        direction = SampleUnitSphereDirection(ref random);
        float radius = MathF.Max(0.0f, shape.Radius);
        float distance = shape.EmitFromShell ? radius : MathF.Pow(random.NextFloat01(), 1.0f / 3.0f) * radius;
        position = direction * distance;
    }

    private static void SampleCone(ParticleShapeModule shape, ref ParticleRandom random, out Vector3 position, out Vector3 direction)
    {
        position = Vector3.Zero;
        float maxAngleRadians = MathHelper.ToRadians(MathHelper.Clamp(shape.AngleDegrees, 0.0f, 180.0f));
        float cosMax = MathF.Cos(maxAngleRadians);
        float cosTheta = shape.EmitFromShell ? cosMax : random.NextFloat(cosMax, 1.0f);
        float sinTheta = MathF.Sqrt(MathF.Max(0.0f, 1.0f - cosTheta * cosTheta));
        float azimuth = random.NextFloat(0.0f, TwoPi);
        direction = new Vector3(MathF.Cos(azimuth) * sinTheta, cosTheta, MathF.Sin(azimuth) * sinTheta);
        direction = NormalizeOrFallback(direction, Vector3.UnitY);
    }

    private static Vector3 SampleUnitSphereDirection(ref ParticleRandom random)
    {
        float z = random.NextFloat(-1.0f, 1.0f);
        float azimuth = random.NextFloat(0.0f, TwoPi);
        float radius = MathF.Sqrt(MathF.Max(0.0f, 1.0f - z * z));
        return new Vector3(MathF.Cos(azimuth) * radius, MathF.Sin(azimuth) * radius, z);
    }

    private static Vector3 NormalizeOrFallback(Vector3 value, Vector3 fallback)
    {
        float lengthSquared = value.LengthSquared();
        if (lengthSquared <= Epsilon)
        {
            return fallback;
        }

        return value / MathF.Sqrt(lengthSquared);
    }
}