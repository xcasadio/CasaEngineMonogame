namespace CasaEngine.Framework.GUI.Neoforce.Input;

public struct InputOffset
{
    public readonly int X;
    public readonly int Y;
    public readonly float RatioX;
    public readonly float RatioY;

    public InputOffset(int x, int y, float rx, float ry)
    {
        X = x;
        Y = y;
        RatioX = rx;
        RatioY = ry;
    }
}