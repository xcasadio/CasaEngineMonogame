using System.IO;

namespace CasaEngine.Framework.Rendering.Shaders;

public readonly record struct BuiltInShaderDescriptor(string SourceRelativePath, string ContentName);

public static class BuiltInShaderCatalog
{
    public const string LitForwardContentName = EffectiveShaderResolver.LitForwardContentName;
    public const string UnlitTextureContentName = EffectiveShaderResolver.UnlitTextureContentName;
    public const string SkinEffectContentName = EffectiveShaderResolver.LinearBlendSkinnedEffectContentName;
    public const string ShadowDepthContentName = "Shaders\\ShadowDepth";
    public const string SkyCubemapContentName = "Shaders\\SkyCubemap";
    public const string SpriteBatchContentName = "Shaders\\SpriteBatch";
    public const string DebugPrimitiveColorContentName = "Shaders\\DebugPrimitiveColor";

    private static readonly BuiltInShaderDescriptor[] RuntimeShaderDescriptors =
    [
        new("Shaders/LitForward.fx", LitForwardContentName),
        new("Shaders/UnlitTexture.fx", UnlitTextureContentName),
        new("Shaders/skinEffect.fx", SkinEffectContentName),
        new("Shaders/ShadowDepth.fx", ShadowDepthContentName),
        new("Shaders/SkyCubemap.fx", SkyCubemapContentName),
        new("Shaders/SpriteBatch.fx", SpriteBatchContentName),
        new("Shaders/DebugPrimitiveColor.fx", DebugPrimitiveColorContentName),
    ];

    private static readonly Dictionary<string, BuiltInShaderDescriptor> DescriptorsByContentName = BuildDescriptorsByContentName();
    private static readonly Dictionary<string, BuiltInShaderDescriptor> DescriptorsBySourceRelativePath = BuildDescriptorsBySourceRelativePath();

    public static IReadOnlyList<BuiltInShaderDescriptor> RuntimeShaders => RuntimeShaderDescriptors;

    public static string NormalizeContentName(string contentName)
        => contentName.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);

    public static string NormalizeSourceRelativePath(string sourceRelativePath)
        => sourceRelativePath.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');

    public static bool TryGetByContentName(string contentName, out BuiltInShaderDescriptor descriptor)
    {
        if (string.IsNullOrWhiteSpace(contentName))
        {
            descriptor = default;
            return false;
        }

        return DescriptorsByContentName.TryGetValue(NormalizeContentName(contentName), out descriptor);
    }

    public static bool TryGetBySourceRelativePath(string sourceRelativePath, out BuiltInShaderDescriptor descriptor)
    {
        if (string.IsNullOrWhiteSpace(sourceRelativePath))
        {
            descriptor = default;
            return false;
        }

        return DescriptorsBySourceRelativePath.TryGetValue(NormalizeSourceRelativePath(sourceRelativePath), out descriptor);
    }

    private static Dictionary<string, BuiltInShaderDescriptor> BuildDescriptorsByContentName()
    {
        var descriptors = new Dictionary<string, BuiltInShaderDescriptor>(RuntimeShaderDescriptors.Length, StringComparer.OrdinalIgnoreCase);

        for (int index = 0; index < RuntimeShaderDescriptors.Length; index++)
        {
            var descriptor = RuntimeShaderDescriptors[index];
            descriptors[NormalizeContentName(descriptor.ContentName)] = descriptor;
        }

        return descriptors;
    }

    private static Dictionary<string, BuiltInShaderDescriptor> BuildDescriptorsBySourceRelativePath()
    {
        var descriptors = new Dictionary<string, BuiltInShaderDescriptor>(RuntimeShaderDescriptors.Length, StringComparer.OrdinalIgnoreCase);

        for (int index = 0; index < RuntimeShaderDescriptors.Length; index++)
        {
            var descriptor = RuntimeShaderDescriptors[index];
            descriptors[NormalizeSourceRelativePath(descriptor.SourceRelativePath)] = descriptor;
        }

        return descriptors;
    }
}