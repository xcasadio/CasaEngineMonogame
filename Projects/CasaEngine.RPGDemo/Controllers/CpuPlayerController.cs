using CasaEngine.Framework.Game;
using CasaEngine.Framework.World;
using CasaEngine.RPGDemo.Controllers.CpuPlayerState;
using Microsoft.Xna.Framework;
using static CasaEngine.RPGDemo.Controllers.Character;

namespace CasaEngine.RPGDemo.Controllers;

public class CpuPlayerController : AiController
{
    private World world;

    public CpuPlayerController(Character character)
        : base(character)
    {
        //character.IsPLayer = true;
    }

    public override void InitializePrivate(World world)
    {
        base.InitializePrivate(world);

        world = world;

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

        if (Character.Position.X > world.Game.GraphicsDevice.Viewport.Width - 50)
        {
            Character.SetAnimation(AnimationIndices.Walk);
        }

        Character.Move(ref dir);
        Character.CurrentDirection = character2dDirection;
    }
}