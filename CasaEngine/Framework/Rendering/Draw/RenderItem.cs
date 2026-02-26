using CasaEngine.Framework.Graphics;
using CasaEngine.Framework.Materials;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Rendering.Draw;

/// <summary>
/// A single renderable unit: one (mesh, submesh-range, material, world-transform) tuple.
/// Built during <c>Flush()</c> and sorted before issuing draw calls.
/// </summary>
public struct RenderItem
{
    /// <summary>Source mesh containing the GPU vertex/index buffers.</summary>
    public StaticModelMesh Mesh;

    /// <summary>
    /// Optional sub-mesh that defines an index range within <see cref="Mesh"/>.
    /// When null, the entire mesh is drawn.
    /// </summary>
    public SubMesh? SubMesh;

    /// <summary>Material to bind for this draw call.</summary>
    public MaterialBase Material;

    /// <summary>World transform matrix.</summary>
    public Matrix World;

    /// <summary>Pre-computed WorldInverseTranspose (avoids per-draw Invert+Transpose).</summary>
    public Matrix WorldInverseTranspose;

    /// <summary>64-bit sort key (queue + shader + material + mesh + distance).</summary>
    public ulong SortKey;

    /// <summary>Distance from camera (used for back-to-front sorting of transparent items).</summary>
    public float DistanceToCamera;

    /// <summary>
    /// Optional per-instance parameter overrides (Phase 6).
    /// Applied after <see cref="MaterialBase.Bind"/> so instance values win over material defaults.
    /// </summary>
    public MaterialPropertyBlock? PropertyOverrides;
}
