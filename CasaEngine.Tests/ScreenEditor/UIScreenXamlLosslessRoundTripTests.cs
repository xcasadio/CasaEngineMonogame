using System.Text;
using CasaEngine.EditorServices.ScreenEditor.DocumentModel;
using CasaEngine.EditorServices.ScreenEditor.Session;
using CasaEngine.EditorServices.ScreenEditor.Xaml;
using CasaEngine.Framework.UI.MGUI;
using CasaEngine.Tests.UI;
using MGUI.Core.UI.XAML;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.ScreenEditor;

/// <summary>
/// T4.1 (engine ADR-0038 "Lossless editor round trip"): opening and saving a screen loses nothing.
/// <para/>
/// Two guarantees are tested here, through <see cref="UIScreenEditorSession"/> exactly as the editor uses
/// it (open a real file, mutate the document, save, re-read the file):
/// <list type="bullet">
/// <item>an unmodified open+save writes the original file back byte for byte;</item>
/// <item>a modified save keeps every comment, namespace declaration, prefix, attribute order,
/// owner-qualified property element and markup-extension value the source carries, in a document that
/// still loads through the real MGUI <see cref="XAMLParser"/>.</item>
/// </list>
/// </summary>
[Collection(MguiDataBindingCollection.Name)]
public class UIScreenXamlLosslessRoundTripTests
{
    // ─────────────────────────────────────────────────────────────────────
    //  Guarantee 1: an unmodified open+save is byte-identical
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public void UnmodifiedSave_OfAlundraInventoryScreen_IsByteIdentical()
    {
        AssertUnmodifiedSaveIsByteIdentical(File.ReadAllBytes(InventoryScreenFixturePath()));
    }

    [Fact]
    public void UnmodifiedSave_WithLineFeedOnlyLineEndings_IsByteIdentical()
    {
        const string xaml = "<Window xmlns=\"clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core\">\n  <TextBlock Text=\"Hello\" />\n</Window>\n";
        AssertUnmodifiedSaveIsByteIdentical(new UTF8Encoding(false).GetBytes(xaml));
    }

    [Fact]
    public void UnmodifiedSave_WithSingleQuotedAttributeValues_IsByteIdentical()
    {
        const string xaml = "<Window xmlns='clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core' Name='MainWindow'>\r\n  <TextBlock Text='Hello' />\r\n</Window>\r\n";
        AssertUnmodifiedSaveIsByteIdentical(new UTF8Encoding(false).GetBytes(xaml));
    }

