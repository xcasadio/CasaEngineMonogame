using System;
using System.Collections.Generic;
using System.Text;
using CasaEngine.Framework.UI;
using MGUI.Core.UI;

namespace CasaEngine.Demos.Demos;

/// <summary>
/// The demo browser beside the scene (ADR-0070): a tree of the demos by theme, the description of the selected demo
/// below it, and the handle the game drags to resize the browser. It lives on its own UI runtime, installed as the
/// game's window-level UI, so it survives demo changes.
/// <para/>
/// Loading (decision D2): a click on a demo, or Enter, loads it; the arrows only move the selection, which shows the
/// description. MGTreeView raises <see cref="MGTreeView.SelectionChanged"/> only when the selection changes, so a click
/// is taken from the leaf itself, which also catches a click on the demo already selected.
/// <para/>
/// Keyboard ownership (plan point P9): the browser takes the keyboard only while the pointer is over it. MGUI gives
/// focus by itself to the first focusable element on any key press and has no public way to take it back, so the game
/// arms the browser (its focusable elements become focusable) when the pointer enters it and disarms it (they stop being
/// focusable, which drops the focus) when the pointer leaves -- the mechanism the former demo info panel used.
/// <para/>
/// Its shell lives in `Content/Screens/demo-browser.xaml`; the tree items do not: there is one per demo, so the tree
/// IS the data and is built here, once.
/// </summary>
internal sealed class DemoBrowserScreen : XamlUIScreenBase
{
    /// <summary>One demo as the browser shows it.</summary>
    internal readonly record struct Entry(string Title, string Description, string Theme);

    private const string CurrentDemoMarker = "> ";

    private readonly IReadOnlyList<Entry> _entries;
    private readonly IReadOnlyList<string> _themeOrder;
    private readonly Action<int> _onDemoRequested;
    private readonly Action _onCollapseRequested;
    private int _currentIndex = -1;

    // ---- MGUI elements ----
    private MGTreeView _tree;
    private MGButton _collapseButton;
    private MGTextBlock _titleLabel;
    private MGTextBlock _themeLabel;
    private MGTextBlock _descriptionLabel;
    private MGElement[] _focusableElements;
    private bool _isKeyboardArmed;

    // ---- Tree, built once ----
    private MGTreeViewItem[] _leaves;
    private MGTextBlock[] _leafLabels;
    private MGTreeViewItem[] _themeItemOfDemo;
    private readonly Dictionary<MGTreeViewItem, int> _demoOfLeaf = new();
    private readonly Dictionary<MGTreeViewItem, string> _themeSummaries = new();

    // ---- IUIScreen ----
    public override UILayer Layer => UILayer.HUD;
    public override bool IsModal => false;

    /// <param name="entries">Every demo, in the order of the game's list (the index the game loads by).</param>
    /// <param name="themeOrder">The themes in the order of the tree; a theme without demos is left out.</param>
    /// <param name="onDemoRequested">Called with the index of the demo the player loads (click or Enter).</param>
    /// <param name="onCollapseRequested">Called when the player clicks the collapse button of the header.</param>
    public DemoBrowserScreen(
        IReadOnlyList<Entry> entries,
        IReadOnlyList<string> themeOrder,
        Action<int> onDemoRequested,
        Action onCollapseRequested)
        : base(DemoScreenXaml.Source("demo-browser.xaml"))
    {
        _entries = entries;
        _themeOrder = themeOrder;
        _onDemoRequested = onDemoRequested;
        _onCollapseRequested = onCollapseRequested;
    }

    protected override void OnWindowLoaded(MGWindow window)
    {
        _tree = FindControl<MGTreeView>("treeDemos");
        _collapseButton = FindControl<MGButton>("btnCollapse");
        _titleLabel = FindControl<MGTextBlock>("lblTitle");
        _themeLabel = FindControl<MGTextBlock>("lblTheme");
        _descriptionLabel = FindControl<MGTextBlock>("lblDescription");
        FindControl<MGTextBlock>("lblHeader").Text = $"Demos ({_entries.Count})";

        _collapseButton.AddCommandHandler((_, _) => _onCollapseRequested?.Invoke());
        BuildTree(window);
        _tree.SelectionChanged += (_, item) => ShowDescriptionOf(item);

        _focusableElements = new MGElement[] { _tree, _collapseButton };
        _isKeyboardArmed = true;
        SetKeyboardArmed(false);

        if (_currentIndex >= 0)
        {
            ApplyCurrentDemo();
        }
    }

