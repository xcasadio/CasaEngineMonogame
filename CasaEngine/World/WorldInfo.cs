using Microsoft.Xna.Framework;
using CasaEngine.Gameplay;
using CasaEngine.Gameplay.Actor;

namespace CasaEngine.World
{
    public class WorldInfo
    {

        private World _world = null;
        //private TimeSpan _StartTime;
        //private PhysicsSettings _PhysicsSettings = new PhysicsSettings();
        private Vector2 _worldGravity = Vector2.Zero;

        //private List<TeamInfo> _TeamInfoList = new List<TeamInfo>();

        public int BotKilled;
        public int Score;



        public World World
        {
            get => _world;
            set => _world = value;
        }

        /*public PhysicsSettings PhysicsSettings
        {
            get { return _PhysicsSettings; }
        }*/

        public Vector2 WorldGravity
        {
            get => _worldGravity;
            set => _worldGravity = value;
        }

        public float ElapsedTime
        {
            get;
            internal set;
        }





        internal void Update(float elapsedTime)
        {
            ElapsedTime += elapsedTime;
        }

        public List<T> GetActors<T>()
        {
            List<T> res = new List<T>();

            return res;
        }

        public List<CharacterActor2D> GetPlayers()
        {
            List<CharacterActor2D> res = new List<CharacterActor2D>();

            foreach (Actor2D a in _world.Actors)
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
