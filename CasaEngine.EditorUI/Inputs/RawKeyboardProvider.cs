#if EDITOR

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using CasaEngine.Core.Log;
using CasaEngine.Engine.Input.InputDeviceStateProviders;
using Microsoft.Xna.Framework.Input;

namespace CasaEngine.EditorUI.Inputs;

/// <summary>
/// Lit l'etat du clavier via Win32 GetAsyncKeyState, sans exiger le focus WPF.
/// Detecte le survol via ViewportBoundsCache — mis a jour sur le thread WPF,
/// consul te depuis le game thread sans aucun appel WPF.
/// </summary>
internal sealed class RawKeyboardProvider : IKeyboardStateProvider
{
    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT pt);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }

    private readonly ViewportBoundsCache _bounds;
    private bool _lastCursorOver;
    private bool _lastCursorOverInit;

    public RawKeyboardProvider(ViewportBoundsCache bounds)
    {
        _bounds = bounds;
        Logs.WriteDebug("[InputDiag] RawKeyboardProvider created");
    }

    /// <summary>
    /// Retourne true si le curseur Win32 est dans les bornes du viewport.
    /// 100% Win32 — safe depuis n'importe quel thread.
    /// </summary>
    public bool IsCursorOverViewport()
    {
        if (!GetCursorPos(out var pt)) return false;

        bool over = _bounds.Contains(pt.X, pt.Y);
        if (!_lastCursorOverInit || over != _lastCursorOver)
        {
            _lastCursorOver     = over;
            _lastCursorOverInit = true;
            Logs.WriteDebug(
                $"[InputDiag] IsCursorOverViewport={over} " +
                $"cursor=({pt.X},{pt.Y}) ForegroundHwnd=0x{GetForegroundWindow():X}");
        }
        return over;
    }

    public KeyboardState GetState()
    {
        if (!IsCursorOverViewport())
            return new KeyboardState();

        var pressed = new List<Keys>();
        for (var i = 8; i < 256; i++)
        {
            if ((GetAsyncKeyState(i) & 0x8000) != 0)
                pressed.Add((Keys)i);
        }

        if (pressed.Count > 0)
            Logs.WriteDebug($"[InputDiag] GetState pressed: {string.Join(", ", pressed)}");

        return new KeyboardState(pressed.ToArray());
    }
}

#endif
