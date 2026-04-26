using MGUI.Core.UI;
using MGUI.Core.UI.Styling;
using MGUI.Core.UI.XAML;
using Xunit;

namespace CasaEngine.Tests.UI;

public class EditorControlTemplateAssetLoadingTests
{
    private static readonly string RepoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    [Fact]
    public void EditorControlTemplateAsset_Can_Load_With_BasedOn_Templates()
    {
        string templatePath = Path.Combine(RepoRoot, "CasaEngine.Editor", "Content", "UI", "Templates", "CasaEditor.Dark.ControlTemplates.xaml");

        Assert.True(File.Exists(templatePath), $"Missing editor control-template asset '{templatePath}'.");

        MGResources resources = new(new MGTheme(MGTheme.BuiltInTheme.Dark_Blue, "JetBrainsMono"));
        MGControlTemplateCatalog.RegisterDefaults(resources);

        IReadOnlyDictionary<string, MGControlTemplate> templates = resources.LoadControlTemplatesFromXaml(XamlDocumentSource.FromFile(templatePath));

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
}