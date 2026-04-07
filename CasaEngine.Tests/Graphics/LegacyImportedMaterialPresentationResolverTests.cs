using CasaEngine.Framework.Assets.Loaders;
using CasaEngine.Framework.Materials;
using Microsoft.Xna.Framework;
using Xunit;

namespace CasaEngine.Tests.Graphics;

public class LegacyImportedMaterialPresentationResolverTests
{
    [Fact]
    public void Resolve_UsesAlphaCutoutPresentationWhenHintIsSet()
    {
        var presentation = LegacyImportedMaterialPresentationResolver.Resolve(new StaticModelImportedMaterial
        {
            AlphaCutoutHint = true,
        });

        Assert.Equal(RenderQueue.AlphaTest, presentation.Queue);
        Assert.Equal(0.35f, presentation.AlphaCutoff);
        Assert.True(presentation.DisableBackfaceCulling);
    }

    [Fact]
    public void Resolve_AppliesBrightAmbientFloorAndClampsColors()
    {
        var presentation = LegacyImportedMaterialPresentationResolver.Resolve(new StaticModelImportedMaterial
        {
            BrightAmbientHint = true,
            AmbientColor = new Vector3(0.1f, 0.8f, 1.4f),
            EmissiveColor = new Vector3(1.2f, -0.25f, 0.35f),
        });

        Assert.Equal(RenderQueue.Opaque, presentation.Queue);
        Assert.Equal(0.5f, presentation.AlphaCutoff);
        Assert.False(presentation.DisableBackfaceCulling);
        AssertVector3Close(new Vector3(128f / 255f, 0.8f, 1.0f), presentation.AmbientColor);
        AssertVector3Close(new Vector3(1.0f, 0.0f, 0.35f), presentation.EmissiveColor);
    }

    private static void AssertVector3Close(Vector3 expected, Vector3 actual, float tolerance = 0.0001f)
    {
        Assert.InRange(Vector3.Distance(expected, actual), 0.0f, tolerance);
    }
}