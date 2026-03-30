using System;
using System.Collections.Generic;
using CasaEngine.Framework.GUI;
using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using MGUI.Core.UI.Containers;
using MonoGame.Extended;
using Microsoft.Xna.Framework;

namespace CasaEngine.Demos.Demos;

/// <summary>
/// HUD-layer overlay window showing the current demo's title and description,
/// plus a clickable list of all available demos for navigation.
///
/// Managed by <see cref="DemosGame"/>. Stays on the ScreenStack for the lifetime
/// of each demo; a new instance is created on every demo change.
///
/// Toggle visibility with <see cref="SetVisible"/>.
/// </summary>
internal sealed class DemoInfoScreen : UIScreenBase
{
    // ---- Data ----
    private readonly IReadOnlyList<(string Title, string Description)> _demoEntries;
    private int _currentIndex;
    private readonly Action<int> _onDemoSelected;

    // ---- MGUI elements ----
    private MGWindow?    _window;
    private MGTextBlock? _titleLabel;
    private MGTextBlock? _descLabel;
    private MGButton[]?  _demoButtons;
    private bool         _isKeyboardNavigationArmed;

    // ---- IUIScreen ----
    public override UILayer Layer   => UILayer.HUD;
    public override bool    IsModal => false;

    // ---- Constructor ----

    /// <param name="demoEntries">Ordered list of (Title, Description) for every demo.</param>
    /// <param name="currentIndex">Index of the demo currently active.</param>
    /// <param name="onDemoSelected">Callback invoked when the player clicks a demo entry.</param>
    public DemoInfoScreen(
        IReadOnlyList<(string Title, string Description)> demoEntries,
        int  currentIndex,
        Action<int> onDemoSelected)
    {
        _demoEntries     = demoEntries;
        _currentIndex    = currentIndex;
        _onDemoSelected  = onDemoSelected;
    }

    // ---- Build UI ----

    protected override void OnInitialize(UIRoot root)
    {
        var bounds = root.Desktop.ValidScreenBounds;
        int winW = 300;
        // Cap height to the available viewport so the window is never taller than the screen.
        // This matters for split-screen demos where each viewport is a fraction of the back-buffer.
        int winH = Math.Min(440, bounds.Height - 20);
        int x = bounds.Width - winW - 10;
        int y = 10;

        _window = new MGWindow(root.Desktop, x, y, winW, winH)
        {
            TitleText       = "Demo Navigator",
            IsUserResizable = false,
        };
        _window.Padding = new Thickness(8);
        _window.BackgroundBrush.NormalValue = new MGSolidFillBrush(new Color(0, 0, 0, 200));

        // ---- Outer vertical stack ----
        var outer = new MGStackPanel(_window, Orientation.Vertical) { Spacing = 6 };

        // Title of current demo
        _titleLabel = new MGTextBlock(_window,
            $"[b][color=white]{Escape(_demoEntries[_currentIndex].Title)}[/color][/b]");
        outer.TryAddChild(_titleLabel);

        // Description
        _descLabel = new MGTextBlock(_window,
            $"[color=lightgray]{Escape(_demoEntries[_currentIndex].Description)}[/color]")
        {
            WrapText = true,
        };
        outer.TryAddChild(_descLabel);

        // Separator
        var sep = new MGSeparator(_window, Orientation.Horizontal, 1);
        sep.Margin = new Thickness(0, 4, 0, 4);
        outer.TryAddChild(sep);

        // Navigation label
        var navLabel = new MGTextBlock(_window, "[color=gray]Click a demo to switch:[/color]");
        outer.TryAddChild(navLabel);

        // Scrollable list of demos
        var scrollViewer = new MGScrollViewer(_window,
            MGUI.Core.UI.ScrollBarVisibility.Auto,
            MGUI.Core.UI.ScrollBarVisibility.Disabled);

        var listStack = new MGStackPanel(_window, Orientation.Vertical) { Spacing = 2 };
        _demoButtons = new MGButton[_demoEntries.Count];
        for (int i = 0; i < _demoEntries.Count; i++)
        {
            int capturedIndex = i;
            var btn = new MGButton(_window, _ => _onDemoSelected(capturedIndex));
            btn.Margin = new Thickness(0, 0, 0, 0);
            btn.Padding = new Thickness(4, 2, 4, 2);
            SetButtonContent(btn, i);
            listStack.TryAddChild(btn);
            _demoButtons[i] = btn;
        }

        if (_demoButtons.Length > 0)
        {
            _window.DefaultFocusElement = _demoButtons[_currentIndex];
        }

        scrollViewer.SetContent(listStack);
        outer.TryAddChild(scrollViewer);

        // F1 hint at the bottom
        var hint = new MGTextBlock(_window, "[color=gray][i]Press F1 to hide this panel[/i][/color]");
        hint.Margin = new Thickness(0, 4, 0, 0);
        outer.TryAddChild(hint);

        _window.SetContent(outer);
        _window.MouseHandler.MovedInside += (_, _) => ArmKeyboardNavigation();
        _window.MouseHandler.LMBPressedInside += (_, _) => ArmKeyboardNavigation();
        _window.MouseHandler.Exited += (_, _) => DisarmKeyboardNavigation();
        _window.MouseHandler.PressedOutside += (_, _) => DisarmKeyboardNavigation();

        DisarmKeyboardNavigation();
    }

