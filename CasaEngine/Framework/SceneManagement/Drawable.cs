using System.Resources;
using CasaEngine.Core.Helpers;
using CasaEngine.Framework.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.SceneManagement;

public class Drawable : Object, IDrawable
{
    protected bool _boundingSphereComputed = false;
    protected BoundingSphere _boundingSphere = BoundingSphereExtension.Create();

    protected BoundingBox _boundingBox;
    protected BoundingBox _initialBoundingBox = BoundingBoxHelper.Create();

    public string Name { get; set; } = string.Empty;

    public BoundingBox InitialBoundingBox
    {
        get => _initialBoundingBox;
        set
        {
            _initialBoundingBox = value;
            DirtyBound();
        }
    }

    public StaticMesh Mesh { get; set; }

    public void DirtyBound()
    {
        if (!_boundingSphereComputed)
        {
            return;
        }

        _boundingSphereComputed = false;
    }

    public BoundingBox GetBoundingBox()
    {
        if (_boundingSphereComputed)
        {
            return _boundingBox;
        }

        _boundingBox = _initialBoundingBox;

        _boundingBox.ExpandBy(ComputeBoundingBox());

        if (_boundingBox.Valid())
        {
            _boundingSphere.Center = _boundingBox.GetCenter();
            _boundingSphere.Radius = _boundingBox.GetRadius();
        }
        else
        {
            _boundingSphere.Init();
        }

        _boundingSphereComputed = true;

        return _boundingBox;
    }

    protected BoundingBox ComputeBoundingBox()
    {
        var min = Vector3.One * int.MaxValue;
        var max = Vector3.One * int.MinValue;

        if (Mesh != null)
        {
            var vertices = Mesh.GetVertices();

            foreach (var vertex in vertices)
            {
                min = Vector3.Min(min, vertex.Position);
                max = Vector3.Max(max, vertex.Position);
            }
        }
        else // default box
        {
            const float length = 0.5f;
            min = Vector3.One * -length;
            max = Vector3.One * length;
        }

        return new BoundingBox(min, max);
    }
}