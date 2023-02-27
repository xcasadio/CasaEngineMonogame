using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;

namespace Microsoft.Xna.Framework.Input
{
    /// <summary>
    /// WpfKeyboard
    /// </summary>
    public class WpfKeyboard
    {
        [DllImport("user32.dll", EntryPoint = "GetKeyboardState", SetLastError = true)]
        static extern bool NativeGetKeyboardState([Out] byte[] keyStates);

        readonly WpfGame _focusElement;

        public WpfKeyboard(WpfGame focusElement) => _focusElement = focusElement ?? throw new ArgumentNullException(nameof(focusElement));

        public KeyboardState GetState()
        {
            if (_focusElement.IsMouseDirectlyOver && System.Windows.Input.Keyboard.FocusedElement != _focusElement)
            {
                // we assume the user wants keyboard input into the control when his mouse is over it in order for the events to register we must focus it
                // however, only focus if we are the active window, otherwise the window will become active and pop into foreground just by hovering the mouse over the game panel
                // finally check if user wants us to focus already on mouse over otherwise, don't focus (and let WpfMouse manually set focus)
                if (_focusElement.IsControlOnActiveWindow() && _focusElement.FocusOnMouseOver)
                {
                    _focusElement.Focus();
                }
            }
            return new KeyboardState(GetKeys(_focusElement));
        }

        static Keys[] GetKeys(IInputElement focusElement)
        {
            var keyStates = new byte[256];
            if (!NativeGetKeyboardState(keyStates))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            var pressedKeys = new List<Keys>();
            if (focusElement.IsKeyboardFocused)
            {
                for (var i = 8; i < keyStates.Length; i++)
                {
                    var key = keyStates[i];
                    if ((key & 0x80) != 0)
                    // This is just for a short demo, you may want this to return multiple keys!
                    {
                        if (key != 0)
                        {
                            pressedKeys.Add((Keys)i);
                        }
                    }
                }
            }

            return pressedKeys.ToArray();
        }
    }
}