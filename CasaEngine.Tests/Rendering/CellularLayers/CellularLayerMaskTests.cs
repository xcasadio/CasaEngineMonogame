using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Application.Components;
using CasaEngine.Framework.Rendering.CellularLayers;
using CasaEngine.Framework.Rendering.Depth;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace CasaEngine.Tests.Rendering.CellularLayers;

/// <summary>
/// <see cref="CellularLayerService.SetLayerActive"/> (plan E19.k2, rule K2-R1): a layer is addressed by its
/// <see cref="CellularLayerDefinition.LayerId"/>; an inactive layer is frozen (cadence, wave tick, cell
/// positions, random draws) and is not submitted by <see cref="CellularLayerComponent"/>.
/// </summary>
public class CellularLayerMaskTests
{
    private int _randomCalls;

    private uint CountingRandom()
    {
        _randomCalls++;
        return 0x80000000u;
    }

    // Layer with a Normal cell (drifts by 2,1 per tick), a FallRespawn cell (overshoots the bottom every tick, so
    // it draws a random value every tick) and a WaveX cell (needs a non-empty lookup table).
    private static CellularLayerDefinition MakeLayer(int layerId)
    {
        return new CellularLayerDefinition
        {
            LayerId = layerId,
            AnimTimer = 100,
            AnimNum = 1,
            Ground = true,
            Blend = SpriteBlendMode.Opaque,
            Tint = Color.White,
            SheetTextureAssetIds = new[] { Guid.NewGuid() },
            Cells = new[]
            {
                new CellularCellDefinition { Type = CellularCellType.Normal, X0 = 10, Y0 = 10, U0 = 0, U1 = 15, V0 = 0, V1 = 15, DX = 2, DY = 1 },
                new CellularCellDefinition { Type = CellularCellType.FallRespawn, X0 = 0, Y0 = 0, U0 = 0, U1 = 15, V0 = 0, V1 = 15, DY = 300 },
                new CellularCellDefinition { Type = CellularCellType.WaveX, X0 = 50, Y0 = 50, U0 = 0, U1 = 15, V0 = 0, V1 = 15 },
            },
        };
    }

    private static CellularLayerService CreateService(params CellularLayerDefinition[] layers)
    {
        var service = new CellularLayerService();
        service.SetLayers(layers);
        service.SetWaveLut(new int[256]);
        return service;
    }

    private void Tick(CellularLayerService service, int ticks)
    {
        service.SetFrame(0, 0, ticks, Vector3.Zero);
        service.Advance(CountingRandom);
    }

    private static (CellularLayerState layer, CellularCellState normal, CellularCellState fall, CellularCellState wave) Snapshot(
        CellularLayerService service, int index)
    {
        Assert.True(service.TryGetLayerState(index, out var layer));
        Assert.True(service.TryGetCellState(index, 0, out var normal));
        Assert.True(service.TryGetCellState(index, 1, out var fall));
        Assert.True(service.TryGetCellState(index, 2, out var wave));
        return (layer, normal, fall, wave);
    }

    [Fact]
    public void SetLayerActive_False_FreezesCadenceWaveTickCellsAndRandomDraws_ThenTrueResumesThem()
    {
        var service = CreateService(MakeLayer(0));

        Tick(service, 1);
        var afterOne = Snapshot(service, 0);
        Assert.Equal((byte)1, afterOne.layer.WaveTick);
        Assert.Equal(1, afterOne.layer.AnimFrameTimer);
        Assert.Equal(12, afterOne.normal.DrawX);
        Assert.Equal(11, afterOne.normal.DrawY);
        Assert.Equal(1, _randomCalls);

        service.SetLayerActive(0, false);
        Assert.False(service.IsLayerActive(0));
        Tick(service, 3);

        var frozen = Snapshot(service, 0);
        Assert.Equal(afterOne, frozen);
        Assert.Equal(1, _randomCalls); // no draw while masked.

        service.SetLayerActive(0, true);
        Assert.True(service.IsLayerActive(0));
        Tick(service, 1);

        var resumed = Snapshot(service, 0);
        Assert.Equal((byte)2, resumed.layer.WaveTick);
        Assert.Equal(2, resumed.layer.AnimFrameTimer);
        Assert.Equal(14, resumed.normal.DrawX);
        Assert.Equal(12, resumed.normal.DrawY);
        Assert.Equal(2, _randomCalls);
    }

