namespace TomShane.Neoforce.Controls;

public class WindowClosingEventArgs : EventArgs
{

    public readonly bool Cancel = false;

    public WindowClosingEventArgs()
    {
    }

}