namespace CasaEngine.Framework.GUI.Neoforce;

public struct Margins
{
    public int Left;
    public int Top;
    public int Right;
    public int Bottom;

    public int Vertical => Top + Bottom;
    public int Horizontal => Left + Right;

    public Margins(int left, int top, int right, int bottom)
    {
        Left = left;
        Top = top;
        Right = right;
        Bottom = bottom;
    }
}