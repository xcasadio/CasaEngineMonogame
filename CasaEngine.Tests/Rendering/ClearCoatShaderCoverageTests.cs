using System.Text.RegularExpressions;

using Xunit;

namespace CasaEngine.Tests.Rendering;

/// <summary>
/// Text coverage of the clear coat of the PBR shaders (ADR-0115, plan T2.1): the formulas of the header of Pbr.fxh are what the code
/// computes, the code is compiled only for the effect that asks for it (skinEffect.fx includes the same Pbr.fxh and has no clear
/// coat), the point and spot slots restate the attenuation of the base slots, and the ambient reflection of the coat is read with the
/// roughness of the coat.
/// </summary>
public class ClearCoatShaderCoverageTests
{
    [Fact]
    public void PbrInclude_CompilesTheClearCoatOnlyUnderTheDefineAndLitPbrDefinesItBeforeTheInclude()
    {
        string pbr = LoadShaderSource("Pbr.fxh");

        // Without the define the text of Pbr.fxh, comments aside, is the one it had before the clear coat: no "Coat" is left once
        // the guarded blocks are removed. The guarded blocks do not nest.
        string withoutComments = Regex.Replace(pbr, @"//[^\n]*", string.Empty);
        string withoutGuardedBlocks = Regex.Replace(withoutComments, @"#ifdef PBR_CLEAR_COAT.*?#endif", string.Empty, RegexOptions.Singleline);
        Assert.DoesNotContain("Coat", withoutGuardedBlocks);
        Assert.DoesNotContain("PBR_CLEAR_COAT", withoutGuardedBlocks);
        Assert.Contains("Coat", withoutComments);
        Assert.Equal(
            CountOccurrences(withoutComments, "#ifdef PBR_CLEAR_COAT") + CountOccurrences(withoutComments, "#ifdef HAS_FORWARD_SHADOW_BINDINGS"),
            CountOccurrences(withoutComments, "#ifdef"));

        string litPbr = LoadShaderSource("LitPbr.fx");
        int define = litPbr.IndexOf("#define PBR_CLEAR_COAT 1", StringComparison.Ordinal);
        int include = litPbr.IndexOf("#include \"Pbr.fxh\"", StringComparison.Ordinal);
        Assert.True(define >= 0 && include > define, "LitPbr.fx must define PBR_CLEAR_COAT before including Pbr.fxh.");
    }

    [Fact]
    public void SkinEffect_HasNoClearCoatAndDoesNotDefineTheSwitch()
    {
        string skin = LoadShaderSource("skinEffect.fx");

        Assert.Contains("#include \"Pbr.fxh\"", skin);
        Assert.DoesNotContain("Coat", skin);
        Assert.DoesNotContain("PBR_CLEAR_COAT", skin);
    }

    [Fact]
    public void DirectLobe_IsTheClosedFormOfTheHeader()
    {
        string pbr = LoadShaderSource("Pbr.fxh");
        string coat = Normalize(ExtractFunctionBody(pbr, "void AccumulatePbrClearCoatLight("));

        // The geometric normal only: NoH_c from the coat normal, never from the normal of the surface.
        Assert.Contains("float coatNoH = saturate(dot(surface.CoatNormal, halfVector));", coat);
        // F_d = Schlick(0.04, VoH), weighted by ClearCoat.
        Assert.Contains(
            "float coatFresnel = surface.ClearCoat * (PbrDielectricF0 + (1.0f - PbrDielectricF0) * (squaredOneMinusVoH * squaredOneMinusVoH * oneMinusVoH));",
            coat);
        // Kelemen visibility 0.25 / LoH^2, with LoH = VoH, floored.
        Assert.Contains("float coatVisibility = 0.25f / max(voH * voH, 0.0001f);", coat);
        // GGX with the coat roughness, on the coat normal.
        Assert.Contains(
            "float coatDistribution = PbrDistributionGgx(surface.CoatNormal, halfVector, coatNoH, surface.CoatAlpha2, surface.CoatAlphaSquaredOverPi);",
            coat);
        // The irradiance is the one of the base lobes of the slot (radiance * NoL * attenuation, NoL from the surface normal).
        Assert.Contains("float3 irradiance = lightRadiance * (noL * attenuation);", coat);
        // base * (1 - Fc) and + D * V * Fc * irradiance, written as a correction of the base sums.
        Assert.Contains("result.Diffuse -= coatFresnel * ((1.0f - fresnel) * surface.DiffuseAlbedo * (irradiance / PbrPi));", coat);
        Assert.Contains(
            "result.Specular += (coatDistribution * coatVisibility - fresnel * (distribution * visibility)) * coatFresnel * irradiance;",
            coat);

        // The coat roughness is clamped like the base one, and everything is derived once per pixel from the geometric normal.
        string apply = Normalize(ExtractFunctionBody(pbr, "void ApplyPbrClearCoat("));
        Assert.Contains("float alpha = max(clearCoatRoughness, PbrMinRoughness);", apply);
        Assert.Contains("alpha = alpha * alpha;", apply);
        Assert.Contains("surface.CoatNoV = max(dot(geometricNormal, surface.EyeVector), 0.0001f);", apply);
        Assert.Contains("surface.CoatNormal = geometricNormal;", apply);
        Assert.Contains("surface.CoatPerceptualRoughness = clearCoatRoughness;", apply);
    }

