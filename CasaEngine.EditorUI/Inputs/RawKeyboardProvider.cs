#if EDITOR

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using CasaEngine.Engine.Input.InputDeviceStateProviders;
using Microsoft.Xna.Framework.Input;

namespace CasaEngine.EditorUI.Inputs;

/// <summary>
/// Lit l'état du clavier via l'API Win32 <c>GetKeyboardState</c> directement,
/// sans exiger que l'élément WPF ait le focus clavier (<c>IsKeyboardFocused</c>).
///
/// Dans l'ancienne architecture multi-onglets, chaque onglet était son propre
/// <c>WpfGame</c> (contrôle visible ET hôte du game loop) — le focus WPF était
/// garanti. Dans la nouvelle architecture, <see cref="EngineHost"/> est un contrôle
/// caché 1×2 et <see cref="ViewportControl"/> est l'élément visible.
/// <c>WpfKeyboard</c> exige <c>IsKeyboardFocused == true</c> sur le focus element
/// pour retourner des touches, ce qui n'est pas fiable dans ce contexte.
///
/// Ce provider retourne les touches physiquement enfoncées dès que la souris
/// survole le viewport, sans avoir besoin du focus WPF.
/// </summary>
internal class RawKeyboardProvider : IKeyboardStateProvider
{
    [DllImport("user32.dll", EntryPoint = "GetKeyboardState", SetLastError = true)]
    private static extern bool NativeGetKeyboardState([Out] byte[] keyStates);

    private readonly Func<bool> _isMouseOver;

    /// <param name="isMouseOver">
    /// Délégué retournant <c>true</c> lorsque la souris est sur le viewport.
    /// Alimenté par les events WPF <c>MouseEnter</c>/<c>MouseLeave</c> du
    /// <see cref="ViewportControl"/> — plus fiable que <c>IsMouseDirectlyOver</c>
    /// sur un contrôle Image/D3D11 dont le hit-testing WPF peut être court-circuité.
    /// </param>
    public RawKeyboardProvider(Func<bool> isMouseOver)
    {
        _isMouseOver = isMouseOver;
    }

    public KeyboardState GetState()
    {
        if (!_isMouseOver())
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
