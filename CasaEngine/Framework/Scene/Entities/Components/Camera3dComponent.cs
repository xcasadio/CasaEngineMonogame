using CasaEngine.Core.Serialization;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Scene.Entities.Components;

public abstract class Camera3dComponent : CameraComponent
{
    /// <summary>Vertical field of view of a camera that does not set one, in radians (45 degrees).</summary>
    public const float DefaultFieldOfView = MathHelper.PiOver4;

    private float _fieldOfView = DefaultFieldOfView;

    /// <summary>
    /// The vertical field of view, in radians. It is kept whatever the shape of the viewport: a wider viewport shows more
    /// on the sides, never less above and below (ADR-0095).
    /// </summary>
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

    public override void InitializeWithWorld(CasaEngine.Framework.Scene.World.World world)
    {
        base.InitializeWithWorld(world);

        if (_fieldOfView <= 0f)
        {
            FieldOfView = DefaultFieldOfView;
        }

        _needToComputeProjectionMatrix = true;
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

    public override void Load(JObject element)
    {
        base.Load(element);

        _fieldOfView = element["fieldOfView"].GetSingle();
    }

}
