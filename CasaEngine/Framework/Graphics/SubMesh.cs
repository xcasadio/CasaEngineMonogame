using CasaEngine.Core.Serialization;
using CasaEngine.Framework.Materials;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Graphics;

/// <summary>
/// Describes a range of indices within a <see cref="StaticModelMesh"/>'s shared index buffer
/// that maps to a single material.  A mesh with N distinct materials has N <see cref="SubMesh"/> entries.
/// </summary>
public class SubMesh : ISerializable
{
    /// <summary>Starting index in the index buffer.</summary>
    public int IndexStart { get; set; }

    /// <summary>Number of primitives (triangles) in this sub-range.</summary>
    public int PrimitiveCount { get; set; }

    /// <summary>Base vertex offset applied to all indices in this sub-mesh.</summary>
    public int VertexOffset { get; set; }

    /// <summary>Asset ID of the material. Resolved at load time.</summary>
    public Guid MaterialAssetId { get; set; } = Guid.Empty;

    /// <summary>Runtime material instance (resolved from <see cref="MaterialAssetId"/>).</summary>
    public MaterialBase? Material { get; set; }

    public void Load(JObject element)
    {
        IndexStart    = element["index_start"]?.Value<int>()    ?? 0;
        PrimitiveCount = element["primitive_count"]?.Value<int>() ?? 0;
        VertexOffset  = element["vertex_offset"]?.Value<int>()   ?? 0;

        if (element["material_asset_id"] is { } matToken)
        {
            MaterialAssetId = Guid.Parse(matToken.Value<string>()!);
        }
    }
}
