namespace CasaEngine.Framework.GUI.Neoforce;

public class ResizeEventArgs : EventArgs
{

    public readonly int Width;
    public readonly int Height;
    public readonly int OldWidth;
    public readonly int OldHeight;

    public ResizeEventArgs()
    {
    }

    public ResizeEventArgs(int width, int height, int oldWidth, int oldHeight)
    {
        Width = width;
        Height = height;
        OldWidth = oldWidth;
        OldHeight = oldHeight;
    }

}