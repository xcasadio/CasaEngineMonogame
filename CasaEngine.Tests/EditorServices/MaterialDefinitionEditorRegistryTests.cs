using CasaEngine.EditorServices.Materials;
using Xunit;

namespace CasaEngine.Tests.EditorServices;

public class MaterialDefinitionEditorRegistryTests
{
    [Fact]
    public void GetDescriptors_LitDiffuseDefinition_UsesSemanticGroupsAndControlHints()
    {
        var registry = MaterialDefinitionEditorRegistry.Default;

        var descriptors = registry.GetDescriptors("lit-diffuse");

        Assert.Equal(7, descriptors.Count);

        var baseColor = GetDescriptor(descriptors, "base_color_texture");
        Assert.Equal("Surface", baseColor.Category);
        Assert.Equal("AssetPicker", baseColor.EditorControlHint);
        Assert.Equal(0, baseColor.DisplayOrder);

        var normalTexture = GetDescriptor(descriptors, "normal_texture");
        Assert.Equal("Normals", normalTexture.Category);
        Assert.Equal("AssetPicker", normalTexture.EditorControlHint);

        var diffuseColor = GetDescriptor(descriptors, "diffuse_color");
        Assert.Equal("Surface", diffuseColor.Category);
        Assert.Equal("ColorPicker", diffuseColor.EditorControlHint);

        var alphaCutoff = GetDescriptor(descriptors, "alpha_cutoff");
        Assert.Equal("Surface", alphaCutoff.Category);
        Assert.Equal("Slider", alphaCutoff.EditorControlHint);

        var emissiveColor = GetDescriptor(descriptors, "emissive_color");
        Assert.Equal("Emission", emissiveColor.Category);
        Assert.Equal("Vector3Editor", emissiveColor.EditorControlHint);

        var specularColor = GetDescriptor(descriptors, "specular_color");
        Assert.Equal("PBR", specularColor.Category);
        Assert.Equal("Vector3Editor", specularColor.EditorControlHint);

        var specularPower = GetDescriptor(descriptors, "specular_power");
        Assert.Equal("PBR", specularPower.Category);
        Assert.Equal("Slider", specularPower.EditorControlHint);
    }

    [Fact]
    public void GetSections_LitDiffuseDefinition_ReturnsOrderedNonEmptySemanticSections()
    {
        var registry = MaterialDefinitionEditorRegistry.Default;

        var sections = registry.GetSections("lit-diffuse");

        Assert.Collection(
            sections,
            surface =>
            {
                Assert.Equal("Surface", surface.Key);
                Assert.Equal(new[] { "base_color_texture", "diffuse_color", "alpha_cutoff" }, GetKeys(surface));
            },
            normals =>
            {
                Assert.Equal("Normals", normals.Key);
                Assert.Equal(new[] { "normal_texture" }, GetKeys(normals));
            },
            pbr =>
            {
                Assert.Equal("PBR", pbr.Key);
                Assert.Equal(new[] { "specular_color", "specular_power" }, GetKeys(pbr));
            },
            emission =>
            {
                Assert.Equal("Emission", emission.Key);
                Assert.Equal(new[] { "emissive_color" }, GetKeys(emission));
            });
    }

    [Fact]
    public void GetSections_LegacyMultiTextureDefinition_RoutesLegacySlotsToSemanticSections()
    {
        var registry = MaterialDefinitionEditorRegistry.Default;

        var sections = registry.GetSections("legacy-multi-texture");

        Assert.Collection(
            sections,
            surface =>
            {
                Assert.Equal("Surface", surface.Key);
                Assert.Equal(new[] { "base_color_texture", "opacity_texture" }, GetKeys(surface));
            },
            normals =>
            {
                Assert.Equal("Normals", normals.Key);
                Assert.Equal(new[] { "normal_texture", "tangent_texture", "height_texture" }, GetKeys(normals));
            },
            pbr =>
            {
                Assert.Equal("PBR", pbr.Key);
                Assert.Equal(new[] { "specular_texture", "roughness_texture", "reflection_texture" }, GetKeys(pbr));
            });
    }

    private static MaterialPropertyDescriptor GetDescriptor(
        IReadOnlyList<MaterialPropertyDescriptor> descriptors,
        string key)
    {
        for (int i = 0; i < descriptors.Count; i++)
        {
            if (string.Equals(descriptors[i].Key, key, StringComparison.Ordinal))
            {
                return descriptors[i];
            }
        }

        throw new Xunit.Sdk.XunitException($"Descriptor '{key}' was not found.");
    }

    private static string[] GetKeys(MaterialPropertySectionDescriptor section)
    {
        var keys = new string[section.Properties.Count];
        for (int i = 0; i < section.Properties.Count; i++)
        {
            keys[i] = section.Properties[i].Key;
        }

        return keys;
    }
}