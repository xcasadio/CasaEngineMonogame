namespace CasaEngine.Framework.Audio;

/// <summary>What happens to a voice once its volume ramp reaches its target.</summary>
public enum AudioFadeCompletion
{
    /// <summary>The voice keeps playing at the target volume.</summary>
    None,

    /// <summary>The voice is stopped and released. This is how a fade out ends.</summary>
    Stop,
}
