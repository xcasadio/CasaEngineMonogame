namespace CasaEngine.Framework.GUI.Neoforce;

public struct Size
{
    public int Width;
    public int Height;

    public Size(int width, int height)
    {
        Width = width;
        Height = height;
    }

    public static Size Zero => new Size(0, 0);
}