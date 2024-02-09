namespace TomShane.Neoforce.Controls;

public class MoveEventArgs : EventArgs
{

    public readonly int Left;
    public readonly int Top;
    public int OldLeft;
    public int OldTop;

    public MoveEventArgs()
    {
    }

    public MoveEventArgs(int left, int top, int oldLeft, int oldTop)
    {
        Left = left;
        Top = top;
        OldLeft = oldLeft;
        OldTop = oldTop;
    }

}