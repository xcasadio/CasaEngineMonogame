using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using MGUI.Core.UI.Containers;
using MGUI.Core.UI.Docking.Controls;
using MGUI.Core.UI.Docking.DockLayout;
using MGUI.Core.UI.Docking;
using MGUI.Core.UI.Styling;
using MGUI.Shared.Assets;
using MGUI.Shared.Helpers;
using MGUI.Shared.Input;
using MGUI.Shared.Rendering;
using MGUI.Shared.Rendering.Clipping;
using MGUI.Shared.Text;
using MGUI.Shared.Text.Engines;
using System.Reflection;
using XamlDocumentSource = MGUI.Core.UI.XAML.XamlDocumentSource;
using Xunit;

namespace CasaEngine.Tests.UI;

public class EditorControlTemplateAssetLoadingTests
{
    private static readonly string RepoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
    private static readonly string TemplatePath = Path.Combine(RepoRoot, "CasaEngine.Editor", "Content", "UI", "Templates", "CasaEditor.Dark.ControlTemplates.xaml");
    private static readonly string ThemePath = Path.Combine(RepoRoot, "CasaEngine.Editor", "Content", "UI", "Themes", "CasaEditor.Dark.Theme.xaml");

    [Fact]
    public void EditorControlTemplateAsset_Can_Load_With_BasedOn_Templates()
    {
        Assert.True(File.Exists(TemplatePath), $"Missing editor control-template asset '{TemplatePath}'.");

        MGResources resources = new(new MGTheme(MGTheme.BuiltInTheme.Dark_Blue, "JetBrainsMono"));
        MGControlTemplateCatalog.RegisterDefaults(resources);

        IReadOnlyDictionary<string, MGControlTemplate> templates = resources.LoadControlTemplatesFromXaml(XamlDocumentSource.FromFile(TemplatePath));

        string[] expectedTemplates =
        {
            "CasaEditor.Window",
            "CasaEditor.ToolTip",
            "CasaEditor.Overlay",
            "CasaEditor.ContextMenu",
            "CasaEditor.ContextMenuItem",
            "CasaEditor.ListBox",
            "CasaEditor.ListView",
            "CasaEditor.ComboBox",
            "CasaEditor.TreeView",
            "CasaEditor.TabControl",
            "CasaEditor.DockTabItem",
            "CasaEditor.DockAutoHideDrawer",
            "CasaEditor.DockAutoHideStrip",
            "CasaEditor.DockSplitter",
            "CasaEditor.DockDropIndicators",
        };

        foreach (string templateName in expectedTemplates)
        {
            Assert.True(templates.ContainsKey(templateName), $"Template '{templateName}' was not loaded from the editor asset.");
            Assert.True(resources.TryGetControlTemplate(templateName, out _), $"Template '{templateName}' was not registered in resources.");
        }
    }

