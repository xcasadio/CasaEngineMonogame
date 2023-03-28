using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Entities.Components;

public abstract class CameraComponent : Component
{
    protected Matrix _viewMatrix;
    protected Matrix _projectionMatrix;
    protected Viewport _viewport;
    protected float _viewDistance; // distance between the camera and the near far clip plane
    protected bool _needToComputeProjectionMatrix;
    protected bool _needToComputeViewMatrix;

    public abstract Vector3 Position { get; }

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

    public Viewport Viewport
    {
        get { return _viewport; }
    }

    public float ViewDistance
    {
        get { return _viewDistance; }
    }

    protected CameraComponent(Entity entity, int type) : base(entity, type)
    {
        _needToComputeProjectionMatrix = true;
        _needToComputeViewMatrix = true;

        _viewport.Width = EngineComponents.Game.GraphicsDevice.PresentationParameters.BackBufferWidth;
        _viewport.Height = EngineComponents.Game.GraphicsDevice.PresentationParameters.BackBufferHeight;
        _viewport.MinDepth = 0.1f;
        _viewport.MaxDepth = 100000.0f;
    }

    public override void Load(JsonElement element)
    {
        throw new NotImplementedException();
    }

    protected abstract void ComputeProjectionMatrix();
    protected abstract void ComputeViewMatrix();

    public override void ScreenResized(int width, int height)
    {
        _viewport.Width = width;
        _viewport.Height = height;
        _needToComputeProjectionMatrix = true;
    }
}