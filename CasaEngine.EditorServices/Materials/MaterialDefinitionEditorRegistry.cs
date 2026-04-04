using CasaEngine.Framework.Materials;

namespace CasaEngine.EditorServices.Materials;

public sealed class MaterialDefinitionEditorRegistry
{
    private static readonly SectionMetadata[] SectionOrder =
    {
        new("Surface", "Surface", 0),
        new("Normals", "Normals", 1),
        new("PBR", "PBR", 2),
        new("Emission", "Emission", 3),
        new("UV", "UV", 4),
        new("Advanced", "Advanced", 5),
    };

    private readonly Dictionary<string, MaterialPropertyDescriptor[]> _descriptorsByDefinitionId =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, MaterialPropertySectionDescriptor[]> _sectionsByDefinitionId =
        new(StringComparer.OrdinalIgnoreCase);

    public static MaterialDefinitionEditorRegistry Default { get; } = new();

    public IReadOnlyList<MaterialPropertyDescriptor> GetDescriptors(string materialDefinitionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(materialDefinitionId);

        if (_descriptorsByDefinitionId.TryGetValue(materialDefinitionId, out var descriptors))
        {
            return descriptors;
        }

        var definition = MaterialDefinitionRegistry.GetRequiredById(materialDefinitionId);
        descriptors = BuildDescriptors(definition);
        _descriptorsByDefinitionId[definition.Id] = descriptors;
        _sectionsByDefinitionId[definition.Id] = BuildSections(descriptors);
        return descriptors;
    }

    public IReadOnlyList<MaterialPropertySectionDescriptor> GetSections(string materialDefinitionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(materialDefinitionId);

        if (_sectionsByDefinitionId.TryGetValue(materialDefinitionId, out var sections))
        {
            return sections;
        }

        _ = GetDescriptors(materialDefinitionId);
        return _sectionsByDefinitionId[MaterialDefinitionRegistry.GetRequiredById(materialDefinitionId).Id];
    }

    private static MaterialPropertyDescriptor[] BuildDescriptors(MaterialDefinition definition)
    {
        var descriptors = new MaterialPropertyDescriptor[definition.Properties.Count];

        for (int i = 0; i < definition.Properties.Count; i++)
        {
            var propertyDefinition = definition.Properties[i];
            descriptors[i] = new MaterialPropertyDescriptor(
                propertyDefinition,
                GetCategory(propertyDefinition),
                GetEditorControlHint(propertyDefinition),
                i);
        }

        return descriptors;
    }

    private static MaterialPropertySectionDescriptor[] BuildSections(IReadOnlyList<MaterialPropertyDescriptor> descriptors)
    {
        var sections = new List<MaterialPropertySectionDescriptor>(SectionOrder.Length);

        for (int i = 0; i < SectionOrder.Length; i++)
        {
            var section = SectionOrder[i];
            var properties = new List<MaterialPropertyDescriptor>();

            for (int propertyIndex = 0; propertyIndex < descriptors.Count; propertyIndex++)
            {
                var descriptor = descriptors[propertyIndex];
                if (string.Equals(descriptor.Category, section.Key, StringComparison.Ordinal))
                {
                    properties.Add(descriptor);
                }
            }

            if (properties.Count == 0)
            {
                continue;
            }

            sections.Add(new MaterialPropertySectionDescriptor(
                section.Key,
                section.DisplayName,
                section.DisplayOrder,
                properties.ToArray()));
        }

        return sections.ToArray();
    }

    private static string GetCategory(MaterialPropertyDefinition propertyDefinition)
    {
        var key = propertyDefinition.Key;

        if (key.Contains("uv", StringComparison.OrdinalIgnoreCase))
        {
            return "UV";
        }

        if (key.Contains("normal", StringComparison.OrdinalIgnoreCase)
            || key.Contains("tangent", StringComparison.OrdinalIgnoreCase)
            || key.Contains("height", StringComparison.OrdinalIgnoreCase))
        {
            return "Normals";
        }

        if (key.Contains("emissive", StringComparison.OrdinalIgnoreCase)
            || key.Contains("emission", StringComparison.OrdinalIgnoreCase))
        {
            return "Emission";
        }

        if (key.Contains("alpha", StringComparison.OrdinalIgnoreCase)
            || key.Contains("opacity", StringComparison.OrdinalIgnoreCase))
        {
            return "Surface";
        }

        if (key.Contains("specular", StringComparison.OrdinalIgnoreCase)
            || key.Contains("roughness", StringComparison.OrdinalIgnoreCase)
            || key.Contains("metal", StringComparison.OrdinalIgnoreCase)
            || key.Contains("reflection", StringComparison.OrdinalIgnoreCase))
        {
            return "PBR";
        }

        return propertyDefinition.Group switch
        {
            MaterialPropertyGroup.Surface => "Surface",
            MaterialPropertyGroup.Textures => "Surface",
            MaterialPropertyGroup.Lighting => "PBR",
            MaterialPropertyGroup.Rendering => "Advanced",
            MaterialPropertyGroup.Advanced => "Advanced",
            _ => "Advanced",
        };
    }

    private static string GetEditorControlHint(MaterialPropertyDefinition propertyDefinition)
    {
        if ((propertyDefinition.Flags & MaterialPropertyFlags.AssetReference) != 0
            || propertyDefinition.ValueType == MaterialPropertyType.Texture)
        {
            return "AssetPicker";
        }

        return propertyDefinition.ValueType switch
        {
            MaterialPropertyType.Float or MaterialPropertyType.Integer
                when propertyDefinition.MinValue.HasValue && propertyDefinition.MaxValue.HasValue
                => "Slider",
            MaterialPropertyType.Float or MaterialPropertyType.Integer => "NumberBox",
            MaterialPropertyType.Boolean => "CheckBox",
            MaterialPropertyType.Color => "ColorPicker",
            MaterialPropertyType.Vector2 => "Vector2Editor",
            MaterialPropertyType.Vector3 => "Vector3Editor",
            MaterialPropertyType.Vector4 => "Vector4Editor",
            MaterialPropertyType.Enum => "Dropdown",
            MaterialPropertyType.String => "TextBox",
            _ => "TextBox",
        };
    }

    private readonly record struct SectionMetadata(string Key, string DisplayName, int DisplayOrder);
}