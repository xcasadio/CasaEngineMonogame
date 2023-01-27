using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CasaEngine.Design.Event;
using System.Xml;
using CasaEngineCommon.Extension;
using CasaEngineCommon.Design;

namespace CasaEngine.Gameplay.Actor.Event
{
    /// <summary>
    /// 
    /// </summary>
    public static class EventActorFactory
    {
        #region Fields

        #endregion

        #region Properties

        #endregion

        #region Constructors

        #endregion

        #region Methods

        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_"></param>
        /// <param name="option_"></param>
        /// <returns></returns>
        static public EventActor LoadEvent(XmlElement el_, SaveOption option_)
        {
            EventActorType type  = (EventActorType)Enum.Parse(typeof(EventActorType), el_.Attributes["type"].Value);

            switch (type)
            {
                case EventActorType.PlaySound:
                    return new PlaySoundEvent(el_, option_);

                case EventActorType.SpawnActor:
                    throw new NotImplementedException();
            }

            return null;
        }

        #endregion
    }
}
