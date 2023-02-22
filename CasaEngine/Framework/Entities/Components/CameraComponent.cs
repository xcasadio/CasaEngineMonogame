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

    public Viewport Viewport => _viewport;

    public float ViewDistance => _viewDistance;

    protected CameraComponent(Entity entity, int type) : base(entity, type)
    {
        _needToComputeProjectionMatrix = true;
        _needToComputeViewMatrix = true;

        //m_WindowResizedConnection = Game::Instance().GetGlobalEventSet().subscribeEvent(
        //    WindowResizeEvent::GetEventName(),
        //    Event::Subscriber(&OnWindowResized, this));

        _viewport.Width = Game.Engine.Instance.Game.Window.ClientBounds.Width;
        _viewport.Height = Game.Engine.Instance.Game.Window.ClientBounds.Height;
        _viewport.MinDepth = 0.1f;
        _viewport.MaxDepth = 100000.0f;
    }

    ~CameraComponent()
    {
        //m_WindowResizedConnection->disconnect();
    }

    public override void Load(JsonElement element)
    {
        throw new NotImplementedException();
    }

    protected abstract void ComputeProjectionMatrix();
    protected abstract void ComputeViewMatrix();

    private bool OnWindowResized()
    {
        //m_needToComputeProjectionMatrix = true;
        //
        //const float d_yfov_tan = 0.267949192431123f;
        //
        //const float w = m_Viewport.Width() * Game::Instance().GetWindowSize().x;
        //const float h = m_Viewport.Height() * Game::Instance().GetWindowSize().y;
        //const float aspect = w / h;
        //const float midx = w * 0.5f;
        ////const float midy = h * 0.5f;
        //m_ViewDistance = midx / (aspect* d_yfov_tan);
        //
        return false;
    }
}