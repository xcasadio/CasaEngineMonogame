using Microsoft.Xna.Framework;

namespace TomShane.Neoforce.Controls;

public class ClipControl : Control
{
    public ClipBox ClientArea { get; set; }

    public override Margins ClientMargins
    {
        get => base.ClientMargins;
        set
        {
            base.ClientMargins = value;
            if (ClientArea != null)
            {
                ClientArea.Left = ClientLeft;
                ClientArea.Top = ClientTop;
                ClientArea.Width = ClientWidth;
                ClientArea.Height = ClientHeight;
            }
        }
    }

    public ClipControl(Manager manager) : base(manager)
    {
        ClientArea = new ClipBox(manager);

        ClientArea.Init();
        ClientArea.MinimumWidth = 0;
        ClientArea.MinimumHeight = 0;
        ClientArea.Left = ClientLeft;
        ClientArea.Top = ClientTop;
        ClientArea.Width = ClientWidth;
        ClientArea.Height = ClientHeight;

        base.Add(ClientArea);
    }

    public virtual void Add(Control control, bool client)
    {
        if (client)
        {
            ClientArea.Add(control);
        }
        else
        {
            base.Add(control);
        }
    }

    public override void Add(Control control)
    {
        Add(control, true);
    }

    public override void Remove(Control control)
    {
        base.Remove(control);
        ClientArea.Remove(control);
    }

    protected override void OnResize(ResizeEventArgs e)
    {
        base.OnResize(e);

        if (ClientArea != null)
        {
            ClientArea.Left = ClientLeft;
            ClientArea.Top = ClientTop;
            ClientArea.Width = ClientWidth;
            ClientArea.Height = ClientHeight;
        }
    }

    protected virtual void AdjustMargins()
    {
    }
}