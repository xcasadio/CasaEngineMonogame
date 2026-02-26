#if EDITOR

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using CasaEngine.Engine.Input.InputDeviceStateProviders;
using Microsoft.Xna.Framework.Input;

namespace CasaEngine.EditorUI.Inputs;

/// <summary>
/// Lit l'etat du clavier via Win32 GetKeyboardState, sans exiger le focus WPF.
/// Detecte le survol du viewport via GetCursorPos + PointFromScreen —
/// independant du routing WPF et du hit-testing D3D11Image non fiable.
/// </summary>
internal sealed class RawKeyboardProvider : IKeyboardStateProvider
{
    [DllImport("user32.dll", EntryPoint = "GetKeyboardState", SetLastError = true)]
    private static extern bool NativeGetKeyboardState([Out] byte[] keyStates);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT pt);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }

    private readonly FrameworkElement _viewport;

    public RawKeyboardProvider(FrameworkElement viewport)
    {
        _viewport = viewport;
    }

    /// <summary>
    /// Retourne true si le curseur Win32 est dans les bornes ecran du viewport.
    /// Fonctionne meme quand le hit-testing WPF/D3D11 echoue.
    /// </summary>
    public bool IsCursorOverViewport()
    {
        if (!GetCursorPos(out var pt))
            return false;
        try
        {
            var local = _viewport.PointFromScreen(new Point(pt.X, pt.Y));
            return local.X >= 0 && local.Y >= 0
                && local.X < _viewport.ActualWidth
                && local.Y < _viewport.ActualHeight;
        }
        catch { return false; }
    }

    public KeyboardState GetState()
    {
        if (!IsCursorOverViewport())
            return new KeyboardState();

        var keyStates = new byte[256];
        if (!NativeGetKeyboardState(keyStates))
            return new KeyboardState();

        var pressed = new List<Keys>();
        for (var i = 8; i < keyStates.Length; i++)
        {
            if ((keyStates[i] & 0x80) != 0)
                pressed.Add((Keys)i);
        }
        return new KeyboardState(pressed.ToArray());
    }
}

#endif
