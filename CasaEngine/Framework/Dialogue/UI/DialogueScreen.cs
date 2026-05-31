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
    private readonly DialogueService _dialogueService;
    private readonly Action _requestClose;
    private MGWindow _window;
    private MGTextBlock _lineText;
    private bool _subscribed;

    public DialogueScreen(DialogueService dialogueService)
        : this(dialogueService, static () => { })
    {
    }

    public DialogueScreen(DialogueService dialogueService, Action requestClose)
    {
        ArgumentNullException.ThrowIfNull(dialogueService);
        ArgumentNullException.ThrowIfNull(requestClose);

        _dialogueService = dialogueService;
        _requestClose = requestClose;
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
        stack.TryAddChild(_lineText);

        _window.SetContent(stack);
        RefreshLine();
    }

    public override void Show()
    {
        if (!_subscribed)
        {
            _dialogueService.StateChanged += OnDialogueStateChanged;
            _subscribed = true;
        }

        RefreshLine();
    }

    public override void Hide()
    {
        if (_subscribed)
        {
            _dialogueService.StateChanged -= OnDialogueStateChanged;
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

    private void OnDialogueStateChanged(object sender, DialogueStateChangedEventArgs args)
    {
        RefreshLine();
    }

    private void RefreshLine()
    {
        if (_lineText == null)
        {
            return;
        }

        DialogueLine line = _dialogueService.CurrentLine;
        string text = line.Speaker.Length == 0 ? line.Text : $"[b]{line.Speaker}[/b]\n{line.Text}";
        _lineText.SetText(text, MGTextInvalidationMode.ReflowLocal);
    }
}