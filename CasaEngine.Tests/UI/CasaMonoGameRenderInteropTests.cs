using CasaEngine.Framework.UI.Backend.MonoGame;
using MGUI.Shared.Rendering;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace CasaEngine.Tests.UI;

/// <summary>The blend table of the engine's MGUI backend, which throws on a value it lacks.</summary>
public class CasaMonoGameRenderInteropTests
{
    [Fact]
    public void EveryBlendType_HasAnEntry()
    {
        foreach (var blendType in Enum.GetValues<BlendType>())
        {
            Assert.NotNull(CasaMonoGameRenderInterop.GetBlendState(DrawSettings.Default with { BlendType = blendType }));
        }
    }

    /// <summary>The same formula as a subtractive sprite: BlendFunction.Subtract would compute source - destination.</summary>
    [Fact]
    public void Subtractive_TakesTheSourceFromTheDestination_AndKeepsTheDestinationAlpha()
    {
        var state = CasaMonoGameRenderInterop.GetBlendState(DrawSettings.Default with { BlendType = BlendType.Subtractive });

        Assert.Equal(BlendFunction.ReverseSubtract, state.ColorBlendFunction);
        Assert.Equal(Blend.One, state.ColorSourceBlend);
        Assert.Equal(Blend.One, state.ColorDestinationBlend);
        Assert.Equal(BlendFunction.Add, state.AlphaBlendFunction);
        Assert.Equal(Blend.Zero, state.AlphaSourceBlend);
        Assert.Equal(Blend.One, state.AlphaDestinationBlend);
    }
}
