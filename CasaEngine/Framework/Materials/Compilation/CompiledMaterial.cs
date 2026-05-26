using CasaEngine.Framework.Rendering.Shaders;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Materials.Compilation;

public sealed class CompiledMaterial
{
    private readonly Dictionary<string, MaterialValue> _properties;
    private readonly Dictionary<string, Texture2D> _textures;
    private readonly Dictionary<string, CompiledMaterialTextureBinding> _textureBindings;

    public CompiledMaterial(
        string definitionId,
        EffectiveShaderReference effectiveShader,
        IEnumerable<KeyValuePair<string, MaterialValue>> properties,
        IEnumerable<KeyValuePair<string, Texture2D>> textures = null,
        IEnumerable<KeyValuePair<string, CompiledMaterialTextureBinding>> textureBindings = null,
        Guid sourceAssetId = default,
        string name = "",
        ShaderFeature features = ShaderFeature.None,
        BlendState blendState = null,
        DepthStencilState depthStencilState = null,
        RasterizerState rasterizerState = null,
        SamplerState samplerState = null,
        bool isTransparent = false,
        RenderQueue queue = RenderQueue.Opaque,
        bool castShadows = true,
        bool receiveShadows = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(definitionId);
        ArgumentNullException.ThrowIfNull(properties);

        if (effectiveShader.ShaderId == Guid.Empty)
        {
            throw new ArgumentException("A compiled material requires a resolved shader id.", nameof(effectiveShader));
        }

        DefinitionId = definitionId;
        EffectiveShader = effectiveShader;
        SourceAssetId = sourceAssetId;
        Name = name;
        Features = features;
        BlendState = blendState ?? BlendState.Opaque;
        DepthStencilState = depthStencilState ?? DepthStencilState.Default;
        RasterizerState = rasterizerState ?? RasterizerState.CullCounterClockwise;
        SamplerState = samplerState ?? SamplerState.AnisotropicClamp;
        IsTransparent = isTransparent;
        Queue = queue;
        CastShadows = castShadows;
        ReceiveShadows = receiveShadows;
        _properties = BuildPropertyLookup(properties);
        _textures = BuildTextureLookup(textures);
        _textureBindings = BuildTextureBindingLookup(_textures, textureBindings);
    }

    public Guid SourceAssetId { get; }

    public string Name { get; }

    public string DefinitionId { get; }

    public EffectiveShaderReference EffectiveShader { get; }

    public ShaderFeature Features { get; }

    public BlendState BlendState { get; }

    public DepthStencilState DepthStencilState { get; }

    public RasterizerState RasterizerState { get; }

    public SamplerState SamplerState { get; }

    public bool IsTransparent { get; }

    public RenderQueue Queue { get; }

    public bool CastShadows { get; }

    public bool ReceiveShadows { get; }

    public IReadOnlyDictionary<string, MaterialValue> Properties => _properties;

    public IReadOnlyDictionary<string, Texture2D> Textures => _textures;

    public IReadOnlyDictionary<string, CompiledMaterialTextureBinding> TextureBindings => _textureBindings;

    public bool TryGetPropertyValue(string key, out MaterialValue value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return _properties.TryGetValue(key, out value!);
    }

    public bool TryGetTexture(string key, out Texture2D texture)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return _textures.TryGetValue(key, out texture);
    }

    public bool TryGetTextureBinding(string key, out CompiledMaterialTextureBinding textureBinding)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return _textureBindings.TryGetValue(key, out textureBinding);
    }

    private static Dictionary<string, MaterialValue> BuildPropertyLookup(
        IEnumerable<KeyValuePair<string, MaterialValue>> properties)
    {
        var lookup = new Dictionary<string, MaterialValue>(StringComparer.OrdinalIgnoreCase);

        foreach (var pair in properties)
        {
            if (string.IsNullOrWhiteSpace(pair.Key))
            {
                throw new ArgumentException("Compiled material property keys cannot be empty.", nameof(properties));
            }

            ArgumentNullException.ThrowIfNull(pair.Value);

            if (!lookup.TryAdd(pair.Key, pair.Value))
            {
                throw new ArgumentException(
                    $"Compiled material contains duplicate property key '{pair.Key}'.",
                    nameof(properties));
            }
        }

        return lookup;
    }

    private static Dictionary<string, Texture2D> BuildTextureLookup(
        IEnumerable<KeyValuePair<string, Texture2D>> textures)
    {
        var lookup = new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);
        if (textures is null)
        {
            return lookup;
        }

        foreach (var pair in textures)
        {
            if (string.IsNullOrWhiteSpace(pair.Key))
            {
                throw new ArgumentException("Compiled material texture keys cannot be empty.", nameof(textures));
            }

            if (!lookup.TryAdd(pair.Key, pair.Value))
            {
                throw new ArgumentException(
                    $"Compiled material contains duplicate texture key '{pair.Key}'.",
                    nameof(textures));
            }
        }

        return lookup;
    }

    private static Dictionary<string, CompiledMaterialTextureBinding> BuildTextureBindingLookup(
        IReadOnlyDictionary<string, Texture2D> textures,
        IEnumerable<KeyValuePair<string, CompiledMaterialTextureBinding>> textureBindings)
    {
        var lookup = new Dictionary<string, CompiledMaterialTextureBinding>(StringComparer.OrdinalIgnoreCase);

        if (textureBindings is not null)
        {
            foreach (var pair in textureBindings)
            {
                if (string.IsNullOrWhiteSpace(pair.Key))
                {
                    throw new ArgumentException("Compiled material texture binding keys cannot be empty.", nameof(textureBindings));
                }

                if (!lookup.TryAdd(pair.Key, pair.Value))
                {
                    throw new ArgumentException(
                        $"Compiled material contains duplicate texture binding key '{pair.Key}'.",
                        nameof(textureBindings));
                }
            }
        }

        foreach (var pair in textures)
        {
            if (lookup.ContainsKey(pair.Key))
            {
                continue;
            }

            lookup.Add(
                pair.Key,
                new CompiledMaterialTextureBinding(
                    Guid.Empty,
                    CompiledMaterialTextureBindingKind.Texture2D,
                    texture: pair.Value));
        }

        return lookup;
    }
}