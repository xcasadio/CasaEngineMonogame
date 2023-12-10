namespace TomShane.Neoforce.Controls;

public class ModalContainer : Container
{
    private ModalContainer _lastModal;

    public override bool Visible
    {
        get => base.Visible;
        set
        {
            if (value)
            {
                Focused = true;
            }

            base.Visible = value;
        }
    }

    public virtual bool IsModal => Manager.ModalWindow == this;

    public virtual ModalResult ModalResult { get; set; } = ModalResult.None;

    public event WindowClosingEventHandler Closing;
    public event WindowClosedEventHandler Closed;

    public ModalContainer(Manager manager) : base(manager)
    {
        Manager.Input.GamePadDown += Input_GamePadDown;
        GamePadActions = new WindowGamePadActions();
    }

    public virtual void ShowModal()
    {
        _lastModal = Manager.ModalWindow;
        Manager.ModalWindow = this;
        Manager.Input.KeyDown += Input_KeyDown;
        Manager.Input.GamePadDown += Input_GamePadDown;
    }

    public virtual void Close()
    {
        var ex = new WindowClosingEventArgs();
        OnClosing(ex);
        if (!ex.Cancel)
        {
            Manager.Input.KeyDown -= Input_KeyDown;
            Manager.Input.GamePadDown -= Input_GamePadDown;
            Manager.ModalWindow = _lastModal;
            if (_lastModal != null)
            {
                _lastModal.Focused = true;
            }

            Hide();
            var ev = new WindowClosedEventArgs();
            OnClosed(ev);

            if (ev.Dispose)
            {
                Dispose();
            }
        }
    }

    public virtual void Close(ModalResult modalResult)
    {
        ModalResult = modalResult;
        Close();
    }

    protected virtual void OnClosing(WindowClosingEventArgs e)
    {
        Closing?.Invoke(this, e);
    }

    protected virtual void OnClosed(WindowClosedEventArgs e)
    {
        Closed?.Invoke(this, e);
    }

    void Input_KeyDown(object sender, KeyEventArgs e)
    {
        if (Visible && Manager.FocusedControl != null && Manager.FocusedControl.Root == this &&
            e.Key == Microsoft.Xna.Framework.Input.Keys.Escape)
        {
            //Close(ModalResult.Cancel);
        }
    }

    void Input_GamePadDown(object sender, GamePadEventArgs e)
    {
        if (Visible && Manager.FocusedControl != null && Manager.FocusedControl.Root == this)
        {
            if (e.Button == (GamePadActions as WindowGamePadActions).Accept)
            {
                Close(ModalResult.Ok);
            }
            else if (e.Button == (GamePadActions as WindowGamePadActions).Cancel)
            {
                Close(ModalResult.Cancel);
            }
        }
    }

}