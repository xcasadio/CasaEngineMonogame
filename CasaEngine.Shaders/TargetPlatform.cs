﻿namespace CasaEngine.Shaders;

// source copy and modified from MonoGame
// MonoGame - Copyright (C) The MonoGame Team
// This file is subject to the terms and conditions defined in
// file 'Licences\MonoGame.txt', which is part of this source code package.
public enum TargetPlatform
{
    /// <summary>
    /// All desktop versions of Windows using DirectX.
    /// </summary>
    Windows,

    /// <summary>
    /// Xbox 360 video game and entertainment system
    /// </summary>
    Xbox360,

    // MonoGame-specific platforms listed below

    /// <summary>
    /// Apple iOS-based devices (iPod Touch, iPhone, iPad)
    /// (MonoGame)
    /// </summary>
    iOS,

    /// <summary>
    /// Android-based devices
    /// (MonoGame)
    /// </summary>
    Android,

    /// <summary>
    /// All desktop versions using OpenGL.
    /// (MonoGame)
    /// </summary>
    DesktopGL,

    /// <summary>
    /// Apple Mac OSX-based devices (iMac, MacBook, MacBook Air, etc)
    /// (MonoGame)
    /// </summary>
    MacOSX,

    /// <summary>
    /// Windows Store App
    /// (MonoGame)
    /// </summary>
    WindowsStoreApp,

    /// <summary>
    /// Google Chrome Native Client
    /// (MonoGame)
    /// </summary>
    NativeClient,

    /// <summary>
    /// Windows Phone 8
    /// (MonoGame)
    /// </summary>
    WindowsPhone8,

    /// <summary>
    /// Raspberry Pi
    /// (MonoGame)
    /// </summary>
    RaspberryPi,

    /// <summary>
    /// Sony PlayStation4
    /// </summary>
    PlayStation4,

    /// <summary>
    /// Sony PlayStation5
    /// </summary>
    PlayStation5,

    /// <summary>
    /// Xbox One
    /// </summary>
    XboxOne,

    /// <summary>
    /// Nintendo Switch
    /// </summary>
    Switch,

    /// <summary>
    /// Google Stadia
    /// </summary>
    Stadia,

    /// <summary>
    /// WebAssembly and Bridge.NET
    /// </summary>
    Web
}