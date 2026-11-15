using CasaEngine.Framework.Rendering.Shaders;
using Xunit;

namespace CasaEngine.Tests.Rendering;

/// <summary>
/// Text coverage of the colour decoding in the shaders (ADR-0102): the constant exists everywhere a display colour is read,
/// the decoding wraps the right values and never the normal map, the shadow map or the detail sample alone.
/// </summary>
public class ColorPipelineShaderCoverageTests
{
    [Fact]
    public void ShaderParameterNames_CarryTheColourPipelineConstants()
    {
        Assert.Equal("ColorPipelineIsLinear", ShaderParameterNames.ColorPipelineIsLinear);
        Assert.Equal("EnvironmentCubeIsLinear", ShaderParameterNames.EnvironmentCubeIsLinear);
    }

    [Fact]
    public void DecodeColor_IsGuardedByTheConstantAndLeavesTheValueUntouchedAtZero()
    {
        string source = LoadShaderSource("ColorPipeline.fxh");

        Assert.Contains("float3 DecodeColor(float3 c)", source);
        Assert.Contains("return ColorPipelineIsLinear > 0.5f ? pow(abs(c), 2.2f) : c;", source);

        // The alpha channel is never decoded.
        Assert.Contains("float4 DecodeColor4(float4 c)", source);
        Assert.Contains("c.rgb = DecodeColor(c.rgb);", source);
        Assert.Equal(1, CountOccurrences(source, "pow(abs("));
    }

    [Theory]
    [InlineData("LitForward.fx")]
    [InlineData("skinEffect.fx")]
    [InlineData("UnlitTexture.fx")]
    [InlineData("SkyCubemap.fx")]
    [InlineData("LegacySkyCube.fx")]
    public void EveryShaderThatReadsDisplayColours_DeclaresTheConstantBeforeIncludingTheDecoder(string shaderFileName)
    {
        string source = LoadShaderSource(shaderFileName);

        int declaration = IndexOfDeclaration(source, "ColorPipelineIsLinear");
        Assert.True(declaration >= 0, $"{shaderFileName} does not declare ColorPipelineIsLinear.");

        int include = source.IndexOf("#include \"ColorPipeline.fxh\"", StringComparison.Ordinal);
        Assert.True(include > declaration, $"{shaderFileName} must include ColorPipeline.fxh after declaring ColorPipelineIsLinear.");
        Assert.Contains("DecodeColor", source[include..]);
    }

    [Theory]
    [InlineData("LitForward.fx")]
    [InlineData("skinEffect.fx")]
    public void LitShaders_PlaceTheConstantInAFreeSlotOfTheSharedLayout(string shaderFileName)
    {
        string source = LoadShaderSource(shaderFileName);

        Assert.Contains("float ColorPipelineIsLinear _vs(c29) _ps(c36) _cb(c32.w);", source);

        // The slot is not shared with another parameter of the same shader.
        Assert.Equal(1, CountOccurrences(source, "_cb(c32.w)"));
        Assert.Equal(1, CountOccurrences(source, "float ColorPipelineIsLinear "));
    }

    [Theory]
    [InlineData("LitForward.fx", "float EnvironmentCubeIsLinear _ps(c38) _cb(c34.y);", "_cb(c34.y)")]
    [InlineData("skinEffect.fx", "float EnvironmentCubeIsLinear _ps(c36) _cb(c32.y);", "_cb(c32.y)")]
    public void LitShaders_DeclareWhetherTheEnvironmentCubeIsAlreadyLinear(string shaderFileName, string declaration, string slot)
    {
        string source = LoadShaderSource(shaderFileName);
        string lighting = LoadShaderSource("Lighting.fxh");

        Assert.Contains(declaration, source);
        Assert.Equal(1, CountOccurrences(source, slot));

        // A cube built from a .hdr panorama is not decoded; every other cube is.
        Assert.Contains("if (EnvironmentCubeIsLinear < 0.5f)", lighting);
        Assert.Contains("DecodeEnvironmentCube(SAMPLE_CUBEMAP(EnvironmentCubeTexture, reflectionVector).rgb)", lighting);
        Assert.Contains("DecodeEnvironmentCube(SAMPLE_CUBEMAP(EnvironmentCubeTexture, normalize(worldNormal)).rgb)", lighting);
        Assert.Contains("DecodeColor(SAMPLE_CUBEMAP(LocalReflectionProbeCubeTexture, reflectionVector).rgb)", lighting);
        Assert.Contains("DecodeColor(SAMPLE_CUBEMAP(SecondaryLocalReflectionProbeCubeTexture, reflectionVector).rgb)", lighting);
    }

