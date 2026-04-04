using CasaEngine.Framework;

namespace CasaEngine.Framework.Materials;

public sealed class MaterialAsset : ObjectBase
{
    public const string DefaultBlendStateName = "Opaque";
    public const string DefaultDepthStencilStateName = "Default";
    public const string DefaultRasterizerStateName = "CullCounterClockwise";
    public const string DefaultSamplerStateName = "AnisotropicClamp";

    private static readonly string[] AllowedBlendStateNames = CopyStateNames(MaterialBase.BlendStateNames);
    private static readonly string[] AllowedDepthStencilStateNames = CopyStateNames(MaterialBase.DepthStencilStateNames);
    private static readonly string[] AllowedRasterizerStateNames = CopyStateNames(MaterialBase.RasterizerStateNames);
    private static readonly string[] AllowedSamplerStateNames = CopyStateNames(MaterialBase.SamplerStateNames);

    private readonly Dictionary<string, MaterialValue> _propertyValues = new(StringComparer.OrdinalIgnoreCase);
    private string _definitionId = string.Empty;
    private string _blendStateName = DefaultBlendStateName;
    private string _depthStencilStateName = DefaultDepthStencilStateName;
    private string _rasterizerStateName = DefaultRasterizerStateName;
    private string _samplerStateName = DefaultSamplerStateName;

    public MaterialAsset()
    {
        Name = $"Material {Id}";
    }

    public MaterialAsset(string definitionId)
        : this()
    {
        DefinitionId = definitionId;
    }

    public string DefinitionId
    {
        get => _definitionId;
        set
        {
            var definition = MaterialDefinitionRegistry.GetRequiredById(value);
            _definitionId = definition.Id;
            RemoveIncompatiblePropertyValues(definition);
        }
    }

    public Guid ParentMaterialAssetId { get; set; } = Guid.Empty;

    public Guid ShaderAssetId { get; set; } = Guid.Empty;

    public bool IsTransparent { get; set; }

    public RenderQueue Queue { get; set; } = RenderQueue.Opaque;

    public bool CastShadows { get; set; } = true;

    public bool ReceiveShadows { get; set; } = true;

    public string BlendStateName
    {
        get => _blendStateName;
        set => _blendStateName = NormalizeStateName(value, AllowedBlendStateNames, nameof(BlendStateName));
    }

    public string DepthStencilStateName
    {
        get => _depthStencilStateName;
        set => _depthStencilStateName = NormalizeStateName(value, AllowedDepthStencilStateNames, nameof(DepthStencilStateName));
    }

    public string RasterizerStateName
    {
        get => _rasterizerStateName;
        set => _rasterizerStateName = NormalizeStateName(value, AllowedRasterizerStateNames, nameof(RasterizerStateName));
    }

    public string SamplerStateName
    {
        get => _samplerStateName;
        set => _samplerStateName = NormalizeStateName(value, AllowedSamplerStateNames, nameof(SamplerStateName));
    }

    public IReadOnlyDictionary<string, MaterialValue> PropertyValues => _propertyValues;

    public bool TryGetDefinition(out MaterialDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(_definitionId))
        {
            definition = null!;
            return false;
        }

