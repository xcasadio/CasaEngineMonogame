using CasaEngine.Framework.Rendering.Depth;
using Xunit;

namespace CasaEngine.Tests.Rendering;

public sealed class RenderSortKey2DTests
{
    [Fact]
    public void CompareTo_UsesLexicographicPriority()
    {
        var ordered = new[]
        {
            new RenderSortKey2D((int)RenderPass2D.Ground, 0, 0, 0, 0, 0, 0),
            new RenderSortKey2D((int)RenderPass2D.YSortedWorld, 1, 0, 0, 0, 0, 0),
            new RenderSortKey2D((int)RenderPass2D.YSortedWorld, 1, 10, 0, 0, 0, 0),
            new RenderSortKey2D((int)RenderPass2D.YSortedWorld, 1, 10, 1, 0, 0, 0),
            new RenderSortKey2D((int)RenderPass2D.YSortedWorld, 1, 10, 1, 120, 0, 0),
            new RenderSortKey2D((int)RenderPass2D.YSortedWorld, 1, 10, 1, 120, 5, 0),
            new RenderSortKey2D((int)RenderPass2D.YSortedWorld, 1, 10, 1, 120, 5, 42),
            new RenderSortKey2D((int)RenderPass2D.Foreground, 0, 0, 0, 0, 0, 0)
        };

        for (var index = 0; index < ordered.Length - 1; index++)
        {
            Assert.True(ordered[index].CompareTo(ordered[index + 1]) < 0);
            Assert.True(ordered[index + 1].CompareTo(ordered[index]) > 0);
        }
    }

    [Fact]
    public void CompareTo_UsesStableIdAsFinalTieBreaker()
    {
        var first = new RenderSortKey2D((int)RenderPass2D.YSortedWorld, 1, 2, 3, 400, 5, 10);
        var second = new RenderSortKey2D((int)RenderPass2D.YSortedWorld, 1, 2, 3, 400, 5, 20);

        Assert.True(first.CompareTo(second) < 0);
        Assert.True(second.CompareTo(first) > 0);
    }

    [Fact]
    public void Equality_UsesAllFields()
    {
        var first = new RenderSortKey2D((int)RenderPass2D.YSortedWorld, 1, 2, 3, 400, 5, 10);
        var same = new RenderSortKey2D((int)RenderPass2D.YSortedWorld, 1, 2, 3, 400, 5, 10);
        var different = new RenderSortKey2D((int)RenderPass2D.YSortedWorld, 1, 2, 3, 401, 5, 10);

        Assert.Equal(first, same);
        Assert.True(first == same);
        Assert.NotEqual(first, different);
        Assert.True(first != different);
    }
}
