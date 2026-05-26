namespace CasaEngine.Framework.Rendering.Shaders;

/// <summary>
/// Identifies a unique compiled shader variant: the combination of a base shader asset
/// and a set of active <see cref="ShaderFeature"/> flags.
/// Used as the key in <see cref="ShaderVariantLibrary"/> to cache and retrieve
/// <see cref="ShaderWrapper"/> instances.
/// </summary>
public readonly struct ShaderVariantKey : IEquatable<ShaderVariantKey>
{
    /// <summary>Guid of the base shader asset (.fx / compiled effect).</summary>
    public Guid ShaderBaseId { get; }

    /// <summary>Active feature flags that distinguish this variant from others.</summary>
    public ShaderFeature Features { get; }

    public ShaderVariantKey(Guid shaderBaseId, ShaderFeature features)
    {
        ShaderBaseId = shaderBaseId;
        Features     = features;
    }

    public bool Equals(ShaderVariantKey other) =>
        ShaderBaseId == other.ShaderBaseId && Features == other.Features;

    public override bool Equals(object obj) =>
        obj is ShaderVariantKey k && Equals(k);

    public override int GetHashCode() => HashCode.Combine(ShaderBaseId, Features);

    public static bool operator ==(ShaderVariantKey a, ShaderVariantKey b) => a.Equals(b);
    public static bool operator !=(ShaderVariantKey a, ShaderVariantKey b) => !a.Equals(b);

    public override string ToString() => $"Shader({ShaderBaseId:N})+{Features}";
}
