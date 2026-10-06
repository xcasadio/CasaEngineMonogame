using CasaEngine.Editor.Controls;
using CasaEngine.Engine.Environment;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Audio;
using CasaEngine.Tests.ContentBrowser;
using MGUI.Core.UI;
using Xunit;

namespace CasaEngine.Tests.Editor;

/// <summary>
/// The name an <see cref="AssetSelector"/> shows for its asset id, built on the headless MGUI desktop. An id the
/// <see cref="AssetCatalog"/> does not know, such as a <c>.sound</c> whose audio file is not catalogued, shows an
/// orange "Unknown" with the first group of the id instead of throwing. The asset catalogue and
/// <see cref="EngineEnvironment.ProjectPath"/> are global state, hence the serialized collection and the restore in
/// <see cref="Dispose"/>.
/// </summary>
[Collection(ProjectEnvironmentCollection.Name)]
public sealed class AssetSelectorTests : IDisposable
{
    private static readonly Guid KnownId = Guid.Parse("d1000000-0000-0000-0000-00000000000a");
    private static readonly Guid UnknownId = Guid.Parse("9f3c2a7e-5b14-4d0e-8a61-2c7b9e4f0d35");

    private const string UnknownText = "[c=Orange]Unknown (9f3c2a7e…)[/c]";

    private readonly string _previousProjectPath;

    public AssetSelectorTests()
    {
        _previousProjectPath = EngineEnvironment.ProjectPath;

        AssetCatalog.ClearInternal();
        AssetCatalog.AddInternal(new AssetInfo(KnownId) { Name = "step_a", FileName = "Sounds/step_a.wav" });
    }

    public void Dispose()
    {
        EngineEnvironment.ProjectPath = _previousProjectPath;
        AssetCatalog.ClearInternal();
    }

    // The name block comes before the Browse button in the selector's tree.
    private static string DisplayedName(AssetSelector selector)
        => selector.TraverseVisualTree().OfType<MGTextBlock>().First().Text;

    [Fact]
    public void AnUncataloguedId_ShowsUnknownWithTheFirstGroupOfTheId()
    {
        var harness = ContentBrowserViewTestHarness.Create();
        var selector = new AssetSelector(harness.Window);
        harness.Window.SetContent(selector);

        var exception = Record.Exception(() =>
        {
            selector.AssetId = UnknownId;
            harness.AdvanceFrame(0);
        });

        Assert.Null(exception);
        Assert.Equal(UnknownId, selector.AssetId);
        Assert.Equal(UnknownText, DisplayedName(selector));
    }

    [Fact]
    public void ACataloguedId_ShowsTheAssetName_AndAnEmptyIdShowsNone()
    {
        var harness = ContentBrowserViewTestHarness.Create();
        var selector = new AssetSelector(harness.Window);

        Assert.Equal("[i]None[/i]", DisplayedName(selector));

        selector.AssetId = KnownId;
        Assert.Equal("step_a", DisplayedName(selector));

        selector.AssetId = UnknownId;
        Assert.Equal(UnknownText, DisplayedName(selector));

        selector.AssetId = Guid.Empty;
        Assert.Equal("[i]None[/i]", DisplayedName(selector));
    }

    [Fact]
    public void TheSoundInspector_OpensASoundWhoseAudioFileIsNotCatalogued()
    {
        EngineEnvironment.ProjectPath = Path.Combine(Path.GetTempPath(), "casa-asset-selector");
        var harness = ContentBrowserViewTestHarness.Create(900, 900);
        var panel = new SoundAssetInspectorPanel(harness.Window);
        var asset = new SoundAsset { Name = "step", AudioFileAssetId = UnknownId };

        MGElement content = null;
        var exception = Record.Exception(() =>
        {
            panel.LoadAsset(asset, Path.Combine(EngineEnvironment.ProjectPath, "step.sound"));
            content = panel.CreateContent();
            harness.Window.SetContent(content);
            harness.AdvanceFrame(0);
        });

        Assert.Null(exception);
        var selector = content.TraverseVisualTree().OfType<AssetSelector>().First();
        Assert.Equal(UnknownId, selector.AssetId);
        Assert.Equal(UnknownText, DisplayedName(selector));
        Assert.False(panel.IsDirty);
    }
}
