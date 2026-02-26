#if EDITOR

using System;
using System.Runtime.InteropServices;
using CasaEngine.Core.Log;
using CasaEngine.Engine.Input.InputDeviceStateProviders;
using Microsoft.Xna.Framework.Input;

namespace CasaEngine.EditorUI.Inputs;

/// <summary>
/// Lit l'etat de la souris via Win32 — independant du routing WPF.
/// Position via GetCursorPos + ViewportBoundsCache.ToLocal (thread-safe).
/// Boutons via GetAsyncKeyState (thread-independant).
/// </summary>
internal sealed class RawMouseProvider : IMouseStateProvider
{
    private const int VK_LBUTTON  = 0x01;
    private const int VK_RBUTTON  = 0x02;
    private const int VK_MBUTTON  = 0x04;
    private const int VK_XBUTTON1 = 0x05;
    private const int VK_XBUTTON2 = 0x06;

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT pt);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }

    private readonly ViewportBoundsCache _bounds;
    private bool _lastRight;

    public RawMouseProvider(ViewportBoundsCache bounds)
    {
        _bounds = bounds;
    }

    public MouseState GetState()
    {
        int localX = 0, localY = 0;
        if (GetCursorPos(out var pt))
            (localX, localY) = _bounds.ToLocal(pt.X, pt.Y);

        var left   = (GetAsyncKeyState(VK_LBUTTON)  & 0x8000) != 0 ? ButtonState.Pressed : ButtonState.Released;
        var right  = (GetAsyncKeyState(VK_RBUTTON)  & 0x8000) != 0 ? ButtonState.Pressed : ButtonState.Released;
        var middle = (GetAsyncKeyState(VK_MBUTTON)  & 0x8000) != 0 ? ButtonState.Pressed : ButtonState.Released;
        var xb1    = (GetAsyncKeyState(VK_XBUTTON1) & 0x8000) != 0 ? ButtonState.Pressed : ButtonState.Released;
        var xb2    = (GetAsyncKeyState(VK_XBUTTON2) & 0x8000) != 0 ? ButtonState.Pressed : ButtonState.Released;

        bool rightDown = right == ButtonState.Pressed;
        if (rightDown != _lastRight)
        {
            _lastRight = rightDown;
            Logs.WriteDebug($"[InputDiag] RawMouseProvider RightButton={right} pos=({localX},{localY})");
        }

        return new MouseState(localX, localY, _bounds.ScrollWheelValue, left, middle, right, xb1, xb2);
    }
}

#endif
