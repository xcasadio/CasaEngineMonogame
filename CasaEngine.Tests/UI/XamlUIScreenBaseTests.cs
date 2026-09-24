using CasaEngine.Framework.Application;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.UI;
using CasaEngine.Framework.UI.MGUI;
using MGUI.Core.UI;
using MGUI.Core.UI.XAML;
using Xunit;

namespace CasaEngine.Tests.UI;

/// <summary>
/// Pins the base class screens inherit once their tree lives in XAML.
/// <para/>
/// Everything here drives <c>BuildWindow(MGDesktop)</c> and never constructs a <c>UIRoot</c> -- that type is
/// sealed around a MonoGame backend, which is the whole reason the seam takes a desktop.
/// <para/>
/// The asset-manager-backed tests resolve their screen through the global <see cref="AssetCatalog"/>, the
/// same door <see cref="CasaEngine.Framework.UI.Backend.MonoGame.Assets.CasaUIAssetProvider"/> already uses
/// for image sources (ADR-0038), so this class runs in <see cref="ProjectEnvironmentCollection"/> and clears
/// the catalog on both sides, like <c>CasaUIAssetProviderTests</c>.
/// </summary>
[Collection(ProjectEnvironmentCollection.Name)]
public class XamlUIScreenBaseTests : IDisposable
{
    public XamlUIScreenBaseTests() => AssetCatalog.ClearInternal();

    public void Dispose() => AssetCatalog.ClearInternal();


    private const string Markup = """
        <Window xmlns="clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core"
                Left="0" Top="0" Width="320" Height="200">
          <StackPanel Name="pnlRoot" Orientation="Vertical">
            <TextBlock Name="lblTitle" Text="Hello" />
          </StackPanel>
        </Window>
        """;

    [Fact]
    public void BuildWindow_LoadsTheTree_AndHandsItToTheScreen()
    {
        var (desktop, _) = HeadlessUiTestHarness.NewDesktop();
        var screen = new FakeScreen();

        var window = screen.BuildWindow(desktop);

        Assert.NotNull(window);
        Assert.Same(window, screen.LoadedWindow);
        Assert.Equal(1, screen.LoadedCount);
        Assert.Equal("Hello", screen.Title.Text);
    }

    [Fact]
    public void GetWindows_YieldsNothing_UntilTheScreenIsBuilt()
    {
        var screen = new FakeScreen();
        Assert.Empty(screen.GetWindows());

        var (desktop, _) = HeadlessUiTestHarness.NewDesktop();
        var window = screen.BuildWindow(desktop);

        Assert.Same(window, Assert.Single(screen.GetWindows()));
    }

    [Fact]
    public void FindControl_Names_TheMissingControlAndTheDocument()
    {
        var (desktop, _) = HeadlessUiTestHarness.NewDesktop();
        var screen = new FakeScreen();
        screen.BuildWindow(desktop);

        var error = Assert.Throws<InvalidOperationException>(() => screen.Find<MGTextBlock>("lblNotThere"));

        // A screen whose XAML lost a name must say so at once, naming both the control and the document.
        Assert.Contains("lblNotThere", error.Message);
        Assert.Contains("FakeScreen", error.Message);
        Assert.Contains("fake-screen.xaml", error.Message);
    }

    [Fact]
    public void FindControl_Says_WhenTheNameBelongsToAnotherKindOfControl()
    {
        var (desktop, _) = HeadlessUiTestHarness.NewDesktop();
        var screen = new FakeScreen();
        screen.BuildWindow(desktop);

        // TryGetElementByName<T> answers false for this too, so the distinction has to be made here.
        var error = Assert.Throws<InvalidOperationException>(() => screen.Find<MGButton>("lblTitle"));

        Assert.Contains("lblTitle", error.Message);
        Assert.Contains(nameof(MGButton), error.Message);
        Assert.Contains(nameof(MGTextBlock), error.Message);
    }

    [Fact]
    public void FindControl_Refuses_BeforeTheScreenIsBuilt()
    {
        var screen = new FakeScreen();

        var error = Assert.Throws<InvalidOperationException>(() => screen.Find<MGTextBlock>("lblTitle"));

        Assert.Contains("before its XAML was loaded", error.Message);
    }

