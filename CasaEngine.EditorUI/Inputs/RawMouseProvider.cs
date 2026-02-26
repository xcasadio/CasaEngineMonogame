#if EDITOR

using System.Runtime.InteropServices;
using System.Windows;
using CasaEngine.Core.Log;
using CasaEngine.Engine.Input.InputDeviceStateProviders;
using Microsoft.Xna.Framework.Input;

namespace CasaEngine.EditorUI.Inputs;

/// <summary>
/// Lit l'etat de la souris via Win32 — independant du routing WPF et du
/// hit-testing D3D11Image non fiable.
/// - Position      : GetCursorPos + PointFromScreen (coords viewport-locales)
/// - Boutons       : GetAsyncKeyState (VK_LBUTTON / VK_RBUTTON / VK_MBUTTON)
/// - Scroll wheel  : non implemente (les cameras editeur ne l'utilisent pas)
/// </summary>
internal sealed class RawMouseProvider : IMouseStateProvider
{
    // virtual-key codes
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

    private readonly FrameworkElement _viewport;

    // Diagnostic : log le changement d'etat des boutons droits (orbite camera).
    private bool _lastRight;

    public RawMouseProvider(FrameworkElement viewport)
    {
        _viewport = viewport;
    }

    public MouseState GetState()
    {
        // Lire la position cursor en coords viewport-locales.
        int localX = 0, localY = 0;
        if (GetCursorPos(out var pt) &&
            PresentationSource.FromVisual(_viewport) != null)
        {
            try
            {
                var local = _viewport.PointFromScreen(new Point(pt.X, pt.Y));
                localX = (int)local.X;
                localY = (int)local.Y;
            }
            catch { /* visual pas encore dans l'arbre */ }
        }

        // Boutons via GetAsyncKeyState — thread-independant (bit 15 = touche enfoncee).
        var left    = (GetAsyncKeyState(VK_LBUTTON)  & 0x8000) != 0
                          ? ButtonState.Pressed : ButtonState.Released;
        var right   = (GetAsyncKeyState(VK_RBUTTON)  & 0x8000) != 0
                          ? ButtonState.Pressed : ButtonState.Released;
        var middle  = (GetAsyncKeyState(VK_MBUTTON)  & 0x8000) != 0
                          ? ButtonState.Pressed : ButtonState.Released;
        var xb1     = (GetAsyncKeyState(VK_XBUTTON1) & 0x8000) != 0
                          ? ButtonState.Pressed : ButtonState.Released;
        var xb2     = (GetAsyncKeyState(VK_XBUTTON2) & 0x8000) != 0
                          ? ButtonState.Pressed : ButtonState.Released;

        // Log uniquement lors d'un changement d'etat du bouton droit (orbite camera).
        bool rightDown = right == ButtonState.Pressed;
        if (rightDown != _lastRight)
        {
            _lastRight = rightDown;
            Logs.WriteDebug($"[InputDiag] RawMouseProvider RightButton={right} pos=({localX},{localY})");
        }

        return new MouseState(localX, localY, 0, left, middle, right, xb1, xb2);
    }
}

#endif
