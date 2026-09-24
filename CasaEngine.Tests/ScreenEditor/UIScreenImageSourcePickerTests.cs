using CasaEngine.Editor.Controls;
using CasaEngine.EditorServices.ScreenEditor.Commands;
using CasaEngine.EditorServices.ScreenEditor.DocumentModel;
using CasaEngine.EditorServices.ScreenEditor.Inspector;
using CasaEngine.EditorServices.ScreenEditor.Preview;
using CasaEngine.EditorServices.ScreenEditor.Selection;
using CasaEngine.EditorServices.ScreenEditor.Xaml;
using CasaEngine.Framework.Assets;
using CasaEngine.Tests.UI;
using MGUI.Core.UI;
using Xunit;

namespace CasaEngine.Tests.ScreenEditor;

/// <summary>
/// T4.3 (ADR-0038, "Images are named by asset"): the screen inspector edits an <c>Image</c>'s source through
/// <c>SourceName</c>, the name MGUI's XAML reads, and offers an asset picker limited to sprites and 2D animations
/// that writes the picked asset's id into the document and updates the preview.
/// <para/>
/// The asset catalog (<see cref="AssetCatalog"/>) is global state, so this class runs in the
/// <see cref="ProjectEnvironmentCollection"/> and clears it on both sides, like <c>CasaUIAssetProviderTests</c>.
/// </summary>
[Collection(ProjectEnvironmentCollection.Name)]
public sealed class UIScreenImageSourcePickerTests : IDisposable
{
    private static readonly Guid SpriteId = Guid.Parse("a1000000-0000-0000-0000-000000000001");
    private static readonly Guid AnimationId = Guid.Parse("a2000000-0000-0000-0000-000000000002");
    private static readonly Guid TextureId = Guid.Parse("a3000000-0000-0000-0000-000000000003");

    private const string ScreenXaml =
        "<Window xmlns=\"clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core\" " +
        "xmlns:dataBinding=\"clr-namespace:MGUI.Core.UI.DataBinding;assembly=MGUI.Core\">" +
        "<Image Name=\"imgCursor\" SourceName=\"cursor\" /></Window>";

    public UIScreenImageSourcePickerTests()
    {
        AssetCatalog.ClearInternal();
        AssetCatalog.AddInternal(new AssetInfo(SpriteId) { Name = "cursor", FileName = "cursor.sprite" });
        AssetCatalog.AddInternal(new AssetInfo(AnimationId) { Name = "cursor_wind", FileName = "cursor_wind.anim2d" });
        AssetCatalog.AddInternal(new AssetInfo(TextureId) { Name = "sheet", FileName = "sheet.texture" });
    }

    public void Dispose()
    {
        AssetCatalog.ClearInternal();
    }

    [Fact]
    public void Registry_EditsAnImageSourceThroughSourceName_AsASpriteOrAnimationReference()
    {
        var descriptors = UIPropertyRegistry.Default.GetDescriptors("Image");

        Assert.DoesNotContain(descriptors, d => d.Name == "Source");
        var sourceName = Assert.Single(descriptors, d => d.Name == "SourceName");
        Assert.True(sourceName.IsAssetReference);
        Assert.True(sourceName.AcceptsAsset(AssetCatalog.Get(SpriteId)));
        Assert.True(sourceName.AcceptsAsset(AssetCatalog.Get(AnimationId)));
        Assert.False(sourceName.AcceptsAsset(AssetCatalog.Get(TextureId)));
        Assert.False(sourceName.AcceptsAsset(null));

        Assert.All(UIPropertyRegistry.Default.GetDescriptors("TextBlock"), d => Assert.False(d.IsAssetReference));
    }

