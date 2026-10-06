namespace CasaEngine.Framework.Audio.Spatial;

/// <summary>How a sound is positioned relative to the audio listener.</summary>
public enum AudioSpatialMode
{
    /// <summary>The sound is not spatialized.</summary>
    None = 0,

    /// <summary>The sound is spatialized on the X/Y plane; the Z coordinate is ignored.</summary>
    Spatial2D = 1,

    /// <summary>The sound is spatialized in three dimensions.</summary>
    Spatial3D = 2
}
