namespace CasaEngine.Core;

public struct Size
{
    public int Width;
    public int Height;

    public static Size Zero => new(0, 0);

    public Size(int width, int height)
    {
        Width = width;
        Height = height;
    } // Size

}