    [Fact]
    public void SetLayerActive_AddressesTheLayerId_AnAbsentIdIsNoEffect()
    {
        // One layer, identifier 1, at index 0.
        var service = CreateService(MakeLayer(1));

        service.SetLayerActive(0, false);
        Assert.True(service.IsLayerActive(0));
        Tick(service, 1);
        Assert.Equal((byte)1, Snapshot(service, 0).layer.WaveTick);

        service.SetLayerActive(1, false);
        Assert.False(service.IsLayerActive(0));
        Tick(service, 2);
        Assert.Equal((byte)1, Snapshot(service, 0).layer.WaveTick);
    }

    [Fact]
    public void SetLayerActive_OnlyTheMatchingLayerIsMasked()
    {
        var service = CreateService(MakeLayer(0), MakeLayer(1));

        service.SetLayerActive(0, false);
        Tick(service, 2);

        Assert.Equal((byte)0, Snapshot(service, 0).layer.WaveTick);
        Assert.Equal((byte)2, Snapshot(service, 1).layer.WaveTick);
        Assert.Equal(2, _randomCalls); // only layer 1's fall cell drew, once per tick.
    }

    [Fact]
    public void SetLayers_AfterAMask_MakesEveryLayerActive()
    {
        var service = CreateService(MakeLayer(0), MakeLayer(1));
        service.SetLayerActive(0, false);
        service.SetLayerActive(1, false);

        service.SetLayers(new[] { MakeLayer(0), MakeLayer(1) });

        Assert.True(service.IsLayerActive(0));
        Assert.True(service.IsLayerActive(1));
    }

    [Fact]
    public void Clear_AfterAMask_MakesTheNextLayersActive()
    {
        var service = CreateService(MakeLayer(0));
        service.SetLayerActive(0, false);

        service.Clear();
        service.SetLayers(new[] { MakeLayer(0) });

        Assert.True(service.IsLayerActive(0));
    }

    [Fact]
    public void Submit_InactiveLayer_QueuesNothing_EvenWithStaleDrawableCells()
    {
        var (component, renderer) = CreateWired();
        component.Service.SetLayers(new[] { MakeLayer(0) });
        component.ResolveTextures(_ => CreateTexture());

        component.Service.SetFrame(0, 0, 1, Vector3.Zero);
        component.Service.Advance(() => 0u);

        component.Submit(renderer, component.Service.CameraTarget, new Rectangle(0, 0, 320, 240));
        var drawn = GetSpriteDatas(renderer).Count;
        Assert.True(drawn > 0);

        component.Service.SetLayerActive(0, false);
        GetSpriteDatas(renderer).Clear();
        component.Submit(renderer, component.Service.CameraTarget, new Rectangle(0, 0, 320, 240));
        Assert.Empty(GetSpriteDatas(renderer));

        component.Service.SetLayerActive(0, true);
        component.Submit(renderer, component.Service.CameraTarget, new Rectangle(0, 0, 320, 240));
        Assert.Equal(drawn, GetSpriteDatas(renderer).Count);
    }

    // ---- Helpers ------------------------------------------------------------------------------------

    private static (CellularLayerComponent component, SpriteRendererComponent renderer) CreateWired()
    {
        var game = CreateHeadlessGame();
        var renderer = new SpriteRendererComponent(game);
        var component = new CellularLayerComponent(game);
        return (component, renderer);
    }

    private static CasaEngineGame CreateHeadlessGame()
    {
        var game = (CasaEngineGame)RuntimeHelpers.GetUninitializedObject(typeof(CasaEngineGame));
        var componentsField = typeof(Game).GetField("_components", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(componentsField);
        componentsField!.SetValue(game, new GameComponentCollection());
        return game;
    }

    private static Texture2D CreateTexture()
    {
        return (Texture2D)RuntimeHelpers.GetUninitializedObject(typeof(Texture2D));
    }

    private static System.Collections.IList GetSpriteDatas(SpriteRendererComponent component)
    {
        var field = typeof(SpriteRendererComponent).GetField("_spriteDatas", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return (System.Collections.IList)field!.GetValue(component)!;
    }
}
