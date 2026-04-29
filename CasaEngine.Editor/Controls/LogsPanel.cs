using System;
using System.Linq;
using CasaEngine.Core.Logging;
using CasaEngine.Editor.Log;
using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using MGUI.Shared.Input.Mouse;
using MGUI.Shared.Helpers;
using Microsoft.Xna.Framework;
using Thickness = MonoGame.Extended.Thickness;
using HorizontalAlignment = MGUI.Core.UI.HorizontalAlignment;
using VerticalAlignment = MGUI.Core.UI.VerticalAlignment;

namespace CasaEngine.Editor.Controls;

/// <summary>
/// A dockable panel that shows real-time log output captured by a <see cref="LoggerEditor"/>.
/// Features a verbosity filter combo box, per-line copy, and actions for visible logs.
/// </summary>
public class LogsPanel
{
    // ─────────────────────────────────────────────────────────────────────────
    // Constants
    // ─────────────────────────────────────────────────────────────────────────

    private const string FilterAll = "All";

    private static readonly string[] FilterItems =
    [
        FilterAll,
        nameof(LogVerbosity.Trace),
        nameof(LogVerbosity.Debug),
        nameof(LogVerbosity.Info),
        nameof(LogVerbosity.Warning),
        nameof(LogVerbosity.Error),
    ];

    // ─────────────────────────────────────────────────────────────────────────
    // Fields
    // ─────────────────────────────────────────────────────────────────────────

    private readonly MGWindow _window;
    private readonly LoggerEditor _logger;

    private MGListBox<LogEntry> _listBox = null!;
    private MGComboBox<string> _filterCombo = null!;
    private MGButton _copyAllButton = null!;
    private MGElement? _rootContent;
    private bool _isSubscribed;

    private LogVerbosity? _activeFilter; // null = show all

    // ─────────────────────────────────────────────────────────────────────────
    // Constructor
    // ─────────────────────────────────────────────────────────────────────────

    public LogsPanel(MGWindow window, LoggerEditor logger)
    {
        _window = window;
        _logger = logger;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Public API
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds and returns the root MGUI element for this panel.
    /// Call once and use as a <c>DockPanelNode.ContentFactory</c> result.
    /// </summary>
    public MGElement CreateContent()
    {
        if (_rootContent != null)
        {
            RefreshList();
            return _rootContent;
        }

        // ── Filter toolbar ────────────────────────────────────────────────
        _filterCombo = new MGComboBox<string>(_window)
        {
            MinWidth = 110,
        };
        _filterCombo.DropdownItemTemplate = item =>
        {
            var btn = _filterCombo.CreateDefaultDropdownButton();
            btn.SetContent(item);
            return btn;
        };
        _filterCombo.SelectedItemTemplate = item => new MGTextBlock(_window, item)
        {
            Padding = new Thickness(4, 1, 4, 1),
            VerticalAlignment = VerticalAlignment.Center,
        };
        _filterCombo.SetItemsSource(FilterItems);
        _filterCombo.SelectedItem = FilterAll;
        _filterCombo.SelectedItemChanged += OnFilterChanged;

        _copyAllButton = new MGButton(_window, _ => CopyVisibleLogsToClipboard())
        {
            Padding = new Thickness(6, 2, 6, 2),
        };
        _copyAllButton.SetContent(new MGTextBlock(_window, "Copy all"));

        var clearButton = new MGButton(_window, _ => ClearLogs())
        {
            Padding = new Thickness(6, 2, 6, 2),
        };
        if (EditorIcons.Trash != null)
        {
            var img = new MGImage(_window, EditorIcons.AsImage(EditorIcons.Trash)!, Stretch: Stretch.Uniform)
            {
                PreferredWidth  = 20,
                PreferredHeight = 20,
            };
            clearButton.SetContent(img);
        }
        else
        {
            clearButton.SetContent(new MGTextBlock(_window, "Clear"));
        }

        var toolbar = new MGStackPanel(_window, Orientation.Horizontal)
        {
            Margin = new Thickness(4, 2, 4, 2),
            Spacing = 6,
            VerticalAlignment = VerticalAlignment.Center,
        };
        toolbar.TryAddChild(new MGTextBlock(_window, "[b]Logs[/b]") { VerticalAlignment = VerticalAlignment.Center });
        toolbar.TryAddChild(_filterCombo);
        toolbar.TryAddChild(_copyAllButton);
        toolbar.TryAddChild(clearButton);

        // ── Log list ──────────────────────────────────────────────────────
        _listBox = new MGListBox<LogEntry>(_window)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            ItemTemplate = BuildEntryTemplate,
            VirtualizationMode = ListBoxVirtualizationMode.Never,
        };
        _listBox.ScrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
        _listBox.ScrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        _listBox.MouseHandler.RMBReleasedInside += OnListBoxRightClick;

        // ── Outer layout ──────────────────────────────────────────────────
        var panel = new MGDockPanel(_window);
        panel.TryAddChild(toolbar, Dock.Top);
        panel.TryAddChild(_listBox, Dock.Top); // fill
        _rootContent = panel;

        // ── Subscribe to new entries ──────────────────────────────────────
        if (!_isSubscribed)
        {
            _logger.EntryAdded += OnEntryAdded;
            _isSubscribed = true;
        }

        // Populate with any entries already present
        RefreshList();

        return _rootContent;
    }

