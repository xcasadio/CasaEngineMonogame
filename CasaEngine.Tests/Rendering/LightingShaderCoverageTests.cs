using CasaEngine.Framework.Rendering;
using CasaEngine.Framework.Rendering.Shaders;
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
    public void LitForwardShader_ConsumesDirectionalShadowBindingsWithoutShadowingAmbientOrEnvironmentInTheDefaultMode()
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

        // Default mode only: the shadow factor scales the shadowed light, never the ambient term. In MultiplyLitColor mode the
        // light is left alone and the tint (which does cover the ambient part of the colour) is applied by the pixel shaders.
        Assert.Contains("float lightOcclusionFactor = ShadowApplyMode > 0.5f ? 1.0f : shadowFactor;", lightingIncludeSource);
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
        Assert.Contains("ComputeSkinnedAmbientContribution(worldNormal)", skinnedSource);
        Assert.Contains("ComposeLitSurfaceColor(color.rgb, directDiffuse, ambientContribution, MaterialEmissiveColor())", skinnedSource);

        // Alpha test uses the same AlphaCutoff parameter/convention as LitForward.fx
        // (disabled when <= 0), applied in the lit pixel shader shared by both skinned techniques.
        Assert.Contains("float AlphaCutoff", skinnedSource);
        Assert.Contains("if (AlphaCutoff > 0.0f)", skinnedSource);
        Assert.Contains("clip(alpha - AlphaCutoff)", skinnedSource);
        Assert.Contains("ApplySkinnedAlphaTest(texelColor.a)", skinnedSource);
    }

    [Theory]
    [InlineData("LitForward.fx", "float3 ComputeBaseAmbientTerm(", "ComputeBaseAmbientTerm(")]
    [InlineData("skinEffect.fx", "float3 ComputeSkinnedAmbientContribution(", "ComputeSkinnedAmbientContribution(")]
    public void AmbientTerm_DoesNotApplyTheBaseColor_BecauseComposeLitSurfaceColorAppliesItOnce(
        string shaderFileName,
        string definitionPrefix,
        string callPrefix)
    {
        string source = LoadShaderSource(shaderFileName);
        string lightingIncludeSource = LoadShaderSource("Lighting.fxh");

        int definitionStart = source.IndexOf(definitionPrefix, StringComparison.Ordinal);
        Assert.True(definitionStart >= 0, $"{definitionPrefix} is not defined in {shaderFileName}.");
        int bodyEnd = source.IndexOf("\n}", definitionStart, StringComparison.Ordinal);
        string definition = source[definitionStart..bodyEnd];

        // The ambient term takes only the normal: it has no base colour to multiply by.
        Assert.Contains("(float3 worldNormal)", definition);
        Assert.DoesNotContain("baseColor", definition);
        Assert.DoesNotContain("color.rgb", definition);
        Assert.Contains("return MaterialDiffuseColor() * ambientTerm;", definition);

        // No caller hands the surface colour to the ambient term.
        Assert.DoesNotContain(callPrefix + "color.rgb", source);
        Assert.DoesNotContain(callPrefix + "baseColor", source);
        Assert.DoesNotContain(callPrefix + "texelColor", source);

        // The surface colour is applied by exactly one place: ComposeLitSurfaceColor.
        Assert.Contains("return albedo * (directDiffuse + ambientTerm) + emissiveColor;", lightingIncludeSource);
    }

    [Theory]
    [InlineData("LitForward.fx", "float3 ComputeBaseAmbientTerm(")]
    [InlineData("skinEffect.fx", "float3 ComputeSkinnedAmbientContribution(")]
    public void AmbientSource_IsDeclaredInBothShadersAndSelectsTheFlatAmbientPath(string shaderFileName, string definitionPrefix)
    {
        string source = LoadShaderSource(shaderFileName);
        string lightingIncludeSource = LoadShaderSource("Lighting.fxh");

        // The flag has its own uniform, distinct from HasEnvironmentCubeTexture, bound by name from the engine.
        Assert.Contains($"float {ShaderParameterNames.UseFlatAmbientSource} _ps(c36) _cb(c32.z);", source);
        Assert.Equal("UseFlatAmbientSource", ShaderParameterNames.UseFlatAmbientSource);

        // Its slot is not shared with another parameter of the same shader.
        Assert.Equal(1, CountOccurrences(source, "_cb(c32.z)"));
        Assert.Equal(1, CountOccurrences(source, "UseFlatAmbientSource _ps("));

        // The ambient helper falls back to the flat path when the flag is set, even with a cube bound.
        int definitionStart = source.IndexOf(definitionPrefix, StringComparison.Ordinal);
        Assert.True(definitionStart >= 0, $"{definitionPrefix} is not defined in {shaderFileName}.");
        string definition = source[definitionStart..source.IndexOf("\n}", definitionStart, StringComparison.Ordinal)];
        Assert.Contains("(HasEnvironmentCubeTexture > 0.5f && UseFlatAmbientSource < 0.5f)", definition);
        Assert.Contains("ComputeAmbientTerm(AmbientColor, MaterialAmbientColor)", definition);

        // Reflections keep following the cube: the flag only concerns the ambient term.
        Assert.DoesNotContain("UseFlatAmbientSource", lightingIncludeSource);
    }

    [Theory]
    [InlineData("LitForward.fx")]
    [InlineData("skinEffect.fx")]
    public void ShadowStyleUniforms_AreDeclaredInBothShadersWithoutSharingASlot(string shaderFileName)
    {
        string source = LoadShaderSource(shaderFileName);

        Assert.Equal("ShadowApplyMode", ShaderParameterNames.ShadowApplyMode);
        Assert.Equal("ShadowTintColor", ShaderParameterNames.ShadowTintColor);
        Assert.Contains($"float {ShaderParameterNames.ShadowApplyMode} _ps(c101) _cb(c91.w);", source);
        Assert.Contains($"float3 {ShaderParameterNames.ShadowTintColor} _ps(c106) _cb(c96);", source);

        Assert.Equal(1, CountOccurrences(source, "_cb(c91.w)"));
        Assert.Equal(1, CountOccurrences(source, "_cb(c96)"));
    }

    [Fact]
    public void ShadowTint_IsAppliedByEveryShadowReceivingPixelShaderAndNeverToTheEmissiveColour()
    {
        string litForwardSource = LoadShaderSource("LitForward.fx");
        string skinnedSource = LoadShaderSource("skinEffect.fx");
        string lightingIncludeSource = LoadShaderSource("Lighting.fxh");

        // The tint takes the emissive colour so that it can leave it out; it is skipped entirely in the default mode.
        Assert.Contains("float3 ApplyShadowTint(float3 litColor, float3 emissiveColor, float shadowFactor)", lightingIncludeSource);
        Assert.Contains("if (ShadowApplyMode > 0.5f)", lightingIncludeSource);
        Assert.Contains("litColor += (ComputeShadowTint(shadowFactor) - 1.0f) * (litColor - emissiveColor);", lightingIncludeSource);

        // The shadow factor is computed once per pixel, outside the light loop, and handed to the pixel shader.
        Assert.Equal(1, CountOccurrences(lightingIncludeSource, "ComputeDirectionalShadowFactor(worldPosition, worldNormal);"));
        Assert.Contains("result.ShadowFactor = ", lightingIncludeSource);

        // Pixel-lighting pixel shaders of LitForward: the plain ones tint after the specular, the reflection ones tint inside
        // the reflection helper (so nothing is tinted twice and the emissive part of the reflection scale is left alone).
        Assert.Equal(5, CountOccurrences(litForwardSource, "ApplyShadowTint(color.rgb, MaterialEmissiveColor(),"));
        Assert.Equal(3, CountOccurrences(litForwardSource, "ApplyReflectionContributionWithShadowTint(color.rgb, MaterialEmissiveColor(),"));
        Assert.Equal(1, CountOccurrences(litForwardSource, "ApplyReflectionContribution(surfaceColor, reflectionContribution)"));
        Assert.DoesNotContain("color.rgb = ApplyReflectionContribution(", litForwardSource);

        // The reflection helper tints only the non-emissive part of the linear form.
        Assert.Contains("float3 emissiveReflected = emissiveColor * (ReflectionMultiplyBase + reflectionContribution * ReflectionMultiplyFactor);", litForwardSource);
        Assert.Contains("reflected += (ComputeShadowTint(shadowFactor) - 1.0f) * (reflected - emissiveReflected);", litForwardSource);

        // The skinned pixel shader tints after the specular too.
        Assert.Equal(1, CountOccurrences(skinnedSource, "ApplyShadowTint(color.rgb, MaterialEmissiveColor(), lightResult.ShadowFactor)"));

        // Vertex-lighting techniques have no shadows: their pixel shaders never tint.
        int vertexLightingStart = litForwardSource.IndexOf("float4 PSBasicVertexLighting(", StringComparison.Ordinal);
        int vertexLightingEnd = litForwardSource.IndexOf("// Untinted pixel lighting colour", StringComparison.Ordinal);
        Assert.DoesNotContain("ShadowTint", litForwardSource[vertexLightingStart..vertexLightingEnd]);
    }

    [Theory]
    [InlineData("LitForward.fx")]
    [InlineData("skinEffect.fx")]
    public void ShadowFilterRadius_IsDeclaredInBothShadersAndDrivesAFixedPoissonDisc(string shaderFileName)
    {
        string source = LoadShaderSource(shaderFileName);
        string lightingIncludeSource = LoadShaderSource("Lighting.fxh");

        Assert.Equal("ShadowFilterRadius", ShaderParameterNames.ShadowFilterRadius);
        Assert.Contains($"float {ShaderParameterNames.ShadowFilterRadius} _ps(c106) _cb(c96.w);", source);
        Assert.Equal(1, CountOccurrences(source, "_cb(c96.w)"));

        // A radius of 0 keeps the 4 point samples; above 0 the receiver walks a fixed table of points scaled by the radius.
        int definitionStart = lightingIncludeSource.IndexOf("float SampleDirectionalShadowPcf(", StringComparison.Ordinal);
        Assert.True(definitionStart >= 0, "SampleDirectionalShadowPcf is not defined in Lighting.fxh.");
        string definition = lightingIncludeSource[definitionStart..lightingIncludeSource.IndexOf("\n}", definitionStart, StringComparison.Ordinal)];
        Assert.Contains("if (ShadowFilterRadius > 0.0f)", definition);
        Assert.Contains("ShadowMapTexelSize * ShadowFilterRadius", definition);
        Assert.Contains("ShadowPoissonDisc[tap] * radiusUv", definition);
        Assert.Contains("float2(-0.5f, -0.5f)", definition);
        Assert.Contains("float2(0.5f, 0.5f)", definition);
        Assert.Contains("visibility *= 0.25f;", definition);

        int tableStart = lightingIncludeSource.IndexOf("ShadowPoissonDisc[", StringComparison.Ordinal);
        string table = lightingIncludeSource[tableStart..lightingIncludeSource.IndexOf("};", tableStart, StringComparison.Ordinal)];
        Assert.Contains("static const float2 ShadowPoissonDisc[12]", lightingIncludeSource);
        Assert.Contains("static const int ShadowPoissonTapCount = 12;", lightingIncludeSource);
        Assert.Equal(12, CountOccurrences(table, "float2("));
    }

    [Theory]
    [InlineData("LitForward.fx")]
    [InlineData("skinEffect.fx")]
    public void ShadowFadeWidth_IsDeclaredInBothShadersAndFadesTheFactorToLitOnASquareBand(string shaderFileName)
    {
        string source = LoadShaderSource(shaderFileName);
        string lightingIncludeSource = LoadShaderSource("Lighting.fxh");

        Assert.Equal("ShadowFadeWidth", ShaderParameterNames.ShadowFadeWidth);
        Assert.Contains($"float {ShaderParameterNames.ShadowFadeWidth} _ps(c107) _cb(c97.x);", source);
        Assert.Equal(1, CountOccurrences(source, "_cb(c97.x)"));

        int definitionStart = lightingIncludeSource.IndexOf("float ComputeDirectionalShadowFactor(", StringComparison.Ordinal);
        string definition = lightingIncludeSource[definitionStart..lightingIncludeSource.IndexOf("\n}", definitionStart, StringComparison.Ordinal)];
        Assert.Contains("if (ShadowFadeWidth > 0.0f)", definition);
        Assert.Contains("max(abs(shadowNdc.x), abs(shadowNdc.y))", definition);
        Assert.Contains("visibility = lerp(visibility, 1.0f, saturate((edgeDistance - (1.0f - ShadowFadeWidth)) / ShadowFadeWidth));", definition);
    }

    [Theory]
    [InlineData(48.0f, 48.0f)]
    [InlineData(1.0f, 1.0f)]
    [InlineData(0.25f, 1.0f)]
    [InlineData(0.0f, 1.0f)]
    public void ShadowPass_LateralExtentIsTheMaximumDistanceAtLeastOne(float maxDistance, float expected)
    {
        Assert.Equal(expected, CasaEngine.Framework.Rendering.Draw.ShadowPass.ResolveLateralExtent(maxDistance));
    }

    [Fact]
    public void DetailTexture_IsDeclaredInLitForwardAndMultipliesTheBaseColourInsideSampleBaseTexture()
    {
        string source = LoadShaderSource("LitForward.fx");

        // The sampler takes the next free texture slot (0 to 6 are taken) and the two uniforms are bound by name.
        Assert.Contains("DECLARE_TEXTURE(DetailTexture, 7);", source);
        Assert.Contains($"float {ShaderParameterNames.HasDetailTexture} ", source);
        Assert.Contains($"float {ShaderParameterNames.DetailTiling} ", source);
        Assert.Equal(1, CountOccurrences(source, "DECLARE_TEXTURE(DetailTexture,"));
        Assert.Equal(1, CountOccurrences(source, "HasDetailTexture _ps("));
        Assert.Equal(1, CountOccurrences(source, "DetailTiling _ps("));

        // The multiplication lives in SampleBaseTexture, guarded by the flag, on the colour channels only.
        int definitionStart = source.IndexOf("float4 SampleBaseTexture(", StringComparison.Ordinal);
        Assert.True(definitionStart >= 0, "SampleBaseTexture is not defined in LitForward.fx.");
        string definition = source[definitionStart..source.IndexOf("\n}", definitionStart, StringComparison.Ordinal)];
        Assert.Contains("if (HasDetailTexture > 0.5f)", definition);
        Assert.Contains("baseColor.rgb *= 2.0f * SAMPLE_TEXTURE(DetailTexture, texCoord * DetailTiling).rgb;", definition);
        Assert.DoesNotContain("baseColor.a", definition);
        Assert.Equal(1, CountOccurrences(source, "SAMPLE_TEXTURE(DetailTexture,"));

        // Every textured pixel shader goes through SampleBaseTexture: no direct read of the base texture elsewhere.
        Assert.Equal(1, CountOccurrences(source, "SAMPLE_TEXTURE(Texture,"));
    }

    [Fact]
    public void DetailTexture_IsNotSupportedByTheSkinnedShaderNorTheShadowPass()
    {
        string skinnedSource = LoadShaderSource("skinEffect.fx");
        string lightingIncludeSource = LoadShaderSource("Lighting.fxh");

        Assert.DoesNotContain("DetailTexture", skinnedSource);
        Assert.DoesNotContain("DetailTexture", lightingIncludeSource);
    }

    private static int CountOccurrences(string text, string value)
    {
        int count = 0;
        int index = 0;
        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
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