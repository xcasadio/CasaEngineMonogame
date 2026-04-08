using CasaEngine.Framework.Rendering.Environment;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace CasaEngine.Tests.Rendering;

public class PanoramaEnvironmentGeneratorTests
{
    [Fact]
    public void GetPanoramaUv_MapsWorldForwardToPanoramaCenter()
    {
        Vector2 uv = PanoramaEnvironmentGenerator.GetPanoramaUv(Vector3.Forward);

        Assert.Equal(0.5f, uv.X, 3);
        Assert.Equal(0.5f, uv.Y, 3);
    }

    [Fact]
    public void GetDirectionForFace_MapsNegativeZFaceCenterToWorldForward()
    {
        Vector3 direction = PanoramaEnvironmentGenerator.GetDirectionForFace(CubeMapFace.NegativeZ, 0.0f, 0.0f);

        Assert.True(Vector3.Distance(Vector3.Forward, direction) < 0.0001f);
    }

    [Fact]
    public void CreateGeneratedCubemapAssetId_IsStableForSameInput()
    {
        Guid panoramaAssetId = Guid.Parse("70d0f0d8-0eb4-4f75-9273-33175f3c97cf");

        Guid first = PanoramaEnvironmentGenerator.CreateGeneratedCubemapAssetId(panoramaAssetId, 256);
        Guid second = PanoramaEnvironmentGenerator.CreateGeneratedCubemapAssetId(panoramaAssetId, 256);
        Guid third = PanoramaEnvironmentGenerator.CreateGeneratedCubemapAssetId(panoramaAssetId, 512);

        Assert.Equal(first, second);
        Assert.NotEqual(first, third);
    }
}