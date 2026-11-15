using System.Text.RegularExpressions;

using CasaEngine.Framework.Rendering;
using CasaEngine.Framework.Rendering.Shaders;
using Xunit;

namespace CasaEngine.Tests.Rendering;

/// <summary>
/// Text coverage of the PBR shader sources (Pbr.fxh): the BRDF terms are present, the light constants are not redeclared, and
/// the attenuation formulas and slot lists restated from Lighting.fxh are identical to the originals (Lighting.fxh is not
/// modified: its attenuation is welded to the Blinn-Phong and reshaping it changed the legacy image, plan open point O10).
/// </summary>
public class PbrShaderCoverageTests
{
    [Fact]
    public void PbrInclude_DeclaresTheGgxBrdfTerms()
    {
        string source = LoadShaderSource("Pbr.fxh");

        Assert.Contains("float PbrDistributionGgx(", source);
        Assert.Contains("float PbrVisibilitySmithGgxCorrelated(", source);
        Assert.Contains("float3 PbrFresnelSchlick(", source);
        Assert.Contains("PbrSurface ComputePbrSurface(", source);
        Assert.Contains("void AccumulatePbrLight(", source);
        Assert.Contains("ColorPair EvaluatePbrLights(", source);

        // F0 = lerp(0.04, baseColor, metallic); the diffuse albedo is scaled by (1 - metallic).
        Assert.Contains("static const float PbrDielectricF0 = 0.04f;", source);
        Assert.Contains("surface.F0 = lerp(float3(PbrDielectricF0, PbrDielectricF0, PbrDielectricF0), baseColor, metallic);", source);
        Assert.Contains("surface.DiffuseAlbedo = baseColor * (1.0f - metallic);", source);

        // alpha = roughness^2, the roughness being clamped to at least 0.045 first.
        Assert.Contains("static const float PbrMinRoughness = 0.045f;", source);
        Assert.Contains("float clampedRoughness = max(roughness, PbrMinRoughness);", source);
        Assert.Contains("float alpha = clampedRoughness * clampedRoughness;", source);

        // GGX, height-correlated Smith visibility (the 4 NoL NoV of the BRDF is folded in), Schlick Fresnel.
        Assert.Contains("float d = noH * noH * (alpha2 - 1.0f) + 1.0f;", source);
        Assert.Contains("return alphaSquaredOverPi / (d * d);", source);
        Assert.Contains("return 0.5f / (noL * smithViewTerm + noV * smithLightTerm);", source);
        Assert.Contains("return f0 + (1.0f - f0) * (squared * squared * oneMinusVoH);", source);

        // Lambert diffuse scaled by (1 - F) and by 1 / pi; the two lobes stay separate.
        Assert.Contains("result.Diffuse += (1.0f - fresnel) * surface.DiffuseAlbedo * (irradiance / PbrPi);", source);
        Assert.Contains("result.Specular += fresnel * (distribution * visibility) * irradiance;", source);
    }

    [Fact]
    public void PbrInclude_DoesNotRedeclareTheLightConstantsNorIncludeAnything()
    {
        string source = LoadShaderSource("Pbr.fxh");

        // The light constants are declared once, by the effect that includes both Lighting.fxh and Pbr.fxh.
        Assert.DoesNotMatch(new Regex(@"^\s*float[1-4]?\s+(DirLight|PointLight|SpotLight)\d", RegexOptions.Multiline), source);
        Assert.DoesNotMatch(new Regex(@"^\s*float[1-4]?\s+Active(Directional|Point|Spot)LightCount", RegexOptions.Multiline), source);
        Assert.DoesNotContain("BEGIN_CONSTANTS", source);
        Assert.DoesNotContain("#include", source);
        Assert.DoesNotContain("MaxDirectionalLights =", source);
        Assert.DoesNotContain("MaxPointLights =", source);
        Assert.DoesNotContain("MaxSpotLights =", source);
    }

