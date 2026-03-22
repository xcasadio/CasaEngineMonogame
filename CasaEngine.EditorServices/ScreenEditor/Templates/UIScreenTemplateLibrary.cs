using System.Collections.Generic;
using System.Linq;
using CasaEngine.EditorServices.ScreenEditor.DocumentModel;
using CasaEngine.EditorServices.ScreenEditor.Xaml;

namespace CasaEngine.EditorServices.ScreenEditor.Templates;

/// <summary>A reusable node sub-tree template that can be inserted from the toolbox.</summary>
public sealed class UIScreenTemplate
{
    /// <summary>Unique template name shown in the toolbox.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Toolbox category (e.g. "Layout Templates", "Form Templates").</summary>
    public string Category { get; set; } = "User Templates";

    /// <summary>Brief description shown as a tooltip.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Raw XAML fragment for the template root element (without the doc-level namespace declarations).
    /// Use the short MGUI names (DockPanel, StackPanel, TextBlock …).
    /// </summary>
    public string Xaml { get; set; } = string.Empty;
}

/// <summary>
/// Central catalogue of <see cref="UIScreenTemplate"/> instances that can be inserted into a screen document.
/// Pre-populated with common layout patterns.
/// </summary>
public sealed class UIScreenTemplateLibrary
{
    private readonly List<UIScreenTemplate> _templates = new();

    private static UIScreenTemplateLibrary? _default;

    /// <summary>Singleton pre-populated with built-in templates.</summary>
    public static UIScreenTemplateLibrary Default => _default ??= CreateDefault();

    // ─── Registration ─────────────────────────────────────────────────────

    /// <summary>Registers a template. Duplicate names are allowed (the last one wins in <see cref="TryGet"/>).</summary>
    public void Register(UIScreenTemplate template)
    {
        ArgumentNullException.ThrowIfNull(template);
        _templates.Add(template);
    }

    // ─── Queries ──────────────────────────────────────────────────────────

    public IReadOnlyList<UIScreenTemplate> GetAll() => _templates;

    public IReadOnlyList<UIScreenTemplate> GetByCategory(string category)
        => _templates
            .Where(t => string.Equals(t.Category, category, StringComparison.Ordinal))
            .ToList();

    public IReadOnlyDictionary<string, IReadOnlyList<UIScreenTemplate>> GetByCategory()
        => _templates
            .GroupBy(t => t.Category, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<UIScreenTemplate>)g.ToList(), StringComparer.Ordinal);

    public UIScreenTemplate? TryGet(string name)
        => _templates.LastOrDefault(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Parses the template XAML and returns a deep-cloned root <see cref="UIScreenNode"/>,
    /// or null if parsing fails.
    /// </summary>
    public UIScreenNode? Instantiate(UIScreenTemplate template)
    {
        ArgumentNullException.ThrowIfNull(template);

        if (string.IsNullOrWhiteSpace(template.Xaml))
        {
            return null;
        }

        try
        {
            var doc = new UIScreenXamlParser().Parse(template.Xaml);
            return doc.Root?.DeepClone();
        }
        catch
        {
            return null;
        }
    }

    // ─── Built-in templates ───────────────────────────────────────────────

    private const string MguiNs = "clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core";
    private const string XamlNs = "http://schemas.microsoft.com/winfx/2006/xaml";

    private static UIScreenTemplateLibrary CreateDefault()
    {
        var lib = new UIScreenTemplateLibrary();

        // ── Layout patterns ───────────────────────────────────────────────
        lib.Register(new UIScreenTemplate
        {
            Name = "Button Row",
            Category = "Layout",
            Description = "Horizontal row with OK / Cancel buttons",
            Xaml = Wrap(@"<StackPanel Orientation=""Horizontal"" Spacing=""8""><Button Content=""OK"" /><Button Content=""Cancel"" /></StackPanel>"),
        });

        lib.Register(new UIScreenTemplate
        {
            Name = "Header + Content",
            Category = "Layout",
            Description = "Title bar docked to top with a scrollable content area below",
            Xaml = Wrap(@"<DockPanel LastChildFill=""True""><Border Dock=""Top"" Padding=""8""><TextBlock Text=""Header"" /></Border><Border Padding=""8""><TextBlock Text=""Content goes here"" WrapText=""True"" /></Border></DockPanel>"),
        });

        lib.Register(new UIScreenTemplate
        {
            Name = "Header + Content + Footer",
            Category = "Layout",
            Description = "Three-section layout: header, scrollable content, footer",
            Xaml = Wrap(@"<DockPanel LastChildFill=""True""><Border Dock=""Top"" Padding=""8""><TextBlock Text=""Header"" /></Border><Border Dock=""Bottom"" Padding=""8""><TextBlock Text=""Footer"" /></Border><Border Padding=""8""><TextBlock Text=""Content"" WrapText=""True"" /></Border></DockPanel>"),
        });

        // ── Form patterns ─────────────────────────────────────────────────
        lib.Register(new UIScreenTemplate
        {
            Name = "Form Row (Label + TextBox)",
            Category = "Form",
            Description = "Label on the left and a text box filling the remaining width",
            Xaml = Wrap(@"<DockPanel><TextBlock Dock=""Left"" Text=""Label:"" PreferredWidth=""120"" VerticalAlignment=""Center"" /><TextBox /></DockPanel>"),
        });

        lib.Register(new UIScreenTemplate
        {
            Name = "Form Row (Label + CheckBox)",
            Category = "Form",
            Description = "Label on the left with a checkbox on the right",
            Xaml = Wrap(@"<DockPanel><TextBlock Dock=""Left"" Text=""Option:"" PreferredWidth=""120"" VerticalAlignment=""Center"" /><CheckBox /></DockPanel>"),
        });

        lib.Register(new UIScreenTemplate
        {
            Name = "Search Bar",
            Category = "Form",
            Description = "Text box with a search label",
            Xaml = Wrap(@"<DockPanel><TextBlock Dock=""Left"" Text=""🔍"" VerticalAlignment=""Center"" Margin=""0,0,4,0"" /><TextBox Placeholder=""Search…"" /></DockPanel>"),
        });

        // ── HUD patterns ──────────────────────────────────────────────────
        lib.Register(new UIScreenTemplate
        {
            Name = "Stat Bar (Label + Progress)",
            Category = "HUD",
            Description = "Named progress bar for HP, XP, etc.",
            Xaml = Wrap(@"<DockPanel><TextBlock Dock=""Left"" Text=""HP:"" PreferredWidth=""40"" VerticalAlignment=""Center"" /><ProgressBar Minimum=""0"" Maximum=""100"" Value=""80"" /></DockPanel>"),
        });

        lib.Register(new UIScreenTemplate
        {
            Name = "Inventory Slot",
            Category = "HUD",
            Description = "Bordered icon placeholder",
            Xaml = Wrap(@"<Border BorderThickness=""1"" Padding=""4"" PreferredWidth=""64"" PreferredHeight=""64""><Image Width=""48"" Height=""48"" /></Border>"),
        });

        return lib;
    }

    private static string Wrap(string inner)
        => $"<Window xmlns=\"{MguiNs}\" xmlns:x=\"{XamlNs}\" Width=\"1280\" Height=\"720\" WindowStyle=\"None\" CanCloseWindow=\"False\" IsUserResizable=\"False\">{inner}</Window>";
}