    public void Refresh()
    {
        if (_rootContent == null)
        {
            return;
        }

        RefreshList();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Item template
    // ─────────────────────────────────────────────────────────────────────────

    private MGElement BuildEntryTemplate(LogEntry entry)
    {
        return new MGTextBlock(_window, FormatMarkupEntry(entry))
        {
            Padding = new Thickness(4, 1, 4, 1),
            WrapText = false,
        };
    }

    private MGContextMenu BuildLogEntryContextMenu(LogEntry entry)
    {
        var menu = new MGContextMenu(_window);
        menu.AddButton("Copy line", _ => CopyToClipboard(FormatClipboardEntry(entry)));
        return menu;
    }

    private string FormatMarkupEntry(LogEntry entry)
    {
        var colorTag = VerbosityColor(entry.Verbosity);
        var ts = entry.Timestamp.ToString("HH:mm:ss");
        return $"[c={colorTag}]{ts} [{entry.Verbosity,-7}] {EscapeMarkup(entry.Message)}[/c]";
    }

    private static string FormatClipboardEntry(LogEntry entry) =>
        $"{entry.Timestamp:HH:mm:ss} [{entry.Verbosity,-7}] {entry.Message}";

    private static string VerbosityColor(LogVerbosity v) => v switch
    {
        LogVerbosity.Trace   => "Gray",
        LogVerbosity.Debug   => "LightGreen",
        LogVerbosity.Info    => "White",
        LogVerbosity.Warning => "Yellow",
        LogVerbosity.Error   => "Red",
        _                    => "White",
    };

    private static string EscapeMarkup(string msg) => msg.Replace("[", "[[");

    // ─────────────────────────────────────────────────────────────────────────
    // Event handlers
    // ─────────────────────────────────────────────────────────────────────────

    private void OnEntryAdded(object? sender, LogEntry entry)
    {
        if (PassesFilter(entry))
        {
            AppendEntry(entry);
        }
    }

    private void OnFilterChanged(object? sender, EventArgs<string> e)
    {
        _activeFilter = _filterCombo.SelectedItem == FilterAll
            ? null
            : Enum.Parse<LogVerbosity>(_filterCombo.SelectedItem);
        RefreshList();
    }

    private void OnListBoxRightClick(object? sender, BaseMouseReleasedEventArgs e)
    {
        if (e.IsHandled || !TryGetEntryAtPosition(e.Position, out var entry))
        {
            return;
        }

        _listBox.SelectItem(entry, true);
        var menu = BuildLogEntryContextMenu(entry);
        if (menu.TryOpenContextMenu(e.Position))
        {
            e.SetHandledBy(menu, false);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Actions
    // ─────────────────────────────────────────────────────────────────────────

    private void ClearLogs()
    {
        _logger.Clear();
        _listBox.SetItemsSource(Array.Empty<LogEntry>());
        UpdateToolbarState();
    }

    private void CopyVisibleLogsToClipboard()
    {
        var visibleEntries = GetVisibleEntries();
        if (visibleEntries.Length == 0)
        {
            return;
        }

        CopyToClipboard(string.Join(Environment.NewLine, visibleEntries.Select(FormatClipboardEntry)));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // List management
    // ─────────────────────────────────────────────────────────────────────────

    private void RefreshList()
    {
        var filtered = GetVisibleEntries();
        _listBox.SetItemsSource(filtered);
        UpdateToolbarState();
        ScrollToBottom();
    }

    private void AppendEntry(LogEntry entry)
    {
        // Rebuild source (light-weight since log count is typically moderate)
        var filtered = GetVisibleEntries();
        _listBox.SetItemsSource(filtered);
        UpdateToolbarState();
        ScrollToBottom();
    }

    private void ScrollToBottom()
    {
        // Defer one frame so layout finishes before scrolling
        _listBox.ScrollViewer.QueueScrollToBottom();
    }

    private void UpdateToolbarState()
    {
        _copyAllButton.IsEnabled = GetVisibleEntries().Length > 0;
    }

    private LogEntry[] GetVisibleEntries() => _logger.Entries.Where(PassesFilter).ToArray();

    private bool TryGetEntryAtPosition(Point screenPosition, out LogEntry entry)
    {
        var unscaledPosition = _listBox.ConvertCoordinateSpace(CoordinateSpace.Screen, CoordinateSpace.UnscaledScreen, screenPosition);
        foreach (var item in _listBox.ListBoxItems)
        {
            var bounds = item.ContentPresenter.ActualLayoutBounds;
            if (!bounds.IsEmpty && bounds.ContainsInclusive(unscaledPosition))
            {
                entry = item.Data;
                return true;
            }
        }

        entry = null!;
        return false;
    }

    private static void CopyToClipboard(string text)
    {
        try
        {
            System.Windows.Forms.Clipboard.SetText(text);
        }
        catch
        {
            // clipboard access can fail in some environments; silently ignore
        }
    }

    private bool PassesFilter(LogEntry entry) =>
        _activeFilter == null || entry.Verbosity == _activeFilter;
}
