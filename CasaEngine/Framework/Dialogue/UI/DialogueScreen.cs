using System.Reflection;
using CasaEngine.Core.Logging;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Configuration.Project;
using CasaEngine.Framework.Dialogue.Presentation;
using CasaEngine.Framework.Dialogue.Runtime;
using CasaEngine.Framework.UI;
using CasaEngine.Framework.UI.MGUI;
using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using MGUI.Core.UI.XAML;
using MGUI.Shared.Helpers;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Dialogue.UI;

/// <summary>
/// The framework's dialogue box.
/// <para/>
/// Its tree is declared in <c>DialogueScreen.xaml</c>, shipped as an <b>embedded resource</b> of this
/// assembly rather than as a project asset: the screen belongs to the engine, so it cannot be the asset of
/// any one game. That is how MGUI ships its own <c>BuiltInThemes.xaml</c>.
/// <para/>
/// A project may replace that markup with its own <c>.uiscreen</c> asset, named by
/// <see cref="ProjectSettings.DialogueScreenAsset"/> and read through the asset manager given to the
/// constructor that takes one. The replacement must declare the elements this screen drives -- a
/// <c>StackPanel</c> <c>pnlContent</c>, a <c>TextBlock</c> <c>lblLine</c>, a <c>StackPanel</c> <c>pnlChoices</c>,
/// and a <c>Button</c> <c>btnClose</c> as a direct child of <c>pnlContent</c> (required when
/// <see cref="ShowCloseButton"/> is true, optional otherwise). When the asset cannot be loaded, fails to parse,
/// or breaks that contract, the screen logs a warning and uses the embedded markup.
/// </summary>
public sealed class DialogueScreen : XamlUIScreenBase
{
    private const string XamlResourceName = "CasaEngine.Framework.Dialogue.UI.DialogueScreen.xaml";

    private const string ContentPanelName = "pnlContent";
    private const string LineTextName = "lblLine";
    private const string ChoicesPanelName = "pnlChoices";
    private const string CloseButtonName = "btnClose";

    private readonly IDialoguePresenter _presenter;
    private readonly Action _requestClose;
    private readonly string _fontFamily;

    // The project's replacement markup (ProjectSettings.DialogueScreenAsset), held from construction to Dispose;
    // null when the project names none or it could not be acquired.
    private readonly string _replacementName;
    private readonly AssetHandle<UIScreenAsset> _replacementHandle;
    private readonly string _replacementFilePath;

    private MGStackPanel _contentPanel;
    private MGTextBlock _lineText;
    private MGStackPanel _choicesPanel;
    private readonly List<MGButton> _choiceButtons = new();
    private MGButton _closeButton;
    private bool _subscribed;

    /// <summary>
    /// Whether the window carries its own "Close" button. Default true, so every existing caller keeps
    /// the behaviour it had. A game whose dialogue boxes are dismissed by a gameplay button (and whose
    /// own dialogue director owns the open/closed state) sets this to false: an extra UI affordance
    /// there closes the window behind that director's back, leaving the box logically open and any
    /// control flags it posted still set. Read once, when the window is built.
    /// </summary>
    public bool ShowCloseButton { get; init; } = true;

    public DialogueScreen(IDialoguePresenter presenter)
        : this(presenter, static () => { })
    {
    }

    public DialogueScreen(IDialoguePresenter presenter, Action requestClose)
        : this(presenter, requestClose, fontFamily: null)
    {
    }

    /// <param name="fontFamily">
    /// Name of a font family previously registered with the desktop's text engine (e.g. via a
    /// bitmap font registered through <c>FontStashSharpTextEngine.AddStaticFont</c>). When null
    /// or empty, the screen falls back to the theme's default TTF font family.
    /// </param>
    public DialogueScreen(IDialoguePresenter presenter, Action requestClose, string fontFamily)
        : base(EmbeddedXaml())
    {
        ArgumentNullException.ThrowIfNull(presenter);
        ArgumentNullException.ThrowIfNull(requestClose);

        _presenter = presenter;
        _requestClose = requestClose;
        _fontFamily = fontFamily;
    }

    /// <summary>
    /// Builds the dialogue box from the project's replacement markup when
    /// <see cref="ProjectSettings.DialogueScreenAsset"/> (read from
    /// <paramref name="assetContentManager"/>'s runtime context) names one, and from the embedded markup
    /// otherwise, or when the replacement cannot be used (logged). The replacement is held until
    /// <see cref="Dispose"/>.
    /// </summary>
    /// <param name="fontFamily">As in <see cref="DialogueScreen(IDialoguePresenter, Action, string)"/>.</param>
    public DialogueScreen(IDialoguePresenter presenter, Action requestClose, string fontFamily, AssetContentManager assetContentManager)
        : this(presenter, requestClose, fontFamily)
    {
        ArgumentNullException.ThrowIfNull(assetContentManager);

        _replacementName = assetContentManager.RuntimeContext?.ProjectSettings?.DialogueScreenAsset;
        if (string.IsNullOrWhiteSpace(_replacementName))
        {
            _replacementName = null;
            return;
        }

        try
        {
            _replacementHandle = AcquireScreenAsset(assetContentManager, _replacementName, out _replacementFilePath);
        }
        catch (Exception ex) when (IsMarkupFailure(ex))
        {
            ReportFallback($"it cannot be loaded ({ex.GetType().Name}: {ex.Message})");
        }
    }

