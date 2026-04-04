using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Materials;

public static class MaterialInstanceDataJsonSerializer
{
    public static void Save(MaterialInstanceData materialInstanceData, JObject node)
    {
        ArgumentNullException.ThrowIfNull(materialInstanceData);
        ArgumentNullException.ThrowIfNull(node);

        if (materialInstanceData.PropertyOverrideCount == 0)
        {
            return;
        }

        var propertyOverridesNode = new JObject();
        foreach (var pair in materialInstanceData.PropertyOverrides)
        {
            propertyOverridesNode[pair.Key] = MaterialValueJsonSerializer.SaveTyped(pair.Value);
        }

        if (propertyOverridesNode.Count > 0)
        {
            node["property_overrides"] = propertyOverridesNode;
        }
    }

    public static void Load(MaterialInstanceData materialInstanceData, JObject node)
    {
        ArgumentNullException.ThrowIfNull(materialInstanceData);
        ArgumentNullException.ThrowIfNull(node);

        materialInstanceData.ClearPropertyOverrides();

        if (node["property_overrides"] is not JObject propertyOverridesNode)
        {
            return;
        }

        foreach (var propertyNode in propertyOverridesNode.Properties())
        {
            if (propertyNode.Value is not JObject valueNode)
            {
                throw new InvalidOperationException(
                    $"Material instance override '{propertyNode.Name}' must be an object containing 'type' and 'value'.");
            }

            materialInstanceData.SetPropertyOverride(propertyNode.Name, MaterialValueJsonSerializer.LoadTyped(valueNode));
        }
    }
}