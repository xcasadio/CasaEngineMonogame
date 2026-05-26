using CasaEngine.Core.Serialization;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Materials.Runtime;

/// <summary>
/// Authoring-time per-object material overrides that remain distinct from the
/// base material asset.
/// </summary>
public sealed class MaterialInstanceData : ISerializable
{
    private readonly Dictionary<string, MaterialValue> _propertyOverrides = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, MaterialValue> PropertyOverrides => _propertyOverrides;

    public int PropertyOverrideCount => _propertyOverrides.Count;

    public bool IsEmpty => _propertyOverrides.Count == 0;

    public MaterialInstanceData()
    {
    }

    public MaterialInstanceData(MaterialInstanceData other)
    {
        ArgumentNullException.ThrowIfNull(other);

        foreach (var pair in other._propertyOverrides)
        {
            _propertyOverrides.Add(pair.Key, pair.Value);
        }
    }

    public MaterialInstanceData Clone() => new(this);

    public void SetPropertyOverride(string propertyKey, MaterialValue value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyKey);
        ArgumentNullException.ThrowIfNull(value);

        _propertyOverrides[propertyKey] = value;
    }

    public void SetPropertyOverride(string propertyKey, MaterialPropertyType type, object value)
        => SetPropertyOverride(propertyKey, MaterialValue.FromObject(type, value));

    public bool TrySetPropertyOverride(
        MaterialDefinition definition,
        string keyOrAlias,
        MaterialValue value,
        out string validationError)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentException.ThrowIfNullOrWhiteSpace(keyOrAlias);
        ArgumentNullException.ThrowIfNull(value);

        if (!definition.TryGetPropertyBySerializedName(keyOrAlias, out var propertyDefinition))
        {
            validationError =
                $"Material definition '{definition.Id}' does not expose a property named '{keyOrAlias}'.";
            return false;
        }

        if (!propertyDefinition.IsValueCompatible(value, out validationError))
        {
            return false;
        }

        _propertyOverrides[propertyDefinition.Key] = value;
        return true;
    }

    public bool TryGetPropertyOverride(string propertyKey, out MaterialValue value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyKey);
        return _propertyOverrides.TryGetValue(propertyKey, out value!);
    }

    public bool HasPropertyOverride(string propertyKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyKey);
        return _propertyOverrides.ContainsKey(propertyKey);
    }

    public bool RemovePropertyOverride(string propertyKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyKey);
        return _propertyOverrides.Remove(propertyKey);
    }

    public void ClearPropertyOverrides()
        => _propertyOverrides.Clear();

    public IReadOnlyList<string> ValidateAgainst(MaterialDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        if (_propertyOverrides.Count == 0)
        {
            return Array.Empty<string>();
        }

        var errors = new List<string>();
        foreach (var pair in _propertyOverrides)
        {
            if (!definition.TryGetPropertyBySerializedName(pair.Key, out var propertyDefinition))
            {
                errors.Add(
                    $"Material instance data stores a value for unknown property '{pair.Key}' on definition '{definition.Id}'.");
                continue;
            }

            if (!propertyDefinition.IsValueCompatible(pair.Value, out var validationError))
            {
                errors.Add(validationError!);
            }
        }

        return errors.Count == 0 ? Array.Empty<string>() : errors.ToArray();
    }

    public IReadOnlyList<string> ValidateAgainst(MaterialAsset materialAsset)
    {
        ArgumentNullException.ThrowIfNull(materialAsset);
        return ValidateAgainst(materialAsset.GetRequiredDefinition());
    }

    public void Load(JObject element)
    {
        MaterialInstanceDataJsonSerializer.Load(this, element);
    }
}