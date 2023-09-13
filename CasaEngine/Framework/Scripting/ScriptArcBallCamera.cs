using System.Text.Json;
using CasaEngine.Core.Design;
using CasaEngine.Core.Helpers;
using CasaEngine.Engine.Input;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Entities.Components;
using CasaEngine.Framework.Game;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Scripting;

public class ScriptArcBallCamera : ExternalComponent
{
    public override int ExternalComponentId => (int)ScriptIds.ArcBallCamera;

    private ArcBallCameraComponent? _arcBallCameraComponent;
    private InputComponent _inputComponent;

    private readonly float _inputTurnRate;

    public float InputDistanceRate { get; set; }

    public float InputDisplacementRate { get; set; }

    public ScriptArcBallCamera()
    {
        InputDistanceRate = 3.0f;
        _inputTurnRate = 0.3f;
        InputDisplacementRate = 10.0f;
    }

    public override void Initialize(Entity entity, CasaEngineGame game)
    {
        _arcBallCameraComponent = entity.ComponentManager.GetComponent<ArcBallCameraComponent>();
        _inputComponent = game.GetGameComponent<InputComponent>();
    }

    public override void Update(float elapsedTime)
    {
        var rightAxis = 0.0f;
        var upAxis = 0.0f;
        var forwardAxis = 0.0f;
        var horizontalOrbit = 0.0f;
        var verticalOrbit = 0.0f;
        var rollOrbit = 0.0f;
        var zoom = 0.0f;

        const float step = 1.0f;


        if (_inputComponent.IsKeyPressed(Keys.Right))
        {
            rightAxis = step;
        }
        else if (_inputComponent.IsKeyPressed(Keys.Left))
        {
            rightAxis = -step;
        }

        if (_inputComponent.IsKeyPressed(Keys.Up))
        {
            forwardAxis = -step;
        }
        else if (_inputComponent.IsKeyPressed(Keys.Down))
        {
            forwardAxis = step;
        }

        if (_inputComponent.IsKeyPressed(Keys.PageUp))
        {
            upAxis = -step;
        }
        else if (_inputComponent.IsKeyPressed(Keys.PageDown))
        {
            upAxis = step;
        }

        if (_inputComponent.MouseRightButtonPressed)
        {
            horizontalOrbit = _inputComponent.MouseXMovement;
            verticalOrbit = -_inputComponent.MouseYMovement;
        }

        //#if EDITOR
        rightAxis = -rightAxis;
        upAxis = -upAxis;
        horizontalOrbit = -horizontalOrbit;
        verticalOrbit = -verticalOrbit;
        //#endif

        //Touch
        //if (_inputComponent.IsTouchMove(0) == true)
        //{
        //    horizontalOrbit = -Game.Instance().GetInput().TouchMoveDeltaX(0);
        //    verticalOrbit = -Game.Instance().GetInput().TouchMoveDeltaY(0);
        //}

        HandleControls(elapsedTime, rightAxis, upAxis, forwardAxis, horizontalOrbit, verticalOrbit, rollOrbit, zoom);
    }

    //horizontalAxis value between -1.0 and 1.0
    //verticalAxis value between -1.0 and 1.0
    //rollAxis value between -1.0 and 1.0
    private void HandleControls(float elapsedTime, float rightAxis, float upAxis, float forwardAxis, float horizontalOrbit, float verticalOrbit,
        float rollOrbit, float zoom)
    {
        var rightOffset = rightAxis * elapsedTime * InputDisplacementRate;
        var upOffset = upAxis * elapsedTime * InputDisplacementRate;
        var forwardOffset = forwardAxis * elapsedTime * InputDisplacementRate;

        var horizontalOrbitOffset = horizontalOrbit * elapsedTime * _inputTurnRate;
        var verticalOrbitOffset = verticalOrbit * elapsedTime * _inputTurnRate;
        //float dR = rollOrbit_ * elapsedTime * _inputTurnRate;

        if (horizontalOrbitOffset != 0.0f)
        {
            _arcBallCameraComponent.RotateTargetRight(horizontalOrbitOffset);
            _arcBallCameraComponent.OrbitRight(horizontalOrbitOffset);
        }

        if (verticalOrbitOffset != 0.0f)
        {
            _arcBallCameraComponent.RotateTargetUp(verticalOrbitOffset);
            _arcBallCameraComponent.OrbitUp(-verticalOrbitOffset);
        }
        //if ( dR != 0.0f ) RotateClockwise( dR );

        if (!MathUtils.IsZero(zoom))
        {
            _arcBallCameraComponent.Distance = Math.Max(_arcBallCameraComponent.Distance + zoom * elapsedTime * InputDistanceRate, 0.001f);
        }

        if (!MathUtils.IsZero(rightOffset) || !MathUtils.IsZero(upOffset) || !MathUtils.IsZero(forwardOffset))
        {
            var pos = _arcBallCameraComponent.Target +
                      _arcBallCameraComponent.Right * rightOffset +
                      _arcBallCameraComponent.Up * upOffset +
                      _arcBallCameraComponent.Direction * forwardOffset;
            _arcBallCameraComponent.Target = pos;
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

    public override void Load(JsonElement element, SaveOption option)
    {

    }

#if EDITOR

    public override void Save(JObject jObject, SaveOption option)
    {

    }

#endif
}