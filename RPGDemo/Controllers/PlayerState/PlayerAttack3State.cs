using CasaEngine.Framework.AI.Messaging;
using CasaEngine.Framework.AI.StateMachines;
using Microsoft.Xna.Framework;

namespace RPGDemo.Controllers.PlayerState;

public class PlayerAttack3State : IState<Controller>
{
    public string Name => "Player Attack3 State";

    public void Enter(Controller controller)
    {
        controller.Character.DoANewAttack();

        Vector2 joyDir = Vector2.Zero;
        controller.Character.Move(ref joyDir);
        controller.Character.SetAnimation(Character.AnimationIndices.Attack3);
    }

    public void Exit(Controller controller)
    {
        controller.Character.ComboNumber = 0;
    }

    public void Update(Controller controller, float elapsedTime)
    { }

    public bool HandleMessage(Controller controller, Message message)
    {
        if (message.Type == (int)MessageType.AnimationChanged)
        {
            controller.StateMachine.Transition(controller.GetState((int)PlayerControllerState.Idle));
        }

        return false;
    }
}