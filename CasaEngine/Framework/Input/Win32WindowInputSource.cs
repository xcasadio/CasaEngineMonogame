using CasaEngine.Engine.Input.Providers;
using MGUI.Shared.Input;
using Microsoft.Xna.Framework.Input;
using System.Collections.Generic;
using System.Threading;
using System.Runtime.InteropServices;

namespace CasaEngine.Framework.Input;

public sealed class Win32WindowInputSource : IWindowInputSource, IRawInputSource, IKeyboardStateProvider, IMouseStateProvider
{
    private const int WH_GETMESSAGE = 3;
    private const int WM_MOUSEWHEEL = 0x020A;
    private const int WM_MOUSEHWHEEL = 0x020E;
    private const int VK_LBUTTON = 0x01;
    private const int VK_RBUTTON = 0x02;
    private const int VK_MBUTTON = 0x04;
    private const int VK_XBUTTON1 = 0x05;
    private const int VK_XBUTTON2 = 0x06;

    private delegate IntPtr HookProc(int code, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public UIntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public POINT pt;
        public uint lPrivate;
    }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern bool ScreenToClient(IntPtr hWnd, ref POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern nint SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hmod, uint dwThreadId);

    [DllImport("user32.dll")]
    private static extern bool UnhookWindowsHookEx(nint hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(nint hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    private readonly Func<IntPtr> _getWindowHandle;
    private readonly HookProc _messageHookProc;
    private nint _messageHookHandle;
    private IntPtr _hookWindowHandle;
    private int _scrollWheelValue;
    private int _horizontalScrollWheelValue;

    public Win32WindowInputSource(Func<IntPtr> getWindowHandle)
    {
        ArgumentNullException.ThrowIfNull(getWindowHandle);
        _getWindowHandle = getWindowHandle;
        _messageHookProc = MessageHookCallback;
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
        EnsureMessageHookInstalled();

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
            Volatile.Read(ref _scrollWheelValue),
            left,
            middle,
            right,
            xButton1,
            xButton2,
            Volatile.Read(ref _horizontalScrollWheelValue));
    }

    public KeyboardState GetKeyboardState()
    {
        var handle = _getWindowHandle();
        if (handle == IntPtr.Zero || GetForegroundWindow() != handle)
        {
            return new KeyboardState();
        }

        var pressedKeys = new List<Keys>();
        for (var virtualKey = 8; virtualKey < 256; virtualKey++)
        {
            if ((GetAsyncKeyState(virtualKey) & 0x8000) != 0)
            {
                pressedKeys.Add((Keys)virtualKey);
            }
        }

        return new KeyboardState(pressedKeys.ToArray());
    }

    private void EnsureMessageHookInstalled()
    {
        var handle = _getWindowHandle();
        if (handle == IntPtr.Zero || (_messageHookHandle != 0 && _hookWindowHandle == handle))
        {
            return;
        }

        if (_messageHookHandle != 0)
        {
            UnhookWindowsHookEx(_messageHookHandle);
            _messageHookHandle = 0;
            _hookWindowHandle = IntPtr.Zero;
        }

        uint threadId = GetWindowThreadProcessId(handle, out _);
        if (threadId == 0)
        {
            return;
        }

        _messageHookHandle = SetWindowsHookEx(WH_GETMESSAGE, _messageHookProc, IntPtr.Zero, threadId);
        if (_messageHookHandle != 0)
        {
            _hookWindowHandle = handle;
        }
    }

    private IntPtr MessageHookCallback(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0 && lParam != IntPtr.Zero)
        {
            var message = Marshal.PtrToStructure<MSG>(lParam);
            if (message.hwnd == _hookWindowHandle)
            {
                if (message.message == WM_MOUSEWHEEL)
                {
                    int delta = (short)(((uint)message.wParam) >> 16);
                    Interlocked.Add(ref _scrollWheelValue, delta);
                }
                else if (message.message == WM_MOUSEHWHEEL)
                {
                    int delta = (short)(((uint)message.wParam) >> 16);
                    Interlocked.Add(ref _horizontalScrollWheelValue, delta);
                }
            }
        }

        return CallNextHookEx(_messageHookHandle, code, wParam, lParam);
    }
}