    /// <summary>The project's replacement markup is in use: it was acquired, and the window built from it kept.
    /// False before the window is built.</summary>
    internal bool UsesReplacementMarkupForTests { get; private set; }

    public override UILayer Layer => UILayer.Modal;
    public override bool IsModal => true;

    /// <summary>Minimum (and initial) window height; the window then GROWS to fit its content -
    /// a wrapped multi-line text plus visible choice buttons overflowed the previous fixed 150 px
    /// (user-reported: the second choice button was clipped out of the window).</summary>
    internal const int MinWindowHeight = 150;
    private const int TopMargin = 20;
    private const int BottomMargin = 48;

    private static XamlDocumentSource EmbeddedXaml()
        => XamlDocumentSource.FromString(
            GeneralUtils.ReadEmbeddedResourceAsString(Assembly.GetExecutingAssembly(), XamlResourceName),
            "DialogueScreen.xaml");

    protected override void OnWindowLoaded(MGWindow window)
    {
        Rectangle bounds = window.Desktop.ValidScreenBounds;
        int width = Math.Min(720, Math.Max(320, bounds.Width - 80));

        window.WindowWidth = width;
        window.Left = bounds.X + (bounds.Width - width) / 2;
        window.Top = bounds.Y + Math.Max(TopMargin, bounds.Height - MinWindowHeight - BottomMargin);
        window.WindowClosed += (_, _) => _requestClose();

        _contentPanel = FindControl<MGStackPanel>(ContentPanelName);
        _contentPanel.PreferredWidth = width - 36;

        _lineText = FindControl<MGTextBlock>(LineTextName);
        ApplyFontFamily(_lineText);

        _choicesPanel = FindControl<MGStackPanel>(ChoicesPanelName);

        if (ShowCloseButton)
        {
            _closeButton = FindControl<MGButton>(CloseButtonName);
            _closeButton.AddCommandHandler((_, _) => _requestClose());
        }
        else
        {
            // Taken out of the tree rather than hidden, so it is genuinely not built into the layout and
            // CloseButtonForTests stays null, as callers that turn it off rely on. A replacement markup may leave
            // it out altogether.
            if (window.TryGetElementByName(CloseButtonName, out MGButton closeButton))
            {
                _contentPanel.TryRemoveChild(closeButton);
            }

            _closeButton = null;
        }

        RefreshPresentation();
    }

    /// <summary>Tries the project's replacement markup first, and falls back to the embedded markup, with a
    /// warning, when it fails to load or breaks the element contract (see the class summary).</summary>
    protected override MGWindow LoadWindow(MGDesktop desktop)
    {
        if (_replacementHandle != null)
        {
            try
            {
                MGWindow window = UIScreenLoader.Load(desktop, _replacementHandle.Asset, _replacementFilePath);
                string violation = FindContractViolation(window, ShowCloseButton);
                if (violation == null)
                {
                    UsesReplacementMarkupForTests = true;
                    return window;
                }

                // The rejected window never reaches the desktop; its bindings must not outlive it (gap G10).
                window.RemoveDataBindings(IncludeChildren: true);
                ReportFallback(violation);
            }
            catch (Exception ex) when (IsMarkupFailure(ex))
            {
                ReportFallback($"it cannot be loaded ({ex.GetType().Name}: {ex.Message})");
            }
        }

        UsesReplacementMarkupForTests = false;
        return base.LoadWindow(desktop);
    }

    /// <summary>Gives back the project's replacement markup, then what the base class holds.</summary>
    public override void Dispose()
    {
        _replacementHandle?.Dispose();
        base.Dispose();
    }

    /// <summary>What is wrong with <paramref name="window"/> as a dialogue box, or null when it declares every
    /// element the screen drives, with the right types and places.</summary>
    internal static string FindContractViolation(MGWindow window, bool showCloseButton)
    {
        string violation = CheckElement<MGStackPanel>(window, ContentPanelName, "StackPanel", out MGStackPanel contentPanel)
            ?? CheckElement<MGTextBlock>(window, LineTextName, "TextBlock", out _)
            ?? CheckElement<MGStackPanel>(window, ChoicesPanelName, "StackPanel", out _);
        if (violation != null)
        {
            return violation;
        }

        if (!window.TryGetElementByName(CloseButtonName, out MGElement closeButton))
        {
            return showCloseButton ? $"it declares no '{CloseButtonName}' (a Button), which a box with a close button needs" : null;
        }

        if (closeButton is not MGButton)
        {
            return $"it declares '{CloseButtonName}' as a {closeButton.GetType().Name}, not a Button";
        }

        return closeButton.Parent == contentPanel
            ? null
            : $"its '{CloseButtonName}' is not a direct child of '{ContentPanelName}'";
    }

