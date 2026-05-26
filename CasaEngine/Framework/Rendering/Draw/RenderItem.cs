using CasaEngine.Framework.Rendering.Models;

using CasaEngine.Framework.Rendering.Shaders;
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
    public SubMesh SubMesh;

    /// <summary>Material to bind for this draw call.</summary>
    public MaterialBase Material;

    /// <summary>
    /// Optional compiled material descriptor associated with <see cref="Material"/>.
    /// When present, shader id, feature flags, queue and render states should flow from this
    /// stable compiled snapshot instead of being recomputed ad hoc in the draw path.
    /// </summary>
    public CompiledMaterial CompiledMaterial;

    /// <summary>
    /// Component-level shadow casting flag propagated by the scene component authoring data.
    /// The effective value also depends on the bound material.
    /// </summary>
    public bool ComponentCastShadows;

    /// <summary>
    /// Component-level shadow receiving flag propagated by the scene component authoring data.
    /// The effective value also depends on the bound material.
    /// </summary>
    public bool ComponentReceiveShadows;

    /// <summary>
    /// Effective runtime shader id resolved from the material. This can be a real shader asset id
    /// or a stable built-in id produced by <see cref="Shaders.EffectiveShaderResolver"/>.
    /// </summary>
    private Guid _effectiveShaderId;

    public Guid EffectiveShaderId
    {
        readonly get => CompiledMaterial?.EffectiveShader.ShaderId ?? _effectiveShaderId;
        set => _effectiveShaderId = value;
    }

    /// <summary>World transform matrix.</summary>
    public Matrix World;

    /// <summary>Pre-computed WorldInverseTranspose (avoids per-draw Invert+Transpose).</summary>
    public Matrix WorldInverseTranspose;

    /// <summary>64-bit sort key (queue + shader + material + mesh + distance).</summary>
    public ulong SortKey;

    /// <summary>Distance from camera (used for back-to-front sorting of transparent items).</summary>
    public float DistanceToCamera;

    /// <summary>
    /// Active shader features for this draw call (Phase 7).
    /// Used to select the correct shader variant via <see cref="ShaderVariantLibrary"/>.
    /// </summary>
    private ShaderFeature _features;

    public ShaderFeature Features
    {
        readonly get => CompiledMaterial?.Features ?? _features;
        set => _features = value;
    }

    public readonly bool EffectiveCastShadows
        => ComponentCastShadows && (CompiledMaterial?.CastShadows ?? Material?.CastShadows ?? true);

    public readonly bool EffectiveReceiveShadows
        => ComponentReceiveShadows && (CompiledMaterial?.ReceiveShadows ?? Material?.ReceiveShadows ?? true);

    public readonly RenderQueue Queue
        => CompiledMaterial?.Queue ?? Material?.Queue ?? RenderQueue.Opaque;

    /// <summary>
    /// Optional per-instance parameter overrides (Phase 6).
    /// Applied after <see cref="MaterialBase.Bind"/> so instance values win over material defaults.
    /// </summary>
    public MaterialPropertyBlock PropertyOverrides;
}