    [Fact]
    public void PbrInclude_UsesTheDiffuseColourOfTheLightsForBothLobesAndDecodesNothing()
    {
        string source = LoadShaderSource("Pbr.fxh");

        // The light's specular colour is ignored: no specular getter, no per-light specular constant, no material specular.
        Assert.DoesNotContain("GetDirectionalLightSpecularColor", source);
        Assert.DoesNotContain("lightSpecular", source);
        Assert.DoesNotContain("DiffuseColor.rgb", source);
        Assert.DoesNotContain("SpecularPower", source);
        Assert.DoesNotMatch(new Regex(@"(DirLight\d|PointLight\d)SpecularColor"), source);
        Assert.Equal(0, CountOccurrences(Regex.Replace(source, "SpecularColorAndOuterConeCos", string.Empty), "SpecularColor"));

        Assert.Contains("float3 lightDiffuse = GetDirectionalLightDiffuseColor(i);", source);
        Assert.Contains("AccumulatePbrLight(result, surface, -lightDirection, directionalShadowFactor, lightDiffuse);", source);
        Assert.Contains("AccumulatePbrLight(result, surface, toLight / distanceToLight, attenuation, lightDiffuse.xyz);", source);
        Assert.Contains("AccumulatePbrLight(result, surface, surfaceToLight, attenuation, lightDiffuse.xyz);", source);

        // Material colours are decoded once per pixel by the effect, never in the light slots (plan open point O10).
        Assert.DoesNotContain("DecodeColor", source);
        Assert.DoesNotContain("MaterialDiffuseColor", source);
        Assert.DoesNotContain("MaterialSpecularColor", source);
        Assert.DoesNotContain("MaterialEmissiveColor", source);
    }

    [Fact]
    public void PbrPointLightAttenuation_IsIdenticalToLightingFxh()
    {
        string lighting = LoadShaderSource("Lighting.fxh");
        string pbr = LoadShaderSource("Pbr.fxh");

        string original = BodyBeforeFinalCall(ExtractFunctionBody(lighting, "void AccumulatePointLightSlot("), "AccumulateLight(");
        string restated = BodyBeforeFinalCall(ExtractFunctionBody(pbr, "void AccumulatePbrPointLightSlot("), "AccumulatePbrLight(");

        Assert.Equal(original, restated);
        Assert.Contains("float attenuation = saturate(1.0f - distanceToLight / range);", restated);
        Assert.Contains("attenuation *= attenuation;", restated);
        Assert.Contains("if (distanceToLight <= 0.0001f || range <= 0.0f)", restated);
    }

    [Fact]
    public void PbrSpotLightAttenuation_IsIdenticalToLightingFxh()
    {
        string lighting = LoadShaderSource("Lighting.fxh");
        string pbr = LoadShaderSource("Pbr.fxh");

        string original = BodyBeforeFinalCall(ExtractFunctionBody(lighting, "void AccumulateSpotLightSlot("), "AccumulateLight(");
        string restated = BodyBeforeFinalCall(ExtractFunctionBody(pbr, "void AccumulatePbrSpotLightSlot("), "AccumulatePbrLight(");

        Assert.Equal(original, restated);
        Assert.Contains("float spotCos = dot(directionAndInnerConeCos.xyz, -surfaceToLight);", restated);
        Assert.Contains(
            "float coneAttenuation = saturate((spotCos - specularAndOuterConeCos.w) / max(directionAndInnerConeCos.w - specularAndOuterConeCos.w, 0.0001f));",
            restated);
        Assert.Contains("attenuation *= coneAttenuation;", restated);
    }

