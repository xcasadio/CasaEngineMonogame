using System;
using System.Collections.Generic;
using System.Text;
using CasaEngine.Framework.UI;
using MGUI.Core.UI;

namespace CasaEngine.Demos.Demos;

/// <summary>
/// The demos main screen (<c>ai-agent/tasks/demos-main-menu-tasks.md</c>, decision D3): the themes and their demos in a
/// tree on the left, the sheet of the selected demo on the right (title, theme, description), and the Launch and Quit
/// buttons. It lives in the view of the menu world and is rebuilt every time the player comes back to it (decision D1).
/// <para/>
/// Choosing: the arrows and the D-pad move the selection (MGUI focus navigation on the tree, which has the focus), and
/// the sheet follows it. Launching: the Launch button, a double click on a demo, or Enter and the A button, which the
/// game reads (<see cref="TryGetSelectedDemo"/>): MGTreeView turns them into expanding or collapsing the selected item and
/// raises nothing a screen could listen to. Quitting: only the Quit button (decision D4).
/// <para/>
/// Its shell lives in `Content/Screens/main-menu.xaml`; the tree items do not: there is one per demo, so the tree IS the
/// data and is built here, once.
/// </summary>
internal sealed class MainMenuScreen : XamlUIScreenBase
{
    /// <summary>One demo as the main screen shows it.</summary>
    internal readonly record struct Entry(string Title, string Description, string Theme);

    private readonly IReadOnlyList<Entry> _entries;
    private readonly IReadOnlyList<string> _themeOrder;
    private readonly int _initialDemoIndex;
    private readonly Action<int> _onLaunchRequested;
    private readonly Action _onQuitRequested;

    // ---- MGUI elements ----
    private MGTreeView _tree;
    private MGButton _launchButton;
    private MGButton _quitButton;
    private MGTextBlock _titleLabel;
    private MGTextBlock _themeLabel;
    private MGTextBlock _descriptionLabel;
    private MGTextBlock _statusLabel;

    // ---- Tree, built once ----
    private MGTreeViewItem[] _leaves;
    private MGTreeViewItem[] _themeItemOfDemo;
    private readonly Dictionary<MGTreeViewItem, int> _demoOfLeaf = new();
    private readonly Dictionary<MGTreeViewItem, string> _themeSummaries = new();

    // ---- IUIScreen ----
    public override UILayer Layer => UILayer.Menu;
    public override bool IsModal => false;

    /// <param name="entries">Every demo, in the order of the game's list (the index the game loads by).</param>
    /// <param name="themeOrder">The themes in the order of the tree; a theme without demos is left out.</param>
    /// <param name="initialDemoIndex">The demo selected when the screen opens: the one the player just left, or the first.</param>
    /// <param name="onLaunchRequested">Called with the index of the demo the player launches from the screen itself
    /// (Launch button, double click).</param>
    /// <param name="onQuitRequested">Called when the player clicks Quit.</param>
    public MainMenuScreen(
        IReadOnlyList<Entry> entries,
        IReadOnlyList<string> themeOrder,
        int initialDemoIndex,
        Action<int> onLaunchRequested,
        Action onQuitRequested)
        : base(DemoScreenXaml.Source("main-menu.xaml"))
    {
        _entries = entries;
        _themeOrder = themeOrder;
        _initialDemoIndex = initialDemoIndex;
        _onLaunchRequested = onLaunchRequested;
        _onQuitRequested = onQuitRequested;
    }

    protected override void OnWindowLoaded(MGWindow window)
    {
        // The MGUI built-in Dark theme for this window only, applied the way UIScreenLoader applies a named theme.
        window.Theme = new MGTheme(MGTheme.BuiltInTheme.Dark, window.Desktop.DefaultFontFamily);

        _tree = FindControl<MGTreeView>("treeDemos");
        _launchButton = FindControl<MGButton>("btnLaunch");
        _quitButton = FindControl<MGButton>("btnQuit");
        _titleLabel = FindControl<MGTextBlock>("lblTitle");
        _themeLabel = FindControl<MGTextBlock>("lblTheme");
        _descriptionLabel = FindControl<MGTextBlock>("lblDescription");
        _statusLabel = FindControl<MGTextBlock>("lblStatus");
        FindControl<MGTextBlock>("lblHeader").Text = $"CasaEngine demos ({_entries.Count})";

        BuildTree(window);
        _tree.SelectionChanged += (_, item) => ShowSheetOf(item);
        _launchButton.AddCommandHandler((_, _) =>
        {
            if (TryGetSelectedDemo(out int demoIndex))
            {
                _onLaunchRequested?.Invoke(demoIndex);
            }
        });
        _quitButton.AddCommandHandler((_, _) => _onQuitRequested?.Invoke());

        SelectDemo(_initialDemoIndex);
        _tree.Focus(KeyboardFocusSource.Programmatic);
    }

