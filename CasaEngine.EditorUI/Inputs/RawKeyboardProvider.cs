#if EDITOR

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using CasaEngine.Core.Log;
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

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }

    private readonly FrameworkElement _viewport;

    // Throttle : on ne log le changement d'etat qu'une seule fois en cas de transition.
    private bool _lastCursorOver     = false;
    private bool _lastCursorOverInit = false;

    public RawKeyboardProvider(FrameworkElement viewport)
    {
        _viewport = viewport;
        Logs.WriteDebug($"[InputDiag] RawKeyboardProvider created for {viewport.GetType().Name}");
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
            bool over = local.X >= 0 && local.Y >= 0
                && local.X < _viewport.ActualWidth
                && local.Y < _viewport.ActualHeight;

            // Log uniquement lors d'un changement d'etat (pas chaque frame)
            if (!_lastCursorOverInit || over != _lastCursorOver)
            {
                _lastCursorOver     = over;
                _lastCursorOverInit = true;
                var fgWnd = GetForegroundWindow();
                Logs.WriteDebug(
                    $"[InputDiag] IsCursorOverViewport={over} " +
                    $"cursor=({pt.X},{pt.Y}) local=({local.X:F0},{local.Y:F0}) " +
                    $"vpSize=({_viewport.ActualWidth:F0}x{_viewport.ActualHeight:F0}) " +
                    $"ForegroundHwnd=0x{fgWnd:X}");
            }
            return over;
        }
        catch (Exception ex)
        {
            Logs.WriteDebug($"[InputDiag] IsCursorOverViewport exception: {ex.Message}");
            return false;
        }
    }

    public KeyboardState GetState()
    {
        if (!IsCursorOverViewport())
            return new KeyboardState();

        var keyStates = new byte[256];
        if (!NativeGetKeyboardState(keyStates))
        {
            Logs.WriteDebug("[InputDiag] NativeGetKeyboardState failed");
            return new KeyboardState();
        }

        var pressed = new List<Keys>();
        for (var i = 8; i < keyStates.Length; i++)
        {
            if ((keyStates[i] & 0x80) != 0)
                pressed.Add((Keys)i);
        }

        if (pressed.Count > 0)
            Logs.WriteDebug($"[InputDiag] GetState pressed: {string.Join(", ", pressed)}");

        return new KeyboardState(pressed.ToArray());
    }
}

#endif
