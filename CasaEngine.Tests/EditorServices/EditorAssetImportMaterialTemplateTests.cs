using CasaEngine.EditorServices;
using CasaEngine.Engine.Environment;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Assets.Loaders;
using CasaEngine.Framework.Materials.Compilation;
using CasaEngine.Framework.Materials.Runtime;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.EditorServices;

/// <summary>The material template option of the editor import of glTF static models (T9.2, ADR-0112).</summary>
[Collection(ProjectEnvironmentCollection.Name)]
public class EditorAssetImportMaterialTemplateTests
{
    private const float ColorTolerance = 1.0f;
    private const float FloatTolerance = 0.0001f;

    // 0.5 encoded with the 2.2 power: 0.5 ^ (1 / 2.2) = 0.7297, that is 186 / 255.
    private const float EncodedHalf = 0.7297f;

    [Fact]
    public void ImportFile_LitPbrWritesTheGltfChannelsOfTheOpaqueMaterial()
    {
        WithImportedProbe(
            new EditorAssetImportOptions { MaterialTemplate = ImportedMaterialTemplate.LitPbr },
            directory =>
            {
                JObject material = ReadMaterial(directory, "Opaque");
                Assert.Equal("lit-pbr", (string?)material["definition_id"]);
                Assert.Equal("Opaque", (string?)material["queue"]);
                Assert.Equal("CullCounterClockwise", (string?)material["rasterizer_state"]);

                JToken properties = material["properties"]!;
                Assert.Equal(0.3f, (float)properties["metallic"]!, FloatTolerance);
                Assert.Equal(0.6f, (float)properties["roughness"]!, FloatTolerance);
                Assert.Equal(0.7f, (float)properties["normal_scale"]!, FloatTolerance);
                Assert.Equal(0.4f, (float)properties["occlusion_strength"]!, FloatTolerance);
                Assert.Equal(0.5f, (float)properties["alpha_cutoff"]!, FloatTolerance);

                // The glTF factor 0.5 is read as byte 127 and encoded to the display colour 186 / 255.
                JToken baseColor = properties["base_color"]!;
                Assert.InRange((float)baseColor["r"]!, 186 - ColorTolerance, 186 + ColorTolerance);
                Assert.InRange((float)baseColor["g"]!, 186 - ColorTolerance, 186 + ColorTolerance);
                Assert.InRange((float)baseColor["b"]!, 186 - ColorTolerance, 186 + ColorTolerance);
                Assert.Equal(255, (int)baseColor["a"]!);

                JToken emissive = properties["emissive_color"]!;
                Assert.Equal(EncodedHalf, (float)emissive["x"]!, 0.005f);
                Assert.Equal(EncodedHalf, (float)emissive["y"]!, 0.005f);
                Assert.Equal(EncodedHalf, (float)emissive["z"]!, 0.005f);

                Guid baseTexture = ReadTextureId(properties, "base_color_texture");
                Guid normalTexture = ReadTextureId(properties, "normal_texture");
                Guid metallicRoughnessTexture = ReadTextureId(properties, "metallic_roughness_texture");
                Guid occlusionTexture = ReadTextureId(properties, "occlusion_texture");
                Guid emissiveTexture = ReadTextureId(properties, "emissive_texture");
                Assert.Equal(5, new HashSet<Guid> { baseTexture, normalTexture, metallicRoughnessTexture, occlusionTexture, emissiveTexture }.Count);

                // Each identifier is the one of the texture wrapper the import wrote for the matching image.
                Assert.Equal(baseTexture, ReadWrapperId(directory, "base"));
                Assert.Equal(normalTexture, ReadWrapperId(directory, "normal"));
                Assert.Equal(metallicRoughnessTexture, ReadWrapperId(directory, "mr"));
                Assert.Equal(occlusionTexture, ReadWrapperId(directory, "occlusion"));
                Assert.Equal(emissiveTexture, ReadWrapperId(directory, "emissive"));

                // The written document loads back as a lit-pbr material with the same values.
                var loaded = new CasaEngine.Framework.Materials.Runtime.MaterialAsset();
                loaded.Load(material);
                Assert.Equal("lit-pbr", loaded.DefinitionId);
                Assert.True(loaded.GetPropertyValueOrDefault("metallic_roughness_texture")!.TryGetTextureId(out var loadedMetallicRoughness));
                Assert.Equal(metallicRoughnessTexture, loadedMetallicRoughness);
                Assert.True(loaded.GetPropertyValueOrDefault("roughness")!.TryGetFloat(out var loadedRoughness));
                Assert.Equal(0.6f, loadedRoughness, FloatTolerance);
            });
    }