    private void BuildTree(MGWindow window)
    {
        _leaves = new MGTreeViewItem[_entries.Count];
        _themeItemOfDemo = new MGTreeViewItem[_entries.Count];

        foreach (var theme in _themeOrder)
        {
            MGTreeViewItem themeItem = null;
            var titles = new StringBuilder();
            int count = 0;

            for (int i = 0; i < _entries.Count; i++)
            {
                if (!string.Equals(_entries[i].Theme, theme, StringComparison.Ordinal))
                {
                    continue;
                }

                themeItem ??= new MGTreeViewItem(window) { Header = new MGTextBlock(window, theme, null, null, false) };

                // A double click launches the demo. MGTreeView.ItemDoubleClicked fires only for items that have children,
                // so the leaf listens to its own double click, as the former demo browser listened to its click.
                int demoIndex = i;
                var leaf = new MGTreeViewItem(window) { Header = new MGTextBlock(window, _entries[i].Title, null, null, false) };
                leaf.MouseHandler.LMBDoubleClickedInside += (_, _) => _onLaunchRequested?.Invoke(demoIndex);
                themeItem.AddItem(leaf);

                _leaves[i] = leaf;
                _themeItemOfDemo[i] = themeItem;
                _demoOfLeaf[leaf] = i;
                titles.Append(count == 0 ? string.Empty : "\n").Append(_entries[i].Title);
                count++;
            }

            if (themeItem != null)
            {
                _tree.AddItem(themeItem);
                _themeSummaries[themeItem] = titles.ToString();
            }
        }

        for (int i = 0; i < _entries.Count; i++)
        {
            if (_leaves[i] == null)
            {
                throw new InvalidOperationException(
                    $"Demo '{_entries[i].Title}' has the theme '{_entries[i].Theme}', which is not in the main screen's theme order.");
            }
        }
    }

    private void ShowSheetOf(MGTreeViewItem item)
    {
        if (item != null && _demoOfLeaf.TryGetValue(item, out int demoIndex))
        {
            var entry = _entries[demoIndex];
            _titleLabel.Text = entry.Title;
            _themeLabel.Text = entry.Theme;
            _descriptionLabel.Text = entry.Description;
            _launchButton.IsEnabled = true;
        }
        else if (item != null && _themeSummaries.TryGetValue(item, out var summary))
        {
            _titleLabel.Text = ((MGTextBlock)item.Header).Text;
            _themeLabel.Text = $"{item.Items.Count} demos";
            _descriptionLabel.Text = summary;
            _launchButton.IsEnabled = false;
        }
    }

    /// <summary>Selects a demo, expands its theme and scrolls to it; the sheet follows. An index out of range selects the
    /// first demo.</summary>
    private void SelectDemo(int index)
    {
        if (_leaves.Length == 0)
        {
            return;
        }

        if (index < 0 || index >= _leaves.Length)
        {
            index = 0;
        }

        _themeItemOfDemo[index].IsExpanded = true;
        _tree.SelectItem(_leaves[index]);
        _tree.ScrollIntoView(_leaves[index]);
    }

    /// <summary>The demo the tree has selected, for Enter and the A button; false when a theme or nothing is selected.</summary>
    public bool TryGetSelectedDemo(out int index)
    {
        index = -1;
        return _tree?.SelectedItem != null && _demoOfLeaf.TryGetValue(_tree.SelectedItem, out index);
    }

    /// <summary>Shows that a demo is loading; the game loads it on the next update (it shows for one frame).</summary>
    public void ShowLoading(int demoIndex)
    {
        if (_statusLabel != null && demoIndex >= 0 && demoIndex < _entries.Count)
        {
            _statusLabel.Text = $"Loading {_entries[demoIndex].Title}...";
            _launchButton.IsEnabled = false;
            _quitButton.IsEnabled = false;
        }
    }
}