    [Fact]
    public void PbrLightSlotLists_AreIdenticalToLightingFxhOnTheSameConstants()
    {
        string lighting = LoadShaderSource("Lighting.fxh");
        string pbr = LoadShaderSource("Pbr.fxh");

        // The same ceilings: one slot per light, the same count constants, the same guards.
        string pointList = ExtractFunctionBody(pbr, "void AccumulatePbrPointLightSlots(");
        string spotList = ExtractFunctionBody(pbr, "void AccumulatePbrSpotLightSlots(");
        Assert.Contains("int count = (int)ActivePointLightCount;", pointList);
        Assert.Contains("int count = (int)ActiveSpotLightCount;", spotList);
        Assert.Contains("int count = (int)ActivePointLightCount;", ExtractFunctionBody(lighting, "void AccumulatePointLightSlots("));
        Assert.Contains("int count = (int)ActiveSpotLightCount;", ExtractFunctionBody(lighting, "void AccumulateSpotLightSlots("));

        for (int index = 0; index < LightingContext.MaxPointLights; index++)
        {
            Assert.Contains(
                $"if (count > {index}) AccumulatePbrPointLightSlot(result, surface, worldPosition, PointLight{index}PositionAndRange, PointLight{index}DiffuseColor);",
                Normalize(pointList));
            Assert.Contains(
                $"if (count > {index}) AccumulatePointLightSlot(result, eyeVector, worldPosition, worldNormal, PointLight{index}PositionAndRange, PointLight{index}DiffuseColor, PointLight{index}SpecularColor);",
                Normalize(lighting));
        }

        for (int index = 0; index < LightingContext.MaxSpotLights; index++)
        {
            Assert.Contains(
                $"if (count > {index}) AccumulatePbrSpotLightSlot(result, surface, worldPosition, SpotLight{index}PositionAndRange, SpotLight{index}DirectionAndInnerConeCos, SpotLight{index}DiffuseColor, SpotLight{index}SpecularColorAndOuterConeCos);",
                Normalize(spotList));
            Assert.Contains(
                $"if (count > {index}) AccumulateSpotLightSlot(result, eyeVector, worldPosition, worldNormal, SpotLight{index}PositionAndRange, SpotLight{index}DirectionAndInnerConeCos, SpotLight{index}DiffuseColor, SpotLight{index}SpecularColorAndOuterConeCos);",
                Normalize(lighting));
        }

        Assert.Equal(LightingContext.MaxPointLights, CountOccurrences(pointList, "AccumulatePbrPointLightSlot("));
        Assert.Equal(LightingContext.MaxSpotLights, CountOccurrences(spotList, "AccumulatePbrSpotLightSlot("));
    }

    [Fact]
    public void PbrDirectionalLoopAndShadowFactor_FollowComputeLights()
    {
        string lighting = Normalize(ExtractFunctionBody(LoadShaderSource("Lighting.fxh"), "ColorPair ComputeLights("));
        string pbr = Normalize(ExtractFunctionBody(LoadShaderSource("Pbr.fxh"), "ColorPair EvaluatePbrLights("));

        // The same loop over the directional lights, bounded by the same ceiling and the same count.
        const string loopHeader = "[unroll] for (int i = 0; i < MaxDirectionalLights; i++) { if (i >= numDirectionalLights) { break; }";
        Assert.Contains(loopHeader, lighting);
        Assert.Contains(loopHeader, pbr);

        // The shadow factor is computed once, scales ShadowedDirectionalLightIndex only, and only in the default shadow mode.
        const string shadowLines =
            "float lightOcclusionFactor = ShadowApplyMode > 0.5f ? 1.0f : shadowFactor;";
        Assert.Contains(shadowLines, lighting);
        Assert.Contains(shadowLines, pbr);
        Assert.Contains("float directionalShadowFactor = i == (int)ShadowedDirectionalLightIndex ? lightOcclusionFactor : 1.0f;", lighting);
        Assert.Contains("float directionalShadowFactor = i == (int)ShadowedDirectionalLightIndex ? lightOcclusionFactor : 1.0f;", pbr);
        Assert.Contains("result.ShadowFactor = (int)ShadowedDirectionalLightIndex < numDirectionalLights ? shadowFactor : 1.0f;", lighting);
        Assert.Contains("result.ShadowFactor = (int)ShadowedDirectionalLightIndex < numDirectionalLights ? shadowFactor : 1.0f;", pbr);
        Assert.Equal(1, CountOccurrences(pbr, "ComputeDirectionalShadowFactor("));
        Assert.Contains("ComputeDirectionalShadowFactor(worldPosition, surface.Normal);", pbr);

        // The point and spot lights are never shadowed.
        Assert.DoesNotContain("ShadowFactor", Normalize(ExtractFunctionBody(LoadShaderSource("Pbr.fxh"), "void AccumulatePbrPointLightSlot(")));
        Assert.DoesNotContain("ShadowFactor", Normalize(ExtractFunctionBody(LoadShaderSource("Pbr.fxh"), "void AccumulatePbrSpotLightSlot(")));
    }