    [Fact]
    public void ImportFile_LitPbrMapsTheAlphaModesAndTheDoubleSidedFlag()
    {
        WithImportedProbe(
            new EditorAssetImportOptions { MaterialTemplate = ImportedMaterialTemplate.LitPbr },
            directory =>
            {
                JObject masked = ReadMaterial(directory, "Masked");
                Assert.Equal("lit-pbr", (string?)masked["definition_id"]);
                Assert.Equal("AlphaTest", (string?)masked["queue"]);
                Assert.Equal("CullNone", (string?)masked["rasterizer_state"]);
                JToken maskedProperties = masked["properties"]!;
                Assert.Equal(0.35f, (float)maskedProperties["alpha_cutoff"]!, FloatTolerance);
                Assert.Equal(0.9f, (float)maskedProperties["metallic"]!, FloatTolerance);
                Assert.Equal(0.1f, (float)maskedProperties["roughness"]!, FloatTolerance);
                AssertNoTextureKeys(maskedProperties);

                JObject blended = ReadMaterial(directory, "Blended");
                Assert.Equal("Transparent", (string?)blended["queue"]);
                Assert.Equal("CullCounterClockwise", (string?)blended["rasterizer_state"]);
                JToken blendedColor = blended["properties"]!["base_color"]!;
                Assert.Equal(255, (int)blendedColor["r"]!);
                Assert.InRange((int)blendedColor["a"]!, 127, 128);

                JObject opaqueDoubleSided = ReadMaterial(directory, "OpaqueDoubleSided");
                Assert.Equal("Opaque", (string?)opaqueDoubleSided["queue"]);
                Assert.Equal("CullNone", (string?)opaqueDoubleSided["rasterizer_state"]);
            });
    }

    [Fact]
    public void ImportFile_LitPbrWritesTheGltfDefaultsExplicitlyAndNoTextureKeysWhenTheImagesAreAbsent()
    {
        WithImportedProbe(
            new EditorAssetImportOptions { MaterialTemplate = ImportedMaterialTemplate.LitPbr },
            directory =>
            {
                foreach (string name in new[] { "Plain", "Unlit" })
                {
                    JObject material = ReadMaterial(directory, name);
                    JToken properties = material["properties"]!;
                    Assert.Equal("lit-pbr", (string?)material["definition_id"]);
                    Assert.Equal("Opaque", (string?)material["queue"]);
                    Assert.Equal(1.0f, (float)properties["roughness"]!, FloatTolerance);
                    Assert.Equal(1.0f, (float)properties["normal_scale"]!, FloatTolerance);
                    Assert.Equal(1.0f, (float)properties["occlusion_strength"]!, FloatTolerance);
                    Assert.Equal(0.5f, (float)properties["alpha_cutoff"]!, FloatTolerance);
                    Assert.Equal(0.0f, (float)properties["emissive_color"]!["x"]!, FloatTolerance);
                    Assert.Equal(255, (int)properties["base_color"]!["r"]!);
                    AssertNoTextureKeys(properties);
                }

                Assert.Equal(1.0f, (float)ReadMaterial(directory, "Plain")["properties"]!["metallic"]!, FloatTolerance);
                Assert.Equal(0.0f, (float)ReadMaterial(directory, "Unlit")["properties"]!["metallic"]!, FloatTolerance);
            });
    }

    [Fact]
    public void ImportFile_LitPbrAssignsTheMaterialsToTheMeshesOfTheStaticModel()
    {
        WithImportedProbe(
            new EditorAssetImportOptions { MaterialTemplate = ImportedMaterialTemplate.LitPbr },
            directory =>
            {
                var materialIds = new HashSet<Guid>();
                foreach (string file in Directory.GetFiles(directory, "*.material"))
                {
                    materialIds.Add(Guid.Parse((string)JObject.Parse(File.ReadAllText(file))["id"]!));
                }

                var modelDocument = JObject.Parse(File.ReadAllText(Path.Combine(directory, "pbrprobe" + Constants.FileNameExtensions.StaticModel)));
                var meshMaterialIds = modelDocument.SelectTokens("$..material_asset_id").Select(token => Guid.Parse((string)token!)).ToList();
                Assert.Equal(6, materialIds.Count);
                Assert.Equal(6, meshMaterialIds.Count);
                Assert.True(materialIds.SetEquals(meshMaterialIds));
            });
    }

