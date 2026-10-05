using System;
using CasaEngine.Framework.Rendering.CellularLayers;
using Microsoft.Xna.Framework;
using Xunit;

namespace CasaEngine.Tests.Rendering.CellularLayers;

/// <summary>
/// The wave counter of the cellular layers is one byte for the whole service, like the single word of the original
/// (<c>0x800C48C4</c>, plan E19.m3 rule M3-R1, ADR-0052): it advances once per tick, before the layers, as long as layers
/// were set since the last <see cref="CellularLayerService.Clear"/>, and nothing resets it. With the identity wave table,
/// <c>BWavePhase</c> 1, <c>BWaveWeight</c> 128 and a wave cell at (50, 50), the drawn abscissa is <c>42 + counter</c>.
/// </summary>
public class CellularLayerWaveCounterTests
{
    private static CellularLayerDefinition MakeWaveLayer(int layerId)
    {
        return new CellularLayerDefinition
        {
            LayerId = layerId,
            AnimTimer = 100,
            AnimNum = 1,
            BWavePhase = 1,
            BWaveWeight = 128,
            Cells = new[]
            {
                new CellularCellDefinition { Type = CellularCellType.WaveX, X0 = 50, Y0 = 50, U0 = 0, U1 = 15, V0 = 0, V1 = 15 },
            },
        };
    }

    private static int[] IdentityLut()
    {
        var lut = new int[256];
        for (var i = 0; i < lut.Length; i++)
        {
            lut[i] = i;
        }

        return lut;
    }

    private static CellularLayerService CreateService(params CellularLayerDefinition[] layers)
    {
        var service = new CellularLayerService();
        service.SetWaveLut(IdentityLut());
        service.SetLayers(layers);
        return service;
    }

    private static void Tick(CellularLayerService service, int ticks)
    {
        service.SetFrame(0, 0, ticks, Vector3.Zero);
        service.Advance(() => 0u);
    }

    private static (byte waveTick, int drawX) Read(CellularLayerService service, int layerIndex)
    {
        Assert.True(service.TryGetLayerState(layerIndex, out var layer));
        Assert.True(service.TryGetCellState(layerIndex, 0, out var cell));
        return (layer.WaveTick, cell.DrawX);
    }

    [Fact]
    public void SetLayers_AgainAfterTicks_DoesNotResetTheCounter()
    {
        var service = CreateService(MakeWaveLayer(0));
        Tick(service, 3);

        service.SetLayers(new[] { MakeWaveLayer(0) });
        Tick(service, 1);

        Assert.Equal(((byte)4, 46), Read(service, 0));
    }

    [Fact]
    public void Clear_ClosesTheGate_TicksWithoutLayersDoNotCount_AndTheCounterIsKept()
    {
        var service = CreateService(MakeWaveLayer(0));
        Tick(service, 2);

        service.Clear();
        Tick(service, 3);
        service.SetWaveLut(IdentityLut());
        service.SetLayers(new[] { MakeWaveLayer(0) });
        Tick(service, 1);

        Assert.Equal(((byte)3, 45), Read(service, 0));
    }

    [Fact]
    public void ResetLayerRuntimeState_DoesNotResetTheCounter()
    {
        var service = CreateService(MakeWaveLayer(0));
        Tick(service, 5);

        service.ResetLayerRuntimeState();
        Tick(service, 1);

        Assert.Equal(((byte)6, 48), Read(service, 0));
    }

    [Fact]
    public void MaskedLayer_DoesNotFreezeTheCounter_BothLayersSeeTheSameValue()
    {
        var service = CreateService(MakeWaveLayer(0), MakeWaveLayer(1));
        service.SetLayerActive(0, false);
        Tick(service, 2);
        service.SetLayerActive(0, true);
        Tick(service, 1);

        Assert.Equal(((byte)3, 45), Read(service, 0));
        Assert.Equal(((byte)3, 45), Read(service, 1));
    }

    [Fact]
    public void MaskThenResume_TheWaveResumesAtTheAdvancedPhase()
    {
        var service = CreateService(MakeWaveLayer(0));
        Tick(service, 1);
        service.SetLayerActive(0, false);
        Tick(service, 3);
        service.SetLayerActive(0, true);
        Tick(service, 1);

        Assert.Equal(((byte)5, 47), Read(service, 0));
    }

    [Fact]
    public void EmptyLayerList_KeepsTheGateOpen_ATilesOnlyMapStillCounts()
    {
        var service = CreateService(MakeWaveLayer(0));
        Tick(service, 1);

        service.SetLayers(Array.Empty<CellularLayerDefinition>());
        Tick(service, 4);
        service.SetLayers(new[] { MakeWaveLayer(0) });
        Tick(service, 1);

        Assert.Equal(((byte)6, 48), Read(service, 0));
    }
}
