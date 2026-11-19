using CasaEngine.EditorServices.ScreenEditor.DocumentModel;

namespace CasaEngine.EditorServices.ScreenEditor.Naming;

/// <summary>
/// Default element names given by the screen editor to the nodes it creates, duplicates or pastes.
/// A name is the type prefix followed by a number (<c>img1</c>, <c>btn2</c>). The prefixes complete the
/// convention of <c>screen-authoring-conventions.md</c>. Comparisons are ordinal, like the name index of
/// <c>MGWindow</c>, so two names that differ only by case are distinct.
/// </summary>
public static class UIScreenNodeNames
{
    private const string MguiTypePrefix = "MG";

    private static readonly Dictionary<string, string> PrefixByType = new(StringComparer.Ordinal)
    {
        ["Button"] = "btn",
        ["TextBlock"] = "lbl",
        ["Image"] = "img",
        ["TextBox"] = "inp",
        ["ListBox"] = "lst",
        ["ComboBox"] = "cmb",
        ["CheckBox"] = "chk",
        ["RadioButton"] = "rdo",
        ["Slider"] = "sld",
        ["ProgressBar"] = "prg",
        ["Window"] = "wnd",
        ["StackPanel"] = "pnl",
        ["DockPanel"] = "pnl",
        ["Grid"] = "pnl",
        ["UniformGrid"] = "pnl",
        ["ScrollViewer"] = "pnl",
        ["Border"] = "pnl",
        ["Expander"] = "pnl",
        ["OverlayPanel"] = "pnl",
    };

    /// <summary>
    /// Returns the name prefix of a control type, read with or without the <c>MG</c> prefix
    /// (<c>MGStackPanel</c> and <c>StackPanel</c> both give <c>pnl</c>). A type missing from the table
    /// gives its name without <c>MG</c>, first letter in lower case.
    /// </summary>
    public static string GetPrefix(string controlType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(controlType);

        var type = StripMguiPrefix(controlType);
        if (PrefixByType.TryGetValue(type, out var prefix))
        {
            return prefix;
        }

        return char.ToLowerInvariant(type[0]) + type[1..];
    }

    /// <summary>
    /// Returns the prefix followed by the smallest integer n >= 1 that no node of the document uses
    /// and that is not in <paramref name="reserved"/> (names not inserted yet).
    /// </summary>
    public static string CreateDefaultName(
        UIScreenDocument document,
        string controlType,
        IReadOnlySet<string>? reserved = null)
    {
        ArgumentNullException.ThrowIfNull(document);

        var used = CollectNames(document);
        if (reserved != null)
        {
            used.UnionWith(reserved);
        }

        return NextFreeName(GetPrefix(controlType), 1, used);
    }

    /// <summary>
    /// Makes the names of a detached subtree unique in the document, in document order. A free name is kept;
    /// a taken one loses its trailing digits and gets the smallest n >= 2 that is free (<c>btnPlay</c> gives
    /// <c>btnPlay2</c>); a node without a name gets a default name. Call it before inserting the subtree.
    /// </summary>
    /// <returns>Every name of the subtree after the call, to reserve them for a wrapper created next.</returns>
    public static IReadOnlySet<string> AssignUniqueNames(UIScreenDocument document, UIScreenNode subtreeRoot)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(subtreeRoot);

        var used = CollectNames(document);
        var subtreeNames = new HashSet<string>(StringComparer.Ordinal);
        AssignUniqueNames(subtreeRoot, used, subtreeNames);
        return subtreeNames;
    }

    private static void AssignUniqueNames(UIScreenNode node, HashSet<string> used, HashSet<string> subtreeNames)
    {
        string name;
        if (string.IsNullOrEmpty(node.Name))
        {
            name = NextFreeName(GetPrefix(node.ControlType), 1, used);
        }
        else if (!used.Contains(node.Name))
        {
            name = node.Name;
        }
        else
        {
            var stem = node.Name.TrimEnd('0', '1', '2', '3', '4', '5', '6', '7', '8', '9');
            name = NextFreeName(stem.Length > 0 ? stem : GetPrefix(node.ControlType), 2, used);
        }

        node.Name = name;
        used.Add(name);
        subtreeNames.Add(name);

        foreach (var child in node.Children)
        {
            AssignUniqueNames(child, used, subtreeNames);
        }
    }

    private static string NextFreeName(string stem, int start, HashSet<string> used)
    {
        for (var n = start; ; n++)
        {
            var candidate = stem + n;
            if (!used.Contains(candidate))
            {
                return candidate;
            }
        }
    }

    private static HashSet<string> CollectNames(UIScreenDocument document)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        if (document.Root != null)
        {
            CollectNames(document.Root, names);
        }

        return names;
    }

    private static void CollectNames(UIScreenNode node, HashSet<string> names)
    {
        if (!string.IsNullOrEmpty(node.Name))
        {
            names.Add(node.Name);
        }

        foreach (var child in node.Children)
        {
            CollectNames(child, names);
        }
    }

    private static string StripMguiPrefix(string controlType) =>
        controlType.Length > MguiTypePrefix.Length
        && controlType.StartsWith(MguiTypePrefix, StringComparison.Ordinal)
        && char.IsUpper(controlType[MguiTypePrefix.Length])
            ? controlType[MguiTypePrefix.Length..]
            : controlType;
}
