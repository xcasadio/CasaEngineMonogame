using CasaEngine.Core.Helpers;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.SceneManagement;

public class Geode : Node, IGeode
{
    private List<IDrawable> _drawables = new();
    public IReadOnlyList<IDrawable> Drawables => _drawables;

    protected BoundingBox _boundingBox;
    protected BoundingBox _initialBoundingBox = BoundingBoxHelper.Create();

    public event Func<INode, BoundingBox>? ComputeBoundingBoxCallback;

    public override void Accept(INodeVisitor nv)
    {
        if (nv.ValidNodeMask(this))
        {
            nv.PushOntoNodePath(this);
            nv.Apply(this);
            nv.PopFromNodePath(this);
        };
    }

    public void AddDrawable(IDrawable drawable)
    {
        _drawables.Add(drawable);
    }

    public BoundingBox GetBoundingBox()
    {
        if (_boundingSphereComputed)
        {
            return _boundingBox;
        }

        _boundingBox = _initialBoundingBox;

        _boundingBox.ExpandBy(ComputeBoundingBoxCallback?.Invoke(this) ?? ComputeBoundingBox());

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
        var boundingBox = BoundingBoxHelper.Create();
        foreach (var drawable in Drawables)
        {
            boundingBox.ExpandBy(drawable.GetBoundingBox());
        }

        return boundingBox;
    }
}