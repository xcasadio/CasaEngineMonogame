namespace CasaEngine.Framework.Audio;

/// <summary>Playback state of a voice, independent from the underlying audio backend.</summary>
public enum AudioVoiceState
{
    /// <summary>The voice does not exist, has finished, or was stopped.</summary>
    Stopped,

    Playing,

    Paused,
}