    [Fact]
    public void EditorThemeAsset_Maps_Editor_Control_Templates()
    {
        Assert.True(File.Exists(TemplatePath), $"Missing editor control-template asset '{TemplatePath}'.");
        Assert.True(File.Exists(ThemePath), $"Missing editor theme asset '{ThemePath}'.");

        MGResources resources = new(MGTheme.CreateEmpty("JetBrainsMono"));
        MGControlTemplateCatalog.RegisterDefaults(resources);
        resources.LoadControlTemplatesFromXaml(XamlDocumentSource.FromFile(TemplatePath));

        IReadOnlyDictionary<string, MGTheme> themes = resources.LoadThemesFromXaml(XamlDocumentSource.FromFile(ThemePath));

        Assert.True(themes.TryGetValue("CasaEditor.Dark", out MGTheme theme));

        AssertMappedTemplate(theme, MGElementType.Window, "CasaEditor.Window");
        AssertMappedTemplate(theme, MGElementType.ToolTip, "CasaEditor.ToolTip");
        AssertMappedTemplate(theme, MGElementType.Overlay, "CasaEditor.Overlay");
        AssertMappedTemplate(theme, MGElementType.ContextMenu, "CasaEditor.ContextMenu");
        AssertMappedTemplate(theme, MGElementType.ContextMenuItem, "CasaEditor.ContextMenuItem");
        AssertMappedTemplate(theme, MGElementType.ListBox, "CasaEditor.ListBox");
        AssertMappedTemplate(theme, MGElementType.ListView, "CasaEditor.ListView");
        AssertMappedTemplate(theme, MGElementType.ComboBox, "CasaEditor.ComboBox");
        AssertMappedTemplate(theme, MGElementType.TreeView, "CasaEditor.TreeView");
        AssertMappedTemplate(theme, MGElementType.TabControl, "CasaEditor.TabControl");

        AssertMappedTemplate(theme, typeof(MGDockTabItem), "CasaEditor.DockTabItem");
        AssertMappedTemplate(theme, typeof(MGDockAutoHideDrawer), "CasaEditor.DockAutoHideDrawer");
        AssertMappedTemplate(theme, typeof(MGDockAutoHideStrip), "CasaEditor.DockAutoHideStrip");
        AssertMappedTemplate(theme, typeof(MGDockSplitterBar), "CasaEditor.DockSplitter");
        AssertMappedTemplate(theme, typeof(MGDockDropIndicators), "CasaEditor.DockDropIndicators");

        Assert.Equal(18, theme.CheckBoxComponentSize);
        Assert.Equal(CheckIndicatorStyle.FilledSquare, theme.CheckBoxCheckedIndicatorStyle);
        VisualStateFillBrush listBoxItemBackground = theme.ListBoxItemBackground.GetValue(true);
        Assert.Equal(new Color(68, 68, 68), Assert.IsType<MGSolidFillBrush>(listBoxItemBackground.FocusedValue).Color);
        Assert.Equal(new Color(68, 68, 68), Assert.IsType<MGSolidFillBrush>(listBoxItemBackground.SelectedValue).Color);
        Assert.Equal(new Color(58, 58, 58), Assert.IsType<MGSolidFillBrush>(theme.Docking.TabActiveBackground).Color);
        Assert.Equal(Color.Transparent, theme.Docking.TabActiveAccentColor);
        Assert.Equal(Color.Transparent, theme.Docking.TabHoverAccentColor);
    }

    [Fact]
    public void EditorThemeAsset_Applies_CheckBox_Defaults()
    {
        EditorThemeTestContext context = CreateThemeTestContext();

        MGCheckBox checkBox = new(context.Window, true) { Name = "Editor CheckBox" };

        Assert.Equal(18, checkBox.CheckBoxComponentSize);
        Assert.Equal(CheckIndicatorStyle.FilledSquare, checkBox.CheckedIndicatorStyle);
    }

    [Fact]
    public void EditorThemeAsset_Disables_Docking_Accent_Bars()
    {
        EditorThemeTestContext context = CreateThemeTestContext();

        DockPanelNode dockPanel = new("content")
        {
            Title = "Content Browser",
            CanClose = true,
        };

        MGDockTabItem dockTabItem = new(context.Window, dockPanel)
        {
            Name = "Dock Tab",
            IsActive = true,
        };

        DockTabGroupNode groupNode = new();
        groupNode.Panels.Add(dockPanel);

        MGDockTabGroup dockTabGroup = new(context.Window, groupNode)
        {
            Name = "Dock Group",
            IsActiveGroup = true,
        };

        dockTabItem.UpdateLayout(new Rectangle(0, 0, 180, 30));
        dockTabGroup.UpdateLayout(new Rectangle(0, 40, 320, 200));

        Assert.True(dockTabItem.TryGetTemplatePart(MGDockTabItem.AccentPartName, out MGElement? tabAccentPart));
        Assert.Equal(Visibility.Collapsed, tabAccentPart.Visibility);

        Assert.True(dockTabGroup.TryGetTemplatePart(MGDockTabGroup.AccentPartName, out MGElement? groupAccentPart));
        Assert.Equal(Visibility.Collapsed, groupAccentPart.Visibility);
    }