    [Fact]
    public void LitForward_DecodesTheBaseColourAfterItsModulationByTheDetailTexture()
    {
        string source = LoadShaderSource("LitForward.fx");

        int definitionStart = source.IndexOf("float4 SampleBaseTexture(", StringComparison.Ordinal);
        Assert.True(definitionStart >= 0);
        string definition = source[definitionStart..source.IndexOf("\n}", definitionStart, StringComparison.Ordinal)];

        int modulation = definition.IndexOf("baseColor.rgb *= 2.0f * SAMPLE_TEXTURE(DetailTexture,", StringComparison.Ordinal);
        int decode = definition.IndexOf("baseColor.rgb = DecodeColor(baseColor.rgb);", StringComparison.Ordinal);
        Assert.True(modulation >= 0, "The detail modulation is missing.");
        Assert.True(decode > modulation, "The base colour must be decoded after its modulation by the detail texture.");

        // The decode sits outside the detail branch: it applies with or without a detail texture.
        int branchEnd = definition.IndexOf("    }\n", modulation, StringComparison.Ordinal);
        Assert.True(branchEnd >= 0 && decode > branchEnd, "The decode must not live inside the detail branch.");

        // The detail sample alone is never decoded; neither are the normal map nor the shadow map.
        Assert.DoesNotContain("DecodeColor(SAMPLE_TEXTURE(DetailTexture,", source);
        Assert.DoesNotContain("DecodeColor(2.0f * SAMPLE_TEXTURE(DetailTexture,", source);
        Assert.Equal(1, CountOccurrences(source, "SAMPLE_TEXTURE(DetailTexture,"));
        AssertNoDecodeOnTheLineOf(source, "SAMPLE_TEXTURE(NormalTexture,");
        AssertNoDecodeOnTheLineOf(LoadShaderSource("Lighting.fxh"), "SAMPLE_TEXTURE_LEVEL(ShadowMapTexture,");
    }

    [Fact]
    public void LitForward_DecodesVertexColoursInTheVertexShaders()
    {
        string source = LoadShaderSource("LitForward.fx");

        Assert.DoesNotContain("vout.Diffuse *= vin.Color;", source);
        Assert.DoesNotContain("vout.Diffuse.rgb = vin.Color.rgb;", source);
        Assert.Equal(6, CountOccurrences(source, "vout.Diffuse *= DecodeColor4(vin.Color);"));
        Assert.Equal(2, CountOccurrences(source, "vout.Diffuse.rgb = DecodeColor(vin.Color.rgb);"));
    }

    [Fact]
    public void LitShaders_DecodeTheMaterialColoursWhereTheyAreUsed()
    {
        string lighting = LoadShaderSource("Lighting.fxh");

        Assert.Contains("return DecodeColor(DiffuseColor.rgb);", lighting);
        Assert.Contains("return DecodeColor(EmissiveColor);", lighting);
        Assert.Contains("return DecodeColor(SpecularColor);", lighting);
        Assert.Contains("return globalAmbientColor * DecodeColor(materialAmbientColor);", lighting);
        Assert.Contains("result.Diffuse += diffuse * lightDiffuse * DecodedMaterialDiffuse;", lighting);
        Assert.Contains("DecodedMaterialDiffuse = MaterialDiffuseColor();", lighting);
        Assert.Contains("DecodedMaterialSpecular = MaterialSpecularColor();", lighting);
        Assert.Contains("result.Specular += specular * lightSpecular * DecodedMaterialSpecular;", lighting);

        // Nothing else reads the raw material colours: the decode cannot be forgotten at a call site.
        Assert.Equal(0, CountOccurrences(lighting, "= EmissiveColor;"));
        Assert.Equal(0, CountOccurrences(lighting, "* DiffuseColor.rgb"));
        Assert.Equal(0, CountOccurrences(lighting, "* SpecularColor;"));

        string litForward = LoadShaderSource("LitForward.fx");
        Assert.Contains("DecodeColor(SAMPLE_CUBEMAP(ReflectionCubeTexture, reflectionVector).rgb)", litForward);
        Assert.Contains("saturate(MaterialSpecularColor())", litForward);
        Assert.Contains("return MaterialDiffuseColor() * ambientTerm;", litForward);

        string skinned = LoadShaderSource("skinEffect.fx");
        Assert.Contains("DecodeColor4(SAMPLE_TEXTURE(Texture, input.TexureCoordinateA) * input.Color)", skinned);
        Assert.Contains("saturate(MaterialSpecularColor())", skinned);
        Assert.Contains("return MaterialDiffuseColor() * ambientTerm;", skinned);
    }

