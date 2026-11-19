using System.Reflection;
using System.Runtime.CompilerServices;
using CasaEngine.Editor.Runtime.Overlays;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Application.Components;
using CasaEngine.Framework.Rendering.Depth;
using CasaEngine.Framework.Rendering.ScreenEffects;
using CasaEngine.Tests.UI.Backend;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace CasaEngine.Tests.Editor;

/// <summary>
/// The veil that the cutscene preview draws for a screen fade (ADR-0142): the blend states are the ones of the engine, a veil
/// that cannot be seen is not drawn, and what is drawn darkens the image and leaves the device as it was found.
/// </summary>
public class CutsceneScreenFadeOverlayTests
{
    private static SpriteRendererComponent CreateSpriteRenderer()
    {
        var game = (CasaEngineGame)RuntimeHelpers.GetUninitializedObject(typeof(CasaEngineGame));
        var componentsField = typeof(Game).GetField("_components", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(componentsField);
        componentsField!.SetValue(game, new GameComponentCollection());
        return new SpriteRendererComponent(game);
    }

    private static BlendState GetEngineBlendState(SpriteRendererComponent renderer, SpriteBlendMode mode)
    {
        var method = typeof(SpriteRendererComponent).GetMethod("GetBlendState", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return (BlendState)method!.Invoke(renderer, new object[] { mode })!;
    }

    [Theory]
    [InlineData(SpriteBlendMode.Opaque)]
    [InlineData(SpriteBlendMode.AlphaBlend)]
    [InlineData(SpriteBlendMode.Additive)]
    [InlineData(SpriteBlendMode.Subtractive)]
    public void TheBlendStateOfEachMode_HasTheSameFieldsAsTheOneOfTheEngine(SpriteBlendMode mode)
    {
        var renderer = CreateSpriteRenderer();

        var engine = GetEngineBlendState(renderer, mode);
        var editor = CutsceneScreenFadeOverlay.GetBlendState(mode);

        Assert.Equal(engine.ColorBlendFunction, editor.ColorBlendFunction);
        Assert.Equal(engine.ColorSourceBlend, editor.ColorSourceBlend);
        Assert.Equal(engine.ColorDestinationBlend, editor.ColorDestinationBlend);
        Assert.Equal(engine.AlphaBlendFunction, editor.AlphaBlendFunction);
        Assert.Equal(engine.AlphaSourceBlend, editor.AlphaSourceBlend);
        Assert.Equal(engine.AlphaDestinationBlend, editor.AlphaDestinationBlend);
    }

    [Fact]
    public void TheBlendStates_AreDefinedOnce()
    {
        Assert.Same(CutsceneScreenFadeOverlay.GetBlendState(SpriteBlendMode.Subtractive), CutsceneScreenFadeOverlay.GetBlendState(SpriteBlendMode.Subtractive));
        Assert.Same(CutsceneScreenFadeOverlay.GetBlendState(SpriteBlendMode.Additive), CutsceneScreenFadeOverlay.GetBlendState(SpriteBlendMode.Additive));
        Assert.Same(CutsceneScreenFadeOverlay.GetBlendState(SpriteBlendMode.Opaque), CutsceneScreenFadeOverlay.GetBlendState(SpriteBlendMode.Opaque));
        Assert.Same(BlendState.NonPremultiplied, CutsceneScreenFadeOverlay.GetBlendState(SpriteBlendMode.AlphaBlend));
    }

    [Fact]
    public void AnInactiveEffect_IsNotDrawn()
    {
        var effect = new ScreenEffectService();
        var overlay = new CutsceneScreenFadeOverlay(effect);

        Assert.False(CutsceneScreenFadeOverlay.ShouldDraw(effect));
        overlay.Draw(null!, null!, default);

        Assert.Equal(0, overlay.DrawCount);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(3, true)]
    [InlineData(255, true)]
    public void AnActiveEffectOfAnAlphaOfTwoOrLess_IsNotDrawn(byte alpha, bool drawn)
    {
        var effect = new ScreenEffectService();
        effect.SetOverlay(0, 0, 0, alpha, SpriteBlendMode.AlphaBlend);

        Assert.Equal(drawn, CutsceneScreenFadeOverlay.ShouldDraw(effect));
    }

    [Fact]
    public void AnActiveEffectOfAnAlphaOfTwoOrLess_AsksNoDrawCall()
    {
        var effect = new ScreenEffectService();
        effect.SetOverlay(255, 255, 255, 2, SpriteBlendMode.Subtractive);
        var overlay = new CutsceneScreenFadeOverlay(effect);

        overlay.Draw(null!, null!, default);

        Assert.Equal(0, overlay.DrawCount);
    }

    [Fact]
    public void AClearedEffect_IsNotDrawnAnymore()
    {
        var effect = new ScreenEffectService();
        effect.SetOverlay(255, 255, 255, 255, SpriteBlendMode.Subtractive);
        Assert.True(CutsceneScreenFadeOverlay.ShouldDraw(effect));

        effect.Clear();

        Assert.False(CutsceneScreenFadeOverlay.ShouldDraw(effect));
    }

    [Collection(GpuDeviceCollection.Name)]
    public class OnARealDevice
    {
        private const int Size = 64;

        private static Color[] DrawVeil(ScreenEffectService effect, Rectangle? viewport, out int drawCount, out bool statesRestored)
        {
            Color[] pixels = new Color[Size * Size];
            int count = 0;
            bool restored = false;
            GpuDeviceHost.Instance.Invoke(() =>
            {
                var device = GpuDeviceHost.Instance.GraphicsDevice;
                using var target = new RenderTarget2D(device, Size, Size, false, SurfaceFormat.Color, DepthFormat.None);
                using var overlay = new CutsceneScreenFadeOverlay(effect);

                device.SetRenderTarget(target);
                device.Viewport = new Viewport(0, 0, Size, Size);
                device.Clear(new Color(200, 200, 200, 255));
                if (viewport.HasValue)
                {
                    var rectangle = viewport.Value;
                    device.Viewport = new Viewport(rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height);
                }

                var viewportBefore = device.Viewport;
                device.BlendState = BlendState.Additive;
                device.DepthStencilState = DepthStencilState.Default;
                device.RasterizerState = RasterizerState.CullClockwise;

                overlay.Draw(device, null!, default);

                restored = ReferenceEquals(device.BlendState, BlendState.Additive)
                           && ReferenceEquals(device.DepthStencilState, DepthStencilState.Default)
                           && ReferenceEquals(device.RasterizerState, RasterizerState.CullClockwise)
                           && device.Viewport.Bounds == viewportBefore.Bounds
                           && device.GetRenderTargets().Length == 1
                           && ReferenceEquals(device.GetRenderTargets()[0].RenderTarget, target);

                device.SetRenderTarget(null);
                target.GetData(pixels);
                count = overlay.DrawCount;
            });
            drawCount = count;
            statesRestored = restored;
            return pixels;
        }

        [GpuFact]
        public void AWhiteSubtractiveVeil_TurnsALightImageToBlack_AndRestoresTheDevice()
        {
            var effect = new ScreenEffectService();
            effect.SetOverlay(255, 255, 255, 255, SpriteBlendMode.Subtractive);

            var pixels = DrawVeil(effect, null, out int drawCount, out bool restored);

            Assert.Equal(1, drawCount);
            Assert.True(restored);
            Assert.Equal(0, pixels[0].R);
            Assert.Equal(0, pixels[(Size / 2) * Size + Size / 2].G);
            Assert.Equal(0, pixels[Size * Size - 1].B);
        }

        [GpuFact]
        public void AHalfSubtractiveVeil_DarkensTheImageByHalf()
        {
            var effect = new ScreenEffectService();
            effect.SetOverlay(128, 128, 128, 255, SpriteBlendMode.Subtractive);

            var pixels = DrawVeil(effect, null, out _, out _);

            Assert.InRange(pixels[(Size / 2) * Size + Size / 2].R, 70, 74);
        }

        [GpuFact]
        public void TheVeil_CoversTheViewportOfTheDevice_AndNothingOutsideIt()
        {
            var effect = new ScreenEffectService();
            effect.SetOverlay(255, 255, 255, 255, SpriteBlendMode.Subtractive);

            var pixels = DrawVeil(effect, new Rectangle(16, 16, 32, 32), out int drawCount, out bool restored);

            Assert.Equal(1, drawCount);
            Assert.True(restored);
            Assert.Equal(0, pixels[17 * Size + 17].R);
            Assert.Equal(0, pixels[46 * Size + 46].R);
            Assert.Equal(200, pixels[4 * Size + 4].R);
            Assert.Equal(200, pixels[60 * Size + 60].R);
            Assert.Equal(200, pixels[15 * Size + 32].R);
            Assert.Equal(200, pixels[49 * Size + 32].R);
        }

        [GpuFact]
        public void AnAdditiveVeil_LightensTheImage()
        {
            var effect = new ScreenEffectService();
            effect.SetOverlay(30, 30, 30, 255, SpriteBlendMode.Additive);

            var pixels = DrawVeil(effect, null, out _, out _);

            Assert.InRange(pixels[(Size / 2) * Size + Size / 2].R, 229, 231);
        }
    }
}
