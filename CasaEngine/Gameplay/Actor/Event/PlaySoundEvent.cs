using Microsoft.Xna.Framework.Audio;
using CasaEngine.Audio;
using System.Xml;
using CasaEngine.Game;
using CasaEngineCommon.Design;

namespace CasaEngine.Gameplay.Actor.Event
{

    public
#if EDITOR
    partial
#endif
    class PlaySoundEvent
        : EventActor
    {

        private string m_AssetName;
        private Sound m_Sound;



        public string AssetName
        {
            get { return m_AssetName; }
#if EDITOR
            set { m_AssetName = value; }
#endif
        }

        public Sound Sound => m_Sound;


        public PlaySoundEvent(string assetName_)
            : base(EventActorType.PlaySound)
        {

        }

        public PlaySoundEvent(XmlElement el_, SaveOption option_)
            : base(el_, option_)
        {

        }



        public override void Initialize()
        {
            m_Sound = new Sound(Engine.Instance.Game.Content.Load<SoundEffect>("Content/" + m_AssetName));
            m_Sound.Initialize();
        }

        public override void Do()
        {
            m_Sound.SoundEffectInstance.Play();
        }

        public override string ToString()
        {
            return "Play Sound '" + m_AssetName + "'";
        }

        public override void Load(XmlElement el_, SaveOption option_)
        {
            base.Load(el_, option_);
            m_AssetName = el_.Attributes["asset"].Value;
        }

    }
}
