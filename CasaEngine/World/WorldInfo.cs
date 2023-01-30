using Microsoft.Xna.Framework;
using CasaEngine.Gameplay;
using CasaEngine.Gameplay.Actor;

namespace CasaEngine.World
{
    public class WorldInfo
    {

        private World m_World = null;
        //private TimeSpan m_StartTime;
        //private PhysicsSettings m_PhysicsSettings = new PhysicsSettings();
        private Vector2 m_WorldGravity = Vector2.Zero;

        //private List<TeamInfo> m_TeamInfoList = new List<TeamInfo>();

        public int BotKilled;
        public int Score;



        public World World
        {
            get => m_World;
            set => m_World = value;
        }

        /*public PhysicsSettings PhysicsSettings
        {
            get { return m_PhysicsSettings; }
        }*/

        public Vector2 WorldGravity
        {
            get => m_WorldGravity;
            set => m_WorldGravity = value;
        }

        public float ElapsedTime
        {
            get;
            internal set;
        }





        internal void Update(float elapsedTime_)
        {
            ElapsedTime += elapsedTime_;
        }

        public List<T> GetActors<T>()
        {
            List<T> res = new List<T>();

            return res;
        }

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
