using CasaEngine.Framework.Rendering;
using CasaEngine.Framework.Rendering.Environment;
using CasaEngine.Framework.Rendering.Shaders;
using Microsoft.Xna.Framework;
using Xunit;

namespace CasaEngine.Tests.Rendering;

/// <summary>
/// CPU-side decoding of the linear colour pipeline (ADR-0105): lights are decoded BEFORE their intensity, the ambient is
/// Decode(AmbientColor) x AmbientIntensity, and the legacy values are exactly the ones bound before the pipeline existed.
/// </summary>
public class ColorPipelineBindingTests
{
    private static readonly float Grey = MathF.Pow(0.5f, 2.2f);

    [Fact]
    public void Decode_IsThePowerLawAndKeepsAlpha()
    {
        Assert.Equal(0.0f, ColorSpace.Decode(0.0f));
        Assert.Equal(1.0f, ColorSpace.Decode(1.0f));
        Assert.Equal(MathF.Pow(128.0f / 255.0f, 2.2f), ColorSpace.Decode(128.0f / 255.0f), 6);

        Vector4 decoded = ColorSpace.Decode(new Color(128, 128, 128, 77));
        Assert.Equal(0.2195f, decoded.X, 3);
        Assert.Equal(decoded.X, decoded.Z, 6);
        Assert.Equal(77.0f / 255.0f, decoded.W, 6);
    }

    [Fact]
    public void LinearLights_AreDecodedBeforeTheMultiplyByIntensity()
    {
        var lighting = new LightingContext();
        lighting.BeginCollection(Vector3.Zero, Vector3.Zero);
        lighting.AddDirectionalLight(new DirectionalLight(Vector3.UnitX, new Vector3(0.5f), new Vector3(0.5f), intensity: 4.0f));
        lighting.AddPointLight(new PointLight(Vector3.Zero, new Vector3(0.5f), new Vector3(0.5f), range: 4.0f, intensity: 4.0f));
        lighting.AddSpotLight(new SpotLight(Vector3.Zero, Vector3.UnitY, new Vector3(0.5f), new Vector3(0.5f), range: 4.0f, innerConeAngle: 0.25f, outerConeAngle: 0.5f, intensity: 4.0f));

        var snapshot = ForwardLightBinder.CreateSnapshot(lighting, decodeColors: true);

        float expected = 4.0f * Grey;
        Assert.Equal(expected, snapshot.DirectionalLightDiffuseColors[0].X, 5);
        Assert.Equal(expected, snapshot.DirectionalLightSpecularColors[0].Y, 5);
        Assert.Equal(expected, snapshot.PointLightDiffuseData[0].X, 5);
        Assert.Equal(expected, snapshot.PointLightSpecularData[0].Z, 5);
        Assert.Equal(expected, snapshot.SpotLightDiffuseData[0].Y, 5);
        Assert.Equal(expected, snapshot.SpotLightSpecularAndOuterConeCosData[0].X, 5);

        // Not decode(colour x intensity): that is the mistake the order guards against.
        Assert.NotEqual(MathF.Pow(2.0f, 2.2f), snapshot.DirectionalLightDiffuseColors[0].X, 3);
    }

