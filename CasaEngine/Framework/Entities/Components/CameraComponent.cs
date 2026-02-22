using CasaEngine.Core.Serialization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Entities.Components;

public abstract class CameraComponent : SceneComponent
{
    protected Matrix _viewMatrix;
    protected Matrix _projectionMatrix;
    protected Viewport _viewport;
    protected float _viewDistance; // distance between the camera and the near far clip plane
    protected bool _needToComputeProjectionMatrix;
    protected bool _needToComputeViewMatrix;

    public Matrix ViewMatrix
    {
        get
        {
            if (_needToComputeViewMatrix)
            {
                ComputeViewMatrix();
                _needToComputeViewMatrix = false;
            }

            return _viewMatrix;
        }
    }

    public Matrix ProjectionMatrix
    {
        get
        {
            if (_needToComputeProjectionMatrix)
            {
                ComputeProjectionMatrix();
                _needToComputeProjectionMatrix = false;
            }

            return _projectionMatrix;
        }
    }

    public Viewport Viewport => _viewport;

    public float ViewDistance => _viewDistance;

    protected CameraComponent()
    {
        _needToComputeProjectionMatrix = true;
        _needToComputeViewMatrix = true;
    }

    protected CameraComponent(CameraComponent other) : base(other)
    {
        _needToComputeProjectionMatrix = true;
        _needToComputeViewMatrix = true;

        other._viewport = _viewport;
        other._viewDistance = _viewDistance;
    }

    protected override void InitializePrivate()
    {
        base.InitializePrivate();
    }

    public override void InitializeWithWorld(World.World world)
    {
        base.InitializeWithWorld(world);

        _viewport.Width = Owner.World.Game.ScreenSizeWidth;
        _viewport.Height = Owner.World.Game.ScreenSizeHeight;
        _viewport.MinDepth = 0.0f;  // normalized depth buffer range [0,1]
        _viewport.MaxDepth = 1.0f;  // near/far clip planes are in ProjectionMatrix
    }

    protected abstract void ComputeProjectionMatrix();
    protected abstract void ComputeViewMatrix();
    public abstract void SetPositionAndTarget(Vector3 position, Vector3 target);

    public override void OnScreenResized(int width, int height)
    {
        _viewport.Width = width;
        _viewport.Height = height;
        _needToComputeProjectionMatrix = true;
    }

    public override BoundingBox GetBoundingBox()
    {
        return new BoundingBox(-Vector3.One, Vector3.One);
    }

    public override void Load(JObject element)
    {
        base.Load(element);
        _viewDistance = element["view_distance"].GetSingle();
        _viewport = element["viewport"].GetViewPort();
    }

#if EDITOR

    public override void Save(JObject jObject)
    {
        base.Save(jObject);

        jObject.Add("view_distance", _viewDistance);

        var newjObject = new JObject();
        Viewport.Save(newjObject);
        jObject.Add("viewport", newjObject);
    }
#endif
}