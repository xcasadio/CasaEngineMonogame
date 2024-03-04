namespace CasaEngine.Framework.GUI.Neoforce;

public class WindowClosingEventArgs : EventArgs
{

    public readonly bool Cancel = false;

    public WindowClosingEventArgs()
    {
    }

}