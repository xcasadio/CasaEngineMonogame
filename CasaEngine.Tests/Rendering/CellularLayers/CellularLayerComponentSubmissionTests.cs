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
/// Covers <see cref="CellularLayerComponent.Submit"/> and <see cref="CellularLayerComponent.ResolveTextures"/>
/// headlessly, the same way as the sibling mechanism's own <c>ScrollingLayerComponentSubmissionTests</c>:
/// an uninitialized <see cref="CasaEngineGame"/>, device-less <see cref="Texture2D"/>s,
/// <see cref="SpriteRendererComponent"/> built without a device, and the queued <c>_spriteDatas</c>
/// read by reflection.
/// </summary>
public class CellularLayerComponentSubmissionTests
{
    [Fact]
    public void Submit_BeforeAnySetFrame_QueuesNothing()
    {
        var (component, renderer) = CreateWired();
        component.Service.SetLayers(new[] { MakeOneCellLayer() });
        component.ResolveTextures(_ => CreateTexture());

        component.Submit(renderer, Vector3.Zero, new Rectangle(0, 0, 320, 240));

        Assert.Empty(GetSpriteDatas(renderer));
    }

    [Fact]
    public void Submit_GroundTrue_UsesEffectsPass_AtCameraDepth()
    {
        var (component, renderer) = CreateWired();
        var layer = MakeOneCellLayer();
        layer.Ground = true;
        component.Service.SetLayers(new[] { layer });
        var texture = CreateTexture();
        component.ResolveTextures(_ => texture);

        component.Service.SetFrame(0, 0, 1, new Vector3(0f, 0f, 5f));
        component.Service.Advance(() => 0u);
        component.Submit(renderer, component.Service.CameraTarget, new Rectangle(0, 0, 320, 240));

        var entry = Assert.Single(GetSpriteDatas(renderer));
        Assert.Equal((int)RenderPass2D.Effects, ((RenderSortKey2D)GetField(entry, "SortKey")).RenderPass);
        var worldMatrix = (Matrix)GetField(entry, "WorldMatrix");
        Assert.Equal(5f, worldMatrix.Translation.Z, 3);
    }

    [Fact]
    public void Submit_GroundFalse_UsesBackgroundPass_RecedingByConfiguredDepth()
    {
        var (component, renderer) = CreateWired();
        var layer = MakeOneCellLayer();
        layer.Ground = false;
        component.Service.SetLayers(new[] { layer });
        component.Service.SetConfiguration(new CellularLayerConfiguration(backgroundDepth: 2f));
        var texture = CreateTexture();
        component.ResolveTextures(_ => texture);

        component.Service.SetFrame(0, 0, 1, new Vector3(0f, 0f, 10f));
        component.Service.Advance(() => 0u);
        component.Submit(renderer, component.Service.CameraTarget, new Rectangle(0, 0, 320, 240));

        var entry = Assert.Single(GetSpriteDatas(renderer));
        Assert.Equal((int)RenderPass2D.Background, ((RenderSortKey2D)GetField(entry, "SortKey")).RenderPass);
        var worldMatrix = (Matrix)GetField(entry, "WorldMatrix");
        Assert.Equal(8f, worldMatrix.Translation.Z, 3); // 10 - 2.
    }

    [Fact]
    public void Submit_PassesTheGivenScissorRectangleThrough()
    {
        var (component, renderer) = CreateWired();
        component.Service.SetLayers(new[] { MakeOneCellLayer() });
        component.ResolveTextures(_ => CreateTexture());

        var scissor = new Rectangle(10, 20, 320, 240);
        component.Service.SetFrame(0, 0, 1, Vector3.Zero);
        component.Service.Advance(() => 0u);
        component.Submit(renderer, component.Service.CameraTarget, scissor);

        var entry = Assert.Single(GetSpriteDatas(renderer));
        Assert.Equal(scissor, (Rectangle)GetField(entry, "ScissorRectangle"));
    }

