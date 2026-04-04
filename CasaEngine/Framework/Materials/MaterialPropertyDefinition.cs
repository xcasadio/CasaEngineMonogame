using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Materials;

public sealed class MaterialPropertyDefinition
{
    private static readonly string[] EmptyAliases = Array.Empty<string>();
    private static readonly MaterialPropertyOption[] EmptyOptions = Array.Empty<MaterialPropertyOption>();

    public MaterialPropertyDefinition(
        string key,
        string displayName,
        MaterialPropertyType valueType,
        MaterialPropertyGroup group,
        MaterialPropertyFlags flags = MaterialPropertyFlags.None,
        object? defaultValue = null,
        string description = "",
        IEnumerable<string>? legacyAliases = null,
        string? assetKind = null,
        IEnumerable<MaterialPropertyOption>? options = null,
        float? minValue = null,
        float? maxValue = null,
        float? step = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        Key = key;
        DisplayName = displayName;
        ValueType = valueType;
        Group = group;
        Flags = flags;
        Description = description;
        AssetKind = string.IsNullOrWhiteSpace(assetKind) ? null : assetKind;
        MinValue = minValue;
        MaxValue = maxValue;
        Step = step;

        LegacyAliases = NormalizeAliases(legacyAliases, key);
        Options = NormalizeOptions(options);

        ValidateNumericMetadata();
        ValidateEnumMetadata();
        ValidateDefaultValue(defaultValue);

        DefaultValue = defaultValue;
    }

    public string Key { get; }
    public string DisplayName { get; }
    public string Description { get; }
    public MaterialPropertyType ValueType { get; }
    public MaterialPropertyGroup Group { get; }
    public MaterialPropertyFlags Flags { get; }
    public object? DefaultValue { get; }
    public string? AssetKind { get; }
    public float? MinValue { get; }
    public float? MaxValue { get; }
    public float? Step { get; }
    public IReadOnlyList<string> LegacyAliases { get; }
    public IReadOnlyList<MaterialPropertyOption> Options { get; }

    public bool IsNumeric => ValueType is MaterialPropertyType.Float or MaterialPropertyType.Integer;

    public bool MatchesSerializedName(string name)
    {
        if (string.Equals(Key, name, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        foreach (var alias in LegacyAliases)
        {
            if (string.Equals(alias, name, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public T? GetDefaultValue<T>()
    {
        if (DefaultValue is null)
        {
            return default;
        }

        if (DefaultValue is T typedValue)
        {
            return typedValue;
        }

        throw new InvalidOperationException(
            $"Default value for material property '{Key}' is not of type {typeof(T).Name}.");
    }

    private void ValidateNumericMetadata()
    {
        if (MinValue.HasValue || MaxValue.HasValue || Step.HasValue)
        {
            if (!IsNumeric)
            {
                throw new ArgumentException(
                    $"Numeric metadata is only valid for numeric material properties. Property '{Key}' is '{ValueType}'.");
            }

            if (MinValue.HasValue && MaxValue.HasValue && MinValue.Value > MaxValue.Value)
            {
                throw new ArgumentException(
                    $"Material property '{Key}' has an invalid numeric range: minValue > maxValue.");
            }

            if (Step.HasValue && Step.Value <= 0f)
            {
                throw new ArgumentException(
                    $"Material property '{Key}' has an invalid step value '{Step.Value}'.");
            }
        }
    }

    private void ValidateEnumMetadata()
    {
        if (ValueType == MaterialPropertyType.Enum)
        {
            if (Options.Count == 0)
            {
                throw new ArgumentException(
                    $"Enum material property '{Key}' must declare at least one option.");
            }

            return;
        }

        if (Options.Count > 0)
        {
            throw new ArgumentException(
                $"Only enum material properties can declare options. Property '{Key}' is '{ValueType}'.");
        }
    }

    private void ValidateDefaultValue(object? defaultValue)
    {
        if (defaultValue is null)
        {
            return;
        }

        if (!IsSupportedDefaultValue(ValueType, defaultValue))
        {
            throw new ArgumentException(
                $"Default value '{defaultValue}' is not compatible with material property '{Key}' of type '{ValueType}'.");
        }

        if (ValueType == MaterialPropertyType.Enum)
        {
            var enumValue = (string)defaultValue;
            for (int i = 0; i < Options.Count; i++)
            {
                if (string.Equals(Options[i].Value, enumValue, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            throw new ArgumentException(
                $"Default enum value '{enumValue}' is not declared for material property '{Key}'.");
        }
    }

    private static bool IsSupportedDefaultValue(MaterialPropertyType valueType, object defaultValue) => valueType switch
    {
        MaterialPropertyType.Float => defaultValue is float,
        MaterialPropertyType.Integer => defaultValue is int,
        MaterialPropertyType.Boolean => defaultValue is bool,
        MaterialPropertyType.Color => defaultValue is Color,
        MaterialPropertyType.Vector2 => defaultValue is Vector2,
        MaterialPropertyType.Vector3 => defaultValue is Vector3,
        MaterialPropertyType.Vector4 => defaultValue is Vector4,
        MaterialPropertyType.Texture => defaultValue is Guid,
        MaterialPropertyType.Enum => defaultValue is string,
        MaterialPropertyType.String => defaultValue is string,
        _ => false,
    };

    private static IReadOnlyList<string> NormalizeAliases(IEnumerable<string>? legacyAliases, string key)
    {
        if (legacyAliases is null)
        {
            return EmptyAliases;
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var aliases = new List<string>();
        foreach (var alias in legacyAliases)
        {
            if (string.IsNullOrWhiteSpace(alias))
            {
                throw new ArgumentException($"Material property '{key}' contains an empty legacy alias.");
            }

            if (string.Equals(alias, key, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(
                    $"Material property '{key}' cannot declare '{alias}' as both key and legacy alias.");
            }

            if (!seen.Add(alias))
            {
                throw new ArgumentException(
                    $"Material property '{key}' declares duplicate legacy alias '{alias}'.");
            }

            aliases.Add(alias);
        }

        return aliases.Count == 0 ? EmptyAliases : aliases.ToArray();
    }

    private static IReadOnlyList<MaterialPropertyOption> NormalizeOptions(IEnumerable<MaterialPropertyOption>? options)
    {
        if (options is null)
        {
            return EmptyOptions;
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var normalizedOptions = new List<MaterialPropertyOption>();
        foreach (var option in options)
        {
            if (!seen.Add(option.Value))
            {
                throw new ArgumentException(
                    $"Material property enum options contain duplicate value '{option.Value}'.");
            }

            normalizedOptions.Add(option);
        }

        return normalizedOptions.Count == 0 ? EmptyOptions : normalizedOptions.ToArray();
    }
}