using System.Numerics;
using System.Text.Json;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;
using SharpDX.Direct3D9;

namespace CasaEngine.Framework.Entities.Components;

public abstract class Camera3dComponent : CameraComponent
{
    private float _fieldOfView;

    protected Camera3dComponent(Entity entity, int type) : base(entity, type)
    {
        _fieldOfView = MathHelper.PiOver4;
    }

    public float FieldOfView
    {
        get { return _fieldOfView; }
        set
        {
            _fieldOfView = Math.Clamp(value, 0.1f, MathHelper.Pi - 0.1f);
            _needToComputeProjectionMatrix = true;
#if EDITOR
            OnPropertyChanged();
#endif
        }
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

    public override void ScreenResized(int width, int height)
    {
        base.ScreenResized(width, height);
        FieldOfView = MathHelper.PiOver4 * 1.777777777f / Viewport.AspectRatio; //1920 / 1080 = 1.777777777 => in angle = MathHelper.PiOver4
    }

    public override void Load(JsonElement element)
    {
        base.Load(element);

        _fieldOfView = element.GetProperty("fieldOfView").GetSingle();
    }

#if EDITOR

    public override void Save(JObject jObject)
    {
        base.Save(jObject);
        jObject.Add("fieldOfView", _fieldOfView);
    }
#endif
}