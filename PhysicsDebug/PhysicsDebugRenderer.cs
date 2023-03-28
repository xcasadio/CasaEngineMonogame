using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using BepuPhysics;
using BepuPhysics.Collidables;
using BepuUtilities.Collections;
using BepuUtilities.Memory;

namespace PhysicsDebug;

public interface IPhysicsDebugRenderer
{
    void DrawBox(float halfWidth, float halfHeight, float halfLength,
        Microsoft.Xna.Framework.Vector3 boxPosition,
        Microsoft.Xna.Framework.Quaternion boxOrientation,
        Microsoft.Xna.Framework.Vector3 boxColor);

    void DrawSphere(float radius,
        Microsoft.Xna.Framework.Vector3 position,
        Microsoft.Xna.Framework.Quaternion orientation,
        Microsoft.Xna.Framework.Vector3 color);

    void DrawCapsule(float radius, float halfLength,
        Microsoft.Xna.Framework.Vector3 position,
        Microsoft.Xna.Framework.Quaternion orientation,
        Microsoft.Xna.Framework.Vector3 color);

    void DrawTriangle(Microsoft.Xna.Framework.Vector3 a,
        Microsoft.Xna.Framework.Vector3 b,
        Microsoft.Xna.Framework.Vector3 c,
        Microsoft.Xna.Framework.Vector3 position,
        Microsoft.Xna.Framework.Quaternion orientation,
        Microsoft.Xna.Framework.Vector3 color);
}

public class PhysicsDebugRenderer
{
    private readonly Simulation _simulation;
    private readonly IPhysicsDebugRenderer _physicsDebugRenderer;
    private readonly ShapesExtractor _shapesExtractor;

    public PhysicsDebugRenderer(Simulation simulation, IPhysicsDebugRenderer physicsDebugRenderer)
    {
        _simulation = simulation;
        _physicsDebugRenderer = physicsDebugRenderer;
        _shapesExtractor = new ShapesExtractor(new BufferPool());
    }

    public void Update()
    {
        _shapesExtractor.AddShapesSequentially(_simulation);
    }

    public void Render()
    {
        foreach (var box in _shapesExtractor.ShapeCache.Boxes)
        {
            _physicsDebugRenderer.DrawBox(box.HalfWidth, box.HalfHeight, box.HalfLength, box.Position, box.Orientation, box.Color);
        }

        foreach (var sphere in _shapesExtractor.ShapeCache.Spheres)
        {
            _physicsDebugRenderer.DrawSphere(sphere.Radius, sphere.Position, sphere.Orientation, sphere.Color);
        }

        foreach (var capsule in _shapesExtractor.ShapeCache.Capsules)
        {
            _physicsDebugRenderer.DrawCapsule(capsule.Radius, capsule.HalfLength, capsule.Position, capsule.Orientation, capsule.Color);
        }

        foreach (var cylinder in _shapesExtractor.ShapeCache.Cylinders)
        {
            _physicsDebugRenderer.DrawCapsule(cylinder.Radius, cylinder.HalfLength, cylinder.Position, cylinder.Orientation, cylinder.Color);
        }

        //foreach (var mesh in _shapesExtractor.ShapeCache.Meshes)
        //{
        //    _physicsDebugRenderer.DrawMesh(mesh.VertexStart, mesh.Position, mesh.Orientation, mesh.Color);
        //}

        foreach (var triangle in _shapesExtractor.ShapeCache.Triangles)
        {
            _physicsDebugRenderer.DrawTriangle(triangle.A, triangle.B, triangle.C, new Vector3(triangle.X, triangle.Y, triangle.Z),
                triangle.Orientation, triangle.Color);
        }
    }
}

public struct SphereInstance
{
    public Vector3 Position;
    public float Radius;
    public Quaternion Orientation;
    public Vector3 Color;
}

public struct CapsuleInstance
{
    public Vector3 Position;
    public float Radius;
    public Quaternion Orientation;
    public float HalfLength;
    public Vector3 Color;
}

public struct CylinderInstance
{
    public Vector3 Position;
    public float Radius;
    public Quaternion Orientation;
    public float HalfLength;
    public Vector3 Color;
}

