using System;


using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework.Audio;
using CasaEngine.Audio;
using System.Xml;
using CasaEngineCommon.Extension;
using CasaEngine.Game;
using CasaEngineCommon.Design;

namespace CasaEngine.Gameplay.Actor.Event
{

    /// <summary>
    /// 
    /// </summary>
    public
#if EDITOR
    partial
#endif
    class PlaySoundEvent
        : EventActor
    {

        private string m_AssetName;
        private Sound m_Sound;



        /// <summary>
        /// Gets
        /// </summary>
        public string AssetName
        {
            get { return m_AssetName; }
#if EDITOR
            set { m_AssetName = value; }
#endif
        }

        /// <summary>
        /// Gets
        /// </summary>
        public Sound Sound
        {
            get { return m_Sound; }
        }



        /// <summary>
        /// 
        /// </summary>
        /// <param name="assetName_"></param>
        public PlaySoundEvent(string assetName_)
            : base(EventActorType.PlaySound)
        {

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="assetName_"></param>
        public PlaySoundEvent(XmlElement el_, SaveOption option_)
            : base(el_, option_)
        {

        }



        /// <summary>
        /// 
        /// </summary>
        public override void Initialize()
        {
            m_Sound = new Sound(Engine.Instance.Game.Content.Load<SoundEffect>("Content/" + m_AssetName));
            m_Sound.Initialize();
        }

        /// <summary>
        /// 
        /// </summary>
        public override void Do()
        {
            m_Sound.SoundEffectInstance.Play();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return "Play Sound '" + m_AssetName + "'";
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_"></param>
        /// <param name="option_"></param>
        public override void Load(XmlElement el_, SaveOption option_)
        {
            base.Load(el_, option_);
            m_AssetName = el_.Attributes["asset"].Value;
        }

    }
}
