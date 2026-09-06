using System.Reflection;
using System.Runtime.CompilerServices;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Application.Components;
using CasaEngine.Framework.Rendering.Depth;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace CasaEngine.Tests.Rendering.ScreenEffects;

/// <summary>
/// Covers <see cref="ScreenEffectComponent.TryGetCameraViewSize"/> (D-E9b-12, plan
/// plan-e9b-backdrops-moteur.md, S0 REVISE fix): a pure seam callable with no <c>GraphicsDevice</c> and
/// no <c>_game</c> at all - added because a fresh camera view size in world units (not raw
/// <c>ScreenSizeWidth</c>/<c>Height</c> pixels) is now what <see cref="ScreenEffectComponent.Update"/>
/// passes to <see cref="ScreenEffectComponent.SubmitOverlay"/>, with a pixel-size fallback when no
/// active camera is resolvable. Also covers <see cref="ScreenEffectComponent.SubmitOverlay"/>'s new
/// optional scissor rectangle parameter (D-E9b-6), purely additive alongside the four pre-existing,
/// untouched <c>ScreenEffectComponentSubmissionTests</c>.
/// </summary>
public class ScreenEffectComponentViewSizeTests
{
    [Fact]
    public void TryGetCameraViewSize_WithA1280x944ViewportAndZoomFour_Returns320x236()
    {
        var camera = new Camera2dComponent();
        camera.OnScreenResized(1280, 944);
        camera.Zoom = 4f;

        var result = ScreenEffectComponent.TryGetCameraViewSize(camera, out var width, out var height);

        Assert.True(result);
        Assert.Equal(320, width);
        Assert.Equal(236, height);
    }

    [Fact]
    public void TryGetCameraViewSize_WithNullCamera_ReturnsFalse()
    {
        var result = ScreenEffectComponent.TryGetCameraViewSize(null, out var width, out var height);

        Assert.False(result);
        Assert.Equal(0, width);
        Assert.Equal(0, height);
    }

    [Fact]
    public void TryGetCameraViewSize_WithAnEmptyViewport_ReturnsFalse()
    {
        var camera = new Camera2dComponent();
        // OnScreenResized never called: viewport starts at its default (0,0).

        var result = ScreenEffectComponent.TryGetCameraViewSize(camera, out var width, out var height);

        Assert.False(result);
        Assert.Equal(0, width);
        Assert.Equal(0, height);
    }

    [Fact]
    public void SubmitOverlay_WithAnExplicitScissorRectangle_IsCarriedThroughToTheSubmission()
    {
        var renderer = CreateSpriteRenderer();
        var component = CreateScreenEffectComponent();
        component.Service.SetOverlay(1, 2, 3, SpriteBlendMode.Opaque);

        var scissor = new Rectangle(5, 6, 320, 236);
        component.SubmitOverlay(renderer, Vector3.Zero, 320, 236, CreateTexture(), scissor);

        var entry = GetSpriteDatas(renderer)[0]!;
        Assert.Equal(scissor, (Rectangle)GetField(entry, "ScissorRectangle"));
    }

    [Fact]
    public void SubmitOverlay_WithNoScissorRectangle_DefaultsToTheFullViewport()
    {
        var renderer = CreateSpriteRenderer();
        var component = CreateScreenEffectComponent();
        component.Service.SetOverlay(1, 2, 3, SpriteBlendMode.Opaque);

        component.SubmitOverlay(renderer, Vector3.Zero, 320, 236, CreateTexture());

        var entry = GetSpriteDatas(renderer)[0]!;
        Assert.Equal(new Rectangle(0, 0, 320, 236), (Rectangle)GetField(entry, "ScissorRectangle"));
    }

    private static SpriteRendererComponent CreateSpriteRenderer()
    {
        var game = CreateHeadlessGame();
        return new SpriteRendererComponent(game);
    }

    private static ScreenEffectComponent CreateScreenEffectComponent()
    {
        var game = CreateHeadlessGame();
        return new ScreenEffectComponent(game);
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