public struct BoxInstance
{
    public Vector3 Position;
    public Vector3 Color;
    public Quaternion Orientation;
    public float HalfWidth;
    public float HalfHeight;
    public float HalfLength;
}

public struct TriangleInstance
{
    public Vector3 A;
    public Vector3 Color;
    public Vector3 B;
    public float X;
    public Vector3 C;
    public float Y;
    public Quaternion Orientation;
    public float Z;
}

public struct MeshInstance
{
    public Vector3 Position;
    public Vector3 Color;
    public Quaternion Orientation;
    public int VertexStart;
    public int VertexCount;
    public Vector3 Scale;
}

public struct ShapeCache
{
    public QuickList<SphereInstance> Spheres;
    public QuickList<CapsuleInstance> Capsules;
    public QuickList<CylinderInstance> Cylinders;
    public QuickList<BoxInstance> Boxes;
    public QuickList<TriangleInstance> Triangles;
    public QuickList<MeshInstance> Meshes;

    public ShapeCache(int initialCapacityPerShapeType, BufferPool pool)
    {
        Spheres = new QuickList<SphereInstance>(initialCapacityPerShapeType, pool);
        Capsules = new QuickList<CapsuleInstance>(initialCapacityPerShapeType, pool);
        Cylinders = new QuickList<CylinderInstance>(initialCapacityPerShapeType, pool);
        Boxes = new QuickList<BoxInstance>(initialCapacityPerShapeType, pool);
        Triangles = new QuickList<TriangleInstance>(initialCapacityPerShapeType, pool);
        Meshes = new QuickList<MeshInstance>(initialCapacityPerShapeType, pool);
    }
    public void Clear()
    {
        Spheres.Count = 0;
        Capsules.Count = 0;
        Cylinders.Count = 0;
        Boxes.Count = 0;
        Triangles.Count = 0;
        Meshes.Count = 0;
    }
    public void Dispose(BufferPool pool)
    {
        Spheres.Dispose(pool);
        Capsules.Dispose(pool);
        Cylinders.Dispose(pool);
        Boxes.Dispose(pool);
        Triangles.Dispose(pool);
        Meshes.Dispose(pool);
    }
}

/// <summary>
/// Stores references to triangle data between usages to avoid the need to regather it every frame.
/// </summary>
public class MeshCache : IDisposable
{
    Buffer<Vector3> vertices;
    public List<Vector3> TriangleBuffer;
    QuickSet<ulong, PrimitiveComparer<ulong>> previouslyAllocatedIds;
    QuickList<ulong> requestedIds;

    struct UploadRequest
    {
        public int Start;
        public int Count;
    }
    QuickList<UploadRequest> pendingUploads;

    public BufferPool Pool { get; private set; }
    Allocator allocator;
    public MeshCache(BufferPool pool, int initialSizeInVertices = 1 << 22)
    {
        Pool = pool;
        pool.TakeAtLeast(initialSizeInVertices, out vertices);
        TriangleBuffer = new List<Vector3>(initialSizeInVertices);//, "Mesh Cache Vertex Buffer");
        allocator = new Allocator(initialSizeInVertices, pool);

        pendingUploads = new QuickList<UploadRequest>(128, pool);
        requestedIds = new QuickList<ulong>(128, pool);
        previouslyAllocatedIds = new QuickSet<ulong, PrimitiveComparer<ulong>>(256, pool);
    }

    public bool TryGetExistingMesh(ulong id, out int start, out Buffer<Vector3> vertices)
    {
        if (allocator.TryGetAllocationRegion(id, out var allocation))
        {
            start = (int)allocation.Start;
            vertices = this.vertices.Slice(start, (int)(allocation.End - start));
            return true;
        }
        start = default;
        vertices = default;
        return false;
    }

