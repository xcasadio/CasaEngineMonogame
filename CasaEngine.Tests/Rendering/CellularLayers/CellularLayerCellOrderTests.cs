using System;
using System.Collections.Generic;
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
/// The cells of a cellular layer are drawn with cell 0 on top, like the original, which inserts every cell at the head of
/// the same ordering-table slot (plan E19.m3 rule M3-R3, ADR-0052): each cell has its own sort key with
/// <c>LocalSortOffset = -cellIndex</c>, so the order does not depend on the stability of the queue sort.
/// </summary>
public class CellularLayerCellOrderTests
{
    private static CellularCellDefinition MakeCell(CellularCellType type, int x0)
    {
        return new CellularCellDefinition { Type = type, X0 = x0, Y0 = 100, U0 = 0, U1 = 15, V0 = 0, V1 = 15 };
    }

    private static CellularLayerDefinition MakeLayer(int layerId, int orderInLayer, params CellularCellDefinition[] cells)
    {
        return new CellularLayerDefinition
        {
            LayerId = layerId,
            OrderInLayer = orderInLayer,
            AnimTimer = 100,
            AnimNum = 1,
            Ground = true,
            Blend = SpriteBlendMode.Opaque,
            Tint = Color.White,
            SheetTextureAssetIds = new[] { Guid.NewGuid() },
            Cells = cells,
        };
    }

    private static CellularCellDefinition[] ThreeNormalCells()
    {
        return new[]
        {
            MakeCell(CellularCellType.Normal, 100),
            MakeCell(CellularCellType.Normal, 110),
            MakeCell(CellularCellType.Normal, 120),
        };
    }

    private static List<object> Submit(params CellularLayerDefinition[] layers)
    {
        var (component, renderer) = CreateWired();
        component.Service.SetLayers(layers);
        component.ResolveTextures(_ => CreateTexture());
        component.Service.SetFrame(0, 0, 1, Vector3.Zero);
        component.Service.Advance(() => 0u);
        component.Submit(renderer, component.Service.CameraTarget, new Rectangle(0, 0, 320, 240));

        var entries = new List<object>();
        foreach (var entry in GetSpriteDatas(renderer))
        {
            entries.Add(entry!);
        }

        _lastRenderer = renderer;
        return entries;
    }

    private static SpriteRendererComponent _lastRenderer;

    private static RenderSortKey2D KeyOf(object entry)
    {
        return (RenderSortKey2D)GetField(entry, "SortKey");
    }

    [Fact]
    public void ThreeCells_GetDescendingLocalSortOffsets_AndStrictlyOrderedKeys()
    {
        var entries = Submit(MakeLayer(7, 1, ThreeNormalCells()));

        Assert.Equal(3, entries.Count);
        var keys = new[] { KeyOf(entries[0]), KeyOf(entries[1]), KeyOf(entries[2]) };
        Assert.Equal(new[] { 0, -1, -2 }, new[] { keys[0].LocalSortOffset, keys[1].LocalSortOffset, keys[2].LocalSortOffset });
        Assert.True(keys[2].CompareTo(keys[1]) < 0);
        Assert.True(keys[1].CompareTo(keys[0]) < 0);
        foreach (var key in keys)
        {
            Assert.Equal((int)RenderPass2D.Effects, key.RenderPass);
            Assert.Equal(0, key.SortingLayer);
            Assert.Equal(1, key.OrderInLayer);
            Assert.Equal(7, key.StableId);
        }
    }

    [Fact]
    public void SkippedCell_TheOffsetFollowsTheCellIndex_NotTheSubmissionCount()
    {
        var entries = Submit(MakeLayer(7, 0,
            MakeCell(CellularCellType.Normal, 100),
            MakeCell(CellularCellType.ScriptTrack, 110),
            MakeCell(CellularCellType.Normal, 120)));

        Assert.Equal(2, entries.Count);
        Assert.Equal(0, KeyOf(entries[0]).LocalSortOffset);
        Assert.Equal(-2, KeyOf(entries[1]).LocalSortOffset);
    }

    [Fact]
    public void TwoLayers_EveryKeyOfLayer1SortsBeforeEveryKeyOfLayer0()
    {
        // The converter gives OrderInLayer 1 to layer 0 and 0 to layer 1 (so layer 0 is on top): unchanged by the cell offsets.
        var entries = Submit(
            MakeLayer(0, 1, ThreeNormalCells()),
            MakeLayer(1, 0, ThreeNormalCells()));

        Assert.Equal(6, entries.Count);
        var layer0 = new List<RenderSortKey2D>();
        var layer1 = new List<RenderSortKey2D>();
        foreach (var entry in entries)
        {
            var key = KeyOf(entry);
            (key.StableId == 0 ? layer0 : layer1).Add(key);
        }

        Assert.Equal(3, layer0.Count);
        Assert.Equal(3, layer1.Count);
        foreach (var low in layer1)
        {
            foreach (var high in layer0)
            {
                Assert.True(low.CompareTo(high) < 0);
            }
        }
    }

    [Fact]
    public void AfterTheQueueSort_TheCellsAreDrawnLastToFirst_CellZeroOnTop()
    {
        var entries = Submit(MakeLayer(7, 0, ThreeNormalCells()));
        Assert.Equal(3, entries.Count);

        _lastRenderer.FillVertices();

        // Cells sit at x 100, 110, 120 of the 320 wide screen and are 16 wide: world centre x = x - 160 + 8. The queue is drawn in list order, later on top.
        var sorted = GetSpriteDatas(_lastRenderer);
        var xs = new float[3];
        for (var i = 0; i < 3; i++)
        {
            xs[i] = ((Matrix)GetField(sorted[i]!, "WorldMatrix")).Translation.X;
        }

        Assert.Equal(new[] { -32f, -42f, -52f }, xs);
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

    private static object GetField(object instance, string fieldName)
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return field!.GetValue(instance)!;
    }
}