    private void BuildTree(MGWindow window)
    {
        _leaves = new MGTreeViewItem[_entries.Count];
        _leafLabels = new MGTextBlock[_entries.Count];
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

                int demoIndex = i;
                var label = new MGTextBlock(window, _entries[i].Title, null, null, false);
                var leaf = new MGTreeViewItem(window) { Header = label };
                leaf.MouseHandler.LMBClickedInside += (_, _) => _onDemoRequested?.Invoke(demoIndex);
                themeItem.AddItem(leaf);

                _leaves[i] = leaf;
                _leafLabels[i] = label;
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
                    $"Demo '{_entries[i].Title}' has the theme '{_entries[i].Theme}', which is not in the browser's theme order.");
            }
        }
    }

    private void ShowDescriptionOf(MGTreeViewItem item)
    {
        if (item != null && _demoOfLeaf.TryGetValue(item, out int demoIndex))
        {
            var entry = _entries[demoIndex];
            _titleLabel.Text = entry.Title;
            _themeLabel.Text = entry.Theme;
            _descriptionLabel.Text = entry.Description;
        }
        else if (item != null && _themeSummaries.TryGetValue(item, out var summary))
        {
            _titleLabel.Text = ((MGTextBlock)item.Header).Text;
            _themeLabel.Text = $"{item.Items.Count} demos";
            _descriptionLabel.Text = summary;
        }
    }

    /// <summary>
    /// Marks the demo that is running, expands its theme, selects it (which shows its description) and scrolls to it.
    /// Called after every demo change; nothing here loads a demo.
    /// </summary>
    public void SetCurrentDemo(int index)
    {
        _currentIndex = index;
        if (_leaves != null)
        {
            ApplyCurrentDemo();
        }
    }

    private void ApplyCurrentDemo()
    {
        for (int i = 0; i < _leafLabels.Length; i++)
        {
            bool isCurrent = i == _currentIndex;
            _leafLabels[i].IsBold = isCurrent;
            _leafLabels[i].Text = isCurrent ? CurrentDemoMarker + _entries[i].Title : _entries[i].Title;
        }

        if (_currentIndex < 0 || _currentIndex >= _leaves.Length)
        {
            return;
        }

        _themeItemOfDemo[_currentIndex].IsExpanded = true;
        _tree.SelectItem(_leaves[_currentIndex]);
        _tree.ScrollIntoView(_leaves[_currentIndex]);
    }

    /// <summary>The demo the tree has selected, for Enter; false when a theme or nothing is selected.</summary>
    public bool TryGetSelectedDemo(out int index)
    {
        index = -1;
        return _tree?.SelectedItem != null && _demoOfLeaf.TryGetValue(_tree.SelectedItem, out index);
    }

    /// <summary>
    /// Arms the browser when the pointer is over it (its focusable elements accept focus and the tree takes it), and
    /// disarms it otherwise (nothing in it can keep the focus, so the scene gets the keyboard back). Plan point P9.
    /// </summary>
    public void SetKeyboardArmed(bool armed)
    {
        if (_focusableElements == null || armed == _isKeyboardArmed)
        {
            return;
        }

        _isKeyboardArmed = armed;
        for (int i = 0; i < _focusableElements.Length; i++)
        {
            _focusableElements[i].IsFocusable = armed;
        }

        if (armed)
        {
            _tree.Focus(KeyboardFocusSource.Pointer);
        }
    }

    /// <summary>Shows or hides the browser window without removing it from its stack.</summary>
    public void SetVisible(bool visible)
    {
        if (Window != null)
        {
            Window.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}