    [Fact]
    public void AnAssetBackedScreen_LoadsThroughTheAssetDoor()
    {
        var root = Directory.CreateTempSubdirectory("xaml-screen-base-").FullName;

        try
        {
            var xamlPath = Path.Combine(root, "fake.xaml");
            File.WriteAllText(xamlPath, Markup);

            var asset = new UIScreenAsset { SourceXamlFile = "fake.xaml" };
            var assetPath = Path.Combine(root, "fake.uiscreen");
            File.WriteAllText(assetPath, "{}");

            var (desktop, _) = HeadlessUiTestHarness.NewDesktop();
            var screen = new FakeScreen(asset, assetPath);

            var window = screen.BuildWindow(desktop);

            Assert.NotNull(window);
            Assert.Equal("Hello", screen.Title.Text);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void AnAssetBackedScreen_NamesTheEnvelope_WhenAControlIsMissing()
    {
        var root = Directory.CreateTempSubdirectory("xaml-screen-base-").FullName;

        try
        {
            File.WriteAllText(Path.Combine(root, "fake.xaml"), Markup);
            var assetPath = Path.Combine(root, "fake.uiscreen");
            File.WriteAllText(assetPath, "{}");

            var (desktop, _) = HeadlessUiTestHarness.NewDesktop();
            var screen = new FakeScreen(new UIScreenAsset { SourceXamlFile = "fake.xaml" }, assetPath);
            screen.BuildWindow(desktop);

            var error = Assert.Throws<InvalidOperationException>(() => screen.Find<MGTextBlock>("lblNotThere"));

            Assert.Contains(assetPath, error.Message);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void AnAssetManagerBackedScreen_SharesItsEnvelope_AndGivesTheHandleBackOnDispose()
    {
        var root = Directory.CreateTempSubdirectory("xaml-screen-manager-").FullName;

        try
        {
            File.WriteAllText(Path.Combine(root, "fake.xaml"), Markup);

            var assetId = Guid.NewGuid();
            const string relativeFileName = "fake.uiscreen";
            File.WriteAllText(Path.Combine(root, relativeFileName), $$"""
                {
                  "id": "{{assetId}}",
                  "name": "FakeScreen",
                  "source_xaml_file": "fake.xaml"
                }
                """);

            AssetCatalog.AddInternal(new AssetInfo(assetId) { Name = "FakeScreen", FileName = relativeFileName });
            var manager = NewManager(root, out var loader);

            // Resolved by name: point 3 of T3.3 (id or name through AssetCatalog, like image sources).
            var screen = new FakeScreen(manager, "FakeScreen");
            var (desktop, _) = HeadlessUiTestHarness.NewDesktop();
            screen.BuildWindow(desktop);
            Assert.Equal("Hello", screen.Title.Text);

            // A second acquisition of the same id shares the one instance the screen already holds --
            // it must not load a second copy.
            var second = manager.Acquire<UIScreenAsset>(assetId);
            Assert.Equal(1, loader.Loads);

            second.Dispose();
            screen.Dispose();

            // Nobody holds it any more: the next collection frees exactly this one asset.
            Assert.Equal(1, manager.CollectUnreferenced());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void DisposingAnAssetManagerBackedScreen_IsIdempotent()
    {
        var root = Directory.CreateTempSubdirectory("xaml-screen-manager-").FullName;

        try
        {
            File.WriteAllText(Path.Combine(root, "fake.xaml"), Markup);

            var assetId = Guid.NewGuid();
            const string relativeFileName = "fake.uiscreen";
            File.WriteAllText(Path.Combine(root, relativeFileName), $$"""
                {
                  "id": "{{assetId}}",
                  "name": "FakeScreen",
                  "source_xaml_file": "fake.xaml"
                }
                """);

            AssetCatalog.AddInternal(new AssetInfo(assetId) { Name = "FakeScreen", FileName = relativeFileName });
            var manager = NewManager(root, out _);
            var screen = new FakeScreen(manager, assetId.ToString());

            screen.Dispose();
            screen.Dispose();

            Assert.Equal(1, manager.CollectUnreferenced());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void AnAssetManagerBackedScreen_Refuses_AnUnregisteredIdOrName()
    {
        var manager = NewManager(Path.GetTempPath(), out _);

        Assert.Throws<InvalidOperationException>(() => new FakeScreen(manager, "NoSuchScreen"));
        Assert.Throws<InvalidOperationException>(() => new FakeScreen(manager, Guid.NewGuid().ToString()));
    }

    /// <summary>An id is resolved by the asset manager's own runtime context, not by the global catalogue: a game
    /// DLL's test gives its manager a catalogue of its own and builds its screen from it.</summary>
    [Fact]
    public void AnAssetManagerBackedScreen_ResolvesAnId_ThroughItsManager_WithoutTheGlobalCatalogue()
    {
        var root = Directory.CreateTempSubdirectory("xaml-screen-manager-").FullName;

        try
        {
            File.WriteAllText(Path.Combine(root, "fake.xaml"), Markup);

            var assetId = Guid.NewGuid();
            var assetInfo = new AssetInfo(assetId) { Name = "FakeScreen", FileName = "fake.uiscreen" };
            File.WriteAllText(Path.Combine(root, assetInfo.FileName), $$"""
                {
                  "id": "{{assetId}}",
                  "name": "FakeScreen",
                  "source_xaml_file": "fake.xaml"
                }
                """);

            var manager = new AssetContentManager
            {
                RuntimeContext = new EngineRuntimeContext(null, root, id => id == assetId ? assetInfo : null),
            };
            manager.RegisterAssetLoader(typeof(UIScreenAsset), new CountingUIScreenLoader());

            Assert.Null(AssetCatalog.Get(assetId));
            using var screen = new FakeScreen(manager, assetId.ToString());
            var (desktop, _) = HeadlessUiTestHarness.NewDesktop();
            screen.BuildWindow(desktop);

            Assert.Equal("Hello", screen.Title.Text);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>A loader that parses a <see cref="UIScreenAsset"/> the way <c>AssetLoader&lt;T&gt;</c> does,
    /// counting how many times it actually reads a file -- so a test can tell a shared instance from a
    /// reloaded one.</summary>
    private sealed class CountingUIScreenLoader : IAssetLoader
    {
        public int Loads;

        public object LoadAsset(string fileName, AssetContentManager assetContentManager)
        {
            Loads++;
            var asset = new UIScreenAsset();
            asset.Load(Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(fileName)));
            return asset;
        }

        public bool IsFileSupported(string fileName) => false;
    }

    /// <summary>A manager whose <see cref="EngineRuntimeContext.ResolveAssetInfo"/> is left null, so ids fall
    /// through to the global <see cref="AssetCatalog"/> -- the same resolution production code uses.</summary>
    private static AssetContentManager NewManager(string projectRoot, out CountingUIScreenLoader loader)
    {
        var manager = new AssetContentManager
        {
            RuntimeContext = new EngineRuntimeContext(null, projectRoot, null),
        };

        loader = new CountingUIScreenLoader();
        manager.RegisterAssetLoader(typeof(UIScreenAsset), loader);
        return manager;
    }

    [Fact]
    public void AScreen_Refuses_ASourceItCannotName()
    {
        Assert.Throws<ArgumentNullException>(() => new FakeScreen((XamlDocumentSource)null));
        Assert.Throws<ArgumentNullException>(() => new FakeScreen((UIScreenAsset)null, "some.uiscreen"));
        Assert.Throws<ArgumentException>(() => new FakeScreen(new UIScreenAsset(), "  "));
    }

    /// <summary>A screen that does nothing but what the base class asks of it.</summary>
    private sealed class FakeScreen : XamlUIScreenBase
    {
        public MGWindow LoadedWindow { get; private set; }
        public int LoadedCount { get; private set; }
        public MGTextBlock Title { get; private set; }

        public override UILayer Layer => UILayer.HUD;

        public FakeScreen()
            : base(XamlDocumentSource.FromString(Markup, "fake-screen.xaml"))
        {
        }

        public FakeScreen(XamlDocumentSource source)
            : base(source)
        {
        }

        public FakeScreen(UIScreenAsset asset, string assetFilePath)
            : base(asset, assetFilePath)
        {
        }

        public FakeScreen(AssetContentManager assetContentManager, string assetIdOrName)
            : base(assetContentManager, assetIdOrName)
        {
        }

        protected override void OnWindowLoaded(MGWindow window)
        {
            LoadedWindow = window;
            LoadedCount++;
            Title = FindControl<MGTextBlock>("lblTitle");
        }

        /// <summary>Exposes the protected lookup so the tests can drive it on its own.</summary>
        public T Find<T>(string name) where T : MGElement => FindControl<T>(name);
    }
}
