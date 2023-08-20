using CasaEngine.Framework.Game;
using Microsoft.Xna.Framework;
using RPGDemo.Controllers.CpuPlayerState;
using static RPGDemo.Controllers.Character;

namespace RPGDemo.Controllers;

public class CpuPlayerController : AiController
{
    private CasaEngineGame _game;

    public CpuPlayerController(Character character)
        : base(character)
    {
        //character.IsPLayer = true;
    }

    public override void Initialize(CasaEngineGame game)
    {
        base.Initialize(game);

        _game = game;

        AddState(0, new CpuPlayerIdleState());
        StateMachine.Transition(GetState(0));

        Character.CurrentDirection = Character2dDirection.Right;
        Character.SetAnimation(AnimationIndices.Walk);
    }

    public override void Update(float elapsedTime)
    {
        base.Update(elapsedTime);

        var dir = Vector2.UnitX;
        var character2dDirection = Character2dDirection.Right;

        if (Character.Position.X > _game.GraphicsDevice.Viewport.Width - 50)
        {
            Character.SetAnimation(AnimationIndices.Walk);
        }

        Character.Move(ref dir);
        Character.CurrentDirection = character2dDirection;
    }
}