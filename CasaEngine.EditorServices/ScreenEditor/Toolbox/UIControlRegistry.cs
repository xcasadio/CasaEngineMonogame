namespace CasaEngine.EditorServices.ScreenEditor.Toolbox;

/// <summary>
/// Central catalogue of UI control types that can be inserted via the toolbox.
/// </summary>
public sealed class UIControlRegistry
{
    // ─── Singleton ────────────────────────────────────────────────────────

    private static UIControlRegistry? _default;

    /// <summary>Default registry pre-populated with common MGUI controls.</summary>
    public static UIControlRegistry Default => _default ??= CreateDefault();

    // ─── State ────────────────────────────────────────────────────────────

    private readonly List<UIControlRegistryEntry> _entries = new();

    // ─── Mutation ─────────────────────────────────────────────────────────

    /// <summary>Registers a control entry.</summary>
    public void Register(UIControlRegistryEntry entry) => _entries.Add(entry);

    // ─── Query ────────────────────────────────────────────────────────────

    /// <summary>Returns all registered entries.</summary>
    public IReadOnlyList<UIControlRegistryEntry> GetAll() => _entries;

    /// <summary>Returns entries grouped by <see cref="UIControlRegistryEntry.Category"/>.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<UIControlRegistryEntry>> GetByCategory()
        => _entries
            .GroupBy(e => e.Category)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<UIControlRegistryEntry>)g.ToList());

    /// <summary>Returns the entry for a given <paramref name="controlType"/>, or null.</summary>
    public UIControlRegistryEntry? TryGet(string controlType)
        => _entries.FirstOrDefault(e => e.ControlType == controlType);

    // ─── Default population ───────────────────────────────────────────────

    private static UIControlRegistry CreateDefault()
    {
        var r = new UIControlRegistry();

        // ── Layout ───────────────────────────────────────────────────────
        r.Register(new("StackPanel",  "Stack Panel",   "Layout",
            new Dictionary<string, string?> { ["Orientation"] = "Vertical" }));

        r.Register(new("DockPanel",   "Dock Panel",    "Layout"));

        r.Register(new("Grid",        "Grid",          "Layout",
            new Dictionary<string, string?> { ["RowDefinitions"] = "1*,1*", ["ColumnDefinitions"] = "1*,1*" }));

        r.Register(new("UniformGrid", "Uniform Grid",  "Layout",
            new Dictionary<string, string?> { ["Rows"] = "2", ["Columns"] = "2" }));

        r.Register(new("ScrollViewer","Scroll Viewer", "Layout"));

        r.Register(new("Border",      "Border",        "Layout",
            new Dictionary<string, string?> { ["BorderThickness"] = "1", ["Padding"] = "4" }));

        r.Register(new("Expander",    "Expander",      "Layout",
            new Dictionary<string, string?> { ["Header"] = "Section" }));

        // ── Controls ─────────────────────────────────────────────────────
        r.Register(new("Button",      "Button",        "Controls",
            new Dictionary<string, string?> { ["Content"] = "Button" }));

        r.Register(new("TextBlock",   "Text Block",    "Controls",
            new Dictionary<string, string?> { ["Text"] = "Text" }));

        r.Register(new("TextBox",     "Text Box",      "Controls",
            new Dictionary<string, string?> { ["Text"] = string.Empty, ["Width"] = "150" }));

        r.Register(new("CheckBox",    "Check Box",     "Controls",
            new Dictionary<string, string?> { ["Content"] = "Check Box" }));

        r.Register(new("RadioButton", "Radio Button",  "Controls",
            new Dictionary<string, string?> { ["Content"] = "Option" }));

        r.Register(new("ComboBox",    "Combo Box",     "Controls",
            new Dictionary<string, string?> { ["Width"] = "150" }));

        r.Register(new("ListBox",     "List Box",      "Controls",
            new Dictionary<string, string?> { ["Width"] = "150", ["Height"] = "100" }));

        r.Register(new("Image",       "Image",         "Controls",
            new Dictionary<string, string?> { ["Width"] = "64", ["Height"] = "64" }));

        // ── Input ────────────────────────────────────────────────────────
        r.Register(new("Slider",      "Slider",        "Input",
            new Dictionary<string, string?> { ["Minimum"] = "0", ["Maximum"] = "100", ["Value"] = "50" }));

        r.Register(new("ProgressBar", "Progress Bar",  "Input",
            new Dictionary<string, string?> { ["Minimum"] = "0", ["Maximum"] = "100", ["Value"] = "50" }));

        return r;
    }
}