    [Fact]
    public void Picker_ListsOnlySpritesAndAnimations_AndShowsTheAssetTheCurrentNameDesignates()
    {
        var (_, _, selector, _) = OpenInspectorOnImage(new UICommandStack());

        var pickable = selector.GetPickableAssets();

        Assert.Equal(new[] { SpriteId, AnimationId }, pickable.Select(a => a.Id));
        // The document names the sprite by its asset name: the picker shows that asset.
        Assert.Equal(SpriteId, selector.AssetId);
    }

    [Theory]
    [InlineData("a1000000-0000-0000-0000-000000000001")]
    [InlineData("a2000000-0000-0000-0000-000000000002")]
    public void PickingAnAsset_WritesItsIdIntoTheDocument_AsOneUndoableStep(string pickedId)
    {
        var commandStack = new UICommandStack();
        var (inspector, node, selector, _) = OpenInspectorOnImage(commandStack);
        var modifications = new List<(DocumentNodeId NodeId, string Property, string Value)>();
        inspector.PropertyModified += (_, nodeId, property, value) => modifications.Add((nodeId, property, value));

        selector.SelectAsset(AssetCatalog.Get(Guid.Parse(pickedId)));

        Assert.Equal(pickedId, node.Properties["SourceName"].SerializedValue);
        Assert.Equal(new[] { (node.Id, "SourceName", pickedId) }, modifications);

        commandStack.Undo();
        Assert.Equal("cursor", node.Properties["SourceName"].SerializedValue);
    }

    [Fact]
    public void PickingAnAsset_SavesAsSourceNameInTheXaml()
    {
        var (_, _, selector, document) = OpenInspectorOnImage(new UICommandStack());

        selector.SelectAsset(AssetCatalog.Get(AnimationId));
        var saved = new UIScreenXamlSerializer().Serialize(document);

        Assert.Contains($"SourceName=\"{AnimationId:D}\"", saved);
        var reparsed = new UIScreenXamlParser().Parse(saved);
        Assert.Equal(AnimationId.ToString("D"), FindImage(reparsed).Properties["SourceName"].SerializedValue);
    }

    [Fact]
    public void Preview_TakesThePickedSourceWithoutARebuild_ButRebuildsForABinding()
    {
        var (desktop, _) = HeadlessUiTestHarness.NewDesktop();
        var document = new UIScreenXamlParser().Parse(ScreenXaml);
        var (_, nodeMap) = new UIScreenPreviewBuilder().BuildWithMapping(desktop, document);
        var image = Assert.IsType<MGImage>(nodeMap[FindImage(document).Id]);

        Assert.True(MGElementPropertyApplier.TryApply(image, "SourceName", SpriteId.ToString("D")));
        Assert.Equal(SpriteId.ToString("D"), image.SourceName);

        Assert.True(MGElementPropertyApplier.TryApply(image, "SourceName", null));
        Assert.Null(image.SourceName);

        Assert.False(MGElementPropertyApplier.TryApply(image, "SourceName", "{dataBinding:MGBinding Path=Icon}"));
        Assert.Null(image.SourceName);
    }

    private static (UIScreenInspectorPanel Inspector, UIScreenNode Node, AssetSelector Selector, UIScreenDocument Document)
        OpenInspectorOnImage(UICommandStack commandStack)
    {
        var (desktop, _) = HeadlessUiTestHarness.NewDesktop();
        var window = new MGWindow(desktop, 0, 0, 400, 600);
        desktop.Windows.Add(window);

        var document = new UIScreenXamlParser().Parse(ScreenXaml);
        var node = FindImage(document);
        var selection = new UIScreenSelectionService();
        var inspector = new UIScreenInspectorPanel(window, selection);
        inspector.SetCommandStack(commandStack);
        inspector.SetDocument(document);
        selection.Select(node.Id);

        var content = inspector.CreateContent();
        var selector = Assert.Single(content.TraverseVisualTree().OfType<AssetSelector>());
        return (inspector, node, selector, document);
    }

    private static UIScreenNode FindImage(UIScreenDocument document)
        => Assert.Single(document.Root!.Children, n => n.ControlType == "Image");
}
