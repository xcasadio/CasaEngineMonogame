using System.Xml;
using CasaEngine.Core.Design;
using CasaEngine.Framework.Audio;
using Microsoft.Xna.Framework.Audio;

namespace CasaEngine.Framework.Gameplay.Actor.Event
{

    public
#if EDITOR
    partial
#endif
    class PlaySoundEvent
        : EventActor
    {

        private string _assetName;
        private Sound _sound;



        public string AssetName
        {
            get { return _assetName; }
#if EDITOR
            set { _assetName = value; }
#endif
        }

        public Sound Sound => _sound;


        public PlaySoundEvent(string assetName)
            : base(EventActorType.PlaySound)
        {

        }

        public PlaySoundEvent(XmlElement el, SaveOption option)
            : base(el, option)
        {

        }



        public override void Initialize()
        {
            _sound = new Sound(Framework.Game.Engine.Instance.Game.Content.Load<SoundEffect>("Content/" + _assetName));
            _sound.Initialize();
        }

        public override void Do()
        {
            _sound.SoundEffectInstance.Play();
        }

        public override string ToString()
        {
            return "Play Sound '" + _assetName + "'";
        }

        public override void Load(XmlElement el, SaveOption option)
        {
            base.Load(el, option);
            _assetName = el.Attributes["asset"].Value;
        }

    }
}
