using System.Numerics;
using System.Runtime.Serialization;

namespace CasaEngine.Core;

public struct Size
{
    public static readonly Size Zero;

    static Size()
    {
        Zero = new Size();
    }

    public int Width;
    public int Height;


    public Size(int width, int height)
    {
        Width = width;
        Height = height;
    }
}