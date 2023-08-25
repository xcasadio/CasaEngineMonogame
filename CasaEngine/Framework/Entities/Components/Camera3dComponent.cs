using System.Text.Json;
using CasaEngine.Core.Design;
using CasaEngine.Framework.Game;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Entities.Components;

public abstract class Camera3dComponent : CameraComponent
{
    private float _fieldOfView;

    protected Camera3dComponent(Entity entity, int type) : base(entity, type)
    {
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

    public override void Initialize(CasaEngineGame game)
    {
        base.Initialize(game);
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

    public override void ScreenResized(int width, int height)
    {
        base.ScreenResized(width, height);
        ComputeFieldOfView();
    }

    private void ComputeFieldOfView()
    {
        FieldOfView =
            MathHelper.PiOver4 * 1.777777777f /
            Viewport.AspectRatio; //1920 / 1080 = 1.777777777 => in angle = MathHelper.PiOver4
    }

    public override void Load(JsonElement element, SaveOption option)
    {
        base.Load(element, option);

        _fieldOfView = element.GetProperty("fieldOfView").GetSingle();
    }

#if EDITOR

    public override void Save(JObject jObject, SaveOption option)
    {
        base.Save(jObject, option);
        jObject.Add("fieldOfView", _fieldOfView);
    }
#endif
}