    public bool Allocate(ulong id, int vertexCount, out int start, out Buffer<Vector3> vertices)
    {
        if (TryGetExistingMesh(id, out start, out vertices))
        {
            return false;
        }
        if (allocator.Allocate(id, vertexCount, out var longStart))
        {
            start = (int)longStart;
            vertices = this.vertices.Slice(start, vertexCount);
            pendingUploads.Add(new UploadRequest { Start = start, Count = vertexCount }, Pool);
            return true;
        }
        //Didn't fit. We need to resize.
        var copyCount = TriangleBuffer.Capacity + vertexCount;
        var newSize = (int)BitOperations.RoundUpToPowerOf2((uint)copyCount);
        Pool.ResizeToAtLeast(ref this.vertices, newSize, copyCount);
        allocator.Capacity = newSize;
        allocator.Allocate(id, vertexCount, out longStart);
        start = (int)longStart;
        vertices = this.vertices.Slice(start, vertexCount);
        //A resize forces an upload of everything, so any previous pending uploads are unnecessary.
        pendingUploads.Count = 0;
        pendingUploads.Add(new UploadRequest { Start = 0, Count = copyCount }, Pool);
        return true;
    }

    public unsafe void FlushPendingUploads()
    {
        if (allocator.Capacity > TriangleBuffer.Capacity)
        {
            //TriangleBuffer.SetCapacityWithoutCopy((int)allocator.Capacity);
        }
        for (int i = 0; i < pendingUploads.Count; ++i)
        {
            ref var upload = ref pendingUploads[i];
            //TriangleBuffer.Update(context, new Span<Vector3>(vertices.Memory, vertices.Length), upload.Count, upload.Start, upload.Start);
            TriangleBuffer.Clear();
            TriangleBuffer.AddRange(new Span<Vector3>(vertices.Memory, vertices.Length).ToArray());
        }
        pendingUploads.Count = 0;

        //Get rid of any stale allocations.
        for (int i = 0; i < requestedIds.Count; ++i)
        {
            previouslyAllocatedIds.FastRemove(requestedIds[i]);
        }
        for (int i = 0; i < previouslyAllocatedIds.Count; ++i)
        {
            allocator.Deallocate(previouslyAllocatedIds[i]);
        }
        previouslyAllocatedIds.FastClear();
        for (int i = 0; i < requestedIds.Count; ++i)
        {
            previouslyAllocatedIds.Add(requestedIds[i], Pool);
        }
        requestedIds.Count = 0;

        //This executes at the end of the frame. The next frame will read the compacted location, which will be valid because the pending upload will be handled.
        if (allocator.IncrementalCompact(out var compactedId, out var compactedSize, out var oldStart, out var newStart))
        {
            vertices.CopyTo((int)oldStart, vertices, (int)newStart, (int)compactedSize);
            pendingUploads.Add(new UploadRequest { Start = (int)newStart, Count = (int)compactedSize }, Pool);
        }

    }

    bool disposed;
    public void Dispose()
    {
        if (!disposed)
        {
            //TriangleBuffer.Dispose();
            pendingUploads.Dispose(Pool);
            Pool.Return(ref vertices);
            requestedIds.Dispose(Pool);
            previouslyAllocatedIds.Dispose(Pool);
            allocator.Dispose();
            disposed = true;
        }
    }

#if DEBUG
    ~MeshCache()
    {
        Helpers.CheckForUndisposed(disposed, this);
    }
#endif
}

public class ShapesExtractor : IDisposable
{
    BufferPool pool;
    public ShapeCache ShapeCache;
    public MeshCache MeshCache;

    public ShapesExtractor(BufferPool pool, int initialCapacityPerShapeType = 1024)
    {
        ShapeCache = new ShapeCache(initialCapacityPerShapeType, pool);
        this.MeshCache = new MeshCache(pool);
        this.pool = pool;
    }

    public void ClearInstances()
    {
        ShapeCache.Clear();
    }

