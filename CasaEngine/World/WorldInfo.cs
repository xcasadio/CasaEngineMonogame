using System;


using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using CasaEngine.Physics2D;
using Microsoft.Xna.Framework;
using CasaEngine.Gameplay;
using CasaEngine.Gameplay.Actor;

namespace CasaEngine.World
{
    /// <summary>
    /// 
    /// </summary>
    public class WorldInfo
    {

        private World m_World = null;
        //private TimeSpan m_StartTime;
        //private PhysicsSettings m_PhysicsSettings = new PhysicsSettings();
        private Vector2 m_WorldGravity = Vector2.Zero;

        //private List<TeamInfo> m_TeamInfoList = new List<TeamInfo>();

        public int BotKilled;
        public int Score;



        /// <summary>
        /// Gets/Sets
        /// </summary>
        public World World
        {
            get { return m_World; }
            set { m_World = value; }
        }

        /// <summary>
        /// 
        /// </summary>
        /*public PhysicsSettings PhysicsSettings
        {
            get { return m_PhysicsSettings; }
        }*/

        /// <summary>
        /// Gets/Sets
        /// </summary>
        public Vector2 WorldGravity
        {
            get { return m_WorldGravity; }
            set { m_WorldGravity = value; }
        }

        /// <summary>
        /// Gets
        /// </summary>
        public float ElapsedTime
        {
            get;
            internal set;
        }





        /// <summary>
        /// 
        /// </summary>
        /// <param name="elapsedTime_"></param>
        internal void Update(float elapsedTime_)
        {
            ElapsedTime += elapsedTime_;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public List<T> GetActors<T>()
        {
            List<T> res = new List<T>();

            return res;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public List<CharacterActor2D> GetPlayers()
        {
            List<CharacterActor2D> res = new List<CharacterActor2D>();

            foreach (Actor2D a in m_World.Actors)
            {
                if (a is CharacterActor2D)
                {
                    CharacterActor2D c = (CharacterActor2D)a;
                    if (c.IsPLayer == true)
                    {
                        res.Add(c);
                    }
                }
            }

            return res;
        }

    }
}
