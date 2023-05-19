namespace CasaEngine.Core.Maths;

public struct Size2F : IEquatable<Size2F>
{
    public static readonly Size2F Zero = new(0, 0);
    public static readonly Size2F Empty = Zero;

    public float Width;
    public float Height;

    public Size2F(float width, float height)
    {
        Width = width;
        Height = height;
    }

    public bool Equals(Size2F other)
    {
        return other.Width == Width && other.Height == Height;
    }

    public override bool Equals(object obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (obj.GetType() != typeof(Size2F)) return false;
        return Equals((Size2F)obj);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            return (Width.GetHashCode() * 397) ^ Height.GetHashCode();
        }
    }

    public static bool operator ==(Size2F left, Size2F right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(Size2F left, Size2F right)
    {
        return !left.Equals(right);
    }

    public override string ToString()
    {
        return string.Format("({0},{1})", Width, Height);
    }

    public void Deconstruct(out float width, out float height)
    {
        width = Width;
        height = Height;
    }
}