    [Fact]
    public void UnmodifiedSave_WithByteOrderMark_IsByteIdentical()
    {
        const string xaml = "<Window xmlns=\"clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core\">\r\n  <TextBlock Text=\"Hello\" />\r\n</Window>\r\n";
        var bytes = WithByteOrderMark(xaml);
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes[..3]);
        AssertUnmodifiedSaveIsByteIdentical(bytes);
    }

    [Fact]
    public void UnmodifiedSave_WithDataBindingNamespaceAndAttachedProperty_IsByteIdentical()
    {
        const string xaml = """
<Window xmlns="clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:dataBinding="clr-namespace:MGUI.Core.UI.DataBinding;assembly=MGUI.Core">
  <!-- Canvas hosts an absolutely-positioned child. -->
  <Canvas Name="RootCanvas">
    <TextBlock Name="TitleText" Text="{dataBinding:MGBinding Path=Title}" Canvas.Left="4" Canvas.Top="8" />
  </Canvas>
</Window>
""";
        AssertUnmodifiedSaveIsByteIdentical(new UTF8Encoding(false).GetBytes(xaml.Replace("\n", "\r\n")));
    }

    private static void AssertUnmodifiedSaveIsByteIdentical(byte[] originalBytes)
    {
        using var fixture = ScreenFixture.Create(originalBytes);

        var session = new UIScreenEditorSession();
        session.Open(fixture.Asset, fixture.AssetPath);

        Assert.NotNull(session.Document);

        session.Save();

        var savedBytes = File.ReadAllBytes(fixture.XamlPath);
        Assert.Equal(originalBytes, savedBytes);
    }

    // ─────────────────────────────────────────────────────────────────────
    //  Guarantee 2: a modified save keeps everything the model does not itself represent
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public void ModifiedSave_ChangingOneAttribute_KeepsCommentsNamespacesPrefixesOrderAndPropertyElements()
    {
        const string xaml = """
<?xml version="1.0" encoding="utf-8"?>
<Window xmlns="clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:dataBinding="clr-namespace:MGUI.Core.UI.DataBinding;assembly=MGUI.Core"
        TitleText="InitialTitle"
        Width="800">
  <!-- Leading comment before the panel. -->
  <StackPanel Name="LayoutRoot">
    <!-- Leading comment before the label. -->
    <TextBlock Name="Label" Text="{dataBinding:MGBinding Path=Title}" />
  </StackPanel>
</Window>
""";
        using var fixture = ScreenFixture.Create(new UTF8Encoding(false).GetBytes(xaml.Replace("\n", "\r\n")));

        var session = new UIScreenEditorSession();
        session.Open(fixture.Asset, fixture.AssetPath);
        Assert.NotNull(session.Document);

        session.Document!.Root!.SetProperty("TitleText", "UpdatedTitle");
        session.Document!.Root!.SetProperty("Height", "600"); // newly added attribute
        session.MarkDirty();
        session.Save();

        var savedText = File.ReadAllText(fixture.XamlPath);

        Assert.Contains("<!-- Leading comment before the panel. -->", savedText);
        Assert.Contains("<!-- Leading comment before the label. -->", savedText);
        Assert.Contains("xmlns:dataBinding=\"clr-namespace:MGUI.Core.UI.DataBinding;assembly=MGUI.Core\"", savedText);
        Assert.Contains("{dataBinding:MGBinding Path=Title}", savedText);
        Assert.Contains("UpdatedTitle", savedText);
        Assert.DoesNotContain("InitialTitle", savedText);
        // The newly added attribute goes after the ones already there.
        Assert.True(savedText.IndexOf("Width=\"800\"", StringComparison.Ordinal)
                     < savedText.IndexOf("Height=\"600\"", StringComparison.Ordinal));
        // Source line endings (CRLF) are kept.
        var bodyAfterDeclaration = savedText[(savedText.IndexOf("?>", StringComparison.Ordinal) + 2)..];
        Assert.Contains("\r\n", bodyAfterDeclaration);

        AssertLoadsInRealMGUIWithWorkingBinding(savedText);
    }

    [Fact]
    public void ModifiedSave_AddingAnAttribute_AppendsItAfterTheExistingOnes()
    {
        const string xaml = "<Window xmlns=\"clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core\" Width=\"800\" Height=\"600\">\r\n  <TextBlock Text=\"Hello\" />\r\n</Window>\r\n";
        using var fixture = ScreenFixture.Create(new UTF8Encoding(false).GetBytes(xaml));

        var session = new UIScreenEditorSession();
        session.Open(fixture.Asset, fixture.AssetPath);

        session.Document!.Root!.SetProperty("TitleText", "New Title");
        session.MarkDirty();
        session.Save();

        var savedText = File.ReadAllText(fixture.XamlPath);
        var rootLine = savedText.Split('\n')[0];
        Assert.True(rootLine.IndexOf("Height", StringComparison.Ordinal) < rootLine.IndexOf("TitleText", StringComparison.Ordinal));
    }

    [Fact]
    public void ModifiedSave_DeletingANode_RemovesItsLeadingComments()
    {
        const string xaml = """
<Window xmlns="clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core">
  <StackPanel Name="LayoutRoot">
    <!-- This button is about to be removed. -->
    <Button Name="RemovedButton" Text="Bye" />
    <TextBlock Name="Survivor" Text="Still here" />
  </StackPanel>
</Window>
""";
        using var fixture = ScreenFixture.Create(new UTF8Encoding(false).GetBytes(xaml));

        var session = new UIScreenEditorSession();
        session.Open(fixture.Asset, fixture.AssetPath);

        var layoutRoot = session.Document!.Root!.Children[0];
        var removedButton = layoutRoot.Children.Single(c => c.Name == "RemovedButton");
        layoutRoot.RemoveChild(removedButton);
        session.MarkDirty();
        session.Save();

        var savedText = File.ReadAllText(fixture.XamlPath);
        Assert.DoesNotContain("This button is about to be removed", savedText);
        Assert.DoesNotContain("RemovedButton", savedText);
        Assert.Contains("Survivor", savedText);
    }

    [Fact]
    public void ModifiedSave_ChangingAttribute_KeepsOtherSiblingCommentsAndPropertyElementUntouched()
    {
        const string xaml = """
<Window xmlns="clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core">
  <Window.TitleBar>
    <DockPanel>
      <TextBlock Text="Dialog" />
    </DockPanel>
  </Window.TitleBar>
  <!-- kept as is -->
  <StackPanel Name="Body">
    <TextBlock Name="Status" Text="Ready" />
  </StackPanel>
</Window>
""";
        using var fixture = ScreenFixture.Create(new UTF8Encoding(false).GetBytes(xaml));

        var session = new UIScreenEditorSession();
        session.Open(fixture.Asset, fixture.AssetPath);

        var status = session.Document!.Root!.Children[0].Children.Single(c => c.Name == "Status");
        status.SetProperty("Text", "Changed");
        session.MarkDirty();
        session.Save();

        var savedText = File.ReadAllText(fixture.XamlPath);
        Assert.Contains("<!-- kept as is -->", savedText);
        Assert.Contains("Window.TitleBar", savedText);
        Assert.Contains("Dialog", savedText);
        Assert.Contains("Changed", savedText);
        Assert.DoesNotContain("Ready", savedText);
    }

    [Fact]
    public void ModifiedSave_OfAlundraInventoryScreen_RewritesOnlyTheEditedStartTag()
    {
        var originalBytes = File.ReadAllBytes(InventoryScreenFixturePath());
        using var fixture = ScreenFixture.Create(originalBytes);

        var session = new UIScreenEditorSession();
        session.Open(fixture.Asset, fixture.AssetPath);

        var edited = EnumerateNodes(session.Document!.Root!).Last(n => n.ControlType == "TextBlock");
        edited.SetProperty("Opacity", "0.5");
        session.MarkDirty();
        session.Save();

        // Everything but the edited element's start tag -- the root's attributes on eight lines included --
        // is written back line for line.
        var originalLines = Encoding.UTF8.GetString(originalBytes).Split('\n');
        var savedLines = File.ReadAllText(fixture.XamlPath).Split('\n');

        var prefix = 0;
        while (prefix < originalLines.Length && prefix < savedLines.Length && originalLines[prefix] == savedLines[prefix])
        {
            prefix++;
        }

        var suffix = 0;
        while (suffix < originalLines.Length - prefix && suffix < savedLines.Length - prefix
               && originalLines[^(suffix + 1)] == savedLines[^(suffix + 1)])
        {
            suffix++;
        }

        var originalRegion = string.Join("\n", originalLines[prefix..(originalLines.Length - suffix)]);
        var savedRegion = Assert.Single(savedLines[prefix..(savedLines.Length - suffix)]);

        Assert.Equal(1, originalRegion.Count(c => c == '<'));
        Assert.Contains($"Name=\"{edited.Name}\"", originalRegion);
        Assert.Contains($"Name=\"{edited.Name}\"", savedRegion);
        Assert.Contains("Opacity=\"0.5\"", savedRegion);
    }

    [Fact]
    public void ModifiedSave_KeepsTheQuotesAndCharacterReferencesOfUntouchedStartTags()
    {
        const string xaml =
            "<?xml version='1.0' encoding='utf-8'?>\r\n" +
            "<Window xmlns='clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core'\r\n" +
            "        Name='Root'>\r\n" +
            "  <StackPanel>\r\n" +
            "    <TextBlock Name='Untouched' Text='a&#x0a;b' />\r\n" +
            "    <TextBlock Name='Edited' Text='x' />\r\n" +
            "  </StackPanel>\r\n" +
            "</Window>\r\n";
        using var fixture = ScreenFixture.Create(new UTF8Encoding(false).GetBytes(xaml));

        var session = new UIScreenEditorSession();
        session.Open(fixture.Asset, fixture.AssetPath);

        var edited = session.Document!.Root!.Children[0].Children.Single(c => c.Name == "Edited");
        edited.SetProperty("Text", "y");
        session.MarkDirty();
        session.Save();

        var expected = xaml.Replace("<TextBlock Name='Edited' Text='x' />", "<TextBlock Name=\"Edited\" Text=\"y\" />");
        Assert.Equal(expected, File.ReadAllText(fixture.XamlPath));
    }

    // ─────────────────────────────────────────────────────────────────────
    //  Structural edits touch only the nodes they change (phase 4 closing review)
    // ─────────────────────────────────────────────────────────────────────

    private const string PanelXaml =
        "<Window xmlns='clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core'>\r\n" +
        "  <StackPanel Name='Panel'>\r\n" +
        "    <!-- first -->\r\n" +
        "    <TextBlock Name='A'\r\n" +
        "               Text='a' />\r\n" +
        "    <TextBlock Name='B' Text='b' />\r\n" +
        "    <!-- third -->\r\n" +
        "    <TextBlock Name='C' Text='c' />\r\n" +
        "    <Button Name='D'>x =&gt; y &#x41;</Button>\r\n" +
        "  </StackPanel>\r\n" +
        "</Window>\r\n";

    [Fact]
    public void ModifiedSave_DeletingAChild_RemovesOnlyItsLines()
    {
        var saved = SaveAfterEdit(PanelXaml, panel => panel.RemoveChild(Child(panel, "B")));

        Assert.Equal(PanelXaml.Replace("    <TextBlock Name='B' Text='b' />\r\n", string.Empty), saved);
    }

    [Fact]
    public void ModifiedSave_DeletingAChild_RemovesItsLeadingCommentLinesToo()
    {
        var saved = SaveAfterEdit(PanelXaml, panel => panel.RemoveChild(Child(panel, "C")));

        Assert.Equal(PanelXaml.Replace("    <!-- third -->\r\n    <TextBlock Name='C' Text='c' />\r\n", string.Empty), saved);
    }

    [Fact]
    public void ModifiedSave_ReorderingAChild_MovesItsLinesWithItsComments()
    {
        var saved = SaveAfterEdit(PanelXaml, panel => panel.MoveChild(Child(panel, "C"), 0));

        var expected =
            "<Window xmlns='clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core'>\r\n" +
            "  <StackPanel Name='Panel'>\r\n" +
            "    <!-- third -->\r\n" +
            "    <TextBlock Name='C' Text='c' />\r\n" +
            "    <!-- first -->\r\n" +
            "    <TextBlock Name='A'\r\n" +
            "               Text='a' />\r\n" +
            "    <TextBlock Name='B' Text='b' />\r\n" +
            "    <Button Name='D'>x =&gt; y &#x41;</Button>\r\n" +
            "  </StackPanel>\r\n" +
            "</Window>\r\n";
        Assert.Equal(expected, saved);
    }

    [Theory]
    [InlineData(4, "    <Button Name='D'>x =&gt; y &#x41;</Button>\r\n")]
    [InlineData(1, "    <TextBlock Name='A'\r\n               Text='a' />\r\n")]
    public void ModifiedSave_AddingAChild_InsertsOneLineAfterItsPreviousSibling(int index, string previousSiblingLines)
    {
        var saved = SaveAfterEdit(PanelXaml, panel =>
        {
            var added = new UIScreenNode("TextBlock") { Name = "E" };
            added.SetProperty("Text", "e");
            panel.AddChild(added);
            panel.MoveChild(added, index);
        });

        var expected = PanelXaml.Replace(
            previousSiblingLines,
            previousSiblingLines + "    <TextBlock Name=\"E\" Text=\"e\" />\r\n");
        Assert.Equal(expected, saved);
    }

    [Fact]
    public void ModifiedSave_KeepsTheByteOrderMark()
    {
        using var fixture = ScreenFixture.Create(WithByteOrderMark(PanelXaml));

        var session = new UIScreenEditorSession();
        session.Open(fixture.Asset, fixture.AssetPath);
        Child(session.Document!.Root!.Children[0], "B").SetProperty("Text", "changed");
        session.MarkDirty();
        session.Save();

        var savedBytes = File.ReadAllBytes(fixture.XamlPath);
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, savedBytes[..3]);
        Assert.Equal(
            PanelXaml.Replace("Name='B' Text='b'", "Name=\"B\" Text=\"changed\""),
            new UTF8Encoding(false).GetString(savedBytes[3..]));
    }

    private static string SaveAfterEdit(string xaml, Action<UIScreenNode> editPanel)
    {
        using var fixture = ScreenFixture.Create(new UTF8Encoding(false).GetBytes(xaml));

        var session = new UIScreenEditorSession();
        session.Open(fixture.Asset, fixture.AssetPath);
        editPanel(session.Document!.Root!.Children[0]);
        session.MarkDirty();
        session.Save();

        Assert.Null(session.LastErrorMessage);
        return File.ReadAllText(fixture.XamlPath);
    }

    private static UIScreenNode Child(UIScreenNode parent, string name)
        => parent.Children.Single(child => child.Name == name);

    /// <summary>UTF-8 bytes of <paramref name="text"/> behind a byte-order mark. <see cref="Encoding.GetBytes(string)"/>
    /// never writes the preamble, even for an encoding constructed to emit one.</summary>
    private static byte[] WithByteOrderMark(string text)
        => new byte[] { 0xEF, 0xBB, 0xBF }.Concat(new UTF8Encoding(false).GetBytes(text)).ToArray();

    [Fact]
    public void NewDocumentWithNoSourceText_StillSerializesWithDefaultNamespaces()
    {
        var document = new UIScreenDocument();
        document.SetRoot(new UIScreenNode("Window") { Name = "FreshWindow" });

        var serializer = new UIScreenXamlSerializer();
        var xaml = serializer.Serialize(document);

        Assert.Contains("xmlns=\"clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core\"", xaml);
        Assert.Contains("xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\"", xaml);

        // Pre-existing v1 behaviour, unrelated to T4.1: XDocument.ToString() never emits the XML
        // declaration even though one is set on the XDocument, so a document with no source XAML text
        // never had one in its serialized output either. This documents that, rather than changing it.
        Assert.DoesNotContain("<?xml", xaml);
    }

    // ─────────────────────────────────────────────────────────────────────
    //  Helpers
    // ─────────────────────────────────────────────────────────────────────

    private static void AssertLoadsInRealMGUIWithWorkingBinding(string xaml)
    {
        var (desktop, _) = HeadlessUiTestHarness.NewDesktop();
        var window = XAMLParser.LoadRootWindow(desktop, xaml, SanitizeXAMLString: false, ReplaceLinebreakLiterals: true);

        Assert.NotNull(window);
        Assert.True(window.TryGetElementByName("Label", out var labelElement));

        window.WindowDataContext = new TitleViewModel { Title = "Bound value" };

        var textBlock = Assert.IsAssignableFrom<MGUI.Core.UI.MGTextBlock>(labelElement);
        Assert.Equal("Bound value", textBlock.Text);
    }

    private sealed class TitleViewModel
    {
        public string Title { get; set; } = string.Empty;
    }

    /// <summary>A copy of Alundra's inventory screen (the parent repository's <c>Alundra/Screens/InventoryScreen.xaml</c>
    /// on 2026-09-24): a real screen with its root attributes on several lines, and comments.</summary>
    private static string InventoryScreenFixturePath()
        => FindRepoFile("CasaEngine.Tests", "ScreenEditor", "Fixtures", "InventoryScreen.xaml");

    private static IEnumerable<UIScreenNode> EnumerateNodes(UIScreenNode node)
    {
        yield return node;
        foreach (var child in node.Children)
        {
            foreach (var descendant in EnumerateNodes(child))
            {
                yield return descendant;
            }
        }
    }

    private static string FindRepoFile(params string[] relativeSegments)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            var candidate = Path.Combine(current.FullName, Path.Combine(relativeSegments));
            if (File.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        throw new FileNotFoundException("Could not locate " + Path.Combine(relativeSegments) + " above " + AppContext.BaseDirectory);
    }

    /// <summary>A temp directory holding one <c>.uiscreen</c> asset and its source XAML file, wired together
    /// exactly as <see cref="UIScreenEditorSession.Open"/> expects.</summary>
    private sealed class ScreenFixture : IDisposable
    {
        public string Directory { get; }
        public string AssetPath { get; }
        public string XamlPath { get; }
        public UIScreenAsset Asset { get; }

        private ScreenFixture(string directory, string assetPath, string xamlPath, UIScreenAsset asset)
        {
            Directory = directory;
            AssetPath = assetPath;
            XamlPath = xamlPath;
            Asset = asset;
        }

        public static ScreenFixture Create(byte[] xamlBytes)
        {
            var directory = Path.Combine(Path.GetTempPath(), "CasaEngineMonogame", Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(directory);

            var xamlPath = Path.Combine(directory, "Screen.xaml");
            File.WriteAllBytes(xamlPath, xamlBytes);

            var assetPath = Path.Combine(directory, "Screen.uiscreen");
            var asset = new UIScreenAsset
            {
                Name = "Screen",
                FileName = "Screen.uiscreen",
                SourceXamlFile = "Screen.xaml",
            };

            var document = new JObject
            {
                ["id"] = asset.Id.ToString(),
                ["name"] = asset.Name,
                ["source_xaml_file"] = asset.SourceXamlFile,
                ["theme_name"] = asset.ThemeName,
                ["preview_resolution"] = new JObject
                {
                    ["x"] = asset.PreviewResolution.X,
                    ["y"] = asset.PreviewResolution.Y,
                },
                ["resource_files"] = new JArray(),
            };
            File.WriteAllText(assetPath, document.ToString());

            return new ScreenFixture(directory, assetPath, xamlPath, asset);
        }

        public void Dispose()
        {
            try
            {
                System.IO.Directory.Delete(Directory, recursive: true);
            }
            catch (IOException)
            {
                // Best effort cleanup; a locked temp file should not fail the test.
            }
        }
    }
}
