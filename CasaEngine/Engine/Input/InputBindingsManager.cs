namespace CasaEngine.Engine.Input;

public class InputBindingsManager
{
    private readonly Dictionary<string, Action<ButtonMapperState>> _bindingsDelegate = new();

    public void AddInputBindingsDelegate(string name, Action<ButtonMapperState> action)
    {
        _bindingsDelegate[name] = action;
    }

    public ButtonMapperState GetInput(string name)
    {
        return new ButtonMapperState
        {
            IsKeyPressed = Button.Pressed(name),
            IsKeyJustPressed = Button.JustPressed(name),
            Value = Button.GetValue(name)
        };
    }

    public void Update(float elapsedTime)
    {
        InternalUpdate(elapsedTime);

        foreach (var pair in _bindingsDelegate)
        {
            var buttonMapperState = GetInput(pair.Key);

            if (buttonMapperState.IsKeyPressed || buttonMapperState.IsKeyJustPressed || buttonMapperState.Value != 0f)
            {
                pair.Value(buttonMapperState);
            }
        }
    }

    private void InternalUpdate(float elapsedTime)
    {
        GamePad.PlayerOne.Update();
        GamePad.PlayerTwo.Update();
        GamePad.PlayerThree.Update();
        GamePad.PlayerFour.Update();
        Keyboard.Update();
        Mouse.Update();

        foreach (var axis in Axis.Axes)
        {
            axis.Update(elapsedTime);
        }

        foreach (var button in Button.Buttons)
        {
            button.Update();
        }
    }
}