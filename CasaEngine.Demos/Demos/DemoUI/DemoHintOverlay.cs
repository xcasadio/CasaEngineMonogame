using CasaEngine.Framework.UI;
using MGUI.Core.UI;

namespace CasaEngine.Demos.Demos;

/// <summary>
/// Small bottom-center overlay displayed in the scene of every demo.
/// Reminds the player how to show the help panel (F1, or Start on a gamepad) and how to go back to the main screen
/// (Esc, or Select on a gamepad).
/// Toggle visibility with <see cref="SetVisible"/>.
/// <para/>
/// Its tree lives in `Content/Screens/demo-hint.xaml`, where it sits included.
/// </summary>
internal sealed class DemoHintOverlay : XamlUIScreenBase
{
    public override UILayer Layer   => UILayer.HUD;
    public override bool    IsModal => false;

    private readonly string _text;

    /// <param name="help">What the "help" action of the input configuration is bound to.</param>
    /// <param name="back">What the "back to the menu" action is bound to.</param>
    public DemoHintOverlay(DemoBinding help, DemoBinding back)
        : base(DemoScreenXaml.Source("demo-hint.xaml"))
    {
        _text = "[color=lightgray][i]" + Describe(help) + ": help    " + Describe(back) + ": back to the menu[/i][/color]";
    }

    // "[b]F1[/b] / [b]Start[/b]": the key, then the button when the action has one.
    private static string Describe(DemoBinding binding)
    {
        string text = "[b]" + DemoBindingText.KeyLabel(binding.Key) + "[/b]";
        string button = DemoBindingText.ButtonLabel(binding.Button);
        return button.Length == 0 ? text : text + " / [b]" + button + "[/b]";
    }

    protected override void OnWindowLoaded(MGWindow window)
    {
        // The document places the line and holds a default text; the keys come from the input configuration.
        FindControl<MGTextBlock>("lblHint").Text = _text;
    }

    /// <summary>Shows or hides this overlay's window without removing it from the stack.</summary>
    public void SetVisible(bool visible)
    {
        if (Window != null)
        {
            Window.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}
