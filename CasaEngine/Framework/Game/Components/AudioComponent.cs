using CasaEngine.Framework.Audio;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;

namespace CasaEngine.Framework.Game.Components;

public class AudioComponent : GameComponent
{
    // The listener describes the ear which is hearing 3D sounds.
    // This is usually set to match the camera.
    public AudioListener Listener => _listener;

    private readonly AudioListener _listener = new();

    // The emitter describes an entity which is making a 3D sound.
    private readonly AudioEmitter _emitter = new();

    // Store all the sound effects that are available to be played.
    private readonly Dictionary<string, SoundEffect> _soundEffects = new();

    // Keep track of all the 3D sounds that are currently playing.
    private readonly List<ActiveSound> _activeSounds = new();

    public AudioComponent(Microsoft.Xna.Framework.Game game) : base(game)
    { }

    public override void Initialize()
    {
        // Set the scale for 3D audio so it matches the scale of our game world.
        // DistanceScale controls how much sounds change volume as you move further away.
        // DopplerScale controls how much sounds change pitch as you move past them.
        SoundEffect.DistanceScale = 2000;
        SoundEffect.DopplerScale = 0.1f;

        // Load all the sound effects.
        /*foreach (string soundName in soundNames)
        {
            soundEffects.Add(soundName, Game.Content.Load<SoundEffect>(soundName));
        }*/

        base.Initialize();
    }

    public void AddSound(string soundName)
    {
        _soundEffects.Add(soundName, Game.Content.Load<SoundEffect>(soundName));
    }

    public void Clear()
    {
        foreach (var soundEffect in _soundEffects.Values)
        {
            soundEffect.Dispose();
        }

        _soundEffects.Clear();
    }

    protected override void Dispose(bool disposing)
    {
        try
        {
            if (disposing)
            {
                Clear();
            }
        }
        finally
        {
            base.Dispose(disposing);
        }
    }

    public override void Update(GameTime gameTime)
    {
        // Loop over all the currently playing 3D sounds.
        var index = 0;

        while (index < _activeSounds.Count)
        {
            var activeSound = _activeSounds[index];

            if (activeSound.Instance.State == SoundState.Stopped)
            {
                // If the sound has stopped playing, dispose it.
                activeSound.Instance.Dispose();

                // Removed it from the active list.
                _activeSounds.RemoveAt(index);
            }
            else
            {
                // If the sound is still playing, update its 3D settings.
                Apply3D(activeSound);

                index++;
            }
        }

        base.Update(gameTime);
    }

    public SoundEffectInstance Play3DSound(string soundName, bool isLooped, IAudioEmitter emitter)
    {
        var activeSound = new ActiveSound();

        // Fill in the instance and emitter fields.
        activeSound.Instance = _soundEffects[soundName].CreateInstance();
        activeSound.Instance.IsLooped = isLooped;

        activeSound.Emitter = emitter;

        // Set the 3D position of this sound, and then play it.
        Apply3D(activeSound);

        activeSound.Instance.Play();

        // Remember that this sound is now active.
        _activeSounds.Add(activeSound);

        return activeSound.Instance;
    }

    private void Apply3D(ActiveSound activeSound)
    {
        _emitter.Position = activeSound.Emitter.Position;
        _emitter.Forward = activeSound.Emitter.Forward;
        _emitter.Up = activeSound.Emitter.Up;
        _emitter.Velocity = activeSound.Emitter.Velocity;

        activeSound.Instance.Apply3D(_listener, _emitter);
    }

    private class ActiveSound
    {
        public SoundEffectInstance Instance;
        public IAudioEmitter Emitter;
    }
}