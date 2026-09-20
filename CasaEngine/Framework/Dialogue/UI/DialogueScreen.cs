using System.Reflection;
using CasaEngine.Framework.Dialogue.Presentation;
using CasaEngine.Framework.Dialogue.Runtime;
using CasaEngine.Framework.UI;
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
/// </summary>
public sealed class DialogueScreen : XamlUIScreenBase
{
    private const string XamlResourceName = "CasaEngine.Framework.Dialogue.UI.DialogueScreen.xaml";

    private readonly IDialoguePresenter _presenter;
    private readonly Action _requestClose;
    private readonly string _fontFamily;
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

        _contentPanel = FindControl<MGStackPanel>("pnlContent");
        _contentPanel.PreferredWidth = width - 36;

        _lineText = FindControl<MGTextBlock>("lblLine");
        ApplyFontFamily(_lineText);

        _choicesPanel = FindControl<MGStackPanel>("pnlChoices");

        var closeButton = FindControl<MGButton>("btnClose");

        if (ShowCloseButton)
        {
            _closeButton = closeButton;
            _closeButton.AddCommandHandler((_, _) => _requestClose());
        }
        else
        {
            // Taken out of the tree rather than hidden, so it is genuinely not built into the layout and
            // CloseButtonForTests stays null, as callers that turn it off rely on.
            _contentPanel.TryRemoveChild(closeButton);
            _closeButton = null;
        }

        RefreshPresentation();
    }

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
