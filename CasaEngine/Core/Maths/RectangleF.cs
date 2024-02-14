using System.Globalization;
using CasaEngine.Core.Helpers;
using Microsoft.Xna.Framework;

namespace CasaEngine.Core.Maths;

public struct RectangleF
{
    public static readonly RectangleF Empty;

    static RectangleF()
    {
        Empty = new RectangleF();
    }

    public float X, Y, Width, Height;

    public RectangleF(float x, float y, float width, float height)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    public float Left
    {
        get => X;
        set => X = value;
    }

    public float Top
    {
        get => Y;
        set => Y = value;
    }

    public float Right => X + Width;

    public float Bottom => Y + Height;

    public Vector2 Location
    {
        get => new(X, Y);
        set
        {
            X = value.X;
            Y = value.Y;
        }
    }

    public Vector2 Center => new(X + (Width / 2), Y + (Height / 2));

    public bool IsEmpty => (Width == 0.0f) && (Height == 0.0f) && (X == 0.0f) && (Y == 0.0f);

    public Size2F Size
    {
        get => new(Width, Height);
        set
        {
            Width = value.Width;
            Height = value.Height;
        }
    }

    public Vector2 TopLeft => new(X, Y);

    public Vector2 TopRight => new(Right, Y);

    public Vector2 BottomLeft => new(X, Bottom);

    public Vector2 BottomRight => new(Right, Bottom);

    public void Offset(Point amount)
    {
        Offset(amount.X, amount.Y);
    }

    public void Offset(Vector2 amount)
    {
        Offset(amount.X, amount.Y);
    }

    public void Offset(float offsetX, float offsetY)
    {
        X += offsetX;
        Y += offsetY;
    }

    public void Inflate(float horizontalAmount, float verticalAmount)
    {
        X -= horizontalAmount;
        Y -= verticalAmount;
        Width += horizontalAmount * 2;
        Height += verticalAmount * 2;
    }

    public void Contains(ref Vector2 value, out bool result)
    {
        result = value.X >= X && value.X <= Right && value.Y >= Y && value.Y <= Bottom;
    }

    public bool Contains(Rectangle value)
    {
        return (X <= value.X) && (value.Right <= Right) && (Y <= value.Y) && (value.Bottom <= Bottom);
    }

    public void Contains(ref RectangleF value, out bool result)
    {
        result = (X <= value.X) && (value.Right <= Right) && (Y <= value.Y) && (value.Bottom <= Bottom);
    }

    public bool Contains(float x, float y)
    {
        return x >= X && x <= Right && y >= Y && y <= Bottom;
    }

    public bool Contains(Vector2 vector2D)
    {
        return Contains(vector2D.X, vector2D.Y);
    }

    //public bool Contains(Int2 int2)
    //{
    //    return Contains(int2.X, int2.Y);
    //}

    public bool Contains(Point point)
    {
        return Contains(point.X, point.Y);
    }

    public bool Intersects(RectangleF value)
    {
        Intersects(ref value, out var result);
        return result;
    }

    public void Intersects(ref RectangleF value, out bool result)
    {
        result = (value.X < Right) && (X < value.Right) && (value.Y < Bottom) && (Y < value.Bottom);
    }

    public static RectangleF Intersect(RectangleF value1, RectangleF value2)
    {
        Intersect(ref value1, ref value2, out var result);
        return result;
    }

    public static void Intersect(ref RectangleF value1, ref RectangleF value2, out RectangleF result)
    {
        float newLeft = (value1.X > value2.X) ? value1.X : value2.X;
        float newTop = (value1.Y > value2.Y) ? value1.Y : value2.Y;
        float newRight = (value1.Right < value2.Right) ? value1.Right : value2.Right;
        float newBottom = (value1.Bottom < value2.Bottom) ? value1.Bottom : value2.Bottom;
        if ((newRight > newLeft) && (newBottom > newTop))
        {
            result = new RectangleF(newLeft, newTop, newRight - newLeft, newBottom - newTop);
        }
        else
        {
            result = Empty;
        }
    }

    public static RectangleF Union(RectangleF value1, RectangleF value2)
    {
        Union(ref value1, ref value2, out var result);
        return result;
    }

    public static void Union(ref RectangleF value1, ref RectangleF value2, out RectangleF result)
    {
        var left = Math.Min(value1.Left, value2.Left);
        var right = Math.Max(value1.Right, value2.Right);
        var top = Math.Min(value1.Top, value2.Top);
        var bottom = Math.Max(value1.Bottom, value2.Bottom);
        result = new RectangleF(left, top, right - left, bottom - top);
    }

    public override bool Equals(object obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (obj.GetType() != typeof(RectangleF)) return false;
        return Equals((RectangleF)obj);
    }

    public bool Equals(RectangleF other)
    {
        return MathUtils.NearEqual(other.Left, Left) &&
               MathUtils.NearEqual(other.Right, Right) &&
               MathUtils.NearEqual(other.Top, Top) &&
               MathUtils.NearEqual(other.Bottom, Bottom);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int result = X.GetHashCode();
            result = (result * 397) ^ Y.GetHashCode();
            result = (result * 397) ^ Width.GetHashCode();
            result = (result * 397) ^ Height.GetHashCode();
            return result;
        }
    }

    public static bool operator ==(RectangleF left, RectangleF right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(RectangleF left, RectangleF right)
    {
        return !(left == right);
    }

    public static explicit operator Rectangle(RectangleF value)
    {
        return new Rectangle((int)value.X, (int)value.Y, (int)value.Width, (int)value.Height);
    }

    public override string ToString()
    {
        return string.Format(CultureInfo.CurrentCulture, "X:{0} Y:{1} Width:{2} Height:{3}", X, Y, Width, Height);
    }
}