    private void AddCompoundChildren(ref Buffer<CompoundChild> children, Shapes shapes, RigidPose pose, Vector3 color, ref ShapeCache shapeCache, BufferPool pool)
    {
        for (int i = 0; i < children.Length; ++i)
        {
            ref var child = ref children[i];
            Compound.GetWorldPose(child.LocalPose, pose, out var childPose);
            AddShape(shapes, child.ShapeIndex, childPose, color, ref shapeCache, pool);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    unsafe void AddShape(void* shapeData, int shapeType, Shapes shapes, RigidPose pose, Vector3 color, ref ShapeCache shapeCache, BufferPool pool)
    {
        //TODO: This should likely be swapped over to a registration-based virtualized table approach to more easily support custom shape extractors-
        //generic terrain windows and examples like voxel grids would benefit.
        switch (shapeType)
        {
            case Sphere.Id:
                {
                    SphereInstance instance;
                    instance.Position = pose.Position;
                    instance.Radius = Unsafe.AsRef<Sphere>(shapeData).Radius;
                    instance.Orientation = pose.Orientation;
                    instance.Color = color;
                    shapeCache.Spheres.Add(instance, pool);
                }
                break;
            case Capsule.Id:
                {
                    CapsuleInstance instance;
                    instance.Position = pose.Position;
                    ref var capsule = ref Unsafe.AsRef<Capsule>(shapeData);
                    instance.Radius = capsule.Radius;
                    instance.HalfLength = capsule.HalfLength;
                    instance.Orientation = pose.Orientation;
                    instance.Color = color;
                    shapeCache.Capsules.Add(instance, pool);
                }
                break;
            case Box.Id:
                {
                    BoxInstance instance;
                    instance.Position = pose.Position;
                    ref var box = ref Unsafe.AsRef<Box>(shapeData);
                    instance.Color = color;
                    instance.Orientation = pose.Orientation;
                    instance.HalfWidth = box.HalfWidth;
                    instance.HalfHeight = box.HalfHeight;
                    instance.HalfLength = box.HalfLength;
                    shapeCache.Boxes.Add(instance, pool);
                }
                break;
            case Triangle.Id:
                {
                    ref var triangle = ref Unsafe.AsRef<Triangle>(shapeData);
                    TriangleInstance instance;
                    instance.A = triangle.A;
                    instance.Color = color;
                    instance.B = triangle.B;
                    instance.C = triangle.C;
                    instance.Orientation = pose.Orientation;
                    instance.X = pose.Position.X;
                    instance.Y = pose.Position.Y;
                    instance.Z = pose.Position.Z;
                    shapeCache.Triangles.Add(instance, pool);
                }
                break;
            case Cylinder.Id:
                {
                    CylinderInstance instance;
                    instance.Position = pose.Position;
                    ref var cylinder = ref Unsafe.AsRef<Cylinder>(shapeData);
                    instance.Radius = cylinder.Radius;
                    instance.HalfLength = cylinder.HalfLength;
                    instance.Orientation = pose.Orientation;
                    instance.Color = color;
                    shapeCache.Cylinders.Add(instance, pool);
                }
                break;
            case ConvexHull.Id:
                {
                    ref var hull = ref Unsafe.AsRef<ConvexHull>(shapeData);
                    MeshInstance instance;
                    instance.Position = pose.Position;
                    instance.Color = color;
                    instance.Orientation = pose.Orientation;
                    instance.Scale = Vector3.One;
                    //Memory can be reused, so we slightly reduce the probability of a bad reuse by taking the first 64 bits of data into the hash.
                    var id = (ulong)hull.Points.Memory ^ (ulong)hull.Points.Length ^ (*(ulong*)hull.Points.Memory);
                    bool meshExisted;
                    Buffer<Vector3> vertices;
                    lock (MeshCache)
                    {
                        meshExisted = MeshCache.TryGetExistingMesh(id, out instance.VertexStart, out vertices);
                    }
                    if (!meshExisted)
                    {
                        int triangleCount = 0;
                        for (int i = 0; i < hull.FaceToVertexIndicesStart.Length; ++i)
                        {
                            hull.GetVertexIndicesForFace(i, out var faceVertexIndices);
                            triangleCount += faceVertexIndices.Length - 2;
                        }
                        instance.VertexCount = triangleCount * 3;
                        lock (MeshCache)
                        {
                            MeshCache.Allocate(id, instance.VertexCount, out instance.VertexStart, out vertices);
                        }
                        //This is a fresh allocation, so we need to upload vertex data.
                        int targetVertexIndex = 0;
                        for (int i = 0; i < hull.FaceToVertexIndicesStart.Length; ++i)
                        {
                            hull.GetVertexIndicesForFace(i, out var faceVertexIndices);
                            hull.GetPoint(faceVertexIndices[0], out var faceOrigin);
                            hull.GetPoint(faceVertexIndices[1], out var previousEdgeEnd);
                            for (int j = 2; j < faceVertexIndices.Length; ++j)
                            {
                                vertices[targetVertexIndex++] = faceOrigin;
                                vertices[targetVertexIndex++] = previousEdgeEnd;
                                hull.GetPoint(faceVertexIndices[j], out previousEdgeEnd);
                                vertices[targetVertexIndex++] = previousEdgeEnd;

                            }
                        }
                    }
                    else
                    {
                        instance.VertexCount = vertices.Length;
                    }
                    shapeCache.Meshes.Add(instance, pool);
                }
                break;
            case Compound.Id:
                {
                    AddCompoundChildren(ref Unsafe.AsRef<Compound>(shapeData).Children, shapes, pose, color, ref shapeCache, pool);
                }
                break;
            case BigCompound.Id:
                {
                    AddCompoundChildren(ref Unsafe.AsRef<BigCompound>(shapeData).Children, shapes, pose, color, ref shapeCache, pool);
                }
                break;
            case Mesh.Id:
                {
                    ref var mesh = ref Unsafe.AsRef<Mesh>(shapeData);
                    MeshInstance instance;
                    instance.Position = pose.Position;
                    instance.Color = color;
                    instance.Orientation = pose.Orientation;
                    instance.Scale = mesh.Scale;
                    //Memory can be reused, so we slightly reduce the probability of a bad reuse by taking the first 64 bits of data into the hash.
                    var id = (ulong)mesh.Triangles.Memory ^ (ulong)mesh.Triangles.Length ^ (*(ulong*)mesh.Triangles.Memory); ;
                    instance.VertexCount = mesh.Triangles.Length * 3;
                    bool newAllocation;
                    Buffer<Vector3> vertices;
                    lock (MeshCache)
                    {
                        newAllocation = MeshCache.Allocate(id, instance.VertexCount, out instance.VertexStart, out vertices);
                    }
                    if (newAllocation)
                    {
                        //This is a fresh allocation, so we need to upload vertex data.
                        for (int i = 0; i < mesh.Triangles.Length; ++i)
                        {
                            ref var triangle = ref mesh.Triangles[i];
                            var baseVertexIndex = i * 3;
                            //Note winding flip for rendering.
                            vertices[baseVertexIndex] = triangle.A;
                            vertices[baseVertexIndex + 1] = triangle.C;
                            vertices[baseVertexIndex + 2] = triangle.B;
                        }
                    }
                    shapeCache.Meshes.Add(instance, pool);
                }
                break;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe void AddShape(void* shapeData, int shapeType, Shapes shapes, RigidPose pose, Vector3 color)
    {
        AddShape(shapeData, shapeType, shapes, pose, color, ref ShapeCache, pool);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    unsafe void AddShape(Shapes shapes, TypedIndex shapeIndex, RigidPose pose, Vector3 color, ref ShapeCache shapeCache, BufferPool pool)
    {
        if (shapeIndex.Exists)
        {
            shapes[shapeIndex.Type].GetShapeData(shapeIndex.Index, out var shapeData, out _);
            AddShape(shapeData, shapeIndex.Type, shapes, pose, color, ref shapeCache, pool);
        }
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe void AddShape(Shapes shapes, TypedIndex shapeIndex, RigidPose pose, Vector3 color)
    {
        if (shapeIndex.Exists)
        {
            shapes[shapeIndex.Type].GetShapeData(shapeIndex.Index, out var shapeData, out _);
            AddShape(shapeData, shapeIndex.Type, shapes, pose, color, ref ShapeCache, pool);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe void AddShape<TShape>(TShape shape, Shapes shapes, RigidPose pose, Vector3 color) where TShape : IShape
    {
        AddShape(Unsafe.AsPointer(ref shape), shape.TypeId, shapes, pose, color, ref ShapeCache, pool);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void AddBodyShape(Shapes shapes, Bodies bodies, int setIndex, int indexInSet, ref ShapeCache shapeCache, BufferPool pool)
    {
        ref var set = ref bodies.Sets[setIndex];
        var handle = set.IndexToHandle[indexInSet];
        //Body color is based on three factors:
        //1) Handle as a hash seed that is unpacked into a color
        //2) Dynamics vs kinematic state
        //3) Activity state
        //The handle is hashed to get variation.
        ref var activity = ref set.Activity[indexInSet];
        Vector3 color;
        Helpers.UnpackColor((uint)HashHelper.Rehash(handle.Value), out Vector3 colorVariation);
        ref var state = ref set.SolverStates[indexInSet];
        if (Bodies.IsKinematic(state.Inertia.Local))
        {
            var kinematicBase = new Vector3(0, 0.609f, 0.37f);
            var kinematicVariationSpan = new Vector3(0.1f, 0.1f, 0.1f);
            color = kinematicBase + kinematicVariationSpan * colorVariation;
        }
        else
        {
            var dynamicBase = new Vector3(0.8f, 0.1f, 0.566f);
            var dynamicVariationSpan = new Vector3(0.2f, 0.2f, 0.2f);
            color = dynamicBase + dynamicVariationSpan * colorVariation;
        }

        if (setIndex == 0)
        {
            if (activity.SleepCandidate)
            {
                var sleepCandidateTint = new Vector3(0.35f, 0.35f, 0.7f);
                color *= sleepCandidateTint;
            }
        }
        else
        {
            var sleepTint = new Vector3(0.2f, 0.2f, 0.4f);
            color *= sleepTint;
        }

        AddShape(shapes, set.Collidables[indexInSet].Shape, state.Motion.Pose, color, ref shapeCache, pool);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void AddStaticShape(Shapes shapes, Statics statics, int index, ref ShapeCache shapeCache, BufferPool pool)
    {
        var handle = statics.IndexToHandle[index];
        //Statics don't have any activity states. Just some simple variation on a central static color.
        Helpers.UnpackColor((uint)HashHelper.Rehash(handle.Value), out Vector3 colorVariation);
        var staticBase = new Vector3(0.1f, 0.057f, 0.014f);
        var staticVariationSpan = new Vector3(0.07f, 0.07f, 0.03f);
        var color = staticBase + staticVariationSpan * colorVariation;
        ref var collidable = ref statics[index];
        AddShape(shapes, collidable.Shape, collidable.Pose, color, ref shapeCache, pool);
    }

    public void AddShapesSequentially(Simulation simulation)
    {
        for (int i = 0; i < simulation.Bodies.Sets.Length; ++i)
        {
            ref var set = ref simulation.Bodies.Sets[i];
            if (set.Allocated) //Islands are stored noncontiguously; skip those which have been deallocated.
            {
                for (int bodyIndex = 0; bodyIndex < set.Count; ++bodyIndex)
                {
                    AddBodyShape(simulation.Shapes, simulation.Bodies, i, bodyIndex, ref ShapeCache, pool);
                }
            }
        }
        for (int i = 0; i < simulation.Statics.Count; ++i)
        {
            AddStaticShape(simulation.Shapes, simulation.Statics, i, ref ShapeCache, pool);
        }
    }

    public void Dispose()
    {
        ShapeCache.Dispose(pool);
        MeshCache.Dispose();
    }
}

public static class Helpers
{
    /// <summary>
    /// Unpacks a 3 component color packed by the Helpers.PackColor function.
    /// </summary>
    /// <param name="packedColor">Packed representation of the color to unpack.</param>
    /// <param name="color">Unpacked color.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void UnpackColor(uint packedColor, out Vector3 color)
    {
        //We don't support any form of alpha, so we dedicate 11 bits to R, 11 bits to G, and 10 bits to B.
        //B is stored in most significant, R in least significant.
        color.X = (packedColor & 0x7FF) / (float)((1 << 11) - 1);
        color.Y = ((packedColor >> 11) & 0x7FF) / (float)((1 << 11) - 1);
        color.Z = ((packedColor >> 22) & 0x3FF) / (float)((1 << 10) - 1);
    }

    [Conditional("DEBUG")]
    public static void CheckForUndisposed(bool disposed, object o)
    {
        Debug.Assert(disposed, "An object of type " + o.GetType() + " was not disposed prior to finalization.");
    }
}
