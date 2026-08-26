using CasaEngine.Core.Logging;
using CasaEngine.Framework.Audio;
using CasaEngine.Framework.Audio.Backends;
using CasaEngine.Framework.Audio.Mixing;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Application.Components;

/// <summary>
/// Single entry point of the engine audio: it owns the audio backend and the mixing buses.
/// </summary>
/// <remarks>
/// The device and the buses are global to the process (there is only one OpenAL device), which
/// is why this is a <see cref="GameComponent"/> and not a per-world system. Voices, on the other
/// hand, are scoped to the world that started them.
/// A failure to open the audio device never prevents the game from running: the component stays
/// alive and every playback call becomes a silent no-op.
/// </remarks>
public class AudioSystemComponent : GameComponent
{
    private readonly IAudioBackend _backend;

    public AudioSystemComponent(Game game, IAudioBackend backend = null)
        : base(game)
    {
        _backend = backend ?? CreateDefaultBackend();
        Mixer = AudioBusNames.CreateDefaultMixer();

        UpdateOrder = (int)ComponentUpdateOrder.Audio;
        game.Components.Add(this);
    }

    /// <summary>The mixing bus tree. Volumes and mutes are set through it.</summary>
    public AudioMixer Mixer { get; }

    /// <summary>False when no audio device could be opened; playback is then a silent no-op.</summary>
    public bool IsAudioAvailable => _backend.IsAvailable;

    /// <summary>Backend in use. Exposed for the systems that build voices on top of it.</summary>
    public IAudioBackend Backend => _backend;

    /// <summary>Volume of the master bus, in [0,1].</summary>
    public float MasterVolume
    {
        get => Mixer.GetBus(AudioBusNames.Master).Volume;
        set => Mixer.GetBus(AudioBusNames.Master).Volume = value;
    }

    /// <summary>Stops every voice, whoever owns it.</summary>
    public virtual void StopAll()
    {
        _backend.StopAll();
    }

    protected override void Dispose(bool disposing)
    {
        try
        {
            if (disposing)
            {
                _backend.Dispose();
            }
        }
        finally
        {
            base.Dispose(disposing);
        }
    }

    private static IAudioBackend CreateDefaultBackend()
    {
        try
        {
            return new MonoGameAudioBackend();
        }
        catch (Exception exception)
        {
            // Creating the backend must never take the game down; the audio is simply lost.
            Logs.WriteException(new Exception("Audio backend could not be created, the game runs without sound.", exception));
            return new NullAudioBackend();
        }
    }
}
