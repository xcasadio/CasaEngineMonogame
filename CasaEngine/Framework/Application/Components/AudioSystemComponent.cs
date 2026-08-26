using CasaEngine.Core.Logging;
using CasaEngine.Framework.Audio;
using CasaEngine.Framework.Audio.Backends;
using CasaEngine.Framework.Audio.Mixing;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Application.Components;

/// <summary>
/// Drives the engine audio from the game loop.
/// </summary>
/// <remarks>
/// All the logic lives in <see cref="Audio.AudioService"/>, which knows nothing about MonoGame:
/// this component only owns the backend lifetime and forwards Update. The device and the buses
/// are global to the process (a single OpenAL device), which is why this is a GameComponent and
/// not a per-world system; voices are scoped through their owner instead.
/// Failing to open the audio device never prevents the game from running: every playback call
/// then becomes a silent no-op.
/// </remarks>
public class AudioSystemComponent : GameComponent
{
    public AudioSystemComponent(Game game, IAudioBackend backend = null)
        : base(game)
    {
        Service = new AudioService(backend ?? CreateDefaultBackend());

        if (game is CasaEngineGame casaEngineGame)
        {
            Service.ClipProvider = new AssetContentManagerAudioClipProvider(casaEngineGame.AssetContentManager);
        }

        UpdateOrder = (int)ComponentUpdateOrder.Audio;
        game.Components.Add(this);
    }

    /// <summary>Playback API: buses, voices, ownership.</summary>
    public AudioService Service { get; }

    /// <summary>The mixing bus tree. Volumes and mutes are set through it.</summary>
    public AudioMixer Mixer => Service.Mixer;

    /// <summary>False when no audio device could be opened; playback is then a silent no-op.</summary>
    public bool IsAudioAvailable => Service.IsAudioAvailable;

    /// <summary>Volume of the master bus, in [0,1].</summary>
    public float MasterVolume
    {
        get => Mixer.GetBus(AudioBusNames.Master).Volume;
        set => Mixer.GetBus(AudioBusNames.Master).Volume = value;
    }

    /// <summary>Stops every voice, whoever owns it.</summary>
    public void StopAll()
    {
        Service.StopAll();
    }

    public override void Update(GameTime gameTime)
    {
        Service.Update((float)gameTime.ElapsedGameTime.TotalSeconds);
        base.Update(gameTime);
    }

    protected override void Dispose(bool disposing)
    {
        try
        {
            if (disposing)
            {
                Service.Dispose();
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
            // Creating the backend must never take the game down; only the sound is lost.
            Logs.WriteException(new Exception("Audio backend could not be created, the game runs without sound.", exception));
            return new NullAudioBackend();
        }
    }
}
