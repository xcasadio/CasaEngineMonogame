namespace CasaEngine.Core.Helpers;

public static class RectangleExtension
{
    public static bool Collide(this Rectangle rect1, Rectangle rect2)
    {
        return rect1.Inside(rect2) || rect2.Inside(rect1);
    }

    public static bool Inside(this Rectangle rect1, Rectangle rect2)
    {
        var point = new Point
        {
            X = rect1.Left,
            Y = rect1.Top
        };

        if (point.InsideRectangle(rect2))
        {
            return true;
        }

        point.X = rect1.Right;
        point.Y = rect1.Top;

        if (point.InsideRectangle(rect2))
        {
            return true;
        }

        point.X = rect1.Left;
        point.Y = rect1.Bottom;

        if (point.InsideRectangle(rect2))
        {
            return true;
        }

        point.X = rect1.Right;
        point.Y = rect1.Bottom;

        if (point.InsideRectangle(rect2))
        {
            return true;
        }

        return false;
    }
}