    [Fact]
    public void UnlitTexture_DecodesTheTextureTimesTheTintAndTheSolidTint()
    {
        string source = LoadShaderSource("UnlitTexture.fx");

        Assert.Contains("DecodeColor4(SAMPLE_TEXTURE(Texture, input.TexCoord) * TintColor)", source);
        Assert.Contains("DecodeColor(TintColor.rgb)", source);
    }

    [Theory]
    [InlineData("TexturedPrimitive.fx", "DecodeColor4(SAMPLE_TEXTURE(Texture, input.TexCoord) * input.Color)")]
    [InlineData("SpriteBatch.fx", "return DecodeColor4(texel);")]
    [InlineData("PsxQuad.fx", "return DecodeColor4(result);")]
    [InlineData("DebugPrimitiveColor.fx", "return DecodeColor4(input.color);")]
    public void ParticleSpriteAndLineShaders_DeclareTheConstantAndDecodeTheSampledAndVertexColours(string shaderFileName, string decodedReturn)
    {
        string source = LoadShaderSource(shaderFileName);

        // The constant defaults to 0 (the UI backend, which shares TexturedPrimitive.fx, never binds it) and is declared
        // before the decoder is included.
        Assert.Contains("float ColorPipelineIsLinear = 0.0f;", source);
        int declaration = source.IndexOf("float ColorPipelineIsLinear", StringComparison.Ordinal);
        int include = source.IndexOf("#include \"ColorPipeline.fxh\"", StringComparison.Ordinal);
        Assert.True(declaration >= 0 && include > declaration, $"{shaderFileName} must declare the constant before including the decoder.");

        // DecodeColor wraps the colour that leaves the pixel shader: the sampled colour times the vertex colour or colour
        // constant (the power law is multiplicative), alpha untouched (DecodeColor4).
        Assert.Contains(decodedReturn, source);
        Assert.Equal(1, CountOccurrences(source, "DecodeColor4("));
    }

    [Fact]
    public void TheEditorGizmoShader_IsNotPartOfTheSceneRenderers()
    {
        // DebugSolidColor.fx draws the editor transform gizmo on top of the view (an editor overlay, not a scene renderer):
        // it is not decoded, so it never takes the constant.
        Assert.DoesNotContain("ColorPipelineIsLinear", LoadShaderSource("DebugSolidColor.fx"));
    }

    [Fact]
    public void SkyShaders_DecodeTheSampledCubeAndTheTint()
    {
        string sky = LoadShaderSource("SkyCubemap.fx");
        Assert.Contains("float EnvironmentCubeIsLinear", sky);
        Assert.Contains("if (EnvironmentCubeIsLinear < 0.5f)", sky);
        Assert.Contains("color = DecodeColor(color);", sky);

        string legacy = LoadShaderSource("LegacySkyCube.fx");
        Assert.Contains("DecodeColor4(SkyTintColor * SAMPLE_CUBEMAP(SkyCube, input.CubeDirection))", legacy);
    }

    private static void AssertNoDecodeOnTheLineOf(string source, string sampleCall)
    {
        int index = source.IndexOf(sampleCall, StringComparison.Ordinal);
        Assert.True(index >= 0, $"{sampleCall} not found.");
        int lineStart = source.LastIndexOf('\n', index) + 1;
        int lineEnd = source.IndexOf('\n', index);
        string line = source[lineStart..lineEnd];
        Assert.DoesNotContain("DecodeColor", line);
    }

    private static int IndexOfDeclaration(string source, string name)
    {
        int index = 0;
        while ((index = source.IndexOf(name, index, StringComparison.Ordinal)) >= 0)
        {
            int lineStart = source.LastIndexOf('\n', index) + 1;
            string prefix = source[lineStart..index].Trim();
            if (prefix == "float" || prefix == "float4" || prefix == "float3")
            {
                return index;
            }

            index += name.Length;
        }

        return -1;
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