    [Fact]
    public void ImportFile_DefaultOptionsMatchTheReferenceCapturedBeforeTheMaterialTemplateExisted()
    {
        JObject reference = JObject.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "EditorServices", "Fixtures", "gltf-import-default-reference.json")));
        string expected = GltfImportProbe.ToText(reference);

        // The existing signature, then the overload with default options and with the explicit LitDiffuse template.
        Assert.Equal(expected, ImportProbeAndSnapshot(static path => EditorAssetImportService.ImportFile(path, path)));
        Assert.Equal(expected, ImportProbeAndSnapshot(static path => EditorAssetImportService.ImportFile(path, path, null, new EditorAssetImportOptions())));
        Assert.Equal(expected, ImportProbeAndSnapshot(static path => EditorAssetImportService.ImportFile(
            path,
            path,
            null,
            new EditorAssetImportOptions { MaterialTemplate = ImportedMaterialTemplate.LitDiffuse })));
    }

    [Fact]
    public void ImportFile_ExistingSignatureCompilesForExistingCallers()
    {
        Func<string, string, ILegacyMaterialImportProfile?, bool> withProfile = EditorAssetImportService.ImportFile;
        Func<string, string, bool> withoutProfile = (source, destination) => EditorAssetImportService.ImportFile(source, destination);

        Assert.NotNull(withProfile);
        Assert.NotNull(withoutProfile);
        Assert.Equal(ImportedMaterialTemplate.LitDiffuse, new EditorAssetImportOptions().MaterialTemplate);
    }

    [Fact]
    public void ImportFile_LitPbrWritesTheClearCoatKeysOnlyForAMaterialThatHasAClearCoat()
    {
        WithProbe(
            path =>
            {
                AddClearCoatToTheOpaqueMaterial(path, clearCoatFactor: 0.8f, clearCoatRoughnessFactor: 0.2f);
                return EditorAssetImportService.ImportFile(path, path, null, new EditorAssetImportOptions { MaterialTemplate = ImportedMaterialTemplate.LitPbr });
            },
            directory =>
            {
                JToken coated = ReadMaterial(directory, "Opaque")["properties"]!;
                Assert.Equal(0.8f, (float)coated["clear_coat"]!, FloatTolerance);
                Assert.Equal(0.2f, (float)coated["clear_coat_roughness"]!, FloatTolerance);

                // The other channels of the same material are read as before (roughness 0.6 set by the helper).
                Assert.Equal(0.6f, (float)coated["roughness"]!, FloatTolerance);

                foreach (string name in new[] { "Masked", "Blended", "OpaqueDoubleSided", "Plain", "Unlit" })
                {
                    JToken properties = ReadMaterial(directory, name)["properties"]!;
                    Assert.Null(properties["clear_coat"]);
                    Assert.Null(properties["clear_coat_roughness"]);
                }

                // The written document loads back as a lit-pbr material with the two values.
                var loaded = new CasaEngine.Framework.Materials.Runtime.MaterialAsset();
                loaded.Load(ReadMaterial(directory, "Opaque"));
                Assert.True(loaded.GetPropertyValueOrDefault("clear_coat")!.TryGetFloat(out var loadedClearCoat));
                Assert.Equal(0.8f, loadedClearCoat, FloatTolerance);
            });
    }

    [Fact]
    public void ImportFile_LitDiffuseIgnoresTheClearCoatOfTheGltf()
    {
        WithProbe(
            path =>
            {
                AddClearCoatToTheOpaqueMaterial(path, clearCoatFactor: 0.8f, clearCoatRoughnessFactor: 0.2f);
                return EditorAssetImportService.ImportFile(path, path, null, new EditorAssetImportOptions());
            },
            directory =>
            {
                string text = File.ReadAllText(ReadMaterialPath(directory, "Opaque"));
                Assert.DoesNotContain("clear_coat", text);
                Assert.Equal("lit-diffuse", (string?)ReadMaterial(directory, "Opaque")["definition_id"]);
            });
    }

    // glTF 2.0, "Alpha Coverage": in the OPAQUE alpha mode the alpha value of the base colour is ignored and the rendered output is
    // fully opaque. The importer therefore writes an alpha of 1 for such a material, on both templates, and keeps the factor
    // alpha for MASK and BLEND.
    [Theory]
    [InlineData(ImportedMaterialTemplate.LitPbr, "base_color")]
    [InlineData(ImportedMaterialTemplate.LitDiffuse, "diffuse_color")]
    public void ImportFile_AnOpaqueMaterialIsImportedOpaqueWhateverTheAlphaOfItsBaseColorFactor(ImportedMaterialTemplate template, string colorKey)
    {
        WithProbe(
            path =>
            {
                SetBaseColorFactorAlpha(path, "OpaqueDoubleSided", 0.5f);
                return EditorAssetImportService.ImportFile(path, path, null, new EditorAssetImportOptions { MaterialTemplate = template });
            },
            directory =>
            {
                JObject document = ReadMaterial(directory, "OpaqueDoubleSided");
                Assert.Equal("Opaque", (string?)document["queue"]);
                Assert.Equal(255, (int)document["properties"]![colorKey]!["a"]!);

                var material = new MaterialAsset();
                material.Load(document);
                var compiled = new MaterialCompiler().Compile(material, new CasaEngine.Framework.Assets.AssetContentManager());
                Assert.False(compiled.IsTransparent);
                Assert.Equal(RenderQueue.Opaque, compiled.Queue);
            });
    }

    [Theory]
    [InlineData(ImportedMaterialTemplate.LitPbr, "base_color")]
    [InlineData(ImportedMaterialTemplate.LitDiffuse, "diffuse_color")]
    public void ImportFile_AMaskedAndABlendedMaterialKeepTheAlphaOfTheirBaseColorFactor(ImportedMaterialTemplate template, string colorKey)
    {
        WithProbe(
            path =>
            {
                SetBaseColorFactorAlpha(path, "Masked", 0.5f);
                return EditorAssetImportService.ImportFile(path, path, null, new EditorAssetImportOptions { MaterialTemplate = template });
            },
            directory =>
            {
                Assert.InRange((int)ReadMaterial(directory, "Masked")["properties"]![colorKey]!["a"]!, 127, 128);
                Assert.InRange((int)ReadMaterial(directory, "Blended")["properties"]![colorKey]!["a"]!, 127, 128);
            });
    }

    private static void SetBaseColorFactorAlpha(string gltfPath, string materialName, float alpha)
    {
        var model = SharpGLTF.Schema2.ModelRoot.Load(gltfPath);
        var material = model.LogicalMaterials.Single(candidate => candidate.Name == materialName);
        // A material without a pbrMetallicRoughness block has no base colour channel until it is initialised.
        material.InitializePBRMetallicRoughness();
        var channel = material.FindChannel("BaseColor")!.Value;
        channel.Color = new System.Numerics.Vector4(1.0f, 1.0f, 1.0f, alpha);
        model.SaveGLTF(gltfPath);
    }

    private static void AddClearCoatToTheOpaqueMaterial(string gltfPath, float clearCoatFactor, float clearCoatRoughnessFactor)
    {
        var model = SharpGLTF.Schema2.ModelRoot.Load(gltfPath);
        var material = model.LogicalMaterials.Single(candidate => candidate.Name == "Opaque");
        // InitializePBRMetallicRoughness resets the metallic-roughness channel to the glTF defaults: the roughness is set again.
        material.InitializePBRMetallicRoughness("ClearCoat");
        material.FindChannel("MetallicRoughness")!.Value.SetFactor("RoughnessFactor", 0.6f);
        material.FindChannel("ClearCoat")!.Value.SetFactor("ClearCoatFactor", clearCoatFactor);
        material.FindChannel("ClearCoatRoughness")!.Value.SetFactor("RoughnessFactor", clearCoatRoughnessFactor);
        model.SaveGLTF(gltfPath);
    }

    private static string ReadMaterialPath(string directory, string name)
        => Path.Combine(directory, name + Constants.FileNameExtensions.Material);

    private static void AssertNoTextureKeys(JToken properties)
    {
        foreach (string key in new[] { "base_color_texture", "normal_texture", "metallic_roughness_texture", "occlusion_texture", "emissive_texture" })
        {
            Assert.Null(properties[key]);
        }
    }

    private static JObject ReadMaterial(string directory, string name)
        => JObject.Parse(File.ReadAllText(Path.Combine(directory, name + Constants.FileNameExtensions.Material)));

    private static Guid ReadTextureId(JToken properties, string key)
    {
        JToken? token = properties[key];
        Assert.NotNull(token);
        Guid id = Guid.Parse((string)token!);
        Assert.NotEqual(Guid.Empty, id);
        return id;
    }

    private static Guid ReadWrapperId(string directory, string imageBaseName)
        => Guid.Parse((string)JObject.Parse(File.ReadAllText(Path.Combine(directory, imageBaseName + Constants.FileNameExtensions.Texture)))["id"]!);

    private static string ImportProbeAndSnapshot(Func<string, bool> import)
    {
        string result = string.Empty;
        WithProbe(import, directory => result = GltfImportProbe.ToText(GltfImportProbe.Snapshot(directory)));
        return result;
    }

    private static void WithImportedProbe(EditorAssetImportOptions options, Action<string> assertions)
        => WithProbe(path => EditorAssetImportService.ImportFile(path, path, null, options), assertions);

    private static void WithProbe(Func<string, bool> import, Action<string> afterImport)
    {
        string directory = Path.Combine(Path.GetTempPath(), "CasaEngineMonogame", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string? previousProjectPath = EngineEnvironment.ProjectPath;

        try
        {
            EngineEnvironment.ProjectPath = directory;
            EditorAssetCatalogService.Clear();

            string gltfPath = GltfImportProbe.Write(directory);
            Assert.True(import(gltfPath));
            afterImport(directory);
        }
        finally
        {
            EditorAssetCatalogService.Clear();
            EngineEnvironment.ProjectPath = previousProjectPath;
            Directory.Delete(directory, recursive: true);
        }
    }
}
