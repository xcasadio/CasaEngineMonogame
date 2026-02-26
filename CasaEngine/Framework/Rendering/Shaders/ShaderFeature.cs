namespace CasaEngine.Framework.Rendering.Shaders;

/// <summary>
/// Bit flags describing which optional features are active for a given draw call.
/// The combination of flags identifies a unique shader variant in <see cref="ShaderVariantLibrary"/>.
/// </summary>
[Flags]
public enum ShaderFeature : uint
{
    None          = 0,
    /// <summary>Mesh has an albedo/diffuse texture.</summary>
    AlbedoTexture = 1 << 0,
    /// <summary>Mesh carries per-vertex colour data.</summary>
    VertexColor   = 1 << 1,
    /// <summary>Material uses alpha-test clip.</summary>
    AlphaTest     = 1 << 2,
    /// <summary>Mesh is skinned / has blend weights.</summary>
    Skinned       = 1 << 3,
    /// <summary>Draw call uses hardware instancing.</summary>
    Instanced     = 1 << 4,
    /// <summary>Material has a normal-map texture.</summary>
    NormalMap     = 1 << 5,
    /// <summary>Material has an emissive texture or non-zero emissive colour.</summary>
    Emissive      = 1 << 6,
}
