namespace CasaEngine.Framework.Audio.Spatial;

/// <summary>The voice property driven by a game parameter binding.</summary>
public enum AudioParameterTarget
{
    /// <summary>A volume factor in [0, 1].</summary>
    Volume = 0,

    /// <summary>A pitch offset in octaves, in [-1, 1].</summary>
    Pitch = 1
}