    [Fact]
    public void BaseWalk_IsNotTouchedByTheClearCoat_AndTheCoatWalkRunsOnlyWhenTheCoatIsOn()
    {
        string pbr = LoadShaderSource("Pbr.fxh");
        string accumulate = Normalize(ExtractFunctionBody(pbr, "void AccumulatePbrLight("));

        Assert.DoesNotContain("Coat", accumulate);
        Assert.DoesNotContain("Coat", ExtractFunctionBody(pbr, "ColorPair EvaluatePbrLights("));

        string shade = Normalize(ExtractFunctionBody(LoadShaderSource("LitPbr.fx"), "float4 ShadePbr("));
        Assert.Contains("ApplyPbrClearCoat(surface, saturate(ClearCoat), saturate(ClearCoatRoughness), geometricNormal);", shade);
        Assert.Contains(
            "if (surface.ClearCoat > 0.0f) { float directionalOcclusion = ShadowApplyMode > 0.5f ? 1.0f : lightResult.ShadowFactor; "
            + "EvaluatePbrClearCoatDirectional(lightResult, surface, (int)ActiveDirectionalLightCount, directionalOcclusion); "
            + "EvaluatePbrClearCoatPointAndSpot(lightResult, surface, positionWS); }",
            shade);
        Assert.Contains("if (surface.ClearCoat > 0.0f) { ApplyPbrClearCoatAmbient(ambient, surface, occlusion); }", shade);

        // The coat lobe is added to the specular part: it is not scaled by a transparent alpha, the diffuse part is.
        Assert.Contains("color = lightResult.Diffuse * diffuseOpacity + lightResult.Specular", shade);
    }