    [Fact]
    public void DockTabItem_Active_AccessoryButtons_Use_Active_Background()
    {
        EditorThemeTestContext context = CreateThemeTestContext();

        DockPanelNode dockPanel = new("content")
        {
            Title = "Content Browser",
            CanClose = true,
            CanAutoHide = true,
        };

        MGDockTabItem dockTabItem = new(context.Window, dockPanel)
        {
            Name = "Dock Tab",
            IsActive = true,
        };

        dockTabItem.UpdateLayout(new Rectangle(0, 0, 180, 30));

        MGBorder closeButton = Assert.IsType<MGBorder>(typeof(MGDockTabItem)
            .GetField("_closeButton", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(dockTabItem));
        MGBorder pinButton = Assert.IsType<MGBorder>(typeof(MGDockTabItem)
            .GetField("_pinButton", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(dockTabItem));

        Color activeColor = Assert.IsType<MGSolidFillBrush>(dockTabItem.ActiveBrush).Color;
        Assert.Equal(activeColor, Assert.IsType<MGSolidFillBrush>(closeButton.BackgroundBrush.NormalValue).Color);
        Assert.Equal(activeColor, Assert.IsType<MGSolidFillBrush>(pinButton.BackgroundBrush.NormalValue).Color);
    }

    [Fact]
    public void DockTabGroup_Uses_Theme_Accent_For_Active_Group()
    {
        EditorThemeTestContext context = CreateThemeTestContext();
        context.Window.Theme = context.Window.GetTheme().Copy();
        context.Window.Theme.Docking.TabActiveAccentColor = new Color(12, 34, 56, 255);

        DockPanelNode dockPanel = new("content")
        {
            Title = "Content Browser",
            CanClose = true,
        };

        DockTabGroupNode groupNode = new();
        groupNode.Panels.Add(dockPanel);

        MGDockTabGroup dockTabGroup = new(context.Window, groupNode)
        {
            Name = "Dock Group",
            IsActiveGroup = true,
        };

        dockTabGroup.UpdateLayout(new Rectangle(0, 40, 320, 200));

        Assert.True(dockTabGroup.TryGetTemplatePart(MGDockTabGroup.AccentPartName, out MGElement? groupAccentPart));
        MGRectangle accentRectangle = Assert.IsType<MGRectangle>(groupAccentPart);
        Assert.Equal(Visibility.Visible, accentRectangle.Visibility);
        Assert.Equal(new Color(12, 34, 56, 255), Assert.IsType<MGSolidFillBrush>(accentRectangle.Fill).Color);
    }

    [Fact]
    public void DockSplitContainer_Overconstrained_MinSizes_Do_Not_Throw()
    {
        EditorThemeTestContext context = CreateThemeTestContext();

        TestDockSplitContainer splitContainer = new(context.Window, Orientation.Horizontal)
        {
            MinFirstSize = 200,
            MinSecondSize = 200,
            SplitterThickness = 4,
            FirstChild = new MGBorder(context.Window),
            SecondChild = new MGBorder(context.Window),
        };

        splitContainer.ForceLayout(new Rectangle(0, 0, 300, 160));

        Exception exception = Record.Exception(() => splitContainer.SetSplitRatio(0.9f));

        Assert.Null(exception);
        Assert.Equal(0.5f, splitContainer.SplitRatio);

        splitContainer.ForceLayout(new Rectangle(0, 0, 300, 160));
        Assert.Equal(148, splitContainer.FirstChild.LayoutBounds.Width);
        Assert.Equal(148, splitContainer.SecondChild.LayoutBounds.Width);
    }

    [Fact]
    public void EditorThemeAssets_Apply_CustomTemplates_Without_TemplateErrors()
    {
        EditorThemeTestContext context = CreateThemeTestContext();

        MGButton toolTipHost = new(context.Window) { Name = "ToolTip Host" };
        Assert.True(context.Scope.TryAddChild(toolTipHost));

        MGToolTip toolTip = new(context.Window, toolTipHost, 180, 80) { Name = "Editor ToolTip" };
        MGBorder overlayContent = new(context.Desktop.OverlayHost.SelfOrParentWindow) { Name = "Overlay Content" };
        MGOverlay overlay = context.Desktop.OverlayHost.AddOverlay(overlayContent, true);
        overlay.Name = "Editor Overlay";

        MGContextMenu contextMenu = MGContextMenu.CreateSimpleMenu(context.Window, "Actions", null,
            new MGSimpleContextMenuItem("open", "Open"));
        contextMenu.Name = "Editor Context Menu";
        MGContextMenuButton contextMenuItem = Assert.IsType<MGContextMenuButton>(Assert.Single(contextMenu.Items));

        MGListBox<string> listBox = new(context.Window) { Name = "Editor ListBox" };
        MGListView<string> listView = new(context.Window) { Name = "Editor ListView" };
        MGComboBox<string> comboBox = new(context.Window) { Name = "Editor ComboBox" };
        MGTreeView treeView = new(context.Window) { Name = "Editor TreeView" };
        MGTabControl tabControl = new(context.Window) { Name = "Editor TabControl" };

        Assert.True(context.Scope.TryAddChild(listBox));
        Assert.True(context.Scope.TryAddChild(listView));
        Assert.True(context.Scope.TryAddChild(comboBox));
        Assert.True(context.Scope.TryAddChild(treeView));
        Assert.True(context.Scope.TryAddChild(tabControl));

        DockPanelNode dockPanel = new("inspector") { Title = "Inspector" };
        MGDockTabItem dockTabItem = new(context.Window, dockPanel) { Name = "Dock Tab" };
        MGDockAutoHideDrawer autoHideDrawer = new(context.Window) { Name = "Auto Hide Drawer" };
        MGDockAutoHideStrip autoHideStrip = new(context.Window, AutoHideSide.Left) { Name = "Auto Hide Strip" };
        MGDockSplitterBar dockSplitter = new(context.Window) { Name = "Dock Splitter" };
        MGDockDropIndicators dockDropIndicators = new(context.Window) { Name = "Dock Drop Indicators" };

        context.Runtime.ApplyFrame(new UpdateBaseArgs(TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(1), default, default));
        context.Desktop.Update();

        AssertAppliedTemplate(context.Window, "CasaEditor.Window",
            MGWindow.BorderPartName,
            MGWindow.TitleBarPartName,
            MGWindow.TitleBarTextPartName,
            MGWindow.CloseButtonPartName,
            MGWindow.ResizeGripPartName);
        AssertAppliedTemplate(toolTip, "CasaEditor.ToolTip",
            MGWindow.BorderPartName,
            MGWindow.TitleBarPartName,
            MGWindow.TitleBarTextPartName,
            MGWindow.CloseButtonPartName,
            MGWindow.ResizeGripPartName);
        AssertAppliedTemplate(overlay, "CasaEditor.Overlay",
            MGOverlay.BorderPartName,
            MGOverlay.CloseButtonPartName);
        AssertAppliedTemplate(contextMenu, "CasaEditor.ContextMenu",
            MGContextMenu.ScrollViewerPartName,
            MGContextMenu.ItemsPanelPartName);
        AssertAppliedTemplate(contextMenuItem, "CasaEditor.ContextMenuItem",
            MGWrappedContextMenuItem.ContentWrapperPartName,
            MGWrappedContextMenuItem.HeaderPresenterPartName,
            MGWrappedContextMenuItem.SubmenuArrowPartName,
            MGWrappedContextMenuItem.ShortcutTextPartName);
        AssertAppliedTemplate(listBox, "CasaEditor.ListBox",
            MGListBox<string>.OuterBorderPartName,
            MGListBox<string>.TitleBorderPartName,
            MGListBox<string>.TitlePresenterPartName,
            MGListBox<string>.InnerBorderPartName,
            MGListBox<string>.ScrollViewerPartName,
            MGListBox<string>.ItemsPanelPartName);
        AssertAppliedTemplate(listView, "CasaEditor.ListView",
            MGListView<string>.DockPanelPartName,
            MGListView<string>.HeaderGridPartName,
            MGListView<string>.ScrollViewerPartName,
            MGListView<string>.DataGridPartName);
        AssertAppliedTemplate(comboBox, "CasaEditor.ComboBox",
            MGComboBox<string>.BorderPartName,
            MGComboBox<string>.DropdownArrowPartName,
            MGComboBox<string>.DropdownWindowPartName,
            MGComboBox<string>.DropdownItemsPanelPartName,
            MGComboBox<string>.DropdownScrollViewerPartName);
        AssertAppliedTemplate(treeView, "CasaEditor.TreeView",
            MGTreeView.OuterBorderPartName,
            MGTreeView.ScrollViewerPartName,
            MGTreeView.ItemsPanelPartName);
        AssertAppliedTemplate(tabControl, "CasaEditor.TabControl",
            MGTabControl.BorderPartName,
            MGTabControl.HeadersPanelPartName);

        AssertAppliedTemplate(dockTabItem, "CasaEditor.DockTabItem",
            MGDockTabItem.SurfacePartName,
            MGDockTabItem.AccentPartName,
            MGDockTabItem.CloseIconPartName,
            MGDockTabItem.PinIconPartName);
        AssertAppliedTemplate(autoHideDrawer, "CasaEditor.DockAutoHideDrawer",
            MGDockAutoHideDrawer.BorderPartName,
            MGDockAutoHideDrawer.HeaderPartName,
            MGDockAutoHideDrawer.TitleLabelPartName,
            MGDockAutoHideDrawer.PinButtonPartName,
            MGDockAutoHideDrawer.CloseButtonPartName,
            MGDockAutoHideDrawer.PinIconPartName,
            MGDockAutoHideDrawer.CloseIconPartName,
            MGDockAutoHideDrawer.ResizeGripPartName);
        AssertAppliedTemplate(autoHideStrip, "CasaEditor.DockAutoHideStrip",
            MGDockAutoHideStrip.SeparatorPartName);
        AssertAppliedTemplate(dockSplitter, "CasaEditor.DockSplitter",
            MGDockSplitterBar.SurfacePartName,
            MGDockSplitterBar.AccentPartName,
            MGDockSplitterBar.GripPartName);
        AssertAppliedTemplate(dockDropIndicators, "CasaEditor.DockDropIndicators");
    }

    private static void AssertMappedTemplate(MGTheme theme, MGElementType elementType, string expectedTemplateName)
    {
        Assert.True(theme.TryGetControlTemplateMapping(elementType, out string actualTemplateName));
        Assert.Equal(expectedTemplateName, actualTemplateName);
    }

    private static void AssertMappedTemplate(MGTheme theme, Type controlType, string expectedTemplateName)
    {
        Assert.True(theme.TryGetControlTemplateMapping(controlType, out string actualTemplateName));
        Assert.Equal(expectedTemplateName, actualTemplateName);
    }

    private static void AssertAppliedTemplate(MGElement element, string expectedTemplateName, params string[] requiredParts)
    {
        Assert.Equal(expectedTemplateName, element.AppliedControlTemplateName);
        Assert.True(string.IsNullOrWhiteSpace(element.LastControlTemplateError),
            $"Control '{element.GetType().Name}' reported template error '{element.LastControlTemplateError}'.");

        foreach (string partName in requiredParts)
        {
            Assert.True(element.TryGetTemplatePart(partName, out _),
                $"Control '{element.GetType().Name}' is missing expected template part '{partName}'.");
        }
    }

    private static EditorThemeTestContext CreateThemeTestContext()
    {
        Assert.True(File.Exists(TemplatePath), $"Missing editor control-template asset '{TemplatePath}'.");
        Assert.True(File.Exists(ThemePath), $"Missing editor theme asset '{ThemePath}'.");

        EditorThemeTestRuntime runtime = new(new Rectangle(0, 0, 1280, 720));
        MGDesktop desktop = new(runtime);
        desktop.LoadDefaultResources();
        desktop.Resources.LoadControlTemplatesFromXaml(XamlDocumentSource.FromFile(TemplatePath));

        IReadOnlyDictionary<string, MGTheme> themes = desktop.Resources.LoadThemesFromXaml(XamlDocumentSource.FromFile(ThemePath));
        desktop.Resources.DefaultTheme = themes["CasaEditor.Dark"];

        MGWindow window = new(desktop, 16, 16, 640, 480) { Name = "Editor Theme Window" };
        desktop.Windows.Add(window);

        MGStackPanel scope = new(window, Orientation.Vertical) { Name = "Editor Theme Scope" };
        window.SetContent(scope);

        return new(runtime, desktop, window, scope);
    }

    private sealed record EditorThemeTestContext(
        EditorThemeTestRuntime Runtime,
        MGDesktop Desktop,
        MGWindow Window,
        MGStackPanel Scope);

    private sealed class TestDockSplitContainer : MGDockSplitContainer
    {
        public TestDockSplitContainer(MGWindow window, Orientation orientation)
            : base(window, orientation)
        {
        }

        public void ForceLayout(Rectangle bounds)
        {
            UpdateLayout(bounds);
        }
    }

    private sealed class EditorThemeTestRuntime : IUIDesktopRuntime
    {
        private ITextMeasurementEngine _textEngine;

        public InputTracker Input { get; } = new();
        public string DefaultFontFamily { get; } = "TestSans";
        public IUISurface Surface { get; }
        public IUIAssetProvider AssetProvider { get; }
        public UpdateBaseArgs UpdateArgs { get; private set; } = new(TimeSpan.Zero, TimeSpan.Zero, default, default);

        public event EventHandler<EventArgs<ITextMeasurementEngine>>? TextEngineChanged;
        public event EventHandler<EventArgs>? EndUpdate
        {
            add { }
            remove { }
        }

        public ITextMeasurementEngine TextEngine
        {
            get => _textEngine;
            set
            {
                ITextMeasurementEngine previous = _textEngine;
                _textEngine = value ?? throw new ArgumentNullException(nameof(value));
                TextEngineChanged?.Invoke(this, new(previous, _textEngine));
            }
        }

        public EditorThemeTestRuntime(Rectangle surfaceBounds)
        {
            Surface = new EditorThemeTestSurface(surfaceBounds, new EditorThemeTestRenderTarget(surfaceBounds.Width, surfaceBounds.Height));
            AssetProvider = new EditorThemeTestAssetProvider();
            _textEngine = new EditorThemeTestTextEngine(DefaultFontFamily);
        }

        public IUIDrawTransaction CreateDrawTransaction(DrawSettings settings, bool deferBegin)
            => new EditorThemeNoOpDrawTransaction(this, settings ?? DrawSettings.Default);

        public void ApplyFrame(UpdateBaseArgs updateArgs)
        {
            UpdateArgs = updateArgs;
            Input.Update(updateArgs);
        }

        public void RegisterView(IUIView view)
        {
        }
    }

    private sealed class EditorThemeNoOpDrawTransaction : IUIDrawTransaction
    {
        private Rectangle? _currentClipBounds;

        public DrawSettings CurrentSettings { get; private set; }
        public IUIDesktopRuntime Renderer { get; }
        public Rectangle? CurrentClipBounds => _currentClipBounds;

        public EditorThemeNoOpDrawTransaction(IUIDesktopRuntime renderer, DrawSettings settings)
        {
            Renderer = renderer;
            CurrentSettings = settings;
        }

        public void DrawTextureTo(IUIImageResource texture, Rectangle? source, Rectangle destination, Color colorMask)
        {
        }

        public void DrawTextureTo(IUIImageResource texture, Rectangle? source, Rectangle destination, Color colorMask,
            Vector2 origin, float rotation = 0f, float depth = 0f, UIDrawFlip flip = UIDrawFlip.None)
        {
        }

        public void DrawTextureAt(IUIImageResource texture, Rectangle? source, Vector2 destination, Color colorMask,
            Vector2 origin, float rotation = 0f, float scaleX = 1f, float scaleY = 1f, float depth = 0f, UIDrawFlip flip = UIDrawFlip.None)
        {
        }

        public void DrawTextViaEngine(ResolvedFont font, string text, Vector2 position, Color color, Vector2 origin, float scale,
            float rotation = 0f, float depth = 0f, UIDrawFlip flip = UIDrawFlip.None)
        {
        }

        public void FillRectangle(Vector2 origin, RectangleF destination, Color color)
        {
        }

        public void FillPoint(Vector2 center, Color color, float width)
        {
        }

        public void StrokeRectangle(Vector2 origin, RectangleF destination, Color color, Thickness thickness)
        {
        }

        public void StrokeAndFillRectangle(Vector2 origin, RectangleF destination, Color strokeColor, Color fillColor, Thickness strokeThickness)
        {
        }

        public void FillPolygon(Vector2 origin, IEnumerable<Vector2> vertices, Color color)
        {
        }

        public void StrokeAndFillPolygon(Vector2 origin, IEnumerable<Vector2> vertices, Color strokeColor, Color fillColor, float strokeThickness = 1.0f)
        {
        }

        public void FillTriangle(Vector2 origin, Vector2 v0, Color c0, Vector2 v1, Color c1, Vector2 v2, Color c2)
        {
        }

        public void FillQuadrilateralLinearClamp(Vector2 origin, Vector2 topLeft, Color topLeftColor, Vector2 topRight, Color topRightColor,
            Vector2 bottomRight, Color bottomRightColor, Vector2 bottomLeft, Color bottomLeftColor)
        {
        }

        public void StrokeLineSegment(Vector2 origin, Vector2 start, Vector2 end, Color color, float thickness = 1.0f)
        {
        }

        public void FillCircle(Vector2 center, Color color, float radius, int numSides = 32)
        {
        }

        public void StrokeCircle(Vector2 center, Color color, float radius, float thickness = 1.0f, int numSides = 32)
        {
        }

        public void StrokeAndFillCircle(Vector2 center, Color strokeColor, Color fillColor, float radius, float strokeThickness = 1.0f, int numSides = 32)
        {
        }

        public void SetDrawSettings(DrawSettings settings)
        {
            CurrentSettings = settings ?? DrawSettings.Default;
        }

        public IDisposable SetDrawSettingsTemporary(DrawSettings settings)
        {
            DrawSettings previous = CurrentSettings;
            CurrentSettings = settings ?? DrawSettings.Default;
            return new DisposableAction(() => CurrentSettings = previous);
        }

        public IDisposable SetRenderTargetTemporary(IUIRenderTarget renderTarget, Color? clearColor)
            => new DisposableAction(() => { });

        public IDisposable SetTransformTemporary(Matrix transform)
            => SetDrawSettingsTemporary(CurrentSettings with { Transform = transform });

        public ClipResolveResult ResolveClip(ClipDefinition definition)
        {
            ClipDefinition effective = definition ?? ClipDefinition.None();
            return new(effective, effective, ClipStrategy.Scissor, false);
        }

        public ClipScope PushClipTemporary(ClipDefinition definition)
        {
            ClipResolveResult resolution = ResolveClip(definition);
            Rectangle? previous = _currentClipBounds;
            _currentClipBounds = resolution.Effective.Kind == ClipKind.None ? null : resolution.Effective.Shape.Bounds;
            return new(resolution, () => _currentClipBounds = previous);
        }

        public ClipScope PushRectangleClip(Rectangle? bounds, bool intersectWithCurrentClipTarget)
        {
            ClipDefinition definition = bounds.HasValue
                ? ClipDefinition.Rectangle(bounds.Value, intersectWithCurrentClipTarget)
                : ClipDefinition.None(intersectWithCurrentClipTarget);
            return PushClipTemporary(definition);
        }

        public IDisposable SetClipTargetTemporary(Rectangle? bounds, bool intersectWithCurrentClipTarget)
            => PushRectangleClip(bounds, intersectWithCurrentClipTarget);

        public void Dispose()
        {
        }
    }

    private sealed class EditorThemeTestSurface : IUISurface
    {
        private readonly Rectangle _bounds;
        private readonly IUIRenderTarget _renderTarget;

        public EditorThemeTestSurface(Rectangle bounds, IUIRenderTarget renderTarget)
        {
            _bounds = bounds;
            _renderTarget = renderTarget;
        }

        public Rectangle GetBounds() => _bounds;

        public IUIRenderTarget GetRenderTarget() => _renderTarget;
    }

    private sealed class EditorThemeTestAssetProvider : IUIAssetProvider
    {
        private readonly Dictionary<string, EditorThemeTestImageResource> _images = new(StringComparer.OrdinalIgnoreCase);

        public IUIImageResource LoadImage(string assetName)
        {
            if (!_images.TryGetValue(assetName, out EditorThemeTestImageResource? image))
            {
                image = new(assetName, 16, 16);
                _images[assetName] = image;
            }

            return image;
        }

        public bool TryLoadImage(string assetName, out IUIImageResource image)
        {
            image = LoadImage(assetName);
            return true;
        }
    }

    private class EditorThemeTestImageResource : IUIImageResource
    {
        public string Id { get; }
        public int Width { get; }
        public int Height { get; }
        public bool IsDisposed { get; private set; }

        public EditorThemeTestImageResource(string id, int width, int height)
        {
            Id = id;
            Width = width;
            Height = height;
        }
    }

    private sealed class EditorThemeTestRenderTarget : EditorThemeTestImageResource, IUIRenderTarget
    {
        public EditorThemeTestRenderTarget(int width, int height)
            : base("editor-theme-render-target", width, height)
        {
        }
    }

    private sealed class EditorThemeTestTextEngine : ITextMeasurementEngine
    {
        private readonly string _defaultFontFamily;

        public EditorThemeTestTextEngine(string defaultFontFamily)
        {
            _defaultFontFamily = defaultFontFamily;
        }

        public ResolvedFont ResolveFont(FontSpec spec)
        {
            int size = Math.Max(1, spec.Size);
            FontSpec effectiveSpec = string.IsNullOrWhiteSpace(spec.Family)
                ? FontSpec.Normal(_defaultFontFamily, size)
                : spec;

            return new ResolvedFont(effectiveSpec, size, 1.0f, 1.0f, size, Math.Max(1.0f, size * 0.5f), Vector2.Zero, false, new object());
        }

        public Vector2 MeasureText(ResolvedFont font, string text)
        {
            float width = (text?.Length ?? 0) * Math.Max(font.SpaceWidth, 1.0f);
            return new(width, font.LineHeight);
        }

        public GlyphMetrics MeasureGlyph(ResolvedFont font, char c)
            => new(0.0f, Math.Max(font.SpaceWidth, 1.0f), 0.0f, font.LineHeight);

        public float GetLineHeight(ResolvedFont font) => font.LineHeight;

        public float GetSpaceWidth(ResolvedFont font) => font.SpaceWidth;

        public void InvalidateCache()
        {
        }
    }

    private sealed class DisposableAction : IDisposable
    {
        private readonly Action _disposeAction;
        private bool _disposed;

        public DisposableAction(Action disposeAction)
        {
            _disposeAction = disposeAction;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _disposeAction();
        }
    }
}