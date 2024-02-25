using Microsoft.Xna.Framework;

namespace CasaEngine.Engine.Input;

public class InputMappingManager
{
    private readonly List<InputMapping> _inputMappings = new();

    public void AddInputMapping(InputMapping inputMapping)
    {
        _inputMappings.Add(inputMapping);
    }

    public void RemoveInputMapping(InputMapping inputMapping)
    {
        _inputMappings.Remove(inputMapping);
    }

    public void Update(KeyboardManager keyboardManager, MouseManager mouseManager, GamePadManager gamePadManager)
    {
        foreach (var button in _inputMappings)
        {
            button.Update(keyboardManager, mouseManager, gamePadManager);
        }
    }

    public ButtonState GetButtonState(string name)
    {
        return new ButtonState
        {
            IsKeyPressed = IsPressed(name),
            IsKeyJustPressed = IsJustPressed(name),
            Value = GetValue(name)
        };
    }

    private bool IsPressed(string buttonName)
    {
        foreach (var inputMapping in _inputMappings)
        {
            if (inputMapping.Name == buttonName)
            {
                return inputMapping.Pressed;
            }
        }

        throw new InvalidOperationException("Input: the button named " + buttonName + " does not exist.");
    }

    private bool IsJustPressed(string buttonName)
    {
        foreach (var inputMapping in _inputMappings)
        {
            if (inputMapping.Name == buttonName)
            {
                return inputMapping.Pressed && !inputMapping.PressedPreviousFrame; ;
            }
        }

        throw new InvalidOperationException("Input: the button named " + buttonName + " does not exist.");
    }

    private float GetValue(string buttonName)
    {
        foreach (var inputMapping in _inputMappings)
        {
            if (inputMapping.Name == buttonName)
            {
                return inputMapping.Value;
            }
        }

        throw new InvalidOperationException("Input: the button named " + buttonName + " does not exist.");
    }
}