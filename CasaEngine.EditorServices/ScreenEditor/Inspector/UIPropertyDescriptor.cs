using CasaEngine.Framework.Assets;

namespace CasaEngine.EditorServices.ScreenEditor.Inspector;

/// <summary>Metadata describing a single editable property in the UI screen inspector.</summary>
public sealed class UIPropertyDescriptor
{
    /// <summary>Name as stored in <see cref="DocumentModel.UIScreenPropertyValue.Name"/>.</summary>
    public string Name { get; }

    /// <summary>Human-readable label shown in the inspector.</summary>
    public string DisplayName { get; }

    /// <summary>Optional grouping label (e.g. "Layout", "Appearance").</summary>
    public string Category { get; }

    /// <summary>CLR type of the property value (string, int, float, enum, …).</summary>
    public Type ValueType { get; }

    /// <summary>Default serialized value used when the property is not yet set on a node.</summary>
    public string? DefaultSerializedValue { get; }

    /// <summary>When false, the property is shown read-only in the inspector.</summary>
    public bool IsEditable { get; }

    /// <summary>
    /// Asset types (<see cref="AssetInfo.AssetType"/>, compared case-insensitively) this property may reference,
    /// or null when the property is not an asset reference. The inspector then offers an asset picker filtered on
    /// these types, which writes the chosen asset's id as the property value (ADR-0038, "Images are named by asset").
    /// </summary>
    public IReadOnlyList<string>? AssetTypes { get; init; }

    /// <summary>True when <see cref="AssetTypes"/> names at least one asset type.</summary>
    public bool IsAssetReference => AssetTypes is { Count: > 0 };

    /// <summary>True when <paramref name="assetInfo"/> is one of <see cref="AssetTypes"/>.</summary>
    public bool AcceptsAsset(AssetInfo? assetInfo)
    {
        if (assetInfo == null || !IsAssetReference)
        {
            return false;
        }

        for (int i = 0; i < AssetTypes!.Count; i++)
        {
            if (string.Equals(AssetTypes[i], assetInfo.AssetType, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public UIPropertyDescriptor(
        string name,
        string displayName,
        string category,
        Type valueType,
        string? defaultSerializedValue = null,
        bool isEditable = true)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Property name must not be empty.", nameof(name));
        }

        Name = name;
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        Category = string.IsNullOrWhiteSpace(category) ? "General" : category;
        ValueType = valueType ?? throw new ArgumentNullException(nameof(valueType));
        DefaultSerializedValue = defaultSerializedValue;
        IsEditable = isEditable;
    }
}
