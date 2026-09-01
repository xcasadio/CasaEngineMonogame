using System.Reflection;
using System.Runtime.CompilerServices;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Application.Components;
using CasaEngine.Framework.Rendering.Depth;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace CasaEngine.Tests.Rendering.ScreenEffects;

/// <summary>
/// Covers <see cref="ScreenEffectComponent.SubmitOverlay"/>: the placement formula (camera-cancel,
/// full-viewport scale, Y flip), the render pass/sort key, the blend mode and the colour it submits
/// to a real (headless-constructed) <see cref="SpriteRendererComponent"/> - the only proof, short of
/// running the game, that the whole slice's drawing is wired correctly (plan §4, item 4/8's "blocage
/// de relecture"). Uses the same headless-construction technique as
/// <c>SpriteRendererComponentBlendModeTests</c>: the component's queuing methods never touch
/// <see cref="GraphicsDevice"/>, only <c>Flush</c>/<c>Draw</c> does.
/// </summary>
public class ScreenEffectComponentSubmissionTests
{
    private static readonly Rectangle SourceRectangle = new(0, 0, 16, 16);

    [Fact]
    public void SubmitOverlay_WhenServiceIsActive_QueuesOneSpriteWithTheCameraCancelledPosition()
    {
        var renderer = CreateSpriteRenderer();
        var component = CreateScreenEffectComponent();
        component.Service.SetOverlay(10, 20, 30, SpriteBlendMode.Additive);

        var cameraPosition = new Vector3(100f, 50f, 0f);
        var texture = CreateTexture();

        component.SubmitOverlay(renderer, cameraPosition, viewportWidth: 320, viewportHeight: 240, overlayTexture: texture);

        var spriteDatas = GetSpriteDatas(renderer);
        Assert.Single(spriteDatas);

        var entry = spriteDatas[0]!;

        // Mirrors BackdropRenderer.Draw's tint block exactly: worldPosition = (cameraPosition.X -
        // halfWidth, cameraPosition.Y + halfHeight), origin (0,0). DrawSprite then re-adds half the
        // (1x1) source size scaled by (viewportWidth, viewportHeight) to centre the quad on that
        // position - which, for a full-viewport quad with origin (0,0), lands the quad's centre
        // exactly on cameraPosition: the two half-viewport terms cancel out algebraically. A quad
        // that forgot the camera-cancel term entirely (used raw `position` with no
        // `- cameraPosition`) would NOT reduce to cameraPosition here, so this pins the mandatory
        // mutation (b) from the plan.
        var worldMatrix = (Matrix)GetField(entry, "WorldMatrix");
        Assert.Equal(cameraPosition.X, worldMatrix.Translation.X, 3);
        Assert.Equal(cameraPosition.Y, worldMatrix.Translation.Y, 3);

        // Scale: the quad is stretched to the full viewport (scale.X * sourceInTexture.Width with a
        // 1x1 source == the raw scale vector). Rotation is 0 here, so the transform's diagonal reads
        // back the scale directly, with no need for a full decompose.
        Assert.Equal(320f, worldMatrix.M11, 2);
        Assert.Equal(240f, worldMatrix.M22, 2);

        var sortKey = (RenderSortKey2D)GetField(entry, "SortKey");
        Assert.Equal((int)RenderPass2D.ScreenEffects, sortKey.RenderPass);

        var blendMode = (SpriteBlendMode)GetField(entry, "BlendMode");
        Assert.Equal(SpriteBlendMode.Additive, blendMode);

        var color = (Color)GetField(entry, "Color");
        Assert.Equal(new Color(10, 20, 30), color);
    }

    [Fact]
    public void SubmitOverlay_WhenServiceIsInactive_QueuesNothing()
    {
        var renderer = CreateSpriteRenderer();
        var component = CreateScreenEffectComponent();
        // Service starts inactive (Clear is the default state).

        component.SubmitOverlay(renderer, Vector3.Zero, 320, 240, CreateTexture());

        Assert.Empty(GetSpriteDatas(renderer));
    }

    [Fact]
    public void SubmitOverlay_WithNoOverlayTextureAndNoGraphicsDevice_QueuesNothingAndDoesNotThrow()
    {
        var renderer = CreateSpriteRenderer();
        var component = CreateScreenEffectComponent();
        component.Service.SetOverlay(1, 2, 3, SpriteBlendMode.Subtractive);

        // No explicit texture, and the component's own game has no GraphicsDevice: the lazy pixel
        // cannot be created, so this must bypass cleanly rather than throw.
        var exception = Record.Exception(() => component.SubmitOverlay(renderer, Vector3.Zero, 320, 240));

        Assert.Null(exception);
        Assert.Empty(GetSpriteDatas(renderer));
    }

    [Fact]
    public void SubmitOverlay_WithZeroViewport_QueuesNothing()
    {
        var renderer = CreateSpriteRenderer();
        var component = CreateScreenEffectComponent();
        component.Service.SetOverlay(1, 2, 3, SpriteBlendMode.Additive);

        component.SubmitOverlay(renderer, Vector3.Zero, 0, 240, CreateTexture());

        Assert.Empty(GetSpriteDatas(renderer));
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
