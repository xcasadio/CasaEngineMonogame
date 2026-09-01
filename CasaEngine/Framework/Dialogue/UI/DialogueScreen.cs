using CasaEngine.Framework.Dialogue.Presentation;
using CasaEngine.Framework.Dialogue.Runtime;
using CasaEngine.Framework.UI;
using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using MGUI.Core.UI.Containers;
using MonoGame.Extended;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Dialogue.UI;

public sealed class DialogueScreen : UIScreenBase
{
    private readonly IDialoguePresenter _presenter;
    private readonly Action _requestClose;
    private readonly string _fontFamily;
    private MGWindow _window;
    private MGTextBlock _lineText;
    private MGStackPanel _choicesPanel;
    private readonly List<MGButton> _choiceButtons = new();
    private bool _subscribed;

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
    {
        ArgumentNullException.ThrowIfNull(presenter);
        ArgumentNullException.ThrowIfNull(requestClose);

        _presenter = presenter;
        _requestClose = requestClose;
        _fontFamily = fontFamily;
    }

    public override UILayer Layer => UILayer.Modal;
    public override bool IsModal => true;

    protected override void OnInitialize(UIRoot root)
    {
        Rectangle bounds = root.Desktop.ValidScreenBounds;
        int width = Math.Min(720, Math.Max(320, bounds.Width - 80));
        int height = 150;
        int x = bounds.X + (bounds.Width - width) / 2;
        int y = bounds.Y + Math.Max(20, bounds.Height - height - 48);

        _window = new MGWindow(root.Desktop, x, y, width, height)
        {
            TitleText = "Dialogue",
            IsTopmost = true,
            IsUserResizable = false,
        };
        _window.WindowClosed += (_, _) => _requestClose();
        _window.Padding = new Thickness(14);
        _window.BackgroundBrush.NormalValue = new MGSolidFillBrush(new Color(12, 18, 26, 235));

        var stack = new MGStackPanel(_window, Orientation.Vertical)
        {
            Spacing = 8,
            PreferredWidth = width - 36,
            PreferredHeight = height - 48,
        };

        _lineText = new MGTextBlock(_window, string.Empty, Color.White, 16)
        {
            WrapText = true,
        };
        ApplyFontFamily(_lineText);
        stack.TryAddChild(_lineText);

        _choicesPanel = new MGStackPanel(_window, Orientation.Vertical)
        {
            Spacing = 4,
        };
        stack.TryAddChild(_choicesPanel);

        var closeButton = new MGButton(_window, _ => _requestClose())
        {
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        closeButton.SetContent("Close");
        closeButton.Margin = new Thickness(0, 8, 0, 0);
        stack.TryAddChild(closeButton);

        _window.SetContent(stack);
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

    public override IEnumerable<MGWindow> GetWindows()
    {
        if (_window != null)
        {
            yield return _window;
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
    }

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
            var button = new MGButton(_window, _ => _presenter.SelectChoice(choiceIndex))
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
