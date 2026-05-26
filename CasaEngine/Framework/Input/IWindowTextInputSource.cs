using MGUI.Shared.Input.Keyboard;

namespace CasaEngine.Framework.Input;

public interface IWindowTextInputSource
{
    void DrainTextInput(IKeyboardTextInputSink sink);
}