using System.Resources;
using CasaEngine.Core.Helpers;
using CasaEngine.Framework.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.SceneManagement;

public abstract class Drawable : Object, IDrawable
{
    public string Name { get; set; } = string.Empty;

    public Type VertexType => GetVertexType();


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

    public VertexDeclaration VertexLayout { get; set; }

    public List<IPrimitiveSet> PrimitiveSets { get; } = new();

    public event Func<Drawable, BoundingBox> ComputeBoundingBoxCallback;
    public event Action<GraphicsDevice, Drawable> DrawImplementationCallback;

    private IPipelineState _pipelineState = null;
    public IPipelineState PipelineState
    {
        get => _pipelineState;
        set => _pipelineState = value;
    }

    public bool HasPipelineState
    {
        get => null != _pipelineState;
    }

    public StaticMesh Mesh { get; set; }

    protected abstract Type GetVertexType();

    public void Draw(GraphicsDevice device, List<Tuple<uint, ResourceSet>> resourceSets)
    {
        if (null != DrawImplementationCallback)
        {
            DrawImplementationCallback(device, this);
        }
        else
        {
            DrawImplementation(device, resourceSets);
        }
    }

    protected abstract void DrawImplementation(GraphicsDevice device, List<Tuple<uint, ResourceSet>> resourceSets);

    public virtual void ConfigureDeviceBuffers(GraphicsDevice device)
    {
        // Nothing by default
    }

    public virtual void ConfigurePipelinesForDevice(GraphicsDevice device)
    {
        // Nothing by default
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

        _boundingBox.ExpandBy(null != ComputeBoundingBoxCallback
            ? ComputeBoundingBoxCallback(this)
            : ComputeBoundingBox());

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

    protected abstract BoundingBox ComputeBoundingBox();

    public abstract VertexBuffer GetVertexBufferForDevice(GraphicsDevice device);

    public abstract IndexBuffer GetIndexBufferForDevice(GraphicsDevice device);


}