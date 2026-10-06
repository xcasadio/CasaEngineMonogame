using CasaEngine.Framework.Assets.Sprites;
using CasaEngine.Framework.Rendering.Depth;
using CasaEngine.Framework.Rendering.EffectQuads;
using Microsoft.Xna.Framework;
using Xunit;

namespace CasaEngine.Tests.Rendering.EffectQuads;

/// <summary>
/// Covers <see cref="EffectQuadService"/> (E19.g G2e, ADR-0071): a GPU-free holder of the sheet ids and of the last pushed frame of
/// effect quads. The component that draws them is covered by <see cref="EffectQuadComponentSubmissionTests"/>.
/// </summary>
public class EffectQuadServiceTests
{
    private static readonly Guid SheetA = Guid.Parse("a1000000-0000-0000-0000-000000000001");
    private static readonly Guid SheetB = Guid.Parse("a1000000-0000-0000-0000-000000000002");

    [Fact]
    public void NewService_IsEmpty_AtVersionZero()
    {
        var service = new EffectQuadService();

        Assert.Equal(0, service.SheetsVersion);
        Assert.Equal(0, service.SheetCount);
        Assert.Equal(0, service.QuadCount);
    }

    [Fact]
    public void SetSheets_StoresTheIdsInOrder_KeepsEmptyForAnAbsentSheet_AndRaisesTheVersionEveryCall()
    {
        var service = new EffectQuadService();

        service.SetSheets(new[] { SheetA, Guid.Empty, SheetB });

        Assert.Equal(1, service.SheetsVersion);
        Assert.Equal(3, service.SheetCount);
        Assert.Equal(SheetA, service.GetSheetId(0));
        Assert.Equal(Guid.Empty, service.GetSheetId(1));
        Assert.Equal(SheetB, service.GetSheetId(2));

        // The same ids again still raise the version: the caller decides when the sheets change.
        service.SetSheets(new[] { SheetA, Guid.Empty, SheetB });

        Assert.Equal(2, service.SheetsVersion);
    }

    [Fact]
    public void SetSheets_CopiesTheIds()
    {
        var service = new EffectQuadService();
        var ids = new[] { SheetA, SheetB };

        service.SetSheets(ids);
        ids[0] = Guid.Empty;

        Assert.Equal(SheetA, service.GetSheetId(0));
    }

    [Fact]
    public void SetFrame_CopiesTheQuads_InOrder_WithEveryFieldKept()
    {
        var service = new EffectQuadService();
        var quads = new[] { MakeQuad(1, 5), MakeQuad(0, 2) };

        service.SetFrame(quads);
        quads[0] = MakeQuad(9, 9);

        Assert.Equal(2, service.QuadCount);
        Assert.Equal(MakeQuad(1, 5), service.GetQuad(0));
        Assert.Equal(MakeQuad(0, 2), service.GetQuad(1));
    }

    [Fact]
    public void SetFrame_ReplacesThePreviousFrame_AndAnEmptyFrameEmptiesIt()
    {
        var service = new EffectQuadService();
        service.SetFrame(new[] { MakeQuad(0, 1), MakeQuad(0, 2), MakeQuad(0, 3) });

        service.SetFrame(new[] { MakeQuad(1, 7) });

        Assert.Equal(1, service.QuadCount);
        Assert.Equal(MakeQuad(1, 7), service.GetQuad(0));

        service.SetFrame(ReadOnlySpan<EffectQuad>.Empty);

        Assert.Equal(0, service.QuadCount);
    }

    [Fact]
    public void TheFrame_PersistsUntilTheNextSetFrameOrClear_ReadingItDoesNotConsumeIt()
    {
        var service = new EffectQuadService();
        service.SetSheets(new[] { SheetA });
        service.SetFrame(new[] { MakeQuad(0, 4) });

        _ = service.GetQuad(0);
        _ = service.QuadCount;
        service.SetSheets(new[] { SheetA, SheetB });

        Assert.Equal(1, service.QuadCount);
        Assert.Equal(MakeQuad(0, 4), service.GetQuad(0));
    }

    [Fact]
    public void Clear_EmptiesTheSheetsAndTheFrame_AndRaisesTheVersion()
    {
        var service = new EffectQuadService();
        service.SetSheets(new[] { SheetA, SheetB });
        service.SetFrame(new[] { MakeQuad(0, 4) });
        var versionBefore = service.SheetsVersion;

        service.Clear();

        Assert.Equal(0, service.SheetCount);
        Assert.Equal(0, service.QuadCount);
        Assert.True(service.SheetsVersion > versionBefore);
    }

    [Fact]
    public void Clear_OnAnEmptyService_StillRaisesTheVersion()
    {
        var service = new EffectQuadService();

        service.Clear();

        Assert.Equal(1, service.SheetsVersion);
    }

    internal static EffectQuad MakeQuad(int sheetIndex, int localSortOffset)
    {
        return new EffectQuad(
            sheetIndex,
            new Rectangle(10, 20, 8, 6),
            new Vector2(0f, 0f),
            new Vector2(8f, 1f),
            new Vector2(1f, -6f),
            new Vector2(9f, -5f),
            SpritePsxSemiTransparency.None,
            new RenderSortKey2D((int)RenderPass2D.YSortedWorld, 300, 0, 214, 11100, localSortOffset, 0));
    }
}
