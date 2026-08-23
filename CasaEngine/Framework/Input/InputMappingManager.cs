using CasaEngine.Engine.Input;

namespace CasaEngine.Framework.Input;

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

    /// <summary>
    /// Returns true if an input mapping with the given name is registered.
    /// </summary>
    public bool Contains(string name)
    {
        return TryGet(name, out _);
    }

    /// <summary>
    /// Attempts to find a registered input mapping by name (exact, ordinal match).
    /// </summary>
    public bool TryGet(string name, out InputMapping? inputMapping)
    {
        foreach (var mapping in _inputMappings)
        {
            if (mapping.Name == name)
            {
                inputMapping = mapping;
                return true;
            }
        }

        inputMapping = null;
        return false;
    }

    public void Update(KeyboardManager keyboardManager, MouseManager mouseManager, GamePadManager gamePadManager)
    {
        foreach (var button in _inputMappings)
        {
            button.Update(keyboardManager, mouseManager, gamePadManager);
        }
    }

    public Engine.Input.ButtonState GetButtonState(string name)
    {
        return new Engine.Input.ButtonState
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