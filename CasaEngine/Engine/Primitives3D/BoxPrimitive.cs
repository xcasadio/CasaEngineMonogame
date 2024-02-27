using Microsoft.Xna.Framework;

namespace CasaEngine.Engine.Primitives3D;

public class BoxPrimitive : GeometricPrimitive
{
#if EDITOR
    private float _width, _height, _length;
#endif

    public BoxPrimitive(float width = 1, float height = 1, float length = 1)
    {
#if EDITOR
        _width = width;
        _height = height;
        _length = length;
#endif

        //front
        AddVertex(false);
        AddVertex((Vector3.UnitZ * length - Vector3.UnitY * height - Vector3.UnitX * width) / 2, Vector3.UnitZ, Vector2.Zero);
        AddVertex((Vector3.UnitZ * length - Vector3.UnitY * height + Vector3.UnitX * width) / 2, Vector3.UnitZ, Vector2.UnitX);
        AddVertex((Vector3.UnitZ * length + Vector3.UnitY * height - Vector3.UnitX * width) / 2, Vector3.UnitZ, Vector2.UnitY);
        AddVertex((Vector3.UnitZ * length + Vector3.UnitY * height + Vector3.UnitX * width) / 2, Vector3.UnitZ, Vector2.One);

        //back
        AddVertex(true);
        AddVertex((-Vector3.UnitZ * length - Vector3.UnitY * height - Vector3.UnitX * width) / 2, -Vector3.UnitZ, Vector2.Zero);
        AddVertex((-Vector3.UnitZ * length - Vector3.UnitY * height + Vector3.UnitX * width) / 2, -Vector3.UnitZ, Vector2.UnitX);
        AddVertex((-Vector3.UnitZ * length + Vector3.UnitY * height - Vector3.UnitX * width) / 2, -Vector3.UnitZ, Vector2.UnitY);
        AddVertex((-Vector3.UnitZ * length + Vector3.UnitY * height + Vector3.UnitX * width) / 2, -Vector3.UnitZ, Vector2.One);

        //up
        AddVertex(true);
        AddVertex((-Vector3.UnitZ * length + Vector3.UnitY * height - Vector3.UnitX * width) / 2, Vector3.UnitY, Vector2.Zero);
        AddVertex((-Vector3.UnitZ * length + Vector3.UnitY * height + Vector3.UnitX * width) / 2, Vector3.UnitY, Vector2.UnitX);
        AddVertex((Vector3.UnitZ * length + Vector3.UnitY * height - Vector3.UnitX * width) / 2, Vector3.UnitY, Vector2.UnitY);
        AddVertex((Vector3.UnitZ * length + Vector3.UnitY * height + Vector3.UnitX * width) / 2, Vector3.UnitY, Vector2.One);

        //bottom
        AddVertex(false);
        AddVertex((-Vector3.UnitZ * length - Vector3.UnitY * height - Vector3.UnitX * width) / 2, -Vector3.UnitY, Vector2.Zero);
        AddVertex((-Vector3.UnitZ * length - Vector3.UnitY * height + Vector3.UnitX * width) / 2, -Vector3.UnitY, Vector2.UnitX);
        AddVertex((Vector3.UnitZ * length - Vector3.UnitY * height - Vector3.UnitX * width) / 2, -Vector3.UnitY, Vector2.UnitY);
        AddVertex((Vector3.UnitZ * length - Vector3.UnitY * height + Vector3.UnitX * width) / 2, -Vector3.UnitY, Vector2.One);

        //right
        AddVertex(true);
        AddVertex((-Vector3.UnitZ * length - Vector3.UnitY * height + Vector3.UnitX * width) / 2, Vector3.UnitX, Vector2.Zero);
        AddVertex((Vector3.UnitZ * length - Vector3.UnitY * height + Vector3.UnitX * width) / 2, Vector3.UnitX, Vector2.UnitX);
        AddVertex((-Vector3.UnitZ * length + Vector3.UnitY * height + Vector3.UnitX * width) / 2, Vector3.UnitX, Vector2.UnitY);
        AddVertex((Vector3.UnitZ * length + Vector3.UnitY * height + Vector3.UnitX * width) / 2, Vector3.UnitX, Vector2.One);

        //left
        AddVertex(false);
        AddVertex((-Vector3.UnitZ * length - Vector3.UnitY * height - Vector3.UnitX * width) / 2, -Vector3.UnitX, Vector2.Zero);
        AddVertex((Vector3.UnitZ * length - Vector3.UnitY * height - Vector3.UnitX * width) / 2, -Vector3.UnitX, Vector2.UnitX);
        AddVertex((-Vector3.UnitZ * length + Vector3.UnitY * height - Vector3.UnitX * width) / 2, -Vector3.UnitX, Vector2.UnitY);
        AddVertex((Vector3.UnitZ * length + Vector3.UnitY * height - Vector3.UnitX * width) / 2, -Vector3.UnitX, Vector2.One);
    }

    private void AddVertex(bool dir)
    {
        if (dir)
        {
            AddIndex(CurrentVertex + 0);
            AddIndex(CurrentVertex + 1);
            AddIndex(CurrentVertex + 2);

            AddIndex(CurrentVertex + 1);
            AddIndex(CurrentVertex + 3);
            AddIndex(CurrentVertex + 2);
        }
        else
        {
            AddIndex(CurrentVertex + 0);
            AddIndex(CurrentVertex + 2);
            AddIndex(CurrentVertex + 1);

            AddIndex(CurrentVertex + 1);
            AddIndex(CurrentVertex + 2);
            AddIndex(CurrentVertex + 3);
        }
    }
}