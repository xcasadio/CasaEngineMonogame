namespace CasaEngine.Gameplay
{
    public class GlobalVar
    {

        private static GlobalVar instance;
        private Dictionary<string, int> m_Vars;



        public static GlobalVar Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new GlobalVar();
                }
                return instance;
            }
        }

        public Dictionary<string, int> Vars
        {
            get => m_Vars;
            set => m_Vars = value;
        }



        private GlobalVar()
        {
            m_Vars = new Dictionary<string, int>();
        }



    }
}
