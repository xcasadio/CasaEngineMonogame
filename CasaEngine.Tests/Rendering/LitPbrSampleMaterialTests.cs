using CasaEngine.EditorServices.Materials;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Rendering;
using CasaEngine.Framework.Rendering.Shaders;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.Rendering;

/// <summary>The hand-written lit-pbr sample opened by the editor: it loads, compiles and is classified by the editor registry.</summary>
public class LitPbrSampleMaterialTests
{
    private const string SamplePath = "CasaEngine.Demos/Content/Materials/pbr-sample.material";

    [Fact]
    public void Sample_LoadsThroughTheSerializer_CompilesAndGivesALitPbrMaterial()
    {
        var document = JObject.Parse(File.ReadAllText(Path.Combine(FindRepositoryRoot(), SamplePath)));
        var materialAsset = new MaterialAsset();
        MaterialAssetJsonSerializer.Load(materialAsset, document);

        Assert.Equal("lit-pbr", materialAsset.DefinitionId);

        var compiler = new MaterialCompiler();
        var assetContentManager = new AssetContentManager();
        var compiled = compiler.Compile(materialAsset, assetContentManager);
        var runtime = Assert.IsType<LitPbrMaterial>(compiler.CompileRuntimeMaterial(materialAsset, assetContentManager));

        Assert.Equal(EffectiveShaderResolver.LitPbrShaderId, compiled.EffectiveShader.ShaderId);
        Assert.Equal(RenderQueue.Opaque, compiled.Queue);
        Assert.Equal(new Color(200, 120, 60, 255), runtime.BaseColor);
        Assert.Equal(0.8f, runtime.Metallic);
        Assert.Equal(0.35f, runtime.Roughness);
    }

    [Fact]
    public void Sample_IsCopiedToTheDemoOutputLikeItsSiblingContent()
    {
        string root = FindRepositoryRoot();
        string contentBuild = File.ReadAllText(Path.Combine(root, "CasaEngine.Demos/Content/Content.mgcb"));
        string assetInfos = File.ReadAllText(Path.Combine(root, "CasaEngine.Demos/Content/AssetInfos.json"));

        Assert.Contains("/copy:Materials/pbr-sample.material", contentBuild);
        Assert.Contains("Materials\\\\pbr-sample.material", assetInfos);
    }

    [Theory]
    [InlineData("base_color_texture", "Surface", "AssetPicker")]
    [InlineData("base_color", "Surface", "ColorPicker")]
    [InlineData("alpha_cutoff", "Surface", "Slider")]
    [InlineData("normal_texture", "Normals", "AssetPicker")]
    [InlineData("normal_scale", "Normals", "NumberBox")]
    [InlineData("metallic_roughness_texture", "PBR", "AssetPicker")]
    [InlineData("metallic", "PBR", "Slider")]
    [InlineData("roughness", "PBR", "Slider")]
    [InlineData("occlusion_texture", "PBR", "AssetPicker")]
    [InlineData("occlusion_strength", "PBR", "Slider")]
    [InlineData("emissive_texture", "Emission", "AssetPicker")]
    [InlineData("emissive_color", "Emission", "ColorPicker")]
    [InlineData("emissive_shadow_factor", "Emission", "Slider")]
    [InlineData("detail_texture", "Surface", "AssetPicker")]
    [InlineData("detail_tiling", "Surface", "NumberBox")]
    [InlineData("clear_coat", "PBR", "Slider")]
    [InlineData("clear_coat_roughness", "PBR", "Slider")]
    public void Registry_ClassifiesTheLitPbrKeys(string key, string category, string hint)
    {
        var descriptors = MaterialDefinitionEditorRegistry.Default.GetDescriptors("lit-pbr");

        Assert.Equal(17, descriptors.Count);
        var descriptor = descriptors.Single(d => d.Key == key);
        Assert.Equal(category, descriptor.Category);
        Assert.Equal(hint, descriptor.EditorControlHint);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CasaEngine.MonoGame.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("CasaEngine repository root was not found.");
    }
}
