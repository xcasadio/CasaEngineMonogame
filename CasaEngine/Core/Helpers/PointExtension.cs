namespace CasaEngine.Core.Helpers;

public static class PointExtension
{
    public static bool InsideRectangle(this Point point, Rectangle rect)
    {
        return point.X >= rect.Left && point.X <= rect.Right &&
               point.Y >= rect.Top && point.Y <= rect.Bottom;
    }
}