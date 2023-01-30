using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework;

namespace CasaEngine.Audio
{
    public class AudioComponent
        : Microsoft.Xna.Framework.GameComponent
    {

        // The listener describes the ear which is hearing 3D sounds.
        // This is usually set to match the camera.
        public AudioListener Listener => listener;

        readonly AudioListener listener = new AudioListener();


        // The emitter describes an entity which is making a 3D sound.
        readonly AudioEmitter emitter = new AudioEmitter();


        // Store all the sound effects that are available to be played.
        readonly Dictionary<string, SoundEffect> soundEffects = new Dictionary<string, SoundEffect>();


        // Keep track of all the 3D sounds that are currently playing.
        readonly List<ActiveSound> activeSounds = new List<ActiveSound>();



        public AudioComponent(Microsoft.Xna.Framework.Game game)
            : base(game)
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

        public void AddSound(string soundName_)
        {
            soundEffects.Add(soundName_, Game.Content.Load<SoundEffect>(soundName_));
        }

        public void Clear()
        {
            foreach (SoundEffect soundEffect in soundEffects.Values)
            {
                soundEffect.Dispose();
            }

            soundEffects.Clear();
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
            int index = 0;

            while (index < activeSounds.Count)
            {
                ActiveSound activeSound = activeSounds[index];

                if (activeSound.Instance.State == SoundState.Stopped)
                {
                    // If the sound has stopped playing, dispose it.
                    activeSound.Instance.Dispose();

                    // Remove it from the active list.
                    activeSounds.RemoveAt(index);
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
            ActiveSound activeSound = new ActiveSound();

            // Fill in the instance and emitter fields.
            activeSound.Instance = soundEffects[soundName].CreateInstance();
            activeSound.Instance.IsLooped = isLooped;

            activeSound.Emitter = emitter;

            // Set the 3D position of this sound, and then play it.
            Apply3D(activeSound);

            activeSound.Instance.Play();

            // Remember that this sound is now active.
            activeSounds.Add(activeSound);

            return activeSound.Instance;
        }


        private void Apply3D(ActiveSound activeSound)
        {
            emitter.Position = activeSound.Emitter.Position;
            emitter.Forward = activeSound.Emitter.Forward;
            emitter.Up = activeSound.Emitter.Up;
            emitter.Velocity = activeSound.Emitter.Velocity;

            activeSound.Instance.Apply3D(listener, emitter);
        }


        private class ActiveSound
        {
            public SoundEffectInstance Instance;
            public IAudioEmitter Emitter;
        }
    }
}