    [Fact]
    public void Submit_SourceRectangleSize_MatchesTheCellsUvBox()
    {
        // The uninitialized (device-less) Texture2D used here reports a zero size, so the queued UV
        // coordinates themselves cannot be read back meaningfully (same limitation the sibling
        // mechanism's own submission tests note) - but the source rectangle's WIDTH/HEIGHT feed the
        // world matrix's scale directly (MatrixExtensions.Transformation), independently of the
        // texture, so they are still observable. The phase-shifted V itself is covered at the pure
        // function level (CellularLayerServicePureFunctionsTests.ComputeSourceV_WrapsModulo256).
        var (component, renderer) = CreateWired();
        var layer = MakeOneCellLayer(v0: 10, v1: 25); // width 16 (U1-U0+1=15-0+1), height 16 (25-10+1).
        component.Service.SetLayers(new[] { layer });
        component.ResolveTextures(_ => CreateTexture());

        component.Service.SetFrame(0, 0, 1, Vector3.Zero);
        component.Service.Advance(() => 0u);
        component.Submit(renderer, component.Service.CameraTarget, new Rectangle(0, 0, 320, 240));

        var entry = Assert.Single(GetSpriteDatas(renderer));
        var worldMatrix = (Matrix)GetField(entry, "WorldMatrix");
        Assert.Equal(16f, worldMatrix.M11, 3); // scale.X * sourceInTexture.Width, scale.X == 1.
        Assert.Equal(16f, worldMatrix.M22, 3); // scale.Y * sourceInTexture.Height, scale.Y == 1.
    }

    [Fact]
    public void Submit_CellWhoseSheetFailedToResolve_IsSkipped()
    {
        var (component, renderer) = CreateWired();
        component.Service.SetLayers(new[] { MakeOneCellLayer() });
        component.ResolveTextures(_ => null); // every sheet fails to load.

        component.Service.SetFrame(0, 0, 1, Vector3.Zero);
        component.Service.Advance(() => 0u);
        component.Submit(renderer, component.Service.CameraTarget, new Rectangle(0, 0, 320, 240));

        Assert.Empty(GetSpriteDatas(renderer));
    }

    [Fact]
    public void RandomSource_Unwired_DoesNotThrow_ItWarnsAndRespawnsAtZero()
    {
        // This runs on the per-frame render path, where the engine's rules forbid throwing every
        // frame. An unwired stream is reported once and degrades to abscissa 0 rather than taking
        // the frame down; wiring it to the gameplay DLL's shared stream is D7's job, in slice C3.
        var (component, _) = CreateWired();
        component.Service.SetLayers(new[] { MakeOneCellLayer() }); // Normal cell - never asks for it.

        component.Service.SetFrame(0, 0, 1, Vector3.Zero);
        var exception = Record.Exception(() => component.Service.Advance(component.RandomSource));

        Assert.Null(exception);

        // And calling it directly - what a FallRespawn cell would do - yields 0 instead of throwing.
        var unwiredException = Record.Exception(() => component.RandomSource());
        Assert.Null(unwiredException);
        Assert.Equal(0u, component.RandomSource());
    }

    // ---- Helpers ------------------------------------------------------------------------------------

    private static CellularLayerDefinition MakeOneCellLayer(int animTimer = 100, int animNum = 1, int v0 = 0, int v1 = 15)
    {
        return new CellularLayerDefinition
        {
            LayerId = 7,
            AnimTimer = animTimer,
            AnimNum = animNum,
            Ground = true,
            Blend = SpriteBlendMode.Opaque,
            Tint = Color.White,
            SheetTextureAssetIds = new[] { Guid.NewGuid() },
            Cells = new[]
            {
                new CellularCellDefinition
                {
                    PalDex = 0,
                    U0 = 0,
                    U1 = 15,
                    V0 = v0,
                    V1 = v1,
                    Type = CellularCellType.Normal,
                    X0 = 100,
                    Y0 = 100,
                },
            },
        };
    }

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
