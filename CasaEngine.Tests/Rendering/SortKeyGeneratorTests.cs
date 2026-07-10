
using CasaEngine.Framework.Rendering.Draw;
using Xunit;

namespace CasaEngine.Tests.Rendering;

public class SortKeyGeneratorTests
{
    private const int ShaderHash = 0x1234;
    private const int MaterialHash = 0x5678;
    private const int MeshHash = 0x9ABC;

    [Fact]
    public void Transparent_FartherItemsSortFirst_WithinSameStateGroup()
    {
        ulong farKey = SortKeyGenerator.Generate(RenderQueue.Transparent, ShaderHash, MaterialHash, MeshHash, distance: 100f);
        ulong nearKey = SortKeyGenerator.Generate(RenderQueue.Transparent, ShaderHash, MaterialHash, MeshHash, distance: 10f);

        // Ascending sort must yield back-to-front: the farther item gets the smaller key.
        Assert.True(farKey < nearKey);
    }

    [Fact]
    public void Opaque_DistanceIsNotEncoded()
    {
        ulong farKey = SortKeyGenerator.Generate(RenderQueue.Opaque, ShaderHash, MaterialHash, MeshHash, distance: 100f);
        ulong nearKey = SortKeyGenerator.Generate(RenderQueue.Opaque, ShaderHash, MaterialHash, MeshHash, distance: 10f);

        Assert.Equal(farKey, nearKey);
    }

    [Fact]
    public void OpaqueItems_SortBeforeTransparentItems()
    {
        ulong opaqueKey = SortKeyGenerator.Generate(RenderQueue.Opaque, ShaderHash, MaterialHash, MeshHash);
        ulong transparentKey = SortKeyGenerator.Generate(RenderQueue.Transparent, ShaderHash, MaterialHash, MeshHash, distance: 1f);

        Assert.True(opaqueKey < transparentKey);
    }

    [Fact]
    public void AlphaTestItems_SortBetweenOpaqueAndTransparent()
    {
        ulong opaqueKey = SortKeyGenerator.Generate(RenderQueue.Opaque, ShaderHash, MaterialHash, MeshHash);
        ulong alphaTestKey = SortKeyGenerator.Generate(RenderQueue.AlphaTest, ShaderHash, MaterialHash, MeshHash);
        ulong transparentKey = SortKeyGenerator.Generate(RenderQueue.Transparent, ShaderHash, MaterialHash, MeshHash, distance: 1f);

        Assert.True(opaqueKey < alphaTestKey);
        Assert.True(alphaTestKey < transparentKey);
    }

    [Fact]
    public void Transparent_DistanceBeyondRange_ClampsInsteadOfWrapping()
    {
        ulong veryFarKey = SortKeyGenerator.Generate(RenderQueue.Transparent, ShaderHash, MaterialHash, MeshHash, distance: 1_000_000f);
        ulong nearKey = SortKeyGenerator.Generate(RenderQueue.Transparent, ShaderHash, MaterialHash, MeshHash, distance: 1f);

        Assert.True(veryFarKey < nearKey);
    }
}
