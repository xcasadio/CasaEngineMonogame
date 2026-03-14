using CasaEngine.Engine.Input.InputDeviceStateProviders;
using MGUI.Shared.Input;
using Microsoft.Xna.Framework.Input;
using System.Runtime.InteropServices;

namespace CasaEngine.Framework.Input;

public sealed class Win32WindowInputSource : IWindowInputSource, IRawInputSource, IKeyboardStateProvider, IMouseStateProvider
{
    private const int VK_LBUTTON = 0x01;
    private const int VK_RBUTTON = 0x02;
    private const int VK_MBUTTON = 0x04;
    private const int VK_XBUTTON1 = 0x05;
    private const int VK_XBUTTON2 = 0x06;

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern bool ScreenToClient(IntPtr hWnd, ref POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    private readonly Func<IntPtr> _getWindowHandle;

    public Win32WindowInputSource(Func<IntPtr> getWindowHandle)
    {
        ArgumentNullException.ThrowIfNull(getWindowHandle);
        _getWindowHandle = getWindowHandle;
    }

    public WindowInputSnapshot GetSnapshot()
    {
        var mouseState = GetMouseState();
        var keyboardState = GetKeyboardState();
        return new WindowInputSnapshot(keyboardState, mouseState);
    }

    public MouseState GetMouseState()
    {
        return GetWindowMouseState();
    }

    MouseState IMouseStateProvider.GetState()
    {
        return GetWindowMouseState();
    }

    KeyboardState IKeyboardStateProvider.GetState()
    {
        return GetKeyboardState();
    }

    private MouseState GetWindowMouseState()
    {
        var fallbackState = Mouse.GetState();
        var handle = _getWindowHandle();
        if (handle == IntPtr.Zero || !GetCursorPos(out var point) || !ScreenToClient(handle, ref point))
        {
            return fallbackState;
        }

        var left = (GetAsyncKeyState(VK_LBUTTON) & 0x8000) != 0 ? ButtonState.Pressed : ButtonState.Released;
        var right = (GetAsyncKeyState(VK_RBUTTON) & 0x8000) != 0 ? ButtonState.Pressed : ButtonState.Released;
        var middle = (GetAsyncKeyState(VK_MBUTTON) & 0x8000) != 0 ? ButtonState.Pressed : ButtonState.Released;
        var xButton1 = (GetAsyncKeyState(VK_XBUTTON1) & 0x8000) != 0 ? ButtonState.Pressed : ButtonState.Released;
        var xButton2 = (GetAsyncKeyState(VK_XBUTTON2) & 0x8000) != 0 ? ButtonState.Pressed : ButtonState.Released;

        return new MouseState(
            point.X,
            point.Y,
            fallbackState.ScrollWheelValue,
            left,
            middle,
            right,
            xButton1,
            xButton2,
            fallbackState.HorizontalScrollWheelValue);
    }

    public KeyboardState GetKeyboardState()
    {
        return Keyboard.GetState();
    }
}
