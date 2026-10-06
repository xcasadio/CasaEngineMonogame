using System;
using CasaEngine.Framework.UI;
using MGUI.Core.UI;

namespace CasaEngine.Demos.Demos;

/// <summary>
/// The demo browser beside the scene (ADR-0070): a tree of the demos by theme, the description of the selected demo
/// below it, and the handle the game drags to resize the browser. It lives on its own UI runtime, installed as the
/// game's window-level UI, so it survives demo changes.
/// <para/>
/// Keyboard ownership (plan point P9): the browser takes the keyboard only while the pointer is over it. MGUI gives
/// focus by itself to the first focusable element on any key press and has no public way to take it back, so the game
/// arms the browser (its focusable elements become focusable) when the pointer enters it and disarms it (they stop being
/// focusable, which drops the focus) when the pointer leaves -- the mechanism the former demo info panel used.
/// <para/>
/// Its shell lives in `Content/Screens/demo-browser.xaml`; the tree items do not: there is one per demo, so the tree
/// IS the data and is built here.
/// </summary>
internal sealed class DemoBrowserScreen : XamlUIScreenBase
{
    private readonly Action _onCollapseRequested;

    // ---- MGUI elements ----
    private MGTreeView _tree;
    private MGButton _collapseButton;
    private MGElement[] _focusableElements;
    private bool _isKeyboardArmed;

    // ---- IUIScreen ----
    public override UILayer Layer => UILayer.HUD;
    public override bool IsModal => false;

    /// <param name="onCollapseRequested">Called when the player clicks the collapse button of the header.</param>
    public DemoBrowserScreen(Action onCollapseRequested)
        : base(DemoScreenXaml.Source("demo-browser.xaml"))
    {
        _onCollapseRequested = onCollapseRequested;
    }

    protected override void OnWindowLoaded(MGWindow window)
    {
        _tree = FindControl<MGTreeView>("treeDemos");
        _collapseButton = FindControl<MGButton>("btnCollapse");
        _collapseButton.AddCommandHandler((_, _) => _onCollapseRequested?.Invoke());

        _focusableElements = new MGElement[] { _tree, _collapseButton };
        _isKeyboardArmed = true;
        SetKeyboardArmed(false);
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
