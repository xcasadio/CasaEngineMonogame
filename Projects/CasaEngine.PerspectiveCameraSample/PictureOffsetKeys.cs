using CasaEngine.Framework.Input;
using CasaEngine.Framework.Physics;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Scene.World;
using CasaEngine.Framework.Scripting;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace CasaEngine.PerspectiveCameraSample;

/// <summary>
/// Scrolls the picture of the <see cref="PerspectiveCameraComponent"/> of its entity with the arrow keys, by changing
/// <see cref="PerspectiveCameraComponent.PictureOffset"/>: the camera itself never moves. Space puts the offset back to zero.
/// </summary>
public class PictureOffsetKeys : GameplayProxy
{
    /// <summary>Change of the offset per second while a key is held, in fractions of the viewport.</summary>
    public const float Speed = 0.5f;

    /// <summary>Largest offset on each axis, in fractions of the viewport.</summary>
    public const float Limit = 0.5f;

    private PerspectiveCameraComponent _camera;
    private InputComponent _inputComponent;

    protected override void InitializePrivate()
    {
        base.InitializePrivate();

        _camera = Owner.GetComponent<PerspectiveCameraComponent>();
    }

    public override void InitializeWithWorld(World world)
    {
        _inputComponent = world.Game.InputComponent;
    }

    public override void Update(float elapsedTime)
    {
        if (_camera == null || _inputComponent == null)
        {
            return;
        }

        KeyboardState keyboard = _inputComponent.KeyboardManager.State;
        if (keyboard.IsKeyDown(Keys.Space))
        {
            _camera.PictureOffset = Vector2.Zero;
            return;
        }

        Vector2 offset = _camera.PictureOffset;
        float step = Speed * elapsedTime;

        if (keyboard.IsKeyDown(Keys.Right))
        {
            offset.X += step;
        }

        if (keyboard.IsKeyDown(Keys.Left))
        {
            offset.X -= step;
        }

        if (keyboard.IsKeyDown(Keys.Down))
        {
            offset.Y += step;
        }

        if (keyboard.IsKeyDown(Keys.Up))
        {
            offset.Y -= step;
        }

        offset.X = MathHelper.Clamp(offset.X, -Limit, Limit);
        offset.Y = MathHelper.Clamp(offset.Y, -Limit, Limit);

        if (offset != _camera.PictureOffset)
        {
            _camera.PictureOffset = offset;
        }
    }

    public override void Draw()
    {
    }

    public override void OnHit(Collision collision)
    {
    }

    public override void OnHitEnded(Collision collision)
    {
    }

    public override void OnBeginPlay(World world)
    {
    }

    public override void OnEndPlay(World world)
    {
    }

    public override IGameplayProxy Clone()
    {
        return new PictureOffsetKeys();
    }
}