    [Fact]
    public void PointAndSpotCoatSlots_RestateTheAttenuationOfTheBaseSlotsAndCoverEverySlot()
    {
        string pbr = LoadShaderSource("Pbr.fxh");

        Assert.Equal(
            BodyBeforeFinalCall(ExtractFunctionBody(pbr, "void AccumulatePbrPointLightSlot("), "AccumulatePbrLight("),
            BodyBeforeFinalCall(ExtractFunctionBody(pbr, "void AccumulatePbrClearCoatPointLightSlot("), "AccumulatePbrClearCoatLight("));
        Assert.Equal(
            BodyBeforeFinalCall(ExtractFunctionBody(pbr, "void AccumulatePbrSpotLightSlot("), "AccumulatePbrLight("),
            BodyBeforeFinalCall(ExtractFunctionBody(pbr, "void AccumulatePbrClearCoatSpotLightSlot("), "AccumulatePbrClearCoatLight("));

        string list = Normalize(ExtractFunctionBody(pbr, "void EvaluatePbrClearCoatPointAndSpot("));
        Assert.Contains("int pointCount = (int)ActivePointLightCount;", list);
        Assert.Contains("int spotCount = (int)ActiveSpotLightCount;", list);
        for (int index = 0; index < 8; index++)
        {
            Assert.Contains(
                $"if (pointCount > {index}) AccumulatePbrClearCoatPointLightSlot(result, surface, worldPosition, PointLight{index}PositionAndRange, PointLight{index}DiffuseColor);",
                list);
            Assert.Contains(
                $"if (spotCount > {index}) AccumulatePbrClearCoatSpotLightSlot(result, surface, worldPosition, SpotLight{index}PositionAndRange, SpotLight{index}DirectionAndInnerConeCos, SpotLight{index}DiffuseColor, SpotLight{index}SpecularColorAndOuterConeCos);",
                list);
            Assert.Contains(
                $"if (numDirectionalLights > {index}) AccumulatePbrClearCoatDirectionalSlot(result, surface, {index}, directionalOcclusion);",
                Normalize(ExtractFunctionBody(pbr, "void EvaluatePbrClearCoatDirectional(")));
        }

        string directionalSlot = Normalize(ExtractFunctionBody(pbr, "void AccumulatePbrClearCoatDirectionalSlot("));
        Assert.Contains("float directionalShadowFactor = slot == (int)ShadowedDirectionalLightIndex ? directionalOcclusion : 1.0f;", directionalSlot);
        Assert.Contains("float3 lightDiffuse = GetDirectionalLightDiffuseColor(slot);", directionalSlot);
    }

    [Fact]
    public void AmbientCoat_ReadsTheGlobalAtlasAndTheBrdfTableWithTheCoatRoughness()
    {
        string source = LoadShaderSource("LitPbr.fx");
        string body = Normalize(ExtractFunctionBody(source, "void ApplyPbrClearCoatAmbient("));

        // (f) The reflection of the coat and its energy use the roughness of the coat, never the one of the base.
        Assert.Contains("float2 brdf = SampleIblBrdf(surface.CoatNoV, surface.CoatPerceptualRoughness);", body);
        Assert.Contains("float coatEnergy = PbrDielectricF0 * brdf.x + brdf.y;", body);
        Assert.Contains(
            "float3 coatPrefiltered = IblToViewSpace(SampleIblPrefiltered(coatReflectionVector, surface.CoatPerceptualRoughness, 0.0f));",
            body);
        Assert.DoesNotMatch(new Regex(@"surface\.PerceptualRoughness"), body);

        // The geometric normal gives the reflection vector, the global atlas only (source 0), no local probe.
        Assert.Contains("float3 coatReflectionVector = reflect(-surface.EyeVector, surface.CoatNormal);", body);
        Assert.DoesNotContain("LocalReflectionProbe", body);
        Assert.Equal(1, CountOccurrences(body, "SampleIblPrefiltered("));

        // E x ClearCoat x EnvironmentSpecularIntensity x occlusion for the reflection; 1 - ClearCoat x E for the base.
        Assert.Contains("float baseScale = 1.0f - surface.ClearCoat * coatEnergy;", body);
        Assert.Contains("coatPrefiltered * (coatEnergy * surface.ClearCoat * EnvironmentSpecularIntensity * occlusion)", body);
        Assert.Contains("ambient.Diffuse *= baseScale;", body);

        // Flat ambient: no reflection, 1 - ClearCoat x Schlick(0.04, NoV_c).
        Assert.Contains("float flatScale = 1.0f - surface.ClearCoat * coatFresnel;", body);
        Assert.Contains("float oneMinusNoV = 1.0f - surface.CoatNoV;", body);
    }

    [Fact]
    public void EveryTechnique_PassesTheGeometricNormalToShadePbr()
    {
        string source = LoadShaderSource("LitPbr.fx");

        Assert.Equal(6, CountOccurrences(source, "return ShadePbr("));
        Assert.Equal(
            6,
            Regex.Matches(source, @"return ShadePbr\(pin\.TexCoord, pin\.PositionWS\.xyz, [^,]+, normalize\(pin\.NormalWS\), baseColor, [^)]+\);").Count);
    }

    // The text of the function body between its braces.
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
