using MGUI.Core.UI;
using MGUI.Core.UI.Docking.Controls;
using MGUI.Core.UI.Styling;
using MGUI.Core.UI.XAML;
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
}