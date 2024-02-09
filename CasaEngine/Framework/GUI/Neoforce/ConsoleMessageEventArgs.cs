namespace TomShane.Neoforce.Controls;

public class ConsoleMessageEventArgs : EventArgs
{

    public ConsoleMessage Message;

    public ConsoleMessageEventArgs()
    {
    }

    public ConsoleMessageEventArgs(ConsoleMessage message)
    {
        Message = message;
    }

}