using CasaEngine.Framework.Rendering.Depth;
using CasaEngine.Framework.Rendering.ScreenEffects;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.UI.MGUI;

/// <summary>What one step of a <see cref="UIScreenCommandData"/> does (ADR-0078).</summary>
public enum UIScreenActionType
{
    /// <summary>
    /// A type this engine does not know. Kept so that saving an envelope does not lose it, and refused when the
    /// screen loads.
    /// </summary>
    Unknown,

    /// <summary>Opens the screen named by <see cref="UIScreenActionData.Target"/> on top of this one.</summary>
    OpenScreen,

    /// <summary>Closes the screen that runs the action.</summary>
    CloseScreen,

    /// <summary>Loads the world file named by <see cref="UIScreenActionData.Target"/>.</summary>
    ChangeWorld,

    /// <summary>Plays the sound asset named by <see cref="UIScreenActionData.Target"/>.</summary>
    PlaySound,

    /// <summary>Quits the game.</summary>
    Quit,

    /// <summary>Starts a screen fade from the fade fields of <see cref="UIScreenActionData"/> (ADR-0081).</summary>
    FadeScreen,

    /// <summary>Holds the rest of the list until no screen fade runs (ADR-0081).</summary>
    WaitFade,
}

/// <summary>The names of <see cref="UIScreenActionType"/> in a <c>.uiscreen</c> file.</summary>
public static class UIScreenActionTypeNames
{
    public const string OpenScreen = "open_screen";
    public const string CloseScreen = "close_screen";
    public const string ChangeWorld = "change_world";
    public const string PlaySound = "play_sound";
    public const string Quit = "quit";
    public const string FadeScreen = "fade_screen";
    public const string WaitFade = "wait_fade";

    /// <summary>The type a file name stands for, <see cref="UIScreenActionType.Unknown"/> when it stands for none.</summary>
    public static UIScreenActionType Parse(string name)
        => name switch
        {
            OpenScreen => UIScreenActionType.OpenScreen,
            CloseScreen => UIScreenActionType.CloseScreen,
            ChangeWorld => UIScreenActionType.ChangeWorld,
            PlaySound => UIScreenActionType.PlaySound,
            Quit => UIScreenActionType.Quit,
            FadeScreen => UIScreenActionType.FadeScreen,
            WaitFade => UIScreenActionType.WaitFade,
            _ => UIScreenActionType.Unknown,
        };

    /// <summary>The file name of <paramref name="type"/>.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="type"/> has no name of its own.</exception>
    public static string ToName(UIScreenActionType type)
        => type switch
        {
            UIScreenActionType.OpenScreen => OpenScreen,
            UIScreenActionType.CloseScreen => CloseScreen,
            UIScreenActionType.ChangeWorld => ChangeWorld,
            UIScreenActionType.PlaySound => PlaySound,
            UIScreenActionType.Quit => Quit,
            UIScreenActionType.FadeScreen => FadeScreen,
            UIScreenActionType.WaitFade => WaitFade,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "This action type has no name in a file."),
        };
}

/// <summary>Which way the focus of a screen wraps at its ends (ADR-0080).</summary>
public enum UIScreenFocusWrap
{
    /// <summary>The focus stops at the ends.</summary>
    None,

    /// <summary>Up from the first element reaches the last, down from the last reaches the first.</summary>
    Vertical,

    /// <summary>Left from the first element reaches the last, right from the last reaches the first.</summary>
    Horizontal,
}

/// <summary>The names of <see cref="UIScreenFocusWrap"/> in a <c>.uiscreen</c> file.</summary>
public static class UIScreenFocusWrapNames
{
    public const string Vertical = "vertical";
    public const string Horizontal = "horizontal";

    /// <summary>Reads a file name. <see cref="UIScreenFocusWrap.None"/> has no name.</summary>
    /// <returns>False when the name stands for no way of wrapping.</returns>
    public static bool TryParse(string name, out UIScreenFocusWrap focusWrap)
    {
        switch (name)
        {
            case Vertical:
                focusWrap = UIScreenFocusWrap.Vertical;
                return true;

            case Horizontal:
                focusWrap = UIScreenFocusWrap.Horizontal;
                return true;

            default:
                focusWrap = UIScreenFocusWrap.None;
                return false;
        }
    }

    /// <summary>The file name of <paramref name="focusWrap"/>.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="focusWrap"/> has no name of its own.</exception>
    public static string ToName(UIScreenFocusWrap focusWrap)
        => focusWrap switch
        {
            UIScreenFocusWrap.Vertical => Vertical,
            UIScreenFocusWrap.Horizontal => Horizontal,
            _ => throw new ArgumentOutOfRangeException(nameof(focusWrap), focusWrap, "This value has no name in a file."),
        };
}

/// <summary>One step of a <see cref="UIScreenCommandData"/>.</summary>
public sealed class UIScreenActionData
{
    public UIScreenActionType Type { get; set; }

    /// <summary>What the action applies to, empty for the types that take nothing.</summary>
    public string Target { get; set; } = string.Empty;

    /// <summary>Where a <see cref="UIScreenActionType.FadeScreen"/> draws. Null when the file does not say.</summary>
    public ScreenEffectLayer? Layer { get; set; }

    /// <summary>How a <see cref="UIScreenActionType.FadeScreen"/> blends. Null when the file does not say.</summary>
    public SpriteBlendMode? BlendMode { get; set; }

    /// <summary>The overlay a <see cref="UIScreenActionType.FadeScreen"/> starts from. Null when the file does not say.</summary>
    public Color? From { get; set; }

    /// <summary>The overlay a <see cref="UIScreenActionType.FadeScreen"/> ends on. Null when the file does not say.</summary>
    public Color? To { get; set; }

    /// <summary>How long a <see cref="UIScreenActionType.FadeScreen"/> lasts. Null when the file does not say.</summary>
    public float? DurationSeconds { get; set; }

    /// <summary>
    /// The JSON an <see cref="UIScreenActionType.Unknown"/> action was read from, written back as it was. Null for
    /// every other type.
    /// </summary>
    public JObject UnknownData { get; set; }
}

/// <summary>
/// A named list of actions a button reaches through its <c>CommandName</c> (ADR-0078). The list runs in order, with no
/// conditions. It can only wait for a screen fade (ADR-0081).
/// </summary>
public sealed class UIScreenCommandData
{
    public string Name { get; set; } = string.Empty;

    public List<UIScreenActionData> Actions { get; } = new();
}

/// <summary>Two XAML names: <see cref="Marker"/> is visible only while <see cref="Element"/> holds the focus (ADR-0082).</summary>
public sealed class UIScreenFocusMarkerData
{
    public string Element { get; set; } = string.Empty;

    public string Marker { get; set; } = string.Empty;
}
