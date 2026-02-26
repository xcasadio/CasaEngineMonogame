#if EDITOR

using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
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

    private readonly IInputElement _hoverElement;

    /// <param name="hoverElement">
    /// Élément WPF utilisé pour vérifier <c>IsMouseDirectlyOver</c>.
    /// Les touches ne sont reportées que lorsque la souris est sur cet élément,
    /// évitant de recevoir des raccourcis qui ne sont pas destinés au viewport.
    /// </param>
    public RawKeyboardProvider(IInputElement hoverElement)
    {
        _hoverElement = hoverElement;
    }

    public KeyboardState GetState()
    {
        // Ne rapporte les touches que si la souris est sur le viewport.
        if (!_hoverElement.IsMouseDirectlyOver)
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