    private static readonly Regex TechniqueRegex = new(
        @"TECHNIQUE\s*\(\s*(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*,",
        RegexOptions.Compiled);

    [Fact]
    public void LitPbrEffect_DeclaresTheFourTechniques()
    {
        string source = LoadShaderSource("LitPbr.fx");

        var techniques = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match match in TechniqueRegex.Matches(source))
        {
            techniques.Add(match.Groups["name"].Value);
        }

        Assert.Equal(
            new[] { "LitPbr", "LitPbr_AlphaTest", "LitPbr_NormalMap", "LitPbr_NormalMap_AlphaTest" }.OrderBy(name => name),
            techniques.OrderBy(name => name));

        // Per-pixel lighting only (decision D5): the normal map techniques read the tangent, the others do not.
        Assert.Contains("TECHNIQUE(LitPbr, VSLitPbr, PSLitPbr);", source);
        Assert.Contains("TECHNIQUE(LitPbr_AlphaTest, VSLitPbr, PSLitPbrAlphaTest);", source);
        Assert.Contains("TECHNIQUE(LitPbr_NormalMap, VSLitPbrTangent, PSLitPbrNormalMap);", source);
        Assert.Contains("TECHNIQUE(LitPbr_NormalMap_AlphaTest, VSLitPbrTangent, PSLitPbrNormalMapAlphaTest);", source);
    }

    [Fact]
    public void LitPbrEffect_DefinesTheBindingMacrosBeforeIncludingLighting()
    {
        string source = LoadShaderSource("LitPbr.fx");

        int environment = source.IndexOf("#define HAS_ENVIRONMENT_BINDINGS 1", StringComparison.Ordinal);
        int shadow = source.IndexOf("#define HAS_FORWARD_SHADOW_BINDINGS 1", StringComparison.Ordinal);
        int colorPipeline = source.IndexOf("#include \"ColorPipeline.fxh\"", StringComparison.Ordinal);
        int lighting = source.IndexOf("#include \"Lighting.fxh\"", StringComparison.Ordinal);
        int pbr = source.IndexOf("#include \"Pbr.fxh\"", StringComparison.Ordinal);
        int colorPipelineDeclaration = source.IndexOf("float ColorPipelineIsLinear ", StringComparison.Ordinal);

        Assert.True(environment >= 0 && shadow >= 0, "LitPbr.fx must define HAS_ENVIRONMENT_BINDINGS and HAS_FORWARD_SHADOW_BINDINGS.");
        Assert.True(environment < lighting, "HAS_ENVIRONMENT_BINDINGS must be defined before Lighting.fxh is included.");
        Assert.True(shadow < lighting, "HAS_FORWARD_SHADOW_BINDINGS must be defined before Lighting.fxh is included.");
        Assert.True(colorPipelineDeclaration >= 0 && colorPipelineDeclaration < colorPipeline, "ColorPipelineIsLinear must be declared before ColorPipeline.fxh.");
        Assert.True(colorPipeline < lighting, "ColorPipeline.fxh must precede Lighting.fxh, which calls DecodeColor.");
        Assert.True(lighting < pbr, "Pbr.fxh builds on Lighting.fxh and is included after it.");
    }

    [Fact]
    public void LitPbrEffect_DeclaresEveryConstantAndTextureTheEngineBindsOrTheIncludesRead()
    {
        string source = LoadShaderSource("LitPbr.fx");

        var constantNames = new List<string>
        {
            ShaderParameterNames.BaseColor,
            ShaderParameterNames.Metallic,
            ShaderParameterNames.Roughness,
            ShaderParameterNames.EmissiveColor,
            ShaderParameterNames.OcclusionStrength,
            ShaderParameterNames.AlphaCutoff,
            ShaderParameterNames.NormalScale,
            ShaderParameterNames.HasBaseColorTexture,
            ShaderParameterNames.HasMetallicRoughnessTexture,
            ShaderParameterNames.HasOcclusionTexture,
            ShaderParameterNames.HasEmissiveTexture,
            ShaderParameterNames.ColorPipelineIsLinear,
            ShaderParameterNames.EnvironmentCubeIsLinear,
            // Read by Lighting.fxh.
            ShaderParameterNames.DiffuseColor,
            ShaderParameterNames.SpecularColor,
            ShaderParameterNames.SpecularPower,
            ShaderParameterNames.EyePosition,
            ShaderParameterNames.AmbientColor,
            ShaderParameterNames.EnvironmentAmbientColor,
            ShaderParameterNames.EnvironmentSpecularIntensity,
            ShaderParameterNames.HasEnvironmentCubeTexture,
            ShaderParameterNames.UseFlatAmbientSource,
            ShaderParameterNames.HasLocalReflectionProbeTexture,
            ShaderParameterNames.HasSecondaryLocalReflectionProbeTexture,
            ShaderParameterNames.LocalReflectionProbeWeight,
            ShaderParameterNames.SecondaryLocalReflectionProbeWeight,
            ShaderParameterNames.LocalReflectionProbeInfluence,
            ShaderParameterNames.ActiveDirectionalLightCount,
            ShaderParameterNames.ActivePointLightCount,
            ShaderParameterNames.ActiveSpotLightCount,
            ShaderParameterNames.ActiveShadowLightCount,
            ShaderParameterNames.ShadowedDirectionalLightIndex,
            ShaderParameterNames.ShadowDepthBias,
            ShaderParameterNames.ShadowNormalBias,
            ShaderParameterNames.ShadowMapTexelSize,
            ShaderParameterNames.ReceiveShadows,
            ShaderParameterNames.ShadowApplyMode,
            ShaderParameterNames.ShadowTintColor,
            ShaderParameterNames.ShadowFilterRadius,
            ShaderParameterNames.ShadowFadeWidth,
            ShaderParameterNames.ShadowLightViewProjection,
            ShaderParameterNames.World,
            ShaderParameterNames.WorldInverseTranspose,
            ShaderParameterNames.WorldViewProj,
        };
        constantNames.AddRange(ShaderParameterNames.DirectionalLightDirectionParameters);
        constantNames.AddRange(ShaderParameterNames.DirectionalLightDiffuseParameters);
        constantNames.AddRange(ShaderParameterNames.DirectionalLightSpecularParameters);
        constantNames.AddRange(ShaderParameterNames.PointLightPositionAndRangeParameters);
        constantNames.AddRange(ShaderParameterNames.PointLightDiffuseParameters);
        constantNames.AddRange(ShaderParameterNames.PointLightSpecularParameters);
        constantNames.AddRange(ShaderParameterNames.SpotLightPositionAndRangeParameters);
        constantNames.AddRange(ShaderParameterNames.SpotLightDirectionAndInnerConeCosParameters);
        constantNames.AddRange(ShaderParameterNames.SpotLightDiffuseParameters);
        constantNames.AddRange(ShaderParameterNames.SpotLightSpecularAndOuterConeCosParameters);

        string constants = ConstantsBlock(source);
        foreach (string name in constantNames)
        {
            Assert.True(
                Regex.IsMatch(constants, $@"^\s*float\w*\s+{name}\b", RegexOptions.Multiline),
                $"LitPbr.fx does not declare the constant '{name}'.");
        }

        // Nine textures, in the documented sampler order (16 pixel samplers are available on this backend).
        string[] textures =
        {
            ShaderParameterNames.BasColorTexture,
            ShaderParameterNames.NormalTexture,
            ShaderParameterNames.MetallicRoughnessTexture,
            ShaderParameterNames.EnvironmentCubeTexture,
            ShaderParameterNames.LocalReflectionProbeCubeTexture,
            ShaderParameterNames.SecondaryLocalReflectionProbeCubeTexture,
            ShaderParameterNames.ShadowMapTexture,
            ShaderParameterNames.OcclusionTexture,
            ShaderParameterNames.EmissiveTexture,
        };
        for (int index = 0; index < textures.Length; index++)
        {
            bool cube = textures[index].EndsWith("CubeTexture", StringComparison.Ordinal);
            string macro = cube ? "DECLARE_CUBEMAP" : "DECLARE_TEXTURE";
            Assert.Contains($"{macro}({textures[index]}, {index});", source);
        }

        Assert.Equal(textures.Length, CountOccurrences(source, "DECLARE_TEXTURE(") + CountOccurrences(source, "DECLARE_CUBEMAP("));
    }

    [Fact]
    public void LitPbrEffect_KeepsTheLightingSlotsOfLitForwardAndGivesTheMaterialConstantsOwnSlots()
    {
        string pbr = LoadShaderSource("LitPbr.fx");
        string litForward = LoadShaderSource("LitForward.fx");

        // Every constant both shaders declare sits in the same slots, so the binders that fill the lighting, environment and
        // shadow constants address the two shaders alike.
        var declaration = new Regex(@"^\s*(?<type>float\w*)\s+(?<name>\w+)(?<annotations>[^;]*);", RegexOptions.Multiline);
        var litForwardDeclarations = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (Match match in declaration.Matches(ConstantsBlock(litForward)))
        {
            litForwardDeclarations[match.Groups["name"].Value] = Normalize(match.Value);
        }

        int shared = 0;
        foreach (Match match in declaration.Matches(ConstantsBlock(pbr)))
        {
            string name = match.Groups["name"].Value;
            if (litForwardDeclarations.TryGetValue(name, out string? litForwardLine))
            {
                Assert.Equal(litForwardLine, Normalize(match.Value));
                shared++;
            }
        }

        Assert.True(shared > 100, $"Only {shared} constants are shared with LitForward.fx.");

        // The PBR material constants take slots no other constant of the file uses, and each is written in the header.
        (string Name, string Slot)[] materialSlots =
        {
            ("NormalScale", "c29.w"),
            ("BaseColor", "c30"),
            ("Metallic", "c32.y"),
            ("Roughness", "c34.z"),
            ("OcclusionStrength", "c34.w"),
            ("HasBaseColorTexture", "c35.x"),
            ("HasMetallicRoughnessTexture", "c35.y"),
            ("HasOcclusionTexture", "c35.z"),
            ("HasEmissiveTexture", "c35.w"),
        };
        foreach (var (name, slot) in materialSlots)
        {
            Assert.Matches($@"float4? {name} _cb\({Regex.Escape(slot)}\);", pbr);
            Assert.Equal(2, CountOccurrences(pbr, $"_cb({slot})"));
            Assert.Matches($@"//\s+_cb\({Regex.Escape(slot)}\)\s+{name}\b", pbr);
        }
    }

    [Fact]
    public void LitPbrEffect_DecodesTheMaterialColoursOnceInsideShadePbrNeverInTheLightSlots()
    {
        string source = LoadShaderSource("LitPbr.fx");
        string shade = ExtractFunctionBody(source, "float4 ShadePbr(");

        Assert.Equal(2, CountOccurrences(source, "DecodeColor("));
        Assert.Contains("float3 linearBaseColor = DecodeColor(baseColor.rgb);", shade);
        Assert.Contains("emissive = DecodeColor(emissive);", shade);

        // The material colours reach EvaluatePbrLights decoded, as the surface; the light slots decode nothing.
        Assert.Contains("ComputePbrSurface(linearBaseColor, saturate(metallic), saturate(roughness), worldNormal, eyeVector)", shade);
        Assert.DoesNotContain("DecodeColor", LoadShaderSource("Pbr.fxh"));

        // The colours of Lighting.fxh's Blinn-Phong are never read.
        Assert.DoesNotContain("DiffuseColor", shade);
        Assert.DoesNotContain("SpecularColor", shade);
        Assert.DoesNotContain("SpecularPower", shade);
        Assert.DoesNotContain("MaterialDiffuseColor", source);
        Assert.DoesNotContain("ComputeLights(", source);
    }

    [Fact]
    public void LitPbrEffect_IsolatesTheFlatAmbientAndHonoursUseFlatAmbientSource()
    {
        string source = LoadShaderSource("LitPbr.fx");
        string ambient = Normalize(ExtractFunctionBody(source, "float3 ComputePbrAmbient("));

        Assert.Contains("(HasEnvironmentCubeTexture > 0.5f && UseFlatAmbientSource < 0.5f)", ambient);
        Assert.Contains("SampleEnvironmentDiffuse(worldNormal) * EnvironmentAmbientColor", ambient);
        Assert.Contains(": AmbientColor;", ambient);
        Assert.Contains("return ambient * occlusion * diffuseAlbedo;", ambient);
        Assert.Contains("ComputePbrAmbient(worldNormal, surface.DiffuseAlbedo, occlusion)", source);
    }

    [Fact]
    public void LitPbrEffect_UsesTheLitForwardShadowFunctionsAndModesAndDoesNotClampItsOutput()
    {
        string source = LoadShaderSource("LitPbr.fx");
        string shade = ExtractFunctionBody(source, "float4 ShadePbr(");

        // The shadow functions are the ones of Lighting.fxh (shared with LitForward.fx), not redefined here.
        Assert.DoesNotContain("float ComputeDirectionalShadowFactor(", source);
        Assert.DoesNotContain("float SampleDirectionalShadowPcf(", source);
        Assert.DoesNotContain("float3 ApplyShadowTint(", source);

        // Default mode: EvaluatePbrLights scales the shadowed light. MultiplyLitColor (ADR-0093): the pixel colour excluding
        // the emissive part is tinted, with the factor the light evaluation computed.
        Assert.Contains("(int)ActiveDirectionalLightCount", shade);
        Assert.Contains("color = ApplyShadowTint(color, emissive, lightResult.ShadowFactor);", shade);

        // Unclamped output, alpha = the alpha of the base colour.
        Assert.Contains("return float4(color, baseColor.a);", shade);
        Assert.DoesNotContain("saturate(color", source);
        Assert.DoesNotContain("saturate(lightResult", source);
    }

    [Fact]
    public void LitPbrEffect_AlphaTestsOnlyInTheAlphaTestPixelShaders()
    {
        string source = LoadShaderSource("LitPbr.fx");

        Assert.Equal(2, CountOccurrences(source, "ApplyAlphaTest(baseColor.a);"));
        Assert.DoesNotContain("ApplyAlphaTest", ExtractFunctionBody(source, "float4 PSLitPbr("));
        Assert.DoesNotContain("ApplyAlphaTest", ExtractFunctionBody(source, "float4 PSLitPbrNormalMap("));
        Assert.Contains("ApplyAlphaTest(baseColor.a);", ExtractFunctionBody(source, "float4 PSLitPbrAlphaTest("));
        Assert.Contains("ApplyAlphaTest(baseColor.a);", ExtractFunctionBody(source, "float4 PSLitPbrNormalMapAlphaTest("));

        // The normal map techniques scale the tangent-space normal by NormalScale; the others never sample the normal texture.
        Assert.Contains("normalMap.xy *= NormalScale;", source);
        Assert.Equal(1, CountOccurrences(source, "SAMPLE_TEXTURE(NormalTexture,"));
        Assert.Contains(
            "ComputePerturbedNormal(pin.TexCoord, pin.NormalWS, pin.TangentWS, pin.BitangentWS)",
            ExtractFunctionBody(source, "float4 PSLitPbrNormalMap("));
    }

    [Fact]
    public void ShaderParameterNames_CarryThePbrMaterialConstants()
    {
        Assert.Equal("BaseColor", ShaderParameterNames.BaseColor);
        Assert.Equal("Metallic", ShaderParameterNames.Metallic);
        Assert.Equal("Roughness", ShaderParameterNames.Roughness);
        Assert.Equal("OcclusionStrength", ShaderParameterNames.OcclusionStrength);
        Assert.Equal("NormalScale", ShaderParameterNames.NormalScale);
        Assert.Equal("MetallicRoughnessTexture", ShaderParameterNames.MetallicRoughnessTexture);
        Assert.Equal("OcclusionTexture", ShaderParameterNames.OcclusionTexture);
        Assert.Equal("EmissiveTexture", ShaderParameterNames.EmissiveTexture);
        Assert.Equal("HasBaseColorTexture", ShaderParameterNames.HasBaseColorTexture);
        Assert.Equal("HasMetallicRoughnessTexture", ShaderParameterNames.HasMetallicRoughnessTexture);
        Assert.Equal("HasOcclusionTexture", ShaderParameterNames.HasOcclusionTexture);
        Assert.Equal("HasEmissiveTexture", ShaderParameterNames.HasEmissiveTexture);
    }

    // The text between BEGIN_CONSTANTS and END_CONSTANTS: the declared constants, without the header comment.
    private static string ConstantsBlock(string source)
    {
        int begin = source.IndexOf("BEGIN_CONSTANTS", StringComparison.Ordinal);
        int end = source.IndexOf("END_CONSTANTS", StringComparison.Ordinal);
        Assert.True(begin >= 0 && end > begin, "BEGIN_CONSTANTS / END_CONSTANTS not found.");
        return source[begin..end];
    }

    private static string ExtractFunctionBody(string source, string signaturePrefix)
    {
        int signature = source.IndexOf(signaturePrefix, StringComparison.Ordinal);
        Assert.True(signature >= 0, $"{signaturePrefix} not found.");

        int open = source.IndexOf('{', signature);
        int depth = 0;
        for (int index = open; index < source.Length; index++)
        {
            if (source[index] == '{')
            {
                depth++;
            }
            else if (source[index] == '}')
            {
                depth--;
                if (depth == 0)
                {
                    return source[(open + 1)..index];
                }
            }
        }

        throw new InvalidOperationException($"Unbalanced braces after {signaturePrefix}.");
    }

    // The function body up to its final accumulation call, whitespace-normalised: what is compared is the attenuation, not
    // the BRDF that follows it.
    private static string BodyBeforeFinalCall(string body, string callPrefix)
    {
        int call = body.LastIndexOf(callPrefix, StringComparison.Ordinal);
        Assert.True(call >= 0, $"{callPrefix} not found.");
        return Normalize(body[..call]);
    }

    private static string Normalize(string text) => Regex.Replace(text, @"\s+", " ").Trim();

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
        return File.ReadAllText(shaderPath).Replace("\r\n", "\n");
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
