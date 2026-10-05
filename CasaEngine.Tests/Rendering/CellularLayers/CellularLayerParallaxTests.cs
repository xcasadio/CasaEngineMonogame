using CasaEngine.Framework.Rendering.CellularLayers;
using Microsoft.Xna.Framework;
using Xunit;

namespace CasaEngine.Tests.Rendering.CellularLayers;

/// <summary>
/// The camera factor of a <see cref="CellularCellType.Normal"/> cell is truncated once, <c>Num / Den</c> in signed integer
/// division, then multiplied by the camera (the original's type 0, <c>0x8005C0AC</c> and <c>0x8005CB78</c>; plan E19.m3 rule
/// M3-R2, ADR-0052). A <see cref="CellularCellType.FallRespawn"/> cell keeps <c>camera * Num / Den</c> (type 2,
/// <c>0x8005D0C4</c>).
/// </summary>
public class CellularLayerParallaxTests
{
    private static (int x, int y) DrawOneTick(CellularCellType type, int num, int den, int cameraX, int cameraY)
    {
        var service = new CellularLayerService();
        service.SetLayers(new[]
        {
            new CellularLayerDefinition
            {
                AnimTimer = 100,
                AnimNum = 1,
                Cells = new[]
                {
                    new CellularCellDefinition
                    {
                        Type = type, X0 = 100, Y0 = 100, U0 = 0, U1 = 15, V0 = 0, V1 = 15,
                        CamXNum = num, CamXDen = den, CamYNum = num, CamYDen = den,
                    },
                },
            },
        });

        service.SetFrame(cameraX, cameraY, 1, Vector3.Zero);
        service.Advance(() => 0u);

        Assert.True(service.TryGetCellState(0, 0, out var cell));
        return (cell.DrawX, cell.DrawY);
    }

    [Theory]
    [InlineData(1, 2, 100, 60, 100, 100)] // factor 0: the cell is fixed on screen.
    [InlineData(3, 2, 10, 10, 90, 90)] // factor 1.
    [InlineData(-1, 2, 10, 10, 100, 100)] // truncated toward zero: 0.
    [InlineData(1, 1, 37, 21, 63, 79)] // guard: an exact factor is unchanged.
    [InlineData(2, 1, 20, 10, 60, 80)]
    public void NormalCell_UsesTheTruncatedFactorTimesTheCamera(int num, int den, int cameraX, int cameraY, int expectedX, int expectedY)
    {
        Assert.Equal((expectedX, expectedY), DrawOneTick(CellularCellType.Normal, num, den, cameraX, cameraY));
    }

    [Fact]
    public void NormalCell_ZeroDenominator_DisablesTheParallax()
    {
        Assert.Equal((100, 100), DrawOneTick(CellularCellType.Normal, 5, 0, 40, 40));
    }

    [Fact]
    public void FallRespawnCell_KeepsTheRuntimeFormula()
    {
        Assert.Equal((50, 70), DrawOneTick(CellularCellType.FallRespawn, 1, 2, 100, 60));
    }
}
