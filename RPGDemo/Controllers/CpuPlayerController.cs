using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Game;
using Microsoft.Xna.Framework;
using RPGDemo.Controllers.CpuPlayerState;

namespace RPGDemo.Controllers;

public class CpuPlayerController
    : AiController
{
    //to delete
    int _s = 0;
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
        Character.SetAnimation((int)AnimationIndex.WalkRight);
    }

    public override void Update(float elapsedTime)
    {
        base.Update(elapsedTime);

        Vector2 dir;
        Character2dDirection character2dDirection;

        if (_s == 0)
        {
            dir = Vector2.UnitX;
            character2dDirection = Character2dDirection.Right;

            if (Character.Position.X > _game.GraphicsDevice.Viewport.Width - 50)
            {
                _s = 1;
                Character.SetAnimation((int)AnimationIndex.WalkLeft);
            }
        }
        else
        {
            dir = -Vector2.UnitX;
            character2dDirection = Character2dDirection.Left;

            if (Character.Position.X < 50)
            {
                _s = 0;
                Character.SetAnimation((int)AnimationIndex.WalkRight);
            }
        }

        Character.Move(ref dir);
        Character.CurrentDirection = character2dDirection;
    }
}