        return MaterialDefinitionRegistry.TryGetById(_definitionId, out definition!);
    }

    public MaterialDefinition GetRequiredDefinition()
    {
        if (string.IsNullOrWhiteSpace(_definitionId))
        {
            throw new InvalidOperationException("Material asset does not define a material definition id.");
        }

        return MaterialDefinitionRegistry.GetRequiredById(_definitionId);
    }

    public void SetPropertyValue(string keyOrAlias, MaterialValue value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var propertyDefinition = GetRequiredPropertyDefinition(keyOrAlias);
        if (!propertyDefinition.IsValueCompatible(value, out var validationError))
        {
            throw new ArgumentException(validationError, nameof(value));
        }

        _propertyValues[propertyDefinition.Key] = value;
    }

    public void SetPropertyValue(string keyOrAlias, object value)
    {
        var propertyDefinition = GetRequiredPropertyDefinition(keyOrAlias);
        SetPropertyValue(propertyDefinition.Key, MaterialValue.FromObject(propertyDefinition.ValueType, value));
    }

    public bool TryGetPropertyValue(string keyOrAlias, out MaterialValue value)
    {
        if (!TryGetPropertyDefinition(keyOrAlias, out var propertyDefinition))
        {
            value = null!;
            return false;
        }

        return _propertyValues.TryGetValue(propertyDefinition.Key, out value!);
    }

    public MaterialValue? GetPropertyValueOrDefault(string keyOrAlias)
    {
        var propertyDefinition = GetRequiredPropertyDefinition(keyOrAlias);
        if (_propertyValues.TryGetValue(propertyDefinition.Key, out var value))
        {
            return value;
        }

        return propertyDefinition.GetDefaultMaterialValue();
    }

    public bool RemovePropertyValue(string keyOrAlias)
    {
        if (!TryGetPropertyDefinition(keyOrAlias, out var propertyDefinition))
        {
            return false;
        }

        return _propertyValues.Remove(propertyDefinition.Key);
    }

    public void ClearPropertyValues()
    {
        _propertyValues.Clear();
    }

    public IReadOnlyList<string> Validate()
    {
        MaterialDefinition definition;
        try
        {
            definition = GetRequiredDefinition();
        }
        catch (Exception ex) when (ex is InvalidOperationException or KeyNotFoundException)
        {
            return new[] { ex.Message };
        }

        var errors = new List<string>();
        if (ParentMaterialAssetId != Guid.Empty && ParentMaterialAssetId == Id)
        {
            errors.Add("Material asset cannot parent itself.");
        }

        for (int i = 0; i < definition.Properties.Count; i++)
        {
            var propertyDefinition = definition.Properties[i];
            if ((propertyDefinition.Flags & MaterialPropertyFlags.Required) == 0)
            {
                continue;
            }

            if (_propertyValues.ContainsKey(propertyDefinition.Key) || propertyDefinition.DefaultValue is not null)
            {
                continue;
            }

            errors.Add($"Required material property '{propertyDefinition.Key}' is missing on material asset '{Name}'.");
        }

        foreach (var pair in _propertyValues)
        {
            if (!definition.TryGetProperty(pair.Key, out var propertyDefinition))
            {
                errors.Add($"Material asset '{Name}' stores a value for unknown property '{pair.Key}' on definition '{definition.Id}'.");
                continue;
            }

            if (!propertyDefinition.IsValueCompatible(pair.Value, out var validationError))
            {
                errors.Add(validationError!);
            }
        }

        return errors.Count == 0 ? Array.Empty<string>() : errors.ToArray();
    }

    private void RemoveIncompatiblePropertyValues(MaterialDefinition definition)
    {
        if (_propertyValues.Count == 0)
        {
            return;
        }

        List<string>? keysToRemove = null;
        foreach (var pair in _propertyValues)
        {
            if (!definition.TryGetProperty(pair.Key, out var propertyDefinition)
                || !propertyDefinition.IsValueCompatible(pair.Value, out _))
            {
                keysToRemove ??= new List<string>();
                keysToRemove.Add(pair.Key);
            }
        }

        if (keysToRemove is null)
        {
            return;
        }

        for (int i = 0; i < keysToRemove.Count; i++)
        {
            _propertyValues.Remove(keysToRemove[i]);
        }
    }

    private bool TryGetPropertyDefinition(string keyOrAlias, out MaterialPropertyDefinition propertyDefinition)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(keyOrAlias);

        if (!TryGetDefinition(out var definition))
        {
            propertyDefinition = null!;
            return false;
        }

        return definition.TryGetPropertyBySerializedName(keyOrAlias, out propertyDefinition!);
    }

    private MaterialPropertyDefinition GetRequiredPropertyDefinition(string keyOrAlias)
    {
        if (TryGetPropertyDefinition(keyOrAlias, out var propertyDefinition))
        {
            return propertyDefinition;
        }

        var definition = GetRequiredDefinition();
        throw new KeyNotFoundException(
            $"Material definition '{definition.Id}' does not expose a property named '{keyOrAlias}'.");
    }

    private static string NormalizeStateName(string value, IReadOnlyList<string> allowedValues, string propertyName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        for (int i = 0; i < allowedValues.Count; i++)
        {
            if (string.Equals(allowedValues[i], value, StringComparison.OrdinalIgnoreCase))
            {
                return allowedValues[i];
            }
        }

        throw new ArgumentException(
            $"Unknown material state '{value}' for '{propertyName}'. Allowed values: {string.Join(", ", allowedValues)}.",
            propertyName);
    }

    private static string[] CopyStateNames(IReadOnlyList<string> stateNames)
    {
        var copy = new string[stateNames.Count];
        for (int i = 0; i < stateNames.Count; i++)
        {
            copy[i] = stateNames[i];
        }

        return copy;
    }
}