    private static string CheckElement<T>(MGWindow window, string name, string xamlType, out T element) where T : MGElement
    {
        element = null;
        if (!window.TryGetElementByName(name, out MGElement found))
        {
            return $"it declares no '{name}' (a {xamlType})";
        }

        if (found is not T typed)
        {
            return $"it declares '{name}' as a {found.GetType().Name}, not a {xamlType}";
        }

        element = typed;
        return null;
    }

    // What a broken or missing replacement can throw: an unresolvable id or name, no source file, an unreadable or
    // malformed envelope, or markup that fails to parse, validate or attach. Anything else is a programming error
    // and propagates.
    private static bool IsMarkupFailure(Exception ex)
        => ex is InvalidOperationException or IOException or UnauthorizedAccessException
            or Newtonsoft.Json.JsonException or XamlLoaderException;

    private void ReportFallback(string reason)
        => Logs.WriteWarning(
            $"DialogueScreen: the project's dialogue screen '{_replacementName}' (ProjectSettings.DialogueScreenAsset) "
            + $"is not used: {reason}. The built-in dialogue box is used instead.");

    public override void Show()
    {
        if (!_subscribed)
        {
            _presenter.PresentationChanged += OnDialoguePresentationChanged;
            _subscribed = true;
        }

        RefreshPresentation();
    }

    public override void Hide()
    {
        if (_subscribed)
        {
            _presenter.PresentationChanged -= OnDialoguePresentationChanged;
            _subscribed = false;
        }
    }

    private void OnDialoguePresentationChanged(object sender, DialoguePresentationChangedEventArgs args)
    {
        RefreshPresentation();
    }

    private void RefreshPresentation()
    {
        RefreshLine();
        RefreshChoices();
        ResizeToFitContent();
    }

    /// <summary>Grows (or shrinks back to <see cref="MinWindowHeight"/>) the window so the current
    /// line and every choice button are inside it, keeping it bottom-anchored.
    /// <see cref="MGWindow.ApplySizeToContent"/> clamps growth by the room BELOW the current Top, so
    /// the window is measured from the top of the screen first and re-anchored after.</summary>
    private void ResizeToFitContent()
    {
        if (Window == null)
        {
            return;
        }

        Rectangle bounds = Window.GetDesktop().ValidScreenBounds;
        Window.Top = bounds.Y + TopMargin;
        Window.ApplySizeToContent(
            SizeToContent.Height,
            MinHeight: MinWindowHeight,
            MaxHeight: Math.Max(MinWindowHeight, bounds.Height - TopMargin - BottomMargin),
            UpdateLayoutImmediately: true);
        Window.Top = Math.Max(bounds.Y + TopMargin, bounds.Bottom - BottomMargin - Window.WindowHeight);
        Window.ValidateWindowSizeAndPosition();
    }

    internal MGWindow WindowForTests => Window;
    internal IReadOnlyList<MGButton> ChoiceButtonsForTests => _choiceButtons;
    internal MGButton CloseButtonForTests => _closeButton;

    private void RefreshLine()
    {
        if (_lineText == null)
        {
            return;
        }

        DialogueLine line = _presenter.CurrentLine;
        string text = line.Speaker.Length == 0 ? line.Text : $"[b]{line.Speaker}[/b]\n{line.Text}";
        _lineText.SetText(text, MGTextInvalidationMode.ReflowLocal);
    }

    private void RefreshChoices()
    {
        if (_choicesPanel == null)
        {
            return;
        }

        foreach (MGButton button in _choiceButtons)
        {
            _choicesPanel.TryRemoveChild(button);
        }

        _choiceButtons.Clear();

        if (!_presenter.HasChoices)
        {
            _choicesPanel.Visibility = Visibility.Collapsed;
            return;
        }

        IReadOnlyList<string> labels = _presenter.Choices;
        for (int i = 0; i < labels.Count; i++)
        {
            int choiceIndex = i;
            var button = new MGButton(Window, _ => _presenter.SelectChoice(choiceIndex))
            {
                HorizontalAlignment = HorizontalAlignment.Left,
            };
            MGTextBlock buttonText = button.SetContent(labels[i]);
            ApplyFontFamily(buttonText);
            _choiceButtons.Add(button);
            _choicesPanel.TryAddChild(button);
        }

        _choicesPanel.Visibility = Visibility.Visible;
    }

    private void ApplyFontFamily(MGTextBlock textBlock)
    {
        if (!string.IsNullOrEmpty(_fontFamily))
        {
            textBlock.FontFamily = _fontFamily;
        }
    }
}
