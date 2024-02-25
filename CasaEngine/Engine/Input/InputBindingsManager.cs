namespace CasaEngine.Engine.Input;

public class InputBindingsManager
{
    public void Update(float elapsedTime)
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

    public ButtonMapperState GetInput(string name)
    {
        return new ButtonMapperState
        {
            IsKeyPressed = Button.Pressed(name),
            IsKeyJustPressed = Button.JustPressed(name),
            Value = Button.GetValue(name)
        };
    }
}