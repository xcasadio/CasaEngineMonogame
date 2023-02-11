using CasaEngine.World;

#if EDITOR

#endif


namespace CasaEngine.Game
{
    public class GameInfo
    {

        static readonly private GameInfo GameInfo = new GameInfo();

        private readonly WorldInfo _worldInfo;



        static public GameInfo Instance => GameInfo;


        public WorldInfo WorldInfo => _worldInfo;


        public int MaximumPlayers
        {
            get;
            set;
        }

        public int MaximumLocalPlayers
        {
            get;
            set;
        }




        public GameInfo()
        {
            _worldInfo = new WorldInfo();
        }



    }
}
