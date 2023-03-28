namespace CasaEngine.Editor.Manipulation;

public class AnchorLocationChangedEventArgs
    : EventArgs
{
    public int OffsetX
    {
        get;
        private set;
    }

    public int OffsetY
    {
        get;
        private set;
    }

    public AnchorLocationChangedEventArgs(int x, int y)
    {
        OffsetX = x;
        OffsetY = y;
    }
}