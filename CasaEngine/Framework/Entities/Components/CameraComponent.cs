using System.Text.Json;
using CasaEngine.Core.Helpers;
using CasaEngine.Framework.Game;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json.Linq;

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

    public Viewport Viewport => _viewport;

    public float ViewDistance => _viewDistance;

    protected CameraComponent(Entity entity, int type) : base(entity, type)
    {
        _needToComputeProjectionMatrix = true;
        _needToComputeViewMatrix = true;
    }

    public override void Initialize(CasaEngineGame game)
    {
        base.Initialize(game);

        _viewport.Width = game.GraphicsDevice.PresentationParameters.BackBufferWidth;
        _viewport.Height = game.GraphicsDevice.PresentationParameters.BackBufferHeight;
        _viewport.MinDepth = 0.1f;
        _viewport.MaxDepth = 100000.0f;
    }

    public override void Load(JsonElement element)
    {
        _viewDistance = element.GetProperty("view_distance").GetSingle();
        _viewport = element.GetJsonPropertyByName("viewport").Value.GetViewPort();
    }

    protected abstract void ComputeProjectionMatrix();
    protected abstract void ComputeViewMatrix();

    public override void ScreenResized(int width, int height)
    {
        _viewport.Width = width;
        _viewport.Height = height;
        _needToComputeProjectionMatrix = true;
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