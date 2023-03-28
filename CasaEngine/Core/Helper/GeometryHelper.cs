namespace CasaEngine.Core.Helper;

public static class GeometryHelper
{
    public static bool PointInsideRect(Point point, Rectangle rect)
    {
        return point.X >= rect.Left && point.X <= rect.Right &&
               point.Y >= rect.Top && point.Y <= rect.Bottom;
    }

    public static bool RectCollideRect(Rectangle rect1, Rectangle rect2)
    {
        return RectInsideRect(rect1, rect2) || RectInsideRect(rect2, rect1);
    }

    public static bool RectInsideRect(Rectangle rect1, Rectangle rect2)
    {
        var point = new Point
        {
            X = rect1.Left,
            Y = rect1.Top
        };

        if (PointInsideRect(point, rect2))
        {
            return true;
        }

        point.X = rect1.Right;
        point.Y = rect1.Top;

        if (PointInsideRect(point, rect2))
        {
            return true;
        }

        point.X = rect1.Left;
        point.Y = rect1.Bottom;

        if (PointInsideRect(point, rect2))
        {
            return true;
        }

        point.X = rect1.Right;
        point.Y = rect1.Bottom;

        if (PointInsideRect(point, rect2))
        {
            return true;
        }

        return false;
    }
}