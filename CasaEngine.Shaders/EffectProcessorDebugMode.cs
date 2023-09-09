namespace CasaEngine.Shaders;

// source copy and modified from MonoGame
// MonoGame - Copyright (C) The MonoGame Team
// This file is subject to the terms and conditions defined in
// file 'Licences\MonoGame.txt', which is part of this source code package.

/// <summary>
/// Specifies how debugging of effects is to be supported in PIX.
/// </summary>
public enum EffectProcessorDebugMode
{
    /// <summary>
    /// Enables effect debugging when built with Debug profile.
    /// </summary>
    Auto = 0,

    /// <summary>
    /// Enables effect debugging for all profiles. Will produce unoptimized shaders.
    /// </summary>
    Debug = 1,

    /// <summary>
    /// Disables debugging for all profiles, produce optimized shaders.
    /// </summary>
    Optimize = 2,
}