    // ---- Public API ----

    /// <summary>
    /// Refreshes the title, description, and highlighted button for the new active demo.
    /// </summary>
    public void UpdateCurrentDemo(int index, string title, string description)
    {
        _currentIndex = index;

        if (_titleLabel != null)
            _titleLabel.Text = $"[b][color=white]{Escape(title)}[/color][/b]";

        if (_descLabel != null)
            _descLabel.Text = $"[color=lightgray]{Escape(description)}[/color]";

        if (_demoButtons != null)
        {
            for (int i = 0; i < _demoButtons.Length; i++)
                SetButtonContent(_demoButtons[i], i);

            if (_window != null)
            {
                _window.DefaultFocusElement = _demoButtons[_currentIndex];
            }
        }
    }

    /// <summary>Shows or hides this screen's window without removing it from the stack.</summary>
    public void SetVisible(bool visible)
    {
        if (_window != null)
        {
            _window.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;

            if (!visible)
            {
                DisarmKeyboardNavigation();
            }
        }
    }

    // ---- IUIScreen ----

    public override IEnumerable<MGWindow> GetWindows()
    {
        if (_window != null) yield return _window;
    }

    // ---- Helpers ----

    private void SetButtonContent(MGButton btn, int index)
    {
        bool isCurrent = index == _currentIndex;
        string label   = Escape(_demoEntries[index].Title);
        btn.SetContent(isCurrent
            ? $"[b][color=yellow]{label}[/color][/b]"
            : $"[color=white]{label}[/color]");
    }

    private void ArmKeyboardNavigation()
    {
        if (_isKeyboardNavigationArmed)
        {
            return;
        }

        _isKeyboardNavigationArmed = true;
        if (_demoButtons == null)
        {
            return;
        }

        foreach (var button in _demoButtons)
        {
            button.IsFocusable = true;
        }

        var focusIndex = Math.Clamp(_currentIndex, 0, _demoButtons.Length - 1);
        _window!.DefaultFocusElement = _demoButtons[focusIndex];
        _demoButtons[focusIndex].Focus(KeyboardFocusSource.Pointer);
    }

    private void DisarmKeyboardNavigation()
    {
        if (_demoButtons == null)
        {
            _isKeyboardNavigationArmed = false;
            return;
        }

        _isKeyboardNavigationArmed = false;
        foreach (var button in _demoButtons)
        {
            button.IsFocusable = false;
        }
    }

    /// <summary>Escapes square brackets so MGUI rich-text parser sees them as literals.</summary>
    private static string Escape(string text)
        => text.Replace("[", "[[").Replace("]", "]]");
}
