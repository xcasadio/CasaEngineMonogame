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

    [Theory]
    [InlineData("LitForward.fx")]
    [InlineData("skinEffect.fx")]
    public void MaterialFacingLightingShaders_DeclareExpandedLocalLightSlots(string shaderFileName)
    {
        string source = LoadShaderSource(shaderFileName);

        for (int index = 0; index < LightingContext.MaxPointLights; index++)
        {
            Assert.Contains($"PointLight{index}PositionAndRange", source);
            Assert.Contains($"PointLight{index}DiffuseColor", source);
            Assert.Contains($"PointLight{index}SpecularColor", source);
        }

        for (int index = 0; index < LightingContext.MaxSpotLights; index++)
        {
            Assert.Contains($"SpotLight{index}PositionAndRange", source);
            Assert.Contains($"SpotLight{index}DirectionAndInnerConeCos", source);
            Assert.Contains($"SpotLight{index}DiffuseColor", source);
            Assert.Contains($"SpotLight{index}SpecularColorAndOuterConeCos", source);
        }

        Assert.DoesNotContain("PointLightPositionAndRange[", source);
        Assert.DoesNotContain("SpotLightPositionAndRange[", source);
    }

    [Fact]
    public void LightingInclude_UsesExplicitLocalLightSlotAccumulation()
    {
        string includeSource = LoadShaderSource("Lighting.fxh");

        Assert.Contains("AccumulatePointLightSlots", includeSource);
        Assert.Contains("AccumulateSpotLightSlots", includeSource);
        Assert.Contains("PointLight1PositionAndRange, PointLight1DiffuseColor, PointLight1SpecularColor", includeSource);
        Assert.Contains("SpotLight1PositionAndRange, SpotLight1DirectionAndInnerConeCos, SpotLight1DiffuseColor, SpotLight1SpecularColorAndOuterConeCos", includeSource);
        Assert.DoesNotContain("GetPointLightPositionAndRange(", includeSource);
        Assert.DoesNotContain("GetSpotLightPositionAndRange(", includeSource);
        Assert.DoesNotContain("pointIndex", includeSource);
        Assert.DoesNotContain("spotIndex", includeSource);
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

    [Fact]
    public void LitForwardShader_ConsumesDirectionalShadowBindingsWithoutShadowingAmbientOrEnvironment()
    {
        string litForwardSource = LoadShaderSource("LitForward.fx");
        string lightingIncludeSource = LoadShaderSource("Lighting.fxh");

        Assert.Contains("DECLARE_TEXTURE(ShadowMapTexture, 6)", litForwardSource);
        Assert.Contains("ActiveShadowLightCount", litForwardSource);
        Assert.Contains("ShadowLightViewProjection", litForwardSource);
        Assert.Contains("ShadowMapTexelSize", litForwardSource);
        Assert.Contains("ReceiveShadows", litForwardSource);
        Assert.Contains("#define HAS_FORWARD_SHADOW_BINDINGS 1", litForwardSource);

        Assert.Contains("ComputeDirectionalShadowFactor", lightingIncludeSource);
        Assert.Contains("ShadowedDirectionalLightIndex", lightingIncludeSource);
        Assert.Contains("ActiveShadowLightCount <= 0.0f || ReceiveShadows <= 0.5f", lightingIncludeSource);
        Assert.Contains("directionalShadowFactor", lightingIncludeSource);
        Assert.DoesNotContain("ComputeAmbientTerm(AmbientColor, MaterialAmbientColor) * ComputeDirectionalShadowFactor", lightingIncludeSource);
        Assert.DoesNotContain("ComputeEnvironmentDiffuseTerm", lightingIncludeSource[lightingIncludeSource.IndexOf("ComputeDirectionalShadowFactor", StringComparison.Ordinal)..]);
    }

    [Fact]
    public void SkinnedLightingShader_DeclaresShadowBindingsAndDepthTechniques()
    {
        string skinnedSource = LoadShaderSource("skinEffect.fx");

        Assert.Contains("DECLARE_TEXTURE(ShadowMapTexture, 6)", skinnedSource);
        Assert.Contains("ActiveShadowLightCount", skinnedSource);
        Assert.Contains("ShadowedDirectionalLightIndex", skinnedSource);
        Assert.Contains("ShadowLightViewProjection", skinnedSource);
        Assert.Contains("ShadowMapTexelSize", skinnedSource);
        Assert.Contains("ReceiveShadows", skinnedSource);
        Assert.Contains("#define HAS_FORWARD_SHADOW_BINDINGS 1", skinnedSource);
        Assert.Contains("RiggedModelShadowDepth", skinnedSource);
        Assert.Contains("RiggedModelShadowDepthDualQuaternion", skinnedSource);
        Assert.Contains("VertexShaderRiggedModelShadowDepthDualQuaternion", skinnedSource);
    }

    [Fact]
    public void SkinnedLightingShader_ConvergesAmbientAndAlphaTestWithLitForward()
    {
        string skinnedSource = LoadShaderSource("skinEffect.fx");

        // Ambient term uses the same AmbientColor/MaterialAmbientColor/EnvironmentAmbientColor
        // formula as LitForward.fx's ComputeBaseAmbientTerm, instead of ignoring the
        // material's own ambient tint.
        Assert.Contains("float3 MaterialAmbientColor", skinnedSource);
        Assert.Contains(
            "ComputeEnvironmentDiffuseTerm(SampleEnvironmentDiffuse(worldNormal) * EnvironmentAmbientColor, MaterialAmbientColor)",
            skinnedSource);
        Assert.Contains("ComputeAmbientTerm(AmbientColor, MaterialAmbientColor)", skinnedSource);
        Assert.Contains("ComposeLitSurfaceColor(color.rgb, directDiffuse, ambientContribution, EmissiveColor)", skinnedSource);

        // Alpha test uses the same AlphaCutoff parameter/convention as LitForward.fx
        // (disabled when <= 0), applied in the lit pixel shader shared by both skinned techniques.
        Assert.Contains("float AlphaCutoff", skinnedSource);
        Assert.Contains("if (AlphaCutoff > 0.0f)", skinnedSource);
        Assert.Contains("clip(alpha - AlphaCutoff)", skinnedSource);
        Assert.Contains("ApplySkinnedAlphaTest(texelColor.a)", skinnedSource);
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