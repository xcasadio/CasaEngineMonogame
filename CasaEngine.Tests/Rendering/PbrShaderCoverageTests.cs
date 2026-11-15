using System.Text.RegularExpressions;

using CasaEngine.Framework.Rendering;
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
