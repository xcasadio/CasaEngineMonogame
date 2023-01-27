using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Xml;
using CasaEngine;
using CasaEngine.Design.Event;
using CasaEngine.Game;
using CasaEngineCommon.Pool;
using CasaEngine.Gameplay.Actor.Event;

namespace CasaEngine.Assets.Graphics2D
{
	/// <summary>
	/// 
	/// </summary>
	public struct Frame2D
	{
		public int spriteID;
		public float time;

#if EDITOR
        public List<EventActor> Events;
#else
        public EventActor[] Events;
#endif

        /// <summary>
        /// 
        /// </summary>
        /// <param name="spriteID_"></param>
        /// <param name="time_"></param>
        /// <param name="eventCount_"></param>
        public Frame2D(int spriteID_, float time_)
        {
            spriteID = spriteID_;
            time = time_;

#if EDITOR
            Events = new List<EventActor>();
#else
            Events = null;
#endif
        }

#if EDITOR

        /// <summary>
        /// 
        /// </summary>
        public string EventsToString()
        {
            if (Events == null)
            {
                return "0 event";
            }

            return Events.Count + " event" + (Events.Count <= 1 ? "" : "s");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="other_"></param>
        /// <returns></returns>
        public bool CompareTo(Frame2D other_)
        {
            if (spriteID != other_.spriteID
                || time != other_.time
                || Events.Count != other_.Events.Count)
            {
                return false;
            }

            for (int i = 0; i < Events.Count; i++)
            {
                if (Events[i].CompareTo(other_.Events[i]) == false)
                {
                    return false;
                }
            }

            return true;
        }
#endif
	}
}
