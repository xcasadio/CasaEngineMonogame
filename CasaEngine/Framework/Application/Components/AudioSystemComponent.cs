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
        Service = new AudioService(backend ?? CreateSelectedBackend(game));
        if (backend != null)
        {
            Logs.WriteInfo($"Audio backend: {backend.GetType().Name} (source: injected)");
        }

        if (game is CasaEngineGame casaEngineGame)
        {
            Service.ClipProvider = new AssetContentManagerAudioClipProvider(casaEngineGame.AssetContentManager);

            // Not applied here: the asset loaders are not registered yet. CasaEngineGame.Initialize calls
            // ApplyProjectMixerAsset right after registering them.
            ProjectMixer = new ProjectAudioMixer(Service, casaEngineGame.AssetContentManager);

            // ADR-0040: the project settings are already loaded at this point (Initialize loads
            // them before creating this component), so this applies the project's mute from the
            // very first frame.
            ProjectAudioSettings.Apply(Service, casaEngineGame.RuntimeContext.ProjectSettings);
        }

        UpdateOrder = (int)ComponentUpdateOrder.Audio;
        game.Components.Add(this);
    }

    /// <summary>Playback API: buses, voices, ownership.</summary>
    public AudioService Service { get; }

    /// <summary>
    /// Applies the project's <c>.audioMixer</c> asset (<see cref="Configuration.Project.ProjectSettings.AudioMixerAsset"/>).
    /// Null when the game is not a <see cref="CasaEngineGame"/>.
    /// </summary>
    public ProjectAudioMixer ProjectMixer { get; }

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

    /// <summary>
    /// Mute of the <see cref="AudioBusNames.Master"/> bus (ADR-0040). The setter mirrors the value
    /// into the project settings (through <see cref="ProjectAudioSettings.SetMuted"/>) so it
    /// survives an editor save; it never writes the project file.
    /// </summary>
    public bool IsMuted
    {
        get => Mixer.GetBus(AudioBusNames.Master).IsMuted;
        set
        {
            if (Game is CasaEngineGame casaEngineGame)
            {
                ProjectAudioSettings.SetMuted(Mixer, value, casaEngineGame.RuntimeContext.ProjectSettings, GameSettings.ProjectSettings);
            }
            else
            {
                Mixer.GetBus(AudioBusNames.Master).IsMuted = value;
            }
        }
    }

    /// <summary>
    /// Applies the mixer asset named by the game's project settings. Call it once the asset loaders are registered;
    /// does nothing outside a <see cref="CasaEngineGame"/>. Never throws.
    /// </summary>
    public void ApplyProjectMixerAsset()
    {
        if (ProjectMixer != null && Game is CasaEngineGame casaEngineGame)
        {
            ProjectMixer.Apply(casaEngineGame.RuntimeContext.ProjectSettings);
        }
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

                // ADR-0037: gives back the clip handles the provider held for the whole game.
                (Service.ClipProvider as IDisposable)?.Dispose();
            }
        }
        finally
        {
            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// Picks the backend (environment variable, then project setting, then default), creates it and
    /// logs the outcome once. The environment variable is read here, once, at startup.
    /// </summary>
    private static IAudioBackend CreateSelectedBackend(Game game)
    {
        string environmentValue = Environment.GetEnvironmentVariable(AudioBackendSelection.EnvironmentVariableName);
        AudioBackendKind? projectSetting = game is CasaEngineGame casaEngineGame
            ? casaEngineGame.RuntimeContext.ProjectSettings.AudioBackend
            : null;

        var selection = AudioBackendSelection.Resolve(environmentValue, projectSetting);
        if (selection.Warning != null)
        {
            Logs.WriteWarning(selection.Warning);
        }

        bool fellBack = false;
        IAudioBackend backend = null;
        if (selection.Kind == AudioBackendKind.Software)
        {
            backend = TryCreateSoftwareBackend();
            fellBack = backend == null;
        }

        backend ??= CreateMonoGameBackend();

        Logs.WriteInfo($"Audio backend: {backend.GetType().Name} (source: {selection.Source.ToString().ToLowerInvariant()})"
            + (fellBack ? " (fallback from Software)" : string.Empty));
        return backend;
    }

    private static IAudioBackend TryCreateSoftwareBackend()
    {
        SoftwareAudioBackend software = null;
        try
        {
            software = new SoftwareAudioBackend();
            if (software.IsAvailable)
            {
                return software;
            }

            Logs.WriteWarning("The software audio backend has no output device, falling back to the MonoGame backend.");
        }
        catch (Exception exception)
        {
            Logs.WriteException(new Exception("The software audio backend could not be created, falling back to the MonoGame backend.", exception));
        }

        try
        {
            software?.Dispose();
        }
        catch (Exception exception)
        {
            Logs.WriteException(new Exception("The unavailable software audio backend could not be disposed.", exception));
        }

        return null;
    }

    private static IAudioBackend CreateMonoGameBackend()
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
