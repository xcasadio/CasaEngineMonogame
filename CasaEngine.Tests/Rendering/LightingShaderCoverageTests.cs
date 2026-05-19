using CasaEngine.Framework.Rendering;
using Xunit;

namespace CasaEngine.Tests.Rendering;

public class LightingShaderCoverageTests
{
    [Theory]
    [InlineData("LitForward.fx")]
    [InlineData("skinEffect.fx")]
    public void MaterialFacingLightingShaders_DeclareExpandedDirectionalLightSlots(string shaderFileName)
    {
        string source = LoadShaderSource(shaderFileName);

        Assert.Contains("ActiveDirectionalLightCount", source);

        for (int i = 0; i < LightingContext.MaxDirectionalLights; i++)
        {
            Assert.Contains($"DirLight{i}Direction", source);
            Assert.Contains($"DirLight{i}DiffuseColor", source);
            Assert.Contains($"DirLight{i}SpecularColor", source);
        }
    }

    [Fact]
    public void MaterialFacingLightingShaders_UseDynamicActiveLightCountOutsideOneLightVariants()
    {
        string litForwardSource = LoadShaderSource("LitForward.fx");
        string skinnedSource = LoadShaderSource("skinEffect.fx");

        Assert.Contains("ComputeLights(eyeVector, pin.PositionWS.xyz, worldNormal, (int)ActiveDirectionalLightCount)", litForwardSource);
        Assert.DoesNotContain("ComputeLights(eyeVector, pin.PositionWS.xyz, worldNormal, 3)", litForwardSource);

        Assert.Contains("ComputeLights(eyeVector, input.Position3D, worldNormal, (int)ActiveDirectionalLightCount)", skinnedSource);
        Assert.Contains("ComputeLights(eyeVector, input.Position3D, N, (int)ActiveDirectionalLightCount)", skinnedSource);
        Assert.DoesNotContain("ComputeLights(eyeVector, input.Position3D, worldNormal, 3)", skinnedSource);
        Assert.DoesNotContain("ComputeLights(eyeVector, input.Position3D, N, 3)", skinnedSource);
    }

    [Fact]
    public void LightingInclude_SupportsConfiguredDirectionalLightCap()
    {
        string includeSource = LoadShaderSource("Lighting.fxh");
        int lastLightIndex = LightingContext.MaxDirectionalLights - 1;

        Assert.Contains($"static const int MaxDirectionalLights = {LightingContext.MaxDirectionalLights};", includeSource);
        Assert.Contains($"DirLight{lastLightIndex}Direction", includeSource);
        Assert.Contains($"DirLight{lastLightIndex}DiffuseColor", includeSource);
        Assert.Contains($"DirLight{lastLightIndex}SpecularColor", includeSource);
    }

    [Fact]
    public void SkinnedLightingShader_ConsumesEnvironmentAmbientAndReflectionBindings()
    {
        string skinnedSource = LoadShaderSource("skinEffect.fx");

        Assert.Contains("#define HAS_ENVIRONMENT_BINDINGS 1", skinnedSource);
        Assert.Contains("EnvironmentAmbientColor", skinnedSource);
        Assert.Contains("EnvironmentSpecularIntensity", skinnedSource);
        Assert.Contains("HasEnvironmentCubeTexture", skinnedSource);
        Assert.Contains("ComputeSkinnedAmbientContribution", skinnedSource);
        Assert.Contains("ComputeSkinnedReflectionContribution", skinnedSource);
        Assert.Contains("SampleEnvironmentDiffuse", skinnedSource);
        Assert.Contains("SampleEnvironmentReflection", skinnedSource);
    }

    private static string LoadShaderSource(string shaderFileName)
    {
        string shaderPath = Path.Combine(FindRepositoryRoot(), "CasaEngine", "Content", "Shaders", shaderFileName);
        return File.ReadAllText(shaderPath);
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CasaEngine.Editor.MonoGame.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Unable to locate the repository root from the test output directory.");
    }
}