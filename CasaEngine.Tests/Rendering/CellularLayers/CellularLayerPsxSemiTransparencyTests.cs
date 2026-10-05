using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Application.Components;
using CasaEngine.Framework.Assets.Sprites;
using CasaEngine.Framework.Rendering.CellularLayers;
using CasaEngine.Framework.Rendering.Depth;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace CasaEngine.Tests.Rendering.CellularLayers;

/// <summary>
/// A cellular layer that carries a PSX semi-transparency mode draws each cell as two entries of the same sort key on two
/// disjoint raw-alpha windows (E19.g G2c, ADR-0051 extended to the background layers); the two entries of cell c keep the
/// per-cell sort offset <c>-c</c> of ADR-0052. A layer without a mode is unchanged (one entry per cell, the blend of the layer).
/// Queue-level tests: the pixels are proved by the engine demo.
/// </summary>
public class CellularLayerPsxSemiTransparencyTests
{
    [Fact]
    public void LayerWithoutMode_IsOneEntryPerCell_WithTheBlendOfTheLayer()
    {
        var entries = Submit(MakeLayer(SpritePsxSemiTransparency.None, SpriteBlendMode.Additive));

        Assert.Equal(3, entries.Count);
        foreach (var entry in entries)
        {
            AssertEntry(entry, SpriteBlendMode.Additive, -1f, 2f, Color.White);
        }
    }

    [Theory]
    [InlineData(SpritePsxSemiTransparency.Mode0, SpriteBlendMode.AlphaBlend, false)]
    [InlineData(SpritePsxSemiTransparency.Mode1, SpriteBlendMode.Additive, false)]
    [InlineData(SpritePsxSemiTransparency.Mode2, SpriteBlendMode.Subtractive, false)]
    [InlineData(SpritePsxSemiTransparency.Mode3, SpriteBlendMode.Additive, true)]
    public void LayerWithMode_IsTwoEntriesPerCell_WithTheOffsetOfTheCell(
        SpritePsxSemiTransparency mode, SpriteBlendMode expectedStpBlend, bool expectedQuarterColor)
    {
        var entries = Submit(MakeLayer(mode, SpriteBlendMode.Opaque));

        Assert.Equal(6, entries.Count);
        for (var c = 0; c < 3; c++)
        {
            var opaque = entries[2 * c];
            var stp = entries[2 * c + 1];
            Assert.Equal(-c, ((RenderSortKey2D)GetField(opaque, "SortKey")).LocalSortOffset);
            Assert.Equal(GetField(opaque, "SortKey"), GetField(stp, "SortKey"));
            Assert.Equal(GetField(opaque, "WorldMatrix"), GetField(stp, "WorldMatrix"));
            AssertEntry(opaque, SpriteBlendMode.Opaque, 0.75f, 1f, Color.White);
            AssertEntry(stp, expectedStpBlend, 0.25f, 0.75f, expectedQuarterColor ? new Color(64, 64, 64, 255) : Color.White);
        }
    }

    [Fact]
    public void LayerWithMode_IgnoresItsBlend()
    {
        var entries = Submit(MakeLayer(SpritePsxSemiTransparency.Mode0, SpriteBlendMode.Additive));

        Assert.Equal(6, entries.Count);
        AssertEntry(entries[0], SpriteBlendMode.Opaque, 0.75f, 1f, Color.White);
        AssertEntry(entries[1], SpriteBlendMode.AlphaBlend, 0.25f, 0.75f, Color.White);
    }

    // ---- helpers ----

    private static CellularLayerDefinition MakeLayer(SpritePsxSemiTransparency mode, SpriteBlendMode blend)
    {
        var cells = new CellularCellDefinition[3];
        for (var c = 0; c < 3; c++)
        {
            cells[c] = new CellularCellDefinition { Type = CellularCellType.Normal, X0 = 100 + 10 * c, Y0 = 100, U0 = 0, U1 = 15, V0 = 0, V1 = 15 };
        }

        return new CellularLayerDefinition
        {
            LayerId = 7,
            AnimTimer = 100,
            AnimNum = 1,
            Ground = true,
            Blend = blend,
            Tint = Color.White,
            SheetTextureAssetIds = new[] { Guid.NewGuid() },
            Cells = cells,
            PsxSemiTransparency = mode,
        };
    }

    private static List<object> Submit(CellularLayerDefinition layer)
    {
        var game = (CasaEngineGame)RuntimeHelpers.GetUninitializedObject(typeof(CasaEngineGame));
        var componentsField = typeof(Game).GetField("_components", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(componentsField);
        componentsField!.SetValue(game, new GameComponentCollection());
        var renderer = new SpriteRendererComponent(game);
        var component = new CellularLayerComponent(game);

        component.Service.SetLayers(new[] { layer });
        component.ResolveTextures(_ => (Texture2D)RuntimeHelpers.GetUninitializedObject(typeof(Texture2D)));
        component.Service.SetFrame(0, 0, 1, Vector3.Zero);
        component.Service.Advance(() => 0u);
        component.Submit(renderer, component.Service.CameraTarget, new Rectangle(0, 0, 320, 240));

        var field = typeof(SpriteRendererComponent).GetField("_spriteDatas", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        var entries = new List<object>();
        foreach (var entry in (System.Collections.IList)field!.GetValue(renderer)!)
        {
            entries.Add(entry!);
        }

        return entries;
    }

    private static void AssertEntry(object entry, SpriteBlendMode blend, float alphaMin, float alphaMax, Color color)
    {
        Assert.Equal(blend, (SpriteBlendMode)GetField(entry, "BlendMode"));
        Assert.Equal(alphaMin, (float)GetField(entry, "AlphaMin"));
        Assert.Equal(alphaMax, (float)GetField(entry, "AlphaMax"));
        Assert.Equal(color, (Color)GetField(entry, "Color"));
    }

    private static object GetField(object instance, string fieldName)
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return field!.GetValue(instance)!;
    }
}
