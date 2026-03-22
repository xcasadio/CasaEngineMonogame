namespace CasaEngine.EditorServices.ScreenEditor;

/// <summary>
/// Provides mock data used to populate UI controls at design time.
/// When <see cref="UIDesignModeContext.IsDesignTime"/> is <c>true</c>,
/// systems can query this class instead of real data sources.
/// </summary>
public static class UIScreenMockDataContext
{
    private static readonly Dictionary<string, string> _textValues = new(StringComparer.Ordinal)
    {
        // Common design-time placeholders
        ["Title"]       = "Screen Title",
        ["Subtitle"]    = "Subtitle goes here",
        ["Header"]      = "Section Header",
        ["Description"] = "Lorem ipsum dolor sit amet, consectetur adipiscing elit.",
        ["Label"]       = "Label",
        ["Value"]       = "42",
        ["Name"]        = "Player One",
        ["Score"]       = "12,500",
        ["Health"]      = "80 / 100",
        ["Level"]       = "Level 7",
        ["Currency"]    = "1,000 Gold",
        ["Status"]      = "Online",
        ["Error"]       = "Something went wrong.",
        ["Placeholder"] = "Type here...",
    };

    /// <summary>
    /// Returns a mock text value for the given key, or a default placeholder
    /// if no specific value is registered.
    /// </summary>
    public static string GetText(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return "(mock)";
        return _textValues.TryGetValue(key, out var value) ? value : $"({key})";
    }

    /// <summary>
    /// Registers a custom mock value. Overwrites any existing entry.
    /// </summary>
    public static void Register(string key, string value)
    {
        if (string.IsNullOrWhiteSpace(key)) return;
        _textValues[key] = value ?? string.Empty;
    }

    /// <summary>
    /// Returns a read-only view of all registered mock values.
    /// </summary>
    public static IReadOnlyDictionary<string, string> All => _textValues;

    /// <summary>
    /// Generates a list of mock item strings for repeating UI elements (e.g. list boxes).
    /// </summary>
    public static IEnumerable<string> GetListItems(int count = 5, string prefix = "Item")
    {
        for (int i = 1; i <= count; i++)
        {
            yield return $"{prefix} {i}";
        }
    }
}
