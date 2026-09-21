using CasaEngine.Framework.Application;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Assets.Fonts;
using CasaEngine.Framework.Assets.Loaders;
using FontStashSharp;
using Xunit;

namespace CasaEngine.Tests.Assets;

/// <summary>
/// ADR-0036: BMFont files as <see cref="BitmapFont"/> assets. Building the font itself needs a real
/// <c>Texture2D</c>, hence a graphics device, which no engine test creates: that part is proven by the
/// Alundra port's in-game check. These tests cover everything before it — the parsing, the resolution of a
/// page through the catalog, the failure when a page is missing — and the font's own lifetime, on a
/// fixed-size font rasterized on the CPU from a TTF (the substitute
/// <c>FontStashSharpBitmapFontRegistrationTests</c> documents).
/// </summary>
public class BitmapFontLoaderTests
{
    // The first lines of the Alundra export's UI\font3.fnt.
    private const string Font3Header =
        "info face=\"font3\" size=16 bold=0 italic=0 charset=\"\" unicode=1 stretchH=100 smooth=0 aa=1 padding=0,0,0,0 spacing=0,0 outline=0\r\n"
        + "common lineHeight=16 base=16 scaleW=256 scaleH=256 pages=1 packed=0 alphaChnl=0 redChnl=0 greenChnl=0 blueChnl=0\r\n"
        + "page id=0 file=\"Textures/font3.png\"\r\n"
        + "chars count=1\r\n"
        + "char id=0 x=0 y=0 width=16 height=16 xoffset=0 yoffset=0 xadvance=16 page=0 chnl=15\r\n";

    [Fact]
    public void Parse_ReadsTheFaceAndThePageFile()
    {
        var descriptor = BitmapFontDescriptor.Parse(Font3Header, "font3.fnt");

        Assert.Equal("font3", descriptor.Face);
        Assert.Equal(new[] { "Textures/font3.png" }, descriptor.PageFiles);
    }

    [Fact]
    public void Parse_OrdersPagesByTheirId()
    {
        const string text = "info face=\"two\" size=8\npage id=1 file=\"b.png\"\npage id=0 file=\"a.png\"\n";

        var descriptor = BitmapFontDescriptor.Parse(text, "two.fnt");

        Assert.Equal(new[] { "a.png", "b.png" }, descriptor.PageFiles);
    }

    [Fact]
    public void Parse_WithoutAFace_ThrowsAndNamesTheFile()
    {
        var exception = Assert.Throws<InvalidDataException>(
            () => BitmapFontDescriptor.Parse("page id=0 file=\"a.png\"\n", @"UI\broken.fnt"));

        Assert.Contains(@"UI\broken.fnt", exception.Message);
    }

    [Fact]
    public void Parse_WithoutAPage_Throws()
    {
        Assert.Throws<InvalidDataException>(() => BitmapFontDescriptor.Parse("info face=\"x\" size=8\n", "x.fnt"));
    }

    [Theory]
    [InlineData(@"UI\font3.fnt", "Textures/font3.png", @"UI\Textures\font3.png")]
    [InlineData(@"UI\font3.fnt", "../Shared/page.png", @"Shared\page.png")]
    [InlineData(@"font3.fnt", "font3_0.png", @"font3_0.png")]
    public void CatalogFileNameNextTo_IsRelativeToTheProject_WithBackslashes(string fnt, string page, string expected)
    {
        var root = Path.Combine(Path.GetTempPath(), "casa-project");

        Assert.Equal(expected, BitmapFontDescriptor.CatalogFileNameNextTo(root, Path.Combine(root, fnt), page));
    }

    [Fact]
    public void Acquire_WhenAPageIsNotInTheCatalog_ThrowsAndNamesTheFontAndThePage()
    {
        var root = Path.Combine(Path.GetTempPath(), "casa-bitmapfont-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "UI"));
        try
        {
            File.WriteAllText(Path.Combine(root, "UI", "font3.fnt"), Font3Header);
            var fontId = Guid.NewGuid();
            var fontInfo = new AssetInfo(fontId) { Name = "font3", FileName = @"UI\font3.fnt" };
            string requestedPage = null;

            var manager = new AssetContentManager
            {
                RuntimeContext = new EngineRuntimeContext(
                    null,
                    root,
                    id => id == fontId ? fontInfo : null,
                    fileName =>
                    {
                        requestedPage = fileName;
                        return null;
                    }),
            };
            manager.RegisterAssetLoader(typeof(BitmapFont), new BitmapFontLoader());

            var exception = Assert.Throws<InvalidOperationException>(() => manager.Acquire<BitmapFont>(fontId));

            Assert.Equal(@"UI\Textures\font3.png", requestedPage);
            Assert.Contains("font3.fnt", exception.Message);
            Assert.Contains("Textures/font3.png", exception.Message);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Loader_SupportsFntFilesOnly()
    {
        var loader = new BitmapFontLoader();

        Assert.True(loader.IsFileSupported(@"UI\font3.fnt"));
        Assert.True(loader.IsFileSupported(@"UI\FONT3.FNT"));
        Assert.False(loader.IsFileSupported(@"UI\Textures\font3.png"));
    }

    private sealed class CountingHandle : IDisposable
    {
        public int DisposeCount;

        public void Dispose() => DisposeCount++;
    }

    private static SpriteFontBase CpuFont()
    {
        var fontSystem = new FontSystem();
        fontSystem.AddFont(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Content", "Fonts", "ttf", "arial.ttf")));
        return fontSystem.GetFont(16);
    }

    [Fact]
    public void Dispose_GivesThePageHoldsBack_AndRaisesDisposed_OnlyOnce()
    {
        var page = new CountingHandle();
        var font = new BitmapFont("font3", CpuFont(), new IDisposable[] { page });
        var raised = 0;
        font.Disposed += (_, _) => raised++;

        font.Dispose();
        font.Dispose();

        Assert.True(font.IsDisposed);
        Assert.Equal(1, page.DisposeCount);
        Assert.Equal(1, raised);
    }
}
