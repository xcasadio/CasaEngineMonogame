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
/// Covers the anti-allocation contract and exact PSX fusion formula of
/// <see cref="SpriteRendererComponent"/>'s private <c>GetBlendState</c> for the two new
/// <see cref="SpriteBlendMode.Additive"/>/<see cref="SpriteBlendMode.Subtractive"/> states (§1.3 of
/// the plan): both resolve to a cached instance (never allocated per call/run), with
/// <c>ColorBlendFunction</c>/factors matching the PSX GPU formulas exactly - in particular
/// <c>ReverseSubtract</c>, not <c>Subtract</c>, for the subtractive channel (Subtract would compute
/// src - dst instead of dst - src).
/// </summary>
public class ScreenEffectBlendStateTests
{
    [Fact]
    public void GetBlendState_ForAdditive_ReturnsTheSameCachedInstanceEveryCall()
    {
        var component = CreateComponent();

        var first = InvokeGetBlendState(component, SpriteBlendMode.Additive);
        var second = InvokeGetBlendState(component, SpriteBlendMode.Additive);

        Assert.Same(first, second);
    }

    [Fact]
    public void GetBlendState_ForSubtractive_ReturnsTheSameCachedInstanceEveryCall()
    {
        var component = CreateComponent();

        var first = InvokeGetBlendState(component, SpriteBlendMode.Subtractive);
        var second = InvokeGetBlendState(component, SpriteBlendMode.Subtractive);

        Assert.Same(first, second);
    }

    [Fact]
    public void GetBlendState_ForAdditive_ReturnsTheAddOneOnePsxFormula()
    {
        var component = CreateComponent();

        var blendState = InvokeGetBlendState(component, SpriteBlendMode.Additive);

        Assert.Equal(BlendFunction.Add, blendState.ColorBlendFunction);
        Assert.Equal(Blend.One, blendState.ColorSourceBlend);
        Assert.Equal(Blend.One, blendState.ColorDestinationBlend);
        Assert.Equal(BlendFunction.Add, blendState.AlphaBlendFunction);
        Assert.Equal(Blend.Zero, blendState.AlphaSourceBlend);
        Assert.Equal(Blend.One, blendState.AlphaDestinationBlend);
    }

    [Fact]
    public void GetBlendState_ForSubtractive_ReturnsTheReverseSubtractOneOnePsxFormula()
    {
        var component = CreateComponent();

        var blendState = InvokeGetBlendState(component, SpriteBlendMode.Subtractive);

        // The mandatory mutation: flipping this to BlendFunction.Subtract would compute
        // src - dst instead of the PSX GPU's dst - src, and must fail this assertion.
        Assert.Equal(BlendFunction.ReverseSubtract, blendState.ColorBlendFunction);
        Assert.Equal(Blend.One, blendState.ColorSourceBlend);
        Assert.Equal(Blend.One, blendState.ColorDestinationBlend);
        Assert.Equal(BlendFunction.Add, blendState.AlphaBlendFunction);
        Assert.Equal(Blend.Zero, blendState.AlphaSourceBlend);
        Assert.Equal(Blend.One, blendState.AlphaDestinationBlend);
    }

    private static SpriteRendererComponent CreateComponent()
    {
        var game = (CasaEngineGame)RuntimeHelpers.GetUninitializedObject(typeof(CasaEngineGame));
        var componentsField = typeof(Game).GetField("_components", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(componentsField);
        componentsField!.SetValue(game, new GameComponentCollection());

        return new SpriteRendererComponent(game);
    }

    private static BlendState InvokeGetBlendState(SpriteRendererComponent component, SpriteBlendMode blendMode)
    {
        var method = typeof(SpriteRendererComponent).GetMethod("GetBlendState", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return (BlendState)method!.Invoke(component, new object[] { blendMode })!;
    }
}
