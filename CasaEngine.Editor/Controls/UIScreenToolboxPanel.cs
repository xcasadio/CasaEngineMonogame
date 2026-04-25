using System;
using CasaEngine.EditorServices.ScreenEditor.DocumentModel;
using CasaEngine.EditorServices.ScreenEditor.Toolbox;
using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using MonoGame.Extended;
using HorizontalAlignment = MGUI.Core.UI.HorizontalAlignment;

namespace CasaEngine.Editor.Controls;

/// <summary>
/// Dockable toolbox panel. Shows categorised control buttons that the user
/// can click to request insertion of a new control into the document.
/// </summary>
public sealed class UIScreenToolboxPanel
{
    private readonly MGWindow _window;
    private readonly UIControlRegistry _registry;

    private MGDockPanel? _root;
    private UIScreenDocument? _document;

    // ─────────────────────────────────────────────────────────────────────
    //  Events
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Fired when the user clicks a control button in the toolbox.
    /// The argument is the <see cref="UIControlRegistryEntry"/> to insert.
    /// </summary>
    public event Action<UIControlRegistryEntry>? ControlRequested;

    // ─────────────────────────────────────────────────────────────────────
    //  Constructor
    // ─────────────────────────────────────────────────────────────────────

    public UIScreenToolboxPanel(MGWindow window)
        : this(window, UIControlRegistry.Default)
    {
    }

    public UIScreenToolboxPanel(MGWindow window, UIControlRegistry registry)
    {
        _window = window;
        _registry = registry;
    }

    // ─────────────────────────────────────────────────────────────────────
    //  Public API
    // ─────────────────────────────────────────────────────────────────────

    public void SetDocument(UIScreenDocument? document)
    {
        _document = document;
        UpdateButtonStates();
    }

    public MGElement CreateContent()
    {
        if (_root != null)
        {
            return _root;
        }

        var header = new MGTextBlock(_window, "[b]Toolbox[/b]")
        {
            Margin = new Thickness(8, 6, 8, 4),
        };

        // Build categorised list into a StackPanel inside a ScrollViewer
        var outerStack = new MGStackPanel(_window, Orientation.Vertical)
        {
            Spacing = 4,
            Margin = new Thickness(4),
        };

        var grouped = _registry.GetByCategory();
        foreach (var (category, entries) in grouped)
        {
            // Category header
            outerStack.TryAddChild(new MGTextBlock(_window, $"[b]{EscapeMarkup(category)}[/b]")
            {
                Margin = new Thickness(2, 6, 2, 2),
                Opacity = 0.8f,
            });

            // Buttons — wrap in a WrapPanel-like horizontal stack (using StackPanel + forced wrap via MGUniformGrid)
            var buttonGrid = new MGStackPanel(_window, Orientation.Vertical) { Spacing = 2 };
            foreach (var entry in entries)
            {
                var btn = new MGButton(_window, _ => OnControlButtonClicked(entry))
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Padding = new Thickness(4, 2, 4, 2),
                };
                btn.SetContent(new MGTextBlock(_window, EscapeMarkup(entry.DisplayName))
                {
                    HorizontalAlignment = HorizontalAlignment.Left,
                });
                buttonGrid.TryAddChild(btn);
            }

            outerStack.TryAddChild(buttonGrid);
        }

        var scrollViewer = new MGScrollViewer(_window);
        scrollViewer.SetContent(outerStack);

        _root = new MGDockPanel(_window);
        _root.TryAddChild(header, Dock.Top);
        _root.TryAddChild(scrollViewer, Dock.Top);

        return _root;
    }

    // ─────────────────────────────────────────────────────────────────────
    //  Internals
    // ─────────────────────────────────────────────────────────────────────

    private void OnControlButtonClicked(UIControlRegistryEntry entry)
    {
        if (_document == null)
        {
            return;
        }

        ControlRequested?.Invoke(entry);
    }

    private void UpdateButtonStates()
    {
        // Enabled state for buttons is handled by checking _document != null in click handler.
        // A future enhancement could grey-out buttons when no document is open.
    }

    private static string EscapeMarkup(string value)
        => value.Replace("[", "\\[").Replace("]", "\\]");
}
