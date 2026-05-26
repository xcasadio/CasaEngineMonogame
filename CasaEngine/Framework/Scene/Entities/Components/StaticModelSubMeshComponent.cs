using System.ComponentModel;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Application.Components;
using CasaEngine.Framework.Rendering.Models;

using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Scene.Entities.Components;

/// <summary>
/// Lightweight component that renders a single <see cref="StaticModelMesh"/> sub-mesh.
/// Instances are created dynamically by the parent <see cref="StaticModelComponent"/>
/// during <c>InitializeWithWorld</c> — one per <see cref="StaticModelNode"/> that
/// references a mesh.
///
/// <para>
/// This component is <b>runtime-only</b>: its mesh data is not serialized because it
/// always comes from the parent's <see cref="Framework.Graphics.StaticModel"/> asset.
/// Only user overrides (visibility, material) are persisted.
/// </para>
/// </summary>
[DisplayName("Sub Mesh")]
public class StaticModelSubMeshComponent : PrimitiveComponent
{
    private StaticMeshRendererComponent _meshRendererComponent;

    /// <summary>
    /// The sub-mesh to render. Assigned at runtime by the parent
    /// <see cref="StaticModelComponent"/>; never serialized.
    /// </summary>
    public StaticModelMesh ModelMesh { get; set; }

    /// <summary>
    /// Optional per-instance shader parameter overrides applied after the mesh
    /// material's <c>Bind()</c> call. Use this to tint an individual entity
    /// differently without duplicating the entire <see cref="MaterialBase"/> asset.
    /// </summary>
    public MaterialPropertyBlock PropertyOverrides { get; set; }

    /// <summary>
    /// Runtime-only per-slot material overrides inherited from the parent
    /// <see cref="StaticModelComponent"/> instance.
    /// </summary>
    public IReadOnlyDictionary<int, MaterialBase> MaterialOverridesBySlotIndex { get; set; }

    /// <summary>
    /// Runtime-only per-slot shader parameter overrides inherited from the parent
    /// <see cref="StaticModelComponent"/> instance.
    /// </summary>
    public IReadOnlyDictionary<int, MaterialPropertyBlock> PropertyOverridesBySlotIndex { get; set; }

    /// <summary>
    /// <c>true</c> when this component was auto-generated from a <see cref="StaticModelComponent"/>.
    /// The parent's <c>Save()</c> uses this flag to skip serializing generated children.
    /// </summary>
    public bool IsGeneratedFromModel { get; set; }

    public StaticModelSubMeshComponent() { }

    public StaticModelSubMeshComponent(StaticModelSubMeshComponent other) : base(other)
    {
        ModelMesh = other.ModelMesh;
        IsGeneratedFromModel = other.IsGeneratedFromModel;
        PropertyOverrides = other.PropertyOverrides;
        MaterialOverridesBySlotIndex = other.MaterialOverridesBySlotIndex;
        PropertyOverridesBySlotIndex = other.PropertyOverridesBySlotIndex;
    }

    public override StaticModelSubMeshComponent Clone() => new(this);

    public override void InitializeWithWorld(CasaEngine.Framework.Scene.World.World world)
    {
        base.InitializeWithWorld(world);
        _meshRendererComponent = world.Game.GetGameComponent<StaticMeshRendererComponent>()!;
    }

    public override void Draw(float elapsedTime)
    {
        base.Draw(elapsedTime);

        if (ModelMesh?.VertexBuffer == null || _meshRendererComponent == null)
        {
            return;
        }

        var world = WorldMatrixWithScale;
        var invTranspose = Matrix.Transpose(Matrix.Invert(world));
        var shadowFlagSource = GetShadowFlagSource();
        _meshRendererComponent.AddMesh(
            ModelMesh,
            world,
            invTranspose,
            materialOverridesBySlotIndex: MaterialOverridesBySlotIndex,
            propertyOverrides: PropertyOverrides,
            propertyOverridesBySlotIndex: PropertyOverridesBySlotIndex,
            castShadows: shadowFlagSource.CastShadows,
            receiveShadows: shadowFlagSource.ReceiveShadows);
    }

    public override BoundingBox GetBoundingBox()
    {
        if (ModelMesh == null)
        {
            return base.GetBoundingBox();
        }

        var min = new Vector3(float.MaxValue);
        var max = new Vector3(float.MinValue);
        var world = WorldMatrixWithScale;

        foreach (var v in ModelMesh.GetVertices())
        {
            var p = Vector3.Transform(v.Position, world);
            min = Vector3.Min(min, p);
            max = Vector3.Max(max, p);
        }

        return new BoundingBox(min, max);
    }

    // Sub-mesh components generated from a model asset do not serialize mesh data —
    // the geometry is always reconstructed from the parent StaticModelComponent asset.
    // Only base SceneComponent data (coordinates) is loaded/saved, and only for
    // non-generated instances (e.g. manually added sub-meshes, future use).

    public override void Load(JObject element)
    {
        base.Load(element);
    }

    private PrimitiveComponent GetShadowFlagSource()
    {
        if (!IsGeneratedFromModel)
        {
            return this;
        }

        var current = Parent;
        while (current != null)
        {
            if (current is StaticModelComponent staticModelComponent)
            {
                return staticModelComponent;
            }

            current = current.Parent;
        }

        return this;
    }

}