    [Fact]
    public void LegacyLights_AreBoundExactlyAsBefore()
    {
        var lighting = new LightingContext();
        lighting.BeginCollection(Vector3.Zero, new Vector3(0.1f, 0.2f, 0.3f));
        lighting.AddDirectionalLight(new DirectionalLight(Vector3.UnitX, new Vector3(0.5f), new Vector3(0.25f), intensity: 4.0f));
        lighting.AddPointLight(new PointLight(Vector3.Zero, new Vector3(0.5f), new Vector3(0.25f), range: 4.0f, intensity: 4.0f));

        var withDefault = ForwardLightBinder.CreateSnapshot(lighting);
        var withFalse = ForwardLightBinder.CreateSnapshot(lighting, decodeColors: false);

        Assert.Equal(new Vector3(2.0f), withDefault.DirectionalLightDiffuseColors[0]);
        Assert.Equal(new Vector3(1.0f), withDefault.DirectionalLightSpecularColors[0]);
        Assert.Equal(new Vector4(2.0f, 2.0f, 2.0f, 0.0f), withDefault.PointLightDiffuseData[0]);
        Assert.Equal(new Vector3(0.1f, 0.2f, 0.3f), withDefault.AmbientColor);

        Assert.Equal(withDefault.DirectionalLightDiffuseColors[0], withFalse.DirectionalLightDiffuseColors[0]);
        Assert.Equal(withDefault.PointLightDiffuseData[0], withFalse.PointLightDiffuseData[0]);
        Assert.Equal(withDefault.AmbientColor, withFalse.AmbientColor);
    }

    [Fact]
    public void LinearAmbient_IsDecodeOfTheColourTimesTheIntensity()
    {
        var environment = new ResolvedEnvironmentSettings
        {
            AmbientColor = new Vector3(0.5f),
            AmbientIntensity = 2.0f,
            LinearColorActive = true,
        };

        Assert.Equal(2.0f * Grey, environment.EffectiveAmbientColor.X, 6);
        Assert.Equal(2.0f * Grey, environment.EffectiveAmbientColor.Z, 6);
    }

    [Fact]
    public void LegacyAmbient_IsTheColourTimesTheIntensityAsBefore()
    {
        var environment = new ResolvedEnvironmentSettings
        {
            AmbientColor = new Vector3(0.5f, 0.25f, 0.125f),
            AmbientIntensity = 2.0f,
        };

        Assert.False(environment.LinearColorActive);
        Assert.Equal(new Vector3(0.5f, 0.25f, 0.125f) * 2.0f, environment.EffectiveAmbientColor);

        // Mode alone does not decode: only a view that really draws through the HDR target does (ADR-0105).
        var asked = environment with { ColorPipelineMode = ColorPipelineMode.LinearHdr };
        Assert.Equal(new Vector3(0.5f, 0.25f, 0.125f) * 2.0f, asked.EffectiveAmbientColor);
    }

    [Fact]
    public void TheAmbientReachesTheLightingContextDecodedOnlyWhenTheViewIsLinear()
    {
        var legacy = new ResolvedEnvironmentSettings { AmbientColor = new Vector3(0.5f), AmbientIntensity = 1.0f };
        var linear = legacy with { LinearColorActive = true };
        var lighting = new LightingContext();

        lighting.BeginCollection(Vector3.Zero, legacy.EffectiveAmbientColor);
        Assert.Equal(new Vector3(0.5f), ForwardLightBinder.CreateSnapshot(lighting).AmbientColor);

        lighting.BeginCollection(Vector3.Zero, linear.EffectiveAmbientColor);
        Assert.Equal(Grey, ForwardLightBinder.CreateSnapshot(lighting, decodeColors: true).AmbientColor.X, 6);
    }

    [Fact]
    public void TheShadowColourAndTheClearColour_AreDecodedByTheirOwners()
    {
        string binder = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "CasaEngine", "Framework", "Rendering", "Shaders", "ForwardLightBinder.cs"));
        Assert.Contains("shadowTintColor = PrepareColor(shadows.Settings.ShadowColor, decodeColors);", binder);

        string pipeline = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "CasaEngine", "Framework", "Rendering", "RenderPipeline.cs")).Replace("\r\n", "\n");
        Assert.Contains("ColorSpace.Decode(resolvedClearColor), 1.0f, 0);", pipeline);
        Assert.Contains("resolvedEnvironment = resolvedEnvironment with { LinearColorActive = true };", pipeline);

        // The decode applies to the colour only: black and white are fixed points.
        Assert.Equal(Vector4.UnitW, ColorSpace.Decode(Color.Black));
        Assert.Equal(Vector4.One, ColorSpace.Decode(Color.White));
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
