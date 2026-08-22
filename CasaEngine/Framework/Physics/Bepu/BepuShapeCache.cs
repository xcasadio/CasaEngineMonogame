using System;
using System.Collections.Generic;
using BepuPhysics;
using BepuPhysics.Collidables;
using CasaEngine.Engine.Geometry;
using Microsoft.Xna.Framework;
using GeometryBox = CasaEngine.Engine.Geometry.Box;
using GeometrySphere = CasaEngine.Engine.Geometry.Sphere;
using GeometryCapsule = CasaEngine.Engine.Geometry.Capsule;
using GeometryCylinder = CasaEngine.Engine.Geometry.Cylinder;
using BepuBox = BepuPhysics.Collidables.Box;
using BepuSphere = BepuPhysics.Collidables.Sphere;
using BepuCapsule = BepuPhysics.Collidables.Capsule;
using BepuCylinder = BepuPhysics.Collidables.Cylinder;

namespace CasaEngine.Framework.Physics.Bepu;

/// <summary>
/// Lowers engine shapes to Bepu convex shapes, caching by (type, cooked dimensions) with a refcount so
/// tilemaps and other repeated shapes do not allocate one Bepu shape per instance. Bepu has no shape
/// scaling: the local scale is baked into the dimensions (and, for compound children, into the offset)
/// at creation time.
/// </summary>
internal sealed class BepuShapeCache
{
    private readonly struct Key : IEquatable<Key>
    {
        public readonly Shape3dType Type;
        public readonly float A;
        public readonly float B;
        public readonly float C;

        public Key(Shape3dType type, float a, float b, float c)
        {
            Type = type;
            A = a;
            B = b;
            C = c;
        }

        public bool Equals(Key other) => Type == other.Type && A.Equals(other.A) && B.Equals(other.B) && C.Equals(other.C);

        public override bool Equals(object obj) => obj is Key other && Equals(other);

        public override int GetHashCode() => HashCode.Combine((int)Type, A, B, C);
    }

    private readonly Dictionary<Key, TypedIndex> _lookup = new();
    private readonly Dictionary<TypedIndex, Key> _keyByIndex = new();
    private readonly Dictionary<TypedIndex, int> _refCounts = new();
    private readonly Simulation _simulation;

    public BepuShapeCache(Simulation simulation)
    {
        _simulation = simulation;
    }

    /// <summary>Final, post-scale dimensions of a shape, as this cache would key and build it.</summary>
    public static Vector3 ComputeDimensions(Shape3d shape, Vector3 localScale)
    {
        ValidateScale(localScale);

        return shape switch
        {
            GeometryBox box => box.Size * localScale,
            GeometrySphere sphere => new Vector3(sphere.Radius * MaxOf(localScale)),
            GeometryCapsule capsule => new Vector3(capsule.Radius * MaxOf(localScale.X, localScale.Z), capsule.Length * localScale.Y, 0f),
            GeometryCylinder cylinder => new Vector3(cylinder.Radius * MaxOf(localScale.X, localScale.Z), cylinder.Length * localScale.Y, 0f),
            _ => throw new ArgumentOutOfRangeException(nameof(shape), shape.Type, "This kind of shape cannot be lowered to a Bepu shape.")
        };
    }

    /// <summary>Mass properties of a shape at the given mass; used for dynamic bodies (compound
    /// dynamic bodies approximate with their first fixture's shape, documented in the migration report).</summary>
    public static BodyInertia ComputeInertia(Shape3d shape, Vector3 localScale, float mass)
    {
        var dimensions = ComputeDimensions(shape, localScale);

        return shape switch
        {
            GeometryBox => new BepuBox(dimensions.X, dimensions.Y, dimensions.Z).ComputeInertia(mass),
            GeometrySphere => new BepuSphere(dimensions.X).ComputeInertia(mass),
            GeometryCapsule => new BepuCapsule(dimensions.X, dimensions.Y).ComputeInertia(mass),
            GeometryCylinder => new BepuCylinder(dimensions.X, dimensions.Y).ComputeInertia(mass),
            _ => throw new ArgumentOutOfRangeException(nameof(shape), shape.Type, "This kind of shape cannot be lowered to a Bepu shape.")
        };
    }

    public TypedIndex GetOrAdd(Shape3d shape, Vector3 localScale)
    {
        ArgumentNullException.ThrowIfNull(shape);

        var dimensions = ComputeDimensions(shape, localScale);
        var key = new Key(shape.Type, dimensions.X, dimensions.Y, dimensions.Z);

        if (_lookup.TryGetValue(key, out var index))
        {
            _refCounts[index]++;
            return index;
        }

        index = Add(shape, dimensions);
        _lookup[key] = index;
        _keyByIndex[index] = key;
        _refCounts[index] = 1;
        return index;
    }

    /// <summary>Releases one reference to a shape previously obtained from this cache.</summary>
    public void Release(TypedIndex index)
    {
        if (!_refCounts.TryGetValue(index, out var count))
        {
            return;
        }

        if (count <= 1)
        {
            _simulation.Shapes.Remove(index);
            _refCounts.Remove(index);
            if (_keyByIndex.Remove(index, out var key))
            {
                _lookup.Remove(key);
            }
        }
        else
        {
            _refCounts[index] = count - 1;
        }
    }

    private TypedIndex Add(Shape3d shape, Vector3 dimensions)
    {
        return shape switch
        {
            GeometryBox => _simulation.Shapes.Add(new BepuBox(dimensions.X, dimensions.Y, dimensions.Z)),
            GeometrySphere => _simulation.Shapes.Add(new BepuSphere(dimensions.X)),
            GeometryCapsule => _simulation.Shapes.Add(new BepuCapsule(dimensions.X, dimensions.Y)),
            GeometryCylinder => _simulation.Shapes.Add(new BepuCylinder(dimensions.X, dimensions.Y)),
            _ => throw new ArgumentOutOfRangeException(nameof(shape), shape.Type, "This kind of shape cannot be lowered to a Bepu shape.")
        };
    }

    private static void ValidateScale(Vector3 localScale)
    {
        if (localScale.X <= 0f || localScale.Y <= 0f || localScale.Z <= 0f)
        {
            throw new ArgumentException("A physics shape requires a strictly positive local scale on every axis.", nameof(localScale));
        }
    }

    private static float MaxOf(Vector3 v) => MathF.Max(v.X, MathF.Max(v.Y, v.Z));

    private static float MaxOf(float a, float b) => MathF.Max(a, b);
}
