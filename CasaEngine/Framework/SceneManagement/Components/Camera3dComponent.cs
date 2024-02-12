
using CasaEngine.Core.Serialization;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.SceneManagement.Components;

public abstract class Camera3dComponent : CameraComponent
{
    private float _fieldOfView;

    public float FieldOfView
    {
        get => _fieldOfView;
        set
        {
            _fieldOfView = Math.Clamp(value, 0.1f, MathHelper.Pi - 0.1f);
            _needToComputeProjectionMatrix = true;
        }
    }
    protected Camera3dComponent()
    {
    }

    protected Camera3dComponent(Camera3dComponent other) : base(other)
    {
        _fieldOfView = other._fieldOfView;
    }

    public override void InitializeWithWorld(World.World world)
    {
        base.InitializeWithWorld(world);

        ComputeFieldOfView();
    }

    protected override void ComputeProjectionMatrix()
    {
        _projectionMatrix = Matrix.CreatePerspectiveFieldOfView(
            _fieldOfView,
            _viewport.AspectRatio,
            _viewport.MinDepth,
            _viewport.MaxDepth);

        _needToComputeProjectionMatrix = false;
    }

    public override void OnScreenResized(int width, int height)
    {
        base.OnScreenResized(width, height);
        ComputeFieldOfView();
    }

    private void ComputeFieldOfView()
    {
        FieldOfView = MathHelper.PiOver4 * 1.777777777f /
                      Viewport.AspectRatio; //1920 / 1080 = 1.777777777 => in angle = MathHelper.PiOver4
    }

    public override void Load(JObject element)
    {
        base.Load(element);

        _fieldOfView = element["fieldOfView"].GetSingle();
    }

#if EDITOR

    public override void Save(JObject jObject)
    {
        base.Save(jObject);
        jObject.Add("fieldOfView", _fieldOfView);
    }
#endif
}