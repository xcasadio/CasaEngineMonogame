using System;
using System.Collections.Generic;
using CasaEngine.Framework.UI;
using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using MonoGame.Extended;

namespace CasaEngine.Demos.Demos;

/// <summary>
/// HUD-layer overlay window showing the current demo's title and description,
/// plus a clickable list of all available demos for navigation.
///
/// Managed by <see cref="DemosGame"/>. Stays on the ScreenStack for the lifetime
/// of each demo; a new instance is created on every demo change.
///
/// Toggle visibility with <see cref="SetVisible"/>.
/// <para/>
/// Its shell lives in `Content/Screens/demo-info.xaml`. The demo buttons do not: there is one per demo
/// entry, so that list IS the data and is built here, into the named panel the document declares for it.
/// </summary>
internal sealed class DemoInfoScreen : XamlUIScreenBase
{
    private const int WindowWidth = 300;
    private const int MaxWindowHeight = 440;

    // ---- Data ----
    private readonly IReadOnlyList<(string Title, string Description)> _demoEntries;
    private int _currentIndex;
    private readonly Action<int> _onDemoSelected;

    // ---- MGUI elements ----
    private MGTextBlock _titleLabel;
    private MGTextBlock _descLabel;
    private MGButton[] _demoButtons;
    private bool _isKeyboardNavigationArmed;

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
        : base(DemoScreenXaml.Source("demo-info.xaml"))
    {
        _demoEntries     = demoEntries;
        _currentIndex    = currentIndex;
        _onDemoSelected  = onDemoSelected;
    }

    // ---- Build UI ----

    protected override void OnWindowLoaded(MGWindow window)
    {
        // Anchored to the top-right corner, and never taller than the viewport. Both depend on a resolution
        // the document cannot know -- in a split-screen demo each viewport is a fraction of the back-buffer.
        var bounds = window.Desktop.ValidScreenBounds;
        window.Left = bounds.Width - WindowWidth - 10;
        window.Top = 10;
        window.WindowHeight = Math.Min(MaxWindowHeight, bounds.Height - 20);

        _titleLabel = FindControl<MGTextBlock>("lblTitle");
        _descLabel = FindControl<MGTextBlock>("lblDescription");

        var listStack = FindControl<MGStackPanel>("lstDemos");
        _demoButtons = new MGButton[_demoEntries.Count];

        for (int i = 0; i < _demoEntries.Count; i++)
        {
            int capturedIndex = i;
            var btn = new MGButton(window, _ => _onDemoSelected(capturedIndex));
            btn.Padding = new Thickness(4, 2, 4, 2);
            SetButtonContent(btn, i);
            listStack.TryAddChild(btn);
            _demoButtons[i] = btn;
        }

        ApplyCurrentDemo(_demoEntries[_currentIndex].Title, _demoEntries[_currentIndex].Description);

        if (_demoButtons.Length > 0)
        {
            window.DefaultFocusElement = _demoButtons[_currentIndex];
        }

        window.MouseHandler.MovedInside += (_, _) => ArmKeyboardNavigation();
        window.MouseHandler.LMBPressedInside += (_, _) => ArmKeyboardNavigation();
        window.MouseHandler.Exited += (_, _) => DisarmKeyboardNavigation();
        window.MouseHandler.PressedOutside += (_, _) => DisarmKeyboardNavigation();

        DisarmKeyboardNavigation();
    }

    // ---- Public API ----

    /// <summary>
    /// Refreshes the title, description, and highlighted button for the new active demo.
    /// </summary>
    public void UpdateCurrentDemo(int index, string title, string description)
    {
        _currentIndex = index;
        ApplyCurrentDemo(title, description);

        if (_demoButtons != null)
        {
            for (int i = 0; i < _demoButtons.Length; i++)
                SetButtonContent(_demoButtons[i], i);

            if (Window != null)
            {
                Window.DefaultFocusElement = _demoButtons[_currentIndex];
            }
        }
    }

    /// <summary>Shows or hides this screen's window without removing it from the stack.</summary>
    public void SetVisible(bool visible)
    {
        if (Window != null)
        {
            Window.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;

            if (!visible)
            {
                DisarmKeyboardNavigation();
            }
        }
    }

    // ---- Helpers ----

    private void ApplyCurrentDemo(string title, string description)
    {
        if (_titleLabel != null)
            _titleLabel.Text = $"[b][color=white]{Escape(title)}[/color][/b]";

        if (_descLabel != null)
            _descLabel.Text = $"[color=lightgray]{Escape(description)}[/color]";
    }

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
        Window.DefaultFocusElement = _demoButtons[focusIndex];
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
