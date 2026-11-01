using CasaEngine.Framework.Application;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Configuration.Project;
using CasaEngine.Framework.Rendering.Depth;
using CasaEngine.Framework.Rendering.ScreenEffects;
using CasaEngine.Framework.UI;
using CasaEngine.Framework.UI.MGUI;
using Microsoft.Xna.Framework;
using Xunit;

namespace CasaEngine.Tests.UI;

/// <summary>
/// The lines of <see cref="GameManager.CreateScreenActionContext"/> that join a screen to the game. The catalogue is
/// static, so this class runs in <see cref="ProjectEnvironmentCollection"/>.
/// </summary>
[Collection(ProjectEnvironmentCollection.Name)]
public sealed class ScreenActionContextWiringTests : IDisposable
{
    private static readonly Guid FontId = Guid.Parse("66666666-aaaa-4aaa-8aaa-666666666666");
    private static readonly Guid SoundId = Guid.Parse("77777777-aaaa-4aaa-8aaa-777777777777");

    private readonly GameManager _gameManager = new(null);
    private readonly ScreenEffectService _effects = new();
    private readonly List<Guid> _heldFonts = new();
    private readonly IDisposable _hold = new StringReader(string.Empty);
    private int _quitCount;

    public ScreenActionContextWiringTests()
    {
        AssetCatalog.ClearInternal();
        AssetCatalog.AddInternal(new AssetInfo(FontId) { Name = "Block", FileName = "block.fnt", AssetType = "fnt" });
        AssetCatalog.AddInternal(new AssetInfo(SoundId) { Name = "Click", FileName = "click.sound", AssetType = "sound" });
    }

    public void Dispose() => AssetCatalog.ClearInternal();

    [Fact]
    public void TheDesignResolution_ComesFromTheProject()
    {
        var settings = new VirtualResolutionSettings { Width = 320, Height = 180, ScaleUI = true };

        Assert.Equal(new Point(320, 180), Context(settings).DesignResolution);
    }

    [Fact]
    public void WithoutScaleUI_ThereIsNoDesignResolution()
    {
        Assert.Null(Context(new VirtualResolutionSettings { Width = 320, Height = 180 }).DesignResolution);
        Assert.Null(Context(null).DesignResolution);
    }

    [Fact]
    public void StartFade_ReachesTheService_WithItsLayer()
    {
        _effects.Layer = ScreenEffectLayer.BelowUI;

        Context().StartFade(Fade());

        Assert.Equal(ScreenEffectLayer.AboveUI, _effects.Layer);
        Assert.True(_effects.IsFading);
    }

    [Fact]
    public void IsFadeRunning_FollowsTheService()
    {
        var context = Context();
        Assert.False(context.IsFadeRunning());

        _effects.StartFade(0, 0, 0, 0, 255, 255, 255, 255, 1f, SpriteBlendMode.AlphaBlend);

        Assert.True(context.IsFadeRunning());
    }

    [Fact]
    public void TheService_IsAskedForWhenItIsUsed_NotWhenTheContextIsBuilt()
    {
        int reads = 0;
        var context = _gameManager.CreateScreenActionContext(
            new FirstScreenTests.StubUIViewRuntime(),
            new ScreenGameServices(
                () =>
                {
                    reads++;
                    return _effects;
                },
                null,
                HoldFont,
                false,
                Quit));
        Assert.Equal(0, reads);

        context.IsFadeRunning();

        Assert.Equal(1, reads);
    }

    [Theory]
    [InlineData("Block")]
    [InlineData("66666666-aaaa-4aaa-8aaa-666666666666")]
    public void AcquireFont_HoldsTheFontTheNameOrIdDesignates(string assetIdOrName)
    {
        var hold = Context().AcquireFont(assetIdOrName);

        Assert.Same(_hold, hold);
        Assert.Equal(new[] { FontId }, _heldFonts);
    }

    [Fact]
    public void AcquireFont_RefusesWhatIsNotAFont_AndHoldsNothing()
    {
        var context = Context();

        Assert.Throws<InvalidOperationException>(() => context.AcquireFont("Click"));

        Assert.Empty(_heldFonts);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void HasUIInputMapping_ComesFromTheGame(bool hasMapping)
    {
        Assert.Equal(hasMapping, Context(null, hasMapping).HasUIInputMapping);
    }

    [Fact]
    public void Quit_ReachesTheGame()
    {
        Context().Quit();

        Assert.Equal(1, _quitCount);
    }

    private UIScreenActionContext Context(VirtualResolutionSettings? virtualResolution = null, bool hasUIInputMapping = false)
        => _gameManager.CreateScreenActionContext(
            new FirstScreenTests.StubUIViewRuntime(),
            new ScreenGameServices(() => _effects, virtualResolution!, HoldFont, hasUIInputMapping, Quit));

    private IDisposable HoldFont(Guid fontId)
    {
        _heldFonts.Add(fontId);
        return _hold;
    }

    private void Quit() => _quitCount++;

    private static UIScreenActionData Fade() => new()
    {
        Type = UIScreenActionType.FadeScreen,
        Layer = ScreenEffectLayer.AboveUI,
        BlendMode = SpriteBlendMode.Subtractive,
        From = new Color(0, 0, 0),
        To = new Color(255, 255, 255),
        DurationSeconds = 1f,
    };
}
