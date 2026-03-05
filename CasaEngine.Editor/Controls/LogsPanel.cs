using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CasaEngine.Core.Log;
using CasaEngine.Editor.Log;
using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using MGUI.Shared.Helpers;
using Microsoft.Xna.Framework;
using Thickness = MonoGame.Extended.Thickness;
using HorizontalAlignment = MGUI.Core.UI.HorizontalAlignment;
using VerticalAlignment = MGUI.Core.UI.VerticalAlignment;

namespace CasaEngine.Editor.Controls;

/// <summary>
/// A dockable panel that shows real-time log output captured by a <see cref="LoggerEditor"/>.
/// Features a verbosity filter combo box and a "Clear" button.
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
    private MGScrollViewer _scrollViewer = null!;
    private MGComboBox<string> _filterCombo = null!;

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

        var clearButton = new MGButton(_window, _ => ClearLogs())
        {
            Padding = new Thickness(6, 2, 6, 2),
        };
        clearButton.SetContent(new MGTextBlock(_window, "🗑 Clear"));

        var toolbar = new MGStackPanel(_window, Orientation.Horizontal)
        {
            Margin = new Thickness(4, 2, 4, 2),
            Spacing = 6,
            VerticalAlignment = VerticalAlignment.Center,
        };
        toolbar.TryAddChild(new MGTextBlock(_window, "[b]Logs[/b]") { VerticalAlignment = VerticalAlignment.Center });
        toolbar.TryAddChild(_filterCombo);
        toolbar.TryAddChild(clearButton);

        // ── Log list ──────────────────────────────────────────────────────
        _listBox = new MGListBox<LogEntry>(_window)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            ItemTemplate = BuildEntryTemplate,
        };

        _scrollViewer = new MGScrollViewer(_window);
        _scrollViewer.SetContent(_listBox);

        // ── Outer layout ──────────────────────────────────────────────────
        var panel = new MGDockPanel(_window);
        panel.TryAddChild(toolbar, Dock.Top);
        panel.TryAddChild(_scrollViewer, Dock.Top); // fill

        // ── Subscribe to new entries ──────────────────────────────────────
        _logger.EntryAdded += OnEntryAdded;

        // Populate with any entries already present
        RefreshList();

        return panel;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Item template
    // ─────────────────────────────────────────────────────────────────────────

    private MGElement BuildEntryTemplate(LogEntry entry)
    {
        var colorTag = VerbosityColor(entry.Verbosity);
        var ts = entry.Timestamp.ToString("HH:mm:ss");
        var text = $"[c={colorTag}]{ts} [{entry.Verbosity,-7}] {EscapeMarkup(entry.Message)}[/c]";

        return new MGTextBlock(_window, text)
        {
            Padding = new Thickness(4, 1, 4, 1),
            WrapText = false,
        };
    }

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

    // ─────────────────────────────────────────────────────────────────────────
    // Actions
    // ─────────────────────────────────────────────────────────────────────────

    private void ClearLogs()
    {
        _logger.Clear();
        _listBox.SetItemsSource(Array.Empty<LogEntry>());
    }

    // ─────────────────────────────────────────────────────────────────────────
    // List management
    // ─────────────────────────────────────────────────────────────────────────

    private void RefreshList()
    {
        var filtered = _logger.Entries
            .Where(PassesFilter)
            .ToList();
        _listBox.SetItemsSource(filtered);
        ScrollToBottom();
    }

    private void AppendEntry(LogEntry entry)
    {
        // Rebuild source (light-weight since log count is typically moderate)
        var filtered = _logger.Entries.Where(PassesFilter).ToList();
        _listBox.SetItemsSource(filtered);
        ScrollToBottom();
    }

    private void ScrollToBottom()
    {
        // Defer one frame so layout finishes before scrolling
        _scrollViewer.VerticalOffset = _scrollViewer.MaxVerticalOffset;
    }

    private bool PassesFilter(LogEntry entry) =>
        _activeFilter == null || entry.Verbosity == _activeFilter;
}
