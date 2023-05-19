namespace CasaEngine.Core.Maths;

public struct Size : IEquatable<Size>
{
    public static readonly Size Zero = new Size(0, 0);
    public static readonly Size Empty = Zero;

    public int Width;
    public int Height;

    public Size(int width, int height)
    {
        Width = width;
        Height = height;
    }

    public bool Equals(Size other)
    {
        return other.Width == Width && other.Height == Height;
    }

    public override bool Equals(object obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (obj.GetType() != typeof(Size)) return false;
        return Equals((Size)obj);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            return (Width * 397) ^ Height;
        }
    }

    public static bool operator ==(Size left, Size right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(Size left, Size right)
    {
        return !left.Equals(right);
    }

    public override string ToString()
    {
        return $"({Width},{Height})";
    }
}