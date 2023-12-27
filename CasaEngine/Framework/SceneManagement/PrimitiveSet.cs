using CasaEngine.Core.Helpers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.SceneManagement;

public abstract class PrimitiveSet : Object, IPrimitiveSet
{
    protected bool _boundingSphereComputed = false;
    protected BoundingSphere _boundingSphere = BoundingSphereExtension.Create();

    protected BoundingBox _boundingBox;
    protected BoundingBox _initialBoundingBox = BoundingBoxHelper.Create();

    public BoundingBox InitialBoundingBox
    {
        get => _initialBoundingBox;
        set
        {
            _initialBoundingBox = value;
            DirtyBound();
        }
    }

    public event Func<PrimitiveSet, BoundingBox> ComputeBoundingBoxCallback;

    public IDrawable Drawable { get; }

    public PrimitiveType PrimitiveTopology { get; set; }

    protected PrimitiveSet(IDrawable drawable, PrimitiveType primitiveTopology)
    {
        PrimitiveTopology = primitiveTopology;
        Drawable = drawable;
    }

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

    public abstract void Draw(GraphicsDevice graphicsDevice);

    protected abstract BoundingBox ComputeBoundingBox();
}