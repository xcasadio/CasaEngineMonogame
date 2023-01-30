using CasaEngine.World;

#if EDITOR

#endif


namespace CasaEngine.Game
{
    public class GameInfo
    {

        static readonly private GameInfo m_GameInfo = new GameInfo();

        private readonly WorldInfo m_WorldInfo;



        static public GameInfo Instance => m_GameInfo;


        public WorldInfo WorldInfo => m_WorldInfo;


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
            m_WorldInfo = new WorldInfo();
        }



    }
}
